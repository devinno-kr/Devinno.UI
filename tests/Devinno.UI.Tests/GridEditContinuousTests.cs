using Devinno.UI;
using Devinno.UI.Grid;
using SkiaSharp;
using Xunit;
using static GridEditFixture;

// 3d-1 연속 입력 — 네이티브 편집기만 Enter 아래·Tab 오른쪽·ShiftTab 왼쪽, 건너뜀·끝·스크롤·거절
public class GridEditContinuousTests
{
    private static void Type(DvSurface s, string text) => s.SetEditText(s.Edit.SessionId, text);
    private static bool Commit(DvSurface s, DvEditExit exit) => s.CommitEdit(s.Edit.SessionId, exit);
    private static GridSpec Spec(DvSurface s) => GridBox(s).Grid!;

    // 이름 | 이름(readonly) | 설정값 | 모드(select) | 수  → Tab 은 이름 → 설정값 → 수
    private const string TabCols = IdCol +
        "<DvColumn header=\"이름\" field=\"Name\" width=\"100\" cell-type=\"input\"/>" +
        "<DvColumn header=\"ro\" field=\"Name\" width=\"100\" cell-type=\"input\" readonly=\"true\"/>" +
        "<DvColumn header=\"설정값\" field=\"SetPoint\" width=\"100\" cell-type=\"input\"/>" +
        "<DvColumn header=\"모드\" field=\"Mode\" width=\"100\" cell-type=\"select\"><DvOption value=\"auto\"/></DvColumn>" +
        "<DvColumn header=\"수\" field=\"Count\" width=\"100\" cell-type=\"input\"/>";

    [Fact]
    public void Enter_moves_down_the_same_column()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, Cell(0, 1));
        var first = s.Edit.SessionId;
        Type(s, "A");
        Assert.True(Commit(s, DvEditExit.Enter));
        Assert.Equal("A", vm.Rows[1].Name);
        Assert.True(s.Edit.IsEditing);
        Assert.NotEqual(first, s.Edit.SessionId);
        Assert.Equal("R2", s.Edit.InitialText);
        Assert.Equal(CellRect(0, 2), s.Edit.Rect);
    }

    private static int EditColumn(DvSurface s) => ((GridCellEditTarget)s.EditTarget!).Slot.Column;

    [Fact]
    public void Tab_and_shift_tab_skip_non_editable_and_non_input_columns()
    {
        // 열 번호: Id 0 · 이름 1 · ro 2 · 설정값 3 · 모드 4 · 수 5
        var s = Surface(new EditVm(), TabCols);
        Tap(s, Cell(0, 0));
        Assert.True(Commit(s, DvEditExit.Tab));
        Assert.Equal(3, EditColumn(s));           // 설정값 — readonly 건너뜀
        Assert.True(Commit(s, DvEditExit.Tab));
        Assert.Equal(5, EditColumn(s));           // 수 — select 건너뜀
        Assert.True(Commit(s, DvEditExit.ShiftTab));
        Assert.Equal(3, EditColumn(s));
        Assert.True(Commit(s, DvEditExit.Tab));
        Assert.True(Commit(s, DvEditExit.Tab));   // 마지막 input 열 → 종료
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Shift_tab_on_the_first_input_column_ends()
    {
        var s = Surface(new EditVm(), TabCols);
        Tap(s, Cell(0, 0));
        Assert.True(Commit(s, DvEditExit.ShiftTab));
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Enter_on_the_last_row_ends()
    {
        var s = Surface(new EditVm());
        var box = GridBox(s);
        box.ScrollY = box.MaxScrollY; // 280 → 행 9 = 192~240
        Tap(s, new SKPoint(X(0), 216));
        Assert.Equal("R9", s.Edit.InitialText);
        Assert.True(Commit(s, DvEditExit.Enter));
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Enter_scrolls_the_next_row_into_view()
    {
        var s = Surface(new EditVm());
        Tap(s, Cell(0, 3)); // 184~232
        Assert.True(Commit(s, DvEditExit.Enter));
        Assert.Equal(40, GridBox(s).ScrollY); // 행 4(192~240 내용 좌표)가 몸통(높이 200) 아래 끝에 맞춤
        Assert.Equal(SKRect.Create(60, 192, 100, 48), s.Edit.Rect);
    }

    [Fact]
    public void Rejected_value_stays_on_the_cell()
    {
        var s = Surface(new EditVm());
        Tap(s, Cell(1, 1));
        Type(s, "130");
        Assert.False(Commit(s, DvEditExit.Enter));
        Assert.Equal(CellRect(1, 1), s.Edit.Rect);
        Assert.NotNull(s.Edit.Error);
    }

    [Fact]
    public void Soft_keyboard_enter_commits_and_ends()
    {
        var vm = new EditVm();
        var s = Surface(vm, native: false);
        Tap(s, Cell(1, 1)); // 설정값 21
        s.KeyInput(DvKey.Backspace); // "2"
        s.KeyInput(DvKey.Enter);
        Assert.Equal(2, vm.Rows[1].SetPoint);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Soft_keyboard_tab_commits_and_ends()
    {
        // 새 서피스 — 앞 세션의 키보드가 내려가는 동안(모달)에는 누름이 삼켜지므로 따로
        var s = Surface(new EditVm(), native: false);
        Tap(s, Cell(0, 1));
        Assert.True(s.Edit.IsEditing);
        s.KeyInput(DvKey.Tab);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Editing_the_sorted_column_keeps_order_and_enter_goes_to_the_next_displayed_row()
    {
        var vm = new EditVm();
        var s = Surface(vm);
        Tap(s, new SKPoint(X(1), 20));
        Tap(s, new SKPoint(X(1), 20)); // 설정값 내림차순 → 화면 행 0 = Id 9
        Tap(s, Cell(1, 0));
        Assert.Equal("29", s.Edit.InitialText);
        Type(s, "1");
        Assert.True(Commit(s, DvEditExit.Enter));
        Assert.Equal(1, vm.Rows[9].SetPoint);
        Assert.Same(vm.Rows[9], Spec(s).ItemAt(0)); // 다시 정렬되지 않음
        Assert.Equal(8, Spec(s).EditMark!.Key);
        Assert.Equal("28", s.Edit.InitialText);
    }
}
