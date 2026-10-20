using System.Globalization;
using SkiaSharp;

namespace Devinno.UI.Text;

internal enum IconStyle
{
    Solid,
    Regular,
    Brands
}

internal readonly record struct IconGlyph(IconStyle Style, SKTypeface Typeface, string Glyph);

/// <summary>
/// Built-in icon font: Font Awesome 6 Free (solid/regular/brands, SIL OFL 1.1, THIRD-PARTY-NOTICES.md).
/// Notation follows the FA web class convention: <c>fa-gear</c> (= solid), <c>fas fa-gear</c>, <c>far fa-star</c>, <c>fab fa-windows</c>.
/// The name table is a resource generated from the fonts (tools/gen_fa_icons.py). Each style's font is loaded on first use.
/// </summary>
internal static class IconFont
{
    private static readonly Lazy<Dictionary<(IconStyle, string), int>> Table = new(LoadTable);

    private static readonly Lazy<SKTypeface>[] Faces =
    {
        new(() => LoadFace("fa-solid.otf")),
        new(() => LoadFace("fa-regular.otf")),
        new(() => LoadFace("fa-brands.otf"))
    };

    public static SKTypeface Face(IconStyle style) => Faces[(int)style].Value;

    public static bool TryResolve(string? spec, out IconGlyph icon)
    {
        icon = default;
        if (string.IsNullOrWhiteSpace(spec))
            return false;

        var parts = spec.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        IconStyle style;
        string name;
        switch (parts.Length)
        {
            case 1:
                (style, name) = (IconStyle.Solid, parts[0]);
                break;
            case 2:
                IconStyle? s = parts[0] switch
                {
                    "fas" or "fa-solid" => IconStyle.Solid,
                    "far" or "fa-regular" => IconStyle.Regular,
                    "fab" or "fa-brands" => IconStyle.Brands,
                    _ => null
                };
                if (s is null)
                    return false;
                (style, name) = (s.Value, parts[1]);
                break;
            default:
                return false;
        }

        if (!name.StartsWith("fa-", StringComparison.Ordinal) ||
            !Table.Value.TryGetValue((style, name[3..]), out var code))
            return false;

        icon = new IconGlyph(style, Face(style), char.ConvertFromUtf32(code));
        return true;
    }

    /// <summary>
    /// Draws the icon with the center of its <b>ink</b> (the actual shape bounds) at <paramref name="center"/>.
    /// Aligning by the em box makes FA glyphs sit high and look ~1px higher than text.
    /// </summary>
    public static void Draw(SKCanvas canvas, IconGlyph icon, SKPoint center, float size, SKPaint paint)
    {
        using var font = new SKFont(icon.Typeface, size);
        font.MeasureText(icon.Glyph, out var ink);
        canvas.DrawText(icon.Glyph, center.X - ink.MidX, center.Y - ink.MidY, SKTextAlign.Left, font, paint);
    }

    private static Stream Resource(string file)
        => typeof(IconFont).Assembly.GetManifestResourceStream("Devinno.UI.Resources." + file)
           ?? throw new InvalidOperationException($"Devinno.UI: 아이콘 리소스 {file} 없음");

    private static SKTypeface LoadFace(string file)
    {
        using var s = Resource(file);
        return SKTypeface.FromStream(s) ?? SKTypeface.Default;
    }

    private static Dictionary<(IconStyle, string), int> LoadTable()
    {
        var map = new Dictionary<(IconStyle, string), int>();
        using var reader = new StreamReader(Resource("fa-icons.txt"));
        while (reader.ReadLine() is { } line)
        {
            var p = line.Split(' ');
            if (p.Length != 3)
                continue;
            var style = p[0] switch { "s" => IconStyle.Solid, "r" => IconStyle.Regular, _ => IconStyle.Brands };
            map[(style, p[1])] = int.Parse(p[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return map;
    }
}
