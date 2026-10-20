using System.Reflection;
using Devinno.UI;
using Devinno.UI.Grid;
using SkiaSharp;
using Xunit;

// 3d-2 date 필터 — 칸 탭 = 필터 팝업 위에 날짜 선택기, 확인 = 칸 값만(VM 쓰기 없음), 선택기 바깥 = 선택기만 닫힘, 확인 = 조건
public class GridFilterDateTests
{
    public sealed class Row { public int Id { get; set; } public DateTime At { get; set; } }

    public sealed class Vm
    {
        public List<Row> Rows { get; } = Enumerable.Range(0, 5).Select(i => new Row { Id = i, At = new DateTime(2026, 10, 1).AddDays(i) }).ToList();
    }

    private static void Tap(DvSurface s, SKPoint p) { s.PointerDown(p); s.PointerUp(p); s.Layout(700, 700); }
    private static SKPoint Mid(SKRect r) => new(r.MidX, r.MidY);
    private static GridSpec Cur(DvSurface s) => s.Root!.Children[0].Grid!;

    private static (DvSurface s, Vm vm, GridFilterPopup fp) OpenDate(string cols, int column)
    {
        var vm = new Vm();
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", cols), vm);
        s.Layout(700, 700);
        Tap(s, Mid(GridGeometry.FilterCellRect(Cur(s), s.Root!.Children[0].Rect, 0, column)));
        return (s, vm, Assert.IsType<GridFilterPopup>(s.TopPopup));
    }

    [Fact]
    public void Date_field_opens_picker_over_filter_popup_and_ok_sets_bound_only()
    {
        var (s, vm, fp) = OpenDate("<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"At\" width=\"200\" filter=\"auto\"/>", 1);

        Tap(s, Mid(fp.FieldRect(1)));                         // 시작 칸
        var dp = Assert.IsType<DateTimePopup>(s.TopPopup);    // 위에 쌓임
        dp.State.TapDay(new DateOnly(2026, 10, 3));
        Tap(s, Mid(dp.OkRect));
        Assert.Same(fp, s.TopPopup);                          // 선택기만 닫힘
        Assert.Equal(new DateTime(2026, 10, 3).Date, ((DateTime)fp.MinValue!).Date);
        Assert.Equal(new DateTime(2026, 10, 1), vm.Rows[0].At); // VM 쓰기 없음

        Tap(s, Mid(fp.OkRect));
        var g = Cur(s);
        GridSorter.Ensure(g);
        Assert.Equal(3, g.ViewCount); // 10-03 ~
        Assert.StartsWith("≥ 2026-10-03", g.Filters[1].Summary);
    }

    [Fact]
    public void Outside_tap_on_picker_closes_only_the_picker()
    {
        var (s, _, fp) = OpenDate("<DvColumn field=\"At\" width=\"200\" filter=\"date\"/>", 0);
        Tap(s, Mid(fp.FieldRect(2)));
        Assert.IsType<DateTimePopup>(s.TopPopup);
        Tap(s, new SKPoint(690, 690));
        Assert.Same(fp, s.TopPopup);
        Assert.Null(fp.MaxValue);
    }

    [Fact]
    public void Empty_start_opens_at_midnight_and_empty_end_at_last_second()
    {
        var (s, _, fp) = OpenDate("<DvColumn field=\"At\" width=\"200\" filter=\"date\"/>", 0);
        Tap(s, Mid(fp.FieldRect(1)));
        var st = Assert.IsType<DateTimePopup>(s.TopPopup).State;
        Assert.Equal((0, 0, 0), (st.Hour, st.Minute, st.Second));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), st.Selected);
        Tap(s, new SKPoint(690, 690));

        Tap(s, Mid(fp.FieldRect(2)));
        var dp = Assert.IsType<DateTimePopup>(s.TopPopup);
        Assert.Equal((23, 59, 59), (dp.State.Hour, dp.State.Minute, dp.State.Second));
        Tap(s, Mid(dp.OkRect)); // 날짜만 바꾸지 않고 확인 — 끝 칸이 23:59:59 로 채워진다
        Assert.Equal(DateTime.Today.AddDays(1).AddSeconds(-1), (DateTime)fp.MaxValue!);
    }

    [Fact]
    public void Closing_filter_popup_while_picker_open_closes_the_picker_too()
    {
        var (s, _, fp) = OpenDate("<DvColumn field=\"At\" width=\"200\" filter=\"date\"/>", 0);
        Tap(s, Mid(fp.FieldRect(1)));
        Assert.IsType<DateTimePopup>(s.TopPopup);
        typeof(DvSurface).GetMethod("ClosePopup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(s, new object[] { fp });
        Assert.Null(s.TopPopup);
    }
    private sealed class FilterPage() : MarkupPage(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
        "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"At\" width=\"200\" filter=\"date\"/>"))
    {
        public List<Row> Rows { get; } = new Vm().Rows;
    }

    [Fact]
    public void App_page_grid_date_filter_through_the_root()
    {
        var page = new FilterPage();
        var (_, shell) = TestApp.WithLeft(page, 100, 700, 700);
        var body = page.Surface!;
        var cell = GridGeometry.FilterCellRect(Cur(body), body.Root!.Children[0].Rect, 0, 1);
        Tap(shell, new SKPoint(100 + cell.MidX, cell.MidY)); // 페이지 칸은 창 x=100 부터
        var fp = Assert.IsType<GridFilterPopup>(shell.TopPopup);
        Assert.Equal(100 + cell.Left, fp.Bounds.Left, 1);
        var fr = fp.FieldRect(1);
        Tap(shell, Mid(fr));
        var dp = Assert.IsType<DateTimePopup>(shell.TopPopup); // 필터 팝업 위에 쌓임
        Assert.True(dp.Bounds.Left >= fr.Left - 1 && dp.Bounds.Left < fr.Right, $"picker {dp.Bounds} field {fr}");
        dp.State.TapDay(new DateOnly(2026, 10, 3));
        Tap(shell, Mid(dp.OkRect));
        Tap(shell, Mid(fp.OkRect));
        Assert.Null(shell.TopPopup);
        using var bmp = new SKBitmap(700, 700);
        using var c = new SKCanvas(bmp);
        shell.RenderTo(c, 700, 700);
        var g = Cur(body);
        GridSorter.Ensure(g);
        Assert.Equal(3, g.ViewCount); // 10/3·10/4·10/5
        Assert.Equal(3, g.Count);
    }
}
