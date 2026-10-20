using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// hold="ms" — 그만큼 누르고 있어야 onclick 이 일어난다(떼기 전, 시간이 찬 프레임에). 짧게 누르면·밖으로 나가면 없음.
public class HoldTests
{
    private const float W = 400, H = 300;
    private static readonly SKPoint OnButton = new(110, 70);   // 버튼: left 50, top 50, 160×40
    private static readonly SKPoint OffButton = new(300, 250);

    public sealed class Vm
    {
        public int Clicks;
        public bool Off { get; set; }
        public string Hold { get; set; } = "1000";

        public void Hit() => Clicks++;
    }

    private static (DvSurface S, Vm Vm, Action<long> Advance) Start(string hold = "hold=\"1000\"", string extra = "")
    {
        long t = 10_000;
        var vm = new Vm();
        var s = new DvSurface(
            $"<DvScreen style=\"padding:0\"><DvButton text=\"기동\" onclick=\"Hit\" {hold} {extra} " +
            "style=\"position:absolute; left:50; top:50; width:160; height:40\"/></DvScreen>", vm, DvTheme.Light)
        {
            Clock = () => t,
        };
        Render(s);
        return (s, vm, d => t += d);
    }

    private static SKBitmap Render(DvSurface s)
    {
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H, 0);
        return bmp;
    }

    [Fact]
    public void ShortPress_NoClick()
    {
        var (s, vm, advance) = Start();
        s.PointerDown(OnButton);
        advance(500);
        Render(s);
        s.PointerUp(OnButton);
        Render(s);

        Assert.Equal(0, vm.Clicks);
    }

    [Fact]
    public void HeldLongEnough_ClicksBeforeRelease_OnlyOnce()
    {
        var (s, vm, advance) = Start();
        s.PointerDown(OnButton);
        advance(999);
        Render(s);
        Assert.Equal(0, vm.Clicks);

        advance(1);
        Render(s);
        Assert.Equal(1, vm.Clicks); // 떼기 전

        advance(5000);
        Render(s);
        s.PointerUp(OnButton);
        Render(s);
        Assert.Equal(1, vm.Clicks); // 떼기는 클릭을 내지 않는다
    }

    [Fact]
    public void LeavingButton_Cancels()
    {
        var (s, vm, advance) = Start();
        s.PointerDown(OnButton);
        advance(300);
        Render(s);
        s.PointerMove(OffButton);
        advance(5000);
        Render(s);
        s.PointerUp(OffButton);
        Render(s);

        Assert.Equal(0, vm.Clicks);
    }

    [Fact]
    public void Disabled_NoClick()
    {
        var (s, vm, advance) = Start(extra: "disabled=\"true\"");
        s.PointerDown(OnButton);
        advance(2000);
        Render(s);
        s.PointerUp(OnButton);

        Assert.Equal(0, vm.Clicks);
    }

    [Fact]
    public void NoHold_ClickOnRelease()
    {
        var (s, vm, _) = Start(hold: "");
        s.PointerDown(OnButton);
        s.PointerUp(OnButton);

        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Bound_HoldValue()
    {
        var (s, vm, advance) = Start(hold: "hold=\"{Hold}\"");
        s.PointerDown(OnButton);
        advance(1000);
        Render(s);

        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Progress_IsDrawn_AndWakesFrames()
    {
        var (s, _, advance) = Start();
        var probe = new SKPoint(55, 70); // 버튼 왼쪽 끝 근처 — 진행 띠가 먼저 덮는 곳
        var before = Render(s).GetPixel((int)probe.X, (int)probe.Y);

        s.PointerDown(OnButton);
        Assert.NotNull(s.NextAnimationDue(0)); // 누르는 동안 프레임을 깨운다
        advance(500);
        var during = Render(s).GetPixel((int)probe.X, (int)probe.Y);

        Assert.NotEqual(before, during);

        s.PointerUp(OnButton);
        var after = Render(s).GetPixel((int)probe.X, (int)probe.Y);
        Assert.Equal(before, after); // 떼면 띠가 사라진다
        Assert.Null(s.NextAnimationDue(0));
    }

    private sealed class AppPage : Devinno.UI.App.DvPage
    {
        public int Clicks;

        public void Hit() => Clicks++;

        protected override string MarkupSource =>
            "<DvScreen style=\"padding:0\"><DvButton text=\"기동\" onclick=\"Hit\" hold=\"80\" " +
            "style=\"position:absolute; left:50; top:50; width:160; height:40\"/></DvScreen>";
    }

    // 앱 화면(페이지 칸) 경로 — 실제 시계로: 누르는 동안 루트가 프레임을 깨우고, 시간이 차면 페이지 처리기가 불린다
    [Fact]
    public void InApp_PageButton_HoldFires()
    {
        var page = new AppPage();
        var app = new Devinno.UI.App.DvApp();
        app.Pages.Add(page);
        var root = app.Surface;
        Render(root);

        root.PointerDown(OnButton);
        Assert.NotNull(root.NextAnimationDue(Environment.TickCount64)); // 루트가 칸의 진행을 보고 깨운다
        var until = Environment.TickCount64 + 2000;
        while (page.Clicks == 0 && Environment.TickCount64 < until)
        {
            Thread.Sleep(20);
            Render(root);
        }

        Assert.Equal(1, page.Clicks);
        root.PointerUp(OnButton);
        Render(root);
        Assert.Equal(1, page.Clicks);
    }
}
