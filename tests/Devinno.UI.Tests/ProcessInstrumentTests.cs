using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Painting;
using Devinno.UI.Process;
using Devinno.UI.Styling;
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

    // ── 신호 종류(kind)·가독성 — 전기 = 점선, 공압 = 실선 + //, 색은 테마 글자색 ──

    // 원점 그대로(여백 없음) — 신호선은 y=100 위 가로줄
    private static string SignalScene(string attrs, string look = "plain") => $"""
        <DvStack style="background:token(surface); padding:0">
          <DvProcess placement="fixed" look="{look}" style="width:800; height:500">
            <DvTerminal name="IN" text="IN" x="100" y="400"/>
            <DvValve name="V1" open="true" x="400" y="392"/>
            <DvTerminal name="OUT" text="OUT" x="700" y="400"/>
            <DvLine from="IN" to="V1.in"/>
            <DvLine from="V1.out" to="OUT"/>
            <DvInstrument name="A" tag="TIC" number="1" on="V1" x="150" y="100"/>
            <DvInstrument name="B" tag="TY" number="1" on="V1" x="650" y="100"/>
            <DvSignal from="A" to="B" {attrs}/>
          </DvProcess>
        </DvStack>
        """;

    private static SKBitmap Draw(string markup, DvTheme theme) => SKBitmap.Decode(DvRenderer.RenderToPng(markup, null, 800, 500, theme));

    private static double Luminance(SKColor c)
    {
        static double Lin(byte v) => v / 255.0 <= 0.03928 ? v / 255.0 / 12.92 : Math.Pow((v / 255.0 + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin(c.Red) + 0.7152 * Lin(c.Green) + 0.0722 * Lin(c.Blue);
    }

    private static double Contrast(SKColor a, SKColor b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static bool IsBackground(SKColor c, SKColor bg) =>
        Math.Abs(c.Red - bg.Red) + Math.Abs(c.Green - bg.Green) + Math.Abs(c.Blue - bg.Blue) < 12;

    [Theory]
    [InlineData("plain", false)]
    [InlineData("plain", true)]
    [InlineData("rich", false)]
    [InlineData("rich", true)]
    public void Signal_StandsOutFromTheBackground_InEveryLookAndTheme(string look, bool dark)
    {
        foreach (var kind in new[] { "", "kind=\"pneumatic\"" })
        {
            using var bmp = Draw(SignalScene(kind, look), dark ? DvTheme.Dark : DvTheme.Light);
            var bg = bmp.GetPixel(400, 60);
            var best = Enumerable.Range(200, 400).SelectMany(x => new[] { bmp.GetPixel(x, 99), bmp.GetPixel(x, 100) }).Max(c => Contrast(c, bg));
            Assert.True(best >= 3, $"{look} dark={dark} {kind}: 대비 {best:0.00}");
        }
    }

    [Fact]
    public void Kind_Electric_IsDashed_Pneumatic_IsSolidWithSlashes()
    {
        static (double Gaps, int Slash) Measure(SKBitmap b)
        {
            var bg = b.GetPixel(400, 60);
            var xs = Enumerable.Range(200, 400).ToList();
            var gaps = xs.Count(x => IsBackground(b.GetPixel(x, 99), bg) && IsBackground(b.GetPixel(x, 100), bg));
            return ((double)gaps / xs.Count, xs.Count(x => !IsBackground(b.GetPixel(x, 95), bg) || !IsBackground(b.GetPixel(x, 104), bg)));
        }

        using var plain = Draw(SignalScene(""), DvTheme.Light);
        using var electric = Draw(SignalScene("kind=\"electric\""), DvTheme.Light);
        using var pneumatic = Draw(SignalScene("kind=\"pneumatic\""), DvTheme.Light);

        Assert.Equal(plain.Bytes, electric.Bytes); // 기본 = 전기
        var (dashGaps, dashSlash) = Measure(electric);
        var (solidGaps, solidSlash) = Measure(pneumatic);
        Assert.True(dashGaps > 0.2, $"점선 틈 {dashGaps:0.00}");
        Assert.Equal(0, dashSlash);
        Assert.True(solidGaps < 0.01, $"실선인데 틈 {solidGaps:0.00}");
        Assert.True(solidSlash > 0, "빗금 없음");
    }

    [Fact]
    public void Kind_DoesNotChangeTheRoute_OrAnyLayout()
    {
        var (_, a) = ProcessTests.Start(Loop);
        var (_, b) = ProcessTests.Start(Loop.Replace("""<DvSignal from="FIC1" to="V1"/>""", """<DvSignal from="FIC1" to="V1" kind="pneumatic"/>"""));
        Assert.DoesNotContain(a.Signals, s => s.Pneumatic);
        Assert.True(b.Signals.Single(s => s.ToRef == "V1").Pneumatic);
        Assert.False(b.Signals.Single(s => s.ToRef == "FIC1").Pneumatic);
        Assert.Equal(a.Signals.Select(s => s.G!.Points.ToList()), b.Signals.Select(s => s.G!.Points.ToList()));
        Assert.Equal(a.Nodes.Select(n => (n.G.X, n.G.Y)), b.Nodes.Select(n => (n.G.X, n.G.Y)));
    }

    [Fact]
    public void Kind_Unknown_Warns_AndDrawsAsElectric()
    {
        var lines = new List<string>();
        var listener = new DelegateListener(m => { if (m.Contains("kind='wireless'")) lock (lines) lines.Add(m); });
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            using var bad = Draw(SignalScene("kind=\"wireless\""), DvTheme.Light);
            using var plain = Draw(SignalScene(""), DvTheme.Light);
            Assert.Equal(plain.Bytes, bad.Bytes);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }

        Assert.NotEmpty(lines);
    }

    private sealed class DelegateListener(Action<string> write) : System.Diagnostics.TraceListener
    {
        public override void Write(string? message) { }
        public override void WriteLine(string? message) => write(message ?? "");
        public override void TraceEvent(System.Diagnostics.TraceEventCache? c, string source, System.Diagnostics.TraceEventType type, int id, string? message) => write(message ?? "");
    }

    // ── 색·두께(color·width) — 안 쓰면 테마 글자색·기본 두께 ──

    private static bool Near(SKColor c, SKColor want) =>
        Math.Abs(c.Red - want.Red) + Math.Abs(c.Green - want.Green) + Math.Abs(c.Blue - want.Blue) < 40;

    // 신호선 위(가로줄 y=100) 픽셀 중 그 색에 가까운 것이 있나
    private static bool LineHasColor(SKBitmap b, SKColor want) =>
        Enumerable.Range(200, 400).Any(x => Near(b.GetPixel(x, 99), want) || Near(b.GetPixel(x, 100), want));

    // 실선 신호선의 굵기 — 가운데 세로로 배경이 아닌 픽셀 수(빗금 없는 자리)
    private static int LineThickness(SKBitmap b, int x)
    {
        var bg = b.GetPixel(400, 60);
        return Enumerable.Range(85, 30).Count(y => !IsBackground(b.GetPixel(x, y), bg));
    }

    [Theory]
    [InlineData("")]
    [InlineData("kind=\"pneumatic\"")]
    public void Color_PaintsTheLine_InBothKinds(string kind)
    {
        // 굵게 — 가는 선은 안티앨리어싱으로 지정한 색 그대로 찍힌 픽셀이 없다
        using var bmp = Draw(SignalScene($"{kind} color=\"#D32F2F\" thickness=\"4\""), DvTheme.Light);
        Assert.True(LineHasColor(bmp, new SKColor(0xD3, 0x2F, 0x2F)));
    }

    [Fact]
    public void Color_AcceptsAThemeToken_UnknownTokenIsMagenta()
    {
        using var token = Draw(SignalScene("color=\"token(text)\" thickness=\"4\""), DvTheme.Light);
        Assert.False(LineHasColor(token, new SKColor(0xFF, 0x00, 0xFF)));

        using var unknown = Draw(SignalScene("color=\"token(no-such-token)\" thickness=\"4\""), DvTheme.Light);
        Assert.True(LineHasColor(unknown, new SKColor(0xFF, 0x00, 0xFF)));
    }

    [Fact]
    public void Width_MakesTheLineThicker()
    {
        // 공압(실선)으로 재서 점선 틈의 영향을 뺀다. x=390 은 빗금 사이가 아니다 — 빗금 자리면 다른 x
        using var thin = Draw(SignalScene("kind=\"pneumatic\""), DvTheme.Light);
        using var thick = Draw(SignalScene("kind=\"pneumatic\" thickness=\"6\""), DvTheme.Light);
        var marks = Painter.SignalMarks([new SKPoint(170, 100), new SKPoint(630, 100)]).Select(m => (int)m.At.X).ToList();
        var x = Enumerable.Range(200, 400).First(v => marks.All(m => Math.Abs(v - m) > 16));
        Assert.True(LineThickness(thick, x) >= LineThickness(thin, x) + 3, $"{LineThickness(thin, x)} → {LineThickness(thick, x)}");
    }

    [Fact]
    public void ColorAndWidth_Omitted_AreTheDefaults_AndChangeNoLayout()
    {
        var (_, a) = ProcessTests.Start(Loop);
        var (_, b) = ProcessTests.Start(Loop.Replace("""<DvSignal from="FIC1" to="V1"/>""", """<DvSignal from="FIC1" to="V1" color="#D32F2F" thickness="4"/>"""));
        Assert.All(a.Signals, s => Assert.Null(s.Color));
        Assert.All(a.Signals, s => Assert.Null(s.Width));
        var set = b.Signals.Single(s => s.ToRef == "V1");
        Assert.Equal(new SKColor(0xD3, 0x2F, 0x2F), set.Color);
        Assert.Equal(4f, set.Width);
        Assert.Equal(a.Signals.Select(s => s.G!.Points.ToList()), b.Signals.Select(s => s.G!.Points.ToList()));
    }

    [Theory]
    [InlineData("color=\"crimson\"", "color='crimson'")]
    [InlineData("thickness=\"0\"", "thickness='0'")]
    [InlineData("thickness=\"thick\"", "thickness='thick'")]
    public void BadColorOrWidth_Warns_AndUsesTheDefault(string attr, string marker)
    {
        var lines = new List<string>();
        var listener = new DelegateListener(m => { if (m.Contains(marker)) lock (lines) lines.Add(m); });
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            using var bad = Draw(SignalScene(attr), DvTheme.Light);
            using var plain = Draw(SignalScene(""), DvTheme.Light);
            Assert.Equal(plain.Bytes, bad.Bytes);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }

        Assert.NotEmpty(lines);
    }

    public sealed class SignalVm
    {
        public string Tone { get; set; } = "#D32F2F";
        public string Thick { get; set; } = "4";
    }

    [Fact]
    public void ColorAndWidth_CanBeBound()
    {
        var vm = new SignalVm();
        var (s, d) = ProcessTests.Start(Loop.Replace("""<DvSignal from="FIC1" to="V1"/>""", """<DvSignal from="FIC1" to="V1" color="{Tone}" thickness="{Thick}"/>"""), vm: vm);
        Assert.Equal(new SKColor(0xD3, 0x2F, 0x2F), d.Signals.Single(x => x.ToRef == "V1").Color);

        vm.Tone = "#1976D2";
        vm.Thick = "2";
        s.Invalidate();
        s.Layout(1100, 700);
        var now = ProcessTests.Diagram(s).Signals.Single(x => x.ToRef == "V1");
        Assert.Equal(new SKColor(0x19, 0x76, 0xD2), now.Color);
        Assert.Equal(2f, now.Width);
    }

    [Fact]
    public void Marks_LongSegmentGetsSeveral_NoneAtTheEnds()
    {
        var marks = Painter.SignalMarks([new SKPoint(0, 100), new SKPoint(400, 100)]);
        Assert.True(marks.Count >= 4, $"{marks.Count}개");
        Assert.All(marks, m =>
        {
            Assert.True(m.Flat);
            Assert.Equal(100, m.At.Y, 0.01);
            Assert.InRange(m.At.X, 12, 388);
        });
    }

    [Fact]
    public void Marks_FollowEachSegment_AndAShortLineStillGetsOne()
    {
        var corner = Painter.SignalMarks([new SKPoint(0, 0), new SKPoint(200, 0), new SKPoint(200, 200)]);
        Assert.Contains(corner, m => m.Flat);
        Assert.Contains(corner, m => !m.Flat);

        var shortLine = Painter.SignalMarks([new SKPoint(0, 0), new SKPoint(20, 0)]);
        var one = Assert.Single(shortLine);
        Assert.Equal(10, one.At.X, 0.01);
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
