using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 레이아웃·그리기 중 VM 읽기 예외(getter·ToString·열거·정렬 비교) — 호스트 루프 밖으로 새지 않고, 그 값은 null(빈 값)로 보며,
// 같은 (Kind, Member, Tag) 는 재빌드(Invalidate) 한 번 동안 HandlerError 1번만.
public class VmReadErrorTests
{
    public sealed class Bad
    {
        public override string ToString() => throw new InvalidOperationException("tostring");
    }

    public sealed class Vm
    {
        public string Title => throw new InvalidOperationException("title-get");
        public Bad Weird { get; } = new();
        public string Ok => "ok";

        public IEnumerable<string> Items
        {
            get
            {
                yield return "a";
                yield return "b";
                throw new InvalidOperationException("enum");
            }
        }
    }

    public sealed class Cmp : IComparable
    {
        public int CompareTo(object? obj) => throw new InvalidOperationException("compare");
    }

    public sealed class Row
    {
        public Row(int id) => Id = id;
        public int Id { get; }
        public string Name => Id == 2 ? throw new InvalidOperationException("name-get") : "R" + Id;
        public int? V => Id == 2 ? throw new InvalidOperationException("v-get") : 10 - Id;
        public Bad Weird { get; } = new();
        public string Cls => throw new InvalidOperationException("cls-get");
        public Cmp K { get; } = new();
    }

    public sealed class GridVm
    {
        public List<Row> Rows { get; } = Enumerable.Range(0, 5).Select(i => new Row(i)).ToList();
    }

    private static List<DvHandlerErrorEventArgs> Watch(DvSurface s)
    {
        var list = new List<DvHandlerErrorEventArgs>();
        s.HandlerError += (_, e) => list.Add(e);
        return list;
    }

    private static DvSurface Surface(string inner, object vm)
        => new($"<DvScreen style=\"padding:0; gap:0\">{inner}</DvScreen>", vm);

    private static void Render(DvSurface s, int times = 1)
    {
        using var bmp = new SKBitmap(400, 300);
        using var c = new SKCanvas(bmp);
        for (var i = 0; i < times; i++)
        {
            s.Repaint(); // 요약도 매번 다시 계산
            s.RenderTo(c, 400, 300);
        }
    }

    private static void AssertOne(List<DvHandlerErrorEventArgs> errors, string kind, string? member, string? tag, string message)
    {
        var e = Assert.Single(errors);
        Assert.Equal(kind, e.Kind);
        Assert.Equal(member, e.Member);
        Assert.Equal(tag, e.Tag);
        Assert.IsType<InvalidOperationException>(e.Exception);
        Assert.Equal(message, e.Exception.Message);
    }

    // ── 바인딩 getter
    [Fact]
    public void Getter_exception_is_an_empty_value_and_reported_once_per_rebuild()
    {
        var s = Surface("<DvLabel text=\"{Title}\"/><DvLabel text=\"{Title}\"/><DvLabel text=\"{Ok}\"/>", new Vm());
        var errors = Watch(s);
        Render(s, 3);

        Assert.Equal("", s.Root!.Children[0].ResolvedText);
        Assert.Equal("ok", s.Root.Children[2].ResolvedText);
        AssertOne(errors, "getter", "Title", "DvLabel", "title-get");

        s.Invalidate(); // 다음 재빌드에서는 다시 알린다
        Render(s, 2);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Getter_exception_in_inline_text_and_visible_does_not_escape()
    {
        var s = Surface("<DvLabel text=\"제목: {Title}\"/><DvLabel visible=\"{Title}\" text=\"x\"/>", new Vm());
        var errors = Watch(s);
        Render(s);
        Assert.Equal("제목: ", s.Root!.Children[0].ResolvedText);
        Assert.Single(s.Root.Children); // visible 읽기 실패 = null = 숨김
        AssertOne(errors, "getter", "Title", "DvLabel", "title-get"); // 두 요소 모두 (getter, Title, DvLabel) — 한 번
    }

    // ── ToString
    [Fact]
    public void ToString_exception_is_empty_text_and_reported_as_format()
    {
        var s = Surface("<DvLabel text=\"{Weird}\"/><DvLabel text=\"값 {Weird}\"/>", new Vm());
        var errors = Watch(s);
        Render(s, 2);
        Assert.Equal("", s.Root!.Children[0].ResolvedText);
        Assert.Equal("값 ", s.Root.Children[1].ResolvedText);
        AssertOne(errors, "format", "Weird", "DvLabel", "tostring");
    }

    // ── 열거
    [Fact]
    public void Repeat_items_enumeration_exception_builds_no_items()
    {
        var s = Surface("<DvRepeat items=\"{Items}\"><DvLabel text=\"{Item}\"/></DvRepeat>", new Vm());
        var errors = Watch(s);
        Render(s, 2);
        Assert.Empty(s.Root!.Children[0].Children);
        AssertOne(errors, "enumerate", "Items", "DvRepeat", "enum");
    }

    // ── 그리드
    private const string Cols =
        "<DvColumn header=\"Id\" field=\"Id\" width=\"60\"/>" +
        "<DvColumn header=\"이름\" field=\"Name\" width=\"100\"/>";

    private static DvSurface Grid(GridVm vm, string cols = Cols, string attrs = "items=\"{Rows}\"", string summary = "")
        => Surface($"<DvDataGrid {attrs} style=\"height:300\">{cols}{summary}</DvDataGrid>", vm);

    private static GridSpec G(DvSurface s) => s.Root!.Children[0].Grid!;

    [Fact]
    public void Grid_cell_getter_exception_draws_an_empty_cell_and_reports_once_per_rebuild()
    {
        var vm = new GridVm();
        var s = Grid(vm);
        var errors = Watch(s);
        Render(s, 3);
        Assert.Equal("", G(s).Columns[1].Text(vm.Rows[2]));
        Assert.Equal("R1", G(s).Columns[1].Text(vm.Rows[1]));
        AssertOne(errors, "getter", "Name", "DvDataGrid", "name-get");

        s.Invalidate();
        Render(s, 2);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Grid_cell_format_exception_draws_an_empty_cell()
    {
        var vm = new GridVm();
        var s = Grid(vm, "<DvColumn header=\"W\" field=\"Weird\" width=\"100\"/>");
        var errors = Watch(s);
        Render(s, 3);
        Assert.Equal("", G(s).Columns[0].Text(vm.Rows[0]));
        AssertOne(errors, "format", "Weird", "DvDataGrid", "tostring");
    }

    [Fact]
    public void Grid_row_class_getter_exception_is_no_class()
    {
        var vm = new GridVm();
        var s = Grid(vm, "<DvColumn header=\"Id\" field=\"Id\" width=\"60\"/>", "items=\"{Rows}\" row-class=\"Cls\"");
        var errors = Watch(s);
        Render(s, 3);
        AssertOne(errors, "getter", "Cls", "DvDataGrid", "cls-get");
    }

    [Fact]
    public void Sort_key_getter_exception_sorts_as_null()
    {
        var vm = new GridVm();
        var s = Grid(vm, "<DvColumn header=\"V\" field=\"V\" width=\"60\" sortable=\"true\" sort=\"asc\"/>");
        var errors = Watch(s);
        Render(s, 2);
        // V = 10,9,(예외),7,6 → 오름차순: null(2) 먼저
        Assert.Equal(new[] { 2, 4, 3, 1, 0 }, G(s).Order);
        AssertOne(errors, "getter", "V", "DvDataGrid", "v-get");
    }

    [Fact]
    public void Sort_compare_exception_does_not_escape()
    {
        var vm = new GridVm();
        var s = Grid(vm, "<DvColumn header=\"K\" field=\"K\" width=\"60\" sortable=\"true\" sort=\"asc\"/>");
        var errors = Watch(s);
        Render(s, 2);
        Assert.Equal(5, G(s).Order!.Length);
        AssertOne(errors, "compare", "K", "DvDataGrid", "compare");
    }

    [Fact]
    public void Summary_skips_values_whose_getter_throws()
    {
        var vm = new GridVm();
        var s = Grid(vm, "<DvColumn header=\"Id\" field=\"Id\" width=\"60\"/><DvColumn header=\"V\" field=\"V\" width=\"60\"/>",
            summary: "<DvSummaryRow title=\"합\" fn=\"sum\"/>");
        var errors = Watch(s);
        Render(s, 3);
        Assert.Equal("32", G(s).Summaries[0].Texts[1]); // 10+9+7+6 (예외 난 행 제외)
        AssertOne(errors, "getter", "V", "DvDataGrid", "v-get");
    }

    // ── 서피스 없이(직접 렌더)도 새지 않음
    [Fact]
    public void Renderer_does_not_throw_on_vm_read_exceptions()
    {
        var png = DvRenderer.RenderToPng(
            "<DvScreen><DvLabel text=\"{Title}\"/><DvLabel text=\"{Weird}\"/><DvRepeat items=\"{Items}\"><DvLabel text=\"{Item}\"/></DvRepeat></DvScreen>",
            new Vm(), 200, 100);
        Assert.NotEmpty(png);

        var box = new LayoutEngine().Layout(Devinno.UI.Markup.MarkupParser.Parse("<DvScreen><DvLabel text=\"{Title}\"/></DvScreen>"), new Vm(), 200, 100);
        Assert.Equal("", box.Children[0].ResolvedText);
    }

    public sealed class ArmedRow
    {
        public bool Armed;
        private bool _on;
        public bool On
        {
            get => Armed ? throw new InvalidOperationException("on-get") : _on;
            set => _on = value;
        }
    }

    public sealed class ArmedVm
    {
        public List<ArmedRow> Rows { get; } = new() { new ArmedRow() };
    }

    public sealed class BadEq
    {
        public override bool Equals(object? obj) => throw new InvalidOperationException("equals");
        public override int GetHashCode() => 0;
    }

    public sealed class MiscVm
    {
        public List<ArmedRow> Rows { get; } = new() { new ArmedRow() };
        public Bad Height { get; } = new();
        public BadEq Pick { get; set; } = new();
        public List<BadEq> Opts { get; } = new() { new BadEq() };
    }

    // row-height 바인딩 값의 ToString 예외 = 기본 높이, 선택 값의 Equals 예외 = 다른 값 — 둘 다 새지 않고 알림
    [Fact]
    public void Row_height_tostring_and_select_equals_exceptions_are_contained()
    {
        var vm = new MiscVm();
        var s = new DvSurface(
            "<DvScreen><DvDataGrid items=\"{Rows}\" row-height=\"{Height}\" style=\"height:200\"><DvColumn field=\"On\"/></DvDataGrid>" +
            "<DvSelect value=\"{Bind Pick}\" items=\"{Opts}\"/></DvScreen>", vm, DvTheme.Light);
        var errors = Watch(s);
        s.Layout(400, 400);
        using (var bmp = new SKBitmap(400, 400))
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 400, 400);

        Assert.Contains(errors, e => e.Kind == "format" && e.Member == "row-height");
        Assert.Contains(errors, e => e.Kind == "compare" && e.Exception.Message == "equals");
    }

    // 입력 처리(포인터) 중의 VM 읽기 예외도 밖으로 새지 않고 그 화면의 HandlerError 로 온다
    [Fact]
    public void Read_exception_during_pointer_handling_is_reported_to_surface()
    {
        var vm = new ArmedVm();
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", "<DvColumn field=\"On\" width=\"60\" cell-type=\"check\"/>"), vm, DvTheme.Light);
        s.Layout(300, 300);
        var errors = Watch(s);

        vm.Rows[0].Armed = true; // 그린 뒤부터 getter 가 던짐 → 탭할 때 현재 값을 읽다가 예외
        var p = new SKPoint(30, 40 + 24);
        s.PointerDown(p);
        s.PointerUp(p);

        Assert.Contains(errors, e => e.Kind == "getter" && e.Exception.Message == "on-get");
    }
}
