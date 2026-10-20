using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class LayoutEngineTests
{
    [Fact]
    public void Column_flex_stacks_children_with_gap_and_padding()
    {
        var root = MarkupParser.Parse(
            "<DvScreen style=\"padding:10; display:flex; flex-direction:column; gap:8\">" +
            "<DvBox style=\"width:100; height:20\"/>" +
            "<DvBox style=\"width:100; height:30\"/>" +
            "</DvScreen>");

        var box = new LayoutEngine().Layout(root, vm: null, width: 200, height: 200);

        Assert.Equal(0, box.Rect.Left);
        Assert.Equal(200, box.Rect.Width, 3);

        var c0 = box.Children[0];
        Assert.Equal(10, c0.Rect.Left, 3);
        Assert.Equal(10, c0.Rect.Top, 3);
        Assert.Equal(20, c0.Rect.Height, 3);

        var c1 = box.Children[1];
        Assert.Equal(38, c1.Rect.Top, 3);
        Assert.Equal(30, c1.Rect.Height, 3);
    }
}
