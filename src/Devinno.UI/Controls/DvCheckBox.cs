using Devinno.UI.Binding;
using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvCheckBox</c> element. Read and write the checked state with <see cref="Checked"/> (value ownership rules are the same as <see cref="DvToggle"/>).
/// </summary>
public sealed class DvCheckBox : DvControl
{
    internal DvCheckBox(string id) : base(id) => Slot = new ValueSlot(new ValueCell<bool>(), canOwn: true);

    /// <summary>Gets or sets whether it is checked. Writing schedules a redraw and may be done from another thread.</summary>
    public bool Checked { get => ValueConvert.ToBool(CurrentValue()); set => SetCurrent(value); }

    /// <summary>Raised when <see cref="Checked"/> changes (same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
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

/// <summary>
/// Handle for a <c>DvRadio</c> element. <see cref="Checked"/> = whether the group value equals this button's <c>option</c>.
/// </summary>
/// <remarks>
/// With <c>value="{Bind X}"</c>, <c>X</c> is the group value; without <c>value</c>, it is the value of the group remembered by the surface (the <c>group</c> name, or this button alone if none).
/// Writing <see langword="true"/> to <see cref="Checked"/> sets the group value to this <c>option</c> (other buttons in the same group turn off);
/// writing <see langword="false"/> while checked clears the group value (<see langword="null"/>). If the bound property's type cannot accept <see langword="null"/> (enum, int, etc.), this throws <see cref="InvalidOperationException"/> — check another button instead.
/// With a one-way or fixed <c>value</c>, the written value overrides the on-screen checked state (revert with <see cref="DvControl.ResetOverrides"/>).
/// </remarks>
public sealed class DvRadio : DvControl
{
    internal DvRadio(string id) : base(id) => Slot = new ValueSlot(new ValueCell<bool>(), canOwn: false);

    /// <summary>Gets or sets whether it is checked (whether the group value is this button's <c>option</c>).</summary>
    public bool Checked { get => ValueConvert.ToBool(CurrentValue()); set => SetCurrent(value); }

    /// <summary>
    /// Raised when <see cref="Checked"/> changes (same rules as <see cref="DvToggle.CheckedChanged"/>). Also raised when this button turns off because another button was chosen.
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

    private string Option => Node is { } n && Surface is { } s
        ? BindingResolver.ResolveText(s.Vm, n.Attributes.GetValueOrDefault("option")) ?? ""
        : "";

    // 묶음 열쇠(value 없을 때) — LayoutEngine.BuildRadio 와 같게: group 이름, 없으면 이 요소
    private object? GroupKey => Node is { } n && Surface is { } s
        ? BindingResolver.ResolveText(s.Vm, n.Attributes.GetValueOrDefault("group")) is { Length: > 0 } g ? g : n
        : null;

    private protected override object? CurrentValue()
    {
        var slot = Slot!;
        var node = Node;
        if (Surface is not { } s || node is null)
            return slot.CellValue;
        switch (slot.ModeOf(node))
        {
            case ValueMode.Bound:
            {
                var path = BindingResolver.ExtractPath(node.Attributes["value"]);
                return SelectValue.Matches(Option, BindingResolver.Resolve(s.Vm, $"{{{path}}}"), BindingResolver.ResolvePropertyType(s.Vm, path));
            }
            case ValueMode.Override:
                return slot.HasOverride ? slot.CellValue : TryGetLaidOutBox(out var box) && box.ToggleOn;
            default: // value 없음 — 화면이 기억하는 묶음
                return GroupKey is { } key && s.RadioGroupValue(key) == Option;
        }
    }

    private protected override void WriteValue(object? value)
    {
        var on = value is true;
        var slot = Slot!;
        var node = Node;
        if (Surface is not { } s || node is null)
        {
            slot.CellValue = on; // 연결 전 — 저장만
            return;
        }

        var cur = ValueConvert.ToBool(CurrentValue());
        switch (slot.ModeOf(node))
        {
            case ValueMode.Bound:
            {
                if (on == cur)
                    return;
                var path = BindingResolver.ExtractPath(node.Attributes["value"]);
                var type = BindingResolver.ResolvePropertyType(s.Vm, path);
                object? v = null;
                if (on && !SelectValue.TryConvert(Option, type, out v))
                    throw new InvalidOperationException($"Devinno.UI: DvRadio '{Id}' option '{Option}' 을 바인딩 속성 형식으로 바꿀 수 없습니다.");
                // 끄기 = 묶음 값 비움. null 을 못 받는 속성(enum·int 등)은 기본값이 들어가 다른 버튼이 켜질 수 있으므로 막는다
                if (!on && type is { IsValueType: true } && Nullable.GetUnderlyingType(type) is null)
                    throw new InvalidOperationException($"Devinno.UI: DvRadio '{Id}' — 바인딩 속성 {type.Name} 은 null 을 받을 수 없어 끌 수 없습니다. 다른 버튼의 Checked 를 true 로 쓰세요.");
                BindingResolver.SetValue(s.Vm, path, v);
                s.Invalidate();
                break;
            }
            case ValueMode.Override:
                slot.CellValue = on;
                slot.HasOverride = true;
                s.Invalidate();
                break;
            default:
                if (on != cur && GroupKey is { } key)
                    s.SetRadioGroupValue(key, on ? Option : null);
                break;
        }

        s.ObserveRadios(byUser: false, except: this); // 같은 묶음의 다른 버튼이 꺼졌으면 그 핸들도 알린다
    }
}
