using System.Text.RegularExpressions;
using Devinno.UI.Localization;
using Xunit;

// 라이브러리 기본 글자(dv. 키) — 영어가 기본, 한국어 표 내장, 앱 표가 이긴다.
public class BuiltInStringsTests
{
    [Fact]
    public void Without_a_table_the_text_is_English()
    {
        Assert.Equal("OK", DvStrings.Builtin(null, "dv.dialog.ok"));
        Assert.Equal("This value is not allowed", DvStrings.Builtin(null, "dv.input.rejected"));
    }

    [Fact]
    public void Default_language_is_English()
    {
        var s = new DvStrings();
        Assert.Equal("en", s.Language);
        Assert.Equal("Cancel", DvStrings.Builtin(s, "dv.dialog.cancel"));
    }

    [Fact]
    public void Korean_language_uses_the_Korean_table()
    {
        var s = new DvStrings { Language = "ko" };
        Assert.Equal("확인", DvStrings.Builtin(s, "dv.dialog.ok"));
        Assert.Equal("허용 범위: {0} ~ {1}", DvStrings.Builtin(s, "dv.input.range"));
    }

    [Fact]
    public void App_entries_win_over_the_built_in_text()
    {
        var s = new DvStrings { Language = "ko" };
        s.Add("ko", new Dictionary<string, string> { ["dv.dialog.ok"] = "적용" });
        Assert.Equal("적용", DvStrings.Builtin(s, "dv.dialog.ok"));
        Assert.Equal("취소", DvStrings.Builtin(s, "dv.dialog.cancel")); // 다른 키는 내장 한국어
    }

    [Fact]
    public void Other_language_uses_its_app_table_then_falls_back_to_English()
    {
        var s = new DvStrings { Language = "ja" };
        s.Add("ja", new Dictionary<string, string> { ["dv.dialog.ok"] = "OK(ja)" });
        Assert.Equal("OK(ja)", DvStrings.Builtin(s, "dv.dialog.ok"));
        Assert.Equal("Cancel", DvStrings.Builtin(s, "dv.dialog.cancel"));
    }

    [Fact]
    public void English_and_Korean_tables_have_the_same_keys_and_placeholders()
    {
        Assert.Equal(BuiltInStrings.En.Keys.OrderBy(k => k), BuiltInStrings.Ko.Keys.OrderBy(k => k));
        foreach (var (key, en) in BuiltInStrings.En)
        {
            var ko = BuiltInStrings.Ko[key];
            Assert.True(Placeholders(en).SetEquals(Placeholders(ko)), $"{key}: '{en}' vs '{ko}'");
        }
        Assert.Equal(7, BuiltInStrings.En["dv.picker.dow"].Split(',').Length);
        Assert.Equal(7, BuiltInStrings.Ko["dv.picker.dow"].Split(',').Length);
    }

    [Fact]
    public void Every_dv_key_used_in_the_library_is_in_the_table()
    {
        var src = Path.Combine(RepoRoot(), "src", "Devinno.UI");
        var used = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.EndsWith("BuiltInStrings.cs"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"(dv\\.[a-z0-9.\\-]+)\"").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();
        Assert.NotEmpty(used);
        Assert.All(used, k => Assert.True(BuiltInStrings.En.ContainsKey(k), $"no built-in text for {k}"));
    }

    [Fact]
    public void Saturation_alarm_is_shown_in_the_screen_language_other_alarms_as_written()
    {
        var sat = new Devinno.UI.Alarms.AlarmRow { Id = 1, Time = DateTime.Now, Priority = Devinno.UI.Alarms.AlarmPriority.Critical, Message = Devinno.UI.Alarms.AlarmLog.SaturationMessage, Source = "AlarmLog" };
        var app = new Devinno.UI.Alarms.AlarmRow { Id = 2, Time = DateTime.Now, Priority = Devinno.UI.Alarms.AlarmPriority.Critical, Message = Devinno.UI.Alarms.AlarmLog.SaturationMessage, Source = "PLC1" };
        var ko = new Devinno.UI.Painting.Painter(null, new DvStrings { Language = "ko" });
        Assert.Equal("알람 버퍼 포화 — 미확인 알람을 확인(ACK)하세요", ko.AlarmText(sat));
        Assert.Equal(Devinno.UI.Alarms.AlarmLog.SaturationMessage, ko.AlarmText(app)); // 앱이 올린 알람은 그대로
        Assert.Equal(Devinno.UI.Alarms.AlarmLog.SaturationMessage, new Devinno.UI.Painting.Painter(null, null).AlarmText(sat));
    }

    private static HashSet<string> Placeholders(string s) =>
        Regex.Matches(s, @"\{(\d+)").Select(m => m.Groups[1].Value).ToHashSet();

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Devinno.UI.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
