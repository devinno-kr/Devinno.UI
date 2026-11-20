using System.Globalization;
using Devinno.UI.Binding;

namespace Devinno.UI.Widgets;

// 커스텀 위젯 속성 읽기 규칙 — 위젯 자신의 속성(DvWidgetContext)과 정의 자식(DvDefinition)이 같은 규칙을 쓴다
internal static class WidgetValues
{
    public static bool Has(Dictionary<string, (object? Value, string? Raw)>? values, string attr) => values?.ContainsKey(attr) == true;

    public static object? Value(Dictionary<string, (object? Value, string? Raw)>? values, string attr) => values?.GetValueOrDefault(attr).Value;

    public static string? Text(Dictionary<string, (object? Value, string? Raw)>? values, string attr) =>
        values is not null && values.TryGetValue(attr, out var v) ? BindingResolver.ToText(v.Value, v.Raw) : null;

    public static double Number(Dictionary<string, (object? Value, string? Raw)>? values, string attr, double fallback) => Value(values, attr) switch
    {
        null => fallback,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        IConvertible c when c is not string => TryConvert(c, fallback),
        var o => double.TryParse(Text(values, attr) ?? o.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : fallback,
    };

    public static bool Flag(Dictionary<string, (object? Value, string? Raw)>? values, string attr, bool fallback) =>
        Value(values, attr) is { } v ? Layout.AttrRules.ParseBool(v) ?? fallback : fallback;

    private static double TryConvert(IConvertible c, double fallback)
    {
        try
        {
            return c.ToDouble(CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            return fallback;
        }
    }
}
