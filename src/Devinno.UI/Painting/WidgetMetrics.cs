using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI.Painting;

/// <summary>
/// Internal dimensions of round and vessel widgets (stroke widths, needle, text, chips). Scale is 1 at the default size (instrument 120, knob 96, tank 80×160);
/// when a widget is enlarged or shrunk, its inner elements follow at the same ratio. Text never goes below a minimum readable size.
/// </summary>
internal static class WidgetMetrics
{
    private const float MinFont = 9f;

    /// <summary>
    /// The area inside the box where a fixed-shape widget is actually drawn; only this area responds to presses. Even if the box is stretched (align-items:stretch), the drawing
    /// stays centered at its own size, and the empty space beside it is not part of the widget. Round widgets (knob, instrument, ring, lamp) use a square based on the shorter side;
    /// toggles and icons use their own size (or width/height if given). Any other widget uses the whole box.
    /// </summary>
    internal static SKRect ShapeRect(LayoutBox box)
    {
        var r = box.Rect;
        var st = box.Style;
        switch (box.Node.Tag)
        {
            case "DvKnob" or "DvGauge" or "DvRing" or "DvLamp":
                var size = MathF.Min(r.Width, r.Height);
                return Centered(r, size, size);
            case "DvToggle":
                return Centered(r, MathF.Min(r.Width, st.Width ?? 44f), MathF.Min(r.Height, st.Height ?? 24f));
            case "DvIcon":
                return Centered(r, MathF.Min(r.Width, st.Width ?? st.FontSize + st.Padding.Horizontal),
                    MathF.Min(r.Height, st.Height ?? st.FontSize + st.Padding.Vertical));
            default:
                return r;
        }
    }

    private static SKRect Centered(SKRect r, float w, float h) =>
        SKRect.Create(r.MidX - w / 2f, r.MidY - h / 2f, w, h);

    /// <param name="Scale">Scale relative to the default size (120).</param>
    /// <param name="Radius">Radius of the arc centerline.</param>
    /// <param name="Stroke">Arc stroke width.</param>
    /// <param name="TickInset">Inward distance where ticks start (from the arc centerline).</param>
    /// <param name="MinorTickEnd">End distance of minor ticks.</param>
    /// <param name="MajorTickEnd">End distance of major ticks.</param>
    /// <param name="NeedleHalf">Half-width of the needle base.</param>
    /// <param name="NeedleTail">Needle tail length (behind the center).</param>
    /// <param name="HubRadius">Radius of the center hub.</param>
    /// <param name="HubDot">Radius of the dot at the center of the hub.</param>
    /// <param name="ValueFont">Value text size (minimum 9).</param>
    /// <param name="ValueBaseline">Value text baseline: distance down from the center (cy).</param>
    internal readonly record struct GaugeMetrics(
        float Scale, float Radius, float Stroke,
        float TickInset, float MinorTickEnd, float MajorTickEnd,
        float NeedleHalf, float NeedleTail, float HubRadius, float HubDot,
        float ValueFont, float ValueBaseline);

    /// <param name="rect">Instrument area (scale is computed from the shorter side).</param>
    /// <param name="tickLabels">When tick numbers are drawn, the value text is moved down into the open gap at the bottom of the arc and made slightly smaller (so it does not overlap the lower tick numbers).</param>
    internal static GaugeMetrics Gauge(SKRect rect, bool tickLabels = false)
    {
        var size = MathF.Min(rect.Width, rect.Height);
        var s = size / 120f;
        var radius = size / 2f - 8f * s;
        var font = MathF.Max(MinFont, (tickLabels ? 15f : 18f) * s);
        return new GaugeMetrics(
            s, radius, 10f * s,
            9f * s, 13f * s, 17f * s,
            3.5f * s, 10f * s, 6f * s, 2.5f * s,
            font, radius * (tickLabels ? 0.82f : 0.55f));
    }

    /// <summary>
    /// Value text size that fits <paramref name="label"/> between the arc ends (the open bottom of the 270° arc) at <see cref="GaugeMetrics.ValueBaseline"/>:
    /// <see cref="GaugeMetrics.ValueFont"/> when it fits, otherwise smaller, down to 60% of it (not below <see cref="MinFont"/> unless the normal size already is).
    /// A text that does not fit at the minimum keeps the minimum.
    /// </summary>
    internal static float GaugeValueFont(GaugeMetrics gm, string label)
    {
        var normal = gm.ValueFont;
        var min = MathF.Min(normal, MathF.Max(MinFont, normal * 0.6f));
        var size = normal;
        for (var i = 0; i < 4; i++)
        {
            using var font = Text.TextFont.Create(label, size, bold: true);
            var width = font.MeasureText(label, out var bounds);
            var room = 2f * (GaugeGapHalf(gm, gm.ValueBaseline + bounds.Top, gm.ValueBaseline + bounds.Bottom) - 3f * gm.Scale);
            if (width <= room || size <= min)
                break;
            size = MathF.Max(min, size * room / width * 0.98f);
        }

        return size;
    }

    // 중심 아래 dy(위·아래 끝 top..bottom) 띠에서 호 두 끝 사이의 반폭 — 호 띠(안쪽 반지름 ri, 바깥 ro)는 45°·135° 반지름 선에서 끝난다(butt).
    // 깊이 dy 에서 호 = |dx| >= dy 인 점 중 ri..ro 거리: dy·√2 < ri 면 안쪽 원(√(ri²−dy²)), ri..ro 면 끝 선(dy), ro 밖이면 호 아래(제한 없음 = 반지름)
    private static float GaugeGapHalf(GaugeMetrics gm, float top, float bottom)
    {
        var ri = gm.Radius - gm.Stroke / 2f;
        var ro = gm.Radius + gm.Stroke / 2f;

        float Half(float dy)
        {
            if (dy <= 0)
                return ri; // 중심 위 — 원 안쪽 전체(글자는 여기까지 오지 않음)
            if (dy * MathF.Sqrt(2f) > ro)
                return ro;
            return dy * MathF.Sqrt(2f) >= ri ? dy : MathF.Sqrt(ri * ri - dy * dy);
        }

        // Half 는 dy = ri/√2 까지 줄고 그 뒤로 는다 — 구간 최소는 양 끝 또는 그 점
        var turn = ri / MathF.Sqrt(2f);
        var m = MathF.Min(Half(top), Half(bottom));
        return top < turn && bottom > turn ? MathF.Min(m, turn) : m;
    }

    internal readonly record struct KnobMetrics(float Scale, float Radius, float Stroke, float FaceInset,
        float RimWidth, float ShadowBlur, float ShadowDy);

    internal static KnobMetrics Knob(SKRect rect)
    {
        var size = MathF.Min(rect.Width, rect.Height);
        var s = size / 96f;
        var stroke = 8f * s;
        return new KnobMetrics(s, size / 2f - stroke, stroke, 12f * s, 1.5f * s, 4f * s, 2f * s);
    }

    internal readonly record struct TankMetrics(float Scale, float CornerRadius, float ChipFont, float MarkFont,
        float TickGap, float TickLen, float LabelGap, float FrameWidth);

    /// <summary>Tanks are used both tall and narrow and wide and low, so the scale is taken from whichever of width or height grew less.</summary>
    internal static TankMetrics Tank(SKRect rect)
    {
        var s = MathF.Min(rect.Width / 80f, rect.Height / 160f);
        return new TankMetrics(s, 10f * s, MathF.Max(MinFont, 15f * s), MathF.Max(8f, 10f * s),
            3f * s, 6f * s, 4f * s, MathF.Max(1f, 1.5f * s));
    }
}
