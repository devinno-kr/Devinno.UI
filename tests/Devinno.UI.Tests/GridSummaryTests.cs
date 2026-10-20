using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3b-3 요약 행 — fn 5종, 숫자 열 자동, DvSummary 우선·value, 빈 목록, null 건너뜀, Repaint 재계산·hover 캐시, 레이아웃
public class GridSummaryTests
{
    public sealed class Row
    {
        public string Name { get; set; } = "";
        public double Value { get; set; }
        public int Qty { get; set; }
        public double? Opt { get; set; }
    }

    public sealed class Vm
    {
        // Value = 0.5·i, Qty = i, Opt = i (짝수만, 홀수 null) — i = 1..10
        public List<Row> Rows { get; set; } = Enumerable.Range(1, 10).Select(i => new Row
        {
            Name = "N" + i, Value = i * 0.5, Qty = i, Opt = i % 2 == 0 ? i : null
        }).ToList();
        public double ServerTotal { get; set; } = 1234.5;
        public List<Row> Picked { get; set; } = new();
    }

    private const string Cols =
        "<DvColumn header=\"이름\" field=\"Name\" width=\"60\"/>" +
        "<DvColumn header=\"값\" field=\"Value\" width=\"60\" format=\"0.0\"/>" +
        "<DvColumn header=\"수량\" field=\"Qty\" width=\"60\"/>" +
        "<DvColumn header=\"옵션\" field=\"Opt\"/>";

    private static DvSurface Surface(Vm vm, string summary, string attrs = "items=\"{Rows}\"")
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen(attrs, Cols + summary), vm, DvTheme.Light);
        s.Layout(300, 300);
        return s;
    }

    private static LayoutBox Box(DvSurface s) => s.Root!.Children[0];
    private static GridSpec G(DvSurface s) => Box(s).Grid!;

    private static void Render(DvSurface s)
    {
        using var bmp = new SKBitmap(300, 300);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 300, 300);
    }

    private static string?[] Texts(DvSurface s, int row = 0)
    {
        Render(s);
        return G(s).Summaries[row].Texts;
    }

    [Fact]
    public void Row_fn_sums_every_numeric_column_and_title_takes_the_first()
    {
        var t = Texts(Surface(new Vm(), "<DvSummaryRow title=\"합계\" fn=\"sum\"/>"));
        Assert.Equal(new[] { "합계", "27.5", "55", "30" }, t);
    }

    [Theory]
    [InlineData("avg", "2.8", "5.5", "6")]
    [InlineData("min", "0.5", "1", "2")]
    [InlineData("max", "5.0", "10", "10")]
    [InlineData("count", "10", "10", "5")]
    public void Aggregates(string fn, string value, string qty, string opt)
    {
        var t = Texts(Surface(new Vm(), $"<DvSummaryRow title=\"T\" fn=\"{fn}\"/>"));
        Assert.Equal(new[] { "T", value, qty, opt }, t);
    }

    [Fact]
    public void Summary_cells_override_the_row_fn_and_take_app_values()
    {
        var t = Texts(Surface(new Vm(),
            "<DvSummaryRow title=\"평균\" fn=\"sum\">" +
            "<DvSummary field=\"Value\" fn=\"avg\" format=\"0.000\"/>" +
            "<DvSummary field=\"Opt\" value=\"{ServerTotal}\" format=\"#,0.0\"/>" +
            "</DvSummaryRow>"));
        Assert.Equal(new[] { "평균", "2.750", "55", "1,234.5" }, t);
    }

    [Fact]
    public void Count_works_for_non_numeric_columns_only_when_asked()
    {
        var t = Texts(Surface(new Vm(), "<DvSummaryRow fn=\"sum\"><DvSummary field=\"Name\" fn=\"count\"/></DvSummaryRow>"));
        Assert.Equal("", t[0]); // 첫 열은 제목 자리(제목 없음)
        var t2 = Texts(Surface(new Vm(),
            "<DvSummaryRow title=\"건수\"/><DvSummaryRow title=\"x\"><DvSummary field=\"Qty\" fn=\"count\"/></DvSummaryRow>"), 1);
        Assert.Equal("10", t2[2]);
    }

    [Fact]
    public void Empty_list_leaves_cells_blank_and_count_zero()
    {
        var vm = new Vm { Rows = new List<Row>() };
        var t = Texts(Surface(vm, "<DvSummaryRow title=\"합계\" fn=\"sum\"><DvSummary field=\"Qty\" fn=\"count\"/></DvSummaryRow>"));
        Assert.Equal("합계", t[0]);
        Assert.True(string.IsNullOrEmpty(t[1]));
        Assert.Equal("0", t[2]);
    }

    [Fact]
    public void Repaint_recomputes_but_hover_renders_use_the_cache()
    {
        var vm = new Vm();
        var s = Surface(vm, "<DvSummaryRow title=\"합계\" fn=\"sum\"/>", "items=\"{Rows}\" selection-mode=\"single\"");
        Assert.Equal("55", Texts(s)[2]);

        vm.Rows[0].Qty = 101; // +100
        s.PointerMove(new SKPoint(150, 40 + 24)); // hover → 다시 그림, 재계산 없음
        Assert.True(s.NeedsRender);
        Assert.Equal("55", Texts(s)[2]);

        s.Repaint();
        Assert.Equal("155", Texts(s)[2]);
    }

    [Fact]
    public void Summary_band_shortens_the_body_so_the_last_row_is_reachable()
    {
        var vm = new Vm { Rows = Enumerable.Range(0, 100).Select(i => new Row { Qty = i }).ToList() };
        var s = Surface(vm, "<DvSummaryRow title=\"합계\" fn=\"sum\"/>");
        var b = Box(s);
        Assert.Equal(240 - 40 - 48, G(s).BodyHeight, 3);
        Assert.Equal(40 + 100 * 48 + 48, b.ContentHeight, 3);
        Assert.Equal(100 * 48 - G(s).BodyHeight, b.MaxScrollY, 3);
        Assert.Equal(48, b.ScrollInsetBottom);
    }

    [Fact]
    public void Tapping_the_summary_band_selects_nothing()
    {
        var vm = new Vm();
        var s = Surface(vm, "<DvSummaryRow title=\"합계\" fn=\"sum\"/>",
            "items=\"{Rows}\" selection-mode=\"single\" selection=\"{Bind Picked}\"");
        var p = new SKPoint(150, 240 - 24);
        s.PointerDown(p);
        s.PointerUp(p);
        Assert.Empty(vm.Picked);
    }

    [Fact]
    public void Summary_band_uses_its_token()
    {
        var s = Surface(new Vm(), "<DvSummaryRow title=\"합계\" fn=\"sum\"/>");
        using var bmp = new SKBitmap(300, 300);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 300, 300);
        Assert.True(DvTheme.Light.TryGet("grid-summary", out var raw));
        Assert.True(DvColor.TryParse(raw, out var col));
        Assert.Equal(col.ToSK(), bmp.GetPixel(3, 240 - 10));
    }

    [Fact]
    public void Horizontal_scrollbar_strip_continues_the_summary_band()
    {
        // 열 합이 넓어 가로 스크롤 → 아래 12px 스크롤바 자리. 요약 행과 스크롤바 사이(6px)가 비지 않아야 한다
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
            "<DvColumn field=\"Name\" width=\"200\"/><DvColumn field=\"Value\" width=\"200\"/><DvSummaryRow title=\"합계\" fn=\"sum\"/>"),
            new Vm(), DvTheme.Light);
        s.Layout(300, 300);
        Assert.True(G(s).OverflowX);
        using var bmp = new SKBitmap(300, 300);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 300, 300);
        Assert.True(DvTheme.Light.TryGet("grid-summary", out var raw));
        Assert.True(DvColor.TryParse(raw, out var col));
        Assert.Equal(col.ToSK(), bmp.GetPixel(150, 240 - 9));
    }

    [Fact]
    public void Default_height_includes_summary_rows()
    {
        var b = new LayoutEngine().Layout(Devinno.UI.Markup.MarkupParser.Parse(
            "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:flex-start\">" +
            "<DvDataGrid><DvColumn field=\"A\"/><DvSummaryRow title=\"x\"/></DvDataGrid></DvScreen>"), null, 1000, 1000).Children[0];
        Assert.Equal(40 + 48 * 5 + 48, b.Rect.Height, 3);
    }
}
