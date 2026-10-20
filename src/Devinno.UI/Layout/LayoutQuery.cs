namespace Devinno.UI.Layout;

/// <summary>
/// Helper for finding boxes in a computed <see cref="LayoutBox"/> tree.
/// </summary>
/// <remarks>
/// A search tool used inside the engine (e.g. finding the box of a specific tag on a screen). App code normally uses <see cref="DvSurface"/>, so it does not need this.
/// Tests or tools can use it to take the result of <see cref="LayoutEngine.Layout"/> directly and check a specific element's position or value.
/// Currently it can search only by tag name; there is no method to search by attributes such as <c>id</c> or <c>name</c> (if needed, walk <see cref="LayoutBox.Children"/> yourself, checking the attributes of <see cref="LayoutBox.Node"/>).
/// </remarks>
/// <example>
/// <code>
/// using Devinno.UI.Layout;
/// using Devinno.UI.Markup;
///
/// var root = MarkupParser.Parse("&lt;DvStack&gt;&lt;DvGauge value=\"{Speed}\"/&gt;&lt;/DvStack&gt;");
/// var tree = new LayoutEngine().Layout(root, new { Speed = 42 }, 800, 600);
/// var gauge = LayoutQuery.FindByTag(tree, "DvGauge");
/// if (gauge is not null)
///     Console.WriteLine($"{gauge.Rect} 값={gauge.GaugeValue}");
/// </code>
/// </example>
public static class LayoutQuery
{
    /// <summary>
    /// Searches depth-first from <paramref name="root"/> (self, then children) and returns the first box whose tag is <paramref name="tag"/>.
    /// </summary>
    /// <param name="root">The box to start searching from. The box itself is also checked.</param>
    /// <param name="tag">The element name to find (case-sensitive). For example <c>DvGauge</c>.</param>
    /// <returns>The first box found, or <see langword="null"/> if none. Elements not built because <c>visible</c> is false are not in the tree, so they are not found.</returns>
    public static LayoutBox? FindByTag(LayoutBox root, string tag)
    {
        if (root.Node.Tag == tag)
            return root;

        foreach (var child in root.Children)
        {
            var found = FindByTag(child, tag);
            if (found is not null)
                return found;
        }

        return null;
    }
}
