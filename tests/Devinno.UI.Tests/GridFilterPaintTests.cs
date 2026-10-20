using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3d-2 필터 행 그리기 — 배경 토큰, 필터 없는 열 = 빈 칸, 조건 있는 칸 = 강조색, 긴 요약 = 칸 안에서 잘림, 헤더 그룹선이 필터 행으로 내려오지 않음
public class GridFilterPaintTests
{
    public sealed class Row { public string Name { get; set; } = "n"; public int Id { get; set; } }
    public sealed class Vm { public List<Row> Rows { get; } = new() { new Row() }; }

    private static (DvSurface s, SKBitmap bmp) Render(string cols, Action<GridSpec>? setup = null, DvTheme? theme = null)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", cols), new Vm(), theme ?? DvTheme.Light);
        s.Layout(400, 300);
        setup?.Invoke(s.Root!.Children[0].Grid!);
        var bmp = new SKBitmap(400, 300);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 400, 300);
        return (s, bmp);
    }

    private static readonly SKColor ActiveLight = Tok("grid-filter-active", DvTheme.Light);
    private static SKColor Tok(string key, DvTheme t) { t.TryGet(key, out var raw); DvColor.TryParse(raw!, out var c); return c.ToSK(); }

    [Fact]
    public void Filter_row_background_and_empty_cell_for_non_filter_column()
    {
        var (_, bmp) = Render("<DvColumn field=\"Name\" width=\"200\" filter=\"text\"/><DvColumn field=\"Id\" width=\"200\"/>");
        using (bmp)
        {
            Assert.Equal(Tok("grid-filter", DvTheme.Light), bmp.GetPixel(300, 60)); // Id 칸 = 배경만
            Assert.Equal(Tok("grid-filter", DvTheme.Light), bmp.GetPixel(190, 60)); // Name 칸 오른쪽 빈 곳
        }
    }

    [Fact]
    public void Active_condition_draws_icon_in_active_color()
    {
        var active = Tok("grid-filter-active", DvTheme.Light);
        var (_, bmp) = Render("<DvColumn field=\"Name\" width=\"200\" filter=\"text\"/>",
            g => g.SetFilter(0, new GridFilterCondition { Text = "n", Summary = "n" }));
        using (bmp)
        {
            var found = false;
            for (var x = 0; x < 30 && !found; x++)
                for (var y = 45; y < 75 && !found; y++)
                    found = bmp.GetPixel(x, y) == active;
            Assert.True(found, "깔때기 아이콘이 강조색으로");
        }
    }

    [Fact]
    public void Long_summary_is_clipped_inside_its_cell()
    {
        var (_, bmp) = Render("<DvColumn field=\"Name\" width=\"80\" filter=\"text\"/><DvColumn field=\"Id\" width=\"200\"/>",
            g => g.SetFilter(0, new GridFilterCondition { Text = "x", Summary = "2026-10-14 09:00 ~ 2026-10-15 18:00" }));
        using (bmp)
        {
            var bg = Tok("grid-filter", DvTheme.Light);
            for (var x = 82; x < 278; x++) // 278 이후는 Id 칸 오른쪽 구분선
                Assert.Equal(bg, bmp.GetPixel(x, 60)); // 옆 칸으로 넘치지 않음
        }
    }

    [Fact]
    public void Dark_theme_has_filter_tokens()
    {
        Assert.True(DvTheme.Dark.TryGet("grid-filter", out _));
        Assert.True(DvTheme.Dark.TryGet("grid-filter-fg", out _));
        Assert.True(DvTheme.Dark.TryGet("grid-filter-active", out _));
    }

    [Fact]
    public void Group_vertical_line_stops_at_header_band()
    {
        // Id(100) 뒤에 Name 만 감싼 그룹 — 그룹 왼쪽 세로선은 x=100 (필터 칸 구분선 x=99 와 안 겹침)
        var (s, bmp) = Render("<DvColumn field=\"Id\" width=\"100\"/><DvColumnGroup header=\"G\"><DvColumn field=\"Name\" width=\"200\" filter=\"text\"/></DvColumnGroup>");
        using (bmp)
        {
            var g = s.Root!.Children[0].Grid!;
            Assert.True(g.HasFilterRow);
            var inBand = (int)(g.HeaderBand - 4);
            var inFilter = (int)(g.HeaderBand + g.FilterRowHeight / 2f);
            Assert.NotEqual(Tok("grid-filter", DvTheme.Light), bmp.GetPixel(100, inBand)); // 헤더 단에서는 세로선이 있음
            Assert.Equal(Tok("grid-filter", DvTheme.Light), bmp.GetPixel(100, inFilter));   // 필터 행에는 내려오지 않음
        }
    }

    [Fact]
    public void Frozen_filter_cell_stays_put_while_scrolled_filter_cell_moves()
    {
        var cols = "<DvColumn field=\"Name\" width=\"100\" frozen=\"true\" filter=\"text\"/>" +
                   "<DvColumn field=\"Id\" width=\"300\" filter=\"text\"/><DvColumn field=\"Name\" width=\"300\"/>";
        var (s, before) = Render(cols, g =>
        {
            g.SetFilter(0, new GridFilterCondition { Text = "n", Summary = "" });
            g.SetFilter(1, new GridFilterCondition { Text = "n", Summary = "" });
        });
        var box = s.Root!.Children[0];
        var g = box.Grid!;
        var y0 = (int)g.HeaderBand + 2;
        var y1 = (int)g.HeaderTotal - 2;

        // 열 칸 [x0, x1) 의 필터 행에서 강조색 가장 왼쪽 x (없으면 -1)
        static int Leftmost(SKBitmap b, int x0, int x1, int y0, int y1)
        {
            for (var x = x0; x < x1; x++)
                for (var y = y0; y < y1; y++)
                    if (b.GetPixel(x, y) == ActiveLight) return x;
            return -1;
        }

        using (before)
        {
            var frozenBefore = Leftmost(before, 0, 100, y0, y1);
            var scrolledBefore = Leftmost(before, 100, 400, y0, y1);
            Assert.True(frozenBefore >= 0 && scrolledBefore >= 0);

            box.ScrollX = 10;
            s.Repaint();
            using var after = new SKBitmap(400, 300);
            using (var c = new SKCanvas(after)) s.RenderTo(c, 400, 300);

            Assert.Equal(frozenBefore, Leftmost(after, 0, 100, y0, y1));          // 고정 열 아이콘은 그대로
            Assert.Equal(scrolledBefore - 10, Leftmost(after, 100, 400, y0, y1)); // 스크롤 열 아이콘은 ScrollX 만큼 이동
            for (var y = y0; y < y1; y++)
                for (var x = 0; x < 99; x++)
                    Assert.Equal(before.GetPixel(x, y), after.GetPixel(x, y));    // 고정 영역은 스크롤 열이 덮지 않음
        }
    }
}
