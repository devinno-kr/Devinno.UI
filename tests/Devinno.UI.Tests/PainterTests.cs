using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using SkiaSharp;
using Xunit;

public class PainterTests
{
    [Fact]
    public void Paints_background_color_at_box_rect()
    {
        var root = MarkupParser.Parse(
            "<DvScreen style=\"background:#ffffff\">" +
            "<DvBox style=\"width:50; height:50; background:#ff0000\"/>" +
            "</DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 100, 100);

        using var bmp = new SKBitmap(100, 100);
        using (var canvas = new SKCanvas(bmp))
            new Painter().Paint(canvas, box, null);

        Assert.Equal(new SKColor(255, 255, 255), bmp.GetPixel(60, 60));
        Assert.Equal(new SKColor(255, 0, 0), bmp.GetPixel(10, 10));
    }

    // 테마 null = DvTheme.Default — 위젯 내부 색(토글·게이지·탱크·노브·경보·그리드)도 Default 와 같아야 함
    [Fact]
    public void Null_theme_paints_widgets_like_default_theme()
    {
        var root = MarkupParser.Parse(
            "<DvScreen style=\"padding:0\">" +
            "<DvToggle style=\"width:44; height:24\"/>" +
            "<DvGauge value=\"40\" style=\"width:120; height:120\"/>" +
            "<DvTank value=\"40\" style=\"width:60; height:100\"/>" +
            "<DvKnob value=\"40\" style=\"width:100; height:100\"/>" +
            "<DvAlarmBanner style=\"width:300; height:40\"/>" +
            "<DvProgress value=\"40\" tone=\"warning\" style=\"width:200; height:20\"/>" +
            "</DvScreen>");

        SKBitmap Draw(Devinno.UI.Styling.DvTheme? theme)
        {
            var box = new LayoutEngine(theme).Layout(root, null, 400, 700);
            var bmp = new SKBitmap(400, 700);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.Black);
            new Painter(theme).Paint(canvas, box, null);
            return bmp;
        }

        using var a = Draw(null);
        using var b = Draw(Devinno.UI.Styling.DvTheme.Default);
        Assert.Equal(b.Pixels, a.Pixels);
    }
}
