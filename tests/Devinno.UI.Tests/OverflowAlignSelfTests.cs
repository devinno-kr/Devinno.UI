using Devinno.UI.Styling;
using Xunit;

public class OverflowAlignSelfTests
{
    [Fact] public void Default_overflow_is_visible()
        => Assert.Equal(Overflow.Visible, StyleParser.Parse("").Overflow);

    [Fact] public void Parses_overflow_hidden()
        => Assert.Equal(Overflow.Hidden, StyleParser.Parse("overflow:hidden").Overflow);

    [Fact] public void Parses_align_self_stretch()
        => Assert.Equal(AlignItems.Stretch, StyleParser.Parse("align-self:stretch").AlignSelf);

    [Fact] public void Align_self_null_by_default()
        => Assert.Null(StyleParser.Parse("color:#fff").AlignSelf);

    // 둥근 상자 + 꽉 찬 자식 띠(옛 OverflowDemo·AccentStripCheck 의 그림을 검사로)
    private static SkiaSharp.SKBitmap Card(bool hidden)
    {
        var of = hidden ? " overflow:hidden;" : "";
        var markup =
            "<DvScreen style=\"background:#ffffff; padding:20\">" +
            $"<DvBox style=\"width:200; height:100; border-radius:20;{of} display:flex; flex-direction:row\">" +
            "<DvBox style=\"width:40; background:#ff0000; align-self:stretch\"/>" +
            "</DvBox></DvScreen>";
        return SkiaSharp.SKBitmap.Decode(Devinno.UI.DvRenderer.RenderToPng(markup, null, 240, 140));
    }

    [Fact] public void Overflow_hidden_clips_child_to_rounded_corner()
    {
        using var clipped = Card(hidden: true);
        using var plain = Card(hidden: false);
        Assert.Equal(SkiaSharp.SKColors.White, clipped.GetPixel(21, 21));          // 모서리 바깥 = 잘림
        Assert.Equal(new SkiaSharp.SKColor(255, 0, 0), plain.GetPixel(21, 21));    // overflow 없으면 삐져나옴
        Assert.Equal(new SkiaSharp.SKColor(255, 0, 0), clipped.GetPixel(40, 70));  // 모서리 안쪽은 그대로
    }

    [Fact] public void Align_self_stretch_fills_cross_axis()
    {
        using var bmp = Card(hidden: false);
        Assert.Equal(new SkiaSharp.SKColor(255, 0, 0), bmp.GetPixel(40, 118));     // 높이 지정 없이 100 까지 늘어남
    }
}
