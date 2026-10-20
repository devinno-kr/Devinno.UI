using System.Globalization;

namespace Devinno.UI.Grid;

internal enum GridWidthKind { Px, Percent, Star }

/// <summary>Column width: <c>120</c>/<c>120px</c> (fixed), <c>20%</c> (fraction of the grid width), or <c>*</c>/<c>2*</c> (weighted share of the remaining width). An invalid value means <c>*</c>.</summary>
internal readonly record struct GridWidth(GridWidthKind Kind, float Value)
{
    public static readonly GridWidth Star = new(GridWidthKind.Star, 1f);

    public static GridWidth Parse(string? raw)
    {
        var s = raw?.Trim();
        if (string.IsNullOrEmpty(s))
            return Star;

        if (s.EndsWith('*'))
        {
            var w = s[..^1].Trim();
            return w.Length == 0 ? Star : Num(w) is > 0 and var k ? new(GridWidthKind.Star, k) : Star;
        }

        if (s.EndsWith('%'))
            return Num(s[..^1]) is > 0 and var pc ? new(GridWidthKind.Percent, pc) : Star;

        if (s.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            s = s[..^2];
        return Num(s) is >= 0 and var px ? new(GridWidthKind.Px, px) : Star;
    }

    private static float Num(string s)
        => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : -1f;
}
