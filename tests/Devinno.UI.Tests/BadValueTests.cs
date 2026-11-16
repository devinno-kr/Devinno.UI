using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 잘못 넣은 값은 무시되거나 안전한 값이 된다 — 숫자 토큰의 NaN/무한대, 쓸 수 없는 ::part() 값, 1 을 넘는 상태 덧칠 세기
public class BadValueTests
{
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Number_token_rejects_non_finite_values(string raw)
    {
        var t = DvTheme.Dark.With(new Dictionary<string, string> { ["texture.depth"] = raw });
        Assert.Equal(0, ThemeResolver.Number(t, "texture.depth"));
    }

    [Theory]
    [InlineData("font-size: big")]
    [InlineData("font-size: token(missing-size)")]
    [InlineData("color: notacolor")]
    [InlineData("font-weight: heavy")]
    public void Unusable_part_value_is_ignored(string decl)
    {
        var p = PartStyle.Parse(decl);
        Assert.Null(p.FontSize);
        Assert.Null(p.Color);
        Assert.Null(p.FontWeight);
    }

    [Fact]
    public void Usable_part_values_are_kept()
    {
        var p = PartStyle.Parse("font-size: 28; color: #ff0000; font-weight: 600");
        Assert.Equal(28, p.FontSize);
        Assert.Equal(new SKColor(0xff, 0, 0), p.Color);
        Assert.Equal(600, p.FontWeight);
    }

    public sealed class Probe : DvWidget
    {
        public static SKColor Seen;
        public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(60, 30);
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) => Seen = ctx.StateOverlay(new SKColor(0xf0, 0xf0, 0xf0));
    }

    [Fact]
    public void State_overlay_clamps_a_strength_above_one()
    {
        // 누름 상태를 만들기 어려우니 hover 세기를 1.5 로 두고 hover 시킨다
        var theme = DvTheme.Light.With(new Dictionary<string, string> { ["overlay.hover"] = "1.5" });
        var r = new ComponentRegistry();
        r.RegisterWidget<Probe>("Probe");
        var s = new DvSurface("<DvScreen style=\"padding:0\"><Probe onclick=\"X\"/></DvScreen>", new { }, theme, r);
        s.Layout(100, 50);
        s.PointerMove(new SKPoint(10, 10));
        using var bmp = new SKBitmap(100, 50);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 100, 50);
        // 세기 1 이 끝 — text 색 그대로(바이트가 넘쳐 뒤집히지 않음)
        Assert.Equal(ThemeResolver.Color(theme, "text"), Probe.Seen.WithAlpha(0xff));
    }
}
