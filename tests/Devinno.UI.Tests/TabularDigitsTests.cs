using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;
using Xunit;

// 고정폭 숫자 — font-variant-numeric: tabular-nums 를 켠 글자는 숫자(0~9)마다 같은 칸 폭(그 글꼴의 가장 넓은 숫자)으로 그린다.
// 실시간 값이 바뀔 때 글자 폭이 흔들리지 않게. 숫자가 비례폭인 글꼴(Georgia)로 확인한다 — 없으면 건너뛴다.
public class TabularDigitsTests
{
    private const string Proportional = "Georgia";
    private static bool HasProportional => SKFontManager.Default.MatchFamily(Proportional) is not null;

    [Fact]
    public void Every_digit_has_the_same_advance()
    {
        if (!HasProportional) return;
        using var f = TextFont.Create("1111", 20, 400, Proportional);
        Assert.Equal(TextFont.MeasureNumber("8888", f), TextFont.MeasureNumber("1111", f), 2);
    }

    [Fact]
    public void Non_digits_keep_their_own_width()
    {
        if (!HasProportional) return;
        using var f = TextFont.Create("1,1.1", 20, 400, Proportional);
        Assert.Equal(TextFont.MeasureNumber("8,8.8", f), TextFont.MeasureNumber("1,1.1", f), 2);
        // 숫자가 아닌 글자는 원래 폭: "ab" 는 그대로
        Assert.Equal(f.MeasureText("ab"), TextFont.MeasureNumber("ab", f), 2);
    }

    [Fact]
    public void Style_key_turns_tabular_digits_on()
    {
        Assert.True(StyleParser.Parse("font-variant-numeric: tabular-nums").TabularNums);
        Assert.False(StyleParser.Parse("font-variant-numeric: normal").TabularNums);
        Assert.False(StyleParser.Parse("color:#fff").TabularNums);
    }

    [Fact]
    public void Label_with_tabular_style_uses_tabular_width()
    {
        if (!HasProportional) return;
        static float Width(string text)
        {
            var root = new LayoutEngine().Layout(MarkupParser.Parse(
                $"<DvBox style=\"display:flex; align-items:start\"><DvLabel text=\"{text}\" style=\"font-family:{Proportional}; font-size:20; font-variant-numeric:tabular-nums\"/></DvBox>"), null, 800, 600);
            return root.Children[0].Rect.Width;
        }

        Assert.Equal(Width("8888"), Width("1111"), 2);
    }

    [Fact]
    public void Korean_and_units_around_numbers_still_render()
    {
        var png = DvRenderer.RenderToPng("<DvScreen><DvLabel text=\"72.5 ℃ 정상\" style=\"font-variant-numeric:tabular-nums\"/></DvScreen>", null, 200, 40);
        Assert.NotEmpty(png);
    }
}
