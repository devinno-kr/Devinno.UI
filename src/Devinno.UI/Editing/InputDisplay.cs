using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>String DvInput shows normally (when not editing) — Painter and tests use the same computation.</summary>
internal static class InputDisplay
{
    /// <summary>Width of the −/+ buttons of <c>show-stepper</c> (at both ends of the input box).</summary>
    public const float StepperWidth = 40f;

    /// <summary>Whether x inside the box (in the box Rect's coordinates) is on the − button (-1), the + button (+1), or elsewhere (0).</summary>
    public static int StepperZone(LayoutBox box, float x)
    {
        if (!box.InputStepper)
            return 0;
        if (x < box.Rect.Left + StepperWidth)
            return -1;
        if (x > box.Rect.Right - StepperWidth)
            return 1;
        return 0;
    }

    /// <summary>Text of the current value moved by dir (±1) × step (within min–max, rounded to step's decimal places). If there is no current value, starts from min or 0.</summary>
    public static string Stepped(LayoutBox box, object? value, int dir)
    {
        var current = value is null ? (box.InputMin ?? 0d)
            : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        var text = ((decimal)box.InputStep).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        var decimals = dot < 0 ? 0 : Math.Min(6, text.Length - dot - 1);
        var next = Math.Round(current + dir * box.InputStep, decimals, MidpointRounding.AwayFromZero);
        if (box.InputMin is { } mn && next < mn) next = mn;
        if (box.InputMax is { } mx && next > mx) next = mx;
        return next.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public const char PasswordDot = '•';

    /// <summary>Display string. Dots for password, <c>format</c> (InvariantCulture) for numeric, otherwise the value as-is.</summary>
    public static string Text(LayoutBox box)
    {
        var raw = Raw(box.InputValue, box.InputKind == DvEditKind.Numeric ? box.InputFormat : null);
        return box.InputKind == DvEditKind.Password ? new string(PasswordDot, raw.Length) : raw;
    }

    /// <summary>Edit start value — the raw value without format (InvariantCulture). Password starts with an empty value.</summary>
    public static string EditText(LayoutBox box)
        => box.InputKind == DvEditKind.Password ? "" : Raw(box.InputValue, null);

    public static bool ShowsPlaceholder(LayoutBox box)
        => !string.IsNullOrEmpty(box.InputPlaceholder) && Text(box).Length == 0;

    /// <summary>Editable if it is two-way bound and not disabled or read-only (<c>readonly</c>) (one-way = read-only).</summary>
    public static bool IsEditable(LayoutBox box)
        => !box.Disabled && !box.ReadOnly && box.ValueBindExpr is { } e && BindingResolver.IsTwoWay(e);

    internal static string Raw(object? value, string? format, string tag = "DvInput")
    {
        try
        {
            return value switch
            {
                null => "",
                string s => s,
                IFormattable f => f.ToString(string.IsNullOrEmpty(format) ? null : format, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
        }
        catch (Exception ex)
        {
            // VM 값 형식의 ToString 예외 = 빈 값(형식 문자열 오류 FormatException 도 같음)
            Interaction.HandlerErrors.ReportRead(ex, "format", null, tag);
            return "";
        }
    }

    /// <summary>
    /// Line breaking for multi-line display — splits first at newline characters, then breaks at the last space when the width is exceeded (per character if there is none).
    /// Shared implementation with label wrapping (<see cref="global::Devinno.UI.Text.TextWrap.Wrap"/>).
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, SKFont font, float width) => global::Devinno.UI.Text.TextWrap.Wrap(text, font, width);
}
