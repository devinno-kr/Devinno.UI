using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 3b-1 헤더 그룹 — 단계·깊이·빈 그룹·고정 경계 분할·헤더 높이
public class GridGroupTests
{
    private static DvSurface Surface(string cols, string attrs = "items=\"{Rows}\"", object? vm = null)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen(attrs, cols), vm ?? new GridLayoutBuildTests.Vm());
        s.Layout(300, 300);
        return s;
    }

    private static LayoutBox Grid(DvSurface s) => s.Root!.Children[0];

    private const string Nested =
        "<DvColumn header=\"ID\" field=\"Id\" width=\"60\"/>" +
        "<DvColumnGroup header=\"G\">" +
        "  <DvColumn header=\"이름\" field=\"Name\"/>" +
        "  <DvColumnGroup header=\"H\"><DvColumn header=\"ID2\" field=\"Id\" width=\"60\"/></DvColumnGroup>" +
        "</DvColumnGroup>";

    [Fact]
    public void Nested_groups_give_levels_depths_and_spans()
    {
        var g = Grid(Surface(Nested)).Grid!;
        Assert.Equal(3, g.Header.Levels);
        Assert.Equal(new[] { 0, 1, 2 }, g.Header.Depth);
        Assert.Equal(new[] { "ID", "이름", "ID2" }, g.Columns.Select(c => c.Header).ToArray());
        Assert.Equal(2, g.Header.Groups.Count);
        Assert.Contains(g.Header.Groups, x => x.Header == "G" && x.Level == 0 && x.First == 1 && x.Last == 2);
        Assert.Contains(g.Header.Groups, x => x.Header == "H" && x.Level == 1 && x.First == 2 && x.Last == 2);
        Assert.Equal(120, g.HeaderTotal);
    }

    [Fact]
    public void Empty_group_is_ignored()
    {
        var g = Grid(Surface("<DvColumn field=\"Id\"/><DvColumnGroup header=\"E\"/>")).Grid!;
        Assert.Equal(1, g.Header.Levels);
        Assert.Empty(g.Header.Groups);
    }

    [Fact]
    public void Group_across_the_frozen_boundary_is_split()
    {
        var g = Grid(Surface(
            "<DvColumnGroup header=\"G\"><DvColumn field=\"Id\" frozen=\"true\"/><DvColumn field=\"Name\"/></DvColumnGroup>")).Grid!;
        Assert.Equal(1, g.FrozenCount);
        Assert.Equal(2, g.Header.Groups.Count);
        Assert.Contains(g.Header.Groups, x => x.First == 0 && x.Last == 0);
        Assert.Contains(g.Header.Groups, x => x.First == 1 && x.Last == 1);
    }

    [Fact]
    public void Header_height_scales_with_levels()
    {
        var b = Grid(Surface("<DvColumnGroup header=\"G\"><DvColumn field=\"Id\"/><DvColumn field=\"Name\"/></DvColumnGroup>"));
        Assert.Equal(80, b.ScrollInsetTop);
        Assert.Equal(80 + 4800, b.ContentHeight, 3);
        Assert.Equal(240 - 80, b.Grid!.BodyHeight, 3);
    }

    [Fact]
    public void First_row_tap_is_below_the_multi_level_header()
    {
        var vm = new GridLayoutBuildTests.Vm();
        var s = Surface("<DvColumnGroup header=\"G\"><DvColumn field=\"Id\"/><DvColumn field=\"Name\"/></DvColumnGroup>",
            "items=\"{Rows}\" selection-mode=\"single\" selection=\"{Bind Picked}\"", vm);
        var p = new SKPoint(150, 80 + 24);
        s.PointerDown(p);
        s.PointerUp(p);
        Assert.Equal(new[] { vm.Rows[0] }, vm.Picked);

        var h = new SKPoint(150, 60); // 헤더 2단째 — 행 아님
        s.Layout(300, 300);
        s.PointerDown(h);
        s.PointerUp(h);
        Assert.Equal(new[] { vm.Rows[0] }, vm.Picked);
    }

    [Fact]
    public void Default_height_counts_header_levels()
    {
        var b = new LayoutEngine().Layout(Devinno.UI.Markup.MarkupParser.Parse(
            "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:flex-start\">" +
            "<DvDataGrid><DvColumnGroup header=\"G\"><DvColumn field=\"A\"/></DvColumnGroup></DvDataGrid></DvScreen>"),
            null, 1000, 1000).Children[0];
        Assert.Equal(80 + 48 * 5, b.Rect.Height, 3);
    }
}
