using Devinno.UI.Painting;
using SkiaSharp;
using Xunit;

// 위젯 내부 요소(선 두께·바늘·글씨·칩)가 위젯 크기에 비례하는지. 기본 크기에서는 기존 값 그대로.
public class WidgetScaleTests
{
    private static SKRect Box(float w, float h) => new(0, 0, w, h);

    [Fact]
    public void Gauge_at_default_size_keeps_original_metrics()
    {
        var m = WidgetMetrics.Gauge(Box(120, 120));
        Assert.Equal(10f, m.Stroke, 3);
        Assert.Equal(18f, m.ValueFont, 3);
        Assert.Equal(6f, m.HubRadius, 3);
        Assert.Equal(52f, m.Radius, 3); // 60 - 8
    }

    [Fact]
    public void Gauge_scales_with_the_shorter_side()
    {
        var big = WidgetMetrics.Gauge(Box(360, 240)); // 짧은 변 240 → 2배
        Assert.Equal(20f, big.Stroke, 3);
        Assert.Equal(36f, big.ValueFont, 3);
        Assert.Equal(12f, big.HubRadius, 3);
        Assert.Equal(104f, big.Radius, 3); // 120 - 16
    }

    [Fact]
    public void Small_gauge_keeps_value_text_readable_and_clear_of_the_hub()
    {
        var m = WidgetMetrics.Gauge(Box(60, 60));
        Assert.True(m.ValueFont >= 9f, $"font {m.ValueFont}");
        // 값 글씨 위쪽(기준선 - 글씨 높이)이 허브 아래에 있어야 한다
        var textTop = m.ValueBaseline - m.ValueFont * 0.75f;
        Assert.True(textTop > m.HubRadius, $"textTop {textTop} hub {m.HubRadius}");
    }

    [Fact]
    public void Gauge_value_moves_into_bottom_gap_when_tick_labels_are_drawn()
    {
        var plain = WidgetMetrics.Gauge(Box(380, 380));
        var labeled = WidgetMetrics.Gauge(Box(380, 380), tickLabels: true);
        Assert.True(labeled.ValueBaseline > plain.ValueBaseline);
        Assert.True(labeled.ValueFont < plain.ValueFont);
        // 호 양 끝(135°/45°)의 높이 아래로 — 아래쪽 눈금 숫자 줄보다 낮게
        Assert.True(labeled.ValueBaseline > labeled.Radius * MathF.Sin(MathF.PI / 4f));
    }

    [Fact]
    public void Knob_scales_track_and_face_inset()
    {
        var d = WidgetMetrics.Knob(Box(96, 96));
        Assert.Equal(8f, d.Stroke, 3);
        Assert.Equal(12f, d.FaceInset, 3);

        var big = WidgetMetrics.Knob(Box(288, 288)); // 3배
        Assert.Equal(24f, big.Stroke, 3);
        Assert.Equal(36f, big.FaceInset, 3);
    }

    [Fact]
    public void Tank_at_default_size_keeps_original_metrics()
    {
        var m = WidgetMetrics.Tank(Box(80, 160));
        Assert.Equal(15f, m.ChipFont, 3);
        Assert.Equal(10f, m.MarkFont, 3);
        Assert.Equal(10f, m.CornerRadius, 3);
    }

    [Fact]
    public void Large_tank_grows_chip_and_marks()
    {
        var m = WidgetMetrics.Tank(Box(160, 320)); // 2배
        Assert.Equal(30f, m.ChipFont, 3);
        Assert.Equal(20f, m.MarkFont, 3);
        Assert.Equal(20f, m.CornerRadius, 3);
    }

    [Fact]
    public void Wide_short_tank_scales_by_the_limiting_side()
    {
        var m = WidgetMetrics.Tank(Box(300, 200)); // 폭 3.75배, 높이 1.25배 → 1.25
        Assert.Equal(15f * 1.25f, m.ChipFont, 3);
    }

    [Fact]
    public void Narrow_tank_chip_font_never_below_minimum()
    {
        var m = WidgetMetrics.Tank(Box(40, 100));
        Assert.True(m.ChipFont >= 9f, $"chip {m.ChipFont}");
        Assert.True(m.MarkFont >= 8f, $"mark {m.MarkFont}");
    }
}
