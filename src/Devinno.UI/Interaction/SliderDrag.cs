using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI.Interaction;

/// <summary>Knob/slider drag. The surface handles both through the same path (preview, then commit on release; carried over on rebuild).</summary>
internal interface IValueDrag
{
    LayoutBox Box { get; }
    bool Active { get; }
    double Value { get; }
    bool Changed { get; }
    void Move(SKPoint p);
    void Retarget(LayoutBox box);
}

/// <summary>
/// Track geometry for <c>DvSlider</c>. Thumb radius is <see cref="Thumb"/>; the track is inset by that amount at both ends.
/// <c>direction</c> = the side toward which the value increases (<c>right</c>, <c>left</c>, <c>up</c>, <c>down</c>; same as DvProgress).
/// With <c>show-value</c>, space for the value text is reserved above (horizontal) or to the right (vertical).
/// </summary>
internal static class SliderMetrics
{
    public const float Thumb = 12f;
    public const float LabelBand = 24f;   // 가로 — 손잡이 위 값 글자 줄
    public const float LabelSide = 56f;   // 세로 — 손잡이 오른쪽 값 글자 칸

    public static bool Vertical(LayoutBox box) => box.Direction is "up" or "down";

    /// <summary>The minimum-side and maximum-side end points (the line the thumb center travels on).</summary>
    public static (SKPoint Min, SKPoint Max) Track(LayoutBox box)
    {
        var r = box.Rect;
        if (Vertical(box))
        {
            var cx = box.ShowValue ? r.Left + MathF.Max(Thumb, (r.Width - LabelSide) / 2f) : r.MidX;
            SKPoint top = new(cx, r.Top + Thumb), bottom = new(cx, r.Bottom - Thumb);
            return box.Direction == "down" ? (top, bottom) : (bottom, top);
        }

        var cy = box.ShowValue ? r.Top + LabelBand + (r.Height - LabelBand) / 2f : r.MidY;
        SKPoint left = new(r.Left + Thumb, cy), right = new(r.Right - Thumb, cy);
        return box.Direction == "left" ? (right, left) : (left, right);
    }

    /// <summary>Value to 0–1 (out-of-range values clamp to the ends).</summary>
    public static float Fraction(LayoutBox box, double value)
    {
        var range = (double)box.KnobMax - box.KnobMin;
        return range == 0 ? 0f : (float)Math.Clamp((value - box.KnobMin) / range, 0, 1);
    }

    public static SKPoint PointAt(LayoutBox box, double value)
    {
        var (a, b) = Track(box);
        var t = Fraction(box, value);
        return new SKPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>Pointer to value (projected onto the track, rounded to step and clamped to the range).</summary>
    public static double ValueAt(LayoutBox box, SKPoint p)
    {
        var (a, b) = Track(box);
        var len = Vertical(box) ? b.Y - a.Y : b.X - a.X;
        var t = len == 0 ? 0 : Math.Clamp(((Vertical(box) ? p.Y - a.Y : p.X - a.X)) / len, 0, 1);
        return KnobBinder.Snap(box, box.KnobMin + t * ((double)box.KnobMax - box.KnobMin));
    }
}

/// <summary>
/// One <c>DvSlider</c> drag. The value jumps to the pressed position (pressing anywhere on the track works) and follows while dragging. The preview goes only to
/// <see cref="LayoutBox.KnobValue"/>; the view model is written on release (the commit path of <see cref="KnobBinder"/>). A press and release without dragging also commits the value at that position.
/// </summary>
internal sealed class SliderDrag : IValueDrag
{
    private readonly double _start;

    public SliderDrag(LayoutBox box, SKPoint down)
    {
        Box = box;
        _start = Math.Round(box.KnobValue, KnobBinder.Decimals(box.KnobStep), MidpointRounding.AwayFromZero);
        Value = SliderMetrics.ValueAt(box, down);
        box.KnobValue = (float)Value;
    }

    public LayoutBox Box { get; private set; }
    public bool Active => true; // 누름 = 끌기 시작(탭도 그 자리로 커밋)
    public double Value { get; private set; }
    public bool Changed => Value != _start;

    public void Move(SKPoint p)
    {
        Value = SliderMetrics.ValueAt(Box, p);
        Box.KnobValue = (float)Value;
    }

    public void Retarget(LayoutBox box)
    {
        Box = box;
        box.KnobValue = (float)Value;
    }
}
