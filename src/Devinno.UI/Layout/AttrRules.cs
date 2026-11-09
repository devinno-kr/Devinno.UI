using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Interaction;
using Devinno.UI.Markup;

namespace Devinno.UI.Layout;

// 속성 값 해석 규칙 한 곳(3단계 R1 참/거짓·R7 숫자) — 빌드·공정도·그리드·핸들 변환이 모두 이 함수로 읽는다. 태그마다 다시 짜지 않는다.
// 잘못된 값의 경고는 Warnings.Warn(화면마다 같은 문구 한 번).
internal static class AttrRules
{
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

    /// <summary>Reads <paramref name="value"/> with <see cref="ParseBool"/>; any other value is <paramref name="fallback"/> with a warning that names the tag and attribute.</summary>
    public static bool Bool(object? value, bool fallback, string tag, string attr)
    {
        if (ParseBool(value, attr) is { } b)
            return b;
        Warnings.Warn($"Devinno.UI: {tag} {attr}='{BindingResolver.ToText(value, attr)}' is not true or false (true, false, 1, 0); using {(fallback ? "true" : "false")}");
        return fallback;
    }

    /// <summary>Reads attribute <paramref name="attr"/> of the element as a boolean: absent = its default (<see cref="TagSchema"/>); present = <see cref="Bool(object?, bool, string, string)"/> with that default.</summary>
    public static bool Bool(AttrReader r, string attr)
    {
        var fallback = DefaultBool(r.Node.Tag, attr);
        return r.Has(attr) ? Bool(r.Value(attr), fallback, r.Node.Tag, attr) : fallback;
    }

    /// <summary>The common <c>visible</c> attribute: absent = shown; present = <see cref="Bool(object?, bool, string, string)"/> with default <see langword="true"/>. A handle's Visible override is read as the value.</summary>
    public static bool Visible(AttrReader r) => !r.Has("visible") || Bool(r.Value("visible"), true, r.Node.Tag, "visible");

    /// <summary>The default of a boolean attribute in <see cref="TagSchema"/> (<see langword="false"/> when it has none).</summary>
    public static bool DefaultBool(string tag, string attr) => TagSchema.Find(tag, attr)?.Default == "true";

    /// <summary>
    /// The number rule: a number, or text read with the invariant culture (<c>1.5</c>, <c>1e3</c>). Absent, <see langword="null"/> or empty text = <see langword="null"/>, silently;
    /// anything else (text that is not a number, bool, not finite) = <see langword="null"/> with a warning that names the tag and attribute and says <paramref name="instead"/>.
    /// </summary>
    public static double? NumberOrNull(object? value, string tag, string attr, string instead = "ignored")
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

        Warnings.Warn($"Devinno.UI: {tag} {attr}='{text}' is not a number; {instead}");
        return null;
    }

    /// <summary><see cref="NumberOrNull(object?, string, string, string)"/>, with <paramref name="fallback"/> when absent, empty or not a number.</summary>
    public static double Number(object? value, double fallback, string tag, string attr)
        => NumberOrNull(value, tag, attr, "using " + fallback.ToString(CultureInfo.InvariantCulture)) ?? fallback;

    /// <summary>Attribute <paramref name="attr"/> of the element as a number (see <see cref="Number(object?, double, string, string)"/>).</summary>
    public static double Number(AttrReader r, string attr, double fallback) => Number(r.Value(attr), fallback, r.Node.Tag, attr);

    /// <summary>Attribute <paramref name="attr"/> of the element as a number, or <see langword="null"/> (see <see cref="NumberOrNull(object?, string, string, string)"/>).</summary>
    public static double? NumberOrNull(AttrReader r, string attr, string instead = "ignored") => NumberOrNull(r.Value(attr), r.Node.Tag, attr, instead);
}
