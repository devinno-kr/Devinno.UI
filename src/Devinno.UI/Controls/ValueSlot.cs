using Devinno.UI.Binding;
using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Value cell held by a handle. The binding resolver reads and writes it through <see cref="ValueSlot.CellExpr"/> (<c>{Bind Value}</c>) — a public property so reflection can find it.
/// The type parameter is the "bound property type", so numeric input rules and option conversion behave exactly as with a VM property.
/// </summary>
internal sealed class ValueCell<T>
{
    private readonly object _gate = new();
    private T _value = default!;

    public T Value
    {
        get { lock (_gate) return _value; }
        set { lock (_gate) _value = value; }
    }
}

/// <summary>Owner of the value (design 4.1).</summary>
internal enum ValueMode
{
    /// <summary><c>value="{Bind X}"</c> — the VM property is the owner.</summary>
    Bound,
    /// <summary>No <c>value</c> — the handle cell is the owner; changed by pressing on screen.</summary>
    Owned,
    /// <summary>One-way or literal — the markup is the owner; writes from the handle override it (read-only).</summary>
    Override,
    /// <summary>Element without a value (a plain <c>DvButton</c> without <c>value</c>).</summary>
    None,
}

/// <summary>
/// State of one value handle — the cell, whether it is overridden, and the last reported value (for deciding change events).
/// The owner of the on-screen value is determined by the element's <c>value</c> attribute (<see cref="ModeOf"/>); the layout and commit paths in <see cref="LayoutEngine"/>/<see cref="DvSurface"/> use this cell.
/// </summary>
internal sealed class ValueSlot
{
    /// <summary>Fake two-way expression placed in the box when the handle is the owner — its target is <see cref="Cell"/>.</summary>
    public const string CellExpr = "{Bind Value}";

    private readonly object _gate = new();
    private object? _last;
    private bool _hasLast;
    private volatile bool _has;

    public ValueSlot(object cell, bool canOwn)
    {
        Cell = cell;
        CanOwn = canOwn;
    }

    /// <summary><see cref="ValueCell{T}"/>.</summary>
    public object Cell { get; }

    /// <summary>Whether the handle becomes the owner when there is no <c>value</c> (not for <c>DvButton</c> — a button without a value is a plain button).</summary>
    public bool CanOwn { get; }

    /// <summary>Attribute that determines the owner — usually <c>value</c>; <c>selected-items</c> for multi-select <c>DvListBox</c>.</summary>
    public string ValueAttr { get; init; } = "value";

    /// <summary>Whether code has overridden a one-way or literal value (cleared with <see cref="DvControl.ResetOverrides"/>).</summary>
    public bool HasOverride { get => _has; set => _has = value; }

    public Type CellType => Cell.GetType().GetGenericArguments()[0];

    public object? CellValue
    {
        get => BindingResolver.Resolve(Cell, "{Value}");
        set => BindingResolver.SetValue(Cell, "Value", value);
    }

    public ValueMode ModeOf(Markup.ElementNode? node)
    {
        if (node is null)
            return CanOwn ? ValueMode.Owned : ValueMode.None;
        if (!node.Attributes.TryGetValue(ValueAttr, out var ve))
            return CanOwn ? ValueMode.Owned : ValueMode.None;
        return BindingResolver.IsTwoWay(ve) ? ValueMode.Bound : ValueMode.Override;
    }

    /// <summary>
    /// Compares the current value with the last reported value; if it changed, remembers it and returns <see langword="true"/>.
    /// The first value seen (nothing remembered yet) is only remembered and returns <see langword="false"/> — the first screen is not a "change".
    /// </summary>
    public bool Changed(object? now)
    {
        lock (_gate)
        {
            if (_hasLast && Same(_last, now))
                return false;
            var first = !_hasLast;
            _last = now;
            _hasLast = true;
            return !first;
        }
    }

    // 목록 값(여러 선택)은 새 목록이어도 내용이 같으면 같은 값
    private static bool Same(object? a, object? b)
        => Equals(a, b) ||
           a is System.Collections.IEnumerable ea and not string && b is System.Collections.IEnumerable eb and not string &&
           ea.Cast<object?>().SequenceEqual(eb.Cast<object?>());
}
