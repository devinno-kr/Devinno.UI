using System.Diagnostics;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using SkiaSharp;

namespace Devinno.UI.Video;

/// <summary>
/// Plays a video file (MP4, MKV, AVI, … — anything FFmpeg can decode) as an <see cref="IFrameSource"/> for <c>&lt;DvVideo source="{…}"/&gt;</c>.
/// <c>&lt;DvVideo src="…"/&gt;</c> creates one of these for you; create it yourself when code needs control (seek, position, end of playback).
/// </summary>
/// <remarks>
/// <para><b>FFmpeg</b>: decoding uses the FFmpeg 8 shared libraries (<c>avcodec-62</c>, <c>avformat-62</c>, <c>avutil-60</c>, <c>swscale-9</c>), which are not part of this package.
/// On Windows put the DLLs of an FFmpeg 8 "shared" build next to the app (or in an <c>ffmpeg</c> subfolder, or set <see cref="FFmpegDirectory"/>);
/// on Linux install the OS package (FFmpeg 8 — <c>libavcodec.so.62</c>). If they are missing or a different major version, <see cref="Error"/> says so and the element shows that message.</para>
/// <para><b>Playback</b>: the first frame is decoded when the player is created, so it shows at once even while paused. Frames are decoded on a background thread and timed by their timestamps.
/// Video only — sound is not played.</para>
/// <para><b>Hidden screens</b>: when no screen has looked at the player for about half a second (e.g. its page is hidden), playback time stops and decoding pauses; it continues where it was when shown again.</para>
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
    private const int UnseenMs = 500; // 이만큼 아무 화면도 안 보면 시계를 멈춘다

    private static readonly object InitGate = new();
    private static string? _initError;
    private static bool _initDone;

    private readonly FrameSource _frames = new();
    private readonly object _gate = new();
    private readonly bool _poster;
    private readonly Thread? _thread;
    private volatile bool _disposed;
    private volatile bool _playing = true;
    private volatile bool _loop;
    private volatile bool _ended;
    private long _lastSeen = Environment.TickCount64;
    private string? _error;

    // FFmpeg 상태 — 생성자(스레드 시작 전)와 디코드 스레드만 만진다
    private AVFormatContext* _fmt;
    private AVCodecContext* _codec;
    private AVFrame* _frame;
    private AVPacket* _packet;
    private SwsContext* _sws;
    private int _swsW, _swsH;
    private AVPixelFormat _swsFormat = AVPixelFormat.AV_PIX_FMT_NONE;
    private int _stream = -1;
    private double _timeBase;
    private bool _flushing;
    private double _duration;

    // 재생 시계(초) — 멈추면 그 자리
    private readonly Stopwatch _clock = new();
    private double _clockBase;
    private double _position;
    private double? _seekTo;

    /// <summary>
    /// The folder that holds the FFmpeg 8 shared libraries. <see langword="null"/> (default) = search the app folder, then its <c>ffmpeg</c> subfolder, then the OS default search (PATH, system libraries).
    /// Set it before the first player is created; it has no effect after FFmpeg has been loaded.
    /// </summary>
    public static string? FFmpegDirectory { get; set; }

    /// <summary>Opens <paramref name="path"/> and decodes its first frame. Never throws for a bad file or missing FFmpeg — see <see cref="Error"/>.</summary>
    /// <param name="path">The video file path (relative paths are resolved against the current working directory, like <c>DvImage src</c>).</param>
    public VideoPlayer(string path) : this(path, poster: false) { }

    internal VideoPlayer(string path, bool poster)
    {
        Path = path;
        _poster = poster;
        try
        {
            Open(path);
            if (_error is null && DecodeNext(out var pts))
            {
                _position = pts;
                _clockBase = pts;
            }
            else
                _error ??= $"Cannot decode a frame from '{path}'";
        }
        catch (Exception ex)
        {
            _error = $"Cannot open '{path}': {ex.Message}";
        }

        if (_error is not null)
            Trace.TraceWarning($"Devinno.UI: DvVideo — {_error}");

        if (_poster || _error is not null)
        {
            Close(); // 첫 프레임만 — 디코더는 바로 닫는다
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "Devinno.UI VideoPlayer" };
        _thread.Start();
    }

    /// <summary>The file path given when the player was created.</summary>
    public string Path { get; }

    /// <summary>Why the video cannot be shown (file missing, unsupported format, FFmpeg 8 not found). <see langword="null"/> if it plays.</summary>
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

    /// <summary>Whether playback reached the end (only when <see cref="Loop"/> is off). Setting <see cref="Playing"/> to <see langword="true"/> again starts over.</summary>
    public bool Ended => _ended;

    /// <summary>The length of the video, or <see cref="TimeSpan.Zero"/> if unknown.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(_duration);

    /// <summary>The timestamp of the frame on screen.</summary>
    public TimeSpan Position
    {
        get { lock (_gate) return TimeSpan.FromSeconds(Math.Max(0, _position)); }
    }

    /// <summary>Jumps to <paramref name="position"/> (the nearest frame at or after it). Works while paused too.</summary>
    /// <param name="position">The target time from the start.</param>
    public void Seek(TimeSpan position)
    {
        lock (_gate)
        {
            _seekTo = Math.Max(0, position.TotalSeconds);
            _ended = false;
        }
        Wake();
    }

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
            return !_disposed && !_poster && _error is null && (_playing && !_ended || _seekTo is not null);
        }
    }

    /// <inheritdoc/>
    public bool UseFrame(Action<SKImage> use)
    {
        Seen();
        return _frames.UseFrame(use);
    }

    /// <summary>Stops the decode thread and releases FFmpeg resources and the current frame.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Wake();
        if (_thread is not null && _thread != Thread.CurrentThread)
            _thread.Join(2000);
        if (_thread is null || !_thread.IsAlive)
            Close();
        _frames.Dispose();
    }

    private void Seen()
    {
        var now = Environment.TickCount64;
        var was = Interlocked.Exchange(ref _lastSeen, now);
        if (now - was > UnseenMs)
            Wake(); // 다시 보이기 시작 — 쉬던 디코드 스레드를 깨운다
    }

    private void Wake()
    {
        lock (_gate)
            Monitor.PulseAll(_gate);
    }

    // ── 디코드 스레드 ────────────────────────────────────────────────

    private void Run()
    {
        SKImage? next = null;
        double nextPts = 0;
        try
        {
            while (!_disposed)
            {
                double? seek;
                lock (_gate)
                {
                    seek = _seekTo;
                    _seekTo = null;
                }

                if (seek is { } target)
                {
                    next?.Dispose();
                    next = null;
                    SeekTo(target);
                    continue;
                }

                var seen = Environment.TickCount64 - Interlocked.Read(ref _lastSeen) <= UnseenMs;
                if (!_playing || _ended || !seen)
                {
                    PauseClock();
                    lock (_gate)
                    {
                        if (!_disposed && _seekTo is null)
                            Monitor.Wait(_gate, 100);
                    }
                    continue;
                }

                ResumeClock();
                if (next is null)
                {
                    if (!DecodeNext(out nextPts, out next))
                    {
                        if (_loop)
                        {
                            SeekTo(0);
                            continue;
                        }

                        _ended = true;
                        continue;
                    }
                }

                var wait = nextPts - Now();
                if (wait > 0.001)
                {
                    lock (_gate)
                    {
                        if (!_disposed && _seekTo is null)
                            Monitor.Wait(_gate, TimeSpan.FromSeconds(Math.Min(wait, 0.05)));
                    }
                    continue;
                }

                lock (_gate)
                    _position = nextPts;
                _frames.Publish(next!);
                next = null;
            }
        }
        catch (Exception ex)
        {
            _error = $"Playback stopped: {ex.Message}";
            Trace.TraceWarning($"Devinno.UI: DvVideo '{Path}' — {_error}");
        }
        finally
        {
            next?.Dispose();
            Close();
        }
    }

    private double Now() => _clockBase + _clock.Elapsed.TotalSeconds;

    private void PauseClock()
    {
        if (!_clock.IsRunning)
            return;
        _clockBase = Now();
        _clock.Reset();
    }

    private void ResumeClock()
    {
        if (!_clock.IsRunning)
            _clock.Start();
    }

    // target(초) 로 가서 그 시각 이상의 첫 프레임을 띄운다. 시계도 그 시각에서 다시
    private void SeekTo(double target)
    {
        var ts = (long)(target * ffmpeg.AV_TIME_BASE);
        if (ffmpeg.av_seek_frame(_fmt, -1, ts, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
            ffmpeg.av_seek_frame(_fmt, -1, 0, ffmpeg.AVSEEK_FLAG_BACKWARD);
        ffmpeg.avcodec_flush_buffers(_codec);
        _flushing = false;
        _ended = false;

        SKImage? shown = null;
        double pts = target;
        while (DecodeNext(out var p, out var image))
        {
            shown?.Dispose();
            shown = image;
            pts = p;
            if (p >= target - 0.001)
                break;
        }

        if (shown is not null)
        {
            lock (_gate)
                _position = pts;
            _frames.Publish(shown);
        }
        _clock.Reset();
        _clockBase = pts;
        if (_playing)
            _clock.Start();
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

        AVCodec* decoder = null;
        _stream = ffmpeg.av_find_best_stream(_fmt, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0);
        if (_stream < 0 || decoder is null)
        {
            _error = $"No video stream FFmpeg can decode in '{path}'";
            return;
        }

        var stream = _fmt->streams[_stream];
        _timeBase = ffmpeg.av_q2d(stream->time_base);
        _duration = _fmt->duration > 0 ? _fmt->duration / (double)ffmpeg.AV_TIME_BASE : 0;

        _codec = ffmpeg.avcodec_alloc_context3(decoder);
        ffmpeg.avcodec_parameters_to_context(_codec, stream->codecpar);
        _codec->thread_count = 0; // 코어 수만큼
        if (ffmpeg.avcodec_open2(_codec, decoder, null) < 0)
        {
            _error = $"Cannot open the video decoder for '{path}'";
            return;
        }

        _frame = ffmpeg.av_frame_alloc();
        _packet = ffmpeg.av_packet_alloc();
    }

    // 생성자용 — 첫 프레임을 바로 띄운다
    private bool DecodeNext(out double pts)
    {
        if (!DecodeNext(out pts, out var image))
            return false;
        _frames.Publish(image);
        return true;
    }

    // 다음 프레임 하나를 디코드해 RGBA 그림으로. 끝이면 false
    private bool DecodeNext(out double pts, out SKImage image)
    {
        pts = 0;
        image = null!;
        if (_codec is null)
            return false;

        while (true)
        {
            var r = ffmpeg.avcodec_receive_frame(_codec, _frame);
            if (r == 0)
            {
                var ts = _frame->best_effort_timestamp;
                pts = ts == ffmpeg.AV_NOPTS_VALUE ? _position : ts * _timeBase;
                image = ToImage(_frame);
                ffmpeg.av_frame_unref(_frame);
                return true;
            }

            if (r == ffmpeg.AVERROR_EOF)
                return false;
            if (r != ffmpeg.AVERROR(ffmpeg.EAGAIN))
                throw new InvalidOperationException($"decode error {r}");
            if (_flushing)
                return false;

            var read = ffmpeg.av_read_frame(_fmt, _packet);
            if (read < 0)
            {
                ffmpeg.avcodec_send_packet(_codec, null); // 끝 — 디코더에 남은 프레임을 빼낸다
                _flushing = true;
                continue;
            }

            if (_packet->stream_index == _stream)
                ffmpeg.avcodec_send_packet(_codec, _packet);
            ffmpeg.av_packet_unref(_packet);
        }
    }

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
        var pixels = Marshal.AllocHGlobal(rowBytes * h);
        var dst = new byte_ptrArray4();
        var dstStride = new int_array4();
        dst[0] = (byte*)pixels;
        dstStride[0] = rowBytes;
        var src = new byte_ptrArray8();
        src.UpdateFrom(frame->data);
        var srcStride = new int_array8();
        srcStride.UpdateFrom(frame->linesize);
        ffmpeg.sws_scale(_sws, src, srcStride, 0, h, dst, dstStride);

        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var pixmap = new SKPixmap(info, pixels, rowBytes);
        return SKImage.FromPixels(pixmap, (addr, _) => Marshal.FreeHGlobal(addr))
               ?? throw new InvalidOperationException("SKImage.FromPixels returned null");
    }

    private void Close()
    {
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
        if (_frame is not null)
        {
            var f = _frame;
            ffmpeg.av_frame_free(&f);
            _frame = null;
        }
        if (_codec is not null)
        {
            var c = _codec;
            ffmpeg.avcodec_free_context(&c);
            _codec = null;
        }
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
}
