using Devinno.UI;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Process;
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
        var (s, d) = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"contain\"", 560, 300);
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
}
