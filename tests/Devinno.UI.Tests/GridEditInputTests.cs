using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;
using static GridEditFixture;

// 3d-1 input 셀 — 셀 위치 편집 세션, 숫자 규격, 커밋·거절·게이트, 편집 불가, 수명
public class GridEditInputTests
{
    private static void Type(DvSurface s, string text) => s.SetEditText(s.Edit.SessionId, text);
    private static bool Commit(DvSurface s, DvEditExit exit) => s.CommitEdit(s.Edit.SessionId, exit);
    private static GridSpec Spec(DvSurface s, int i = 0) => GridBox(s, i).Grid!;

    [Fact]
    public void String_cell_tap_starts_a_text_session_on_the_cell()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(0, 1));
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(DvEditKind.Text, s.Edit.Kind);
        Assert.Equal("R1", s.Edit.InitialText);
        Assert.Equal(CellRect(0, 1), s.Edit.Rect);
        Assert.Equal(TextAlign.Left, s.Edit.Align);
        Assert.IsType<GridCellEditTarget>(s.EditTarget);
        Assert.Empty(vm.Picked);
        Assert.Empty(vm.Clicked);
    }

    [Fact]
    public void Commit_writes_the_item_once()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(0, 1));
        Type(s, "펌프");
        Assert.True(Commit(s, DvEditExit.FocusLost));
        Assert.Equal("펌프", vm.Rows[1].Name);
        Assert.Equal(1, vm.Rows[1].Writes);
        Assert.False(s.Edit.IsEditing);
        Assert.Null(Spec(s).EditMark);
    }

    [Fact]
    public void Same_value_writes_nothing()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(1, 1));
        Type(s, "21.0"); // 21 과 같은 수
        Assert.True(Commit(s, DvEditExit.FocusLost));
        Assert.Equal(0, vm.Rows[1].Writes);
    }

    [Fact]
    public void Numeric_spec_comes_from_property_type_and_column()
    {
        var cols = IdCol +
                   "<DvColumn header=\"설정값\" field=\"SetPoint\" width=\"100\" cell-type=\"input\" min=\"0\" max=\"120\" unit=\"℃\"/>" +
                   "<DvColumn header=\"수\" field=\"Count\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"회\" field=\"Cycles\" width=\"100\" cell-type=\"input\"/>";
        var s = Surface(new EditVm(), cols);
        Tap(s, Cell(0, 0));
        Assert.Equal(new DvNumericSpec(false, true, 0, 120, "℃"), s.Edit.Numeric);
        Assert.Equal(TextAlign.Right, s.Edit.Align);
        Tap(s, Cell(1, 0));
        Assert.Equal(new DvNumericSpec(true, true, null, null, null), s.Edit.Numeric);
        Tap(s, Cell(2, 0));
        Assert.Equal(new DvNumericSpec(true, false, null, null, null), s.Edit.Numeric);
        Assert.Equal(DvEditKind.Numeric, s.Edit.Kind);
    }

    [Fact]
    public void Out_of_range_is_rejected_and_the_session_stays()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(1, 1));
        Type(s, "130");
        Assert.False(Commit(s, DvEditExit.Enter));
        Assert.True(s.Edit.IsEditing);
        Assert.Equal("허용 범위: 0 ~ 120", s.Edit.Error);
        Assert.Equal("허용 범위: 0 ~ 120", Spec(s).EditMark!.Error);
        Assert.Equal(21, vm.Rows[1].SetPoint);
    }

    [Fact]
    public void Item_gate_gets_the_row_and_the_typed_value()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(1, 2));
        Type(s, "55");
        Assert.True(Commit(s, DvEditExit.FocusLost));
        Assert.Equal((vm.Rows[2], 55d), vm.SetPointGate.Single());
        Assert.Equal(55, vm.Rows[2].SetPoint);

        vm.Allow = false;
        Tap(s, Cell(1, 3));
        Type(s, "66");
        Assert.False(Commit(s, DvEditExit.Enter));
        Assert.Equal("허용되지 않는 값입니다", s.Edit.Error);
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(23, vm.Rows[3].SetPoint);
    }

    [Fact]
    public void Value_only_gate_missing_gate_and_throwing_gate()
    {
        var cols = IdCol +
                   "<DvColumn header=\"a\" field=\"Name\" width=\"100\" cell-type=\"input\" onbeforecommit=\"CheckName\"/>" +
                   "<DvColumn header=\"b\" field=\"Name\" width=\"100\" cell-type=\"input\" onbeforecommit=\"Nope\"/>" +
                   "<DvColumn header=\"c\" field=\"Name\" width=\"100\" cell-type=\"input\" onbeforecommit=\"Throws\"/>";
        var vm = new EditVm();
        var s = Surface(vm, cols);
        Tap(s, Cell(0, 0));
        Type(s, "A");
        Assert.True(Commit(s, DvEditExit.FocusLost));
        Assert.Equal(new[] { "A" }, vm.NameGate);
        Assert.Equal("A", vm.Rows[0].Name);

        foreach (var k in new[] { 1, 2 })
        {
            Tap(s, Cell(k, 1));
            Type(s, "B");
            Assert.False(Commit(s, DvEditExit.Enter)); // 없음·예외 = 거절
            Assert.Equal("R1", vm.Rows[1].Name);
            s.CancelEdit(s.Edit.SessionId);
        }
    }

    [Fact]
    public void Non_editable_input_cells_are_normal_row_taps()
    {
        var cols = IdCol +
                   "<DvColumn header=\"a\" field=\"Name\" width=\"100\" cell-type=\"input\" readonly=\"true\"/>" +
                   "<DvColumn header=\"b\" field=\"Computed\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"c\" field=\"ReserveAt\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"d\" field=\"Pos.X\" width=\"100\" cell-type=\"input\"/>";
        var vm = new EditVm();
        var s = Surface(vm, cols);
        for (var k = 0; k < 4; k++)
        {
            Tap(s, Cell(k, k));
            Assert.False(s.Edit.IsEditing);
            Assert.Equal(new[] { vm.Rows[k] }, vm.Picked);
        }

        Assert.Equal(4, vm.Clicked.Count);
    }

    [Fact]
    public void Tapping_another_cell_commits_first()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(0, 1));
        Type(s, "A");
        Tap(s, Cell(0, 2));
        Assert.Equal("A", vm.Rows[1].Name);
        Assert.True(s.Edit.IsEditing);
        Assert.Equal("R2", s.Edit.InitialText);
        Assert.Equal(2, Spec(s).EditMark!.Key);
    }

    [Fact]
    public void Header_tap_commits_then_sorts()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(0, 1));
        Type(s, "ZZ");
        Tap(s, new SKPoint(30, 20)); // Id 헤더
        Assert.Equal("ZZ", vm.Rows[1].Name);
        Assert.False(s.Edit.IsEditing);
        Assert.Single(Spec(s).Sort);
    }

    [Fact]
    public void Soft_keyboard_shows_typed_text_in_the_cell_and_escape_cancels()
    {
        var vm = new EditVm();
        var s = Surface(vm, native: false);
        Tap(s, Cell(0, 1));
        Render(s).Dispose(); // 렌더가 키보드를 연다(SyncKeyboard)
        Assert.NotNull(s.Keyboard);
        Type(s, "R1x");
        var mark = Spec(s).EditMark!;
        Assert.Equal("R1x", mark.Text);
        Assert.False(mark.HideText);
        s.KeyInput(DvKey.Escape);
        Assert.False(s.Edit.IsEditing);
        Assert.Equal("R1", vm.Rows[1].Name);
        Assert.Null(Spec(s).EditMark);
    }

    [Fact]
    public void Native_editor_hides_the_cell_text()
    {
        var s = Surface(new EditVm());
        Tap(s, Cell(0, 1));
        var mark = Spec(s).EditMark!;
        Assert.True(mark.HideText);
        Assert.Null(mark.Text);
    }

    [Fact]
    public void Partly_hidden_cell_is_scrolled_into_view_on_start()
    {
        var s = Surface(new EditVm());
        Tap(s, new SKPoint(X(0), 236)); // 행 4: 232~280 중 232~240 만 보임
        Assert.Equal(40, GridBox(s).ScrollY);
        Assert.Equal(SKRect.Create(60, 192, 100, 48), s.Edit.Rect);
    }

    // ── 수명
    [Fact]
    public void Rebuild_with_new_row_objects_keeps_the_session_on_the_same_key()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(0, 2));
        var id = s.Edit.SessionId;
        vm.Rows = Enumerable.Range(0, 10).Select(i => new EditRow(i)).ToList();
        s.Invalidate();
        s.Layout(W, H);
        Assert.Equal(id, s.Edit.SessionId);
        Type(s, "N");
        Assert.True(Commit(s, DvEditExit.FocusLost));
        Assert.Equal("N", vm.Rows[2].Name);
    }

    [Fact]
    public void Without_row_key_replaced_rows_close_the_edit_without_write()
    {
        var vm = new EditVm();
        var s = Surface(vm, attrs: "items=\"{Rows}\"");
        Tap(s, Cell(0, 1));
        Type(s, "X");
        var old = vm.Rows[1];
        vm.Rows = Enumerable.Range(0, 10).Select(i => new EditRow(i)).ToList();
        s.Invalidate();
        s.Layout(W, H);
        Assert.False(s.Edit.IsEditing);
        Assert.Equal("R1", old.Name);
        Assert.Equal("R1", vm.Rows[1].Name);
    }

    [Fact]
    public void Repaint_keeps_the_session()
    {
        var s = Surface(new EditVm());
        Tap(s, Cell(0, 1));
        var id = s.Edit.SessionId;
        s.Repaint();
        Render(s).Dispose();
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(id, s.Edit.SessionId);
    }

    [Fact]
    public void Scrolling_the_cell_out_commits_the_typed_value()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(0, 0));
        Type(s, "S");
        s.PointerWheel(new SKPoint(X(0), Y(2)), -5); // 240 아래로 — 행 0 은 몸통 밖
        Render(s).Dispose();
        Assert.False(s.Edit.IsEditing);
        Assert.Equal("S", vm.Rows[0].Name);
    }

    [Fact]
    public void Removed_row_closes_the_edit_without_writing()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(0, 1));
        Type(s, "X");
        var removed = vm.Rows[1];
        vm.Rows.RemoveAt(1);
        s.Repaint();
        Render(s).Dispose();
        Assert.False(s.Edit.IsEditing);
        Assert.Equal(0, removed.Writes);
    }

    [Fact]
    public void Clearing_a_nullable_number_is_rejected_like_dvinput()
    {
        var cols = IdCol + "<DvColumn header=\"수\" field=\"Count\" width=\"100\" cell-type=\"input\"/>";
        var vm = new EditVm();
        var s = Surface(vm, cols);
        Tap(s, Cell(0, 0));
        Type(s, "");
        Assert.False(Commit(s, DvEditExit.Enter));
        Assert.Equal("값을 입력하세요", s.Edit.Error);
        Assert.Equal(3, vm.Rows[0].Count);
    }

    [Fact]
    public void Tapping_another_grid_commits_first()
    {
        var vm = new EditVm();
        var markup = "<DvScreen style=\"padding:0; gap:0; display:flex; flex-direction:column; align-items:stretch\">" +
                     GridEditFixture.Grid() + GridEditFixture.Grid() + "</DvScreen>";
        var s = new DvSurface(markup, vm, Devinno.UI.Styling.DvTheme.Light) { UseNativeEditor = true };
        s.Layout(W, H);
        Tap(s, Cell(0, 1));
        Type(s, "A");
        Tap(s, new SKPoint(X(0), 240 + Y(2))); // 둘째 그리드(240~480) 행 2
        Assert.Equal("A", vm.Rows[1].Name);
        Assert.Null(Spec(s, 0).EditMark);
        Assert.Equal(2, Spec(s, 1).EditMark!.Key);
        Assert.True(s.Edit.IsEditing);
    }

    [Fact]
    public void Gate_invoke_failure_of_any_kind_is_a_reject()
    {
        // Invoke 자체 예외(인수 타입 불일치 = ArgumentException)도 거절 — TargetInvocationException 만이 아님
        var vm = new EditVm();
        var s = Surface(vm);
        var slot = new GridCellSlot(s, GridBox(s), 2, vm.Rows[1]);
        Assert.Equal(CommitResult.Rejected, slot.Write("abc"));
        Assert.Empty(vm.SetPointGate);
        Assert.Equal(21, vm.Rows[1].SetPoint);
    }

    // ── 바깥 스크롤 상자 안 그리드: 셀 기준으로 드러낸다(그리드 전체 기준이면 페이지가 튀고 셀이 가려진다)
    private static DvSurface Nested(EditVm vm, float outerScrollY)
    {
        var markup = "<DvScreen style=\"padding:0; gap:0; display:flex; flex-direction:column; align-items:stretch\">" +
                     "<DvStack style=\"height:400; overflow:scroll; gap:0; padding:0\">" +
                     GridEditFixture.Grid().Replace("height:240", "height:800") + "</DvStack></DvScreen>";
        var s = new DvSurface(markup, vm, DvTheme.Light) { UseNativeEditor = true };
        s.Layout(W, H);
        s.Root!.Children[0].ScrollY = outerScrollY;
        s.Layout(W, H);
        return s;
    }

    [Fact]
    public void Visible_cell_in_scrolled_outer_box_does_not_move_the_outer_box()
    {
        var s = Nested(new EditVm(), 300);
        Tap(s, new SKPoint(X(0), 40 + 48 * 7 + 24 - 300)); // 행 7 = 화면 76~124
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(300, s.Root!.Children[0].ScrollY);
        Assert.Equal(SKRect.Create(60, 76, 100, 48), s.Edit.Rect);
    }

    [Fact]
    public void Partly_hidden_cell_scrolls_the_outer_box_just_enough()
    {
        var s = Nested(new EditVm(), 0);
        Tap(s, new SKPoint(X(0), 390)); // 행 7 = 376~424, 바깥 뷰포트 0~400
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(24, s.Root!.Children[0].ScrollY);
        Assert.Equal(SKRect.Create(60, 352, 100, 48), s.Edit.Rect);
    }
}
