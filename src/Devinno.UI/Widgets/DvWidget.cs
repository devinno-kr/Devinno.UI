using SkiaSharp;

namespace Devinno.UI.Widgets;

/// <summary>
/// Base class for an app-defined widget that paints itself with SkiaSharp. Register the class under a tag with
/// <see cref="Components.ComponentRegistry.RegisterWidget{T}(string)"/> and use the tag in markup like any other element.
/// </summary>
/// <remarks>
/// <para><b>What the class does</b>: measuring and painting its own area, and optionally handling pointer gestures (<see cref="OnPointerDown(DvWidgetContext, SKPoint)"/>; for other buttons, modifier keys, double presses, hover and the wheel the overloads that take <see cref="DvPointerArgs"/>) and writing its <c>value</c> (<see cref="DvWidgetContext.Commit"/>). Everything else comes from the markup as for built-in elements:
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
    /// The element was pressed. Return <see langword="true"/> to take the gesture: this widget then receives <see cref="OnPointerMove(DvWidgetContext, SKPoint)"/> and <see cref="OnPointerUp(DvWidgetContext, SKPoint)"/>
    /// (or <see cref="OnPointerCancel"/>), the screen does not scroll, and no click (<c>onclick</c>) happens.
    /// Return <see langword="false"/> (the default) to let the press work as for any element (click, <c>hold</c>, scrolling).
    /// </summary>
    /// <param name="ctx">The element's context; <see cref="DvWidgetContext.Bounds"/> is the content area in the same coordinates as <paramref name="point"/>.</param>
    /// <param name="point">The press point.</param>
    /// <returns>Whether this widget takes the gesture.</returns>
    /// <remarks>
    /// Not called while the element is disabled. Typical use: keep a preview value in a field while dragging (paint it in <see cref="Paint"/>) and write it once
    /// with <see cref="DvWidgetContext.Commit"/> in <see cref="OnPointerUp(DvWidgetContext, SKPoint)"/> — the same way the built-in knob and slider work.
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

    /// <summary>
    /// The element was pressed with any button. The default calls <see cref="OnPointerDown(DvWidgetContext, SKPoint)"/> for the left button and returns
    /// <see langword="false"/> for the others, so override this one to receive right and middle presses, modifier keys or the click count (double press).
    /// </summary>
    /// <param name="ctx">The element's context.</param>
    /// <param name="e">The press point, button, modifier keys and click count.</param>
    /// <returns>Whether this widget takes the gesture. A left gesture works as described on <see cref="OnPointerDown(DvWidgetContext, SKPoint)"/>.
    /// A right or middle gesture then sends its moves and release to this widget; other buttons never click, scroll, hold or show press styles.</returns>
    public virtual bool OnPointerDown(DvWidgetContext ctx, DvPointerArgs e) => e.Button == DvPointerButton.Left && OnPointerDown(ctx, e.Point);

    /// <summary>The pointer moved during a gesture this widget took. The default calls <see cref="OnPointerMove(DvWidgetContext, SKPoint)"/> for a left gesture.</summary>
    /// <param name="ctx">The element's context.</param>
    /// <param name="e">The pointer position (may be outside the element), the gesture's button and click count, and the modifier keys now.</param>
    public virtual void OnPointerMove(DvWidgetContext ctx, DvPointerArgs e)
    {
        if (e.Button == DvPointerButton.Left)
            OnPointerMove(ctx, e.Point);
    }

    /// <summary>The gesture's button was released. The default calls <see cref="OnPointerUp(DvWidgetContext, SKPoint)"/> for a left gesture. The screen is rebuilt afterwards.</summary>
    /// <param name="ctx">The element's context.</param>
    /// <param name="e">The release position (may be outside the element), the gesture's button and click count, and the modifier keys now.</param>
    public virtual void OnPointerUp(DvWidgetContext ctx, DvPointerArgs e)
    {
        if (e.Button == DvPointerButton.Left)
            OnPointerUp(ctx, e.Point);
    }

    /// <summary>
    /// The pointer moved over the element without a press (a mouse; touch screens do not hover). Called on every such move while the element is the input target
    /// (see <see cref="Interaction.HitTester.HitTarget(Layout.LayoutBox, SKPoint)"/>) and not disabled.
    /// </summary>
    /// <param name="ctx">The element's context.</param>
    /// <param name="e">The pointer position and modifier keys; <see cref="DvPointerArgs.Button"/> is <see cref="DvPointerButton.None"/>.</param>
    /// <returns><see langword="true"/> if the picture changed (for example a highlighted handle) and the screen must be repainted. The default does nothing and returns <see langword="false"/>.</returns>
    /// <remarks>To change the mouse cursor (a resize arrow over a handle), set the host control's cursor from here, e.g. <c>viewControl.Cursor = Cursors.SizeNS</c>, and restore it in <see cref="OnPointerLeave"/>.</remarks>
    public virtual bool OnPointerHover(DvWidgetContext ctx, DvPointerArgs e) => false;

    /// <summary>
    /// The pointer stopped hovering over the element: it moved to another element, left the screen (<see cref="DvSurface.PointerLeave"/>), was covered by a popup,
    /// or the element became disabled. Called once after the last <see cref="OnPointerHover"/>. The screen is repainted afterwards.
    /// </summary>
    /// <param name="ctx">The element's context.</param>
    public virtual void OnPointerLeave(DvWidgetContext ctx) { }

    /// <summary>The mouse wheel turned over the element (it is the input target and not disabled).</summary>
    /// <param name="ctx">The element's context.</param>
    /// <param name="e">The pointer position and modifier keys; <see cref="DvPointerArgs.Button"/> is <see cref="DvPointerButton.None"/>.</param>
    /// <param name="delta">Wheel notches as passed to <see cref="DvSurface.PointerWheel"/>: positive when rolled up, fractions allowed.</param>
    /// <returns><see langword="true"/> to take the wheel (for example Ctrl+wheel zoom): no scroll box scrolls and the screen is repainted.
    /// <see langword="false"/> (the default) to let the scroll box under the pointer scroll as usual.</returns>
    public virtual bool OnPointerWheel(DvWidgetContext ctx, DvPointerArgs e, float delta) => false;
}
