using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class TwoWayBinderTests
{
    private sealed class Vm
    {
        public bool Flag { get; set; }
        public bool Allow = true;
        public bool CanToggle(bool proposed) => Allow;
    }

    private static LayoutBox Toggle(Vm vm, string extra = "")
    {
        var root = MarkupParser.Parse($"<DvScreen><DvToggle value=\"{{Bind Data.Flag}}\" {extra}/></DvScreen>");
        return new LayoutEngine().Layout(root, vm, 100, 100).Children[0];
    }

    [Fact]
    public void Click_flips_bound_bool()
    {
        var vm = new Vm { Flag = false };

        TwoWayBinder.Commit(Toggle(vm), vm);

        Assert.True(vm.Flag);
    }

    [Fact]
    public void Commit_gate_false_blocks_writeback()
    {
        var vm = new Vm { Flag = false, Allow = false };

        TwoWayBinder.Commit(Toggle(vm, "onbeforecommit=\"CanToggle\""), vm);

        Assert.False(vm.Flag);
    }

    [Fact]
    public void Disabled_toggle_does_not_commit()
    {
        var vm = new Vm { Flag = false };

        TwoWayBinder.Commit(Toggle(vm, "disabled=\"true\""), vm);

        Assert.False(vm.Flag);
    }
}
