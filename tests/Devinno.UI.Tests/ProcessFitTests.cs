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
    public void FontSize_UnitsFollowOuter()
    {
        var (_, d) = ProcessTests.Start(ProcessUnitTests.PlantMarkup, "direction=\"tb\" style=\"font-size:28\"", 1000, 1600,
            ProcessUnitTests.CreateRegistry(), new ProcessUnitTests.Vm());
        var unit = d.Nodes.First(n => n.Unit is not null).Unit!;
        Assert.Equal(2f, unit.TextScale, 0.001);
    }
}
