using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Video;
using SkiaSharp;
using Xunit;

// DvVideo 1단계 — 앱이 넘긴 IFrameSource 를 보여 주고, 새 프레임이 있을 때만 그 상자만 다시 그린다
public class VideoTests
{
    public sealed class Vm
    {
        public FrameSource Source { get; } = new();
        public object? Other { get; set; } = "not a source";
    }

    private static SKImage Solid(SKColor color, int w = 160, int h = 90)
    {
        using var bmp = new SKBitmap(w, h);
        bmp.Erase(color);
        return SKImage.FromPixelCopy(bmp.Info, bmp.GetPixels(), bmp.RowBytes);
    }

    private static DvSurface Surface(string body, Vm vm) =>
        new($"<DvScreen style=\"padding:0\"><DvStack style=\"align-items:start\">{body}</DvStack><DvLabel text=\"아래 글자\"/></DvScreen>", vm, DvTheme.Light);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static LayoutBox Video(DvSurface s) => Flatten(s.Root!).First(b => b.Node.Tag == "DvVideo");

    private static SKBitmap Render(DvSurface s, SKBitmap? keep = null, long t = 1000)
    {
        var bmp = keep ?? new SKBitmap(600, 400);
        using var canvas = new SKCanvas(bmp);
        if (keep is null)
            canvas.Clear(SKColors.White);
        s.RenderTo(canvas, 600, 400, t);
        return bmp;
    }

    [Fact]
    public void DefaultSize_Is320x180_StyleWins()
    {
        var vm = new Vm();
        var s = Surface("<DvVideo source=\"{Source}\"/><DvVideo source=\"{Source}\" style=\"width:200; height:100\"/>", vm);
        s.Layout(600, 400);
        var boxes = Flatten(s.Root!).Where(b => b.Node.Tag == "DvVideo").ToList();
        Assert.Equal(320, boxes[0].Rect.Width);
        Assert.Equal(180, boxes[0].Rect.Height);
        Assert.Equal(200, boxes[1].Rect.Width);
        Assert.Equal(100, boxes[1].Rect.Height);
    }

    [Fact]
    public void PaintsTheCurrentFrame_WithFit()
    {
        var vm = new Vm();
        vm.Source.Publish(Solid(SKColors.Red, 160, 90)); // 16:9
        var s = Surface("<DvVideo source=\"{Source}\" style=\"width:200; height:200\"/>", vm); // contain → 위아래 여백
        using var bmp = Render(s);
        var r = Video(s).Rect;
        Assert.Equal(SKColors.Red, bmp.GetPixel((int)r.MidX, (int)r.MidY));
        Assert.Equal(Devinno.UI.Styling.ThemeResolver.Color(DvTheme.Light, "background"), bmp.GetPixel((int)r.MidX, (int)r.Top + 5)); // 여백 = 캔버스 바탕(3단계 R11: 흰색 → background 토큰)

        var cover = Surface("<DvVideo source=\"{Source}\" fit=\"cover\" style=\"width:200; height:200\"/>", vm);
        using var bmp2 = Render(cover);
        var r2 = Video(cover).Rect;
        Assert.Equal(SKColors.Red, bmp2.GetPixel((int)r2.MidX, (int)r2.Top + 5));
    }

    [Fact]
    public void NoFrame_OrNoSource_PaintsNothing_DoesNotThrow()
    {
        var vm = new Vm();
        var s = Surface("<DvVideo source=\"{Source}\"/><DvVideo/><DvVideo source=\"{Other}\"/>", vm);
        using var bmp = Render(s);
        var r = Video(s).Rect;
        Assert.Equal(Devinno.UI.Styling.ThemeResolver.Color(DvTheme.Light, "background"), bmp.GetPixel((int)r.MidX, (int)r.MidY)); // 캔버스 바탕(3단계 R11: 흰색 → background 토큰)
    }

    [Fact]
    public void LiveSource_DrivesNextAnimationDue_PausedDoesNot()
    {
        var vm = new Vm();
        var s = Surface("<DvVideo source=\"{Source}\"/>", vm);
        using (Render(s)) { }
        Assert.Equal(1008, s.NextAnimationDue(1000)); // 16 ms 주기

        vm.Source.IsLive = false;
        Assert.Null(s.NextAnimationDue(1000));
    }

    [Fact]
    public void NewFrame_RepaintsOnlyTheVideoBox_NoNewFrame_RepaintsNothing()
    {
        var vm = new Vm();
        vm.Source.Publish(Solid(SKColors.Red));
        var s = Surface("<DvVideo source=\"{Source}\"/>", vm);
        s.PartialRender = true;
        using var bmp = Render(s);
        Assert.Null(s.LastRenderRect); // 첫 프레임은 전체

        Render(s, bmp, 1016);
        Assert.Equal(SKRectI.Empty, s.LastRenderRect); // 새 프레임 없음 — 아무것도 안 그림

        vm.Source.Publish(Solid(SKColors.Blue));
        Assert.False(s.NeedsRender); // 전체 다시 그리기 요청이 아니다
        Render(s, bmp, 1032);
        var area = s.LastRenderRect;
        Assert.NotNull(area);
        var box = Video(s).Rect;
        Assert.True(area!.Value.Contains(SKRectI.Round(box)), $"{area} ⊇ {box}");
        Assert.True(area.Value.Height < 400 - 1, "전체가 아닌 상자 영역만");
        Assert.Equal(SKColors.Blue, bmp.GetPixel((int)box.MidX, (int)box.MidY));

        Render(s, bmp, 1048);
        Assert.Equal(SKRectI.Empty, s.LastRenderRect);
    }

    [Fact]
    public void PublishFromAnotherThread_WhilePainting_IsSafe()
    {
        var vm = new Vm();
        var s = Surface("<DvVideo source=\"{Source}\"/>", vm);
        using var stop = new CancellationTokenSource();
        var producer = Task.Run(() =>
        {
            var i = 0;
            while (!stop.IsCancellationRequested)
                vm.Source.Publish(Solid(i++ % 2 == 0 ? SKColors.Red : SKColors.Green, 64, 36));
        });
        using var bmp = new SKBitmap(600, 400);
        s.PartialRender = true;
        for (var f = 0; f < 200; f++)
            Render(s, bmp, 1000 + f * 16);
        stop.Cancel();
        producer.Wait();
    }

    [Fact]
    public void FrameSource_Publish_DisposesThePrevious_Clear_RemovesIt()
    {
        using var src = new FrameSource();
        var a = Solid(SKColors.Red);
        src.Publish(a);
        var v1 = src.FrameVersion;
        src.Publish(Solid(SKColors.Blue));
        Assert.True(src.FrameVersion > v1);
        Assert.Equal(IntPtr.Zero, a.Handle); // 이전 프레임 해제됨

        SKColor seen = default;
        Assert.True(src.UseFrame(img =>
        {
            using var bmp = SKBitmap.FromImage(img);
            seen = bmp.GetPixel(0, 0);
        }));
        Assert.Equal(SKColors.Blue, seen);

        src.Clear();
        Assert.False(src.UseFrame(_ => throw new InvalidOperationException()));
    }
}
