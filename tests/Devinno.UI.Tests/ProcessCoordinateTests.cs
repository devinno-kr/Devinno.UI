using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 공정도 좌표 배치(placement="fixed") — 장비는 x·y(심볼 가운데), 배관은 자동. 기준 크기(base-size)는 위치만 비례(장비·글자 크기 그대로)
public class ProcessCoordinateTests
{
    public sealed class Vm
    {
        public float X { get; set; } = 400;
    }

    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    // 심볼 가운데(공정도 내용 원점 기준)
    private static SKPoint Center(ProcessNode n) => new(n.G.X + n.Sym.MidX, n.G.Y + n.Sym.MidY);

    private static LayoutBox ProcessBox(DvSurface s) => Flatten(s.Root!).First(b => b.Process is not null);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

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

    private static List<string> Warnings(string marker, Action act)
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

    // 렌더까지(진단은 그릴 때 한 번 보고된다)
    private static ProcessDiagram Render(string body, string attrs, float w = 1100, float h = 700, object? vm = null)
    {
        var s = new DvSurface($"<DvScreen><DvProcess {attrs}>{body}</DvProcess></DvScreen>", vm ?? new Vm(), DvTheme.Light);
        using var bmp = new SKBitmap((int)w, (int)h);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, w, h);
        return ProcessTests.Diagram(s);
    }

    private const string Chain = """
        <DvValve name="A" open="true" x="150" y="120"/>
        <DvValve name="B" open="true" x="420" y="120"/>
        <DvValve name="C" open="true" x="420" y="360"/>
        <DvLine from="A.out" to="B.in"/>
        <DvLine from="B.out" to="C.in"/>
        """;

    [Fact]
    public void Fixed_PutsSymbolCentersAtCoordinates()
    {
        var (s, d) = ProcessTests.Start(Chain, "placement=\"fixed\"");
        Assert.Equal(new SKPoint(150, 120), Center(Node(d, "A")));
        Assert.Equal(new SKPoint(420, 120), Center(Node(d, "B")));
        Assert.Equal(new SKPoint(420, 360), Center(Node(d, "C")));

        // 원점 = 공정도 안쪽 왼쪽 위(가운데 정렬 안 함)
        var box = ProcessBox(s);
        Assert.Equal(box.Rect.Left, d.Origin.X, 0.5f);
        Assert.Equal(box.Rect.Top, d.Origin.Y, 0.5f);
        Assert.Empty(ProcessTests.Violations(d));
    }

    [Fact]
    public void Fixed_PipesStillRouteAroundEquipment()
    {
        // C 가 A→B 사이를 막고 있어도 배관은 피해 간다
        var (_, d) = ProcessTests.Start("""
            <DvValve name="A" open="true" x="100" y="200"/>
            <DvValve name="B" open="true" x="600" y="200"/>
            <DvVessel name="C" level="50" x="350" y="200"/>
            <DvLine from="A.out" to="B.in"/>
            """, "placement=\"fixed\"");
        Assert.True(d.Lines[0].G!.Points.Count > 2);
        Assert.Empty(ProcessTests.Violations(d));
    }

    [Fact]
    public void Auto_Explicit_IsSameAsDefault()
    {
        var (_, a) = ProcessTests.Start(ProcessTests.Serial);
        var (_, b) = ProcessTests.Start(ProcessTests.Serial, "placement=\"auto\"");
        Assert.Equal(a.Nodes.Select(n => (n.G.X, n.G.Y)), b.Nodes.Select(n => (n.G.X, n.G.Y)));
        Assert.Equal(a.Graph!.Size, b.Graph!.Size);
    }

    // 터미널 포트(가운데)와 밸브 입구 포트(심볼 가운데보다 아래)는 같은 y 를 줘도 몇 px 어긋난다 — 스냅이 하류를 옮겨 일직선
    [Fact]
    public void Snap_SmallOffset_StraightensByMovingDownstream()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="FEED" x="100" y="200"/>
            <DvValve name="V" open="true" x="320" y="200"/>
            <DvValve name="W" open="true" x="560" y="200"/>
            <DvLine from="IN" to="V.in"/>
            <DvLine from="V.out" to="W.in"/>
            """, "placement=\"fixed\"");
        var l = d.Lines[0].G!;
        Assert.Equal(2, l.Points.Count);
        Assert.Equal(l.From.Point.Y, l.To.Point.Y, 0.01f);
        Assert.Equal(new SKPoint(100, 200), Center(Node(d, "IN"))); // 상류는 그대로
        Assert.NotEqual(200f, Center(Node(d, "V")).Y);                // 하류가 옮겨짐
        Assert.Equal(2, d.Lines[1].G!.Points.Count);                  // 연쇄: W 도 V 를 따라
        Assert.Empty(ProcessTests.Violations(d));
    }

    [Fact]
    public void Snap_LargeOffset_KeepsCoordinates()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="IN" text="FEED" x="100" y="200"/>
            <DvValve name="V" open="true" x="320" y="260"/>
            <DvLine from="IN" to="V.in"/>
            """, "placement=\"fixed\"");
        Assert.Equal(new SKPoint(320, 260), Center(Node(d, "V")));
        Assert.True(d.Lines[0].G!.Points.Count > 2);
    }

    // 마주 보는 포트가 곧은 끝 둘(36px)보다 가까워도 고리 없이 잇는다 — 예전엔 곧은 끝이 서로 지나쳐 한 바퀴 돌았다
    [Theory]
    [InlineData(200f)] // 일직선
    [InlineData(230f)] // 스냅 범위 밖으로 어긋남 → Z
    public void CloseFacingPorts_NoLoop(float y)
    {
        var (_, d) = ProcessTests.Start($"""
            <DvValve name="A" kind="check" open="true" x="100" y="200"/>
            <DvValve name="B" kind="check" open="true" x="170" y="{y}"/>
            <DvLine from="A.out" to="B.in"/>
            """, "placement=\"fixed\"");
        var l = d.Lines[0].G!;
        Assert.InRange(l.To.Point.X - l.From.Point.X, 1f, 35f); // 시험 전제: 곧은 끝 둘보다 가깝다
        for (var k = 0; k + 1 < l.Points.Count; k++)
            Assert.True(l.Points[k + 1].X >= l.Points[k].X - 0.01f, $"x goes back at point {k}");
        if (y == 200f)
        {
            Assert.Equal(2, l.Points.Count);
            Assert.Empty(ProcessTests.Violations(d)); // 어긋난 쪽은 좁은 틈의 Z 라 장비 여유보다 가까움(forced) — 그건 맞는 경고
        }
    }

    [Fact]
    public void Snap_SkipsFacePorts()
    {
        // 용기 side 는 면 포트 — 라우터가 면 위 자리를 고르므로 용기를 옮기지 않는다
        var (_, d) = ProcessTests.Start("""
            <DvValve name="V" open="true" x="120" y="200"/>
            <DvVessel name="T" level="40" x="400" y="190"/>
            <DvLine from="V.out" to="T.side"/>
            """, "placement=\"fixed\"");
        Assert.Equal(new SKPoint(400, 190), Center(Node(d, "T")));
        Assert.Empty(ProcessTests.Violations(d));
    }

    [Fact]
    public void Fixed_MissingCoordinate_NotDrawn_AndErrors()
    {
        List<string> w = [];
        ProcessDiagram d = null!;
        w = Warnings("NOXY7", () => d = Render("""
            <DvValve name="A" open="true" x="100" y="100"/>
            <DvValve name="NOXY7" open="true" x="300"/>
            <DvLine from="A.out" to="NOXY7.in"/>
            """, "placement=\"fixed\""));
        Assert.DoesNotContain(d.Nodes, n => n.Name == "NOXY7");
        Assert.Contains(w, m => m.Contains("NOXY7") && m.Contains("x and y"));
    }

    [Fact]
    public void Fixed_IgnoresRowRankAlign_WithWarnings()
    {
        var w = Warnings("ignored", () => Render("""
            <DvValve name="RR8" open="true" x="100" y="100" row="1" rank="2"/>
            """, "placement=\"fixed\" align=\"row\""));
        Assert.Contains(w, m => m.Contains("RR8") && m.Contains("row"));
        Assert.Contains(w, m => m.Contains("RR8") && m.Contains("rank"));
        Assert.Contains(w, m => m.Contains("align") && m.Contains("placement=\"fixed\""));
    }

    [Fact]
    public void Auto_IgnoresXY_WithWarning()
    {
        ProcessDiagram d = null!;
        var w = Warnings("AXY3", () => d = Render("""
            <DvValve name="AXY3" open="true" x="500" y="500"/>
            <DvValve name="B" open="true"/>
            <DvLine from="AXY3.out" to="B.in"/>
            """, ""));
        Assert.Contains(w, m => m.Contains("AXY3") && m.Contains("placement=\"fixed\""));
        Assert.NotEqual(new SKPoint(500, 500), Center(Node(d, "AXY3")));
    }

    [Theory]
    [InlineData("placement=\"grid\"", "placement='grid'")]
    [InlineData("placement=\"fixed\" base-size=\"wide\"", "base-size='wide'")]
    [InlineData("placement=\"fixed\" base-size=\"800 0\"", "base-size='800 0'")]
    [InlineData("base-size=\"800 600\"", "base-size")]
    public void InvalidModeAttributes_Warn(string attrs, string marker)
    {
        var w = Warnings(marker, () => Render("<DvValve name=\"V\" x=\"10\" y=\"10\"/>", attrs));
        Assert.NotEmpty(w);
    }

    [Theory]
    [InlineData(500, 300, 0.5f)]
    [InlineData(1000, 600, 1f)]
    [InlineData(1500, 900, 1.5f)]
    public void BaseSize_ScalesPositionsOnly(float w, float h, float scale)
    {
        const string body = """
            <DvValve name="A" open="true" x="200" y="300"/>
            <DvValve name="B" open="true" x="800" y="300"/>
            """;
        var (_, one) = ProcessTests.Start(body, "placement=\"fixed\"");
        var (s, d) = ProcessTests.Start(body, $"placement=\"fixed\" base-size=\"1000 600\" style=\"width:{w};height:{h}\"", w: 2000, h: 1200);
        Assert.Equal(new SKPoint(200 * scale, 300 * scale), Center(Node(d, "A")));
        Assert.Equal(new SKPoint(800 * scale, 300 * scale), Center(Node(d, "B")));
        Assert.Equal(Node(one, "A").Sym.Size, Node(d, "A").Sym.Size); // 장비 크기 그대로
        var box = ProcessBox(s);
        Assert.Equal(box.Rect.Left, d.Origin.X, 0.5f);
    }

    // 고객 P&ID(Test16) — 창 크기마다 길을 다시 찾던 때 큰 창에서는 안전밸브 복귀 배관이 탱크와 L 0011 사이로 지나갔다
    private const string Drawing = """
        <DvTerminal name="IN" text="SOLVENT&#10;FROM UNIT 1" x="55" y="105"/>
        <DvVessel name="T1" level="64" label="T001" height="200" x="200" y="165"/>
        <DvInline name="VT1" kind="vent" x="175" y="22"/>
        <DvValve name="DR1" kind="ball" rotate="90" x="200" y="374"/>
        <DvValve name="CV0" kind="check" x="262" y="302"/>
        <DvPump name="P1" kind="isa" label="P001" x="360" y="310"><DvMotor kind="isa"/></DvPump>
        <DvValve name="PSV1" kind="relief" flip="h" label="PA 6 barg" x="412" y="190"/>
        <DvValve name="CV1" kind="check" x="462" y="302"/>
        <DvValve name="CV2" kind="check" x="558" y="302"/>
        <DvTerminal name="OUT" text="SOLVENT&#10;TO UNIT 3" x="672" y="310"/>
        <DvLine from="IN" to="T1.left@0.15" arrow="end"/>
        <DvLine from="T1.top@0.25" to="VT1"/>
        <DvLine from="T1.bottom" to="DR1.in"/>
        <DvLine from="T1.bottom" to="CV0.in"/>
        <DvLine from="CV0.out" to="P1.in"/>
        <DvLine from="P1.out" to="PSV1.in"/>
        <DvLine from="PSV1.out" to="T1.right@0.15" arrow="end"/>
        <DvLine from="P1.out" to="CV1.in"/>
        <DvLine from="CV1.out" to="CV2.in" label="02-100-PE-N"/>
        <DvLine from="CV2.out" to="OUT"/>
        <DvInstrument name="LT11" tag="L" number="0011" tap="T1.right@0.55" value="64.2 %"/>
        <DvInstrument name="LS12" tag="L" number="0012" panel="true" tap="T1.left@0.85"/>
        <DvInstrument name="PI13" tag="PI" number="0013" tap="CV2.out" value="0.00 bar"/>
        <DvSignal from="LT11" to="P1" arrow="end" label="SH"/>
        """;

    // 구간마다 방향(+x, +y, -x, -y) — 꺾임 수·순서·도는 쪽
    private static List<int> Turns(List<SKPoint> pts)
    {
        var dirs = new List<int>();
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            var (a, b) = (pts[k], pts[k + 1]);
            Assert.True(MathF.Abs(a.X - b.X) < 0.01f || MathF.Abs(a.Y - b.Y) < 0.01f, $"diagonal segment {a} → {b}");
            dirs.Add(MathF.Abs(a.Y - b.Y) < 0.01f ? (b.X > a.X ? 0 : 2) : (b.Y > a.Y ? 1 : 3));
        }

        return dirs;
    }

    [Theory]
    [InlineData(944, 546)]
    [InlineData(730, 480)]
    [InlineData(1200, 700)]
    public void BaseSize_KeepsPipeShapeAtEverySize(float w, float h)
    {
        var (_, b) = ProcessTests.Start(Drawing, "placement=\"fixed\" base-size=\"718 410\" style=\"width:718;height:410\"", w: 2000, h: 1200);
        var (_, d) = ProcessTests.Start(Drawing, $"placement=\"fixed\" base-size=\"718 410\" style=\"width:{w};height:{h}\"", w: 2000, h: 1200);
        for (var i = 0; i < b.Lines.Count; i++)
        {
            Assert.True(Turns(b.Lines[i].G!.Points).SequenceEqual(Turns(d.Lines[i].G!.Points)), $"{d.Lines[i].FromRef}->{d.Lines[i].ToRef}: {string.Join(" ", b.Lines[i].G!.Points)} vs {string.Join(" ", d.Lines[i].G!.Points)}");
            Assert.Equal(d.Lines[i].G!.From.Point, d.Lines[i].G!.Points[0]);
            Assert.Equal(d.Lines[i].G!.To.Point, d.Lines[i].G!.Points[^1]);
        }

        for (var i = 0; i < b.Signals.Count; i++)
            Assert.True(Turns(b.Signals[i].G!.Points).SequenceEqual(Turns(d.Signals[i].G!.Points)), $"signal: {string.Join(" ", b.Signals[i].G!.Points)} vs {string.Join(" ", d.Signals[i].G!.Points)}");
        Assert.Equal(200 * d.Graph!.Spacing, Center(Node(d, "T1")).X, 0.5f); // 위치는 그대로 비례
    }

    [Fact]
    public void BaseSize_SmallerAreaThatDoesNotFit_RoutesAgainAndWarns()
    {
        // 0.86 배 — L 0011 버블 아래가 모터 위보다 낮아져 기준 크기의 신호선 모양(아래→오른쪽→아래)이 안 들어간다
        List<string> w = [];
        ProcessDiagram d = null!;
        w = Warnings("base-size", () => d = Render(Drawing, "placement=\"fixed\" base-size=\"718 410\" style=\"width:620;height:360\"", 2000, 1200));
        Assert.Contains(w, m => m.Contains("smaller than base-size"));
        foreach (var l in d.Lines)
            Turns(l.G!.Points); // 비스듬한 구간 없음
        Turns(d.Signals.Single().G!.Points);
    }

    [Fact]
    public void Pipes_DoNotCrossInstrumentTapLines()
    {
        // 기준 크기 없이 1.31476 배 좌표 — 예전엔 복귀 배관이 탱크와 L 0011 사이 틈으로 연결선을 가로질렀다
        var body = System.Text.RegularExpressions.Regex.Replace(Drawing, " (x|y)=\"(\\d+)\"",
            m => $" {m.Groups[1].Value}=\"{(int.Parse(m.Groups[2].Value) * 944f / 718f).ToString(System.Globalization.CultureInfo.InvariantCulture)}\"");
        var (_, d) = ProcessTests.Start(body, "placement=\"fixed\" style=\"width:944;height:546\"", w: 2000, h: 1200);
        foreach (var inst in d.Nodes.SelectMany(n => n.Instruments).Where(x => x.TapPort is not null))
        {
            var h = inst.Host!.G;
            var (ta, tb) = (new SKPoint(h.X + inst.TapFrom.X, h.Y + inst.TapFrom.Y), new SKPoint(h.X + inst.TapTo.X, h.Y + inst.TapTo.Y));
            var tap = new SKRect(MathF.Min(ta.X, tb.X), MathF.Min(ta.Y, tb.Y), MathF.Max(ta.X, tb.X), MathF.Max(ta.Y, tb.Y));
            tap.Inflate(0.5f, 0.5f);
            foreach (var l in d.Lines)
            {
                var pts = l.G!.Points;
                for (var k = 0; k + 1 < pts.Count; k++)
                {
                    var seg = new SKRect(MathF.Min(pts[k].X, pts[k + 1].X), MathF.Min(pts[k].Y, pts[k + 1].Y), MathF.Max(pts[k].X, pts[k + 1].X), MathF.Max(pts[k].Y, pts[k + 1].Y));
                    // 배관 위 tap(PI13)은 그 배관에서 시작한다 — 시작점 3px 밖만 본다
                    var inner = new SKRect(tap.Left, tap.Top, tap.Right, tap.Bottom);
                    if (ta.Y > tb.Y) inner.Bottom -= 3; else if (ta.Y < tb.Y) inner.Top += 3;
                    if (ta.X > tb.X) inner.Right -= 3; else if (ta.X < tb.X) inner.Left += 3;
                    Assert.False(seg.IntersectsWith(inner), $"{l.FromRef}->{l.ToRef} crosses the tap line of {inst.Name}");
                }
            }
        }
    }

    [Theory]
    [InlineData(718, 410)]
    [InlineData(730, 480)]
    [InlineData(620, 360)]
    public void SignalLabel_StaysOffLines(float w, float h)
    {
        var (_, d) = ProcessTests.Start(Drawing, $"placement=\"fixed\" base-size=\"718 410\" style=\"width:{w};height:{h}\"", w: 2000, h: 1200);
        var g = d.Signals.Single().G!;
        var r = Devinno.UI.Painting.Painter.SignalLabelRect(d, g, 16, 12);
        var pts = g.Points;
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            var seg = new SKRect(MathF.Min(pts[k].X, pts[k + 1].X), MathF.Min(pts[k].Y, pts[k + 1].Y), MathF.Max(pts[k].X, pts[k + 1].X), MathF.Max(pts[k].Y, pts[k + 1].Y));
            seg.Inflate(1, 1);
            Assert.False(seg.IntersectsWith(r), $"label {r} on segment {k}");
        }
    }

    [Fact]
    public void BaseSize_CentersWhenAspectDiffers()
    {
        // 1000×300 영역, 기준 1000×600 → 배율 0.5, 500 폭 상자를 가운데(왼쪽 250)
        var (s, d) = ProcessTests.Start("<DvValve name=\"A\" open=\"true\" x=\"0\" y=\"0\"/>",
            "placement=\"fixed\" base-size=\"1000 600\" style=\"width:1000;height:300\"", w: 2000, h: 1200);
        var box = ProcessBox(s);
        Assert.Equal(box.Rect.Left + 250, d.Origin.X, 0.5f);
        Assert.Equal(box.Rect.Top, d.Origin.Y, 0.5f);
    }

    [Fact]
    public void NaturalSize_BaseSize_OrContentExtent()
    {
        var (s1, d1) = ProcessTests.Start("<DvValve name=\"A\" x=\"100\" y=\"100\"/>", "placement=\"fixed\" base-size=\"900 500\"");
        Assert.Equal(new SKSize(900, 500), ProcessBuilder.Natural(ProcessBox(s1)));

        var (s2, d2) = ProcessTests.Start("<DvValve name=\"A\" x=\"300\" y=\"200\"/>", "placement=\"fixed\"");
        var a = Node(d2, "A");
        var natural = ProcessBuilder.Natural(ProcessBox(s2));
        Assert.Equal(a.G.X + a.Footprint.Width + ProcessGraph.Margin, natural.Width, 0.5f);
        Assert.Equal(a.G.Y + a.Footprint.Height + ProcessGraph.Margin, natural.Height, 0.5f);
    }

    [Fact]
    public void Fixed_TooSmallArea_ReportsShortage()
    {
        var (_, d) = ProcessTests.Start("<DvValve name=\"A\" x=\"900\" y=\"100\"/>", "placement=\"fixed\" style=\"width:400;height:300\"");
        Assert.NotNull(d.Shortage);
        Assert.Contains("x/y", d.Shortage);
    }

    [Fact]
    public void Fixed_EquipmentPastTopLeft_ReportsShortage()
    {
        // 터미널 폭 64 → 가운데 x=10 이면 왼쪽으로 22px 나간다
        var (_, d) = ProcessTests.Start("<DvTerminal name=\"IN\" text=\"FEED\" x=\"10\" y=\"100\"/>", "placement=\"fixed\"");
        Assert.NotNull(d.Shortage);

        var (_, ok) = ProcessTests.Start("<DvTerminal name=\"IN\" text=\"FEED\" x=\"60\" y=\"100\"/>", "placement=\"fixed\"");
        Assert.Null(ok.Shortage);
    }

    [Fact]
    public void Fixed_NoEquipment_IsEmpty()
    {
        var (_, d) = ProcessTests.Start("", "placement=\"fixed\" base-size=\"800 600\"");
        Assert.Empty(d.Nodes);
        Assert.Null(d.Shortage);
    }

    [Fact]
    public void Overlap_Warns()
    {
        var w = Warnings("OVL5", () => Render("""
            <DvValve name="OVL5a" open="true" x="200" y="200"/>
            <DvValve name="OVL5b" open="true" x="215" y="205"/>
            """, "placement=\"fixed\""));
        Assert.Contains(w, m => m.Contains("OVL5a") && m.Contains("OVL5b") && m.Contains("overlap"));
    }

    [Fact]
    public void BoundCoordinate_MovesOnRelayout()
    {
        var vm = new Vm();
        var s = new DvSurface("<DvScreen><DvProcess placement=\"fixed\"><DvValve name=\"A\" x=\"{X}\" y=\"100\"/></DvProcess></DvScreen>", vm, DvTheme.Light);
        s.Layout(1100, 700);
        Assert.Equal(400, Center(Node(ProcessTests.Diagram(s), "A")).X, 0.01f);
        vm.X = 250;
        s.Invalidate();
        s.Layout(1100, 700);
        Assert.Equal(250, Center(Node(ProcessTests.Diagram(s), "A")).X, 0.01f);
    }

    [Fact]
    public void Unit_PlacedAsWhole_InsideStaysAuto()
    {
        var (_, d) = ProcessTests.Start("""
            <DvUnit name="U1" direction="tb" label="Feed" x="300" y="250">
              <DvVessel name="T" level="30"/>
              <DvPump name="P"/>
              <DvPort name="out" side="right"/>
              <DvLine from="T.bottom" to="P.in"/>
              <DvLine from="P.out" to="out"/>
            </DvUnit>
            <DvValve name="V" open="true" x="700" y="250"/>
            <DvLine from="U1.out" to="V.in"/>
            """, "placement=\"fixed\"");
        var u = Node(d, "U1");
        Assert.Equal(new SKPoint(300, 250), Center(u));
        Assert.Empty(ProcessTests.Violations(d));
        Assert.Empty(ProcessTests.Violations(u.Unit!));

        var w = Warnings("UFX4", () => Render("""
            <DvUnit name="UFX4" placement="fixed" x="300" y="250"><DvPump name="P"/></DvUnit>
            """, "placement=\"fixed\""));
        Assert.Contains(w, m => m.Contains("UFX4") && m.Contains("placement"));
    }
}
