using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Video;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

// DvVideo 2단계 — FFmpeg 8 로 파일 재생. FFmpeg 공유 라이브러리 폴더를 환경 변수 DEVINNO_FFMPEG_DIR 로 받는다.
// 없으면(CI 등) 재생 테스트는 아무것도 확인하지 않고 끝난다 — 출력에 알린다.
public class VideoPlayerTests
{
    private readonly ITestOutputHelper _out;

    public VideoPlayerTests(ITestOutputHelper output) => _out = output;

    private static readonly string Clip = Path.Combine(AppContext.BaseDirectory, "Assets", "redblue.mp4"); // 빨강 0~1초, 파랑 1~2초

    private static readonly Lazy<bool> Available = new(() =>
    {
        var dir = Environment.GetEnvironmentVariable("DEVINNO_FFMPEG_DIR");
        if (string.IsNullOrEmpty(dir))
            return false;
        VideoPlayer.FFmpegDirectory = dir;
        using var probe = new VideoPlayer(Clip);
        return probe.Error is null;
    });

    private bool Ready()
    {
        if (Available.Value)
            return true;
        _out.WriteLine("FFmpeg 8 not available (set DEVINNO_FFMPEG_DIR) — skipped");
        return false;
    }

    private static SKColor Center(IFrameSource s)
    {
        SKColor c = default;
        Assert.True(s.UseFrame(img =>
        {
            using var bmp = SKBitmap.FromImage(img);
            c = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
        }));
        return c;
    }

    private static bool IsRed(SKColor c) => c.Red > 200 && c.Blue < 60;
    private static bool IsBlue(SKColor c) => c.Blue > 200 && c.Red < 60;

    // 화면이 보고 있는 것처럼 FrameVersion 을 읽으며 조건을 기다린다
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
    public void Open_ShowsFirstFrame_KnowsDuration()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(Clip) { Playing = false };
        Assert.Null(p.Error);
        Assert.True(IsRed(Center(p)), Center(p).ToString());
        Assert.InRange(p.Duration.TotalSeconds, 1.9, 2.1);
    }

    [Fact]
    public void Plays_ToTheSecondColor_ThenEnds()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(Clip);
        Assert.True(WaitFor(p, () => IsBlue(Center(p)), 3000), "1초 뒤 파랑");
        Assert.True(WaitFor(p, () => p.Ended, 3000), "2초 뒤 끝");
        Assert.False(p.IsLive);
        Assert.True(IsBlue(Center(p)), "마지막 프레임이 남는다");
    }

    [Fact]
    public void Loop_StartsOver()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(Clip) { Loop = true };
        Assert.True(WaitFor(p, () => IsBlue(Center(p)), 3000));
        Assert.True(WaitFor(p, () => IsRed(Center(p)), 3000), "끝나면 처음(빨강)으로");
        Assert.False(p.Ended);
    }

    [Fact]
    public void Seek_WhilePaused_ShowsThatFrame()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(Clip) { Playing = false };
        p.Seek(TimeSpan.FromSeconds(1.5));
        Assert.True(WaitFor(p, () => IsBlue(Center(p)), 2000));
        Assert.InRange(p.Position.TotalSeconds, 1.4, 1.6);
        p.Seek(TimeSpan.Zero);
        Assert.True(WaitFor(p, () => IsRed(Center(p)), 2000));
    }

    [Fact]
    public void Unseen_PausesTheClock()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer(Clip);
        Thread.Sleep(1600); // 아무도 안 봄 — 시계가 0.5초쯤에서 멈춘다
        Assert.True(p.Position.TotalSeconds < 1.0, $"position {p.Position}");
        Assert.True(WaitFor(p, () => IsBlue(Center(p)), 3000), "다시 보면 이어서 재생");
    }

    [Fact]
    public void MissingFile_GivesError_DoesNotThrow()
    {
        if (!Ready())
            return;
        using var p = new VideoPlayer("no-such-file.mp4");
        Assert.Contains("not found", p.Error);
        Assert.False(p.IsLive);
        Assert.False(p.UseFrame(_ => { }));
    }

    public sealed class Vm
    {
        public string Path { get; set; } = Clip;
        public bool Play { get; set; } = true;
        public bool Show { get; set; } = true;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Src_ElementOwnsThePlayer_KeptAcrossRebuilds_DisposedWhenGone()
    {
        if (!Ready())
            return;
        var vm = new Vm();
        var s = new DvSurface("<DvScreen><DvVideo src=\"{Path}\" playing=\"{Play}\" loop=\"true\" visible=\"{Show}\"/></DvScreen>", vm, DvTheme.Light);
        s.Layout(600, 400);
        var player = (VideoPlayer)Flatten(s.Root!).First(b => b.Node.Tag == "DvVideo").VideoSource!;
        Assert.True(player.Playing);
        Assert.True(player.Loop);

        vm.Play = false;
        s.Invalidate();
        s.Layout(600, 400);
        var again = Flatten(s.Root!).First(b => b.Node.Tag == "DvVideo").VideoSource;
        Assert.Same(player, again);
        Assert.False(player.Playing);

        vm.Path = "no-such-file.mp4";
        s.Invalidate();
        s.Layout(600, 400);
        var box = Flatten(s.Root!).First(b => b.Node.Tag == "DvVideo");
        Assert.NotSame(player, box.VideoSource);
        Assert.False(player.UseFrame(_ => { })); // 이전 재생기는 닫힘
        Assert.Contains("not found", box.VideoError);
    }

    [Fact]
    public void OneOffRender_ShowsTheFirstFrame()
    {
        if (!Ready())
            return;
        var png = DvRenderer.RenderToPng($"<DvScreen style=\"padding:0\"><DvVideo src=\"{Clip}\" style=\"width:160; height:90\"/></DvScreen>", null, 200, 120);
        using var bmp = SKBitmap.Decode(png);
        Assert.True(IsRed(bmp.GetPixel(80, 45)), bmp.GetPixel(80, 45).ToString());
    }

    [Fact]
    public void ErrorMessage_IsPaintedInTheBox()
    {
        if (!Ready())
            return;
        var png = DvRenderer.RenderToPng("<DvScreen style=\"padding:0\"><DvVideo src=\"missing.mp4\" style=\"width:320; height:180\"/></DvScreen>", null, 320, 180);
        using var bmp = SKBitmap.Decode(png);
        var ink = 0;
        for (var x = 0; x < 320; x++)
            for (var y = 60; y < 120; y++)
                if (bmp.GetPixel(x, y).Red < 180)
                    ink++;
        Assert.True(ink > 50, "가운데에 글자");
    }
}
