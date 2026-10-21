using Devinno.UI;
using Devinno.UI.Grid;
using SkiaSharp;
using Xunit;

// 3d-2 필터 팝업 — 칸 탭으로 열기, text·values·range 입력, 확인·지우기·취소·바깥, 잘못된 숫자 거절, values 항목(다른 열 필터 반영·빈 값·정렬·상한), 스냅샷
public class GridFilterPopupTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public double Level { get; set; }
        public bool On { get; set; }
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 6).Select(i => new Row
            { Id = i, Name = i == 5 ? null : "P" + (i % 3), Level = i * 10, On = i % 2 == 0 }).ToList();
    }

    private const string Cols =
        "<DvColumn header=\"Id\" field=\"Id\" width=\"60\"/>" +
        "<DvColumn header=\"N\" field=\"Name\" width=\"100\" filter=\"values\"/>" +
        "<DvColumn header=\"L\" field=\"Level\" width=\"100\" filter=\"range\"/>" +
        "<DvColumn header=\"T\" field=\"Name\" width=\"100\" filter=\"text\"/>";

    private static (DvSurface s, GridSpec g, Vm vm) Make()
    {
        var vm = new Vm();
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", Cols), vm) { UseNativeEditor = true };
        s.Layout(600, 500);
        return (s, s.Root!.Children[0].Grid!, vm);
    }

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(600, 500);
    }

    private static GridFilterPopup Open(DvSurface s, GridSpec g, int column)
    {
        var r = GridGeometry.FilterCellRect(g, s.Root!.Children[0].Rect, 0, column);
        Tap(s, new SKPoint(r.MidX, r.MidY));
        return Assert.IsType<GridFilterPopup>(s.TopPopup);
    }

    // 탭마다 재빌드(Invalidate)되므로 결과는 지금 트리의 그리드로 읽는다
    private static GridSpec Cur(DvSurface s) => s.Root!.Children[0].Grid!;

    private static int[] Ids(DvSurface s)
    {
        var g = Cur(s);
        GridSorter.Ensure(g);
        return Enumerable.Range(0, g.ViewCount).Select(v => ((Row)g.ItemAtCurrent(v)!).Id).ToArray();
    }

    [Fact]
    public void Values_popup_lists_distinct_values_sorted_with_empty_item()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 1);
        Assert.Equal(new[] { "(Empty)", "P0", "P1", "P2" }, p.Items.Select(i => i.Text).ToArray());
        Assert.Equal(4, p.Checked.Count); // 조건 없음 = 전부 체크
    }

    [Fact]
    public void Values_uncheck_and_ok_applies_filter_and_summary()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 1);
        Tap(s, Mid(p.ItemRect(1))); // P0 해제
        Tap(s, Mid(p.ItemRect(0))); // (빈 값) 해제
        Tap(s, Mid(p.OkRect));
        Assert.Null(s.TopPopup);
        Assert.Equal(new[] { 1, 2, 4 }, Ids(s)); // P1·P2
        Assert.Equal("P1, P2", Cur(s).Filters[1].Summary);
    }

    [Fact]
    public void Values_all_item_toggles_everything_and_all_checked_means_no_condition()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 1);
        Tap(s, Mid(p.AllRect)); // 전부 해제
        Assert.Empty(p.Checked);
        Tap(s, Mid(p.AllRect)); // 전부 체크
        Tap(s, Mid(p.OkRect));
        Assert.Empty(Cur(s).Filters);
    }

    [Fact]
    public void Values_items_respect_other_column_filters()
    {
        var (s, g, _) = Make();
        g.SetFilter(3, new GridFilterCondition { Text = "P1", Summary = "P1" });
        var p = Open(s, g, 1);
        Assert.Equal(new[] { "P1" }, p.Items.Select(i => i.Text).ToArray());
    }

    [Fact]
    public void Values_popup_snapshot_survives_repaint()
    {
        var (s, g, vm) = Make();
        var p = Open(s, g, 1);
        vm.Rows.Clear();
        s.Repaint();
        s.Layout(600, 500);
        Assert.Equal(4, p.Items.Count);
        Tap(s, Mid(p.OkRect)); // 예외 없음
    }

    [Fact]
    public void Values_cap_at_1000_with_truncated_flag()
    {
        var vm = new Vm { Rows = Enumerable.Range(0, 1500).Select(i => new Row { Id = i, Name = "N" + i.ToString("D4") }).ToList() };
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", Cols), vm);
        s.Layout(600, 500);
        var p = Open(s, s.Root!.Children[0].Grid!, 1);
        Assert.Equal(1000, p.Items.Count);
        Assert.True(p.Truncated);
    }

    [Fact]
    public void Range_fields_use_keypad_rules_and_reject_non_numbers()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 2);
        Tap(s, Mid(p.FieldRect(1)));                       // 최소 칸 → 편집 세션
        Assert.Equal(DvEditKind.Numeric, s.Edit!.Kind);
        s.SetEditText(s.Edit.SessionId, "20");
        s.CommitEdit(s.Edit.SessionId, DvEditExit.FocusLost);
        Tap(s, Mid(p.FieldRect(2)));
        s.SetEditText(s.Edit!.SessionId, "abc");
        Assert.False(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter)); // 숫자 아님 = 거절, 세션 유지
        s.SetEditText(s.Edit.SessionId, "40");
        s.CommitEdit(s.Edit.SessionId, DvEditExit.FocusLost);
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new[] { 2, 3, 4 }, Ids(s));
        Assert.Equal("20 ~ 40", Cur(s).Filters[2].Summary);
    }

    [Fact]
    public void Text_field_and_clear_and_cancel_and_outside()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 3);
        Tap(s, Mid(p.FieldRect(0)));
        s.SetEditText(s.Edit!.SessionId, "p1");
        s.CommitEdit(s.Edit.SessionId, DvEditExit.FocusLost);
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new[] { 1, 4 }, Ids(s));

        p = Open(s, g, 3);
        Tap(s, Mid(p.CancelRect)); // 취소 = 그대로
        Assert.Equal(new[] { 1, 4 }, Ids(s));

        p = Open(s, g, 3);
        Tap(s, new SKPoint(590, 490)); // 바깥 = 취소
        Assert.Null(s.TopPopup);
        Assert.Equal(new[] { 1, 4 }, Ids(s));

        p = Open(s, g, 3);
        Tap(s, Mid(p.ClearRect)); // 지우기 = 조건 없앰 + 닫힘
        Assert.Null(s.TopPopup);
        Assert.Empty(Cur(s).Filters);
    }

    [Fact]
    public void Popup_width_fits_longest_item_up_to_screen()
    {
        var vm = new Vm { Rows = new() { new Row { Name = new string('가', 40) }, new Row { Name = "a" } } };
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", Cols), vm);
        s.Layout(600, 500);
        var p = Open(s, s.Root!.Children[0].Grid!, 1);
        Assert.True(p.Bounds.Width > 100);           // 칸 폭(100)보다 넓게
        Assert.True(p.Bounds.Width <= 600 - 16 + 0.5f); // 화면 폭 − 여백
    }

    [Theory]
    [InlineData("<DvColumn header=\"L\" field=\"Level\" width=\"100\" filter=\"range\" format=\"N0\"/>", 1500d, 2500d)]
    [InlineData("<DvColumn header=\"I\" field=\"Id\" width=\"100\" filter=\"range\" format=\"0.0\"/>", 2, 4)]
    public void Range_reopen_and_ok_unchanged_keeps_condition_with_column_format(string col, object min, object max)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", col), new Vm()) { UseNativeEditor = true };
        s.Layout(600, 500);
        Cur(s).SetFilter(0, new GridFilterCondition { Min = min, Max = max, Summary = "x" });
        var p = Open(s, Cur(s), 0);
        Tap(s, Mid(p.OkRect));
        Assert.Null(s.TopPopup);
        Assert.Equal(min, Cur(s).Filters[0].Min);
        Assert.Equal(max, Cur(s).Filters[0].Max);
    }

    [Fact]
    public void Blocked_clear_does_not_turn_a_later_ok_into_clear()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 2);
        Tap(s, Mid(p.FieldRect(1)));
        s.SetEditText(s.Edit.SessionId, "abc");
        Tap(s, Mid(p.ClearRect)); // 칸 커밋 거절 → 이 누름의 지우기는 막힘
        Assert.Same(p, s.TopPopup);
        Tap(s, Mid(p.FieldRect(1)));
        s.SetEditText(s.Edit.SessionId, "20");
        s.CommitEdit(s.Edit.SessionId, DvEditExit.FocusLost);
        Tap(s, Mid(p.OkRect));
        Assert.Null(s.TopPopup);
        Assert.Equal(20d, Cur(s).Filters[2].Min);
    }

    [Fact]
    public void Values_untouched_ok_keeps_condition_when_other_filter_narrowed_items_to_chosen()
    {
        var (s, g, _) = Make();
        g.SetFilter(3, new GridFilterCondition { Text = "P1", Summary = "P1" });
        g.SetFilter(1, new GridFilterCondition { Values = new HashSet<object?> { "P1" }, Summary = "P1" });
        var p = Open(s, g, 1);
        Assert.Equal(new[] { "P1" }, p.Items.Select(i => i.Text).ToArray()); // 전부 체크로 보임
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new object?[] { "P1" }, Cur(s).Filters[1].Values!.ToArray());
        Assert.Equal(new[] { 1, 4 }, Ids(s));
    }

    [Fact]
    public void Rejected_field_shows_error_line_under_fields_without_moving_them()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 2);
        var field = p.FieldRect(2);
        var h = p.Bounds.Height;
        Tap(s, Mid(p.FieldRect(2)));
        s.SetEditText(s.Edit.SessionId, "abc");
        s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter);
        using (var bmp = new SKBitmap(600, 500))
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 600, 500); // 그릴 때 다시 배치(거절 사유 줄 포함) — 예외 없이 그려짐도 확인
        Assert.True(p.ShowsError);
        Assert.Equal(h + GridFilterPopup.ErrorH, p.Bounds.Height);
        Assert.Equal(field, p.FieldRect(2));
        Assert.True(p.ErrorRect.Top >= field.Bottom && p.ErrorRect.Bottom <= p.CancelRect.Top);
    }

    // 팝업이 열린 동안 열 순서가 바뀐 그리드로 바뀌었다(재빌드) — 확인은 연 열의 field 경로로 찾아 건다
    private static GridSpec Swap(DvSurface s, string cols)
    {
        var other = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", cols), new Vm());
        other.Layout(600, 500);
        var g = other.Root!.Children[0].Grid!;
        s.Root!.Children[0].Grid = g;
        return g;
    }

    [Fact]
    public void Ok_applies_to_the_column_with_the_same_field_after_columns_moved()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 1); // Name(values)
        Tap(s, Mid(p.ItemRect(1))); // P0 해제
        var moved = Swap(s, "<DvColumn field=\"Name\" width=\"100\" filter=\"values\"/><DvColumn field=\"Id\" width=\"60\" filter=\"values\"/>");
        Tap(s, Mid(p.OkRect));
        Assert.Null(s.TopPopup);
        Assert.Equal(new[] { 0 }, moved.Filters.Keys.ToArray()); // 같은 번호(1 = Id)가 아니라 Name 열
    }

    [Fact]
    public void Ok_closes_without_applying_when_no_column_of_that_field_and_kind_is_left()
    {
        var (s, g, _) = Make();
        var p = Open(s, g, 1); // Name(values)
        Tap(s, Mid(p.ItemRect(1)));
        var moved = Swap(s, "<DvColumn field=\"Id\" width=\"60\" filter=\"values\"/><DvColumn field=\"Name\" width=\"100\" filter=\"text\"/>");
        Tap(s, Mid(p.OkRect));
        Assert.Null(s.TopPopup);
        Assert.Empty(moved.Filters);
    }

    private static SKPoint Mid(SKRect r) => new(r.MidX, r.MidY);
}
