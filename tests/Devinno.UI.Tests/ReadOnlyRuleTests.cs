using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3단계 R5 — 값 위젯 전부 readonly 를 받는다. 판단식 하나: readonly 참(공용 참/거짓 규칙, 바인딩 가능) 또는 value 가 단방향·리터럴.
// 모양 하나: 값은 그대로, 조작만 막힘(흐리게는 disabled 몫) — 점선 테두리(입력 상자 .input-readonly, 그리는 위젯은 모양 둘레 점선).
public class ReadOnlyRuleTests
{
    private const float W = 600, H = 700;

    public enum Mode { Auto, Manual }

    public sealed class Vm
    {
        public bool On { get; set; }
        public Mode Run { get; set; } = Mode.Auto;
        public double Level { get; set; } = 40;
        public string Pick { get; set; } = "A";
        public bool Ro { get; set; } = true;
        public int Gates;
        public bool GateBool(bool proposed) { Gates++; return true; }
        public bool GateMode(object proposed) { Gates++; return true; }
        public bool GateNum(double proposed) { Gates++; return true; }
        public bool GateText(object proposed) { Gates++; return true; }
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20; gap:8\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static LayoutBox One(DvSurface s, string tag) => Flatten(s.Root!).First(b => b.Node.Tag == tag);

    private static void TapAt(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static void Tap(DvSurface s, LayoutBox b) => TapAt(s, new SKPoint(b.Rect.MidX, b.Rect.MidY));

    private static void Drag(DvSurface s, SKPoint from, SKPoint to)
    {
        s.PointerDown(from);
        s.PointerMove(new SKPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        s.PointerMove(to);
        s.PointerUp(to);
        s.Layout(W, H);
    }

    [Theory]
    [InlineData("readonly=\"true\"")]
    [InlineData("readonly=\"1\"")]
    [InlineData("readonly=\"{Ro}\"")]
    public void Toggle_CheckBox_ReadOnly_TapBlocked_GateNotCalled(string ro)
    {
        foreach (var tag in new[] { "DvToggle", "DvCheckBox" })
        {
            var (s, vm) = Start($"<{tag} value=\"{{Bind On}}\" onbeforecommit=\"GateBool\" {ro}/>");
            Assert.True(One(s, tag).ReadOnly, tag);
            Tap(s, One(s, tag));
            Assert.False(vm.On, tag);
            Assert.Equal(0, vm.Gates);
        }
    }

    [Fact]
    public void Toggle_ReadOnlyFalse_Writes()
    {
        var (s, vm) = Start("<DvToggle value=\"{Bind On}\" readonly=\"false\"/>");
        Assert.False(One(s, "DvToggle").ReadOnly);
        Tap(s, One(s, "DvToggle"));
        Assert.True(vm.On);
    }

    [Fact]
    public void Toggle_OneWay_IsReadOnly()
    {
        var (s, _) = Start("<DvToggle value=\"{On}\"/><DvCheckBox value=\"true\"/>");
        Assert.True(One(s, "DvToggle").ReadOnly);
        Assert.True(One(s, "DvCheckBox").ReadOnly);
    }

    [Fact]
    public void Radio_ReadOnly_TapBlocked()
    {
        var (s, vm) = Start("<DvRadio option=\"Manual\" value=\"{Bind Run}\" onbeforecommit=\"GateMode\" readonly=\"true\" text=\"M\"/>");
        Assert.True(One(s, "DvRadio").ReadOnly);
        Tap(s, One(s, "DvRadio"));
        Assert.Equal(Mode.Auto, vm.Run);
        Assert.Equal(0, vm.Gates);
    }

    [Fact]
    public void Radio_GroupMemory_NotReadOnly_UnlessAttribute()
    {
        var (s, _) = Start("<DvRadio option=\"A\" group=\"g\" text=\"A\"/>");
        Assert.False(One(s, "DvRadio").ReadOnly); // value 없음 = 화면이 기억(쓸 수 있음)
        var (s2, _) = Start("<DvRadio option=\"A\" group=\"g\" text=\"A\" readonly=\"true\"/>");
        Assert.True(One(s2, "DvRadio").ReadOnly);
        Tap(s2, One(s2, "DvRadio"));
        Assert.False(One(s2, "DvRadio").ToggleOn);
        var (s3, _) = Start("<DvRadio option=\"A\" value=\"{Pick}\" text=\"A\"/>");
        Assert.True(One(s3, "DvRadio").ReadOnly); // 단방향
    }

    [Fact]
    public void Knob_ReadOnly_DragBlocked()
    {
        var (s, vm) = Start("<DvKnob value=\"{Bind Level}\" onbeforecommit=\"GateNum\" readonly=\"true\" style=\"position:absolute; left:100; top:100\"/>");
        var k = One(s, "DvKnob");
        Assert.True(k.ReadOnly);
        Assert.False(Devinno.UI.Interaction.KnobBinder.CanDrag(k));
        Drag(s, new SKPoint(k.Rect.Left + 10, k.Rect.MidY), new SKPoint(k.Rect.Right - 10, k.Rect.MidY));
        Assert.Equal(40, vm.Level);
        Assert.Equal(0, vm.Gates);
    }

    [Fact]
    public void Slider_ReadOnly_TapAndDragBlocked()
    {
        var (s, vm) = Start("<DvSlider value=\"{Bind Level}\" onbeforecommit=\"GateNum\" readonly=\"true\" style=\"width:300\"/>");
        var b = One(s, "DvSlider");
        Assert.True(b.ReadOnly);
        TapAt(s, new SKPoint(b.Rect.Right - 14, b.Rect.MidY));
        Drag(s, new SKPoint(b.Rect.Left + 14, b.Rect.MidY), new SKPoint(b.Rect.Right - 14, b.Rect.MidY));
        Assert.Equal(40, vm.Level);
        Assert.Equal(0, vm.Gates);
    }

    [Fact]
    public void Segment_ListBox_TreeView_ReadOnly_UseSameRule()
    {
        var (s, vm) = Start("""
            <DvSegment value="{Bind Pick}" readonly="{Ro}"><DvOption value="A"/><DvOption value="B"/></DvSegment>
            <DvListBox value="{Bind Pick}" readonly="{Ro}"><DvOption value="A"/><DvOption value="B"/></DvListBox>
            """);
        Assert.True(One(s, "DvSegment").ReadOnly);
        Assert.True(One(s, "DvListBox").ReadOnly);
        Tap(s, Flatten(s.Root!).Where(b => b.Node.Tag == "DvSegmentItem").ElementAt(1));
        Tap(s, Flatten(s.Root!).Where(b => b.Node.Tag == "DvListItem").ElementAt(1));
        Assert.Equal("A", vm.Pick);
        vm.Ro = false;
        s.Invalidate();
        s.Layout(W, H);
        Assert.False(One(s, "DvSegment").ReadOnly);
        Assert.False(One(s, "DvListBox").ReadOnly);
    }

    // 모양 하나 — 입력 상자처럼 생긴 것(DvSegment·DvListBox·DvTreeView 포함)은 .input-readonly(투명 바탕 + 점선), 흐리게 하지 않는다
    [Fact]
    public void Segment_ListBox_TreeView_GetInputReadOnlyLook()
    {
        var (s, _) = Start("""
            <DvSegment value="A"><DvOption value="A"/><DvOption value="B"/></DvSegment>
            <DvListBox value="A"><DvOption value="A"/></DvListBox>
            <DvTreeView value="{Pick}"/>
            <DvSegment value="{Bind Pick}"><DvOption value="A"/></DvSegment>
            """);
        var segs = Flatten(s.Root!).Where(b => b.Node.Tag == "DvSegment").ToList();
        Assert.True(segs[0].Style.BorderDashed);
        Assert.True(One(s, "DvListBox").Style.BorderDashed);
        Assert.True(One(s, "DvTreeView").Style.BorderDashed);
        Assert.False(segs[1].Style.BorderDashed);
    }

    // 그리는 위젯(토글·체크·라디오·노브·슬라이더) — 모양 둘레 점선, 값 색은 그대로(반투명 아님)
    [Theory]
    [InlineData("DvCheckBox", "value=\"true\"", "value=\"{Bind On}\"")]
    [InlineData("DvRadio", "option=\"A\" value=\"A\"", "option=\"A\" value=\"{Bind Pick}\"")]
    [InlineData("DvKnob", "value=\"40\"", "value=\"{Bind Level}\"")]
    [InlineData("DvSlider", "value=\"40\" style=\"width:200\"", "value=\"{Bind Level}\" style=\"width:200\"")]
    [InlineData("DvToggle", "value=\"true\"", "value=\"{Bind On}\"")]
    public void DrawnWidgets_ReadOnly_DashedOutline_NotDimmed(string tag, string readOnly, string writable)
    {
        var vm = new Vm { On = true, Pick = "A" };
        using var ro = Render($"<{tag} {readOnly}/>", vm);
        using var rw = Render($"<{tag} {writable}/>", vm);
        var diff = 0;
        var dimmer = 0;
        for (var y = 0; y < ro.Height; y++)
        for (var x = 0; x < ro.Width; x++)
        {
            var a = ro.GetPixel(x, y);
            var b = rw.GetPixel(x, y);
            if (a != b)
                diff++;
            if (b != SKColors.White && a.Alpha < b.Alpha)
                dimmer++;
        }
        Assert.True(diff > 20, $"{tag}: read-only should draw an outline ({diff} px differ)");
        Assert.Equal(0, dimmer);
    }

    private static SKBitmap Render(string body, Vm vm)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:30; background:#ffffff\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(300, 200);
        var root = s.Root!;
        var bmp = new SKBitmap(300, 200);
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.White);
        new Painter(DvTheme.Light).Paint(c, root, null);
        return bmp;
    }
}
