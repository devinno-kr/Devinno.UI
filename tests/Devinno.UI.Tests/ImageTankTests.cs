using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using SkiaSharp;
using Xunit;

// 같은 전역 이미지 캐시를 쓴다 — ImageCacheTests 가 시계·상한을 바꾸는 동안 함께 돌지 않게
[Collection("ImageCache")]
public class DvImageTests
{
    private static string TempPng(SKColor color, int w, int h)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dvimg_{color.Red}_{color.Green}_{color.Blue}_{w}x{h}.png");
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var c = new SKCanvas(bmp))
            c.Clear(color);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static SKColor PixelAt(string markup, int w, int h, int x, int y)
    {
        var box = new LayoutEngine().Layout(MarkupParser.Parse(markup), null, w, h);
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var sk = SKSurface.Create(info);
        sk.Canvas.Clear(SKColors.White);
        new Painter().Paint(sk.Canvas, box, null);
        sk.Canvas.Flush();
        using var img = sk.Snapshot();
        using var bmp = SKBitmap.FromImage(img);
        return bmp.GetPixel(x, y);
    }

    [Fact]
    public void Image_fill_covers_box_with_pixels()
    {
        var src = TempPng(new SKColor(0x33, 0x99, 0xff), 8, 8).Replace("\\", "/");
        var c = PixelAt($"<DvImage src=\"{src}\" fit=\"fill\" style=\"width:80; height:60\"/>", 80, 60, 40, 30);
        Assert.True(c.Blue > 180 && c.Red < 120, $"center={c}");
    }

    [Fact]
    public void Image_contain_letterboxes_keeping_aspect()
    {
        var src = TempPng(new SKColor(0xff, 0x33, 0x33), 8, 8).Replace("\\", "/");
        var center = PixelAt($"<DvImage src=\"{src}\" fit=\"contain\" style=\"width:160; height:80\"/>", 160, 80, 80, 40);
        var leftEdge = PixelAt($"<DvImage src=\"{src}\" fit=\"contain\" style=\"width:160; height:80\"/>", 160, 80, 5, 40);
        Assert.True(center.Red > 180 && center.Green < 120, $"center={center}");
        Assert.True(leftEdge.Red > 230 && leftEdge.Green > 230, $"leftEdge={leftEdge}");
    }

    [Fact]
    public void Image_missing_src_renders_nothing()
    {
        var c = PixelAt("<DvImage src=\"___no_such_file___.png\" style=\"width:40; height:40\"/>", 40, 40, 20, 20);
        Assert.True(c.Red > 230 && c.Green > 230 && c.Blue > 230, $"center={c}");
    }

    [Theory]
    [InlineData("Cover", Devinno.UI.Widgets.ImageFit.Cover)]
    [InlineData("FILL", Devinno.UI.Widgets.ImageFit.Fill)]
    [InlineData(" cover ", Devinno.UI.Widgets.ImageFit.Cover)]
    [InlineData("Contain", Devinno.UI.Widgets.ImageFit.Contain)]
    [InlineData("bogus", Devinno.UI.Widgets.ImageFit.Contain)]
    [InlineData(null, Devinno.UI.Widgets.ImageFit.Contain)]
    public void Fit_parser_ignores_case(string? v, Devinno.UI.Widgets.ImageFit expected)
        => Assert.Equal(expected, Devinno.UI.Widgets.ImageFitParser.Parse(v));
}

public class DvTankTests
{
    // 흰 캔버스 위 밝은 탱크 몸체를 전제로 한 검사 — 테마 null 은 Default(Dark) 이므로 Light 를 명시
    private static SKBitmap Render(string markup, int w, int h)
    {
        var theme = Devinno.UI.Styling.DvTheme.Light;
        var box = new LayoutEngine(theme).Layout(MarkupParser.Parse(markup), null, w, h);
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        var sk = SKSurface.Create(info);
        sk.Canvas.Clear(SKColors.White);
        new Painter(theme).Paint(sk.Canvas, box, null);
        sk.Canvas.Flush();
        return SKBitmap.FromImage(sk.Snapshot());
    }

    [Fact]
    public void Tank_fills_from_bottom_to_level()
    {
        using var bmp = Render("<DvTank value=\"50\" min=\"0\" max=\"100\" style=\"width:80; height:160\"/>", 80, 160);
        var bottom = bmp.GetPixel(40, 150);
        var top = bmp.GetPixel(40, 20);
        Assert.True(bottom.Blue > 150 && bottom.Red < 120, $"bottom={bottom}");
        Assert.True(top.Red > 220 && top.Green > 220 && top.Blue > 220, $"top={top}");
    }

    [Fact]
    public void Tank_full_fills_near_top()
    {
        using var bmp = Render("<DvTank value=\"100\" min=\"0\" max=\"100\" style=\"width:80; height:160\"/>", 80, 160);
        var top = bmp.GetPixel(40, 20);
        Assert.True(top.Blue > 150 && top.Red < 120, $"top={top}");
    }

    [Fact]
    public void Tank_empty_area_paints_light_body_background()
    {
        using var bmp = Render(
            "<DvScreen style=\"background:#0f172a\"><DvTank value=\"20\" min=\"0\" max=\"100\" style=\"width:80; height:160\"/></DvScreen>",
            80, 160);
        var top = bmp.GetPixel(40, 20);
        Assert.True(top.Red > 220 && top.Green > 220 && top.Blue > 220, $"top={top}");
    }

    [Fact]
    public void Tank_value_in_critical_zone_fills_red()
    {
        using var bmp = Render(
            "<DvTank value=\"92\" min=\"0\" max=\"100\" " +
            "alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\" style=\"width:80; height:160\"/>",
            80, 160);
        var bottom = bmp.GetPixel(40, 150);
        Assert.True(bottom.Red > 160 && bottom.Green < 110 && bottom.Blue < 110, $"bottom={bottom}");
    }

    [Fact]
    public void Tank_zone_markers_drawn_on_right_edge()
    {
        using var bmp = Render(
            "<DvTank value=\"10\" min=\"0\" max=\"100\" " +
            "alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\" style=\"width:80; height:160\"/>",
            80, 160);
        // 눈금+경계값 라벨은 우측 영역(경계값 숫자 여백 포함)에 그려진다
        var red = false;
        for (var yy = 4; yy <= 34 && !red; yy++)
        for (var xx = 40; xx < 80 && !red; xx++)
        {
            var c = bmp.GetPixel(xx, yy);
            if (c.Red > 160 && c.Green < 110 && c.Blue < 110)
                red = true;
        }

        Assert.True(red, "critical zone marker not found on right edge");
    }

    [Fact]
    public void Tank_zone_threshold_labels_reserve_right_gutter()
    {
        using var bmp = Render(
            "<DvTank value=\"10\" min=\"0\" max=\"100\" " +
            "alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\" style=\"width:80; height:160\"/>",
            80, 160);

        // 라벨 여백(오른쪽 끝 열)에 경계값 숫자(빨강 85 / 노랑 60) 픽셀이 존재
        var label = false;
        for (var yy = 0; yy < 160 && !label; yy++)
        for (var xx = 68; xx < 80 && !label; xx++)
        {
            var c = bmp.GetPixel(xx, yy);
            if ((c.Red > 160 && c.Green < 110 && c.Blue < 110) || (c.Red > 180 && c.Green > 140 && c.Blue < 80))
                label = true;
        }

        Assert.True(label, "threshold label not found in right gutter");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(" show-zone-marks=\"false\"", false)]
    public void Tank_zone_marks_attribute_toggles_gutter_but_keeps_zone_fill(string attr, bool gutter)
    {
        using var bmp = Render(
            "<DvTank value=\"92\" min=\"0\" max=\"100\" " +
            "alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\"" + attr + " style=\"width:80; height:160\"/>",
            80, 160);

        var nearRight = bmp.GetPixel(74, 150);
        var isFill = nearRight.Red > 160 && nearRight.Green < 110 && nearRight.Blue < 110;
        Assert.Equal(!gutter, isFill);

        var center = bmp.GetPixel(30, 150);
        Assert.True(center.Red > 160 && center.Green < 110, $"zone fill lost: {center}");
    }

    [Fact]
    public void Tank_empty_area_follows_dark_theme()
    {
        var box = new Devinno.UI.Layout.LayoutEngine(Devinno.UI.Styling.DvTheme.Dark).Layout(
            Devinno.UI.Markup.MarkupParser.Parse("<DvTank value=\"10\" style=\"width:80; height:160\"/>"), null, 80, 160);
        using var bmp = new SKBitmap(80, 160);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        new Painter(Devinno.UI.Styling.DvTheme.Dark).Paint(canvas, box, null);

        var top = bmp.GetPixel(40, 20);
        Assert.True(top.Red < 80 && top.Green < 80 && top.Blue < 100, $"top={top}");
    }
}
