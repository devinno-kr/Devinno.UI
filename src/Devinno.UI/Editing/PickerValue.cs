using System.Globalization;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Picker value ↔ binding type (DateTime/DateOnly/TimeOnly, string/SKColor) and display text.</summary>
internal static class PickerValue
{
    private static readonly HashSet<string> Warned = new();

    private static Type? Base(Type? t) => t is null ? null : Nullable.GetUnderlyingType(t) ?? t;

    public static PickerMode DefaultMode(Type? valueType)
        => Base(valueType) == typeof(DateOnly) ? PickerMode.Date
         : Base(valueType) == typeof(TimeOnly) ? PickerMode.Time
         : PickerMode.DateTime;

    public static PickerMode ResolveMode(string? attr, Type? valueType)
    {
        var def = DefaultMode(valueType);
        if (string.IsNullOrEmpty(attr))
            return def;
        PickerMode? want = attr switch { "date" => PickerMode.Date, "time" => PickerMode.Time, "datetime" => PickerMode.DateTime, _ => null };
        // DateTime(또는 타입 모름) 만 모드를 고를 수 있다. DateOnly·TimeOnly 는 자기 모드만.
        if (want is { } m && (def == PickerMode.DateTime || m == def))
            return m;
        Warn($"Devinno.UI: DvDateTimeInput mode='{attr}' does not match the value type ({Base(valueType)?.Name ?? "?"}); using {def}");
        return def;
    }

    private static void Warn(string msg)
    {
        lock (Warned)
        {
            if (Warned.Add(msg))
                System.Diagnostics.Trace.TraceWarning(msg);
        }
    }

    public static bool TryToDateTime(object? value, out DateTime dt)
    {
        switch (value)
        {
            case DateTime d: dt = d; return true;
            case DateOnly d: dt = d.ToDateTime(TimeOnly.MinValue); return true;
            case TimeOnly t: dt = DateTime.Today.Add(t.ToTimeSpan()); return true;
            default: dt = default; return false;
        }
    }

    public static object FromDateTime(DateTime dt, Type valueType)
        => Base(valueType) == typeof(DateOnly) ? DateOnly.FromDateTime(dt)
         : Base(valueType) == typeof(TimeOnly) ? TimeOnly.FromDateTime(dt)
         : dt;

    public static string DefaultFormat(PickerMode mode, bool seconds)
    {
        var time = seconds ? "HH:mm:ss" : "HH:mm";
        return mode switch { PickerMode.Date => "yyyy-MM-dd", PickerMode.Time => time, _ => "yyyy-MM-dd " + time };
    }

    public static string Display(object? value, PickerMode mode, bool seconds, string? format)
    {
        if (!TryToDateTime(value, out var dt))
            return "";
        if (!string.IsNullOrEmpty(format))
        {
            try
            {
                return dt.ToString(format, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                Warn($"Devinno.UI: DvDateTimeInput format='{format}' is not a valid date format; using the default format");
            }
        }

        return dt.ToString(DefaultFormat(mode, seconds), CultureInfo.CurrentCulture);
    }

    public static bool TryToColor(object? value, out SKColor c)
    {
        switch (value)
        {
            case SKColor k: c = k; return true;
            case string s when DvColor.TryParse(s, out var d): c = d.ToSK(); return true;
            default: c = SKColors.White; return false;
        }
    }

    public static object FromColor(SKColor rgb, object? original, Type valueType)
    {
        if (Base(valueType) == typeof(SKColor))
        {
            var a = original is SKColor o ? o.Alpha : (byte)255;
            return new SKColor(rgb.Red, rgb.Green, rgb.Blue, a);
        }

        return Hex(rgb);
    }

    public static string Hex(SKColor c) => $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
}
