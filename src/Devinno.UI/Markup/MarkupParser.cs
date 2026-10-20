using System.Xml.Linq;

namespace Devinno.UI.Markup;

/// <summary>
/// Parser that turns an XML markup string into an <see cref="ElementNode"/> tree. This is the first stage of rendering a screen.
/// </summary>
/// <remarks>
/// <para>This is an internal engine stage. App code usually does not call it directly; it passes the markup string to <see cref="DvSurface"/>/<see cref="DvRenderer"/>.
/// Use it directly to pre-parse markup and catch XML syntax errors before showing a screen, or to inspect the element tree in tools.</para>
/// <para>This stage checks XML syntax only. Unknown tags or attributes and invalid binding paths or style values do not cause errors here.
/// Element and attribute names use only the local name, with any namespace prefix dropped. It is stateless, so it can be called from any thread.</para>
/// </remarks>
/// <example>
/// <code>
/// using Devinno.UI.Markup;
///
/// try
/// {
///     var root = MarkupParser.Parse("&lt;DvStack&gt;&lt;DvLabel text=\"{Title}\"/&gt;&lt;/DvStack&gt;");
///     Console.WriteLine($"{root.Tag}, 자식 {root.Children.Count}개"); // DvStack, 자식 1개
/// }
/// catch (System.Xml.XmlException ex)
/// {
///     Console.WriteLine($"마크업 오류 {ex.LineNumber}:{ex.LinePosition} {ex.Message}");
/// }
/// </code>
/// </example>
public static class MarkupParser
{
    /// <summary>
    /// Parses a markup string and returns the <see cref="ElementNode"/> tree of the root element.
    /// </summary>
    /// <param name="markup">An XML markup string with a single root element.</param>
    /// <returns>The root element node: a new tree with attributes, child elements and text all filled in.</returns>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The XML syntax is invalid (unclosed tag, unquoted attribute value, unescaped <c>&lt;</c> or <c>&amp;</c>, etc.). Contains the line and column position.</exception>
    public static ElementNode Parse(string markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
            throw new ArgumentException("markup is empty", nameof(markup));

        var doc = XDocument.Parse(markup);
        return Convert(doc.Root!);
    }

    private static ElementNode Convert(XElement el)
    {
        var node = new ElementNode { Tag = el.Name.LocalName };

        foreach (var a in el.Attributes())
            node.Attributes[a.Name.LocalName] = a.Value;

        foreach (var child in el.Elements())
            node.Children.Add(Convert(child));

        if (!el.HasElements)
        {
            var text = el.Value;
            if (!string.IsNullOrWhiteSpace(text))
                node.Text = text.Trim();
        }

        return node;
    }
}
