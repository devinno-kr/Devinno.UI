using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>Handle for a <c>DvButton</c> element.</summary>
public sealed class DvButton : DvControl
{
    internal DvButton(string id) : base(id) => Slot = new ValueSlot(new ValueCell<bool>(), canOwn: false);

    /// <summary>
    /// Gets or sets the text. Setting it overrides the markup value with the text as-is; setting <see langword="null"/> reverts to the markup value.
    /// Reading returns the override value, or if none, the markup/binding value from the last layout (<see langword="null"/> if not connected or not yet laid out).
    /// </summary>
    public string? Text { get => CurrentText; set => SetOverrideText(value); }

    /// <summary>
    /// Gets or sets whether a latching button (a button with <c>value</c>) is pressed. With <c>value="{Bind X}"</c> it reads and writes <c>X</c>; with a one-way or fixed value it overrides it.
    /// On a plain button without <c>value</c>, setting it has no effect on screen (plain buttons have no on state).
    /// </summary>
    public bool Checked { get => ValueConvert.ToBool(CurrentValue()); set => SetCurrent(value); }

    /// <summary>Raised when <see cref="Checked"/> changes (from screen, code or binding — same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
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
