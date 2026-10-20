using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvKnob</c> element. Read and write the value with <see cref="Value"/> (value ownership rules are the same as <see cref="DvToggle"/>).
/// </summary>
public sealed class DvKnob : DvControl
{
    internal DvKnob(string id) : base(id) => Slot = new ValueSlot(new ValueCell<double>(), canOwn: true);

    /// <summary>
    /// Gets or sets the value. A value written from code is stored as-is, without <c>step</c> rounding or <c>min</c>/<c>max</c> clamping (the on-screen display is drawn within the range).
    /// If the bound property is an integer, the value is converted to that type.
    /// </summary>
    public double Value { get => ValueConvert.ToDouble(CurrentValue()) ?? 0; set => SetCurrent(value); }

    /// <summary>Raised when <see cref="Value"/> changes (when turned on screen, from code, or via binding — same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
    public event EventHandler<DvValueChangedEventArgs>? ValueChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override object? ValueFromBox(LayoutBox box) => (double)box.KnobValue;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);
}

/// <summary>
/// Handle for a <c>DvSlider</c> element. Members and value ownership rules are the same as <see cref="DvKnob"/>.
/// </summary>
public sealed class DvSlider : DvControl
{
    internal DvSlider(string id) : base(id) => Slot = new ValueSlot(new ValueCell<double>(), canOwn: true);

    /// <inheritdoc cref="DvKnob.Value"/>
    public double Value { get => ValueConvert.ToDouble(CurrentValue()) ?? 0; set => SetCurrent(value); }

    /// <summary>Raised when <see cref="Value"/> changes (when dragged on screen, from code, or via binding — same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
    public event EventHandler<DvValueChangedEventArgs>? ValueChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override object? ValueFromBox(LayoutBox box) => (double)box.KnobValue;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);
}
