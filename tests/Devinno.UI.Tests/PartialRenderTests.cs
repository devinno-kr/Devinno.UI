using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Controls;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 부분 다시 그리기(PartialRender) — 시간으로만 바뀌는 프레임(흐름 애니메이션·점멸)은 그 영역만 다시 그린다.
// 정확성 = 같은 시각에 화면 전체를 다시 그린 것과 픽셀이 같다(이전 프레임을 지우지 않고 유지하는 캔버스에서).
// 단 잘라 그린 가장자리를 도형이 지나면 안티앨리어싱 반올림이 채널당 1 단계쯤 다를 수 있어(Skia) 2 단계까지 같은 것으로 본다.
public class PartialRenderTests
{
    private const int W = 900, H = 600;

    public sealed class Vm
    {
        public bool Run { get; set; } = true;
    }

    // 머리글·점멸 램프·스크롤 상자 안 공정도(흐름 애니메이션)·정적인 아래 글자
    private const string Screen = """
        <DvScreen style="padding:16; gap:12; align-items:start">
          <DvRow gap="md" align="center">
            <DvLabel class="title" text="부분 다시 그리기"/>
            <DvLamp on="true" blink="true" blink-interval="200"/>
          </DvRow>
          <DvStack style="height:300; overflow:scroll">
            <DvLabel text="위 여백"/>
            <DvProcess flow-animation="true" look="rich" style="height:260">
              <DvTerminal name="IN" text="IN"/>
              <DvPump name="P1" running="{Run}"/>
              <DvValve name="V1" open="true"/>
              <DvTerminal name="OUT" text="OUT"/>
              <DvLine from="IN" to="P1.in"/>
              <DvLine from="P1.out" to="V1.in"/>
              <DvLine from="V1.out" to="OUT"/>
            </DvProcess>
            <DvLabel text="아래 여백" style="height:400"/>
          </DvStack>
          <DvLabel text="정적인 글자"/>
        </DvScreen>
        """;

    private static DvSurface Make(string markup = Screen) => new(markup, new Vm(), DvTheme.Dark);

    // a = 부분 다시 그리기(같은 비트맵 유지), b = 매번 새 비트맵에 전체. 프레임마다 두 그림이 같아야 한다
    private static List<SKRectI?> Compare(DvSurface a, DvSurface b, int frames, Action<DvSurface, int>? step = null, int w = W, int h = H)
    {
        a.PartialRender = true;
        using var bmpA = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        var rects = new List<SKRectI?>();
        for (var i = 0; i < frames; i++)
        {
            step?.Invoke(a, i);
            step?.Invoke(b, i);
            var t = 1000 + i * 50L;
            using (var ca = new SKCanvas(bmpA))
                a.RenderTo(ca, w, h, t);
            rects.Add(a.LastRenderRect);

            using var bmpB = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var cb = new SKCanvas(bmpB))
                b.RenderTo(cb, w, h, t);
            Assert.Null(b.LastRenderRect); // 끄면 언제나 전체
            Assert.True(Same(bmpA, bmpB), $"frame {i} rect {a.LastRenderRect} 이 전체 다시 그리기와 다름 — 다른 곳 {Diff(bmpA, bmpB)} {Px(bmpA, bmpB)}");
        }

        return rects;
    }

    private const int Tolerance = 2;

    private static bool Same(SKBitmap a, SKBitmap b)
    {
        var x = a.GetPixelSpan();
        var y = b.GetPixelSpan();
        for (var i = 0; i < x.Length; i++)
        {
            if (Math.Abs(x[i] - y[i]) > Tolerance)
                return false;
        }

        return true;
    }

    // 2 단계 넘게 다른 픽셀을 감싸는 사각형(실패 메시지용)
    private static SKRectI Diff(SKBitmap a, SKBitmap b)
    {
        int l = int.MaxValue, t = int.MaxValue, r = -1, btm = -1;
        for (var y = 0; y < a.Height; y++)
            for (var x = 0; x < a.Width; x++)
                if (Far(a.GetPixel(x, y), b.GetPixel(x, y)))
                {
                    l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x); btm = Math.Max(btm, y);
                }
        return r < 0 ? SKRectI.Empty : new SKRectI(l, t, r + 1, btm + 1);
    }

    private static bool Far(SKColor p, SKColor q) =>
        Math.Abs(p.Red - q.Red) > Tolerance || Math.Abs(p.Green - q.Green) > Tolerance || Math.Abs(p.Blue - q.Blue) > Tolerance || Math.Abs(p.Alpha - q.Alpha) > Tolerance;

    private static string Px(SKBitmap a, SKBitmap b)
    {
        var d = Diff(a, b);
        return d.IsEmpty ? "" : $"A={a.GetPixel(d.Left, d.Top)} B={b.GetPixel(d.Left, d.Top)}";
    }

    private static bool Small(SKRectI? r) => r is { } x && !x.IsEmpty && x.Width * x.Height < W * H / 2;

    [Fact]
    public void AnimationOnly_RepaintsArea_PixelIdentical()
    {
        var rects = Compare(Make(), Make(), 12);
        Assert.Null(rects[0]); // 첫 프레임은 전체
        Assert.All(rects.Skip(1), r => Assert.True(Small(r), $"{r}"));
    }

    [Fact]
    public void Scrolled_ProcessArea_FollowsScroll()
    {
        var a = Make();
        var rects = Compare(a, Make(), 12, (s, i) =>
        {
            if (i == 4)
                s.PointerWheel(new SKPoint(300, 250), -3f); // 스크롤 상자 안에서 아래로
        });
        var scroller = Flatten(a.Root!).Single(b => b.IsScroller);
        Assert.True(scroller.ScrollY > 3 * 24, $"스크롤 {scroller.ScrollY} — 번짐 여유(24)보다 크게 움직여야 검사가 된다");
        Assert.Null(rects[4]); // 입력이 있던 프레임은 전체
        Assert.All(rects.Skip(5), r => Assert.True(Small(r), $"{r}"));
    }

    private static IEnumerable<Devinno.UI.Layout.LayoutBox> Flatten(Devinno.UI.Layout.LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Scrolled_ProcessOnly_AreaMovesWithScroll()
    {
        // 램프 없이 — 영역이 공정도 하나뿐이라 스크롤을 빼먹으면 어긋난다
        var markup = Screen.Replace("""<DvLamp on="true" blink="true" blink-interval="200"/>""", "");
        var a = Make(markup);
        var rects = Compare(a, Make(markup), 10, (s, i) =>
        {
            if (i == 3)
                s.PointerWheel(new SKPoint(300, 250), -4f);
        });
        var scroller = Flatten(a.Root!).Single(b => b.IsScroller);
        var process = Flatten(a.Root!).Single(b => b.Process is not null);
        Assert.True(scroller.ScrollY > 60, $"{scroller.ScrollY}");
        var r = rects[^1]!.Value;
        Assert.True(r.Top >= scroller.Rect.Top - 1, $"보이는 칸 위로 나가지 않는다 {r} {scroller.Rect}"); // 바깥으로 정수 픽셀 반올림
        Assert.True(r.Bottom <= process.Rect.Bottom - scroller.ScrollY + 24 + 1, $"스크롤만큼 올라간다 {r}");
    }

    [Fact]
    public void Invalidate_And_InputState_FullFrame()
    {
        var rects = Compare(Make(), Make(), 10, (s, i) =>
        {
            if (i == 3)
                s.Invalidate();
            if (i == 6)
                s.PointerMove(new SKPoint(40, 40)); // hover
        });
        Assert.Null(rects[3]);
        Assert.True(Small(rects[4]));
    }

    [Fact]
    public void SizeChange_FullFrame()
    {
        var s = Make();
        s.PartialRender = true;
        using var bmp = new SKBitmap(W, H);
        using (var c = new SKCanvas(bmp))
        {
            s.RenderTo(c, W, H, 0);
            s.RenderTo(c, W, H, 50);
            Assert.True(Small(s.LastRenderRect));
            s.RenderTo(c, W - 20, H, 100);
            Assert.Null(s.LastRenderRect);
        }
    }

    [Fact]
    public void NothingAnimating_RepaintsNothing()
    {
        var s = new DvSurface("<DvScreen><DvLabel text=\"정적\"/></DvScreen>", null, DvTheme.Light);
        s.PartialRender = true;
        using var bmp = new SKBitmap(W, H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H, 0);
        Assert.Null(s.LastRenderRect);
        s.RenderTo(c, W, H, 50);
        Assert.Equal(SKRectI.Empty, s.LastRenderRect);
    }

    [Fact]
    public void Off_ByDefault()
    {
        var s = Make();
        Assert.False(s.PartialRender);
        using var bmp = new SKBitmap(W, H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H, 0);
        s.RenderTo(c, W, H, 50);
        Assert.Null(s.LastRenderRect);
    }

    // ── DvApp — 페이지 칸 안의 애니메이션, 위 고정 영역 ──

    private sealed class Page(string markup) : DvPage, IDvControlHost
    {
        public bool Run { get; set; } = true;
        protected override string MarkupSource => markup;
        void IDvControlHost.Attach(DvControlLookup controls) { }
    }

    private sealed class Bar(string markup) : DvBar, IDvControlHost
    {
        protected override string MarkupSource => markup;
        void IDvControlHost.Attach(DvControlLookup controls) { }
    }

    private static DvApp MakeApp()
    {
        var app = new DvApp { Theme = DvTheme.Dark, Top = new Bar("<DvStack style=\"height:56; padding:12\"><DvLabel class=\"title\" text=\"TOP\"/></DvStack>") };
        app.Pages.Add(new Page(Screen));
        return app;
    }

    [Fact]
    public void DvApp_PageSlot_PixelIdentical()
    {
        var rects = Compare(MakeApp().Surface, MakeApp().Surface, 12);
        Assert.Null(rects[0]);
        Assert.All(rects.Skip(2), r => Assert.True(Small(r), $"{r}"));
        Assert.All(rects.Skip(2), r => Assert.True(r!.Value.Top >= 56, "페이지 칸(위 고정 영역 아래) 안"));
    }

    [Fact]
    public void DvApp_PageSwitch_FullFrame()
    {
        var a = MakeApp();
        var b = MakeApp();
        var rects = Compare(a.Surface, b.Surface, 8, (s, i) =>
        {
            if (i == 4)
            {
                var app = ReferenceEquals(s, a.Surface) ? a : b;
                app.Show(new Page("<DvScreen><DvLabel text=\"다른 페이지\"/><DvLamp on=\"true\" blink=\"true\"/></DvScreen>"));
            }
        });
        Assert.Null(rects[4]);
    }

    // 페이지 칸(자식 화면) 안의 관성 스크롤 — 루트는 자기 관성만 보면 "시간으로만 바뀌는 프레임"으로 잘못 알고 잘라 그린다(화면이 뚝뚝 끊김)
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DvApp_ChildFling_IsFullFrame(bool withAnimation)
    {
        var markup = withAnimation ? Screen : Screen.Replace("""<DvLamp on="true" blink="true" blink-interval="200"/>""", "").Replace("flow-animation=\"true\"", "");
        var app = new DvApp { Theme = DvTheme.Dark };
        app.Pages.Add(new Page(markup));
        var s = app.Surface;
        s.PartialRender = true;
        using var bmp = new SKBitmap(W, H);
        void Frame()
        {
            using var c = new SKCanvas(bmp);
            s.RenderTo(c, W, H, Environment.TickCount64);
        }

        Frame();
        Frame(); // 부분 다시 그리기가 켜진 상태

        // 스크롤 상자 안에서 빠르게 위로 끌었다 놓는다(실제 시간 — 관성 속도 계산)
        var p = new SKPoint(300, 300);
        s.PointerDown(p);
        for (var i = 1; i <= 6; i++)
        {
            Thread.Sleep(8);
            s.PointerMove(new SKPoint(300, 300 - i * 25));
        }

        s.PointerUp(new SKPoint(300, 150));
        Frame(); // 놓은 프레임(입력이 있었으니 전체)
        Assert.NotNull(s.NextAnimationDue(Environment.TickCount64)); // 관성이 진행 중
        Thread.Sleep(16);
        Frame();
        Assert.Null(s.LastRenderRect); // 관성으로 내용이 움직이는 프레임 — 전체
    }

    // ── 화면 배율(P7-3) — 어댑터는 물리 px 비트맵에 배율을 곱한 캔버스로 논리 px 레이아웃을 그린다. 부분 다시 그리기도 전체와 같아야 한다 ──

    [Theory]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void Scaled_canvas_partial_frames_match_full_frames(float scale)
    {
        const int lw = 600, lh = 400;
        int pw = (int)MathF.Round(lw * scale), ph = (int)MathF.Round(lh * scale);
        var a = Make();
        var b = Make();
        a.PartialRender = true;
        using var bmpA = new SKBitmap(pw, ph, SKColorType.Rgba8888, SKAlphaType.Premul);
        var partial = 0;
        for (var i = 0; i < 24; i++)
        {
            var t = 1000 + i * 50L;
            using (var ca = new SKCanvas(bmpA))
            {
                ca.Scale(scale);
                a.RenderTo(ca, lw, lh, t);
            }
            if (a.LastRenderRect is { IsEmpty: false })
                partial++;

            using var bmpB = new SKBitmap(pw, ph, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var cb = new SKCanvas(bmpB))
            {
                cb.Scale(scale);
                b.RenderTo(cb, lw, lh, t);
            }
            Assert.True(Same(bmpA, bmpB), $"x{scale} frame {i} rect {a.LastRenderRect} 이 전체 다시 그리기와 다름 — 다른 곳 {Diff(bmpA, bmpB)} {Px(bmpA, bmpB)}");
        }
        Assert.True(partial > 0); // 실제로 부분 다시 그리기가 일어났다
    }
}
