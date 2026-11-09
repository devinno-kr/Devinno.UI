using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Xunit;

// R12 — 자식 노드(DvTab·DvOption·DvColumn …)도 그 노드 핸들의 코드 덮어쓰기를 따른다.
// DvTab·DvOption·DvColumn 의 핸들은 DvControl 이라 Text 공개 속성이 없다 — 덮어쓰기 경로(SetOverrideText)를 직접 쓴다.
public class ChildOverrideTests
{
    public sealed class Vm : IDvControlHost
    {
        public DvControl? tab2, opt1, col1;
        public string Pick { get; set; } = "a";
        public List<Row> Rows { get; } = [new()];
        public void Attach(DvControlLookup c)
        {
            tab2 = c.Bind(tab2, "tab2");
            opt1 = c.Bind(opt1, "opt1");
            col1 = c.Bind(col1, "col1");
        }
    }

    public sealed class Row
    {
        public int A { get; set; }
    }

    private const string Markup = """
        <DvScreen>
          <DvTabs style="height:120">
            <DvTab text="One"><DvLabel text="one"/></DvTab>
            <DvTab id="tab2" text="Two"><DvLabel text="two"/></DvTab>
          </DvTabs>
          <DvSelect value="{Bind Pick}">
            <DvOption id="opt1" value="a" text="Alpha"/>
            <DvOption value="b" text="Beta"/>
          </DvSelect>
          <DvDataGrid items="{Rows}" style="height:120">
            <DvColumn id="col1" field="A" cell-type="button" text="Go"/>
          </DvDataGrid>
        </DvScreen>
        """;

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static (Vm vm, DvSurface s) Make()
    {
        var vm = new Vm();
        var s = new DvSurface(Markup, vm, DvTheme.Light);
        s.Layout(800, 600);
        return (vm, s);
    }

    private static LayoutBox TabItem(DvSurface s, int i) => Flatten(s.Root!).Where(b => b.Node.Tag == "DvTabItem").ElementAt(i);

    [Fact]
    public void Tab_CodeText_ShowsInHeader()
    {
        var (vm, s) = Make();
        Assert.Equal("Two", TabItem(s, 1).ResolvedText);

        vm.tab2!.SetOverrideText("Renamed");
        s.Layout(800, 600);
        Assert.Equal("Renamed", TabItem(s, 1).ResolvedText);

        vm.tab2.ResetOverrides();
        s.Layout(800, 600);
        Assert.Equal("Two", TabItem(s, 1).ResolvedText);
    }

    [Fact]
    public void Tab_CodeEnabledFalse_DisablesHeaderItem()
    {
        var (vm, s) = Make();
        Assert.False(TabItem(s, 1).Disabled);

        vm.tab2!.Enabled = false;
        s.Layout(800, 600);
        Assert.True(TabItem(s, 1).Disabled);
        Assert.False(TabItem(s, 0).Disabled);
    }

    [Fact]
    public void Option_CodeText_ShowsInOptions()
    {
        var (vm, s) = Make();
        var select = Flatten(s.Root!).First(b => b.Node.Tag == "DvSelect");
        Assert.Equal("Alpha", select.SelectOptions[0].Text);

        vm.opt1!.SetOverrideText("First");
        s.Layout(800, 600);
        select = Flatten(s.Root!).First(b => b.Node.Tag == "DvSelect");
        Assert.Equal("First", select.SelectOptions[0].Text);
        Assert.Equal("a", select.SelectOptions[0].Value);
    }

    [Fact]
    public void Column_CodeText_ShowsOnButtonCells()
    {
        var (vm, s) = Make();
        var grid = Flatten(s.Root!).First(b => b.Node.Tag == "DvDataGrid");
        Assert.Equal("Go", grid.Grid!.Columns[0].ButtonText);

        vm.col1!.SetOverrideText("Run");
        s.Layout(800, 600);
        grid = Flatten(s.Root!).First(b => b.Node.Tag == "DvDataGrid");
        Assert.Equal("Run", grid.Grid!.Columns[0].ButtonText);
    }
}
