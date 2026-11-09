using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Controls;
using SkiaSharp;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Xunit;

// 공정도 장비의 id 핸들 — 코드의 Visible 덮어쓰기가 일반 요소와 같이 적용된다.
public class ProcessHandleTests
{
    public sealed class Vm : IDvControlHost
    {
        public DvControl? p1;
        public void Attach(DvControlLookup c) => p1 = c.Bind(p1, "p1");
    }

    private const string Markup = """
        <DvScreen><DvProcess>
          <DvPump id="p1" name="P1" label="P-101"/>
          <DvPump name="P2" label="P-102"/>
          <DvLine from="P1.out" to="P2.in"/>
        </DvProcess></DvScreen>
        """;

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static bool HasPump(DvSurface s, string name) =>
        Flatten(s.Root!).Any(b => b.Node.Tag == "DvPump" && b.Node.Attributes.GetValueOrDefault("name") == name);

    [Fact]
    public void Equipment_HandleVisibleFalse_RemovesIt()
    {
        var vm = new Vm();
        var s = new DvSurface(Markup, vm, DvTheme.Light);
        s.Layout(800, 500);
        Assert.True(HasPump(s, "P1"));

        vm.p1!.Visible = false;
        s.Layout(800, 500);
        Assert.False(HasPump(s, "P1"));
        Assert.True(HasPump(s, "P2"));

        vm.p1.ResetOverrides();
        s.Layout(800, 500);
        Assert.True(HasPump(s, "P1"));
    }
    // 유닛 컴포넌트(템플릿 루트가 DvUnit) 인스턴스 — 일반 컴포넌트처럼 id 핸들·onclick·style 이 인스턴스에서 유닛 상자로 이어진다.
    public sealed class Blend { public required string Name { get; init; } }

    public sealed class UnitVm : IDvControlHost
    {
        public DvControl? bl1;
        public Blend BL1 { get; } = new() { Name = "BL-1" };
        public Blend BL2 { get; } = new() { Name = "BL-2" };
        public List<string> Hits { get; } = new();
        public void Open(Blend u) => Hits.Add(u.Name);
        public void Attach(DvControlLookup c) => bl1 = c.Bind(bl1, "bl1");
    }

    private const string UnitDef = """
        <DvComponent name="Blender">
          <DvParam name="U"/>
          <DvUnit label="{U.Name}">
            <DvPump name="P1"/>
            <DvPort name="out" side="bottom"/>
            <DvLine from="P1.out" to="out"/>
          </DvUnit>
        </DvComponent>
        """;

    private static (DvSurface S, UnitVm Vm) StartUnits()
    {
        var reg = new ComponentRegistry();
        reg.Register(UnitDef);
        var vm = new UnitVm();
        var s = new DvSurface("""
            <DvScreen><DvProcess>
              <Blender id="bl1" name="BL1" U="{BL1}" onclick="Open" style="background:#ff0000"/>
              <Blender name="BL2" U="{BL2}"/>
            </DvProcess></DvScreen>
            """, vm, DvTheme.Light, reg);
        s.Layout(1000, 700);
        return (s, vm);
    }

    private static List<LayoutBox> Units(DvSurface s) =>
        Flatten(s.Root!).Where(b => b.Node.Tag == "DvUnit").ToList();

    [Fact]
    public void UnitInstance_HandleConnects_BoundsAndEnabled()
    {
        var (s, vm) = StartUnits();
        Assert.False(vm.bl1!.Bounds.IsEmpty);
        Assert.Equal(Units(s)[0].Rect.Width, vm.bl1.Bounds.Width, 1);

        vm.bl1.Enabled = false;
        s.Layout(1000, 700);
        Assert.True(Units(s)[0].Disabled);
        Assert.False(Units(s)[1].Disabled);
    }

    [Fact]
    public void UnitInstance_OnclickAndStyle_PassThrough()
    {
        var (s, vm) = StartUnits();
        Assert.Equal(new DvColor(0xff, 0, 0, 0xff), Units(s)[0].Style.Background);

        var u = Units(s)[0];
        var p = new SKPoint(u.Rect.Left + 4, u.Rect.Top + 4);
        s.PointerDown(p);
        s.PointerUp(p);
        Assert.Equal(["BL-1"], vm.Hits);
    }

    [Fact]
    public void UnitInstance_HandleClick_Fires()
    {
        var (s, vm) = StartUnits();
        var clicks = 0;
        vm.bl1!.Click += (_, _) => clicks++;
        s.Layout(1000, 700);
        var u = Units(s)[0];
        var p = new SKPoint(u.Rect.Left + 4, u.Rect.Top + 4);
        s.PointerDown(p);
        s.PointerUp(p);
        Assert.Equal(1, clicks);
    }
}
