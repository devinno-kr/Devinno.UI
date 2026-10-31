using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

// 공정도(DvProcess) — 장비·연결만 적으면 배치·직교 배관. 불변식: 배관은 장비를 지나지 않고, 다른 배관 묶음과 포개지지 않는다.
public class ProcessTests(ITestOutputHelper output)
{
    public sealed class Vm
    {
        public double Level { get; set; } = 60;
        public bool Run { get; set; } = true;
        public int Clicks;
        public void Hit() => Clicks++;
    }

    internal static (DvSurface S, ProcessDiagram D) Start(string body, string attrs = "", float w = 1100, float h = 700,
        ComponentRegistry? registry = null, object? vm = null)
    {
        var s = new DvSurface($"<DvScreen><DvProcess {attrs}>{body}</DvProcess></DvScreen>", vm ?? new Vm(), DvTheme.Light, registry);
        s.Layout(w, h);
        return (s, Diagram(s));
    }

    internal static ProcessDiagram Diagram(DvSurface s) => Flatten(s.Root!).First(b => b.Process is not null).Process!;

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    internal const string Serial = """
        <DvTerminal name="IN" text="RAW WATER"/>
        <DvVessel name="T1" level="{Level}" label="T-101"/>
        <DvPump name="P1" running="{Run}" label="P-101"/>
        <DvPump name="P2" label="P-102"/>
        <DvValve name="V1" kind="control" open="true" label="FCV-101"/>
        <DvInline name="F1" kind="flowmeter" label="FT-101"/>
        <DvVessel name="T2" kind="reactor" label="R-201"><DvAgitator running="true"/></DvVessel>
        <DvTerminal name="OUT" text="TO DRAIN"/>
        <DvLine from="IN" to="T1.top" arrow="end"/>
        <DvLine from="T1.bottom" to="P1.in"/>
        <DvLine from="T1.bottom" to="P2.in"/>
        <DvLine from="P1.out" to="V1.in"/>
        <DvLine from="P2.out" to="V1.in"/>
        <DvLine from="V1.out" to="F1.in"/>
        <DvLine from="F1.out" to="T2.top"/>
        <DvLine from="T2.bottom" to="OUT"/>
        """;

    internal const string Blender = """
        <DvTerminal name="MTO" text="MTO RMMT-1"/>
        <DvTerminal name="UGTF" text="UGTF BL-1"/>
        <DvValve name="OV" kind="control" open="true" label="OV 1001"/>
        <DvVessel name="BL" level="55" label="BL-1">
          <DvAgitator running="true"/>
          <DvAttach side="right"><DvLabel class="caption" text="TIA 1001"/><DvLabel class="title" text="105.5"/></DvAttach>
          <DvAttach side="left"><DvButton variant="secondary" text="Reset" onclick="Hit"/></DvAttach>
        </DvVessel>
        <DvPump name="P1" running="true" label="RTP 1001"/>
        <DvPump name="P2" label="RTP 1002"/>
        <DvTerminal name="OUT" text="RESIN TANK"/>
        <DvLine from="MTO" to="OV.in"/>
        <DvLine from="OV.out" to="BL.top" arrow="end"/>
        <DvLine from="UGTF" to="BL.side" arrow="end"/>
        <DvLine from="BL.bottom" to="P1.in"/>
        <DvLine from="BL.bottom" to="P2.in"/>
        <DvLine from="P1.out" to="OUT"/>
        <DvLine from="P2.out" to="OUT"/>
        """;

    // ── 불변식 ──

    internal static List<string> Violations(ProcessDiagram d)
    {
        var bad = new List<string>();
        var bodies = d.Nodes.Select(n => (n.Name, Rect: new SKRect(n.G.X + n.Sym.Left + 1, n.G.Y + n.Sym.Top + 1, n.G.X + n.Sym.Right - 1, n.G.Y + n.Sym.Bottom - 1))).ToList();
        var segs = new List<(ProcessLine L, SKPoint A, SKPoint B)>();
        foreach (var l in d.Lines.Where(l => l.G is not null))
        {
            var pts = l.G!.Points;
            if (pts.Count < 2)
                bad.Add($"{l.FromRef}->{l.ToRef}: no path");
            if (pts.Count > 0 && (!Near(pts[0], l.G.From.Point) || !Near(pts[^1], l.G.To.Point)))
                bad.Add($"{l.FromRef}->{l.ToRef}: ends off port");
            if (l.G.Forced)
                bad.Add($"{l.FromRef}->{l.ToRef}: forced");
            for (var k = 0; k + 1 < pts.Count; k++)
            {
                var a = pts[k];
                var b = pts[k + 1];
                if (MathF.Abs(a.X - b.X) > 0.01f && MathF.Abs(a.Y - b.Y) > 0.01f)
                    bad.Add($"{l.FromRef}->{l.ToRef}: diagonal segment");
                foreach (var (name, r) in bodies)
                {
                    if (SegmentHitsRect(a, b, r))
                        bad.Add($"{l.FromRef}->{l.ToRef}: passes through {name}");
                }

                segs.Add((l, a, b));
            }
        }

        var pitch = d.Pitch - 1;
        for (var i = 0; i < segs.Count; i++)
        {
            for (var j = i + 1; j < segs.Count; j++)
            {
                var (la, a1, a2) = segs[i];
                var (lb, b1, b2) = segs[j];
                if (ReferenceEquals(la, lb))
                    continue;
                var sameNet = la.G!.Net == lb.G!.Net;
                var av = MathF.Abs(a1.X - a2.X) < 0.01f;
                var bv = MathF.Abs(b1.X - b2.X) < 0.01f;
                if (av != bv)
                    continue;
                var gap = av ? MathF.Abs(a1.X - b1.X) : MathF.Abs(a1.Y - b1.Y);
                var overlap = av
                    ? MathF.Min(MathF.Max(a1.Y, a2.Y), MathF.Max(b1.Y, b2.Y)) - MathF.Max(MathF.Min(a1.Y, a2.Y), MathF.Min(b1.Y, b2.Y))
                    : MathF.Min(MathF.Max(a1.X, a2.X), MathF.Max(b1.X, b2.X)) - MathF.Max(MathF.Min(a1.X, a2.X), MathF.Min(b1.X, b2.X));
                if (overlap <= 1)
                    continue;
                if (!sameNet && gap < pitch)
                    bad.Add($"{la.FromRef}->{la.ToRef} and {lb.FromRef}->{lb.ToRef}: parallel {gap:0.#}px apart over {overlap:0}px");
                if (sameNet && gap > 0.01f && gap < pitch)
                    bad.Add($"{la.FromRef}->{la.ToRef} and {lb.FromRef}->{lb.ToRef}: same net doubled {gap:0.#}px apart");
            }
        }

        return bad;
    }

    private static bool Near(SKPoint a, SKPoint b) => MathF.Abs(a.X - b.X) < 0.5f && MathF.Abs(a.Y - b.Y) < 0.5f;

    private static bool SegmentHitsRect(SKPoint a, SKPoint b, SKRect r)
    {
        if (MathF.Abs(a.X - b.X) < 0.01f)
            return a.X > r.Left && a.X < r.Right && MathF.Max(a.Y, b.Y) > r.Top && MathF.Min(a.Y, b.Y) < r.Bottom;
        return a.Y > r.Top && a.Y < r.Bottom && MathF.Max(a.X, b.X) > r.Left && MathF.Min(a.X, b.X) < r.Right;
    }

    private void Dump(ProcessDiagram d)
    {
        foreach (var n in d.Nodes)
            output.WriteLine($"{n.Name}: foot=({n.G.X:0},{n.G.Y:0}) sym={n.Sym}");
        foreach (var l in d.Lines)
            output.WriteLine($"{l.FromRef}->{l.ToRef} net={l.G?.Net} forced={l.G?.Forced} x={l.G?.Crossings} : {string.Join(" ", l.G?.Points.Select(p => $"({p.X:0},{p.Y:0})") ?? [])}");
    }

    private void AssertClean(ProcessDiagram d)
    {
        var bad = Violations(d);
        if (bad.Count > 0)
            Dump(d);
        Assert.Empty(bad);
    }

    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    private static SKRect SymAbs(ProcessNode n) => new(n.G.X + n.Sym.Left, n.G.Y + n.Sym.Top, n.G.X + n.Sym.Right, n.G.Y + n.Sym.Bottom);

    // ── 견본: 겹침 없음 ──

    [Theory]
    [InlineData("plain")]
    [InlineData("rich")]
    public void Serial_Clean_NoCrossings(string look)
    {
        var (_, d) = Start(Serial, $"look=\"{look}\"");
        AssertClean(d);
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("rich")]
    public void Blender_TopDown_Clean_NoCrossings(string look)
    {
        var (_, d) = Start(Blender, $"direction=\"tb\" look=\"{look}\"");
        AssertClean(d);
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
    }

    [Fact]
    public void SlightlySqueezed_KeepsNaturalShape()
    {
        // 자연 크기 모양(계단식)을 줄여서 들어가면 그 모양 그대로 — 창 크기마다 꺾임·갈림 자리가 바뀌지 않게
        var (_, natural) = Start(Serial);
        var turns = natural.Lines.Select(l => Turns(l.G!.Points)).ToList();
        var (_, d) = Start(Serial, "style=\"height:440\"");
        Assert.Equal(ProcessAlign.Step, d.Graph!.Align);
        Assert.True(d.Graph.Size.Height <= 440.5f, $"{d.Graph.Size}");
        Assert.Null(d.Shortage);
        Assert.Equal(turns, d.Lines.Select(l => Turns(l.G!.Points)).ToList());
        AssertClean(d);
    }

    // 구간마다 방향(+x, +y, -x, -y)
    private static string Turns(List<SKPoint> pts) =>
        string.Concat(pts.Zip(pts.Skip(1)).Select(s => MathF.Abs(s.First.Y - s.Second.Y) < 0.01f ? (s.Second.X > s.First.X ? 'R' : 'L') : (s.Second.Y > s.First.Y ? 'D' : 'U')));

    [Theory]
    [InlineData(400)]
    public void Squeezed_AutoAlign_FitsAndStaysClean(float h)
    {
        // 자연 크기 모양을 줄여도 안 들어가면 같은 줄 방식(낮고 넓게)으로 — 그래도 안 맞으면 간격을 줄인다
        var (_, natural) = Start(Serial);
        Assert.Equal(ProcessAlign.Step, natural.Graph!.Align);
        var (_, d) = Start(Serial, $"style=\"height:{h}\"");
        Assert.Equal(ProcessAlign.Row, d.Graph!.Align);
        Assert.True(d.Graph.Size.Height <= h + 0.5f, $"{d.Graph.Size} spacing={d.Graph.Spacing}");
        Assert.Null(d.Shortage);
        Assert.Contains(d.Graph.Warnings, w => w.Contains("natural-size shape")); // 모양이 바뀐 것을 알린다
        AssertClean(d);
    }

    [Theory]
    [InlineData(220)]
    public void Squeezed_FixedStep_KeepsShape_AndWarns(float h)
    {
        // 어느 방식으로도 안 들어가면 모양을 바꿀 까닭이 없다 — 자연 크기 모양을 세로로 최대한 줄이고(넘친다) 공간 부족을 알린다
        var (_, natural) = Start(Serial, "align=\"step\"");
        var (_, d) = Start(Serial, $"align=\"step\" style=\"height:{h}\"");
        Assert.Equal(ProcessAlign.Step, d.Graph!.Align); // 고정 — 같은 줄로 바꾸지 않는다
        Assert.True(d.Graph.SpacingY < 1f);
        Assert.Equal(natural.Lines.Select(l => Turns(l.G!.Points)), d.Lines.Select(l => Turns(l.G!.Points)));
        Assert.NotNull(d.Shortage);
        AssertClean(d);
    }

    [Fact]
    public void Align_Step_PlacesPumpBelowTank_Row_LevelWithTank()
    {
        var (_, step) = Start(Serial, "align=\"step\"");
        Assert.True(step.Lines.First(l => l.ToRef == "P1.in").G!.To.Point.Y > SymAbs(Node(step, "T1")).Bottom); // 계단식: 펌프 입구가 탱크 바닥보다 아래
        AssertClean(step);

        var (_, row) = Start(Serial, "align=\"row\"");
        var t1 = SymAbs(Node(row, "T1"));
        var inY = row.Lines.First(l => l.ToRef == "P1.in").G!.To.Point.Y;
        Assert.True(MathF.Abs(inY - t1.MidY) < 1, $"P1.in y={inY} vs tank mid {t1.MidY}"); // 같은 줄: 펌프 입구 = 탱크 가운데 높이
        Assert.True(row.Graph!.Size.Height < step.Graph!.Size.Height, $"row {row.Graph.Size} step {step.Graph.Size}");
        AssertClean(row);
    }

    [Fact]
    public void Align_Invalid_FallsBackToAuto()
    {
        var (_, d) = Start(Serial, "align=\"middle\"");
        Assert.Null(d.Align);
        Assert.Equal(ProcessAlign.Step, d.Graph!.Align);
    }

    [Fact]
    public void SerialChain_IsStraight()
    {
        // 같은 줄로 맞춰 꺾임 없이 일직선
        var (_, d) = Start("""
            <DvValve name="A"/><DvInline name="B"/><DvValve name="C"/>
            <DvLine from="A.out" to="B.in"/><DvLine from="B.out" to="C.in"/>
            """);
        AssertClean(d);
        Assert.All(d.Lines, l => Assert.Equal(2, l.G!.Points.Count));
    }

    // ── 배치 규칙 ──

    [Fact]
    public void LeftToRight_FlowOrder()
    {
        var (_, d) = Start(Serial);
        Assert.True(Node(d, "IN").G.X < Node(d, "T1").G.X);
        Assert.True(Node(d, "T1").G.X < Node(d, "P1").G.X);
        Assert.True(Node(d, "P1").G.X < Node(d, "V1").G.X);
        Assert.True(Node(d, "F1").G.X < Node(d, "T2").G.X);
    }

    [Fact]
    public void ParallelPumps_LeftToRight_SameColumn_Stacked()
    {
        var (_, d) = Start(Serial);
        Assert.Equal(Node(d, "P1").G.X, Node(d, "P2").G.X, 0.5);
        Assert.True(SymAbs(Node(d, "P1")).Bottom < SymAbs(Node(d, "P2")).Top);
    }

    [Fact]
    public void ParallelPumps_TopDown_StackedInFlow_SameColumn()
    {
        // 위→아래 흐름에서 좌우 포트 병렬 펌프는 옆으로 서지 않고 위아래로 쌓여 같은 줄
        var (_, d) = Start(Blender, "direction=\"tb\"");
        Assert.Equal(Node(d, "P1").G.X, Node(d, "P2").G.X, 0.5);
        Assert.True(SymAbs(Node(d, "P1")).Bottom < SymAbs(Node(d, "P2")).Top);
        Assert.True(SymAbs(Node(d, "BL")).Bottom < SymAbs(Node(d, "P1")).Top);
    }

    [Fact]
    public void BranchAndMerge_ShareNet()
    {
        var (_, d) = Start(Serial);
        var lines = d.Lines.ToDictionary(l => l.FromRef + ">" + l.ToRef, l => l.G!.Net);
        Assert.Equal(lines["T1.bottom>P1.in"], lines["T1.bottom>P2.in"]);
        Assert.Equal(lines["P1.out>V1.in"], lines["P2.out>V1.in"]);
        Assert.NotEqual(lines["T1.bottom>P1.in"], lines["P1.out>V1.in"]);
    }

    [Fact]
    public void FacePort_ManyInlets_MergeIntoOneNozzle()
    {
        // 같은 면으로 들어오는 선 셋 → 한 노즐·한 배관 묶음(탱크 앞에서 T 자로 모임)
        var (_, d) = Start("""
            <DvValve name="A"/><DvValve name="B"/><DvValve name="C"/><DvVessel name="T"/>
            <DvLine from="A.out" to="T.top"/><DvLine from="B.out" to="T.top"/><DvLine from="C.out" to="T.top"/>
            """, "direction=\"tb\"");
        AssertClean(d);
        Assert.Single(d.Lines.Select(l => l.G!.To.Point).Distinct());
        Assert.Single(d.Lines.Select(l => l.G!.Net).Distinct());
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
    }

    [Fact]
    public void FacePort_InAndOut_SeparateNozzles()
    {
        // 같은 면이라도 들어오는 선과 나가는 선은 합치지 않는다(합치면 탱크 밖에서 입구·출구가 이어져 보임)
        var (_, d) = Start("""
            <DvValve name="A"/><DvVessel name="T"/><DvPump name="P"/>
            <DvLine from="A.out" to="T.top"/><DvLine from="T.top" to="P.in"/>
            """);
        AssertClean(d);
        Assert.NotEqual(d.Lines[0].G!.To.Point, d.Lines[1].G!.From.Point);
        Assert.NotEqual(d.Lines[0].G!.Net, d.Lines[1].G!.Net);
    }

    [Fact]
    public void SidePort_LeftAndRight_NotMerged()
    {
        var (_, d) = Start("""
            <DvValve name="A"/><DvVessel name="T"/><DvValve name="B" flip="h"/>
            <DvLine from="A.out" to="T.side"/><DvLine from="B.out" to="T.side"/>
            """, "direction=\"tb\"");
        var ends = d.Lines.Select(l => l.G!.To).ToList();
        if (ends[0].Out != ends[1].Out)
        {
            Assert.NotEqual(ends[0].Point, ends[1].Point);
            Assert.NotEqual(d.Lines[0].G!.Net, d.Lines[1].G!.Net);
        }

        AssertClean(d);
    }

    [Fact]
    public void Agitator_TopSlots_AvoidCenter()
    {
        var (_, d) = Start(Serial);
        var t2 = Node(d, "T2");
        var x = d.Lines.Single(l => l.ToRef == "T2.top").G!.To.Point.X;
        var sym = SymAbs(t2);
        Assert.True(MathF.Abs(x - sym.MidX) > 20); // 교반기 자리(가운데)를 피함
    }

    [Fact]
    public void SidePort_PicksFaceTowardOther_AndAvoidsAttach()
    {
        var (_, d) = Start(Blender, "direction=\"tb\"");
        var bl = Node(d, "BL");
        var end = d.Lines.Single(l => l.ToRef == "BL.side").G!.To;
        Assert.Equal(PortSide.Left, end.Out);
        var reset = Flatten(bl.Attach.Single(a => a.Side == PortSide.Left).Box).First(b => b.Node.Tag == "DvButton").Rect;
        var y = end.Point.Y + d.Origin.Y;
        Assert.True(y < reset.Top - 4 || y > reset.Bottom + 4, $"side port y={y} hits Reset {reset}");
    }

    [Fact]
    public void RowHint_SwapsOrder()
    {
        var (_, d) = Start(Serial.Replace("<DvPump name=\"P1\"", "<DvPump name=\"P1\" row=\"1\"").Replace("<DvPump name=\"P2\"", "<DvPump name=\"P2\" row=\"0\""));
        Assert.True(Node(d, "P2").G.Y < Node(d, "P1").G.Y);
        AssertClean(d);
    }

    [Fact]
    public void RankHint_PlacesColumn()
    {
        var (_, d) = Start("""
            <DvValve name="A"/><DvValve name="B"/><DvValve name="C" rank="3"/>
            <DvLine from="A.out" to="B.in"/><DvLine from="B.out" to="C.in"/>
            """);
        Dump(d);
        var gapAB = Node(d, "B").G.X - Node(d, "A").G.X;
        Assert.True(Node(d, "C").G.X - Node(d, "B").G.X > gapAB * 1.4f); // 2열을 비우고 3열
        AssertClean(d);
    }

    [Fact]
    public void Recycle_Cycle_Routes_NoHang()
    {
        var (_, d) = Start("""
            <DvVessel name="T"/><DvPump name="P"/><DvValve name="V"/>
            <DvLine from="T.bottom" to="P.in"/><DvLine from="P.out" to="V.in"/><DvLine from="V.out" to="T.side"/>
            """);
        Assert.All(d.Lines, l => Assert.True(l.G!.Points.Count >= 2));
        AssertClean(d);
    }

    [Fact]
    public void IsolatedEquipment_Placed()
    {
        var (_, d) = Start("""<DvVessel name="T"/><DvPump name="P"/>""");
        Assert.False(SymAbs(Node(d, "T")).IntersectsWith(SymAbs(Node(d, "P"))));
    }

    [Fact]
    public void Deterministic_SameMarkup_SameResult()
    {
        var (_, a) = Start(Blender, "direction=\"tb\"");
        var (_, b) = Start(Blender, "direction=\"tb\"");
        for (var i = 0; i < a.Lines.Count; i++)
            Assert.Equal(a.Lines[i].G!.Points, b.Lines[i].G!.Points);
    }

    [Fact]
    public void ValueChange_ReusesLayout()
    {
        var vm = new Vm();
        var (s, d) = Start(Serial, vm: vm);
        var graph = d.Graph;
        vm.Level = 10;
        vm.Run = false;
        s.Invalidate();
        s.Layout(1100, 700);
        var d2 = Diagram(s);
        Assert.Same(graph, d2.Graph);
        Assert.Equal(10, Node(d2, "T1").Level, 0.01);
        Assert.False(Node(d2, "P1").On("running"));
    }

    // ── 끝점·회전 ──

    [Fact]
    public void Terminal_SourceAndSink_PortSides()
    {
        var (_, lr) = Start(Serial);
        Assert.Equal(PortSide.Right, lr.Lines.Single(l => l.FromRef == "IN").G!.From.Out);
        Assert.Equal(PortSide.Left, lr.Lines.Single(l => l.ToRef == "OUT").G!.To.Out);
        var (_, tb) = Start(Blender, "direction=\"tb\"");
        Assert.Equal(PortSide.Bottom, tb.Lines.Single(l => l.FromRef == "MTO").G!.From.Out);
        Assert.Equal(PortSide.Top, tb.Lines.First(l => l.ToRef == "OUT").G!.To.Out);
    }

    [Theory]
    [InlineData("0", "Left", "Right")]
    [InlineData("90", "Top", "Bottom")]
    [InlineData("180", "Right", "Left")]
    [InlineData("270", "Bottom", "Top")]
    public void Rotate_MapsPortSides(string rotate, string inName, string outName)
    {
        var inSide = Enum.Parse<PortSide>(inName);
        var outSide = Enum.Parse<PortSide>(outName);
        var (_, d) = Start($"""
            <DvValve name="S"/><DvValve name="V" rotate="{rotate}"/><DvValve name="E"/>
            <DvLine from="S.out" to="V.in"/><DvLine from="V.out" to="E.in"/>
            """);
        Assert.Equal(inSide, d.Lines[0].G!.To.Out);
        Assert.Equal(outSide, d.Lines[1].G!.From.Out);
        var v = SymAbs(Node(d, "V"));
        var p = d.Lines[0].G!.To.Point;
        var onFace = inSide switch
        {
            PortSide.Left => MathF.Abs(p.X - v.Left) < 0.5f,
            PortSide.Right => MathF.Abs(p.X - v.Right) < 0.5f,
            PortSide.Top => MathF.Abs(p.Y - v.Top) < 0.5f,
            _ => MathF.Abs(p.Y - v.Bottom) < 0.5f,
        };
        Assert.True(onFace, $"in port {p} not on {inSide} of {v}");
        AssertClean(d);
    }

    [Fact]
    public void FlipH_SwapsLeftRight()
    {
        var (_, d) = Start("""
            <DvValve name="S"/><DvPump name="P" flip="h"/>
            <DvLine from="S.out" to="P.in"/>
            """);
        Assert.Equal(PortSide.Right, d.Lines[0].G!.To.Out);
    }

    // ── 붙은 칸·눌림 ──

    [Fact]
    public void Attach_PlacedBesideSymbol_AndClickable()
    {
        var vm = new Vm();
        var (s, d) = Start(Blender, "direction=\"tb\"", vm: vm);
        var bl = Node(d, "BL");
        var sym = SymAbs(bl);
        var right = bl.Attach.Single(a => a.Side == PortSide.Right).Box.Rect;
        var left = bl.Attach.Single(a => a.Side == PortSide.Left).Box.Rect;
        Assert.True(right.Left >= sym.Right + d.Origin.X);
        Assert.True(left.Right <= sym.Left + d.Origin.X);

        var btn = Flatten(s.Root!).First(b => b.Node.Tag == "DvButton").Rect;
        s.PointerDown(new SKPoint(btn.MidX, btn.MidY));
        s.PointerUp(new SKPoint(btn.MidX, btn.MidY));
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Equipment_Onclick()
    {
        var vm = new Vm();
        var (s, d) = Start("""<DvPump name="P" onclick="Hit"/>""", vm: vm);
        var r = Node(d, "P").Box.Rect;
        s.PointerDown(new SKPoint(r.MidX, r.MidY));
        s.PointerUp(new SKPoint(r.MidX, r.MidY));
        Assert.Equal(1, vm.Clicks);
    }

    // ── 오류 ──

    [Fact]
    public void BadReferences_MarkedError_OthersStillRoute()
    {
        var (_, d) = Start("""
            <DvValve name="A"/><DvValve name="B"/><DvValve name="C"/>
            <DvLine from="A.out" to="B.in"/>
            <DvLine from="A.out" to="NOPE.in"/>
            <DvLine from="B.outlet" to="C.in"/>
            <DvLine from="B" to="C.in"/>
            """);
        Assert.False(d.Lines[0].Error);
        Assert.NotNull(d.Lines[0].G);
        Assert.True(d.Lines[1].Error);
        Assert.True(d.Lines[2].Error);
        Assert.True(d.Lines[3].Error); // 포트가 여럿인 장비는 포트를 골라야 함
        Assert.True(Node(d, "B").Error);
        Assert.All(d.Lines.Skip(1), l => Assert.Null(l.G));
    }

    [Fact]
    public void UnknownTag_And_UnknownKind_DoNotBreak()
    {
        var (_, d) = Start("""
            <DvValve name="A" kind="nope"/><DvButton text="x"/><DvPump name="P"/>
            <DvLine from="A.out" to="P.in"/>
            """);
        Assert.Equal(["A", "P"], d.Nodes.Select(n => n.Name));
        Assert.Equal("gate", Node(d, "A").Def!.Name);
        AssertClean(d);
    }

    [Fact]
    public void DuplicateName_SecondNotLinkable()
    {
        var (_, d) = Start("""
            <DvValve name="A"/><DvValve name="A"/><DvPump name="P"/>
            <DvLine from="A.out" to="P.in"/>
            """);
        Assert.Equal(3, d.Nodes.Count);
        Assert.Equal(0, d.Lines[0].FromNode);
    }

    [Fact]
    public void Empty_NoCrash()
    {
        var (_, d) = Start("");
        Assert.Empty(d.Nodes);
    }

    // ── 사용자 심볼 ──

    private const string Cyclone = """
        <DvSymbol name="Cyclone" view-box="0 0 60 100">
          <DvParam name="running" type="bool"/>
          <DvShape class="state" when="running" path="M10 0 H50 V40 L35 90 H25 L10 40 Z"/>
          <DvPort name="in" side="left" at="0.2" role="in"/>
          <DvPort name="gas" side="top" at="0.5" role="out"/>
          <DvPort name="dust" side="bottom" at="0.5" role="out"/>
          <DvPass from="in" to="gas"/>
        </DvSymbol>
        """;

    [Fact]
    public void UserSymbol_RegisteredAndConnected()
    {
        var reg = new ComponentRegistry();
        reg.Register(Cyclone);
        var (_, d) = Start("""
            <DvPump name="P"/><Cyclone name="CY" running="{Run}"/><DvTerminal name="G" text="GAS"/><DvTerminal name="D" text="DUST"/>
            <DvLine from="P.out" to="CY.in"/><DvLine from="CY.gas" to="G"/><DvLine from="CY.dust" to="D"/>
            """, registry: reg);
        var cy = Node(d, "CY");
        Assert.Equal("Cyclone", cy.Def!.Name);
        Assert.True(cy.On("running"));
        Assert.Equal(PortSide.Top, d.Lines[1].G!.From.Out);
        Assert.Equal(PortSide.Bottom, d.Lines[2].G!.From.Out);
        AssertClean(d);
    }

    [Theory]
    [InlineData("""<DvSymbol view-box="0 0 10 10"/>""", "name")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 0 10"/>""", "view-box")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvPort name="a" side="up"/></DvSymbol>""", "side")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvPort name="a" side="left" at="1.5"/></DvSymbol>""", "at")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvPort name="a" side="left"/><DvPort name="a" side="right"/></DvSymbol>""", "duplicate")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvPort name="a" side="left" role="both"/></DvSymbol>""", "role")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvPort name="a" side="left"/><DvPass from="a" to="b"/></DvSymbol>""", "DvPass")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvShape class="metal" path="M0 0 H10"/></DvSymbol>""", "class")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvFill clip="vessel" value="level"/></DvSymbol>""", "clip")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvCircle/></DvSymbol>""", "DvCircle")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvShape class="vessel" path="M0 0 Q"/></DvSymbol>""", "path")]
    [InlineData("""<DvSymbol name="X" view-box="0 0 10 10"><DvShape class="vessel" path="hello"/></DvSymbol>""", "path")]
    public void UserSymbol_BadDefinition_Throws(string markup, string mentions)
    {
        var ex = Assert.Throws<ArgumentException>(() => new ComponentRegistry().Register(markup));
        Assert.Contains(mentions, ex.Message);
    }

    [Fact]
    public void BuiltIns_AllKindsParse_WithExpectedPorts()
    {
        foreach (var (tag, kinds) in BuiltInSymbols.Kinds)
        {
            foreach (var kind in kinds)
            {
                var def = BuiltInSymbols.Get(tag, kind);
                Assert.NotNull(def);
                var expected = (tag, kind) switch
                {
                    ("DvInline", "vent" or "blind") => new[] { "in" }, // 포트 하나(대기 개방 끝·막힌 노즐)
                    ("DvVessel", _) => new[] { "top", "bottom", "side" },
                    ("DvValve3", _) => new[] { "a", "b", "c" },
                    ("DvExchanger", _) => new[] { "in", "out", "in2", "out2" },
                    _ => new[] { "in", "out" },
                };
                Assert.Equal(expected, def!.Ports.Select(p => p.Name));
            }
        }

        Assert.NotNull(BuiltInSymbols.Get("DvAgitator", null));
        Assert.NotNull(BuiltInSymbols.Get("DvMotor", null));
        Assert.NotNull(BuiltInSymbols.Get("DvMotor", "isa"));
        Assert.Null(BuiltInSymbols.Get("DvMotor", "nope"));
    }

    // ── 그리기 ──

    [Theory]
    [InlineData("plain")]
    [InlineData("rich")]
    public void Paints_PipesAndSymbols(string look)
    {
        var (s, d) = Start(Serial, $"look=\"{look}\"");
        using var bmp = new SKBitmap(1100, 700);
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.White);
        s.RenderTo(c, 1100, 700, 0);

        // 직선 배관 가운데 점은 배관 색(흰 바탕이 아님)
        var line = d.Lines.Single(l => l.FromRef == "V1.out").G!;
        var mid = new SKPoint((line.Points[0].X + line.Points[1].X) / 2 + d.Origin.X, line.Points[0].Y + d.Origin.Y);
        Assert.NotEqual(SKColors.White, bmp.GetPixel((int)mid.X, (int)mid.Y));

        // 운전 중 펌프와 정지 펌프는 다른 색
        var p1 = SymAbs(Node(d, "P1"));
        var p2 = SymAbs(Node(d, "P2"));
        var c1 = bmp.GetPixel((int)(p1.Left + d.Origin.X + 20), (int)(p1.Top + d.Origin.Y + 30));
        var c2 = bmp.GetPixel((int)(p2.Left + d.Origin.X + 20), (int)(p2.Top + d.Origin.Y + 30));
        Assert.NotEqual(c1, c2);
    }

    // ── 재순환(되돌아가는 선) ──

    private const string Recirc = """
        <DvTerminal name="IN" text="FEED"/>
        <DvVessel name="T1" level="70" label="T-1"/>
        <DvPump name="P1" running="true" label="P-1"/>
        <DvValve name="V1" open="true" label="XV-1"/>
        <DvTerminal name="OUT" text="PRODUCT"/>
        <DvLine from="IN" to="T1.top"/>
        <DvLine from="T1.bottom" to="P1.in"/>
        <DvLine from="P1.out" to="V1.in"/>
        <DvLine from="V1.out" to="OUT"/>
        <DvLine from="P1.out" to="T1.side" arrow="end"/>
        """;

    // 되돌아가는 선은 장비 자리 맞춤에 쓰지 않는다 — 위→아래에서 펌프가 탱크 옆으로 밀려 재순환이 입구 배관과 엇갈리던 문제
    [Theory]
    [InlineData("")]
    [InlineData("direction=\"tb\"")]
    public void Recirculation_NoCrossing_ReturnsOnPumpOutletSide(string attrs)
    {
        var (_, d) = Start(Recirc, attrs);
        AssertClean(d);
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
        var ret = d.Lines.Single(l => l.ToRef == "T1.side").G!;
        var tank = SymAbs(Node(d, "T1"));
        // 펌프 출구(오른쪽) 쪽 면으로 돌아 들어간다
        Assert.Equal(tank.Right, ret.To.Point.X, 0.5);
    }
    // 터미널 글자 줄바꿈(&#10;) — 줄마다 따로 그린다. 폭은 가장 긴 줄, 높이는 줄 수만큼
    [Theory]
    [InlineData("")]
    [InlineData("direction=\"tb\"")]
    public void Terminal_MultiLineText_FitsLongestLineAndGrowsTaller(string attrs)
    {
        const string body = """
            <DvTerminal name="A" text="SOLVENT&#10;FROM UNIT 1"/>
            <DvTerminal name="B" text="FROM UNIT 1"/>
            <DvTerminal name="C" text="SOLVENT FROM UNIT 1"/>
            <DvValve name="V1" open="true"/>
            <DvValve name="V2" open="true"/>
            <DvLine from="A" to="V1.in"/>
            <DvLine from="B" to="V2.in"/>
            <DvLine from="C" to="V2.in"/>
            """;
        var (_, d) = Start(body, attrs);
        var (two, one, flat) = (Node(d, "A").Sym, Node(d, "B").Sym, Node(d, "C").Sym);
        Assert.Equal(one.Width, two.Width, 0.5);
        Assert.True(two.Width < flat.Width - 20);
        Assert.True(two.Height >= one.Height + 12);
    }
}
