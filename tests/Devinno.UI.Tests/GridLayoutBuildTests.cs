using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using SkiaSharp;
using Xunit;

// 3a-2 DvDataGrid 빌드·내용 크기·스크롤 상자 통합
public class GridLayoutBuildTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 100).Select(i => new Row { Id = i, Name = "R" + i }).ToList();
        public IEnumerable<Row> Seq => Rows.Where(r => r.Id < 10);
        public List<Row>? Nothing { get; set; }
        public List<Row> Picked { get; set; } = new();
    }

    private const string Cols = "<DvColumn header=\"ID\" field=\"Id\" width=\"80\"/><DvColumn header=\"이름\" field=\"Name\"/>";

    // 300×300 화면, 그리드 300×240 (헤더 40 + 몸통 200)
    internal static string Screen(string attrs = "items=\"{Rows}\"", string cols = Cols)
        => "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\">" +
           $"<DvDataGrid {attrs} style=\"height:240\">{cols}</DvDataGrid></DvScreen>";

    private static (DvSurface s, Vm vm) Surface(string markup, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface(markup, vm);
        s.Layout(300, 300);
        return (s, vm);
    }

    private static LayoutBox GridBox(DvSurface s) => s.Root!.Children[0];

    [Fact]
    public void IList_items_are_referenced_not_copied()
    {
        var (s, vm) = Surface(Screen());
        Assert.Same(vm.Rows, GridBox(s).Grid!.Items);
    }

    [Fact]
    public void Non_list_enumerable_is_materialized()
    {
        var (s, _) = Surface(Screen("items=\"{Seq}\""));
        Assert.Equal(10, GridBox(s).Grid!.Items!.Count);
    }

    [Theory]
    [InlineData("items=\"{Nothing}\"")]
    [InlineData("items=\"abc\"")]
    [InlineData("")]
    public void Null_string_or_missing_items_have_no_rows(string attrs)
    {
        var (s, _) = Surface(Screen(attrs));
        var g = GridBox(s);
        Assert.Null(g.Grid!.Items);
        Assert.Equal(g.Rect.Height, g.ContentHeight, 3);
    }

    [Fact]
    public void Columns_are_not_layout_boxes()
    {
        var (s, _) = Surface(Screen());
        var g = GridBox(s);
        Assert.Empty(g.Children);
        Assert.Equal(new[] { "ID", "이름" }, g.Grid!.Columns.Select(c => c.Header).ToArray());
    }

    [Fact]
    public void Default_size()
    {
        var b = new LayoutEngine().Layout(MarkupParser.Parse(
            "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:flex-start\"><DvDataGrid/></DvScreen>"),
            null, 1000, 1000).Children[0];
        Assert.Equal(480, b.Rect.Width, 3);
        Assert.Equal(40 + 48 * 5, b.Rect.Height, 3);
    }

    [Fact]
    public void Row_and_header_height_attributes()
    {
        var (s, _) = Surface(Screen("items=\"{Rows}\" row-height=\"30\" header-height=\"50\""));
        var g = GridBox(s).Grid!;
        Assert.Equal(30, g.RowHeight);
        Assert.Equal(50, g.HeaderHeight);
        Assert.Equal(50 + 3000, GridBox(s).ContentHeight, 3);
    }

    [Fact]
    public void Content_extent_vertical_overflow_only()
    {
        var (s, _) = Surface(Screen());
        var b = GridBox(s);
        Assert.True(b.IsScroller);
        Assert.Equal(40 + 4800, b.ContentHeight, 3);
        Assert.Equal(4600, b.MaxScrollY, 3);
        Assert.Equal(0, b.MaxScrollX, 3);
        Assert.Equal(40, b.ScrollInsetTop);
        // 세로 스크롤바 자리를 비우고 열 배분: 300 − 12 = 288 → 이름 = 208
        Assert.Equal(208, b.Grid!.Columns[1].W, 3);
    }

    [Fact]
    public void Content_extent_horizontal_overflow()
    {
        var (s, _) = Surface(Screen(cols: "<DvColumn field=\"Id\" width=\"200\" frozen=\"true\"/><DvColumn field=\"Name\" width=\"200\"/>"));
        var b = GridBox(s);
        var g = b.Grid!;
        Assert.True(g.OverflowX);
        Assert.Equal(40 + 4800 + 12, b.ContentHeight, 3);
        Assert.Equal(412, b.ContentWidth, 3);
        Assert.Equal(112, b.MaxScrollX, 3);
        Assert.Equal(200, g.FrozenWidth, 3);
        Assert.Equal(240 - 40 - 12, g.BodyHeight, 3);
    }

    [Fact]
    public void Frozen_only_counts_leading_columns()
    {
        var (s, _) = Surface(Screen(cols:
            "<DvColumn field=\"Id\" frozen=\"true\"/><DvColumn field=\"Name\"/><DvColumn field=\"Id\" frozen=\"true\"/>"));
        Assert.Equal(1, GridBox(s).Grid!.FrozenCount);
    }

    [Fact]
    public void Wheel_scrolls_the_grid()
    {
        var (s, _) = Surface(Screen());
        s.PointerWheel(new SKPoint(150, 150), -1);
        Assert.Equal(48, GridBox(s).ScrollY, 3);
    }

    [Fact]
    public void Scroll_survives_rebuild()
    {
        var (s, _) = Surface(Screen());
        s.PointerWheel(new SKPoint(150, 150), -3);
        s.Invalidate();
        s.Layout(300, 300);
        Assert.Equal(144, GridBox(s).ScrollY, 3);
    }

    [Fact]
    public void Initial_selection_from_vm()
    {
        var vm = new Vm();
        vm.Picked.Add(vm.Rows[3]);
        var (s, _) = Surface(Screen("items=\"{Rows}\" selection-mode=\"multi\" selection=\"{Bind Picked}\""), vm);
        var g = GridBox(s).Grid!;
        Assert.Equal(GridSelectionMode.Multi, g.Mode);
        Assert.True(g.IsSelected(vm.Rows[3]));
        Assert.False(g.IsSelected(vm.Rows[4]));
    }

    [Fact]
    public void Numeric_column_aligns_right_before_first_paint()
    {
        var (s, _) = Surface(Screen());
        Assert.Equal(Devinno.UI.Styling.TextAlign.Right, GridBox(s).Grid!.Columns[0].EffectiveAlign);
    }
}
