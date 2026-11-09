using System.Diagnostics;
using System.Globalization;
using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using Xunit;

// 3단계 R1(참/거짓, D1 빈 값 = 거짓)·R7(숫자) — 모든 태그·핸들이 같은 해석 함수(AttrRules)를 쓴다.
// 참 = bool true · 0 아닌 숫자(글자로 쓴 숫자 포함) · true. 거짓 = bool false · 0 · false · 빈 값 · null. 그 밖의 글자 = 기본값 + 경고.
// 숫자 = Invariant, 숫자 아님 = 기본값 + 경고. 경고 값마다 고유한 글자를 써서 다른 테스트의 경고와 섞이지 않게 한다.
public class AttrRulesTests
{
    public sealed class Vm
    {
        public string Word { get; set; } = "abc-r1vm";
        public int Two { get; set; } = 2;
        public int Minus { get; set; } = -1;
        public double Interval { get; set; } = 250;
        public bool Yes { get; set; } = true;
        public string? Nothing { get; set; }
        public string Name { get; set; } = "abc";
        public bool Off { get; set; }
        public int Zero { get; set; }
        public int One { get; set; } = 1;
        public Devinno.UI.Trends.TrendBuffer Buf { get; } = new(10);
        public List<Devinno.UI.Alarms.AlarmRow> Alarms { get; } = new();
        public List<Row> Rows { get; } = [new()];
        public void Ack(long id) { }
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
        new LayoutEngine().Layout(MarkupParser.Parse($"<DvScreen>{body}</DvScreen>"), vm ?? new Vm(), 800, 600);

    private static LayoutBox? Find(LayoutBox b, string tag) =>
        b.Node.Tag == tag ? b : b.Children.Select(c => Find(c, tag)).FirstOrDefault(x => x is not null);

    // ── 해석 함수 ──

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("true", true)]
    [InlineData(" TRUE ", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("0", false)]
    [InlineData("-1", true)]
    [InlineData("2", true)]
    [InlineData("0.0", false)]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(0.5, true)]
    public void ParseBool_CommonRule(object? value, bool expected) => Assert.Equal(expected, AttrRules.ParseBool(value));

    [Theory]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("abc")]
    [InlineData("NaN")]
    public void ParseBool_OtherText_IsNull(string value) => Assert.Null(AttrRules.ParseBool(value));

    [Fact]
    public void Bool_OtherText_DefaultAndWarning()
    {
        var (v, w) = Watch("yes-r1fn", () => AttrRules.Bool("yes-r1fn", true, "DvX", "flag"));
        Assert.True(v);
        var line = Assert.Single(w);
        Assert.Contains("DvX", line);
        Assert.Contains("flag", line);
    }

    [Fact]
    public void Number_NotANumber_DefaultAndWarning()
    {
        var (v, w) = Watch("abc-r7fn", () => AttrRules.Number("abc-r7fn", 100, "DvX", "max"));
        Assert.Equal(100, v);
        Assert.Single(w);
        Assert.Equal(12.5, AttrRules.Number("12.5", 0, "DvX", "max"));
        Assert.Equal(7, AttrRules.Number(7, 0, "DvX", "max"));
        Assert.Equal(3, AttrRules.Number(null, 3, "DvX", "max"));
        Assert.Equal(3, AttrRules.Number(" ", 3, "DvX", "max")); // 빈 값 = 없음(경고 없음)
    }

    [Fact]
    public void Number_IgnoresCurrentCulture()
    {
        var old = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Equal(1.5, AttrRules.Number("1.5", 0, "DvX", "max"));
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }

    // ── 공통 속성 ── (대소문자·앞뒤 공백 무시)

    [Theory]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData(" false ")]
    [InlineData(" 0 ")]
    [InlineData("{Nothing}")]   // null → 숨김
    [InlineData("")]            // 빈 값 → 숨김
    [InlineData("{Off}")]       // bool false
    public void Visible_FalsyValues_Hide(string raw) =>
        Assert.Null(Find(Build($"<DvLabel text=\"x\" visible=\"{raw}\"/>"), "DvLabel"));

    [Theory]
    [InlineData("{Name}")]      // 다른 글자 → 보임
    [InlineData("true")]
    [InlineData("{One}")]
    public void Visible_TruthyValues_Show(string raw) =>
        Assert.NotNull(Find(Build($"<DvLabel text=\"x\" visible=\"{raw}\"/>"), "DvLabel"));

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("{Off}")]
    [InlineData("{Zero}")]
    [InlineData("{Nothing}")]
    public void Disabled_FalsyValues_Enabled(string raw) =>
        Assert.False(Find(Build($"<DvButton text=\"x\" disabled=\"{raw}\"/>"), "DvButton")!.Disabled);

    [Fact]
    public void Visible_OtherText_ShowsWithWarning()
    {
        var (b, w) = Watch("abc-r1vis", () => Build("<DvLabel text=\"x\" visible=\"abc-r1vis\"/>"));
        Assert.NotNull(Find(b, "DvLabel"));
        Assert.Single(w);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("TRUE")]
    [InlineData(" true ")]
    [InlineData("{One}")]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("{Two}")]
    public void Disabled_TrueOrNonZero_Disables(string raw) =>
        Assert.True(Find(Build($"<DvButton text=\"x\" disabled=\"{raw}\"/>"), "DvButton")!.Disabled);

    [Fact]
    public void Disabled_OtherText_EnabledWithWarning()
    {
        var (b, w) = Watch("abc-r1vm", () => Build("<DvButton text=\"x\" disabled=\"{Word}\"/>"));
        Assert.False(Find(b, "DvButton")!.Disabled);
        Assert.Single(w);
    }

    [Fact]
    public void ComponentInstance_DisabledBoundNumber_Disables()
    {
        var reg = new Devinno.UI.Components.ComponentRegistry();
        reg.Register("<DvComponent name=\"Tile\"><DvButton text=\"t\"/></DvComponent>");
        var root = new LayoutEngine().Layout(MarkupParser.Parse("<DvScreen><Tile disabled=\"{Two}\"/></DvScreen>"), new Vm(), 800, 600, reg);
        Assert.True(Find(root, "DvButton")!.Disabled);
    }

    // ── "있으면 켜짐" 특례 제거 ──

    [Fact]
    public void TrendArea_Yes_OffWithWarning()
    {
        var (b, w) = Watch("yes-r1area", () => Build("<DvTrend source=\"{Buf}\" area=\"yes-r1area\"/>"));
        Assert.False(Find(b, "DvTrend")!.Trend!.Area);
        Assert.Single(w);
    }

    [Fact]
    public void TrendSmooth_EmptyIsFalse_OtherTextDefault()
    {
        Assert.False(Find(Build("<DvTrend source=\"{Buf}\" smooth=\"\"/>"), "DvTrend")!.Trend!.Smooth);
        var (b, w) = Watch("on-r1smooth", () => Build("<DvTrend source=\"{Buf}\" smooth=\"on-r1smooth\"/>"));
        Assert.True(Find(b, "DvTrend")!.Trend!.Smooth);
        Assert.Single(w);
    }

    [Fact]
    public void ShowAck_Yes_ShownWithWarning_EmptyHides()
    {
        var (b, w) = Watch("yes-r1ack", () => Build("<DvAlarmList items=\"{Alarms}\" show-ack=\"yes-r1ack\"/>"));
        Assert.True(Find(b, "DvAlarmList")!.AlarmShowAck); // 기본값(onack 없음이어도 show-ack 기본 = 보임)
        Assert.Single(w);
        Assert.False(Find(Build("<DvAlarmList items=\"{Alarms}\" onack=\"Ack\" show-ack=\"\"/>"), "DvAlarmList")!.AlarmShowAck);
        Assert.False(Find(Build("<DvAlarmList items=\"{Alarms}\" onack=\"Ack\" show-ack=\"0\"/>"), "DvAlarmList")!.AlarmShowAck);
    }

    [Fact]
    public void TickLabelsAndZoneMarks_ZeroHides()
    {
        Assert.False(Find(Build("<DvGauge show-tick-labels=\"0\"/>"), "DvGauge")!.GaugeTickLabels);
        Assert.False(Find(Build("<DvTank show-zone-marks=\"0\"/>"), "DvTank")!.TankZoneMarks);
        var (b, w) = Watch("yes-r1tick", () => Build("<DvGauge show-tick-labels=\"yes-r1tick\"/>"));
        Assert.True(Find(b, "DvGauge")!.GaugeTickLabels);
        Assert.Single(w);
    }

    // ── ToBool("true" 만 참) 쓰던 곳 ──

    [Fact]
    public void Lamp_OnOne_IsOn()
    {
        var lamp = Find(Build("<DvLamp on=\"1\" blink=\"{Two}\"/>"), "DvLamp")!;
        Assert.True(lamp.LampOn);
        Assert.True(lamp.LampBlink);
    }

    [Fact]
    public void Lamp_OnYes_OffWithWarning()
    {
        var (b, w) = Watch("yes-r1lamp", () => Build("<DvLamp on=\"yes-r1lamp\"/>"));
        Assert.False(Find(b, "DvLamp")!.LampOn);
        Assert.Single(w);
    }

    [Fact]
    public void Toggle_ValueOne_IsOn_HandleToo()
    {
        Assert.True(Find(Build("<DvToggle value=\"1\"/>"), "DvToggle")!.ToggleOn);
        Assert.True(ValueConvert.ToBool("1"));
        Assert.True(ValueConvert.ToBool(-1));
        Assert.False(ValueConvert.ToBool("yes"));
    }

    [Fact]
    public void Grid_FlagOne_IsSet()
    {
        var g = Find(Build("<DvDataGrid items=\"{Rows}\"><DvColumn field=\"A\" sortable=\"1\" readonly=\"{Two}\"/></DvDataGrid>"), "DvDataGrid")!.Grid!;
        Assert.True(g.Columns[0].Sortable);
        Assert.True(g.Columns[0].ReadOnly);
    }

    // ── 공정도 Truthy·visible 특례 제거 ──

    private const string Plant = """
        <DvTerminal name="IN"/>
        <DvPump name="P1" running="{Minus}"/>
        <DvLine from="IN" to="P1.in"/>
        """;

    [Fact]
    public void Process_NegativeNumber_IsTrue()
    {
        var (_, d) = ProcessTests.Start(Plant + "<DvValve name=\"V1\" open=\"-1\" visible=\"-1\"/>", vm: new Vm());
        Assert.True(d.Nodes.Single(n => n.Name == "P1").On("running"));
        var v1 = d.Nodes.Single(n => n.Name == "V1");
        Assert.True(v1.On("open"));
    }

    [Fact]
    public void Process_VisibleOtherText_ShowsWithWarning_ZeroHides()
    {
        var ((_, d), w) = Watch("abc-r1pvis", () => ProcessTests.Start(Plant + "<DvValve name=\"V1\" visible=\"abc-r1pvis\"/>", vm: new Vm()));
        Assert.Contains(d.Nodes, n => n.Name == "V1");
        Assert.NotEmpty(w);
        var (_, d2) = ProcessTests.Start(Plant + "<DvValve name=\"V1\" visible=\"0\"/>", vm: new Vm());
        Assert.DoesNotContain(d2.Nodes, n => n.Name == "V1");
    }

    [Fact]
    public void Process_StateYes_OffWithWarning()
    {
        var ((_, d), w) = Watch("yes-r1run", () => ProcessTests.Start("<DvTerminal name=\"IN\"/><DvPump name=\"P1\" running=\"yes-r1run\"/><DvLine from=\"IN\" to=\"P1.in\"/>", vm: new Vm()));
        Assert.False(d.Nodes.Single(n => n.Name == "P1").On("running"));
        Assert.Single(w);
    }

    [Fact]
    public void Process_FlowAnimationAndPanel_One()
    {
        var (_, d) = ProcessTests.Start(Plant + "<DvInstrument name=\"PI1\" tag=\"PI\" on=\"P1\" panel=\"1\"/>", "flow-animation=\"1\"", vm: new Vm());
        Assert.True(d.FlowAnimation);
        Assert.True(d.Instruments.Single().Panel);
    }

    // ── 숫자(R7) ──

    [Fact]
    public void Gauge_MaxNotANumber_DefaultWithWarning()
    {
        var (b, w) = Watch("abc-r7max", () => Build("<DvGauge max=\"abc-r7max\"/>"));
        Assert.Equal(100f, Find(b, "DvGauge")!.GaugeMax);
        Assert.Single(w);
    }

    [Fact]
    public void BlinkInterval_Invariant_NotANumberWarns()
    {
        var old = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Equal(250, Find(Build("<DvLamp blink-interval=\"{Interval}\"/>"), "DvLamp")!.BlinkIntervalMs);
            Assert.Equal(1500, Find(Build("<DvLamp blink-interval=\"1.5e3\"/>"), "DvLamp")!.BlinkIntervalMs);
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }

        var (b, w) = Watch("abc-r7blink", () => Build("<DvLamp blink-interval=\"abc-r7blink\"/>"));
        Assert.Equal(500, Find(b, "DvLamp")!.BlinkIntervalMs);
        Assert.Single(w);
    }

    [Fact]
    public void Knob_StepNotANumber_DefaultWithWarning()
    {
        var (b, w) = Watch("abc-r7step", () => Build("<DvKnob step=\"abc-r7step\"/>"));
        Assert.Equal(1f, Find(b, "DvKnob")!.KnobStep);
        Assert.Single(w);
    }

    [Fact]
    public void Process_RowNotANumber_Warns()
    {
        var (_, w) = Watch("abc-r7row", () => ProcessTests.Start(Plant.Replace("<DvTerminal name=\"IN\"/>", "<DvTerminal name=\"IN\" row=\"abc-r7row\"/>"), vm: new Vm()));
        Assert.Single(w);
    }
}
