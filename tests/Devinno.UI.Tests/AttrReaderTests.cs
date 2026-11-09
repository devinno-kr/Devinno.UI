using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// 속성 읽기 한 통로 — 코드가 덮어쓴 값이 있으면 그것(바인딩 풀이 없이 그대로), 없으면 마크업 값(바인딩 풀이).
// 식(onclick 등)은 Raw 로 읽고 덮어쓰기와 상관없다. 덮어쓰기도 "있음"으로 친다.
public class AttrReaderTests
{
    private sealed class Vm
    {
        public string Name { get; set; } = "vm";
        public int Count { get; set; } = 3;
    }

    private static ElementNode Node(string xml) => MarkupParser.Parse(xml);

    [Fact]
    public void NoOverride_ResolvesMarkup()
    {
        var r = new AttrReader(Node("<DvLabel text=\"{Name}!\" max=\"{Count}\"/>"), new Vm(), null);
        Assert.Equal("vm!", r.Text("text"));
        Assert.Equal(3, r.Value("max"));
        Assert.True(r.Has("text"));
        Assert.False(r.Has("min"));
        Assert.Null(r.Value("min"));
    }

    [Fact]
    public void Override_WinsAndIsLiteral()
    {
        var overrides = new Dictionary<string, object?> { ["text"] = "{Name}", ["max"] = 9 };
        var r = new AttrReader(Node("<DvLabel text=\"{Name}!\" max=\"{Count}\"/>"), new Vm(), overrides);
        Assert.Equal("{Name}", r.Text("text")); // 코드 값은 바인딩으로 풀지 않음
        Assert.Equal(9, r.Value("max"));
    }

    [Fact]
    public void Override_CountsAsPresent()
    {
        var r = new AttrReader(Node("<DvLabel/>"), new Vm(), new Dictionary<string, object?> { ["visible"] = false });
        Assert.True(r.Has("visible"));
        Assert.Equal(false, r.Value("visible"));
        Assert.Null(r.Raw("visible")); // 식은 마크업에서만
    }

    [Fact]
    public void Raw_IgnoresOverrides()
    {
        var r = new AttrReader(Node("<DvButton onclick=\"Go\"/>"), new Vm(), new Dictionary<string, object?> { ["onclick"] = "Other" });
        Assert.Equal("Go", r.Raw("onclick"));
    }
}
