using Devinno.UI;
using System.Diagnostics;
using Devinno.UI.Process;
using SkiaSharp;
using Xunit;

// 면 위 자리(장비.면@위치) — 배관 끝과 계기 연결선(tap)이 장비 면의 정한 자리에 붙는다(도면의 노즐 N1·N6 …)
public class ProcessTapTests
{
    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    private static ProcessInstrument Inst(ProcessDiagram d, string name) => d.Instruments.Single(i => i.Name == name);

    private static SKRect Abs(ProcessNode host, SKRect local) =>
        new(host.G.X + local.Left, host.G.Y + local.Top, host.G.X + local.Right, host.G.Y + local.Bottom);

    private static SKPoint Abs(ProcessNode host, SKPoint local) => new(host.G.X + local.X, host.G.Y + local.Y);

    private static GraphLine G(ProcessDiagram d, string from, string to) => d.Lines.Single(l => l.FromRef == from && l.ToRef == to).G!;

    private sealed class Capture : TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && message.Contains("Devinno.UI process"))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message)
            => WriteLine(message);
    }

    private static List<string> Warnings(Action act)
    {
        var cap = new Capture();
        Trace.Listeners.Add(cap);
        try
        {
            act();
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        return cap.Lines;
    }

    private const string Tank = """
        <DvTerminal name="IN" text="IN"/>
        <DvVessel name="T1" level="{Level}"/>
        <DvTerminal name="OUT" text="OUT"/>
        """;

    [Theory]
    [InlineData("left@0.25", 0f, 0.25f)]
    [InlineData("right@0.9", 1f, 0.9f)]
    [InlineData("top@0.7", 0.7f, 0f)]
    [InlineData("bottom@0.3", 0.3f, 1f)]
    public void LineEnd_FaceAt_LandsOnThatPointOfTheFace(string port, float fx, float fy)
    {
        var (_, d) = ProcessTests.Start(Tank + $"""
            <DvLine from="IN" to="T1.{port}"/>
            <DvLine from="T1.bottom" to="OUT"/>
            """);
        Assert.All(d.Lines, l => Assert.False(l.Error));
        var sym = Abs(Node(d, "T1"), Node(d, "T1").Sym);
        var end = G(d, "IN", $"T1.{port}").To;
        Assert.Equal(sym.Left + sym.Width * fx, end.Point.X, 0.5);
        Assert.Equal(sym.Top + sym.Height * fy, end.Point.Y, 0.5);
        var side = port[..port.IndexOf('@')];
        Assert.Equal(side, end.Out.ToString().ToLowerInvariant());
    }

    [Fact]
    public void LineEnd_FaceAt_SamePointSharesOneNozzle()
    {
        var (_, d) = ProcessTests.Start(Tank + """
            <DvTerminal name="IN2" text="IN2"/>
            <DvLine from="IN" to="T1.left@0.2"/>
            <DvLine from="IN2" to="T1.left@.20"/>
            <DvLine from="T1.bottom" to="OUT"/>
            """);
        var a = G(d, "IN", "T1.left@0.2").To.Point;
        var b = G(d, "IN2", "T1.left@.20").To.Point;
        Assert.Equal(a, b);
    }

    [Fact]
    public void LineEnd_FaceAt_OutOfRange_ClampsWithWarning()
    {
        ProcessDiagram d = null!;
        var w = Warnings(() => (_, d) = ProcessTests.Start(Tank + """
            <DvLine from="IN" to="T1.left@1.5"/>
            <DvLine from="T1.bottom" to="OUT"/>
            """));
        var sym = Abs(Node(d, "T1"), Node(d, "T1").Sym);
        Assert.Equal(sym.Bottom, G(d, "IN", "T1.left@1.5").To.Point.Y, 0.5);
        Assert.Contains(w, x => x.Contains("left@1.5") && x.Contains("0") && x.Contains("1"));
    }

    [Theory]
    [InlineData("T1.middle@0.5", "top·bottom·left·right")]
    [InlineData("T1.left@abc", "0")]
    public void LineEnd_FaceAt_Invalid_IsAnError(string to, string hint)
    {
        ProcessDiagram d = null!;
        var w = Warnings(() => (_, d) = ProcessTests.Start(Tank + $"""
            <DvLine from="IN" to="{to}"/>
            <DvLine from="T1.bottom" to="OUT"/>
            """));
        Assert.True(d.Lines.Single(l => l.ToRef == to).Error);
        Assert.Contains(w, x => x.Contains(to) && x.Contains(hint));
    }

    [Fact]
    public void LineEnd_FaceAt_WorksOnAnyEquipment()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="IN"/>
            <DvValve name="V1" open="true"/>
            <DvLine from="IN" to="V1.in"/>
            <DvTerminal name="OUT" text="OUT"/>
            <DvLine from="V1.top@0.5" to="OUT"/>
            """);
        Assert.All(d.Lines, l => Assert.False(l.Error));
        var sym = Abs(Node(d, "V1"), Node(d, "V1").Sym);
        Assert.Equal(new SKPoint(sym.MidX, sym.Top), G(d, "V1.top@0.5", "OUT").From.Point);
    }

    [Fact]
    public void LineEnd_FaceAt_VesselWithLevel_IsASource()
    {
        var s = new DvSurface($"<DvScreen><DvProcess>{Tank}<DvLine from=\"IN\" to=\"T1.top\"/><DvLine from=\"T1.right@0.9\" to=\"OUT\"/></DvProcess></DvScreen>",
            new ProcessTests.Vm(), Devinno.UI.Styling.DvTheme.Light);
        s.Layout(1100, 700);
        var d = ProcessTests.Diagram(s);
        Assert.True(d.Lines.Single(l => l.FromRef == "T1.right@0.9").Flowing);
    }

    // ── 계기 연결선(tap) ──

    [Fact]
    public void Tap_FaceAt_BubbleOutsideThatPoint_OnNotNeeded()
    {
        var (_, d) = ProcessTests.Start(Tank + """
            <DvLine from="IN" to="T1.top"/>
            <DvLine from="T1.bottom" to="OUT"/>
            <DvInstrument name="LS" tag="L" number="0012" value="AL" tap="T1.left@0.8"/>
            <DvInstrument name="LT" tag="L" number="0011" tap="T1.top@0.7"/>
            """);
        var t1 = Node(d, "T1");
        var sym = Abs(t1, t1.Sym);

        var ls = Inst(d, "LS");
        Assert.Same(t1, ls.Host);
        var lsRect = Abs(t1, ls.Rect);
        var lsTap = Abs(t1, ls.TapFrom);
        Assert.Equal(new SKPoint(sym.Left, sym.Top + sym.Height * 0.8f), lsTap);
        Assert.Equal(lsTap.Y, lsRect.MidY, 0.5);                 // 연결선은 수평
        Assert.Equal(lsTap.X - ProcessBuilder.TapLength, lsRect.Right, 0.5);
        Assert.Equal(new SKPoint(lsRect.Right, lsRect.MidY), Abs(t1, ls.TapTo));
        Assert.True(Abs(t1, ls.ValueRect).Top > lsRect.MidY);       // 값 글자는 오른쪽으로 나가는 연결선 아래

        var lt = Inst(d, "LT");
        var ltRect = Abs(t1, lt.Rect);
        var ltTap = Abs(t1, lt.TapFrom);
        Assert.Equal(new SKPoint(sym.Left + sym.Width * 0.7f, sym.Top), ltTap);
        Assert.Equal(ltTap.X, ltRect.MidX, 0.5);                 // 연결선은 수직
        Assert.Equal(ltTap.Y - ProcessBuilder.TapLength, ltRect.Bottom, 0.5);
    }

    [Fact]
    public void Tap_Port_BubbleAbovePipeNearThePort()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="IN"/>
            <DvValve name="V1" open="true"/>
            <DvTerminal name="OUT" text="OUT"/>
            <DvLine from="IN" to="V1.in"/>
            <DvLine from="V1.out" to="OUT"/>
            <DvInstrument name="PI" tag="PI" number="13" tap="V1.in"/>
            """);
        var v1 = Node(d, "V1");
        var pi = Inst(d, "PI");
        var port = G(d, "IN", "V1.in").To.Point;
        var tap = Abs(v1, pi.TapFrom);
        var rect = Abs(v1, pi.Rect);
        Assert.Equal(port.Y, tap.Y, 0.5);                         // 배관 위(곧은 끝)
        Assert.True(tap.X < port.X - 1);                          // 포트에서 배관 쪽으로 나간 자리
        Assert.Equal(tap.X, rect.MidX, 0.5);
        Assert.Equal(tap.Y - ProcessBuilder.TapLength, rect.Bottom, 0.5);
    }

    [Fact]
    public void Tap_OnDisagrees_TapWinsWithWarning()
    {
        ProcessDiagram d = null!;
        var w = Warnings(() => (_, d) = ProcessTests.Start(Tank + """
            <DvLine from="IN" to="T1.top"/>
            <DvLine from="T1.bottom" to="OUT"/>
            <DvValve name="V9" open="true"/>
            <DvInstrument name="LX" tag="L" on="V9" tap="T1.left@0.5"/>
            """));
        Assert.Same(Node(d, "T1"), Inst(d, "LX").Host);
        Assert.Contains(w, x => x.Contains("LX") && x.Contains("tap"));
    }

    [Theory]
    [InlineData("T1.side", "@")]          // 위치가 정해지지 않는 면 포트
    [InlineData("NOPE.left@0.5", "NOPE")]
    public void Tap_Invalid_NotDrawn(string tap, string hint)
    {
        ProcessDiagram d = null!;
        var w = Warnings(() => (_, d) = ProcessTests.Start(Tank + $"""
            <DvLine from="IN" to="T1.top"/>
            <DvLine from="T1.bottom" to="OUT"/>
            <DvInstrument name="LX" tag="L" tap="{tap}"/>
            """));
        Assert.Null(Inst(d, "LX").Host);
        Assert.Contains(w, x => x.Contains("LX") && x.Contains(hint));
    }

    [Fact]
    public void Tap_SignalStartsFromTheTappedInstrument()
    {
        // 계기가 서로 다른 면에 따로 서면, 신호선은 from 계기 자기 가장자리에서 나간다
        var (_, d) = ProcessTests.Start(Tank + """
            <DvPump name="P1" running="true"/>
            <DvLine from="IN" to="T1.top"/>
            <DvLine from="T1.bottom" to="P1.in"/>
            <DvLine from="P1.out" to="OUT"/>
            <DvInstrument name="LT" tag="L" number="0011" tap="T1.top@0.7"/>
            <DvInstrument name="LS" tag="L" number="0012" tap="T1.left@0.8"/>
            <DvSignal from="LT" to="P1"/>
            """);
        var t1 = Node(d, "T1");
        var lt = Abs(t1, Inst(d, "LT").Rect);
        var start = d.Signals.Single().G!.From.Point;
        Assert.True(start.X >= lt.Left - 0.5f && start.X <= lt.Right + 0.5f && start.Y >= lt.Top - 0.5f && start.Y <= lt.Bottom + 0.5f);
    }
    // 같은 면에 나란히 선 계기(Test15 도면 그대로) — 신호선은 옆 버블 쪽 면으로 나가지 않는다(옆 버블 밑으로 지나가 그 계기에서 나오는 것처럼 보이던 문제)
    [Fact]
    public void Signal_DoesNotLeaveTowardASiblingBubble()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="UNIT 1" x="48" y="200"/>
            <DvVessel name="T1" level="60" label="T001" x="175" y="200"/>
            <DvValve name="DV" kind="ball" open="false" rotate="90" label="DRAIN" x="175" y="422"/>
            <DvValve name="CV0" kind="check" open="true" x="268" y="374"/>
            <DvPump name="P1" running="true" label="P001" x="370" y="378"><DvMotor running="true"/></DvPump>
            <DvValve name="PSV" kind="check" rotate="270" label="PSV 6 barg" x="432" y="262"/>
            <DvValve name="CV1" kind="check" open="true" x="478" y="358"/>
            <DvValve name="CV2" kind="check" open="true" x="558" y="358"/>
            <DvTerminal name="OUT" text="UNIT 3" x="646" y="366"/>
            <DvLine from="IN" to="T1.side" arrow="end"/>
            <DvLine from="T1.bottom" to="CV0.in"/>
            <DvLine from="T1.bottom" to="DV.in"/>
            <DvLine from="CV0.out" to="P1.in"/>
            <DvLine from="P1.out" to="CV1.in"/>
            <DvLine from="CV1.out" to="CV2.in"/>
            <DvLine from="CV2.out" to="OUT" arrow="end"/>
            <DvLine from="P1.out" to="PSV.in" flow="false"/>
            <DvLine from="PSV.out" to="T1.side" arrow="end" flow="false"/>
            <DvInstrument name="LT" tag="L" number="0011" on="T1"/>
            <DvInstrument name="LS" tag="L" number="0012" value="AL" on="T1"/>
            <DvSignal from="LT" to="P1"/>
            """, "placement=\"fixed\" base-size=\"696 480\"", 696, 480);
        var t1 = Node(d, "T1");
        var (lt, ls) = (Abs(t1, Inst(d, "LT").Rect), Abs(t1, Inst(d, "LS").Rect));
        Assert.True(ls.Left > lt.Right);                          // 나란히(LS 가 오른쪽)
        var pts = d.Signals.Single().G!.Points;
        for (var k = 1; k < pts.Count; k++)
        {
            var (a, b) = (pts[k - 1], pts[k]);
            var seg = new SKRect(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
            Assert.False(seg.IntersectsWith(ls) || (seg.Height == 0 && seg.Top > ls.Top && seg.Top < ls.Bottom && seg.Left < ls.Right && seg.Right > ls.Left),
                $"segment {a}->{b} crosses the sibling bubble {ls}");
        }
    }
    // 신호선 화살표·라벨, 모터가 있는 펌프로 가는 신호선은 모터에 닿는다(도면: SH ---▼ M)
    [Fact]
    public void Signal_ArrowLabel_EndsOnPumpMotor()
    {
        var (_, d) = ProcessTests.Start(Tank + """
            <DvPump name="P1" running="true"><DvMotor running="true"/></DvPump>
            <DvLine from="IN" to="T1.top"/>
            <DvLine from="T1.bottom" to="P1.in"/>
            <DvLine from="P1.out" to="OUT"/>
            <DvInstrument name="LT" tag="L" tap="T1.top@0.7"/>
            <DvSignal from="LT" to="P1" arrow="end" label="SH"/>
            """);
        var s = d.Signals.Single();
        Assert.True(s.ArrowEnd);
        Assert.False(s.ArrowStart);
        Assert.Equal("SH", s.Label);
        var p1 = Node(d, "P1");
        var motor = Abs(p1, p1.AccessoryRects.Single().Rect);
        var end = s.G!.To.Point;
        Assert.True(end.X >= motor.Left - 0.5f && end.X <= motor.Right + 0.5f && end.Y >= motor.Top - 0.5f && end.Y <= motor.Bottom + 0.5f,
            $"end {end} is not on the motor {motor}");
    }
    // 용기 높이(height) — 곧은 몸통만 늘어나고 폭·머리 모양은 그대로, 아래 포트는 새 바닥에
    [Fact]
    public void VesselHeight_StretchesBodyOnly()
    {
        ProcessDiagram d = null!;
        var w = Warnings(() => (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="IN"/>
            <DvVessel name="T1" height="240"/>
            <DvVessel name="T2" height="9999"/>
            <DvTerminal name="OUT" text="OUT"/>
            <DvLine from="IN" to="T1.top"/>
            <DvLine from="T1.bottom" to="T2.top"/>
            <DvLine from="T2.bottom" to="OUT"/>
            """, w: 1400, h: 1400));
        var t1 = Node(d, "T1");
        Assert.Equal(100, t1.Sym.Width, 0.5);
        Assert.Equal(240, t1.Sym.Height, 0.5);
        var vessel = t1.Def!.Layers.Single(l => l.Class == "vessel").Path.Bounds;
        Assert.Equal(4, vessel.Top, 0.5);                         // 위 머리는 그대로
        Assert.Equal(236, vessel.Bottom, 0.5);                    // 아래 머리는 90 내려감
        var sym = Abs(t1, t1.Sym);
        Assert.Equal(sym.Bottom, G(d, "T1.bottom", "T2.top").From.Point.Y, 0.5);
        Assert.Equal(600, Node(d, "T2").Sym.Height, 0.5);         // 4배까지
        Assert.Contains(w, x => x.Contains("9999"));
    }

    [Fact]
    public void LineLabel_Parsed()
    {
        var (_, d) = ProcessTests.Start(Tank + """
            <DvLine from="IN" to="T1.top" label="01-100-PE-N"/>
            <DvLine from="T1.bottom" to="OUT"/>
            """);
        Assert.Equal("01-100-PE-N", d.Lines.Single(l => l.FromRef == "IN").Label);
    }
}
