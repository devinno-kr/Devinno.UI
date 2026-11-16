using Devinno.UI;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using Devinno.UI.Trends;
using SkiaSharp;
using Xunit;

// 트렌드 생김새(단계 4): 가로 격자만, 눈금 간격은 높이에 맞춤(≥ 28px), 채움은 0 이 범위 안 + 채널 하나일 때만, 범례 format.
public class TrendLookTests
{
    private static readonly DateTime End = new(2026, 11, 16, 12, 0, 0);

    public sealed class Vm
    {
        public DateTime End => TrendLookTests.End;
        public List<TrendPoint> A { get; set; } = Flat(80);
        public List<TrendPoint> B { get; set; } = Flat(60);
        public static List<TrendPoint> Flat(double v) => Enumerable.Range(0, 61).Select(i => new TrendPoint(TrendLookTests.End.AddSeconds(i - 60), v)).ToList();
    }

    private const int W = 400, H = 220;

    private static SKBitmap Render(string trendAttrs, string channels, Vm? vm = null)
    {
        var markup = "<DvScreen style=\"padding:0\">" +
                     $"<DvTrend end=\"{{End}}\" window=\"60\" {trendAttrs} style=\"width:{W}; height:{H}; padding:0\">{channels}</DvTrend></DvScreen>";
        var s = new DvSurface(markup, vm ?? new Vm(), DvTheme.Light);
        s.Layout(W, H);
        var bmp = new SKBitmap(W, H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        return bmp;
    }

    // 플롯 세로: 위 26, 아래 20 (padding 0)
    private static int Y(double v, double lo, double hi) => (int)(H - 20 - (v - lo) / (hi - lo) * (H - 46));

    [Fact]
    public void Time_ticks_draw_no_vertical_grid_lines()
    {
        using var bmp = Render("y-min=\"0\" y-max=\"100\"", "<DvTrendChannel points=\"{A}\"/>");
        var bg = bmp.GetPixel(W - 2, H / 2);
        var worst = 0;
        for (var x = 60; x < W - 10; x++)
        {
            var hits = 0;
            for (var y = Y(70, 0, 100); y < Y(5, 0, 100); y++)
                if (bmp.GetPixel(x, y) != bg)
                    hits++;
            worst = Math.Max(worst, hits);
        }
        Assert.True(worst < 10, $"a column has {worst} non-background pixels (vertical line)");
    }

    [Theory]
    [InlineData(40f)]
    [InlineData(84f)]
    [InlineData(200f)]
    [InlineData(400f)]
    public void Y_ticks_are_at_least_28px_apart(float plotHeight)
    {
        var (lo, hi, step) = Painter.TrendAxis(null, null, [0, 100], plotHeight);
        Assert.True(plotHeight * step / (hi - lo) >= 28 || (hi - lo) / step <= 1, $"{plotHeight}: step {step} over {lo}..{hi}");
        if (plotHeight >= 400)
            Assert.True((hi - lo) / step >= 4, $"tall plot keeps enough ticks: step {step}");
    }

    [Fact]
    public void Fixed_range_ticks_also_fit_the_height()
    {
        var (lo, hi, step) = Painter.TrendAxis(0, 100, [10, 20], 84f);
        Assert.Equal(0, lo);
        Assert.Equal(100, hi);
        Assert.True(84f * step / (hi - lo) >= 28, $"step {step}");
    }

    [Fact]
    public void Several_channels_draw_no_area_fill()
    {
        using var bmp = Render("y-min=\"0\" y-max=\"100\" area=\"true\"", "<DvTrendChannel points=\"{A}\"/><DvTrendChannel points=\"{B}\"/>");
        Assert.Equal(bmp.GetPixel(W - 2, H / 2), bmp.GetPixel(W / 2, Y(30, 0, 100)));
    }

    [Fact]
    public void Area_fill_is_skipped_when_zero_is_outside_the_range()
    {
        using var bmp = Render("y-min=\"50\" y-max=\"100\" area=\"true\"", "<DvTrendChannel points=\"{A}\"/>");
        Assert.Equal(bmp.GetPixel(W - 2, H / 2), bmp.GetPixel(W / 2, Y(65, 50, 100))); // 격자(10 간격) 사이
    }

    [Fact]
    public void Single_channel_from_zero_keeps_its_area_fill()
    {
        using var bmp = Render("y-min=\"0\" y-max=\"100\" area=\"true\"", "<DvTrendChannel points=\"{A}\"/>");
        Assert.NotEqual(bmp.GetPixel(W - 2, H / 2), bmp.GetPixel(W / 2, Y(70, 0, 100)));
    }

    [Fact]
    public void Channel_format_sets_the_legend_number()
    {
        var s = new DvSurface("<DvScreen><DvTrend end=\"{End}\"><DvTrendChannel points=\"{A}\" unit=\"bar\" format=\"0.0\"/></DvTrend></DvScreen>", new Vm(), DvTheme.Light);
        s.Layout(W, H);
        var ch = Flatten(s.Root!).First(b => b.Node.Tag == "DvTrend").Trend!.Channels[0];
        Assert.Equal("0.0", ch.Format);
        Assert.Equal("80.0", Painter.TrendLegendNumber(80, ch));
        Assert.Equal("80", Painter.TrendLegendNumber(80, Flatten(new DvSurface(
            "<DvScreen><DvTrend end=\"{End}\"><DvTrendChannel points=\"{A}\"/></DvTrend></DvScreen>", new Vm(), DvTheme.Light).Also(x => x.Layout(W, H)).Root!)
            .First(b => b.Node.Tag == "DvTrend").Trend!.Channels[0]));
    }

    private static IEnumerable<Devinno.UI.Layout.LayoutBox> Flatten(Devinno.UI.Layout.LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
}

internal static class TrendLookTestExtensions
{
    public static T Also<T>(this T x, Action<T> a) { a(x); return x; }
}
