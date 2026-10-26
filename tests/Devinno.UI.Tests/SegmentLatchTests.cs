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

    // 세로 배치(stretch 기본) 안의 세그먼트는 내용 폭 — 배경이 마지막 항목 뒤로 비지 않는다
    [Theory]
    [InlineData("<DvStack>{0}</DvStack>")]
    [InlineData("<DvCard title=\"모드\">{0}</DvCard>")]
    [InlineData("{0}")]
    public void Segment_InColumn_FitsItems(string wrap)
    {
        var (s, _) = Start(string.Format(wrap, Seg));
        var seg = Find(s, b => b.Node.Tag == "DvSegment");
        var last = Items(s)[^1];
        Assert.True(seg.Rect.Width < W / 2, $"segment width {seg.Rect.Width}");
        Assert.InRange(seg.Rect.Right - last.Rect.Right, 0f, 5f);
    }

    [Theory]
    [InlineData("style=\"align-self:stretch\"", W - 40)] // 화면 padding 20 × 2
    [InlineData("width=\"300\"", 300f)]
    public void Segment_InColumn_ExplicitStretchOrWidth_Kept(string attrs, float expected)
    {
        var (s, _) = Start($"<DvStack><DvSegment value=\"{{Bind Mode}}\" {attrs}><DvOption value=\"auto\" text=\"자동\"/><DvOption value=\"manual\" text=\"수동\"/></DvSegment></DvStack>");
        var seg = Find(s, b => b.Node.Tag == "DvSegment");
        Assert.Equal(expected, seg.Rect.Width, 0.5f);
    }

    // grow 로 넓어진 세그먼트 — 기본은 항목 폭 그대로·배경만 항목까지, fill="true" 면 남는 폭을 항목에 나눠 꽉 채운다
    private const string GrownRow = """
        <DvRow><DvSegment value="{Bind Mode}" grow="1" ATTRS><DvOption value="auto" text="자동"/><DvOption value="manual" text="수동 운전"/></DvSegment><DvButton text="ON"/></DvRow>
        """;

    [Fact]
    public void Segment_Grown_PaintsBackgroundFittedToItems()
    {
        var (s, _) = Start(GrownRow.Replace("ATTRS", ""));
        var seg = Find(s, b => b.Node.Tag == "DvSegment");
        var last = Items(s)[^1];
        Assert.True(seg.Rect.Right - last.Rect.Right > 50, "the segment box itself grows");
        var fit = Devinno.UI.Painting.Painter.SegmentFitRect(seg, seg.Style);
        Assert.Equal(last.Rect.Right + seg.Style.Padding.Right, fit.Right, 0.5f);
        Assert.Equal(seg.Rect.Left, fit.Left);
    }

    [Fact]
    public void Segment_GrownFill_ItemsShareExtraWidth()
    {
        var (plain, _) = Start(GrownRow.Replace("ATTRS", ""));
        var (s, _) = Start(GrownRow.Replace("ATTRS", "fill=\"true\""));
        var seg = Find(s, b => b.Node.Tag == "DvSegment");
        var items = Items(s);
        var natural = Items(plain);
        Assert.Equal(seg.Rect.Right - seg.Style.Padding.Right, items[^1].Rect.Right, 0.5f);
        // 같은 양을 더한다 — 긴 글자 항목은 여전히 더 넓다
        Assert.Equal(items[0].Rect.Width - natural[0].Rect.Width, items[1].Rect.Width - natural[1].Rect.Width, 0.5f);
        Assert.True(items[1].Rect.Width > items[0].Rect.Width);
    }

    [Fact]
    public void Segment_InColumn_Fill_Stretches()
    {
        var (s, _) = Start($"<DvStack><DvSegment value=\"{{Bind Mode}}\" fill=\"true\"><DvOption value=\"auto\" text=\"자동\"/><DvOption value=\"manual\" text=\"수동\"/></DvSegment></DvStack>");
        var seg = Find(s, b => b.Node.Tag == "DvSegment");
        Assert.Equal(W - 40, seg.Rect.Width, 0.5f);
        Assert.Equal(seg.Rect.Right - seg.Style.Padding.Right, Items(s)[^1].Rect.Right, 0.5f);
    }

    [Fact]
    public void Segment_InRow_StaysVerticallyCentered()
    {
        var (s, _) = Start($"<DvRow>{Seg}<DvButton text=\"ON\" style=\"height:80\"/></DvRow>");
        var seg = Find(s, b => b.Node.Tag == "DvSegment");
        var btn = Find(s, b => b.Node.Tag == "DvButton");
        Assert.Equal(btn.Rect.MidY, seg.Rect.MidY, 0.5f);
    }
}
