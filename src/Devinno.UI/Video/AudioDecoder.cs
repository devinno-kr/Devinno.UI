using FFmpeg.AutoGen;

namespace Devinno.UI.Video;

// 디코드된 소리(48000 Hz 스테레오 float32 interleaved)와 그 첫 샘플의 시각(초)
internal delegate void PcmHandler(ReadOnlySpan<float> samples, double startPts);

// 파일의 오디오 스트림 하나를 디코드해 48000 Hz·스테레오·float32 interleaved 로 리샘플한다.
// 한 스레드만 쓴다(패킷을 넣는 쪽). 결과는 Pcm 으로 내보낸다 — 넘긴 span 은 호출 동안만 유효
internal sealed unsafe class AudioDecoder : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 2;

    private AVCodecContext* _codec;
    private AVFrame* _frame;
    private SwrContext* _swr;
    private AVChannelLayout _swrInLayout;
    private int _swrInRate;
    private AVSampleFormat _swrInFormat = AVSampleFormat.AV_SAMPLE_FMT_NONE;
    private readonly double _timeBase;
    private double _nextPts = double.NaN; // 시각이 없는 프레임용 — 앞 프레임의 끝
    private float[] _buffer = [];

    private AudioDecoder(AVCodecContext* codec, int stream, double timeBase)
    {
        _codec = codec;
        Stream = stream;
        _timeBase = timeBase;
        _frame = ffmpeg.av_frame_alloc();
    }

    // 디코드할 스트림 번호
    public int Stream { get; }

    // 디코드한 소리를 받는 곳(디코드 스레드에서 불린다)
    public PcmHandler? Pcm { get; set; }

    // fmt 의 대표 오디오 스트림(비디오와 짝인 것)을 연다. 없거나 못 열면 null(이유는 error, 스트림이 아예 없으면 error 도 null)
    public static AudioDecoder? Open(AVFormatContext* fmt, int relatedStream, out string? error)
    {
        error = null;
        AVCodec* decoder = null;
        var index = ffmpeg.av_find_best_stream(fmt, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, relatedStream, &decoder, 0);
        if (index == ffmpeg.AVERROR_STREAM_NOT_FOUND)
            return null;
        if (index < 0 || decoder is null)
        {
            error = "no decoder for the audio stream";
            return null;
        }

        try
        {
            _ = ffmpeg.swresample_version(); // swresample 이 없으면 여기서 — 소리만 빼고 재생
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or NotSupportedException)
        {
            error = "FFmpeg swresample library not found";
            return null;
        }

        var stream = fmt->streams[index];
        var codec = ffmpeg.avcodec_alloc_context3(decoder);
        ffmpeg.avcodec_parameters_to_context(codec, stream->codecpar);
        if (ffmpeg.avcodec_open2(codec, decoder, null) < 0)
        {
            ffmpeg.avcodec_free_context(&codec);
            error = "cannot open the audio decoder";
            return null;
        }
        return new AudioDecoder(codec, index, ffmpeg.av_q2d(stream->time_base));
    }

    // 패킷 하나를 넣고 나온 프레임을 모두 내보낸다. null = 끝(디코더와 리샘플러에 남은 것까지 빼낸다)
    public void Send(AVPacket* packet)
    {
        if (_codec is null)
            return;
        ffmpeg.avcodec_send_packet(_codec, packet);
        while (ffmpeg.avcodec_receive_frame(_codec, _frame) == 0)
        {
            Convert(_frame);
            ffmpeg.av_frame_unref(_frame);
        }
        if (packet is null)
            Drain();
    }

    // seek 뒤 — 디코더·리샘플러에 남은 옛 위치 소리를 버린다
    public void Flush()
    {
        if (_codec is not null)
            ffmpeg.avcodec_flush_buffers(_codec);
        FreeSwr();
        _nextPts = double.NaN;
    }

    private void Convert(AVFrame* frame)
    {
        var format = (AVSampleFormat)frame->format;
        if (_swr is null || frame->sample_rate != _swrInRate || format != _swrInFormat || !SameLayout(&frame->ch_layout))
            InitSwr(frame, format);

        var ts = frame->best_effort_timestamp;
        var framePts = ts != ffmpeg.AV_NOPTS_VALUE ? ts * _timeBase : double.IsNaN(_nextPts) ? 0 : _nextPts;
        // 리샘플러가 아직 내보내지 않은 앞 입력만큼 출력은 이 프레임보다 앞선다
        var start = framePts - ffmpeg.swr_get_delay(_swr, _swrInRate) / (double)_swrInRate;

        var max = ffmpeg.swr_get_out_samples(_swr, frame->nb_samples);
        if (max <= 0)
            return;
        Ensure(max);
        int n;
        fixed (float* p = _buffer)
        {
            var outPtr = (byte*)p;
            n = ffmpeg.swr_convert(_swr, &outPtr, max, frame->extended_data, frame->nb_samples);
        }
        _nextPts = framePts + frame->nb_samples / (double)frame->sample_rate;
        if (n > 0)
            Pcm?.Invoke(_buffer.AsSpan(0, n * Channels), start);
    }

    // 끝 — 리샘플러에 남은 꼬리
    private void Drain()
    {
        if (_swr is null)
            return;
        var start = double.IsNaN(_nextPts) ? 0 : _nextPts - ffmpeg.swr_get_delay(_swr, _swrInRate) / (double)_swrInRate;
        var max = ffmpeg.swr_get_out_samples(_swr, 0);
        if (max <= 0)
            return;
        Ensure(max);
        int n;
        fixed (float* p = _buffer)
        {
            var outPtr = (byte*)p;
            n = ffmpeg.swr_convert(_swr, &outPtr, max, null, 0);
        }
        if (n > 0)
            Pcm?.Invoke(_buffer.AsSpan(0, n * Channels), start);
    }

    private void Ensure(int samples)
    {
        if (_buffer.Length < samples * Channels)
            _buffer = new float[samples * Channels];
    }

    private bool SameLayout(AVChannelLayout* layout)
    {
        fixed (AVChannelLayout* mine = &_swrInLayout)
            return ffmpeg.av_channel_layout_compare(mine, layout) == 0;
    }

    private void InitSwr(AVFrame* frame, AVSampleFormat format)
    {
        FreeSwr();
        AVChannelLayout inLayout;
        if (frame->ch_layout.order == AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC || ffmpeg.av_channel_layout_check(&frame->ch_layout) == 0)
            ffmpeg.av_channel_layout_default(&inLayout, Math.Max(1, frame->ch_layout.nb_channels)); // 배치를 모르면 채널 수로 기본 배치
        else
            ffmpeg.av_channel_layout_copy(&inLayout, &frame->ch_layout);

        AVChannelLayout outLayout;
        ffmpeg.av_channel_layout_default(&outLayout, Channels);
        SwrContext* swr = null;
        var r = ffmpeg.swr_alloc_set_opts2(&swr, &outLayout, AVSampleFormat.AV_SAMPLE_FMT_FLT, SampleRate,
            &inLayout, format, frame->sample_rate, 0, null);
        ffmpeg.av_channel_layout_uninit(&outLayout);
        if (r < 0 || swr is null || ffmpeg.swr_init(swr) < 0)
        {
            ffmpeg.swr_free(&swr);
            ffmpeg.av_channel_layout_uninit(&inLayout);
            throw new InvalidOperationException($"cannot resample audio format {format} at {frame->sample_rate} Hz");
        }

        _swr = swr;
        // 비교용으로는 프레임의 원래 배치를 둔다(UNSPEC 이면 매번 같게 다시 만들지 않도록)
        fixed (AVChannelLayout* mine = &_swrInLayout)
            ffmpeg.av_channel_layout_copy(mine, &frame->ch_layout);
        ffmpeg.av_channel_layout_uninit(&inLayout);
        _swrInRate = frame->sample_rate;
        _swrInFormat = format;
    }

    private void FreeSwr()
    {
        if (_swr is not null)
        {
            var s = _swr;
            ffmpeg.swr_free(&s);
            _swr = null;
        }
        fixed (AVChannelLayout* mine = &_swrInLayout)
            ffmpeg.av_channel_layout_uninit(mine);
        _swrInRate = 0;
        _swrInFormat = AVSampleFormat.AV_SAMPLE_FMT_NONE;
    }

    public void Dispose()
    {
        FreeSwr();
        if (_frame is not null)
        {
            var f = _frame;
            ffmpeg.av_frame_free(&f);
            _frame = null;
        }
        if (_codec is not null)
        {
            var c = _codec;
            ffmpeg.avcodec_free_context(&c); // avcodec_close 는 FFmpeg 8 에서 없어짐 — free_context 가 닫기까지 한다
            _codec = null;
        }
    }
}
