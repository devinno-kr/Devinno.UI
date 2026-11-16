using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;
using Xunit;

// 숫자 굵기 — font-weight 100~900 이 실제 글꼴 면으로 그려진다(가짜 볼드는 Bold 면이 없는 글꼴에서만).
// 면 기대값은 Windows 기본 글꼴(Segoe UI 400/600/700, 맑은 고딕 400/700) 기준 — 그 글꼴이 없으면 해당 단언은 건너뛴다.
public class FontWeightTests
{
    private static bool SegoeIsDefault => SKTypeface.Default.FamilyName == "Segoe UI";

    [Theory]
    [InlineData("bold", 700)]
    [InlineData("normal", 400)]
    [InlineData("500", 500)]
    [InlineData("550", 600)]
    [InlineData("1000", 900)]
    [InlineData("-5", 100)]
    [InlineData("heavy", 400)]
    [InlineData("NaN", 400)]
    [InlineData("Infinity", 400)]
    public void Font_weight_parses_keywords_and_clamps_numbers(string raw, int expected)
        => Assert.Equal(expected, StyleParser.Parse($"font-weight:{raw}").FontWeight);

    [Fact]
    public void Bold_reads_true_from_600()
    {
        Assert.False(StyleParser.Parse("font-weight:500").Bold);
        Assert.True(StyleParser.Parse("font-weight:600").Bold);
        Assert.True(StyleParser.Parse("font-weight:bold").Bold);
    }

    [Fact]
    public void Bold_uses_a_real_bold_face_not_fake_bold()
    {
        if (!SegoeIsDefault) return;
        using var font = TextFont.Create("Ab", 14, 700);
        Assert.True(font.Typeface.FontWeight >= 700);
        Assert.False(font.Embolden);
    }

    [Fact]
    public void Semibold_request_picks_the_semibold_face_when_the_family_has_one()
    {
        if (!SegoeIsDefault) return;
        using var font = TextFont.Create("Ab", 14, 600);
        Assert.Equal(600, font.Typeface.FontWeight);
        Assert.False(font.Embolden);
    }

    [Fact]
    public void Fallback_typeface_keeps_the_requested_weight()
    {
        if (!SegoeIsDefault) return;
        using var font = TextFont.Create("한글", 14, 700);
        Assert.True(font.Typeface.ContainsGlyph('한'));
        Assert.True(font.Typeface.FontWeight >= 700);
        Assert.False(font.Embolden);
    }

    [Theory]
    [InlineData(700, 400, true)]   // Bold 면 없음 → 가짜 볼드
    [InlineData(600, 400, true)]
    [InlineData(500, 400, false)]  // 600 미만 요청은 가짜 볼드 안 함
    [InlineData(700, 600, false)]  // 가까운 굵은 면이 있으면 그대로
    [InlineData(700, 700, false)]
    public void Missing_weight_falls_back_to_nearest_face_and_emboldens_only_without_bold(int requested, int face, bool embolden)
        => Assert.Equal(embolden, TextFont.NeedsEmbolden(requested, face));

    [Fact]
    public void Heavier_weight_measures_wider()
    {
        using var regular = TextFont.Create("8888 Wide", 14, 400);
        using var bold = TextFont.Create("8888 Wide", 14, 700);
        Assert.True(bold.MeasureText("8888 Wide") > regular.MeasureText("8888 Wide"));
    }

    // 글자 위치가 정수 픽셀로 반올림되면(힌팅된 폭, 서브픽셀 위치 꺼짐) 크기에 비례하지 않고 "Latch o n" 처럼 간격이 들쭉날쭉해진다
    [Theory]
    [InlineData(400)]
    [InlineData(700)]
    public void Text_width_scales_linearly_with_size(int weight)
    {
        const string text = "Buttons Danger Latch on";
        using var small = TextFont.Create(text, 15, weight);
        using var large = TextFont.Create(text, 30, weight);
        Assert.True(small.Subpixel);
        Assert.Equal(large.MeasureText(text), small.MeasureText(text) * 2, 2);
    }

    [Fact]
    public void Label_layout_follows_font_weight()
    {
        static float Width(string weight)
        {
            var root = new LayoutEngine().Layout(MarkupParser.Parse(
                $"<DvBox style=\"display:flex; align-items:start\"><DvLabel text=\"Measure me 8888\" style=\"font-weight:{weight}\"/></DvBox>"), null, 800, 600);
            return root.Children[0].Rect.Width;
        }

        Assert.True(Width("700") > Width("400"));
    }
}
