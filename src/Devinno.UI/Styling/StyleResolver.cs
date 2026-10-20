namespace Devinno.UI.Styling;

/// <summary>
/// Helper that merges base declarations and state (hover, etc.) declarations and parses them into a single <see cref="ComputedStyle"/>.
/// </summary>
/// <remarks>
/// This is an internal engine stage. App code normally does not use it directly; it uses markup <c>class</c>/<c>style</c>/<c>hover-style</c> and so on,
/// and parsing is handled by <see cref="DvSurface"/>/<see cref="DvRenderer"/>. Use it from custom tools that parse declaration strings directly.
/// </remarks>
public static class StyleResolver
{
    /// <summary>
    /// Joins the two declaration lists with <c>;</c> and parses them with <see cref="StyleParser.Parse"/>. For the same key, <paramref name="stateDecls"/> wins.
    /// Tokens (<c>token(...)</c>) are not substituted, so run <see cref="ThemeResolver.ResolveVars"/> first if needed.
    /// </summary>
    /// <param name="baseDecls">Base declarations (e.g. <c>background:#fff; padding:8</c>). May be <see langword="null"/> or empty.</param>
    /// <param name="stateDecls">State declarations that override (e.g. <c>background:#eee</c>). If <see langword="null"/> or empty, only <paramref name="baseDecls"/> is parsed.</param>
    /// <returns>A newly created <see cref="ComputedStyle"/>.</returns>
    public static ComputedStyle Merge(string? baseDecls, string? stateDecls)
    {
        if (string.IsNullOrWhiteSpace(stateDecls))
            return StyleParser.Parse(baseDecls);

        var combined = string.IsNullOrWhiteSpace(baseDecls) ? stateDecls : $"{baseDecls};{stateDecls}";
        return StyleParser.Parse(combined);
    }
}
