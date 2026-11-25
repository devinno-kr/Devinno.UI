using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Components;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 편집 도구용 공개 읽기: 설비 포트 표(PortsOf), 그려진 배관(PipesOf), 그려진 자리(TryGetDrawnRect·ContentScale), 고유 크기(IntrinsicSize)
public class ProcessInspectorTests
{
    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Ports_of_built_in_equipment()
    {
        var valve = ProcessInspector.PortsOf("DvValve", "ball")!;
        Assert.Equal(["in", "out"], valve.Select(p => p.Name));
        Assert.Equal(PortSide.Left, valve[0].Side);
        Assert.Equal(0.6667f, valve[0].At, 3);
        Assert.Equal("in", valve[0].Role);
        Assert.Equal(PortSide.Right, valve[1].Side);

        var tank = ProcessInspector.PortsOf("DvVessel")!; // 기본 kind = tank
        Assert.True(tank.Single(p => p.Name == "top").Face);
        Assert.True(tank.Single(p => p.Name == "side").EitherSide);

        Assert.Equal(ProcessInspector.PortsOf("DvValve", null), ProcessInspector.PortsOf("DvValve", "gate"));
        Assert.Null(ProcessInspector.PortsOf("DvValve", "no-such-kind"));
        Assert.Null(ProcessInspector.PortsOf("NoSuchTag"));
    }

    [Fact]
    public void Ports_of_a_registered_symbol()
    {
        var reg = new ComponentRegistry();
        reg.Register("""
            <DvSymbol name="Cyclone" view-box="0 0 60 100">
              <DvShape class="vessel" path="M10 0 H50 V40 L35 90 H25 L10 40 Z"/>
              <DvPort name="in" side="left" at="0.2" role="in"/>
              <DvPort name="gas" side="top" at="0.5" role="out"/>
            </DvSymbol>
            """);
        var ports = ProcessInspector.PortsOf("Cyclone", null, reg)!;
        Assert.Equal([new SymbolPort("in", PortSide.Left, 0.2f, "in"), new SymbolPort("gas", PortSide.Top, 0.5f, "out")], ports);
        Assert.Null(ProcessInspector.PortsOf("Cyclone")); // 등록부 없이는 모름
    }

    private const string Flow = """
        <DvScreen style="padding:0">
          <DvProcess {0}>
            <DvVessel name="T1"/>
            <DvPump name="P1"/>
            <DvValve name="V1"/>
            <DvLine from="T1.side" to="P1.in"/>
            <DvLine from="P1.out" to="V1.in"/>
            <DvLine from="V1.out" to="NOPE"/>
          </DvProcess>
        </DvScreen>
        """;

    private static DvSurface Surface(string attrs, float w = 800, float h = 600)
    {
        var s = new DvSurface(Flow.Replace("{0}", attrs), new { }, DvTheme.Light);
        s.Layout(w, h);
        return s;
    }

    [Fact]
    public void Pipes_as_drawn_end_on_the_equipment()
    {
        var s = Surface("style=\"width:600; height:300\"");
        var process = Flatten(s.Root!).Single(b => b.Node.Tag == "DvProcess");
        var pipes = ProcessInspector.PipesOf(process);
        Assert.Equal(2, pipes.Count); // 그리지 못한 줄(NOPE)은 빠진다
        Assert.Equal("P1.in", pipes[0].Line.Attributes["to"]);
        var pump = Flatten(process).First(b => b.Node.Tag == "DvPump").Rect;
        var end = pipes[0].Points[^1];
        Assert.InRange(end.X, pump.Left - 2, pump.Right + 2); // 펌프 상자 언저리의 노즐
        Assert.InRange(end.Y, pump.Top - 2, pump.Bottom + 2);
        Assert.True(pipes.All(p => p.Points.Count >= 2));
        Assert.Empty(ProcessInspector.PipesOf(s.Root!)); // 공정도가 아닌 상자
    }

    [Fact]
    public void Drawn_rect_applies_the_content_scale()
    {
        var s = Surface("fit=\"contain\" style=\"width:200; height:100\"");
        var process = Flatten(s.Root!).Single(b => b.Node.Tag == "DvProcess");
        Assert.True(process.ContentScale < 1f);
        var pump = Flatten(process).First(b => b.Node.Tag == "DvPump");
        Assert.True(HitTester.TryGetDrawnRect(s.Root!, pump, out var drawn, out var visible));
        var k = process.ContentScale;
        var o = process.ContentOffset;
        Assert.Equal(pump.Rect.Left * k + o.X, drawn.Left, 2);
        Assert.Equal(pump.Rect.Width * k, drawn.Width, 2);
        Assert.False(visible.IsEmpty);
        // 그려진 자리의 가운데를 누르면 그 설비가 맞는다
        var hit = HitTester.HitPath(s.Root!, new SKPoint(drawn.MidX, drawn.MidY)).Select(x => x.Box);
        Assert.Contains(pump, hit);

        Assert.False(HitTester.TryGetDrawnRect(process, s.Root!, out _, out _)); // 트리 밖
    }

    [Fact]
    public void Drawn_rect_applies_scroll_offsets()
    {
        var s = new DvSurface("""
            <DvScreen style="padding:0"><DvStack style="height:100; overflow:scroll; gap:0">
              <DvBox style="height:300"/><DvLabel id="x" text="x" style="height:20"/>
            </DvStack></DvScreen>
            """, new { });
        s.Layout(200, 200);
        var scroller = Flatten(s.Root!).First(b => b.IsScroller);
        scroller.ScrollY = 250;
        var label = Flatten(s.Root!).First(b => b.Node.Tag == "DvLabel");
        Assert.True(HitTester.TryGetDrawnRect(s.Root!, label, out var r, out var v));
        Assert.Equal(label.Rect.Top - 250, r.Top, 2);
        Assert.False(v.IsEmpty);
    }

    [Fact]
    public void Intrinsic_size_is_the_size_the_app_gives_a_bar()
    {
        var bar = new Bar();
        var app = new DvApp();
        app.Pages.Add(new Page());
        app.Top = bar;
        var root = app.Surface;
        root.Layout(800, 600);
        var barSurface = bar.Surface!;
        var size = LayoutEngine.IntrinsicSize(barSurface.Root!, 800);
        Assert.Equal(LayoutEngine.MeasureBox(barSurface.Root!, 800), (size.Width, size.Height));
        Assert.Equal(size.Height, barSurface.Root!.Rect.Height, 1);

        var fixedBox = new LayoutEngine().Layout(Devinno.UI.Markup.MarkupParser.Parse("<DvScreen style=\"width:120; height:30\"/>"), null, 800, 600);
        Assert.Equal(new SKSize(120, 30), LayoutEngine.IntrinsicSize(fixedBox));
    }

    private sealed class Page : DvPage
    {
        protected override string MarkupSource => "<DvScreen><DvLabel text=\"p\"/></DvScreen>";
    }

    private sealed class Bar : DvBar
    {
        protected override string MarkupSource => "<DvScreen style=\"padding:12\"><DvLabel text=\"Title\"/><DvLabel text=\"Second line\"/></DvScreen>";
    }
}
