namespace Devinno.UI.Markup;

/// <summary>
/// A node representing one markup element. Created by <see cref="MarkupParser.Parse"/>; it holds the raw text with bindings and styles not yet resolved.
/// </summary>
/// <remarks>
/// This is internal engine-stage data. App code normally does not use it directly; it uses <see cref="DvSurface"/>/<see cref="DvRenderer"/> instead.
/// <see cref="Layout.LayoutEngine"/> builds a <see cref="Layout.LayoutBox"/> tree from this tree and the view model, and each box's <see cref="Layout.LayoutBox.Node"/> points to its original node.
/// </remarks>
public sealed class ElementNode
{
    /// <summary>Element name (local name without the namespace prefix). E.g. <c>DvButton</c>, <c>DvStack</c>.</summary>
    public required string Tag { get; init; }
    /// <summary>
    /// Attribute name → raw value. Names are case-sensitive. Values are the raw strings, with binding expressions such as <c>{Path}</c> not resolved.
    /// </summary>
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);
    /// <summary>Child element nodes, in the order they appear in the markup.</summary>
    public List<ElementNode> Children { get; } = new();
    /// <summary>
    /// The text inside the element (leading and trailing whitespace trimmed). Filled only when there are no child elements and the text is not just whitespace; otherwise <see langword="null"/>.
    /// If there is even one child element, the text between children is discarded. E.g. <c>안녕</c> in <c>&lt;DvLabel&gt;안녕&lt;/DvLabel&gt;</c>, or the stylesheet body of <c>DvStyle</c>.
    /// </summary>
    public string? Text { get; set; }
}
