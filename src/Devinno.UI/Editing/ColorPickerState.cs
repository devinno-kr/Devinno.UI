using System.Globalization;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Temporary state of the color popup. Holds H, S and V and computes RGB (so the bar and square positions are kept even for gray and black). No alpha.</summary>
internal sealed class ColorPickerState
{
    public ColorPickerState(SKColor initial)
    {
        Initial = new SKColor(initial.Red, initial.Green, initial.Blue);
        (H, S, V) = ToHsv(Initial);
    }

    public SKColor Initial { get; }
    public double H { get; private set; }
    public double S { get; private set; }
    public double V { get; private set; }
    public SKColor Rgb => FromHsv(H, S, V);

    public void SetSV(double s, double v) => (S, V) = (Math.Clamp(s, 0, 1), Math.Clamp(v, 0, 1));
    public void SetHue(double h) => H = Math.Clamp(h, 0, 360);

    public bool TrySetHex(string text)
    {
        var t = text; // 공백 허용 안 함 — # 뒤 6자리 hex 만
        if (t.StartsWith('#'))
            t = t[1..];
        if (t.Length != 6 || !uint.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var n))
            return false;

        var (h, s, v) = ToHsv(new SKColor((byte)(n >> 16), (byte)(n >> 8), (byte)n));
        if (v > 0 && s > 0)
            H = h;
        if (v > 0)
            S = s;
        V = v;
        return true;
    }

    public static (double h, double s, double v) ToHsv(SKColor c)
    {
        double r = c.Red / 255.0, g = c.Green / 255.0, b = c.Blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }

        return (h, max == 0 ? 0 : d / max, max);
    }

    public static SKColor FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var hp = (h % 360) / 60.0;
        var x = c * (1 - Math.Abs(hp % 2 - 1));
        var (r, g, b) = hp switch
        {
            < 1 => (c, x, 0d),
            < 2 => (x, c, 0d),
            < 3 => (0d, c, x),
            < 4 => (0d, x, c),
            < 5 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        var m = v - c;
        static byte B(double f) => (byte)Math.Round(Math.Clamp(f, 0, 1) * 255);
        return new SKColor(B(r + m), B(g + m), B(b + m));
    }
}
