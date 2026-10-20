using Devinno.UI.Styling;
using Xunit;

public class StyleResolverTests
{
    [Fact]
    public void State_decls_override_base()
    {
        var eff = StyleResolver.Merge("background:#3A7BD5; color:#fff", "background:#2f6cc0");

        Assert.Equal((byte)0x2f, eff.Background!.Value.R);
        Assert.Equal((byte)0xff, eff.Color.R);
    }

    [Fact]
    public void Null_state_returns_base_equiv()
    {
        var eff = StyleResolver.Merge("color:#111", null);

        Assert.Equal((byte)0x11, eff.Color.R);
    }
}
