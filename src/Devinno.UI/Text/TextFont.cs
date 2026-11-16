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

    /// <summary>
    /// A font for <paramref name="text"/> at <paramref name="weight"/> (100–900). The family is chosen as in <see cref="ResolveTypeface"/>,
    /// then its face nearest to the weight is used; synthetic bold only when the request is 600 or more and the family has no bold face.
    /// </summary>
    public static SKFont Create(string text, float size, int weight, string? family = null)
        => ForFace(ResolveTypeface(text, family ?? AmbientFamily), size, weight);

    /// <summary>A font from an already chosen family face, at that family's face nearest to <paramref name="weight"/>.</summary>
    internal static SKFont ForFace(SKTypeface familyFace, float size, int weight)
    {
        var face = Weighted(familyFace, weight);
        // 서브픽셀 위치 + 선형 폭: 글자 시작점을 정수 픽셀로 반올림하지 않는다(안 하면 "Latch o n" 처럼 간격이 들쭉날쭉)
        return new SKFont(face, size)
        {
            Embolden = NeedsEmbolden(weight, face.FontWeight),
            Subpixel = true,
            LinearMetrics = true
        };
    }

    /// <summary>Synthetic bold is needed when 600 or more is asked for and the chosen face is at least 150 lighter.</summary>
    internal static bool NeedsEmbolden(int requested, int faceWeight) => requested >= 600 && faceWeight <= requested - 150;

    // (가족 면, 굵기) → 그 가족에서 가장 가까운 굵기 면. 네이티브 글꼴 질의는 비싸므로 캐시
    private static readonly Dictionary<(SKTypeface Face, int Weight), SKTypeface> WeightFaces = new();

    private static SKTypeface Weighted(SKTypeface face, int weight)
    {
        if (face.FontWeight == weight)
            return face;
        lock (WeightFaces)
        {
            if (!WeightFaces.TryGetValue((face, weight), out var tf))
            {
                var match = SKFontManager.Default.MatchFamily(face.FamilyName, new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, SKFontStyleSlant.Upright));
                if (match is not null && match.FamilyName == face.FamilyName)
                    tf = match;
                else
                {
                    match?.Dispose(); // 다른 가족을 돌려준 경우 — 원래 면을 쓴다
                    tf = face;
                }
                WeightFaces[(face, weight)] = tf;
            }
            return tf;
        }
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
