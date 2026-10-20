using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Process;
using SkiaSharp;
using Xunit;

// 공정도 계기(DvInstrument)·신호선(DvSignal) — 계기는 배치에 끼지 않고 on 장비 옆에, 신호선은 배관 뒤에 직교 점선(배치·흐름 영향 없음).
public class ProcessInstrumentTests
{
    // ProcessTests.Serial + 유량 제어 고리(FT → FIC → 조절 밸브) + 수위 계기
    internal const string Loop = ProcessTests.Serial + """
        <DvInstrument name="FT1" tag="FT" number="101" on="F1"/>
        <DvInstrument name="FIC1" tag="FIC" number="101" on="V1" panel="true" value="12.5 m³/h"/>
        <DvInstrument name="LT1" tag="LT" number="101" on="T1" value="{Level}"/>
        <DvSignal from="FT1" to="FIC1"/>
        <DvSignal from="FIC1" to="V1"/>
        """;

    // ProcessTests.Blender + 수위 제어 고리(LT → LIC → 입구 조절 밸브)
    internal const string BlenderLoop = ProcessTests.Blender + """
        <DvInstrument name="LT1" tag="LT" number="1001" on="BL" value="55 %"/>
        <DvInstrument name="LIC1" tag="LIC" number="1001" on="OV" panel="true"/>
        <DvSignal from="LT1" to="LIC1"/>
        <DvSignal from="LIC1" to="OV"/>
        """;

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    private static ProcessInstrument Inst(ProcessDiagram d, string name) => d.Instruments.Single(i => i.Name == name);

    private static SKRect Abs(ProcessNode host, SKRect local) =>
        new(host.G.X + local.Left, host.G.Y + local.Top, host.G.X + local.Right, host.G.Y + local.Bottom);

    private static SKRect SymAbs(ProcessNode n) => Abs(n, n.Sym);

    private static bool SegmentHitsRect(SKPoint a, SKPoint b, SKRect r)
    {
        if (MathF.Abs(a.X - b.X) < 0.01f)
            return a.X > r.Left && a.X < r.Right && MathF.Max(a.Y, b.Y) > r.Top && MathF.Min(a.Y, b.Y) < r.Bottom;
        return a.Y > r.Top && a.Y < r.Bottom && MathF.Max(a.X, b.X) > r.Left && MathF.Min(a.X, b.X) < r.Right;
    }

    private static bool Near(SKPoint a, SKPoint b) => MathF.Abs(a.X - b.X) < 0.5f && MathF.Abs(a.Y - b.Y) < 0.5f;

    [Fact]
    public void Instrument_Parsed_AttachedToHost_NotAFlowNode()
    {
        var (_, d) = ProcessTests.Start(Loop);
        Assert.DoesNotContain(d.Nodes, n => n.Name is "FT1" or "FIC1" or "LT1");
        var fic = Inst(d, "FIC1");
        Assert.Equal("FIC", fic.Letters);
        Assert.Equal("101", fic.Number);
        Assert.True(fic.Panel);
        Assert.Equal("12.5 m³/h", fic.Value);
        Assert.Same(Node(d, "V1"), fic.Host);
        Assert.False(Inst(d, "FT1").Panel);
        Assert.Equal("60", Inst(d, "LT1").Value); // 바인딩
        Assert.Contains(fic, Node(d, "V1").Instruments);
    }

    [Fact]
    public void Instrument_InsideHostFootprint_BesideSymbol_NoOverlap()
    {
        var (_, d) = ProcessTests.Start(Loop);
        foreach (var inst in d.Instruments)
        {
            var host = inst.Host!;
            var foot = SKRect.Create(host.G.X, host.G.Y, host.Footprint.Width, host.Footprint.Height);
            var bubble = Abs(host, inst.Rect);
            Assert.True(foot.Contains(bubble), $"{inst.Name} 차지 영역 밖");
            Assert.False(bubble.IntersectsWith(SymAbs(host)), $"{inst.Name} 이 장비 몸통과 겹침");
            Assert.Equal(bubble.Width, bubble.Height, 0.01);
            Assert.True(bubble.Width >= ProcessBuilder.BubbleMin - 0.01f);
            if (!string.IsNullOrEmpty(inst.Value))
                Assert.True(Abs(host, inst.ValueRect).Left >= bubble.Right, "값 글자는 버블 오른쪽");
            foreach (var other in d.Nodes.Where(n => n != host && !n.IsPort))
                Assert.False(bubble.IntersectsWith(SymAbs(other)), $"{inst.Name} 이 {other.Name} 와 겹침");
        }
    }

    [Fact]
    public void ControlValve_Instrument_AboveValve_Centered()
    {
        var (_, d) = ProcessTests.Start(Loop);
        var v = Node(d, "V1");
        var bubble = Abs(v, Inst(d, "FIC1").Rect);
        var sym = SymAbs(v);
        Assert.True(bubble.Bottom <= sym.Top, "포트는 좌우, 이름표는 아래 → 계기는 위");
        Assert.Equal(sym.MidX, bubble.MidX, 0.5);
    }

    [Fact]
    public void Pipes_AvoidInstruments_StillClean()
    {
        var (_, d) = ProcessTests.Start(Loop);
        Assert.Empty(ProcessTests.Violations(d));
        foreach (var l in d.Lines.Where(l => l.G is not null))
        {
            var pts = l.G!.Points;
            for (var k = 0; k + 1 < pts.Count; k++)
            {
                foreach (var inst in d.Instruments)
                {
                    var r = Abs(inst.Host!, inst.Rect);
                    r.Inflate(-1, -1);
                    Assert.False(SegmentHitsRect(pts[k], pts[k + 1], r), $"{l.FromRef}->{l.ToRef} 이 {inst.Name} 를 지남");
                }
            }
        }
    }

    [Fact]
    public void Signals_Orthogonal_EndOnBubbleAndActuator_AvoidBodies()
    {
        var (_, d) = ProcessTests.Start(Loop);
        Assert.Equal(2, d.Signals.Count);
        foreach (var s in d.Signals)
        {
            Assert.False(s.Error);
            var pts = s.G!.Points;
            Assert.True(pts.Count >= 2, $"{s.FromRef}->{s.ToRef}: 경로 없음");
            for (var k = 0; k + 1 < pts.Count; k++)
            {
                Assert.True(MathF.Abs(pts[k].X - pts[k + 1].X) < 0.01f || MathF.Abs(pts[k].Y - pts[k + 1].Y) < 0.01f, "사선");
                foreach (var n in d.Nodes.Where(n => !n.IsPort))
                {
                    var body = SymAbs(n);
                    body.Inflate(-1, -1);
                    // 조절 밸브 구동부로 들어가는 마지막 구간만 몸통 안(구동부 꼭대기)으로 들어간다
                    if (k == pts.Count - 2 && s.ToRef == n.Name)
                        continue;
                    Assert.False(SegmentHitsRect(pts[k], pts[k + 1], body), $"{s.FromRef}->{s.ToRef} 이 {n.Name} 를 지남");
                }
            }
        }

        // FIC → 밸브: 버블 아래에서 구동부 꼭대기로 곧게
        var toValve = d.Signals.Single(s => s.ToRef == "V1").G!;
        var v = Node(d, "V1");
        var bubble = Abs(v, Inst(d, "FIC1").Rect);
        Assert.True(Near(toValve.Points[0], new SKPoint(bubble.MidX, bubble.Bottom)));
        Assert.Equal(2, toValve.Points.Count);
        var top = v.Transform.MapPoint(new SKPoint(30, 4.5f));
        Assert.True(Near(toValve.Points[^1], new SKPoint(v.G.X + top.X, v.G.Y + top.Y)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("direction=\"tb\"")]
    public void Signals_DoNotCross_ValueText_OrOtherBubbles(string attrs)
    {
        foreach (var markup in new[] { Loop, BlenderLoop })
        {
            var (_, d) = ProcessTests.Start(markup, attrs, 1200, 1200);
            foreach (var s in d.Signals)
            {
                var pts = s.G!.Points;
                for (var k = 0; k + 1 < pts.Count; k++)
                {
                    foreach (var inst in d.Instruments.Where(i => i.Host is not null))
                    {
                        if (!inst.ValueRect.IsEmpty)
                            Assert.False(SegmentHitsRect(pts[k], pts[k + 1], Abs(inst.Host!, inst.ValueRect)), $"{s.FromRef}->{s.ToRef} 이 {inst.Name} 값 글자를 지남");
                        if (inst.Name != s.FromRef && inst.Name != s.ToRef)
                            Assert.False(SegmentHitsRect(pts[k], pts[k + 1], Abs(inst.Host!, inst.Rect)), $"{s.FromRef}->{s.ToRef} 이 {inst.Name} 버블을 지남");
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("direction=\"tb\"")]
    public void Signals_OnSameBubble_UseDifferentSides(string attrs)
    {
        var (_, d) = ProcessTests.Start(BlenderLoop, attrs, 1200, 1200);
        var ends = d.Signals.SelectMany(s => new[] { (Ref: s.FromRef, P: s.G!.From.Point), (Ref: s.ToRef, P: s.G.To.Point) })
            .Where(x => d.Instruments.Any(i => i.Name == x.Ref)).ToList();
        foreach (var g in ends.GroupBy(x => x.Ref))
            Assert.Equal(g.Count(), g.Select(x => (MathF.Round(x.P.X), MathF.Round(x.P.Y))).Distinct().Count());
    }

    [Fact]
    public void Signals_DoNotChangeEquipmentPlacement_OrFlow()
    {
        var withInst = ProcessTests.Serial + """
            <DvInstrument name="FT1" tag="FT" number="101" on="F1"/>
            <DvInstrument name="FIC1" tag="FIC" number="101" on="V1"/>
            """;
        var (_, a) = ProcessTests.Start(withInst);
        var (_, b) = ProcessTests.Start(withInst + """<DvSignal from="FT1" to="FIC1"/><DvSignal from="FIC1" to="V1"/>""");
        Assert.Equal(a.Nodes.Select(n => (n.G.X, n.G.Y)), b.Nodes.Select(n => (n.G.X, n.G.Y)));
        Assert.Equal(a.Lines.Select(l => l.Flowing), b.Lines.Select(l => l.Flowing));
        Assert.Equal(a.Lines.Count, b.Lines.Count); // 신호선은 배관이 아니다
    }

    [Fact]
    public void Signals_NoHops_PipesGetNoHopsFromSignals()
    {
        var (_, d) = ProcessTests.Start(Loop);
        var (_, plain) = ProcessTests.Start(Loop.Replace("<DvSignal from=\"FT1\" to=\"FIC1\"/>", "").Replace("<DvSignal from=\"FIC1\" to=\"V1\"/>", ""));
        Assert.Equal(plain.Lines.Sum(l => l.G!.Hops.Count), d.Lines.Sum(l => l.G!.Hops.Count));
    }

    [Fact]
    public void Instrument_MissingOrUnknownOn_NotDrawn()
    {
        var (s, d) = ProcessTests.Start(ProcessTests.Serial + """
            <DvInstrument name="X1" tag="PI" number="1"/>
            <DvInstrument name="X2" tag="PI" number="2" on="NOPE"/>
            <DvSignal from="X1" to="V1"/>
            """);
        Assert.All(d.Instruments, i => Assert.Null(i.Host));
        Assert.DoesNotContain(Flatten(s.Root!), b => b.Instrument is not null);
        Assert.True(d.Signals.Single().Error); // 그리지 않는 계기는 이을 수 없다
        Assert.Empty(ProcessTests.Violations(d));
    }

    [Fact]
    public void Signal_UnknownOrPortRef_IsError()
    {
        var (_, d) = ProcessTests.Start(ProcessTests.Serial + """
            <DvInstrument name="FT1" tag="FT" number="101" on="F1"/>
            <DvSignal from="FT1" to="NOPE"/>
            <DvSignal from="FT1" to="V1.in"/>
            <DvSignal from="FT1" to="IN"/>
            """);
        Assert.All(d.Signals, s => Assert.True(s.Error));
        Assert.All(d.Signals, s => Assert.Null(s.G));
    }

    [Fact]
    public void Instrument_DuplicateName_RenamedAndUnreachable()
    {
        var (_, d) = ProcessTests.Start(ProcessTests.Serial + """
            <DvInstrument name="V1" tag="FT" number="1" on="F1"/>
            <DvSignal from="V1" to="F1"/>
            """);
        Assert.NotEqual("V1", d.Instruments.Single().Name);
        Assert.False(d.Signals.Single().Error); // V1 은 밸브(장비)로 이어진다
    }

    [Fact]
    public void Instrument_Click_Works_LikeOtherBoxes()
    {
        var vm = new ProcessTests.Vm();
        var (s, d) = ProcessTests.Start(ProcessTests.Serial + """<DvInstrument name="FT1" tag="FT" number="101" on="F1" onclick="Hit"/>""", vm: vm);
        var box = Flatten(s.Root!).Single(b => b.Instrument is not null);
        Assert.Equal(Inst(d, "FT1").Rect.Width, box.Rect.Width, 0.01);
        s.PointerDown(new SKPoint(box.Rect.MidX, box.Rect.MidY));
        s.PointerUp(new SKPoint(box.Rect.MidX, box.Rect.MidY));
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Vertical_And_Rich_StayClean()
    {
        var (_, d) = ProcessTests.Start(Loop, "direction=\"tb\" look=\"rich\"", 1100, 1400);
        Assert.Empty(ProcessTests.Violations(d));
        Assert.All(d.Signals, s => Assert.True(s.G!.Points.Count >= 2));
    }

    [Fact]
    public void Unit_InnerInstrumentsAndSignals()
    {
        var (_, d) = ProcessTests.Start("""
            <DvUnit name="U1" label="UNIT">
              <DvPort name="in" side="left"/>
              <DvValve name="V" kind="control" open="true"/>
              <DvInline name="F" kind="flowmeter"/>
              <DvPort name="out" side="right"/>
              <DvInstrument name="FT" tag="FT" number="1" on="F"/>
              <DvInstrument name="FC" tag="FIC" number="1" on="V"/>
              <DvLine from="in" to="V.in"/>
              <DvLine from="V.out" to="F.in"/>
              <DvLine from="F.out" to="out"/>
              <DvSignal from="FT" to="FC"/>
              <DvSignal from="FC" to="V"/>
            </DvUnit>
            <DvTerminal name="A" text="A"/><DvTerminal name="B" text="B"/>
            <DvLine from="A" to="U1.in"/><DvLine from="U1.out" to="B"/>
            """);
        var inner = Node(d, "U1").Unit!;
        Assert.Equal(2, inner.Instruments.Count(i => i.Host is not null));
        Assert.All(inner.Signals, s => Assert.True(s.G!.Points.Count >= 2));
        Assert.Empty(ProcessTests.Violations(inner));
        Assert.Empty(ProcessTests.Violations(d));
    }

    [Fact]
    public void Renders_WithoutError()
    {
        var (s, _) = ProcessTests.Start(Loop, "look=\"rich\"");
        using var bmp = new SKBitmap(1100, 700);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 1100, 700);
    }
}
