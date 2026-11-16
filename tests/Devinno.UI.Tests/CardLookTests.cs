using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 카드·KPI·상태 항목(단계 4): 그림자는 질감 rich 에서만(S1), KPI 위쪽 줄은 단색, 상태 항목의 색 표시는 램프 하나.
public class CardLookTests
{
    private static SKBitmap Render(string body, DvTheme theme, int w = 300, int h = 160)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:20; align-items:start\">{body}</DvScreen>", null, theme);
        s.Layout(w, h);
        var bmp = new SKBitmap(w, h);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, w, h);
        return bmp;
    }

    private static DvTheme Flat => DvTheme.Light.With(new Dictionary<string, string> { ["look.texture"] = "flat" });

    // 카드 아래 4px — rich 는 그림자, flat 은 화면 배경
    [Theory]
    [InlineData("<DvCard title=\"T\" style=\"width:200; height:80\"/>")]
    [InlineData("<DvStat label=\"L\" value=\"1\" style=\"width:200; height:80\"/>")]
    public void Shadows_are_drawn_only_with_rich_texture(string body)
    {
        using var rich = Render(body, DvTheme.Light);
        using var flat = Render(body, Flat);
        var bg = flat.GetPixel(290, 150);
        Assert.NotEqual(bg, rich.GetPixel(120, 20 + 80 + 4));
        Assert.Equal(bg, flat.GetPixel(120, 20 + 80 + 4));
    }

    [Fact]
    public void Kpi_accent_line_is_one_color()
    {
        using var bmp = Render("<DvStat label=\"L\" value=\"1\" tone=\"success\" style=\"width:200; height:80\"/>", DvTheme.Light);
        // 위쪽 줄(테두리 1 안쪽 3px) 가운데 행 — 둥근 모서리 클립을 피해 안쪽 두 점
        Assert.Equal(bmp.GetPixel(60, 22), bmp.GetPixel(180, 22));
        Assert.Equal(ThemeResolver.Color(DvTheme.Light, "tone-success"), bmp.GetPixel(120, 22));
    }

    [Fact]
    public void Status_text_has_no_colored_chip()
    {
        using var bmp = Render("<DvStatusItem label=\"Pump\" state=\"run\" status=\"Running\" style=\"width:260\"/>", DvTheme.Light);
        // 상태 글자 자리(오른쪽)에 색 칩이 없다 — 회색 계열(흐린 글자·배경)만
        for (var x = 200; x < 280; x++)
            for (var y = 24; y < 52; y++)
            {
                var c = bmp.GetPixel(x, y);
                Assert.True(c.Green - c.Red < 30, $"colored pixel {c} at ({x},{y})");
            }
    }
}
