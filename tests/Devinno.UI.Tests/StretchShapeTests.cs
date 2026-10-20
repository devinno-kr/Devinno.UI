using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// align-items 기본 stretch(CSS 와 같음) — 크기를 안 준 위젯은 부모 폭으로 늘어난다. 모양이 정해진 위젯(노브·토글 등)은
// 그림이 가운데 자기 크기로 남고, 누르는 자리도 그 모양 안뿐이다(늘어난 상자의 빈 곳은 위젯이 아님).
public class StretchShapeTests
{
    private const int W = 400, H = 300;

    public sealed class Vm
    {
        public bool On { get; set; }
        public double Level { get; set; } = 40;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static (DvSurface S, LayoutBox Box) Start(string body, Vm vm, string tag)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:0\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, Flatten(s.Root!).First(b => b.Node.Tag == tag));
    }

    private static void Tap(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        s.Layout(W, H);
    }

    [Fact]
    public void Widgets_without_a_size_fill_the_parent_width_like_css()
    {
        var (_, input) = Start("<DvInput value=\"a\"/>", new Vm(), "DvInput");
        Assert.Equal(W, input.Rect.Width, 3);
        var (_, fixedInput) = Start("<DvInput value=\"a\" style=\"width:120\"/>", new Vm(), "DvInput");
        Assert.Equal(120, fixedInput.Rect.Width, 3);  // 크기를 주면 그대로
        var (_, start) = Start("<DvStack style=\"align-items:start\"><DvInput value=\"a\"/></DvStack>", new Vm(), "DvInput");
        Assert.Equal(200, start.Rect.Width, 3);       // 늘리지 않으면 기본 크기
    }

    [Fact]
    public void Stretched_toggle_draws_at_its_own_size_and_only_the_track_takes_taps()
    {
        var vm = new Vm();
        var (s, box) = Start("<DvToggle value=\"{Bind On}\"/>", vm, "DvToggle");
        Assert.Equal(W, box.Rect.Width, 3);
        Assert.Equal(new SKRect(178, 0, 222, 24), Devinno.UI.Painting.WidgetMetrics.ShapeRect(box)); // 44×24 가운데

        Tap(s, 20, 12);                 // 늘어난 상자의 왼쪽 빈 곳
        Assert.False(vm.On);
        Tap(s, 200, 12);                // 트랙
        Assert.True(vm.On);

        using var bmp = new SKBitmap(W, H);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, W, H, 0);
        Assert.NotEqual(bmp.GetPixel(200, 12), bmp.GetPixel(20, 12)); // 트랙은 가운데에만
    }

    [Fact]
    public void Stretched_knob_ignores_drags_beside_the_dial()
    {
        var vm = new Vm();
        var (s, box) = Start("<DvKnob value=\"{Bind Level}\"/>", vm, "DvKnob");
        Assert.Equal(W, box.Rect.Width, 3);
        var dial = Devinno.UI.Painting.WidgetMetrics.ShapeRect(box);
        Assert.Equal(96, dial.Width, 3);

        // 노브 왼쪽 빈 곳에서 위로 끌기 — 값 그대로
        s.PointerDown(new SKPoint(20, 48));
        for (var i = 1; i <= 10; i++)
            s.PointerMove(new SKPoint(20, 48 - i * 4));
        s.PointerUp(new SKPoint(20, 8));
        Assert.Equal(40, vm.Level);
        Assert.Same(box, Devinno.UI.Interaction.HitTester.HitTest(s.Root!, new SKPoint(dial.MidX, dial.MidY)));
        Assert.NotSame(box, Devinno.UI.Interaction.HitTester.HitTest(s.Root!, new SKPoint(20, 48)));
    }
}
