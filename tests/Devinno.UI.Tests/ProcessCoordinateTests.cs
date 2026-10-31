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
