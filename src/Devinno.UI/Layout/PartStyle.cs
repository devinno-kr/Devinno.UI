using System.Text.RegularExpressions;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI.Layout;

/// <summary>The overrides of one widget part from <c>::part()</c> rules — only the keys the rules set (others stay <see langword="null"/>).</summary>
internal sealed record PartStyle(float? FontSize, int? FontWeight, SKColor? Color)
{
    private static readonly Regex Key = new(@"(?:^|;)\s*(font-size|font-weight|color)\s*:", RegexOptions.Compiled);

    /// <summary>Reads <c>font-size</c>, <c>font-weight</c> and <c>color</c> from resolved declarations; other keys are ignored.</summary>
    internal static PartStyle Parse(string declarations)
    {
        var set = Key.Matches(declarations).Select(m => m.Groups[1].Value).ToHashSet();
        var st = StyleParser.Parse(declarations);
        return new PartStyle(
            set.Contains("font-size") ? st.FontSize : null,
            set.Contains("font-weight") ? st.FontWeight : null,
            set.Contains("color") ? st.Color.ToSK() : null);
    }
}
