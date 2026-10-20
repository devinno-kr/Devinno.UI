using Devinno.UI.Binding;
using Xunit;

public class ParamScopeBindingTests
{
    private sealed class Motor
    {
        public double Rpm { get; set; } = 1500;
    }

    [Fact]
    public void Resolves_first_segment_from_dictionary_scope()
    {
        var scope = new Dictionary<string, object?>
        {
            ["Title"] = "펌프 A",
            ["Motor"] = new Motor()
        };

        Assert.Equal("펌프 A", BindingResolver.Resolve(scope, "{Title}")?.ToString());
        Assert.Equal(1500d, BindingResolver.Resolve(scope, "{Motor.Rpm}"));
    }
}
