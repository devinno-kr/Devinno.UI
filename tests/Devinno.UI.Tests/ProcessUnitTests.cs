using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 공정도 유닛(DvUnit) — 안쪽을 먼저 배치하고 바깥에서는 장비 하나처럼. 바깥 연결은 DvPort 로만. 컴포넌트로 같은 유닛 반복.
public class ProcessUnitTests
{
    public sealed class Blend
    {
        public required string Name { get; init; }
        public double Level { get; set; }
        public bool Run { get; set; }
    }

    public sealed class Vm
    {
        public Blend BL1 { get; } = new() { Name = "BL-1", Level = 20, Run = true };
        public Blend BL2 { get; } = new() { Name = "BL-2", Level = 50 };
        public Blend BL3 { get; } = new() { Name = "BL-3", Level = 80, Run = true };
        public List<string> Resets { get; } = new();
        public int Plain;

        public void Reset(Blend u) => Resets.Add(u.Name);
        public void Hit() => Plain++;
    }

    private const string BlenderDef = """
        <DvComponent name="Blender">
          <DvParam name="U"/>
          <DvUnit direction="tb" label="{U.Name}">
            <DvTerminal name="IN" text="FEED"/>
            <DvVessel name="T" level="{U.Level}">
              <DvAgitator running="{U.Run}"/>
              <DvAttach side="left"><DvButton variant="secondary" text="Reset" onclick="Reset"/></DvAttach>
            </DvVessel>
            <DvPump name="P1" running="{U.Run}"/>
            <DvPump name="P2"/>
            <DvPort name="out" side="bottom" at="0.7"/>
            <DvLine from="IN" to="T.top"/>
            <DvLine from="T.bottom" to="P1.in"/>
            <DvLine from="T.bottom" to="P2.in"/>
            <DvLine from="P1.out" to="out"/>
            <DvLine from="P2.out" to="out"/>
          </DvUnit>
        </DvComponent>
        """;

    internal const string PlantMarkup = Plant;

    internal static ComponentRegistry CreateRegistry() => Registry();

    private const string Plant = """
        <Blender name="BL1" U="{BL1}"/>
        <Blender name="BL2" U="{BL2}"/>
        <Blender name="BL3" U="{BL3}"/>
        <DvVessel name="RMT" level="40" label="RMT"/>
        <DvTerminal name="OUT" text="FIRM"/>
        <DvLine from="BL1.out" to="RMT.top"/>
        <DvLine from="BL2.out" to="RMT.top"/>
        <DvLine from="BL3.out" to="RMT.top"/>
        <DvLine from="RMT.bottom" to="OUT"/>
        """;

    private static ComponentRegistry Registry()
    {
        var r = new ComponentRegistry();
        r.Register(BlenderDef);
        return r;
    }

    private static (DvSurface S, ProcessDiagram D, Vm Vm) Start(string body, string attrs = "direction=\"tb\"", Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen><DvProcess {attrs}>{body}</DvProcess></DvScreen>", vm, DvTheme.Light, Registry());
        s.Layout(1600, 1200);
        return (s, ProcessTests.Diagram(s), vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static ProcessNode Node(ProcessDiagram d, string name) => d.Nodes.Single(n => n.Name == name);

    // 화면 좌표의 심볼 영역
    private static SKRect Screen(ProcessNode n) =>
        new(n.Owner.Origin.X + n.G.X + n.Sym.Left, n.Owner.Origin.Y + n.G.Y + n.Sym.Top,
            n.Owner.Origin.X + n.G.X + n.Sym.Right, n.Owner.Origin.Y + n.G.Y + n.Sym.Bottom);

    [Fact]
    public void ComponentUnits_Expand_PerInstanceValues()
    {
        var (_, d, _) = Start(Plant);
        Assert.Equal(["BL1", "BL2", "BL3", "RMT", "OUT"], d.Nodes.Select(n => n.Name));
        var levels = new[] { "BL1", "BL2", "BL3" }.Select(u => Node(Node(d, u).Unit!, "T").Level).ToArray();
        Assert.Equal([20f, 50f, 80f], levels);
        Assert.Equal("BL-2", Node(d, "BL2").Label);
        Assert.False(Node(Node(d, "BL2").Unit!, "P1").On("running"));
    }

    [Fact]
    public void Layout_Clean_InsideAndOutside_NoCrossings()
    {
        var (_, d, _) = Start(Plant);
        Assert.Empty(ProcessTests.Violations(d));
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
        foreach (var u in d.Nodes.Where(n => n.Unit is not null))
        {
            Assert.Empty(ProcessTests.Violations(u.Unit!));
            Assert.All(u.Unit!.Lines, l => Assert.Equal(0, l.G!.Crossings));
        }
    }

    [Theory]
    [InlineData(900)]
    [InlineData(700)]
    [InlineData(500)]
    public void Squeezed_StaysClean(float h)
    {
        var (_, d, _) = Start(Plant, $"direction=\"tb\" look=\"rich\" style=\"height:{h}\"");
        var bad = ProcessTests.Violations(d);
        Assert.True(bad.Count == 0, $"spacing={d.Graph!.Spacing}: " + string.Join(" / ", bad) + " :: " +
            string.Join(" | ", d.Lines.Select(l => $"{l.FromRef}->{l.ToRef} " + string.Join(" ", l.G!.Points.Select(p => $"({p.X:0},{p.Y:0})")))) + " :: " +
            string.Join(" | ", d.Nodes.Select(n => $"{n.Name}@({n.G.X:0},{n.G.Y:0}) {n.Footprint}")));
    }

    private const string FullDef = """
        <DvComponent name="BlenderFull">
          <DvParam name="U"/>
          <DvUnit direction="tb" label="{U.Name}">
            <DvTerminal name="IN" text="MTO RMMT-1"/>
            <DvVessel name="T" level="{U.Level}">
              <DvAgitator running="{U.Run}"/>
              <DvAttach side="right"><DvLabel class="caption muted" text="TIA 1001"/><DvLabel class="title" text="83.5 ℃"/></DvAttach>
              <DvAttach side="left"><DvButton variant="secondary" size="sm" text="Reset" onclick="Reset"/></DvAttach>
            </DvVessel>
            <DvPump name="P1" running="{U.Run}" label="RTP 1011"/>
            <DvPump name="P2" label="RTP 1012"/>
            <DvPort name="out" side="bottom" at="0.7"/>
            <DvLine from="IN" to="T.top" arrow="end"/>
            <DvLine from="T.bottom" to="P1.in"/>
            <DvLine from="T.bottom" to="P2.in"/>
            <DvLine from="P1.out" to="out"/>
            <DvLine from="P2.out" to="out"/>
          </DvUnit>
        </DvComponent>
        """;

    [Theory]
    [InlineData(1100)]
    [InlineData(822)]
    public void FullUnits_Squeezed_StaysClean(float h)
    {
        var reg = Registry();
        reg.Register(FullDef);
        var s = new DvSurface($$"""
            <DvScreen><DvProcess direction="tb" look="rich" style="width:957; height:{{h}}">
              <BlenderFull name="BL1" U="{BL1}"/><BlenderFull name="BL2" U="{BL2}"/><BlenderFull name="BL3" U="{BL3}"/>
              <DvVessel name="RMT" level="40" label="RMT"/>
              <DvTerminal name="OUT" text="RESIN TANK FIRM"/>
              <DvLine from="BL1.out" to="RMT.top" arrow="end"/><DvLine from="BL2.out" to="RMT.top" arrow="end"/><DvLine from="BL3.out" to="RMT.top" arrow="end"/>
              <DvLine from="RMT.bottom" to="OUT"/>
            </DvProcess></DvScreen>
            """, new Vm(), DvTheme.Dark, reg);
        s.Layout(1000, 1200);
        var d = ProcessTests.Diagram(s);
        var bad = ProcessTests.Violations(d);
        Assert.True(bad.Count == 0, $"spacing={d.Graph!.Spacing}: " + string.Join(" / ", bad) + " :: " +
            string.Join(" | ", d.Lines.Select(l => $"{l.FromRef}->{l.ToRef} " + string.Join(" ", l.G!.Points.Select(p => $"({p.X:0},{p.Y:0})")))) + " :: " +
            string.Join(" | ", d.Nodes.Select(n => $"{n.Name}@({n.G.X:0},{n.G.Y:0}) {n.Footprint} sym={n.Sym}")));
    }

    [Fact]
    public void LeftToRight_Units_NoJogs_NoCrossings()
    {
        // 유닛 안 펌프 출구 → 유닛 포트: 곧은 끝이 거의 겹치면 작은 지그재그가 생기던 문제,
        // 같은 열 유닛 셋 → 한 탱크 윗면: 자리 순서·긋는 순서로 엇갈리던 문제
        var reg = new ComponentRegistry();
        reg.Register(FullDef.Replace("BlenderFull", "BlLr").Replace("direction=\"tb\"", "direction=\"lr\"").Replace("side=\"bottom\"", "side=\"right\""));
        var s = new DvSurface("""
            <DvScreen><DvProcess direction="lr" look="rich">
              <BlLr name="BL1" U="{BL1}"/><BlLr name="BL2" U="{BL2}"/><BlLr name="BL3" U="{BL3}"/>
              <DvVessel name="RMT" kind="hopper" level="40" label="RMT"/>
              <DvLine from="BL1.out" to="RMT.top"/><DvLine from="BL2.out" to="RMT.top"/><DvLine from="BL3.out" to="RMT.top"/>
            </DvProcess></DvScreen>
            """, new Vm(), DvTheme.Dark, reg);
        s.Layout(1100, 1600);
        var d = ProcessTests.Diagram(s);
        Assert.Empty(ProcessTests.Violations(d));
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
        foreach (var u in d.Nodes.Where(n => n.Unit is not null).Select(n => n.Unit!))
        {
            Assert.Empty(ProcessTests.Violations(u));
            foreach (var l in u.Lines)
            {
                var pts = l.G!.Points;
                for (var k = 1; k + 2 < pts.Count; k++) // 양 끝(포트 곧은 끝)을 뺀 안쪽 구간
                {
                    var len = MathF.Abs(pts[k + 1].X - pts[k].X) + MathF.Abs(pts[k + 1].Y - pts[k].Y);
                    Assert.True(len >= 4, $"{l.FromRef}->{l.ToRef}: {len:0.#}px 조각 — " + string.Join(" ", pts.Select(p => $"({p.X:0.#},{p.Y:0.#})")));
                }
            }
        }
    }

    [Fact]
    public void ThreeUnits_IntoOneTop_MergeIntoOneNozzle()
    {
        var (_, d, _) = Start(Plant);
        var into = d.Lines.Where(l => l.ToRef == "RMT.top").ToList();
        Assert.Single(into.Select(l => l.G!.To.Point).Distinct());
        Assert.Single(into.Select(l => l.G!.Net).Distinct());
        Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
    }

    [Fact]
    public void Units_FollowOuterAlign()
    {
        var reg = Registry();
        reg.Register(FullDef.Replace("BlenderFull", "BlLr").Replace("direction=\"tb\"", "direction=\"lr\"").Replace("side=\"bottom\"", "side=\"right\""));
        foreach (var align in new[] { "step", "row" })
        {
            var s = new DvSurface($$"""
                <DvScreen><DvProcess direction="lr" align="{{align}}">
                  <BlLr name="BL1" U="{BL1}"/><DvVessel name="RMT"/><DvLine from="BL1.out" to="RMT.top"/>
                </DvProcess></DvScreen>
                """, new Vm(), DvTheme.Light, reg);
            s.Layout(1400, 1400);
            var inner = Node(ProcessTests.Diagram(s), "BL1").Unit!;
            Assert.Equal(align == "row" ? ProcessAlign.Row : ProcessAlign.Step, inner.Graph!.Align);
            var t = Screen(Node(inner, "T"));
            var inY = inner.Origin.Y + inner.Lines.First(l => l.ToRef == "P1.in").G!.To.Point.Y;
            if (align == "step")
                Assert.True(inY > t.Bottom); // 펌프 입구가 탱크 바닥 아래
            else
                Assert.True(inY < t.Bottom); // 탱크와 같은 줄
            Assert.Empty(ProcessTests.Violations(inner));
        }
    }

    [Fact]
    public void Units_SideBySide_TargetCentered()
    {
        // 자연 크기 영역(늘이지 않은 배치)에서 — 영역을 채우려고 늘이면 위치만 비례라 이 맞춤은 조금 어긋날 수 있다
        var (s0, _, _) = Start(Plant);
        var size = ProcessBuilder.Natural(Flatten(s0.Root!).First(b => b.Process is not null));
        var (_, d, _) = Start(Plant, $"direction=\"tb\" style=\"width:{size.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)};height:{size.Height.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"");
        var units = new[] { "BL1", "BL2", "BL3" }.Select(n => Screen(Node(d, n))).ToArray();
        Assert.Equal(units[0].Top, units[1].Top, 0.5);
        Assert.True(units[0].Right < units[1].Left && units[1].Right < units[2].Left);
        var rmt = Screen(Node(d, "RMT"));
        Assert.True(rmt.Top > units[0].Bottom);
        // 세 유닛 출구 포트의 가운데 아래(포트는 유닛 폭 0.7 자리)
        var ports = d.Lines.Where(l => l.ToRef == "RMT.top").Select(l => d.Origin.X + l.G!.From.Point.X).ToArray();
        Assert.Equal(ports.Average(), rmt.MidX, 2);
    }

    [Fact]
    public void UnitPort_OnFrame_InnerAndOuterPipesMeet()
    {
        var (_, d, _) = Start(Plant);
        var bl = Node(d, "BL1");
        var frame = Screen(bl);
        var outer = d.Lines.First(l => l.FromRef == "BL1.out").G!.From.Point;
        var outerAbs = new SKPoint(d.Origin.X + outer.X, d.Origin.Y + outer.Y);
        Assert.Equal(frame.Bottom, outerAbs.Y, 0.5);
        Assert.Equal(frame.Left + frame.Width * 0.7f, outerAbs.X, 0.5);

        var inner = bl.Unit!;
        foreach (var l in inner.Lines.Where(l => l.ToRef == "out"))
        {
            var p = l.G!.To.Point;
            var abs = new SKPoint(inner.Origin.X + p.X, inner.Origin.Y + p.Y);
            Assert.Equal(outerAbs.X, abs.X, 0.5);
            Assert.Equal(outerAbs.Y, abs.Y, 0.5);
        }
    }

    [Fact]
    public void InnerPipes_StayInsideFrame()
    {
        var (_, d, _) = Start(Plant);
        foreach (var u in d.Nodes.Where(n => n.Unit is not null))
        {
            var size = u.Unit!.Graph!.Size;
            foreach (var p in u.Unit.Lines.SelectMany(l => l.G!.Points))
            {
                Assert.InRange(p.X, -0.5f, size.Width + 0.5f);
                Assert.InRange(p.Y, -0.5f, size.Height + 0.5f);
            }
        }
    }

    [Fact]
    public void Reset_InUnitTemplate_ReceivesItsUnit()
    {
        var (s, d, vm) = Start(Plant);
        var buttons = Flatten(s.Root!).Where(b => b.Node.Tag == "DvButton").ToList();
        Assert.Equal(3, buttons.Count);
        foreach (var b in new[] { buttons[2], buttons[0] })
        {
            var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
            s.PointerDown(p);
            s.PointerUp(p);
        }

        Assert.Equal(["BL-3", "BL-1"], vm.Resets);
    }

    [Fact]
    public void TypeMatchedClick_WorksForPlainComponents_Too()
    {
        var reg = new ComponentRegistry();
        reg.Register("""
            <DvComponent name="UnitCard">
              <DvParam name="U"/>
              <DvButton text="{U.Name}" onclick="Reset"/>
            </DvComponent>
            """);
        var vm = new Vm();
        var s = new DvSurface("<DvScreen><UnitCard U=\"{BL2}\"/></DvScreen>", vm, DvTheme.Light, reg);
        s.Layout(400, 200);
        var b = Flatten(s.Root!).First(x => x.Node.Tag == "DvButton").Rect;
        s.PointerDown(new SKPoint(b.MidX, b.MidY));
        s.PointerUp(new SKPoint(b.MidX, b.MidY));
        Assert.Equal(["BL-2"], vm.Resets);
    }

    [Fact]
    public void DirectUnit_WithoutComponent()
    {
        var (_, d, _) = Start("""
            <DvUnit name="U1" direction="tb" label="Feed">
              <DvVessel name="T" level="30"/>
              <DvPump name="P"/>
              <DvPort name="out" side="right"/>
              <DvLine from="T.bottom" to="P.in"/>
              <DvLine from="P.out" to="out"/>
            </DvUnit>
            <DvValve name="V" open="true"/>
            <DvLine from="U1.out" to="V.in"/>
            """, attrs: "");
        var u = Node(d, "U1");
        Assert.NotNull(u.Unit);
        Assert.True(u.Unit!.Vertical);
        Assert.False(d.Lines[0].Error);
        Assert.Empty(ProcessTests.Violations(d));
        Assert.Empty(ProcessTests.Violations(u.Unit));
    }

    [Fact]
    public void OuterReference_IntoUnit_IsError()
    {
        var (_, d, _) = Start(Plant + """<DvLine from="BL1.T.bottom" to="RMT.side"/>""");
        var bad = d.Lines.Last();
        Assert.True(bad.Error);
        Assert.Null(bad.G);
        Assert.True(Node(d, "BL1").Error);
        Assert.All(d.Lines.Take(d.Lines.Count - 1), l => Assert.False(l.Error));
    }

    [Fact]
    public void UnknownUnitPort_IsError()
    {
        var (_, d, _) = Start(Plant + """<DvLine from="BL1.drain" to="RMT.side"/>""");
        Assert.True(d.Lines.Last().Error);
    }

    [Fact]
    public void PortOutsideUnit_Ignored_AndUnitInUnit_NotDrawn()
    {
        var (_, d, _) = Start("""
            <DvPort name="x" side="left"/>
            <DvUnit name="A">
              <DvUnit name="B"><DvPump name="P"/></DvUnit>
              <DvPump name="Q"/>
            </DvUnit>
            """);
        Assert.Equal(["A"], d.Nodes.Select(n => n.Name));
        Assert.Equal(["Q"], Node(d, "A").Unit!.Nodes.Select(n => n.Name));
    }

    [Fact]
    public void EmptyUnit_HasMinimumFrame()
    {
        var (_, d, _) = Start("""<DvUnit name="E"><DvPort name="o" side="right"/></DvUnit><DvPump name="P"/><DvLine from="E.o" to="P.in"/>""", attrs: "");
        var size = Node(d, "E").Unit!.Graph!.Size;
        Assert.True(size.Width > 0 && size.Height > 0);
        Assert.Empty(ProcessTests.Violations(d));
    }

    [Fact]
    public void ValueChange_ReusesOuterAndInnerLayout()
    {
        var vm = new Vm();
        var (s, d, _) = Start(Plant, vm: vm);
        var outer = d.Graph;
        var inner = Node(d, "BL1").Unit!.Graph;
        vm.BL1.Level = 90;
        vm.BL2.Run = true;
        s.Invalidate();
        s.Layout(1600, 1200);
        var d2 = ProcessTests.Diagram(s);
        Assert.Same(outer, d2.Graph);
        Assert.Same(inner, Node(d2, "BL1").Unit!.Graph);
        Assert.Equal(90, Node(Node(d2, "BL1").Unit!, "T").Level, 0.01);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("rich")]
    public void Paints_UnitPipes_UnderEquipment(string look)
    {
        var (s, d, _) = Start(Plant, $"direction=\"tb\" look=\"{look}\"");
        using var bmp = new SKBitmap(1600, 1200);
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.White);
        s.RenderTo(c, 1600, 1200, 0);
        // 유닛 안 배관(바닥 → 펌프 줄기) 가운데 점은 흰 바탕이 아님
        var inner = Node(d, "BL1").Unit!;
        var l = inner.Lines.First(x => x.FromRef == "T.bottom").G!;
        var a = l.Points[0];
        var b = l.Points[1];
        var mid = new SKPoint(inner.Origin.X + (a.X + b.X) / 2, inner.Origin.Y + (a.Y + b.Y) / 2);
        Assert.NotEqual(SKColors.White, bmp.GetPixel((int)mid.X, (int)mid.Y));
    }
}
