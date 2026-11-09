using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Editing;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3단계 R6 — 사용자 입력은 범위 밖 값을 만들 수 없다(드래그·스테퍼 = 경계에서 멈춤, 키보드·키패드 = 거절). 코드·VM 값은 그대로, 그리기만 경계에서 자름.
// min ≥ max = 경고 + 0~100 (Knob·Slider·Input(숫자)·Gauge·Ring·Progress·Tank·그리드 입력 셀). 경고 값마다 고유한 숫자를 써서 다른 테스트와 섞이지 않게 한다.
public class RangeRuleTests
{
    private const float W = 800, H = 600;

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
        public List<Row> Rows { get; } = [new()];
    }

    public sealed class Row
    {
        public double A { get; set; } = 3;
    }

    private static LayoutBox Build(string body) =>
        new LayoutEngine(DvTheme.Light).Layout(MarkupParser.Parse($"<DvScreen>{body}</DvScreen>"), new Vm(), 800, 600);

    private static IEnumerable<LayoutBox> All(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(All));
    private static LayoutBox Find(LayoutBox b, string tag) => All(b).First(x => x.Node.Tag == tag);

    // ── min ≥ max = 경고 + 0~100 ──

    [Theory]
    [InlineData("DvKnob", "min=\"91.25\" max=\"10\"")]
    [InlineData("DvSlider", "min=\"91.25\" max=\"91.25\"")]
    public void Knob_Slider_MinNotBelowMax_WarnsAndUses0To100(string tag, string range)
    {
        var (b, w) = Watch("91.25", () => Build($"<{tag} value=\"{{Bind Level}}\" {range}/>"));
        var k = Find(b, tag);
        Assert.Equal((0f, 100f), (k.KnobMin, k.KnobMax)); // 노브가 뒤집어 쓰던 것(10~91.25)도 이제 0~100
        Assert.Single(w);
        Assert.Contains($"{tag} min='91.25' is not below max=", w[0]);
    }

    [Theory]
    [InlineData("DvGauge")]
    [InlineData("DvRing")]
    [InlineData("DvProgress")]
    public void Dials_MinNotBelowMax_WarnsAndUses0To100(string tag)
    {
        var (b, w) = Watch("92.25", () => Build($"<{tag} value=\"50\" min=\"92.25\" max=\"5\"/>"));
        var d = Find(b, tag);
        Assert.Equal((0f, 100f), (d.GaugeMin, d.GaugeMax));
        Assert.Single(w);
    }

    [Fact]
    public void Tank_MinNotBelowMax_WarnsAndUses0To100()
    {
        var (b, w) = Watch("93.25", () => Build("<DvTank value=\"50\" min=\"93.25\" max=\"93.25\"/>"));
        var t = Find(b, "DvTank");
        Assert.Equal((0f, 100f), (t.TankMin, t.TankMax));
        Assert.Single(w);
    }

    [Fact]
    public void NumericInput_MinNotBelowMax_WarnsAndUses0To100_TextInputUntouched()
    {
        var (b, w) = Watch("94.25", () => Build("<DvInput value=\"{Bind Level}\" keyboard-type=\"numeric\" min=\"94.25\" max=\"3\"/>"));
        var i = Find(b, "DvInput");
        Assert.Equal((0d, 100d), (i.InputMin, i.InputMax));
        Assert.Single(w);
        // 한쪽만 있으면 범위 검사 없음(지금 그대로)
        var one = Find(Build("<DvInput value=\"{Bind Level}\" keyboard-type=\"numeric\" min=\"94.25\"/>"), "DvInput");
        Assert.Equal((94.25d, (double?)null), (one.InputMin, one.InputMax));
    }

    [Fact]
    public void GridInputCell_MinNotBelowMax_WarnsAndUses0To100()
    {
        var (b, w) = Watch("95.25", () => Build("<DvDataGrid items=\"{Rows}\"><DvColumn field=\"A\" cell-type=\"input\" min=\"95.25\" max=\"1\"/></DvDataGrid>"));
        var col = Find(b, "DvDataGrid").Grid!.Columns[0];
        Assert.Equal((0d, 100d), (col.Min, col.Max));
        Assert.Single(w);
    }

    [Fact]
    public void ValidRange_NoWarning()
    {
        var (b, w) = Watch("96.25", () => Build("<DvKnob min=\"-96.25\" max=\"10\"/><DvGauge min=\"-96.25\" max=\"0\"/>"));
        Assert.Equal((-96.25f, 10f), (Find(b, "DvKnob").KnobMin, Find(b, "DvKnob").KnobMax));
        Assert.Empty(w);
    }

    // 코드·VM 값은 자르지 않는다(그리기만 경계에서)
    [Fact]
    public void VmValue_OutOfRange_NotClamped()
    {
        var b = Build("<DvKnob value=\"150\"/><DvGauge value=\"-20\"/>");
        Assert.Equal(150f, Find(b, "DvKnob").KnobValue);
        Assert.Equal(-20f, Find(b, "DvGauge").GaugeValue);
    }

    // ── 노브 키패드 ──

    private sealed class KnobPage : DvPage
    {
        public double Level { get; set; } = 40;

        protected override string MarkupSource =>
            "<DvScreen style=\"padding:0\"><DvKnob value=\"{Bind Level}\" min=\"0\" max=\"100\" input-mode=\"tap\" " +
            "style=\"position:absolute; left:100; top:100\"/></DvScreen>";
    }

    private static (DvApp App, DvSurface Root, KnobPage Page) OpenKeypad()
    {
        var page = new KnobPage();
        var app = new DvApp(); // 터치 모드(화면 키보드)
        app.Pages.Add(page);
        app.Surface.Layout(W, H);
        var root = app.Surface;
        var c = new SKPoint(148, 148);
        root.PointerDown(c);
        root.PointerUp(c);
        root.Layout(W, H);
        return (app, root, page);
    }

    private static void Render(DvSurface s)
    {
        using var bmp = new SKBitmap((int)W, (int)H);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, W, H);
    }

    // 키패드에서 범위 밖을 치면 입력창과 같이 거절(세션 유지 + 오류 표시) — 값은 그대로
    [Fact]
    public void Keypad_TypedOutOfRange_RejectedLikeInput()
    {
        var (app, root, page) = OpenKeypad();
        Render(root);
        root.KeyInput(DvKey.Backspace);
        root.KeyInput(DvKey.Backspace);
        root.TextInput("150");
        root.KeyInput(DvKey.Enter);
        root.Layout(W, H);

        Assert.Single(app.Dialogs);
        Assert.True(root.Edit.IsEditing);
        Assert.NotNull(root.Edit.Error);
        Assert.Equal(40, page.Level);
    }

    // 키패드가 범위 밖 값을 돌려주면(확인) 자르지 않고 거절 — 지금까지는 100 으로 잘라 썼다
    [Fact]
    public void Keypad_ResultOutOfRange_Rejected_NotClamped()
    {
        var (app, root, page) = OpenKeypad();
        var d = Assert.IsType<DvNumberBoxDialog>(app.Dialogs.Top);
        d.Value = 150;
        d.PressOk();
        root.Layout(W, H);
        root.Layout(W, H);

        Assert.Equal(40, page.Level);
    }
}
