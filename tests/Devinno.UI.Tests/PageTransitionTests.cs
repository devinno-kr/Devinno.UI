using Devinno.UI.App;
using SkiaSharp;
using Xunit;

// 페이지 전환 움직임(P7-5) — DvApp.PageTransition(기본 None) · Show(page, transition). 약 0.2초 동안 옛·새 페이지를 함께 그린다.
public class PageTransitionTests
{
    private const int W = 400, H = 300;

    private sealed class ColorPage(string color) : DvPage
    {
        protected override string MarkupSource => $"<DvScreen style=\"background:{color}\"/>";
    }

    private static (DvApp App, DvPage Red, DvPage Blue) Start(DvPageTransition kind)
    {
        var red = new ColorPage("#ff0000");
        var blue = new ColorPage("#0000ff");
        var app = new DvApp { PageTransition = kind };
        app.Pages.Add(red);
        app.Pages.Add(blue);
        app.Show(red);
        return (app, red, blue);
    }

    private static SKBitmap Frame(DvApp app, long t)
    {
        var bmp = new SKBitmap(W, H);
        app.Surface.Layout(W, H);
        using var c = new SKCanvas(bmp);
        app.Surface.RenderTo(c, W, H, t);
        return bmp;
    }

    [Fact]
    public void Slide_pushes_the_new_page_in_from_the_right()
    {
        var (app, _, blue) = Start(DvPageTransition.Slide);
        Frame(app, 0).Dispose(); // 첫 페이지 — 움직임 없음
        Assert.Null(app.Surface.NextAnimationDue(0));

        app.Show(blue);
        Frame(app, 1000).Dispose(); // 시작
        Assert.Equal(1016, app.Surface.NextAnimationDue(1000));
        using (var mid = Frame(app, 1060))
        {
            Assert.Equal(new SKColor(255, 0, 0), mid.GetPixel(5, 150));      // 왼쪽 = 나가는 빨강
            Assert.Equal(new SKColor(0, 0, 255), mid.GetPixel(W - 5, 150));  // 오른쪽 = 들어오는 파랑
        }
        using (var end = Frame(app, 1250))
        {
            Assert.Equal(new SKColor(0, 0, 255), end.GetPixel(5, 150));
            Assert.Equal(new SKColor(0, 0, 255), end.GetPixel(W - 5, 150));
        }
        Assert.Null(app.Surface.NextAnimationDue(1250));
    }

    [Fact]
    public void Fade_blends_and_per_call_choice_beats_the_default()
    {
        var (app, red, blue) = Start(DvPageTransition.None);
        Frame(app, 0).Dispose();
        app.Show(blue, DvPageTransition.Fade);
        Frame(app, 1000).Dispose();
        using (var mid = Frame(app, 1050))
        {
            var p = mid.GetPixel(200, 150);
            Assert.True(p.Red is > 20 and < 235 && p.Blue is > 20 and < 235, p.ToString()); // 섞인 색
        }
        Frame(app, 1300).Dispose();

        app.Show(red); // 기본 None — 바로
        using (var now = Frame(app, 2000))
            Assert.Equal(new SKColor(255, 0, 0), now.GetPixel(200, 150));
        Assert.Null(app.Surface.NextAnimationDue(2000));
    }
}
