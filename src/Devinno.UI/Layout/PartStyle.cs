using System.Text.RegularExpressions;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI.Layout;

/// <summary>The overrides of one widget part from <c>::part()</c> rules — only the keys the rules set (others stay <see langword="null"/>).</summary>
internal sealed record PartStyle(float? FontSize, int? FontWeight, SKColor? Color)
{
    private static readonly Regex Key = new(@"(?:^|;)\s*(font-size|font-weight|color)\s*:", RegexOptions.Compiled);

    // 쓸 수 없는 값(font-size: big, 없는 토큰, 색 아닌 글자)은 무시한다 — 기본값(14px·#222)으로 위젯 값을 덮지 않게.
    // 표시 값으로 시작해 파서가 바꾼 키만 쓴다
    private static readonly DvColor Unset = new(1, 2, 3, 4);

    /// <summary>Reads <c>font-size</c>, <c>font-weight</c> and <c>color</c> from resolved declarations; other keys and unusable values are ignored.</summary>
    internal static PartStyle Parse(string declarations)
    {
        var set = Key.Matches(declarations).Select(m => m.Groups[1].Value).ToHashSet();
        var st = StyleParser.ParseInto(new ComputedStyle { FontSize = float.NaN, FontWeight = -1, Color = Unset }, declarations);
        return new PartStyle(
            set.Contains("font-size") && float.IsFinite(st.FontSize) ? st.FontSize : null,
            set.Contains("font-weight") && st.FontWeight > 0 ? st.FontWeight : null,
            set.Contains("color") && !st.Color.Equals(Unset) ? st.Color.ToSK() : null);
    }
}
