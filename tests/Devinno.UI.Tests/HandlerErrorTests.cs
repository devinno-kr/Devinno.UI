using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Alarms;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 앱(VM) 코드 예외 방어 — setter·핸들러가 던져도 입력 처리 밖으로 새지 않고, Trace 오류 + DvSurface.HandlerError 로 1번 알린다.
public class HandlerErrorTests
{
    public sealed class Vm
    {
        public bool ThrowOnSet = true;
        private bool _flag;
        private double _level = 40;
        private string _name = "펌프 A";
        private DateTime _at = new(2026, 10, 14, 9, 30, 0);

        public bool Flag { get => _flag; set { if (ThrowOnSet) throw new InvalidOperationException("flag-set"); _flag = value; } }
        public double Level { get => _level; set { if (ThrowOnSet) throw new InvalidOperationException("level-set"); _level = value; } }
        public string Name { get => _name; set { if (ThrowOnSet) throw new InvalidOperationException("name-set"); _name = value; } }
        public DateTime At { get => _at; set { if (ThrowOnSet) throw new InvalidOperationException("at-set"); _at = value; } }
        public int Count { get; set; } = 2;
        public int CountSetThrows { get => Count; set => throw new InvalidOperationException("count-set"); }

        public void Boom() => throw new InvalidOperationException("boom");
        public bool GateBoom(bool proposed) => throw new InvalidOperationException("gate-boom");

        public AlarmLog Log { get; } = new(50);
        public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
        public void AckBoom(AlarmRow row) => throw new InvalidOperationException("ack-boom");
    }

    public sealed class Row
    {
        public Row(int id) => Id = id;
        public int Id { get; }
        public string Name { get => "R" + Id; set => throw new InvalidOperationException("row-name-set"); }
        public bool On { get => false; set => throw new InvalidOperationException("row-on-set"); }
    }

    public sealed class GridVm
    {
        public List<Row> Rows { get; } = Enumerable.Range(0, 5).Select(i => new Row(i)).ToList();
        public List<Row> Picked { get => new(); set => throw new InvalidOperationException("picked-set"); }
        public void RowBoom(Row r) => throw new InvalidOperationException("row-boom");
        public void CellBoom(Row r) => throw new InvalidOperationException("cell-boom");
    }

    private static List<DvHandlerErrorEventArgs> Watch(DvSurface s)
    {
        var list = new List<DvHandlerErrorEventArgs>();
        s.HandlerError += (_, e) => list.Add(e);
        return list;
    }

    private static DvSurface Surface(string inner, object vm, bool native = true, float w = 400, float h = 300)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:0; gap:0\">{inner}</DvScreen>", vm) { UseNativeEditor = native };
        s.Layout(w, h);
        return s;
    }

    private static SKPoint Mid(SKRect r) => new(r.MidX, r.MidY);

    private static void Tap(DvSurface s, SKPoint p, float w = 400, float h = 300)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(w, h);
    }

    private static void AssertOne(List<DvHandlerErrorEventArgs> errors, string kind, string? member, string? tag, string message)
    {
        var e = Assert.Single(errors);
        Assert.Equal(kind, e.Kind);
        Assert.Equal(member, e.Member);
        Assert.Equal(tag, e.Tag);
        Assert.IsType<InvalidOperationException>(e.Exception); // TargetInvocationException 이 아니라 안쪽 예외
        Assert.Equal(message, e.Exception.Message);
    }

    // ── 핸들러
    [Fact]
    public void Onclick_exception_does_not_escape_and_is_reported_once()
    {
        var s = Surface("<DvButton text=\"x\" onclick=\"Boom\" style=\"width:100; height:40\"/>", new Vm());
        var errors = Watch(s);
        Tap(s, new SKPoint(50, 20));
        AssertOne(errors, "onclick", "Boom", "DvButton", "boom");
    }

    [Fact]
    public void Onack_exception_does_not_escape_and_is_reported()
    {
        var vm = new Vm();
        vm.Log.Raise(AlarmPriority.Critical, "c");
        var s = Surface("<DvAlarmBanner items=\"{Alarms}\" onack=\"AckBoom\" style=\"width:400; height:40\"/>", vm);
        var errors = Watch(s);
        Tap(s, new SKPoint(200, 20));
        AssertOne(errors, "onack", "AckBoom", "DvAlarmBanner", "ack-boom");
    }

    [Fact]
    public void Onbeforecommit_exception_rejects_and_is_reported()
    {
        var vm = new Vm { ThrowOnSet = false };
        var s = Surface("<DvToggle value=\"{Bind Flag}\" onbeforecommit=\"GateBoom\"/>", vm);
        var errors = Watch(s);
        Tap(s, Mid(s.Root!.Children[0].Rect));
        Assert.False(vm.Flag);
        AssertOne(errors, "onbeforecommit", "GateBoom", "DvToggle", "gate-boom");
    }

    // ── setter
    [Fact]
    public void Toggle_setter_exception_leaves_value_and_is_reported()
    {
        var vm = new Vm();
        var s = Surface("<DvToggle value=\"{Bind Flag}\"/>", vm);
        var errors = Watch(s);
        Tap(s, Mid(s.Root!.Children[0].Rect));
        Assert.False(vm.Flag);
        Assert.False(s.Root!.Children[0].ToggleOn); // 다시 빌드되어 VM 값으로 보임
        AssertOne(errors, "setter", "Flag", "DvToggle", "flag-set");
    }

    [Fact]
    public void Knob_setter_exception_leaves_value_and_is_reported()
    {
        var vm = new Vm();
        var s = new DvSurface("<DvScreen><DvKnob value=\"{Bind Level}\" style=\"width:100; height:100\" /></DvScreen>", vm);
        s.Layout(300, 300);
        var errors = Watch(s);
        s.PointerDown(new SKPoint(50, 10));
        s.PointerMove(new SKPoint(90, 50));
        s.PointerUp(new SKPoint(90, 50));
        s.Layout(300, 300);
        Assert.Equal(40, vm.Level);
        Assert.Equal(40, s.Root!.Children[0].KnobValue);
        AssertOne(errors, "setter", "Level", "DvKnob", "level-set");
    }

    [Fact]
    public void Input_setter_exception_keeps_session_with_write_failed_message()
    {
        var vm = new Vm();
        var s = Surface("<DvInput value=\"{Bind Name}\"/>", vm);
        var errors = Watch(s);
        Tap(s, new SKPoint(50, 20));
        var id = s.Edit.SessionId;
        s.SetEditText(id, "펌프 B");

        Assert.False(s.CommitEdit(id, DvEditExit.Enter));
        Assert.True(s.Edit.IsEditing);
        Assert.Equal("Could not save the value", s.Edit.Error);
        Assert.Equal("펌프 A", vm.Name);
        AssertOne(errors, "setter", "Name", "DvInput", "name-set");
    }

    [Fact]
    public void Input_write_failed_message_uses_the_strings_table()
    {
        var vm = new Vm();
        var s = Surface("<DvInput value=\"{Bind Name}\"/>", vm);
        var strings = new Devinno.UI.Localization.DvStrings();
        strings.Add("en", new Dictionary<string, string> { ["dv.input.write-failed"] = "Could not save" });
        strings.Language = "en";
        s.Strings = strings;
        s.Layout(400, 300);
        Tap(s, new SKPoint(50, 20));
        s.SetEditText(s.Edit.SessionId, "x");
        Assert.False(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.Equal("Could not save", s.Edit.Error);
    }

    [Fact]
    public void Picker_setter_exception_keeps_popup_open()
    {
        var vm = new Vm();
        var s = Surface("<DvDateTimeInput value=\"{Bind At}\"/>", vm, w: 800, h: 600);
        var errors = Watch(s);
        Tap(s, Mid(s.Root!.Children[0].Rect), 800, 600);
        var p = Assert.IsType<DateTimePopup>(s.TopPopup);
        var day = Enumerable.Range(0, 42).First(i => p.State.CellDate(i) == new DateOnly(2026, 10, 20));
        Tap(s, Mid(p.DayRect(day)), 800, 600);
        Tap(s, Mid(p.OkRect), 800, 600);
        Assert.Same(p, s.TopPopup);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 0), vm.At);
        AssertOne(errors, "setter", "At", "DvDateTimeInput", "at-set");
    }

    [Fact]
    public void Select_setter_exception_is_handled_like_a_rejection()
    {
        var vm = new Vm();
        var s = Surface("<DvSelect value=\"{Bind CountSetThrows}\"><DvOption value=\"1\"/><DvOption value=\"2\"/><DvOption value=\"3\"/></DvSelect>", vm);
        var errors = Watch(s);
        Tap(s, Mid(s.Root!.Children[0].Rect));
        var r = s.Dropdown!.ItemRect(2);
        Tap(s, Mid(r));
        Assert.Equal(2, vm.Count);
        AssertOne(errors, "setter", "CountSetThrows", "DvSelect", "count-set");
    }

    // ── 그리드
    private const string GridCols =
        "<DvColumn header=\"Id\" field=\"Id\" width=\"60\"/>" +
        "<DvColumn header=\"이름\" field=\"Name\" width=\"100\" cell-type=\"input\"/>" +
        "<DvColumn header=\"켬\" field=\"On\" width=\"60\" cell-type=\"check\"/>" +
        "<DvColumn header=\"\" width=\"80\" cell-type=\"button\" text=\"보기\" onclick=\"CellBoom\"/>";

    private static DvSurface Grid(GridVm vm, string attrs = "items=\"{Rows}\" row-key=\"Id\"")
        => Surface($"<DvDataGrid {attrs} style=\"height:300\">{GridCols}</DvDataGrid>", vm, w: 600, h: 400);

    // 헤더 40, 행 48 → 행 r 가운데 y = 64 + 48r. 열 가운데 x: Id 30 · 이름 110 · 켬 190 · 버튼 260
    private static SKPoint GridCell(float x, int row) => new(x, 64 + 48 * row);

    [Fact]
    public void Grid_input_cell_setter_exception_keeps_session_with_write_failed_message()
    {
        var vm = new GridVm();
        var s = Grid(vm);
        var errors = Watch(s);
        Tap(s, GridCell(110, 1), 600, 400);
        s.SetEditText(s.Edit.SessionId, "새 이름");
        Assert.False(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.True(s.Edit.IsEditing);
        Assert.Equal("Could not save the value", s.Edit.Error);
        AssertOne(errors, "setter", "Name", "DvDataGrid", "row-name-set");
    }

    [Fact]
    public void Grid_check_cell_setter_exception_is_reported()
    {
        var vm = new GridVm();
        var s = Grid(vm);
        var errors = Watch(s);
        Tap(s, GridCell(190, 0), 600, 400);
        Assert.False(vm.Rows[0].On);
        AssertOne(errors, "setter", "On", "DvDataGrid", "row-on-set");
    }

    [Fact]
    public void Grid_button_cell_and_row_click_exceptions_are_reported()
    {
        var vm = new GridVm();
        var s = Grid(vm, "items=\"{Rows}\" row-key=\"Id\" onrowclick=\"RowBoom\"");
        var errors = Watch(s);
        Tap(s, GridCell(260, 0), 600, 400);
        AssertOne(errors, "onclick", "CellBoom", "DvDataGrid", "cell-boom");
        errors.Clear();
        Tap(s, GridCell(30, 0), 600, 400);
        AssertOne(errors, "onrowclick", "RowBoom", "DvDataGrid", "row-boom");
    }

    [Fact]
    public void Grid_selection_setter_exception_keeps_previous_selection()
    {
        var vm = new GridVm();
        var s = Grid(vm, "items=\"{Rows}\" row-key=\"Id\" selection-mode=\"single\" selection=\"{Bind Picked}\"");
        var errors = Watch(s);
        Tap(s, GridCell(30, 1), 600, 400);
        Assert.Empty(s.Root!.Children[0].Grid!.SelectedItems);
        AssertOne(errors, "setter", "Picked", "DvDataGrid", "picked-set");
    }

    // ── 진단 경로
    private sealed class Capture : TraceListener
    {
        public readonly List<string> Lines = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { lock (Lines) Lines.Add(message ?? ""); }
    }

    [Fact]
    public void Trace_error_is_written_even_without_subscribers()
    {
        var cap = new Capture();
        Trace.Listeners.Add(cap);
        try
        {
            var s = Surface("<DvButton text=\"x\" onclick=\"Boom\" style=\"width:100; height:40\"/>", new Vm());
            Tap(s, new SKPoint(50, 20));
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        lock (cap.Lines)
            Assert.Contains(cap.Lines, l => l.Contains("Boom") && l.Contains("boom"));
    }

    [Fact]
    public void Subscriber_exception_is_swallowed_and_later_subscribers_still_run()
    {
        var s = Surface("<DvButton text=\"x\" onclick=\"Boom\" style=\"width:100; height:40\"/>", new Vm());
        s.HandlerError += (_, _) => throw new InvalidOperationException("subscriber");
        var errors = Watch(s);
        Tap(s, new SKPoint(50, 20));
        Assert.Single(errors);
    }

    // ── DvApp 페이지 칸 → 루트 화면·앱
    private sealed class BoomPage() : MarkupPage(
        "<DvScreen style=\"padding:0; gap:0\"><DvButton text=\"x\" onclick=\"Boom\" style=\"width:100; height:40\"/></DvScreen>")
    {
        public void Boom() => throw new InvalidOperationException("body-boom");
    }

    [Fact]
    public void Page_surface_errors_also_reach_the_app_root()
    {
        var page = new BoomPage();
        var (app, root) = TestApp.WithLeft(page, 100, 400, 300);
        var top = Watch(root);
        var body = Watch(page.Surface!);
        var fromApp = new List<DvHandlerErrorEventArgs>();
        app.HandlerError += (_, e) => fromApp.Add(e);

        Tap(root, new SKPoint(150, 20), 400, 300);
        AssertOne(top, "onclick", "Boom", "DvButton", "body-boom");
        Assert.Single(body);
        Assert.Same(top[0], body[0]);
        Assert.Same(top[0], Assert.Single(fromApp));
    }
}
