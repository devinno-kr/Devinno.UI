using Devinno.UI;
using Devinno.UI.App;
using SkiaSharp;
using Xunit;

// DvKnob input-mode tap·both — 끌지 않은 탭이 숫자 키패드(InputNumber 박스)를 띄우고, 확인한 값은 드래그와 같은 커밋 경로로 쓴다.
public class KnobKeypadTests
{
    private const float W = 800, H = 600;
    private static readonly SKPoint Center = new(148, 148); // left 100, top 100, 96×96

    private sealed class Page(string mode, string extra = "") : DvPage
    {
        public double Level { get; set; } = 40;
        public bool Allow { get; set; } = true;
        public int Clicks;

        public bool Gate(double proposed) => Allow;

        public void Hit() => Clicks++;

        protected override string MarkupSource =>
            $"<DvScreen style=\"padding:0\"><DvKnob value=\"{{Bind Level}}\" min=\"0\" max=\"100\" unit=\"%\" {mode} {extra} " +
            "style=\"position:absolute; left:100; top:100\"/></DvScreen>";
    }

    private static (DvApp App, DvSurface Root, Page Page) Start(string mode, string extra = "")
    {
        var page = new Page(mode, extra);
        var app = new DvApp();
        app.Pages.Add(page);
        app.Surface.Layout(W, H);
        return (app, app.Surface, page);
    }

    private static void Tap(DvSurface root, SKPoint p)
    {
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(W, H);
    }

    private static DvNumberBoxDialog Keypad(DvApp app) => Assert.IsType<DvNumberBoxDialog>(app.Dialogs.Top);

    private static void Ok(DvApp app, DvSurface root, double value)
    {
        var d = Keypad(app);
        d.Value = value;
        d.PressOk();
        root.Layout(W, H);
        root.Layout(W, H);
    }

    [Fact]
    public void Tap_OpensKeypad_WithCurrentValueRangeAndUnit()
    {
        var (app, root, _) = Start("input-mode=\"tap\"");
        Tap(root, Center);

        var d = Keypad(app);
        Assert.Equal(40, d.Value);
        Assert.Equal(0, d.Min);
        Assert.Equal(100, d.Max);
        Assert.Equal("%", d.Unit);
    }

    [Fact]
    public void Ok_WritesSnappedValue()
    {
        var (app, root, page) = Start("input-mode=\"tap\"");
        Tap(root, Center);
        Ok(app, root, 42.4);

        Assert.Equal(42, page.Level); // step 1 반올림
        Assert.Empty(app.Dialogs);
    }

    [Fact]
    public void Ok_WithStep_RoundsLikeDrag()
    {
        var (app, root, page) = Start("input-mode=\"tap\"", "step=\"5\"");
        Tap(root, Center);
        Ok(app, root, 62.5);

        Assert.Equal(65, page.Level); // 반 올림(half-up)
    }

    [Fact]
    public void Cancel_LeavesValue()
    {
        var (app, root, page) = Start("input-mode=\"tap\"");
        Tap(root, Center);
        Keypad(app).PressCancel();
        root.Layout(W, H);
        root.Layout(W, H);

        Assert.Equal(40, page.Level);
    }

    [Fact]
    public void BeforeCommitRejects_ValueUnchanged()
    {
        var (app, root, page) = Start("input-mode=\"tap\"", "onbeforecommit=\"Gate\"");
        page.Allow = false;
        Tap(root, Center);
        Ok(app, root, 70);

        Assert.Equal(40, page.Level);
    }

    [Fact]
    public void TapMode_DragDoesNotRotate()
    {
        var (app, root, page) = Start("input-mode=\"tap\"");
        root.PointerDown(new SKPoint(110, 148));
        root.PointerMove(new SKPoint(148, 105));
        root.PointerMove(new SKPoint(186, 148));
        root.PointerUp(new SKPoint(186, 148));
        root.Layout(W, H);

        Assert.Equal(40, page.Level);
    }

    [Fact]
    public void BothMode_DragRotates_NoKeypad()
    {
        var (app, root, page) = Start("input-mode=\"both\"");
        root.PointerDown(new SKPoint(110, 148));
        root.PointerMove(new SKPoint(148, 105));
        root.PointerMove(new SKPoint(186, 148));
        root.PointerUp(new SKPoint(186, 148));
        root.Layout(W, H);

        Assert.NotEqual(40, page.Level);
        Assert.Empty(app.Dialogs);
    }

    [Fact]
    public void BothMode_TapOpensKeypad()
    {
        var (app, root, _) = Start("input-mode=\"both\"");
        Tap(root, Center);

        Assert.IsType<DvNumberBoxDialog>(app.Dialogs.Top);
    }

    [Theory]
    [InlineData("")]
    [InlineData("input-mode=\"drag\"")]
    [InlineData("input-mode=\"keypad\"")] // 모르는 값 = drag
    public void DragMode_TapDoesNotOpenKeypad(string mode)
    {
        var (app, root, _) = Start(mode);
        Tap(root, Center);

        Assert.Empty(app.Dialogs);
    }

    [Fact]
    public void TapMode_OnclickNotCalled()
    {
        var (app, root, page) = Start("input-mode=\"tap\"", "onclick=\"Hit\"");
        Tap(root, Center);

        Assert.Equal(0, page.Clicks);
        Assert.Single(app.Dialogs);
    }

    [Theory]
    [InlineData("disabled=\"true\"")]
    public void Disabled_NoKeypad(string extra)
    {
        var (app, root, _) = Start("input-mode=\"tap\"", extra);
        Tap(root, Center);

        Assert.Empty(app.Dialogs);
    }

    [Fact]
    public void ReadOnlyKnob_NoKeypad()
    {
        var page = new ReadOnlyPage();
        var app = new DvApp();
        app.Pages.Add(page);
        app.Surface.Layout(W, H);
        Tap(app.Surface, Center);

        Assert.Empty(app.Dialogs);
    }

    private sealed class ReadOnlyPage : DvPage
    {
        public double Level => 40;

        protected override string MarkupSource =>
            "<DvScreen style=\"padding:0\"><DvKnob value=\"{Level}\" input-mode=\"tap\" style=\"position:absolute; left:100; top:100\"/></DvScreen>";
    }

    [Fact]
    public void PlainSurface_TapIgnored_NoThrow()
    {
        var vm = new Vm();
        var s = new DvSurface(
            "<DvScreen style=\"padding:0\"><DvKnob value=\"{Bind Level}\" input-mode=\"tap\" style=\"position:absolute; left:100; top:100\"/></DvScreen>", vm);
        s.Layout(W, H);
        s.PointerDown(Center);
        s.PointerUp(Center);
        s.Layout(W, H);

        Assert.Equal(40, vm.Level);
    }

    public sealed class Vm
    {
        public double Level { get; set; } = 40;
    }
}
