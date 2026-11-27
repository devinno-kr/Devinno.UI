using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 일반 화면 도형 — DvPolyline(선·꺾은선, 화살표), DvPolygon(닫힌 다각형), DvEllipse(타원). 점 = 상자 왼쪽 위 기준 px(늘이지 않음).
// 칠 = background(색·linear-gradient, 닫힌 모양만), 외곽선 = border(border-width·border-color·border-style). 속성 = 같은 이름의 style(가장 낮은 우선순위).
// 둘레 사각형(상자)에는 배경·테두리를 그리지 않는다. 누름 = 그려진 모양(선은 max(두께/2, 6px) 안, 칠한 모양은 안쪽까지).
public class ShapeTests
{
    private static LayoutBox Layout(string body, object? vm = null, float w = 300, float h = 200) =>
        new LayoutEngine().Layout(MarkupParser.Parse($"<DvScreen style=\"padding:0; align-items:start\">{body}</DvScreen>"), vm, w, h);

    private static SKBitmap Render(string body, object? vm = null, int w = 300, int h = 200, DvTheme? theme = null)
    {
        var box = new LayoutEngine(theme).Layout(MarkupParser.Parse($"<DvScreen style=\"padding:0; align-items:start; background:#ffffff\">{body}</DvScreen>"), vm, w, h);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        new Painter(theme).Paint(canvas, box, vm, null, 0);
        return bmp;
    }

    private static bool IsWhite(SKColor c) => c.Red > 245 && c.Green > 245 && c.Blue > 245;
    private static bool IsRed(SKColor c) => c.Red > 200 && c.Green < 60 && c.Blue < 60;
    private static bool IsBlue(SKColor c) => c.Blue > 200 && c.Red < 60 && c.Green < 60;
    private static bool IsGreen(SKColor c) => c.Green > 150 && c.Red < 60 && c.Blue < 60;

    // ── 크기

    [Theory]
    [InlineData("<DvPolyline points=\"0,0 200,40\"/>", 200, 40)]
    [InlineData("<DvPolyline points=\"10,5 60,30 120,10\"/>", 120, 30)]
    [InlineData("<DvPolygon points=\"40,0 80,70 0,70\"/>", 80, 70)]
    [InlineData("<DvEllipse points=\"10,10 110,60\"/>", 110, 60)]
    [InlineData("<DvEllipse/>", 100, 100)]
    [InlineData("<DvEllipse style=\"width:60; height:30\"/>", 60, 30)]
    [InlineData("<DvPolyline points=\"0,0 200,40\" style=\"width:50; height:10\"/>", 50, 10)]
    public void Natural_size_is_the_point_range(string markup, float w, float h)
    {
        var b = Layout(markup).Children[0];
        Assert.Equal(w, b.Rect.Width, 3);
        Assert.Equal(h, b.Rect.Height, 3);
    }

    // ── 점 검사

    [Theory]
    [InlineData("<DvPolyline points=\"0,0 abc\"/>", "points")]
    [InlineData("<DvPolyline points=\"0,0\"/>", "at least 2")]
    [InlineData("<DvPolygon points=\"0,0 10,10\"/>", "at least 3")]
    [InlineData("<DvEllipse points=\"0,0\"/>", "0 or 2")]
    [InlineData("<DvEllipse points=\"0,0 1,1 2,2\"/>", "0 or 2")]
    public void Bad_points_are_not_drawn_and_warn(string markup, string expect)
    {
        var cap = new Capture("points");
        Trace.Listeners.Add(cap);
        SKBitmap bmp;
        LayoutBox b;
        try
        {
            var m = markup.Replace("/>", " border-color=\"#ff0000\" border-width=\"6\"/>");
            bmp = Render(m);
            b = Layout(m).Children[0];
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        Assert.Contains(cap.Lines, l => l.Contains(expect));
        Assert.True(IsWhite(bmp.GetPixel(0, 0)));
        Assert.True(IsWhite(bmp.GetPixel(5, 5)));
        Assert.Equal(0, b.Rect.Width, 3);
        bmp.Dispose();
    }

    [Fact]
    public void Points_may_be_bound()
    {
        var b = Layout("<DvPolyline points=\"{P}\"/>", new { P = "0,0 50,20" }).Children[0];
        Assert.Equal(50, b.Rect.Width, 3);
    }

    // ── 그리기

    [Fact]
    public void Polyline_draws_its_line_and_no_box()
    {
        using var bmp = Render("<DvPolyline points=\"0,20 200,20\" border-color=\"#ff0000\" border-width=\"4\" style=\"background:#00ff00; height:40\"/>");
        Assert.True(IsRed(bmp.GetPixel(100, 20)));
        Assert.True(IsWhite(bmp.GetPixel(100, 35))); // 상자 배경(초록)은 그리지 않는다
        Assert.True(IsWhite(bmp.GetPixel(100, 25)));
    }

    [Fact]
    public void Polygon_fills_inside_and_leaves_the_box_corners()
    {
        using var bmp = Render("<DvPolygon points=\"50,0 100,100 0,100\" background=\"#0000ff\" border-width=\"0\"/>");
        Assert.True(IsBlue(bmp.GetPixel(50, 70)));
        Assert.True(IsWhite(bmp.GetPixel(3, 3)));
        Assert.True(IsWhite(bmp.GetPixel(97, 3)));
    }

    [Fact]
    public void Polygon_takes_a_linear_gradient()
    {
        using var bmp = Render("<DvPolygon points=\"0,0 100,0 100,100 0,100\" background=\"linear-gradient(to right, #ff0000, #0000ff)\" border-width=\"0\"/>");
        Assert.True(bmp.GetPixel(5, 50).Red > 200);
        Assert.True(bmp.GetPixel(95, 50).Blue > 200);
    }

    [Fact]
    public void Ellipse_fills_its_rect_and_not_the_corners()
    {
        using var bmp = Render("<DvEllipse background=\"#0000ff\" border-width=\"0\" style=\"width:120; height:60\"/>");
        Assert.True(IsBlue(bmp.GetPixel(60, 30)));
        Assert.True(IsWhite(bmp.GetPixel(2, 2)));
    }

    [Fact]
    public void Ellipse_outline_stays_inside_the_box()
    {
        using var bmp = Render("<DvEllipse border-color=\"#ff0000\" border-width=\"6\" style=\"width:100; height:100\"/>");
        Assert.True(IsRed(bmp.GetPixel(50, 3)));  // 위쪽 외곽선이 상자 안(0~6)
        Assert.True(IsWhite(bmp.GetPixel(50, 50)));
    }

    [Fact]
    public void Style_wins_over_the_attribute()
    {
        using var bmp = Render("<DvPolyline points=\"0,20 200,20\" border-color=\"#ff0000\" border-width=\"4\" style=\"border-color:#00a000\"/>");
        Assert.True(IsGreen(bmp.GetPixel(100, 20)));
    }

    [Fact]
    public void Default_outline_is_the_theme_text_color_two_px()
    {
        var theme = DvTheme.Light;
        DvColor text = default;
        Assert.True(theme.TryResolve("text", out var raw) && DvColor.TryParse(raw, out text));
        using var bmp = Render("<DvPolyline points=\"0,20 200,20\"/>", theme: theme);
        var c = bmp.GetPixel(100, 20);
        Assert.InRange(Math.Abs(c.Red - text.R), 0, 40);
        Assert.InRange(Math.Abs(c.Blue - text.B), 0, 40);
        Assert.True(IsWhite(bmp.GetPixel(100, 23)));
    }

    [Fact]
    public void Zero_width_draws_no_outline()
    {
        using var bmp = Render("<DvPolyline points=\"0,20 200,20\" border-color=\"#ff0000\" border-width=\"0\"/>");
        Assert.True(IsWhite(bmp.GetPixel(100, 20)));
    }

    [Fact]
    public void Dotted_and_dashed_leave_gaps()
    {
        foreach (var s in new[] { "dotted", "dashed" })
        {
            using var bmp = Render($"<DvPolyline points=\"0,20 200,20\" border-color=\"#ff0000\" border-width=\"4\" border-style=\"{s}\"/>");
            var row = Enumerable.Range(10, 150).Select(x => bmp.GetPixel(x, 20)).ToList();
            Assert.Contains(row, IsRed);
            Assert.Contains(row, IsWhite);
        }
    }

    [Fact]
    public void Arrow_head_is_wider_than_the_line()
    {
        using var none = Render("<DvPolyline points=\"0,20 200,20\" border-color=\"#ff0000\" border-width=\"2\"/>");
        using var end = Render("<DvPolyline points=\"0,20 200,20\" border-color=\"#ff0000\" border-width=\"2\" arrow=\"end\"/>");
        using var both = Render("<DvPolyline points=\"0,20 200,20\" border-color=\"#ff0000\" border-width=\"2\" arrow=\"both\"/>");
        Assert.True(IsWhite(none.GetPixel(190, 23)));
        Assert.True(IsRed(end.GetPixel(190, 23)));
        Assert.True(IsWhite(end.GetPixel(10, 23)));
        Assert.True(IsRed(both.GetPixel(10, 23)));
    }

    [Fact]
    public void Drawing_past_the_box_is_not_clipped_or_culled()
    {
        // 상자(-100..-50)는 화면 밖이지만 점은 200 까지 — 그려진 곳은 보인다
        using var bmp = Render("<DvPolyline points=\"0,20 300,20\" border-color=\"#ff0000\" border-width=\"4\" style=\"position:absolute; left:-100; top:0; width:50; height:40\"/>");
        Assert.True(IsRed(bmp.GetPixel(150, 20)));
    }

    [Fact]
    public void Colors_may_be_bound_and_take_tokens()
    {
        using var bmp = Render("<DvPolygon points=\"0,0 100,0 100,100 0,100\" background=\"{C}\" border-width=\"0\"/>", new { C = "#0000ff" });
        Assert.True(IsBlue(bmp.GetPixel(50, 50)));
        using var tok = Render("<DvPolyline points=\"0,20 200,20\" border-color=\"token(primary)\" border-width=\"4\"/>", theme: DvTheme.Light);
        Assert.False(IsWhite(tok.GetPixel(100, 20)));
    }

    // ── 누름

    public sealed class Vm
    {
        public int Clicks { get; private set; }
        public void Hit() => Clicks++;
    }

    private static int Click(string body, float x, float y)
    {
        var vm = new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:0; align-items:start\">{body}</DvScreen>", vm);
        s.Layout(300, 200);
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        return vm.Clicks;
    }

    [Fact]
    public void Line_is_pressed_near_the_line_only()
    {
        const string line = "<DvPolyline points=\"0,0 200,100\" border-width=\"2\" onclick=\"Hit\"/>";
        Assert.Equal(1, Click(line, 100, 50));
        Assert.Equal(1, Click(line, 100, 55)); // 6px 안
        Assert.Equal(0, Click(line, 150, 20)); // 상자 안이지만 선에서 멀다
    }

    [Fact]
    public void Filled_shape_is_pressed_inside_unfilled_only_on_the_outline()
    {
        Assert.Equal(1, Click("<DvPolygon points=\"0,0 100,0 100,100 0,100\" background=\"#0000ff\" onclick=\"Hit\"/>", 50, 50));
        Assert.Equal(0, Click("<DvPolygon points=\"0,0 100,0 100,100 0,100\" onclick=\"Hit\"/>", 50, 50));
        Assert.Equal(1, Click("<DvPolygon points=\"0,0 100,0 100,100 0,100\" onclick=\"Hit\"/>", 50, 1));
        Assert.Equal(0, Click("<DvEllipse background=\"#0000ff\" style=\"width:100; height:100\" onclick=\"Hit\"/>", 4, 4)); // 모서리
    }

    [Fact]
    public void Part_drawn_past_the_box_is_pressed()
    {
        // 상자는 50×40 이지만 선은 200 까지 — 상자 밖 150 에서도 눌린다
        Assert.Equal(1, Click("<DvPolyline points=\"0,20 200,20\" border-width=\"2\" style=\"width:50; height:40\" onclick=\"Hit\"/>", 150, 20));
    }

    [Fact]
    public void Negative_points_draw_left_of_the_box()
    {
        using var bmp = Render("<DvPolyline points=\"-40,20 60,20\" border-color=\"#ff0000\" border-width=\"4\" style=\"position:absolute; left:100; top:0\"/>");
        Assert.True(IsRed(bmp.GetPixel(70, 20)));
        var b = Layout("<DvPolyline points=\"-40,20 60,20\"/>").Children[0];
        Assert.Equal(60, b.Rect.Width, 3);
    }

    [Fact]
    public void Box_shadow_image_and_radius_do_nothing_on_shapes()
    {
        using var bmp = Render("<DvPolyline points=\"0,20 200,20\" border-width=\"0\" style=\"height:40; box-shadow:4 4 0 #ff0000; border-radius:8\"/>");
        Assert.True(IsWhite(bmp.GetPixel(100, 42)));
        Assert.True(IsWhite(bmp.GetPixel(100, 20)));
    }

    [Fact]
    public void Whole_box_hit_for_editors()
    {
        var root = Layout("<DvPolyline points=\"0,0 200,100\"/>");
        var line = root.Children[0];
        var path = HitTester.HitPath(root, new SKPoint(150, 20), wholeBox: true);
        Assert.Contains(path, e => ReferenceEquals(e.Box, line));
        Assert.DoesNotContain(HitTester.HitPath(root, new SKPoint(150, 20), wholeBox: false), e => ReferenceEquals(e.Box, line));
    }

    private sealed class Capture(string marker) : TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && message.Contains(marker))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message)
            => WriteLine(message);
    }
}
