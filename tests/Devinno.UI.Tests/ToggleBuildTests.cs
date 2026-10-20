using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class ToggleBuildTests
{
    private sealed class Vm
    {
        public bool Flag { get; set; } = true;
    }

    [Fact]
    public void Toggle_resolves_value_path_and_commit_gate()
    {
        var root = MarkupParser.Parse(
            "<DvScreen><DvToggle value=\"{Bind Data.Flag}\" onbeforecommit=\"CanToggle\"/></DvScreen>");
        var box = new LayoutEngine().Layout(root, new Vm(), 200, 100);
        var tg = box.Children[0];

        Assert.Equal("{Bind Data.Flag}", tg.ValueBindExpr);
        Assert.True(tg.ToggleOn);
        Assert.Equal("CanToggle", tg.OnBeforeCommit);
        Assert.True(tg.Rect.Width >= 40 && tg.Rect.Height >= 20);
    }
}
