using System.Globalization;

namespace Devinno.UI.Styling;

/// <summary>
/// A linear gradient background. Created from the style <c>background: linear-gradient(각도, 색1, 색2, …)</c> (angle, color1, color2, …).
/// </summary>
/// <remarks>
/// <para>Supported syntax: the first value is an optional direction — an angle such as <c>135deg</c>, or <c>to right</c>, <c>to left</c>, <c>to top</c>, <c>to bottom</c> (case and the number of spaces between words are ignored).
/// If the direction is omitted, it is 180 degrees (top → bottom). Angles are the same as CSS: 0 degrees = bottom to top, 90 degrees = left to right.
/// Diagonal keywords such as <c>to bottom right</c> are not supported and are treated as 180 degrees.</para>
/// <para>Colors must be in the <see cref="DvColor.TryParse"/> format. Stop positions after a color (<c>#fff 30%</c>) are ignored, and the colors are spread evenly.
/// Colors that cannot be parsed are skipped. Theme tokens (<c>token(primary)</c>) are replaced with values before style parsing, so they can be used in markup.</para>
/// <para>If parsing fails (format mismatch, fewer than 2 valid colors), it returns <see langword="null"/> and does not throw.</para>
/// </remarks>
/// <example>
/// <code>
/// var g = LinearGradient.TryParse("linear-gradient(135deg, #2563eb, #7c3aed)");
/// // g.AngleDeg == 135, g.Stops.Count == 2
/// </code>
/// </example>
public sealed class LinearGradient
{
    /// <summary>The gradient direction (degrees). By CSS convention, 0 = bottom→top, 90 = left→right, 180 = top→bottom (the value used as the default). When constructed directly, it starts at 0.</summary>
    public float AngleDeg { get; init; }

    /// <summary>The list of color stops. They are placed in order from start to end at equal intervals. At least 2 are needed to draw.</summary>
    public List<DvColor> Stops { get; } = new();

    /// <summary>
    /// Parses a <c>linear-gradient(...)</c> string. <c>linear-gradient(</c> is matched case-insensitively, and leading and trailing whitespace is ignored.
    /// </summary>
    /// <param name="v">The string to parse (e.g. <c>linear-gradient(to right, #fff, #000)</c>). Must not be <see langword="null"/> (not checked).</param>
    /// <returns>The parsed gradient, or <see langword="null"/> if the format does not match or there are fewer than 2 valid colors.</returns>
    public static LinearGradient? TryParse(string v)
    {
        v = v.Trim();
        const string prefix = "linear-gradient(";
        if (!v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !v.EndsWith(")"))
            return null;

        var inner = v[prefix.Length..^1];
        var parts = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        var angle = ParseAngle(parts[0], out var consumed);
        var g = new LinearGradient { AngleDeg = consumed ? angle : 180 };
        var startIdx = consumed ? 1 : 0;

        for (var i = startIdx; i < parts.Length; i++)
        {
            if (DvColor.TryParse(StripStop(parts[i]), out var col))
                g.Stops.Add(col);
        }

        return g.Stops.Count >= 2 ? g : null;
    }

    private static float ParseAngle(string p, out bool consumed)
    {
        p = p.Trim();
        consumed = false;

        if (p.EndsWith("deg", StringComparison.OrdinalIgnoreCase) &&
            float.TryParse(p[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
        {
            consumed = true;
            return a;
        }

        if (p.StartsWith("to ", StringComparison.OrdinalIgnoreCase))
        {
            consumed = true;
            return p[3..].Trim().ToLowerInvariant() switch
            {
                "right" => 90,
                "left" => 270,
                "top" => 0,
                _ => 180
            };
        }

        return 180;
    }

    private static string StripStop(string s)
    {
        var sp = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return sp.Length > 0 ? sp[0] : s;
    }
}
