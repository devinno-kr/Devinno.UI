using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 3a-4·5 그리드 상호작용 — 선택·스냅샷·row-key·onrowclick·드래그·hover, Repaint
public class GridInteractionTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private static List<Row> Rows(int n = 100) => Enumerable.Range(0, n).Select(i => new Row { Id = i, Name = "R" + i }).ToList();

    public sealed class Vm
    {
        public List<Row> Rows { get; set; } = GridInteractionTests.Rows();
        public List<Row> Picked { get; set; } = new();
        public List<Row> Clicked { get; } = new();
        public void Inspect(Row r) => Clicked.Add(r);
    }

    public sealed class ArrVm
    {
        public List<Row> Rows { get; set; } = GridInteractionTests.Rows();
        public Row[] Picked { get; set; } = Array.Empty<Row>();
    }

    public sealed class RoVm
    {
        public List<Row> Rows { get; set; } = GridInteractionTests.Rows();
        public IReadOnlyList<Row> Picked { get; set; } = Array.Empty<Row>();
    }

    public sealed class OneVm
    {
        public List<Row> Rows { get; set; } = GridInteractionTests.Rows();
        public Row? Picked { get; set; }
    }

    public sealed class NoArgVm
    {
        public List<Row> Rows { get; set; } = GridInteractionTests.Rows();
        public int Hits;
        public void Inspect() => Hits++;
    }

    private static DvSurface Surface(string attrs, object vm)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen(attrs), vm);
        s.Layout(300, 300);
        return s;
    }

    private static LayoutBox Grid(DvSurface s) => s.Root!.Children[0];

    // 행 i 의 가운데(스크롤 0 기준) — 그리드 240 = 헤더 40 + 몸통 200, 화면 안은 행 0~3
    private static SKPoint RowPt(int i) => new(150, 40 + 48 * i + 24);

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(300, 300);
    }

    private const string Multi = "items=\"{Rows}\" selection-mode=\"multi\" selection=\"{Bind Picked}\"";
    private const string Single = "items=\"{Rows}\" selection-mode=\"single\" selection=\"{Bind Picked}\"";

    [Fact]
    public void Single_selects_the_tapped_row_only()
    {
        var vm = new Vm();
        var s = Surface(Single, vm);
        Tap(s, RowPt(2));
        Assert.Equal(new[] { vm.Rows[2] }, vm.Picked);
        Tap(s, RowPt(3));
        Assert.Equal(new[] { vm.Rows[3] }, vm.Picked);
        Tap(s, RowPt(3));
        Assert.Equal(new[] { vm.Rows[3] }, vm.Picked); // 다시 탭해도 유지
    }

    [Fact]
    public void Multi_toggles()
    {
        var vm = new Vm();
        var s = Surface(Multi, vm);
        Tap(s, RowPt(1));
        Tap(s, RowPt(3));
        Assert.Equal(new[] { vm.Rows[1], vm.Rows[3] }, vm.Picked);
        Tap(s, RowPt(1));
        Assert.Equal(new[] { vm.Rows[3] }, vm.Picked);
        Assert.True(Grid(s).Grid!.IsSelected(vm.Rows[3]));
        Assert.False(Grid(s).Grid!.IsSelected(vm.Rows[1]));
    }

    [Fact]
    public void Each_change_writes_a_new_snapshot()
    {
        var vm = new Vm();
        var s = Surface(Multi, vm);
        Tap(s, RowPt(0));
        var first = vm.Picked;
        Tap(s, RowPt(1));
        Assert.NotSame(first, vm.Picked);
        Assert.Single(first);
    }

    [Fact]
    public void Writes_array_readonly_list_and_single_item_properties()
    {
        var arr = new ArrVm();
        Tap(Surface(Multi, arr), RowPt(2));
        Assert.Equal(new[] { arr.Rows[2] }, arr.Picked);

        var ro = new RoVm();
        Tap(Surface(Multi, ro), RowPt(3));
        Assert.Equal(new[] { ro.Rows[3] }, ro.Picked);

        var one = new OneVm();
        Tap(Surface(Single, one), RowPt(1));
        Assert.Same(one.Rows[1], one.Picked);
    }

    [Fact]
    public void One_way_selection_is_kept_on_screen_but_not_written()
    {
        var vm = new Vm();
        var s = Surface("items=\"{Rows}\" selection-mode=\"multi\" selection=\"{Picked}\"", vm);
        Tap(s, RowPt(2));
        Assert.Empty(vm.Picked);
        Assert.True(Grid(s).Grid!.IsSelected(vm.Rows[2])); // 탭 뒤 재빌드를 거쳐도 화면 선택 유지
    }

    [Fact]
    public void One_way_selection_follows_the_vm_when_it_changes()
    {
        var vm = new Vm();
        var s = Surface("items=\"{Rows}\" selection-mode=\"single\" selection=\"{Picked}\"", vm);
        Tap(s, RowPt(1));
        vm.Picked = new List<Row> { vm.Rows[2] }; // VM 이 새 목록으로 선택을 바꿈
        s.Invalidate();
        s.Layout(300, 300);
        Assert.True(Grid(s).Grid!.IsSelected(vm.Rows[2]));
        Assert.False(Grid(s).Grid!.IsSelected(vm.Rows[1]));
    }

    [Fact]
    public void Wheel_clears_the_hover_row()
    {
        var vm = new Vm();
        var s = Surface(Single, vm);
        s.PointerMove(RowPt(1));
        Assert.Equal(1, Grid(s).Grid!.HoverRow);
        s.PointerWheel(RowPt(1), -2);
        Assert.Equal(-1, Grid(s).Grid!.HoverRow);
    }

    [Fact]
    public void Drag_scroll_clears_the_hover_row()
    {
        var vm = new Vm();
        var s = Surface(Single, vm);
        s.PointerMove(RowPt(1));
        s.PointerDown(RowPt(1));
        s.PointerMove(RowPt(0));
        Assert.Equal(-1, Grid(s).Grid!.HoverRow);
    }

    public sealed class HookRow
    {
        public Action? OnRead;
        public string Name { get { var h = OnRead; OnRead = null; h?.Invoke(); return "x"; } }
    }

    public sealed class HookVm
    {
        public List<HookRow> Rows { get; } = new() { new HookRow() };
    }

    [Fact]
    public void Repaint_requested_during_a_frame_is_not_lost()
    {
        var vm = new HookVm();
        var s = new DvSurface(GridLayoutBuildTests.Screen(cols: "<DvColumn field=\"Name\"/>"), vm);
        s.Layout(300, 300);
        using var bmp = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, 300, 300);

        vm.Rows[0].OnRead = s.Repaint; // 다음 그리기 도중(셀 값을 읽을 때) 다른 스레드가 Repaint 한 것과 같다
        s.Repaint();
        s.RenderTo(canvas, 300, 300);
        Assert.True(s.NeedsRender);
        s.RenderTo(canvas, 300, 300);
        Assert.False(s.NeedsRender);
    }

    [Fact]
    public void Selection_without_binding_is_local()
    {
        var vm = new Vm();
        var s = Surface("items=\"{Rows}\" selection-mode=\"single\"", vm);
        Tap(s, RowPt(3));
        Assert.True(Grid(s).Grid!.IsSelected(vm.Rows[3]));
    }

    [Fact]
    public void Row_key_keeps_selection_when_rows_are_replaced()
    {
        var vm = new Vm();
        var s = Surface(Multi + " row-key=\"Id\"", vm);
        Tap(s, RowPt(3));
        vm.Rows = Rows(); // 같은 Id 의 새 객체들
        s.Invalidate();
        s.Layout(300, 300);
        Assert.True(Grid(s).Grid!.IsSelected(vm.Rows[3]));
        Tap(s, RowPt(3)); // 새 객체로 다시 탭 = 해제(키 비교)
        Assert.Empty(vm.Picked);
    }

    [Fact]
    public void Without_row_key_replaced_rows_are_not_selected()
    {
        var vm = new Vm();
        var s = Surface(Multi, vm);
        Tap(s, RowPt(3));
        vm.Rows = Rows();
        s.Invalidate();
        s.Layout(300, 300);
        Assert.False(Grid(s).Grid!.IsSelected(vm.Rows[3]));
    }

    [Fact]
    public void Onrowclick_passes_the_item()
    {
        var vm = new Vm();
        var s = Surface("items=\"{Rows}\" onrowclick=\"Inspect\"", vm);
        Tap(s, RowPt(2));
        Assert.Equal(new[] { vm.Rows[2] }, vm.Clicked);
    }

    [Fact]
    public void Onrowclick_falls_back_to_no_argument_method()
    {
        var vm = new NoArgVm();
        Tap(Surface("items=\"{Rows}\" onrowclick=\"Inspect\"", vm), RowPt(0));
        Assert.Equal(1, vm.Hits);
    }

    [Fact]
    public void Row_click_uses_scroll_offset()
    {
        var vm = new Vm();
        var s = Surface(Single, vm);
        s.PointerWheel(new SKPoint(150, 150), -2); // 96 = 2행
        Tap(s, RowPt(0));
        Assert.Equal(new[] { vm.Rows[2] }, vm.Picked);
    }

    [Fact]
    public void Drag_scroll_does_not_select()
    {
        var vm = new Vm();
        var s = Surface(Single, vm);
        s.PointerDown(new SKPoint(150, 200));
        s.PointerMove(new SKPoint(150, 150));
        s.PointerUp(new SKPoint(150, 150));
        Assert.Empty(vm.Picked);
        Assert.Equal(50, Grid(s).ScrollY, 3);
    }

    [Fact]
    public void Header_and_below_last_row_taps_do_nothing()
    {
        var vm = new Vm { Rows = Rows(3) };
        var s = Surface(Single, vm);
        Tap(s, new SKPoint(150, 20));
        Tap(s, new SKPoint(150, 40 + 48 * 3 + 10));
        Assert.Empty(vm.Picked);
    }

    [Fact]
    public void Hover_requests_render_only_when_the_row_changes()
    {
        var vm = new Vm();
        var s = Surface(Single, vm);
        using var bmp = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, 300, 300);

        s.PointerMove(RowPt(1));
        Assert.Equal(1, Grid(s).Grid!.HoverRow);
        Assert.True(s.NeedsRender);
        s.RenderTo(canvas, 300, 300);

        s.PointerMove(RowPt(1) + new SKPoint(0, 10));
        Assert.False(s.NeedsRender);
        s.PointerMove(RowPt(2));
        Assert.True(s.NeedsRender);
    }

    [Fact]
    public void Hover_is_not_drawn_for_a_passive_grid()
    {
        var vm = new Vm();
        var s = Surface("items=\"{Rows}\"", vm);
        using var bmp = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, 300, 300);
        s.PointerMove(RowPt(1));
        Assert.False(s.NeedsRender);
    }

    // ── 3a-5 Repaint

    [Fact]
    public void Repaint_requests_render_without_rebuild()
    {
        var vm = new Vm();
        var s = Surface("items=\"{Rows}\"", vm);
        using var bmp = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, 300, 300);
        var root = s.Root;
        Assert.False(s.NeedsRender);

        s.Repaint();
        Assert.True(s.NeedsRender);
        s.RenderTo(canvas, 300, 300);
        Assert.Same(root, s.Root);
    }

    [Fact]
    public void Repaint_shows_changed_values()
    {
        var vm = new Vm { Rows = Rows(3) };
        var s = Surface("items=\"{Rows}\"", vm);
        using var a = new SKBitmap(300, 300);
        using (var c = new SKCanvas(a)) s.RenderTo(c, 300, 300);
        vm.Rows[0].Name = "완전히 다른 긴 이름 0000000000";
        s.Repaint();
        using var b = new SKBitmap(300, 300);
        using (var c = new SKCanvas(b)) s.RenderTo(c, 300, 300);

        var diff = false;
        for (var x = 0; x < 300 && !diff; x++)
            for (var y = 40; y < 88 && !diff; y++)
                diff = a.GetPixel(x, y) != b.GetPixel(x, y);
        Assert.True(diff);
    }

    [Fact]
    public void Repaint_picks_up_added_rows()
    {
        var vm = new Vm();
        var s = Surface("items=\"{Rows}\"", vm);
        vm.Rows.AddRange(Rows(50));
        s.Repaint();
        using var bmp = new SKBitmap(300, 300);
        using (var c = new SKCanvas(bmp)) s.RenderTo(c, 300, 300);
        Assert.Equal(40 + 150 * 48, Grid(s).ContentHeight, 3);
    }
}
