using Devinno.UI;
using Devinno.UI.Styling;
using Xunit;

// border-width·border-color(CSS 와 같은 한 쪽 키)와 border-style dotted — border 단축과 같은 칸에 쓴다(나중 선언이 이김)
public class BorderKeyTests
{
    private static DvColor C(string hex) => DvColor.TryParse(hex, out var c) ? c : throw new ArgumentException(hex);

    [Fact]
    public void Width_and_color_keys_set_the_border()
    {
        var s = StyleParser.Parse("border-width:3; border-color:#ff0000");
        Assert.Equal(3, s.BorderWidth);
        Assert.Equal(C("#ff0000"), s.BorderColor);
    }

    [Fact]
    public void Later_declaration_wins_between_shorthand_and_longhand()
    {
        var a = StyleParser.Parse("border:1 solid #000000; border-color:#00ff00; border-width:4");
        Assert.Equal(4, a.BorderWidth);
        Assert.Equal(C("#00ff00"), a.BorderColor);
        var b = StyleParser.Parse("border-color:#00ff00; border:2 solid #000000");
        Assert.Equal(C("#000000"), b.BorderColor);
    }

    [Fact]
    public void Unreadable_color_keeps_the_previous_one()
    {
        var s = StyleParser.Parse("border:1 solid #000000; border-color:nope");
        Assert.Equal(C("#000000"), s.BorderColor);
    }

    [Theory]
    [InlineData("border-style:dotted", BorderLine.Dotted)]
    [InlineData("border-style:dashed", BorderLine.Dashed)]
    [InlineData("border-style:solid", BorderLine.Solid)]
    [InlineData("border-style:wavy", BorderLine.Solid)]
    [InlineData("border:2 dotted #000000", BorderLine.Dotted)]
    [InlineData("border:2 dashed #000000", BorderLine.Dashed)]
    [InlineData("border-style:dotted; border:2 solid #000000", BorderLine.Solid)]
    public void Border_style_words(string css, BorderLine expected)
        => Assert.Equal(expected, StyleParser.Parse(css).BorderStyle);

    [Fact]
    public void Dotted_constant_parses()
        => Assert.Equal(BorderLine.Dotted, StyleParser.Parse("border-style:" + Styles.BorderStyle.Dotted).BorderStyle);
}
