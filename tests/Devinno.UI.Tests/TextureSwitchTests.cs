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
    [InlineData("<DvScreen><DvRing value=\"70\" style=\"width:110; height:110\"/></DvScreen>")]
    [InlineData("<DvScreen><DvGauge value=\"5\" min=\"0\" max=\"10\" style=\"width:120; height:120\"/></DvScreen>")]
    public void Flat_instrument_differs_from_rich(string markup)
    {
        // 그림자·결·글로우가 빠지므로 그림이 달라진다(내장 테마 = rich 는 지금 모습)
        Assert.NotEqual(DvRenderer.RenderToPng(markup, null, 120, 120, DvTheme.Dark), DvRenderer.RenderToPng(markup, null, 120, 120, Flat));
    }
    // 음영 깊이(texture.depth) — rich 그대로여도 0 이면 음영이 없다(같은 줄의 왼쪽·가운데·오른쪽이 같은 색)
    [Theory]
    [InlineData("<DvScreen><DvTank value=\"100\" style=\"width:80; height:160\"/></DvScreen>", 80, 160, 12, 40, 30)]
    [InlineData("<DvScreen style=\"padding:0\"><DvProcess look=\"rich\" style=\"width:300; height:240\"><DvVessel name=\"T1\" level=\"0\"/></DvProcess></DvScreen>", 300, 240, 0, 0, 0)]
    public void Depth_zero_draws_rich_texture_without_shading(string markup, int w, int h, int x0, int x1, int y)
    {
        var none = DvTheme.Dark.With(new Dictionary<string, string> { ["texture.depth"] = "0" });
        var flatPng = DvRenderer.RenderToPng(markup, null, w, h, none);
        var deepPng = DvRenderer.RenderToPng(markup, null, w, h, DvTheme.Dark);
        if (y == 0) // 공정도 — 용기 몸통 가운데 가로줄에서 몸통 픽셀(배경과 다른 것)의 색 수
        {
            int Colors(byte[] png)
            {
                using var bmp = SKBitmap.Decode(png);
                var bg = bmp.GetPixel(2, 2);
                return Enumerable.Range(0, w).Select(x => bmp.GetPixel(x, h / 2)).Where(c => !Near(c, bg, 6)).Select(c => (uint)c).Distinct().Count();
            }
            Assert.True(Colors(flatPng) < 8, $"depth 0 body colors {Colors(flatPng)}");
            Assert.True(Colors(deepPng) > 20, $"depth 1 body colors {Colors(deepPng)}");
            return;
        }
        Assert.True(Near(Pixel(flatPng, x0, y), Pixel(flatPng, x1, y), 2), "depth 0 tank row is one color");
        Assert.False(Near(Pixel(deepPng, x0, y), Pixel(deepPng, x1, y), 2), "depth 1 tank row is shaded");
    }

    // 노브는 예외 — 돌리는 손잡이라는 단서(떠 보이는 금속 면·그림자)를 질감 스위치와 무관하게 늘 그린다
    [Fact]
    public void Knob_keeps_its_raised_face_with_flat_texture()
    {
        const string knob = "<DvScreen><DvKnob value=\"50\" min=\"0\" max=\"100\" style=\"width:110; height:110\"/></DvScreen>";
        Assert.Equal(DvRenderer.RenderToPng(knob, null, 120, 120, DvTheme.Dark), DvRenderer.RenderToPng(knob, null, 120, 120, Flat));
    }
}