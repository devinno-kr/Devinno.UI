using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 위젯 포인터 확장 — 버튼·수정키·클릭 횟수, hover·leave, 휠. 왼쪽 버튼만 쓰는 기존 위젯·앱 동작은 그대로.
public class WidgetPointerArgsTests
{
    public sealed class Vm
    {
        public int Clicks;
        public void Hit() => Clicks++;
        public bool Show { get; set; } = true;
        public List<string> Items { get; } = Enumerable.Range(0, 60).Select(i => $"row {i}").ToList();
    }

    // 받은 것을 모두 기록하는 캔버스
    public sealed class Canvas : DvWidget
    {
        public readonly List<string> Log = new();
        public bool TakeWheel = true;
        public bool HoverRepaint = true;
        public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(200, 100);
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) { }

        public override bool OnPointerDown(DvWidgetContext ctx, DvPointerArgs e)
        {
            Log.Add($"down {e.Button} {e.Modifiers} {e.ClickCount}");
            return true;
        }

        public override void OnPointerMove(DvWidgetContext ctx, DvPointerArgs e) => Log.Add($"move {e.Button} {e.Modifiers}");
        public override void OnPointerUp(DvWidgetContext ctx, DvPointerArgs e) => Log.Add($"up {e.Button} {e.ClickCount}");
        public override void OnPointerCancel(DvWidgetContext ctx) => Log.Add("cancel");

        public override bool OnPointerHover(DvWidgetContext ctx, DvPointerArgs e)
        {
            Log.Add($"hover {e.Point.X - ctx.Bounds.Left:0} {e.Modifiers}");
            return HoverRepaint;
        }

        public override void OnPointerLeave(DvWidgetContext ctx) => Log.Add("leave");

        public override bool OnPointerWheel(DvWidgetContext ctx, DvPointerArgs e, float delta)
        {
            Log.Add($"wheel {delta} {e.Modifiers}");
            return TakeWheel;
        }
    }

    // 예전 방식(점만 받는) 위젯 — 왼쪽 버튼에서만 불린다
    public sealed class OldStyle : DvWidget
    {
        public int Downs, Ups;
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) { }
        public override bool OnPointerDown(DvWidgetContext ctx, SKPoint point) { Downs++; return true; }
        public override void OnPointerUp(DvWidgetContext ctx, SKPoint point) => Ups++;
    }

    private long _now = 1000;

    private DvSurface Start(string body, Vm vm)
    {
        var r = new ComponentRegistry();
        r.RegisterWidget<Canvas>("Canvas");
        r.RegisterWidget<OldStyle>("OldStyle");
        var s = new DvSurface($"<DvScreen><DvStack align=\"start\">{body}</DvStack></DvScreen>", vm, DvTheme.Light, r) { Clock = () => _now };
        s.Layout(800, 600);
        using var surface = SKSurface.Create(new SKImageInfo(800, 600)); // 캔버스는 서피스가 소유한다 — 서피스를 붙잡아 두지 않으면 그리는 도중 GC 가 해제할 수 있다
        s.RenderTo(surface.Canvas, 800, 600, _now);
        return s;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static LayoutBox Find(DvSurface s, string tag) => Flatten(s.Root!).First(b => b.Node.Tag == tag);
    private static SKPoint Mid(LayoutBox b) => new(b.Rect.MidX, b.Rect.MidY);

    [Fact]
    public void RightButton_GoesToTheWidget_WithModifiers()
    {
        var vm = new Vm();
        var s = Start("<Canvas onclick=\"Hit\"/>", vm);
        var box = Find(s, "Canvas");
        var c = (Canvas)box.Widget!;

        s.PointerDown(Mid(box), DvPointerButton.Right, DvModifiers.Shift | DvModifiers.Ctrl);
        s.PointerMove(Mid(box) + new SKPoint(5, 0), DvModifiers.Shift);
        s.PointerUp(Mid(box) + new SKPoint(5, 0), DvPointerButton.Right);
        Assert.Equal(["down Right Shift, Ctrl 1", "move Right Shift", "up Right 1"], c.Log);
        Assert.Equal(0, vm.Clicks);
    }

    [Fact]
    public void RightButton_OnAPlainElement_DoesNothing()
    {
        var vm = new Vm();
        var s = Start("<DvButton text=\"Go\" onclick=\"Hit\"/>", vm);
        var b = Find(s, "DvButton");
        s.PointerDown(Mid(b), DvPointerButton.Right);
        Assert.Null(s.PressBox);
        s.PointerUp(Mid(b), DvPointerButton.Right);
        Assert.Equal(0, vm.Clicks);

        s.PointerDown(Mid(b)); // 왼쪽은 그대로 클릭
        s.PointerUp(Mid(b));
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void ClickCount_CountsQuickPressesInPlace()
    {
        var s = Start("<Canvas/>", new Vm());
        var box = Find(s, "Canvas");
        var c = (Canvas)box.Widget!;
        void Click(SKPoint p) { s.PointerDown(p); s.PointerUp(p); }

        Click(Mid(box));
        _now += 200;
        Click(Mid(box) + new SKPoint(2, 1));
        _now += 200;
        Click(Mid(box));
        _now += 600; // 너무 늦음
        Click(Mid(box));
        _now += 100;
        Click(Mid(box) + new SKPoint(20, 0)); // 너무 멂
        Assert.Equal([1, 2, 3, 1, 1], c.Log.Where(l => l.StartsWith("down")).Select(l => int.Parse(l.Split(' ')[^1])));
    }

    [Fact]
    public void Hover_And_Leave_ReachTheWidget()
    {
        var s = Start("<Canvas/><DvLabel text=\"other\"/>", new Vm());
        var box = Find(s, "Canvas");
        var c = (Canvas)box.Widget!;
        var label = Find(s, "DvLabel");

        s.PointerMove(new SKPoint(box.Rect.Left + 30, box.Rect.MidY), DvModifiers.Alt);
        Assert.True(s.NeedsRender);
        s.PointerMove(Mid(label));
        s.PointerMove(Mid(box));
        s.PointerLeave();
        Assert.Equal(["hover 30 Alt", "leave", "hover 100 None", "leave"], c.Log);
    }

    [Fact]
    public void Hover_RepaintsOnlyWhenTheWidgetSaysSo()
    {
        var s = Start("<Canvas/>", new Vm());
        var box = Find(s, "Canvas");
        ((Canvas)box.Widget!).HoverRepaint = false;
        s.PointerMove(Mid(box));
        using var surface = SKSurface.Create(new SKImageInfo(800, 600)); // 캔버스는 서피스가 소유한다 — 서피스를 붙잡아 두지 않으면 그리는 도중 GC 가 해제할 수 있다
        s.RenderTo(surface.Canvas, 800, 600, _now);
        s.PointerMove(Mid(box) + new SKPoint(1, 0));
        Assert.False(s.NeedsRender);
    }

    [Fact]
    public void Wheel_TakenByTheWidget_DoesNotScroll()
    {
        var vm = new Vm();
        var s = Start("<DvBox style=\"height:300; overflow:scroll\"><DvStack><Canvas/><DvRepeat items=\"{Items}\"><DvLabel text=\"{Item}\"/></DvRepeat></DvStack></DvBox>", vm);
        var box = Find(s, "Canvas");
        var c = (Canvas)box.Widget!;
        var scroller = Flatten(s.Root!).First(b => b.IsScroller);

        s.PointerWheel(Mid(box), -1, DvModifiers.Ctrl);
        Assert.Equal(0, scroller.ScrollY);
        Assert.Equal(["wheel -1 Ctrl"], c.Log);

        c.TakeWheel = false;
        s.PointerWheel(Mid(box), -1);
        Assert.True(scroller.ScrollY > 0);
    }

    [Fact]
    public void OldStyleWidget_GetsOnlyTheLeftButton()
    {
        var s = Start("<OldStyle/>", new Vm());
        var box = Find(s, "OldStyle");
        var w = (OldStyle)box.Widget!;
        s.PointerDown(Mid(box), DvPointerButton.Right);
        s.PointerUp(Mid(box), DvPointerButton.Right);
        Assert.Equal((0, 0), (w.Downs, w.Ups));
        s.PointerDown(Mid(box));
        s.PointerUp(Mid(box));
        Assert.Equal((1, 1), (w.Downs, w.Ups));
    }

    [Fact]
    public void RightGesture_IsCancelledWhenTheWidgetDisappears()
    {
        var vm = new Vm();
        var s = Start("<Canvas visible=\"{Show}\"/>", vm);
        var box = Find(s, "Canvas");
        var c = (Canvas)box.Widget!;
        s.PointerDown(Mid(box), DvPointerButton.Middle);
        vm.Show = false;
        s.Invalidate();
        s.Layout(800, 600);
        s.PointerUp(Mid(box), DvPointerButton.Middle);
        Assert.Equal(["down Middle None 1", "cancel"], c.Log);
    }

    [Fact]
    public void LeftPress_CancelsARightGestureInProgress()
    {
        var s = Start("<Canvas/>", new Vm());
        var box = Find(s, "Canvas");
        var c = (Canvas)box.Widget!;
        s.PointerDown(Mid(box), DvPointerButton.Right);
        s.PointerDown(Mid(box));
        s.PointerMove(Mid(box) + new SKPoint(3, 0));
        s.PointerUp(Mid(box) + new SKPoint(3, 0));
        s.PointerUp(Mid(box), DvPointerButton.Right); // 이미 취소됨 — 아무 일 없음
        Assert.Equal(["down Right None 1", "cancel", "down Left None 1", "move Left None", "up Left 1"], c.Log);
    }
}
