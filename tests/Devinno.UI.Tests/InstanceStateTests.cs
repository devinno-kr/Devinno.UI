using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 화면이 기억하는 상태(값 없는 탭 선택·익스팬더 펼침·트리 펼침)는 요소 인스턴스마다 따로 —
// DvRepeat 항목끼리, 같은 컴포넌트의 인스턴스끼리 섞이지 않는다. 평범한 요소는 다시 그려도 유지(기존 동작).
public class InstanceStateTests
{
    private const float W = 900, H = 700;

    public sealed class Node(string name, params Node[] kids)
    {
        public string Name { get; } = name;
        public Node[] Kids { get; } = kids;
    }

    public sealed class Vm
    {
        public string[] Items { get; } = ["a", "b"];
        public Node[] Plant { get; } = [new("Root", new Node("Child"))];
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static DvSurface Start(string body, ComponentRegistry? registry = null)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:10; gap:10\">{body}</DvScreen>", new Vm(), DvTheme.Light, registry);
        s.Layout(W, H);
        return s;
    }

    private static void Tap(DvSurface s, LayoutBox b)
    {
        var p = new SKPoint(b.Rect.Left + 6, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static string[] Bodies(DvSurface s) =>
        Flatten(s.Root!).Where(b => b.Node.Tag == "DvLabel" && b.ResolvedText is "one" or "two").Select(b => b.ResolvedText!).ToArray();

    private const string Tabs = """
        <DvTabs style="height:120">
          <DvTab text="1"><DvLabel text="one"/></DvTab>
          <DvTab text="2"><DvLabel text="two"/></DvTab>
        </DvTabs>
        """;

    [Fact]
    public void Tabs_InRepeat_EachItemRemembersItsOwnTab()
    {
        var s = Start($"<DvRepeat items=\"{{Items}}\">{Tabs}</DvRepeat>");
        Assert.Equal(["one", "one"], Bodies(s));

        var secondHeaderOfFirstItem = Flatten(s.Root!).Where(b => b.Node.Tag == "DvTabItem").ElementAt(1);
        Tap(s, secondHeaderOfFirstItem);

        Assert.Equal(["two", "one"], Bodies(s));
    }

    [Fact]
    public void Tabs_InTwoComponentInstances_AreSeparate()
    {
        var reg = new ComponentRegistry();
        reg.Register($"<DvComponent name=\"TabCard\">{Tabs}</DvComponent>");
        var s = Start("<TabCard/><TabCard/>", reg);

        Tap(s, Flatten(s.Root!).Where(b => b.Node.Tag == "DvTabItem").ElementAt(1));

        Assert.Equal(["two", "one"], Bodies(s));
    }

    [Fact]
    public void Tabs_Plain_KeepSelectionAcrossRebuilds()
    {
        var s = Start(Tabs);
        Tap(s, Flatten(s.Root!).Where(b => b.Node.Tag == "DvTabItem").ElementAt(1));
        s.Invalidate();
        s.Layout(W, H);
        Assert.Equal(["two"], Bodies(s));
    }

    [Fact]
    public void Expander_InRepeat_EachItemOpensAlone()
    {
        var s = Start("<DvRepeat items=\"{Items}\"><DvExpander header=\"h\"><DvLabel text=\"one\"/></DvExpander></DvRepeat>");
        Assert.Empty(Bodies(s));

        Tap(s, Flatten(s.Root!).First(b => b.Node.Tag == "DvExpanderHeader"));

        Assert.Single(Bodies(s));
        var expanders = Flatten(s.Root!).Where(b => b.Node.Tag == "DvExpander").ToList();
        Assert.True(expanders[0].ToggleOn);
        Assert.False(expanders[1].ToggleOn);
    }

    [Fact]
    public void Tree_InRepeat_EachItemExpandsAlone()
    {
        var s = Start("<DvRepeat items=\"{Items}\"><DvTreeView items=\"{Plant}\" item-children=\"Kids\" item-text=\"Name\" style=\"height:80\"/></DvRepeat>");
        var trees = Flatten(s.Root!).Where(b => b.Node.Tag == "DvTreeView").ToList();
        Assert.All(trees, t => Assert.Single(t.TreeRows));

        var first = trees[0];
        var p = new SKPoint(first.Rect.Left + 14, first.Rect.Top + 14); // 첫 행의 ▶ 자리
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);

        trees = Flatten(s.Root!).Where(b => b.Node.Tag == "DvTreeView").ToList();
        Assert.Equal(2, trees[0].TreeRows.Count);
        Assert.Single(trees[1].TreeRows);
    }
}
