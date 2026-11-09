using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// visible·disabled 의 참/거짓 해석 — 대소문자·앞뒤 공백 무시, disabled 는 "1" 도 참.
// 바인딩 값이 bool 이 아닐 때(null, 빈 문자열, 숫자, 다른 글자)의 기존 동작은 그대로.
public class BoolAttributeTests
{
    private sealed class Vm
    {
        public string? Nothing { get; set; }
        public string Name { get; set; } = "abc";
        public bool Off { get; set; }
        public int Zero { get; set; }
        public int One { get; set; } = 1;
    }

    private static LayoutBox Build(string body) =>
        new LayoutEngine().Layout(MarkupParser.Parse($"<DvScreen>{body}</DvScreen>"), new Vm(), 400, 300);

    private static LayoutBox? Find(LayoutBox b, string tag) =>
        b.Node.Tag == tag ? b : b.Children.Select(c => Find(c, tag)).FirstOrDefault(x => x is not null);

    [Theory]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData(" false ")]
    [InlineData(" 0 ")]
    public void Visible_FalseAnyCase_Hides(string raw) =>
        Assert.Null(Find(Build($"<DvLabel text=\"x\" visible=\"{raw}\"/>"), "DvLabel"));

    [Theory]
    [InlineData("{Nothing}")]   // null → 숨김
    [InlineData("")]            // 빈 값 → 숨김
    [InlineData("{Off}")]       // bool false
    public void Visible_FalsyValues_Hide(string raw) =>
        Assert.Null(Find(Build($"<DvLabel text=\"x\" visible=\"{raw}\"/>"), "DvLabel"));

    [Theory]
    [InlineData("{Name}")]      // 다른 글자 → 보임
    [InlineData("true")]
    [InlineData("{One}")]
    public void Visible_TruthyValues_Show(string raw) =>
        Assert.NotNull(Find(Build($"<DvLabel text=\"x\" visible=\"{raw}\"/>"), "DvLabel"));

    [Theory]
    [InlineData("1")]
    [InlineData("TRUE")]
    [InlineData(" true ")]
    [InlineData("{One}")]
    public void Disabled_OneOrTrueAnyCase_Disables(string raw) =>
        Assert.True(Find(Build($"<DvButton text=\"x\" disabled=\"{raw}\"/>"), "DvButton")!.Disabled);

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("{Off}")]
    [InlineData("{Zero}")]
    [InlineData("{Nothing}")]
    [InlineData("{Name}")]      // 다른 글자 → 활성 (지금 동작 유지)
    public void Disabled_OtherValues_Enabled(string raw) =>
        Assert.False(Find(Build($"<DvButton text=\"x\" disabled=\"{raw}\"/>"), "DvButton")!.Disabled);
}
