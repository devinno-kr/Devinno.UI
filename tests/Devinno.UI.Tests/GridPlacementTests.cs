using System.Diagnostics;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// grid 칸 지정 — style grid-row·grid-column(CSS: 3, 2 / span 2, 2 / 4, span 2) 과 속성 grid-row·grid-col·row-span·col-span.
// 1부터 센다. 지정 안 한 자식은 순서대로 놓이고 찬 칸은 건너뛴다. 템플릿의 행·열은 비어 있어도 남고, 범위를 넘으면 트랙이 늘어난다(auto).
// 겹치게 지정하면 겹쳐 놓인다. 속성은 가장 낮은 우선순위 — 같은 축의 style grid-row/grid-column 이 있으면 그 축의 두 속성을 통째로 덮는다.
public class GridPlacementTests
{
    // 300×500, gap 0, 3열 5행 → 칸 = 100×100
    private static LayoutBox Grid(string children, string attrs = "gap=\"0\" columns=\"3\" rows=\"1fr 1fr 1fr 1fr 1fr\"",
        float w = 300, float h = 500, ComponentRegistry? reg = null)
    {
        var root = new LayoutEngine().Layout(
            MarkupParser.Parse($"<DvScreen style=\"padding:0\"><DvGrid {attrs} style=\"height:{h}\">{children}</DvGrid></DvScreen>"),
            null, w, h, reg);
        return root.Children[0];
    }

    private static (float x, float y, float w, float h) R(LayoutBox b) => (b.Rect.Left, b.Rect.Top, b.Rect.Width, b.Rect.Height);

    [Fact]
    public void Attribute_places_a_single_child_at_column_3_row_5()
    {
        var g = Grid("<DvBox grid-row=\"5\" grid-col=\"3\"/>");
        Assert.Equal((200f, 400f, 100f, 100f), R(g.Children[0]));
    }

    [Fact]
    public void Style_places_a_child_like_css()
    {
        var g = Grid("<DvBox style=\"grid-row:5; grid-column:3\"/>");
        Assert.Equal((200f, 400f, 100f, 100f), R(g.Children[0]));
    }

    [Fact]
    public void Empty_template_rows_keep_their_size()
    {
        // 자식 하나뿐이어도 5행 — 첫 칸 높이 = 500 / 5
        var g = Grid("<DvBox/>");
        Assert.Equal((0f, 0f, 100f, 100f), R(g.Children[0]));
    }

    [Fact]
    public void Empty_template_rows_keep_their_size_in_a_content_sized_grid()
    {
        var root = new LayoutEngine().Layout(
            MarkupParser.Parse("<DvScreen style=\"padding:0; align-items:start\"><DvGrid gap=\"0\" columns=\"100 100\" rows=\"40 40 40\"><DvBox/></DvGrid></DvScreen>"),
            null, 400, 400);
        Assert.Equal(120, root.Children[0].Rect.Height, 3);
    }

    [Theory]
    [InlineData("grid-column:1 / span 2")]
    [InlineData("grid-column:1 / 3")]
    [InlineData("grid-column:3 / 1")] // 끝이 앞이면 CSS 처럼 바꿔 읽는다
    public void Column_span_covers_two_columns(string style)
    {
        var g = Grid($"<DvBox style=\"{style}\"/>");
        Assert.Equal((0f, 0f, 200f, 100f), R(g.Children[0]));
    }

    [Fact]
    public void Span_attributes_cover_rows_and_columns()
    {
        var g = Grid("<DvBox grid-row=\"2\" grid-col=\"2\" row-span=\"3\" col-span=\"2\"/>");
        Assert.Equal((100f, 100f, 200f, 300f), R(g.Children[0]));
    }

    [Fact]
    public void Span_without_start_is_auto_placed()
    {
        var g = Grid("<DvBox/><DvBox col-span=\"3\"/><DvBox/>");
        Assert.Equal((0f, 100f, 300f, 100f), R(g.Children[1])); // 1행에 2칸이 안 남아 2행으로
        Assert.Equal((0f, 200f, 100f, 100f), R(g.Children[2]));
    }

    [Fact]
    public void Auto_children_skip_cells_taken_by_placed_children()
    {
        // 문서 순서: 자동 A, 지정 B(1행 1~2열), 자동 C → B 먼저 자리를 잡고 A = 1행 3열, C = 2행 1열
        var g = Grid("<DvBox/><DvBox grid-row=\"1\" grid-col=\"1\" col-span=\"2\"/><DvBox/>");
        Assert.Equal((200f, 0f, 100f, 100f), R(g.Children[0]));
        Assert.Equal((0f, 0f, 200f, 100f), R(g.Children[1]));
        Assert.Equal((0f, 100f, 100f, 100f), R(g.Children[2]));
    }

    [Fact]
    public void Row_only_child_takes_the_first_free_column_of_that_row()
    {
        var g = Grid("<DvBox grid-row=\"3\" grid-col=\"1\"/><DvBox grid-row=\"3\"/>");
        Assert.Equal((100f, 200f, 100f, 100f), R(g.Children[1]));
    }

    [Fact]
    public void Column_only_child_goes_down_to_a_free_row()
    {
        var g = Grid("<DvBox/><DvBox/><DvBox grid-col=\"1\"/>");
        Assert.Equal((0f, 100f, 100f, 100f), R(g.Children[2])); // 커서(1행 2열) 뒤 1열 = 다음 행
    }

    [Fact]
    public void Placing_beyond_the_template_adds_auto_rows()
    {
        var g = Grid("<DvBox grid-row=\"4\" style=\"height:30\"/>", "gap=\"0\" columns=\"3\" rows=\"50 50\"");
        var b = g.Children[0];
        Assert.Equal(100, b.Rect.Top, 3); // 3행(빈 auto) = 0
        Assert.Equal(30, b.Rect.Height, 3);
    }

    [Fact]
    public void Placing_beyond_the_template_adds_auto_columns()
    {
        var g = Grid("<DvBox grid-col=\"4\" style=\"width:30\"/>", "gap=\"0\" columns=\"100 100 100\" rows=\"50\"");
        var b = g.Children[0];
        Assert.Equal(300, b.Rect.Left, 3);
        Assert.Equal(30, b.Rect.Width, 3);
    }

    [Fact]
    public void Children_placed_on_the_same_cell_overlap()
    {
        var g = Grid("<DvBox grid-row=\"2\" grid-col=\"2\"/><DvBox grid-row=\"2\" grid-col=\"2\"/>");
        Assert.Equal(R(g.Children[0]), R(g.Children[1]));
        Assert.Equal((100f, 100f, 100f, 100f), R(g.Children[1]));
    }

    [Fact]
    public void Style_overrides_both_attributes_of_its_axis()
    {
        // style grid-row:4 → 행 속성 둘(grid-row=2, row-span=3)은 쓰지 않는다. 열 속성은 그대로
        var g = Grid("<DvBox grid-row=\"2\" row-span=\"3\" grid-col=\"2\" style=\"grid-row:4\"/>");
        Assert.Equal((100f, 300f, 100f, 100f), R(g.Children[0]));
    }

    [Fact]
    public void Class_overrides_attributes_too()
    {
        var g = Grid("<DvStyle>.cell { grid-column: 3 }</DvStyle><DvBox class=\"cell\" grid-col=\"1\"/>");
        Assert.Equal(200, g.Children[0].Rect.Left, 3);
    }

    [Fact]
    public void Bound_attributes_place_like_literals()
    {
        var root = new LayoutEngine().Layout(
            MarkupParser.Parse("<DvScreen style=\"padding:0\"><DvGrid gap=\"0\" columns=\"3\" rows=\"1fr 1fr\" style=\"height:200\"><DvBox grid-row=\"{R}\" grid-col=\"{C}\"/></DvGrid></DvScreen>"),
            new { R = 2, C = 3 }, 300, 200);
        Assert.Equal((200f, 100f, 100f, 100f), R(root.Children[0].Children[0]));
    }

    [Fact]
    public void Gap_is_inside_a_spanned_area()
    {
        var g = Grid("<DvBox col-span=\"2\"/>", "columns=\"3\" rows=\"100\" gap=\"10\"", w: 320);
        Assert.Equal(210, g.Children[0].Rect.Width, 3);
    }

    [Fact]
    public void Content_sized_grid_grows_auto_columns_for_a_spanning_child()
    {
        var root = new LayoutEngine().Layout(
            MarkupParser.Parse("<DvScreen style=\"padding:0; align-items:start\"><DvGrid gap=\"0\" columns=\"auto auto\">" +
                               "<DvBox style=\"width:50; height:10\"/><DvBox style=\"width:50; height:10\"/>" +
                               "<DvBox col-span=\"2\" style=\"width:300; height:10\"/></DvGrid></DvScreen>"),
            null, 800, 400);
        var g = root.Children[0];
        Assert.Equal(300, g.Rect.Width, 3);
        Assert.Equal(150, g.Children[1].Rect.Left, 3); // 모자란 200 을 두 auto 열이 나눔
    }

    [Fact]
    public void Auto_rows_grow_for_a_row_spanning_child()
    {
        var root = new LayoutEngine().Layout(
            MarkupParser.Parse("<DvScreen style=\"padding:0; align-items:start\"><DvGrid gap=\"0\" columns=\"100 100\">" +
                               "<DvBox row-span=\"2\" style=\"height:100\"/><DvBox style=\"height:20\"/><DvBox style=\"height:20\"/></DvGrid></DvScreen>"),
            null, 800, 400);
        var g = root.Children[0];
        Assert.Equal(100, g.Rect.Height, 3);
        Assert.Equal(50, g.Children[2].Rect.Top - g.Rect.Top, 3); // 두 auto 행 = 20 → 모자란 60 을 나눠 50·50
    }

    [Fact]
    public void Component_instance_passes_placement_to_its_root()
    {
        var reg = new ComponentRegistry();
        reg.Register("<DvComponent name=\"Tile\"><DvBox/></DvComponent>");
        var g = Grid("<Tile grid-row=\"5\" grid-col=\"3\"/>", reg: reg);
        Assert.Equal((200f, 400f, 100f, 100f), R(g.Children[0]));
    }

    [Theory]
    [InlineData("grid-row:-1")]
    [InlineData("grid-row:0")]
    [InlineData("grid-row:a")]
    [InlineData("grid-row:span 0")]
    [InlineData("grid-row:1 / span x")]
    [InlineData("grid-row:2000")]
    public void Invalid_value_is_ignored_with_a_warning(string style)
    {
        var cap = new Capture("grid-row");
        Trace.Listeners.Add(cap);
        LayoutBox g;
        try
        {
            g = Grid($"<DvBox/><DvBox style=\"{style}\"/>");
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        Assert.Equal((100f, 0f, 100f, 100f), R(g.Children[1])); // 자동 배치
        Assert.NotEmpty(cap.Lines);
    }

    [Fact]
    public void Auto_value_is_auto_placement()
    {
        var g = Grid("<DvBox/><DvBox style=\"grid-row:auto; grid-column:auto / span 2\"/>");
        Assert.Equal((100f, 0f, 200f, 100f), R(g.Children[1]));
    }

    [Fact]
    public void Absolute_children_take_no_cell()
    {
        var g = Grid("<DvBox style=\"position:absolute; left:0; top:0; width:10; height:10\"/><DvBox/>");
        Assert.Equal((0f, 0f, 100f, 100f), R(g.Children[1]));
    }

    [Fact]
    public void Grid_box_reports_every_track_relative_to_its_rect()
    {
        var root = new LayoutEngine().Layout(
            MarkupParser.Parse("<DvScreen style=\"padding:0\"><DvGrid gap=\"10\" columns=\"100 1fr\" rows=\"40 1fr 1fr\" style=\"padding:5; height:200\"><DvBox/></DvGrid></DvScreen>"),
            null, 320, 200);
        var t = root.Children[0].GridTracks;
        Assert.NotNull(t);
        // 열: 5 + 100, 5 + 110 부터 나머지(320 - 10 - 100 - 10 = 200). 행: 40, 그다음 (190 - 40 - 20) / 2 = 65 씩 — 빈 행도 있다
        Assert.Equal(new[] { new GridTrack(5, 100), new GridTrack(115, 200) }, t!.Columns);
        Assert.Equal(new[] { new GridTrack(5, 40), new GridTrack(55, 65), new GridTrack(130, 65) }, t.Rows);
    }

    [Fact]
    public void Grid_child_reports_its_area_from_1()
    {
        var g = Grid("<DvBox/><DvBox grid-row=\"5\" grid-col=\"2\" col-span=\"2\"/><DvBox style=\"position:absolute\"/>");
        Assert.Equal(new GridArea(1, 1, 1, 1), g.Children[0].GridArea);
        Assert.Equal(new GridArea(5, 2, 1, 2), g.Children[1].GridArea);
        Assert.Null(g.Children[2].GridArea);
        Assert.Null(g.GridArea); // DvScreen 은 grid 가 아님
    }

    [Fact]
    public void Non_grid_boxes_have_no_tracks()
    {
        var root = new LayoutEngine().Layout(MarkupParser.Parse("<DvScreen><DvStack><DvBox/></DvStack></DvScreen>"), null, 200, 200);
        Assert.Null(root.GridTracks);
        Assert.Null(root.Children[0].GridTracks);
        Assert.Null(root.Children[0].Children[0].GridArea);
    }

    [Fact]
    public void Tracks_stay_relative_when_a_tool_moves_the_grid()
    {
        var g = Grid("<DvBox/>");
        var before = g.GridTracks!.Columns[2];
        g.Rect = SkiaSharp.SKRect.Create(50, 50, g.Rect.Width, g.Rect.Height);
        Assert.Equal(before, g.GridTracks!.Columns[2]);
        Assert.Equal(new GridTrack(200, 100), before);
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
