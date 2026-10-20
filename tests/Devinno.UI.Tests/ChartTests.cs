using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvSparkline(values) · DvBarChart · DvPieChart(items + item-value + item-text) — 계열 하나, 표시 전용.
public class ChartTests
{
    private const float W = 800, H = 600;

    public sealed record Line(string Name, double Count);

    public sealed class Vm
    {
        public List<double> Trend { get; } = new() { 3, 5, 4, 8, 6, 9 };
        public List<Line> Lines { get; } = new() { new("1라인", 120), new("2라인", 80), new("3라인", 40) };
        public List<Line> Empty { get; } = new();
        public List<Line> Mixed { get; } = new() { new("A", 10), new("B", -5), new("C", 0), new("D", 10) };
    }

    private static (DvSurface S, SKBitmap Bmp) Render(string body)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:20; gap:10; align-items:start\">{body}</DvScreen>", new Vm(), DvTheme.Light);
        s.Layout(W, H);
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        return (s, bmp);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static LayoutBox Box(DvSurface s, string tag) => Flatten(s.Root!).First(b => b.Node.Tag == tag);
    private static SKColor Token(string name) => DvColor.TryParse(DvTheme.Light.Tokens[name], out var c) ? c.ToSK() : default;

    [Fact]
    public void Sparkline_Values_From_List_Or_Text()
    {
        var (s, bmp) = Render("<DvSparkline values=\"{Trend}\" area=\"true\"/><DvSparkline values=\"1, 2,x,4\" tone=\"danger\"/>");
        using (bmp)
        {
            var boxes = Flatten(s.Root!).Where(b => b.Node.Tag == "DvSparkline").ToList();
            Assert.Equal(new double[] { 3, 5, 4, 8, 6, 9 }, boxes[0].ChartValues);
            Assert.Equal(new double[] { 1, 2, 0, 4 }, boxes[1].ChartValues); // 못 읽는 값 = 0
            Assert.Equal((120f, 32f), (boxes[0].Rect.Width, boxes[0].Rect.Height));
            Assert.True(boxes[0].ChartArea);
        }
    }

    [Fact]
    public void BarChart_Items_And_Bar_Color()
    {
        var (s, bmp) = Render("<DvBarChart items=\"{Lines}\" item-value=\"Count\" item-text=\"Name\" show-value=\"true\" tone=\"success\"/>");
        using (bmp)
        {
            var b = Box(s, "DvBarChart");
            Assert.Equal(new double[] { 120, 80, 40 }, b.ChartValues);
            Assert.Equal(new[] { "1라인", "2라인", "3라인" }, b.ChartLabels);
            // 첫 막대(가장 높음) 가운데 아래쪽은 tone-success 색
            var x = (int)(b.Rect.Left + b.Rect.Width / 6);
            Assert.Equal(Token("tone-success"), bmp.GetPixel(x, (int)(b.Rect.Bottom - 40)));
        }
    }

    [Fact]
    public void PieChart_First_Slice_Starts_At_Top_Clockwise()
    {
        var (s, bmp) = Render("<DvPieChart items=\"{Lines}\" item-value=\"Count\" item-text=\"Name\"/>");
        using (bmp)
        {
            var b = Box(s, "DvPieChart");
            var size = b.Rect.Height;
            var c = new SKPoint(b.Rect.Left + size / 2f, b.Rect.MidY);
            var rr = size / 2f - 4f;
            // 첫 조각(120/240 = 반원)은 12시에서 시계 방향 — 3시 방향은 첫 색, 9시 방향은 다른 색
            SKColor At(float deg) => bmp.GetPixel((int)(c.X + MathF.Cos(deg * MathF.PI / 180) * rr * 0.6f), (int)(c.Y + MathF.Sin(deg * MathF.PI / 180) * rr * 0.6f));
            Assert.Equal(Devinno.UI.Trends.TrendSpec.Palette[0], At(0));
            Assert.NotEqual(Devinno.UI.Trends.TrendSpec.Palette[0], At(180));
        }
    }

    [Fact]
    public void Donut_Hole_Is_Surface()
    {
        var (s, bmp) = Render("<DvPieChart items=\"{Lines}\" item-value=\"Count\" inner-radius=\"0.6\" style=\"width:180; height:180\"/>");
        using (bmp)
        {
            var b = Box(s, "DvPieChart");
            Assert.Equal(0.6f, b.ChartInner);
            Assert.Equal(Token("surface"), bmp.GetPixel((int)b.Rect.MidX, (int)b.Rect.MidY));
        }
    }

    [Fact]
    public void Empty_Negative_Zero_Do_Not_Throw()
    {
        var (_, bmp) = Render(
            "<DvSparkline/><DvSparkline values=\"5\"/>" +
            "<DvBarChart items=\"{Empty}\"/><DvBarChart items=\"{Mixed}\" item-value=\"Count\" item-text=\"Name\"/>" +
            "<DvPieChart items=\"{Empty}\"/><DvPieChart items=\"{Mixed}\" item-value=\"Count\" item-text=\"Name\" show-value=\"true\" inner-radius=\"2\"/>");
        bmp.Dispose();
    }
}
