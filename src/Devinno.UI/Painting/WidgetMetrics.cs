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
