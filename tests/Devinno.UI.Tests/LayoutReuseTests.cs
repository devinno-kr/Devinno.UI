using Devinno.UI;
using Devinno.UI.Text;
using Xunit;

// 리사이즈 비용: 크기만 바뀌면 트리 재사용, 측정 결과 재사용, 폴백 폰트 탐색 캐시
public class LayoutReuseTests
{
    private const string Markup =
        "<DvScreen style=\"padding:0; display:flex; flex-direction:row; align-items:stretch\">" +
        "<DvBox style=\"flex-grow:1; background:#111\"/><DvLabel text=\"펌프 #1\"/></DvScreen>";

    [Fact]
    public void Fallback_typeface_lookup_is_cached()
    {
        var a = TextFont.ResolveTypeface("한글 텍스트 가나다");
        var b = TextFont.ResolveTypeface("한글 텍스트 가나다");
        Assert.Same(a, b);
    }

    [Fact]
    public void Size_change_alone_rearranges_the_existing_tree()
    {
        var s = new DvSurface(Markup, null);
        s.Layout(300, 100);
        var tree = s.Root;
        var growW = tree!.Children[0].Rect.Width;

        s.Layout(500, 100);

        Assert.Same(tree, s.Root);                       // 재빌드 없음
        Assert.Equal(growW + 200, s.Root!.Children[0].Rect.Width, 3); // 배치는 새 크기로
    }

    [Fact]
    public void Invalidate_still_rebuilds()
    {
        var s = new DvSurface(Markup, null);
        s.Layout(300, 100);
        var tree = s.Root;
        s.Invalidate();
        s.Layout(500, 100);
        Assert.NotSame(tree, s.Root);
    }
}
