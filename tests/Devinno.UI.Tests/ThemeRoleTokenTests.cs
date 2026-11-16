using System.Diagnostics;
using Devinno.UI.Styling;
using Xunit;

// 역할 토큰과 스위치(단계 2) — 위젯은 시각 값을 역할 이름으로만 읽고, 값은 테마가 정한다.
// 이 단계의 값은 "지금 생김새 그대로"다(굵기 700/400, 선 1/1.5/2, 포커스 3·2, 투명도 0x14/0x26/0x33/0x60/0x80).
public class ThemeRoleTokenTests
{
    public static TheoryData<string> Themes => new() { "Light", "Dark", "Graphite", "Carbon" };

    private static DvTheme Theme(string name) => name switch
    {
        "Light" => DvTheme.Light,
        "Graphite" => DvTheme.Graphite,
        "Carbon" => DvTheme.Carbon,
        _ => DvTheme.Dark,
    };

    [Theory]
    [MemberData(nameof(Themes))]
    public void Number_roles_have_the_current_values(string name)
    {
        var t = Theme(name);
        Assert.Equal(400, ThemeResolver.Number(t, "weight.regular"));
        Assert.Equal(500, ThemeResolver.Number(t, "weight.medium"));
        Assert.Equal(600, ThemeResolver.Number(t, "weight.strong"));
        Assert.Equal(700, ThemeResolver.Number(t, "weight.bold"));
        Assert.Equal(1, ThemeResolver.Number(t, "stroke.hairline"));
        Assert.Equal(1.5f, ThemeResolver.Number(t, "stroke.outline"));
        Assert.Equal(2, ThemeResolver.Number(t, "stroke.emphasis"));
        Assert.Equal(3, ThemeResolver.Number(t, "stroke.focus"));
        Assert.Equal(2, ThemeResolver.Number(t, "focus.gap"));
        Assert.Equal(12, ThemeResolver.Number(t, "font.meta"));
        Assert.Equal(4, ThemeResolver.Number(t, "radius.xs"));
        Assert.Equal(0x14 / 255f, ThemeResolver.Number(t, "overlay.hover"), 2);
        Assert.Equal(0x26 / 255f, ThemeResolver.Number(t, "alpha.selected"), 2);
        Assert.Equal(0x33 / 255f, ThemeResolver.Number(t, "alpha.soft"), 2);
        Assert.Equal(0x60 / 255f, ThemeResolver.Number(t, "alpha.dim"), 2);
        Assert.Equal(0x80 / 255f, ThemeResolver.Number(t, "opacity.disabled"), 2);
        foreach (var key in new[] { "control.sm", "control.md", "control.lg", "row.md", "row.lg", "stroke.track", "ratio.unit", "overlay.press" })
            Assert.True(ThemeResolver.Number(t, key) > 0, key);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Color_roles_point_at_existing_tokens(string name)
    {
        var t = Theme(name);
        Assert.Equal(ThemeResolver.Color(t, "primary"), ThemeResolver.Color(t, "selection"));
        Assert.Equal(ThemeResolver.Color(t, "primary-fg"), ThemeResolver.Color(t, "selection-fg"));
        Assert.Equal(ThemeResolver.Color(t, "primary"), ThemeResolver.Color(t, "value-input"));
        Assert.Equal(ThemeResolver.Color(t, "primary"), ThemeResolver.Color(t, "value-read"));
        Assert.Equal(ThemeResolver.Color(t, "tone-success"), ThemeResolver.Color(t, "normal"));
        Assert.True(t.TryGet("alarm-critical-icon", out var icon) && icon == "");
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Built_in_themes_keep_the_current_look(string name)
    {
        var look = ThemeLook.Of(Theme(name));
        Assert.Equal(LookTexture.Rich, look.Texture);
        Assert.True(look.NormalColor);
        // 선택 강조: Light/Dark = strong, ISA-101 쪽 Graphite/Carbon = quiet (단계 3)
        Assert.Equal(name is "Graphite" or "Carbon" ? LookSelection.Quiet : LookSelection.Strong, look.Selection);
    }

    [Fact]
    public void Switches_follow_the_theme_values()
    {
        var t = DvTheme.Dark.With(new Dictionary<string, string>
        {
            ["look.texture"] = "flat", ["look.normal-color"] = "off", ["look.selection"] = "quiet",
        });
        var look = ThemeLook.Of(t);
        Assert.Equal(LookTexture.Flat, look.Texture);
        Assert.False(look.NormalColor);
        Assert.Equal(LookSelection.Quiet, look.Selection);
    }

    [Fact]
    public void Missing_number_token_reads_zero_with_a_warning()
    {
        var cap = new Capture("x-missing-number");
        Trace.Listeners.Add(cap);
        try
        {
            Assert.Equal(0, ThemeResolver.Number(DvTheme.Dark, "x-missing-number"));
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
        Assert.NotEmpty(cap.Lines);
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
