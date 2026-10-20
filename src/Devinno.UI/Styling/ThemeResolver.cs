using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Devinno.UI.Styling;

/// <summary>
/// Replaces theme token references in a style string (<c>token(name)</c>, <c>var(--name)</c>) with the actual values from <see cref="DvTheme"/>.
/// </summary>
/// <remarks>
/// An internal engine step. App code normally does not use it directly: when markup styles use <c>token(...)</c>, <see cref="DvSurface"/>/<see cref="DvRenderer"/> replace them at render time.
/// In custom widgets or tools, run user-entered color strings that may contain tokens through it before <see cref="DvColor.TryParse"/>.
/// </remarks>
public static class ThemeResolver
{
    /// <summary>
    /// The value used to replace tokens missing from the theme (magenta <c>#FF00FF</c>). It makes the problem stand out on screen instead of failing silently.
    /// Unresolved tokens can be detected by checking whether the result contains this string.
    /// </summary>
    public const string MissingColor = "#FF00FF";

    // token(spacing.md) = 정식 표기, var(--spacing-md) = 별칭
    private static readonly Regex VarRegex = new(
        @"token\(\s*([A-Za-z0-9_.-]+)\s*\)|var\(\s*--([A-Za-z0-9_.-]+)\s*\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Replaces every <c>token(name)</c> and <c>var(--name)</c> in <paramref name="style"/> with theme values. See <see cref="DvTheme"/> for naming rules.
    /// </summary>
    /// <remarks>
    /// Names missing from the theme are replaced with <see cref="MissingColor"/>, and a warning is written with <see cref="Trace.TraceWarning(string)"/> each time.
    /// Replacement is done only once, so tokens contained inside a token's value are not resolved.
    /// </remarks>
    /// <param name="style">A style declaration string (or a value such as a single color). Returned as is if <see langword="null"/> or whitespace.</param>
    /// <param name="theme">The theme to use for replacement. <see langword="null"/> means <see cref="DvTheme.Default"/>.</param>
    /// <returns>The string with tokens replaced. If the input is <see langword="null"/> or whitespace, the input as is.</returns>
    public static string? ResolveVars(string? style, DvTheme? theme)
    {
        if (string.IsNullOrWhiteSpace(style))
            return style;

        theme ??= DvTheme.Default;
        return VarRegex.Replace(style, m =>
        {
            var name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (theme.TryGet(name, out var value))
                return value;

            Trace.TraceWarning($"Devinno.UI: 토큰 미해석 '{name}'");
            return MissingColor;
        });
    }
}
