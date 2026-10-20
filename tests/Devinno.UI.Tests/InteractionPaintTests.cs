using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using SkiaSharp;
using Xunit;

public class InteractionPaintTests
{
    [Fact]
    public void Hovered_box_paints_hover_background()
    {
        var root = MarkupParser.Parse(
            "<DvScreen style=\"background:#ffffff\">" +
            "<DvButton text=\"A\" style=\"width:50; height:50; background:#ff0000\" hover-style=\"background:#00ff00\"/>" +
            "</DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 100, 100);
        var btn = box.Children[0];
        var state = new InteractionState { Hover = btn };

        using var bmp = new SKBitmap(100, 100);
        using (var canvas = new SKCanvas(bmp))
            new Painter().Paint(canvas, box, null, state);

        Assert.Equal(new SKColor(0, 255, 0), bmp.GetPixel(10, 10));
    }
}
