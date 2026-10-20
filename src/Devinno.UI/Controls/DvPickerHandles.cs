using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvDateTimeInput</c> element. Use <see cref="Value"/> to read and write the date and time (value ownership rules are the same as <see cref="DvToggle"/>).
/// </summary>
public sealed class DvDateTimeInput : DvControl
{
    internal DvDateTimeInput(string id) : base(id) => Slot = new ValueSlot(new ValueCell<DateTime?>(), canOwn: true);

    /// <summary>
    /// Gets or sets the date and time (no value = <see langword="null"/>). If the bound property is <see cref="DateOnly"/> or <see cref="TimeOnly"/>, reads convert it to <see cref="DateTime"/>
    /// (<see cref="TimeOnly"/> uses today's date), and writes convert back to that type.
    /// </summary>
    public DateTime? Value
    {
        get => PickerValue.TryToDateTime(CurrentValue(), out var dt) ? dt : null;
        set => SetCurrent(value is { } v && BoundType() is { } t ? PickerValue.FromDateTime(v, t) : value);
    }

    /// <summary>Raised when <see cref="Value"/> changes (same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
    public event EventHandler<DvValueChangedEventArgs>? ValueChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override object? ValueFromBox(LayoutBox box) => box.InputValue;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);

    // 바인딩이면 그 속성 형식(DateOnly·TimeOnly 로 바꿔 쓰려고), 아니면 null
    private Type? BoundType()
        => Surface is { } s && Node is { } n && n.Attributes.TryGetValue("value", out var ve) && Binding.BindingResolver.IsTwoWay(ve)
            ? Binding.BindingResolver.ResolvePropertyType(s.Vm, Binding.BindingResolver.ExtractPath(ve))
            : null;
}

/// <summary>
/// Handle for a <c>DvColorInput</c> element. Use <see cref="Value"/> to read and write the color (value ownership rules are the same as <see cref="DvToggle"/>).
/// </summary>
public sealed class DvColorInput : DvControl
{
    internal DvColorInput(string id) : base(id) => Slot = new ValueSlot(new ValueCell<SKColor?>(), canOwn: true);

    /// <summary>
    /// Gets or sets the color (no value = <see langword="null"/>). If the bound property is a string (<c>#RRGGBB</c>), reads convert it to a color and writes store <c>#RRGGBB</c> (alpha is dropped).
    /// </summary>
    public SKColor? Value
    {
        get => PickerValue.TryToColor(CurrentValue(), out var c) ? c : null;
        set => SetCurrent(value is { } v && BoundType() == typeof(string) ? PickerValue.Hex(v) : value);
    }

    /// <summary>Raised when <see cref="Value"/> changes (same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
    public event EventHandler<DvValueChangedEventArgs>? ValueChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override object? ValueFromBox(LayoutBox box) => box.InputValue;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);

    private Type? BoundType()
        => Surface is { } s && Node is { } n && n.Attributes.TryGetValue("value", out var ve) && Binding.BindingResolver.IsTwoWay(ve)
            ? Binding.BindingResolver.ResolvePropertyType(s.Vm, Binding.BindingResolver.ExtractPath(ve))
            : null;
}
