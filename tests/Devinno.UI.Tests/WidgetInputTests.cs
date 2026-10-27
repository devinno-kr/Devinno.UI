using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 커스텀 위젯 2단계 — 포인터 입력(누름에서 잡으면 이동·뗌·취소를 받음)과 value 양방향 쓰기(ctx.Commit, onbeforecommit 게이트)
public class WidgetInputTests
{
    public sealed class Row
    {
        public double X { get; set; } = 10;
    }

    public sealed class Vm
    {
        public double Level { get; set; } = 20;
        public bool Show { get; set; } = true;
        public List<Row> Rows { get; } = [new(), new()];
        public int Clicks;
        public void Hit() => Clicks++;
        public double? Proposed;
        public bool Allow = true;
        public bool Gate(double proposed)
        {
            Proposed = proposed;
            return Allow;
        }
    }

    // 가로 끌기 = 0–100. 끄는 동안은 필드(미리보기)만, 뗄 때 Commit
    public sealed class Dial : DvWidget
    {
        public double? Preview;
        public bool Committed;
        public bool CanCommitSeen;
        public int Moves, Cancels;
        public SKRect PaintBounds;

        public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(200, 40);

        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) => PaintBounds = bounds;

        public override bool OnPointerDown(DvWidgetContext ctx, SKPoint p)
        {
            CanCommitSeen = ctx.CanCommit;
            Preview = At(ctx, p);
            return true;
        }

        public override void OnPointerMove(DvWidgetContext ctx, SKPoint p)
        {
            Moves++;
            Preview = At(ctx, p);
        }

        public override void OnPointerUp(DvWidgetContext ctx, SKPoint p)
        {
            Committed = ctx.Commit(At(ctx, p));
            Preview = null;
        }

        public override void OnPointerCancel(DvWidgetContext ctx)
        {
            Cancels++;
            Preview = null;
        }

        private static double At(DvWidgetContext ctx, SKPoint p) =>
            Math.Round(Math.Clamp((p.X - ctx.Bounds.Left) / ctx.Bounds.Width, 0, 1) * 100);
    }

    public sealed class Faulty : DvWidget
    {
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) { }
        public override bool OnPointerDown(DvWidgetContext ctx, SKPoint p) => throw new InvalidOperationException("down");
    }

    private static ComponentRegistry Registry()
    {
        var r = new ComponentRegistry();
        r.RegisterWidget<Dial>("Dial");
        r.RegisterWidget<Faulty>("Faulty");
        r.Register("""
            <DvComponent name="DialCard">
              <DvParam name="V"/>
              <DvCard title="dial"><Dial value="{V}"/></DvCard>
            </DvComponent>
            """);
        return r;
    }

    private static DvSurface Start(string body, Vm vm)
    {
        var s = new DvSurface($"<DvScreen><DvStack align=\"start\">{body}</DvStack></DvScreen>", vm, DvTheme.Light, Registry());
        s.Layout(800, 600);
        return s;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static List<LayoutBox> Dials(DvSurface s) => Flatten(s.Root!).Where(b => b.Node.Tag == "Dial").ToList();

    private static SKPoint At(LayoutBox b, double fraction) => new(b.Rect.Left + (float)(b.Rect.Width * fraction), b.Rect.MidY);

    [Fact]
    public void Drag_PreviewsInWidget_ThenCommitsOnRelease()
    {
        var vm = new Vm();
        var s = Start("<Dial value=\"{Bind Level}\"/>", vm);
        var box = Dials(s)[0];
        var dial = (Dial)box.Widget!;

        s.PointerDown(At(box, 0.1));
        Assert.True(dial.CanCommitSeen);
        s.PointerMove(At(box, 0.5));
        s.PointerMove(At(box, 0.75));
        Assert.Equal(75, dial.Preview);
        Assert.Equal(20, vm.Level); // 끄는 동안은 VM 그대로
        Assert.True(s.NeedsRender);

        s.PointerUp(At(box, 0.75));
        Assert.True(dial.Committed);
        Assert.Equal(75, vm.Level);
    }

    [Fact]
    public void Capture_SwallowsClick()
    {
        var vm = new Vm();
        var s = Start("<Dial value=\"{Bind Level}\" onclick=\"Hit\"/>", vm);
        var box = Dials(s)[0];
        s.PointerDown(At(box, 0.5));
        s.PointerUp(At(box, 0.5));
        Assert.Equal(0, vm.Clicks);
        Assert.Equal(50, vm.Level);
    }

    [Fact]
    public void Disabled_GetsNoPointerCalls()
    {
        var vm = new Vm();
        var s = Start("<Dial value=\"{Bind Level}\" disabled=\"true\"/>", vm);
        var box = Dials(s)[0];
        s.PointerDown(At(box, 0.5));
        s.PointerUp(At(box, 0.5));
        Assert.Null(((Dial)box.Widget!).Preview);
        Assert.False(((Dial)box.Widget!).Committed);
        Assert.Equal(20, vm.Level);
    }

    [Fact]
    public void OneWayValue_CannotCommit()
    {
        var vm = new Vm();
        var s = Start("<Dial value=\"{Level}\"/>", vm);
        var box = Dials(s)[0];
        s.PointerDown(At(box, 0.2));
        s.PointerUp(At(box, 0.9));
        var dial = (Dial)box.Widget!;
        Assert.False(dial.CanCommitSeen);
        Assert.False(dial.Committed);
        Assert.Equal(20, vm.Level);
    }

    [Theory]
    [InlineData(true, 60)]
    [InlineData(false, 20)]
    public void BeforeCommitGate_IsAsked(bool allow, double expected)
    {
        var vm = new Vm { Allow = allow };
        var s = Start("<Dial value=\"{Bind Level}\" onbeforecommit=\"Gate\"/>", vm);
        var box = Dials(s)[0];
        s.PointerDown(At(box, 0.6));
        s.PointerUp(At(box, 0.6));
        Assert.Equal(60, vm.Proposed);
        Assert.Equal(expected, vm.Level);
        Assert.Equal(allow, ((Dial)box.Widget!).Committed);
    }

    [Fact]
    public void PointerException_IsReported_AndNothingCaptured()
    {
        var vm = new Vm();
        var s = Start("<Faulty onclick=\"Hit\"/>", vm);
        var errors = new List<DvHandlerErrorEventArgs>();
        s.HandlerError += (_, e) => errors.Add(e);
        var r = Flatten(s.Root!).First(b => b.Node.Tag == "Faulty").Rect;
        s.PointerDown(new SKPoint(r.MidX, r.MidY));
        s.PointerUp(new SKPoint(r.MidX, r.MidY));
        Assert.Single(errors);
        Assert.Equal("Pointer", errors[0].Kind);
        Assert.Equal(1, vm.Clicks); // 잡지 않았으니 보통 클릭으로
    }

    [Fact]
    public void RebuildDuringDrag_KeepsTheDrag()
    {
        var vm = new Vm();
        var s = Start("<Dial value=\"{Bind Level}\"/>", vm);
        var box = Dials(s)[0];
        s.PointerDown(At(box, 0.1));
        s.Invalidate();
        s.Layout(800, 600); // 끄는 중 재빌드(폴링 Invalidate 등)
        s.PointerMove(At(box, 0.4));
        s.PointerUp(At(box, 0.4));
        Assert.Equal(40, vm.Level);
    }

    [Fact]
    public void WidgetGone_DuringDrag_IsCancelled()
    {
        var vm = new Vm();
        var s = Start("<Dial value=\"{Bind Level}\" visible=\"{Show}\"/>", vm);
        var box = Dials(s)[0];
        var dial = (Dial)box.Widget!;
        s.PointerDown(At(box, 0.3));
        vm.Show = false;
        s.Invalidate();
        s.Layout(800, 600);
        s.PointerUp(At(box, 0.9));
        Assert.Equal(1, dial.Cancels);
        Assert.False(dial.Committed);
        Assert.Equal(20, vm.Level);
    }

    [Fact]
    public void InRepeat_CommitsToTheItem()
    {
        var vm = new Vm();
        var s = Start("<DvRepeat items=\"{Rows}\"><Dial value=\"{Bind Item.X}\"/></DvRepeat>", vm);
        var second = Dials(s)[1];
        s.PointerDown(At(second, 0.3));
        s.PointerUp(At(second, 0.3));
        Assert.Equal(10, vm.Rows[0].X);
        Assert.Equal(30, vm.Rows[1].X);
    }

    [Fact]
    public void InComponent_TwoWayParameter_WritesOuterVm()
    {
        var vm = new Vm();
        var s = Start("<DialCard V=\"{Bind Level}\"/>", vm);
        var box = Dials(s)[0];
        s.PointerDown(At(box, 0.8));
        s.PointerUp(At(box, 0.8));
        Assert.Equal(80, vm.Level);
    }

    [Fact]
    public void Bounds_IsTheContentArea_SameAsPaint()
    {
        var vm = new Vm();
        var s = Start("<Dial value=\"{Bind Level}\" style=\"padding:20\"/>", vm);
        using (var bmp = new SKBitmap(800, 600))
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 800, 600);
        var box = Dials(s)[0];
        var dial = (Dial)box.Widget!;
        // 내용 영역 왼쪽 끝(패딩 안) = 0
        s.PointerDown(new SKPoint(dial.PaintBounds.Left, box.Rect.MidY));
        s.PointerUp(new SKPoint(dial.PaintBounds.Left, box.Rect.MidY));
        Assert.Equal(0, vm.Level);
    }
}
