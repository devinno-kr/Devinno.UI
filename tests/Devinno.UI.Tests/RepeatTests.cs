using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using SkiaSharp;
using Xunit;

// DvRepeat — 항목 범위({Item}/{Index}/VM 폴백), 빈·null collapse, 항목 전달 클릭, 양방향 커밋, 컴포넌트·중첩
public class RepeatTests
{
    public sealed class Pump
    {
        public string Name { get; set; } = "";
        public bool Running { get; set; }
        public double Speed { get; set; } = 40;
        public List<string> Tags { get; set; } = new();
    }

    public sealed class Vm
    {
        public List<Pump>? Pumps { get; set; } = new()
        {
            new() { Name = "P1", Tags = { "a", "b" } },
            new() { Name = "P2", Running = true },
            new() { Name = "P3" }
        };
        public string Unit => "rpm";
        public Pump? Started;
        public int PlainClicks;
        public void Start(Pump p) => Started = p;
        public void Refresh() => PlainClicks++;
    }

    private static LayoutBox Lay(string markup, object vm, ComponentRegistry? registry = null)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), vm, 300, 600, registry);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static List<string> Texts(LayoutBox b) => Flatten(b).Select(x => x.ResolvedText).OfType<string>().ToList();

    [Fact]
    public void Item_index_and_vm_fallback_resolve_per_item()
    {
        var b = Lay("<DvScreen><DvRepeat items=\"{Pumps}\"><DvLabel text=\"{Index}:{Item.Name} {Unit}\"/></DvRepeat></DvScreen>", new Vm());
        Assert.Equal(new[] { "0:P1 rpm", "1:P2 rpm", "2:P3 rpm" }, Texts(b));
    }

    [Fact]
    public void Repeat_is_a_vertical_stack_by_default()
    {
        var b = Lay("<DvScreen style=\"padding:0\"><DvRepeat items=\"{Pumps}\" gap=\"none\"><DvBox style=\"height:30\"/></DvRepeat></DvScreen>", new Vm());
        var r = b.Children[0];
        Assert.Equal(3, r.Children.Count);
        Assert.Equal(new[] { 0f, 30f, 60f }, r.Children.Select(c => c.Rect.Top).ToArray());
    }

    [Fact]
    public void Empty_and_null_collections_collapse()
    {
        var vm = new Vm { Pumps = new() };
        var b = Lay("<DvScreen style=\"padding:0\"><DvRepeat items=\"{Pumps}\"><DvBox style=\"height:30\"/></DvRepeat></DvScreen>", vm);
        Assert.Empty(b.Children[0].Children);
        Assert.Equal(0, b.Children[0].Rect.Height, 3);

        vm.Pumps = null;
        b = Lay("<DvScreen style=\"padding:0\"><DvRepeat items=\"{Pumps}\"><DvBox style=\"height:30\"/></DvRepeat></DvScreen>", vm);
        Assert.Empty(b.Children[0].Children);
    }

    [Fact]
    public void Visible_can_use_item_properties()
    {
        var b = Lay("<DvScreen><DvRepeat items=\"{Pumps}\"><DvLabel text=\"{Item.Name}\" visible=\"{Item.Running}\"/></DvRepeat></DvScreen>", new Vm());
        Assert.Equal(new[] { "P2" }, Texts(b));
    }

    private static DvSurface Surface(string markup, object vm, ComponentRegistry? registry = null)
    {
        var s = new DvSurface(markup, vm, theme: null, registry);
        s.Layout(300, 600);
        return s;
    }

    private static void Click(DvSurface s, LayoutBox target)
    {
        var p = new SKPoint(target.Rect.MidX, target.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
    }

    private const string Rows =
        "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\"><DvRepeat items=\"{Pumps}\" gap=\"none\">" +
        "<DvBox style=\"height:30\" onclick=\"Start\"/></DvRepeat></DvScreen>";

    [Fact]
    public void Click_passes_the_item_when_the_vm_method_takes_one()
    {
        var vm = new Vm();
        var s = Surface(Rows, vm);
        Click(s, s.Root!.Children[0].Children[1]);
        Assert.Same(vm.Pumps![1], vm.Started);
    }

    [Fact]
    public void Click_falls_back_to_parameterless_method()
    {
        var vm = new Vm();
        var s = Surface(Rows.Replace("Start", "Refresh"), vm);
        Click(s, s.Root!.Children[0].Children[2]);
        Assert.Equal(1, vm.PlainClicks);
    }

    [Fact]
    public void Click_bubbles_from_inner_child_to_the_item_row()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvRepeat items=\"{Pumps}\" gap=\"none\">" +
                        "<DvBox style=\"height:30\" onclick=\"Start\"><DvLabel text=\"{Item.Name}\"/></DvBox></DvRepeat></DvScreen>", vm);
        Click(s, s.Root!.Children[0].Children[2].Children[0]);
        Assert.Same(vm.Pumps![2], vm.Started);
    }

    [Fact]
    public void Toggle_commits_to_the_item()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvRepeat items=\"{Pumps}\" gap=\"none\">" +
                        "<DvToggle value=\"{Bind Item.Running}\"/></DvRepeat></DvScreen>", vm);
        Click(s, s.Root!.Children[0].Children[0]);
        Assert.True(vm.Pumps![0].Running);
        Assert.True(vm.Pumps[1].Running); // 다른 항목은 그대로
        Assert.False(vm.Pumps[2].Running);
    }

    [Fact]
    public void Knob_commits_to_the_item()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvRepeat items=\"{Pumps}\" gap=\"none\">" +
                        "<DvKnob value=\"{Bind Item.Speed}\" style=\"width:100; height:100\"/></DvRepeat></DvScreen>", vm);
        var knob = s.Root!.Children[0].Children[1].Rect; // 두 번째 항목, 중심 (50, 150)
        s.PointerDown(new SKPoint(knob.MidX, knob.Top + 10));
        s.PointerMove(new SKPoint(knob.Right - 10, knob.MidY));
        s.PointerUp(new SKPoint(knob.Right - 10, knob.MidY));
        Assert.Equal(40, vm.Pumps![0].Speed);
        Assert.NotEqual(40, vm.Pumps[1].Speed);
    }

    [Fact]
    public void Nested_repeat_uses_the_inner_item()
    {
        var b = Lay("<DvScreen><DvRepeat items=\"{Pumps}\"><DvRepeat items=\"{Item.Tags}\"><DvLabel text=\"{Item}\"/></DvRepeat></DvRepeat></DvScreen>", new Vm());
        Assert.Equal(new[] { "a", "b" }, Texts(b));
    }

    [Fact]
    public void Repeat_inside_a_component_template_and_components_inside_a_repeat()
    {
        var reg = new ComponentRegistry();
        reg.Register("<DvComponent name=\"PumpLine\"><DvParam name=\"Title\"/><DvLabel text=\"[{Title}]\"/></DvComponent>");
        reg.Register("<DvComponent name=\"PumpList\"><DvParam name=\"Source\"/>" +
                     "<DvRepeat items=\"{Source}\"><DvLabel text=\"{Item.Name}\"/></DvRepeat></DvComponent>");

        var inner = Lay("<DvScreen><DvRepeat items=\"{Pumps}\"><PumpLine Title=\"{Item.Name}\"/></DvRepeat></DvScreen>", new Vm(), reg);
        Assert.Equal(new[] { "[P1]", "[P2]", "[P3]" }, Texts(inner));

        var outer = Lay("<DvScreen><PumpList Source=\"{Pumps}\"/></DvScreen>", new Vm(), reg);
        Assert.Equal(new[] { "P1", "P2", "P3" }, Texts(outer));
    }

    [Fact]
    public void Repeat_inside_a_scroll_box_scrolls_and_keeps_offset_after_data_change()
    {
        var vm = new Vm { Pumps = Enumerable.Range(0, 20).Select(i => new Pump { Name = $"P{i}" }).ToList() };
        var s = Surface("<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\">" +
                        "<DvRepeat items=\"{Pumps}\" gap=\"none\" style=\"height:100; overflow:scroll\">" +
                        "<DvBox style=\"height:30\" onclick=\"Start\"/></DvRepeat></DvScreen>", vm);
        s.PointerWheel(new SKPoint(20, 50), -1); // 48
        vm.Pumps.Add(new Pump { Name = "new" });
        s.Invalidate();
        s.Layout(300, 600);
        var list = s.Root!.Children[0];
        Assert.Equal(21, list.Children.Count);
        Assert.Equal(48, list.ScrollY, 3);

        // 스크롤된 상태에서 화면 y=10 → 내용 y=58 → 두 번째 항목
        s.PointerDown(new SKPoint(20, 10));
        s.PointerUp(new SKPoint(20, 10));
        Assert.Same(vm.Pumps[1], vm.Started);
    }
}
