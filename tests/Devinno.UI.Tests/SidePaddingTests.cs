using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Xunit;

// 한쪽 패딩 키(padding-top/right/bottom/left) — CSS 와 같이 뒤에 온 선언이 그쪽 값만 바꾼다.
public class SidePaddingTests
{
    [Fact]
    public void One_side_keys_change_only_that_side_and_order_matters()
    {
        var a = StyleParser.Parse("padding:12; padding-left:0; padding-top:4px");
        Assert.Equal(new[] { 4f, 12f, 12f, 0f }, new[] { a.Padding.Top, a.Padding.Right, a.Padding.Bottom, a.Padding.Left });

        var b = StyleParser.Parse("padding-left:20; padding:8"); // 뒤의 padding 이 덮음
        Assert.Equal(8f, b.Padding.Left);

        Assert.Equal(0f, StyleParser.Parse("padding-left: 4 8").Padding.Left); // 값 하나만 받음
    }

    [Fact]
    public void Inline_side_key_beats_class_padding_in_layout()
    {
        var root = new LayoutEngine().Layout(MarkupParser.Parse("""
            <DvScreen style="padding:0; align-items:start">
              <DvStyle>.pad { padding: 10 }</DvStyle>
              <DvBox class="pad" style="padding-left:30"><DvBox style="width:20; height:20"/></DvBox>
            </DvScreen>
            """), null, 400, 300);
        var box = root.Children.First(b => b.Node.Tag == "DvBox");
        var inner = box.Children[0];
        Assert.Equal(30, inner.Rect.Left - box.Rect.Left, 3);
        Assert.Equal(10, inner.Rect.Top - box.Rect.Top, 3);
        Assert.Equal(20 + 30 + 10, box.Rect.Width, 3);
    }
}
