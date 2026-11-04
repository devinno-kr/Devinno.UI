using Devinno.UI;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 공정도 글자 크기(font-size)·값 글자(배치에 영향 없음)·통째 축소(fit="shrink")
public class ProcessFitTests
{
    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    private static ProcessInstrument Inst(ProcessDiagram d, string name) => d.Instruments.Single(i => i.Name == name);

    [Fact]
    public void FontSize_Unset_KeepsBaseTextSizes()
    {
        var (_, d) = ProcessTests.Start(ProcessInstrumentTests.Loop);
        Assert.Equal(1f, d.TextScale);
        Assert.Equal(ProcessBuilder.BubbleMin, Inst(d, "FT1").Rect.Width, 0.01);
    }

    [Fact]
    public void FontSize_ScalesLabelsAndBubbles()
    {
        var (_, small) = ProcessTests.Start(ProcessInstrumentTests.Loop);
        var (_, big) = ProcessTests.Start(ProcessInstrumentTests.Loop, "style=\"font-size:21\"");
        Assert.Equal(1.5f, big.TextScale, 0.001);
        Assert.Equal(ProcessBuilder.BubbleMin * 1.5f, Inst(big, "FT1").Rect.Width, 0.01);
        Assert.True(Node(big, "P1").LabelRect.Height > Node(small, "P1").LabelRect.Height * 1.3f, "이름표가 글자 크기만큼 커진다");
        Assert.True(Node(big, "IN").Sym.Width > Node(small, "IN").Sym.Width, "끝점 상자도 글자만큼");
    }

    [Fact]
    public void ValueText_DoesNotMoveAnything()
    {
        var shortValue = ProcessInstrumentTests.Loop.Replace("value=\"12.5 m³/h\"", "value=\"8\"");
        var longValue = ProcessInstrumentTests.Loop.Replace("value=\"12.5 m³/h\"", "value=\"1,284,000.5 m³/h (high)\"");
        var (_, a) = ProcessTests.Start(shortValue);
        var (_, b) = ProcessTests.Start(longValue);
        Assert.Equal(a.Graph!.Size, b.Graph!.Size);
        foreach (var n in a.Nodes)
        {
            var m = Node(b, n.Name);
            Assert.Equal((n.G.X, n.G.Y), (m.G.X, m.G.Y));
            Assert.Equal(n.Footprint, m.Footprint);
        }

        Assert.Equal(Inst(a, "FIC1").Rect, Inst(b, "FIC1").Rect);
    }

    [Fact]
    public void ValueText_OnOuterSide()
    {
        // 왼쪽 면 열 → 버블 왼쪽, 위 면 여럿 → 버블 위, 하나 → 오른쪽
        const string markup = """
            <DvTerminal name="IN" text="IN"/>
            <DvPump name="P1" label="P-1"/>
            <DvTerminal name="OUT" text="OUT"/>
            <DvLine from="IN" to="P1.in"/>
            <DvLine from="P1.out" to="OUT"/>
            <DvInstrument name="A" tag="PI" number="1" on="P1" value="1.2 bar"/>
            <DvInstrument name="B" tag="TI" number="2" on="P1" value="40 °C"/>
            """;
        var (_, d) = ProcessTests.Start(markup);
        foreach (var name in new[] { "A", "B" })
        {
            var i = Inst(d, name);
            Assert.NotEqual(PortSide.Right, i.ValueSide); // 버블 둘 — 옆 버블 쪽(오른쪽)이 아니다
            Assert.False(i.ValueRect.IntersectsWith(Inst(d, name == "A" ? "B" : "A").Rect), "값 글자가 옆 버블과 겹침");
        }

        var (_, single) = ProcessTests.Start(ProcessInstrumentTests.Loop);
        Assert.Equal(PortSide.Right, Inst(single, "FIC1").ValueSide);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static LayoutBox ProcessBox(DvSurface s) => Flatten(s.Root!).First(b => b.Process is not null);

    private static SKRect Inner(LayoutBox b) =>
        new(b.Rect.Left + b.Style.Padding.Left, b.Rect.Top + b.Style.Padding.Top, b.Rect.Right - b.Style.Padding.Right, b.Rect.Bottom - b.Style.Padding.Bottom);

    [Fact]
    public void Shrink_Overflowing_ScalesWholeDiagramIntoArea()
    {
        var (s, d) = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"shrink\"", 420, 220);
        var box = ProcessBox(s);
        Assert.True(box.ContentScale < 0.8f, $"배율 {box.ContentScale}");
        var drawn = new ScreenMap(box.ContentScale, box.ContentOffset).Map(d.Drawn);
        var inner = Inner(box);
        inner.Inflate(0.5f, 0.5f);
        Assert.True(inner.Contains(drawn), $"{drawn} 이 영역 {inner} 밖");
        Assert.StartsWith("Shrunk to", d.Shortage);
        Assert.Contains("px", d.Shortage);
    }

    [Fact]
    public void Shrink_KeepsTheLayoutItWouldHaveOverflowedWith()
    {
        // 축소는 마지막 단계 — 장비 배치(내용 좌표)는 fit 이 없을 때와 같다
        var (_, plain) = ProcessTests.Start(ProcessInstrumentTests.Loop, "", 420, 220);
        var (_, shrunk) = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"shrink\"", 420, 220);
        foreach (var n in plain.Nodes)
            Assert.Equal((n.G.X, n.G.Y), (Node(shrunk, n.Name).G.X, Node(shrunk, n.Name).G.Y));
        Assert.StartsWith("Not enough space", plain.Shortage);
        Assert.Contains("fit=\"shrink\"", plain.Shortage);
    }

    [Fact]
    public void Shrink_FitsAlready_DoesNothing()
    {
        var (s, d) = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"shrink\"");
        Assert.Equal(1f, ProcessBox(s).ContentScale);
        Assert.False(ProcessBox(s).HasContentTransform);
        Assert.Null(d.Shortage);
    }

    [Fact]
    public void Shrink_HitTest_FindsEquipmentWhereItIsDrawn()
    {
        var (s, d) = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"shrink\"", 420, 220);
        var box = ProcessBox(s);
        var map = new ScreenMap(box.ContentScale, box.ContentOffset);
        foreach (var name in new[] { "P1", "V1", "T2" })
        {
            var n = Node(d, name);
            var at = map.Map(new SKPoint(n.Box.Rect.MidX, n.Box.Rect.MidY));
            var path = HitTester.HitPath(s.Root!, at);
            Assert.Contains(path, x => ReferenceEquals(x.Box, n.Box));
            Assert.Equal(box.ContentScale, HitTester.ScaleOf(path, n.Box), 0.0001);
        }

        // 줄이기 전 자리(내용 좌표 그대로)는 그 장비가 아니다
        var p1 = Node(d, "T2").Box.Rect;
        Assert.DoesNotContain(HitTester.HitPath(s.Root!, new SKPoint(p1.MidX, p1.MidY)), x => ReferenceEquals(x.Box, Node(d, "T2").Box));
    }

    [Fact]
    public void Shrink_Fixed_ScalesEquipmentBeyondArea()
    {
        const string markup = """
            <DvTerminal name="IN" text="IN" x="60" y="60"/>
            <DvPump name="P1" label="P-1" x="700" y="60"/>
            <DvLine from="IN" to="P1.in"/>
            """;
        var (s, d) = ProcessTests.Start(markup, "placement=\"fixed\" fit=\"shrink\"", 500, 300);
        var box = ProcessBox(s);
        Assert.True(box.ContentScale < 1f);
        var drawn = new ScreenMap(box.ContentScale, box.ContentOffset).Map(d.Drawn);
        var inner = Inner(box);
        inner.Inflate(0.5f, 0.5f);
        Assert.True(inner.Contains(drawn), $"{drawn} 이 영역 {inner} 밖");
    }

    [Fact]
    public void Shrink_Click_OnDrawnEquipment()
    {
        var vm = new ProcessTests.Vm();
        var (s, d) = ProcessTests.Start(ProcessInstrumentTests.Loop.Replace("<DvPump name=\"P2\" label=\"P-102\"/>", "<DvPump name=\"P2\" label=\"P-102\" onclick=\"Hit\"/>"),
            "fit=\"shrink\"", 420, 220, vm: vm);
        var box = ProcessBox(s);
        var at = new ScreenMap(box.ContentScale, box.ContentOffset).Map(new SKPoint(Node(d, "P2").Box.Rect.MidX, Node(d, "P2").Box.Rect.MidY));
        s.PointerDown(at);
        s.PointerUp(at);
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Shrink_SliderDrag_FollowsTheFinger()
    {
        // 줄여 그린 슬라이더 — 그려진 막대 왼쪽 끝 → 오른쪽 끝을 끌면 최솟값 → 최댓값(배율만큼 덜 가지 않는다)
        var vm = new ProcessTests.Vm { Level = 0 };
        var body = ProcessInstrumentTests.Loop.Replace("<DvVessel name=\"T1\" level=\"{Level}\" label=\"T-101\"/>",
            "<DvVessel name=\"T1\" level=\"{Level}\" label=\"T-101\"><DvAttach side=\"left\"><DvSlider value=\"{Bind Level}\" style=\"width:160\"/></DvAttach></DvVessel>");
        var (s, _) = ProcessTests.Start(body, "fit=\"shrink\"", 420, 220, vm: vm);
        var box = ProcessBox(s);
        Assert.True(box.ContentScale < 0.8f);
        var map = new ScreenMap(box.ContentScale, box.ContentOffset);
        var slider = Flatten(s.Root!).First(b => b.Node.Tag == "DvSlider");
        var (a, z) = Devinno.UI.Interaction.SliderMetrics.Track(slider);
        var (sa, sz) = (map.Map(a), map.Map(z));
        s.PointerDown(sa);
        for (var t = 0.1f; t <= 1.001f; t += 0.1f)
            s.PointerMove(new SKPoint(sa.X + (sz.X - sa.X) * t, sa.Y));
        s.PointerUp(sz);
        Assert.Equal(100, vm.Level, 0.5);
    }

    [Fact]
    public void Fit_InvalidValue_NotShrinking()
    {
        var (s, d) = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"stretch\"", 560, 300);
        Assert.False(d.Shrink);
        Assert.Equal(1f, ProcessBox(s).ContentScale);
    }

    [Fact]
    public void FontSize_UnitsFollowOuter()
    {
        var (_, d) = ProcessTests.Start(ProcessUnitTests.PlantMarkup, "direction=\"tb\" style=\"font-size:28\"", 1000, 1600,
            ProcessUnitTests.CreateRegistry(), new ProcessUnitTests.Vm());
        var unit = d.Nodes.First(n => n.Unit is not null).Unit!;
        Assert.Equal(2f, unit.TextScale, 0.001);
    }

    [Fact]
    public void FontSize_AttachedBoxesGrowWithIt()
    {
        // 붙은 칸(DvAttach)도 글자 배율만큼 — 자리 크기 × 1.5, 안쪽은 1.5배로 그린다
        var (s1, a) = ProcessTests.Start(ProcessTests.Blender, "direction=\"tb\"");
        var (s2, b) = ProcessTests.Start(ProcessTests.Blender, "direction=\"tb\" style=\"font-size:21\"");
        var ra = Node(a, "BL").AttachRects.Select(r => r.Size).ToArray();
        var rb = Node(b, "BL").AttachRects.Select(r => r.Size).ToArray();
        for (var i = 0; i < ra.Length; i++)
        {
            Assert.Equal(ra[i].Width * 1.5f, rb[i].Width, 0.05);
            Assert.Equal(ra[i].Height * 1.5f, rb[i].Height, 0.05);
        }

        var box1 = Node(a, "BL").Attach[0].Box;
        var box2 = Node(b, "BL").Attach[0].Box;
        Assert.False(box1.HasContentTransform);
        Assert.Equal(1.5f, box2.ContentScale, 0.001);
        Assert.Equal(box1.Rect.Width * 1.5f, box2.Rect.Width, 0.05);
    }

    [Fact]
    public void FontSize_And_Shrink_AttachedButtonStillClicks()
    {
        var vm = new ProcessTests.Vm();
        var (s, d) = ProcessTests.Start(ProcessTests.Blender, "direction=\"tb\" fit=\"shrink\" style=\"font-size:21\"", 300, 380, vm: vm);
        var process = ProcessBox(s);
        Assert.True(process.ContentScale < 1f);
        var attach = Node(d, "BL").Attach.Single(x => x.Side == PortSide.Left).Box;
        var btn = Flatten(attach).First(x => x.Node.Tag == "DvButton");
        // 버튼 → 붙은 칸(글자 배율) → 공정도(축소) 순서로 화면 좌표
        var inAttach = new ScreenMap(attach.ContentScale, attach.ContentOffset).Map(new SKPoint(btn.Rect.MidX, btn.Rect.MidY));
        var at = new ScreenMap(process.ContentScale, process.ContentOffset).Map(inAttach);
        s.PointerDown(at);
        s.PointerUp(at);
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Shrink_SmallestText_CountsAttachedBoxes()
    {
        // 붙은 칸 글자(caption 등)가 공정도 글자(11)보다 작으면 경고의 X 는 그 글자 기준
        var body = ProcessTests.Blender.Replace("<DvButton variant=\"secondary\" text=\"Reset\" onclick=\"Hit\"/>",
            "<DvLabel text=\"tiny\" style=\"font-size:8\"/>");
        Assert.NotEqual(ProcessTests.Blender, body);
        var (s, d) = ProcessTests.Start(body, "direction=\"tb\" fit=\"shrink\"", 200, 260);
        var scale = ProcessBox(s).ContentScale;
        Assert.True(scale < 1f);
        Assert.Contains($"text is now {8 * scale:0.#}px", d.Shortage);
    }
    // ── fit="contain"(좌표 배치) — base-size 로 그린 도면을 통째로 줄이기·키우기, 가운데 ──

    private const string FixedMarkup = """
        <DvTerminal name="IN" text="IN" x="80" y="100"/>
        <DvPump name="P1" label="P-1" x="300" y="100" onclick="Hit"/>
        <DvVessel name="T1" label="T-1" level="50" x="520" y="200"/>
        <DvLine from="IN" to="P1.in"/>
        <DvLine from="P1.out" to="T1.top"/>
        """;

    // 공정도가 영역 전체를 받게(DvScreen 은 flex 가 아니라 grow 가 안 먹으므로 크기를 직접)
    private static (DvSurface S, ProcessDiagram D) StartSized(string body, string attrs, float w, float h, ProcessTests.Vm? vm = null) =>
        ProcessTests.Start(body, $"{attrs} style=\"width:{w}; height:{h}\"", w, h, vm: vm);

    private static SKRect Drawn(DvSurface s, ProcessDiagram d)
    {
        var b = ProcessBox(s);
        return new ScreenMap(b.ContentScale, b.ContentOffset).Map(d.Drawn);
    }

    [Theory]
    [InlineData(600, 300, 1f)]     // 기준 크기 그대로
    [InlineData(1200, 600, 2f)]    // 두 배 — 키운다
    [InlineData(300, 150, 0.5f)]   // 절반 — 줄인다
    public void Contain_ScalesBaseSizeBothWays(float w, float h, float expected)
    {
        var (s, d) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\" fit=\"contain\"", w, h);
        var box = ProcessBox(s);
        Assert.Equal(expected, box.ContentScale, 0.001);
        var drawn = Drawn(s, d);
        Assert.Equal(600 * expected, drawn.Width, 0.5);
        Assert.Equal(300 * expected, drawn.Height, 0.5);
    }

    [Theory]
    [InlineData(1600, 400)] // 가로로 긴 영역 — 세로에 맞추고 가로 가운데
    [InlineData(400, 900)]  // 세로로 긴 영역 — 가로에 맞추고 세로 가운데
    public void Contain_CentersOnTheLongAxis(float w, float h)
    {
        var (s, d) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\" fit=\"contain\"", w, h);
        var inner = Inner(ProcessBox(s));
        var drawn = Drawn(s, d);
        Assert.Equal(inner.MidX, drawn.MidX, 0.5);
        Assert.Equal(inner.MidY, drawn.MidY, 0.5);
        Assert.True(drawn.Width <= inner.Width + 0.5f && drawn.Height <= inner.Height + 0.5f);
        Assert.True(MathF.Abs(drawn.Width - inner.Width) < 0.5f || MathF.Abs(drawn.Height - inner.Height) < 0.5f, "한 축은 꽉 찬다");
    }

    [Theory]
    [InlineData(1200, 600)]
    [InlineData(300, 150)]
    public void Contain_LayoutIsTheSameAtEverySize(float w, float h)
    {
        // 장비 위치(내용 좌표)는 영역 크기와 무관 — 그림 한 장처럼 배율만 달라진다
        var (_, a) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\" fit=\"contain\"", 600, 300);
        var (_, b) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\" fit=\"contain\"", w, h);
        foreach (var n in a.Nodes)
            Assert.Equal((n.G.X, n.G.Y), (Node(b, n.Name).G.X, Node(b, n.Name).G.Y));
        for (var i = 0; i < a.Lines.Count; i++)
            Assert.Equal(a.Lines[i].G!.Points, b.Lines[i].G!.Points);
    }

    [Theory]
    [InlineData(1200, 600)]
    [InlineData(300, 150)]
    public void Contain_ClickFollowsTheDrawing(float w, float h)
    {
        var vm = new ProcessTests.Vm();
        var (s, d) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\" fit=\"contain\"", w, h, vm: vm);
        var box = ProcessBox(s);
        var r = Node(d, "P1").Box.Rect;
        var at = new ScreenMap(box.ContentScale, box.ContentOffset).Map(new SKPoint(r.MidX, r.MidY));
        s.PointerDown(at);
        s.PointerUp(at);
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Contain_Shrinking_ReportsTextSize_Enlarging_Silent()
    {
        var (_, small) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\" fit=\"contain\"", 300, 150);
        Assert.StartsWith("Shrunk to 50%", small.Shortage);
        var (_, big) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\" fit=\"contain\"", 1200, 600);
        Assert.Null(big.Shortage);
    }

    [Fact]
    public void Contain_WithoutBaseSize_UsesTheDrawingExtent()
    {
        var (s, d) = StartSized(FixedMarkup, "placement=\"fixed\" fit=\"contain\"", 1600, 900);
        Assert.True(ProcessBox(s).ContentScale > 1f, "내용 범위 기준으로 키운다");
        var inner = Inner(ProcessBox(s));
        Assert.Equal(inner.MidX, Drawn(s, d).MidX, 0.5);
    }

    [Fact]
    public void BaseSizeWithoutFit_KeepsPositionOnlyScaling()
    {
        // 기존 동작(10-27) 그대로 — 위치만 비례, 장비 크기·내용 배율 없음
        var (s, d) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\"", 1200, 600);
        Assert.False(ProcessBox(s).HasContentTransform);
        var (_, at1) = StartSized(FixedMarkup, "placement=\"fixed\" base-size=\"600 300\"", 600, 300);
        Assert.Equal(Node(at1, "P1").Box.Rect.Width, Node(d, "P1").Box.Rect.Width, 0.01);
        Assert.True(Node(d, "T1").G.X > Node(at1, "T1").G.X + 100, "위치는 벌어진다");
    }

    [Fact]
    public void Contain_OnAutoPlacement_ActsAsShrink()
    {
        var (s, d) = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"contain\"", 420, 220);
        Assert.False(d.Contain);
        Assert.True(d.Shrink);
        Assert.True(ProcessBox(s).ContentScale < 1f);
    }
    // ── 경고 — 값 글자가 배관을 덮을 때, 같은 프로세스에서 다시 만든 화면에서도 다시 알림 ──

    private sealed class Capture : System.Diagnostics.TraceListener
    {
        public readonly List<string> Lines = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { }
        public override void TraceEvent(System.Diagnostics.TraceEventCache? c, string source, System.Diagnostics.TraceEventType t, int id, string? message)
        {
            lock (Lines)
                Lines.Add(message ?? "");
        }
    }

    private static List<string> Warnings(Action render)
    {
        var cap = new Capture();
        System.Diagnostics.Trace.Listeners.Add(cap);
        try
        {
            render();
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(cap);
        }

        lock (cap.Lines)
            return cap.Lines.ToList();
    }

    [Fact]
    public void ValueText_OverPipe_IsReported()
    {
        // 증류탑 견본에서 TIC-201 을 리보일러 복귀 배관 쪽으로 — font-size 21 이면 값 글자가 그 배관을 덮는다
        var body = DistillationSample.Markup.Replace("name=\"TV201\" kind=\"control\" open=\"true\" label=\"TV-201\" x=\"470\" y=\"725\"",
            "name=\"TV201\" kind=\"control\" open=\"true\" label=\"TV-201\" x=\"500\" y=\"725\"");
        Assert.NotEqual(DistillationSample.Markup, body);
        var w = Warnings(() =>
        {
            var s = new DvSurface($"<DvScreen><DvProcess placement=\"fixed\" base-size=\"1800 980\" fit=\"contain\" style=\"width:1800; height:980; font-size:21\">{body}</DvProcess></DvScreen>",
                new ProcessTests.Vm(), DvTheme.Light);
            s.Layout(1800, 980);
            using var bmp = new SKBitmap(1800, 980);
            using var canvas = new SKCanvas(bmp);
            s.RenderTo(canvas, 1800, 980, 0); // 경고는 그릴 때 알린다
        });
        Assert.Contains(w, m => m.Contains("'TIC201' value text overlaps 'E202.out'→'T201.left@0.9'"));
    }

    [Fact]
    public void LayoutWarnings_AppearAgain_ForANewScreen()
    {
        // 같은 마크업을 새 화면으로 다시 만들면(고쳐서 다시 렌더) 남은 경고가 다시 보인다
        static void Render()
        {
            var s = new DvSurface($"<DvScreen><DvProcess style=\"width:420; height:220\">{ProcessInstrumentTests.Loop}</DvProcess></DvScreen>", new ProcessTests.Vm(), DvTheme.Light);
            s.Layout(420, 220);
            using var bmp = new SKBitmap(420, 220);
            using var canvas = new SKCanvas(bmp);
            s.RenderTo(canvas, 420, 220, 0);
        }

        Assert.Contains(Warnings(Render), m => m.Contains("Not enough space"));
        Assert.Contains(Warnings(Render), m => m.Contains("Not enough space"));
    }
}
