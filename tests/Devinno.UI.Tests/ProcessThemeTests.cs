using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 공정도 색은 테마 토큰(process-*)을 따르고, look 이 없으면 테마 질감(look.texture)을 따른다(단계 5b).
public class ProcessThemeTests
{
    private const string Body = "<DvVessel name=\"T1\" level=\"60\"/><DvPump name=\"P1\" running=\"true\"/>" +
                                "<DvTerminal name=\"A\" text=\"A\"/><DvTerminal name=\"B\" text=\"B\"/><DvLine from=\"A\" to=\"B\" medium=\"water\"/>";

    private static byte[] Png(string attrs, DvTheme theme)
        => DvRenderer.RenderToPng($"<DvScreen style=\"padding:0\"><DvProcess {attrs} style=\"width:400; height:240\">{Body}</DvProcess></DvScreen>", null, 400, 240, theme);

    private static int Count(byte[] png, SKColor c, int tol = 24)
    {
        using var bmp = SKBitmap.Decode(png);
        var n = 0;
        for (var y = 0; y < bmp.Height; y++)
            for (var x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (Math.Abs(p.Red - c.Red) <= tol && Math.Abs(p.Green - c.Green) <= tol && Math.Abs(p.Blue - c.Blue) <= tol)
                    n++;
            }
        return n;
    }

    [Theory]
    [InlineData("process-line")]
    [InlineData("process-body")]
    [InlineData("process-pipe-water")]
    public void Process_colors_come_from_theme_tokens(string token)
    {
        var probe = new SKColor(0x00, 0xc8, 0x00);
        var theme = DvTheme.Light.With(new Dictionary<string, string> { [token] = "#00c800" });
        Assert.Equal(0, Count(Png("look=\"plain\"", DvTheme.Light), probe));
        Assert.True(Count(Png("look=\"plain\"", theme), probe) > 10, token);
    }

    [Fact]
    public void Without_look_the_diagram_follows_the_theme_texture()
    {
        var flat = DvTheme.Light.With(new Dictionary<string, string> { ["look.texture"] = "flat" });
        Assert.Equal(Png("look=\"plain\"", flat), Png("", flat));
        Assert.Equal(Png("look=\"rich\"", DvTheme.Light), Png("", DvTheme.Light));
    }

    [Fact]
    public void Look_attribute_overrides_the_theme()
    {
        Assert.NotEqual(Png("look=\"plain\"", DvTheme.Light), Png("", DvTheme.Light));
    }
}
