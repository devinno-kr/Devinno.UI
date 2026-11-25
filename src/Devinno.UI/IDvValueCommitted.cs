namespace Devinno.UI;

/// <summary>
/// Implemented by a view model that wants to know when <b>the screen</b> wrote one of its properties — the user changed a two-way bound value
/// (<c>{Bind Setpoint}</c>) and the write succeeded. Code that sets the property itself is not reported, so a value read from a device and put
/// into the view model is not sent back to the device.
/// </summary>
/// <remarks>
/// <para>Called on the input thread right after the setter returned, once per write: every two-way widget (<c>DvInput</c>, <c>DvKnob</c>, <c>DvSlider</c>,
/// <c>DvToggle</c>, <c>DvSelect</c> …), grid cell edits and row selection, <c>DvExpander expanded</c>, and custom widgets (<c>DvWidgetContext.Commit</c>).
/// It is called even when the new value equals the old one.</para>
/// <para>Not called for: writes from code, values written through a control handle (<c>Checked</c>, <c>Value</c> … in code), a write that was
/// rejected by <c>onbeforecommit</c> or failed (setter exception, conversion failure — reported as <c>"setter"</c>), and the values a <c>DvVideo</c>
/// keeps up to date (<c>position</c>, <c>duration</c>).</para>
/// <para>The object told is the one the binding path starts from: the screen view model for <c>{Bind Motor.Speed}</c> (<paramref name="path"/> =
/// <c>"Motor.Speed"</c>), the item for <c>{Bind Item.Speed}</c> in a <c>DvRepeat</c> and for a grid cell (<paramref name="path"/> = <c>"Speed"</c>,
/// the column path) — implement the interface on the item type to hear those.</para>
/// <para>An exception thrown here is reported through <see cref="DvSurface.HandlerError"/> with <c>Kind</c> = <c>"committed"</c>; the value stays written.</para>
/// </remarks>
/// <example>
/// <code>
/// public partial class MainPage : DvPage, IDvValueCommitted
/// {
///     public double Setpoint { get; set; }
///
///     public void OnValueCommitted(string path, object? value)
///     {
///         if (path == nameof(Setpoint))
///             plc.Write("D100", Setpoint);
///     }
/// }
/// </code>
/// </example>
public interface IDvValueCommitted
{
    /// <summary>The screen wrote <paramref name="value"/> to the property at <paramref name="path"/>.</summary>
    /// <param name="path">Property path from this object, as written in the binding without braces, <c>Bind</c>, <c>Data.</c> and <c>Item.</c> (<c>"Setpoint"</c>, <c>"Motor.Speed"</c>).</param>
    /// <param name="value">The value stored, already converted to the property type (<see langword="null"/> when the screen cleared it).</param>
    void OnValueCommitted(string path, object? value);
}
