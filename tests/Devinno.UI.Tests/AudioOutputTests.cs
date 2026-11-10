using Devinno.UI.Video;
using Xunit;
using Xunit.Abstractions;

// SDL2 소리 출력. SDL2.dll 은 앱 폴더나 VideoPlayer.FFmpegDirectory(테스트는 DEVINNO_FFMPEG_DIR) 에서 찾는다.
// SDL2 나 소리 장치가 없으면(CI 등) 건너뛴다. 소리는 볼륨 0 으로 넣는다(시각만 본다)
[Collection("Sound device")] // 소리 장치 테스트끼리는 겹치지 않게 — 장치 열기·닫기는 프로세스 전체에서 줄을 선다
public class AudioOutputTests
{
    private readonly ITestOutputHelper _out;

    public AudioOutputTests(ITestOutputHelper output) => _out = output;

    private AudioOutput? OpenOrSkip()
    {
        if (Environment.GetEnvironmentVariable("DEVINNO_FFMPEG_DIR") is { Length: > 0 } dir)
            VideoPlayer.FFmpegDirectory = dir;
        var o = new AudioOutput { Volume = 0 };
        if (o.Open())
            return o;
        _out.WriteLine($"no SDL2 sound device ({o.Error}) — skipped");
        o.Dispose();
        return null;
    }

    private static float[] Silence(double seconds) => new float[(int)(seconds * AudioOutput.SampleRate) * AudioOutput.Channels];

    private static bool WaitUntil(Func<bool> cond, int ms)
    {
        var until = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < until)
        {
            if (cond())
                return true;
            Thread.Sleep(10);
        }
        return cond();
    }

    [Fact]
    public void CurrentTime_IsEndMinusQueued_AndAdvancesWhilePlaying()
    {
        using var o = OpenOrSkip();
        if (o is null)
            return;
        Assert.True(double.IsNaN(o.CurrentTime), "넣기 전에는 모름");
        o.Queue(Silence(0.5), 10.0);
        Assert.InRange(o.QueuedSeconds, 0.45, 0.5);
        Assert.InRange(o.CurrentTime, 10.0, 10.05); // 일시정지로 열림 — 아직 안 나감

        o.Pause(false);
        Thread.Sleep(250);
        var t = o.CurrentTime;
        _out.WriteLine($"after 250 ms: {t:F3}, queued {o.QueuedSeconds:F3}, chunk {o.ChunkSeconds:F3}");
        Assert.InRange(t, 10.1, 10.4);
        Assert.True(WaitUntil(() => o.QueuedSeconds == 0, 1500), "다 나간다");
        Assert.InRange(o.CurrentTime, 10.499, 10.501);
    }

    [Fact]
    public void Pause_FreezesCurrentTime()
    {
        using var o = OpenOrSkip();
        if (o is null)
            return;
        o.Queue(Silence(1.0), 0);
        o.Pause(false);
        Thread.Sleep(200);
        o.Pause(true);
        Thread.Sleep(50); // 장치가 가져가던 덩어리가 끝나기를
        var frozen = o.CurrentTime;
        Thread.Sleep(300);
        Assert.Equal(frozen, o.CurrentTime, 3);
        o.Pause(false);
        Assert.True(WaitUntil(() => o.CurrentTime > frozen + 0.1, 1000), "다시 흐른다");
    }

    [Fact]
    public void Clear_DropsTheQueue_TimeUnknownUntilNextQueue()
    {
        using var o = OpenOrSkip();
        if (o is null)
            return;
        o.Queue(Silence(1.0), 0);
        o.Pause(false);
        Thread.Sleep(100);
        o.Clear();
        Assert.Equal(0, o.QueuedSeconds);
        Assert.True(double.IsNaN(o.CurrentTime));
        o.Queue(Silence(0.2), 5.0);
        Assert.InRange(o.CurrentTime, 5.0, 5.05);
    }

    [Fact]
    public void Volume_IsClamped_AndDisposeTwiceIsSafe()
    {
        var o = OpenOrSkip();
        if (o is null)
            return;
        o.Volume = 2;
        Assert.Equal(1, o.Volume);
        o.Volume = -1;
        Assert.Equal(0, o.Volume);
        o.Queue(Silence(0.1), 0);
        o.Dispose();
        o.Dispose();
        Assert.Equal(0, o.QueuedSeconds);
        o.Queue(Silence(0.1), 0); // 닫힌 뒤에는 아무것도 안 한다
    }

    [Fact]
    public void ChangingVolume_RequeuesTheBufferedSound_TimeAndAmountUnchanged()
    {
        using var o = OpenOrSkip();
        if (o is null)
            return;
        o.Queue(Silence(0.4), 3.0); // 일시정지 상태 — 그대로 남아 있다
        var queued = o.QueuedSeconds;
        var time = o.CurrentTime;
        o.Volume = 0.5;
        Assert.Equal(queued, o.QueuedSeconds, 3);
        Assert.Equal(time, o.CurrentTime, 3);
        o.Clear();
        o.Volume = 0.2; // 빈 큐 — 아무것도 다시 넣지 않는다
        Assert.Equal(0, o.QueuedSeconds);
    }

    [Fact]
    public void OpenAndDispose_FromManyThreadsAtOnce_DoesNotCrash()
    {
        using var probe = OpenOrSkip();
        if (probe is null)
            return;
        // SDL_Init·SDL_QuitSubSystem 은 스레드 안전하지 않다 — 줄 세우지 않으면 여기서 프로세스가 죽는다
        var until = Environment.TickCount64 + 1500;
        var opened = 0;
        var threads = Enumerable.Range(0, 6).Select(_ => new Thread(() =>
        {
            while (Environment.TickCount64 < until)
            {
                using var o = new AudioOutput { Volume = 0 };
                if (o.Open())
                    Interlocked.Increment(ref opened);
                o.Queue(Silence(0.01), 0);
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());
        Assert.True(opened > 0);
    }
}
