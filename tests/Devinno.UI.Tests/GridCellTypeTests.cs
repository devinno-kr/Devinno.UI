using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3c 즉시 셀 — 체크박스(즉시 쓰기)·램프(표시·점멸)·버튼(행 항목 호출)
public class GridCellTypeTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool On { get; set; }
        public bool? Maybe { get; set; }
        public int Count { get; set; }
        public string? State { get; set; }
        public bool CanAck { get; set; } = true;
        public bool Computed => Id % 2 == 0; // setter 없음
    }

    public class Vm
    {
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 10).Select(i => new Row { Id = i, Name = "R" + i }).ToList();
        public List<Row> Picked { get; set; } = new();
        public List<Row> Acks { get; } = new();
        public List<Row> Clicked { get; } = new();
        public bool Allow = true;
        public List<(Row, bool)> Gate { get; } = new();
        public void Ack(Row r) => Acks.Add(r);
        public void Inspect(Row r) => Clicked.Add(r);
        public bool CanToggle(Row r, bool v) { Gate.Add((r, v)); return Allow; }
        public bool Wrong(Row r, int v) => true;                       // 시그니처 불일치
        public bool Throws(Row r, bool v) => throw new InvalidOperationException("거절");
        public void Boom(Row r) => throw new InvalidOperationException("버튼 실패");
    }

    public sealed class BoolGateVm : Vm
    {
        public List<bool> Seen { get; } = new();
        public bool OnlyBool(bool v) { Seen.Add(v); return false; }
        public int Plain;
        public void Hit() => Plain++;
    }

    // Id 60 | 체크 60 | 램프 60 | 버튼 60 | 이름 *  → 셀 가운데 x = 90, 150, 210
    private const string Cols =
        "<DvColumn header=\"Id\" field=\"Id\" width=\"60\" sortable=\"true\"/>" +
        "<DvColumn header=\"사용\" field=\"On\" width=\"60\" cell-type=\"check\"/>" +
        "<DvColumn header=\"운전\" field=\"On\" width=\"60\" cell-type=\"lamp\"/>" +
        "<DvColumn header=\"확인\" width=\"60\" cell-type=\"button\" text=\"ACK\" onclick=\"Ack\" enabled=\"CanAck\"/>" +
        "<DvColumn header=\"이름\" field=\"Name\"/>";

    private const string Selectable = "items=\"{Rows}\" selection-mode=\"single\" selection=\"{Bind Picked}\" onrowclick=\"Inspect\"";

    private static DvSurface Surface(Vm vm, string cols = Cols, string attrs = Selectable)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen(attrs, cols), vm, DvTheme.Light);
        s.Layout(300, 300);
        return s;
    }

    private static float Y(int row) => 40 + 48 * row + 24;

    private static void Tap(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        s.Layout(300, 300);
    }

    private static SKBitmap Render(DvSurface s, long ts = 0)
    {
        var bmp = new SKBitmap(300, 300);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 300, 300, ts);
        return bmp;
    }

    private static SKColor Token(string key)
    {
        Assert.True(DvTheme.Light.TryGet(key, out var raw));
        Assert.True(DvColor.TryParse(raw, out var c));
        return c.ToSK();
    }

    // ── 그리기

    [Fact]
    public void Check_box_is_filled_when_true()
    {
        var vm = new Vm();
        vm.Rows[0].On = true;
        using var bmp = Render(Surface(vm));
        Assert.Equal(Token("primary"), bmp.GetPixel(83, (int)Y(0) - 7));   // 체크된 상자 안(체크 표시 밖)
        Assert.NotEqual(Token("primary"), bmp.GetPixel(83, (int)Y(1) - 7));
    }

    [Fact]
    public void Lamp_uses_on_color_state_tokens_and_off_color()
    {
        var vm = new Vm();
        vm.Rows[0].On = true;
        using var bmp = Render(Surface(vm));
        Assert.Equal(Token("state-run"), bmp.GetPixel(150, (int)Y(0)));
        Assert.Equal(new SKColor(0x3a, 0x3f, 0x47), bmp.GetPixel(150, (int)Y(1)));

        vm.Rows[0].State = "fault";
        vm.Rows[1].State = "nope";
        using var s2 = Render(Surface(vm, "<DvColumn field=\"Id\" width=\"120\"/><DvColumn field=\"State\" width=\"60\" cell-type=\"lamp\"/>" +
                                          "<DvColumn field=\"On\" width=\"60\" cell-type=\"lamp\" on-color=\"#ff0000\"/>"));
        Assert.Equal(Token("state-fault"), s2.GetPixel(150, (int)Y(0)));
        Assert.Equal(new SKColor(0x3a, 0x3f, 0x47), s2.GetPixel(150, (int)Y(1)));
        Assert.Equal(new SKColor(0xff, 0, 0), s2.GetPixel(210, (int)Y(0)));
    }

    [Fact]
    public void Blinking_lamp_follows_the_common_phase_and_schedules_frames()
    {
        var vm = new Vm();
        vm.Rows[0].On = true;
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"120\"/><DvColumn field=\"On\" width=\"60\" cell-type=\"lamp\" blink=\"true\"/>");
        using (var on = Render(s, 0))
            Assert.Equal(Token("state-run"), on.GetPixel(150, (int)Y(0)));
        s.Repaint();
        using (var off = Render(s, 500))
            Assert.Equal(new SKColor(0x3a, 0x3f, 0x47), off.GetPixel(150, (int)Y(0)));
        Assert.Equal(500, s.NextAnimationDue(100));
        Assert.Null(Surface(vm).NextAnimationDue(100)); // 점멸 램프 없음
    }

    [Fact]
    public void Button_uses_variant_and_dims_when_disabled()
    {
        var vm = new Vm();
        vm.Rows[1].CanAck = false;
        using var bmp = Render(Surface(vm));
        Assert.Equal(Token("primary"), bmp.GetPixel(188, (int)Y(0)));
        Assert.Equal(Token("surface-raised"), bmp.GetPixel(188, (int)Y(1)));
    }

    // ── 입력

    [Fact]
    public void Check_tap_toggles_and_writes_without_selecting()
    {
        var vm = new Vm();
        var s = Surface(vm);
        Tap(s, 90, Y(0));
        Assert.True(vm.Rows[0].On);
        Assert.Empty(vm.Picked);
        Assert.Empty(vm.Clicked);
        Tap(s, 90, Y(0));
        Assert.False(vm.Rows[0].On);
    }

    [Fact]
    public void Check_null_becomes_true()
    {
        var vm = new Vm();
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"Maybe\" width=\"60\" cell-type=\"check\"/>");
        Tap(s, 90, Y(2));
        Assert.True(vm.Rows[2].Maybe);
    }

    [Fact]
    public void Onbeforecommit_with_item_can_reject()
    {
        var vm = new Vm { Allow = false };
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"On\" width=\"60\" cell-type=\"check\" onbeforecommit=\"CanToggle\"/>");
        Tap(s, 90, Y(1));
        Assert.False(vm.Rows[1].On);
        Assert.Equal((vm.Rows[1], true), Assert.Single(vm.Gate));
        vm.Allow = true;
        Tap(s, 90, Y(1));
        Assert.True(vm.Rows[1].On);
    }

    [Fact]
    public void Onbeforecommit_bool_only_form_is_used_when_no_item_form()
    {
        var vm = new BoolGateVm();
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"On\" width=\"60\" cell-type=\"check\" onbeforecommit=\"OnlyBool\"/>");
        Tap(s, 90, Y(0));
        Assert.Equal(new[] { true }, vm.Seen);
        Assert.False(vm.Rows[0].On);
    }

    [Fact]
    public void Mismatched_or_throwing_gate_rejects_the_write()
    {
        var vm = new Vm();
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"On\" width=\"60\" cell-type=\"check\" onbeforecommit=\"Wrong\"/>" +
                            "<DvColumn field=\"On\" width=\"60\" cell-type=\"check\" onbeforecommit=\"Nope\"/>" +
                            "<DvColumn field=\"On\" width=\"60\" cell-type=\"check\" onbeforecommit=\"Throws\"/>");
        Tap(s, 90, Y(0));
        Tap(s, 150, Y(0));
        Tap(s, 210, Y(0)); // 예외가 PointerUp 밖으로 나오지 않는다
        Assert.False(vm.Rows[0].On);
    }

    [Fact]
    public void Throwing_button_handler_does_not_escape()
    {
        var vm = new Vm();
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn width=\"60\" cell-type=\"button\" text=\"x\" onclick=\"Boom\"/>");
        Tap(s, 90, Y(0));
        Tap(s, 90, Y(1)); // 눌림 상태가 남지 않아 다음 탭도 처리된다(예외 없음)
    }

    [Fact]
    public void Read_only_property_is_not_written_and_gate_is_not_asked()
    {
        var vm = new Vm();
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"Computed\" width=\"60\" cell-type=\"check\" onbeforecommit=\"CanToggle\"/>");
        Tap(s, 90, Y(0));
        Assert.Empty(vm.Gate);
    }

    [Fact]
    public void Readonly_and_non_bool_check_columns_do_not_write()
    {
        var vm = new Vm();
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"On\" width=\"60\" cell-type=\"check\" readonly=\"true\"/>" +
                            "<DvColumn field=\"Count\" width=\"60\" cell-type=\"check\"/>");
        Tap(s, 90, Y(0));
        Tap(s, 150, Y(0));
        Assert.False(vm.Rows[0].On);
        Assert.Equal(0, vm.Rows[0].Count);
    }

    [Fact]
    public void Check_tap_writes_to_the_displayed_row_when_sorted()
    {
        var vm = new Vm();
        var s = Surface(vm);
        Tap(s, 30, 20); // Id 오름
        Tap(s, 30, 20); // Id 내림 → 첫 행 = Id 9
        Tap(s, 90, Y(0));
        Assert.True(vm.Rows[9].On);
        Assert.False(vm.Rows[0].On);
    }

    [Fact]
    public void Drag_over_a_check_cell_does_not_toggle()
    {
        var vm = new Vm();
        var s = Surface(vm);
        s.PointerDown(new SKPoint(90, 200));
        s.PointerMove(new SKPoint(90, 150));
        s.PointerUp(new SKPoint(90, 150));
        Assert.DoesNotContain(vm.Rows, r => r.On);
    }

    [Fact]
    public void Button_calls_with_the_item_and_respects_enabled()
    {
        var vm = new Vm();
        vm.Rows[1].CanAck = false;
        var s = Surface(vm);
        Tap(s, 210, Y(0));
        Tap(s, 210, Y(1));
        Assert.Equal(new[] { vm.Rows[0] }, vm.Acks);
        Assert.Empty(vm.Picked);
        Assert.Empty(vm.Clicked);
    }

    [Fact]
    public void Button_falls_back_to_no_argument_method()
    {
        var vm = new BoolGateVm();
        var s = Surface(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn width=\"60\" cell-type=\"button\" text=\"Go\" onclick=\"Hit\"/>");
        Tap(s, 90, Y(0));
        Assert.Equal(1, vm.Plain);
    }

    [Fact]
    public void Lamp_cell_tap_is_a_normal_row_tap()
    {
        var vm = new Vm();
        var s = Surface(vm);
        Tap(s, 150, Y(2));
        Assert.Equal(new[] { vm.Rows[2] }, vm.Picked);
        Assert.Equal(new[] { vm.Rows[2] }, vm.Clicked);
    }
}
