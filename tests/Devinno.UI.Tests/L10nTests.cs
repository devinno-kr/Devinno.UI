using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Text;
using SkiaSharp;
using Xunit;

public class L10nTests
{
    private static DvStrings Strings()
    {
        var s = new DvStrings { Language = "ko", FallbackLanguage = "en" };
        s.LoadJson("ko", """{ "title": "정수장", "pump": "펌프", "card": { "status": "설비 상태" } }""");
        s.LoadJson("en", """{ "title": "Water Plant", "pump": "Pump", "card": { "status": "Equipment" }, "only_en": "EN only" }""");
        return s;
    }

    private static LayoutBox Lay(string markup, object? vm, DvStrings? s)
        => new LayoutEngine(null, s).Layout(MarkupParser.Parse(markup), vm, 600, 400);

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    private static IEnumerable<string?> Texts(LayoutBox b) => All(b).Select(x => x.ResolvedText);

    // ── 문자열 테이블
    [Fact]
    public void Lookup_uses_current_then_fallback_then_marks_missing_key()
    {
        var s = Strings();
        Assert.Equal("정수장", s.Get("title"));
        Assert.Equal("EN only", s.Get("only_en"));
        Assert.Equal("[nope]", s.Get("nope"));
    }

    [Fact]
    public void Json_nested_objects_flatten_to_dotted_keys()
        => Assert.Equal("설비 상태", Strings().Get("card.status"));

    [Fact]
    public void Changing_language_raises_changed_once()
    {
        var s = Strings();
        var n = 0;
        s.Changed += (_, _) => n++;
        s.Language = "en";
        s.Language = "en";
        Assert.Equal(1, n);
        Assert.Equal("Water Plant", s.Get("title"));
    }

    // ── 바인딩
    [Fact]
    public void L10n_binding_and_at_shorthand_resolve()
    {
        var root = Lay("<DvStack><DvLabel text=\"{L10n.title}\"/><DvLabel text=\"@pump\"/></DvStack>", null, Strings());
        Assert.Contains("정수장", Texts(root));
        Assert.Contains("펌프", Texts(root));
    }

    [Fact]
    public void Mixed_text_interpolates_keys_and_vm_values()
    {
        var root = Lay("<DvStack><DvLabel text=\"{L10n.pump} #1 {Status}\"/></DvStack>", new { Status = "RUN" }, Strings());
        Assert.Contains("펌프 #1 RUN", Texts(root));
    }

    [Fact]
    public void Mixed_vm_bindings_work_without_strings_too()
    {
        var root = Lay("<DvStack><DvLabel text=\"{A} / {B}\"/></DvStack>", new { A = 1, B = 2 }, null);
        Assert.Contains("1 / 2", Texts(root));
    }

    [Theory]
    [InlineData("@", "@")]            // 한 글자
    [InlineData("@@pump", "@pump")]   // 이스케이프
    [InlineData("a@b.com", "a@b.com")]
    public void At_literals_are_kept(string text, string expected)
        => Assert.Contains(expected, Texts(Lay($"<DvStack><DvLabel text=\"{text}\"/></DvStack>", null, Strings())));

    [Fact]
    public void Without_strings_at_text_stays_literal()
        => Assert.Contains("@pump", Texts(Lay("<DvStack><DvLabel text=\"@pump\"/></DvStack>", null, null)));

    [Fact]
    public void Component_parameters_accept_l10n()
    {
        var root = Lay("<DvStack><DvCard title=\"{L10n.card.status}\"/></DvStack>", null, Strings());
        Assert.Contains("설비 상태", Texts(root));
    }

    [Fact]
    public void Missing_key_is_visible_not_empty()
        => Assert.Contains("[ghost]", Texts(Lay("<DvStack><DvLabel text=\"{L10n.ghost}\"/></DvStack>", null, Strings())));

    // ── 언어 전환 → 자동 재레이아웃
    [Fact]
    public void Surface_relayouts_when_language_changes()
    {
        var s = Strings();
        var surface = new DvSurface("<DvStack><DvLabel text=\"@title\" style=\"font-size:30\"/></DvStack>", null) { Strings = s };
        using var bmp = new SKBitmap(300, 80);
        using var c = new SKCanvas(bmp);
        surface.RenderTo(c, 300, 80);
        var ko = bmp.Bytes;
        s.Language = "en";
        surface.RenderTo(c, 300, 80);
        Assert.NotEqual(ko, bmp.Bytes);
    }

    // ── 폰트
    [Fact]
    public void Font_family_is_used_when_it_has_every_glyph()
    {
        Assert.Equal("Consolas", TextFont.ResolveTypeface("PUMP-01", "Consolas").FamilyName);
        Assert.NotEqual("Consolas", TextFont.ResolveTypeface("펌프", "Consolas").FamilyName); // 한글 없음 → 폴백
        Assert.Equal("Consolas", TextFont.ResolveTypeface("PUMP", "NoSuchFont, Consolas").FamilyName);
    }

    [Fact]
    public void Root_font_lang_sets_default_family_for_current_language_and_inherits()
    {
        var s = Strings();
        s.Language = "en";
        var root = Lay(
            "<DvStack font-lang-en=\"Consolas\" font-lang-ko=\"Malgun Gothic\">" +
            "<DvBox><DvLabel text=\"a\"/></DvBox><DvLabel text=\"b\" style=\"font-family: Segoe UI\"/></DvStack>", null, s);
        Assert.Equal("Consolas", All(root).First(b => b.ResolvedText == "a").Style.FontFamily);
        Assert.Equal("Segoe UI", All(root).First(b => b.ResolvedText == "b").Style.FontFamily);
    }

    // ── 내장 문구
    [Fact]
    public void Built_in_widget_text_can_be_overridden_with_dv_keys()
    {
        var s = new DvStrings { Language = "en" };
        s.Add("en", new Dictionary<string, string> { ["dv.alarm.none"] = "No active alarms" });
        var p = new Painter(null, s);
        Assert.Equal("No active alarms", p.T("dv.alarm.none", "활성 알람 없음"));
        Assert.Equal("데이터 없음", p.T("dv.trend.empty", "데이터 없음"));
    }
}
