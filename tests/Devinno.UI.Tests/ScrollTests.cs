using Devinno.UI;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// overflow:scroll — 레이아웃·히트테스트·휠·드래그·손잡이·관성·보존
public class ScrollTests
{
    public sealed class Vm
    {
        public int Clicks;
        public double Level { get; set; } = 40;
        public void Hit() => Clicks++;
    }

    // 300×300 화면, 스크롤 상자 = 위에서 0, 높이 100, 자식 6개 × 50 (내용 300 → 최대 200)
    private static string List(string extra = "", int n = 6)
        => "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\">" +
           $"<DvBox style=\"height:100; overflow:scroll; display:flex; flex-direction:column; align-items:stretch{extra}\">" +
           string.Concat(Enumerable.Range(0, n).Select(i => $"<DvBox style=\"height:50\" onclick=\"Hit\"/>")) +
           "</DvBox><DvBox style=\"height:50\"/></DvScreen>";

    private static (DvSurface s, Vm vm, long[] clock) Surface(string markup)
    {
        var vm = new Vm();
        var clock = new long[] { 1000 };
        var s = new DvSurface(markup, vm) { Clock = () => clock[0] };
        s.Layout(300, 300);
        return (s, vm, clock);
    }

    private static LayoutBox Scroller(DvSurface s) => s.Root!.Children[0];

    [Fact]
    public void Parses_overflow_scroll()
    {
        var b = new LayoutEngine().Layout(MarkupParser.Parse("<DvBox style=\"overflow:scroll\"/>"), null, 10, 10);
        Assert.Equal(Overflow.Scroll, b.Style.Overflow);
    }

    [Fact]
    public void Content_extent_and_max_offset()
    {
        var (s, _, _) = Surface(List());
        var sc = Scroller(s);
        Assert.Equal(100, sc.Rect.Height, 3);
        Assert.Equal(300, sc.ContentHeight, 3);
        Assert.Equal(200, sc.MaxScrollY, 3);
        Assert.Equal(0, sc.MaxScrollX, 3);
    }

    [Fact]
    public void Vertical_overflow_reserves_a_gutter_so_content_does_not_touch_the_bar()
    {
        var (s, _, _) = Surface(List());
        var sc = Scroller(s);
        var gutter = ScrollBars.Gutter;
        Assert.All(sc.Children, c => Assert.Equal(sc.Rect.Right - gutter, c.Rect.Right, 3));
        Assert.True(ScrollBars.VerticalThumb(sc)!.Value.Left >= sc.Children[0].Rect.Right);
    }

    [Fact]
    public void No_gutter_when_content_fits()
    {
        var (s, _, _) = Surface(List(n: 2)); // 내용 100 = 뷰포트 100
        var sc = Scroller(s);
        Assert.Equal(0, sc.MaxScrollY, 3);
        Assert.All(sc.Children, c => Assert.Equal(sc.Rect.Right, c.Rect.Right, 3));
    }

    [Fact]
    public void Scroll_box_does_not_grow_with_its_content()
    {
        // grow 로 남은 공간만 차지 — 내용(300) 때문에 늘어나지 않는다
        var (s, _, _) = Surface("<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\">" +
                                "<DvBox style=\"height:50\"/>" +
                                "<DvBox style=\"overflow:scroll; flex-grow:1; display:flex; flex-direction:column\">" +
                                string.Concat(Enumerable.Repeat("<DvBox style=\"height:100\"/>", 6)) + "</DvBox></DvScreen>");
        var sc = s.Root!.Children[1];
        Assert.Equal(250, sc.Rect.Height, 3);
        Assert.Equal(350, sc.MaxScrollY, 3);
    }

    [Fact]
    public void Hit_test_applies_scroll_offset_and_viewport_clip()
    {
        var (s, _, _) = Surface(List());
        var sc = Scroller(s);
        sc.ScrollY = 60;
        // 화면 y=10 → 내용 y=70 → 두 번째 자식(50~100)
        Assert.Same(sc.Children[1], HitTester.HitTarget(s.Root!, new SKPoint(20, 10)));
        // 뷰포트 아래(y=120)는 스크롤 자식이 아니라 다음 형제
        Assert.Same(s.Root!.Children[1], HitTester.HitTest(s.Root!, new SKPoint(20, 120)));
    }

    [Fact]
    public void Wheel_scrolls_48_per_notch_and_clamps()
    {
        var (s, _, _) = Surface(List());
        s.PointerWheel(new SKPoint(20, 50), -1);
        Assert.Equal(48, Scroller(s).ScrollY, 3);
        s.PointerWheel(new SKPoint(20, 50), -10);
        Assert.Equal(200, Scroller(s).ScrollY, 3);
        s.PointerWheel(new SKPoint(20, 50), 100);
        Assert.Equal(0, Scroller(s).ScrollY, 3);
        Assert.True(s.NeedsRender);
    }

    [Fact]
    public void Wheel_outside_any_scroll_box_does_nothing()
    {
        var (s, _, _) = Surface(List());
        s.PointerWheel(new SKPoint(20, 120), -1);
        Assert.Equal(0, Scroller(s).ScrollY, 3);
    }

    [Fact]
    public void Small_move_is_still_a_click()
    {
        var (s, vm, _) = Surface(List());
        s.PointerDown(new SKPoint(20, 20));
        s.PointerMove(new SKPoint(20, 25));
        s.PointerUp(new SKPoint(20, 25));
        Assert.Equal(1, vm.Clicks);
        Assert.Equal(0, Scroller(s).ScrollY, 3);
    }

    [Fact]
    public void Drag_scrolls_and_cancels_the_click()
    {
        var (s, vm, clock) = Surface(List());
        s.PointerDown(new SKPoint(20, 80));
        clock[0] += 500; // 천천히 → 관성 없음
        s.PointerMove(new SKPoint(20, 60));
        s.PointerMove(new SKPoint(20, 50));
        clock[0] += 500;
        s.PointerUp(new SKPoint(20, 50));
        Assert.Equal(30, Scroller(s).ScrollY, 3);
        Assert.Equal(0, vm.Clicks);
    }

    [Fact]
    public void Thumb_drag_maps_track_to_content()
    {
        var (s, _, _) = Surface(List());
        var sc = Scroller(s);
        var thumb = ScrollBars.VerticalThumb(sc)!.Value;
        // 손잡이 길이 = 100×100/300 ≈ 33.3, 이동 가능 = 100 − 33.3 → 손잡이 33.3 이동 = 내용 100
        var travel = sc.Rect.Height - thumb.Height;
        s.PointerDown(new SKPoint(thumb.MidX, thumb.MidY));
        s.PointerMove(new SKPoint(thumb.MidX, thumb.MidY + travel / 2));
        s.PointerUp(new SKPoint(thumb.MidX, thumb.MidY + travel / 2));
        Assert.Equal(100, sc.ScrollY, 1);
    }

    [Fact]
    public void Knob_inside_scroll_box_drags_the_knob_not_the_list()
    {
        var (s, vm, _) = Surface("<DvScreen style=\"padding:0\"><DvBox style=\"height:100; overflow:scroll; display:flex; flex-direction:column\">" +
                                 "<DvKnob value=\"{Bind Data.Level}\" style=\"width:100; height:100\"/><DvBox style=\"height:200\"/></DvBox></DvScreen>");
        s.PointerDown(new SKPoint(50, 10));
        s.PointerMove(new SKPoint(90, 50));
        s.PointerUp(new SKPoint(90, 50));
        Assert.Equal(0, Scroller(s).ScrollY, 3);
        Assert.NotEqual(40, vm.Level);
    }

    [Fact]
    public void Fast_flick_keeps_moving_after_release_then_stops()
    {
        var (s, _, clock) = Surface(List(n: 40)); // 내용 2000
        s.PointerDown(new SKPoint(20, 90));
        clock[0] += 10; s.PointerMove(new SKPoint(20, 70));
        clock[0] += 10; s.PointerMove(new SKPoint(20, 50));
        clock[0] += 10; s.PointerUp(new SKPoint(20, 30));
        var released = Scroller(s).ScrollY;
        Assert.Equal(60, released, 3);
        Assert.NotNull(s.NextAnimationDue(clock[0]));

        clock[0] += 300;
        Render(s, clock[0]);
        Assert.True(Scroller(s).ScrollY > released, $"{Scroller(s).ScrollY}");

        clock[0] += 60_000;
        Render(s, clock[0]);
        Assert.Null(s.NextAnimationDue(clock[0]));
        var stopped = Scroller(s).ScrollY;
        Assert.InRange(stopped, released, Scroller(s).MaxScrollY);
    }

    [Fact]
    public void Touching_again_stops_the_fling()
    {
        var (s, _, clock) = Surface(List(n: 40));
        s.PointerDown(new SKPoint(20, 90));
        clock[0] += 10; s.PointerMove(new SKPoint(20, 50));
        clock[0] += 10; s.PointerUp(new SKPoint(20, 10));
        clock[0] += 50;
        Render(s, clock[0]);
        s.PointerDown(new SKPoint(20, 10));
        var held = Scroller(s).ScrollY;
        clock[0] += 500;
        Render(s, clock[0]);
        Assert.Equal(held, Scroller(s).ScrollY, 3);
        Assert.Null(s.NextAnimationDue(clock[0]));
    }

    [Fact]
    public void Offset_survives_invalidate_rebuild_and_clamps_on_resize()
    {
        var (s, _, _) = Surface(List());
        s.PointerWheel(new SKPoint(20, 50), -3); // 144
        s.Invalidate();
        s.Layout(300, 300);
        Assert.Equal(144, Scroller(s).ScrollY, 3);

        // 뷰포트가 커지면 최대 오프셋이 줄어 그에 맞춰 제한
        var (s2, _, _) = Surface("<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\">" +
                                 "<DvBox style=\"overflow:scroll; flex-grow:1; display:flex; flex-direction:column\">" +
                                 string.Concat(Enumerable.Repeat("<DvBox style=\"height:100\"/>", 6)) + "</DvBox></DvScreen>");
        s2.PointerWheel(new SKPoint(20, 50), -100); // 최대 300
        Assert.Equal(300, Scroller(s2).ScrollY, 3);
        s2.Layout(300, 500);
        Assert.Equal(100, Scroller(s2).ScrollY, 3);
    }

    [Fact]
    public void App_root_forwards_wheel_to_the_page()
    {
        var page = new MarkupPage(List());
        var (_, root) = TestApp.WithLeft(page, 50, 350, 300); // 페이지 칸 = x 50~350
        root.PointerWheel(new SKPoint(70, 50), -1);
        Assert.Equal(48, page.Surface!.Root!.Children[0].ScrollY, 3);
    }

    [Fact]
    public void Paint_clips_content_and_draws_a_thumb()
    {
        var markup = "<DvScreen style=\"padding:0; background:#000; display:flex; flex-direction:column; align-items:stretch\">" +
                     "<DvBox style=\"height:100; overflow:scroll; display:flex; flex-direction:column; align-items:stretch\">" +
                     string.Concat(Enumerable.Range(0, 6).Select(i => "<DvBox style=\"height:50; background:#ff0000\"/>")) +
                     "</DvBox></DvScreen>";
        var s = new DvSurface(markup, null, DvTheme.Dark);
        using var bmp = new SKBitmap(300, 300);
        using (var c = new SKCanvas(bmp)) s.RenderTo(c, 300, 300, 0);
        Assert.Equal(SKColors.Black, bmp.GetPixel(20, 150)); // 뷰포트(0~100) 밖은 잘림
        Assert.Equal(new SKColor(0xff, 0, 0), bmp.GetPixel(20, 50));
        var thumbPx = bmp.GetPixel(296, 10); // 오른쪽 가장자리 손잡이
        Assert.NotEqual(new SKColor(0xff, 0, 0), thumbPx);
    }

    private static void Render(DvSurface s, long ts)
    {
        using var bmp = new SKBitmap(300, 300);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 300, 300, ts);
    }
    // ── 스크롤 상자 측정(CSS 와 같게): 자연 높이 = 내용, max-height 로 제한, 모자라면 줄어든다 ──

    private static LayoutBox Lay(string markup, float w = 400, float h = 1000)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), null, w, h);

    private const string Content600 = "<DvBox style=\"height:600\"/>";

    [Fact]
    public void ScrollBox_NaturalHeight_IsContentHeight()
    {
        var root = Lay("<DvBox style=\"display:flex; flex-direction:column\"><DvBox>" +
                       "<DvBox style=\"overflow:scroll; padding:10\">" + Content600 + "</DvBox></DvBox></DvBox>");
        var sc = root.Children[0].Children[0];
        Assert.Equal(620, sc.Rect.Height, 3);
        Assert.Equal(0, sc.MaxScrollY, 3);
    }

    [Fact]
    public void ScrollBox_MaxHeight_Clamps()
    {
        var root = Lay("<DvBox style=\"display:flex; flex-direction:column\">" +
                       "<DvBox style=\"overflow:scroll; max-height:320; width:200\">" + Content600 + "</DvBox></DvBox>");
        var sc = root.Children[0];
        Assert.Equal(320, sc.Rect.Height, 3);
        Assert.True(sc.MaxScrollY > 0);
        Assert.Equal(280, sc.MaxScrollY, 3);
    }

    [Fact]
    public void AutoParent_ScrollTakesContentHeight()
    {
        // 의도된 변경(spec §8-9): 높이 auto 인 부모 안 스크롤 상자는 0 이 아니라 내용 높이
        var root = Lay("<DvBox style=\"display:flex; flex-direction:column\"><DvBox style=\"display:flex; flex-direction:column\">" +
                       "<DvBox style=\"overflow:scroll\">" + Content600 + "</DvBox></DvBox></DvBox>");
        Assert.Equal(600, root.Children[0].Rect.Height, 3);
        Assert.Equal(600, root.Children[0].Children[0].Rect.Height, 3);
    }

    [Fact]
    public void FixedParent_ScrollShrinksToFit()
    {
        var root = Lay("<DvBox style=\"display:flex; flex-direction:column\">" +
                       "<DvBox style=\"height:300; display:flex; flex-direction:column\">" +
                       "<DvBox style=\"overflow:scroll; width:200\">" + Content600 + "</DvBox>" +
                       "<DvBox style=\"height:40\"/></DvBox></DvBox>");
        var parent = root.Children[0];
        var sc = parent.Children[0];
        var button = parent.Children[1];
        Assert.Equal(260, sc.Rect.Height, 3);
        Assert.Equal(340, sc.MaxScrollY, 3);
        Assert.Equal(40, button.Rect.Height, 3);
        Assert.True(button.Rect.Bottom <= parent.Rect.Bottom + 0.001f);
    }

    [Fact]
    public void Shrink_StopsAtMinHeight()
    {
        // CSS 와 같게 — 줄어들어도 min-height 아래로는 가지 않는다(넘치면 부모 밖으로)
        var root = Lay("<DvBox style=\"display:flex; flex-direction:column\">" +
                       "<DvBox style=\"height:300; display:flex; flex-direction:column\">" +
                       "<DvBox style=\"overflow:scroll; min-height:280\">" + Content600 + "</DvBox>" +
                       "<DvBox style=\"height:40\"/></DvBox></DvBox>");
        Assert.Equal(280, root.Children[0].Children[0].Rect.Height, 3);
    }

    [Fact]
    public void ScrollWithExplicitShrink0_DoesNotShrink()
    {
        var root = Lay("<DvBox style=\"display:flex; flex-direction:column\">" +
                       "<DvBox style=\"height:300; display:flex; flex-direction:column\">" +
                       "<DvBox style=\"overflow:scroll; flex-shrink:0\">" + Content600 + "</DvBox>" +
                       "<DvBox style=\"height:40\"/></DvBox></DvBox>");
        Assert.Equal(600, root.Children[0].Children[0].Rect.Height, 3);
    }
}
