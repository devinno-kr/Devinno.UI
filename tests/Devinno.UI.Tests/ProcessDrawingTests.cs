using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 도면 맞추기(placement="fixed") — 계기 x·y(버블 가운데, 장비 차지 영역과 무관)와 배관 via(지정한 점을 지나는 경로)
public class ProcessDrawingTests
{
    private const string Fixed = "placement=\"fixed\"";

    // 일직선 공정 — 포트 줄은 모두 y=300
    private const string Line = """
        <DvTerminal name="IN" text="FEED" x="80" y="300"/>
        <DvValve name="V1" kind="control" open="true" label="V-1" x="400" y="292"/>
        <DvInline name="F1" kind="flowmeter" x="620" y="292"/>
        <DvTerminal name="OUT" text="OUT" x="860" y="300"/>
        <DvLine from="IN" to="V1.in"/>
        <DvLine from="V1.out" to="F1.in"/>
        <DvLine from="F1.out" to="OUT"/>
        """;

    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    private static ProcessInstrument Inst(ProcessDiagram d, string name) => d.Instruments.Single(i => i.Name == name);

    private static SKRect Abs(ProcessNode host, SKRect r) => new(host.G.X + r.Left, host.G.Y + r.Top, host.G.X + r.Right, host.G.Y + r.Bottom);

    private static SKRect Bubble(ProcessDiagram d, string name) => Abs(Inst(d, name).Host!, Inst(d, name).Rect);

    private static SKPoint Center(ProcessNode n) => new(n.G.X + n.Sym.MidX, n.G.Y + n.Sym.MidY);

    private static SKPoint Plus(ProcessNode host, SKPoint p) => new(host.G.X + p.X, host.G.Y + p.Y);

    private static bool SegmentHitsRect(SKPoint a, SKPoint b, SKRect r)
    {
        if (MathF.Abs(a.X - b.X) < 0.01f)
            return a.X > r.Left && a.X < r.Right && MathF.Max(a.Y, b.Y) > r.Top && MathF.Min(a.Y, b.Y) < r.Bottom;
        return a.Y > r.Top && a.Y < r.Bottom && MathF.Max(a.X, b.X) > r.Left && MathF.Min(a.X, b.X) < r.Right;
    }

    private static void AssertAt(float x, float y, SKPoint p)
    {
        Assert.Equal(x, p.X, 0.01);
        Assert.Equal(y, p.Y, 0.01);
    }

    private static void AssertOrthogonal(IReadOnlyList<SKPoint> pts)
    {
        for (var k = 0; k + 1 < pts.Count; k++)
            Assert.True(MathF.Abs(pts[k].X - pts[k + 1].X) < 0.01f || MathF.Abs(pts[k].Y - pts[k + 1].Y) < 0.01f, $"사선: {pts[k]} → {pts[k + 1]}");
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

    private static List<string> Messages(string marker, Action act)
    {
        var cap = new Capture(marker);
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

    // ── 계기 x·y ──

    [Fact]
    public void Instrument_XY_PutsBubbleCenterThere_AndLeavesHostAlone()
    {
        var (_, plain) = ProcessTests.Start(Line, Fixed);
        var (_, d) = ProcessTests.Start(Line + """<DvInstrument name="A" tag="TIC" number="1" on="V1" panel="true" x="150" y="80"/>""", Fixed);

        var b = Bubble(d, "A");
        Assert.Equal(150, b.MidX, 0.01);
        Assert.Equal(80, b.MidY, 0.01);
        Assert.Equal(Center(Node(plain, "V1")), Center(Node(d, "V1")));
        Assert.Equal(Node(plain, "V1").Footprint, Node(d, "V1").Footprint); // 멀리 있어도 장비 차지 영역은 그대로
        Assert.Equal(new SKPoint(b.MidX + d.Origin.X, b.MidY + d.Origin.Y), new SKPoint(Inst(d, "A").Box.Rect.MidX, Inst(d, "A").Box.Rect.MidY));
    }

    [Fact]
    public void Instrument_XY_WithBaseSize_MovesWithTheScale_BubbleKeepsItsSize()
    {
        var (_, d) = ProcessTests.Start(Line + """<DvInstrument name="A" tag="TT" number="1" on="V1" x="150" y="80"/>""", Fixed + " base-size=\"1100 700\" style=\"width:2200; height:1400\"", 2200, 1400);
        var (_, one) = ProcessTests.Start(Line + """<DvInstrument name="A" tag="TT" number="1" on="V1" x="150" y="80"/>""", Fixed + " base-size=\"1100 700\"", 1100, 700);

        Assert.Equal(300, Bubble(d, "A").MidX, 0.5);
        Assert.Equal(160, Bubble(d, "A").MidY, 0.5);
        Assert.Equal(Bubble(one, "A").Width, Bubble(d, "A").Width, 0.01);
    }

    [Fact]
    public void Instrument_XY_OthersOnTheSameHostStayAutomatic()
    {
        var (_, plain) = ProcessTests.Start(Line + """<DvInstrument name="B" tag="FIC" number="1" on="V1"/>""", Fixed);
        var (_, d) = ProcessTests.Start(Line + """
            <DvInstrument name="A" tag="TT" number="1" on="V1" x="150" y="80"/>
            <DvInstrument name="B" tag="FIC" number="1" on="V1"/>
            """, Fixed);
        Assert.Equal(Bubble(plain, "B"), Bubble(d, "B"));
    }

    [Fact]
    public void Instrument_XY_NotMovedWhenTheHostIsSnapped()
    {
        // V1 입구 줄이 IN 과 4px 어긋남 → V1 이 맞춰 옮겨진다. 버블은 적은 좌표 그대로
        var (_, d) = ProcessTests.Start(Line.Replace("y=\"292\"", "y=\"296\"") + """<DvInstrument name="A" tag="TT" number="1" on="V1" x="150" y="80"/>""", Fixed);
        Assert.Equal(292, Center(Node(d, "V1")).Y, 0.5);
        Assert.Equal(80, Bubble(d, "A").MidY, 0.01);
    }

    [Fact]
    public void Instrument_XY_InAutomaticPlacement_Warns_AndPlacesAutomatically()
    {
        ProcessDiagram with = null!, plain = null!;
        var lines = Messages("XYAUTO", () =>
        {
            (_, with) = ProcessTests.Start(ProcessTests.Serial + """<DvInstrument name="XYAUTO" tag="FT" number="1" on="F1" x="10" y="10"/>""");
            (_, plain) = ProcessTests.Start(ProcessTests.Serial + """<DvInstrument name="XYAUTO" tag="FT" number="1" on="F1"/>""");
        });
        Assert.Equal(Inst(plain, "XYAUTO").Rect, Inst(with, "XYAUTO").Rect);
        Assert.Contains(lines, l => l.Contains("used only with"));
    }

    [Fact]
    public void Instrument_OnlyX_Warns_AndPlacesAutomatically()
    {
        ProcessDiagram with = null!, plain = null!;
        var lines = Messages("XONLY", () =>
        {
            (_, with) = ProcessTests.Start(Line + """<DvInstrument name="XONLY" tag="FT" number="1" on="F1" x="10"/>""", Fixed);
            (_, plain) = ProcessTests.Start(Line + """<DvInstrument name="XONLY" tag="FT" number="1" on="F1"/>""", Fixed);
        });
        Assert.Equal(Inst(plain, "XONLY").Rect, Inst(with, "XONLY").Rect);
        Assert.Contains(lines, l => l.Contains("both"));
    }

    [Fact]
    public void Pipes_GoAroundAFreeBubble()
    {
        var (_, d) = ProcessTests.Start(Line + """<DvInstrument name="A" tag="TT" number="1" on="V1" x="240" y="300"/>""", Fixed);
        var r = Bubble(d, "A");
        foreach (var l in d.Lines)
        {
            Assert.False(l.G!.Forced);
            for (var k = 0; k + 1 < l.G.Points.Count; k++)
                Assert.False(SegmentHitsRect(l.G.Points[k], l.G.Points[k + 1], r), $"{l.FromRef}->{l.ToRef} 이 버블을 지남");
        }
    }

    [Fact]
    public void Signals_ReachFreeBubbles_OnTheirEdge()
    {
        var (_, d) = ProcessTests.Start(Line + """
            <DvInstrument name="A" tag="TIC" number="1" on="V1" panel="true" x="400" y="80"/>
            <DvInstrument name="B" tag="TY" number="1" on="V1" x="700" y="80"/>
            <DvSignal from="B" to="A"/>
            <DvSignal from="A" to="V1"/>
            """, Fixed);
        var (a, b) = (Bubble(d, "A"), Bubble(d, "B"));
        var between = d.Signals.Single(s => s.FromRef == "B").G!;
        AssertOrthogonal(between.Points);
        AssertAt(b.Left, b.MidY, between.Points[0]);
        AssertAt(a.Right, a.MidY, between.Points[^1]);

        // 자기 장비로 가는 신호선 — 곧게 아래로
        var down = d.Signals.Single(s => s.FromRef == "A").G!;
        AssertAt(a.MidX, a.Bottom, down.Points[0]);
    }

    [Fact]
    public void FreeBubble_OverEquipment_IsReported()
    {
        var (_, d) = ProcessTests.Start(Line + """<DvInstrument name="A" tag="TT" number="1" on="F1" x="400" y="292"/>""", Fixed);
        Assert.Contains(d.Graph!.Warnings, w => w.Contains("'V1' and 'A' overlap"));
    }

    [Fact]
    public void FreeBubble_ValueText_SitsBesideIt_AndDoesNotMoveAnything()
    {
        var (_, plain) = ProcessTests.Start(Line + """<DvInstrument name="A" tag="TT" number="1" on="V1" x="150" y="80"/>""", Fixed);
        var (_, d) = ProcessTests.Start(Line + """<DvInstrument name="A" tag="TT" number="1" on="V1" x="150" y="80" value="123 ℃"/>""", Fixed);
        var host = Node(d, "V1");
        Assert.True(Abs(host, Inst(d, "A").ValueRect).Left >= Bubble(d, "A").Right);
        Assert.Equal(Bubble(plain, "A"), Bubble(d, "A"));
        Assert.Equal(plain.Lines.Select(l => l.G!.Points.ToList()), d.Lines.Select(l => l.G!.Points.ToList()));
    }

    [Fact]
    public void Layout_IsReused_WhenNothingChanged()
    {
        var (s, d) = ProcessTests.Start(Line + """<DvInstrument name="A" tag="TT" number="1" on="V1" x="150" y="80"/>""" + """<DvLine from="IN" to="F1.in" via="80,200 620,200"/>""", Fixed);
        var runs = ProcessBuilder.LayoutRuns(d.Source);
        s.Invalidate();
        s.Layout(1100, 700);
        Assert.Equal(runs, ProcessBuilder.LayoutRuns(ProcessTests.Diagram(s).Source));
    }

    // ── 계기 x·y + tap ──

    [Fact]
    public void Tap_XY_OnAPort_DropsTheLineStraightOntoThatPipe()
    {
        var (_, d) = ProcessTests.Start(Line + """<DvInstrument name="T" tag="TT" number="1" tap="V1.out" x="510" y="220"/>""", Fixed);
        var inst = Inst(d, "T");
        var host = inst.Host!;
        Assert.Equal("V1", host.Name);
        AssertAt(510, 300, Plus(host, inst.TapFrom)); // V1→F1 배관 위, 버블 바로 아래
        AssertAt(510, 240, Plus(host, inst.TapTo));   // 버블 아래 가장자리
    }

    [Fact]
    public void Tap_XY_OnAPort_FollowsARoutedPipe()
    {
        // 세로 구간 왼쪽의 버블 — 그 높이에서 가로로 닿는다
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="FEED" x="80" y="60"/>
            <DvValve name="V1" kind="gate" rotate="90" open="true" x="308" y="300"/>
            <DvLine from="IN" to="V1.in"/>
            <DvInstrument name="T" tag="PT" number="1" tap="V1.in" x="220" y="180"/>
            """, Fixed);
        var inst = Inst(d, "T");
        var from = Plus(inst.Host!, inst.TapFrom);
        var to = Plus(inst.Host!, inst.TapTo);
        Assert.Equal(180, from.Y, 0.01);
        Assert.Equal(300, from.X, 0.5);
        AssertAt(240, 180, to);
    }

    [Fact]
    public void Tap_XY_OnAFace_SmallOffset_KeepsTheLineStraight()
    {
        var (_, d) = ProcessTests.Start(Line + """<DvInstrument name="T" tag="FT" number="1" tap="F1.bottom@0.5" x="630" y="440"/>""", Fixed);
        var inst = Inst(d, "T");
        var from = Plus(inst.Host!, inst.TapFrom);
        var to = Plus(inst.Host!, inst.TapTo);
        Assert.Equal(630, from.X, 0.01);
        Assert.Equal(630, to.X, 0.01);
        Assert.Equal(420, to.Y, 0.01);
    }

    [Fact]
    public void Tap_WithoutXY_IsUnchanged()
    {
        var (_, d) = ProcessTests.Start(Line + """<DvInstrument name="T" tag="TT" number="1" tap="V1.out"/>""", Fixed);
        var inst = Inst(d, "T");
        var sym = Abs(inst.Host!, inst.Host!.Sym);
        Assert.Equal(sym.Right + 12, Plus(inst.Host!, inst.TapFrom).X, 0.5);
        Assert.Equal(Plus(inst.Host!, inst.TapFrom).Y - ProcessBuilder.TapLength, Plus(inst.Host!, inst.TapTo).Y, 0.5);
    }

    // ── 배관 via ──

    private const string Rise = """
        <DvTerminal name="IN" text="FEED" x="80" y="300"/>
        <DvPump name="P1" running="true" label="P-1" x="420" y="100"/>
        """;

    [Fact]
    public void Via_PipePassesThroughThePoints()
    {
        var (_, d) = ProcessTests.Start(Rise + """<DvLine from="IN" to="P1.in" via="230,300 230,104"/>""", Fixed);
        var pts = d.Lines.Single().G!.Points;
        AssertOrthogonal(pts);
        Assert.Equal(4, pts.Count);
        AssertAt(230, 300, pts[1]);
        AssertAt(230, 104, pts[2]);
    }

    [Fact]
    public void Via_ReplacesTheAutomaticRoute()
    {
        var (_, auto) = ProcessTests.Start(Rise + """<DvLine from="IN" to="P1.in"/>""", Fixed);
        var (_, d) = ProcessTests.Start(Rise + """<DvLine from="IN" to="P1.in" via="230,300 230,104"/>""", Fixed);
        Assert.NotEqual(auto.Lines.Single().G!.Points[1], d.Lines.Single().G!.Points[1]);
    }

    [Fact]
    public void Via_SmallOffsetsFromThePortLines_AreStraightened()
    {
        var (_, d) = ProcessTests.Start(Rise + """<DvLine from="IN" to="P1.in" via="230,292 230,110"/>""", Fixed);
        var pts = d.Lines.Single().G!.Points;
        Assert.Equal(4, pts.Count);
        Assert.Equal(300, pts[1].Y, 0.01);
        Assert.Equal(104, pts[2].Y, 0.01);
    }

    [Fact]
    public void Via_OneOffset_MovesNeighborsOnTheSameLine()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="FEED" x="80" y="300"/>
            <DvTerminal name="OUT" text="OUT" x="800" y="500"/>
            <DvLine from="IN" to="OUT" via="300,290 500,290 500,500"/>
            """, Fixed);
        var pts = d.Lines.Single().G!.Points;
        AssertOrthogonal(pts);
        AssertAt(500, 300, pts[1]); // 300,290 과 500,290 이 함께 포트 줄로 — 일직선이라 합쳐진다
        Assert.Equal(4, pts.Count);
    }

    [Fact]
    public void Via_PointsNotOnOneLine_AreJoinedByAnElbow()
    {
        var (_, d) = ProcessTests.Start(Rise + """<DvLine from="IN" to="P1.in" via="230,200"/>""", Fixed);
        var pts = d.Lines.Single().G!.Points;
        AssertOrthogonal(pts);
        Assert.Contains(new SKPoint(230, 200), pts);
    }

    [Fact]
    public void Via_FollowsAReceivingPort()
    {
        // 세로 포트로 들어가는 선 — 마지막 구간은 포트 줄을 따라 들어간다
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="FEED" x="80" y="100"/>
            <DvValve name="V1" kind="gate" rotate="90" open="true" x="408" y="400"/>
            <DvLine from="IN" to="V1.in" via="250,100 250,250"/>
            """, Fixed);
        var pts = d.Lines.Single().G!.Points;
        AssertOrthogonal(pts);
        AssertAt(250, 250, pts[2]);
        AssertAt(400, 250, pts[3]);
        Assert.Equal(400, pts[^1].X, 0.5);
    }

    [Fact]
    public void Via_OtherPipesStayOffIt()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="A" text="A" x="80" y="300"/>
            <DvTerminal name="B" text="B" x="700" y="300"/>
            <DvTerminal name="C" text="C" x="80" y="120"/>
            <DvTerminal name="D" text="D" x="700" y="120"/>
            <DvLine from="C" to="D"/>
            <DvLine from="A" to="B" via="300,300 300,120 500,120 500,300"/>
            """, Fixed);
        var other = d.Lines.Single(l => l.FromRef == "C").G!;
        var mine = d.Lines.Single(l => l.FromRef == "A").G!;
        Assert.Contains(new SKPoint(300, 120), mine.Points);
        Assert.False(other.Forced);
        Assert.DoesNotContain(other.Points, p => MathF.Abs(p.Y - 120) < 0.01f && p.X > 300 && p.X < 500); // 지정한 구간과 겹치지 않게 비켜 간다
        Assert.True(other.Points.Count > 2);
    }

    [Fact]
    public void Via_WithBaseSize_ScalesWithTheCoordinates()
    {
        var (_, d) = ProcessTests.Start(Rise + """<DvLine from="IN" to="P1.in" via="230,300 230,104"/>""", Fixed + " base-size=\"1100 700\" style=\"width:2200; height:1400\"", 2200, 1400);
        var pts = d.Lines.Single().G!.Points;
        AssertOrthogonal(pts);
        Assert.Equal(460, pts[1].X, 0.5);
        Assert.Equal(460, pts[2].X, 0.5);
    }

    [Fact]
    public void Via_InAutomaticPlacement_Warns_AndIsIgnored()
    {
        ProcessDiagram with = null!, plain = null!;
        const string serial = ProcessTests.Serial;
        var lines = Messages("via is used only", () =>
        {
            (_, with) = ProcessTests.Start(serial.Replace("""<DvLine from="IN" to="T1.top" arrow="end"/>""", """<DvLine from="IN" to="T1.top" arrow="end" via="1,2 3,2"/>"""));
            (_, plain) = ProcessTests.Start(serial);
        });
        Assert.Equal(plain.Lines.Select(l => l.G!.Points.ToList()), with.Lines.Select(l => l.G!.Points.ToList()));
        Assert.NotEmpty(lines);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,2 3")]
    [InlineData("1,2,3")]
    public void Via_BadText_Warns_AndRoutesAutomatically(string text)
    {
        ProcessDiagram bad = null!, plain = null!;
        var lines = Messages($"via='{text}'", () =>
        {
            (_, bad) = ProcessTests.Start(Rise + $"""<DvLine from="IN" to="P1.in" via="{text}"/>""", Fixed);
            (_, plain) = ProcessTests.Start(Rise + """<DvLine from="IN" to="P1.in"/>""", Fixed);
        });
        Assert.Equal(plain.Lines.Single().G!.Points.ToList(), bad.Lines.Single().G!.Points.ToList());
        Assert.NotEmpty(lines);
    }

    [Fact]
    public void Via_ThroughEquipment_IsReported()
    {
        var (_, d) = ProcessTests.Start(Rise + """
            <DvValve name="V9" open="true" label="V-9" x="300" y="300"/>
            <DvLine from="IN" to="P1.in" via="300,300 300,104"/>
            """, Fixed);
        Assert.Contains(d.Graph!.Warnings, w => w.Contains("via runs through 'V9'"));
    }

    public sealed class RouteVm
    {
        public string Route { get; set; } = "230,300 230,104";
    }

    [Fact]
    public void Via_Bound_ChangesTheRouteOnRelayout()
    {
        var vm = new RouteVm();
        var (s, d) = ProcessTests.Start(Rise + """<DvLine from="IN" to="P1.in" via="{Route}"/>""", Fixed, vm: vm);
        Assert.Equal(230, d.Lines.Single().G!.Points[1].X, 0.01);

        vm.Route = "260,300 260,104";
        s.Invalidate();
        s.Layout(1100, 700);
        Assert.Equal(260, ProcessTests.Diagram(s).Lines.Single().G!.Points[1].X, 0.01);
    }

    [Fact]
    public void Via_FlowAndMedium_StillFollowTheLine()
    {
        var (_, d) = ProcessTests.Start(Rise + """
            <DvTerminal name="OUT" text="OUT" x="800" y="88"/>
            <DvLine from="IN" to="P1.in" medium="water" via="230,300 230,104"/>
            <DvLine from="P1.out" to="OUT"/>
            """, Fixed);
        Assert.All(d.Lines, l => Assert.True(l.Flowing));
        Assert.All(d.Lines, l => Assert.Equal("water", l.Medium));
    }

    [Fact]
    public void Renders_InBothLooks()
    {
        foreach (var look in new[] { "plain", "rich" })
        {
            var (s, _) = ProcessTests.Start(Rise + """
                <DvLine from="IN" to="P1.in" via="230,300 230,104" label="L-1"/>
                <DvInstrument name="T" tag="PT" number="1" tap="P1.in" x="230" y="40" value="1.2 bar"/>
                <DvInstrument name="C" tag="PIC" number="1" on="P1" panel="true" x="600" y="40"/>
                <DvSignal from="T" to="C"/>
                """, $"{Fixed} look=\"{look}\"");
            using var bmp = new SKBitmap(1100, 700);
            using var c = new SKCanvas(bmp);
            s.RenderTo(c, 1100, 700);
        }
    }
}
