using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Xunit;

// 스크롤 상자를 품은 상자는 공간이 모자랄 때 그 스크롤 상자가 줄어드는 만큼 같이 줄어든다
// (<DvCard grow="1"><DvStack grow="1" style="overflow:scroll"> 가 height:0 없이 스크롤 — CSS 는 min-height:0 이 필요한 곳)
public class ScrollContainerShrinkTests
{
    private const float W = 600, H = 400;

    private static string Rows(int n) => string.Concat(Enumerable.Range(1, n).Select(i => $"<DvLabel text=\"행 {i}\" style=\"height:30\"/>"));

    private static (DvSurface S, LayoutBox Root) Layout(string body)
    {
        var s = new DvSurface($"<DvStack style=\"padding:0; gap:0\">{body}</DvStack>", null, DvTheme.Dark); // DvPageFrame 처럼 flex 세로 스택
        s.Layout(W, H);
        return (s, s.Root!);
    }

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    private static LayoutBox Id(LayoutBox root, string id) =>
        All(root).First(b => b.Node.Attributes.GetValueOrDefault("id") == id || b.Origin.Attributes.GetValueOrDefault("id") == id);

    [Fact]
    public void CardWithScrollList_GrowOnly_FitsAndScrolls()
    {
        // 문서 3.7 패턴 그대로(height:0 없음)
        var (_, root) = Layout($"""
            <DvLabel text="머리" style="height:40"/>
            <DvCard id="card" title="목록" grow="1">
              <DvStack id="list" gap="xs" grow="1" style="overflow:scroll">{Rows(40)}</DvStack>
            </DvCard>
            <DvLabel id="foot" text="아래" style="height:30"/>
            """);
        var card = Id(root, "card");
        var list = Id(root, "list");
        var foot = Id(root, "foot");
        Assert.True(foot.Rect.Bottom <= H + 0.5f, $"아래 글자가 창 안에 남는다 {foot.Rect}");
        Assert.True(card.Rect.Height < 400, $"카드가 내용(40행) 높이로 커지지 않는다 {card.Rect}");
        Assert.True(list.MaxScrollY > 0, "안쪽 목록이 스크롤된다");
    }

    [Fact]
    public void EnoughSpace_Unchanged()
    {
        // 내용이 들어가면 지금과 같다 — 카드 = 내용 + 남은 몫
        var (_, root) = Layout($"""
            <DvCard id="card" title="목록" grow="1">
              <DvStack id="list" gap="xs" grow="1" style="overflow:scroll">{Rows(3)}</DvStack>
            </DvCard>
            """);
        Assert.Equal(H, Id(root, "card").Rect.Height, 0.5);
        Assert.Equal(0, Id(root, "list").MaxScrollY, 0.5);
    }

    [Fact]
    public void NestedPlainStack_AlsoShrinks()
    {
        // 카드 → 일반 세로 스택 → 스크롤 상자: 중간 상자도 같이 줄어든다
        var (_, root) = Layout($"""
            <DvCard id="card" title="목록" grow="1">
              <DvStack id="mid" gap="sm" grow="1">
                <DvLabel text="부제" style="height:20"/>
                <DvStack id="list" grow="1" style="overflow:scroll">{Rows(40)}</DvStack>
              </DvStack>
            </DvCard>
            """);
        Assert.True(Id(root, "card").Rect.Bottom <= H + 0.5f);
        Assert.True(Id(root, "list").MaxScrollY > 0);
    }

    [Fact]
    public void MinHeight_OfScrollList_Respected()
    {
        var (_, root) = Layout($"""
            <DvLabel text="머리" style="height:300"/>
            <DvCard id="card" title="목록" grow="1">
              <DvStack id="list" grow="1" style="overflow:scroll; min-height:200">{Rows(40)}</DvStack>
            </DvCard>
            """);
        Assert.True(Id(root, "list").Rect.Height >= 199.5f, $"{Id(root, "list").Rect}");
    }

    [Fact]
    public void WithoutScrollInside_DoesNotShrink()
    {
        // 스크롤 상자가 없으면 지금처럼 줄지 않는다(내용이 넘침)
        var (_, root) = Layout($"""
            <DvCard id="card" title="목록" grow="1">
              <DvStack gap="xs">{Rows(40)}</DvStack>
            </DvCard>
            """);
        Assert.True(Id(root, "card").Rect.Height > H);
    }

    [Fact]
    public void ExplicitFlexShrinkZero_StaysAsBefore()
    {
        var (_, root) = Layout($"""
            <DvCard id="card" title="목록" grow="1" style="flex-shrink:0">
              <DvStack id="list" grow="1" style="overflow:scroll">{Rows(40)}</DvStack>
            </DvCard>
            """);
        Assert.True(Id(root, "card").Rect.Height > H);
    }

    [Fact]
    public void RowScroller_InColumn_SideBySide()
    {
        // 옆으로 나란한 두 칸 중 하나만 스크롤 — 줄은 다른 칸(고정 내용) 높이까지만 줄어든다
        var (_, root) = Layout($"""
            <DvRow id="row" grow="1" align="stretch">
              <DvStack id="list" grow="1" style="overflow:scroll">{Rows(40)}</DvStack>
              <DvStack id="side" style="width:100">{Rows(5)}</DvStack>
            </DvRow>
            """);
        Assert.True(Id(root, "row").Rect.Height <= H + 0.5f);
        Assert.True(Id(root, "list").MaxScrollY > 0);
    }
}
