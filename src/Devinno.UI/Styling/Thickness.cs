using System.Globalization;

namespace Devinno.UI.Styling;

/// <summary>
/// Thickness (px) on four sides (top, right, bottom, left). Style <c>padding</c> and <c>margin</c> values are parsed into this type.
/// </summary>
/// <remarks>
/// <para><see cref="TryParse"/> accepts 1, 2, 3 or 4 space-separated numbers (CSS order).
/// <c>8</c> = 8 on all four sides, <c>8 12</c> = top/bottom 8, left/right 12, <c>1 2 3</c> = top 1, left/right 2, bottom 3, <c>1 2 3 4</c> = top, right, bottom, left. A <c>px</c>/<c>dp</c> suffix on each number is ignored.</para>
/// <para>Parsing fails with 5 or more values, comma separators or <c>%</c>. On failure, the style resolver ignores that declaration.</para>
/// </remarks>
/// <example>
/// <code>
/// Thickness.TryParse("8 12", out var p);   // Top=8, Right=12, Bottom=8, Left=12
/// var t = new Thickness(4, 8, 4, 8);       // top, right, bottom, left
/// float w = t.Horizontal;                  // 16
/// </code>
/// </example>
public readonly struct Thickness
{
    /// <summary>Top thickness (px).</summary>
    public readonly float Top;
    /// <summary>Right thickness (px).</summary>
    public readonly float Right;
    /// <summary>Bottom thickness (px).</summary>
    public readonly float Bottom;
    /// <summary>Left thickness (px).</summary>
    public readonly float Left;

    /// <summary>Creates a thickness from four values in CSS order (top, right, bottom, left).</summary>
    /// <param name="top">Top (px).</param>
    /// <param name="right">Right (px).</param>
    /// <param name="bottom">Bottom (px).</param>
    /// <param name="left">Left (px).</param>
    public Thickness(float top, float right, float bottom, float left)
    {
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    /// <summary>A value that is 0 on all four sides. Also the output value when <see cref="TryParse"/> fails.</summary>
    public static readonly Thickness Zero = new(0, 0, 0, 0);

    /// <summary>Returns a thickness with only one side (<c>top</c>, <c>right</c>, <c>bottom</c>, <c>left</c>) changed — used for single-side keys such as <c>padding-left</c>.</summary>
    internal Thickness WithSide(string side, float value) => side switch
    {
        "top" => new(value, Right, Bottom, Left),
        "right" => new(Top, value, Bottom, Left),
        "bottom" => new(Top, Right, value, Left),
        "left" => new(Top, Right, Bottom, value),
        _ => this,
    };

    /// <summary>Horizontal sum (<see cref="Left"/> + <see cref="Right"/>, px). Used to compute inner width.</summary>
    public float Horizontal => Left + Right;

    /// <summary>Vertical sum (<see cref="Top"/> + <see cref="Bottom"/>, px). Used to compute inner height.</summary>
    public float Vertical => Top + Bottom;

    /// <summary>Parses 1, 2, 3 or 4 space-separated numbers as a thickness. Numbers are read with <c>.</c> as the decimal point regardless of culture.</summary>
    /// <param name="s">The string to parse (e.g. <c>8</c>, <c>8 12</c>, <c>4 8 4 8</c>). Fails if <see langword="null"/> or empty.</param>
    /// <param name="t">The parsed thickness on success, or <see cref="Zero"/> on failure.</param>
    /// <returns><see langword="true"/> if there are 1, 2, 3 or 4 values and all are numbers. Does not throw.</returns>
    public static bool TryParse(string? s, out Thickness t)
    {
        t = Zero;
        if (string.IsNullOrWhiteSpace(s))
            return false;

        var p = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var n = new float[p.Length];

        for (var i = 0; i < p.Length; i++)
        {
            if (!float.TryParse(p[i].Replace("px", "").Replace("dp", ""), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out n[i]))
                return false;
        }

        t = p.Length switch
        {
            1 => new Thickness(n[0], n[0], n[0], n[0]),
            2 => new Thickness(n[0], n[1], n[0], n[1]),
            3 => new Thickness(n[0], n[1], n[2], n[1]),
            4 => new Thickness(n[0], n[1], n[2], n[3]),
            _ => Zero
        };

        return p.Length is >= 1 and <= 4;
    }
}
