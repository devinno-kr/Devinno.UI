using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 컴포넌트 인스턴스의 onclick·disabled 전달, 정의 안 DvStyle, 템플릿 안 DvDataGrid 열 정의
public class ComponentPassthroughTests
{
    public sealed class Pump
    {
        public string Name { get; set; } = "";
    }

    public sealed class Vm
    {
        public bool Locked { get; set; }
        public List<string> Calls { get; } = new();
        public List<Pump> Pumps { get; } = new() { new Pump { Name = "P1" }, new Pump { Name = "P2" } };
        public void OpenPump() => Calls.Add("OpenPump");
        public void Open(Pump p) => Calls.Add("Open:" + p.Name);
        public void Inner() => Calls.Add("Inner");
        public void Outer() => Calls.Add("Outer");
    }

    private static DvSurface Surface(string body, Vm vm, ComponentRegistry? reg = null)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:0; gap:0\">{body}</DvScreen>", vm, reg);
        s.Layout(400, 300);
        return s;
    }

    private static void Click(DvSurface s, LayoutBox b)
    {
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
    }

    [Fact]
    public void Builtin_card_instance_onclick_reaches_template_root()
    {
        var vm = new Vm();
        var s = Surface("<DvCard title=\"펌프\" onclick=\"OpenPump\"/>", vm);
        Assert.Equal("OpenPump", s.Root!.Children[0].OnClick);
        Click(s, s.Root!.Children[0]);
        Assert.Equal(new[] { "OpenPump" }, vm.Calls);
    }

    private static ComponentRegistry Tile(string rootAttrs = "")
    {
        var reg = new ComponentRegistry();
        reg.Register($"<DvComponent name=\"Tile\"><DvParam name=\"t\"/><DvBox {rootAttrs} style=\"height:80\"><DvLabel text=\"{{t}}\"/></DvBox></DvComponent>");
        return reg;
    }

    [Fact]
    public void Instance_onclick_wins_over_template_root_onclick()
    {
        var vm = new Vm();
        var s = Surface("<Tile t=\"a\" onclick=\"Outer\"/>", vm, Tile("onclick=\"Inner\""));
        Click(s, s.Root!.Children[0]);
        Assert.Equal(new[] { "Outer" }, vm.Calls);
    }

    [Fact]
    public void Template_root_onclick_is_kept_without_instance_onclick()
    {
        var vm = new Vm();
        var s = Surface("<Tile t=\"a\"/>", vm, Tile("onclick=\"Inner\""));
        Click(s, s.Root!.Children[0]);
        Assert.Equal(new[] { "Inner" }, vm.Calls);
    }

    [Fact]
    public void Instance_disabled_reaches_root_and_resolves_against_outer_vm()
    {
        var vm = new Vm { Locked = true };
        var s = Surface("<DvCard title=\"펌프\" onclick=\"OpenPump\" disabled=\"{Data.Locked}\"/>", vm);
        Assert.True(s.Root!.Children[0].Disabled);
        Click(s, s.Root!.Children[0]);
        Assert.Empty(vm.Calls);

        var user = Surface("<Tile t=\"a\" disabled=\"true\"/>", new Vm(), Tile());
        Assert.True(user.Root!.Children[0].Disabled);
    }

    [Fact]
    public void Instance_onclick_inside_repeat_receives_the_item()
    {
        var vm = new Vm();
        var s = Surface("<DvRepeat items=\"{Data.Pumps}\"><Tile t=\"{Item.Name}\" onclick=\"Open\"/></DvRepeat>", vm, Tile());
        Click(s, s.Root!.Children[0].Children[1]);
        Assert.Equal(new[] { "Open:P2" }, vm.Calls);
    }

    // ── 정의 안 DvStyle
    private const string StyledDef = """
        <DvComponent name="Badge2">
          <DvStyle>.b2 { background:#ff0000; height:30 } .shared { background:#00ff00 }</DvStyle>
          <DvParam name="t"/>
          <DvBox class="b2"><DvLabel text="{t}"/></DvBox>
        </DvComponent>
        """;

    [Fact]
    public void Leading_dvstyle_is_not_the_template_root_and_its_rules_apply()
    {
        var reg = new ComponentRegistry();
        reg.Register(StyledDef);
        Assert.True(reg.TryGet("Badge2", out var def));
        Assert.Equal("DvBox", def!.Template.Tag);

        var box = new LayoutEngine().Layout(MarkupParser.Parse("<DvScreen><Badge2 t=\"x\"/></DvScreen>"), null, 200, 200, reg).Children[0];
        Assert.Equal("DvBox", box.Node.Tag);
        Assert.Equal(new DvColor(0xff, 0, 0, 0xff), box.Style.Background);
    }

    [Fact]
    public void Component_dvstyle_is_global_and_screen_dvstyle_wins()
    {
        var reg = new ComponentRegistry();
        reg.Register(StyledDef);
        var root = MarkupParser.Parse("""
            <DvScreen>
              <DvStyle>.shared { background:#0000ff }</DvStyle>
              <DvBox class="b2"/>
              <DvBox class="shared"/>
            </DvScreen>
            """);
        var box = new LayoutEngine().Layout(root, null, 200, 200, reg);
        Assert.Equal(new DvColor(0xff, 0, 0, 0xff), box.Children[0].Style.Background); // 컴포넌트 DvStyle 이 화면 요소에도
        Assert.Equal(new DvColor(0, 0, 0xff, 0xff), box.Children[1].Style.Background); // 같은 클래스는 화면 DvStyle 이 이김
    }

    // ── 템플릿 안 DvDataGrid: 열 정의는 박스가 되지 않는다
    [Fact]
    public void Grid_column_definitions_inside_template_are_not_built_as_boxes()
    {
        var reg = new ComponentRegistry();
        reg.Register("""
            <DvComponent name="Table">
              <DvParam name="rows"/>
              <DvDataGrid items="{rows}" style="height:200">
                <DvColumnGroup header="그룹"><DvColumn header="이름" field="Name"/></DvColumnGroup>
                <DvSummaryRow><DvSummary field="Name" fn="count"/></DvSummaryRow>
              </DvDataGrid>
            </DvComponent>
            """);
        var vm = new Vm();
        var screen = new LayoutEngine().Layout(MarkupParser.Parse("<DvScreen><Table rows=\"{Data.Pumps}\"/></DvScreen>"), vm, 400, 300, reg);
        var direct = new LayoutEngine().Layout(MarkupParser.Parse("""
            <DvScreen><DvDataGrid items="{Data.Pumps}" style="height:200">
              <DvColumnGroup header="그룹"><DvColumn header="이름" field="Name"/></DvColumnGroup>
              <DvSummaryRow><DvSummary field="Name" fn="count"/></DvSummaryRow>
            </DvDataGrid></DvScreen>
            """), vm, 400, 300);

        Assert.Equal(direct.Children[0].Children.Count, screen.Children[0].Children.Count);
        Assert.Empty(screen.Children[0].Children);
    }
}
