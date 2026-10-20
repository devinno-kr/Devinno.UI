using Devinno.UI.Styling;
using Xunit;

public class ThemeTests
{
    [Fact]
    public void Resolves_var_tokens_before_style_parse()
    {
        var resolved = ThemeResolver.ResolveVars(
            "background:var(--bg); color:var(--text); border:1 solid var(--border)",
            DvTheme.Dark);

        var style = StyleParser.Parse(resolved);

        Assert.True(style.Background.HasValue);
        Assert.Equal(0x0f, style.Background.Value.R);
        Assert.Equal(0x17, style.Background.Value.G);
        Assert.Equal(0x2a, style.Background.Value.B);
        Assert.Equal(0xf8, style.Color.R);
        Assert.True(style.BorderColor.HasValue);
        Assert.Equal(0x33, style.BorderColor.Value.R);
    }

    [Fact]
    public void With_dotted_token_also_updates_dash_alias()
    {
        var t = DvTheme.Dark.With(new Dictionary<string, string> { ["spacing.md"] = "20" });

        Assert.True(t.TryGet("spacing.md", out var dotted));
        Assert.True(t.TryGet("spacing-md", out var dashed));
        Assert.Equal("20", dotted);
        Assert.Equal("20", dashed);
        Assert.Equal("20", ThemeResolver.ResolveVars("var(--spacing-md)", t));
    }
}
