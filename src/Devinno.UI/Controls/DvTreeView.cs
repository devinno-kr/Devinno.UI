using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvTreeView</c> element. Read and write the chosen item with <see cref="SelectedNode"/> (value ownership rules are the same as <see cref="DvToggle"/> — the value is <c>value</c>).
/// </summary>
/// <remarks>Items are based on the tree read by the last layout (including collapsed branches).</remarks>
public sealed class DvTreeView : DvControl
{
    internal DvTreeView(string id) : base(id) => Slot = new ValueSlot(new ValueCell<object?>(), canOwn: true);

    /// <summary>
    /// Gets or sets the chosen item (an item object from <c>items</c>/<c>item-children</c>, or <see langword="null"/> if none). If <c>item-value</c> is set, the item is found by value.
    /// Writing it writes that item's value to <c>value</c> and expands ancestor branches so it is visible. Writing an item not in the tree writes that object as the value (same as when <c>item-value</c> is not set).
    /// </summary>
    public object? SelectedNode
    {
        get
        {
            var v = CurrentValue();
            if (v is null || !TryGetLaidOutBox(out var box))
                return v;
            return box.TreeAll.FirstOrDefault(r => SelectValue.Matches(r.Value, v, box.SelectValueType))?.Item
                   ?? (box.Node.Attributes.ContainsKey("item-value") ? null : v);
        }
        set
        {
            if (value is not null && TryGetLaidOutBox(out var box) && box.TreeAll.FirstOrDefault(r => Equals(r.Item, value)) is { } row)
            {
                // 조상 가지 펼치기(뿌리까지)
                for (var parent = row.Parent; parent is not null && Surface is { } s && Node is { } n;
                     parent = box.TreeAll.FirstOrDefault(r => Equals(r.Item, parent))?.Parent)
                    s.SetTreeExpanded(n, parent, true);
                SetCurrent(SelectValue.TryConvert(row.Value, box.SelectValueType, out var c) ? c : row.Value);
                return;
            }
            SetCurrent(value);
        }
    }

    /// <summary>Raised when the chosen item changes (same rules as <see cref="DvToggle.CheckedChanged"/>). Expanding and collapsing are not changes.</summary>
    public event EventHandler<DvValueChangedEventArgs>? SelectionChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override string ValueEventName => "SelectionChanged";
    private protected override object? ValueFromBox(LayoutBox box) => box.InputValue;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);
}
