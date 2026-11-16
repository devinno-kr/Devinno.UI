using Devinno.UI;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 값+단위 붙이기, 슬라이더 값 글자와 손잡이 사이 여백, 비활성 = 컨트롤 전체를 한 번에 흐리게(CSS opacity) — 겹친 부분이 비치지 않는다.
public class SliderLabelAndDisabledTests
{
    private const int W = 400, H = 200;

    [Theory]
    [InlineData("40", "%", "40%")]
    [InlineData("20", "°C", "20°C")]
    [InlineData("90", "°", "90°")]
    [InlineData("3", "bar", "3 bar")]
    [InlineData("3", "", "3")]
    [InlineData("3", null, "3")]
    public void Unit_joins_without_a_space_for_percent_and_degree(string value, string? unit, string expected)
        => Assert.Equal(expected, Painter.WithUnit(value, unit));

    private static (DvSurface S, SKBitmap Bmp) Render(string body)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:20; align-items:start\">{body}</DvScreen>", new Vm(), DvTheme.Light);
        s.Layout(W, H);
        var bmp = new SKBitmap(W, H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        return (s, bmp);
    }

    private static LayoutBox First(DvSurface s, string tag) => Flatten(s.Root!).First(b => b.Node.Tag == tag);
    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Slider_value_text_keeps_a_clear_gap_above_the_thumb()
    {
        var (s, bmp) = Render("<DvSlider value=\"{Bind Pct}\" unit=\"%\" show-value=\"true\" style=\"width:300\"/>");
        using var _ = bmp;
        var at = SliderMetrics.PointAt(First(s, "DvSlider"), 40);
        var bg = bmp.GetPixel((int)at.X, (int)(at.Y - SliderMetrics.Thumb - 30));
        var top = (int)MathF.Floor(at.Y - SliderMetrics.Thumb);
        for (var y = top - 6; y < top - 1; y++)
            for (var x = (int)at.X - 20; x <= (int)at.X + 20; x++)
                Assert.True(bmp.GetPixel(x, y) == bg, $"text pixel at ({x},{y}) within 6px of the thumb");
    }

    // 비활성 그림 = 활성 그림을 opacity.disabled 로 배경 위에 한 번 얹은 것(겹친 손잡이·막대가 서로 비치지 않음)
    [Theory]
    [InlineData("<DvSlider value=\"{{Bind Half}}\" style=\"width:300\"{0}/>", "DvSlider")]
    [InlineData("<DvToggle value=\"{{Bind On}}\"{0}/>", "DvToggle")]
    [InlineData("<DvCheckBox value=\"{{Bind Off}}\" text=\"A\"{0}/>", "DvCheckBox")]
    public void Disabled_control_is_dimmed_as_one_group(string template, string tag)
    {
        var (on, onBmp) = Render(string.Format(template, ""));
        var (off, offBmp) = Render(string.Format(template, " disabled=\"true\""));
        using var _1 = onBmp;
        using var _2 = offBmp;
        var r = First(on, tag).Rect;
        if (tag == "DvCheckBox") r = SKRect.Create(r.Left, r.MidY - Painter.CheckSize / 2f, Painter.CheckSize, Painter.CheckSize);
        var alpha = ThemeResolver.Number(DvTheme.Light, "opacity.disabled");
        var bg = onBmp.GetPixel(2, 2);
        var worst = 0;
        for (var y = (int)r.Top; y < (int)r.Bottom; y++)
            for (var x = (int)r.Left; x < (int)r.Right; x++)
            {
                var c = onBmp.GetPixel(x, y);
                var d = offBmp.GetPixel(x, y);
                worst = Math.Max(worst, Math.Abs(d.Red - Mix(bg.Red, c.Red, alpha)));
                worst = Math.Max(worst, Math.Abs(d.Green - Mix(bg.Green, c.Green, alpha)));
                worst = Math.Max(worst, Math.Abs(d.Blue - Mix(bg.Blue, c.Blue, alpha)));
            }
        Assert.True(worst <= 3, $"{tag}: worst channel difference {worst}");
    }

    // 바인딩 = 편집 가능(값을 글자로 박으면 읽기 전용 점선이 생김)
    public sealed class Vm { public double Pct { get; set; } = 40; public double Half { get; set; } = 50; public bool On { get; set; } = true; public bool Off { get; set; } }

    private static int Mix(byte bg, byte fg, float a) => (int)MathF.Round(bg + (fg - bg) * a);
}
