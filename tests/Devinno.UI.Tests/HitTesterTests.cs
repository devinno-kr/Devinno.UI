using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using SkiaSharp;
using Xunit;

public class HitTesterTests
{
    [Fact]
    public void Returns_deepest_box_containing_point()
    {
        var root = MarkupParser.Parse(
            "<DvScreen style=\"padding:10; display:flex; flex-direction:column; gap:8\">" +
            "<DvButton text=\"A\" style=\"width:80; height:30\" onclick=\"X\"/>" +
            "</DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 200, 200);

        var hit = HitTester.HitTest(box, new SKPoint(20, 20));
        Assert.NotNull(hit);
        Assert.Equal("DvButton", hit!.Node.Tag);

        var miss = HitTester.HitTest(box, new SKPoint(150, 150));
        Assert.Equal("DvScreen", miss!.Node.Tag);
    }

    // 편집기: 컴포넌트 안을 눌러도 경로를 거슬러 올라가면 편집 파일의 인스턴스 노드가 나온다
    [Fact]
    public void HitPath_ReachesTheComponentInstanceNode()
    {
        var reg = new Devinno.UI.Components.ComponentRegistry();
        reg.Register("<DvComponent name=\"Card\"><DvBox style=\"padding:10\"><DvLabel text=\"x\"/></DvBox></DvComponent>");
        var root = MarkupParser.Parse("<DvScreen><Card/></DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 200, 200, reg);

        var path = HitTester.HitPath(box, new SKPoint(14, 14));
        Assert.Equal("DvLabel", path[^1].Box.Node.Tag);
        var instance = root.Children[0];
        Assert.DoesNotContain(path, e => ReferenceEquals(e.Box.Node, instance)); // 노드는 공유 템플릿
        Assert.Contains(path, e => ReferenceEquals(e.Box.Origin, instance));      // Origin 이 인스턴스
        Assert.Same(root, path[0].Box.Origin);
    }

    // 모양이 정해진 위젯은 그려진 모양 안만 — wholeBox 면 늘어난 상자 전체
    [Fact]
    public void HitPath_WholeBox_IgnoresTheDrawnShape()
    {
        var root = MarkupParser.Parse("<DvScreen><DvKnob style=\"width:200; height:100\"/></DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 300, 300);
        var knob = box.Children[0];
        var corner = new SKPoint(knob.Rect.Right - 2, knob.Rect.Bottom - 2);

        Assert.Equal("DvScreen", HitTester.HitPath(box, corner)[^1].Box.Node.Tag);
        Assert.Same(knob, HitTester.HitPath(box, corner, wholeBox: true)[^1].Box);
        Assert.Empty(HitTester.HitPath(box, new SKPoint(-5, -5)));
    }
}
