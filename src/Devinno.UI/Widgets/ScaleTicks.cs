namespace Devinno.UI.Widgets;

/// <summary>
/// Computes the list of instrument scale tick values. Ticks start at <c>min</c> and step by <c>step</c> only up to <c>max</c>, so there are no out-of-range ticks and no division by zero.
/// </summary>
/// <remarks>
/// Used by <c>ticks</c>/<c>minor-ticks</c> of <c>DvGauge</c> and by the value-axis ticks of <c>DvTrend</c>.
/// It can be used as is to find tick positions when drawing custom instruments or charts; get a nice-looking step first with <see cref="Trends.TrendScale.Nice"/>.
/// </remarks>
/// <example>
/// <code>
/// ScaleTicks.Values(0, 95, 10);   // 0, 10, 20, …, 90  (100 is excluded because it is out of range)
/// ScaleTicks.Values(3, 15, 5);    // 3, 8, 13
/// ScaleTicks.Values(0, 100, 0);   // empty list
/// </code>
/// </example>
public static class ScaleTicks
{
    /// <summary>Creates the list of tick values.</summary>
    /// <param name="min">The first tick value (start of the range).</param>
    /// <param name="max">End of the range. No tick beyond this value is created (a floating-point tolerance of 1e-9 is allowed).</param>
    /// <param name="step">Tick interval.</param>
    /// <returns>
    /// The values <c>min + k × step</c> (rounded to 9 decimal places). Empty if <paramref name="step"/> is 0 or less or <paramref name="max"/> is less than <paramref name="min"/>.
    /// Even if the step is very dense, at most 1001 values are created.
    /// </returns>
    public static IReadOnlyList<double> Values(double min, double max, double step)
    {
        var list = new List<double>();
        if (step <= 0 || max < min)
            return list;

        var count = (int)Math.Floor((max - min) / step + 1e-9);
        for (var k = 0; k <= count && k <= 1000; k++)
            list.Add(Math.Round(min + k * step, 9));
        return list;
    }
}
