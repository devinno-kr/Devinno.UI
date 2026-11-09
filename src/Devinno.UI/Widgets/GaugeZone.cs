using System.Globalization;
using Devinno.UI.Styling;

namespace Devinno.UI.Widgets;

/// <summary>
/// One alarm zone of an instrument. Shows the value range <see cref="From"/>–<see cref="To"/> in <see cref="Color"/>.
/// The <c>alarm-zones</c> attribute of <c>DvGauge</c>, <c>DvRing</c>, <c>DvProgress</c> and <c>DvTank</c> is parsed into a list of this type.
/// </summary>
/// <remarks>
/// App code normally just writes it in markup, like <c>alarm-zones="0:60:#16a34a 60:85:#eab308 85:100:#dc2626"</c>.
/// In markup, the whole attribute can be bound (<c>alarm-zones="{Zones}"</c>), and using <c>token(이름)</c> (token name) for a zone color turns it into the screen's theme color (the layout engine replaces it before parsing). In markup, a zone that cannot be read (format, number, order or color) is dropped with a warning and the other zones stay; an unknown token draws that zone magenta.
/// When building a custom instrument widget that should accept the same string format, use <see cref="ParseZones"/> as is.
/// <see cref="ParseZones"/> itself does not know about tokens, so if tokens can appear, pass the string through <see cref="ThemeResolver.ResolveVars"/> first.
/// </remarks>
/// <param name="From">The zone start value (same unit as the instrument value). Must be less than <see cref="To"/>.</param>
/// <param name="To">The zone end value.</param>
/// <param name="Color">The zone color.</param>
public sealed record GaugeZone(float From, float To, DvColor Color)
{
    /// <summary>
    /// Parses a string in the <c>"from:to:#color from:to:#color …"</c> format into a list of zones. Zones are separated by spaces, and numbers are read with a <c>.</c> decimal point regardless of culture.
    /// Each zone must satisfy <c>from &lt; to</c>, and zones must be in ascending order of <c>from</c> (overlaps are not checked).
    /// </summary>
    /// <param name="spec">The string to parse. Example: <c>"0:60:#16a34a 60:85:#eab308 85:100:#dc2626"</c>.</param>
    /// <returns>The parsed zones (in input order). An empty list if <paramref name="spec"/> is <see langword="null"/>, empty or whitespace.</returns>
    /// <exception cref="InvalidOperationException">When a zone does not have the three parts <c>from:to:color</c>, <c>from</c> or <c>to</c> is not a number, <c>from &gt;= to</c>, the color cannot be parsed, or <c>from</c> is less than that of the previous zone. All format errors are this single exception.</exception>
    public static List<GaugeZone> ParseZones(string? spec)
        => Parse(spec, s => DvColor.TryParse(s, out var c) ? c : null, (_, why) => throw new InvalidOperationException(why));

    // 형식 하나, 오류 처리 둘 — 공개 ParseZones 는 첫 오류에서 예외, 레이아웃 엔진(3단계 R2)은 잘못된 구간만 버리고 알린다(bad(구간, 이유)).
    // color = 구간 색 글자 → 색(못 읽으면 null). 토큰 안의 공백(token( 이름 ))은 구간을 나누지 않게 먼저 붙인다.
    internal static List<GaugeZone> Parse(string? spec, Func<string, DvColor?> color, Action<string, string> bad)
    {
        var list = new List<GaugeZone>();
        if (string.IsNullOrWhiteSpace(spec))
            return list;

        spec = TokenSpaces.Replace(spec, m => m.Value.Replace(" ", ""));
        foreach (var token in spec.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(':');
            if (parts.Length != 3)
            {
                bad(token, $"Invalid alarm-zone token '{token}' (expected from:to:#color).");
                continue;
            }

            if (!float.TryParse(parts[0], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var from) ||
                !float.TryParse(parts[1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var to))
            {
                bad(token, $"Invalid alarm-zone number in '{token}' (expected from:to:#color).");
                continue;
            }

            if (from >= to)
            {
                bad(token, $"alarm-zone from({from}) must be < to({to}).");
                continue;
            }

            if (color(parts[2]) is not { } c)
            {
                bad(token, $"Invalid alarm-zone color '{parts[2]}'.");
                continue;
            }

            if (list.Count > 0 && from < list[^1].From)
            {
                bad(token, "alarm-zones must be in ascending 'from' order.");
                continue;
            }

            list.Add(new GaugeZone(from, to, c));
        }

        return list;
    }

    private static readonly System.Text.RegularExpressions.Regex TokenSpaces = new(@"\((?:[^()]*)\)", System.Text.RegularExpressions.RegexOptions.Compiled);
}
