using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using Xunit;

/// <summary>같은 속성을 리터럴로 쓴 것과 바인딩(<c>{X}</c>)·토큰(<c>token()</c>)으로 쓴 것이 똑같이 해석되는지.</summary>
public class BindingParityTests
{
    private sealed class Vm
    {
        public string Kind { get; set; } = "primary";
        public string Size { get; set; } = "lg";
        public int Cols { get; set; } = 4;
        public string Justify { get; set; } = "between";
        public string Gap { get; set; } = "xl";
        public string Pad { get; set; } = "lg none";
        public string Align { get; set; } = "end";
        public string Zones { get; set; } = "0:60:#16a34a 60:85:token(tone-danger)";
        public string Unit { get; set; } = "rpm";
        public string Interval { get; set; } = "250";
        public string Fit { get; set; } = "cover";
    }

    private static LayoutBox Lay(string markup, object? vm = null, DvTheme? theme = null, float w = 800, float h = 600)
        => new LayoutEngine(theme).Layout(MarkupParser.Parse(markup), vm, w, h);

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    private static LayoutBox Tag(LayoutBox root, string tag) => All(root).First(b => b.Node.Tag == tag);

    private static DvColor Hex(string s)
    {
        Assert.True(DvColor.TryParse(s, out var c));
        return c;
    }

    private static DvColor Token(string name, DvTheme theme)
    {
        Assert.True(theme.TryGet(name, out var v));
        return Hex(v);
    }

    // ── 1. DvButton variant·size
    [Fact]
    public void Button_bound_variant_and_size_get_same_theme_classes_as_literal()
    {
        var lit = Tag(Lay("<DvStack><DvButton variant=\"primary\" size=\"lg\" text=\"A\"/></DvStack>"), "DvButton");
        var bound = Tag(Lay("<DvStack><DvButton variant=\"{Kind}\" size=\"{Size}\" text=\"A\"/></DvStack>", new Vm()), "DvButton");

        Assert.Equal(lit.Style.Padding.Left, bound.Style.Padding.Left);
        Assert.Equal(lit.Style.FontSize, bound.Style.FontSize);
        Assert.Equal(lit.Style.Bold, bound.Style.Bold);
        Assert.NotNull(bound.StyleHover);
        Assert.Equal(lit.Rect, bound.Rect);
    }

    [Fact]
    public void Button_variant_bound_to_null_stays_unstyled()
    {
        var plain = Tag(Lay("<DvStack><DvButton text=\"A\"/></DvStack>"), "DvButton");
        var bound = Tag(Lay("<DvStack><DvButton variant=\"{Missing}\" text=\"A\"/></DvStack>", new Vm()), "DvButton");
        Assert.Equal(plain.Rect, bound.Rect);
        Assert.Null(bound.StyleHover);
    }

    // ── 2. alarm-zones
    [Fact]
    public void Alarm_zones_binding_and_token_color_resolve_with_theme()
    {
        foreach (var tag in new[] { "DvGauge", "DvRing", "DvProgress", "DvTank" })
        {
            var box = Tag(Lay($"<DvStack><{tag} value=\"50\" alarm-zones=\"{{Zones}}\"/></DvStack>", new Vm(), DvTheme.Light), tag);
            var zones = tag == "DvTank" ? box.TankZones : box.GaugeZones;
            Assert.Equal(2, zones.Count);
            Assert.Equal(Hex("#16a34a"), zones[0].Color);
            Assert.Equal(Token("tone-danger", DvTheme.Light), zones[1].Color);
            Assert.Equal(85f, zones[1].To);
        }
    }

    [Fact]
    public void Alarm_zones_literal_token_resolves_with_theme()
    {
        var box = Tag(Lay("<DvStack><DvGauge alarm-zones=\"0:100:token(tone-success)\"/></DvStack>", null, DvTheme.Dark), "DvGauge");
        Assert.Equal(Token("tone-success", DvTheme.Dark), Assert.Single(box.GaugeZones).Color);
    }

    [Theory]
    [InlineData("a:60:#16a34a")]
    [InlineData("0:b:#16a34a")]
    [InlineData("0:60")]
    public void Alarm_zones_non_numeric_throws_same_exception_type_as_other_format_errors(string spec)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => GaugeZone.ParseZones(spec));
        Assert.Contains("alarm-zone", ex.Message);
    }

    // ── 3. 색 token()
    [Fact]
    public void Trend_channel_color_token_resolves_with_theme()
    {
        var box = Tag(Lay("<DvStack><DvTrend><DvTrendChannel name=\"a\" color=\"token(tone-danger)\"/></DvTrend></DvStack>",
            null, DvTheme.Light), "DvTrend");
        Assert.Equal(Token("tone-danger", DvTheme.Light).ToSK(), Assert.Single(box.Trend!.Channels).Color);
    }

    [Fact]
    public void Lamp_color_token_resolves_with_theme()
    {
        var box = Tag(Lay("<DvStack><DvLamp on=\"true\" color=\"token(tone-danger)\"/></DvStack>", null, DvTheme.Dark), "DvLamp");
        Assert.Equal(Token("tone-danger", DvTheme.Dark), box.LampColor);
    }

    // ── 4. unit 바인딩
    [Fact]
    public void Tank_and_knob_unit_are_bound()
    {
        var root = Lay("<DvStack><DvTank unit=\"{Unit}\"/><DvKnob unit=\"{Unit}\"/></DvStack>", new Vm());
        Assert.Equal("rpm", Tag(root, "DvTank").TankUnit);
        Assert.Equal("rpm", Tag(root, "DvKnob").KnobUnit);
    }

    // ── 5. 레이아웃 슈가
    private const string Cells = "<DvBox style=\"height:10\"/><DvBox style=\"height:10\"/><DvBox style=\"height:10\"/><DvBox style=\"height:10\"/>";

    [Fact]
    public void Grid_bound_column_count_is_column_count()
    {
        var lit = Tag(Lay($"<DvStack><DvGrid columns=\"4\">{Cells}</DvGrid></DvStack>"), "DvGrid");
        var bound = Tag(Lay($"<DvStack><DvGrid columns=\"{{Cols}}\">{Cells}</DvGrid></DvStack>", new Vm()), "DvGrid");
        Assert.Equal(lit.Children.Select(c => c.Rect), bound.Children.Select(c => c.Rect));
    }

    [Fact]
    public void Sugar_bound_step_names_convert_like_literal()
    {
        const string kids = "<DvBox style=\"width:10; height:10\"/><DvBox style=\"width:10; height:10\"/><DvBox style=\"width:10; height:10\"/>";
        var lit = Tag(Lay($"<DvStack><DvRow justify=\"between\" gap=\"xl\" padding=\"lg none\" align=\"end\" style=\"height:60\">{kids}</DvRow></DvStack>"), "DvRow");
        var bound = Tag(Lay($"<DvStack><DvRow justify=\"{{Justify}}\" gap=\"{{Gap}}\" padding=\"{{Pad}}\" align=\"{{Align}}\" style=\"height:60\">{kids}</DvRow></DvStack>",
            new Vm()), "DvRow");
        Assert.Equal(lit.Style.Gap, bound.Style.Gap);
        Assert.Equal(lit.Style.Padding.Top, bound.Style.Padding.Top);
        Assert.Equal(lit.Children.Select(c => c.Rect), bound.Children.Select(c => c.Rect));
    }

    // ── 6. 그 밖에 리터럴만 되던 속성
    [Fact]
    public void Lamp_blink_interval_and_image_fit_are_bound()
    {
        var root = Lay("<DvStack><DvLamp blink-interval=\"{Interval}\"/><DvImage fit=\"{Fit}\"/></DvStack>", new Vm());
        var lit = Lay("<DvStack><DvLamp blink-interval=\"250\"/><DvImage fit=\"cover\"/></DvStack>");
        Assert.Equal(Tag(lit, "DvLamp").BlinkIntervalMs, Tag(root, "DvLamp").BlinkIntervalMs);
        Assert.Equal(Tag(lit, "DvImage").ImageFit, Tag(root, "DvImage").ImageFit);
    }
}
