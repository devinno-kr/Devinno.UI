using System.Globalization;
using System.Text.RegularExpressions;
using Devinno.UI.Binding;
using Devinno.UI.Interaction;
using Devinno.UI.Markup;
using Devinno.UI.Styling;

namespace Devinno.UI.Layout;

// 속성 값 해석 규칙 한 곳(3단계 R1 참/거짓·R2 색·R3 목록 값·R7 숫자) — 빌드·공정도·그리드·핸들 변환이 모두 이 함수로 읽는다. 태그마다 다시 짜지 않는다.
// 잘못된 값의 경고는 Warnings.Warn — 열쇠 = (태그, 속성, 마크업 원문 raw): 바인딩은 값이 바뀌어도 화면마다 한 번, 리터럴은 값마다 한 번. raw 를 모르면(null) 문구가 열쇠.
internal static class AttrRules
{
    // 경고 중복 열쇠 — raw 가 없으면 null(문구가 열쇠)
    private static string? Key(string tag, string attr, string? raw) => raw is null ? null : string.Concat(tag, "\u001f", attr, "\u001f", raw);

    /// <summary>The markup expression of <paramref name="attr"/> for a warning key: the text as written, or a fixed marker when code overrides it.</summary>
    public static string? RawOf(AttrReader r, string attr) => r.Overridden(attr) ? "\u0001code" : r.Raw(attr);

    /// <summary>
    /// The boolean rule: true = bool <see langword="true"/>, a non-zero number, or text <c>true</c> or a non-zero number (<c>1</c>, <c>-1</c>);
    /// false = bool <see langword="false"/>, 0, text <c>false</c> or <c>0</c>, empty text or <see langword="null"/>. Case and surrounding spaces are ignored.
    /// </summary>
    /// <returns>The value, or <see langword="null"/> for any other value (<c>yes</c>, <c>on</c>, <c>abc</c>).</returns>
    public static bool? ParseBool(object? value) => ParseBool(value, null);

    // attr = VM 형식의 ToString 예외를 알릴 때의 Member
    private static bool? ParseBool(object? value, string? attr)
    {
        switch (value)
        {
            case null:
                return false;
            case bool b:
                return b;
            case string s:
                return ParseBoolText(s);
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return double.IsNaN(d) ? null : d != 0;
            default:
                return ParseBoolText(BindingResolver.ToText(value, attr));
        }
    }

    private static bool? ParseBoolText(string? text)
    {
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s))
            return false;
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !double.IsNaN(d) ? d != 0 : null;
    }

    /// <summary>
    /// Reads <paramref name="value"/> with <see cref="ParseBool"/>; any other value is <paramref name="fallback"/> with a warning that names the tag and attribute
    /// (once per surface for each <paramref name="raw"/> markup expression; <see langword="null"/> = once per message).
    /// </summary>
    public static bool Bool(object? value, bool fallback, string tag, string attr, string? raw = null)
    {
        if (ParseBool(value, attr) is { } b)
            return b;
        Warnings.Warn($"Devinno.UI: {tag} {attr}='{BindingResolver.ToText(value, attr)}' is not true or false (true, false, 1, 0); using {(fallback ? "true" : "false")}", key: Key(tag, attr, raw));
        return fallback;
    }

    /// <summary>Reads attribute <paramref name="attr"/> of the element as a boolean: absent = its default (<see cref="TagSchema"/>); present = <see cref="Bool(object?, bool, string, string, string?)"/> with that default.</summary>
    public static bool Bool(AttrReader r, string attr)
    {
        var fallback = DefaultBool(r.Node.Tag, attr);
        return r.Has(attr) ? Bool(r.Value(attr), fallback, r.Node.Tag, attr, RawOf(r, attr)) : fallback;
    }

    /// <summary>The common <c>visible</c> attribute: absent = shown; present = <see cref="Bool(object?, bool, string, string, string?)"/> with default <see langword="true"/>. A handle's Visible override is read as the value.</summary>
    public static bool Visible(AttrReader r) => !r.Has("visible") || Bool(r.Value("visible"), true, r.Node.Tag, "visible", RawOf(r, "visible"));

    /// <summary>The default of a boolean attribute in <see cref="TagSchema"/> (<see langword="false"/> when it has none).</summary>
    public static bool DefaultBool(string tag, string attr) => TagSchema.Find(tag, attr)?.Default == "true";

    /// <summary>
    /// The number rule: a number, or text read with the invariant culture (<c>1.5</c>, <c>1e3</c>). Absent, <see langword="null"/> or empty text = <see langword="null"/>, silently;
    /// anything else (text that is not a number, bool, not finite) = <see langword="null"/> with a warning that names the tag and attribute and says <paramref name="instead"/>
    /// (once per surface for each <paramref name="raw"/> markup expression; <see langword="null"/> = once per message).
    /// </summary>
    public static double? NumberOrNull(object? value, string tag, string attr, string instead = "ignored", string? raw = null)
    {
        string? text;
        switch (value)
        {
            case null:
                return null;
            case bool:
                text = value.ToString();
                break;
            case IConvertible c when value is not string:
                try
                {
                    var d = c.ToDouble(CultureInfo.InvariantCulture);
                    if (double.IsFinite(d))
                        return d;
                }
                catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
                {
                }
                text = Convert.ToString(value, CultureInfo.InvariantCulture);
                break;
            default:
                text = BindingResolver.ToText(value, attr)?.Trim();
                if (string.IsNullOrEmpty(text))
                    return null;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
                    return parsed;
                break;
        }

        Warnings.Warn($"Devinno.UI: {tag} {attr}='{text}' is not a number; {instead}", key: Key(tag, attr, raw));
        return null;
    }

    /// <summary><see cref="NumberOrNull(object?, string, string, string, string?)"/>, with <paramref name="fallback"/> when absent, empty or not a number.</summary>
    public static double Number(object? value, double fallback, string tag, string attr, string? raw = null)
        => NumberOrNull(value, tag, attr, "using " + fallback.ToString(CultureInfo.InvariantCulture), raw) ?? fallback;

    /// <summary>Attribute <paramref name="attr"/> of the element as a number (see <see cref="Number(object?, double, string, string, string?)"/>).</summary>
    public static double Number(AttrReader r, string attr, double fallback) => Number(r.Value(attr), fallback, r.Node.Tag, attr, RawOf(r, attr));

    /// <summary>Attribute <paramref name="attr"/> of the element as a number, or <see langword="null"/> (see <see cref="NumberOrNull(object?, string, string, string, string?)"/>).</summary>
    public static double? NumberOrNull(AttrReader r, string attr, string instead = "ignored") => NumberOrNull(r.Value(attr), r.Node.Tag, attr, instead, RawOf(r, attr));

    /// <summary>
    /// The value format rule (<c>format</c>): <paramref name="value"/> formatted with the .NET format string <paramref name="format"/> and the invariant culture.
    /// <see langword="null"/> when <paramref name="format"/> is absent or empty, silently, or when it is not a valid format for the value's type, with a warning that names the tag
    /// (the caller then shows its default text).
    /// </summary>
    public static string? FormatOrNull(IFormattable value, string? format, string tag)
    {
        if (string.IsNullOrEmpty(format))
            return null;
        try
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            Warnings.Warn($"Devinno.UI: {tag} format='{format}' is not a .NET format for {value.GetType().Name}; using the default display");
            return null;
        }
    }

    /// <summary>
    /// The range rule: <paramref name="min"/> must be below <paramref name="max"/>; otherwise (<c>min &gt;= max</c>) 0..100 with a warning that names the tag
    /// (once per surface for each <paramref name="raw"/> — the <c>min</c> and <c>max</c> markup expressions; <see langword="null"/> = once per message).
    /// </summary>
    public static (double Min, double Max) Range(double min, double max, string tag, string? raw = null)
    {
        if (min < max)
            return (min, max);
        Warnings.Warn($"Devinno.UI: {tag} min='{min.ToString(CultureInfo.InvariantCulture)}' is not below max='{max.ToString(CultureInfo.InvariantCulture)}'; using 0..100", key: Key(tag, "min/max", raw));
        return (0, 100);
    }

    /// <summary>The range rule for optional bounds: checked only when both are given (see <see cref="Range(double, double, string, string?)"/>); one bound alone is kept.</summary>
    public static (double? Min, double? Max) Range(double? min, double? max, string tag, string? raw = null)
        => min is { } lo && max is { } hi ? Range(lo, hi, tag, raw) : (min, max);

    /// <summary>The <c>min</c> and <c>max</c> markup expressions of the element, as the warning key of <see cref="Range(double, double, string, string?)"/>.</summary>
    public static string RangeRaw(AttrReader r) => RawOf(r, "min") + "\u001f" + RawOf(r, "max");

    // ── 3단계 R2 색 ──

    // 값 전체가 토큰 하나 — token(이름) 또는 별칭 var(--이름)
    private static readonly Regex TokenOnly = new(@"^(?:token\(\s*([A-Za-z0-9_.-]+)\s*\)|var\(\s*--([A-Za-z0-9_.-]+)\s*\))$", RegexOptions.Compiled);

    /// <summary>
    /// The color rule: <c>#RGB</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c> or <c>transparent</c> (<see cref="DvColor.TryParse"/>), or a theme token <c>token(name)</c> (alias <c>var(--name)</c>).
    /// Absent, <see langword="null"/> or empty text = <see langword="null"/>, silently. A token the theme does not have = magenta (<see cref="ThemeResolver.MissingColor"/>) with a warning;
    /// any other value = <see langword="null"/> (the attribute's default color) with a warning that says <paramref name="instead"/>
    /// (warnings once per surface for each <paramref name="raw"/> markup expression; <see langword="null"/> = once per message).
    /// </summary>
    public static DvColor? Color(object? value, DvTheme? theme, string tag, string attr, string instead = "using the default color", string? raw = null)
    {
        var text = BindingResolver.ToText(value, attr)?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (TryColor(text, theme, tag, attr, out var c, raw))
            return c;
        Warnings.Warn($"Devinno.UI: {tag} {attr}='{text}' is not a color (#RRGGBB or token(name)); {instead}", key: Key(tag, attr, raw));
        return null;
    }

    /// <summary>
    /// Reads <paramref name="text"/> with the color rule without the warning for an unparsable value (the caller reports it).
    /// A token the theme does not have still gives magenta and a warning.
    /// </summary>
    public static bool TryColor(string text, DvTheme? theme, string tag, string attr, out DvColor color, string? raw = null)
    {
        text = text.Trim();
        var m = TokenOnly.Match(text);
        if (!m.Success)
            return DvColor.TryParse(text, out color);

        var name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        if ((theme ?? DvTheme.Default).TryGet(name, out var tokenValue))
            return DvColor.TryParse(tokenValue, out color);
        Warnings.Warn($"Devinno.UI: {tag} {attr}='{text}' uses a token the theme does not have; drawing {ThemeResolver.MissingColor} (magenta)", key: Key(tag, attr, raw));
        DvColor.TryParse(ThemeResolver.MissingColor, out color);
        return true;
    }

    // ── 3단계 R3 목록 값 ──

    /// <summary>The listed value that <paramref name="value"/> names (surrounding spaces and case ignored), as written in <paramref name="values"/>; <see langword="null"/> when none matches or the value is empty. No warning.</summary>
    public static string? MatchEnum(object? value, IReadOnlyList<string>? values)
    {
        var text = (value as string ?? BindingResolver.ToText(value, null))?.Trim();
        if (string.IsNullOrEmpty(text) || values is null)
            return null;
        foreach (var v in values)
        {
            if (string.Equals(v, text, StringComparison.OrdinalIgnoreCase))
                return v;
        }

        return null;
    }

    /// <summary>
    /// The enum rule: one of <paramref name="values"/>, surrounding spaces and case ignored (returned as listed). Absent, <see langword="null"/> or empty text = <paramref name="fallback"/>, silently;
    /// any other value = <paramref name="fallback"/> with a warning that names the tag and attribute and lists the values (it says <paramref name="instead"/>, by default "using {fallback}" or "ignored";
    /// once per surface for each <paramref name="raw"/> markup expression, <see langword="null"/> = once per message).
    /// </summary>
    public static string? Enum(object? value, string? fallback, string tag, string attr, IReadOnlyList<string>? values, string? instead = null, bool error = false, string? raw = null)
    {
        var text = BindingResolver.ToText(value, attr)?.Trim();
        if (string.IsNullOrEmpty(text))
            return fallback;
        if (MatchEnum(text, values) is { } match)
            return match;
        var list = values is { Count: > 0 } ? string.Join(", ", values) : "(none)";
        Warnings.Warn($"Devinno.UI: {tag} {attr}='{text}' is not one of {list}; {instead ?? (fallback is null ? "ignored" : "using " + fallback)}", error, Key(tag, attr, raw));
        return fallback;
    }

    /// <summary>
    /// The theme word rule (<c>tone</c>, <c>DvLamp state</c>): a word is valid when the theme (<see cref="DvTheme.Default"/> if <see langword="null"/>) has the token <c>{prefix}-{word}</c>,
    /// so a custom theme can add its own words (<c>state-idle</c>). Surrounding spaces and case are ignored; the word is returned in lower case.
    /// Absent, <see langword="null"/> or empty text = <paramref name="fallback"/>, silently; a word without a token = <paramref name="fallback"/> with a warning that says <paramref name="instead"/>
    /// (once per surface for each <paramref name="raw"/> markup expression; <see langword="null"/> = once per message).
    /// </summary>
    public static string? ThemeWord(object? value, string prefix, DvTheme? theme, string? fallback, string tag, string attr, string? instead = null, string? raw = null)
    {
        var text = BindingResolver.ToText(value, attr)?.Trim();
        if (string.IsNullOrEmpty(text))
            return fallback;
        if ((theme ?? DvTheme.Default).TryGet(prefix + "-" + text, out _))
            return text.ToLowerInvariant();
        Warnings.Warn($"Devinno.UI: {tag} {attr}='{text}' has no theme token {prefix}-{text}; {instead ?? (fallback is null ? "ignored" : "using " + fallback)}", key: Key(tag, attr, raw));
        return fallback;
    }

    /// <summary>Attribute <paramref name="attr"/> of the element with the enum rule, its values and default from <see cref="TagSchema"/>.</summary>
    public static string? Enum(AttrReader r, string attr, string? instead = null, bool error = false)
    {
        var def = TagSchema.Find(r.Node.Tag, attr);
        return Enum(r.Value(attr), def?.Default, r.Node.Tag, attr, def?.Values, instead, error, RawOf(r, attr));
    }
}
