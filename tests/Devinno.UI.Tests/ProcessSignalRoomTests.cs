using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 신호선 끝 앞의 자리 — 조절 밸브 구동부 쪽엔 이름표를 두지 않고, 앞이 막힌 면으로는 나가지 않는다
public class ProcessSignalRoomTests
{
    private const string Fixed = "placement=\"fixed\"";

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

    // 품질 경고는 그릴 때 나온다
    private static (ProcessDiagram D, List<string> Warnings) Render(string body, string marker, string attrs = Fixed)
    {
        var cap = new Capture(marker);
        Trace.Listeners.Add(cap);
        try
        {
            var (s, d) = ProcessTests.Start(body, attrs);
            using var bmp = new SKBitmap(1100, 700);
            using var c = new SKCanvas(bmp);
            s.RenderTo(c, 1100, 700);
            return (d, cap.Lines);
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
    }

    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    private static SKRect Abs(ProcessNode n, SKRect r) => new(n.G.X + r.Left, n.G.Y + r.Top, n.G.X + r.Right, n.G.Y + r.Bottom);

    private static bool Crosses(IReadOnlyList<SKPoint> pts, SKRect r)
    {
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            var seg = new SKRect(MathF.Min(pts[k].X, pts[k + 1].X), MathF.Min(pts[k].Y, pts[k + 1].Y), MathF.Max(pts[k].X, pts[k + 1].X), MathF.Max(pts[k].Y, pts[k + 1].Y));
            if (seg.Left < r.Right && seg.Right > r.Left && seg.Top < r.Bottom && seg.Bottom > r.Top)
                return true;
        }

        return false;
    }

    // ── 구동부 쪽 이름표 ──

    private const string ThreeWay = """
        <DvTerminal name="IN" text="IN"/>
        <DvValve3 name="V" kind="control" open="true" label="TCV-2"/>
        <DvTerminal name="O1" text="O1"/>
        <DvTerminal name="O2" text="O2"/>
        <DvLine from="IN" to="V.a"/>
        <DvLine from="V.b" to="O1"/>
        <DvLine from="V.c" to="O2"/>
        """;

    [Fact]
    public void ThreeWayControlValve_LabelKeepsClearOfTheActuator()
    {
        var (d, w) = Render(ThreeWay + """<DvInstrument name="LA" tag="FIC" number="1" on="V"/><DvSignal from="LA" to="V"/>""", "'LA'", "");
        Assert.Empty(w);
        var v = Node(d, "V");
        Assert.True(v.LabelRect.Top >= v.Sym.MidY, $"구동부(위)에 이름표: {v.LabelRect} / {v.Sym}");
        Assert.False(Crosses(d.Signals[0].G.Points, Abs(v, v.LabelRect)));
    }

    [Theory]
    [InlineData(90, "in", "out")]
    [InlineData(270, "out", "in")]
    public void UprightControlValve_LabelKeepsClearOfTheActuator(int rotate, string upper, string lower)
    {
        var (d, w) = Render($"""
            <DvVessel name="A" x="400" y="100"/>
            <DvVessel name="B" x="400" y="520"/>
            <DvValve name="V" kind="control" open="true" rotate="{rotate}" label="TCV-1" x="400" y="310"/>
            <DvLine from="A.bottom" to="V.{upper}"/>
            <DvLine from="V.{lower}" to="B.top"/>
            <DvInstrument name="LB" tag="FIC" number="2" on="V" x="{(rotate == 90 ? 620 : 180)}" y="310"/>
            <DvSignal from="LB" to="V"/>
            """, "'LB'");
        Assert.Empty(w);
        var v = Node(d, "V");
        Assert.False(Crosses(d.Signals[0].G.Points, Abs(v, v.LabelRect)));
    }

    [Fact]
    public void ControlValve_OnAHorizontalPipe_LabelStaysBelow()
    {
        var (d, _) = Render("""
            <DvTerminal name="IN" text="IN" x="100" y="300"/>
            <DvValve name="V" kind="control" open="true" label="FCV-1" x="400" y="292"/>
            <DvTerminal name="OUT" text="OUT" x="700" y="300"/>
            <DvLine from="IN" to="V.in"/>
            <DvLine from="V.out" to="OUT"/>
            """, "'LC'");
        var v = Node(d, "V");
        Assert.True(v.LabelRect.Top >= v.Sym.Bottom);
    }

    // ── 앞이 막힌 면 ──

    // 버블(지름 40) 오른쪽 면과 용기 왼쪽 면 사이가 gap. 상대는 용기 너머 오른쪽
    private static string Blocked(int gap, string from, string to) => $"""
        <DvVessel name="T" x="{220 + gap + 50}" y="300"/>
        <DvValve name="V" kind="gate" x="200" y="600"/>
        <DvInstrument name="RA" tag="TT" number="1" on="V" x="200" y="300"/>
        <DvInstrument name="RB" tag="TIC" number="1" on="V" x="700" y="300"/>
        <DvSignal from="{from}" to="{to}"/>
        """;

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    public void Instrument_FaceWithNoRoom_LeavesFromAnother(int gap)
    {
        var (d, w) = Render(Blocked(gap, "RA", "RB"), "'RA'");
        Assert.Empty(w);
        var g = d.Signals[0].G;
        Assert.NotEqual(PortSide.Right, g.From.Out);
        var t = Node(d, "T");
        Assert.False(Crosses(g.Points, Abs(t, t.Body)));
    }

    [Fact]
    public void Instrument_FaceWithNoRoom_AlsoAtTheEndOfTheLine()
    {
        var (d, w) = Render(Blocked(8, "RB", "RA"), "'RB'");
        Assert.Empty(w);
        Assert.NotEqual(PortSide.Right, d.Signals[0].G.To.Out);
    }

    [Fact]
    public void Equipment_LabelInFrontOfTheFace_SignalEntersFromAnother()
    {
        // 이름표는 아래 면 4px 밑 — 아래에서 오는 신호선이 그 면을 고르면 끝이 이름표 안이다
        var (d, w) = Render("""
            <DvTerminal name="IN" text="IN" x="100" y="300"/>
            <DvPump name="P" running="true" label="P-101" x="400" y="300"/>
            <DvTerminal name="OUT" text="OUT" x="700" y="288"/>
            <DvLine from="IN" to="P.in"/>
            <DvLine from="P.out" to="OUT"/>
            <DvInstrument name="RC" tag="SIC" number="1" on="P" x="400" y="520"/>
            <DvSignal from="RC" to="P"/>
            """, "'RC'");
        Assert.Empty(w);
        var p = Node(d, "P");
        Assert.False(Crosses(d.Signals[0].G.Points, Abs(p, p.LabelRect)));
    }

    // ── 붙은 칸이 포트 앞을 막을 때 ──

    private const string AttachBox = """<DvAttach side="top" style="width:80; height:30"><DvLabel text="99.9"/></DvAttach>""";

    [Fact]
    public void AttachedBox_InFrontOfAPort_WarningNamesIt_Automatic()
    {
        var (_, w) = Render($"""
            <DvTerminal name="AIN" text="IN"/>
            <DvExchanger name="AE">{AttachBox}</DvExchanger>
            <DvTerminal name="AOUT" text="OUT"/>
            <DvTerminal name="AS" text="STM"/>
            <DvLine from="AIN" to="AE.in"/>
            <DvLine from="AE.out" to="AOUT"/>
            <DvLine from="AS" to="AE.in2"/>
            """, "'AS'", "");
        Assert.Contains("DvAttach", Assert.Single(w));
    }

    [Fact]
    public void AttachedBox_InFrontOfAPort_WarningNamesIt_Fixed()
    {
        var (_, w) = Render($"""
            <DvTerminal name="BS" text="STM" x="100" y="120"/>
            <DvExchanger name="BE" x="500" y="400">{AttachBox}</DvExchanger>
            <DvLine from="BS" to="BE.in2"/>
            """, "'BS'");
        Assert.Contains("DvAttach", Assert.Single(w));
    }

    // ── 포트·도형 없는 심볼 ──

    [Fact]
    public void Symbol_WithoutPortsOrShapes_CarriesALabelAndAnAttachedBox()
    {
        var registry = new ComponentRegistry();
        registry.Register("""<DvSymbol name="NoteMark" view-box="0 0 2 2"/>""");
        var cap = new Capture("'Note");
        System.Diagnostics.Trace.Listeners.Add(cap);
        try
        {
            var (s, d) = ProcessTests.Start("""
                <NoteMark name="Note1" label="TCV-102" x="300" y="200"/>
                <NoteMark name="Note2" x="500" y="200"><DvAttach side="right"><DvLabel class="caption" text="Note"/></DvAttach></NoteMark>
                """, Fixed, 1100, 700, registry);
            using var bmp = new SKBitmap(1100, 700);
            using var c = new SKCanvas(bmp);
            s.RenderTo(c, 1100, 700);
            Assert.False(Node(d, "Note1").LabelRect.IsEmpty);
            Assert.Single(Node(d, "Note2").AttachRects);
            Assert.Empty(cap.Lines);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(cap);
        }
    }

    // ── detail 획 색 ──

    private static SKColor Sample(string body, string attrs, float symX, float symY, ComponentRegistry? registry = null)
    {
        var (s, d) = ProcessTests.Start(body, attrs, 600, 400, registry);
        using var bmp = new SKBitmap(600, 400);
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.White);
        s.RenderTo(c, 600, 400);
        var n = d.Nodes[0];
        return bmp.GetPixel((int)(n.G.X + n.Sym.Left + symX + d.Origin.X), (int)(n.G.Y + n.Sym.Top + symY + d.Origin.Y));
    }

    [Theory]
    [InlineData("control")]
    [InlineData("gate")]
    public void OpenValve_Stem_KeepsItsColorOnTheBackground(string kind)
    {
        // 줄기는 심볼 x=30, 몸통 위 y=17 은 바탕 위다
        string Valve(string open) => $"""<DvValve name="V" kind="{kind}" open="{open}" x="300" y="200"/>""";
        var opened = Sample(Valve("true"), Fixed, 30, 17);
        var closed = Sample(Valve("false"), Fixed, 30, 17);
        Assert.True(closed.Red < 0xB0, $"닫힘 {closed}");
        Assert.True(Math.Abs(opened.Red - closed.Red) <= 8, $"열림 {opened} / 닫힘 {closed}");
    }

    [Fact]
    public void Detail_IsLightOnlyOverARunningBody()
    {
        var registry = new ComponentRegistry();
        registry.Register("""
            <DvSymbol name="HalfBody" view-box="0 0 100 40">
              <DvParam name="run" type="bool"/>
              <DvShape class="state" when="run" path="M0 0 H50 V40 H0 Z"/>
              <DvShape class="detail" path="M10 20 H90"/>
            </DvSymbol>
            """);
        const string on = """<HalfBody name="S" run="true" x="300" y="200"/>""";
        const string off = """<HalfBody name="S" run="false" x="300" y="200"/>""";

        Assert.True(Sample(on, Fixed, 25, 20, registry).Red > 0xB0); // 켜진 몸통 위 — 밝게
        Assert.True(Sample(on, Fixed, 75, 20, registry).Red < 0xB0); // 몸통 밖 — 평소 색
        Assert.True(Sample(off, Fixed, 25, 20, registry).Red < 0xB0);
        Assert.True(Sample(off, Fixed, 75, 20, registry).Red < 0xB0);
    }
}
