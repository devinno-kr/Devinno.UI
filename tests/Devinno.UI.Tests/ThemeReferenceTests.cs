using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 토큰 참조 — 테마 값이 다른 토큰을 가리킬 수 있다(gauge-track = token(surface-raised)).
// 복사한 테마에서 기준 토큰만 바꿔도 그것을 가리키는 위젯 색이 따라온다. 순환·없는 대상은 마젠타 + 경고.
public class ThemeReferenceTests
{
    private static DvTheme With(params (string Key, string Value)[] overrides)
        => DvTheme.Dark.With(overrides.ToDictionary(o => o.Key, o => o.Value));

    private static readonly SKColor Magenta = new(0xff, 0x00, 0xff);

    [Fact]
    public void Theme_value_can_reference_another_token()
    {
        var t = With(("gauge-track", "token(primary)"));
        Assert.Equal(ThemeResolver.Color(t, "primary"), ThemeResolver.Color(t, "gauge-track"));
    }

    [Fact]
    public void Reference_chain_resolves()
    {
        var t = With(("x-a", "token(x-b)"), ("x-b", "token(primary)"));
        Assert.Equal(ThemeResolver.Color(t, "primary"), ThemeResolver.Color(t, "x-a"));
    }

    [Fact]
    public void Var_alias_reference_resolves()
    {
        var t = With(("x-a", "var(--primary)"));
        Assert.Equal(ThemeResolver.Color(t, "primary"), ThemeResolver.Color(t, "x-a"));
    }

    [Fact]
    public void Markup_token_resolves_through_reference()
    {
        var t = With(("x-a", "token(primary)"));
        Assert.True(t.TryGet("primary", out var primary));
        Assert.Equal($"background: {primary}", ThemeResolver.ResolveVars("background: token(x-a)", t));
    }

    [Fact]
    public void Changing_the_base_token_moves_its_dependents()
    {
        var t = With(("gauge-track", "token(primary)")).With(new Dictionary<string, string> { ["primary"] = "#ff0000" });
        Assert.Equal(new SKColor(0xff, 0x00, 0x00), ThemeResolver.Color(t, "gauge-track"));
    }

    [Fact]
    public void Reference_to_a_missing_token_draws_magenta_with_a_warning()
    {
        var t = With(("x-a", "token(x-nowhere)"));
        var (color, warnings) = Watch("x-nowhere", () => ThemeResolver.Color(t, "x-a"));
        Assert.Equal(Magenta, color);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void Reference_cycle_draws_magenta_with_a_warning()
    {
        var t = With(("x-a", "token(x-b)"), ("x-b", "token(x-a)"), ("x-self", "token(x-self)"));
        var (color, warnings) = Watch("x-a", () => ThemeResolver.Color(t, "x-a"));
        Assert.Equal(Magenta, color);
        Assert.NotEmpty(warnings);
        Assert.Equal(Magenta, ThemeResolver.Color(t, "x-self"));
    }

    [Fact]
    public void Value_with_text_around_a_reference_is_not_followed()
    {
        var t = With(("x-shadow", "0 2 6 token(primary)"));
        Assert.True(t.TryResolve("x-shadow", out var value));
        Assert.Equal("0 2 6 token(primary)", value);
    }

    // 경고는 Interaction.Warnings(화면마다 한 번) 또는 Trace 로 나간다 — 표식 낱말이 든 줄만 모은다
    private static (T Result, List<string> Lines) Watch<T>(string marker, Func<T> act)
    {
        var cap = new Capture(marker);
        Trace.Listeners.Add(cap);
        try
        {
            var r = act();
            lock (cap.Lines) return (r, cap.Lines.ToList());
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
    }

    private sealed class Capture(string marker) : TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && message.Contains(marker))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message)
            => WriteLine(message);
    }
}
