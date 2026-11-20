using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 정의 자식을 읽는 커스텀 위젯(DvDefinitionWidget) — 자식은 상자가 아니라 속성 묶음. 바인딩·순서·재빌드·반복·컴포넌트·경고
public class WidgetDefinitionTests
{
    public sealed class Vm
    {
        public double Turret { get; set; } = 30;
        public double Lift { get; set; } = 120;
        public bool Lamp { get; set; } = true;
        public List<string> Fans { get; } = ["FanA", "FanB"];
    }

    // 그릴 때 정의를 (태그, node, value) 로 기록한다
    public sealed class Rig : DvDefinitionWidget
    {
        public override IReadOnlyCollection<string> DefinitionTags { get; } = ["Rotate", "Move", "Light"];

        public List<(string Tag, string? Node, double Value)> Seen = new();
        public List<DvDefinition> Last = new();

        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
        {
            Seen = ctx.Definitions.Select(d => (d.Tag, d.Text("node"), d.Number("value", double.NaN))).ToList();
            Last = ctx.Definitions.ToList();
        }
    }

    // 정의를 읽지 않는 보통 위젯
    public sealed class Plain : DvWidget
    {
        public int Count = -1;
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) => Count = ctx.Definitions.Count;
    }

    private static ComponentRegistry Registry()
    {
        var r = new ComponentRegistry();
        r.RegisterWidget<Rig>("Rig");
        r.RegisterWidget<Plain>("Plain");
        return r;
    }

    private static DvSurface Surface(string body, object? vm = null, ComponentRegistry? registry = null) =>
        new($"<DvScreen><DvStack>{body}</DvStack></DvScreen>", vm ?? new Vm(), DvTheme.Light, registry ?? Registry());

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static List<LayoutBox> Boxes(DvSurface s, string tag) => Flatten(s.Root!).Where(b => b.Node.Tag == tag).ToList();

    private static void Render(DvSurface s)
    {
        using var bmp = new SKBitmap(600, 400);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, 600, 400);
    }

    private static Rig RigOf(DvSurface s, int index = 0) => (Rig)Boxes(s, "Rig")[index].Widget!;

    [Fact]
    public void Definitions_AreResolved_InMarkupOrder()
    {
        var s = Surface("""
            <Rig>
              <Rotate node="Turret" value="{Turret}"/>
              <Move node="LiftRod" value="{Lift}"/>
              <Rotate node="Door" value="45"/>
            </Rig>
            """);
        Render(s);
        Assert.Equal(new[] { ("Rotate", (string?)"Turret", 30.0), ("Move", "LiftRod", 120.0), ("Rotate", "Door", 45.0) }, RigOf(s).Seen);
    }

    [Fact]
    public void Definitions_AreNotBoxes()
    {
        var s = Surface("""<Rig style="align-self:start"><Rotate node="A" value="1"/><Move node="B" value="2"/></Rig>""");
        s.Layout(600, 400);
        var rig = Boxes(s, "Rig")[0];
        Assert.Empty(rig.Children);
        Assert.Equal(new SKSize(120, 80), rig.Rect.Size); // 크기는 위젯이 정한다(자식이 늘리지 않음)
        Assert.Empty(Boxes(s, "Rotate"));
    }

    [Fact]
    public void Definitions_ReadLikeWidgetAttributes()
    {
        var s = Surface("""<Rig><Light node="Lamp" on="{Lamp}" level="0.5" label="L{Turret:0.0}"/></Rig>""");
        Render(s);
        var d = RigOf(s).Last.Single();
        Assert.Equal("Light", d.Tag);
        Assert.True(d.Has("on"));
        Assert.False(d.Has("value"));
        Assert.True(d.Flag("on"));
        Assert.Equal(0.5, d.Number("level"));
        Assert.Equal(7, d.Number("missing", 7));
        Assert.Equal(true, d.Value("on"));
        Assert.Equal(new[] { "node", "on", "level", "label" }, d.Names);
    }

    [Fact]
    public void Definitions_FollowTheVm_OnRebuild()
    {
        var vm = new Vm();
        var s = Surface("""<Rig><Rotate node="Turret" value="{Turret}"/></Rig>""", vm);
        Render(s);
        var first = RigOf(s);
        Assert.Equal(30, first.Seen[0].Value);

        vm.Turret = 75;
        Render(s); // 다시 만들기 전에는 스냅숏 그대로
        Assert.Equal(30, RigOf(s).Seen[0].Value);

        s.Invalidate();
        Render(s);
        Assert.Same(first, RigOf(s)); // 인스턴스 유지
        Assert.Equal(75, RigOf(s).Seen[0].Value);
    }

    [Fact]
    public void Definitions_InsideRepeat_ReadTheItem()
    {
        var s = Surface("""<DvRepeat items="{Fans}"><Rig><Rotate node="{Item}" value="{Turret}"/></Rig></DvRepeat>""");
        Render(s);
        Assert.Equal("FanA", RigOf(s, 0).Seen[0].Node);
        Assert.Equal("FanB", RigOf(s, 1).Seen[0].Node);
        Assert.Equal(30, RigOf(s, 1).Seen[0].Value); // 항목 밖 VM 값도 읽힌다
    }

    [Fact]
    public void Definitions_InsideComponent_ReadParameters()
    {
        var r = Registry();
        r.Register("""
            <DvComponent name="Arm">
              <DvParam name="Angle"/>
              <Rig><Rotate node="Arm1" value="{Angle}"/></Rig>
            </DvComponent>
            """);
        var s = Surface("""<Arm Angle="{Lift}"/>""", registry: r);
        Render(s);
        Assert.Equal(("Rotate", (string?)"Arm1", 120.0), RigOf(s).Seen.Single());
    }

    [Fact]
    public void UnknownTag_IsSkipped_WithWarning_NestedChildrenWarn()
    {
        var cap = new Capture("Devinno.UI");
        Trace.Listeners.Add(cap);
        try
        {
            var s = Surface("""
                <Rig>
                  <Spin node="Fan" value="3"/>
                  <Rotate node="Turret" value="1"><Move node="X" value="2"/></Rotate>
                </Rig>
                """);
            Render(s);
            Assert.Equal(new[] { ("Rotate", (string?)"Turret", 1.0) }, RigOf(s).Seen);
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        Assert.Contains(cap.Lines, m => m.Contains("<Rig> does not read <Spin>") && m.Contains("Rotate, Move, Light"));
        Assert.Contains(cap.Lines, m => m.Contains("<Rotate> inside <Rig> is a definition"));
    }

    [Fact]
    public void PlainWidget_HasNoDefinitions_AndStillWarns()
    {
        var cap = new Capture("child elements");
        Trace.Listeners.Add(cap);
        try
        {
            var s = Surface("""<Plain><Rotate node="A" value="1"/></Plain>""");
            Render(s);
            Assert.Equal(0, ((Plain)Boxes(s, "Plain")[0].Widget!).Count);
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        Assert.Contains(cap.Lines, m => m.Contains("<Plain>") && m.Contains("DvDefinitionWidget"));
    }

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
}
