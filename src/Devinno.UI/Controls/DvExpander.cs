using Devinno.UI.Binding;
using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvExpander</c> element. Use <see cref="IsExpanded"/> to read and write the expanded state.
/// </summary>
/// <remarks>
/// With <c>expanded="{Bind X}"</c> it reads and writes <c>X</c>; without <c>expanded</c> it reads and writes the expanded state remembered by the screen (kept while that screen is alive).
/// With a one-way or fixed <c>expanded</c>, a written value overrides the screen value (revert with <see cref="DvControl.ResetOverrides"/>).
/// </remarks>
public sealed class DvExpander : DvControl
{
    internal DvExpander(string id) : base(id) => Slot = new ValueSlot(new ValueCell<bool>(), canOwn: false) { ValueAttr = "expanded" };

    /// <summary>Gets or sets whether it is expanded. Setting it schedules a repaint and is safe from any thread.</summary>
    public bool IsExpanded { get => ValueConvert.ToBool(CurrentValue()); set => SetCurrent(value); }

    /// <summary>Raised when <see cref="IsExpanded"/> changes (header tap, code or binding; same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
    public event EventHandler<DvValueChangedEventArgs>? ExpandedChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override string ValueEventName => "ExpandedChanged";
    private protected override object? ValueFromBox(LayoutBox box) => box.ToggleOn;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);

    // expanded 없음 = 화면이 기억하는 펼침(없으면 접힘)
    private protected override object? CurrentValue()
    {
        if (Slot!.ModeOf(Node) == ValueMode.None && Surface is { } s && Node is { } n)
            return s.ExpanderValue(n) ?? false;
        return base.CurrentValue();
    }

    private protected override void WriteValue(object? value)
    {
        if (Slot!.ModeOf(Node) == ValueMode.None && Surface is { } s && Node is { } n)
        {
            s.SetExpanderValue(n, value is true);
            return;
        }
        base.WriteValue(value);
    }
}
