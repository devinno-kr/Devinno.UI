using System.Diagnostics;
using System.Text.RegularExpressions;
using Devinno.UI;
using Devinno.UI.Alarms;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Trends;
using SkiaSharp;
using Xunit;

// 3단계 R11 — 그리는 색은 모두 테마 토큰. 내장 테마(Light·Dark)에 그리기가 쓰는 토큰이 다 있고, 대체 hex 는 없다.
// 없는 토큰 = 마젠타(ThemeResolver.MissingColor) + 경고 한 정책. 그리기 파일의 리터럴 색은 아래 허용 목록(D 분류)만.
// 공정도(Painter.Process.cs) 색은 look 팔레트라 예외(규칙표 R11·D5 승인).
public class ThemeTokenContractTests
{
    // ── 소스 검사 범위: 그리기 코드(그리는 파일 + 테마 색 헬퍼를 부르는 곳)
    private static readonly string[] DrawingDirs = ["Painting", "Overlay", "Editing"];
    private static readonly string[] DrawingFiles = ["DvRenderer.cs", "DvSurface.cs", "DvSurface.Edit.cs", "DvSurface.Popup.cs"];

    // 테마 색 헬퍼 — Painter.ThemeColor(name), PopupChrome.Tok(theme, name), SoftKeyboard 의 Tok(name), DvSurface.Token(name), ThemeResolver.Color(theme, name)
    private static readonly Regex HelperCall = new(
        @"\b(?:ThemeColor|Tok|Token|ThemeResolver\.Color)\(\s*(?:(?:theme|_theme)\s*,\s*)?(?<arg>[^()]*(?:\([^()]*\))?[^()]*)\)",
        RegexOptions.Compiled);

    // 정적 필드는 적힌 순서로 초기화된다 — DynamicCalls 보다 먼저
    private static readonly string[] Tones = ["tone-neutral", "tone-info", "tone-success", "tone-warning", "tone-danger"];
    private static readonly string[] States = ["state-run", "state-stop", "state-fault", "state-warn", "state-manual", "state-off"];
    private static readonly string[] Charts = ["chart-1", "chart-2", "chart-3", "chart-4", "chart-5", "chart-6"];

    // 이름이 값에서 만들어지는 호출 — 펼칠 이름 목록과 함께. (파일, 인자 식) 그대로 적는다: 새 동적 호출이 생기면 여기 추가해야 통과.
    private static readonly (string File, string Expr, string[] Names, string Why)[] DynamicCalls =
    [
        ("Painting/Painter.cs", "string token", [], "the ThemeColor definition"),
        ("Painting/Painter.cs", "token", [], "ThemeColor forwards to ThemeResolver.Color"),
        ("Painting/Painter.cs", "\"state-\" + ls", States, "DvLamp state, checked at build by AttrRules.ThemeWord (R3)"),
        ("Painting/Painter.Chart.cs", "\"tone-\" + box.Tone", Tones, "chart tone, checked at build by AttrRules.ThemeWord (R3)"),
        ("Painting/Painter.Chart.cs", "Layout.LayoutEngine.ChartToken(i)", Charts, "pie slice / legend colors chart-1..6"),
        ("Painting/Painter.Instruments.cs", "\"tone-\" + box.Tone", Tones, "instrument tone, checked at build by AttrRules.ThemeWord (R3)"),
        ("Painting/Painter.Grid.cs", "name", States, "grid lamp cell data value; looked up only when the theme has state-{value}, else off"),
        ("Overlay/IPopup.cs", "DvTheme? theme, string name", [], "the PopupChrome.Tok definition"),
        ("Overlay/IPopup.cs", "name", [], "PopupChrome.Tok forwards to ThemeResolver.Color"),
        ("Editing/SoftKeyboard.cs", "string name", [], "the local Tok definition"),
        ("Editing/SoftKeyboard.cs", "name", [], "Tok forwards to ThemeResolver.Color"),
        ("DvSurface.Edit.cs", "string name", [], "the DvSurface.Token definition"),
        ("DvSurface.Edit.cs", "name", [], "Token forwards to ThemeResolver.Color"),
        ("Layout/Builders/LayoutEngine.Instruments.cs", "ChartToken(index)", Charts, "trend channel default colors chart-1..6"),
    ];

    // ── 리터럴 색 허용 목록(D 분류 — 테마 색이 아닌 것). (파일, 줄에 들어 있는 글자, 이유). Snippet null = 파일 전체.
    private static readonly (string File, string? Snippet, string Why)[] LiteralAllowList =
    [
        ("Painting/Painter.Process.cs", null, "process symbol colors follow look (plain/rich), not the theme — the R11 exception (D5)"),
        ("Painting/Painter.cs", "SKColors.White.WithAlpha((byte)MathF.Round(opacity * 255f))", "SaveLayer opacity: only the alpha is used"),
        ("Painting/Painter.cs", "new SKColor(0xff, 0xff, 0xff, 0x66)", "lamp gloss highlight on top of the lit color"),
        ("Painting/Painter.cs", "new SKColor(0, 0, 0, 0x38), new SKColor(0xff, 0xff, 0xff, 0x1f)", "tank cylinder shading (alpha black/white)"),
        ("Painting/Painter.cs", "new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 0x30)", "tank cylinder shading (alpha black)"),
        ("Painting/Painter.Alarms.cs", "Mix(comm, SKColors.Black, 0.35f)", "blink-off shade derived from the comm-alarm token"),
        ("Painting/Painter.Alarms.cs", "Mix(prio, SKColors.Black, 0.35f)", "blink-off shade derived from the alarm token"),
        ("Painting/Painter.Alarms.cs", "new SKColor(0, 0, 0, 0x33)", "translucent chip over the alarm color"),
        ("Painting/Painter.Gauge.cs", "new SKColor(0, 0, 0, 0x60)", "needle drop shadow"),
        ("Painting/Painter.Grid.cs", "new SKColor(0xff, 0xff, 0xff, 0x66)", "lamp cell gloss highlight on top of the lit color"),
        ("Painting/Painter.Grid.cs", "SKColors.Transparent", "ghost button background"),
        ("Painting/Painter.Instruments.cs", "Mix(color, SKColors.White, 0.25f)", "bar gradient derived from the zone/tone color"),
        ("Painting/Painter.Instruments.cs", "Color = SKColors.White", "value text on the filled bar (contrast on any zone/tone color)"),
        ("Painting/Painter.Instruments.cs", "Mix(color, SKColors.Black, 0.08f), Mix(color, SKColors.White, 0.28f)", "ring gradient derived from the zone/tone color"),
        ("Painting/Painter.Knob.cs", "SKColors.White", "face light/dark shades derived from the knob-face token"),
        ("Painting/Painter.Knob.cs", "SKColors.Black", "face light/dark shades derived from the knob-face token"),
        ("Painting/Painter.Knob.cs", "new SKColor(0, 0, 0, 0x48)", "knob shadow"),
        ("Painting/Painter.Knob.cs", "new SKColor(0, 0, 0, 0x90)", "knob indicator shadow"),
        ("Painting/Painter.Picker.cs", "new SKColor(0, 0, 0, 0x66)", "color-picker cursor edge (visible on any hue)"),
        ("Painting/Painter.Picker.cs", "SKColors.White, SKColors.White.WithAlpha(0)", "HSV saturation gradient (color space, not a theme color)"),
        ("Painting/Painter.Picker.cs", "SKColors.Black.WithAlpha(0), SKColors.Black", "HSV value gradient (color space, not a theme color)"),
        ("Painting/Painter.Picker.cs", "Color = SKColors.White })", "color-picker cursor ring (visible on any hue)"),
        ("Overlay/IPopup.cs", "new SKColor(0, 0, 0, 0x40)", "popup drop shadow"),
        ("Editing/PickerValue.cs", "c = SKColors.White; return false;", "color value parsing default, not drawing"),
        ("DvSurface.cs", "SKColors.White.WithAlpha(alpha)", "SaveLayer opacity: only the alpha is used"),
        ("DvSurface.Popup.cs", "new ColorPickerState(hadColor ? c : SKColors.White)", "color picker start value for an empty color, not drawing"),
    ];

    private static readonly Regex LiteralColor = new(
        @"new\s+SKColor\(\s*(?:0x|\d)|\bnew\(\s*0x[0-9a-fA-F]|SKColors\.\w+|SKColor\.Parse|""#[0-9a-fA-F]{3,8}""",
        RegexOptions.Compiled);

    [Fact]
    public void Every_token_the_drawing_code_asks_for_is_in_both_built_in_themes()
    {
        var src = Path.Combine(RepoRoot(), "src", "Devinno.UI");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();
        var dynamicSeen = new HashSet<(string, string)>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(NotObj))
        {
            var rel = Path.GetRelativePath(src, file).Replace('\\', '/');
            foreach (var line in CodeLines(file))
            {
                foreach (Match m in HelperCall.Matches(line))
                {
                    var arg = m.Groups["arg"].Value.Trim();
                    if (Regex.Match(arg, "^\"([^\"]+)\"$") is { Success: true } lit)
                    {
                        names.Add(lit.Groups[1].Value);
                        continue;
                    }

                    var dyn = DynamicCalls.Where(d => d.File == rel && d.Expr == arg).ToList();
                    if (dyn.Count == 0)
                    {
                        unknown.Add($"{rel}: {m.Value}");
                        continue;
                    }

                    dynamicSeen.Add((rel, arg));
                    foreach (var n in dyn[0].Names)
                        names.Add(n);
                }
            }
        }

        Assert.True(unknown.Count == 0, "theme color calls with a computed name not in DynamicCalls (add it with the names it can take):\n" + string.Join("\n", unknown));
        var stale = DynamicCalls.Where(d => !dynamicSeen.Contains((d.File, d.Expr))).Select(d => $"{d.File}: {d.Expr}").ToList();
        Assert.True(stale.Count == 0, "DynamicCalls entries that no longer match code:\n" + string.Join("\n", stale));
        Assert.Contains("lamp-off", names);
        Assert.Contains("tank-fill", names);
        Assert.Contains("comm-alarm-fg", names);
        Assert.Contains("background", names);
        Assert.True(names.Count > 60, $"only {names.Count} token names found — the scan pattern is broken");

        foreach (var (themeName, theme) in new[] { ("Light", DvTheme.Light), ("Dark", DvTheme.Dark) })
        {
            var missing = names.Where(n => !theme.TryGet(n, out var v) || !DvColor.TryParse(v, out _)).OrderBy(n => n).ToList();
            Assert.True(missing.Count == 0, $"{themeName} lacks color tokens the drawing code uses: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void Drawing_code_has_no_literal_colors_outside_the_allow_list()
    {
        var src = Path.Combine(RepoRoot(), "src", "Devinno.UI");
        var files = DrawingDirs.SelectMany(d => Directory.EnumerateFiles(Path.Combine(src, d), "*.cs", SearchOption.AllDirectories))
            .Concat(DrawingFiles.Select(f => Path.Combine(src, f)))
            .Where(NotObj);
        var bad = new List<string>();
        var used = new HashSet<int>();
        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(src, file).Replace('\\', '/');
            var n = 0;
            foreach (var raw in File.ReadLines(file))
            {
                n++;
                var line = StripComment(raw);
                if (!LiteralColor.IsMatch(line))
                    continue;
                var hit = Array.FindIndex(LiteralAllowList, a => a.File == rel && (a.Snippet is null || line.Contains(a.Snippet, StringComparison.Ordinal)));
                if (hit < 0)
                    bad.Add($"{rel}:{n}: {raw.Trim()}");
                else
                    used.Add(hit);
            }
        }

        Assert.True(bad.Count == 0, "literal colors in drawing code — use a theme token (R11) or add a class-D entry with a reason:\n" + string.Join("\n", bad));
        var stale = LiteralAllowList.Where((_, i) => !used.Contains(i)).Select(a => $"{a.File}: {a.Snippet}").ToList();
        Assert.True(stale.Count == 0, "allow-list entries that match nothing (remove them):\n" + string.Join("\n", stale));
        Assert.All(LiteralAllowList, a => Assert.False(string.IsNullOrWhiteSpace(a.Why)));
    }

    [Fact]
    public void New_tokens_have_the_approved_values_in_both_themes()
    {
        var expected = new Dictionary<string, string>
        {
            ["lamp-off"] = "#3a3f47", ["tank-fill"] = "#3a7bd5", ["comm-alarm-fg"] = "#ffffff",
            ["chart-1"] = "#3b82f6", ["chart-2"] = "#22d3ee", ["chart-3"] = "#a78bfa",
            ["chart-4"] = "#f59e0b", ["chart-5"] = "#10b981", ["chart-6"] = "#ef4444",
        };
        foreach (var theme in new[] { DvTheme.Light, DvTheme.Dark })
            foreach (var (k, v) in expected)
            {
                Assert.True(theme.TryGet(k, out var got), k);
                Assert.Equal(v, got);
            }

        // 공개 TrendSpec.Palette 는 그대로 두고 chart-1..6 과 같은 값(목록 순서)
        for (var i = 0; i < 6; i++)
            Assert.Equal(TrendSpec.Palette[i], ThemeResolver.Color(DvTheme.Dark, $"chart-{i + 1}"));
    }

    [Fact]
    public void A_missing_token_draws_magenta_with_a_warning_instead_of_a_fallback()
    {
        var theme = new DvTheme(DvTheme.Dark.Tokens.Where(t => t.Key != "lamp-off").ToDictionary(t => t.Key, t => t.Value));
        var (png, warnings) = Watch("lamp-off", () =>
            DvRenderer.RenderToPng("<DvScreen style=\"background: token(background)\"><DvLamp on=\"false\" style=\"width:40; height:40\"/></DvScreen>", null, 40, 40, theme));
        Assert.Equal(new SKColor(0xff, 0x00, 0xff), Pixel(png, 20, 20));
        Assert.Contains(warnings, w => w.Contains("unresolved token 'lamp-off'"));
    }

    [Fact]
    public void Built_in_themes_draw_the_same_colors_as_before()
    {
        // 옛 하드코딩 값 = 새 토큰 값: 꺼진 램프 #3a3f47, 구간 없는 탱크 액체 #3a7bd5 (두 테마 같음)
        foreach (var theme in new[] { DvTheme.Light, DvTheme.Dark })
        {
            var lamp = DvRenderer.RenderToPng("<DvScreen><DvLamp on=\"false\" style=\"width:40; height:40\"/></DvScreen>", null, 40, 40, theme);
            Assert.Equal(new SKColor(0x3a, 0x3f, 0x47), Pixel(lamp, 20, 20));
        }
    }

    [Fact]
    public void Canvas_outside_the_root_is_the_background_token()
    {
        // 루트가 칠하지 않은 곳(투명 루트) — 흰색이 아니라 테마 background
        foreach (var theme in new[] { DvTheme.Light, DvTheme.Dark })
        {
            var png = DvRenderer.RenderToPng("<DvScreen/>", null, 20, 20, theme);
            Assert.Equal(ThemeResolver.Color(theme, "background"), Pixel(png, 10, 10));
        }
    }

    [Fact]
    public void Alarm_row_ack_button_text_is_localized()
    {
        Assert.Equal("ACK", DvStrings.Builtin(null, "dv.alarm.ack"));
        Assert.Equal("확인", DvStrings.Builtin(new DvStrings { Language = "ko" }, "dv.alarm.ack"));
        var keys = BuiltInStrings.En.Keys.ToList();
        Assert.Equal(keys.IndexOf("dv.alarm.col.ack") + 1, keys.IndexOf("dv.alarm.ack"));
    }

    [Fact]
    public void Sample_screen_draws_no_magenta_in_either_built_in_theme()
    {
        foreach (var theme in new[] { DvTheme.Light, DvTheme.Dark })
            foreach (var lang in new[] { "en", "ko" })
            {
                var (png, warnings) = Watch("token", () =>
                    DvRenderer.RenderToPng(SampleMarkup, new SampleVm(), 900, 700, theme, null, 0, new DvStrings { Language = lang }));
                Assert.DoesNotContain(warnings, w => w.Contains("unresolved token") || w.Contains("is not a color"));
                using var bmp = SKBitmap.Decode(png);
                var magenta = 0;
                for (var y = 0; y < bmp.Height; y++)
                    for (var x = 0; x < bmp.Width; x++)
                        if (bmp.GetPixel(x, y) == new SKColor(0xff, 0x00, 0xff))
                            magenta++;
                Assert.Equal(0, magenta);
            }
    }

    // ── R11 시각 확인용 화면: 꺼진 램프, 탱크, 트렌드, 파이, ACK 알람 목록, 통신 이상 배너, 램프 셀 그리드
    internal const string SampleMarkup = """
        <DvScreen style="background: token(background); padding: 12; gap: 12">
          <DvRow style="gap: 12; height: 180">
            <DvLamp on="false" style="width:40; height:40"/>
            <DvLamp on="true" state="run" style="width:40; height:40"/>
            <DvTank value="55" unit="%" style="width:80; height:170"/>
            <DvPieChart items="{Slices}" item-value="V" item-text="N" style="width:320; height:170"/>
            <DvTrend source="{Trend}" end="{End}" window="60" style="width:340; height:170">
              <DvTrendChannel name="a" label="A"/>
              <DvTrendChannel name="b" label="B"/>
              <DvTrendChannel name="c" label="C"/>
            </DvTrend>
          </DvRow>
          <DvAlarmBanner items="{Alarms}" comm-alarm="{Comm}" onack="Ack" style="width:860"/>
          <DvAlarmList items="{Alarms}" onack="Ack" style="width:860; height:170"/>
          <DvDataGrid items="{Rows}" row-height="32" style="width:500; height:150">
            <DvColumn header="Name" field="N"/>
            <DvColumn header="On" field="On" cell-type="lamp"/>
            <DvColumn header="State" field="S" cell-type="lamp"/>
          </DvDataGrid>
        </DvScreen>
        """;

    public sealed class SampleVm
    {
        private static readonly DateTime T0 = new(2026, 11, 9, 9, 0, 0);
        private DateTime _t = T0;

        public SampleVm()
        {
            Log = new AlarmLog(50, () => _t = _t.AddSeconds(1));
            Log.Raise(AlarmPriority.Critical, "Pump 1 overload");
            Log.Raise(AlarmPriority.Major, "Tank level high");
            Log.Raise(AlarmPriority.Minor, "Filter check");
            for (var i = 0; i <= 60; i++)
            {
                Trend.Add("a", T0.AddSeconds(i), 40 + 20 * Math.Sin(i / 8.0));
                Trend.Add("b", T0.AddSeconds(i), 50 + 15 * Math.Cos(i / 6.0));
                Trend.Add("c", T0.AddSeconds(i), 30 + i / 3.0);
            }
        }

        public AlarmLog Log { get; }
        public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
        public string Comm => "PLC1";
        public TrendBuffer Trend { get; } = new(200);
        public DateTime End => T0.AddSeconds(60);
        public List<Slice> Slices { get; } =
            [new("A", 30), new("B", 20), new("C", 15), new("D", 12), new("E", 10), new("F", 8), new("G", 5)];
        public List<GridRow> Rows { get; } =
            [new("P1", true, "run"), new("P2", false, "fault"), new("P3", false, "unknown-state"), new("P4", true, "off")];

        public void Ack(AlarmRow row) => Log.Ack(row.Id);
    }

    public sealed record Slice(string N, double V);
    public sealed record GridRow(string N, bool On, string S);

    private static SKColor Pixel(byte[] png, int x, int y)
    {
        using var bmp = SKBitmap.Decode(png);
        return bmp.GetPixel(x, y);
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

    private static (T Result, List<string> Warnings) Watch<T>(string marker, Func<T> act)
    {
        var cap = new Capture(marker);
        Trace.Listeners.Add(cap);
        try
        {
            var result = act();
            lock (cap.Lines)
                return (result, cap.Lines.ToList());
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
    }

    private static bool NotObj(string f) =>
        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static IEnumerable<string> CodeLines(string file) => File.ReadLines(file).Select(StripComment);

    // 줄 끝 // 주석과 /// XML 주석 제거(문자열 안의 // 는 그리기 코드에 없음 — URL 등은 색 검사와 무관)
    private static string StripComment(string line)
    {
        var t = line.TrimStart();
        if (t.StartsWith("//", StringComparison.Ordinal))
            return "";
        var inStr = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                inStr = !inStr;
            else if (!inStr && line[i] == '/' && line[i + 1] == '/')
                return line[..i];
        }

        return line;
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Devinno.UI.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
