using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 핸들의 Bounds 는 창 기준(조상 스크롤·내비게이션 본문 위치 반영)이고, Enabled/Visible/Text 읽기는 실제 레이아웃 박스를 따른다.
public class ControlBoundsTests
{
    public sealed class Vm : IDvControlHost
    {
        public DvButton? btn;
        public DvControl? p1;
        public void Attach(DvControlLookup c) { btn = c.Bind(btn, "btn"); p1 = c.Bind(p1, "p1"); }
    }

    private static (Vm vm, DvSurface s) Make(string markup, ComponentRegistry? reg = null)
    {
        var vm = new Vm();
        var s = new DvSurface(markup, vm, reg);
        return (vm, s);
    }

    [Fact]
    public void Bounds_MatchesLayoutRect()
    {
        var (vm, s) = Make("<DvScreen style=\"padding:0\"><DvButton id=\"btn\" style=\"width:100; height:40\"/></DvScreen>");
        s.Layout(400, 300);
        Assert.Equal(SKRect.Create(0, 0, 100, 40), vm.btn!.Bounds);
    }

    [Fact]
    public void Bounds_EmptyBeforeLayout_AndWhenInvisible()
    {
        var (vm, s) = Make("<DvScreen style=\"padding:0\"><DvButton id=\"btn\" style=\"width:100; height:40\"/></DvScreen>");
        Assert.Equal(SKRect.Empty, vm.btn!.Bounds);
        s.Layout(400, 300);
        Assert.True(vm.btn.Visible);
        vm.btn.Visible = false;
        s.Layout(400, 300);
        Assert.Equal(SKRect.Empty, vm.btn.Bounds);
        Assert.False(vm.btn.Visible);
    }

    [Fact]
    public void Bounds_InsideScroller_SubtractsScroll()
    {
        var (vm, s) = Make(
            "<DvScreen style=\"padding:0; gap:0\">" +
            "<DvBox style=\"overflow:scroll; height:100; padding:0; gap:0\">" +
            "<DvBox style=\"height:150\"/>" +
            "<DvButton id=\"btn\" style=\"width:100; height:40\"/>" +
            "</DvBox></DvScreen>");
        s.Layout(400, 300);
        Assert.Equal(150, vm.btn!.Bounds.Top, 3);
        s.PointerWheel(new SKPoint(10, 10), -50f / 48f); // 한 칸 48 → 50 아래로
        Assert.Equal(50, s.Root!.Children[0].ScrollY, 3);
        Assert.Equal(100, vm.btn.Bounds.Top, 3);
    }

    [Fact]
    public void Bounds_UpdatesOnResize()
    {
        var (vm, s) = Make(
            "<DvScreen style=\"padding:0; display:flex; flex-direction:row; gap:0\">" +
            "<DvBox grow=\"1\"/><DvButton id=\"btn\" style=\"width:100; height:40\"/></DvScreen>");
        s.Layout(400, 300);
        Assert.Equal(300, vm.btn!.Bounds.Left, 3);
        s.Layout(800, 300);
        Assert.Equal(700, vm.btn.Bounds.Left, 3);
    }

    [Fact]
    public void ComponentInstance_IdTargetsInstanceRoot()
    {
        var reg = new ComponentRegistry();
        reg.Register("<DvComponent name=\"Tile\"><DvBox style=\"height:80\"><DvButton text=\"x\"/></DvBox></DvComponent>");
        var (vm, s) = Make("<DvScreen style=\"padding:0; gap:0\"><Tile id=\"p1\"/><Tile/></DvScreen>", reg);
        s.Layout(400, 300);
        var first = s.Root!.Children[0];
        var second = s.Root!.Children[1];
        Assert.Equal(first.Rect, vm.p1!.Bounds);
        Assert.NotEqual(second.Rect, vm.p1.Bounds);

        vm.p1.Enabled = false;
        s.Layout(400, 300);
        Assert.True(s.Root!.Children[0].Disabled);
        Assert.False(s.Root!.Children[1].Disabled);
        Assert.False(vm.p1.Enabled);

        vm.p1.Visible = false;
        s.Layout(400, 300);
        Assert.Single(s.Root!.Children);
        Assert.Equal(SKRect.Empty, vm.p1.Bounds);
    }

    [Fact]
    public void Enabled_Read_IncludesInheritedDisable()
    {
        var (vm, s) = Make("<DvStack disabled=\"true\"><DvButton id=\"btn\"/></DvStack>");
        s.Layout(400, 300);
        Assert.False(vm.btn!.Enabled);
    }

    [Fact]
    public void Enabled_ReadAfterWrite_BeforeLayout()
    {
        var (vm, s) = Make("<DvStack><DvButton id=\"btn\"/></DvStack>");
        s.Layout(400, 300);
        Assert.True(vm.btn!.Enabled);
        vm.btn.Enabled = false;
        Assert.False(vm.btn.Enabled); // 다음 Layout 전에도 바로 반영
    }

    [Fact]
    public void Visible_ReadAfterWrite_BeforeLayout()
    {
        var (vm, s) = Make("<DvStack><DvButton id=\"btn\"/></DvStack>");
        s.Layout(400, 300);
        Assert.True(vm.btn!.Visible);
        vm.btn.Visible = false;
        Assert.False(vm.btn.Visible);
    }

    [Fact]
    public void Visible_Read_FalseWhenAncestorHidden()
    {
        var (vm, s) = Make("<DvStack><DvBox visible=\"false\"><DvButton id=\"btn\"/></DvBox></DvStack>");
        s.Layout(400, 300);
        Assert.False(vm.btn!.Visible);
    }

    [Fact]
    public void Text_Read_FallsBackToResolvedMarkupText()
    {
        var (vm, s) = Make("<DvStack><DvButton id=\"btn\" text=\"A\"/></DvStack>");
        Assert.Null(vm.btn!.Text);
        s.Layout(400, 300);
        Assert.Equal("A", vm.btn.Text);
        vm.btn.Text = "B";
        Assert.Equal("B", vm.btn.Text);
    }
}
