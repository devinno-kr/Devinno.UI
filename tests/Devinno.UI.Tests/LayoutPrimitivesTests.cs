using Devinno.UI.Styling;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class StyleParsePrimitivesTests
{
    [Fact]
    public void Parses_min_max_sizes()
    {
        var s = StyleParser.Parse("min-width:10; max-width:200; min-height:5; max-height:100");
        Assert.Equal(10f, s.MinWidth);
        Assert.Equal(200f, s.MaxWidth);
        Assert.Equal(5f, s.MinHeight);
        Assert.Equal(100f, s.MaxHeight);
    }

    [Fact]
    public void Parses_flex_grow_shrink()
    {
        var s = StyleParser.Parse("flex-grow:2; flex-shrink:3");
        Assert.Equal(2f, s.FlexGrow);
        Assert.Equal(3f, s.FlexShrink);
    }

    [Fact]
    public void Flex_shorthand_single_number_sets_grow_and_shrink_one()
    {
        var s = StyleParser.Parse("flex:1");
        Assert.Equal(1f, s.FlexGrow);
        Assert.Equal(1f, s.FlexShrink);
    }

    [Fact]
    public void Flex_shorthand_two_numbers()
    {
        var s = StyleParser.Parse("flex:2 0");
        Assert.Equal(2f, s.FlexGrow);
        Assert.Equal(0f, s.FlexShrink);
    }

    [Fact]
    public void Parses_wrap_and_align_content()
    {
        var s = StyleParser.Parse("flex-wrap:wrap; align-content:center");
        Assert.True(s.Wrap);
        Assert.Equal(AlignContent.Center, s.AlignContent);
    }

    [Fact]
    public void Parses_position_and_offsets()
    {
        var s = StyleParser.Parse("position:absolute; left:10; top:20; right:5; bottom:8");
        Assert.Equal(Position.Absolute, s.Position);
        Assert.Equal(10f, s.Left);
        Assert.Equal(20f, s.Top);
        Assert.Equal(5f, s.Right);
        Assert.Equal(8f, s.Bottom);
    }

    [Fact]
    public void Defaults_are_neutral()
    {
        var s = StyleParser.Parse("");
        Assert.Null(s.MinWidth);
        Assert.Null(s.MaxWidth);
        Assert.Equal(0f, s.FlexGrow);
        Assert.Equal(0f, s.FlexShrink);
        Assert.False(s.Wrap);
        Assert.Equal(AlignContent.Start, s.AlignContent);
        Assert.Equal(Position.Static, s.Position);
    }
}

public class MinMaxTests
{
    private static LayoutBox Layout(string markup, float w, float h)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), null, w, h);

    [Fact]
    public void Max_height_clamps_measured_child()
    {
        var root = Layout(
            "<DvBox style=\"display:flex; flex-direction:column\">" +
            "<DvBox style=\"max-height:100\"><DvBox style=\"height:300\"/></DvBox>" +
            "</DvBox>", 400, 400);

        var mid = root.Children[0];
        Assert.Equal(100f, mid.Rect.Height, 1);
    }

    [Fact]
    public void Grow_ThenMaxHeight_Clamped()
    {
        // grow 로 받을 자리 500 이어도 max-height 200 에서 멈춘다 — 남는 공간은 다른 형제에게 다시 나누지 않는다
        var root = Layout(
            "<DvBox style=\"display:flex; flex-direction:column\">" +
            "<DvBox style=\"flex-grow:1; max-height:200\"/><DvBox style=\"height:50\"/>" +
            "</DvBox>", 400, 550);

        Assert.Equal(200f, root.Children[0].Rect.Height, 1);
        Assert.Equal(50f, root.Children[1].Rect.Height, 1);
        Assert.Equal(200f, root.Children[1].Rect.Top, 1);
    }

    [Fact]
    public void Min_width_expands_small_child()
    {
        var root = Layout(
            "<DvBox style=\"display:flex; flex-direction:row\">" +
            "<DvBox style=\"min-width:80\"><DvLabel text=\"x\"/></DvBox>" +
            "</DvBox>", 400, 100);

        var child = root.Children[0];
        Assert.Equal(80f, child.Rect.Width, 1);
    }
}

public class FlexGrowShrinkTests
{
    private static LayoutBox Layout(string markup, float w, float h)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), null, w, h);

    [Fact]
    public void Grow_child_fills_remaining_main_space()
    {
        var root = Layout(
            "<DvBox style=\"display:flex; flex-direction:row; width:300; height:60\">" +
            "<DvBox style=\"width:100\"/>" +
            "<DvBox style=\"flex-grow:1\"/>" +
            "</DvBox>", 300, 60);

        Assert.Equal(100f, root.Children[0].Rect.Width, 1);
        Assert.Equal(200f, root.Children[1].Rect.Width, 1);
    }

    [Fact]
    public void Two_grow_children_split_space_by_ratio()
    {
        var root = Layout(
            "<DvBox style=\"display:flex; flex-direction:row; width:300; height:60\">" +
            "<DvBox style=\"flex-grow:1\"/>" +
            "<DvBox style=\"flex-grow:1\"/>" +
            "</DvBox>", 300, 60);

        Assert.Equal(150f, root.Children[0].Rect.Width, 1);
        Assert.Equal(150f, root.Children[1].Rect.Width, 1);
    }

    [Fact]
    public void Shrink_children_reduce_on_overflow()
    {
        var root = Layout(
            "<DvBox style=\"display:flex; flex-direction:row; width:100; height:60\">" +
            "<DvBox style=\"width:80; flex-shrink:1\"/>" +
            "<DvBox style=\"width:80; flex-shrink:1\"/>" +
            "</DvBox>", 100, 60);

        Assert.Equal(50f, root.Children[0].Rect.Width, 1);
        Assert.Equal(50f, root.Children[1].Rect.Width, 1);
    }
}

public class FlexWrapTests
{
    private static LayoutBox Layout(string markup, float w, float h)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), null, w, h);

    [Fact]
    public void Items_wrap_to_next_line_when_exceeding_main_axis()
    {
        var root = Layout(
            "<DvBox style=\"display:flex; flex-direction:row; flex-wrap:wrap; width:200; height:200\">" +
            "<DvBox style=\"width:90; height:30\"/>" +
            "<DvBox style=\"width:90; height:30\"/>" +
            "<DvBox style=\"width:90; height:30\"/>" +
            "</DvBox>", 200, 200);

        Assert.Equal(0f, root.Children[0].Rect.Top, 1);
        Assert.Equal(0f, root.Children[1].Rect.Top, 1);
        Assert.Equal(30f, root.Children[2].Rect.Top, 1);
        Assert.Equal(0f, root.Children[2].Rect.Left, 1);
    }

    [Fact]
    public void Align_content_center_offsets_lines_in_cross_axis()
    {
        var root = Layout(
            "<DvBox style=\"display:flex; flex-direction:row; flex-wrap:wrap; " +
            "align-content:center; width:100; height:200\">" +
            "<DvBox style=\"width:100; height:30\"/>" +
            "<DvBox style=\"width:100; height:30\"/>" +
            "</DvBox>", 100, 200);

        Assert.Equal(70f, root.Children[0].Rect.Top, 1);
        Assert.Equal(100f, root.Children[1].Rect.Top, 1);
    }
}

public class PositionAbsoluteTests
{
    private static LayoutBox Layout(string markup, float w, float h)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), null, w, h);

    [Fact]
    public void Absolute_child_positioned_by_top_left()
    {
        var root = Layout(
            "<DvBox style=\"position:relative; width:200; height:100\">" +
            "<DvBox style=\"position:absolute; top:10; left:20; width:30; height:30\"/>" +
            "</DvBox>", 200, 100);

        var a = root.Children[0].Rect;
        Assert.Equal(20f, a.Left, 1);
        Assert.Equal(10f, a.Top, 1);
        Assert.Equal(30f, a.Width, 1);
        Assert.Equal(30f, a.Height, 1);
    }

    [Fact]
    public void Absolute_child_positioned_by_right_bottom()
    {
        var root = Layout(
            "<DvBox style=\"position:relative; width:200; height:100\">" +
            "<DvBox style=\"position:absolute; right:10; bottom:5; width:40; height:20\"/>" +
            "</DvBox>", 200, 100);

        var a = root.Children[0].Rect;
        Assert.Equal(150f, a.Left, 1);
        Assert.Equal(75f, a.Top, 1);
    }

    [Fact]
    public void Absolute_child_excluded_from_flow_sizing()
    {
        var root = Layout(
            "<DvBox style=\"position:relative; display:flex; flex-direction:column; width:200; height:120\">" +
            "<DvBox style=\"position:absolute; top:0; right:0; width:20; height:20\"/>" +
            "<DvBox style=\"height:40\"/>" +
            "</DvBox>", 200, 120);

        Assert.Equal(0f, root.Children[1].Rect.Top, 1);
        Assert.Equal(40f, root.Children[1].Rect.Height, 1);
    }
}
