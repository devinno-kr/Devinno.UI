using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 차트·단계 생김새(단계 4): 막대는 위쪽 모서리만 둥글게, 단계 막대 연결선은 stroke.emphasis.
public class ChartLookTests
{
    public sealed class Vm
    {
        public double[] Values { get; } = [40, 80, 60];
        public string[] Steps { get; } = ["A", "B", "C", "D"];
    }

    private static SKBitmap Render(string body, int w = 300, int h = 200)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:0\">{body}</DvScreen>", new Vm(), DvTheme.Light);
        s.Layout(w, h);
        var bmp = new SKBitmap(w, h);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, w, h);
        return bmp;
    }

    [Fact]
    public void Bars_round_only_the_top_corners()
    {
        using var bmp = Render("<DvBarChart items=\"{Values}\" style=\"width:300; height:200\"/>");
        var bar = ThemeResolver.Color(DvTheme.Light, "tone-info");
        var cx = 300 / 3 / 2 + 300 / 3; // 가운데(80) 막대
        int top = 0, bottom = 0;
        for (var y = 0; y < 200; y++)
            if (bmp.GetPixel(cx, y) == bar) { if (top == 0) top = y; bottom = y; }
        Assert.True(top > 0, "bar not found");
        var left = cx;
        while (left > 0 && bmp.GetPixel(left - 1, (top + bottom) / 2) == bar)
            left--;
        Assert.Equal(bar, bmp.GetPixel(left, bottom));      // 아래 모서리 = 각진 채움
        Assert.NotEqual(bar, bmp.GetPixel(left, top));      // 위 모서리 = 둥글게 깎임
    }

    [Fact]
    public void Step_connectors_use_the_emphasis_stroke()
    {
        using var bmp = Render("<DvStepBar steps=\"{Steps}\" current=\"3\" style=\"width:300; height:80\"/>", 300, 80);
        var x = 75; // 1·2 단계 사이(칸 75, 점 가운데 37.5 · 112.5)
        var bg = bmp.GetPixel(x, 70);
        var rows = Enumerable.Range(0, 60).Count(y => bmp.GetPixel(x, y) != bg);
        Assert.Equal((int)ThemeResolver.Number(DvTheme.Light, "stroke.emphasis"), rows);
    }
}
