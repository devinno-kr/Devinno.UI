using System.Globalization;
using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvToggle</c> element. Read and write the on state with <see cref="Checked"/>.
/// </summary>
/// <remarks>
/// The value has a single owner. With <c>value="{Bind X}"</c>, it reads and writes view model property <c>X</c>; without <c>value</c>, this handle holds the value and it can also be pressed on screen.
/// With a one-way (<c>value="{X}"</c>) or fixed value, the written value overrides the on-screen value (revert with <see cref="DvControl.ResetOverrides"/>) and it cannot be pressed on screen.
/// </remarks>
public sealed class DvToggle : DvControl
{
    internal DvToggle(string id) : base(id) => Slot = new ValueSlot(new ValueCell<bool>(), canOwn: true);

    /// <summary>Gets or sets whether it is on. Writing schedules a redraw and may be done from another thread.</summary>
    public bool Checked { get => ValueConvert.ToBool(CurrentValue()); set => SetCurrent(value); }

    /// <summary>
    /// Raised when <see cref="Checked"/> changes — when pressed on screen (<see cref="DvValueChangedEventArgs.ByUser"/> = true),
    /// when written from code (on the writing thread), and when a change to the bound view model property is detected while rebuilding the surface.
    /// </summary>
    public event EventHandler<DvValueChangedEventArgs>? CheckedChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override string ValueEventName => "CheckedChanged";
    private protected override object? ValueFromBox(LayoutBox box) => box.ToggleOn;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);
}

/// <summary>Handle value conversion — converts a raw binding value (bool, "true", number) to the handle's type.</summary>
internal static class ValueConvert
{
    public static bool ToBool(object? v) => v switch
    {
        bool b => b,
        null => false,
        _ => string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase),
    };

    public static double? ToDouble(object? v)
    {
        switch (v)
        {
            case null:
                return null;
            case double d:
                return d;
            case IConvertible c when v is not string:
                try { return c.ToDouble(CultureInfo.InvariantCulture); }
                catch (Exception) { return null; }
            default:
                return double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : null;
        }
    }

    public static string? ToText(object? v) => v switch
    {
        null => null,
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString(),
    };
}
