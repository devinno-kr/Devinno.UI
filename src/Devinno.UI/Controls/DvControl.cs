using SkiaSharp;

namespace Devinno.UI.Controls;

/// <summary>
/// A handle for working with one markup element from code. Keep it in a view model field and the same object is re-attached to the new screen even when the screen is rebuilt.
/// </summary>
/// <remarks>
/// If the <c>id</c> is not found in the markup, the handle is unattached (inactive). Writing values does not throw; they are only stored and have no effect on the screen.
/// </remarks>
public class DvControl
{
    // 덮어쓴 값 묶음 — 불변 레코드를 통째 교체해 읽는 쪽(렌더 스레드)이 항상 일관된 한 벌을 본다.
    private sealed record Ov(bool? Enabled, bool? Visible, string? Text)
    {
        public static readonly Ov Empty = new(null, null, null);
    }

    private readonly object _gate = new();
    private volatile Ov _ov = Ov.Empty;

    private void Write(Func<Ov, Ov> change)
    {
        lock (_gate)
            _ov = change(_ov);
        Surface?.Invalidate(); // 다른 스레드에서 써도 안전(플래그만 세움)
    }

    internal DvControl(string id) => Id = id;

    /// <summary>Gets the <c>id</c> value from the markup.</summary>
    public string Id { get; }

    /// <summary>Gets or sets an arbitrary value the app can attach.</summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Gets or sets whether it is enabled. Setting it overrides the markup value; revert with <see cref="ResetOverrides"/>.
    /// When reading, if <see langword="false"/> was written, it is <see langword="false"/> immediately, even before the next layout.
    /// Otherwise it returns the actual state from the last completed layout; if an ancestor is disabled it is <see langword="false"/> regardless of its own value.
    /// If not attached or not laid out yet, the last written value (<see langword="true"/> if none).
    /// </summary>
    public bool Enabled
    {
        get
        {
            var ov = _ov.Enabled;
            if (ov == false)
                return false; // 쓴 직후 읽어도 바로 반영
            return TryGetBox(out var box, out _) ? !box.Disabled : ov ?? true;
        }
        set => Write(o => o with { Enabled = value });
    }

    /// <summary>
    /// Gets or sets whether it is visible. Setting it overrides the markup value; revert with <see cref="ResetOverrides"/>.
    /// When reading, if <see langword="false"/> was written, it is <see langword="false"/> immediately, even before the next layout.
    /// Otherwise it returns whether this element is actually present in the last completed layout (<see langword="false"/> also when an ancestor is hidden).
    /// If not attached or not laid out yet, the last written value (<see langword="true"/> if none).
    /// </summary>
    public bool Visible
    {
        get
        {
            var ov = _ov.Visible;
            if (ov == false)
                return false; // 쓴 직후 읽어도 바로 반영
            var s = Surface;
            if (s is not null && IsAttached && s.HasControlBoxes)
                return s.TryGetBox(this, out _, out _);
            return ov ?? true;
        }
        set => Write(o => o with { Visible = value });
    }

    /// <summary>
    /// Gets the bounds relative to the window (top-level screen), in px. Inside a scroll box it reflects the current scroll position; on a <see cref="App.DvApp"/> area or page screen it reflects where that slot is drawn in the window.
    /// Based on the last completed layout; <see cref="SKRect.Empty"/> if not attached, not laid out yet, or not visible.
    /// </summary>
    public SKRect Bounds
    {
        get
        {
            var s = Surface;
            if (s is null || !TryGetBox(out var box, out var scroll))
                return SKRect.Empty;
            var r = box.Rect;
            var o = s.WindowOffset;
            r.Offset(o.X - scroll.X, o.Y - scroll.Y);
            return r;
        }
    }

    private EventHandler<DvClickEventArgs>? _click;

    /// <summary>
    /// Raised on press and release. Called after the markup <c>onclick</c> handling; subscribing makes an element without <c>onclick</c> receive clicks too (the click does not leak to an outer element's <c>onclick</c>).
    /// Not raised when disabled. Not raised on elements with their own behavior, such as toggle, input, select, grid and alarm widgets.
    /// If a subscriber throws, it is reported via <see cref="DvSurface.HandlerError"/> (<c>Kind</c> = <c>"Click"</c>).
    /// </summary>
    public event EventHandler<DvClickEventArgs>? Click
    {
        add { lock (_gate) _click += value; Surface?.Invalidate(); }
        remove { lock (_gate) _click -= value; Surface?.Invalidate(); }
    }

    /// <summary>
    /// Clears values overridden from code (<see cref="Enabled"/>, <see cref="Visible"/>, text, and values written over a one-way or fixed <c>value</c>) and reverts to the markup values.
    /// The value of an element whose handle owns the value (no <c>value</c>) and bound view model values are left unchanged.
    /// </summary>
    public virtual void ResetOverrides()
    {
        if (Slot is { } slot)
            slot.HasOverride = false;
        Write(_ => Ov.Empty);
    }

    /// <summary>Text to read: the override value, otherwise the markup/binding value from the last layout, otherwise <see langword="null"/>.</summary>
    internal string? CurrentText => _ov.Text ?? (TryGetBox(out var box, out _) ? box.ResolvedText : null);

    private bool TryGetBox(out Layout.LayoutBox box, out SKPoint scroll)
    {
        var s = Surface;
        if (s is not null && IsAttached)
            return s.TryGetBox(this, out box, out scroll);
        box = null!;
        scroll = default;
        return false;
    }

    internal void SetOverrideText(string? text) => Write(o => o with { Text = text });

    /// <summary>Snapshot of override values that layout reads at once (a single reference read, so it is never torn).</summary>
    internal (bool? Enabled, bool? Visible, string? Text, bool HasText, bool HasClick) Overrides
    {
        get
        {
            var o = _ov;
            return (o.Enabled, o.Visible, o.Text, o.Text is not null, _click is not null);
        }
    }

    internal volatile DvSurface? Surface;
    internal Markup.ElementNode? Node;
    internal bool IsAttached => Node is not null;

    internal void RaiseClick(SKPoint position) => _click?.Invoke(this, new DvClickEventArgs(position));

    // ── 값 핸들(설계 4.1 "값의 주인은 하나") ──
    // 값이 있는 요소의 핸들만 Slot 을 가진다. 주인은 요소의 value 속성이 정한다: {Bind X} = VM 속성, 없음 = 핸들 칸, 단방향·리터럴 = 마크업(쓰면 덮어씀).

    /// <summary>Value slot (<see langword="null"/> for handles without a value).</summary>
    internal ValueSlot? Slot { get; private protected set; }

    /// <summary>After attach (once the node is known). Handles whose slot type depends on element settings (<c>DvInput</c>) choose their slot here.</summary>
    internal virtual void OnAttached() { }

    /// <summary>After layout. Applies values written before layout (selected index, etc.).</summary>
    internal virtual void ApplyPending() { }

    /// <summary>The box from the last layout (false before attach or layout).</summary>
    private protected bool TryGetLaidOutBox(out Layout.LayoutBox box) => TryGetBox(out box, out _);

    /// <summary>Value from the box of the last layout, used when reading one-way or literal values.</summary>
    private protected virtual object? ValueFromBox(Layout.LayoutBox box) => null;

    /// <summary>Raises the changed event (each handle has its own event).</summary>
    private protected virtual void OnValueChanged(DvValueChangedEventArgs e) { }

    /// <summary>Name of the changed event (the Kind of HandlerError).</summary>
    private protected virtual string ValueEventName => "ValueChanged";

    /// <summary>Current value according to the owner: Bound = VM property, Owned = slot, Override = override value or the last layout value.</summary>
    private protected virtual object? CurrentValue()
    {
        var slot = Slot!;
        var node = Node;
        switch (slot.ModeOf(node))
        {
            case ValueMode.Bound when Surface is { } s && node is not null:
                return Binding.BindingResolver.Resolve(s.Vm, $"{{{Binding.BindingResolver.ExtractPath(node.Attributes[slot.ValueAttr])}}}");
            case ValueMode.Owned:
                return slot.CellValue;
            default:
                if (slot.HasOverride || !IsAttached)
                    return slot.CellValue;
                return TryGetBox(out var box, out _) ? ValueFromBox(box) : slot.CellValue;
        }
    }

    /// <summary>
    /// Writes a value from code: to the VM property if Bound (setter exceptions propagate), otherwise to the slot. Schedules a repaint and, if the value changed, raises the changed event (<c>ByUser</c> = false) on this thread.
    /// </summary>
    private protected void SetCurrent(object? value)
    {
        var slot = Slot!;
        slot.Changed(CurrentValue()); // 아직 기억한 값이 없으면 쓰기 전 값을 기준으로
        WriteValue(value);
        if (slot.Changed(CurrentValue()))
            OnValueChanged(new DvValueChangedEventArgs(false));
    }

    /// <summary>Writes the value to its owner (the write part of <see cref="SetCurrent"/>; radios write the group value).</summary>
    private protected virtual void WriteValue(object? value)
    {
        var slot = Slot!;
        var node = Node;
        var mode = slot.ModeOf(node);
        if (mode == ValueMode.Bound && Surface is { } s && node is not null)
        {
            Binding.BindingResolver.SetValue(s.Vm, Binding.BindingResolver.ExtractPath(node.Attributes[slot.ValueAttr]), value);
            s.Invalidate();
        }
        else
        {
            slot.CellValue = value;
            if (mode != ValueMode.Owned)
                slot.HasOverride = true;
            Surface?.Invalidate();
        }
    }

    /// <summary>
    /// Checks the value from the screen side: when the user changed it (<paramref name="byUser"/>) and when the screen is rebuilt (if the VM property changed).
    /// Handler exceptions are reported via <see cref="DvSurface.HandlerError"/> and swallowed.
    /// </summary>
    internal void ObserveValue(bool byUser)
    {
        if (Slot is not { } slot)
            return;
        object? now;
        try
        {
            now = CurrentValue();
        }
        catch (Exception ex)
        {
            Interaction.HandlerErrors.Report(Surface, ex, "getter", Id, Node?.Tag);
            return;
        }

        if (!slot.Changed(now))
            return;
        try
        {
            OnValueChanged(new DvValueChangedEventArgs(byUser));
        }
        catch (Exception ex)
        {
            Interaction.HandlerErrors.Report(Surface, ex, ValueEventName, Id, Node?.Tag);
        }
    }

    /// <summary>Common handle lock used when adding and removing changed-event handlers.</summary>
    private protected object EventGate => _gate;
}
