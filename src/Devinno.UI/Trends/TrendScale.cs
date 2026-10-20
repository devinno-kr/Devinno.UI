using SkiaSharp;

namespace Devinno.UI.Trends;

/// <summary>
/// Axis tick calculation for trend charts. Computes a "nice" range and interval for the value axis (<see cref="Nice"/>), and the tick interval (<see cref="TimeStep"/>) and labels (<see cref="TimeLabel"/>) for the time axis.
/// </summary>
/// <remarks>
/// This is the calculation <c>DvTrend</c> uses. App code usually does not call it directly, but when painting charts, bar graphs or instruments in a custom widget
/// you can reuse it to get the same tick rules. Build the list of tick values with <see cref="Widgets.ScaleTicks.Values"/>.
/// </remarks>
/// <example>
/// <code>
/// var (lo, hi, step) = TrendScale.Nice(3.7, 96.2);          // (0, 100, 20)
/// var ticks = ScaleTicks.Values(lo, hi, step);              // 0, 20, 40, 60, 80, 100
/// var timeStep = TrendScale.TimeStep(120);                  // 30 (seconds)
/// var label = TrendScale.TimeLabel(DateTime.Now, timeStep); // "HH:mm:ss"
/// </code>
/// </example>
public static class TrendScale
{
    /// <summary>
    /// Computes a "nice" axis range and tick interval that encloses the value range. The interval is one of 1, 2 or 5 × 10ⁿ, and the range is rounded outward to multiples of the interval.
    /// </summary>
    /// <param name="min">Data minimum.</param>
    /// <param name="max">Data maximum. If less than <paramref name="min"/>, the two are swapped.</param>
    /// <param name="targetTicks">Desired number of tick intervals (default 5). The interval is chosen to give roughly this many. Values under 1 are treated as 1.</param>
    /// <returns>
    /// <c>Lo</c>/<c>Hi</c> = the axis range rounded to multiples of the interval, <c>Step</c> = the tick interval. If the two values are effectively equal (difference under 1e-12), the range is widened up and down by 10% of the absolute value (±1 near 0) before calculating.
    /// Returns (0, 1, 0.2) if <paramref name="min"/> or <paramref name="max"/> is NaN or infinite.
    /// </returns>
    public static (double Lo, double Hi, double Step) Nice(double min, double max, int targetTicks = 5)
    {
        if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max))
            return (0, 1, 0.2);
        if (max < min)
            (min, max) = (max, min);
        if (max - min < 1e-12)
        {
            var pad = Math.Abs(min) > 1e-12 ? Math.Abs(min) * 0.1 : 1;
            min -= pad;
            max += pad;
        }

        var rough = (max - min) / Math.Max(1, targetTicks);
        var mag = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var norm = rough / mag;
        var step = (norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10) * mag;
        return (Math.Floor(min / step + 1e-9) * step, Math.Ceiling(max / step - 1e-9) * step, step);
    }

    private static readonly double[] TimeSteps =
    {
        0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
        1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 21600, 43200, 86400
    };

    /// <summary>
    /// Picks a time axis tick interval (seconds) that divides the time window into roughly <paramref name="targetTicks"/> intervals. Returns the smallest human-friendly interval,
    /// such as 1ms, 2ms, 5ms … 1s, 2s, 5s, 10s, 15s, 30s, 1min … 1 day, that is at least "window ÷ interval count". Also handles millisecond windows (e.g. 0.2 s).
    /// </summary>
    /// <param name="windowSeconds">Length of the visible time window (seconds).</param>
    /// <param name="targetTicks">Desired number of tick intervals (default 5). Values under 1 are treated as 1.</param>
    /// <returns>The tick interval (seconds). Minimum 0.001, maximum 86400 (1 day).</returns>
    public static double TimeStep(double windowSeconds, int targetTicks = 5)
    {
        var want = windowSeconds / Math.Max(1, targetTicks);
        foreach (var s in TimeSteps)
            if (s >= want - 1e-12)
                return s;
        return TimeSteps[^1];
    }

    /// <summary>
    /// Builds a time axis tick label: <c>HH:mm</c> if the interval is 1 minute or more, <c>HH:mm:ss</c> if 1 second or more, and <c>초.소수</c> (seconds.fraction) if under 1 second
    /// (1–3 fraction digits, as many as the interval needs; e.g. interval 0.05 s → <c>"07.25"</c>). The format is the same regardless of culture.
    /// </summary>
    /// <param name="t">The tick time to label.</param>
    /// <param name="step">Tick interval (seconds). Usually the result of <see cref="TimeStep"/>.</param>
    /// <returns>The label string.</returns>
    public static string TimeLabel(DateTime t, double step)
    {
        if (step >= 60)
            return t.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        if (step >= 1)
            return t.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

        var digits = Math.Clamp((int)Math.Ceiling(-Math.Log10(step) - 1e-9), 1, 3);
        return t.ToString("ss", System.Globalization.CultureInfo.InvariantCulture) + "." +
               t.ToString("fff", System.Globalization.CultureInfo.InvariantCulture)[..digits];
    }
}

/// <summary>
/// Builds the path of a trend line. Smooth curves use monotone cubic interpolation (Fritsch–Carlson), so the curve never overshoots
/// above or below the value range of two neighboring points. As a result, values that cannot go below 0 (flow, level, etc.) never look negative because of the curve.
/// </summary>
/// <remarks>
/// This is the calculation <c>DvTrend</c> uses when <c>smooth="true"</c>. Custom widgets can also use it as is to paint time series or sparklines by passing a list of points in screen coordinates.
/// </remarks>
/// <example>
/// <code>
/// var pts = new List&lt;SKPoint&gt; { new(0, 80), new(40, 20), new(80, 20), new(120, 60) };
/// using var path = TrendPath.Build(pts, smooth: true);
/// using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.DeepSkyBlue };
/// canvas.DrawPath(path, paint);
/// </code>
/// </example>
public static class TrendPath
{
    /// <summary>Builds a path that connects the points.</summary>
    /// <param name="pts">Points in screen coordinates. They must be in increasing X order (segments where X stays the same or decreases are treated as slope 0).</param>
    /// <param name="smooth"><see langword="true"/> for a monotone cubic curve, <see langword="false"/> for a polyline. With 2 points or fewer, the result is always straight lines.</param>
    /// <returns>A new <see cref="SKPath"/>, which the caller must Dispose. An empty path if there are no points; a path with only <c>MoveTo</c> if there is one.</returns>
    public static SKPath Build(IReadOnlyList<SKPoint> pts, bool smooth)
    {
        var path = new SKPath();
        if (pts.Count == 0)
            return path;

        path.MoveTo(pts[0]);
        if (pts.Count == 1)
            return path;

        if (!smooth || pts.Count == 2)
        {
            for (var i = 1; i < pts.Count; i++)
                path.LineTo(pts[i]);
            return path;
        }

        var n = pts.Count;
        var d = new float[n - 1]; // 구간 기울기
        for (var i = 0; i < n - 1; i++)
        {
            var dx = pts[i + 1].X - pts[i].X;
            d[i] = dx <= 0 ? 0 : (pts[i + 1].Y - pts[i].Y) / dx;
        }

        var m = new float[n]; // 점 접선
        m[0] = d[0];
        m[n - 1] = d[n - 2];
        for (var i = 1; i < n - 1; i++)
            m[i] = d[i - 1] * d[i] <= 0 ? 0 : (d[i - 1] + d[i]) / 2f;

        for (var i = 0; i < n - 1; i++)
        {
            if (d[i] == 0)
            {
                m[i] = 0;
                m[i + 1] = 0;
                continue;
            }

            var a = m[i] / d[i];
            var b = m[i + 1] / d[i];
            var s = a * a + b * b;
            if (s > 9f)
            {
                var t = 3f / MathF.Sqrt(s);
                m[i] = t * a * d[i];
                m[i + 1] = t * b * d[i];
            }
        }

        for (var i = 0; i < n - 1; i++)
        {
            var p0 = pts[i];
            var p1 = pts[i + 1];
            var h = (p1.X - p0.X) / 3f;
            path.CubicTo(p0.X + h, p0.Y + m[i] * h, p1.X - h, p1.Y - m[i + 1] * h, p1.X, p1.Y);
        }

        return path;
    }
}
