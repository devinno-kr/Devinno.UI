using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvSelect</c> element. Read and write the selection with <see cref="SelectedIndex"/> and <see cref="SelectedValue"/> (value ownership rules are the same as <see cref="DvToggle"/>).
/// </summary>
public sealed class DvSelect : DvControl
{
    private readonly SelectionCore _core;

    internal DvSelect(string id) : base(id)
    {
        Slot = new ValueSlot(new ValueCell<object?>(), canOwn: true);
        _core = new SelectionCore(this);
    }

    /// <summary>
    /// Gets or sets the index of the chosen option (-1 if none). Options are as of the last layout. Writing it writes that option's value as <see cref="SelectedValue"/> (-1 = no value).
    /// If written before layout, it is applied at the first layout.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">When less than -1 or not less than the number of options (after layout).</exception>
    public int SelectedIndex { get => _core.Index; set => _core.SetIndex(value); }

    /// <summary>Gets or sets the value of the chosen option (the same as the bound property value). Writing a value not among the options shows nothing selected.</summary>
    public object? SelectedValue { get => CurrentValue(); set => SetCurrent(value); }

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
    internal override void ApplyPending() => _core.ApplyPending();

    internal object? Current() => CurrentValue();
    internal void Set(object? v) => SetCurrent(v);
    internal bool TryLaidOut(out LayoutBox box) => TryGetLaidOutBox(out box);
}

/// <summary>Handle for a <c>DvSegment</c> element. Its members are the same as <see cref="DvSelect"/>.</summary>
public sealed class DvSegment : DvControl
{
    private readonly SelectionCore _core;

    internal DvSegment(string id) : base(id)
    {
        Slot = new ValueSlot(new ValueCell<object?>(), canOwn: true);
        _core = new SelectionCore(this);
    }

    /// <inheritdoc cref="DvSelect.SelectedIndex"/>
    public int SelectedIndex { get => _core.Index; set => _core.SetIndex(value); }

    /// <inheritdoc cref="DvSelect.SelectedValue"/>
    public object? SelectedValue { get => CurrentValue(); set => SetCurrent(value); }

    /// <inheritdoc cref="DvSelect.SelectionChanged"/>
    public event EventHandler<DvValueChangedEventArgs>? SelectionChanged
    {
        add { lock (EventGate) _changed += value; }
        remove { lock (EventGate) _changed -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _changed;
    private protected override string ValueEventName => "SelectionChanged";
    private protected override object? ValueFromBox(LayoutBox box) => box.InputValue;
    private protected override void OnValueChanged(DvValueChangedEventArgs e) => _changed?.Invoke(this, e);
    internal override void ApplyPending() => _core.ApplyPending();

    internal object? Current() => CurrentValue();
    internal void Set(object? v) => SetCurrent(v);
    internal bool TryLaidOut(out LayoutBox box) => TryGetLaidOutBox(out box);
}

/// <summary>Shared logic for selection handles — index ↔ option value (based on the options of the last layout); an index set before layout is applied at the first layout.</summary>
internal sealed class SelectionCore
{
    private readonly DvControl _owner;
    private readonly Func<object?> _current;
    private readonly Action<object?> _set;
    private readonly TryBox _tryBox;
    private int? _pending;

    private delegate bool TryBox(out LayoutBox box);

    public SelectionCore(DvSelect s) : this(s, s.Current, s.Set, s.TryLaidOut) { }
    public SelectionCore(DvSegment s) : this(s, s.Current, s.Set, s.TryLaidOut) { }

    private SelectionCore(DvControl owner, Func<object?> current, Action<object?> set, TryBox tryBox)
    {
        _owner = owner;
        _current = current;
        _set = set;
        _tryBox = tryBox;
    }

    public int Index
    {
        get
        {
            if (!_tryBox(out var box))
                return _pending ?? -1;
            var cur = _current();
            return box.SelectOptions.FindIndex(o => SelectValue.Matches(o.Value, cur, box.SelectValueType));
        }
    }

    public void SetIndex(int index)
    {
        if (index < -1)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Devinno.UI: SelectedIndex must be -1 or greater.");
        if (!_tryBox(out var box))
        {
            _pending = index; // 배치 전 — 선택지를 아직 모른다
            _owner.Surface?.Invalidate();
            return;
        }

        if (index >= box.SelectOptions.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Devinno.UI: there are only {box.SelectOptions.Count} options.");
        _set(index < 0 ? null : Typed(box, box.SelectOptions[index].Value));
    }

    // DvOption 값은 글자 그대로 — 바인딩 속성 형식(enum·숫자)으로 바꿔 쓴다(드롭다운과 같음)
    private static object? Typed(LayoutBox box, object? v) => SelectValue.TryConvert(v, box.SelectValueType, out var c) ? c : v;

    public void ApplyPending()
    {
        if (_pending is not { } i || !_tryBox(out var box))
            return;
        _pending = null;
        if (i < box.SelectOptions.Count)
            _set(i < 0 ? null : Typed(box, box.SelectOptions[i].Value));
        else
            System.Diagnostics.Trace.TraceWarning($"Devinno.UI: id '{_owner.Id}' SelectedIndex {i}: there are only {box.SelectOptions.Count} options; not applied.");
    }
}
