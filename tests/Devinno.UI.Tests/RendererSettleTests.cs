using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvRenderer.RenderToFile(DvSurface) = 지금 상태를 그린다 — 진행 중인 움직임(CSS transition, 페이지 전환, 접는 패널 높이)은 끝난 모습으로.
// 시각 0 으로만 그리므로, 끝내지 않으면 움직임이 시작점에 멈춰 바뀌기 전 모습이 찍힌다(AI 가 PNG 로 확인할 때 틀린 그림).
[Collection("AttachedApp")] // 앱을 붙이는 테스트와 같은 비병렬 묶음
public class RendererSettleTests
{
    public sealed class TabVm
    {
        public string Cls { get; set; } = "tab";
    }

    private const string TabMarkup = """
        <DvRow style="padding:10">
          <DvStyle>
            .tab { width:120; height:40; background: #1e293b; transition: background-color 150ms, color 150ms }
            .tab-on { background: #60a5fa }
          </DvStyle>
          <DvButton class="{Cls}" text="Tab"/>
        </DvRow>
        """;

    [Fact]
    public void RenderToFile_draws_finished_css_transition()
    {
        var vm = new TabVm();
        var s = new DvSurface(TabMarkup, vm, DvTheme.Dark);
        var path = TempPng();
        DvRenderer.RenderToFile(s, 300, 80, path);

        vm.Cls = "tab tab-on";
        s.Invalidate();
        DvRenderer.RenderToFile(s, 300, 80, path);

        Assert.Equal(new SKColor(0x60, 0xa5, 0xfa), Pixel(path, 20, 30));
        Assert.Null(s.NextAnimationDue(0)); // 끝났으니 다음 프레임을 요청하지 않는다
    }

    [Fact]
    public void Window_rendering_still_animates_the_transition()
    {
        var vm = new TabVm();
        var s = new DvSurface(TabMarkup, vm, DvTheme.Dark);
        Draw(s, 0);

        vm.Cls = "tab tab-on";
        s.Invalidate();
        using var bmp = Draw(s, 1000);              // 바뀐 첫 프레임 = 전환 시작점(옛 색)
        Assert.Equal(new SKColor(0x1e, 0x29, 0x3b), bmp.GetPixel(20, 30));
        Assert.NotNull(s.NextAnimationDue(1000));   // 진행 중 — 창은 다음 프레임을 그린다
        using var later = Draw(s, 1200);
        Assert.Equal(new SKColor(0x60, 0xa5, 0xfa), later.GetPixel(20, 30));
    }

    [Theory]
    [InlineData(DvPageTransition.Slide)]
    [InlineData(DvPageTransition.Fade)]
    public void RenderToFile_draws_the_new_page_after_a_page_transition(DvPageTransition transition)
    {
        var app = new DvApp();
        app.Top = new TopBar();
        var home = new HomePage();
        var monitor = new MonitorPage();
        app.Pages.Add(home);
        app.Pages.Add(monitor);
        var path = TempPng();
        DvRenderer.RenderToFile(app.Surface, 1024, 640, path);

        app.Show<MonitorPage>(transition);
        DvRenderer.RenderToFile(app.Surface, 1024, 640, path);

        // 페이지 칸에 새 페이지만 — Monitoring 의 "← Back" 버튼 자리 픽셀이 새 페이지 그대로 그린 것과 같다
        var back = Find(monitor.Surface!.Root!, b => b.Node.Tag == "DvButton" && b.ResolvedText == "← Back")!.Rect;
        var expected = TempPng();
        DvRenderer.RenderToFile(monitor.Surface!, 1024, 640 - 56, expected);
        Assert.Equal(Pixel(expected, (int)back.MidX, (int)back.MidY), Pixel(path, (int)back.MidX, (int)back.MidY + 56));
        Assert.Null(app.Surface.NextAnimationDue(0));
    }

    public sealed class ExpVm
    {
        public bool Open { get; set; }
    }

    [Fact]
    public void RenderToFile_draws_the_expander_fully_open()
    {
        var vm = new ExpVm();
        var s = new DvSurface("""
            <DvScreen style="padding:20; gap:8">
              <DvExpander header="More" expanded="{Bind Open}">
                <DvBox style="height:200; background:#ff0000"/>
              </DvExpander>
            </DvScreen>
            """, vm, DvTheme.Light);
        var path = TempPng();
        DvRenderer.RenderToFile(s, 400, 400, path);
        var header = Find(s.Root!, b => b.Node.Tag == "DvExpanderHeader")!.Rect;
        s.PointerDown(new SKPoint(header.MidX, header.MidY));
        s.PointerUp(new SKPoint(header.MidX, header.MidY));
        Assert.True(vm.Open);

        DvRenderer.RenderToFile(s, 400, 400, path);

        var body = Find(s.Root!, b => b.Node.Tag == "DvBox" && b.Rect.Height == 200)!.Rect;
        Assert.Equal(SKColors.Red, Pixel(path, (int)body.MidX, (int)body.Bottom - 4)); // 내용 아래 끝까지 보인다
        Assert.Null(s.NextAnimationDue(0));
    }

    private static SKBitmap Draw(DvSurface s, long t)
    {
        var bmp = new SKBitmap(300, 80);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 300, 80, t);
        return bmp;
    }

    private static string TempPng() => Path.Combine(Path.GetTempPath(), $"settle-{Guid.NewGuid():N}.png");

    private static SKColor Pixel(string path, int x, int y)
    {
        using var bmp = SKBitmap.Decode(path);
        return bmp.GetPixel(x, y);
    }

    private static LayoutBox? Find(LayoutBox b, Func<LayoutBox, bool> pred)
    {
        if (pred(b)) return b;
        foreach (var c in b.Children)
            if (Find(c, pred) is { } f) return f;
        return null;
    }
}
