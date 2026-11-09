using System.Diagnostics;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using Xunit;

// 3단계 R2(색)·R3(모르는 값)·R4(바인딩, D3 공정도 이름은 원문) — 모든 태그가 같은 해석 함수(AttrRules.Color·Enum)를 쓴다.
// 색 = #hex 또는 token(이름). 모르는 토큰 = 자홍 + 경고, 해석 못 하는 값 = 없는 것처럼(기본색) + 경고.
// 목록 값 = TagSchema Values(앞뒤 공백·대소문자 무시), 그 밖 = 기본값 + 경고. 경고 값마다 고유한 글자를 써서 다른 테스트와 섞이지 않게 한다.
public class AttrRulesColorEnumTests
{
    public sealed class Vm
    {
        public List<Row> Rows { get; } = [new()];
        public Devinno.UI.Trends.TrendBuffer Buf { get; } = new(10);
        public string Al { get; set; } = "center";
        public string Fmt { get; set; } = "0.0";
        public string So { get; set; } = "desc";
        public string Ct { get; set; } = "button";
        public string Ic { get; set; } = "fa-play";
        public string Va { get; set; } = "danger";
        public string Fn { get; set; } = "max";
        public string Mode { get; set; } = "tap";
        public string X { get; set; } = "P1";
    }

    public sealed class Row
    {
        public int A { get; set; }
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

    private static LayoutBox Build(string body, object? vm = null) =>
        new LayoutEngine(DvTheme.Light).Layout(MarkupParser.Parse($"<DvScreen>{body}</DvScreen>"), vm ?? new Vm(), 800, 600);

    private static LayoutBox Find(LayoutBox b, string tag) =>
        All(b).First(x => x.Node.Tag == tag);

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    private static DvColor Hex(string s)
    {
        Assert.True(DvColor.TryParse(s, out var c));
        return c;
    }

    private static readonly DvColor Magenta = Hex(ThemeResolver.MissingColor);

    // ── R2 색 ──

    [Fact]
    public void Color_Rule()
    {
        Assert.Equal(Hex("#123456"), AttrRules.Color(" #123456 ", DvTheme.Light, "DvX", "color"));
        Assert.True(DvTheme.Light.TryGet("tone-danger", out var danger));
        Assert.Equal(Hex(danger), AttrRules.Color("token(tone-danger)", DvTheme.Light, "DvX", "color"));
        Assert.Equal(Hex(danger), AttrRules.Color("var(--tone-danger)", DvTheme.Light, "DvX", "color"));
        Assert.Null(AttrRules.Color(null, DvTheme.Light, "DvX", "color"));
        Assert.Null(AttrRules.Color("  ", DvTheme.Light, "DvX", "color")); // 빈 값 = 없음(경고 없음)

        var (bad, w1) = Watch("nope-r2fn", () => AttrRules.Color("nope-r2fn", DvTheme.Light, "DvX", "color"));
        Assert.Null(bad);
        Assert.Contains("DvX", Assert.Single(w1));

        var (tok, w2) = Watch("nope-r2tk", () => AttrRules.Color("token(nope-r2tk)", DvTheme.Light, "DvX", "color"));
        Assert.Equal(Magenta, tok);
        Assert.Single(w2); // 테마 해석기의 "unresolved token" 과 겹쳐 두 번 쓰지 않는다
    }

    [Fact]
    public void Lamp_Color_Unparsable_DefaultWithWarning()
    {
        var (b, w) = Watch("nope-r2lamp", () => Build("<DvLamp on=\"true\" color=\"nope-r2lamp\"/>"));
        Assert.Null(Find(b, "DvLamp").LampColor);
        var line = Assert.Single(w);
        Assert.Contains("DvLamp", line);
        Assert.Contains("color", line);
    }

    [Fact]
    public void Lamp_Color_UnknownToken_MagentaWithWarning()
    {
        var (b, w) = Watch("nope-r2lt", () => Build("<DvLamp on=\"true\" color=\"token(nope-r2lt)\"/>"));
        Assert.Equal(Magenta, Find(b, "DvLamp").LampColor);
        Assert.Single(w);
    }

    [Fact]
    public void TrendChannel_Color_Unparsable_PaletteWithWarning()
    {
        var plain = Find(Build("<DvTrend source=\"{Buf}\"><DvTrendChannel name=\"a\"/></DvTrend>"), "DvTrend").Trend!.Channels[0].Color;
        var (b, w) = Watch("nope-r2tc", () => Build("<DvTrend source=\"{Buf}\"><DvTrendChannel name=\"a\" color=\"nope-r2tc\"/></DvTrend>"));
        Assert.Equal(plain, Find(b, "DvTrend").Trend!.Channels[0].Color);
        Assert.Single(w);
    }

    [Fact]
    public void Grid_Color_UnknownToken_Magenta_Unparsable_ThemeDefault()
    {
        var (b, w) = Watch("r2grid", () => Build(
            "<DvDataGrid items=\"{Rows}\" header-color=\"token(nope-r2grid)\" row-color=\"bad-r2grid\"><DvColumn field=\"A\"/></DvDataGrid>"));
        var colors = Find(b, "DvDataGrid").Grid!.Colors;
        Assert.Equal(Magenta, colors.Header);
        Assert.Null(colors.Row);
        Assert.Equal(2, w.Count);
    }

    [Fact]
    public void Column_OnColor_UnknownToken_Magenta()
    {
        var (b, w) = Watch("nope-r2on", () => Build(
            "<DvDataGrid items=\"{Rows}\"><DvColumn field=\"A\" cell-type=\"lamp\" on-color=\"token(nope-r2on)\"/></DvDataGrid>"));
        Assert.Equal(Magenta, Find(b, "DvDataGrid").Grid!.Columns[0].OnColor);
        Assert.Single(w);
    }

    [Theory]
    [InlineData("DvGauge")]
    [InlineData("DvRing")]
    [InlineData("DvProgress")]
    [InlineData("DvTank")]
    public void AlarmZones_BadZone_DroppedWithWarning(string tag)
    {
        var (b, w) = Watch("r2zone", () => Build($"<{tag} value=\"50\" alarm-zones=\"0:50:#f00 bad-r2zone 50:100:#0f0\"/>"));
        var box = Find(b, tag);
        var zones = tag == "DvTank" ? box.TankZones : box.GaugeZones;
        Assert.Equal(2, zones.Count);
        Assert.Equal(Hex("#0f0"), zones[1].Color);
        Assert.Single(w);
    }

    [Fact]
    public void AlarmZones_BadColorOrOrder_DroppedWithWarning_UnknownTokenMagenta()
    {
        var (b, w) = Watch("r2zc", () => Build(
            "<DvGauge alarm-zones=\"0:40:#f00 40:60:r2zc 60:80:token(nope-r2zc) 10:20:#00f 80:100:#0f0\"/>"));
        var zones = Find(b, "DvGauge").GaugeZones;
        Assert.Equal(new[] { 0f, 60f, 80f }, zones.Select(z => z.From));
        Assert.Equal(Magenta, zones[1].Color);
        Assert.Equal(2, w.Count); // 색 못 읽음 · 모르는 토큰 (순서 오류는 아래)
    }

    [Fact]
    public void AlarmZones_PublicParser_StillThrows()
        => Assert.Throws<InvalidOperationException>(() => GaugeZone.ParseZones("0:50:#f00 bad"));

    [Fact]
    public void Signal_Color_UnknownToken_Magenta_Unparsable_Default()
    {
        const string body = """
            <DvPump name="P1"/>
            <DvInstrument name="FIC" tag="FIC" on="P1"/>
            <DvSignal from="FIC" to="P1" color="token(nope-r2sig)"/>
            <DvSignal from="FIC" to="P1" color="bad-r2sig"/>
            """;
        var ((_, d), w) = Watch("r2sig", () => ProcessTests.Start(body));
        Assert.Equal(Magenta.ToSK(), d.Signals[0].Color);
        Assert.Null(d.Signals[1].Color);
        Assert.Equal(2, w.Count);
    }

    // ── R3 모르는 값 ──

    [Fact]
    public void Enum_Rule()
    {
        string[] values = ["right", "left", "up", "down"];
        Assert.Equal("up", AttrRules.Enum(" UP ", "right", "DvX", "direction", values));
        Assert.Equal("right", AttrRules.Enum(null, "right", "DvX", "direction", values));
        Assert.Equal("right", AttrRules.Enum("", "right", "DvX", "direction", values)); // 빈 값 = 없음(경고 없음)
        var (v, w) = Watch("sideways-r3fn", () => AttrRules.Enum("sideways-r3fn", "right", "DvX", "direction", values));
        Assert.Equal("right", v);
        var line = Assert.Single(w);
        Assert.Contains("DvX", line);
        Assert.Contains("direction", line);
    }

    [Fact]
    public void Progress_Direction_Unknown_RightWithWarning()
    {
        var (b, w) = Watch("sideways-r3p", () => Build("<DvProgress direction=\"sideways-r3p\"/>"));
        Assert.Equal("right", Find(b, "DvProgress").Direction);
        Assert.Single(w);
    }

    [Fact]
    public void Slider_Direction_CaseAndSpacesIgnored()
    {
        var (b, w) = Watch("DvSlider direction", () => Build("<DvSlider direction=\" UP \"/>"));
        Assert.Equal("up", Find(b, "DvSlider").Direction);
        Assert.Empty(w);
    }

    [Fact]
    public void Button_Variant_Unknown_NoClassWithWarning()
    {
        var plain = Find(Build("<DvButton text=\"A\"/>"), "DvButton");
        var (b, w) = Watch("foo-r3v", () => Build("<DvButton variant=\"foo-r3v\" text=\"A\"/>"));
        var box = Find(b, "DvButton");
        Assert.Equal(plain.Rect, box.Rect);
        Assert.Null(box.StyleHover);
        Assert.Single(w);
    }

    [Fact]
    public void Button_Size_Unknown_NoSizeClassWithWarning()
    {
        var primary = Find(Build("<DvButton variant=\"primary\" text=\"A\"/>"), "DvButton");
        var (b, w) = Watch("huge-r3s", () => Build("<DvButton variant=\"primary\" size=\"huge-r3s\" text=\"A\"/>"));
        Assert.Equal(primary.Rect, Find(b, "DvButton").Rect);
        Assert.Single(w);
    }

    [Fact]
    public void Tone_Unknown_InfoWithWarning()
    {
        var (b, w) = Watch("ok-r3tone", () => Build("<DvRing tone=\"ok-r3tone\"/><DvSparkline tone=\"ok-r3tone\"/>"));
        Assert.Equal("info", Find(b, "DvRing").Tone);
        Assert.Equal("info", Find(b, "DvSparkline").Tone);
        Assert.Equal(2, w.Count);
        Assert.Equal("danger", Find(Build("<DvProgress tone=\"Danger\"/>"), "DvProgress").Tone);
    }

    [Fact]
    public void Lamp_State_Unknown_NoneWithWarning()
    {
        var (b, w) = Watch("going-r3st", () => Build("<DvLamp state=\"going-r3st\"/>"));
        Assert.Null(Find(b, "DvLamp").LampState);
        Assert.Single(w);
        Assert.Equal("run", Find(Build("<DvLamp state=\"Run\"/>"), "DvLamp").LampState);
    }

    // 3.4 — tone·DvLamp state 는 고정 목록이 아니라 지금 테마의 토큰(tone-{v}·state-{v}) 기준(3.3 결정 뒤집음): 사용자 테마의 state-idle 도 쓸 수 있다
    [Fact]
    public void Tone_State_CustomThemeToken_Accepted()
    {
        var theme = DvTheme.Light.With(new Dictionary<string, string> { ["tone-brand-r34"] = "#123456", ["state-idle-r34"] = "#654321" });
        LayoutBox B(string body) => new LayoutEngine(theme).Layout(MarkupParser.Parse($"<DvScreen>{body}</DvScreen>"), new Vm(), 800, 600);
        var (b, w) = Watch("-r34", () => B("<DvRing tone=\"brand-r34\"/><DvBarChart tone=\" Brand-R34 \"/><DvLamp state=\"idle-r34\"/>"));
        Assert.Equal("brand-r34", Find(b, "DvRing").Tone);
        Assert.Equal("brand-r34", Find(b, "DvBarChart").Tone); // 앞뒤 공백·대소문자 무시(소문자로)
        Assert.Equal("idle-r34", Find(b, "DvLamp").LampState);
        Assert.Empty(w);
        // 기본 테마에는 그 토큰이 없다 = 기본값 + 경고
        var (d, w2) = Watch("-r34", () => Build("<DvRing tone=\"brand-r34\"/><DvLamp state=\"idle-r34\"/>"));
        Assert.Equal("info", Find(d, "DvRing").Tone);
        Assert.Null(Find(d, "DvLamp").LampState);
        Assert.Equal(2, w2.Count);
    }

    [Fact]
    public void SelectionMode_Unknown_DefaultWithWarning()
    {
        var (b, w) = Watch("many-r3sel", () => Build(
            "<DvListBox selection-mode=\"many-r3sel\"/><DvDataGrid items=\"{Rows}\" selection-mode=\"many-r3sel\"><DvColumn field=\"A\"/></DvDataGrid>"));
        Assert.False(Find(b, "DvListBox").ListMulti);
        Assert.Equal(Devinno.UI.Grid.GridSelectionMode.None, Find(b, "DvDataGrid").Grid!.Mode);
        Assert.Equal(2, w.Count);
    }

    [Fact]
    public void ImageFit_Unknown_ContainWithWarning()
    {
        var (b, w) = Watch("stretch-r3fit", () => Build("<DvImage fit=\"stretch-r3fit\"/>"));
        Assert.Equal(ImageFit.Contain, Find(b, "DvImage").ImageFit);
        Assert.Single(w);
        Assert.Equal(ImageFit.Cover, Find(Build("<DvImage fit=\"COVER\"/>"), "DvImage").ImageFit);
    }

    [Fact]
    public void ContentAlign_Unknown_IgnoredWithWarning()
    {
        var (b, w) = Watch("mid-r3ca", () => Build("<DvLabel text=\"A\" content-align=\"mid-r3ca\"/>"));
        Assert.Single(w);
        Assert.Equal(ContentVAlign.Middle, Find(Build("<DvLabel text=\"A\" content-align=\"middle-center\"/>"), "DvLabel").Style.ContentVertical);
        _ = b;
    }

    [Fact]
    public void Knob_InputMode_Unknown_DragWithWarning()
    {
        var (b, w) = Watch("swipe-r3knob", () => Build("<DvKnob input-mode=\"swipe-r3knob\"/>"));
        Assert.Equal(KnobInputMode.Drag, Find(b, "DvKnob").KnobInput);
        Assert.Single(w);
    }

    [Fact]
    public void Column_UnknownValues_DefaultWithWarning()
    {
        var (b, w) = Watch("r3col", () => Build(
            "<DvDataGrid items=\"{Rows}\"><DvColumn field=\"A\" align=\"mid-r3col\" sort=\"up-r3col\" cell-type=\"knob-r3col\"/>" +
            "<DvSummaryRow fn=\"total-r3col\"/></DvDataGrid>"));
        var g = Find(b, "DvDataGrid").Grid!;
        Assert.Null(g.Columns[0].Align);
        Assert.Null(g.Columns[0].InitialSortDesc);
        Assert.Equal(Devinno.UI.Grid.GridCellType.Text, g.Columns[0].CellType);
        Assert.Equal(Devinno.UI.Grid.GridAggregate.None, g.Summaries[0].Fn);
        Assert.Equal(4, w.Count);
    }

    [Fact]
    public void Process_UnknownValues_DefaultWithWarning_FlipVhAlias()
    {
        const string body = """
            <DvTerminal name="IN"/>
            <DvPump name="P1" flip="vh"/>
            <DvPump name="P2" flip="x-r3proc"><DvAttach side="middle-r3proc"><DvLabel text="a"/></DvAttach></DvPump>
            <DvLine from="IN" to="P1.in" arrow="tip-r3proc"/>
            """;
        var ((_, d), w) = Watch("r3proc", () => ProcessTests.Start(body, "look=\"fancy-r3proc\""));
        var p1 = d.Nodes.Single(n => n.Name == "P1");
        Assert.True(p1.FlipH && p1.FlipV);
        var p2 = d.Nodes.Single(n => n.Name == "P2");
        Assert.False(p2.FlipH || p2.FlipV);
        Assert.False(d.Rich);
        Assert.False(d.Lines[0].ArrowEnd || d.Lines[0].ArrowStart);
        Assert.Equal(4, w.Count);
        Assert.All(w, line => Assert.Contains("is not one of", line));
    }

    // 등록 심볼(사용자 DvSymbol 태그)도 장비 공통 rotate·flip 을 같은 목록으로 읽는다(TagSchema 에 태그가 없어도)
    [Fact]
    public void UserSymbol_RotateFlip_UseEquipmentValues()
    {
        var reg = new Devinno.UI.Components.ComponentRegistry();
        reg.Register("""
            <DvSymbol name="BoxR3" view-box="0 0 40 40">
              <DvPort name="in" side="left" at="0.5" role="in"/>
              <DvPort name="out" side="right" at="0.5" role="out"/>
              <DvPass from="in" to="out"/>
            </DvSymbol>
            """);
        var ((_, d), w) = Watch("BoxR3", () => ProcessTests.Start("<BoxR3 name=\"B\" rotate=\"90\" flip=\"H\"/>", registry: reg));
        var b = d.Nodes.Single(n => n.Name == "B");
        Assert.Equal(90, b.Rotate);
        Assert.True(b.FlipH);
        Assert.Empty(w);
    }

    // ── R4 바인딩 ──

    [Fact]
    public void Column_ValueAttributes_AreBound()
    {
        var g = Find(Build(
            "<DvDataGrid items=\"{Rows}\"><DvColumn field=\"A\" align=\"{Al}\" format=\"{Fmt}\" sort=\"{So}\" cell-type=\"{Ct}\" icon=\"{Ic}\" variant=\"{Va}\"/>" +
            "<DvSummaryRow fn=\"{Fn}\"><DvSummary field=\"A\" fn=\"{Fn}\" format=\"{Fmt}\"/></DvSummaryRow></DvDataGrid>"), "DvDataGrid").Grid!;
        var c = g.Columns[0];
        Assert.Equal(TextAlign.Center, c.Align);
        Assert.Equal("0.0", c.Format);
        Assert.True(c.InitialSortDesc);
        Assert.Equal(Devinno.UI.Grid.GridCellType.Button, c.CellType);
        Assert.Equal("fa-play", c.Icon);
        Assert.Equal("danger", c.Variant);
        Assert.Equal(Devinno.UI.Grid.GridAggregate.Max, g.Summaries[0].Fn);
        Assert.Equal(Devinno.UI.Grid.GridAggregate.Max, g.Summaries[0].Cells[0].Fn);
        Assert.Equal("0.0", g.Summaries[0].Cells[0].Format);
    }

    [Fact]
    public void Knob_InputMode_IsBound()
        => Assert.Equal(KnobInputMode.Tap, Find(Build("<DvKnob input-mode=\"{Mode}\"/>"), "DvKnob").KnobInput);

    [Fact]
    public void Process_NamesAndReferences_AreRaw_WithWarning()
    {
        const string body = """
            <DvPump name="P1"/>
            <DvPump name="{X}"/>
            <DvInstrument name="PI1" tag="PI" on="{X}"/>
            """;
        var ((_, d), w) = Watch("not bindable", () => ProcessTests.Start(body, vm: new Vm()));
        Assert.Contains(d.Nodes, n => n.Name == "{X}");
        Assert.Equal("{X}", d.Instruments.Single().OnRef);
        Assert.Equal(2, w.Count);
    }
}
