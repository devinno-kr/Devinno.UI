using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using SkiaSharp;
using Xunit;

public class TogglePaintTests
{
    private sealed class Vm
    {
        public bool Flag { get; set; } = true;
    }

    [Fact]
    public void On_toggle_track_is_not_gray()
    {
        var root = MarkupParser.Parse(
            "<DvScreen style=\"background:#ffffff\"><DvToggle value=\"{Bind Data.Flag}\" style=\"width:44; height:24\"/></DvScreen>");
        var box = new LayoutEngine().Layout(root, new Vm(), 60, 40);

        using var bmp = new SKBitmap(60, 40);
        using (var canvas = new SKCanvas(bmp))
            new Painter().Paint(canvas, box, null);

        var px = bmp.GetPixel(4, 12);
        Assert.True(px.Red != 255 || px.Green != 255 || px.Blue != 255);
    }
}
