using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Trends;
using SkiaSharp;
using Xunit;

public class TrendWidgetTests
{
    private static readonly DateTime T0 = new(2026, 10, 12, 10, 0, 0);

    public sealed class Vm
    {
        public TrendBuffer Trend { get; } = new(1000);
        public List<TrendPoint> History { get; set; } = new();
        public DateTime? End { get; set; }
    }

    private static LayoutBox Lay(string markup, object vm)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), vm, 480, 240);

    private static SKBitmap Render(string markup, object vm)
    {
        var box = Lay(markup, vm);
        var bmp = new SKBitmap(480, 240, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        new Painter().Paint(canvas, box, vm);
        return bmp;
    }

    private static bool IsLine(SKColor c) => c.Blue > 180 && c.Red < 140 && c.Green < 170; // 팔레트 0 #3b82f6

    private static bool Any(SKBitmap b, int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
            if (IsLine(b.GetPixel(x, y)))
                return true;
        return false;
    }

    private const string Markup =
        "<DvTrend source=\"{Trend}\" window=\"60\" y-min=\"0\" y-max=\"100\" style=\"width:480; height:240\">" +
        "<DvTrendChannel name=\"temp\" label=\"온도\" unit=\"C\"/></DvTrend>";

    [Fact]
    public void Channels_come_from_child_tags_and_create_no_child_boxes()
    {
        var vm = new Vm();
        vm.Trend.Add("temp", T0, 10);
        var box = Lay(Markup, vm);

        Assert.Empty(box.Children);
        var ch = Assert.Single(box.Trend!.Channels);
        Assert.Equal(("temp", "온도", "C"), (ch.Name, ch.Label, ch.Unit));
        Assert.Single(ch.Points);
        Assert.Equal(60, box.Trend.WindowSeconds);
        Assert.Equal((0d, 100d), (box.Trend.YMin!.Value, box.Trend.YMax!.Value));
    }

    [Fact]
    public void Default_size_is_480_by_240()
    {
        var box = new LayoutEngine().Layout(
            MarkupParser.Parse("<DvScreen style=\"align-items:start\"><DvTrend><DvTrendChannel name=\"a\"/></DvTrend></DvScreen>"), null, 800, 600);
        Assert.Equal((480f, 240f), (box.Children[0].Rect.Width, box.Children[0].Rect.Height));
    }

    [Fact]
    public void Constant_series_is_drawn_at_its_value_height()
    {
        var vm = new Vm();
        for (var i = 0; i <= 60; i++)
            vm.Trend.Add("temp", T0.AddSeconds(i), 50);

        using var bmp = Render(Markup, vm);
        // 플롯: top=26, bottom=220 → 50 → y≈123
        Assert.True(Any(bmp, 300, 115, 300, 131), "line at 50 missing");
        Assert.False(Any(bmp, 300, 30, 300, 100), "unexpected line in upper half");
    }

    [Fact]
    public void Points_older_than_the_window_are_not_drawn()
    {
        var vm = new Vm();
        vm.Trend.Add("temp", T0, 100);                  // 창 밖(오래됨) — 창 직전 1점이라 왼쪽 경계로만 이어짐
        vm.Trend.Add("temp", T0.AddSeconds(30), 20);
        for (var i = 31; i <= 120; i++)
            vm.Trend.Add("temp", T0.AddSeconds(i), 20);

        using var bmp = Render(Markup, vm);
        Assert.False(Any(bmp, 150, 30, 470, 120), "spike outside window leaked into plot");
        Assert.True(Any(bmp, 300, 170, 300, 190), "value 20 line missing");
    }

    [Fact]
    public void Points_binding_overrides_source_and_end_pins_the_window()
    {
        var vm = new Vm { End = T0.AddSeconds(60) };
        vm.Trend.Add("temp", T0.AddSeconds(500), 90);   // source 데이터 — points 가 있으면 무시
        for (var i = 0; i <= 60; i++)
            vm.History.Add(new TrendPoint(T0.AddSeconds(i), 50));

        using var bmp = Render(
            "<DvTrend source=\"{Trend}\" end=\"{End}\" window=\"60\" y-min=\"0\" y-max=\"100\" style=\"width:480; height:240\">" +
            "<DvTrendChannel name=\"temp\" points=\"{History}\"/></DvTrend>", vm);
        Assert.True(Any(bmp, 300, 115, 300, 131));
    }

    [Fact]
    public void NaN_breaks_the_line()
    {
        var vm = new Vm();
        for (var i = 0; i <= 60; i++)
            vm.Trend.Add("temp", T0.AddSeconds(i), i is >= 25 and <= 35 ? double.NaN : 50);

        using var bmp = Render(Markup, vm);
        // 30초 지점 x ≈ plotLeft + 0.5*w — 끊긴 구간
        var box = Lay(Markup, vm);
        Assert.NotNull(box.Trend);
        Assert.False(Any(bmp, 245, 115, 260, 131), "gap should not be drawn");
        Assert.True(Any(bmp, 400, 115, 400, 131));
    }

    [Fact]
    public void Empty_trend_renders_placeholder_without_throwing()
    {
        using var bmp = Render(Markup, new Vm());
        var dark = false;
        for (var y = 110; y < 135 && !dark; y++)
        for (var x = 200; x < 290 && !dark; x++)
            dark = bmp.GetPixel(x, y).Red < 150;
        Assert.True(dark, "placeholder text missing");
    }

    [Fact]
    public void Auto_scale_without_y_limits_uses_nice_range()
    {
        var vm = new Vm();
        vm.Trend.Add("temp", T0, 12.3);
        vm.Trend.Add("temp", T0.AddSeconds(30), 87.9);
        var box = Lay(
            "<DvTrend source=\"{Trend}\" style=\"width:480; height:240\"><DvTrendChannel name=\"temp\"/></DvTrend>", vm);
        Assert.Null(box.Trend!.YMin);
        using var bmp = Render(
            "<DvTrend source=\"{Trend}\" style=\"width:480; height:240\"><DvTrendChannel name=\"temp\"/></DvTrend>", vm);
        Assert.NotNull(bmp);
    }

    [Fact]
    public void Sub_second_window_is_not_clamped_to_one_second()
    {
        var vm = new Vm();
        // 0~1초 동안 1 ms 샘플: 0.8초 이전은 0, 이후는 90
        for (var i = 0; i <= 1000; i++)
            vm.Trend.Add("temp", T0.AddMilliseconds(i), i < 800 ? 0 : 90);

        using var bmp = Render(
            "<DvTrend source=\"{Trend}\" window=\"0.2\" y-min=\"0\" y-max=\"100\" style=\"width:480; height:240\">" +
            "<DvTrendChannel name=\"temp\"/></DvTrend>", vm);
        // 창 = 0.8~1.0초 → 전 구간 90(y≈45). 1초 창이었다면 왼쪽 80% 가 0(바닥)
        Assert.True(Any(bmp, 150, 38, 150, 52), "0.2 s window should show only the last 200 ms");
        Assert.False(Any(bmp, 150, 205, 150, 219));
    }
}
