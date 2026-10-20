using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;
using static GridEditFixture;

// 3d-1 편집 셀 팝업 — select·datetime·color, 셀 아래 배치, 항목에 쓰기, 게이트, 수명
public class GridEditPopupTests
{
    private static int DayIndex(DateTimePopup p, DateOnly d)
    {
        for (var i = 0; i < 42; i++)
        {
            if (p.State.CellDate(i) == d)
                return i;
        }

        throw new InvalidOperationException("날짜 칸 없음");
    }

    [Fact]
    public void Select_cell_tap_opens_dropdown_under_the_cell_without_selecting()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(2, 1));
        var d = s.Dropdown;
        Assert.NotNull(d);
        var slot = Assert.IsType<GridCellSlot>(d!.Slot);
        Assert.Same(vm.Rows[1], slot.Item);
        Assert.Equal(CellRect(2, 1).Left, d.Bounds.Left);
        Assert.Equal(CellRect(2, 1).Bottom + 4, d.Bounds.Top);
        Assert.Equal(1, d.Slot.SelectedIndex); // 수동
        Assert.Empty(vm.Picked);
        Assert.Empty(vm.Clicked);
    }

    [Fact]
    public void Picking_writes_the_item_once_and_closes()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(2, 1));
        Tap(s, Mid(s.Dropdown!.ItemRect(0)));
        Assert.Equal("auto", vm.Rows[1].Mode);
        Assert.Equal(1, vm.Rows[1].Writes);
        Assert.Null(s.TopPopup);
        Assert.True(s.NeedsRender); // Repaint 요청(재빌드 없이 다시 그림)
    }

    [Fact]
    public void Picking_the_same_value_writes_nothing()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(2, 1));
        Tap(s, Mid(s.Dropdown!.ItemRect(1)));
        Assert.Equal(0, vm.Rows[1].Writes);
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Value_only_gate_rejects_and_keeps_the_popup_open()
    {
        var cols = IdCol +
                   "<DvColumn header=\"모드\" field=\"Mode\" width=\"100\" cell-type=\"select\" onbeforecommit=\"CheckMode\">" +
                   "<DvOption value=\"auto\" text=\"자동\"/><DvOption value=\"manual\" text=\"수동\"/></DvColumn>";
        var vm = new EditVm { Allow = false };
        var s = Surface(vm, cols);
        Tap(s, Cell(0, 1));
        var d = s.Dropdown!;
        Tap(s, Mid(d.ItemRect(0)));
        Assert.Equal(new[] { "auto" }, vm.ModeGate);
        Assert.Equal("manual", vm.Rows[1].Mode);
        Assert.Same(d, s.TopPopup);
    }

    [Fact]
    public void Enum_and_items_selects_write_converted_values()
    {
        var cols = IdCol +
                   "<DvColumn header=\"종류\" field=\"Kind\" width=\"100\" cell-type=\"select\"/>" +
                   "<DvColumn header=\"레시피\" field=\"RecipeId\" width=\"100\" cell-type=\"select\" items=\"{Catalog}\" item-value=\"Id\" item-text=\"Name\"/>";
        var vm = new EditVm();
        var s = Surface(vm, cols);
        Tap(s, Cell(0, 0));
        Assert.Equal(3, s.Dropdown!.Slot.Options.Count);
        Tap(s, Mid(s.Dropdown!.ItemRect(2)));
        Assert.Equal(MachineKind.Fan, vm.Rows[0].Kind);

        Tap(s, Cell(1, 0));
        Tap(s, Mid(s.Dropdown!.ItemRect(1)));
        Assert.Equal(2, vm.Rows[0].RecipeId);
    }

    [Fact]
    public void Datetime_cell_ok_writes_the_item_keeping_time()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(3, 0));
        var p = Assert.IsType<DateTimePopup>(s.TopPopup);
        Assert.IsType<GridCellSlot>(p.Slot);
        Tap(s, Mid(p.DayRect(DayIndex(p, new DateOnly(2026, 10, 20)))));
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new DateTime(2026, 10, 20, 9, 30, 0), vm.Rows[0].ReserveAt);
        Assert.Equal(1, vm.Rows[0].Writes);
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Item_gate_receives_the_row_and_can_keep_the_picker_open()
    {
        var cols = IdCol + "<DvColumn header=\"예약\" field=\"ReserveAt\" width=\"100\" cell-type=\"datetime\" onbeforecommit=\"CheckAt\"/>";
        var vm = new EditVm { Allow = false };
        var s = Surface(vm, cols);
        Tap(s, Cell(0, 2));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.DayRect(DayIndex(p, new DateOnly(2026, 10, 25)))));
        Tap(s, Mid(p.OkRect));
        Assert.Single(vm.AtGate);
        Assert.Same(vm.Rows[2], vm.AtGate[0].Row);
        Assert.Equal(new DateTime(2026, 10, 25, 9, 30, 0), vm.AtGate[0].Value);
        Assert.Equal(new DateTime(2026, 10, 16, 9, 30, 0), vm.Rows[2].ReserveAt);
        Assert.Same(p, s.TopPopup);
    }

    [Fact]
    public void Color_cell_writes_hex_string_and_keeps_skcolor_alpha()
    {
        var cols = IdCol +
                   "<DvColumn header=\"색\" field=\"Pen\" width=\"100\" cell-type=\"color\"/>" +
                   "<DvColumn header=\"채움\" field=\"Fill\" width=\"100\" cell-type=\"color\"/>";
        var vm = new EditVm();
        var s = Surface(vm, cols);
        foreach (var k in new[] { 0, 1 })
        {
            Tap(s, Cell(k, 0));
            var p = Assert.IsType<ColorPopup>(s.TopPopup);
            var sv = p.SvRect;
            s.PointerDown(new SKPoint(sv.MidX, sv.MidY));
            s.PointerUp(new SKPoint(sv.MidX, sv.MidY));
            Tap(s, Mid(p.OkRect));
        }

        Assert.Matches("^#[0-9A-F]{6}$", vm.Rows[0].Pen);
        Assert.NotEqual("#0000FF", vm.Rows[0].Pen);
        Assert.Equal(0x80, vm.Rows[0].Fill.Alpha);
        Assert.NotEqual(new SKColor(0, 0, 255, 0x80), vm.Rows[0].Fill);
    }

    [Fact]
    public void Non_editable_select_cell_is_a_normal_row_tap()
    {
        var cols = IdCol + "<DvColumn header=\"모드\" field=\"Mode\" width=\"100\" cell-type=\"select\" readonly=\"true\"><DvOption value=\"auto\"/></DvColumn>";
        var vm = new EditVm();
        var s = Surface(vm, cols);
        Tap(s, Cell(0, 1));
        Assert.Null(s.TopPopup);
        Assert.Equal(new[] { vm.Rows[1] }, vm.Picked);
        Assert.Equal(new[] { vm.Rows[1] }, vm.Clicked);
    }

    [Fact]
    public void Open_popup_marks_the_cell_until_closed()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        using var before = Render(s);
        Tap(s, Cell(2, 1));
        var g = GridBox(s).Grid!;
        Assert.Equal(3, g.EditMark!.Column);
        Assert.Equal(1, g.EditMark.Key);
        using (var after = Render(s))
        {
            Assert.NotEqual(before.GetPixel(261, 112), after.GetPixel(261, 112)); // 행 1 모드 셀(260~360) 왼쪽 테두리
            Assert.Equal(before.GetPixel(261, 64), after.GetPixel(261, 64));      // 행 0 은 그대로(목록은 y 140 아래)
        }

        Tap(s, new SKPoint(590, 690)); // 바깥 = 닫기
        Assert.Null(g.EditMark);
    }

    // ── 수명
    [Fact]
    public void Rebuild_with_new_row_objects_keeps_the_popup_on_the_same_key()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(2, 1));
        var d = s.Dropdown!;
        vm.Rows = Enumerable.Range(0, 10).Select(i => new EditRow(i)).ToList();
        s.Invalidate();
        s.Layout(W, H);
        Assert.Same(d, s.TopPopup);
        Tap(s, Mid(d.ItemRect(0)));
        Assert.Equal("auto", vm.Rows[1].Mode);
        Assert.Same(vm.Rows[1], ((GridCellSlot)d.Slot).Item);
    }

    [Fact]
    public void Scrolling_the_cell_out_of_view_closes_the_popup()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(2, 1));
        GridBox(s).ScrollY = 200; // 행 1 (48~96) 은 몸통(200~400) 밖
        Render(s).Dispose();
        Assert.Null(s.TopPopup);
        Assert.Equal(0, vm.Rows[1].Writes);
    }

    [Fact]
    public void Removed_row_closes_the_popup_without_writing()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(2, 1));
        var removed = vm.Rows[1];
        vm.Rows.RemoveAt(1);
        s.Repaint();
        Render(s).Dispose();
        Assert.Null(s.TopPopup);
        Assert.Equal(0, removed.Writes);
    }

    [Fact]
    public void Header_tap_with_an_open_popup_only_closes_it()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(2, 1));
        Tap(s, new SKPoint(30, 20)); // Id 헤더(정렬 가능)
        Assert.Null(s.TopPopup);
        Assert.Empty(GridBox(s).Grid!.Sort);
    }

    [Fact]
    public void Cell_rect_follows_frozen_columns_and_scroll()
    {
        var cols = "<DvColumn header=\"Id\" field=\"Id\" width=\"60\" frozen=\"true\"/>" +
                   "<DvColumn header=\"a\" field=\"Name\" width=\"200\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"b\" field=\"SetPoint\" width=\"200\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"c\" field=\"Pen\" width=\"200\" cell-type=\"color\"/>";
        var s = Surface(new EditVm(), cols);
        var box = GridBox(s);
        var g = box.Grid!;
        box.ScrollX = 100;
        Assert.Equal(SKRect.Create(0, 88, 60, 48), GridGeometry.CellRect(g, box.Rect, box.ScrollX, box.ScrollY, 1, 0, out var fclip));
        Assert.Equal(SKRect.Create(0, 88, 60, 48), fclip);
        Assert.Equal(SKRect.Create(-40, 88, 200, 48), GridGeometry.CellRect(g, box.Rect, box.ScrollX, box.ScrollY, 1, 1, out var aclip));
        Assert.Equal(new SKRect(60, 88, 160, 136), aclip); // 고정 열 아래로 들어간 부분은 가려짐
        box.ScrollY = 384;
        GridGeometry.CellRect(g, box.Rect, box.ScrollX, box.ScrollY, 1, 1, out var hidden);
        Assert.True(hidden.IsEmpty);
    }

    // 페이지 자신이 VM — 그리드 마크업이 읽는 멤버만 EditVm 으로 넘긴다
    private sealed class GridPage(EditVm vm) : MarkupPage(Screen())
    {
        public List<EditRow> Rows => vm.Rows;
        public List<EditRow> Picked { get => vm.Picked; set => vm.Picked = value; }
        public void Inspect(EditRow r) => vm.Inspect(r);
        public bool CheckSetPoint(EditRow r, double v) => vm.CheckSetPoint(r, v);
    }

    [Fact]
    public void Page_grid_select_opens_on_the_app_root_and_writes_the_item()
    {
        var vm = new EditVm();
        var page = new GridPage(vm);
        var (_, root) = TestApp.WithLeft(page, 100, 700, 700); // 페이지 칸 = x 100~700
        void RootTap(SKPoint p) { root.PointerDown(p); root.PointerUp(p); root.Layout(700, 700); }

        RootTap(new SKPoint(100 + X(2), Y(1)));
        var d = root.Dropdown;
        Assert.NotNull(d);
        Assert.IsType<GridCellSlot>(d!.Slot);
        Assert.Equal(100 + CellRect(2, 1).Left, d.Bounds.Left);
        Assert.Null(page.Surface!.Dropdown);
        RootTap(Mid(d.ItemRect(0)));
        Assert.Equal("auto", vm.Rows[1].Mode);
    }

    // F1 — 행 없이 빌드 → 행이 생긴 뒤 그리기 경로에서 항목 목록을 다시 만들지 않는다(@키 글자 유지)
    [Fact]
    public void Select_option_keys_survive_rows_arriving_after_layout()
    {
        var strings = new DvStrings { Language = "ko" };
        strings.LoadJson("ko", """{ "mode": { "auto": "자동", "manual": "수동" } }""");
        var cols = IdCol +
                   "<DvColumn header=\"모드\" field=\"Mode\" width=\"100\" cell-type=\"select\">" +
                   "<DvOption value=\"auto\" text=\"@mode.auto\"/><DvOption value=\"manual\" text=\"@mode.manual\"/></DvColumn>" +
                   "<DvColumn header=\"종류\" field=\"Kind\" width=\"100\" cell-type=\"select\"/>";
        var vm = new EditVm { Rows = new List<EditRow>() };
        var s = new DvSurface(Screen(cols), vm, DvTheme.Light) { UseNativeEditor = true, Strings = strings };
        s.Layout(W, H);
        var col = GridBox(s).Grid!.Columns[1];
        var built = col.Options;
        Assert.Equal(new[] { "자동", "수동" }, built.Select(o => o.Text));

        vm.Rows.Add(new EditRow(0)); // Mode = auto
        s.Repaint();
        Render(s).Dispose();
        Assert.Same(built, col.Options);
        Assert.Equal("자동", col.CellText(vm.Rows[0]));
        Render(s).Dispose();
        Assert.Same(built, col.Options); // 그리기마다 다시 만들지 않음
        Assert.Equal(new[] { "Pump", "Valve", "Fan" }, GridBox(s).Grid!.Columns[2].Options.Select(o => o.Text)); // enum 자동은 타입 확정 후 생성

        Tap(s, Cell(0, 0));
        Assert.Equal(new[] { "자동", "수동" }, s.Dropdown!.Slot.Options.Select(o => o.Text));
    }

    // F2 — disabled 그리드는 편집 셀 탭에 반응 없음
    [Fact]
    public void Disabled_grid_ignores_edit_cell_taps()
    {
        var vm = new EditVm();
        var s = Surface(vm, attrs: Attrs + " disabled=\"true\"");
        Tap(s, Cell(0, 1));
        Assert.False(s.Edit.IsEditing);
        Tap(s, Cell(2, 1));
        Assert.Null(s.TopPopup);
        Tap(s, Cell(3, 1));
        Assert.Null(s.TopPopup);
        Assert.Equal(0, vm.Rows[1].Writes);
    }

    // F2 — 셀 커밋 뒤 다음 그리기에서 요약 행 반영
    [Fact]
    public void Summary_row_reflects_a_cell_commit_on_next_render()
    {
        var cols = IdCol +
                   "<DvColumn header=\"설정값\" field=\"SetPoint\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvSummaryRow title=\"합계\" fn=\"sum\"/>";
        var vm = new EditVm();
        var s = Surface(vm, cols);
        Render(s).Dispose();
        Assert.Equal("245", GridBox(s).Grid!.Summaries[0].Texts[1]); // 20..29
        Tap(s, Cell(0, 0));
        s.SetEditText(s.Edit.SessionId, "100");
        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.FocusLost));
        Assert.Equal(100d, vm.Rows[0].SetPoint);
        Render(s).Dispose();
        Assert.Equal("325", GridBox(s).Grid!.Summaries[0].Texts[1]);
    }
}
