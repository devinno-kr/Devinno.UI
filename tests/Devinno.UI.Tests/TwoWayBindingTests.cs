using Devinno.UI.Binding;
using Xunit;

public class TwoWayBindingTests
{
    private sealed class Sub
    {
        public bool On { get; set; }
    }

    private sealed class Vm
    {
        public bool Flag { get; set; }
        public Sub Child { get; } = new();
    }

    [Fact]
    public void Detects_two_way()
    {
        Assert.True(BindingResolver.IsTwoWay("{Bind Data.Flag}"));
        Assert.False(BindingResolver.IsTwoWay("{Data.Flag}"));
        Assert.False(BindingResolver.IsTwoWay("hi"));
    }

    [Fact]
    public void ExtractPath_strips_bind_and_data()
    {
        Assert.Equal("Flag", BindingResolver.ExtractPath("{Bind Data.Flag}"));
        Assert.Equal("Child.On", BindingResolver.ExtractPath("{Data.Child.On}"));
    }

    [Fact]
    public void SetValue_writes_simple_and_nested()
    {
        var vm = new Vm();

        BindingResolver.SetValue(vm, "Flag", true);
        BindingResolver.SetValue(vm, "Child.On", true);

        Assert.True(vm.Flag);
        Assert.True(vm.Child.On);
    }
}
