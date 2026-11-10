using Devinno.UI.Video;
using Xunit;
using Xunit.Abstractions;

// DvVideo 소리 — 디코딩(48000 Hz 스테레오 float32). FFmpeg 8 은 DEVINNO_FFMPEG_DIR, 없으면 건너뛴다
public class VideoAudioTests
{
    private readonly ITestOutputHelper _out;

    public VideoAudioTests(ITestOutputHelper output) => _out = output;

    private static readonly string Video = Path.Combine(AppContext.BaseDirectory, "Assets", "redblue.mp4");
    private static readonly string VideoTone = Path.Combine(AppContext.BaseDirectory, "Assets", "redblue_tone.mp4");
    private static readonly string Tone = Path.Combine(AppContext.BaseDirectory, "Assets", "tone.m4a");

    private static readonly Lazy<bool> Available = new(() =>
    {
        var dir = Environment.GetEnvironmentVariable("DEVINNO_FFMPEG_DIR");
        if (string.IsNullOrEmpty(dir))
            return false;
        VideoPlayer.FFmpegDirectory = dir;
        using var probe = new VideoPlayer(Video);
        return probe.Error is null;
    });

    private bool Ready()
    {
        if (Available.Value)
            return true;
        _out.WriteLine("FFmpeg 8 not available (set DEVINNO_FFMPEG_DIR) — skipped");
        return false;
    }

    private static bool WaitFor(VideoPlayer p, Func<bool> cond, int ms)
    {
        var until = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < until)
        {
            _ = p.FrameVersion;
            if (cond())
                return true;
            Thread.Sleep(15);
        }
        return cond();
    }

    [Fact]
    public void AudioStream_IsDecodedTo48kStereoFloat_WithTimestamps()
    {
        if (!Ready())
            return;
        var gate = new object();
        long samples = 0;
        var odd = false;
        double first = double.NaN, last = 0, peak = 0;
        using var p = new VideoPlayer(VideoTone);
        Assert.Null(p.Error);
        p.AudioDecoded += (pcm, pts) =>
        {
            lock (gate)
            {
                odd |= pcm.Length % AudioDecoder.Channels != 0;
                if (double.IsNaN(first))
                    first = pts;
                Assert.True(pts >= last - 0.001, $"시각이 거꾸로: {pts} < {last}");
                last = pts + pcm.Length / AudioDecoder.Channels / (double)AudioDecoder.SampleRate;
                samples += pcm.Length / AudioDecoder.Channels;
                foreach (var v in pcm)
                    peak = Math.Max(peak, Math.Abs(v));
            }
        };
        Assert.True(WaitFor(p, () => p.Ended, 4000), "끝까지 재생");
        lock (gate)
        {
            _out.WriteLine($"samples {samples} first {first:F3} end {last:F3} peak {peak:F2}");
            Assert.False(odd, "스테레오 interleaved");
            Assert.InRange(first, 0, 0.6); // 생성자에서 디코드한 앞부분은 구독 전이라 빠질 수 있다
            Assert.InRange(last, 1.95, 2.1);
            Assert.InRange(samples / (double)AudioDecoder.SampleRate, last - first - 0.05, last - first + 0.05); // 시각과 샘플 수가 맞다(48000 Hz)
            Assert.True(peak > 0.1, "사인파 소리");
        }
    }

    [Fact]
    public void VideoWithoutAudio_PlaysAsBefore_NoPcm()
    {
        if (!Ready())
            return;
        var count = 0;
        using var p = new VideoPlayer(Video);
        p.AudioDecoded += (_, _) => Interlocked.Increment(ref count);
        Assert.True(WaitFor(p, () => p.Ended, 4000));
        Assert.Equal(0, count);
    }
}
