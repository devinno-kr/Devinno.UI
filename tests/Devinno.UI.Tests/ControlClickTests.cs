using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 컨트롤 핸들의 Click 이벤트 — 일반 클릭 분기에서 onclick 다음에 창 기준 좌표와 함께 일어난다.
public class ControlClickTests
{
    public sealed class Vm : IDvControlHost
    {
        public DvButton? btn; public DvControl? inner;
        public List<string> Log = new();
        public int Outer;
        public void Go() => Log.Add("onclick");
        public void OuterGo() => Outer++;
        public void Attach(DvControlLookup c) { btn = c.Bind(btn, "btn"); inner = c.Bind(inner, "inner"); }
    }

    private const string Page = "<DvStack><DvButton id=\"btn\" text=\"A\" onclick=\"Go\"/></DvStack>";

    private static (Vm vm, DvSurface s) Make(string markup = Page)
    {
        var vm = new Vm();
        var s = new DvSurface(markup, vm);
        s.Layout(400, 300);
        return (vm, s);
    }

    private static LayoutBox Box(DvSurface s, string tag) => LayoutQuery.FindByTag(s.Root!, tag)!;

    private static SKPoint Center(LayoutBox b) => new(b.Rect.MidX, b.Rect.MidY);

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(400, 300);
    }

    [Fact]
    public void Click_Raised_WithSenderAndWindowPosition()
    {
        var (vm, s) = Make();
        object? sender = null; SKPoint? pos = null; var n = 0;
        vm.btn!.Click += (o, e) => { sender = o; pos = e.Position; n++; };
        s.Layout(400, 300);
        var p = Center(Box(s, "DvButton"));
        Tap(s, p);
        Assert.Equal(1, n);
        Assert.Same(vm.btn, sender);
        Assert.Equal(p, pos);
    }

    [Fact]
    public void Click_InScreenOffset_IsWindowRelative()
    {
        var (vm, s) = Make();
        s.WindowOffset = new SKPoint(30, 40);
        SKPoint? pos = null;
        vm.btn!.Click += (_, e) => pos = e.Position;
        s.Layout(400, 300);
        var p = Center(Box(s, "DvButton"));
        Tap(s, p);
        Assert.Equal(new SKPoint(p.X + 30, p.Y + 40), pos);
    }

    [Fact]
    public void Onclick_Then_Click_Order()
    {
        var (vm, s) = Make();
        vm.btn!.Click += (_, _) => vm.Log.Add("Click");
        s.Layout(400, 300);
        Tap(s, Center(Box(s, "DvButton")));
        Assert.Equal(new[] { "onclick", "Click" }, vm.Log);
    }

    [Fact]
    public void ClickOnly_Box_IsInteractive_StopsBubbling()
    {
        var (vm, s) = Make("<DvBox onclick=\"OuterGo\"><DvBox id=\"inner\" style=\"width:50;height:50\"/></DvBox>");
        var n = 0;
        vm.inner!.Click += (_, _) => n++;
        s.Layout(400, 300);
        var inner = LayoutQuery.FindByTag(s.Root!, "DvBox")!;
        inner = inner.Children[0];
        Tap(s, Center(inner));
        Assert.Equal(1, n);
        Assert.Equal(0, vm.Outer);
    }

    [Fact]
    public void Disabled_NoClick()
    {
        var (vm, s) = Make();
        var n = 0;
        vm.btn!.Click += (_, _) => n++;
        vm.btn.Enabled = false;
        s.Layout(400, 300);
        Tap(s, Center(Box(s, "DvButton")));
        Assert.Equal(0, n);
    }

    [Fact]
    public void Throwing_Subscriber_ReportsHandlerError()
    {
        var (vm, s) = Make();
        var errors = new List<DvHandlerErrorEventArgs>();
        s.HandlerError += (_, e) => errors.Add(e);
        vm.btn!.Click += (_, _) => throw new InvalidOperationException("boom");
        s.Layout(400, 300);
        var p = Center(Box(s, "DvButton"));
        Tap(s, p);
        var err = Assert.Single(errors);
        Assert.Equal("Click", err.Kind);
        Assert.Equal("btn", err.Member);
        Assert.Equal("DvButton", err.Tag);
        Tap(s, p); // 화면 계속 동작
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Click_Survives_NewSurface()
    {
        var (vm, _) = Make();
        var n = 0;
        vm.btn!.Click += (_, _) => n++;
        var s2 = new DvSurface(Page, vm);
        s2.Layout(400, 300);
        Tap(s2, Center(Box(s2, "DvButton")));
        Assert.Equal(1, n);
    }

    [Fact]
    public void OldSurface_Tap_NoClick_NewSurface_ClickOnce()
    {
        var (vm, s1) = Make();
        var n = 0; var onclick = 0;
        vm.btn!.Click += (_, _) => n++;
        var s2 = new DvSurface(Page, vm); // 핸들이 새 화면으로 옮겨 감
        s2.Layout(400, 300);
        Assert.Same(s2, vm.btn.Surface);
        onclick = vm.Log.Count;
        Tap(s1, Center(Box(s1, "DvButton")));
        Assert.Equal(0, n);
        Assert.Equal(onclick + 1, vm.Log.Count); // 옛 화면의 onclick(마크업 처리기)은 그대로
        Tap(s2, Center(Box(s2, "DvButton")));
        Assert.Equal(1, n);
    }
}
