using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 매체 색(DvLine medium) — 적은 선부터 흐름 방향으로 장비 안 길(상태 무관)을 따라 퍼지고, 용기·끝점·자기 매체를 적은 선에서 멈춘다.
public class ProcessMediumTests
{
    // 냉각 공정: 공급수 → 펌프(정지) → 닫힌 밸브 → 열교환기 → 탱크 → 펌프 → 배출. 열매체는 스팀
    internal const string Cooling = """
        <DvTerminal name="IN" text="RAW WATER"/>
        <DvPump name="P1" label="P-1"/>
        <DvValve name="V1" label="XV-1"/>
        <DvExchanger name="E1" label="E-101"/>
        <DvTerminal name="STM" text="STEAM"/>
        <DvTerminal name="CND" text="CONDENSATE"/>
        <DvVessel name="T1" level="50" label="T-1"/>
        <DvPump name="P2" running="true" label="P-2"/>
        <DvTerminal name="OUT" text="PRODUCT"/>
        <DvLine from="IN" to="P1.in" medium="water"/>
        <DvLine from="P1.out" to="V1.in"/>
        <DvLine from="V1.out" to="E1.in"/>
        <DvLine from="STM" to="E1.in2" medium="steam"/>
        <DvLine from="E1.out2" to="CND"/>
        <DvLine from="E1.out" to="T1.top"/>
        <DvLine from="T1.bottom" to="P2.in"/>
        <DvLine from="P2.out" to="OUT"/>
        """;

    private static ProcessLine Line(ProcessDiagram d, string from, string to) => d.Lines.Single(l => l.FromRef == from && l.ToRef == to);

    [Fact]
    public void Spreads_ThroughEquipment_RegardlessOfState()
    {
        var (_, d) = ProcessTests.Start(Cooling);
        Assert.Equal("water", Line(d, "IN", "P1.in").Medium);
        Assert.Equal("water", Line(d, "P1.out", "V1.in").Medium);   // 정지한 펌프 너머
        Assert.Equal("water", Line(d, "V1.out", "E1.in").Medium);   // 닫힌 밸브 너머
        Assert.Equal("water", Line(d, "E1.out", "T1.top").Medium);  // 열교환기 공정 쪽
    }

    [Fact]
    public void Exchanger_KeepsPathsSeparate()
    {
        var (_, d) = ProcessTests.Start(Cooling);
        Assert.Equal("steam", Line(d, "STM", "E1.in2").Medium);
        Assert.Equal("steam", Line(d, "E1.out2", "CND").Medium);
    }

    [Fact]
    public void StopsAtVessel()
    {
        var (_, d) = ProcessTests.Start(Cooling);
        Assert.Null(Line(d, "T1.bottom", "P2.in").Medium);
        Assert.Null(Line(d, "P2.out", "OUT").Medium);
    }

    [Fact]
    public void ExplicitMedium_StopsSpreading()
    {
        var (_, d) = ProcessTests.Start(Cooling.Replace("""<DvLine from="V1.out" to="E1.in"/>""", """<DvLine from="V1.out" to="E1.in" medium="oil"/>"""));
        Assert.Equal("water", Line(d, "P1.out", "V1.in").Medium);
        Assert.Equal("oil", Line(d, "V1.out", "E1.in").Medium);
        Assert.Equal("oil", Line(d, "E1.out", "T1.top").Medium);
    }

    [Fact]
    public void Valve3_SpreadsToBothBranches_MixConflict_KeepsFirst()
    {
        var (_, d) = ProcessTests.Start(ProcessValve3ExchangerTests.Divert.Replace("""<DvLine from="T1.bottom" to="P1.in"/>""", """<DvLine from="T1.bottom" to="P1.in" medium="water"/>"""),
            vm: new ProcessValve3ExchangerTests.Vm());
        Assert.Equal("water", Line(d, "V3.b", "E1.in").Medium);
        Assert.Equal("water", Line(d, "V3.c", "DR").Medium);   // 닫힌 가지도 같은 배관

        var (_, m) = ProcessTests.Start(ProcessValve3ExchangerTests.Mix
            .Replace("""<DvLine from="HOT" to="V3.b"/>""", """<DvLine from="HOT" to="V3.b" medium="water"/>""")
            .Replace("""<DvLine from="COLD" to="V3.c"/>""", """<DvLine from="COLD" to="V3.c" medium="acid"/>"""), vm: new ProcessValve3ExchangerTests.Vm());
        Assert.Equal("water", Line(m, "V3.a", "E2.in").Medium); // 먼저 적은 매체
        Assert.Equal("water", Line(m, "E2.out", "OUT").Medium);
    }

    [Fact]
    public void UnknownMedium_IsGray()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="A" text="A"/><DvTerminal name="B" text="B"/>
            <DvLine from="A" to="B" medium="lava"/>
            """);
        Assert.Null(d.Lines.Single().Medium);
    }

    public sealed class BindVm
    {
        public string M { get; set; } = "gas";
    }

    [Fact]
    public void Medium_Binds()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="A" text="A"/><DvValve name="V" open="true"/><DvTerminal name="B" text="B"/>
            <DvLine from="A" to="V.in" medium="{M}"/><DvLine from="V.out" to="B"/>
            """, vm: new BindVm());
        Assert.All(d.Lines, l => Assert.Equal("gas", l.Medium));
    }

    [Fact]
    public void SpreadsAcrossUnitPorts()
    {
        var (_, d) = ProcessTests.Start("""
            <DvTerminal name="A" text="A"/>
            <DvUnit name="U" label="U">
              <DvPort name="in" side="left"/>
              <DvValve name="V" open="true"/>
              <DvPort name="out" side="right"/>
              <DvLine from="in" to="V.in"/>
              <DvLine from="V.out" to="out"/>
            </DvUnit>
            <DvTerminal name="B" text="B"/>
            <DvLine from="A" to="U.in" medium="air"/>
            <DvLine from="U.out" to="B"/>
            """);
        var inner = d.Nodes.Single(n => n.Name == "U").Unit!;
        Assert.All(inner.Lines, l => Assert.Equal("air", l.Medium));
        Assert.Equal("air", Line(d, "U.out", "B").Medium);
    }

    [Theory]
    [InlineData("water", 0xFF3B6EA8u)]
    [InlineData("steam", 0xFF9B3B3Bu)]
    [InlineData(null, 0xFF5F656Cu)]
    public void Plain_PaintsPipeInMediumColor(string? medium, uint expected)
    {
        var attr = medium is null ? "" : $"medium=\"{medium}\"";
        var (s, d) = ProcessTests.Start($"""
            <DvTerminal name="A" text="A"/><DvTerminal name="B" text="B"/>
            <DvLine from="A" to="B" {attr}/>
            """, w: 600, h: 200);
        using var bmp = new SKBitmap(600, 200);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 600, 200);
        var pts = d.Lines.Single().G!.Points;
        // 가장 긴 가로 구간 가운데(관 중심)
        var (a, b) = Enumerable.Range(0, pts.Count - 1).Select(k => (pts[k], pts[k + 1]))
            .Where(x => MathF.Abs(x.Item1.Y - x.Item2.Y) < 0.01f).OrderByDescending(x => MathF.Abs(x.Item1.X - x.Item2.X)).First();
        var px = bmp.GetPixel((int)(d.Origin.X + (a.X + b.X) / 2), (int)MathF.Round(d.Origin.Y + a.Y));
        Assert.Equal(new SKColor(expected), px);
    }
}
