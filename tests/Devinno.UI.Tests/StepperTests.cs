using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 숫자 DvInput show-stepper — 양 끝 −/+ 로 step 만큼 증감(min~max), 숫자 입력과 같은 쓰기 경로.
public class StepperTests
{
    private const float W = 500, H = 200;

    public sealed class Vm
    {
        public double Temp { get; set; } = 25;
        public int Count { get; set; } = 3;
        public int? Maybe { get; set; }
        public bool Allow { get; set; } = true;

        public bool Gate(double proposed) => Allow;
    }

    private static (DvSurface S, Vm Vm) Start(string attrs, Vm? vm = null, bool native = false)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20\"><DvInput keyboard-type=\"numeric\" {attrs} style=\"width:240\"/></DvScreen>", vm, DvTheme.Light)
        {
            UseNativeEditor = native,
        };
        s.Layout(W, H);
        return (s, vm);
    }

    private static LayoutBox Input(DvSurface s)
    {
        IEnumerable<LayoutBox> Flat(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flat));
        return Flat(s.Root!).First(b => b.Node.Tag == "DvInput");
    }

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static SKPoint Minus(DvSurface s) => new(Input(s).Rect.Left + 12, Input(s).Rect.MidY);
    private static SKPoint Plus(DvSurface s) => new(Input(s).Rect.Right - 12, Input(s).Rect.MidY);
    private static SKPoint Middle(DvSurface s) => new(Input(s).Rect.MidX, Input(s).Rect.MidY);

    [Fact]
    public void PlusMinus_StepAndClamp()
    {
        var (s, vm) = Start("value=\"{Bind Temp}\" min=\"0\" max=\"26\" step=\"0.5\" show-stepper=\"true\"");
        Tap(s, Plus(s));
        Assert.Equal(25.5, vm.Temp);
        Tap(s, Plus(s));
        Tap(s, Plus(s));
        Assert.Equal(26, vm.Temp); // max 에서 멈춤
        Tap(s, Minus(s));
        Assert.Equal(25.5, vm.Temp);
    }

    [Fact]
    public void IntProperty_DefaultStep1_ClampsMin()
    {
        var (s, vm) = Start("value=\"{Bind Count}\" min=\"2\" show-stepper=\"true\"");
        Tap(s, Minus(s));
        Assert.Equal(2, vm.Count);
        Tap(s, Minus(s));
        Assert.Equal(2, vm.Count);
        Tap(s, Plus(s));
        Assert.Equal(3, vm.Count);
    }

    [Fact]
    public void NullValue_StartsFromMin()
    {
        var (s, vm) = Start("value=\"{Bind Maybe}\" min=\"10\" show-stepper=\"true\"");
        Tap(s, Plus(s));
        Assert.Equal(11, vm.Maybe);
    }

    [Fact]
    public void BeforeCommitRejects()
    {
        var (s, vm) = Start("value=\"{Bind Temp}\" onbeforecommit=\"Gate\" show-stepper=\"true\"");
        vm.Allow = false;
        Tap(s, Plus(s));
        Assert.Equal(25, vm.Temp);
    }

    [Theory]
    [InlineData("value=\"{Temp}\" show-stepper=\"true\"")]
    [InlineData("value=\"{Bind Temp}\" readonly=\"true\" show-stepper=\"true\"")]
    [InlineData("value=\"{Bind Temp}\" disabled=\"true\" show-stepper=\"true\"")]
    public void ReadOnlyOrDisabled_NoStep(string attrs)
    {
        var (s, vm) = Start(attrs);
        Tap(s, Plus(s));
        Assert.Equal(25, vm.Temp);
    }

    [Fact]
    public void MiddleTap_StartsEditing_NotStep()
    {
        var (s, vm) = Start("value=\"{Bind Temp}\" show-stepper=\"true\"");
        Tap(s, Middle(s));
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(25, vm.Temp);
    }

    [Fact]
    public void WhileEditing_PlusCommitsTypedThenSteps()
    {
        var (s, vm) = Start("value=\"{Bind Temp}\" show-stepper=\"true\"", native: true);
        Tap(s, Middle(s));
        s.SetEditText(s.Edit.SessionId, "50");
        Tap(s, Plus(s));

        Assert.Equal(51, vm.Temp);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void WithoutStepper_EdgeTapEdits()
    {
        var (s, vm) = Start("value=\"{Bind Temp}\"");
        Tap(s, new SKPoint(Input(s).Rect.Right - 12, Input(s).Rect.MidY));
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(25, vm.Temp);
    }

    [Fact]
    public void TextAreaLeavesRoomForButtons()
    {
        var (with, _) = Start("value=\"{Bind Temp}\" show-stepper=\"true\"");
        var (without, _) = Start("value=\"{Bind Temp}\"");
        Assert.Equal(without.Root is not null ? Input(without).Style.Padding.Left + 40 : 0, Input(with).Style.Padding.Left);
        Assert.Equal(Input(without).Style.Padding.Right + 40, Input(with).Style.Padding.Right);
    }
}
