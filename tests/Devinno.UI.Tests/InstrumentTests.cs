using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

public class InstrumentTests
{
    private static SKBitmap Render(string markup, int w, int h, DvTheme? theme = null)
    {
        var box = new LayoutEngine(theme).Layout(MarkupParser.Parse(markup), null, w, h);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        new Painter(theme).Paint(canvas, box, null);
        return bmp;
    }

    private static LayoutBox Lay(string markup)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), null, 300, 300);

    private static bool IsWhite(SKColor c) => c.Red > 245 && c.Green > 245 && c.Blue > 245;
    private static bool IsBlue(SKColor c) => c.Blue > 180 && c.Red < 120;
    private static bool IsRed(SKColor c) => c.Red > 180 && c.Green < 110 && c.Blue < 110;

    private static bool AnyIn(SKBitmap b, int x0, int y0, int x1, int y1, Func<SKColor, bool> f)
    {
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
            if (f(b.GetPixel(x, y)))
                return true;
        return false;
    }

    // ── 1. 눈금
    [Fact]
    public void ScaleTicks_stay_within_range_and_ignore_bad_step()
    {
        Assert.Equal(new[] { 0d, 10, 20, 30, 40, 50, 60, 70, 80, 90 }, ScaleTicks.Values(0, 95, 10));
        Assert.Equal(new[] { 0d, 0.5, 1 }, ScaleTicks.Values(0, 1, 0.5));
        Assert.Empty(ScaleTicks.Values(0, 100, 0));
        Assert.Empty(ScaleTicks.Values(0, 100, -5));
        Assert.Equal(new[] { 3d, 8, 13 }, ScaleTicks.Values(3, 15, 5));
    }

    [Fact]
    public void Gauge_without_ticks_attribute_draws_no_scale()
    {
        Assert.Equal(0, Lay("<DvGauge value=\"0\"/>").GaugeTicks);
        // 140 박스: cx=70, R=62. 12시 큰 눈금 자리(70, 22)
        using var bmp = Render("<DvGauge value=\"0\" style=\"width:140; height:140\"/>", 140, 140);
        Assert.True(IsWhite(bmp.GetPixel(70, 22)));
    }

    [Fact]
    public void Gauge_ticks_draw_major_tick_and_upright_label()
    {
        using var bmp = Render("<DvGauge value=\"0\" ticks=\"10\" minor-ticks=\"2\" style=\"width:140; height:140\"/>", 140, 140);
        Assert.False(IsWhite(bmp.GetPixel(70, 22)), "major tick at 50 missing");
        Assert.True(AnyIn(bmp, 62, 28, 78, 42, c => c.Red < 160), "label '50' missing");
    }

    [Fact]
    public void Gauge_tick_labels_can_be_hidden()
    {
        using var bmp = Render(
            "<DvGauge value=\"0\" ticks=\"10\" show-tick-labels=\"false\" style=\"width:140; height:140\"/>", 140, 140);
        Assert.False(AnyIn(bmp, 62, 30, 78, 40, c => c.Red < 160));
    }

    [Fact]
    public void Gauge_needle_tip_is_highlighted()
    {
        using var bmp = Render("<DvGauge value=\"50\" style=\"width:140; height:140\"/>", 140, 140, DvTheme.Light);
        Assert.True(AnyIn(bmp, 66, 16, 74, 28, IsRed), "needle tip not highlighted");
    }

    // ── 2. DvProgress
    [Fact]
    public void Progress_defaults_and_parsing()
    {
        var p = Lay("<DvScreen style=\"align-items:start\"><DvProgress value=\"30\" tone=\"success\" direction=\"up\"/></DvScreen>").Children[0];
        Assert.Equal(30, p.GaugeValue);
        Assert.Equal("success", p.Tone);
        Assert.Equal("up", p.Direction);
        Assert.Equal((12f, 120f), (p.Rect.Width, p.Rect.Height));

        var h = Lay("<DvScreen style=\"align-items:start\"><DvProgress value=\"30\"/></DvScreen>").Children[0];
        Assert.Equal((200f, 12f), (h.Rect.Width, h.Rect.Height));
    }

    [Theory]
    [InlineData("right", 25, 175)]
    [InlineData("left", 175, 25)]
    public void Progress_fills_in_direction(string dir, int filledX, int emptyX)
    {
        using var bmp = Render($"<DvProgress value=\"40\" direction=\"{dir}\" style=\"width:200; height:12\"/>", 200, 12);
        Assert.True(IsBlue(bmp.GetPixel(filledX, 6)), $"filled {bmp.GetPixel(filledX, 6)}");
        Assert.False(IsBlue(bmp.GetPixel(emptyX, 6)), $"empty {bmp.GetPixel(emptyX, 6)}");
    }

    [Fact]
    public void Progress_vertical_up_fills_from_bottom()
    {
        using var bmp = Render("<DvProgress value=\"40\" direction=\"up\" style=\"width:12; height:200\"/>", 12, 200);
        Assert.True(IsBlue(bmp.GetPixel(6, 180)));
        Assert.False(IsBlue(bmp.GetPixel(6, 20)));
    }

    [Fact]
    public void Progress_alarm_zone_overrides_tone()
    {
        using var bmp = Render(
            "<DvProgress value=\"92\" alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\" style=\"width:200; height:12\"/>",
            200, 12);
        Assert.True(IsRed(bmp.GetPixel(100, 6)), $"{bmp.GetPixel(100, 6)}");
    }

    // ── 3. DvRing
    [Fact]
    public void Ring_270_fills_top_only_past_midpoint()
    {
        // 140 박스, 두께 기본 = 140*0.11 ≈ 15, 링 중심선 반지름 ≈ 70-7.7 → 12시 (70, 8)
        using var low = Render("<DvRing value=\"30\" style=\"width:140; height:140\"/>", 140, 140);
        using var high = Render("<DvRing value=\"70\" style=\"width:140; height:140\"/>", 140, 140);
        Assert.False(IsBlue(low.GetPixel(70, 8)), $"{low.GetPixel(70, 8)}");
        Assert.True(IsBlue(high.GetPixel(70, 8)), $"{high.GetPixel(70, 8)}");
    }

    [Fact]
    public void Ring_360_starts_at_twelve_oclock()
    {
        using var bmp = Render("<DvRing value=\"25\" sweep=\"360\" style=\"width:140; height:140\"/>", 140, 140);
        Assert.True(IsBlue(bmp.GetPixel(111, 29)), "1:30 should be filled");  // 45° 시계 방향
        Assert.False(IsBlue(bmp.GetPixel(70, 132)), "6 o'clock should be empty");
        Assert.False(IsBlue(bmp.GetPixel(29, 29)), "10:30 should be empty");
    }

    [Fact]
    public void Ring_center_shows_value_and_label()
    {
        var r = Lay("<DvScreen style=\"align-items:start\"><DvRing value=\"75\" unit=\"%\" label=\"LINE 1\"/></DvScreen>").Children[0];
        Assert.Equal("%", r.Unit);
        Assert.Equal("LINE 1", r.Label);
        Assert.Equal((120f, 120f), (r.Rect.Width, r.Rect.Height));

        using var bmp = Render("<DvRing value=\"75\" unit=\"%\" label=\"LINE 1\" style=\"width:140; height:140\"/>", 140, 140, DvTheme.Light);
        Assert.True(AnyIn(bmp, 50, 55, 90, 80, c => c.Red < 120), "value text missing");
    }

    // ── 4. DvTank 원통 음영
    [Fact]
    public void Tank_liquid_edges_are_darker_than_center()
    {
        using var bmp = Render("<DvTank value=\"100\" style=\"width:80; height:160\"/>", 80, 160);
        var edge = bmp.GetPixel(6, 120);
        var center = bmp.GetPixel(30, 120);
        float Lum(SKColor c) => c.Red * 0.299f + c.Green * 0.587f + c.Blue * 0.114f;
        Assert.True(Lum(edge) < Lum(center) - 6, $"edge={edge} center={center}");
    }

    // ── 값 글자 format — 자릿수를 고정해 실시간 값에서 폭이 흔들리지 않게
    [Theory]
    [InlineData(6f, null, "6")]
    [InlineData(6.2f, null, "6.2")]
    [InlineData(6f, "0.0", "6.0")]
    [InlineData(6.25f, "0.0", "6.3")]
    [InlineData(58f, "0.00", "58.00")]
    [InlineData(6.2f, "", "6.2")]
    [InlineData(6.2f, "X", "6.2")]      // 실수에 쓸 수 없는 형식 = 기본 규칙
    public void Value_text_uses_format_when_given(float v, string? format, string expected)
        => Assert.Equal(expected, Painter.FormatValue(v, format));

    [Theory]
    [InlineData("DvGauge")]
    [InlineData("DvRing")]
    [InlineData("DvProgress")]
    [InlineData("DvTank")]
    public void Instruments_read_format_attribute(string tag)
    {
        var b = Lay($"<DvScreen style=\"align-items:start\"><{tag} value=\"6\" format=\"0.0\"/></DvScreen>").Children[0];
        Assert.Equal("0.0", b.ValueFormat);
    }
}
