using Devinno.UI;
using Devinno.UI.Grid;
using SkiaSharp;
using Xunit;

// 3d-2 — 숨겨진 행 = 선택에서 빠짐(양방향 = VM 스냅샷), 편집 중 행 유지, 재빌드·items 교체 후 필터 유지, 열 제거 = 조건 제거
public class GridFilterSelectionTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public bool On { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 6).Select(i => new Row { Id = i, On = i % 2 == 0, Name = "n" + i }).ToList();
        public List<Row> Picked { get; set; } = new();
        public bool ShowOn { get; set; } = true;
    }

    private const string Cols =
        "<DvColumn header=\"Id\" field=\"Id\" width=\"60\"/>" +
        "<DvColumn header=\"O\" field=\"On\" width=\"60\" filter=\"values\"/>" +
        "<DvColumn header=\"N\" field=\"Name\" width=\"100\" cell-type=\"input\"/>";

    private static DvSurface Make(Vm vm, string cols = Cols)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\" selection-mode=\"multi\" selection=\"{Bind Picked}\"", cols), vm)
            { UseNativeEditor = true };
        s.Layout(400, 400);
        return s;
    }

    private static GridSpec G(DvSurface s) => s.Root!.Children[0].Grid!;
    private static GridFilterCondition OnOnly() => new() { Values = new HashSet<object?> { true }, Summary = "예" };

    [Fact]
    public void Hidden_selected_rows_leave_selection_and_vm_gets_snapshot()
    {
        var vm = new Vm();
        var s = Make(vm);
        var g = G(s);
        g.SetSelection(new object[] { vm.Rows[0], vm.Rows[1] });
        vm.Picked = new List<Row> { vm.Rows[0], vm.Rows[1] };
        g.SetFilter(1, OnOnly()); // 1 은 On=false → 숨김
        s.Layout(400, 400);
        using (var bmp = new SKBitmap(400, 400))
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 400, 400);

        Assert.Equal(new object[] { vm.Rows[0] }, g.SelectedItems);
        Assert.Equal(new[] { vm.Rows[0] }, vm.Picked);

        g.SetFilter(1, null); // 풀어도 다시 선택되지 않음
        s.Layout(400, 400);
        Assert.Equal(new object[] { vm.Rows[0] }, g.SelectedItems);
    }

    [Fact]
    public void Editing_row_stays_until_edit_ends_then_hides_and_leaves_selection()
    {
        var vm = new Vm();
        var s = Make(vm);
        var g = G(s);
        g.SetFilter(1, OnOnly()); // 0,2,4
        s.Layout(400, 400);
        g.SetSelection(new object[] { vm.Rows[0] });

        // Id 0 행의 Name 셀(첫 보이는 행) 탭 → 편집 시작
        var y = g.HeaderTotal + g.RowHeight / 2;
        s.PointerDown(new SKPoint(200, y));
        s.PointerUp(new SKPoint(200, y));
        Assert.NotNull(g.EditMark);

        vm.Rows[0].On = false; // 조건에서 벗어남
        s.Repaint();
        s.Layout(400, 400);
        GridSorter.Ensure(g);
        Assert.Equal(3, g.ViewCount); // 편집 중 = 남음

        s.CommitEdit(s.Edit!.SessionId, DvEditExit.FocusLost);
        s.Layout(400, 400);
        g = G(s); // 커밋이 화면을 다시 빌드했다 — 새 스펙
        GridSorter.Ensure(g);
        Assert.Equal(2, g.ViewCount);
        Assert.Empty(g.SelectedItems);
    }

    [Fact]
    public void Rebuild_keeps_filter_by_field()
    {
        var vm = new Vm();
        var s = Make(vm);
        G(s).SetFilter(1, OnOnly());
        s.Invalidate();
        s.Layout(400, 400);
        var g = G(s);
        Assert.True(g.Filters.ContainsKey(1));
        GridSorter.Ensure(g);
        Assert.Equal(3, g.ViewCount);
    }

    [Fact]
    public void Items_replacement_keeps_filter()
    {
        var vm = new Vm();
        // 바인딩 없는 선택 — 재빌드가 화면 선택을 옮겨 오므로, 비려면 다시 거르기가 빼야 한다
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\" selection-mode=\"multi\"", Cols), vm);
        s.Layout(400, 400);
        G(s).SetFilter(1, OnOnly());
        G(s).SetSelection(new object[] { vm.Rows[0], vm.Rows[2] });
        Assert.Equal(2, G(s).SelectedItems.Count);
        vm.Rows = Enumerable.Range(0, 4).Select(i => new Row { Id = i, On = i == 3 }).ToList();
        s.Invalidate();
        s.Layout(400, 400);
        var g = G(s);
        Assert.Equal(2, g.SelectedItems.Count); // 옮겨 왔다
        GridSorter.Ensure(g);
        Assert.Equal(1, g.ViewCount);
        Assert.Empty(g.SelectedItems); // 옛 행은 새 목록에 없다(row-key 없음 = 참조)
    }

    [Fact]
    public void Removed_or_unfiltered_column_drops_its_condition()
    {
        var tvm = new ToggleVm(new Vm());
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
            "<DvColumn field=\"Id\"/><DvColumn field=\"On\" filter=\"{FilterOn}\"/>"), tvm);
        s.Layout(400, 400);
        G(s).SetFilter(1, OnOnly());
        Assert.Single(G(s).Filters); // 조건이 실제로 걸려 있었다
        tvm.FilterOn = "none";
        s.Invalidate();
        s.Layout(400, 400);
        Assert.Empty(G(s).Filters);
    }

    [Fact]
    public void Edit_mark_moved_directly_to_another_row_rechecks_the_old_row()
    {
        var vm = new Vm();
        var s = Make(vm);
        var g = G(s);
        g.SetFilter(1, OnOnly()); // 0,2,4
        s.Layout(400, 400);
        GridSorter.Ensure(g);
        Assert.Equal(3, g.ViewCount);

        g.EditMark = new GridEditMark { Key = vm.Rows[0], Column = 2 };
        vm.Rows[0].On = false;
        g.FilterDirty = true;
        GridSorter.Ensure(g);
        Assert.Equal(3, g.ViewCount); // 편집 중 = 남음

        g.EditMark = new GridEditMark { Key = vm.Rows[2], Column = 2 }; // 먼저 지우지 않고 바로 다른 행으로
        GridSorter.Ensure(g);
        Assert.Equal(2, g.ViewCount); // 0 행은 다시 판정돼 빠짐
    }

    [Fact]
    public void Still_filtered_column_carries_its_condition_over_rebuild()
    {
        var tvm = new ToggleVm(new Vm());
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
            "<DvColumn field=\"Id\"/><DvColumn field=\"On\" filter=\"{FilterOn}\"/>"), tvm);
        s.Layout(400, 400);
        G(s).SetFilter(1, OnOnly());
        s.Invalidate();
        s.Layout(400, 400);
        Assert.Single(G(s).Filters);
    }

    [Fact]
    public void Rebuild_of_sorted_unfiltered_grid_reuses_the_order()
    {
        var vm = new Vm();
        var s = Make(vm, "<DvColumn field=\"Id\" sort=\"desc\" sortable=\"true\"/><DvColumn field=\"Name\"/>");
        GridSorter.Ensure(G(s));
        var order = G(s).Order;
        Assert.NotNull(order);
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Same(order, G(s).Order); // 필터 없는 정렬 그리드는 재빌드해도 다시 정렬하지 않는다
    }

    [Fact]
    public void Rebuild_of_filtered_grid_refilters_with_new_values()
    {
        var vm = new Vm();
        var s = Make(vm);
        G(s).SetFilter(1, OnOnly());
        GridSorter.Ensure(G(s));
        Assert.Equal(3, G(s).ViewCount);
        vm.Rows[0].On = false;
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Equal(2, G(s).ViewCount);
    }

    [Fact]
    public void Dropped_condition_on_rebuild_shows_all_rows_again()
    {
        var tvm = new ToggleVm(new Vm());
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
            "<DvColumn field=\"Id\"/><DvColumn field=\"On\" filter=\"{FilterOn}\"/>"), tvm);
        s.Layout(400, 400);
        G(s).SetFilter(1, OnOnly());
        GridSorter.Ensure(G(s));
        Assert.Equal(3, G(s).ViewCount);
        tvm.FilterOn = "none";
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Equal(6, G(s).ViewCount);
        Assert.Null(G(s).Order);
    }

    [Fact]
    public void Dropped_condition_on_sorted_grid_rebuilds_the_order()
    {
        var tvm = new ToggleVm(new Vm());
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
            "<DvColumn field=\"Id\" sort=\"desc\" sortable=\"true\"/><DvColumn field=\"On\" filter=\"{FilterOn}\"/>"), tvm);
        s.Layout(400, 400);
        G(s).SetFilter(1, OnOnly());
        GridSorter.Ensure(G(s));
        Assert.Equal(3, G(s).ViewCount);
        tvm.FilterOn = "none";
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Equal(6, G(s).ViewCount);
    }

    [Fact]
    public void Rebuild_of_user_sorted_unfiltered_grid_keeps_the_order_when_values_change()
    {
        var vm = new Vm();
        var s = Make(vm, "<DvColumn field=\"Id\" sortable=\"true\"/><DvColumn field=\"Name\"/>");
        GridSorter.Toggle(G(s), 0);
        GridSorter.Ensure(G(s));
        var order = G(s).Order;
        Assert.NotNull(order);
        vm.Rows[0].Id = 99; // 값만 바뀜 — 정렬 트리거가 없으면 행이 튀지 않는다
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Same(order, G(s).Order);
    }

    [Fact]
    public void Rebuild_of_markup_sorted_unfiltered_grid_reads_no_sort_keys()
    {
        var vm = new CountVm();
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", "<DvColumn field=\"Id\" sort=\"asc\" sortable=\"true\"/>"), vm);
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        vm.Reads = 0;
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Equal(1, vm.Reads); // 빌드가 값 타입을 정하려 첫 행만 읽는다 — 다시 정렬하지 않는다(정렬이면 6번 이상)
    }

    [Fact]
    public void Rebuild_of_markup_sorted_filtered_grid_refilters_with_new_values()
    {
        var vm = new Vm();
        var s = Make(vm, "<DvColumn field=\"Id\" sort=\"asc\" sortable=\"true\"/><DvColumn field=\"On\" filter=\"values\"/>");
        G(s).SetFilter(1, OnOnly());
        GridSorter.Ensure(G(s));
        Assert.Equal(3, G(s).ViewCount);
        vm.Rows[0].On = false;
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Equal(2, G(s).ViewCount);
    }

    [Fact]
    public void Rebuild_of_user_sorted_filtered_grid_refilters_with_new_values()
    {
        var vm = new Vm();
        var s = Make(vm, "<DvColumn field=\"Id\" sortable=\"true\"/><DvColumn field=\"On\" filter=\"values\"/>");
        G(s).SetFilter(1, OnOnly());
        GridSorter.Toggle(G(s), 0);
        GridSorter.Ensure(G(s));
        Assert.Equal(3, G(s).ViewCount);
        vm.Rows[0].On = false;
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Equal(2, G(s).ViewCount);
    }

    [Fact]
    public void Dropped_condition_on_user_sorted_grid_rebuilds_the_order()
    {
        var tvm = new ToggleVm(new Vm());
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
            "<DvColumn field=\"Id\" sortable=\"true\"/><DvColumn field=\"On\" filter=\"{FilterOn}\"/>"), tvm);
        s.Layout(400, 400);
        G(s).SetFilter(1, OnOnly());
        GridSorter.Toggle(G(s), 0);
        GridSorter.Ensure(G(s));
        Assert.Equal(3, G(s).ViewCount);
        tvm.FilterOn = "none";
        s.Invalidate();
        s.Layout(400, 400);
        GridSorter.Ensure(G(s));
        Assert.Equal(6, G(s).ViewCount);
    }

    [Fact]
    public void Condition_of_another_kind_is_not_carried_over_rebuild()
    {
        var tvm = new ToggleVm(new Vm());
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"",
            "<DvColumn field=\"Id\"/><DvColumn field=\"On\" filter=\"{FilterOn}\"/>"), tvm);
        s.Layout(400, 400);
        G(s).SetFilter(1, OnOnly());
        GridSorter.Ensure(G(s));
        Assert.Equal(3, G(s).ViewCount);
        tvm.FilterOn = "text"; // 값 목록 조건은 글자 필터에서 아무것도 거르지 않는다
        s.Invalidate();
        s.Layout(400, 400);
        Assert.Empty(G(s).Filters); // 활성으로 칠해지지 않는다
        GridSorter.Ensure(G(s));
        Assert.Equal(6, G(s).ViewCount);
        Assert.Null(G(s).Order);
    }

    public sealed class CountRow(CountVm vm, int id)
    {
        public int Id { get { vm.Reads++; return id; } }
    }

    public sealed class CountVm
    {
        public int Reads;
        public List<CountRow> Rows { get; }
        public CountVm() => Rows = Enumerable.Range(0, 6).Select(i => new CountRow(this, i)).ToList();
    }

    public sealed class ToggleVm(Vm inner)
    {
        public List<Row> Rows => inner.Rows;
        public string FilterOn { get; set; } = "values";
    }
}
