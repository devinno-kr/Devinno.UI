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
        VideoPlayer.InitialVolumeForTests = 0; // 테스트 중 소리는 내지 않는다(장치·시계는 그대로)
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

    [Fact]
    public void VideoWithSound_PlaysToTheEnd_OnTheSoundClock()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(VideoTone);
        Assert.Null(p.Error);
        if (!p.HasSound)
        {
            _out.WriteLine("no SDL2 sound device — sound part skipped");
            return;
        }
        Assert.True(WaitFor(p, () => p.Position.TotalSeconds > 1.1, 3000), "1초를 넘는다");
        var gap = p.Position.TotalSeconds - p.SoundTime;
        _out.WriteLine($"picture {p.Position.TotalSeconds:F3} sound {p.SoundTime:F3}");
        Assert.InRange(gap, -0.08, 0.08); // 그림과 소리가 함께 간다
        Assert.True(WaitFor(p, () => p.Ended, 3000), "끝");
        Assert.False(p.IsLive);
    }

    [Fact]
    public void AudioOnlyFile_Plays_PositionAdvances_ThenEnds()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(Tone);
        if (p.Error is { } e && e.Contains("sound"))
        {
            _out.WriteLine($"{e} — skipped");
            return;
        }
        Assert.Null(p.Error);
        Assert.True(p.HasSound);
        Assert.True(p.IsLive);
        Assert.False(p.UseFrame(_ => { })); // 그림은 없다
        Assert.True(WaitFor(p, () => p.Position.TotalSeconds > 0.8, 2500), $"위치가 흐른다 {p.Position}");
        Assert.True(WaitFor(p, () => p.Ended, 3000), "끝");
        Assert.InRange(p.Position.TotalSeconds, 1.9, 2.2);
    }

    [Fact]
    public void AudioOnly_Poster_HasNoError()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(Tone, poster: true);
        Assert.Null(p.Error);
        Assert.False(p.IsLive);
    }

    [Fact]
    public void AfterSeek_NoSoundFromTheOldPosition()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(VideoTone);
        if (!p.HasSound)
            return;
        Assert.True(WaitFor(p, () => p.SoundTime > 0.2, 2000));
        p.Seek(TimeSpan.FromSeconds(1.5));
        var until = Environment.TickCount64 + 400;
        var seen = 0;
        while (Environment.TickCount64 < until)
        {
            _ = p.FrameVersion;
            var t = p.SoundTime;
            if (!double.IsNaN(t))
            {
                seen++;
                Assert.True(t >= 1.5 - 0.001, $"옛 위치 소리 {t:F3}");
            }
            Thread.Sleep(5);
        }
        Assert.True(seen > 0, "새 위치 소리가 나온다");
    }

    [Fact]
    public void PauseAndResume_ClockStops_AndContinuesWithoutAJump()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(VideoTone);
        if (!p.HasSound)
            return;
        Assert.True(WaitFor(p, () => p.ClockNow > 0.5, 2000));
        p.Playing = false;
        Thread.Sleep(100);
        var frozen = p.ClockNow;
        var sound = p.SoundTime;
        Thread.Sleep(400);
        Assert.Equal(frozen, p.ClockNow, 3);
        Assert.Equal(sound, p.SoundTime, 3);
        p.Playing = true;
        Assert.True(WaitFor(p, () => p.ClockNow != frozen, 500));
        var resumed = p.ClockNow;
        _out.WriteLine($"frozen {frozen:F3} first after resume {resumed:F3}");
        Assert.InRange(resumed - frozen, 0, 0.05); // 이어서 — 튀지 않는다
    }

    [Fact]
    public void RapidSeeks_FromAnotherThread_NoDeadlock_ThenSettles()
    {
        if (!Ready())
            return;
        var p = new VideoPlayer(VideoTone) { Loop = true };
        var rnd = new Random(1);
        var stop = Environment.TickCount64 + 3000;
        var seeker = new Thread(() =>
        {
            while (Environment.TickCount64 < stop)
            {
                p.Seek(TimeSpan.FromSeconds(rnd.NextDouble() * 2.2)); // 끝 뒤도 섞는다
                if (rnd.Next(10) == 0)
                    p.Playing = !p.Playing;
                Thread.Sleep(rnd.Next(0, 16));
            }
        });
        seeker.Start();
        while (Environment.TickCount64 < stop)
        {
            _ = p.FrameVersion;
            _ = p.IsLive;
            p.UseFrame(_ => { });
            Thread.Sleep(16);
        }
        seeker.Join();
        Assert.Null(p.Error);
        p.Playing = false;
        p.Seek(TimeSpan.FromSeconds(1.5));
        Assert.True(WaitFor(p, () => !p.IsLive, 2000), "seek 가 끝난다");
        Assert.InRange(p.Position.TotalSeconds, 1.4, 1.6);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        p.Dispose();
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Dispose {sw.ElapsedMilliseconds} ms — 스레드가 바로 끝난다");
    }
}
