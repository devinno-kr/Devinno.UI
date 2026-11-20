namespace Devinno.UI.Widgets;

/// <summary>
/// One definition (child element) of a <see cref="DvDefinitionWidget"/>: its tag and its attribute values, read with the same rules as the widget's own attributes.
/// </summary>
/// <remarks>The values are the snapshot taken when the screen was last built. Get the list from <see cref="DvWidgetContext.Definitions"/> on every call; do not keep it.</remarks>
public sealed class DvDefinition
{
    private readonly Dictionary<string, (object? Value, string? Raw)> _values;

    internal DvDefinition(string tag, Dictionary<string, (object? Value, string? Raw)> values)
    {
        Tag = tag;
        _values = values;
    }

    /// <summary>Gets the element's tag (one of <see cref="DvDefinitionWidget.DefinitionTags"/>).</summary>
    public string Tag { get; }

    /// <summary>Gets the names of the attributes written on the element, so the widget can report names it does not know.</summary>
    public IEnumerable<string> Names => _values.Keys;

    /// <summary>Gets whether the element has the attribute (literal or binding).</summary>
    /// <param name="attr">The attribute name.</param>
    public bool Has(string attr) => WidgetValues.Has(_values, attr);

    /// <summary>Gets an attribute's resolved value as the original object (a number, a color, a model object …). <see langword="null"/> if missing.</summary>
    /// <param name="attr">The attribute name.</param>
    public object? Value(string attr) => WidgetValues.Value(_values, attr);

    /// <summary>Gets an attribute as text (bindings resolved, <c>format</c> of the binding applied). <see langword="null"/> if missing.</summary>
    /// <param name="attr">The attribute name.</param>
    public string? Text(string attr) => WidgetValues.Text(_values, attr);

    /// <summary>Gets an attribute as a number. Returns <paramref name="fallback"/> if it is missing or not a number.</summary>
    /// <param name="attr">The attribute name.</param>
    /// <param name="fallback">The value to use when there is no number.</param>
    public double Number(string attr, double fallback = 0) => WidgetValues.Number(_values, attr, fallback);

    /// <summary>
    /// Gets an attribute as a flag, with the same rule as <see cref="DvWidgetContext.Flag"/>: <c>true</c>, a non-zero number or a bool <see langword="true"/> binding is on;
    /// <c>false</c>, <c>0</c> or empty text is off. Returns <paramref name="fallback"/> if missing or any other text.
    /// </summary>
    /// <param name="attr">The attribute name.</param>
    /// <param name="fallback">The value to use when the attribute is missing or not a flag.</param>
    public bool Flag(string attr, bool fallback = false) => WidgetValues.Flag(_values, attr, fallback);
}
