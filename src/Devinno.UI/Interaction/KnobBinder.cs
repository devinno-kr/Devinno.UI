using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI.Interaction;

/// <summary>
/// The component that handles <c>DvKnob</c> value calculation (<c>step</c> rounding, <c>min</c>/<c>max</c> clamping, decimal places) and the commit when a drag ends (two-way binding write + <c>onbeforecommit</c> check).
/// </summary>
/// <remarks>An internal engine stage. App code normally does not use it directly; it uses markup bindings and <see cref="DvSurface"/>/<see cref="DvRenderer"/>.</remarks>
public static class KnobBinder
{
    /// <summary>The total sweep angle of the knob arc (degrees). Turning this far changes the value from <c>min</c> to <c>max</c>.</summary>
    public const float SweepDeg = 270f;

    /// <summary>Movement of less than this distance (px) from the press point is not treated as a drag (distinguishes hand tremor from a tap).</summary>
    public const float MinDragDistance = 8f;

    /// <summary>Checks whether the knob can be turned. It must not be disabled, and <c>value</c> must be a two-way binding (<c>{Bind …}</c>).</summary>
    /// <param name="box">The knob box.</param>
    /// <returns><see langword="true"/> if it can be turned.</returns>
    public static bool CanDrag(LayoutBox box)
        => !box.Disabled && box.ValueBindExpr is { } e && BindingResolver.IsTwoWay(e);

    /// <summary>
    /// Rounds the value to <c>step</c> intervals from <c>min</c> (0.5 rounds away from zero), clamps it to <c>min</c>–<c>max</c>, then rounds to the decimal places of <c>step</c> to remove floating-point error.
    /// A <c>step</c> of 0 or less is treated as 1. Works even when <c>min</c> is greater than <c>max</c>.
    /// </summary>
    /// <param name="box">The knob box (reads <c>min</c>, <c>max</c> and <c>step</c>).</param>
    /// <param name="raw">The value before correction.</param>
    /// <returns>The corrected value.</returns>
    public static double Snap(LayoutBox box, double raw)
    {
        double min = box.KnobMin, max = box.KnobMax;
        double lo = Math.Min(min, max), hi = Math.Max(min, max);
        var step = box.KnobStep > 0 ? (double)(decimal)box.KnobStep : 1d;
        var snapped = min + Math.Round((raw - min) / step, MidpointRounding.AwayFromZero) * step;
        return Math.Round(Math.Clamp(snapped, lo, hi), Decimals(box.KnobStep), MidpointRounding.AwayFromZero);
    }

    /// <summary>Returns the number of decimal places of <c>step</c> (0.5→1, 0.25→2, 10→0). The maximum is 6.</summary>
    /// <param name="step">The knob <c>step</c> value.</param>
    /// <returns>The number of decimal places (0–6).</returns>
    public static int Decimals(float step)
    {
        var text = ((decimal)step).ToString(CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        return dot < 0 ? 0 : Math.Min(6, text.Length - dot - 1);
    }

    /// <summary>
    /// Writes the knob value to the two-way binding path. If <c>onbeforecommit="M"</c> is set, it first calls <c>bool M(double proposed)</c> and writes only when it returns <see langword="true"/>
    /// (if the method is missing or its signature does not match, nothing is written and a Trace warning is written once per cause; nothing is written if the method throws either). The value is converted to the target property type, and inside a <c>DvRepeat</c> item it is written to the item scope.
    /// </summary>
    /// <param name="box">The knob box. Ignored if <see cref="CanDrag"/> is <see langword="false"/>.</param>
    /// <param name="vm">The screen view model. Ignored if <see langword="null"/>.</param>
    /// <param name="proposed">The value to write (usually a value passed through <see cref="Snap"/>).</param>
    /// <remarks>
    /// Exceptions thrown by the check method and write failures (property setter exceptions, type conversion failures) are not propagated. They are treated as not written and a Trace error is written.
    /// (For knob operations through <see cref="DvSurface"/>, they are also reported via <see cref="DvSurface.HandlerError"/>.)
    /// </remarks>
    public static void Commit(LayoutBox box, object? vm, double proposed) => Commit(box, vm, proposed, owner: null);

    // owner = 예외를 알릴 서피스(HandlerError)
    internal static void Commit(LayoutBox box, object? vm, double proposed, DvSurface? owner)
    {
        if (vm is null || !CanDrag(box))
            return;

        if (!CommitGate.Allows(owner, box.Node.Tag, vm, box.OnBeforeCommit, proposed, typeof(double)))
            return;

        // DvRepeat 안이면 항목 범위로. setter 예외 = 쓰지 않음(다시 빌드되어 VM 값으로 보임)
        if (HandlerErrors.TrySet(owner, box.Node.Tag, box.ValueContext(vm), BindingResolver.ExtractPath(box.ValueBindExpr!), proposed))
            owner?.NotifyValueWritten(box);
    }
}

/// <summary>
/// The state of one <c>DvKnob</c> rotation drag. The value changes by <b>relative rotation</b> (angle change) from the grab point, so the value does not jump at the moment of pressing.
/// </summary>
/// <remarks>
/// <para>The angle turned past an end (<c>min</c>/<c>max</c>) is remembered, and the value starts moving again only after turning back by that much and passing the end position again (so the relation between the hand and the pointer line stays aligned even after hitting an end).
/// At most one full turn of overshoot is remembered. Near the knob center (a radius of 15% of the diameter, at least 8px) the angle is unstable and is ignored.</para>
/// <para>The preview value is applied only to <see cref="LayoutBox.KnobValue"/> and is not written to the view model. The view model is written by <see cref="KnobBinder.Commit(LayoutBox, object?, double)"/> when the drag ends.</para>
/// <para>An internal engine stage. App code normally does not use it directly; it uses markup bindings and <see cref="DvSurface"/>/<see cref="DvRenderer"/>.</para>
/// </remarks>
public sealed class KnobDrag : IValueDrag
{
    private readonly SKPoint _down;
    private readonly double _start;
    private SKPoint _center;
    private float _deadRadius;
    private float? _lastAngle;
    private double _accumDeg;

    /// <summary>Creates the drag state at the moment the knob is pressed. The start value is the knob's current value (rounded to the decimal places of <c>step</c>).</summary>
    /// <param name="box">The knob box to drag.</param>
    /// <param name="down">The press point (same coordinate system as the knob box's <see cref="LayoutBox.Rect"/>).</param>
    public KnobDrag(LayoutBox box, SKPoint down)
    {
        Box = box;
        _down = down;
        _start = Math.Round(box.KnobValue, KnobBinder.Decimals(box.KnobStep), MidpointRounding.AwayFromZero);
        Value = _start;
        _center = new SKPoint(box.Rect.MidX, box.Rect.MidY);
        _deadRadius = MathF.Max(8f, MathF.Min(box.Rect.Width, box.Rect.Height) * 0.15f);
        _lastAngle = AngleOrNull(down);
    }

    /// <summary>The knob box being dragged. When the layout is rebuilt, <see cref="Retarget"/> switches it to the new box.</summary>
    public LayoutBox Box { get; private set; }

    /// <summary>
    /// When the layout is rebuilt during a drag (periodic <see cref="DvSurface.Invalidate"/>, etc.), moves to the same knob in the new tree and carries over the preview value.
    /// If the knob position changed, the reference angle is reset (relative rotation from the next move).
    /// </summary>
    /// <param name="box">The same knob box in the new layout tree.</param>
    public void Retarget(LayoutBox box)
    {
        Box = box;
        if (Active)
            box.KnobValue = (float)Value;

        var center = new SKPoint(box.Rect.MidX, box.Rect.MidY);
        if (center == _center)
            return;
        _center = center;
        _deadRadius = MathF.Max(8f, MathF.Min(box.Rect.Width, box.Rect.Height) * 0.15f);
        _lastAngle = null;
    }

    /// <summary>Whether it has moved at least <see cref="KnobBinder.MinDragDistance"/> from the press point and been judged a real drag.</summary>
    public bool Active { get; private set; }

    /// <summary>The current preview value (with <c>step</c> rounding and <c>min</c>/<c>max</c> clamping applied).</summary>
    public double Value { get; private set; }

    /// <summary>Whether the preview value differs from the start value. If <see langword="false"/>, there is no need to commit.</summary>
    public bool Changed => Value != _start;

    /// <summary>Applies pointer movement and updates <see cref="Value"/> and the knob's preview value. Before the drag is judged, only the distance is checked.</summary>
    /// <param name="p">The pointer position (same coordinate system as the knob box's <see cref="LayoutBox.Rect"/>).</param>
    public void Move(SKPoint p)
    {
        if (!Active)
        {
            if (SKPoint.Distance(p, _down) < KnobBinder.MinDragDistance)
                return;
            Active = true;
        }

        if (AngleOrNull(p) is not { } angle)
            return;

        if (_lastAngle is not { } last)
        {
            _lastAngle = angle;
            return;
        }

        var delta = angle - last;
        if (delta > 180f) delta -= 360f;
        else if (delta <= -180f) delta += 360f;
        _lastAngle = angle;
        _accumDeg += delta;

        // 끝을 넘어간 각도도 누적에 남겨 둔다 → 되감아야 다시 움직임(잡은 손과 지시선 관계 유지).
        // 단 초과는 최대 한 바퀴까지만: 360° 를 빼도 포인터의 화면 각도는 같으므로 관계는 유지된다.
        var range = (double)Box.KnobMax - Box.KnobMin;
        if (range != 0)
        {
            var pos = (_start - Box.KnobMin) / range * KnobBinder.SweepDeg + _accumDeg; // 호 위 위치(0~270)
            if (pos > KnobBinder.SweepDeg + 360) _accumDeg -= 360;
            else if (pos < -360) _accumDeg += 360;
        }

        var raw = _start + _accumDeg / KnobBinder.SweepDeg * range;
        Value = KnobBinder.Snap(Box, raw);
        Box.KnobValue = (float)Value;
    }

    private float? AngleOrNull(SKPoint p)
    {
        var dx = p.X - _center.X;
        var dy = p.Y - _center.Y;
        if (dx * dx + dy * dy < _deadRadius * _deadRadius)
            return null;
        return MathF.Atan2(dy, dx) * 180f / MathF.PI; // 화면 좌표: 시계 방향 = +
    }
}
