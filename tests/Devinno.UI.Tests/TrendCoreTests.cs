using Devinno.UI.Trends;
using SkiaSharp;
using Xunit;

public class TrendCoreTests
{
    private static readonly DateTime T0 = new(2026, 10, 12, 10, 0, 0);

    [Fact]
    public void Buffer_keeps_time_order_and_drops_oldest_over_capacity()
    {
        var b = new TrendBuffer(capacity: 3);
        for (var i = 0; i < 5; i++)
            b.Add("t", T0.AddSeconds(i), i);

        Assert.Equal(new double[] { 2, 3, 4 }, b.Snapshot("t").Select(p => p.Value));
        Assert.Empty(b.Snapshot("none"));
    }

    [Fact]
    public void Buffer_channels_are_independent_and_snapshot_is_a_copy()
    {
        var b = new TrendBuffer(10);
        b.Add("a", T0, 1);
        b.AddRange("b", new[] { new TrendPoint(T0, 5), new TrendPoint(T0.AddSeconds(1), 6) });
        var snap = b.Snapshot("b");
        b.Add("b", T0.AddSeconds(2), 7);

        Assert.Single(b.Snapshot("a"));
        Assert.Equal(2, snap.Length);
        Assert.Equal(3, b.Snapshot("b").Length);
    }

    [Fact]
    public void Buffer_is_safe_for_a_background_writer()
    {
        var b = new TrendBuffer(500);
        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 5000; i++)
                b.Add("x", T0.AddMilliseconds(i), i);
        });
        while (!writer.IsCompleted)
        {
            var s = b.Snapshot("x");
            Assert.True(s.Length <= 500);
            for (var i = 1; i < s.Length; i++)
                Assert.True(s[i].Time > s[i - 1].Time);
        }

        Assert.Equal(500, b.Snapshot("x").Length);
    }

    [Theory]
    [InlineData(12.3, 87.9, 0, 100, 20)]
    [InlineData(0.21, 0.79, 0.2, 0.8, 0.1)]
    [InlineData(-3, 47, -10, 50, 10)]
    public void Nice_scale_wraps_values_with_round_steps(double min, double max, double lo, double hi, double step)
    {
        var (l, h, s) = TrendScale.Nice(min, max);
        Assert.Equal(lo, l, 9);
        Assert.Equal(hi, h, 9);
        Assert.Equal(step, s, 9);
    }

    [Fact]
    public void Nice_scale_expands_a_flat_series()
    {
        var (lo, hi, _) = TrendScale.Nice(50, 50);
        Assert.True(lo < 50 && hi > 50);
    }

    [Theory]
    [InlineData(60, 15)]
    [InlineData(10, 2)]
    [InlineData(3600, 900)]
    [InlineData(0.2, 0.05)]
    [InlineData(0.02, 0.005)]
    public void Time_step_divides_the_window_into_about_five(double window, double step)
        => Assert.Equal(step, TrendScale.TimeStep(window), 9);

    [Theory]
    [InlineData(300, "10:05")]
    [InlineData(15, "10:05:07")]
    [InlineData(0.05, "07.25")]
    [InlineData(0.5, "07.2")]
    [InlineData(0.002, "07.250")]
    public void Time_label_precision_follows_the_step(double step, string expected)
        => Assert.Equal(expected, TrendScale.TimeLabel(new DateTime(2026, 10, 13, 10, 5, 7, 250), step));

    [Fact]
    public void Smooth_path_never_overshoots_the_data_range()
    {
        var pts = new[] { new SKPoint(0, 50), new SKPoint(10, 10), new SKPoint(20, 90), new SKPoint(30, 85), new SKPoint(40, 20) };
        using var path = TrendPath.Build(pts, smooth: true);
        var b = path.TightBounds;
        Assert.True(b.Top >= 10 - 0.01f && b.Bottom <= 90 + 0.01f, $"bounds={b}");
    }
}
