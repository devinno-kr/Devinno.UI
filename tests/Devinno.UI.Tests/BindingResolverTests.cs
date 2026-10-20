using Devinno.UI.Binding;
using Xunit;

public class BindingResolverTests
{
    private sealed class Motor
    {
        public string Name { get; set; } = "M1";
        public double Rpm { get; set; } = 1500;
    }

    private sealed class Vm
    {
        public string Title { get; set; } = "Dashboard";
        public Motor M1 { get; } = new();
    }

    [Fact]
    public void Detects_binding_expression()
    {
        Assert.True(BindingResolver.IsBinding("{Data.Title}"));
        Assert.False(BindingResolver.IsBinding("hello"));
    }

    [Fact]
    public void Resolves_simple_and_nested_paths()
    {
        var vm = new Vm();

        Assert.Equal("Dashboard", BindingResolver.Resolve(vm, "{Data.Title}")?.ToString());
        Assert.Equal("M1", BindingResolver.Resolve(vm, "{Data.M1.Name}")?.ToString());
        Assert.Equal(1500d, BindingResolver.Resolve(vm, "{M1.Rpm}"));
    }
}
