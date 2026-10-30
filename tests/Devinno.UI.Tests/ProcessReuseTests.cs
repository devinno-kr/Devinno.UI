using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Markup;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using Xunit;
using Xunit.Abstractions;

// 값만 바뀐 갱신(Invalidate → Layout)은 공정도 배치를 다시 계산하지 않고 재사용한다(dvml-process.md 13장 "Updates and cost")
public class ProcessReuseTests(ITestOutputHelper output)
{
    public sealed class Vm
    {
        public double RawLevel { get; set; } = 72;
        public double ReactorLevel { get; set; } = 88;
        public double TreatedLevel { get; set; } = 54;
        public bool Pump1Run { get; set; } = true;
        public bool Pump2Run { get; set; }
        public bool ValveOpen { get; set; } = true;
        public bool DosingRun { get; set; } = true;
        public bool AgitatorRun { get; set; } = true;
        public string FicText { get; set; } = "1,284 m³/h";
    }

    private const string Equipment = """
        <DvTerminal name="IN" text="RAW"/>
        <DvVessel name="T1" level="{RawLevel}" label="T-101"/>
        <DvPump name="P1" running="{Pump1Run}" label="P-101"/>
        <DvPump name="P2" running="{Pump2Run}" label="P-102"/>
        <DvValve name="FCV" kind="control" open="{ValveOpen}" label="FCV-101"/>
        <DvTerminal name="CHEM" text="PAC"/>
        <DvPump name="P3" kind="gear" running="{DosingRun}" label="P-301"/>
        <DvVessel name="R1" kind="reactor" level="{ReactorLevel}" label="R-201">
          <DvAgitator running="{AgitatorRun}"/>
        </DvVessel>
        <DvVessel name="T2" level="{TreatedLevel}" label="T-401"/>
        <DvLine from="IN" to="T1.top" arrow="end" medium="water"/>
        <DvLine from="T1.bottom" to="P1.in" medium="water"/>
        <DvLine from="T1.bottom" to="P2.in"/>
        <DvLine from="P1.out" to="FCV.in"/>
        <DvLine from="P2.out" to="FCV.in"/>
        <DvLine from="FCV.out" to="R1.top" arrow="end"/>
        <DvLine from="CHEM" to="P3.in" medium="acid"/>
        <DvLine from="P3.out" to="R1.side" arrow="end"/>
        <DvLine from="R1.bottom" to="T2.top" arrow="end" medium="water"/>
        """;

    private const string Instrument = """
        <DvInstrument name="FIC1" tag="FIC" number="101" on="FCV" panel="true" value="{FicText}"/>
        <DvSignal from="FIC1" to="FCV"/>
        """;

    // 조절 밸브 오른쪽에 고정 폭 붙임 상자(값 글자)
    private static string WithAttach(string equipment) => equipment.Replace(
        """<DvValve name="FCV" kind="control" open="{ValveOpen}" label="FCV-101"/>""",
        """<DvValve name="FCV" kind="control" open="{ValveOpen}" label="FCV-101"><DvAttach side="right"><DvLabel text="{FicText}" style="width:90"/></DvAttach></DvValve>""");

    private const string UnitDef = """
        <DvComponent name="Line">
          <DvParam name="U"/>
          <DvUnit direction="tb" label="{U.Name}">
            <DvTerminal name="IN" text="FEED"/>
            <DvVessel name="T" level="{U.Level}" label="{U.Name}"/>
            <DvValve name="V" kind="control" open="{U.Open}" label="FCV"/>
            <DvPort name="out" side="bottom"/>
            <DvLine from="IN" to="T.top"/>
            <DvLine from="T.bottom" to="V.in"/>
            <DvLine from="V.out" to="out"/>
            <DvInstrument name="FIC" tag="FIC" number="1" on="V" value="{U.Text}"/>
            <DvSignal from="FIC" to="V"/>
          </DvUnit>
        </DvComponent>
        """;

    public sealed class UnitState
    {
        public required string Name { get; init; }
        public double Level { get; set; } = 40;
        public bool Open { get; set; } = true;
        public string Text { get; set; } = "12.5 m³/h";
    }

    public sealed class UnitVm
    {
        public UnitState A { get; } = new() { Name = "LINE-A" };
        public UnitState B { get; } = new() { Name = "LINE-B" };
        public UnitState C { get; } = new() { Name = "LINE-C" };
    }

    private static string Markup(string body) =>
        $"""<DvStack style="padding:10"><DvProcess look="rich" flow-animation="true" style="height:480">{body}</DvProcess></DvStack>""";

    // 바깥과 유닛 안쪽(같은 소스는 한 번) 배치 계산 횟수의 합
    private static int Runs(ProcessDiagram d)
    {
        var sources = new HashSet<ElementNode>();
        void Walk(ProcessDiagram x)
        {
            sources.Add(x.Source);
            foreach (var n in x.Nodes)
                if (n.Unit is { } u)
                    Walk(u);
        }

        Walk(d);
        return sources.Sum(ProcessBuilder.LayoutRuns);
    }

    // 첫 Layout 뒤 값만 바꿔 가며 Invalidate → Layout 을 반복했을 때 늘어난 배치 계산 횟수
    private int ExtraRuns<T>(string markup, T vm, Action<T, int>? change = null, ComponentRegistry? registry = null, int passes = 10)
    {
        var s = new DvSurface(markup, vm, DvTheme.Light, registry);
        s.Layout(940, 548);
        var first = Runs(ProcessTests.Diagram(s));
        for (var i = 0; i < passes; i++)
        {
            change?.Invoke(vm, i);
            s.Invalidate();
            s.Layout(940, 548);
        }

        var extra = Runs(ProcessTests.Diagram(s)) - first;
        output.WriteLine($"first {first}, extra {extra}");
        return extra;
    }

    private static void Toggle(Vm vm, int i)
    {
        vm.RawLevel = 30 + i;
        vm.ReactorLevel = 90 - i;
        vm.Pump1Run = i % 2 == 0;
        vm.Pump2Run = !vm.Pump1Run;
        vm.ValveOpen = i % 3 != 0;
    }

    [Fact]
    public void Control_NoInstrument_Reuses() =>
        Assert.Equal(0, ExtraRuns(Markup(Equipment), new Vm(), Toggle));

    [Fact]
    public void Instrument_WithSignal_ValuesOnly_Reuses() =>
        Assert.Equal(0, ExtraRuns(Markup(Equipment + Instrument), new Vm(), Toggle));

    [Fact]
    public void Instrument_WithoutSignal_Reuses() =>
        Assert.Equal(0, ExtraRuns(Markup(Equipment + Instrument[..Instrument.IndexOf("<DvSignal", StringComparison.Ordinal)]), new Vm(), Toggle));

    [Fact]
    public void Attach_Only_Reuses() =>
        Assert.Equal(0, ExtraRuns(Markup(WithAttach(Equipment)), new Vm(), (vm, i) =>
        {
            Toggle(vm, i);
            vm.FicText = $"{1000 + i * 37:#,0} m³/h"; // 고정 폭 상자 — 글자가 바뀌어도 크기 같음
        }));

    [Fact]
    public void Instrument_TextWidthChange_RecomputesOnce_ThenReuses()
    {
        // 폭이 다른 두 글자를 번갈아 — 처음 바뀔 때만 계산하고, 그다음부터는 두 폭 모두 재사용
        var vm = new Vm();
        var s = new DvSurface(Markup(Equipment + Instrument), vm, DvTheme.Light);
        s.Layout(940, 548);
        var start = Runs(ProcessTests.Diagram(s));
        int warm = 0;
        for (var i = 0; i < 12; i++)
        {
            vm.FicText = i % 2 == 0 ? "8 m³/h" : "1,284 m³/h";
            s.Invalidate();
            s.Layout(940, 548);
            if (i == 1)
                warm = Runs(ProcessTests.Diagram(s));
        }

        var end = Runs(ProcessTests.Diagram(s));
        output.WriteLine($"start {start}, after first change {warm}, end {end}");
        Assert.True(warm > start);
        Assert.Equal(warm, end);
    }

    [Fact]
    public void Units_WithInstruments_Reuse()
    {
        var r = new ComponentRegistry();
        r.Register(UnitDef);
        const string plant = """
            <Line name="LA" U="{A}"/>
            <Line name="LB" U="{B}"/>
            <Line name="LC" U="{C}"/>
            <DvVessel name="R" level="50" label="R-1"/>
            <DvLine from="LA.out" to="R.top"/>
            <DvLine from="LB.out" to="R.top"/>
            <DvLine from="LC.out" to="R.top"/>
            """;
        Assert.Equal(0, ExtraRuns(Markup(plant), new UnitVm(), (vm, i) =>
        {
            vm.A.Level = 10 + i;
            vm.B.Open = i % 2 == 0;
            vm.C.Level = 90 - i;
        }, r));
    }
}
