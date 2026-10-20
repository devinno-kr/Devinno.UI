using System.Diagnostics;
using System.Globalization;
using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;
using static GridEditFixture;

// 3d-1 편집 셀 — 열 파싱·select 항목·편집 가능 판정·표시
public class GridEditBuildTests
{
    private static GridSpec Spec(DvSurface s) => GridBox(s).Grid!;

    [Fact]
    public void Edit_cell_types_and_attributes_parse()
    {
        var c = Spec(Surface(new EditVm())).Columns;
        Assert.Equal(new[] { GridCellType.Text, GridCellType.Input, GridCellType.Input, GridCellType.Select, GridCellType.DateTime, GridCellType.Color },
            c.Select(x => x.CellType));
        Assert.Equal(0d, c[2].Min);
        Assert.Equal(120d, c[2].Max);
        Assert.Equal("℃", c[2].Unit);
        Assert.Equal("0.0", c[2].Format);
        Assert.Equal("CheckSetPoint", c[2].OnBeforeCommit);
        Assert.Equal("MM-dd HH:mm", c[4].Format);
        Assert.Equal(PickerMode.DateTime, c[4].DateTimeMode);
        Assert.False(c[4].ShowSeconds);
        Assert.True(c[1].IsEditCell);
        Assert.False(c[0].IsEditCell);
    }

    [Fact]
    public void Datetime_mode_and_seconds_follow_dvdatetimeinput_rules()
    {
        var cols = IdCol +
                   "<DvColumn header=\"a\" field=\"ReserveAt\" width=\"100\" cell-type=\"datetime\" mode=\"date\"/>" +
                   "<DvColumn header=\"b\" field=\"ReserveAt\" width=\"100\" cell-type=\"datetime\" mode=\"time\" show-seconds=\"true\"/>" +
                   "<DvColumn header=\"c\" field=\"Day\" width=\"100\" cell-type=\"datetime\"/>";
        var c = Spec(Surface(new EditVm(), cols)).Columns;
        Assert.Equal(PickerMode.Date, c[1].DateTimeMode);
        Assert.Equal(PickerMode.Time, c[2].DateTimeMode);
        Assert.True(c[2].ShowSeconds);
        Assert.Equal(PickerMode.Date, c[3].DateTimeMode); // DateOnly → date
    }

    [Fact]
    public void Select_options_come_from_children_items_or_enum()
    {
        var cols = IdCol +
                   "<DvColumn header=\"모드\" field=\"Mode\" width=\"100\" cell-type=\"select\">" +
                   "<DvOption value=\"auto\" text=\"자동\" icon=\"fa-robot\"/><DvOption value=\"manual\" text=\"수동\"/></DvColumn>" +
                   "<DvColumn header=\"레시피\" field=\"RecipeId\" width=\"100\" cell-type=\"select\" items=\"{Catalog}\" item-value=\"Id\" item-text=\"Name\"/>" +
                   "<DvColumn header=\"종류\" field=\"Kind\" width=\"100\" cell-type=\"select\"/>";
        var vm = new EditVm();
        vm.Rows[0].RecipeId = 2;
        vm.Rows[0].Kind = MachineKind.Fan;
        var c = Spec(Surface(vm, cols)).Columns;
        Assert.Equal(new[] { "자동", "수동" }, c[1].Options.Select(o => o.Text));
        Assert.Equal("fa-robot", c[1].Options[0].Icon);
        Assert.Equal(new[] { "A", "B", "C" }, c[2].Options.Select(o => o.Text));
        Assert.Equal(new[] { "Pump", "Valve", "Fan" }, c[3].Options.Select(o => o.Text));
        Assert.Equal("수동", c[1].CellText(vm.Rows[1]));
        Assert.Equal("B", c[2].CellText(vm.Rows[0]));
        Assert.Equal("Fan", c[3].CellText(vm.Rows[0]));
        Assert.Equal(1, c[2].OptionIndex(2));
        vm.Rows[2].Mode = "stop";
        Assert.Equal("", c[1].CellText(vm.Rows[2])); // 맞는 항목 없음 = 빈 칸
    }

    [Fact]
    public void Datetime_and_color_cells_show_picker_text()
    {
        var vm = new EditVm();
        vm.Rows[0].Pen = "#22c55e";
        var c = Spec(Surface(vm)).Columns;
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 0).ToString("MM-dd HH:mm", CultureInfo.CurrentCulture), c[4].CellText(vm.Rows[0]));
        Assert.Equal("#22C55E", c[5].CellText(vm.Rows[0]));
        Assert.Equal("20.0", c[2].CellText(vm.Rows[0])); // input = 글자 셀 규칙(format)
    }

    [Fact]
    public void Picker_and_select_cells_align_left_input_numbers_right()
    {
        var cols = IdCol +
                   "<DvColumn header=\"설정값\" field=\"SetPoint\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"레시피\" field=\"RecipeId\" width=\"100\" cell-type=\"select\" items=\"{Catalog}\" item-value=\"Id\" item-text=\"Name\"/>";
        var c = Spec(Surface(new EditVm(), cols)).Columns;
        Assert.Equal(TextAlign.Right, c[1].EffectiveAlign);
        Assert.Equal(TextAlign.Left, c[2].EffectiveAlign); // int 속성이어도 이름을 보이므로 왼쪽
    }

    [Fact]
    public void Can_edit_rejects_readonly_no_setter_struct_path_and_mismatch()
    {
        var cols = IdCol +
                   "<DvColumn header=\"a\" field=\"Name\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"b\" field=\"Name\" width=\"100\" cell-type=\"input\" readonly=\"true\"/>" +
                   "<DvColumn header=\"c\" field=\"Computed\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"d\" field=\"Pos.X\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"e\" field=\"ReserveAt\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"f\" field=\"Name\" width=\"100\" cell-type=\"datetime\"/>" +
                   "<DvColumn header=\"g\" field=\"Fill\" width=\"100\" cell-type=\"color\"/>" +
                   "<DvColumn header=\"h\" field=\"Count\" width=\"100\" cell-type=\"input\"/>" +
                   "<DvColumn header=\"i\" field=\"Name\" width=\"100\"/>";
        var vm = new EditVm();
        var c = Spec(Surface(vm, cols)).Columns;
        Assert.Equal(new[] { false, true, false, false, false, false, false, true, true, false },
            c.Select(x => x.CanEdit(vm.Rows[0])));
    }

    [Fact]
    public void Color_cell_paints_a_swatch()
    {
        var vm = new EditVm();
        vm.Rows[0].Pen = "#22C55E";
        using var bmp = Render(Surface(vm));
        // 색 열 460~560, 왼쪽 여백 12 → 견본 472~492 × (64±10)
        Assert.Equal(new SKColor(0x22, 0xc5, 0x5e), bmp.GetPixel(482, 64));
    }

    private sealed class Capture(string marker) : TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && message.Contains(marker))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message)
            => WriteLine(message);
    }

    // F3 — 해석 못 한 min·max 는 조용히 무시하지 않고 경고 1회
    [Fact]
    public void Unparsable_min_max_warn_once()
    {
        var cols = IdCol + "<DvColumn header=\"설정값\" field=\"SetPoint\" width=\"100\" cell-type=\"input\" min=\"f3-lo\" max=\"f3-hi\"/>";
        var cap = new Capture("f3-");
        Trace.Listeners.Add(cap);
        try
        {
            var c = Spec(Surface(new EditVm(), cols)).Columns;
            Surface(new EditVm(), cols); // 다시 빌드해도 추가 경고 없음
            Assert.Null(c[1].Min);
            Assert.Null(c[1].Max);
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        Assert.Equal(2, cap.Lines.Count);
        Assert.Contains(cap.Lines, l => l.Contains("min") && l.Contains("f3-lo"));
        Assert.Contains(cap.Lines, l => l.Contains("max") && l.Contains("f3-hi"));
    }
}
