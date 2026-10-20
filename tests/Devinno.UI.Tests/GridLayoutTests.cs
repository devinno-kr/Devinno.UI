using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class GridLayoutTests
{
    [Fact]
    public void Two_fr_columns_split_width_with_gap()
    {
        var root = MarkupParser.Parse(
            "<DvScreen style=\"display:grid; grid-template-columns:1fr 1fr; gap:10\">" +
            "<DvBox style=\"height:20\"/><DvBox style=\"height:20\"/>" +
            "<DvBox style=\"height:20\"/><DvBox style=\"height:20\"/>" +
            "</DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 210, 200);

        var c0 = box.Children[0];
        var c1 = box.Children[1];
        var c2 = box.Children[2];

        Assert.Equal(0, c0.Rect.Left, 3);
        Assert.Equal(100, c0.Rect.Width, 3);
        Assert.Equal(110, c1.Rect.Left, 3);
        Assert.Equal(0, c2.Rect.Left, 3);
        Assert.Equal(30, c2.Rect.Top, 3);
    }
}
