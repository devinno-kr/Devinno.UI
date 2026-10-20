using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

public class DesignSystemTests
{
    private static LayoutBox Lay(string markup, object? vm = null, DvTheme? theme = null, float w = 800, float h = 600)
        => new LayoutEngine(theme).Layout(MarkupParser.Parse(markup), vm, w, h);

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    private static LayoutBox ByText(LayoutBox root, string text) => All(root).First(b => b.ResolvedText == text);

    private static DvColor Hex(string s)
    {
        Assert.True(DvColor.TryParse(s, out var c));
        return c;
    }

    // ── S1 토큰
    [Fact]
    public void Token_canonical_and_var_alias_resolve_to_same_value()
    {
        var a = ThemeResolver.ResolveVars("padding:token(spacing.md)", DvTheme.Dark);
        var b = ThemeResolver.ResolveVars("padding:var(--spacing-md)", DvTheme.Dark);
        Assert.Equal("padding:12", a);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Legacy_var_keys_still_resolve()
        => Assert.Equal("background:#0f172a", ThemeResolver.ResolveVars("background:var(--bg)", DvTheme.Dark));

    [Fact]
    public void Missing_token_resolves_to_magenta_marker()
        => Assert.Equal("color:#FF00FF", ThemeResolver.ResolveVars("color:token(no-such)", DvTheme.Dark));

    [Fact]
    public void Null_theme_uses_default_theme_for_tokens()
        => Assert.Equal(
            ThemeResolver.ResolveVars("color:token(text)", DvTheme.Default),
            ThemeResolver.ResolveVars("color:token(text)", null));

    [Fact]
    public void Theme_With_overrides_single_token()
    {
        var t = DvTheme.Dark.With(new Dictionary<string, string> { ["primary"] = "#0ea5e9" });
        Assert.Equal("c:#0ea5e9", ThemeResolver.ResolveVars("c:token(primary)", t));
        Assert.Equal("c:12", ThemeResolver.ResolveVars("c:token(spacing.md)", t));
    }

    [Fact]
    public void Thickness_accepts_px_units()
    {
        Assert.True(Thickness.TryParse("14px 24px", out var t));
        Assert.Equal(14, t.Top);
        Assert.Equal(24, t.Left);
    }

    // ── S2 클래스 / DvStyle
    [Fact]
    public void StyleSheet_parses_states_comments_and_ignores_unsupported()
    {
        var sheet = StyleSheet.Parse("/* c */ .a { color:#111 } .a:hover { color:#222 } .a.b { color:#333 } div { x:1 }");
        Assert.True(sheet.TryGet("a", out var r));
        Assert.Equal("color:#111", r.Base);
        Assert.Equal("color:#222", r.Hover);
        Assert.False(sheet.TryGet("b", out _));
    }

    [Fact]
    public void Builtin_card_class_applies_theme_surface()
    {
        var box = Lay("<DvScreen><DvBox class=\"card\"/></DvScreen>", theme: DvTheme.Dark).Children[0];
        Assert.Equal(Hex("#1e293b"), box.Style.Background);
        Assert.Equal(16, box.Style.BorderRadius);
        Assert.NotNull(box.Style.Shadow);
    }

    [Fact]
    public void Inline_style_merges_over_class_without_dropping_other_props()
    {
        var box = Lay("<DvScreen><DvBox class=\"card\" style=\"border-radius:4\"/></DvScreen>").Children[0];
        Assert.Equal(4, box.Style.BorderRadius);
        Assert.NotNull(box.Style.Background);
        Assert.NotNull(box.Style.Shadow);
    }

    [Fact]
    public void Inline_solid_background_overrides_class_gradient()
    {
        var box = Lay("<DvScreen><DvButton variant=\"primary\" text=\"A\" style=\"background:#ff0000\"/></DvScreen>")
            .Children[0];
        Assert.Null(box.Style.Gradient);
        Assert.Equal(Hex("#ff0000"), box.Style.Background);
    }

    [Fact]
    public void DvStyle_rules_override_builtin_and_later_class_wins()
    {
        var root = Lay(
            "<DvScreen><DvStyle>.card { border-radius: 3 } .x { color:#123456 } .y { color:#abcdef }</DvStyle>" +
            "<DvBox class=\"card\"/><DvLabel class=\"x y\" text=\"t\"/></DvScreen>");
        Assert.Equal(2, root.Children.Count); // DvStyle 은 박스를 만들지 않음
        Assert.Equal(3, root.Children[0].Style.BorderRadius);
        Assert.Equal(Hex("#abcdef"), root.Children[1].Style.Color);
    }

    [Fact]
    public void Class_hover_creates_hover_style_and_hover_style_attribute_still_wins()
    {
        var root = Lay(
            "<DvScreen><DvStyle>.h { color:#111111 } .h:hover { color:#222222 }</DvStyle>" +
            "<DvLabel class=\"h\" text=\"a\"/><DvLabel class=\"h\" text=\"b\" hover-style=\"color:#333333\"/></DvScreen>");
        Assert.Equal(Hex("#222222"), root.Children[0].StyleHover!.Color);
        Assert.Equal(Hex("#333333"), root.Children[1].StyleHover!.Color);
    }

    [Fact]
    public void Button_variant_maps_to_classes_but_plain_button_is_unchanged()
    {
        var root = Lay("<DvScreen><DvButton variant=\"danger\" text=\"A\"/><DvButton text=\"B\"/></DvScreen>",
            theme: DvTheme.Dark);
        var danger = root.Children[0];
        Assert.Equal(Hex("#ef44441f"), danger.Style.Background);
        Assert.NotNull(danger.StyleHover);
        var plain = root.Children[1];
        Assert.Null(plain.Style.Background);
        Assert.Null(plain.StyleHover);
    }

    // ── S3 레이아웃 슈가
    [Fact]
    public void Row_with_spacer_pushes_last_item_to_right_edge()
    {
        var row = Lay("<DvScreen><DvRow width=\"400\"><DvLabel text=\"L\"/><DvSpacer/><DvLabel text=\"R\"/></DvRow></DvScreen>");
        Assert.Equal(400, ByText(row, "R").Rect.Right, 1);
    }

    [Fact]
    public void Stack_stretches_children_to_full_width()
    {
        var stack = Lay("<DvStack width=\"300\"><DvLabel text=\"a\"/></DvStack>", w: 300);
        Assert.Equal(300, ByText(stack, "a").Rect.Width, 1);
    }

    [Fact]
    public void Grid_splits_columns_equally_and_measures_one_row_height()
    {
        var root = Lay(
            "<DvStack gap=\"none\"><DvGrid columns=\"4\" gap=\"none\">" +
            "<DvBox style=\"height:50\"/><DvBox style=\"height:50\"/><DvBox style=\"height:50\"/><DvBox style=\"height:50\"/>" +
            "</DvGrid><DvLabel text=\"below\"/></DvStack>", w: 400);
        var grid = root.Children[0];
        Assert.All(grid.Children, c => Assert.Equal(100, c.Rect.Width, 1));
        Assert.Equal(50, grid.Rect.Height, 1);
        Assert.Equal(50, ByText(root, "below").Rect.Top, 1);
    }

    [Fact]
    public void Gap_step_names_map_to_spacing_tokens()
        => Assert.Equal(24, Lay("<DvRow gap=\"xl\"/>").Style.Gap);

    // ── S4 의미 컴포넌트
    [Fact]
    public void Visible_false_removes_element()
    {
        var root = Lay("<DvScreen><DvLabel text=\"a\" visible=\"{Show}\"/><DvLabel text=\"b\"/></DvScreen>",
            new { Show = false });
        Assert.Single(root.Children);
    }

    [Fact]
    public void Card_renders_title_and_slot_and_hides_missing_badge()
    {
        var root = Lay("<DvScreen><DvCard title=\"설비\"><DvLabel text=\"body\"/></DvCard></DvScreen>");
        Assert.Contains(All(root), b => b.ResolvedText == "설비");
        Assert.Contains(All(root), b => b.ResolvedText == "body");
        Assert.DoesNotContain(All(root), b => b.ResolvedText == "");
    }

    [Fact]
    public void Badge_tone_maps_to_tone_tokens()
    {
        var badge = ByText(Lay("<DvScreen><DvBadge tone=\"success\" text=\"OK\"/></DvScreen>", theme: DvTheme.Dark), "OK");
        Assert.Equal(Hex("#6ee7b7"), badge.Style.Color);
        Assert.Equal(Hex("#10b9811f"), badge.Style.Background);
    }

    [Fact]
    public void Stat_shows_value_unit_and_delta()
    {
        var root = Lay("<DvScreen><DvStat label=\"유량\" value=\"1,284\" unit=\"m3/h\" delta=\"+3%\" tone=\"success\"/></DvScreen>");
        foreach (var t in new[] { "유량", "1,284", "m3/h", "+3%" })
            Assert.Contains(All(root), b => b.ResolvedText == t);
    }

    [Fact]
    public void StatusItem_state_binds_from_outer_vm()
    {
        var root = Lay("<DvScreen><DvStatusItem label=\"P1\" state=\"{S}\" status=\"정지\"/></DvScreen>",
            new { S = "fault" }, DvTheme.Dark);
        Assert.Equal(Hex("#fca5a5"), ByText(root, "정지").Style.Color);
    }

    [Fact]
    public void Component_instance_style_merges_into_template_root()
    {
        var card = Lay("<DvScreen><DvCard style=\"border-radius:2\" width=\"222\"/></DvScreen>").Children[0];
        Assert.Equal(2, card.Style.BorderRadius);
        Assert.Equal(222, card.Style.Width);
        Assert.NotNull(card.Style.Shadow);
    }

    [Fact]
    public void User_registry_component_with_builtin_name_wins()
    {
        var reg = new Devinno.UI.Components.ComponentRegistry();
        reg.Register("<DvComponent name=\"DvBadge\"><DvParam name=\"text\"/><DvLabel text=\"custom\"/></DvComponent>");
        var root = new LayoutEngine().Layout(MarkupParser.Parse("<DvScreen><DvBadge text=\"x\"/></DvScreen>"), null, 200, 100, reg);
        Assert.Contains(All(root), b => b.ResolvedText == "custom");
    }

    public sealed class SwitchVm
    {
        public bool Auto { get; set; }
    }

    [Fact]
    public void Switch_toggle_commits_to_outer_vm_two_way_path()
    {
        var vm = new SwitchVm();
        var surface = new DvSurface("<DvStack><DvSwitch label=\"자동\" value=\"{Bind Data.Auto}\"/></DvStack>", vm);
        using var bmp = new SKBitmap(300, 100);
        using var canvas = new SKCanvas(bmp);
        surface.RenderTo(canvas, 300, 100);

        var box = new LayoutEngine().Layout(
            MarkupParser.Parse("<DvStack><DvSwitch label=\"자동\" value=\"{Bind Data.Auto}\"/></DvStack>"), vm, 300, 100);
        var toggle = All(box).First(b => b.Node.Tag == "DvToggle");
        var p = new SKPoint(toggle.Rect.MidX, toggle.Rect.MidY);

        surface.PointerDown(p);
        surface.PointerUp(p);
        Assert.True(vm.Auto);
    }

    [Fact]
    public void Slot_inside_a_nested_component_receives_the_outer_instance_children()
    {
        var reg = new Devinno.UI.Components.ComponentRegistry();
        reg.Register("<DvComponent name=\"PumpCard\"><DvParam name=\"Title\"/>" +
                     "<DvCard title=\"{Title}\"><DvLabel text=\"inner\"/><DvSlot/></DvCard></DvComponent>");
        var root = new LayoutEngine().Layout(MarkupParser.Parse(
            "<DvStack><PumpCard Title=\"P1\"><DvLabel text=\"{Name}\"/></PumpCard></DvStack>"),
            new { Name = "from-outer-vm" }, 600, 400, reg);

        Assert.Contains(All(root), b => b.ResolvedText == "P1");
        Assert.Contains(All(root), b => b.ResolvedText == "inner");
        Assert.Contains(All(root), b => b.ResolvedText == "from-outer-vm");
        Assert.DoesNotContain(All(root), b => b.Node.Tag == "DvSlot");
    }
}
