using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 질감 스위치(look.texture) — flat 이면 광택·그라데이션·그림자·글로우 없이 단색으로. rich(내장 테마의 지금 모습)는 그대로.
public class TextureSwitchTests
{
    private static readonly DvTheme Flat = DvTheme.Dark.With(new Dictionary<string, string> { ["look.texture"] = "flat" });

    private static SKColor Pixel(byte[] png, int x, int y)
    {
        using var bmp = SKBitmap.Decode(png);
        return bmp.GetPixel(x, y);
    }

    private static bool Near(SKColor a, SKColor b, int tol = 3) =>
        Math.Abs(a.Red - b.Red) <= tol && Math.Abs(a.Green - b.Green) <= tol && Math.Abs(a.Blue - b.Blue) <= tol;

    private const string Lamp = "<DvScreen><DvLamp on=\"true\" state=\"run\" style=\"width:40; height:40\"/></DvScreen>";

    [Fact]
    public void Flat_lamp_has_no_highlight()
    {
        // 하이라이트 원 중심 = (cx − 0.3r, cy − 0.3r), r = 19
        var run = ThemeResolver.Color(Flat, "state-run");
        Assert.True(Near(run, Pixel(DvRenderer.RenderToPng(Lamp, null, 40, 40, Flat), 14, 14)));
        Assert.False(Near(run, Pixel(DvRenderer.RenderToPng(Lamp, null, 40, 40, DvTheme.Dark), 14, 14)));
    }

    [Fact]
    public void Flat_progress_fill_is_one_color()
    {
        const string bar = "<DvScreen><DvProgress value=\"100\" style=\"width:200; height:20\"/></DvScreen>";
        var flat = DvRenderer.RenderToPng(bar, null, 200, 20, Flat);
        Assert.True(Near(Pixel(flat, 20, 10), Pixel(flat, 180, 10)));
        var rich = DvRenderer.RenderToPng(bar, null, 200, 20, DvTheme.Dark);
        Assert.False(Near(Pixel(rich, 20, 10), Pixel(rich, 180, 10)));
    }

    [Fact]
    public void Flat_tank_liquid_is_one_color()
    {
        const string tank = "<DvScreen><DvTank value=\"100\" show-value=\"false\" style=\"width:80; height:160\"/></DvScreen>";
        var flat = DvRenderer.RenderToPng(tank, null, 80, 160, Flat);
        Assert.True(Near(Pixel(flat, 40, 30), Pixel(flat, 40, 130)));
        var rich = DvRenderer.RenderToPng(tank, null, 80, 160, DvTheme.Dark);
        Assert.False(Near(Pixel(rich, 40, 30), Pixel(rich, 40, 130)));
    }

    [Theory]
    [InlineData("<DvScreen><DvKnob value=\"50\" min=\"0\" max=\"100\" style=\"width:110; height:110\"/></DvScreen>")]
    [InlineData("<DvScreen><DvRing value=\"70\" style=\"width:110; height:110\"/></DvScreen>")]
    [InlineData("<DvScreen><DvGauge value=\"5\" min=\"0\" max=\"10\" style=\"width:120; height:120\"/></DvScreen>")]
    public void Flat_instrument_differs_from_rich(string markup)
    {
        // 그림자·결·글로우가 빠지므로 그림이 달라진다(내장 테마 = rich 는 지금 모습)
        Assert.NotEqual(DvRenderer.RenderToPng(markup, null, 120, 120, DvTheme.Dark), DvRenderer.RenderToPng(markup, null, 120, 120, Flat));
    }
}
