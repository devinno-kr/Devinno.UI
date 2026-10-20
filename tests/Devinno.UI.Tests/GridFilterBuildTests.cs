using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 3d-2 필터 — filter 속성 해석(auto 포함), 필터 행 유무·높이, 필터 칸 판정
public class GridFilterBuildTests
{
    public enum Mode { Auto, Manual }

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public double? Level { get; set; }
        public decimal Price { get; set; }
        public DateTime At { get; set; }
        public DateOnly Day { get; set; }
        public TimeOnly Clock { get; set; }
        public bool On { get; set; }
        public Mode Kind { get; set; }
        public object? Any { get; set; }
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; } = new() { new Row { Name = "a", Any = new object() } };
        public string F { get; set; } = "text";
    }

    private static DvSurface Surface(string cols, object? vm = null)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", cols), vm ?? new Vm());
        s.Layout(400, 300);
        return s;
    }

    private static LayoutBox Grid(DvSurface s) => s.Root!.Children[0];

    [Theory]
    [InlineData("Name", "text")]
    [InlineData("Count", "range")]
    [InlineData("Level", "range")]
    [InlineData("Price", "range")]
    [InlineData("At", "date")]
    [InlineData("Day", "date")]
    [InlineData("Clock", "date")]
    [InlineData("On", "values")]
    [InlineData("Kind", "values")]
    [InlineData("Any", "text")]
    public void Auto_resolves_from_value_type(string field, string expected)
    {
        var g = Grid(Surface($"<DvColumn field=\"{field}\" filter=\"auto\"/>")).Grid!;
        Assert.Equal(expected, g.Columns[0].FilterKind.ToString().ToLowerInvariant());
    }

    [Fact]
    public void Auto_on_select_column_is_values()
    {
        var g = Grid(Surface("<DvColumn field=\"Name\" cell-type=\"select\" filter=\"auto\"><DvOption value=\"a\" text=\"A\"/></DvColumn>")).Grid!;
        Assert.Equal(GridFilterKind.Values, g.Columns[0].FilterKind);
    }

    [Theory]
    [InlineData("none", "None")]
    [InlineData("auto", "Auto")]
    [InlineData("text", "Text")]
    [InlineData("values", "Values")]
    [InlineData("range", "Range")]
    [InlineData("date", "Date")]
    [InlineData(" Values ", "Values")]
    [InlineData("bogus", "None")]
    public void Filter_attribute_values(string raw, string expected)
    {
        var g = Grid(Surface($"<DvColumn field=\"Name\" filter=\"{raw}\"/>")).Grid!;
        Assert.Equal(expected, g.Columns[0].Filter.ToString()); // 내부 enum 은 공개 Theory 인자로 못 씀
    }

    [Fact]
    public void Filter_attribute_is_bindable()
    {
        var g = Grid(Surface("<DvColumn field=\"Name\" filter=\"{F}\"/>")).Grid!;
        Assert.Equal(GridFilterSetting.Text, g.Columns[0].Filter);
    }

    [Fact]
    public void No_filter_column_means_no_filter_row()
    {
        var g = Grid(Surface("<DvColumn field=\"Name\"/><DvColumn field=\"Count\" filter=\"none\"/>")).Grid!;
        Assert.False(g.HasFilterRow);
        Assert.Equal(40f, g.HeaderTotal);
    }

    [Fact]
    public void Any_filter_column_adds_a_filter_row_of_header_height_below_all_levels()
    {
        var s = Surface("<DvColumnGroup header=\"G\"><DvColumn field=\"Name\" filter=\"text\"/></DvColumnGroup><DvColumn field=\"Count\"/>");
        var g = Grid(s).Grid!;
        Assert.True(g.HasFilterRow);
        Assert.Equal(80f, g.HeaderBand);
        Assert.Equal(120f, g.HeaderTotal);
        Assert.Equal(120f, Grid(s).ScrollInsetTop); // 몸통이 필터 행 아래에서 시작
    }

    [Fact]
    public void Filter_cell_hit_only_on_filter_columns_and_header_tap_stays_in_header_band()
    {
        var s = Surface("<DvColumn field=\"Name\" width=\"100\" filter=\"text\" sortable=\"true\"/><DvColumn field=\"Count\" width=\"100\" sortable=\"true\"/>");
        var box = Grid(s);
        Assert.Equal(0, GridInteraction.FilterColumnAt(box, new SKPoint(50, 60)));   // 필터 행(40~80)의 Name
        Assert.Equal(-1, GridInteraction.FilterColumnAt(box, new SKPoint(150, 60))); // Count = 필터 없음
        Assert.Equal(-1, GridInteraction.FilterColumnAt(box, new SKPoint(50, 20)));  // 헤더 단
        Assert.Equal(-1, GridInteraction.HeaderColumnAt(box, new SKPoint(50, 60))); // 필터 행은 정렬 판정 아님
        Assert.Equal(0, GridInteraction.HeaderColumnAt(box, new SKPoint(50, 20)));
    }

    [Fact]
    public void Disabled_grid_has_no_filter_hit()
    {
        var s = new DvSurface("<DvScreen style=\"padding:0\"><DvDataGrid items=\"{Rows}\" disabled=\"true\" style=\"height:240\">" +
                              "<DvColumn field=\"Name\" width=\"100\" filter=\"text\"/></DvDataGrid></DvScreen>", new Vm());
        s.Layout(400, 300);
        Assert.Equal(-1, GridInteraction.FilterColumnAt(s.Root!.Children[0], new SKPoint(50, 60)));
    }

    [Fact]
    public void Filter_cell_rect_follows_horizontal_scroll_and_frozen_columns()
    {
        var s = Surface("<DvColumn field=\"Name\" width=\"100\" frozen=\"true\" filter=\"text\"/><DvColumn field=\"Count\" width=\"300\" filter=\"range\"/><DvColumn field=\"At\" width=\"300\"/>");
        var box = Grid(s);
        var g = box.Grid!;
        Assert.Equal(SKRect.Create(0, 40, 100, 40), GridGeometry.FilterCellRect(g, box.Rect, 50, 0)); // 고정 열 = 스크롤 무관
        Assert.Equal(SKRect.Create(50, 40, 300, 40), GridGeometry.FilterCellRect(g, box.Rect, 50, 1));
    }
}
