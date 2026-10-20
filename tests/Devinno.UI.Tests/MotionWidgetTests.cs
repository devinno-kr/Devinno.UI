using Devinno.UI.Motion;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

public class MotionPhaseTests
{
    [Theory]
    [InlineData(0, 500, true)]
    [InlineData(499, 500, true)]
    [InlineData(500, 500, false)]
    [InlineData(999, 500, false)]
    [InlineData(1000, 500, true)]
    public void BlinkOn_is_square_wave(long ts, int interval, bool expected)
    {
        Assert.Equal(expected, MotionPhase.BlinkOn(ts, interval));
    }

    [Fact]
    public void BlinkOn_nonpositive_interval_is_always_on()
    {
        Assert.True(MotionPhase.BlinkOn(12345, 0));
        Assert.True(MotionPhase.BlinkOn(12345, -1));
    }
}

public class DvLampTests
{
    private static SKColor PixelAt(string markup, int w, int h, int x, int y, long ts)
    {
        var root = MarkupParser.Parse(markup);
        var box = new LayoutEngine().Layout(root, null, w, h);
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var sk = SKSurface.Create(info);
        sk.Canvas.Clear(SKColors.White);
        new Painter().Paint(sk.Canvas, box, null, null, ts);
        sk.Canvas.Flush();
        using var img = sk.Snapshot();
        using var bmp = SKBitmap.FromImage(img);
        return bmp.GetPixel(x, y);
    }

    [Fact]
    public void Lamp_on_shows_on_color()
    {
        var c = PixelAt("<DvLamp on=\"true\" style=\"color:#22c55e; width:40; height:40\"/>", 40, 40, 20, 20, 0);
        Assert.True(c.Green > 150 && c.Red < 120, $"center={c}");
    }

    [Fact]
    public void Lamp_off_is_dim()
    {
        var c = PixelAt("<DvLamp on=\"false\" style=\"color:#22c55e; width:40; height:40\"/>", 40, 40, 20, 20, 0);
        Assert.True(c.Green < 150, $"center={c}");
    }

    [Fact]
    public void Lamp_blink_toggles_between_timestamps()
    {
        const string m = "<DvLamp on=\"true\" blink=\"true\" blink-interval=\"500\" style=\"color:#22c55e; width:40; height:40\"/>";
        var onPhase = PixelAt(m, 40, 40, 20, 20, 0);
        var offPhase = PixelAt(m, 40, 40, 20, 20, 600);
        Assert.True(onPhase.Green > 150, $"onPhase={onPhase}");
        Assert.True(offPhase.Green < 150, $"offPhase={offPhase}");
    }
}

public class DvGaugeTests
{
    private static SKBitmap Render(string markup, int w, int h)
    {
        var root = MarkupParser.Parse(markup);
        var box = new LayoutEngine().Layout(root, null, w, h);
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        var sk = SKSurface.Create(info);
        sk.Canvas.Clear(SKColors.White);
        new Painter().Paint(sk.Canvas, box, null, null, 0);
        sk.Canvas.Flush();
        return SKBitmap.FromImage(sk.Snapshot());
    }

    [Fact]
    public void Gauge_min_value_points_needle_lower_left()
    {
        using var bmp = Render("<DvGauge value=\"0\" min=\"0\" max=\"100\" style=\"width:120; height:120\"/>", 120, 120);
        var found = false;
        for (var d = 10; d < 45 && !found; d++)
        {
            var c = bmp.GetPixel(60 - d, 60 + d);
            if (c.Red < 230 || c.Green < 230 || c.Blue < 230)
                found = true;
        }

        Assert.True(found, "needle not found in lower-left for value=min");
    }

    [Fact]
    public void Gauge_max_value_points_needle_lower_right()
    {
        using var bmp = Render("<DvGauge value=\"100\" min=\"0\" max=\"100\" style=\"width:120; height:120\"/>", 120, 120);
        var found = false;
        for (var d = 10; d < 45 && !found; d++)
        {
            var c = bmp.GetPixel(60 + d, 60 + d);
            if (c.Red < 230 || c.Green < 230 || c.Blue < 230)
                found = true;
        }

        Assert.True(found, "needle not found in lower-right for value=max");
    }

    [Fact]
    public void Gauge_value_in_critical_zone_colors_value_text_red()
    {
        using var bmp = Render(
            "<DvGauge value=\"92\" min=\"0\" max=\"100\" " +
            "alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\" style=\"width:140; height:140\"/>",
            140, 140);

        var red = false;
        for (var yy = 78; yy <= 104 && !red; yy++)
        for (var xx = 55; xx <= 85 && !red; xx++)
        {
            var c = bmp.GetPixel(xx, yy);
            if (c.Red > 160 && c.Green < 110 && c.Blue < 110)
                red = true;
        }

        Assert.True(red, "critical value text not red");
    }

    [Fact]
    public void Gauge_zone_ends_do_not_leave_pale_track_caps()
    {
        using var bmp = Render(
            "<DvGauge value=\"50\" min=\"0\" max=\"100\" " +
            "alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\" style=\"width:140; height:140\"/>",
            140, 140);

        AssertNoPalePixels(bmp, 18, 105, 16, 16, "left arc end");
        AssertNoPalePixels(bmp, 106, 105, 16, 16, "right arc end");
    }

    private static void AssertNoPalePixels(SKBitmap bmp, int x, int y, int w, int h, string area)
    {
        for (var yy = y; yy < y + h; yy++)
        for (var xx = x; xx < x + w; xx++)
        {
            var c = bmp.GetPixel(xx, yy);
            var looksLikeTrack = Math.Abs(c.Red - 0xe2) <= 8 &&
                                 Math.Abs(c.Green - 0xe8) <= 8 &&
                                 Math.Abs(c.Blue - 0xf0) <= 8;
            Assert.False(looksLikeTrack, $"{area} has pale cap pixel {c} at ({xx},{yy})");
        }
    }
}

public class GaugeZoneTests
{
    [Fact]
    public void Parses_zones()
    {
        var zones = GaugeZone.ParseZones("0:60:#16a34a 60:85:#eab308 85:100:#dc2626");
        Assert.Equal(3, zones.Count);
        Assert.Equal(0f, zones[0].From);
        Assert.Equal(60f, zones[0].To);
        Assert.Equal(85f, zones[2].From);
    }

    [Fact]
    public void Empty_or_null_yields_no_zones()
    {
        Assert.Empty(GaugeZone.ParseZones(null));
        Assert.Empty(GaugeZone.ParseZones(""));
    }

    [Fact]
    public void Reversed_zone_throws()
    {
        Assert.Throws<InvalidOperationException>(() => GaugeZone.ParseZones("80:60:#dc2626"));
    }

    [Fact]
    public void Out_of_order_zones_throw()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GaugeZone.ParseZones("60:85:#eab308 0:60:#16a34a"));
    }
}
