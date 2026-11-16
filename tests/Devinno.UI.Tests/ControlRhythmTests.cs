using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Xunit;

// 높이·반경 단계(H1·H2, 단계 3) — 한 줄에 나란히 놓이는 컨트롤은 같은 높이(control.*)와 같은 반경을 쓴다.
public class ControlRhythmTests
{
    public sealed class Vm
    {
        public string Name { get; set; } = "Line 2";
        public int? Pick { get; set; } = 1;
        public List<Item> Items { get; } = [new(1, "One"), new(2, "Two")];
        public string Mode { get; set; } = "a";
        public DateTime When { get; set; } = new(2026, 11, 16, 9, 30, 0);
        public string Pen { get; set; } = "#3b82f6";
    }

    public sealed record Item(int Id, string Name);

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    private static LayoutBox Row(DvTheme theme) => new LayoutEngine(theme).Layout(MarkupParser.Parse("""
        <DvScreen><DvRow align="start" gap="sm">
          <DvButton variant="primary" text="Start"/>
          <DvButton variant="secondary" text="Reset"/>
          <DvInput value="{Bind Name}" style="width:120"/>
          <DvSelect value="{Bind Pick}" items="{Items}" item-value="Id" item-text="Name" style="width:120"/>
          <DvSegment value="{Bind Mode}"><DvOption value="a" text="A"/><DvOption value="b" text="B"/></DvSegment>
          <DvDateTimeInput value="{Bind When}" style="width:180"/>
          <DvColorInput value="{Bind Pen}" style="width:140"/>
        </DvRow></DvScreen>
        """), new Vm(), 1200, 400);

    public static TheoryData<string> Themes => new() { "Light", "Dark", "Graphite", "Carbon" };

    private static DvTheme Theme(string n) => n switch { "Light" => DvTheme.Light, "Graphite" => DvTheme.Graphite, "Carbon" => DvTheme.Carbon, _ => DvTheme.Dark };

    [Theory]
    [MemberData(nameof(Themes))]
    public void Controls_on_one_row_share_the_control_height(string theme)
    {
        var t = Theme(theme);
        var row = Row(t);
        var md = ThemeResolver.Number(t, "control.md");
        foreach (var tag in new[] { "DvButton", "DvInput", "DvSelect", "DvSegment", "DvDateTimeInput", "DvColorInput" })
        {
            var box = All(row).First(b => b.Node.Tag == tag);
            Assert.True(Math.Abs(box.Rect.Height - md) < 0.5f, $"{tag} height {box.Rect.Height} ≠ control.md {md}");
        }
    }

    [Fact]
    public void Button_sizes_follow_the_control_steps()
    {
        var root = new LayoutEngine(DvTheme.Dark).Layout(MarkupParser.Parse(
            "<DvScreen><DvRow align=\"start\"><DvButton variant=\"primary\" size=\"sm\" text=\"S\"/><DvButton variant=\"primary\" size=\"lg\" text=\"L\"/></DvRow></DvScreen>"), null, 400, 200);
        var buttons = All(root).Where(b => b.Node.Tag == "DvButton").ToList();
        Assert.Equal(ThemeResolver.Number(DvTheme.Dark, "control.sm"), buttons[0].Rect.Height, 0);
        Assert.Equal(ThemeResolver.Number(DvTheme.Dark, "control.lg"), buttons[1].Rect.Height, 0);
    }

    [Fact]
    public void Buttons_and_inputs_share_one_radius_and_dialogs_are_at_least_card_round()
    {
        var root = Row(DvTheme.Dark);
        var btn = All(root).First(b => b.Node.Tag == "DvButton").Style.BorderRadius;
        var input = All(root).First(b => b.Node.Tag == "DvInput").Style.BorderRadius;
        Assert.Equal(btn, input);
        var sheet = StyleSheet.Parse(BuiltInStyles.Sheet);
        Assert.True(sheet.TryGet("dialog", out var dialog) && dialog.Base!.Contains("token(radius.lg)"));
    }

    [Fact]
    public void Button_text_stays_on_one_line()
    {
        var root = new LayoutEngine(DvTheme.Dark).Layout(MarkupParser.Parse(
            "<DvScreen><DvButton variant=\"secondary\" size=\"lg\" text=\"Secondary action\" style=\"width:90\"/></DvScreen>"), null, 400, 200);
        var b = All(root).First(x => x.Node.Tag == "DvButton");
        Assert.Equal(ThemeResolver.Number(DvTheme.Dark, "control.lg"), b.Rect.Height, 0);
    }
}

// 테두리는 CSS 처럼 상자 안쪽에 그린다 — 경계에 걸쳐 그리면 1px 선의 절반이 상자 밖으로 나가 반경에 따라 흐려지거나 사라진다
public class BorderInsideTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(16)]
    public void One_pixel_border_is_drawn_fully_inside_the_box(int radius)
    {
        var png = Devinno.UI.DvRenderer.RenderToPng(
            $"<DvScreen style=\"background:#ffffff\"><DvBox style=\"width:200; height:40; background:#ffffff; border:1 #000000; border-radius:{radius}\"/></DvScreen>", null, 220, 60);
        using var bmp = SkiaSharp.SKBitmap.Decode(png);
        var edge = bmp.GetPixel(0, 20);
        Assert.True(edge.Red < 40, $"left edge pixel {edge} at radius {radius} is not the border color");
    }
}

// 굵기 단계(단계 3) — 버튼·제목 = weight.strong(600), 탭·세그먼트·칩·펼침 머리글 = weight.medium(500). 주 버튼은 단색.
public class ControlWeightTests
{
    private static int Weight(string cls)
    {
        var sheet = Devinno.UI.Styling.StyleSheet.Parse(Devinno.UI.Styling.BuiltInStyles.Sheet);
        Assert.True(sheet.TryGet(cls, out var rule));
        var resolved = Devinno.UI.Styling.ThemeResolver.ResolveVars(rule.Base, Devinno.UI.Styling.DvTheme.Dark);
        return Devinno.UI.Styling.StyleParser.Parse(resolved).FontWeight;
    }

    [Theory]
    [InlineData("btn", 600)]
    [InlineData("title", 600)]
    [InlineData("heading", 600)]
    [InlineData("display", 600)]
    [InlineData("stat-value", 600)]
    [InlineData("tab", 500)]
    [InlineData("segment-item", 500)]
    [InlineData("chip", 500)]
    [InlineData("expander-header", 500)]
    public void Classes_use_the_weight_roles(string cls, int weight) => Assert.Equal(weight, Weight(cls));

    [Fact]
    public void Primary_button_is_a_solid_fill()
    {
        var sheet = Devinno.UI.Styling.StyleSheet.Parse(Devinno.UI.Styling.BuiltInStyles.Sheet);
        Assert.True(sheet.TryGet("btn-primary", out var rule));
        Assert.DoesNotContain("gradient", rule.Base + rule.Hover);
    }
}

// 선택 강조 스위치 S3(단계 3) — look.selection = quiet 이면 세그먼트·목록·트리 선택이 primary 채움 대신 표면 단계/옅은 틴트
public class SelectionLookTests
{
    public sealed class Vm { public string Mode { get; set; } = "a"; }

    private static IEnumerable<Devinno.UI.Layout.LayoutBox> All(Devinno.UI.Layout.LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    private static SkiaSharp.SKColor SelectedSegmentBg(Devinno.UI.Styling.DvTheme theme)
    {
        var root = new Devinno.UI.Layout.LayoutEngine(theme).Layout(Devinno.UI.Markup.MarkupParser.Parse(
            "<DvScreen><DvSegment value=\"{Bind Mode}\"><DvOption value=\"a\" text=\"A\"/><DvOption value=\"b\" text=\"B\"/></DvSegment></DvScreen>"), new Vm(), 400, 200);
        var item = All(root).First(b => b.ResolvedText == "A");
        return item.Style.Background!.Value.ToSK();
    }

    // 테마가 덧붙인 자기 CSS 는 스위치 규칙(조용한 선택·평면)보다 이긴다 — 내장 시트 < 스위치 규칙 < 테마 자기 CSS < 화면 DvStyle
    [Fact]
    public void Theme_css_wins_over_switch_rules()
    {
        var custom = new Devinno.UI.Styling.DvTheme(Devinno.UI.Styling.DvTheme.Dark.Tokens,
            Devinno.UI.Styling.BuiltInStyles.Sheet + " .segment-item-on { background: #ff0000 }");
        Assert.Equal(new SkiaSharp.SKColor(0xff, 0, 0), SelectedSegmentBg(custom));
    }

    // 내장 시트를 쓰지 않는 테마 시트도 스위치 규칙에 지지 않는다
    [Fact]
    public void Replacement_theme_sheet_wins_over_switch_rules()
    {
        var custom = new Devinno.UI.Styling.DvTheme(Devinno.UI.Styling.DvTheme.Dark.Tokens, ".segment-item-on { background: #00ff00 }");
        Assert.Equal(new SkiaSharp.SKColor(0, 0xff, 0), SelectedSegmentBg(custom));
    }

    [Fact]
    public void Strong_selection_fills_with_primary()
    {
        // 내장 테마는 모두 quiet — strong 은 테마 값으로 고른다
        var strong = Devinno.UI.Styling.DvTheme.Dark.With(new Dictionary<string, string> { ["look.selection"] = "strong" });
        Assert.Equal(Devinno.UI.Styling.ThemeResolver.Color(strong, "primary"), SelectedSegmentBg(strong));
    }

    [Fact]
    public void Quiet_selection_raises_a_surface_instead()
    {
        var quiet = Devinno.UI.Styling.DvTheme.Dark.With(new Dictionary<string, string> { ["look.selection"] = "quiet" });
        Assert.Equal(Devinno.UI.Styling.ThemeResolver.Color(quiet, "surface-overlay"), SelectedSegmentBg(quiet));
    }

    [Theory]
    [InlineData("Graphite")]
    [InlineData("Carbon")]
    public void Isa_themes_use_quiet_selection(string name)
    {
        var t = name == "Graphite" ? Devinno.UI.Styling.DvTheme.Graphite : Devinno.UI.Styling.DvTheme.Carbon;
        Assert.Equal(Devinno.UI.Styling.LookSelection.Quiet, Devinno.UI.Styling.ThemeLook.Of(t).Selection);
    }
}

// 켜짐/꺼짐 컨트롤 상태(단계 3, H3·H4) — hover 로 테두리가 굵어지지 않고, 꺼진 토글 손잡이는 흐리다
public class ToggleStateLookTests
{
    private static IEnumerable<Devinno.UI.Layout.LayoutBox> All(Devinno.UI.Layout.LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    private static (SkiaSharp.SKBitmap Bmp, Devinno.UI.Layout.LayoutBox Box) Render(string markup, string tag, bool hover)
    {
        var theme = Devinno.UI.Styling.DvTheme.Dark;
        var root = new Devinno.UI.Layout.LayoutEngine(theme).Layout(Devinno.UI.Markup.MarkupParser.Parse(markup), null, 200, 60);
        var box = All(root).First(b => b.Node.Tag == tag);
        var bmp = new SkiaSharp.SKBitmap(200, 60);
        using var c = new SkiaSharp.SKCanvas(bmp);
        c.Clear(SkiaSharp.SKColors.Black);
        var state = hover ? new Devinno.UI.Interaction.InteractionState { Hover = box } : null;
        new Devinno.UI.Painting.Painter(theme).Paint(c, root, null, state);
        return (bmp, box);
    }

    private static int Inked(SkiaSharp.SKBitmap bmp, SkiaSharp.SKRect r, SkiaSharp.SKColor fill)
    {
        var n = 0;
        for (var y = (int)r.Top; y < r.Bottom; y++)
            for (var x = (int)r.Left; x < r.Right; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (Math.Abs(p.Red - fill.Red) + Math.Abs(p.Green - fill.Green) + Math.Abs(p.Blue - fill.Blue) > 30) n++;
            }
        return n;
    }

    [Fact]
    public void Checkbox_border_keeps_its_width_on_hover()
    {
        const string m = "<DvScreen style=\"background:#000000\"><DvCheckBox text=\"x\" id=\"c\"/></DvScreen>";
        var (idle, box) = Render(m, "DvCheckBox", false);
        var (hot, _) = Render(m, "DvCheckBox", true);
        var check = new SkiaSharp.SKRect(box.Rect.Left, box.Rect.MidY - 12, box.Rect.Left + 24, box.Rect.MidY + 12);
        var surface = Devinno.UI.Styling.ThemeResolver.Color(Devinno.UI.Styling.DvTheme.Dark, "surface");
        Assert.InRange(Inked(hot, check, surface), Inked(idle, check, surface) - 2, Inked(idle, check, surface) + 2);
    }

    [Fact]
    public void Off_toggle_knob_is_muted()
    {
        var (bmp, box) = Render("<DvScreen><DvToggle value=\"false\" style=\"width:46; height:26\"/></DvScreen>", "DvToggle", false);
        var knob = bmp.GetPixel((int)box.Rect.Left + 13, (int)box.Rect.Top + 13);
        Assert.Equal(Devinno.UI.Styling.ThemeResolver.Color(Devinno.UI.Styling.DvTheme.Dark, "text-muted"), knob);
    }
}

// 편집 중 표시(단계 3, H4) — 바깥 링 대신 테두리 자체를 강조색·강조 두께로
public class EditingLookTests
{
    private static IEnumerable<Devinno.UI.Layout.LayoutBox> All(Devinno.UI.Layout.LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    public sealed class Vm { public string Name { get; set; } = "a"; }

    [Theory]
    [InlineData("<DvInput value=\"{Bind Name}\" style=\"width:160\"/>", "DvInput")]
    [InlineData("<DvSelect value=\"{Bind Name}\" style=\"width:160\"><DvOption value=\"a\" text=\"A\"/></DvSelect>", "DvSelect")]
    public void Editing_box_draws_no_outer_ring_and_an_accent_border(string markup, string tag)
    {
        var theme = Devinno.UI.Styling.DvTheme.Dark;
        var root = new Devinno.UI.Layout.LayoutEngine(theme).Layout(Devinno.UI.Markup.MarkupParser.Parse(
            "<DvScreen style=\"padding:20; background:#000000\">" + markup + "</DvScreen>"), new Vm(), 300, 100);
        var box = All(root).First(b => b.Node.Tag == tag);
        box.Editing = true;
        using var bmp = new SkiaSharp.SKBitmap(300, 100);
        using (var c = new SkiaSharp.SKCanvas(bmp))
            new Devinno.UI.Painting.Painter(theme).Paint(c, root, null);
        var outside = bmp.GetPixel((int)box.Rect.Left - 3, (int)box.Rect.MidY);
        Assert.Equal(new SkiaSharp.SKColor(0, 0, 0), outside);
        var edge = bmp.GetPixel((int)box.Rect.Left, (int)box.Rect.MidY);
        var primary = Devinno.UI.Styling.ThemeResolver.Color(theme, "primary");
        Assert.True(Math.Abs(edge.Blue - primary.Blue) < 40 && Math.Abs(edge.Red - primary.Red) < 40, $"edge {edge} vs primary {primary}");
    }
}

// 글리프 한 벌·탭 구분선(단계 3, H9·H3) — 펼침 뜻은 chevron 하나, 탭 아래 구분선은 hairline
public class GlyphAndTabRuleTests
{
    [Fact]
    public void Expand_glyphs_are_chevrons_everywhere()
    {
        var root = Path.GetDirectoryName(typeof(GlyphAndTabRuleTests).Assembly.Location)!;
        while (!File.Exists(Path.Combine(root, "Devinno.UI.slnx"))) root = Path.GetDirectoryName(root)!;
        var painting = Path.Combine(root, "src", "Devinno.UI", "Painting");
        foreach (var f in Directory.GetFiles(painting, "*.cs"))
            Assert.DoesNotContain("fa-angle-", File.ReadAllText(f));
    }

    [Fact]
    public void Tab_rule_is_a_hairline()
    {
        var sheet = Devinno.UI.Styling.StyleSheet.Parse(Devinno.UI.Styling.BuiltInStyles.Sheet);
        Assert.True(sheet.TryGet("tab-rule", out var rule));
        Assert.Contains("token(stroke.hairline)", rule.Base);
    }
}
