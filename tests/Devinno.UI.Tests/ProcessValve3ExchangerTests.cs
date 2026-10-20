using Devinno.UI.Process;
using SkiaSharp;
using Xunit;

// 3방 밸브(DvValve3 a·b·c, open-b·open-c)와 열교환기(DvExchanger in·out·in2·out2) — 흐름 판단, 옆면 포트 장비의 자리, 이름표가 포트를 가리지 않음.
public class ProcessValve3ExchangerTests
{
    // 분기: 펌프 → 3방 밸브 → (b) 열교환기 → 탱크 / (c) 배수. 열교환기 열매체는 위(공급) → 아래(회수)
    internal const string Divert = """
        <DvVessel name="T1" level="60" label="T-1"/>
        <DvPump name="P1" running="true" label="P-1"/>
        <DvValve3 name="V3" kind="control" open-b="{B}" open-c="{C}" label="TV-301"/>
        <DvExchanger name="E1" label="E-101"/>
        <DvTerminal name="CWS" text="CW SUPPLY"/>
        <DvTerminal name="CWR" text="CW RETURN"/>
        <DvVessel name="T2" level="30" label="T-2"/>
        <DvTerminal name="DR" text="DRAIN"/>
        <DvLine from="T1.bottom" to="P1.in"/>
        <DvLine from="P1.out" to="V3.a"/>
        <DvLine from="V3.b" to="E1.in"/>
        <DvLine from="V3.c" to="DR"/>
        <DvLine from="CWS" to="E1.in2"/>
        <DvLine from="E1.out2" to="CWR"/>
        <DvLine from="E1.out" to="T2.top"/>
        """;

    // 혼합: 두 입구(b·c) → 공통(a) → 판형 열교환기. 공통 출구가 흐름 쪽(오른쪽)에 오도록 flip="h"
    internal const string Mix = """
        <DvTerminal name="HOT" text="HOT"/>
        <DvTerminal name="COLD" text="COLD"/>
        <DvValve3 name="V3" open-b="{B}" open-c="{C}" label="MV-1" flip="h"/>
        <DvExchanger name="E2" kind="plate" label="E-201"/>
        <DvTerminal name="STM" text="STEAM"/>
        <DvTerminal name="CND" text="CONDENSATE"/>
        <DvTerminal name="OUT" text="PRODUCT"/>
        <DvLine from="HOT" to="V3.b"/>
        <DvLine from="COLD" to="V3.c"/>
        <DvLine from="V3.a" to="E2.in"/>
        <DvLine from="STM" to="E2.in2"/>
        <DvLine from="E2.out2" to="CND"/>
        <DvLine from="E2.out" to="OUT"/>
        """;

    public sealed class Vm
    {
        public bool B { get; set; } = true;
        public bool C { get; set; }
    }

    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    private static ProcessLine Line(ProcessDiagram d, string from, string to) => d.Lines.Single(l => l.FromRef == from && l.ToRef == to);

    private static SKRect SymAbs(ProcessNode n) => new(n.G.X + n.Sym.Left, n.G.Y + n.Sym.Top, n.G.X + n.Sym.Right, n.G.Y + n.Sym.Bottom);

    [Theory]
    [InlineData(Divert, "")]
    [InlineData(Divert, "look=\"rich\"")]
    [InlineData(Mix, "")]
    [InlineData(Mix, "look=\"rich\"")]
    public void Layout_Clean_NoCrossings(string body, string attrs)
    {
        var (_, d) = ProcessTests.Start(body, attrs, vm: new Vm());
        Assert.Empty(ProcessTests.Violations(d));
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
    }

    [Fact]
    public void SidePortEquipment_SitsOnThatSide()
    {
        var (_, d) = ProcessTests.Start(Divert, vm: new Vm());
        // 가지(c, 아래) → 배수는 열교환기보다 아래, 열매체 공급(in2, 위)은 위, 회수(out2, 아래)는 아래
        Assert.True(Node(d, "DR").G.Y > Node(d, "E1").G.Y);
        Assert.True(SymAbs(Node(d, "CWS")).Bottom <= SymAbs(Node(d, "E1")).Top);
        Assert.True(SymAbs(Node(d, "CWR")).Top >= SymAbs(Node(d, "E1")).Bottom);
    }

    [Fact]
    public void TopToBottom_Rotated_Clean()
    {
        var (_, d) = ProcessTests.Start("""
            <DvVessel name="T1" level="60" label="T-1"/>
            <DvPump name="P1" running="true" label="P-1" rotate="90"/>
            <DvValve3 name="V3" open-c="true" label="XV-3" rotate="90"/>
            <DvExchanger name="E1" label="E-101" rotate="90"/>
            <DvTerminal name="A" text="TO T-2"/>
            <DvTerminal name="B" text="TO DRAIN"/>
            <DvLine from="T1.bottom" to="P1.in"/>
            <DvLine from="P1.out" to="V3.a"/>
            <DvLine from="V3.b" to="E1.in"/>
            <DvLine from="E1.out" to="A"/>
            <DvLine from="V3.c" to="B"/>
            """, "direction=\"tb\"", 1100, 900);
        Assert.Empty(ProcessTests.Violations(d));
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Valve3_Divert_FlowFollowsOpenBranches(bool b, bool c)
    {
        var (_, d) = ProcessTests.Start(Divert, vm: new Vm { B = b, C = c });
        Assert.Equal(b, Line(d, "V3.b", "E1.in").Flowing);
        Assert.Equal(b, Line(d, "E1.out", "T2.top").Flowing);
        Assert.Equal(c, Line(d, "V3.c", "DR").Flowing);
        Assert.Equal(b || c, Line(d, "P1.out", "V3.a").Flowing);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Valve3_Mix_FlowsBackwardThroughBranches(bool b, bool c)
    {
        var (_, d) = ProcessTests.Start(Mix, vm: new Vm { B = b, C = c });
        Assert.Equal(b, Line(d, "HOT", "V3.b").Flowing);
        Assert.Equal(c, Line(d, "COLD", "V3.c").Flowing);
        Assert.Equal(b || c, Line(d, "V3.a", "E2.in").Flowing);
    }

    [Fact]
    public void Exchanger_TwoPaths_Independent()
    {
        // 공정 쪽이 멈춰도(밸브 둘 다 닫힘) 열매체 쪽은 흐른다 — 두 길은 섞이지 않는다
        var (_, d) = ProcessTests.Start(Divert, vm: new Vm { B = false, C = false });
        Assert.True(Line(d, "CWS", "E1.in2").Flowing);
        Assert.True(Line(d, "E1.out2", "CWR").Flowing);
        Assert.False(Line(d, "E1.out", "T2.top").Flowing);
    }

    [Fact]
    public void Valve3Control_SignalLandsOnActuatorTop()
    {
        var (_, d) = ProcessTests.Start(Divert + """
            <DvInstrument name="TIC" tag="TIC" number="301" on="V3"/>
            <DvSignal from="TIC" to="V3"/>
            """, vm: new Vm());
        var v = Node(d, "V3");
        var top = v.Transform.MapPoint(new SKPoint(30, 4.5f));
        var end = d.Signals.Single().G!.Points[^1];
        Assert.Equal(v.G.X + top.X, end.X, 0.5);
        Assert.Equal(v.G.Y + top.Y, end.Y, 0.5);
    }

    [Theory]
    [InlineData("shell")]
    [InlineData("plate")]
    public void Exchanger_Label_ClearOfBottomPort(string kind)
    {
        // 네 면을 다 쓰는 열교환기 — 이름표가 아래 포트(out2) 앞을 막지 않는다
        var (_, d) = ProcessTests.Start($"""
            <DvTerminal name="A" text="A"/><DvTerminal name="B" text="B"/>
            <DvTerminal name="C" text="C"/><DvTerminal name="D" text="D"/>
            <DvExchanger name="E" kind="{kind}" label="E-101 LONG"/>
            <DvLine from="A" to="E.in"/><DvLine from="E.out" to="B"/>
            <DvLine from="C" to="E.in2"/><DvLine from="E.out2" to="D"/>
            """);
        var e = Node(d, "E");
        var port = d.Lines.Single(l => l.FromRef == "E.out2").G!.From.Point;
        var label = new SKRect(e.G.X + e.LabelRect.Left, e.G.Y + e.LabelRect.Top, e.G.X + e.LabelRect.Right, e.G.Y + e.LabelRect.Bottom);
        Assert.True(port.X <= label.Left - 12 || port.X >= label.Right - 0.01f + 12 || label.Top > port.Y + 40, $"label {label} port {port}");
        Assert.Empty(ProcessTests.Violations(d));
        Assert.DoesNotContain(d.Lines, l => l.G!.Forced);
    }

    [Fact]
    public void UnknownKind_FallsBackToDefault()
    {
        var (_, d) = ProcessTests.Start("""<DvValve3 name="V" kind="nope"/><DvExchanger name="E" kind="nope"/>""");
        Assert.Equal("valve3-ball", Node(d, "V").Def!.Name);
        Assert.Equal("exchanger-shell", Node(d, "E").Def!.Name);
    }
}
