using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvListBox — 선택지(items·DvOption·enum)마다 행(높이 40), 스크롤 패널. 한 개 = value, 여럿(selection-mode="multi") = selected-items.
public class ListBoxTests
{
    private const float W = 600, H = 600;

    public enum Shift { Day, Swing, Night }
    public sealed record Recipe(int Id, string Name);

    public sealed class Vm : IDvControlHost
    {
        public DvListBox? lst;
        public List<Recipe> Recipes { get; } = Enumerable.Range(1, 12).Select(i => new Recipe(i, $"레시피 {i:00}")).ToList();
        public int? RecipeId { get; set; }
        public Shift Now { get; set; } = Shift.Swing;
        public List<int> Picked { get; set; } = new() { 2 };
        public string[] Tags { get; set; } = Array.Empty<string>();
        public bool Allow { get; set; } = true;
        public bool Gate(object proposed) => Allow;
        public void Attach(DvControlLookup c) => lst = c.Bind(lst, "lst");
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static LayoutBox List(DvSurface s) => Flatten(s.Root!).First(b => b.Node.Tag == "DvListBox");
    private static List<LayoutBox> Rows(DvSurface s) => Flatten(s.Root!).Where(b => b.Node.Tag == "DvListItem").ToList();
    private static List<bool> On(DvSurface s) => Rows(s).Select(r => r.ListOwner!.ListSelected![r.ListIndex]).ToList();

    private static void Tap(DvSurface s, LayoutBox row)
    {
        var p = new SKPoint(row.Rect.MidX, row.Rect.MidY - List(s).ScrollY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private const string Shifts = "<DvOption value=\"Day\" text=\"주간\"/><DvOption value=\"Swing\" text=\"오후\"/><DvOption value=\"Night\" text=\"야간\"/>";

    [Fact]
    public void Single_Enum_Options_ShowsSelected_TapSelects()
    {
        var (s, vm) = Start($"<DvListBox value=\"{{Bind Now}}\">{Shifts}</DvListBox>");
        Assert.Equal(3, Rows(s).Count);
        Assert.Equal(new[] { false, true, false }, On(s));
        Assert.Equal(40f, Rows(s)[0].Rect.Height);
        Tap(s, Rows(s)[2]);
        Assert.Equal(Shift.Night, vm.Now);
        Assert.Equal(new[] { false, false, true }, On(s));
        Assert.Contains("listbox-item-on", Rows(s)[2].Node.Attributes["class"]);
    }

    [Fact]
    public void Items_ScrollsAfterFiveRows_DragScrollsWithoutSelecting()
    {
        var (s, vm) = Start("<DvListBox value=\"{Bind RecipeId}\" items=\"{Recipes}\" item-value=\"Id\" item-text=\"Name\"/>");
        var list = List(s);
        Assert.True(list.IsScroller);
        Assert.InRange(list.Rect.Height, 5 * 40, 5 * 40 + 30); // 5행 + 간격·안쪽 여백
        var r = Rows(s)[3];
        var p = new SKPoint(r.Rect.MidX, r.Rect.MidY);
        s.PointerDown(p);
        for (var i = 1; i <= 10; i++)
            s.PointerMove(new SKPoint(p.X, p.Y - i * 12));
        s.PointerUp(new SKPoint(p.X, p.Y - 120));
        s.Layout(W, H);
        Assert.Null(vm.RecipeId);         // 끌기 = 스크롤, 고르지 않음
        Assert.True(List(s).ScrollY > 0);
        Tap(s, Rows(s)[5]);   // 120 스크롤 뒤 보이는 행
        Assert.Equal(6, vm.RecipeId);     // int? 로 변환
    }

    [Fact]
    public void Multi_Toggles_And_Writes_New_List_Of_Property_Type()
    {
        var (s, vm) = Start("<DvListBox selection-mode=\"multi\" selected-items=\"{Bind Picked}\" items=\"{Recipes}\" item-value=\"Id\" item-text=\"Name\"/>");
        Assert.True(On(s)[1]);
        var before = vm.Picked;
        Tap(s, Rows(s)[0]);
        Assert.Equal(new[] { 1, 2 }, vm.Picked);
        Assert.NotSame(before, vm.Picked);
        Tap(s, Rows(s)[1]);
        Assert.Equal(new[] { 1 }, vm.Picked);
    }

    [Fact]
    public void Multi_StringArray()
    {
        var (s, vm) = Start("<DvListBox selection-mode=\"multi\" selected-items=\"{Bind Tags}\"><DvOption value=\"a\"/><DvOption value=\"b\"/></DvListBox>");
        Tap(s, Rows(s)[1]);
        Assert.Equal(new[] { "b" }, vm.Tags);
    }

    [Fact]
    public void OneWay_Readonly_Disabled_DoNotSelect()
    {
        var (s, vm) = Start(
            $"<DvListBox value=\"{{Now}}\">{Shifts}</DvListBox>" +
            $"<DvListBox value=\"{{Bind Now}}\" readonly=\"true\">{Shifts}</DvListBox>" +
            $"<DvListBox value=\"{{Bind Now}}\" disabled=\"true\">{Shifts}</DvListBox>");
        var rows = Rows(s);
        foreach (var i in new[] { 0, 3, 6 })
        {
            var p = new SKPoint(rows[i].Rect.MidX, rows[i].Rect.MidY);
            s.PointerDown(p);
            s.PointerUp(p);
        }
        Assert.Equal(Shift.Swing, vm.Now);
    }

    [Fact]
    public void Gate_Rejects()
    {
        var vm = new Vm { Allow = false };
        var (s, _) = Start($"<DvListBox value=\"{{Bind Now}}\" onbeforecommit=\"Gate\">{Shifts}</DvListBox>", vm);
        Tap(s, Rows(s)[0]);
        Assert.Equal(Shift.Swing, vm.Now);
    }

    [Fact]
    public void Handle_Owned_Single()
    {
        var (s, vm) = Start($"<DvListBox id=\"lst\">{Shifts}</DvListBox>");
        var ev = new List<bool>();
        vm.lst!.SelectionChanged += (_, e) => ev.Add(e.ByUser);
        Assert.False(List(s).ReadOnly);
        Tap(s, Rows(s)[1]);
        Assert.Equal("Swing", vm.lst.SelectedValue);
        Assert.Equal(1, vm.lst.SelectedIndex);
        vm.lst.SelectedIndex = -1;
        s.Layout(W, H);
        Assert.Equal(new[] { false, false, false }, On(s));
        Assert.Equal(new[] { true, false }, ev);
    }

    [Fact]
    public void Handle_Owned_Multi_And_Bound_Multi()
    {
        var (s, vm) = Start($"<DvListBox id=\"lst\" selection-mode=\"multi\">{Shifts}</DvListBox>");
        Tap(s, Rows(s)[0]);
        Tap(s, Rows(s)[2]);
        Assert.Equal(new object?[] { "Day", "Night" }, vm.lst!.SelectedItems);
        Assert.Equal(0, vm.lst.SelectedIndex);
        vm.lst.SelectedItems = new object?[] { "Swing" };
        s.Layout(W, H);
        Assert.Equal(new[] { false, true, false }, On(s));

        var (s2, vm2) = Start("<DvListBox id=\"lst\" selection-mode=\"multi\" selected-items=\"{Bind Picked}\" items=\"{Recipes}\" item-value=\"Id\" item-text=\"Name\"/>");
        vm2.lst!.SelectedItems = new object?[] { 5, "7" }; // 요소 형식(int)으로 바꿔 씀
        Assert.Equal(new[] { 5, 7 }, vm2.Picked);
        vm2.lst.SelectedIndex = 0;
        Assert.Equal(new[] { 1 }, vm2.Picked);
    }

    [Fact]
    public void Paints_Without_Throwing()
    {
        var (s, _) = Start($"<DvListBox value=\"{{Bind Now}}\">{Shifts}</DvListBox>");
        using var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
    }
}

// 핸들의 번호 쓰기 — DvOption 글자 값을 enum 속성 형식으로 바꿔 쓴다(DvSelect·DvSegment·DvListBox)
public class SelectionHandleEnumTests
{
    public sealed class Vm : IDvControlHost
    {
        public DvSelect? sel; public DvSegment? seg; public DvListBox? lst;
        public ListBoxTests.Shift A { get; set; }
        public ListBoxTests.Shift B { get; set; }
        public ListBoxTests.Shift C { get; set; }
        public void Attach(DvControlLookup c) { sel = c.Bind(sel, "sel"); seg = c.Bind(seg, "seg"); lst = c.Bind(lst, "lst"); }
    }

    [Fact]
    public void SelectedIndex_Writes_Enum()
    {
        const string o = "<DvOption value=\"Day\"/><DvOption value=\"Swing\"/><DvOption value=\"Night\"/>";
        var vm = new Vm();
        var s = new DvSurface($"<DvScreen><DvSelect id=\"sel\" value=\"{{Bind A}}\">{o}</DvSelect><DvSegment id=\"seg\" value=\"{{Bind B}}\">{o}</DvSegment>" +
                              $"<DvListBox id=\"lst\" value=\"{{Bind C}}\">{o}</DvListBox></DvScreen>", vm, Devinno.UI.Styling.DvTheme.Light);
        s.Layout(600, 600);
        vm.sel!.SelectedIndex = 2;
        vm.seg!.SelectedIndex = 1;
        vm.lst!.SelectedIndex = 2;
        Assert.Equal((ListBoxTests.Shift.Night, ListBoxTests.Shift.Swing, ListBoxTests.Shift.Night), (vm.A, vm.B, vm.C));
    }
}
