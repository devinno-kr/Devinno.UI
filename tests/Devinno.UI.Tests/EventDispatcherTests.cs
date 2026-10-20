using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class EventDispatcherTests
{
    private sealed class Vm
    {
        public int Clicks;
        public void OnConfirm() => Clicks++;
    }

    [Fact]
    public void Invokes_named_void_method_on_vm()
    {
        var vm = new Vm();
        var root = MarkupParser.Parse("<DvScreen><DvButton text=\"확인\" onclick=\"OnConfirm\"/></DvScreen>");
        var box = new LayoutEngine().Layout(root, vm, 100, 100);
        var btn = box.Children[0];

        EventDispatcher.Click(btn, vm);
        EventDispatcher.Click(btn, vm);

        Assert.Equal(2, vm.Clicks);
    }

    [Fact]
    public void Disabled_box_does_not_invoke()
    {
        var vm = new Vm();
        var root = MarkupParser.Parse("<DvScreen><DvButton onclick=\"OnConfirm\" disabled=\"true\"/></DvScreen>");
        var box = new LayoutEngine().Layout(root, vm, 100, 100);

        EventDispatcher.Click(box.Children[0], vm);

        Assert.Equal(0, vm.Clicks);
    }
}
