using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

public class ApiConsistencyTests
{
    private static SKColor LampCenter(string attrs, DvTheme? theme = null)
    {
        var box = new LayoutEngine(theme).Layout(
            MarkupParser.Parse($"<DvLamp on=\"true\" {attrs} style=\"width:40; height:40; color:#0000ff\"/>"), null, 40, 40);
        using var bmp = new SKBitmap(40, 40);
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.White);
        new Painter(theme).Paint(c, box, null);
        return bmp.GetPixel(30, 30); // 하이라이트(좌상) 피한 지점
    }

    [Fact]
    public void Lamp_color_attribute_and_state_override_style_color()
    {
        var styleOnly = LampCenter("");
        Assert.True(styleOnly.Blue > 200 && styleOnly.Red < 60, $"style={styleOnly}");

        var byState = LampCenter("state=\"fault\"", DvTheme.Dark);
        Assert.True(byState.Red > 200 && byState.Green < 100, $"state={byState}");

        var byColor = LampCenter("state=\"fault\" color=\"#00ff00\"");
        Assert.True(byColor.Green > 200 && byColor.Red < 60, $"color={byColor}");
    }

    [Fact]
    public void Lamp_state_without_theme_uses_light_state_colors()
    {
        var run = LampCenter("state=\"run\"");
        Assert.True(run.Green > run.Red && run.Green > run.Blue, $"run={run}");
    }
}
