using Devinno.UI.Binding;
using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// The handle of a <c>DvListBox</c> element. With single selection the value is <c>value</c>; with multiple selection (<c>selection-mode="multi"</c>) it is <c>selected-items</c> (value ownership rules are the same as <see cref="DvToggle"/>).
/// </summary>
/// <remarks>Option numbers are based on the options of the last layout. Values are option values (<c>item-value</c>/<c>DvOption value</c>), not item objects.</remarks>
public sealed class DvListBox : DvControl
{
    private int? _pendingIndex;

    internal DvListBox(string id) : base(id) => Slot = new ValueSlot(new ValueCell<object?>(), canOwn: true);

    private bool Multi => Node is { } n && Surface is { } s &&
                          BindingResolver.ResolveText(s.Vm, n.Attributes.GetValueOrDefault("selection-mode")) == "multi";

    internal override void OnAttached()
    {
        // 칸 = 한 개면 값 하나(value), 여럿이면 값 목록(selected-items)
        var multi = Multi;
        if (multi == (Slot!.ValueAttr == "selected-items"))
            return;
        Slot = multi
            ? new ValueSlot(new ValueCell<IReadOnlyList<object?>?>(), canOwn: true) { ValueAttr = "selected-items" }
            : new ValueSlot(new ValueCell<object?>(), canOwn: true);
    }

    /// <summary>
    /// Gets or sets the selected option number (-1 if none; the first one with multiple selection). Setting it selects only that one (-1 = select none). If set before arrange, it is applied at the first layout.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than -1, or not less than the number of options (after arrange).</exception>
    public int SelectedIndex
    {
        get
        {
            if (!TryGetLaidOutBox(out var box))
                return _pendingIndex ?? -1;
            var picked = Picked();
            return box.SelectOptions.FindIndex(o => picked.Any(c => SelectValue.Matches(o.Value, c, box.SelectValueType)));
        }
        set
        {
            if (value < -1)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Devinno.UI: SelectedIndex 는 -1 이상이어야 합니다.");
            if (!TryGetLaidOutBox(out var box))
            {
                _pendingIndex = value;
                Surface?.Invalidate();
                return;
            }
            if (value >= box.SelectOptions.Count)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Devinno.UI: 선택지가 {box.SelectOptions.Count}개입니다.");
            Pick(value < 0 ? Array.Empty<object?>() : new[] { box.SelectOptions[value].Value });
        }
    }

    /// <summary>Gets or sets the selected value (<see langword="null"/> if none; the first one with multiple selection). Setting it selects only that value.</summary>
    public object? SelectedValue
    {
        get => Picked().FirstOrDefault();
        set => Pick(value is null ? Array.Empty<object?>() : new[] { value });
    }

    /// <summary>
    /// Gets or sets all selected values (in the order they were selected, not option order). With single selection there are 0 or 1.
    /// Setting it selects those values; with single selection only the first value is used. A bound list property is written as a new list of that property's type (T[], List&lt;T&gt;, etc.).
    /// </summary>
    public IReadOnlyList<object?> SelectedItems
    {
        get => Picked();
        set => Pick(value ?? Array.Empty<object?>());
    }

    /// <summary>Raised when the selection changes (same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
    public event EventHandler<DvValueChangedEventArgs>? SelectionChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override string ValueEventName => "SelectionChanged";
    private protected override object? ValueFromBox(LayoutBox box) => box.InputValue;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);

    internal override void ApplyPending()
    {
        if (_pendingIndex is not { } i || !TryGetLaidOutBox(out var box))
            return;
        _pendingIndex = null;
        if (i < box.SelectOptions.Count)
            Pick(i < 0 ? Array.Empty<object?>() : new[] { box.SelectOptions[i].Value });
        else
            System.Diagnostics.Trace.TraceWarning($"Devinno.UI: id '{Id}' SelectedIndex {i} — 선택지가 {box.SelectOptions.Count}개라 적용하지 않습니다.");
    }

    private List<object?> Picked()
    {
        var v = CurrentValue();
        if (Slot!.ValueAttr == "selected-items")
            return v is System.Collections.IEnumerable e and not string ? e.Cast<object?>().ToList() : new List<object?>();
        return v is null ? new List<object?>() : new List<object?> { v };
    }

    // 고르기 — 한 개면 값 하나, 여럿이면 바인딩 속성 형식의 새 목록(바인딩이 아니면 값 목록 그대로)
    private void Pick(IReadOnlyList<object?> values)
    {
        if (Slot!.ValueAttr != "selected-items")
        {
            // DvOption 값은 글자 그대로 — 바인딩 속성 형식(enum·숫자)으로 바꿔 쓴다
            var v = values.Count > 0 ? values[0] : null;
            if (TryGetLaidOutBox(out var box) && SelectValue.TryConvert(v, box.SelectValueType, out var c))
                v = c;
            SetCurrent(v);
            return;
        }

        if (Slot.ModeOf(Node) == ValueMode.Bound && Surface is { } s && Node is { } n)
        {
            var type = BindingResolver.ResolvePropertyType(s.Vm, BindingResolver.ExtractPath(n.Attributes["selected-items"]));
            var elem = type is null ? null : Interaction.GridInteraction.ElementType(Nullable.GetUnderlyingType(type) ?? type);
            var items = values.Select(v => SelectValue.TryConvert(v, elem == typeof(object) ? null : elem, out var c) ? c : v).OfType<object>().ToList();
            var (ok, list) = Interaction.GridInteraction.Snapshot(type, items);
            if (!ok)
                throw new InvalidOperationException($"Devinno.UI: DvListBox '{Id}' selected-items 속성 형식({type?.Name})에 목록을 쓸 수 없습니다(T[]·List<T>·IList<T>·IReadOnlyList<T>·IEnumerable<T>).");
            SetCurrent(list);
            return;
        }

        SetCurrent(values.ToList());
    }
}
