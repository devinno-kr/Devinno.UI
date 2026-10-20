using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvSwitch</c> element (a built-in component: a label plus a toggle on one row). Use <see cref="Checked"/> to read and write the on state.
/// </summary>
/// <remarks>
/// Value ownership is the same as <see cref="DvToggle"/>. With <c>value="{Bind X}"</c> the owner is the view model property <c>X</c>; without <c>value</c> this handle holds the value and the switch can also be pressed on screen.
/// With a one-way (<c>value="{X}"</c>) or fixed value, a written value overrides the screen value (revert with <see cref="DvControl.ResetOverrides"/>) and the switch cannot be pressed on screen.
/// <see cref="DvControl.Bounds"/> covers the whole row, including the label.
/// </remarks>
public sealed class DvSwitch : DvControl
{
    internal DvSwitch(string id) : base(id) => Slot = new ValueSlot(new ValueCell<bool>(), canOwn: true);

    /// <summary>Gets or sets whether it is on. Setting it schedules a repaint and is safe from any thread.</summary>
    public bool Checked { get => ValueConvert.ToBool(CurrentValue()); set => SetCurrent(value); }

    /// <summary>
    /// Raised when <see cref="Checked"/> changes: when pressed on screen (<see cref="DvValueChangedEventArgs.ByUser"/> = true),
    /// when written from code (on the writing thread), and when a screen rebuild detects that the bound view model property changed.
    /// </summary>
    public event EventHandler<DvValueChangedEventArgs>? CheckedChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override string ValueEventName => "CheckedChanged";
    // 핸들이 붙은 박스 = 컴포넌트 루트(한 줄) — 값은 안쪽 토글
    private protected override object? ValueFromBox(LayoutBox box) => Toggle(box)?.ToggleOn ?? false;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);

    private static LayoutBox? Toggle(LayoutBox box)
    {
        if (box.Node.Tag == "DvToggle")
            return box;
        foreach (var c in box.Children)
            if (Toggle(c) is { } t)
                return t;
        return null;
    }
}
