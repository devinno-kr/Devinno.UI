using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvTreeView — items + item-children + item-text(+ item-value). ▶/▼ 자리를 누르면 펼치기·접기, 나머지는 고르기(value).
public class TreeViewTests
{
    private const float W = 600, H = 700;

    public sealed class Node(string name, int id, params Node[] kids)
    {
        public string Name { get; } = name;
        public int Id { get; } = id;
        public List<Node> Kids { get; } = kids.ToList();
    }

    public sealed class Vm : IDvControlHost
    {
        public DvTreeView? tree;
        public List<Node> Plant { get; } = new()
        {
            new Node("1공장", 1, new Node("A라인", 11, new Node("펌프 1", 111), new Node("펌프 2", 112)), new Node("B라인", 12)),
            new Node("2공장", 2, new Node("C라인", 21)),
        };
        public Node? Picked { get; set; }
        public int PickedId { get; set; }
        public void Attach(DvControlLookup c) => tree = c.Bind(tree, "tree");
    }

    private static (DvSurface S, Vm Vm) Start(string attrs, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20\"><DvTreeView items=\"{{Plant}}\" item-children=\"Kids\" item-text=\"Name\" {attrs}/></DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static List<LayoutBox> Rows(DvSurface s) => Flatten(s.Root!).Where(b => b.Node.Tag == "DvTreeItem").ToList();
    private static List<string?> Texts(DvSurface s) => Rows(s).Select(r => r.ResolvedText).ToList();

    private static void TapAt(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static void TapChevron(DvSurface s, LayoutBox row) => TapAt(s, new SKPoint(row.Rect.Left + row.Style.Padding.Left + 7, row.Rect.MidY));
    private static void TapText(DvSurface s, LayoutBox row) => TapAt(s, new SKPoint(row.Rect.Right - 20, row.Rect.MidY));

    [Fact]
    public void Roots_Only_Then_Expand_With_Chevron_Indents()
    {
        var (s, _) = Start("value=\"{Bind Picked}\"");
        Assert.Equal(new[] { "1공장", "2공장" }, Texts(s));
        Assert.Equal("fa-chevron-right", Rows(s)[0].Icon);
        TapChevron(s, Rows(s)[0]);
        Assert.Equal(new[] { "1공장", "A라인", "B라인", "2공장" }, Texts(s));
        Assert.Equal("fa-chevron-down", Rows(s)[0].Icon);
        Assert.Null(Rows(s)[2].Icon);                                          // 잎
        Assert.True(Rows(s)[1].Style.Padding.Left > Rows(s)[0].Style.Padding.Left); // 들여쓰기
        Assert.Equal(40f, Rows(s)[0].Rect.Height);
        TapChevron(s, Rows(s)[1]);
        Assert.Equal(new[] { "1공장", "A라인", "펌프 1", "펌프 2", "B라인", "2공장" }, Texts(s));
        TapChevron(s, Rows(s)[0]); // 접으면 자손 가지도 숨김(펼침 기억은 유지)
        Assert.Equal(new[] { "1공장", "2공장" }, Texts(s));
        TapChevron(s, Rows(s)[0]);
        Assert.Equal(6, Rows(s).Count);
    }

    [Fact]
    public void Tap_Text_Selects_Item_Not_Toggle()
    {
        var (s, vm) = Start("value=\"{Bind Picked}\"");
        TapText(s, Rows(s)[1]);
        Assert.Equal("2공장", vm.Picked!.Name);
        Assert.Equal(2, Rows(s).Count); // 고르기는 펼치지 않음
        Assert.Contains("treeview-item-on", Rows(s)[1].Node.Attributes["class"]);
    }

    [Fact]
    public void ItemValue_Writes_Value()
    {
        var (s, vm) = Start("item-value=\"Id\" value=\"{Bind PickedId}\"");
        TapChevron(s, Rows(s)[0]);
        TapText(s, Rows(s)[2]); // B라인
        Assert.Equal(12, vm.PickedId);
    }

    [Fact]
    public void ReadOnly_CanStillExpand_ButNotSelect()
    {
        var (s, vm) = Start("value=\"{Picked}\"");
        TapChevron(s, Rows(s)[0]);
        Assert.Equal(4, Rows(s).Count);
        TapText(s, Rows(s)[1]);
        Assert.Null(vm.Picked);
    }

    [Fact]
    public void Handle_SelectedNode_ExpandsAncestors_AndRaises()
    {
        var (s, vm) = Start("id=\"tree\" item-value=\"Id\"");
        var ev = new List<bool>();
        vm.tree!.SelectionChanged += (_, e) => ev.Add(e.ByUser);
        var pump2 = vm.Plant[0].Kids[0].Kids[1];
        vm.tree.SelectedNode = pump2;
        s.Layout(W, H);
        Assert.Equal(new[] { "1공장", "A라인", "펌프 1", "펌프 2", "B라인", "2공장" }, Texts(s));
        Assert.Same(pump2, vm.tree.SelectedNode);
        Assert.Contains("treeview-item-on", Rows(s)[3].Node.Attributes["class"]);
        TapText(s, Rows(s)[5]);
        Assert.Equal("2공장", ((Node)vm.tree.SelectedNode!).Name);
        Assert.Equal(new[] { false, true }, ev);
    }

    [Fact]
    public void Cycle_Does_Not_Hang()
    {
        var vm = new Vm();
        vm.Plant[0].Kids[1].Kids.Add(vm.Plant[0]); // B라인 → 1공장 (순환)
        var x = vm.Plant[1].Kids[0];
        x.Kids.Add(x);
        x.Kids.Add(x);                             // 자기 자신을 둘 — 갈래가 두 배씩 늘지 않게
        var (s, _) = Start("value=\"{Bind Picked}\"", vm);
        Assert.Equal(2, Rows(s).Count);
        using var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
    }
}
