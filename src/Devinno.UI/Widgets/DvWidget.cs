using SkiaSharp;

namespace Devinno.UI.Widgets;

/// <summary>
/// Base class for an app-defined widget that paints itself with SkiaSharp. Register the class under a tag with
/// <see cref="Components.ComponentRegistry.RegisterWidget{T}(string)"/> and use the tag in markup like any other element.
/// </summary>
/// <remarks>
/// <para><b>What the class does</b>: measuring and painting its own area, and optionally handling pointer gestures (<see cref="OnPointerDown"/>) and writing its <c>value</c> (<see cref="DvWidgetContext.Commit"/>). Everything else comes from the markup as for built-in elements:
/// attributes and bindings (read them through <see cref="DvWidgetContext"/>), <c>class</c>, <c>style</c> (background, border, padding, size),
/// <c>onclick</c>, <c>hold</c>, <c>disabled</c>, <c>visible</c> and <c>id</c> (a plain <see cref="Controls.DvControl"/> handle). Child elements are not used.</para>
/// <para><b>Instances</b>: one instance per element on screen, created with the parameterless constructor. The same instance is kept when the screen is rebuilt
/// (binding updates, <c>Invalidate</c>), so fields may hold state such as animation progress. Inside <c>DvRepeat</c> each item has its own instance,
/// matched by position. Do not cache attribute values in fields — read them from the context on every call.</para>
/// <para><b>Threads</b>: called on the UI thread, like the rest of the screen.</para>
/// <para><b>Errors</b>: an exception thrown from <see cref="Paint"/> is caught, reported once through <see cref="DvSurface.HandlerError"/> (<c>Kind</c> = <c>"Paint"</c>,
/// <c>Tag</c> = the tag) and the area is outlined in red; the rest of the screen keeps painting.</para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class BatteryGauge : DvWidget
/// {
///     public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(160, 60);
///
///     public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
///     {
///         var soc = Math.Clamp(ctx.Number("value", 0), 0, 100);
///         using var track = new SKPaint { IsAntialias = true, Color = ctx.Color("gauge-track") };
///         using var fill = new SKPaint { IsAntialias = true, Color = ctx.Color(soc &lt; 20 ? "tone-danger" : "tone-success") };
///         canvas.DrawRoundRect(bounds, 6, 6, track);
///         canvas.DrawRoundRect(new SKRect(bounds.Left, bounds.Top, bounds.Left + bounds.Width * (float)soc / 100, bounds.Bottom), 6, 6, fill);
///     }
/// }
///
/// registry.RegisterWidget&lt;BatteryGauge&gt;("BatteryGauge");
/// // markup: &lt;BatteryGauge value="{Soc}"/&gt;
/// </code>
/// </example>
public abstract class DvWidget
{
    /// <summary>
    /// Returns the size of the content area (inside <c>padding</c>). <c>width</c>/<c>height</c> in the element's style take precedence. The default is 120×80.
    /// </summary>
    /// <param name="ctx">Attributes, theme and text helpers of this element.</param>
    /// <param name="availableWidth">The width offered by the parent, if known; otherwise <see langword="null"/>.</param>
    /// <returns>The content size in px.</returns>
    public virtual SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(120, 80);

    /// <summary>
    /// Paints the widget. The canvas is clipped to the element's area; background and border from the style are already painted.
    /// </summary>
    /// <param name="canvas">The canvas to paint on (screen coordinates, px).</param>
    /// <param name="bounds">The content area: the element's area minus <c>padding</c>.</param>
    /// <param name="ctx">Attributes, theme, paint time and text helpers of this element.</param>
    public abstract void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx);

    /// <summary>
    /// For a picture that changes over time: how many ms until the next frame (e.g. 50 = 20 frames per second). <see langword="null"/> (the default) = a still picture.
    /// The host then repaints on schedule (<see cref="DvSurface.NextAnimationDue"/>); use <see cref="DvWidgetContext.TimestampMs"/> in <see cref="Paint"/> for the phase.
    /// </summary>
    /// <param name="ctx">Attributes of this element (read attributes only; the theme here is the default theme).</param>
    /// <returns>The frame interval in ms (at least 16), or <see langword="null"/>.</returns>
    public virtual int? FrameInterval(DvWidgetContext ctx) => null;

    /// <summary>
    /// The element was pressed. Return <see langword="true"/> to take the gesture: this widget then receives <see cref="OnPointerMove"/> and <see cref="OnPointerUp"/>
    /// (or <see cref="OnPointerCancel"/>), the screen does not scroll, and no click (<c>onclick</c>) happens.
    /// Return <see langword="false"/> (the default) to let the press work as for any element (click, <c>hold</c>, scrolling).
    /// </summary>
    /// <param name="ctx">The element's context; <see cref="DvWidgetContext.Bounds"/> is the content area in the same coordinates as <paramref name="point"/>.</param>
    /// <param name="point">The press point.</param>
    /// <returns>Whether this widget takes the gesture.</returns>
    /// <remarks>
    /// Not called while the element is disabled. Typical use: keep a preview value in a field while dragging (paint it in <see cref="Paint"/>) and write it once
    /// with <see cref="DvWidgetContext.Commit"/> in <see cref="OnPointerUp"/> — the same way the built-in knob and slider work.
    /// An exception from any pointer method is reported through <see cref="DvSurface.HandlerError"/> (<c>Kind</c> = <c>"Pointer"</c>) and ends the gesture.
    /// </remarks>
    public virtual bool OnPointerDown(DvWidgetContext ctx, SKPoint point) => false;

    /// <summary>The pointer moved during a gesture this widget took. The screen is repainted afterwards.</summary>
    /// <param name="ctx">The element's context.</param>
    /// <param name="point">The pointer position (may be outside the element).</param>
    public virtual void OnPointerMove(DvWidgetContext ctx, SKPoint point) { }

    /// <summary>The pointer was released, ending the gesture. The screen is rebuilt afterwards, so committed values show at once.</summary>
    /// <param name="ctx">The element's context.</param>
    /// <param name="point">The release position (may be outside the element).</param>
    public virtual void OnPointerUp(DvWidgetContext ctx, SKPoint point) { }

    /// <summary>The gesture ended without a release — the element disappeared in a rebuild, or a dialog opened on top. Drop any preview; do not commit.</summary>
    /// <param name="ctx">The element's context.</param>
    public virtual void OnPointerCancel(DvWidgetContext ctx) { }
}
