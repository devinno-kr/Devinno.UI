using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// Graphite·Carbon 글자 대비 — WCAG 2.x 대비비. 본문·작은 글자 4.5:1 이상(큰 값 글자 3:1 은 같은 쌍이 4.5 를 넘으므로 따로 안 봄).
// 반투명 바탕(-soft)은 surface 위에 합성해서 잰다. Light·Dark 는 기존 값 그대로라 여기서 보지 않는다.
public class ThemeContrastTests
{
    private static readonly string[] Tones = ["neutral", "info", "success", "warning", "danger"];
    private static readonly string[] States = ["run", "stop", "fault", "warn", "manual", "off"];

    // (글자, 바탕) — 바탕 위에 실제로 그 글자가 놓이는 쌍
    private static IEnumerable<(string Fg, string Bg)> TextPairs()
    {
        foreach (var bg in new[] { "background", "surface", "surface-raised", "surface-overlay" })
            yield return ("text", bg);
        foreach (var bg in new[] { "background", "surface", "surface-raised" })
            yield return ("text-muted", bg);
        foreach (var bg in new[] { "primary", "primary-hover", "primary-press", "tone-danger" }) // 기본 버튼·켜진 항목·위험 버튼 hover
            yield return ("primary-fg", bg);
        yield return ("primary", "surface-raised"); // 선택된 탭 글자
        yield return ("primary", "surface");
        yield return ("grid-header-fg", "grid-header");
        foreach (var bg in new[] { "grid-row", "grid-row-alt", "grid-row-hover", "grid-row-selected" })
            yield return ("text", bg);
        yield return ("grid-summary-fg", "grid-summary");
        yield return ("kbd-key-fg", "kbd-key");
        yield return ("kbd-key-special-fg", "kbd-key-special");
        foreach (var a in new[] { "alarm-critical", "alarm-major", "alarm-minor", "comm-alarm" })
            yield return (a + "-fg", a);
        yield return ("tank-chip-fg", "tank-chip-bg");
        foreach (var t in Tones)
        {
            yield return ($"tone-{t}-fg", $"tone-{t}-soft");
            yield return ($"tone-{t}-fg", "surface");
        }

        foreach (var s in States)
        {
            yield return ($"state-{s}-fg", $"state-{s}-soft");
            yield return ($"state-{s}-fg", "surface");
        }
    }

    public static TheoryData<string> NewThemes => new() { "Graphite", "Carbon" };

    private static DvTheme Theme(string name) => name == "Graphite" ? DvTheme.Graphite : DvTheme.Carbon;

    [Theory]
    [MemberData(nameof(NewThemes))]
    public void Text_pairs_reach_wcag_aa(string name)
    {
        var theme = Theme(name);
        var fails = TextPairs()
            .Select(p => (p.Fg, p.Bg, Ratio: Contrast(theme, p.Fg, p.Bg)))
            .Where(p => p.Ratio < 4.5)
            .Select(p => $"{p.Fg} on {p.Bg} = {p.Ratio:0.00}")
            .ToList();
        Assert.True(fails.Count == 0, $"{name}: text pairs below 4.5:1 — " + string.Join("; ", fails));
    }

    [Theory]
    [MemberData(nameof(NewThemes))]
    public void Alarm_colors_read_as_chip_text_on_a_card(string name)
    {
        // 알람 목록은 등급 색을 칩 글자·ACK 글자로도 쓴다(굵은 11~12px) — 카드(surface) 위 3:1 이상
        foreach (var a in new[] { "alarm-critical", "alarm-major", "alarm-minor" })
            Assert.True(Contrast(Theme(name), a, "surface") >= 3, $"{name}: {a} on surface = {Contrast(Theme(name), a, "surface"):0.00}");
    }

    [Theory]
    [MemberData(nameof(NewThemes))]
    public void A_running_lamp_stands_apart_from_an_off_lamp(string name)
    {
        // 그림 요소 대비(WCAG 1.4.11) 3:1 — 켜짐(state-run)과 꺼짐(lamp-off)
        Assert.True(Contrast(Theme(name), "state-run", "lamp-off") >= 3, name);
    }

    [Fact]
    public void Carbon_is_dimmer_than_Dark()
    {
        // 야간 관제실 — 본문 대비와 표면 밝기가 Dark 보다 낮다(눈부심 감소)
        Assert.True(Contrast(DvTheme.Carbon, "text", "background") < Contrast(DvTheme.Dark, "text", "background"));
        Assert.True(Luminance(Color(DvTheme.Carbon, "surface")) < Luminance(Color(DvTheme.Dark, "surface")));
        Assert.True(Luminance(Color(DvTheme.Carbon, "text")) < Luminance(Color(DvTheme.Dark, "text")));
    }

    internal static double Contrast(DvTheme theme, string fg, string bg)
    {
        var back = Color(theme, bg);
        if (back.Alpha < 255)
            back = Over(back, Color(theme, "surface"));
        var fore = Over(Color(theme, fg), back);
        var (a, b) = (Luminance(fore), Luminance(back));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static SKColor Color(DvTheme theme, string name) => ThemeResolver.Color(theme, name);

    private static SKColor Over(SKColor fg, SKColor bg)
    {
        var a = fg.Alpha / 255.0;
        byte Mix(byte f, byte b) => (byte)Math.Round(f * a + b * (1 - a));
        return new SKColor(Mix(fg.Red, bg.Red), Mix(fg.Green, bg.Green), Mix(fg.Blue, bg.Blue));
    }

    private static double Luminance(SKColor c)
    {
        static double Ch(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Ch(c.Red) + 0.7152 * Ch(c.Green) + 0.0722 * Ch(c.Blue);
    }
}
