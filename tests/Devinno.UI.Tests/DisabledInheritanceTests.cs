using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using SkiaSharp;
using Xunit;

// 상위 요소가 disabled 면 하위는 자기 disabled 값과 상관없이 비활성(HTML fieldset·WPF IsEnabled 와 같음)
public class DisabledInheritanceTests
{
    private sealed class Vm
    {
        public int Clicks;
        public bool On { get; set; }
        public bool Locked { get; set; } = true;
        public void Hit() => Clicks++;
    }

    private static LayoutBox Find(LayoutBox b, string tag) =>
        b.Node.Tag == tag ? b : b.Children.Select(c => Find(c, tag)).FirstOrDefault(x => x is not null)!;

    [Fact]
    public void Descendants_of_disabled_box_are_disabled_even_if_they_say_false()
    {
        var box = new LayoutEngine().Layout(MarkupParser.Parse(
            "<DvScreen><DvStack disabled=\"true\"><DvRow><DvButton onclick=\"Hit\" disabled=\"false\"/>" +
            "<DvToggle value=\"{Bind On}\"/></DvRow></DvStack><DvButton onclick=\"Hit\"/></DvScreen>"), new Vm(), 400, 300);

        var stack = box.Children[0];
        Assert.True(stack.Disabled);
        Assert.True(Find(stack, "DvButton").Disabled);
        Assert.True(Find(stack, "DvToggle").Disabled);
        Assert.False(box.Children[1].Disabled); // 형제는 영향 없음
    }

    [Fact]
    public void Bound_ancestor_disabled_is_inherited()
    {
        var box = new LayoutEngine().Layout(MarkupParser.Parse(
            "<DvScreen><DvStack disabled=\"{Locked}\"><DvButton onclick=\"Hit\"/></DvStack></DvScreen>"), new Vm(), 400, 300);
        Assert.True(Find(box, "DvButton").Disabled);
    }

    [Fact]
    public void Click_and_toggle_inside_disabled_container_do_nothing()
    {
        var vm = new Vm();
        var s = new DvSurface(
            "<DvScreen><DvStack disabled=\"true\" style=\"width:400; height:200\">" +
            "<DvButton onclick=\"Hit\" style=\"width:100; height:40\"/>" +
            "<DvToggle value=\"{Bind On}\"/></DvStack></DvScreen>", vm);
        s.Layout(400, 300);

        foreach (var tag in new[] { "DvButton", "DvToggle" })
        {
            var r = Find(s.Root!, tag).Rect;
            var p = new SKPoint(r.MidX, r.MidY);
            s.PointerDown(p);
            s.PointerUp(p);
            s.Layout(400, 300);
        }

        Assert.Equal(0, vm.Clicks);
        Assert.False(vm.On);
    }

    private sealed class ToggleVm
    {
        public bool ParentLocked { get; set; }
    }

    [Fact]
    public void Parent_enable_disable_enable_restores_each_childs_own_state()
    {
        var vm = new ToggleVm();
        var s = new DvSurface(
            "<DvScreen><DvStack disabled=\"{ParentLocked}\">" +
            "<DvButton text=\"A\" disabled=\"false\"/><DvButton text=\"B\" disabled=\"true\"/></DvStack></DvScreen>", vm);

        (bool a, bool b) States()
        {
            s.Invalidate();
            s.Layout(400, 300);
            var stack = s.Root!.Children[0];
            return (stack.Children[0].Disabled, stack.Children[1].Disabled);
        }

        Assert.Equal((false, true), States());   // 부모 enable
        vm.ParentLocked = true;
        Assert.Equal((true, true), States());    // 부모 disable → 둘 다 비활성
        vm.ParentLocked = false;
        Assert.Equal((false, true), States());   // 부모 다시 enable → 각자 원래 값
    }

    private sealed class VisibleVm
    {
        public bool ParentShown { get; set; } = true;
    }

    // visible 은 거짓이면 그 요소를 만들지 않으므로 자식도 함께 빠진다(자식 visible="true" 여도)
    [Fact]
    public void Parent_visible_false_hides_children_and_restores_on_true()
    {
        var vm = new VisibleVm();
        var s = new DvSurface(
            "<DvScreen><DvStack visible=\"{ParentShown}\">" +
            "<DvButton text=\"A\" visible=\"true\"/><DvButton text=\"B\" visible=\"false\"/></DvStack>" +
            "<DvSwitch label=\"x\" visible=\"{ParentShown}\"/></DvScreen>", vm);

        string[] Shown()
        {
            s.Invalidate();
            s.Layout(400, 300);
            var tags = new List<string>();
            void Walk(LayoutBox b)
            {
                if (b.Node.Tag == "DvButton") tags.Add(b.Node.Attributes["text"]);
                if (b.Node.Tag == "DvToggle") tags.Add("toggle");
                foreach (var c in b.Children) Walk(c);
            }
            Walk(s.Root!);
            return tags.ToArray();
        }

        Assert.Equal(new[] { "A", "toggle" }, Shown());
        vm.ParentShown = false;
        Assert.Empty(Shown());
        vm.ParentShown = true;
        Assert.Equal(new[] { "A", "toggle" }, Shown());
    }

    [Fact]
    public void Component_instance_disabled_disables_inner_widgets()
    {
        var vm = new Vm();
        var s = new DvSurface("<DvScreen><DvSwitch label=\"자동\" value=\"{Bind On}\" disabled=\"true\"/></DvScreen>", vm);
        s.Layout(400, 300);

        var toggle = Find(s.Root!, "DvToggle");
        Assert.True(toggle.Disabled);
        var p = new SKPoint(toggle.Rect.MidX, toggle.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        Assert.False(vm.On);
    }
}
