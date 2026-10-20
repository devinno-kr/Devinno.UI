using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class LayoutBoxBuildTests
{
    [Fact]
    public void Build_populates_state_styles_disabled_and_onclick()
    {
        var root = MarkupParser.Parse(
            "<DvScreen><DvButton text=\"확인\" style=\"background:#3A7BD5\" " +
            "hover-style=\"background:#2f6cc0\" disabled=\"true\" onclick=\"OnConfirm\"/></DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 200, 100);
        var btn = box.Children[0];

        Assert.NotNull(btn.StyleHover);
        Assert.Equal((byte)0x2f, btn.StyleHover!.Background!.Value.R);
        Assert.True(btn.Disabled);
        Assert.Equal("OnConfirm", btn.OnClick);
    }
}
