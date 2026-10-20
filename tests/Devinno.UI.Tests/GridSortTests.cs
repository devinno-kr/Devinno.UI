using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 3b-2 그리드 내부 정렬 — 3상태 순환, 탭 순서 누적, 안정, null·타입 섞임, 초기 sort, 재빌드·값 변경·행 수 변경, 정렬 후 선택
public class GridSortTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public int Group { get; set; }
        public double? Value { get; set; }
        public object? Mixed { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class Vm
    {
        // Value = 0,7,4,1,8,5,2,9,6,3 (서로 다름), Group = 2,1,2,1…, Mixed = int 와 string 섞임
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 10).Select(i => new Row
        {
            Id = i, Group = i % 2 == 0 ? 2 : 1, Value = i * 7 % 10, Mixed = i % 3 == 0 ? i : "s" + i, Name = "N" + i
        }).ToList();
        public List<Row> Picked { get; set; } = new();
        public List<Row> Clicked { get; } = new();
        public void Inspect(Row r) => Clicked.Add(r);
    }

    private const string Cols =
        "<DvColumn header=\"Id\" field=\"Id\" width=\"60\" sortable=\"true\"/>" +
        "<DvColumn header=\"G\" field=\"Group\" width=\"60\" sortable=\"true\"/>" +
        "<DvColumn header=\"V\" field=\"Value\" width=\"60\" sortable=\"true\"/>" +
        "<DvColumn header=\"M\" field=\"Mixed\" width=\"60\" sortable=\"true\"/>" +
        "<DvColumn header=\"N\" field=\"Name\"/>";

    // 헤더 가운데(높이 40): Id 30, G 90, V 150, M 210, N 264
    private static readonly SKPoint HId = new(30, 20), HG = new(90, 20), HV = new(150, 20), HM = new(210, 20), HN = new(264, 20);

    private static DvSurface Surface(Vm vm, string cols = Cols, string attrs = "items=\"{Rows}\"")
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen(attrs, cols), vm);
        s.Layout(300, 300);
        return s;
    }

    private static GridSpec G(DvSurface s) => s.Root!.Children[0].Grid!;

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(300, 300);
    }

    private static int[] Ids(DvSurface s) => Enumerable.Range(0, G(s).Count).Select(i => ((Row)G(s).ItemAt(i)!).Id).ToArray();

    private static void Render(DvSurface s)
    {
        using var bmp = new SKBitmap(300, 300);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 300, 300);
    }

    [Fact]
    public void Tap_cycles_ascending_descending_none()
    {
        var s = Surface(new Vm());
        Tap(s, HV);
        Assert.Equal(new[] { 0, 3, 6, 9, 2, 5, 8, 1, 4, 7 }, Ids(s)); // Value 0..9
        Tap(s, HV);
        Assert.Equal(new[] { 7, 4, 1, 8, 5, 2, 9, 6, 3, 0 }, Ids(s));
        Tap(s, HV);
        Assert.Equal(Enumerable.Range(0, 10).ToArray(), Ids(s));
        Assert.Empty(G(s).Sort);
    }

    [Fact]
    public void Sorts_accumulate_in_tap_order_and_removing_keeps_the_rest()
    {
        var s = Surface(new Vm());
        Tap(s, HG); // 1차 Group 오름
        Tap(s, HId); // 2차 Id 오름
        Assert.Equal(new[] { (1, false), (0, false) }, G(s).Sort.Select(k => (k.Column, k.Desc)).ToArray());
        Assert.Equal(new[] { 1, 3, 5, 7, 9, 0, 2, 4, 6, 8 }, Ids(s));

        Tap(s, HId); // Id 내림 — 자리 유지
        Assert.Equal(new[] { 9, 7, 5, 3, 1, 8, 6, 4, 2, 0 }, Ids(s));
        Tap(s, HG); // Group 내림 — 1차 그대로
        Tap(s, HG); // Group 해제 → Id 내림만
        Assert.Equal(new[] { (0, true) }, G(s).Sort.Select(k => (k.Column, k.Desc)).ToArray());
        Assert.Equal(new[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }, Ids(s));
    }

    [Fact]
    public void Sort_is_stable()
    {
        var s = Surface(new Vm());
        Tap(s, HG);
        Assert.Equal(new[] { 1, 3, 5, 7, 9, 0, 2, 4, 6, 8 }, Ids(s)); // 같은 Group 안은 원본 순서
    }

    [Fact]
    public void Nulls_sort_first_and_mixed_types_do_not_throw()
    {
        var vm = new Vm();
        vm.Rows[5].Value = null;
        var s = Surface(vm);
        Tap(s, HV);
        Assert.Equal(5, Ids(s)[0]);

        Tap(s, HM); // int 와 string 섞인 열 — 예외 없이 순서가 정해진다
        Assert.Equal(10, Ids(s).Distinct().Count());
    }

    [Fact]
    public void Descending_keeps_ties_in_original_order_and_nulls_last()
    {
        var vm = new Vm();
        vm.Rows[5].Value = null;
        vm.Rows[2].Value = 9; // Id 7 과 같은 값
        var s = Surface(vm);
        Tap(s, HV);
        Tap(s, HV);
        var ids = Ids(s);
        Assert.Equal(new[] { 2, 7 }, ids[..2]); // 같은 9 — 원본 순서
        Assert.Equal(5, ids[^1]);
    }

    [Fact]
    public void Single_key_path_matches_the_comparer_path()
    {
        // 단일 열(기본형 키 정렬)과 같은 열 + 동률 없는 둘째 열(비교자 정렬)의 순서가 같아야 한다
        var vm = new Vm();
        vm.Rows[5].Value = null;
        vm.Rows[2].Value = 9;
        var one = Surface(vm);
        Tap(one, HV);
        Tap(one, HV);
        var two = Surface(vm);
        Tap(two, HV);
        Tap(two, HV);
        Tap(two, HId); // 둘째 열 Id 오름 = 원본 순서 동률 해소와 같다
        Assert.Equal(Ids(one), Ids(two));
    }

    [Fact]
    public void Non_sortable_column_tap_is_ignored()
    {
        var s = Surface(new Vm());
        Tap(s, HN);
        Assert.Empty(G(s).Sort);
        Assert.Null(G(s).Order);
    }

    [Fact]
    public void Initial_sort_from_markup()
    {
        var s = Surface(new Vm(), "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"Value\" sort=\"desc\" sortable=\"true\"/>");
        Assert.Equal(new[] { (1, true) }, G(s).Sort.Select(k => (k.Column, k.Desc)).ToArray());
        Assert.Equal(7, Ids(s)[0]); // Value 9
    }

    [Fact]
    public void Rebuild_and_row_taps_reuse_the_sorted_order()
    {
        var vm = new Vm();
        var s = Surface(vm, attrs: "items=\"{Rows}\" selection-mode=\"single\" selection=\"{Bind Picked}\"");
        Tap(s, HV);
        Render(s); // 정렬은 처음 그리거나 읽을 때 계산된다
        var order = G(s).Order;
        Assert.NotNull(order);
        Tap(s, new SKPoint(150, 40 + 24)); // 행 탭 → 재빌드
        Assert.Same(order, G(s).Order);
        s.Invalidate();
        s.Layout(300, 300);
        Assert.Same(order, G(s).Order);
    }

    [Fact]
    public void Value_change_keeps_order_but_row_count_change_resorts()
    {
        var vm = new Vm();
        var s = Surface(vm);
        Tap(s, HV);
        Render(s);
        vm.Rows[0].Value = 100; // 맨 앞(0)이 가장 커짐 — 순서는 그대로
        s.Repaint();
        Render(s);
        Assert.Equal(0, Ids(s)[0]);

        vm.Rows.Add(new Row { Id = 99, Value = -1 });
        s.Repaint();
        Render(s);
        Assert.Equal(99, Ids(s)[0]);
        Assert.Equal(0, Ids(s)[^1]); // 다시 정렬되며 값 100 이 맨 뒤
    }

    [Fact]
    public void Selection_and_rowclick_use_the_sorted_row()
    {
        var vm = new Vm();
        var s = Surface(vm, attrs: "items=\"{Rows}\" selection-mode=\"single\" selection=\"{Bind Picked}\" onrowclick=\"Inspect\"");
        Tap(s, HV);
        Tap(s, HV); // 내림 — 첫 행 = Value 9 = Id 7
        Tap(s, new SKPoint(150, 40 + 24));
        Assert.Equal(7, Assert.Single(vm.Picked).Id);
        Assert.Equal(7, Assert.Single(vm.Clicked).Id);
    }

    [Fact]
    public void Header_tap_does_not_change_selection()
    {
        var vm = new Vm();
        var s = Surface(vm, attrs: "items=\"{Rows}\" selection-mode=\"single\" selection=\"{Bind Picked}\"");
        Tap(s, HV);
        Assert.Empty(vm.Picked);
    }

    [Fact]
    public void Sort_in_frozen_and_scrolled_columns()
    {
        // 고정 열(Id 120) + 넓은 열 → 가로 스크롤 후 스크롤 열 헤더 탭
        var vm = new Vm();
        var s = Surface(vm,
            "<DvColumn header=\"Id\" field=\"Id\" width=\"120\" frozen=\"true\" sortable=\"true\"/>" +
            "<DvColumn header=\"N\" field=\"Name\" width=\"300\"/>" +
            "<DvColumn header=\"V\" field=\"Value\" width=\"100\" sortable=\"true\"/>");
        s.Root!.Children[0].ScrollX = 200; // V 열 = 내용 x 420~520 → 화면 220~320 중 보이는 부분
        Tap(s, new SKPoint(240, 20));
        Assert.Equal(new[] { 2 }, G(s).Sort.Select(k => k.Column).ToArray());
        Tap(s, new SKPoint(60, 20)); // 고정 열은 스크롤과 무관
        Assert.Equal(new[] { 2, 0 }, G(s).Sort.Select(k => k.Column).ToArray());
    }
}
