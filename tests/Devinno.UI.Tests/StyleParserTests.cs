using Devinno.UI.Styling;
using Xunit;

public class StyleParserTests
{
    [Fact]
    public void Parses_core_properties()
    {
        var s = StyleParser.Parse(
            "background:#fff; color:#222; border-radius:12; padding:8 16; font-size:18; font-weight:bold; display:flex; flex-direction:column; gap:10");

        Assert.True(s.Background.HasValue);
        Assert.Equal((byte)255, s.Background!.Value.R);
        Assert.Equal((byte)0x22, s.Color.R);
        Assert.Equal(12, s.BorderRadius);
        Assert.Equal(16, s.Padding.Left);
        Assert.Equal(18, s.FontSize);
        Assert.True(s.Bold);
        Assert.True(s.IsFlex);
        Assert.Equal(FlexDirection.Column, s.FlexDirection);
        Assert.Equal(10, s.Gap);
    }

    [Fact]
    public void Parses_border_and_shadow()
    {
        var s = StyleParser.Parse("border:2 solid #3A7BD5; box-shadow:0 4 12 #00000030");

        Assert.Equal(2, s.BorderWidth);
        Assert.True(s.BorderColor.HasValue);
        Assert.True(s.Shadow.HasValue);
        Assert.Equal(12, s.Shadow!.Value.Blur);
    }

    [Fact]
    public void Background_color_is_a_solid_alias_of_background_and_last_wins()
    {
        var a = StyleParser.Parse("background: linear-gradient(90deg, #ff0000, #0000ff); background-color: #00ff00");
        Assert.Equal(new DvColor(0, 0xff, 0, 0xff), a.Background);
        Assert.Null(a.Gradient);

        var b = StyleParser.Parse("background-color: #00ff00; background: #0000ff");
        Assert.Equal(new DvColor(0, 0, 0xff, 0xff), b.Background);

        // 그라디언트는 background-color 로 받지 않음(무시 — 이전 값 유지)
        var c = StyleParser.Parse("background: #0000ff; background-color: linear-gradient(90deg, #ff0000, #00ff00)");
        Assert.Equal(new DvColor(0, 0, 0xff, 0xff), c.Background);
        Assert.Null(c.Gradient);
    }
}
