using SkiaSharp;

namespace Devinno.UI.Text;

internal static class TextFont
{
    private static readonly Dictionary<string, SKTypeface?> Families = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The inherited font-family (ambient) of the element currently being drawn. The Painter sets it per element, so it also applies to text inside widgets.
    /// Assumes a single UI thread.
    /// </summary>
    [ThreadStatic] internal static string? AmbientFamily;

    public static SKFont Create(string text, float size, bool bold, string? family = null)
    {
        return new SKFont(ResolveTypeface(text, family ?? AmbientFamily), size)
        {
            Embolden = bold
        };
    }

    /// <summary>
    /// Returns the first font in the font-family list (comma-separated) that has every character of the string. If none does or some characters are missing,
    /// picks a fallback font using the first character that the base font (the first installed font in the list, or the default if none) cannot draw.
    /// (Basing it on the first non-ASCII character would make the '·' in "MVP · 배너" pick Segoe UI, which has no Korean glyphs, so the Korean would render as □.)
    /// </summary>
    internal static SKTypeface ResolveTypeface(string text, string? family = null)
    {
        SKTypeface? baseFace = null;
        if (!string.IsNullOrWhiteSpace(family))
        {
            foreach (var raw in family.Split(','))
            {
                if (Family(raw.Trim().Trim('\'', '"')) is not { } tf)
                    continue;
                baseFace ??= tf;
                if (HasAll(tf, text))
                    return tf;
            }
        }

        var face = baseFace ?? SKTypeface.Default;
        foreach (var ch in text)
        {
            if (ch > 0x7f && !char.IsSurrogate(ch) && !face.ContainsGlyph(ch))
                return Fallback(face, ch);
        }

        return face;
    }

    // 폴백 폰트 탐색(네이티브 폰트 매니저 질의)은 비싸다 — 레이아웃·페인트마다 같은 글자로 반복되므로 (기준 폰트, 글자)로 캐시
    private static readonly Dictionary<(SKTypeface Face, char Ch), SKTypeface> Fallbacks = new();

    private static SKTypeface Fallback(SKTypeface face, char ch)
    {
        lock (Fallbacks)
        {
            if (!Fallbacks.TryGetValue((face, ch), out var tf))
                Fallbacks[(face, ch)] = tf = SKFontManager.Default.MatchCharacter(ch) ?? face;
            return tf;
        }
    }

    private static bool HasAll(SKTypeface tf, string text)
    {
        foreach (var ch in text)
        {
            if (!char.IsWhiteSpace(ch) && !char.IsSurrogate(ch) && !tf.ContainsGlyph(ch))
                return false;
        }

        return true;
    }

    private static SKTypeface? Family(string name)
    {
        if (name.Length == 0)
            return null;
        lock (Families)
        {
            if (!Families.TryGetValue(name, out var tf))
                Families[name] = tf = SKFontManager.Default.MatchFamily(name);
            return tf;
        }
    }
}
