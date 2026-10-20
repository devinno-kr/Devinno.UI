using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3b+ 색상 — 그리드별 색 속성, row-class·cell-class, 우선순위(cell > 선택 > row > hover > 줄무늬)
public class GridColorTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? RowClass { get; set; }
        public string? ValueClass { get; set; }
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 10).Select(i => new Row { Id = i, Name = "R" + i }).ToList();
        public List<Row> Picked { get; set; } = new();
    }

    private static readonly SKColor Red = new(0xff, 0, 0), Green = new(0, 0xff, 0), Blue = new(0, 0, 0xff);

    private const string Styles =
        "<DvStyle>.hot { background:#ff0000 } .a { background:#ff0000 } .b { background:#0000ff }" +
        " .over { background:#ff0000; color:#ffffff; font-weight:bold }</DvStyle>";

    // Id(80) + 이름(*) — 이름 칸 가운데 위쪽(글자 없는 자리) x=200
    private const string Cols = "<DvColumn field=\"Id\" width=\"80\"/><DvColumn field=\"Name\" cell-class=\"ValueClass\"/>" + Styles;

    private static DvSurface Surface(Vm vm, string attrs)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\" " + attrs, Cols), vm, DvTheme.Light);
        s.Layout(300, 300);
        return s;
    }

    private static SKBitmap Render(DvSurface s)
    {
        var bmp = new SKBitmap(300, 300);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 300, 300);
        return bmp;
    }

    private static SKColor Token(string key)
    {
        Assert.True(DvTheme.Light.TryGet(key, out var raw));
        Assert.True(DvColor.TryParse(raw, out var c));
        return c.ToSK();
    }

    // 행 i 의 이름 칸·Id 칸 위쪽(글자 없는 자리)
    private static (int X, int Y) NameAt(int row) => (200, 40 + 48 * row + 6);
    private static (int X, int Y) IdAt(int row) => (6, 40 + 48 * row + 6);
    private static SKColor Px(SKBitmap b, (int X, int Y) p) => b.GetPixel(p.X, p.Y);

    [Fact]
    public void Grid_color_attributes_override_the_theme()
    {
        using var bmp = Render(Surface(new Vm(), "header-color=\"#ff0000\" row-color=\"#00ff00\" row-alt-color=\"#0000ff\""));
        Assert.Equal(Red, bmp.GetPixel(3, 8));
        Assert.Equal(Green, Px(bmp, NameAt(0)));
        Assert.Equal(Blue, Px(bmp, NameAt(1)));
    }

    [Fact]
    public void Color_attributes_accept_tokens_and_ignore_bad_values()
    {
        using var a = Render(Surface(new Vm(), "row-color=\"token(tone-danger)\""));
        Assert.Equal(Token("tone-danger"), Px(a, NameAt(0)));
        using var b = Render(Surface(new Vm(), "row-color=\"nope\""));
        Assert.Equal(Token("grid-row"), Px(b, NameAt(0)));
        using var c = Render(Surface(new Vm(), "row-color=\"token(tone-dangr)\"")); // 없는 토큰 = 테마 색(자홍 아님)
        Assert.Equal(Token("grid-row"), Px(c, NameAt(0)));
    }

    [Fact]
    public void Class_declarations_are_case_insensitive()
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
            "<DvColumn field=\"Id\"/><DvStyle>.up { Background:#ff0000; COLOR:#00ff00 }</DvStyle>"), new Vm(), DvTheme.Light);
        s.Layout(300, 300);
        var st = s.Root!.Children[0].Grid!.StyleOf("up")!;
        Assert.Equal(Red, st.Background!.Value.ToSK());
        Assert.Equal(Green, st.Color!.Value.ToSK());
    }

    [Fact]
    public void Row_class_paints_the_row()
    {
        var vm = new Vm();
        vm.Rows[1].RowClass = "hot";
        using var bmp = Render(Surface(vm, "row-class=\"RowClass\""));
        Assert.Equal(Red, Px(bmp, NameAt(1)));
        Assert.Equal(Red, Px(bmp, IdAt(1)));
        Assert.Equal(Token("grid-row"), Px(bmp, NameAt(0)));
    }

    [Fact]
    public void Selection_beats_row_class_and_row_class_beats_hover()
    {
        var vm = new Vm();
        vm.Rows[1].RowClass = "hot";
        vm.Rows[2].RowClass = "hot";
        vm.Picked.Add(vm.Rows[1]);
        var s = Surface(vm, "row-class=\"RowClass\" selection-mode=\"single\" selection=\"{Bind Picked}\"");
        s.PointerMove(new SKPoint(150, 40 + 48 * 2 + 24)); // hover = 행 2
        using var bmp = Render(s);
        Assert.Equal(Token("grid-row-selected"), Px(bmp, NameAt(1)));
        Assert.Equal(Red, Px(bmp, NameAt(2)));
    }

    [Fact]
    public void Multiple_classes_merge_later_wins_and_unknown_classes_are_ignored()
    {
        var vm = new Vm();
        vm.Rows[0].RowClass = "a b";
        vm.Rows[1].RowClass = "nope";
        using var bmp = Render(Surface(vm, "row-class=\"RowClass\""));
        Assert.Equal(Blue, Px(bmp, NameAt(0)));
        Assert.Equal(Token("grid-row-alt"), Px(bmp, NameAt(1)));
    }

    [Fact]
    public void Repaint_picks_up_class_changes()
    {
        var vm = new Vm();
        var s = Surface(vm, "row-class=\"RowClass\"");
        using (var before = Render(s))
            Assert.Equal(Token("grid-row"), Px(before, NameAt(0)));
        vm.Rows[0].RowClass = "hot";
        s.Repaint();
        using var after = Render(s);
        Assert.Equal(Red, Px(after, NameAt(0)));
    }

    [Fact]
    public void Cell_class_paints_only_its_cell_and_beats_selection()
    {
        var vm = new Vm();
        vm.Rows[0].ValueClass = "over";
        vm.Picked.Add(vm.Rows[0]);
        using var bmp = Render(Surface(vm, "selection-mode=\"single\" selection=\"{Bind Picked}\""));
        Assert.Equal(Red, Px(bmp, NameAt(0)));                       // 셀 클래스 > 선택
        Assert.Equal(Token("grid-row-selected"), Px(bmp, IdAt(0)));  // 같은 행 다른 셀 = 선택색
    }

    [Fact]
    public void Cell_background_keeps_the_row_divider()
    {
        var vm = new Vm();
        vm.Rows[0].ValueClass = "over";
        using var bmp = Render(Surface(vm, ""));
        Assert.Equal(Token("grid-line"), bmp.GetPixel(200, 40 + 48 - 1));
    }

    [Fact]
    public void Class_style_reads_background_color_and_weight_only_when_given()
    {
        var s = Surface(new Vm(), "");
        var g = s.Root!.Children[0].Grid!;
        var over = g.StyleOf("over")!;
        Assert.Equal(Red, over.Background!.Value.ToSK());
        Assert.Equal(new SKColor(0xff, 0xff, 0xff), over.Color!.Value.ToSK());
        Assert.True(over.Bold);
        var hot = g.StyleOf("hot")!;
        Assert.Null(hot.Color);
        Assert.Null(hot.Bold);
        Assert.Null(g.StyleOf(null));
        Assert.Null(g.StyleOf("nope"));
    }

    [Fact]
    public void Cell_class_background_color_paints_the_cell()
    {
        const string cols = "<DvColumn field=\"Id\" width=\"80\"/><DvColumn field=\"Name\" cell-class=\"ValueClass\"/>" +
                            "<DvStyle>.bc { background-color:#ff0000 }</DvStyle>";
        var vm = new Vm();
        vm.Rows[0].ValueClass = "bc";
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", cols), vm, DvTheme.Light);
        s.Layout(300, 300);
        using var bmp = Render(s);
        Assert.Equal(Red, Px(bmp, NameAt(0)));
    }
}
