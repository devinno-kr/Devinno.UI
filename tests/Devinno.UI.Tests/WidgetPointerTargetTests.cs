using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 포인터를 다루는 위젯은 onclick 없이도 누름 대상(감싼 상자의 onclick 에 뺏기지 않음, 거절하면 감싼 상자로),
// 위젯 좌표 ↔ 창 좌표(ToWindow·FromWindow), 펼침 창 열림(IsPopupOpen)
public class WidgetPointerTargetTests
{
    public sealed class Vm
    {
        public int Clicks;
        public void Hit() => Clicks++;
        public int Count { get; set; } = 1;
    }

    public sealed class Grabber : DvWidget
    {
        public bool Take = true;
        public int Downs;
        public SKPoint Down, DownWindow, DownBack;
        public SKRect PaintedWindow;
        public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(100, 60);
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) => PaintedWindow = ctx.ToWindow(bounds);
        public override bool OnPointerDown(DvWidgetContext ctx, SKPoint point)
        {
            Downs++;
            Down = point;
            DownWindow = ctx.ToWindow(point);
            DownBack = ctx.FromWindow(DownWindow);
            return Take;
        }
    }

    public sealed class Plain : DvWidget
    {
        public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(100, 60);
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) { }
    }

    private static readonly List<Grabber> Made = new();

    private static ComponentRegistry Reg()
    {
        var r = new ComponentRegistry();
        r.RegisterWidget<Grabber>("Grabber");
        r.RegisterWidget<Plain>("Plain");
        return r;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static (DvSurface S, Vm Vm, LayoutBox Widget) Start(string inner)
    {
        var vm = new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:0\"><DvStack onclick=\"Hit\" style=\"padding:20\">{inner}</DvStack></DvScreen>", vm, null, Reg());
        s.Layout(400, 300);
        return (s, vm, Flatten(s.Root!).First(b => b.Widget is not null));
    }

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(400, 300);
    }

    private static SKPoint Mid(LayoutBox b) => new(b.Rect.MidX, b.Rect.MidY);

    [Fact]
    public void A_widget_that_takes_the_press_gets_it_inside_an_onclick_parent()
    {
        var (s, vm, w) = Start("<Grabber/>");
        Tap(s, Mid(w));
        var g = (Grabber)w.Widget!;
        Assert.Equal(1, g.Downs);
        Assert.Equal(0, vm.Clicks);
    }

    [Fact]
    public void A_declined_press_goes_to_the_onclick_parent()
    {
        var (s, vm, w) = Start("<Grabber/>");
        var g = (Grabber)w.Widget!;
        g.Take = false;
        Tap(s, Mid(w));
        Assert.Equal(1, g.Downs);
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void A_widget_without_pointer_methods_lets_the_parent_click()
    {
        var (s, vm, w) = Start("<Plain/>");
        Tap(s, Mid(w));
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Window_coordinates_follow_scroll_and_the_page_position()
    {
        var app = new DvApp();
        var page = new ScrollPage();
        app.Pages.Add(page);
        app.Top = new TopBar();
        app.Registry = Reg();
        var root = app.Surface;
        root.Layout(400, 300);
        var scroller = Flatten(page.Surface!.Root!).First(b => b.IsScroller);
        scroller.ScrollY = 120;
        var wbox = Flatten(page.Surface.Root!).First(b => b.Widget is not null);
        var g = (Grabber)wbox.Widget!;

        // 창 좌표 = 페이지 위치(위 막대 40) + 스크롤 반영 자리
        var at = new SKPoint(wbox.Rect.MidX, wbox.Rect.MidY - 120 + 40);
        root.PointerDown(at);
        root.PointerUp(at);
        Assert.Equal(1, g.Downs);
        Assert.Equal(at.X, g.DownWindow.X, 2);
        Assert.Equal(at.Y, g.DownWindow.Y, 2);
        Assert.Equal(g.Down.X, g.DownBack.X, 2);
        Assert.Equal(g.Down.Y, g.DownBack.Y, 2);

        using var bmp = new SKBitmap(400, 300);
        using (var c = new SKCanvas(bmp))
            root.RenderTo(c, 400, 300);
        Assert.Equal(wbox.Rect.Top - 120 + 40, g.PaintedWindow.Top, 2); // Paint 에서도 같은 변환
    }

    [Fact]
    public void Popup_open_is_public()
    {
        var vm = new Vm();
        var s = new DvSurface("""
            <DvScreen style="padding:0"><DvSelect value="{Bind Count}" style="width:200; height:40">
              <DvOption value="1"/><DvOption value="2"/>
            </DvSelect></DvScreen>
            """, vm);
        s.Layout(400, 300);
        Assert.False(s.IsPopupOpen);
        Tap(s, new SKPoint(100, 20));
        Assert.True(s.IsPopupOpen);
        s.PointerDown(new SKPoint(390, 290)); // 바깥 = 닫힘
        s.PointerUp(new SKPoint(390, 290));
        Assert.False(s.IsPopupOpen);
    }

    [Fact]
    public void Popup_open_reads_the_same_from_an_app_page()
    {
        var app = new DvApp();
        var page = new SelectPage();
        app.Pages.Add(page);
        var root = app.Surface;
        root.Layout(400, 300);
        Tap(root, new SKPoint(100, 20));
        Assert.True(root.IsPopupOpen);
        Assert.True(page.Surface!.IsPopupOpen);
    }

    private sealed class ScrollPage : DvPage
    {
        protected override string MarkupSource => """
            <DvScreen style="padding:0"><DvStack style="height:200; overflow:scroll; gap:0">
              <DvBox style="height:150"/><Grabber/><DvBox style="height:300"/>
            </DvStack></DvScreen>
            """;
    }

    private sealed class TopBar : DvBar
    {
        protected override string MarkupSource => "<DvStack style=\"height:40\"/>";
    }

    private sealed class SelectPage : DvPage
    {
        public int Count { get; set; } = 1;
        protected override string MarkupSource => """
            <DvScreen style="padding:0"><DvSelect value="{Bind Count}" style="width:200; height:40">
              <DvOption value="1"/><DvOption value="2"/>
            </DvSelect></DvScreen>
            """;
    }
}
