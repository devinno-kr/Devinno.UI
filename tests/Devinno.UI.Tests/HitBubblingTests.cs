using Devinno.UI;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using SkiaSharp;
using Xunit;

public class HitBubblingTests
{
    private sealed class Vm
    {
        public int Clicks;
        public void Go() => Clicks++;
    }

    // 버튼 박스(0,0,120,40) 안에 라벨(16,10,...) — 라벨 위 좌표 = (20,15)
    private const string Button =
        "<DvScreen><DvBox onclick=\"Go\" style=\"width:120; height:40; padding:10 16\" hover-style=\"background:#333333\">" +
        "<DvLabel text=\"운전 시작\"/></DvBox></DvScreen>";

    [Fact]
    public void Click_on_child_label_bubbles_to_onclick_container()
    {
        var vm = new Vm();
        var surface = new DvSurface(Button, vm);
        surface.Layout(300, 100);

        surface.PointerDown(new SKPoint(20, 15));
        surface.PointerUp(new SKPoint(20, 15));

        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void HitTarget_returns_nearest_interactive_ancestor()
    {
        var box = new LayoutEngine().Layout(MarkupParser.Parse(Button), null, 300, 100);

        Assert.Equal("DvLabel", HitTester.HitTest(box, new SKPoint(20, 15))!.Node.Tag);
        Assert.Equal("DvBox", HitTester.HitTarget(box, new SKPoint(20, 15))!.Node.Tag);
    }

    [Fact]
    public void HitTarget_falls_back_to_deepest_when_nothing_interactive()
    {
        var box = new LayoutEngine().Layout(
            MarkupParser.Parse("<DvScreen><DvBox style=\"width:100; height:40\"><DvLabel text=\"x\"/></DvBox></DvScreen>"),
            null, 300, 100);

        Assert.Equal("DvLabel", HitTester.HitTarget(box, new SKPoint(2, 2))!.Node.Tag);
        Assert.Null(HitTester.HitTarget(box, new SKPoint(-5, -5)));
    }

    [Fact]
    public void Press_on_label_and_release_on_container_padding_is_same_target()
    {
        var vm = new Vm();
        var surface = new DvSurface(Button, vm);
        surface.Layout(300, 100);

        surface.PointerDown(new SKPoint(20, 15));  // 라벨 위
        surface.PointerUp(new SKPoint(110, 35));   // 같은 버튼의 패딩 영역
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Hover_over_child_applies_container_hover_style()
    {
        var surface = new DvSurface(Button, null);
        using var bmp = new SKBitmap(300, 100);
        using var canvas = new SKCanvas(bmp);
        surface.RenderTo(canvas, 300, 100);
        var before = bmp.GetPixel(60, 30);

        surface.PointerMove(new SKPoint(20, 15));
        surface.RenderTo(canvas, 300, 100);

        Assert.NotEqual(before, bmp.GetPixel(60, 30));
        Assert.Equal(new SKColor(0x33, 0x33, 0x33), bmp.GetPixel(115, 35));
    }
}
