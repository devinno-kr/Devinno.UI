using Devinno.UI.Text;
using Xunit;

public class TextFontTests
{
    [Theory]
    [InlineData("ISA-18.2 MVP · 배너 클릭")] // 첫 비-ASCII 가 '·'
    [InlineData("72°C 정상")]               // 첫 비-ASCII 가 '°'
    [InlineData("확인 23:06")]
    public void Mixed_text_picks_a_typeface_that_has_every_glyph(string text)
    {
        var tf = TextFont.ResolveTypeface(text);
        Assert.All(text.Where(c => !char.IsWhiteSpace(c)), c => Assert.True(tf.ContainsGlyph(c), $"'{c}' missing in {tf.FamilyName}"));
    }

    [Fact]
    public void Ascii_and_default_covered_symbols_keep_default_typeface()
    {
        Assert.Equal(SkiaSharp.SKTypeface.Default.FamilyName, TextFont.ResolveTypeface("72°C").FamilyName);
        Assert.Equal(SkiaSharp.SKTypeface.Default.FamilyName, TextFont.ResolveTypeface("PLC1 OK").FamilyName);
    }
}
