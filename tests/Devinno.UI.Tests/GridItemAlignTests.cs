using System.Diagnostics;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// DvGrid align·justify = 칸 안에서 각 자식의 자리(CSS grid 의 align-items·justify-items). stretch(기본) = 칸을 채움,
// start·center·end = 자식의 측정 크기로 칸 안에 놓음. 자식의 align-self 는 세로 자리를 덮어쓴다. justify="between" = stretch + 경고.
public class GridItemAlignTests
{
    private static LayoutBox Grid(string attrs, string children, float w = 400, float h = 300)
    {
        var root = new LayoutEngine().Layout(
            MarkupParser.Parse($"<DvScreen style=\"padding:0\"><DvGrid columns=\"2\" gap=\"0\" {attrs}>{children}</DvGrid></DvScreen>"), null, w, h);
        return root.Children[0];
    }

    // 첫 칸 = 80×20 상자, 둘째 칸 = 높이 60 상자(행 높이 60), 열 폭 = 200
    private const string Pair = "<DvBox style=\"width:80; height:20\"/><DvBox style=\"height:60\"/>";

    [Fact]
    public void Default_children_fill_the_cell()
    {
        var g = Grid("", Pair);
        var a = g.Children[0].Rect;
        Assert.Equal(200, a.Width, 3);
        Assert.Equal(60, a.Height, 3);
    }

    [Fact]
    public void Align_center_keeps_the_child_height_and_centers_it_vertically()
    {
        var g = Grid("align=\"center\"", Pair);
        var a = g.Children[0].Rect;
        Assert.Equal(20, a.Height, 3);
        Assert.Equal(g.Rect.Top + 20, a.Top, 3);
        Assert.Equal(200, a.Width, 3); // justify 기본 stretch = 칸 폭
    }

    [Fact]
    public void Align_end_puts_the_child_at_the_bottom_of_the_cell()
    {
        var g = Grid("align=\"end\"", Pair);
        Assert.Equal(g.Rect.Top + 60, g.Children[0].Rect.Bottom, 3);
    }

    [Fact]
    public void Justify_center_keeps_the_child_width_and_centers_it_horizontally()
    {
        var g = Grid("justify=\"center\"", Pair);
        var a = g.Children[0].Rect;
        Assert.Equal(80, a.Width, 3);
        Assert.Equal(g.Rect.Left + 60, a.Left, 3);
        Assert.Equal(60, a.Height, 3); // align 기본 stretch = 행 높이
    }

    [Fact]
    public void Justify_end_and_start_place_the_child_at_the_cell_edges()
    {
        var end = Grid("justify=\"end\"", Pair);
        Assert.Equal(end.Rect.Left + 200, end.Children[0].Rect.Right, 3);
        var start = Grid("justify=\"start\"", Pair);
        Assert.Equal(start.Rect.Left, start.Children[0].Rect.Left, 3);
        Assert.Equal(80, start.Children[0].Rect.Width, 3);
    }

    [Fact]
    public void Justify_start_text_takes_its_text_width()
    {
        var g = Grid("justify=\"start\"", "<DvLabel text=\"Hi\"/><DvBox style=\"height:20\"/>");
        Assert.True(g.Children[0].Rect.Width < 100, $"label width {g.Children[0].Rect.Width}");
    }

    [Fact]
    public void Align_self_on_a_child_overrides_the_grid_align()
    {
        var g = Grid("", "<DvBox style=\"height:20; align-self:center\"/><DvBox style=\"height:60\"/>");
        var a = g.Children[0].Rect;
        Assert.Equal(20, a.Height, 3);
        Assert.Equal(g.Rect.Top + 20, a.Top, 3);

        var s = Grid("align=\"center\"", "<DvBox style=\"height:20; align-self:stretch\"/><DvBox style=\"height:60\"/>");
        Assert.Equal(60, s.Children[0].Rect.Height, 3);
    }

    [Fact]
    public void Justify_between_is_stretch_with_a_warning()
    {
        var cap = new Capture("DvGrid justify='between'");
        Trace.Listeners.Add(cap);
        LayoutBox g;
        try
        {
            g = Grid("justify=\"between\"", Pair);
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        Assert.Equal(200, g.Children[0].Rect.Width, 3);
        Assert.Single(cap.Lines);
    }

    private sealed class Capture(string marker) : TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && message.Contains(marker))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message)
            => WriteLine(message);
    }
}
