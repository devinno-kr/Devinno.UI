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

    [Fact]
    public void FontSize_UnitsFollowOuter()
    {
        var (_, d) = ProcessTests.Start(ProcessUnitTests.PlantMarkup, "direction=\"tb\" style=\"font-size:28\"", 1000, 1600,
            ProcessUnitTests.CreateRegistry(), new ProcessUnitTests.Vm());
        var unit = d.Nodes.First(n => n.Unit is not null).Unit!;
        Assert.Equal(2f, unit.TextScale, 0.001);
    }
}
