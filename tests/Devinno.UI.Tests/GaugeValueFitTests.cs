using Devinno.UI;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvGauge 값 글자(값 + unit)는 호의 열린 아래(두 끝 사이)에 맞춘다 — 넘치면 글자를 줄인다(보통 크기의 60% 까지).
// 검사: show-value 켬/끔 두 번 그려 다른 픽셀(= 값 글자) 중 호 띠(끝 포함, ±1px) 위에 있는 것이 없어야 한다.
public class GaugeValueFitTests
{
    private static SKBitmap Render(int size, string value, string unit, bool ticks, bool show)
    {
        var markup = $"<DvScreen style=\"padding:0\"><DvGauge value=\"{value}\" max=\"20000\" unit=\"{unit}\" " +
                     (ticks ? "ticks=\"5000\" " : "") + $"show-value=\"{(show ? "true" : "false")}\" style=\"width:{size}; height:{size}\"/></DvScreen>";
        return SKBitmap.Decode(DvRenderer.RenderToPng(markup, null, size, size, DvTheme.Dark));
    }

    [Theory]
    [InlineData(120, false, "1480", "rpm")]
    [InlineData(200, false, "1480", "rpm")]
    [InlineData(320, false, "1480", "rpm")]
    [InlineData(120, true, "1480", "rpm")]
    [InlineData(200, true, "1480", "rpm")]
    [InlineData(320, true, "1480", "rpm")]
    [InlineData(120, false, "10000", "kPa")]
    [InlineData(200, true, "10000", "kPa")]
    public void Value_text_does_not_touch_the_arc(int size, bool ticks, string value, string unit)
    {
        using var on = Render(size, value, unit, ticks, show: true);
        using var off = Render(size, value, unit, ticks, show: false);
        var gm = WidgetMetrics.Gauge(SKRect.Create(0, 0, size, size), ticks);
        float c = size / 2f;
        var ri = gm.Radius - gm.Stroke / 2f - 1f;
        var ro = gm.Radius + gm.Stroke / 2f + 1f;

        var textPixels = 0;
        var onArc = 0;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            if (on.GetPixel(x, y) == off.GetPixel(x, y))
                continue;
            textPixels++;
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            var d = MathF.Sqrt(dx * dx + dy * dy);
            var inArcAngles = dy < 0 || MathF.Abs(dx) >= dy - 1f; // 아래 90° 틈 밖(끝 1px 포함)
            if (d >= ri && d <= ro && inArcAngles)
                onArc++;
        }

        Assert.True(textPixels > 0, "no value text drawn");
        Assert.Equal(0, onArc);
    }

    [Fact]
    public void Short_text_keeps_the_normal_size()
    {
        var gm = WidgetMetrics.Gauge(SKRect.Create(0, 0, 120, 120));
        Assert.Equal(gm.ValueFont, WidgetMetrics.GaugeValueFont(gm, "58 %"), 3);
    }

    [Fact]
    public void Text_that_cannot_fit_stops_at_60_percent()
    {
        var gm = WidgetMetrics.Gauge(SKRect.Create(0, 0, 120, 120));
        Assert.Equal(gm.ValueFont * 0.6f, WidgetMetrics.GaugeValueFont(gm, "1234567890 kPa/min"), 3);
    }
}
