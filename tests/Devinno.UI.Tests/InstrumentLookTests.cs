using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 계기 생김새(단계 5a): 값 문법(값 > 단위 작게·흐림, ::part(unit)), 게이지 구간 띠·짧은 바늘, 링 굵기, 탱크 한계 점선, 노브 가장자리 표시.
public class InstrumentLookTests
{
    private static (DvSurface S, SKBitmap Bmp) Render(string body, DvTheme? theme = null, int w = 240, int h = 240)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:0\">{body}</DvScreen>", null, theme ?? DvTheme.Light);
        s.Layout(w, h);
        var bmp = new SKBitmap(w, h);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, w, h);
        return (s, bmp);
    }

    private static LayoutBox Find(DvSurface s, string tag) => Flatten(s.Root!).First(b => b.Node.Tag == tag);
    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static bool Near(SKColor a, SKColor b, int tol = 40)
        => Math.Abs(a.Red - b.Red) <= tol && Math.Abs(a.Green - b.Green) <= tol && Math.Abs(a.Blue - b.Blue) <= tol;

    private static (int Top, int Bottom, int Count) Extent(SKBitmap bmp, SKColor color)
    {
        int top = int.MaxValue, bottom = -1, n = 0;
        for (var y = 0; y < bmp.Height; y++)
            for (var x = 0; x < bmp.Width; x++)
                if (Near(bmp.GetPixel(x, y), color, 60))
                {
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                    n++;
                }
        return (top, bottom, n);
    }

    [Theory]
    [InlineData("DvRing")]
    [InlineData("DvGauge")]
    [InlineData("DvKnob")]
    [InlineData("DvTank")]
    public void Unit_is_drawn_smaller_than_the_value_and_takes_its_part_style(string tag)
    {
        var css = $"<DvStyle>{tag}::part(value) {{ color: #0000ff }} {tag}::part(unit) {{ color: #ff0000 }}</DvStyle>";
        var (_, bmp) = Render(css + $"<{tag} value=\"75\" unit=\"bar\" style=\"width:200; height:200\"/>");
        using var _1 = bmp;
        var v = Extent(bmp, new SKColor(0, 0, 255));
        var u = Extent(bmp, new SKColor(255, 0, 0));
        Assert.True(v.Count > 0 && u.Count > 0, $"value {v.Count} px, unit {u.Count} px");
        Assert.True(u.Bottom - u.Top < (v.Bottom - v.Top) * 0.85, $"unit height {u.Bottom - u.Top} vs value {v.Bottom - v.Top}");
    }

    // 구간은 트랙 위를 덮지 않고, 트랙 안쪽에 틈을 두고 얇은 띠로
    [Fact]
    public void Gauge_zone_is_a_thin_band_inside_the_track()
    {
        var (s, bmp) = Render("<DvGauge value=\"0\" alarm-zones=\"0:100:#ff0000\" style=\"width:200; height:200\"/>");
        using var _ = bmp;
        var r = Find(s, "DvGauge").Rect;
        var gm = Devinno.UI.Painting.WidgetMetrics.Gauge(r, true);
        // 값 50 각도 = 위쪽(바늘은 값 0 에 있어 겹치지 않음)
        var track = bmp.GetPixel((int)r.MidX, (int)MathF.Round(r.MidY - gm.Radius));
        var band = bmp.GetPixel((int)r.MidX, (int)MathF.Round(r.MidY - gm.ZoneRadius));
        var gap = bmp.GetPixel((int)r.MidX, (int)MathF.Round(r.MidY - (gm.Radius - gm.TrackWidth / 2f + gm.ZoneRadius + gm.ZoneWidth / 2f) / 2f));
        Assert.True(Near(track, ThemeResolver.Color(DvTheme.Light, "gauge-track"), 25), $"track {track}");
        Assert.True(Near(band, new SKColor(255, 0, 0), 30), $"band {band}");
        Assert.False(Near(gap, new SKColor(255, 0, 0), 60), $"gap {gap}");
        Assert.True(gm.TrackWidth <= gm.ZoneWidth, "the track is a thin line");
    }

    // 링 기본 굵기 = 계기 호 굵기 단계(크기의 1/12, H3)
    [Fact]
    public void Ring_default_thickness_is_the_instrument_arc_step()
    {
        var (s, bmp) = Render("<DvRing value=\"0\" style=\"width:200; height:200\"/>");
        using var _ = bmp;
        var r = Find(s, "DvRing").Rect;
        var track = ThemeResolver.Color(DvTheme.Light, "gauge-track");
        // 링 위쪽 가운데(값 0 이라 트랙만, 아래는 열린 호) — 트랙 색 줄 수
        var rows = Enumerable.Range((int)r.Top, (int)(r.Height / 2)).Count(y => Near(bmp.GetPixel((int)r.MidX, y), track, 4));
        var expected = Devinno.UI.Painting.WidgetMetrics.Gauge(r).Stroke;
        Assert.InRange(rows, expected - 1.5f, expected + 1.5f);
    }

    // 탱크 한계 — 바깥 눈금에 더해 몸통 안 얇은 점선(구간색)
    [Fact]
    public void Tank_limit_is_also_a_dashed_line_inside_the_body()
    {
        var (s, bmp) = Render("<DvTank value=\"10\" alarm-zones=\"50:100:#ff0000\" style=\"width:120; height:200\"/>");
        using var _ = bmp;
        var r = Find(s, "DvTank").Rect;
        int red = 0, rows = 0;
        for (var y = (int)r.MidY - 3; y <= (int)r.MidY + 3; y++)
        {
            var n = 0;
            for (var x = (int)r.Left + 6; x < (int)(r.Left + r.Width * 0.55f); x++)
                if (Near(bmp.GetPixel(x, y), new SKColor(255, 0, 0), 70))
                    n++;
            if (n > 0) rows++;
            red += n;
        }
        Assert.True(red > 6, $"no limit line inside the body ({red} px)");
        Assert.True(rows <= 3, $"limit line is thin ({rows} rows)");
    }

    // 정상 색(S2) off — 액체는 구간색을 따르지 않고 tank-fill(한계는 표시·값 칩이 알림)
    [Fact]
    public void Tank_liquid_ignores_zone_color_when_normal_color_is_off()
    {
        var off = DvTheme.Light.With(new Dictionary<string, string> { ["look.normal-color"] = "off", ["look.texture"] = "flat" });
        var on = DvTheme.Light.With(new Dictionary<string, string> { ["look.texture"] = "flat" });
        const string tank = "<DvTank value=\"90\" alarm-zones=\"85:100:#ff0000\" style=\"width:120; height:200\"/>";
        var (s, offBmp) = Render(tank, off);
        var (_, onBmp) = Render(tank, on);
        using var _1 = offBmp;
        using var _2 = onBmp;
        var r = Find(s, "DvTank").Rect;
        var at = ((int)(r.Left + r.Width * 0.2f), (int)(r.Bottom - 12));
        Assert.True(Near(onBmp.GetPixel(at.Item1, at.Item2), new SKColor(255, 0, 0), 30), "S2 on follows the zone");
        Assert.True(Near(offBmp.GetPixel(at.Item1, at.Item2), ThemeResolver.Color(off, "tank-fill"), 12), $"S2 off liquid {offBmp.GetPixel(at.Item1, at.Item2)}");
    }

    // 노브 지시 표시는 면 가장자리의 짧은 표시 — 면 가운데 쪽(값 글자 자리)으로 들어오지 않는다
    [Fact]
    public void Knob_indicator_stays_at_the_face_edge()
    {
        var (s, bmp) = Render("<DvKnob value=\"0\" min=\"0\" max=\"100\" style=\"width:200; height:200\"/>");
        using var _ = bmp;
        var r = Find(s, "DvKnob").Rect;
        var km = Devinno.UI.Painting.WidgetMetrics.Knob(r);
        var faceR = km.Radius - km.FaceInset;
        var a = 135f * MathF.PI / 180f; // 값 0 각도
        var fill = ThemeResolver.Color(DvTheme.Light, "knob-fill");
        bool At(float k) => Near(bmp.GetPixel((int)(r.MidX + MathF.Cos(a) * faceR * k), (int)(r.MidY + MathF.Sin(a) * faceR * k)), fill, 20);
        Assert.False(At(0.6f), "indicator reaches toward the center");
        Assert.True(At(0.85f), "indicator at the edge");
    }

    // 눈금 숫자는 바늘 위에 그린다(바늘이 숫자를 가리지 않음)
    [Fact]
    public void Gauge_tick_labels_are_drawn_over_the_needle()
    {
        var css = "<DvStyle>DvGauge::part(tick) { color: #00ff00 }</DvStyle>";
        // 0..10, 큰 눈금 5 → "5" 는 위쪽 가운데. 값 5 면 바늘이 그 숫자를 지난다
        var (_, bmp) = Render(css + "<DvGauge value=\"5\" min=\"0\" max=\"10\" ticks=\"5\" style=\"width:200; height:200\"/>");
        using var _1 = bmp;
        var (_, without) = Render(css + "<DvGauge value=\"0\" min=\"0\" max=\"10\" ticks=\"5\" style=\"width:200; height:200\"/>");
        using var _2 = without;
        var green = new SKColor(0, 255, 0);
        int Count(SKBitmap b) { var n = 0; for (var y = 0; y < 100; y++) for (var x = 80; x < 120; x++) if (Near(b.GetPixel(x, y), green, 60)) n++; return n; }
        Assert.True(Count(bmp) >= Count(without) * 0.95, $"label pixels with needle {Count(bmp)} vs without {Count(without)}");
    }
}
