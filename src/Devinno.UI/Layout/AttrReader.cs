using Devinno.UI.Binding;
using Devinno.UI.Markup;

namespace Devinno.UI.Layout;

/// <summary>
/// Reads one element's attributes during a build: a value written from code (the handle's overrides) wins and is used as is, without binding;
/// otherwise the markup value is resolved against the context. Expressions (<c>onclick</c>, two-way <c>value</c> paths) are read with <see cref="Raw"/> and are never overridden.
/// A child element (DvTab, DvOption, DvColumn …) is read with <see cref="Child(ElementNode)"/>, which looks up that child's own overrides.
/// </summary>
internal readonly struct AttrReader(ElementNode node, object? ctx, IReadOnlyDictionary<string, object?>? overrides,
    Func<ElementNode, IReadOnlyDictionary<string, object?>?>? overridesOf = null)
{
    /// <summary>Test hook: called with the element and the attribute name on every read (this thread only).</summary>
    [ThreadStatic] internal static Action<ElementNode, string>? Watch;

    /// <summary>The element being read.</summary>
    public ElementNode Node => node;

    /// <summary>The binding context (screen VM, repeat item scope or component parameter scope).</summary>
    public object? Context => ctx;

    /// <summary>A reader for <paramref name="child"/> in the same context, with that child's own code overrides (none if it has no handle).</summary>
    public AttrReader Child(ElementNode child) => Child(child, ctx);

    /// <summary>A reader for <paramref name="child"/> in <paramref name="childCtx"/>, with that child's own code overrides (none if it has no handle).</summary>
    public AttrReader Child(ElementNode child, object? childCtx) => new(child, childCtx, overridesOf?.Invoke(child), overridesOf);

    /// <summary>Whether code has overridden <paramref name="name"/>.</summary>
    public bool Overridden(string name) => overrides?.ContainsKey(name) == true;

    /// <summary>Whether the attribute is present — in markup or overridden from code.</summary>
    public bool Has(string name)
    {
        Watch?.Invoke(node, name);
        return Overridden(name) || node.Attributes.ContainsKey(name);
    }

    /// <summary>The markup text as written (not resolved, not overridden). For expressions and ownership checks.</summary>
    public string? Raw(string name)
    {
        Watch?.Invoke(node, name);
        return node.Attributes.GetValueOrDefault(name);
    }

    /// <summary>The value: the code override, otherwise the markup value resolved (a single binding keeps its type). <see langword="null"/> if absent.</summary>
    public object? Value(string name) =>
        overrides is not null && overrides.TryGetValue(name, out var v) ? Seen(name, v) : BindingResolver.ResolveValueOrLiteral(ctx, Raw(name));

    /// <summary>The value as text (see <see cref="Value"/>).</summary>
    public string? Text(string name) =>
        overrides is not null && overrides.TryGetValue(name, out var v) ? BindingResolver.ToText(Seen(name, v), name) : BindingResolver.ResolveText(ctx, Raw(name));

    private object? Seen(string name, object? value)
    {
        Watch?.Invoke(node, name);
        return value;
    }
}
