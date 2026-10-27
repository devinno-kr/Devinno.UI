using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 커스텀 위젯(DvWidget) 1단계 — 앱이 C# 클래스로 그리는 태그. 속성·바인딩·크기·그리기·테마·오류 격리·인스턴스 유지
public class WidgetTests
{
    public sealed class Vm
    {
        public string Name { get; set; } = "Cell-A";
        public double Soc { get; set; } = 72.5;
        public List<double> Series { get; } = [1, 2, 3];
        public List<string> Items { get; } = ["a", "b", "c"];
        public int Clicks;
        public void Hit() => Clicks++;
    }

    // 칸 수 × 20 폭, label·soc·series 를 기억, 내용 영역 밖까지 큰 사각형을 칠해 잘림을 본다
    public sealed class Cells : DvWidget
    {
        public string? Label;
        public double Soc;
        public object? Series;
        public SKRect Bounds;
        public SKColor Primary;
        public int Painted;

        public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new((float)ctx.Number("cells", 4) * 20, 30);

        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
        {
            Painted++;
            Label = ctx.Text("label");
            Soc = ctx.Number("soc", -1);
            Series = ctx.Value("series");
            Bounds = bounds;
            Primary = ctx.Color("primary");
            using var p = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(SKRect.Inflate(bounds, 200, 200), p);
        }
    }

    public sealed class Plain : DvWidget
    {
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) { }
    }

    public sealed class Boom : DvWidget
    {
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) => throw new InvalidOperationException("boom");
    }

    public sealed class Spinner : DvWidget
    {
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) { }
        public override int? FrameInterval(DvWidgetContext ctx) => ctx.Flag("spin") ? 50 : null;
    }

    private static ComponentRegistry Registry()
    {
        var r = new ComponentRegistry();
        r.RegisterWidget<Cells>("Cells");
        r.RegisterWidget<Plain>("Plain");
        r.RegisterWidget<Boom>("Boom");
        r.RegisterWidget<Spinner>("Spinner");
        return r;
    }

    private static DvSurface Surface(string body, Vm? vm = null, DvTheme? theme = null) =>
        new($"<DvScreen><DvStack>{body}</DvStack></DvScreen>", vm ?? new Vm(), theme ?? DvTheme.Light, Registry());

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static List<LayoutBox> Boxes(DvSurface s, string tag) => Flatten(s.Root!).Where(b => b.Node.Tag == tag).ToList();

    private static SKBitmap Render(DvSurface s, int w = 600, int h = 400)
    {
        var bmp = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        s.RenderTo(canvas, w, h);
        return bmp;
    }

    [Fact]
    public void Measure_SetsContentSize_PaddingAdds_StyleWins()
    {
        var s = Surface("""
            <Cells cells="6" style="align-self:start"/>
            <Cells cells="6" style="align-self:start; padding:10"/>
            <Cells cells="6" style="width:300; height:50"/>
            """);
        s.Layout(600, 400);
        var b = Boxes(s, "Cells");
        Assert.Equal(new SKSize(120, 30), b[0].Rect.Size);
        Assert.Equal(new SKSize(140, 50), b[1].Rect.Size);
        Assert.Equal(new SKSize(300, 50), b[2].Rect.Size);
    }

    [Fact]
    public void Measure_Default_Is120x80()
    {
        var s = Surface("<Plain style=\"align-self:start\"/>");
        s.Layout(600, 400);
        Assert.Equal(new SKSize(120, 80), Boxes(s, "Plain")[0].Rect.Size);
    }

    [Fact]
    public void Paint_GetsResolvedAttributes_ContentBounds_AndIsClipped()
    {
        var vm = new Vm();
        var s = Surface("""
            <DvLabel text="above"/>
            <Cells cells="5" label="{Name}" soc="{Soc}" series="{Series}" style="align-self:start; padding:8"/>
            """, vm);
        using var bmp = Render(s);
        var box = Boxes(s, "Cells")[0];
        var w = (Cells)box.Widget!;
        Assert.Equal("Cell-A", w.Label);
        Assert.Equal(72.5, w.Soc);
        Assert.Same(vm.Series, w.Series);
        Assert.Equal(new SKRect(box.Rect.Left + 8, box.Rect.Top + 8, box.Rect.Right - 8, box.Rect.Bottom - 8), w.Bounds);

        // 잘림 — 상자 안은 빨강, 상자 밖(오른쪽 아래)은 흰 배경 그대로
        Assert.Equal(SKColors.Red, bmp.GetPixel((int)box.Rect.MidX, (int)box.Rect.MidY));
        Assert.Equal(SKColors.White, bmp.GetPixel((int)box.Rect.Right + 20, (int)box.Rect.Bottom + 20));
    }

    [Fact]
    public void Color_FollowsTheme()
    {
        var light = Surface("<Cells/>", theme: DvTheme.Light);
        using (Render(light)) { }
        var dark = Surface("<Cells/>", theme: DvTheme.Dark);
        using (Render(dark)) { }

        static SKColor Token(DvTheme t) => t.TryGet("primary", out var raw) && DvColor.TryParse(raw, out var c) ? c.ToSK() : default;
        Assert.Equal(Token(DvTheme.Light), ((Cells)Boxes(light, "Cells")[0].Widget!).Primary);
        Assert.Equal(Token(DvTheme.Dark), ((Cells)Boxes(dark, "Cells")[0].Widget!).Primary);
    }

    public sealed class Ink : DvWidget
    {
        public SKColor Text;
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) => Text = ctx.TextColor;
    }

    [Fact]
    public void TextColor_IsThemeText_UnlessStyleSetsColor()
    {
        var r = new ComponentRegistry();
        r.RegisterWidget<Ink>("Ink");
        var s = new DvSurface("<DvScreen><DvStack><Ink/><Ink style=\"color:#ff0000\"/></DvStack></DvScreen>", new Vm(), DvTheme.Dark, r);
        using (Render(s)) { }
        var inks = Boxes(s, "Ink").Select(b => ((Ink)b.Widget!).Text).ToList();
        Assert.True(DvTheme.Dark.TryGet("text", out var raw));
        Assert.True(DvColor.TryParse(raw, out var text));
        Assert.Equal(text.ToSK(), inks[0]);
        Assert.Equal(new SKColor(0xff, 0, 0), inks[1]);
    }

    [Fact]
    public void PaintException_IsReportedOnce_AndScreenStillPaints()
    {
        var s = Surface("""
            <Boom style="align-self:start"/>
            <Cells cells="3" style="align-self:start"/>
            """);
        var errors = new List<DvHandlerErrorEventArgs>();
        s.HandlerError += (_, e) => errors.Add(e);
        using (Render(s)) { }
        using (Render(s)) { }
        Assert.Single(errors);
        Assert.Equal("Paint", errors[0].Kind);
        Assert.Equal("Boom", errors[0].Tag);
        Assert.Equal(2, ((Cells)Boxes(s, "Cells")[0].Widget!).Painted); // 옆 위젯은 두 프레임 모두 그려짐
    }

    [Fact]
    public void Instance_IsKeptAcrossRebuilds()
    {
        var vm = new Vm();
        var s = Surface("<Cells label=\"{Name}\"/>", vm);
        s.Layout(600, 400);
        var first = Boxes(s, "Cells")[0].Widget;
        vm.Name = "Cell-B";
        s.Invalidate();
        s.Layout(600, 400);
        Assert.Same(first, Boxes(s, "Cells")[0].Widget);
    }

    [Fact]
    public void Repeat_EachItemHasItsOwnInstance_KeptAcrossRebuilds()
    {
        var s = Surface("<DvRepeat items=\"{Items}\"><Cells label=\"{Item}\"/></DvRepeat>");
        s.Layout(600, 400);
        var first = Boxes(s, "Cells").Select(b => b.Widget).ToList();
        Assert.Equal(3, first.Distinct().Count());
        s.Invalidate();
        s.Layout(600, 400);
        Assert.Equal(first, Boxes(s, "Cells").Select(b => b.Widget).ToList());
    }

    [Fact]
    public void FrameInterval_DrivesNextAnimationDue()
    {
        var still = Surface("<Spinner/>");
        using (Render(still)) { }
        Assert.Null(still.NextAnimationDue(1000));

        var spin = Surface("<Spinner spin=\"true\"/>");
        using (Render(spin)) { }
        Assert.Equal(1050, spin.NextAnimationDue(1000));
    }

    [Fact]
    public void Onclick_Works_LikeAnyElement()
    {
        var vm = new Vm();
        var s = Surface("<Cells cells=\"4\" onclick=\"Hit\" style=\"align-self:start\"/>", vm);
        s.Layout(600, 400);
        var r = Boxes(s, "Cells")[0].Rect;
        s.PointerDown(new SKPoint(r.MidX, r.MidY));
        s.PointerUp(new SKPoint(r.MidX, r.MidY));
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Children_AreIgnored_WithWarning()
    {
        var cap = new Capture("child elements");
        Trace.Listeners.Add(cap);
        try
        {
            var s = Surface("<Cells><DvLabel text=\"CHILD-W1\"/></Cells>");
            s.Layout(600, 400);
            Assert.Empty(Boxes(s, "Cells")[0].Children);
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        Assert.Contains(cap.Lines, m => m.Contains("<Cells>"));
    }

    [Fact]
    public void Register_RejectsBuiltInAndComponentNames()
    {
        var r = new ComponentRegistry();
        Assert.Throws<ArgumentException>(() => r.RegisterWidget<Plain>("DvButton"));
        Assert.Throws<ArgumentException>(() => r.RegisterWidget<Plain>(""));

        r.Register("""<DvComponent name="Card1"><DvBox/></DvComponent>""");
        Assert.Throws<ArgumentException>(() => r.RegisterWidget<Plain>("Card1"));

        r.RegisterWidget<Plain>("Gadget");
        Assert.Throws<ArgumentException>(() => r.Register("""<DvComponent name="Gadget"><DvBox/></DvComponent>"""));
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
