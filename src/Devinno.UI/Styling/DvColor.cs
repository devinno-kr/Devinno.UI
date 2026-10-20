using System.Globalization;
using SkiaSharp;

namespace Devinno.UI.Styling;

/// <summary>
/// An 8-bit RGBA color value. Style <c>color</c>, <c>background</c> and <c>border</c> colors and theme token values are parsed into this type.
/// </summary>
/// <remarks>
/// <para>To create one from a string, use <see cref="TryParse"/>. The only supported notations are <c>#RGB</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c> (alpha <b>last</b>)
/// and <c>transparent</c>. <c>#RGBA</c> (4 digits), <c>rgb()</c>/<c>rgba()</c>/<c>hsl()</c> and color names such as <c>red</c> are not supported.</para>
/// <para>Parsing failures do not throw. <see cref="TryParse"/> returns <see langword="false"/>, and the style parser ignores that declaration.</para>
/// <para>Theme tokens (<c>token(name)</c>) are not parsed by this type directly. <see cref="ThemeResolver"/> first replaces a token with its actual value, which is then parsed.</para>
/// </remarks>
/// <example>
/// <code>
/// if (DvColor.TryParse("#2563eb80", out var c))
///     paint.Color = c.ToSK();          // R=0x25, G=0x63, B=0xEB, A=0x80
/// var red = new DvColor(255, 0, 0, 255);
/// </code>
/// </example>
public readonly struct DvColor
{
    /// <summary>Red component (0–255).</summary>
    public readonly byte R;
    /// <summary>Green component (0–255).</summary>
    public readonly byte G;
    /// <summary>Blue component (0–255).</summary>
    public readonly byte B;
    /// <summary>Alpha (opacity) component. 0 = fully transparent, 255 = opaque.</summary>
    public readonly byte A;

    /// <summary>Creates a color from component values.</summary>
    /// <param name="r">Red (0–255).</param>
    /// <param name="g">Green (0–255).</param>
    /// <param name="b">Blue (0–255).</param>
    /// <param name="a">Alpha (0 = transparent, 255 = opaque).</param>
    public DvColor(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Fully transparent color (R=G=B=A=0). The result of parsing the string <c>transparent</c>, and also the output value when <see cref="TryParse"/> fails.</summary>
    public static readonly DvColor Transparent = new(0, 0, 0, 0);

    /// <summary>Converts to a SkiaSharp <see cref="SKColor"/>. Use it to set <see cref="SKPaint.Color"/> in custom drawing.</summary>
    /// <returns>An <see cref="SKColor"/> with the same RGBA values.</returns>
    public SKColor ToSK() => new(R, G, B, A);

    /// <summary>
    /// Parses a color string. Accepts <c>#RGB</c> (each digit doubled), <c>#RRGGBB</c> (alpha 255), <c>#RRGGBBAA</c> and <c>transparent</c> (case-insensitive).
    /// Leading and trailing whitespace is ignored.
    /// </summary>
    /// <param name="s">The string to parse. Fails if <see langword="null"/> or empty.</param>
    /// <param name="color">The parsed color on success; <see cref="Transparent"/> on failure.</param>
    /// <returns><see langword="true"/> if the notation is supported and the hex digits are valid. Does not throw.</returns>
    public static bool TryParse(string? s, out DvColor color)
    {
        color = Transparent;
        if (string.IsNullOrWhiteSpace(s))
            return false;

        s = s.Trim();
        if (s.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = Transparent;
            return true;
        }

        if (!s.StartsWith('#'))
            return false;

        var h = s[1..];

        try
        {
            switch (h.Length)
            {
                case 3:
                    color = new DvColor(Dup(h[0]), Dup(h[1]), Dup(h[2]), 255);
                    return true;
                case 6:
                    color = new DvColor(B2(h, 0), B2(h, 2), B2(h, 4), 255);
                    return true;
                case 8:
                    color = new DvColor(B2(h, 0), B2(h, 2), B2(h, 4), B2(h, 6));
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static byte Dup(char c) => byte.Parse($"{c}{c}", NumberStyles.HexNumber);

    private static byte B2(string h, int i) => byte.Parse(h.Substring(i, 2), NumberStyles.HexNumber);
}
