using Devinno.UI;
using Devinno.UI.Editing;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3단계 R13 — onbeforecommit 탐색 하나(CommitGate): bool M(값), 반복·그리드 행 안이면 bool M(항목, 값) 먼저. 각각 정확한 형 → object.
public class CommitGateRuleTests
{
    public sealed class Item
    {
        public string Name { get; set; } = "";
        public bool On { get; set; }
        public double Level { get; set; }
    }

    public sealed class Vm
    {
        public List<Item> Items { get; } = [new() { Name = "a" }, new() { Name = "b" }];
        public bool Flag { get; set; }
        public List<string> Calls { get; } = new();

        // 같은 이름의 겹친 형식 — 어느 것이 불리는지 기록
        public bool Pick(Item item, bool v) { Calls.Add("item,bool:" + item.Name); return true; }
        public bool Pick(Item item, object v) { Calls.Add("item,object"); return true; }
        public bool Pick(bool v) { Calls.Add("bool"); return true; }
        public bool Pick(object v) { Calls.Add("object"); return true; }

        public bool ItemObj(Item item, object v) { Calls.Add("item,object:" + item.Name); return true; }
        public bool ItemObj(bool v) { Calls.Add("bool"); return true; }

        public bool ValueOnly(bool v) { Calls.Add("bool"); return true; }
        public bool ValueOnly(object v) { Calls.Add("object"); return true; }

        public bool ObjOnly(object v) { Calls.Add("object:" + v); return true; }

        public bool Deny(bool v) { Calls.Add("deny"); return false; }
        public bool Allow(bool v) { Calls.Add("allow"); return true; }

        public void VoidGate(bool v) => Calls.Add("void");
        public int IntGate(object v) { Calls.Add("int"); return 1; }
    }

    [Fact]
    public void Lookup_ItemThenValue_ExactThenObject()
    {
        var vm = new Vm();
        var it = vm.Items[0];
        Assert.True(CommitGate.Allows(null, "T", vm, "Pick", true, typeof(bool), it));
        Assert.True(CommitGate.Allows(null, "T", vm, "ItemObj", true, typeof(bool), it)); // M(항목, object) 가 M(bool) 보다 먼저
        Assert.True(CommitGate.Allows(null, "T", vm, "Pick", true, typeof(bool)));        // 항목 없음 = M(값) 만
        Assert.True(CommitGate.Allows(null, "T", vm, "ValueOnly", true, typeof(bool), it)); // 항목형이 없으면 M(값)
        Assert.True(CommitGate.Allows(null, "T", vm, "ObjOnly", 3.5, typeof(double), it));
        Assert.Equal(["item,bool:a", "item,object:a", "bool", "bool", "object:3.5"], vm.Calls);
    }

    [Fact]
    public void Lookup_NullableMatchesUnderlying()
    {
        var vm = new NullableVm();
        Assert.True(CommitGate.Allows(null, "T", vm, "G", 3, typeof(int)));
        Assert.True(CommitGate.Allows(null, "T", vm, "G", 3, typeof(int?)));
        Assert.Equal(2, vm.Calls);
    }

    public sealed class NullableVm
    {
        public int Calls;
        public bool G(int? v) { Calls++; return true; }
    }

    // 값 매개변수는 정확한 형 또는 object 만 — 다른 기반 형·인터페이스는 찾지 않는다(전에는 위젯이 리플렉션 기본 바인더로 받았다)
    [Fact]
    public void Lookup_OtherBaseTypes_AreNotMatched()
    {
        var vm = new BaseVm();
        Assert.False(CommitGate.Allows(null, "T", vm, "G", true, typeof(bool)));
        Assert.False(CommitGate.Allows(null, "T", vm, "L", new List<string>(), typeof(List<string>)));
        Assert.Equal(0, vm.Calls);
    }

    public sealed class BaseVm
    {
        public int Calls;
        public bool G(IComparable v) { Calls++; return true; }
        public bool L(IList<string> v) { Calls++; return true; }
    }

    [Fact]
    public void Lookup_NonBoolReturn_IsNotAGate()
    {
        var vm = new Vm();
        Assert.False(CommitGate.Allows(null, "T", vm, "VoidGate", true, typeof(bool)));
        Assert.False(CommitGate.Allows(null, "T", vm, "IntGate", true, typeof(bool)));
        Assert.Empty(vm.Calls);
    }

    private static LayoutBox Build(string inner, object vm)
        => new LayoutEngine().Layout(MarkupParser.Parse($"<DvScreen>{inner}</DvScreen>"), vm, 300, 300);

    // 반복 안의 값 위젯도 bool M(항목, 값) 먼저(전에는 위젯은 M(값) 만)
    [Fact]
    public void ToggleInRepeat_GetsTheItemFirst()
    {
        var vm = new Vm();
        var root = Build("""<DvRepeat items="{Items}"><DvToggle value="{Bind Item.On}" onbeforecommit="Pick"/></DvRepeat>""", vm);
        TwoWayBinder.Commit(root.Children[0].Children[1], vm);
        Assert.Equal(["item,bool:b"], vm.Calls);
        Assert.True(vm.Items[1].On);
    }

    // 정확한 형 다음 object(전에는 토글·노브는 정확한 형만)
    [Fact]
    public void Toggle_FallsBackToObject()
    {
        var vm = new Vm();
        TwoWayBinder.Commit(Build("""<DvToggle value="{Bind Flag}" onbeforecommit="ObjOnly"/>""", vm).Children[0], vm);
        Assert.Equal(["object:True"], vm.Calls);
        Assert.True(vm.Flag);
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public bool On { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class GridVm
    {
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 3).Select(i => new Row { Id = i, Name = "R" + i }).ToList();
        public List<string> Calls { get; } = new();
        public bool CheckObj(Row r, object v) { Calls.Add($"row,object:{r.Id}:{v}"); return true; }
        public bool NameObj(object v) { Calls.Add("object:" + v); return true; }
    }

    private static DvSurface Grid(GridVm vm, string cols)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", cols), vm, DvTheme.Light);
        s.Layout(300, 300);
        return s;
    }

    // 그리드 체크 셀 — bool M(행, object) 도(전에는 bool 만)
    [Fact]
    public void GridCheckCell_AcceptsObjectForms()
    {
        var vm = new GridVm();
        var s = Grid(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"On\" width=\"60\" cell-type=\"check\" onbeforecommit=\"CheckObj\"/>");
        s.PointerDown(new SKPoint(90, 40 + 24));
        s.PointerUp(new SKPoint(90, 40 + 24));
        Assert.Equal(["row,object:0:True"], vm.Calls);
        Assert.True(vm.Rows[0].On);
    }

    // 리뷰 M4 — 확인할 수 없는 게이트(VM 없음)는 거절: onbeforecommit 이 있는데 VM 이 없으면 체크 셀을 쓰지 않는다(처음부터 Click 이 이렇게 동작 — 고정)
    [Fact]
    public void GridCheckCell_GateWithoutVm_NotWritten()
    {
        var vm = new GridVm();
        var s = Grid(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"On\" width=\"60\" cell-type=\"check\" onbeforecommit=\"CheckObj\"/>");
        var grid = Flatten(s.Root!).First(b => b.Grid is not null);
        var local = new SKPoint(90 - grid.Rect.Left, 40 + 24 - grid.Rect.Top);
        Assert.True(Devinno.UI.Interaction.GridInteraction.Click(grid, local, null, s));
        Assert.False(vm.Rows[0].On);
        Assert.Empty(vm.Calls);
        Assert.True(Devinno.UI.Interaction.GridInteraction.Click(grid, local, vm, s)); // 같은 자리, VM 있음 = 게이트 확인 후 씀
        Assert.True(vm.Rows[0].On);
    }

    // 그리드 편집 셀 — bool M(object) 도(전에는 정확한 형만)
    [Fact]
    public void GridEditCell_AcceptsObjectForm()
    {
        var vm = new GridVm();
        var s = Grid(vm, "<DvColumn field=\"Id\" width=\"60\"/><DvColumn field=\"Name\" cell-type=\"input\" onbeforecommit=\"NameObj\"/>");
        var grid = Flatten(s.Root!).First(b => b.Grid is not null);
        var slot = new GridCellSlot(s, grid, 1, vm.Rows[2]);
        Assert.Equal(CommitResult.Written, slot.Write("새 이름"));
        Assert.Equal(["object:새 이름"], vm.Calls);
        Assert.Equal("새 이름", vm.Rows[2].Name);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    // DvSwitch(내장 컴포넌트) — 인스턴스의 onbeforecommit 을 안쪽 토글이 쓴다(전에는 버려짐)
    [Fact]
    public void Switch_CallsItsOnBeforeCommit()
    {
        var vm = new Vm();
        var s = new DvSurface("""<DvScreen><DvSwitch label="자동" value="{Bind Flag}" onbeforecommit="Deny"/></DvScreen>""", vm, DvTheme.Light);
        s.Layout(400, 300);
        var toggle = Flatten(s.Root!).First(b => b.Node.Tag == "DvToggle");
        Assert.Equal("Deny", toggle.OnBeforeCommit);
        TwoWayBinder.Commit(toggle, vm);
        Assert.Equal(["deny"], vm.Calls);
        Assert.False(vm.Flag);

        var s2 = new DvSurface("""<DvScreen><DvSwitch label="자동" value="{Bind Flag}" onbeforecommit="Allow"/></DvScreen>""", vm, DvTheme.Light);
        s2.Layout(400, 300);
        TwoWayBinder.Commit(Flatten(s2.Root!).First(b => b.Node.Tag == "DvToggle"), vm);
        Assert.True(vm.Flag);
    }
}
