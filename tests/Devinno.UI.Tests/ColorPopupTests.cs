using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// P8d-7 색 팝업 — 사각형·막대 끌기, 확인 커밋(string·SKColor 알파), 취소
public class ColorPopupTests
{
    public sealed class Vm
    {
        private string _pen = "#0000FF";
        public int Writes;
        public string Pen { get => _pen; set { _pen = value; Writes++; } }
        public SKColor Fill { get; set; } = new(0, 0, 255, 0x80);
    }

    private const float W = 800, H = 600;

    private static DvSurface Surface(Vm vm)
    {
        var s = new DvSurface("""
            <DvScreen style="padding:0; gap:0">
              <DvColorInput id="pen" value="{Bind Data.Pen}"/>
              <DvColorInput id="fill" value="{Bind Data.Fill}"/>
            </DvScreen>
            """, vm) { UseNativeEditor = true };
        s.Layout(W, H);
        return s;
    }

    private static void Tap(DvSurface s, SKPoint p) { s.PointerDown(p); s.PointerUp(p); s.Layout(W, H); }
    private static SKPoint Mid(SKRect r) => new(r.MidX, r.MidY);
    private static LayoutBox Box(DvSurface s, string id)
        => Flatten(s.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);
    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static ColorPopup Open(DvSurface s, string id)
    {
        Tap(s, Mid(Box(s, id).Rect));
        return Assert.IsType<ColorPopup>(s.TopPopup);
    }

    [Fact]
    public void Opens_with_value_hsv()
    {
        var s = Surface(new Vm());
        var p = Open(s, "pen");
        Assert.Equal(240, p.State.H, 3);
        Assert.Equal(new SKSize(ColorPopup.Width, p.Bounds.Height), p.Bounds.Size);
    }

    [Fact]
    public void Dragging_square_updates_sv_and_clamps_outside()
    {
        var s = Surface(new Vm());
        var p = Open(s, "pen");
        var sv = p.SvRect;
        s.PointerDown(new SKPoint(sv.Left + sv.Width / 2, sv.Top + sv.Height / 4));
        Assert.Equal(0.5, p.State.S, 2);
        Assert.Equal(0.75, p.State.V, 2);
        s.PointerMove(new SKPoint(sv.Right + 50, sv.Top - 50)); // 영역 밖 = 가장자리
        Assert.Equal((1d, 1d), (Math.Round(p.State.S, 3), Math.Round(p.State.V, 3)));
        s.PointerUp(new SKPoint(sv.Right + 50, sv.Top - 50));
        Assert.Same(p, s.TopPopup); // 팝업 밖에서 떼도 닫히지 않음
    }

    [Fact]
    public void Dragging_bar_updates_hue_only()
    {
        var s = Surface(new Vm());
        var p = Open(s, "pen");
        var (sv, v) = (p.State.S, p.State.V);
        var bar = p.BarRect;
        s.PointerDown(new SKPoint(bar.MidX, bar.Top));
        s.PointerMove(new SKPoint(bar.MidX, bar.Top + bar.Height / 2));
        s.PointerUp(new SKPoint(bar.MidX, bar.Top + bar.Height / 2));
        Assert.Equal(180, p.State.H, 0);
        Assert.Equal((sv, v), (p.State.S, p.State.V));
    }

    [Fact]
    public void Ok_writes_hex_string()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "pen");
        s.PointerDown(new SKPoint(p.BarRect.MidX, p.BarRect.Top));   // H 0 = 빨강
        s.PointerUp(new SKPoint(p.BarRect.MidX, p.BarRect.Top));
        Tap(s, Mid(p.OkRect));
        Assert.Equal("#FF0000", vm.Pen);
        Assert.Equal(1, vm.Writes);
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Ok_keeps_skcolor_alpha()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "fill");
        s.PointerDown(new SKPoint(p.BarRect.MidX, p.BarRect.Top));
        s.PointerUp(new SKPoint(p.BarRect.MidX, p.BarRect.Top));
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new SKColor(255, 0, 0, 0x80), vm.Fill);
    }

    [Fact]
    public void Unchanged_ok_and_cancel_write_nothing()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "pen");
        Tap(s, Mid(p.OkRect));
        Assert.Equal(0, vm.Writes);
        p = Open(s, "pen");
        s.PointerDown(new SKPoint(p.BarRect.MidX, p.BarRect.Top));
        s.PointerUp(new SKPoint(p.BarRect.MidX, p.BarRect.Top));
        Tap(s, Mid(p.CancelRect));
        Assert.Equal(0, vm.Writes);
        Assert.Equal("#0000FF", vm.Pen);
    }
}
