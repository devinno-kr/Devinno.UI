using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 커스텀 위젯도 내장 위젯과 같은 테마 체계를 쓴다(방침 3): 스위치(Look), 역할 숫자(TokenNumber), 상태 오버레이(StateOverlay).
public class WidgetThemeApiTests
{
    public sealed class Probe : DvWidget
    {
        public static ThemeLook? SeenLook;
        public static float SeenStroke;
        public static SKColor SeenOverlay;

        public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(60, 30);

        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
        {
            SeenLook = ctx.Look;
            SeenStroke = ctx.TokenNumber("stroke.outline");
            SeenOverlay = ctx.StateOverlay(new SKColor(0x10, 0x20, 0x30));
        }
    }

    private static byte[] Render(DvTheme theme, string attrs = "")
    {
        var r = new ComponentRegistry();
        r.RegisterWidget<Probe>("Probe");
        var s = new DvSurface($"<DvScreen><Probe {attrs}/></DvScreen>", null, theme, r);
        using var bmp = new SKBitmap(100, 50);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 100, 50);
        return Array.Empty<byte>();
    }

    [Fact]
    public void Widget_reads_switches_and_role_numbers_from_the_theme()
    {
        Render(DvTheme.Dark.With(new Dictionary<string, string> { ["look.texture"] = "flat", ["stroke.outline"] = "2.5" }));
        Assert.Equal(LookTexture.Flat, Probe.SeenLook?.Texture);
        Assert.Equal(2.5f, Probe.SeenStroke);
    }

    [Fact]
    public void State_overlay_leaves_an_idle_widget_unchanged_and_dims_a_disabled_one()
    {
        var fill = new SKColor(0x10, 0x20, 0x30);
        Render(DvTheme.Dark);
        Assert.Equal(fill, Probe.SeenOverlay);
        Render(DvTheme.Dark, "disabled=\"true\"");
        var expected = ThemeResolver.Number(DvTheme.Dark, "opacity.disabled") * 255;
        Assert.InRange(Probe.SeenOverlay.Alpha, expected - 1, expected + 1);
    }
}
