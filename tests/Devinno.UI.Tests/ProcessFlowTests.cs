using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 흐름 판단(자동 + DvLine flow 덮어쓰기)과 흐름 애니메이션(flow-animation, 기본 끔)
public class ProcessFlowTests
{
    public sealed class Vm
    {
        public double Level { get; set; } = 60;
        public bool P1 { get; set; } = true;
        public bool P2 { get; set; }
        public bool V1 { get; set; } = true;
        public bool? Meter { get; set; }
    }

    // IN → T1(수위) → P1·P2(병렬) → V1 → OUT
    private const string Plant = """
        <DvTerminal name="IN" text="IN"/>
        <DvVessel name="T1" level="{Level}"/>
        <DvPump name="P1" running="{P1}"/>
        <DvPump name="P2" running="{P2}"/>
        <DvValve name="V1" open="{V1}"/>
        <DvTerminal name="OUT" text="OUT"/>
        <DvLine from="IN" to="T1.top"/>
        <DvLine from="T1.bottom" to="P1.in"/>
        <DvLine from="T1.bottom" to="P2.in"/>
        <DvLine from="P1.out" to="V1.in"/>
        <DvLine from="P2.out" to="V1.in"/>
        <DvLine from="V1.out" to="OUT"/>
        """;

    private static ProcessDiagram Flow(string body, Vm? vm = null, string attrs = "", ComponentRegistry? reg = null)
    {
        var s = new DvSurface($"<DvScreen><DvProcess {attrs}>{body}</DvProcess></DvScreen>", vm ?? new Vm(), DvTheme.Light, reg);
        s.Layout(1200, 800);
        return ProcessTests.Diagram(s);
    }

    private static bool F(ProcessDiagram d, string from, string to) => d.Lines.Single(l => l.FromRef == from && l.ToRef == to).Flowing;

    [Fact]
    public void RunningPump_OpenValve_PathFlows_StoppedPumpBranchDoesNot()
    {
        var d = Flow(Plant);
        Assert.True(F(d, "IN", "T1.top"));         // 바깥에서 탱크로
        Assert.True(F(d, "T1.bottom", "P1.in"));   // 운전 펌프 쪽
        Assert.True(F(d, "P1.out", "V1.in"));
        Assert.True(F(d, "V1.out", "OUT"));
        Assert.False(F(d, "T1.bottom", "P2.in"));  // 정지 펌프 쪽은 막힘
        Assert.False(F(d, "P2.out", "V1.in"));
    }

    [Fact]
    public void ClosedValve_StopsUpstreamAndDownstream()
    {
        var d = Flow(Plant, new Vm { V1 = false });
        Assert.False(F(d, "T1.bottom", "P1.in")); // 받는 곳까지 못 이어짐 — 막힌 배관
        Assert.False(F(d, "P1.out", "V1.in"));
        Assert.False(F(d, "V1.out", "OUT"));
        Assert.True(F(d, "IN", "T1.top"));        // 탱크로 들어오는 것은 그대로
    }

    [Fact]
    public void EmptyTank_NoSourceDownstream()
    {
        var d = Flow(Plant, new Vm { Level = 0 });
        Assert.False(F(d, "T1.bottom", "P1.in"));
        Assert.False(F(d, "V1.out", "OUT"));
        Assert.True(F(d, "IN", "T1.top"));
    }

    [Fact]
    public void StoppedPumps_StopDischarge()
    {
        var d = Flow(Plant, new Vm { P1 = false });
        Assert.All(d.Lines.Where(l => l.FromRef != "IN"), l => Assert.False(l.Flowing));
    }

    [Fact]
    public void CheckValve_OneWayOnly()
    {
        var d = Flow("""
            <DvTerminal name="A" text="A"/><DvValve name="C" kind="check"/><DvTerminal name="B" text="B"/>
            <DvTerminal name="X" text="X"/><DvValve name="R" kind="check"/><DvTerminal name="Y" text="Y"/>
            <DvLine from="A" to="C.in"/><DvLine from="C.out" to="B"/>
            <DvLine from="X" to="R.out"/><DvLine from="R.in" to="Y"/>
            """);
        Assert.True(F(d, "A", "C.in"));   // 정방향 통과
        Assert.True(F(d, "C.out", "B"));
        Assert.False(F(d, "X", "R.out")); // 역방향은 막힘
        Assert.False(F(d, "R.in", "Y"));
    }

    [Fact]
    public void Recycle_LoopTerminates()
    {
        var d = Flow("""
            <DvVessel name="T" level="50"/><DvPump name="P" running="true"/><DvValve name="V" open="true"/>
            <DvLine from="T.bottom" to="P.in"/><DvLine from="P.out" to="V.in"/><DvLine from="V.out" to="T.side"/>
            """);
        Assert.All(d.Lines, l => Assert.True(l.Flowing));
    }

    [Fact]
    public void Override_False_BlocksDownstream_True_LightsLine()
    {
        var d = Flow(Plant.Replace("""<DvLine from="P1.out" to="V1.in"/>""", """<DvLine from="P1.out" to="V1.in" flow="{Meter}"/>"""),
            new Vm { Meter = false });
        Assert.False(F(d, "P1.out", "V1.in"));
        Assert.False(F(d, "V1.out", "OUT"));       // 뒤쪽도 멈춤
        Assert.False(F(d, "T1.bottom", "P1.in"));  // 받는 곳이 막혀 앞쪽도 흐름 없음

        var stopped = Flow(Plant.Replace("""<DvLine from="P2.out" to="V1.in"/>""", """<DvLine from="P2.out" to="V1.in" flow="{Meter}"/>"""),
            new Vm { P1 = false, Meter = true });
        Assert.True(F(stopped, "P2.out", "V1.in")); // 정지 펌프 뒤라도 실제 흐름이 있다고 알려 주면 흐름
        Assert.True(F(stopped, "V1.out", "OUT"));   // 뒤쪽으로 이어짐
    }

    [Fact]
    public void Override_NullBinding_FallsBackToAuto()
    {
        var d = Flow(Plant.Replace("""<DvLine from="V1.out" to="OUT"/>""", """<DvLine from="V1.out" to="OUT" flow="{Meter}"/>"""),
            new Vm { Meter = null });
        Assert.Null(d.Lines.Last().FlowOverride);
        Assert.True(F(d, "V1.out", "OUT"));
    }

    [Fact]
    public void FlowCrossesUnitPorts()
    {
        var reg = new ComponentRegistry();
        reg.Register("""
            <DvComponent name="Feed">
              <DvParam name="Run"/>
              <DvUnit direction="tb">
                <DvVessel name="T" level="50"/>
                <DvPump name="P" running="{Run}"/>
                <DvPort name="out" side="bottom"/>
                <DvLine from="T.bottom" to="P.in"/>
                <DvLine from="P.out" to="out"/>
              </DvUnit>
            </DvComponent>
            """);
        var on = Flow("""<Feed name="U1" Run="true"/><DvTerminal name="O" text="O"/><DvLine from="U1.out" to="O"/>""", reg: reg, attrs: "direction=\"tb\"");
        Assert.True(on.Lines.Single().Flowing);
        Assert.All(on.Nodes[0].Unit!.Lines, l => Assert.True(l.Flowing));

        var off = Flow("""<Feed name="U1" Run="false"/><DvTerminal name="O" text="O"/><DvLine from="U1.out" to="O"/>""", reg: reg, attrs: "direction=\"tb\"");
        Assert.False(off.Lines.Single().Flowing);
    }

    // ── 애니메이션 ──

    [Fact]
    public void Animation_OffByDefault_NoRepaintScheduled()
    {
        var s = new DvSurface($"<DvScreen><DvProcess>{Plant}</DvProcess></DvScreen>", new Vm(), DvTheme.Light);
        using var bmp = new SKBitmap(1200, 800);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 1200, 800, 1000);
        Assert.False(ProcessTests.Diagram(s).FlowAnimation);
        Assert.Null(s.NextAnimationDue(1000));
    }

    [Fact]
    public void Animation_On_SchedulesFrames_OnlyWhileFlowing()
    {
        var vm = new Vm();
        var s = new DvSurface($"<DvScreen><DvProcess flow-animation=\"true\">{Plant}</DvProcess></DvScreen>", vm, DvTheme.Light);
        using var bmp = new SKBitmap(1200, 800);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 1200, 800, 1000);
        Assert.Equal(1050, s.NextAnimationDue(1000)); // 50ms 뒤(초당 20프레임)

        // 모두 멈추면(입구 배관 덮어쓰기 없이는 바깥 공급이 남으므로 수위·공급 없는 구성으로) 예약 없음
        var idle = new DvSurface("""
            <DvScreen><DvProcess flow-animation="true">
              <DvVessel name="T" level="0"/><DvPump name="P"/><DvTerminal name="O" text="O"/>
              <DvLine from="T.bottom" to="P.in"/><DvLine from="P.out" to="O"/>
            </DvProcess></DvScreen>
            """, null, DvTheme.Light);
        idle.RenderTo(c, 1200, 800, 1000);
        Assert.Null(idle.NextAnimationDue(1000));
    }

    [Fact]
    public void Animation_DashesMoveWithTime()
    {
        var s = new DvSurface($"<DvScreen><DvProcess flow-animation=\"true\" look=\"plain\">{Plant}</DvProcess></DvScreen>", new Vm(), DvTheme.Light);
        using var a = new SKBitmap(1200, 800);
        using var b = new SKBitmap(1200, 800);
        using (var c = new SKCanvas(a)) { c.Clear(SKColors.White); s.RenderTo(c, 1200, 800, 0); }
        using (var c = new SKCanvas(b)) { c.Clear(SKColors.White); s.RenderTo(c, 1200, 800, 100); }
        var d = ProcessTests.Diagram(s);
        var line = d.Lines.Single(l => l.FromRef == "V1.out").G!;
        // 직선 구간 위 픽셀을 따라가며 두 시각의 색이 다른 곳이 있어야 한다(점선이 움직임)
        var p0 = line.Points[0];
        var p1 = line.Points[1];
        var changed = 0;
        for (var t = 0.2f; t < 0.8f; t += 0.02f)
        {
            var x = (int)(d.Origin.X + p0.X + (p1.X - p0.X) * t);
            var y = (int)(d.Origin.Y + p0.Y + (p1.Y - p0.Y) * t);
            if (a.GetPixel(x, y) != b.GetPixel(x, y))
                changed++;
        }

        Assert.True(changed > 3, $"changed {changed}");
    }

    [Fact]
    public void Animation_DashesMoveEveryFrame_LongAfterBoot()
    {
        // 시각은 부팅 후 ms — 30일 켜 두면 약 26억. float 로 계산하면 256ms 동안 점선이 같은 자리에 머물렀다
        const long t = 2_667_631_779;
        var s = new DvSurface($"<DvScreen><DvProcess flow-animation=\"true\" look=\"plain\">{Plant}</DvProcess></DvScreen>", new Vm(), DvTheme.Light);
        using var a = new SKBitmap(1200, 800);
        using var b = new SKBitmap(1200, 800);
        using (var c = new SKCanvas(a)) { c.Clear(SKColors.White); s.RenderTo(c, 1200, 800, t); }
        using (var c = new SKCanvas(b)) { c.Clear(SKColors.White); s.RenderTo(c, 1200, 800, t + 16); } // 한 프레임(16ms) 뒤
        Assert.False(a.GetPixelSpan().SequenceEqual(b.GetPixelSpan()), "dashes did not move in 16ms");
    }

    [Fact]
    public void ValueChange_RecomputesFlow_ButReusesLayout()
    {
        var vm = new Vm();
        var s = new DvSurface($"<DvScreen><DvProcess>{Plant}</DvProcess></DvScreen>", vm, DvTheme.Light);
        s.Layout(1200, 800);
        var graph = ProcessTests.Diagram(s).Graph;
        Assert.True(F(ProcessTests.Diagram(s), "V1.out", "OUT"));
        vm.V1 = false;
        s.Invalidate();
        s.Layout(1200, 800);
        Assert.Same(graph, ProcessTests.Diagram(s).Graph);
        Assert.False(F(ProcessTests.Diagram(s), "V1.out", "OUT"));
    }
}
