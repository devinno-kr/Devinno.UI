using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using SkiaSharp;
using Xunit;

public class HitTesterTests
{
    [Fact]
    public void Returns_deepest_box_containing_point()
    {
        var root = MarkupParser.Parse(
            "<DvScreen style=\"padding:10; display:flex; flex-direction:column; gap:8\">" +
            "<DvButton text=\"A\" style=\"width:80; height:30\" onclick=\"X\"/>" +
            "</DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 200, 200);

        var hit = HitTester.HitTest(box, new SKPoint(20, 20));
        Assert.NotNull(hit);
        Assert.Equal("DvButton", hit!.Node.Tag);

        var miss = HitTester.HitTest(box, new SKPoint(150, 150));
        Assert.Equal("DvScreen", miss!.Node.Tag);
    }
}
