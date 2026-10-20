using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 모션②(P7-5) — opacity·transform 적용, CSS transition(상태가 바뀔 때 값을 서서히).
public class TransitionTests
{
    private const int W = 300, H = 200;

    public sealed class Vm
    {
        public string Bg { get; set; } = "#ff0000";
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static (DvSurface S, SKBitmap Bmp) Start(string body, Vm? vm = null)
    {
        var s = new DvSurface($"<DvScreen style=\"background:#ffffff\">{body}</DvScreen>", vm ?? new Vm(), DvTheme.Light);
        s.Layout(W, H);
        return (s, new SKBitmap(W, H));
    }

    private static SKColor Render(DvSurface s, SKBitmap bmp, long t, int x, int y)
    {
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, W, H, t);
        return bmp.GetPixel(x, y);
    }

    // ── 파싱 ──

    [Fact]
    public void Parses_transition_list_transform_and_easing()
    {
        var st = StyleParser.Parse("transition: background-color 200ms ease-out, opacity 0.3s linear 50ms; transform: translate(4, -2) scale(1.5)");
        Assert.Equal(2, st.Transitions.Count);
        Assert.Equal(new TransitionSpec("background-color", 200, Easing.EaseOut, 0), st.Transitions[0]);
        Assert.Equal(new TransitionSpec("opacity", 300, Easing.Linear, 50), st.Transitions[1]);
        Assert.Equal(new Transform2D(4, -2, 1.5f, 1.5f), st.Transform);
        Assert.Equal("all", StyleParser.Parse("transition: 150ms").Transitions[0].Property);
        Assert.Empty(StyleParser.Parse("transition: none").Transitions);
        Assert.Empty(StyleParser.Parse("transition: wobble 2").Transitions); // 잘못된 항목은 건너뜀
        Assert.True(StyleParser.Parse("transform: rotate(10deg)").Transform.IsIdentity); // 모르는 함수 = 무시
    }

    [Theory]
    [InlineData(Easing.Linear)]
    [InlineData(Easing.Ease)]
    [InlineData(Easing.EaseIn)]
    [InlineData(Easing.EaseOut)]
    [InlineData(Easing.EaseInOut)]
    public void Easing_runs_from_0_to_1_without_going_back(Easing e)
    {
        Assert.Equal(0f, EasingMath.Apply(e, 0f), 3);
        Assert.Equal(1f, EasingMath.Apply(e, 1f), 3);
        var prev = 0f;
        for (var x = 0.05f; x <= 1f; x += 0.05f)
        {
            var y = EasingMath.Apply(e, x);
            Assert.True(y >= prev - 1e-4f, $"{e} {x}");
            prev = y;
        }
    }

    // ── 전환 ──

    private const string HoverBox =
        "<DvBox style=\"width:100; height:60; background:#ff0000; transition: background-color 200ms linear\" hover-style=\"background:#0000ff\" onclick=\"X\"/>";

    [Fact]
    public void Hover_background_fades_over_the_duration()
    {
        var (s, bmp) = Start(HoverBox);
        using (bmp)
        {
            Assert.Equal(new SKColor(255, 0, 0), Render(s, bmp, 1000, 50, 30));
            s.PointerMove(new SKPoint(50, 30));
            Assert.Equal(new SKColor(255, 0, 0), Render(s, bmp, 1000, 50, 30)); // 시작 프레임
            Assert.Equal(1016, s.NextAnimationDue(1000));                       // 진행 중 — 다음 프레임
            var mid = Render(s, bmp, 1100, 50, 30);
            Assert.InRange(mid.Red, 110, 145);
            Assert.InRange(mid.Blue, 110, 145);
            Assert.Equal(new SKColor(0, 0, 255), Render(s, bmp, 1250, 50, 30));
            Assert.Null(s.NextAnimationDue(1250));                              // 끝 — 더 그릴 것 없음
        }
    }

    [Fact]
    public void Reversal_starts_from_the_current_value()
    {
        var (s, bmp) = Start(HoverBox);
        using (bmp)
        {
            Render(s, bmp, 1000, 50, 30);
            s.PointerMove(new SKPoint(50, 30));
            Render(s, bmp, 1000, 50, 30);
            var mid = Render(s, bmp, 1100, 50, 30);       // 절반쯤
            s.PointerMove(new SKPoint(250, 150));          // 벗어남 — 지금 값에서 빨강으로
            var back = Render(s, bmp, 1100, 50, 30);
            Assert.Equal(mid, back);                       // 튀지 않는다
            Assert.Equal(new SKColor(255, 0, 0), Render(s, bmp, 1400, 50, 30));
        }
    }

    [Fact]
    public void Without_transition_state_changes_at_once()
    {
        var (s, bmp) = Start("<DvBox style=\"width:100; height:60; background:#ff0000\" hover-style=\"background:#0000ff\" onclick=\"X\"/>");
        using (bmp)
        {
            Render(s, bmp, 1000, 50, 30);
            s.PointerMove(new SKPoint(50, 30));
            Assert.Equal(new SKColor(0, 0, 255), Render(s, bmp, 1000, 50, 30));
            Assert.Null(s.NextAnimationDue(1000));
        }
    }

    [Fact]
    public void Bound_value_change_transitions_after_invalidate()
    {
        var vm = new Vm();
        var (s, bmp) = Start("<DvBox style=\"width:100; height:60; background:{Bg}; transition: background-color 100ms linear\"/>", vm);
        using (bmp)
        {
            Assert.Equal(new SKColor(255, 0, 0), Render(s, bmp, 1000, 50, 30));
            vm.Bg = "#00ff00";
            s.Invalidate();
            s.Layout(W, H);
            Render(s, bmp, 2000, 50, 30);
            var mid = Render(s, bmp, 2050, 50, 30);
            Assert.InRange(mid.Green, 100, 160);
            Assert.Equal(new SKColor(0, 255, 0), Render(s, bmp, 2200, 50, 30));
        }
    }

    // ── opacity · transform ──

    [Fact]
    public void Opacity_blends_the_box_and_its_children()
    {
        var (s, bmp) = Start("<DvBox style=\"width:100; height:60; background:#000000; opacity:0.5\"><DvBox style=\"width:40; height:40; background:#000000\"/></DvBox>");
        using (bmp)
        {
            var p = Render(s, bmp, 0, 20, 20);   // 겹친 곳도 한 장으로 겹쳐 반투명(두 번 어두워지지 않음)
            Assert.InRange(p.Red, 120, 135);
            Assert.Equal(p, Render(s, bmp, 0, 80, 50));
        }
    }

    [Fact]
    public void Transform_scales_around_the_center_and_moves()
    {
        var (s, bmp) = Start("<DvBox style=\"width:100; height:60; background:#000000; transform: scale(1.5) translate(20, 0)\"/>");
        using (bmp)
        {
            var box = Flatten(s.Root!).First(b => b.Node.Tag == "DvBox");
            Assert.Equal(new SKRect(0, 0, 100, 60), box.Rect);     // 배치는 그대로
            Render(s, bmp, 0, 0, 0);
            // 가운데(50,30) 기준 1.5배 → 가로 150, 그 뒤 20 옮김 → 왼쪽 -5 ~ 오른쪽 145
            Assert.Equal(SKColors.Black, bmp.GetPixel(2, 30));
            Assert.Equal(SKColors.Black, bmp.GetPixel(140, 30));
            Assert.Equal(SKColors.White, bmp.GetPixel(150, 30));
        }
    }
}
