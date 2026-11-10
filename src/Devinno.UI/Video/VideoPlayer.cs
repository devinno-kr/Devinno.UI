using System.Diagnostics;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using SkiaSharp;

namespace Devinno.UI.Video;

/// <summary>
/// Plays a video or sound file (MP4, MKV, AVI, M4A, … — anything FFmpeg can decode) with its sound, as an <see cref="IFrameSource"/> for <c>&lt;DvVideo source="{…}"/&gt;</c>.
/// <c>&lt;DvVideo src="…"/&gt;</c> creates one of these for you; create it yourself when code needs control (seek, position, end of playback).
/// </summary>
/// <remarks>
/// <para><b>FFmpeg</b>: decoding uses the FFmpeg 8 shared libraries (<c>avcodec-62</c>, <c>avformat-62</c>, <c>avutil-60</c>, <c>swscale-9</c>), which are not part of this package.
/// On Windows put the DLLs of an FFmpeg 8 "shared" build next to the app (or in an <c>ffmpeg</c> subfolder, or set <see cref="FFmpegDirectory"/>);
/// on Linux install the OS package (FFmpeg 8 — <c>libavcodec.so.62</c>). If they are missing or a different major version, <see cref="Error"/> says so and the element shows that message.</para>
/// <para><b>Playback</b>: the first frame is decoded when the player is created, so it shows at once even while paused. Pictures and sound are decoded on background threads;
/// pictures are timed by the sound (lip sync — a late picture is skipped), or by a stopwatch when the file has no sound. A file with only sound plays too (nothing is drawn).</para>
/// <para><b>Sound</b>: played with SDL2 (audio only, no window), which is not part of this package. Windows: <c>SDL2.dll</c> (SDL 2.x, x64) next to the app or in <see cref="FFmpegDirectory"/>;
/// Linux: the OS package <c>libsdl2-2.0-0</c> (<c>libSDL2-2.0.so.0</c>). Without SDL2 or a sound device the video plays silently (one Trace warning); a file with only sound then reports why in <see cref="Error"/>.
/// Set the volume with <see cref="Volume"/> and <see cref="Muted"/>.</para>
/// <para><b>Hidden screens</b>: when no screen has looked at the player for about half a second (e.g. its page is hidden), playback time and sound stop and decoding pauses; it continues where it was when shown again.</para>
/// <para>All members can be called from any thread. Dispose the player when you created it yourself; players made for <c>src</c> are disposed by the screen.</para>
/// </remarks>
/// <example>
/// <code>
/// public VideoPlayer Guide { get; } = new("videos/guide.mp4") { Loop = true };
/// // markup: &lt;DvVideo source="{Guide}" fit="cover"/&gt;
/// // code:   Guide.Seek(TimeSpan.Zero); Guide.Playing = false;
/// </code>
/// </example>
public sealed unsafe class VideoPlayer : IFrameSource, IDisposable
{
    // 스레드: demux(패킷 읽기·seek) → 비디오 디코드 → 프레임 큐 → 표시(시계와 비교해 FrameSource 에 내놓음, 화면은 기존 16 ms 확인 주기로 그린다)
    //                              └→ 오디오 디코드 → AudioOutput(SDL 큐)
    // seek 는 serial 번호로 정리한다: 번호를 올리고 패킷 큐에 flush 표시를 넣으면 디코더가 그걸 보고 버퍼를 비우고, 옛 번호의 프레임·소리는 버려진다.
    private const int UnseenMs = 500;           // 이만큼 아무 화면도 안 보면 시계를 멈춘다
    private const int PictureQueueSize = 4;     // 디코드해 둔 프레임 수
    private const double LateDrop = 0.040;      // 시계보다 이만큼 늦은 프레임은 버린다(뒤에 다음 프레임이 있을 때 — 느린 장비에서 화면이 아예 멈추지 않게)
    private const double AudioAhead = 0.5;      // 오디오 큐가 이보다 많으면 오디오 디코드가 기다린다
    private const long PacketBytesMax = 16L << 20;
    private const int VideoPacketsEnough = 30, AudioPacketsEnough = 60, PacketsMax = 2000;
    private const double SeekThrottle = 0.2;    // 앞 seek 의 첫 프레임이 아직이면 다음 seek 는 이만큼 기다린다(슬라이더 드래그 중에도 화면이 따라오게)

    private static readonly object InitGate = new();
    private static string? _initError;
    private static bool _initDone;

    private readonly FrameSource _frames = new();
    private readonly object _gate = new();      // 큐·serial·seek·위치·끝 상태
    private readonly object _audioGate = new(); // 오디오 serial 확인과 큐 넣기를 한 덩어리로(seek 뒤 옛 소리가 들어가지 않게)
    private readonly bool _poster;
    private readonly List<Thread> _threads = [];
    private volatile bool _disposed;
    private volatile bool _stopped;             // 스레드 하나가 오류로 멈춤 — 나머지도 멈춘다
    private volatile bool _playing = true;
    private volatile bool _loop;
    private volatile bool _ended;
    private double _volume = 1;
    private volatile bool _muted;
    private long _lastSeen = Environment.TickCount64;
    private string? _error;

    // demux — 생성자(스레드 시작 전)와 demux 스레드만
    private AVFormatContext* _fmt;
    private AVPacket* _packet;
    private double _duration;

    // 비디오 디코드 — 생성자와 비디오 스레드만
    private int _vStream = -1;
    private AVCodecContext* _vCodec;
    private AVFrame* _vFrame;
    private SwsContext* _sws;
    private int _swsW, _swsH;
    private AVPixelFormat _swsFormat = AVPixelFormat.AV_PIX_FMT_NONE;
    private double _vTimeBase;
    private double _lastVideoPts;
    private readonly PixelPool _pool = new();

    // 오디오 — 디코더는 오디오 스레드만, 출력은 _audioGate
    private AudioDecoder? _audio;               // 오디오 스트림이 없거나 소리를 못 내면 null
    private AudioOutput? _output;
    private int _audioSerial;                   // _audioGate. -1 = seek 중(아무것도 안 넣음)
    private int _aSerial;                       // 오디오 스레드가 디코드 중인 serial
    private double _aDropBefore = double.NegativeInfinity; // seek 목표 앞 소리는 버린다

    // 큐·상태 — _gate
    private readonly PacketQueue _vq = new(), _aq = new();
    private readonly Queue<Picture> _pictures = new();
    private int _serial;
    private double? _seekTo;
    private bool _awaitFirst;                   // seek 했고 그 위치 첫 프레임을 아직 안 내놓음
    private long _seekStartedAt;
    private double _seekTarget;
    private bool _videoDone, _audioDone;
    private double _position;

    // 시계 — 소리를 내면 AudioClock, 아니면 SystemClock. 표시 스레드가 쓴다
    private readonly IClock _clock;

    /// <summary>
    /// The folder that holds the FFmpeg 8 shared libraries. <see langword="null"/> (default) = search the app folder, then its <c>ffmpeg</c> subfolder, then the OS default search (PATH, system libraries).
    /// SDL2 (for sound) is looked for in the app folder, then this folder, then the OS default search.
    /// Set it before the first player is created; it has no effect after FFmpeg and SDL2 have been loaded.
    /// </summary>
    public static string? FFmpegDirectory { get; set; }

    /// <summary>Opens <paramref name="path"/>, decodes its first frame and opens a sound device when the file has sound. Never throws for a bad file or missing FFmpeg — see <see cref="Error"/>.</summary>
    /// <param name="path">The video file path (relative paths are resolved against the current working directory, like <c>DvImage src</c>).</param>
    public VideoPlayer(string path) : this(path, poster: false) { }

    internal VideoPlayer(string path, bool poster)
    {
        Path = path;
        _poster = poster;
        try
        {
            Open(path);
            if (_error is null && _vStream >= 0)
            {
                if (DecodeFirst(out var pts))
                    _position = pts;
                else
                    _error = $"Cannot decode a frame from '{path}'";
            }
        }
        catch (Exception ex)
        {
            _error = $"Cannot open '{path}': {ex.Message}";
        }

        if (_error is null && !_poster && _audio is not null)
            OpenOutput();
        _clock = _output is not null ? new AudioClock(_output) : new SystemClock();

        if (_error is not null)
            Trace.TraceWarning($"Devinno.UI: DvVideo — {_error}");

        if (_poster || _error is not null)
        {
            Close(); // 첫 프레임만 — 디코더는 바로 닫는다
            return;
        }

        _clock.Reset(_position);
        Start(DemuxLoop, "demux");
        if (_vStream >= 0)
            Start(VideoLoop, "video");
        if (_audio is not null)
            Start(AudioLoop, "audio");
        Start(PresentLoop, "present");
    }

    private void Start(ThreadStart run, string name)
    {
        var t = new Thread(() => Guard(run)) { IsBackground = true, Name = "Devinno.UI VideoPlayer " + name };
        _threads.Add(t);
        t.Start();
    }

    private void Guard(ThreadStart run)
    {
        try
        {
            run();
        }
        catch (Exception ex)
        {
            _error = $"Playback stopped: {ex.Message}";
            _stopped = true;
            Trace.TraceWarning($"Devinno.UI: DvVideo '{Path}' — {_error}");
            Wake();
        }
    }

    /// <summary>The file path given when the player was created.</summary>
    public string Path { get; }

    /// <summary>Why the file cannot be played (file missing, unsupported format, FFmpeg 8 not found, or SDL2 not found for a file with only sound). <see langword="null"/> if it plays.</summary>
    public string? Error => _error;

    /// <summary>Whether playback runs. Default <see langword="true"/>. Set <see langword="false"/> to pause (the current frame stays on screen).</summary>
    public bool Playing
    {
        get => _playing;
        set
        {
            if (_playing == value)
                return;
            _playing = value;
            if (value && _ended && !_loop)
                Seek(TimeSpan.Zero); // 끝난 뒤 다시 재생 = 처음부터
            Wake();
        }
    }

    /// <summary>Whether playback starts over at the end. Default <see langword="false"/> (stops on the last frame, <see cref="Ended"/> = <see langword="true"/>).</summary>
    public bool Loop
    {
        get => _loop;
        set
        {
            _loop = value;
            Wake();
        }
    }

    /// <summary>
    /// The sound volume, from 0 (silent) to 1 (full, the default); values outside are clamped. Changing it also changes the sound already buffered (about half a second),
    /// so it is heard after about 20 ms. No effect on files without sound or when sound cannot be played (see the remarks).
    /// </summary>
    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(double.IsNaN(value) ? 1 : value, 0, 1);
            ApplyVolume();
        }
    }

    /// <summary>Whether the sound is off. Default <see langword="false"/>. <see cref="Volume"/> keeps its value and applies again when this is set back to <see langword="false"/>.</summary>
    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            ApplyVolume();
        }
    }

    private void ApplyVolume()
    {
        lock (_audioGate)
            if (_output is not null)
                _output.Volume = _muted ? 0 : _volume * InitialVolumeForTests;
    }

    /// <summary>Whether playback reached the end (only when <see cref="Loop"/> is off). Setting <see cref="Playing"/> to <see langword="true"/> again starts over.</summary>
    public bool Ended => _ended;

    /// <summary>The length of the video, or <see cref="TimeSpan.Zero"/> if unknown.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(_duration);

    /// <summary>The timestamp of the frame on screen. Right after <see cref="Seek"/> it is the target time, before that frame is decoded.</summary>
    public TimeSpan Position
    {
        get { lock (_gate) return TimeSpan.FromSeconds(Math.Max(0, _position)); }
    }

    /// <summary>Jumps to <paramref name="position"/> (the nearest frame at or after it). Works while paused too.</summary>
    /// <param name="position">The target time from the start.</param>
    public void Seek(TimeSpan position)
    {
        // 소리는 바로 끊는다 — 옛 위치 소리가 seek 처리 전까지 이어지지 않게
        lock (_audioGate)
        {
            _audioSerial = -1;
            _output?.Clear();
            _output?.Pause(true);
        }
        lock (_gate)
        {
            _seekTo = Math.Max(0, position.TotalSeconds);
            _position = _seekTo.Value; // Position 은 바로 목표 시각 — 화면 바인딩이 옛 위치를 되써 넣지 않게
            _ended = false;
            Monitor.PulseAll(_gate);
        }
    }

    // 디코드된 소리(48000 Hz 스테레오 float32, 첫 샘플 시각 초). 오디오 스레드에서 불린다
    internal event PcmHandler? AudioDecoded;

    // 테스트용 — 소리를 내는 중인가, 지금 재생 시계, 장치 볼륨에 곱하는 값(테스트는 0 = 소리 없이)
    internal bool HasSound => _output is not null;
    internal double ClockNow => _clock.Now;
    internal double SoundTime
    {
        get { lock (_audioGate) return _output?.CurrentTime ?? double.NaN; }
    }
    internal static double InitialVolumeForTests = 1;

    // DvVideo position·duration 바인딩 상태(UI 스레드만) — 상자는 재빌드마다 새로 만들어지므로 재생기에 둔다
    internal double BoundPosition = double.NaN; // 뷰모델에 있는 값(마지막으로 읽거나 쓴 값). NaN = 아직 안 봄
    internal long PositionWrittenAt;
    internal double WrittenDuration = double.NaN;

    /// <inheritdoc/>
    public long FrameVersion
    {
        get
        {
            Seen();
            return _frames.FrameVersion;
        }
    }

    /// <inheritdoc/>
    public bool IsLive
    {
        get
        {
            Seen();
            bool seeking;
            lock (_gate)
                seeking = _seekTo is not null || _awaitFirst;
            return !_disposed && !_stopped && !_poster && _error is null && (_playing && !_ended || seeking);
        }
    }

    /// <inheritdoc/>
    public bool UseFrame(Action<SKImage> use)
    {
        Seen();
        return _frames.UseFrame(use);
    }

    /// <summary>Stops playback: closes the sound device, ends the background threads and releases FFmpeg resources and the current frame.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Wake();
        var all = true;
        foreach (var t in _threads)
        {
            if (t != Thread.CurrentThread)
                t.Join(2000);
            all &= !t.IsAlive;
        }
        lock (_audioGate)
        {
            _output?.Dispose(); // SDL 장치 닫기
            _output = null;
        }
        if (all)
        {
            lock (_gate)
            {
                _vq.Clear();
                _aq.Clear();
                while (_pictures.Count > 0)
                    _pictures.Dequeue().Image?.Dispose();
            }
            Close();
        }
        else
            Trace.TraceWarning($"Devinno.UI: DvVideo '{Path}' — a playback thread did not stop; its FFmpeg resources are left open");
        _frames.Dispose();
        _pool.Dispose();
    }

    private bool Stop => _disposed || _stopped;

    private void Seen()
    {
        var now = Environment.TickCount64;
        var was = Interlocked.Exchange(ref _lastSeen, now);
        if (now - was > UnseenMs)
            Wake(); // 다시 보이기 시작 — 쉬던 표시 스레드를 깨운다
    }

    private void Wake()
    {
        lock (_gate)
            Monitor.PulseAll(_gate);
    }

    private void WaitGate(int ms)
    {
        lock (_gate)
        {
            if (!Stop)
                Monitor.Wait(_gate, ms);
        }
    }

    // ── demux 스레드 ─────────────────────────────────────────────────

    private void DemuxLoop()
    {
        var eof = false;
        while (!Stop)
        {
            double? seek = null;
            lock (_gate)
            {
                if (_seekTo is not null && (!_awaitFirst || Stopwatch.GetElapsedTime(_seekStartedAt).TotalSeconds >= SeekThrottle))
                {
                    seek = _seekTo;
                    _seekTo = null;
                }
                else if (eof || QueuesFull())
                {
                    if (!Stop)
                        Monitor.Wait(_gate, 50);
                    continue;
                }
            }

            if (seek is { } target)
            {
                DoSeek(target);
                eof = false;
                continue;
            }

            if (ffmpeg.av_read_frame(_fmt, _packet) < 0)
            {
                // 끝(또는 더 못 읽음) — 디코더에 남은 것을 빼내라고 알린다
                lock (_gate)
                {
                    if (_vStream >= 0)
                        _vq.PutMarker(PacketKind.Eof, _serial, 0);
                    if (_audio is not null)
                        _aq.PutMarker(PacketKind.Eof, _serial, 0);
                    Monitor.PulseAll(_gate);
                }
                eof = true;
                continue;
            }

            lock (_gate)
            {
                if (_packet->stream_index == _vStream)
                    _vq.Put(_packet, _serial);
                else if (_audio is not null && _packet->stream_index == _audio.Stream)
                    _aq.Put(_packet, _serial);
                Monitor.PulseAll(_gate);
            }
            ffmpeg.av_packet_unref(_packet); // Put 이 옮겨 가지 않은(다른 스트림) 패킷
        }
    }

    // _gate 안에서. 패킷을 더 읽지 않고 기다릴 만큼 쌓였는가
    private bool QueuesFull()
    {
        if (_vq.Count > PacketsMax || _aq.Count > PacketsMax || _vq.Bytes + _aq.Bytes > PacketBytesMax)
            return true;
        return (_vStream < 0 || _vq.Count >= VideoPacketsEnough) && (_audio is null || _aq.Count >= AudioPacketsEnough);
    }

    // seek: 소리 끊기 → av_seek_frame → 패킷 큐 비우기 + flush 표시(디코더가 avcodec_flush_buffers) →
    // 프레임 큐의 옛 프레임은 표시 스레드가 버리고, 새 위치 첫 프레임 PTS 로 시계를 다시 잡은 뒤 재개한다(CompleteSeek)
    private void DoSeek(double target)
    {
        int serial;
        lock (_gate)
            serial = _serial + 1;
        lock (_audioGate)
        {
            _audioSerial = serial; // 새 위치 소리만 받는다
            _output?.Clear();
            _output?.Pause(true);  // 첫 프레임이 나올 때까지 소리도 멈춤
        }

        var ts = (long)(target * ffmpeg.AV_TIME_BASE);
        if (ffmpeg.av_seek_frame(_fmt, -1, ts, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
            ffmpeg.av_seek_frame(_fmt, -1, 0, ffmpeg.AVSEEK_FLAG_BACKWARD);

        lock (_gate)
        {
            _serial = serial;
            _seekTarget = target;
            _awaitFirst = true;
            _seekStartedAt = Stopwatch.GetTimestamp();
            _videoDone = _audioDone = false;
            _ended = false;
            _vq.Clear();
            _aq.Clear();
            if (_vStream >= 0)
                _vq.PutMarker(PacketKind.Flush, serial, target);
            if (_audio is not null)
                _aq.PutMarker(PacketKind.Flush, serial, target);
            Monitor.PulseAll(_gate); // 꽉 찬 프레임 큐에서 기다리던 비디오 디코더도 깨어나 옛 프레임을 버린다
        }
    }

    // ── 비디오 디코드 스레드 ─────────────────────────────────────────

    private void VideoLoop()
    {
        var serial = 0;
        var dropBefore = double.NegativeInfinity;
        Picture? below = null; // seek 목표 앞의 마지막 프레임 — 목표 이상이 없이 끝나면 이것을 보인다

        while (!Stop)
        {
            PacketQueue.Item item = default;
            lock (_gate)
            {
                while (!Stop && !_vq.TryTake(out item))
                    Monitor.Wait(_gate, 50);
                if (Stop)
                    break;
            }

            switch (item.Kind)
            {
                case PacketKind.Flush:
                    ffmpeg.avcodec_flush_buffers(_vCodec);
                    serial = item.Serial;
                    dropBefore = item.Target;
                    below?.Image?.Dispose();
                    below = null;
                    break;

                case PacketKind.Eof:
                    if (item.Serial != serial)
                        break;
                    ffmpeg.avcodec_send_packet(_vCodec, null); // 디코더에 남은 프레임을 빼낸다
                    ReceiveVideo(serial, ref dropBefore, ref below);
                    if (below is { } last)
                    {
                        below = null;
                        PushPicture(last);
                    }
                    PushPicture(new Picture(null, 0, serial, Eof: true));
                    break;

                default:
                    var pkt = (AVPacket*)item.Packet;
                    var current = item.Serial == serial;
                    if (current)
                        ffmpeg.avcodec_send_packet(_vCodec, pkt);
                    ffmpeg.av_packet_free(&pkt);
                    if (current)
                        ReceiveVideo(serial, ref dropBefore, ref below);
                    break;
            }
        }
        below?.Image?.Dispose();
    }

    private void ReceiveVideo(int serial, ref double dropBefore, ref Picture? below)
    {
        while (!Stop)
        {
            var r = ffmpeg.avcodec_receive_frame(_vCodec, _vFrame);
            if (r == ffmpeg.AVERROR(ffmpeg.EAGAIN) || r == ffmpeg.AVERROR_EOF)
                return;
            if (r < 0)
                throw new InvalidOperationException($"decode error {r}");

            var pts = FramePts(_vFrame);
            var image = ToImage(_vFrame);
            ffmpeg.av_frame_unref(_vFrame);
            if (pts < dropBefore - 0.001)
            {
                below?.Image?.Dispose();
                below = new Picture(image, pts, serial, false);
                continue;
            }
            dropBefore = double.NegativeInfinity;
            below?.Image?.Dispose();
            below = null;
            PushPicture(new Picture(image, pts, serial, false));
        }
    }

    // 프레임 큐에 넣는다(꽉 차면 기다림). 그사이 seek 했으면 버린다
    private void PushPicture(Picture p)
    {
        lock (_gate)
        {
            while (!Stop && p.Serial == _serial && _pictures.Count >= PictureQueueSize)
                Monitor.Wait(_gate, 50);
            if (Stop || p.Serial != _serial)
            {
                p.Image?.Dispose();
                return;
            }
            _pictures.Enqueue(p);
            Monitor.PulseAll(_gate);
        }
    }

    private double FramePts(AVFrame* frame)
    {
        var ts = frame->best_effort_timestamp;
        var pts = ts == ffmpeg.AV_NOPTS_VALUE ? _lastVideoPts : ts * _vTimeBase;
        _lastVideoPts = pts;
        return pts;
    }

    // ── 오디오 디코드 스레드 ─────────────────────────────────────────

    private void AudioLoop()
    {
        while (!Stop)
        {
            PacketQueue.Item item = default;
            lock (_gate)
            {
                while (!Stop && !_aq.TryTake(out item))
                    Monitor.Wait(_gate, 50);
                if (Stop)
                    break;
            }

            switch (item.Kind)
            {
                case PacketKind.Flush:
                    _audio!.Flush();
                    _aSerial = item.Serial;
                    _aDropBefore = item.Target;
                    break;

                case PacketKind.Eof:
                    if (item.Serial != _aSerial)
                        break;
                    _audio!.Send(null); // 남은 소리(리샘플러 꼬리까지)
                    _audio.Flush();     // 다음(루프·seek)에 다시 받을 수 있게
                    lock (_gate)
                    {
                        if (item.Serial == _serial)
                            _audioDone = true;
                        Monitor.PulseAll(_gate);
                    }
                    break;

                default:
                    var pkt = (AVPacket*)item.Packet;
                    if (item.Serial == _aSerial)
                        _audio!.Send(pkt);
                    ffmpeg.av_packet_free(&pkt);
                    break;
            }
        }
    }

    // 오디오 디코더가 내놓은 소리 → seek 목표 앞은 잘라내고 → 큐가 0.5초를 넘으면 기다렸다가 → AudioOutput.Queue
    private void OnPcm(ReadOnlySpan<float> samples, double start)
    {
        AudioDecoded?.Invoke(samples, start);
        if (_output is null)
            return;

        if (start < _aDropBefore)
        {
            var skip = (int)Math.Ceiling((_aDropBefore - start) * AudioDecoder.SampleRate) * AudioDecoder.Channels;
            if (skip >= samples.Length)
                return;
            samples = samples[skip..];
            start += skip / AudioDecoder.Channels / (double)AudioDecoder.SampleRate;
        }
        _aDropBefore = double.NegativeInfinity;

        while (!Stop)
        {
            if (Volatile.Read(ref _audioSerial) != _aSerial)
                return; // seek 했다 — 이 소리는 옛 위치
            if (_output.QueuedSeconds <= AudioAhead)
                break;
            WaitGate(10); // 큐는 신호 없이 줄어든다 — 짧게 다시 본다
        }

        lock (_audioGate)
        {
            if (!Stop && _audioSerial == _aSerial)
                _output?.Queue(samples, start);
        }
    }

    // ── 표시 스레드 ─────────────────────────────────────────────────
    // 판단은 여기서(타이머 해상도와 무관), 화면은 기존 렌더 루프가 FrameVersion 을 16 ms 마다 보고 그 요소만 다시 그린다

    private void PresentLoop()
    {
        bool? running = null; // 시계·장치에 마지막으로 적용한 상태

        while (!Stop)
        {
            Picture pic;
            bool have, pending, awaitFirst, hasNext;
            int serial;
            lock (_gate)
            {
                serial = _serial;
                while (_pictures.Count > 0 && _pictures.Peek().Serial != serial)
                {
                    _pictures.Dequeue().Image?.Dispose(); // seek 전 프레임
                    Monitor.PulseAll(_gate);
                }
                have = _pictures.TryPeek(out pic);
                hasNext = _pictures.Count > 1;
                awaitFirst = _awaitFirst;
                pending = awaitFirst || _seekTo is not null;
            }

            if (pending)
            {
                SetRunning(false, ref running);
                if (awaitFirst && (_vStream < 0 || have))
                    CompleteSeek(have ? pic : null, ref running);
                else
                    WaitGate(10);
                continue;
            }

            var seen = Environment.TickCount64 - Interlocked.Read(ref _lastSeen) <= UnseenMs;
            var run = _playing && !_ended && seen;
            SetRunning(run, ref running);
            if (!run)
            {
                WaitGate(100);
                continue;
            }

            if (have && pic.Eof)
            {
                lock (_gate)
                {
                    if (TakeFront(serial))
                        _videoDone = true;
                }
                continue;
            }

            if (!have)
            {
                if (_vStream < 0 || _videoDone)
                {
                    var now = _clock.Now;
                    lock (_gate)
                        _position = Math.Max(_position, now); // 그림이 더 없으면(오디오만 등) 시계가 위치
                }
                if (!CheckEnd())
                    WaitGate(10);
                continue;
            }

            var wait = pic.Pts - _clock.Now;
            if (wait > 0.002)
            {
                WaitGate((int)Math.Clamp(wait * 1000, 1, 20)); // 앞 프레임은 그대로
                continue;
            }

            bool taken;
            lock (_gate)
                taken = TakeFront(serial);
            if (!taken)
            {
                pic.Image?.Dispose(); // 그사이 seek — 옛 프레임
                continue;
            }

            if (-wait > LateDrop && hasNext)
            {
                pic.Image?.Dispose(); // 늦었다 — 버리고 다음 프레임
                continue;
            }

            _frames.Publish(pic.Image!);
            lock (_gate)
                _position = pic.Pts;
        }
    }

    // _gate 안에서 맨 앞 프레임을 꺼낸다(이 스레드만 꺼냄). 그사이 seek 했으면 false — 꺼낸 것은 호출한 쪽이 해제
    private bool TakeFront(int serial)
    {
        _pictures.Dequeue();
        Monitor.PulseAll(_gate); // 자리가 났다 — 디코더
        return serial == _serial;
    }

    private void CompleteSeek(Picture? first, ref bool? running)
    {
        double pts;
        lock (_gate)
            pts = _seekTarget;
        if (first is { } p)
        {
            bool current;
            lock (_gate)
                current = TakeFront(p.Serial);
            if (!current)
            {
                p.Image?.Dispose();
                return;
            }
            if (p.Eof)
                lock (_gate)
                    _videoDone = true; // 목표가 끝 뒤 — 그림 없이
            else
            {
                pts = p.Pts;
                _frames.Publish(p.Image!);
            }
        }
        _clock.Reset(pts);
        lock (_gate)
        {
            if (_seekTo is null)
                _position = pts; // 다음 seek 가 이미 왔으면 그 목표를 그대로 둔다
            _awaitFirst = false;
            Monitor.PulseAll(_gate);
        }
        running = null; // 재생 상태를 다시 적용(seek 중 멈춘 장치를 다시 켠다)
    }

    // 비디오·오디오가 다 끝났으면 루프면 처음으로(true), 아니면 Ended(true). 아직이면 false
    private bool CheckEnd()
    {
        bool videoDone, audioDone;
        lock (_gate)
        {
            videoDone = _vStream < 0 || _videoDone && _pictures.Count == 0;
            audioDone = _audio is null || _audioDone;
        }
        if (audioDone && _output is not null && _output.QueuedSeconds > 0)
            audioDone = false; // 큐에 남은 소리가 다 나갈 때까지
        if (!videoDone || !audioDone)
            return false;

        if (_loop)
            Seek(TimeSpan.Zero);
        else
            _ended = true;
        return true;
    }

    private void SetRunning(bool run, ref bool? running)
    {
        if (running == run)
            return;
        running = run;
        _clock.Pause(!run);
        lock (_audioGate)
            _output?.Pause(!run);
    }

    // ── FFmpeg ───────────────────────────────────────────────────────

    private void Open(string path)
    {
        if (EnsureFFmpeg() is { } initError)
        {
            _error = initError;
            return;
        }

        if (!File.Exists(path))
        {
            _error = $"Video file not found: '{path}'";
            return;
        }

        AVFormatContext* fmt = null;
        if (ffmpeg.avformat_open_input(&fmt, System.IO.Path.GetFullPath(path), null, null) < 0)
        {
            _error = $"Cannot open '{path}' (not a video FFmpeg can read)";
            return;
        }
        _fmt = fmt;
        if (ffmpeg.avformat_find_stream_info(_fmt, null) < 0)
        {
            _error = $"Cannot read stream info of '{path}'";
            return;
        }
        _duration = _fmt->duration > 0 ? _fmt->duration / (double)ffmpeg.AV_TIME_BASE : 0;
        _packet = ffmpeg.av_packet_alloc();

        AVCodec* decoder = null;
        var stream = ffmpeg.av_find_best_stream(_fmt, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0);
        if (stream >= 0 && decoder is not null)
        {
            var s = _fmt->streams[stream];
            _vTimeBase = ffmpeg.av_q2d(s->time_base);
            _vCodec = ffmpeg.avcodec_alloc_context3(decoder);
            ffmpeg.avcodec_parameters_to_context(_vCodec, s->codecpar);
            _vCodec->thread_count = 0; // 코어 수만큼
            if (ffmpeg.avcodec_open2(_vCodec, decoder, null) < 0)
            {
                _error = $"Cannot open the video decoder for '{path}'";
                return;
            }
            _vFrame = ffmpeg.av_frame_alloc();
            _vStream = stream;
        }

        if (!_poster)
        {
            _audio = AudioDecoder.Open(_fmt, _vStream, out var audioError);
            if (audioError is not null)
                Trace.TraceWarning($"Devinno.UI: DvVideo '{path}' — {audioError}; playing without sound");
            if (_audio is not null)
                _audio.Pcm = OnPcm;
        }

        if (_vStream >= 0 || _audio is not null)
            return;
        // 포스터(첫 그림만)는 소리를 열지 않는다 — 오디오만 있는 파일이면 그릴 것이 없을 뿐 오류는 아니다
        if (_poster && ffmpeg.av_find_best_stream(_fmt, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0) >= 0)
            return;
        _error = $"No video or audio stream FFmpeg can decode in '{path}'";
    }

    // 소리 장치를 연다. 못 열면 비디오만(오디오만 있는 파일이면 그 이유가 오류)
    private void OpenOutput()
    {
        var output = new AudioOutput { Volume = InitialVolumeForTests }; // 아직 Volume·Muted 를 못 받았다 — 생성자 뒤 설정은 ApplyVolume
        if (output.Open())
        {
            _output = output;
            return;
        }

        output.Dispose();
        if (_vStream < 0)
        {
            _error = $"Cannot play the sound of '{Path}': {output.Error}";
            return;
        }
        Trace.TraceWarning($"Devinno.UI: DvVideo '{Path}' — {output.Error}; playing without sound");
        _audio?.Dispose();
        _audio = null;
        lock (_gate)
            _aq.Clear();
    }

    // 생성자용 — 첫 프레임을 디코드해 바로 띄운다. 그동안 읽은 오디오 패킷은 오디오 큐로(처음 소리를 잃지 않게)
    private bool DecodeFirst(out double pts)
    {
        pts = 0;
        var flushing = false;
        while (true)
        {
            var r = ffmpeg.avcodec_receive_frame(_vCodec, _vFrame);
            if (r == 0)
            {
                pts = FramePts(_vFrame);
                _frames.Publish(ToImage(_vFrame));
                ffmpeg.av_frame_unref(_vFrame);
                return true;
            }
            if (r == ffmpeg.AVERROR_EOF || flushing)
                return false;
            if (r != ffmpeg.AVERROR(ffmpeg.EAGAIN))
                throw new InvalidOperationException($"decode error {r}");

            if (ffmpeg.av_read_frame(_fmt, _packet) < 0)
            {
                ffmpeg.avcodec_send_packet(_vCodec, null);
                flushing = true;
                continue;
            }
            if (_packet->stream_index == _vStream)
                ffmpeg.avcodec_send_packet(_vCodec, _packet);
            else if (_audio is not null && _packet->stream_index == _audio.Stream)
                lock (_gate)
                    _aq.Put(_packet, 0);
            ffmpeg.av_packet_unref(_packet);
        }
    }

    // RGBA 로 변환해 미리 잡아 둔 픽셀 버퍼(풀)에 직접 쓴다. 그림이 해제되면 버퍼는 풀로 돌아간다
    private SKImage ToImage(AVFrame* frame)
    {
        var w = frame->width;
        var h = frame->height;
        var format = (AVPixelFormat)frame->format;
        if (_sws is null || w != _swsW || h != _swsH || format != _swsFormat)
        {
            ffmpeg.sws_freeContext(_sws);
            _sws = ffmpeg.sws_getContext(w, h, format, w, h, AVPixelFormat.AV_PIX_FMT_RGBA, (int)SwsFlags.SWS_BILINEAR, null, null, null);
            if (_sws is null)
                throw new InvalidOperationException($"cannot convert pixel format {format}");
            (_swsW, _swsH, _swsFormat) = (w, h, format);
        }

        var rowBytes = w * 4;
        var pixels = _pool.Rent(rowBytes * h);
        var dst = new byte_ptrArray4();
        var dstStride = new int_array4();
        dst[0] = (byte*)pixels;
        dstStride[0] = rowBytes;
        var src = new byte_ptrArray8();
        src.UpdateFrom(frame->data);
        var srcStride = new int_array8();
        srcStride.UpdateFrom(frame->linesize);
        ffmpeg.sws_scale(_sws, src, srcStride, 0, h, dst, dstStride);

        return _pool.Wrap(pixels, new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque), rowBytes);
    }

    private void Close()
    {
        _audio?.Dispose(); // swr_free·avcodec_free_context(닫기 포함)·av_frame_free
        _audio = null;
        if (_sws is not null)
        {
            ffmpeg.sws_freeContext(_sws);
            _sws = null;
        }
        if (_packet is not null)
        {
            var p = _packet;
            ffmpeg.av_packet_free(&p);
            _packet = null;
        }
        if (_vFrame is not null)
        {
            var f = _vFrame;
            ffmpeg.av_frame_free(&f);
            _vFrame = null;
        }
        if (_vCodec is not null)
        {
            var c = _vCodec;
            ffmpeg.avcodec_free_context(&c);
            _vCodec = null;
        }
        lock (_gate)
            _aq.Clear(); // 생성자에서 읽어 둔 오디오 패킷(재생 전에 닫힌 경우)
        if (_fmt is not null)
        {
            var f = _fmt;
            ffmpeg.avformat_close_input(&f);
            _fmt = null;
        }
    }

    // FFmpeg 8 공유 라이브러리를 한 번 불러 본다. 실패 이유(없음·주 버전 다름)를 돌려준다
    private static string? EnsureFFmpeg()
    {
        lock (InitGate)
        {
            if (_initDone)
                return _initError;
            _initDone = true;
            try
            {
                ffmpeg.RootPath = FindFFmpeg() ?? "";
                var major = ffmpeg.avcodec_version() >> 16;
                if (major != Required)
                    _initError = $"FFmpeg 8 is required (avcodec {Required}); found avcodec {major}";
                else
                {
                    // 나머지 라이브러리도 지금 확인 — 재생 도중이 아니라 여기서 알린다
                    _ = ffmpeg.avformat_version();
                    _ = ffmpeg.swscale_version();
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or NotSupportedException or BadImageFormatException)
            {
                _initError = "FFmpeg 8 libraries not found. Put the FFmpeg 8 shared DLLs (avcodec-62 …) next to the app or set VideoPlayer.FFmpegDirectory";
            }

            if (_initError is not null)
                Trace.TraceWarning($"Devinno.UI: {_initError}");
            return _initError;
        }
    }

    private const uint Required = 62; // avcodec 주 버전 = FFmpeg 8

    private static string? FindFFmpeg()
    {
        if (FFmpegDirectory is { Length: > 0 } dir)
            return dir;
        var name = OperatingSystem.IsWindows() ? $"avcodec-{Required}.dll" : $"libavcodec.so.{Required}";
        foreach (var candidate in new[] { AppContext.BaseDirectory, System.IO.Path.Combine(AppContext.BaseDirectory, "ffmpeg") })
        {
            if (File.Exists(System.IO.Path.Combine(candidate, name)))
                return candidate;
        }
        return null; // OS 기본 검색
    }

    internal static void ResetForTests()
    {
        lock (InitGate)
        {
            _initDone = false;
            _initError = null;
        }
    }

    // ── 큐 ───────────────────────────────────────────────────────────

    private readonly record struct Picture(SKImage? Image, double Pts, int Serial, bool Eof);

    private enum PacketKind { Packet, Flush, Eof }

    // 패킷 큐(_gate 안에서만). 패킷은 복사 없이 옮겨 담고, 꺼낸 쪽이 av_packet_free 한다
    private sealed class PacketQueue
    {
        public readonly record struct Item(nint Packet, int Serial, PacketKind Kind, double Target);

        private readonly Queue<Item> _items = new();

        public int Count => _items.Count;
        public long Bytes { get; private set; }

        public void Put(AVPacket* src, int serial)
        {
            var p = ffmpeg.av_packet_alloc();
            ffmpeg.av_packet_move_ref(p, src);
            Bytes += p->size;
            _items.Enqueue(new Item((nint)p, serial, PacketKind.Packet, 0));
        }

        public void PutMarker(PacketKind kind, int serial, double target) => _items.Enqueue(new Item(0, serial, kind, target));

        public bool TryTake(out Item item)
        {
            if (!_items.TryDequeue(out item))
                return false;
            if (item.Kind == PacketKind.Packet)
                Bytes -= ((AVPacket*)item.Packet)->size;
            return true;
        }

        public void Clear()
        {
            while (_items.TryDequeue(out var item))
            {
                if (item.Kind != PacketKind.Packet)
                    continue;
                var p = (AVPacket*)item.Packet;
                ffmpeg.av_packet_free(&p);
            }
            Bytes = 0;
        }
    }

    // RGBA 픽셀 버퍼 풀 — 같은 크기 버퍼를 다시 쓴다(프레임마다 할당하지 않음). SKImage 가 해제되면(어느 스레드든) 돌려받는다
    private sealed class PixelPool : IDisposable
    {
        private const int Keep = PictureQueueSize + 4; // 큐 + 화면에 있는 것 + 디코드 중 + 여유
        private readonly object _gate = new();
        private readonly Stack<IntPtr> _free = new();
        private readonly SKImageRasterReleaseDelegate _release;
        private int _size;
        private bool _disposed;

        public PixelPool() => _release = (addr, ctx) => Return(addr, (int)ctx!);

        public IntPtr Rent(int size)
        {
            lock (_gate)
            {
                if (size != _size)
                {
                    FreeAll();
                    _size = size; // 해상도가 바뀌면 옛 크기 버퍼는 돌아오는 대로 해제
                }
                if (_free.Count > 0)
                    return _free.Pop();
            }
            return Marshal.AllocHGlobal(size);
        }

        public SKImage Wrap(IntPtr pixels, SKImageInfo info, int rowBytes)
        {
            using var pixmap = new SKPixmap(info, pixels, rowBytes);
            return SKImage.FromPixels(pixmap, _release, rowBytes * info.Height)
                   ?? throw new InvalidOperationException("SKImage.FromPixels returned null");
        }

        private void Return(IntPtr pixels, int size)
        {
            lock (_gate)
            {
                if (!_disposed && size == _size && _free.Count < Keep)
                {
                    _free.Push(pixels);
                    return;
                }
            }
            Marshal.FreeHGlobal(pixels);
        }

        private void FreeAll()
        {
            while (_free.Count > 0)
                Marshal.FreeHGlobal(_free.Pop());
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                FreeAll();
            }
        }
    }
}
