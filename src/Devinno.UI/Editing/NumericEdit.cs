using System.Globalization;
using Devinno.UI.Layout;
using Devinno.UI.Localization;

namespace Devinno.UI;

/// <summary>
/// Numeric editing: decides integer/floating-point and signedness from the bound property type, and parses the text with InvariantCulture into a value of that type.
/// Out-of-range values are rejected (not clamped).
/// </summary>
internal static class NumericEdit
{
    public static DvNumericSpec Spec(Type? bound, LayoutBox box) => Spec(bound, box.InputMin, box.InputMax, box.Unit);

    public static DvNumericSpec Spec(Type? bound, double? min, double? max, string? unit)
    {
        var t = bound is null ? null : Nullable.GetUnderlyingType(bound) ?? bound;
        var isInteger = t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(sbyte) ||
                        t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(byte);
        var isSigned = !(t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(byte));
        return new DvNumericSpec(isInteger, isSigned, min, max, unit);
    }

    /// <param name="text">The entered text (leading and trailing whitespace is ignored).</param>
    /// <param name="bound">The bound property type (for Nullable, the underlying type is used).</param>
    /// <param name="spec">Integer/sign/range rules.</param>
    /// <param name="strings">Translations of the rejection messages (built-in English or Korean text if none).</param>
    /// <param name="value">The value to write (underlying type; trimmed text for a string property).</param>
    /// <param name="number">A double for validation and the commit gate.</param>
    /// <param name="error">The rejection reason (null on success).</param>
    public static bool TryParse(string text, Type? bound, DvNumericSpec spec, DvStrings? strings,
        out object? value, out double number, out string? error)
    {
        value = null;
        number = 0;
        error = null;
        var s = text.Trim();
        var t = bound is null ? null : Nullable.GetUnderlyingType(bound) ?? bound;

        if (s.Length == 0)
            return Fail(DvStrings.Builtin(strings, "dv.input.required"), out error);

        if (spec.IsInteger)
        {
            if (!spec.IsSigned && s.StartsWith('-'))
                return Fail(DvStrings.Builtin(strings, "dv.input.unsigned"), out error);
            if (!decimal.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
                return Fail(DvStrings.Builtin(strings, "dv.input.integer"), out error);
            try
            {
                value = Convert.ChangeType(whole, t!, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                return Fail(RangeText(spec, strings, t), out error);
            }

            number = (double)whole;
        }
        else if (t == typeof(decimal))
        {
            if (!decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec))
                return Fail(DvStrings.Builtin(strings, "dv.input.number"), out error);
            value = dec;
            number = (double)dec;
        }
        else
        {
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || !double.IsFinite(d))
                return Fail(DvStrings.Builtin(strings, "dv.input.number"), out error);
            number = d;
            value = t == typeof(float) ? (float)d : t == typeof(double) ? d : (object)s; // 그 외(문자열 등) = 텍스트 그대로
        }

        if ((spec.Min is { } min && number < min) || (spec.Max is { } max && number > max))
            return Fail(RangeText(spec, strings, t), out error);

        return true;
    }

    /// <summary>Whether the number equals the current value (a difference only in text notation, such as "07" vs 7, is not written).</summary>
    public static bool SameNumber(object? current, double number)
    {
        try
        {
            return current is not null && Convert.ToDouble(current, CultureInfo.InvariantCulture) == number;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private static string RangeText(DvNumericSpec spec, DvStrings? strings, Type? t)
    {
        var lo = spec.Min?.ToString(CultureInfo.InvariantCulture) ?? "";
        var hi = spec.Max?.ToString(CultureInfo.InvariantCulture) ?? "";
        if (lo.Length == 0 && hi.Length == 0)
            return DvStrings.Builtin(strings, "dv.input.overflow");
        return string.Format(DvStrings.Builtin(strings, "dv.input.range"), lo, hi);
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }
}
