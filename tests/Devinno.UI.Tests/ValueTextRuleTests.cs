using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3단계 R8 — format/unit/show-value. format = .NET 형식(Invariant), 숫자를 글자로 보이는 모든 곳에서 같은 뜻, 쓸 수 없는 형식 = 기본 표시 + 경고.
// unit = 값 글자 뒤에 한 칸 띄고. show-value = 값 글자를 그리는 태그가 모두 지킨다(D2: DvGauge·DvRing 지원, 기본 참).
public class ValueTextRuleTests
{
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

    private static (T Result, List<string> Warnings) Watch<T>(string marker, Func<T> act)
    {
        var cap = new Capture(marker);
        Trace.Listeners.Add(cap);
        try
        {
            var result = act();
            lock (cap.Lines)
                return (result, cap.Lines.ToList());
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
    }

    public sealed class Vm
    {
        public double Level { get; set; } = 40;
        public List<Row> Rows { get; } = [new() { A = 1.5 }, new() { A = 2.5 }];
        public List<Row> Bars { get; } = [new() { A = 3.25 }, new() { A = 7.5 }];
    }

    public sealed class Row
    {
        public double A { get; set; }
    }

    private static LayoutBox Build(string body) =>
        new LayoutEngine(DvTheme.Light).Layout(MarkupParser.Parse($"<DvScreen>{body}</DvScreen>"), new Vm(), 800, 600);

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));
    private static LayoutBox Find(LayoutBox b, string tag) => All(b).First(x => x.Node.Tag == tag);

    private static SKBitmap Render(string body, int w = 300, int h = 200)
    {
        var box = new LayoutEngine(DvTheme.Light).Layout(MarkupParser.Parse($"<DvScreen style=\"padding:10\">{body}</DvScreen>"), new Vm(), w, h);
        var bmp = new SKBitmap(w, h);
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.White);
        new Painter(DvTheme.Light).Paint(c, box, null);
        return bmp;
    }

    private static int Diff(SKBitmap a, SKBitmap b)
    {
        var n = 0;
        for (var y = 0; y < a.Height; y++)
        for (var x = 0; x < a.Width; x++)
            if (a.GetPixel(x, y) != b.GetPixel(x, y))
                n++;
        return n;
    }

    private static int DiffOf(string a, string b, int w = 300, int h = 200)
    {
        using var x = Render(a, w, h);
        using var y = Render(b, w, h);
        return Diff(x, y);
    }

    // ── format: 쓸 수 없는 형식 = 기본 표시 + 경고(전에는 경고 없이 기본 표시, DvInput 은 빈 칸) ──

    [Fact]
    public void Dial_InvalidFormat_DefaultTextWithWarning()
    {
        var (text, w) = Watch("format='X81'", () => Painter.FormatValue(6.2f, "X81", "DvGauge"));
        Assert.Equal("6.2", text);
        Assert.Single(w);
        Assert.Contains("DvGauge format='X81'", w[0]);
        var (ok, none) = Watch("format=", () => Painter.FormatValue(6.25f, "0.0", "DvGauge"));
        Assert.Equal("6.3", ok);
        Assert.Empty(none);
    }

    [Theory]
    [InlineData("DvGauge")]
    [InlineData("DvRing")]
    [InlineData("DvProgress show-value=\"true\"")]
    [InlineData("DvTank")]
    public void Dials_InvalidFormat_WarnWhenDrawn(string tag)
    {
        var (_, w) = Watch("format='X82'", () => { using var _ = Render($"<{tag} value=\"6.2\" format=\"X82\" style=\"width:120; height:120\"/>"); return 0; });
        Assert.NotEmpty(w);
    }

    [Fact]
    public void Input_InvalidFormat_ShowsRawValueWithWarning()
    {
        var (b, _) = Watch("never", () => Build("<DvInput value=\"{Bind Level}\" keyboard-type=\"numeric\" format=\"X83\"/>"));
        var (text, w) = Watch("format='X83'", () => InputDisplay.Text(Find(b, "DvInput")));
        Assert.Equal("40", text); // 전: 빈 칸 + 읽기 오류
        Assert.Single(w);
    }

    [Fact]
    public void GridColumnAndSummary_InvalidFormat_DefaultTextWithWarning()
    {
        var b = Build("<DvDataGrid items=\"{Rows}\"><DvColumn header=\"n\"/><DvColumn field=\"A\" format=\"X84\"/><DvSummaryRow><DvSummary field=\"A\" fn=\"sum\" format=\"X85\"/></DvSummaryRow></DvDataGrid>");
        var g = Find(b, "DvDataGrid").Grid!;
        var (text, w) = Watch("format='X84'", () => g.Columns[1].Text(new Vm().Rows[0]));
        Assert.Equal("1.5", text);
        Assert.Single(w);
        Assert.Contains("DvColumn format='X84'", w[0]);
        var (_, ws) = Watch("format='X85'", () => { Devinno.UI.Grid.GridSummary.Compute(g); return 0; });
        Assert.Single(ws);
        Assert.Equal("4", g.Summaries[0].Texts![1]); // 칸 0 은 요약 제목
    }

    // format 이 정수형에는 맞고 실수형에는 안 맞는 경우(X) — 값 형식마다 판단
    [Fact]
    public void Format_ValidForIntegers_IsUsed()
    {
        var (text, w) = Watch("format='X'", () => AttrRules.FormatOrNull(255, "X", "DvColumn"));
        Assert.Equal("FF", text);
        Assert.Empty(w);
    }

    // ── format 이 새로 붙는 태그: DvSlider 값 글자, DvBarChart·DvPieChart 값 글자 ──

    [Fact]
    public void Slider_Format_ChangesValueText()
    {
        Assert.True(DiffOf("<DvSlider value=\"40\" show-value=\"true\" style=\"width:200\"/>",
            "<DvSlider value=\"40\" show-value=\"true\" format=\"0.00\" style=\"width:200\"/>") > 0);
        Assert.Equal("0.00", Find(Build("<DvSlider value=\"{Level}\" format=\"0.00\"/>"), "DvSlider").ValueFormat);
    }

    [Fact]
    public void Charts_Format_ChangesValueText()
    {
        Assert.True(DiffOf("<DvBarChart items=\"{Bars}\" item-value=\"A\" show-value=\"true\" style=\"width:200; height:150\"/>",
            "<DvBarChart items=\"{Bars}\" item-value=\"A\" show-value=\"true\" format=\"0.000\" style=\"width:200; height:150\"/>") > 0);
        Assert.True(DiffOf("<DvPieChart items=\"{Bars}\" item-value=\"A\" show-value=\"true\" style=\"width:260; height:120\"/>",
            "<DvPieChart items=\"{Bars}\" item-value=\"A\" show-value=\"true\" format=\"0.000\" style=\"width:260; height:120\"/>") > 0);
    }

    // ── unit: DvGauge 가 이제 그린다(전에는 읽고 안 그림) ──

    [Fact]
    public void Gauge_Unit_IsDrawn()
    {
        Assert.True(DiffOf("<DvGauge value=\"40\" style=\"width:140; height:140\"/>",
            "<DvGauge value=\"40\" unit=\"°C\" style=\"width:140; height:140\"/>") > 0);
    }

    // ── show-value: D2 — DvGauge·DvRing 기본 참, false 면 숨김 ──

    [Theory]
    [InlineData("DvGauge")]
    [InlineData("DvRing")]
    public void GaugeRing_ShowValue_DefaultTrue_FalseHides(string tag)
    {
        Assert.True(Find(Build($"<{tag} value=\"40\"/>"), tag).ShowValue);
        Assert.False(Find(Build($"<{tag} value=\"40\" show-value=\"false\"/>"), tag).ShowValue);
        Assert.True(DiffOf($"<{tag} value=\"40\" style=\"width:140; height:140\"/>",
            $"<{tag} value=\"40\" show-value=\"false\" style=\"width:140; height:140\"/>") > 0);
        Assert.Equal(0, DiffOf($"<{tag} value=\"40\" style=\"width:140; height:140\"/>",
            $"<{tag} value=\"40\" show-value=\"true\" style=\"width:140; height:140\"/>"));
    }

    // DvProgress 세로 — 폭에 값 글자가 들어가면 그린다(전에는 세로면 항상 안 그림), 좁으면(기본 12) 안 그림
    [Fact]
    public void Progress_Vertical_ShowValue_DrawnWhenItFits()
    {
        Assert.True(DiffOf("<DvProgress value=\"40\" direction=\"up\" style=\"width:48; height:160\"/>",
            "<DvProgress value=\"40\" direction=\"up\" show-value=\"true\" style=\"width:48; height:160\"/>") > 0);
        Assert.Equal(0, DiffOf("<DvProgress value=\"40\" direction=\"up\" unit=\"%\" style=\"width:12; height:120\"/>",
            "<DvProgress value=\"40\" direction=\"up\" unit=\"%\" show-value=\"true\" style=\"width:12; height:120\"/>"));
    }
}
