using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 래칭 버튼(DvButton value) · 세그먼트(DvSegment + DvOption)
public class SegmentLatchTests
{
    private const float W = 600, H = 300;

    public sealed class Vm
    {
        public bool Auto { get; set; }
        public string Mode { get; set; } = "auto";
        public int Level { get; set; } = 1;
        public bool Allow { get; set; } = true;
        public int Clicks;

        public bool GateBool(bool proposed) => Allow;
        public bool GateText(string proposed) => Allow;
        public void Hit() => Clicks++;
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static LayoutBox Find(DvSurface s, Func<LayoutBox, bool> pick) => Flatten(s.Root!).First(pick);

    private static void Tap(DvSurface s, LayoutBox b)
    {
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static LayoutBox Button(DvSurface s) => Find(s, b => b.Node.Tag == "DvButton");

    private static List<LayoutBox> Items(DvSurface s) => Flatten(s.Root!).Where(b => b.Node.Tag == "DvSegmentItem").ToList();

    // ── 래칭 버튼 ──

    [Fact]
    public void Latch_TapFlips_AndLooksOn()
    {
        var (s, vm) = Start("<DvButton text=\"자동\" value=\"{Bind Auto}\"/>");
        var offBg = Button(s).Style.Background;

        Tap(s, Button(s));
        Assert.True(vm.Auto);
        Assert.True(Button(s).ToggleOn);
        Assert.NotEqual(offBg, Button(s).Style.Background); // 켜짐 모양

        Tap(s, Button(s));
        Assert.False(vm.Auto);
        Assert.Equal(offBg, Button(s).Style.Background);
    }

    [Fact]
    public void Latch_BeforeCommitRejects()
    {
        var (s, vm) = Start("<DvButton text=\"자동\" value=\"{Bind Auto}\" onbeforecommit=\"GateBool\"/>");
        vm.Allow = false;
        Tap(s, Button(s));
        Assert.False(vm.Auto);
    }

    [Fact]
    public void Latch_OneWay_ShowsOnly_OnclickStillRuns()
    {
        var vm = new Vm { Auto = true };
        var (s, _) = Start("<DvButton text=\"자동\" value=\"{Auto}\" onclick=\"Hit\"/>", vm);
        Assert.True(Button(s).ToggleOn);

        Tap(s, Button(s));
        Assert.True(vm.Auto);
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Latch_WithOnclick_BothRun()
    {
        var (s, vm) = Start("<DvButton text=\"자동\" value=\"{Bind Auto}\" onclick=\"Hit\"/>");
        Tap(s, Button(s));
        Assert.True(vm.Auto);
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Latch_Disabled_NoFlip()
    {
        var (s, vm) = Start("<DvButton text=\"자동\" value=\"{Bind Auto}\" disabled=\"true\"/>");
        Tap(s, Button(s));
        Assert.False(vm.Auto);
    }

    [Fact]
    public void Latch_WithHold_NeedsHold()
    {
        long t = 0;
        var vm = new Vm();
        var s = new DvSurface("<DvScreen style=\"padding:20\"><DvButton text=\"자동\" value=\"{Bind Auto}\" hold=\"500\"/></DvScreen>", vm, DvTheme.Light)
        {
            Clock = () => t,
        };
        using var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H, 0);
        var b = Button(s);
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);

        s.PointerDown(p);
        s.PointerUp(p);
        s.RenderTo(c, W, H, 0);
        Assert.False(vm.Auto); // 짧게 = 안 바뀜

        s.PointerDown(p);
        t += 500;
        s.RenderTo(c, W, H, 0);
        Assert.True(vm.Auto);
        s.PointerUp(p);
    }

    // ── 세그먼트 ──

    private const string Seg = """
        <DvSegment value="{Bind Mode}" onbeforecommit="GateText">
          <DvOption value="auto" text="자동"/>
          <DvOption value="manual" text="수동"/>
          <DvOption value="stop" text="정지"/>
        </DvSegment>
        """;

    [Fact]
    public void Segment_BuildsItems_SelectedHighlighted()
    {
        var (s, _) = Start(Seg);
        var items = Items(s);

        Assert.Equal(["자동", "수동", "정지"], items.Select(i => i.ResolvedText));
        Assert.NotEqual(items[1].Style.Background, items[0].Style.Background); // 선택 = 강조
        Assert.Equal(items[1].Style.Background, items[2].Style.Background);
        Assert.True(items[0].Rect.Right <= items[1].Rect.Left); // 한 줄
        Assert.Equal(items[0].Rect.Top, items[1].Rect.Top);
    }

    [Fact]
    public void Segment_TapWritesOptionValue()
    {
        var (s, vm) = Start(Seg);
        Tap(s, Items(s)[2]);
        Assert.Equal("stop", vm.Mode);
        Assert.NotEqual(Items(s)[0].Style.Background, Items(s)[2].Style.Background);
    }

    [Fact]
    public void Segment_BeforeCommitRejects()
    {
        var (s, vm) = Start(Seg);
        vm.Allow = false;
        Tap(s, Items(s)[1]);
        Assert.Equal("auto", vm.Mode);
    }

    [Theory]
    [InlineData("value=\"{Mode}\"")]
    [InlineData("value=\"{Bind Mode}\" readonly=\"true\"")]
    [InlineData("value=\"{Bind Mode}\" disabled=\"true\"")]
    public void Segment_ReadOnlyOrDisabled_NoWrite(string attrs)
    {
        var (s, vm) = Start($"<DvSegment {attrs}><DvOption value=\"auto\" text=\"자동\"/><DvOption value=\"manual\" text=\"수동\"/></DvSegment>");
        Tap(s, Items(s)[1]);
        Assert.Equal("auto", vm.Mode);
    }

    [Fact]
    public void Segment_IntValue_Converted()
    {
        var (s, vm) = Start("<DvSegment value=\"{Bind Level}\"><DvOption value=\"1\" text=\"저\"/><DvOption value=\"2\" text=\"중\"/><DvOption value=\"3\" text=\"고\"/></DvSegment>");
        Tap(s, Items(s)[2]);
        Assert.Equal(3, vm.Level);
    }
}
