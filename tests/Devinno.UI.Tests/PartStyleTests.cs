using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 위젯 부분 지정 ::part() — 위젯 안쪽 글자(값·이름·눈금)를 태그나 클래스로 덮어쓴다(CSS ::part 이름 그대로).
// 덮어쓰기 순서: 테마 역할 → 태그 ::part → 클래스 ::part(나중 클래스가 이김). 지원 키: font-size, font-weight, color.
public class PartStyleTests
{
    private static LayoutBox Lay(string inner)
        => new LayoutEngine().Layout(MarkupParser.Parse("<DvScreen>" + inner + "</DvScreen>"), null, 400, 300);

    private static LayoutBox Find(LayoutBox root, string tag) => LayoutQuery.FindByTag(root, tag)!;

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));

    [Fact]
    public void Stylesheet_reads_tag_and_class_part_selectors()
    {
        var sheet = StyleSheet.Parse("DvGauge::part(value) { font-size: 40 } .big::part(value), .big::part(tick) { color: #ff0000 }");
        Assert.True(sheet.TryGetPart("DvGauge", "value", out var a) && a.Contains("font-size: 40"));
        Assert.True(sheet.TryGetPart(".big", "value", out var b) && b.Contains("#ff0000"));
        Assert.True(sheet.TryGetPart(".big", "tick", out _));
        Assert.False(sheet.TryGetPart("DvGauge", "tick", out _));
    }

    [Fact]
    public void Tag_part_rule_reaches_the_widget()
    {
        var gauge = Find(Lay("<DvStyle>DvGauge::part(value) { font-size: 40; font-weight: 500 }</DvStyle><DvGauge value=\"5\"/>"), "DvGauge");
        var part = gauge.Part("value");
        Assert.Equal(40f, part?.FontSize);
        Assert.Equal(500, part?.FontWeight);
    }

    [Fact]
    public void Class_part_rule_wins_over_tag_part_rule()
    {
        var root = Lay("<DvStyle>DvGauge::part(value) { font-size: 40 } .big::part(value) { font-size: 50 }</DvStyle>" +
                       "<DvGauge class=\"big\" value=\"5\"/><DvGauge value=\"5\"/>");
        var gauges = All(root).Where(b => b.Node.Tag == "DvGauge").ToList();
        Assert.Equal(50f, gauges[0].Part("value")?.FontSize);
        Assert.Equal(40f, gauges[1].Part("value")?.FontSize);
    }

    [Fact]
    public void Part_color_paints_only_that_part()
    {
        static int Red(byte[] png)
        {
            using var bmp = SKBitmap.Decode(png);
            var n = 0;
            for (var y = 0; y < bmp.Height; y++)
                for (var x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.Red > 200 && c.Green < 40 && c.Blue < 40) n++;
                }
            return n;
        }

        const string knob = "<DvKnob value=\"50\" min=\"0\" max=\"100\" style=\"width:110; height:110\"/>";
        var plain = DvRenderer.RenderToPng("<DvScreen>" + knob + "</DvScreen>", null, 120, 120);
        var red = DvRenderer.RenderToPng("<DvScreen><DvStyle>DvKnob::part(value) { color: #ff0000 }</DvStyle>" + knob + "</DvScreen>", null, 120, 120);
        Assert.Equal(0, Red(plain));
        Assert.True(Red(red) > 20);
    }

    [Fact]
    public void Part_size_changes_the_widget_value_text()
    {
        const string ring = "<DvRing value=\"70\" style=\"width:110; height:110\"/>";
        var plain = DvRenderer.RenderToPng("<DvScreen>" + ring + "</DvScreen>", null, 120, 120);
        var big = DvRenderer.RenderToPng("<DvScreen><DvStyle>DvRing::part(value) { font-size: 12 }</DvStyle>" + ring + "</DvScreen>", null, 120, 120);
        Assert.NotEqual(plain, big);
    }

    [Fact]
    public void Unknown_part_name_warns()
    {
        var cap = new Capture("Zq9part");
        Trace.Listeners.Add(cap);
        try
        {
            Lay("<DvStyle>DvGauge::part(Zq9part) { font-size: 40 }</DvStyle><DvGauge value=\"5\"/>");
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
        Assert.NotEmpty(cap.Lines);
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
