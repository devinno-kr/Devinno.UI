using Devinno.UI;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 배경 그림(background-image·background-size) — 상자의 일부(padding 영역까지, 모서리로 자름), 누름을 가로막지 않음, 상태 스타일로 바꿀 수 있음
public class BackgroundImageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dv-bgimg-" + Guid.NewGuid().ToString("N"));
    private readonly string _red, _blue, _wide;

    public BackgroundImageTests()
    {
        Directory.CreateDirectory(_dir);
        _red = Png("red.png", 10, 10, SKColors.Red);
        _blue = Png("blue.png", 10, 10, SKColors.Blue);
        _wide = Png("wide.png", 40, 10, SKColors.Lime); // 4:1
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private string Png(string name, int w, int h, SKColor c)
    {
        using var bmp = new SKBitmap(w, h);
        bmp.Erase(c);
        var path = Path.Combine(_dir, name);
        using var data = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path.Replace('\\', '/');
    }

    public sealed class Vm
    {
        public string Img { get; set; } = "";
        public int Clicks;
        public void Hit() => Clicks++;
    }

    private static SKBitmap Render(DvSurface s, int w, int h)
    {
        s.Layout(w, h);
        var bmp = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        s.RenderTo(canvas, w, h, 0);
        return bmp;
    }

    private static DvSurface Screen(string box, object? vm = null) =>
        new($"<DvScreen style=\"padding:0; background:#ffffff\">{box}</DvScreen>", vm ?? new Vm(), DvTheme.Light);

    // ── 파싱 ──

    [Theory]
    [InlineData("background-image: url(a/b.png)", "a/b.png")]
    [InlineData("background-image: url(\"a b.png\")", "a b.png")]
    [InlineData("background-image: url('c.png')", "c.png")]
    [InlineData("background-image: url()", null)]
    [InlineData("background-image: url(x.png); background-image: none", null)]
    [InlineData("background: url(d.png)", "d.png")]
    public void Parse_Url(string decl, string? expected) => Assert.Equal(expected, StyleParser.Parse(decl).BackgroundImage);

    [Theory]
    [InlineData("", BackgroundFit.Cover)]
    [InlineData("background-size: contain", BackgroundFit.Contain)]
    [InlineData("background-size: fill", BackgroundFit.Fill)]
    [InlineData("background-size: 100% 100%", BackgroundFit.Fill)]
    [InlineData("background-size: natural", BackgroundFit.Natural)]
    [InlineData("background-size: contain; background-size: auto", BackgroundFit.Contain)] // 모르는 값은 무시
    public void Parse_Size(string decl, BackgroundFit expected) => Assert.Equal(expected, StyleParser.Parse(decl).BackgroundSize);

    [Fact]
    public void Shorthand_ReplacesLikeCss()
    {
        // background 묶음 표기는 CSS 처럼 색·그림을 함께 바꾼다. background-color·background-image 는 자기 것만
        var a = StyleParser.Parse("background:#ff0000; background: url(x.png)");
        Assert.Equal("x.png", a.BackgroundImage);
        Assert.Null(a.Background);
        var b = StyleParser.Parse("background: url(x.png); background:#ff0000");
        Assert.Null(b.BackgroundImage);
        var c = StyleParser.Parse("background-image: url(x.png); background-color:#ff0000");
        Assert.Equal("x.png", c.BackgroundImage);
        Assert.NotNull(c.Background);
    }

    // ── 그리기 ──

    [Fact]
    public void Draws_UnderPadding_AndUnderChildren()
    {
        // padding 띠까지 그림, 자식(글자)은 그 위
        var s = Screen($"<DvBox style=\"position:absolute; left:0; top:0; width:100; height:60; padding:20; background-image:url({_red}); background-size:fill\"><DvLabel text=\"X\" style=\"color:#0000ff\"/></DvBox>");
        using var bmp = Render(s, 120, 80);
        Assert.Equal(SKColors.Red, bmp.GetPixel(3, 3));      // padding 영역
        Assert.Equal(SKColors.Red, bmp.GetPixel(96, 56));
        Assert.Equal(SKColors.White, bmp.GetPixel(110, 70)); // 상자 밖
    }

    [Fact]
    public void ClippedToBorderRadius()
    {
        var s = Screen($"<DvBox style=\"position:absolute; left:0; top:0; width:100; height:100; border-radius:40; background-image:url({_red}); background-size:fill\"/>");
        using var bmp = Render(s, 100, 100);
        Assert.Equal(SKColors.White, bmp.GetPixel(1, 1)); // 둥근 모서리 밖
        Assert.Equal(SKColors.Red, bmp.GetPixel(50, 50));
    }

    [Fact]
    public void Contain_LeavesBands_Cover_Fills()
    {
        // 4:1 그림, 100×100 상자 — contain 은 위아래 띠(배경색), cover 는 꽉 채움
        var contain = Screen($"<DvBox style=\"position:absolute; left:0; top:0; width:100; height:100; background:#000000; background-image:url({_wide}); background-size:contain\"/>");
        using (var bmp = Render(contain, 100, 100))
        {
            Assert.Equal(SKColors.Black, bmp.GetPixel(50, 5));
            Assert.Equal(SKColors.Lime, bmp.GetPixel(50, 50));
        }

        var cover = Screen($"<DvBox style=\"position:absolute; left:0; top:0; width:100; height:100; background:#000000; background-image:url({_wide})\"/>");
        using (var bmp = Render(cover, 100, 100))
        {
            Assert.Equal(SKColors.Lime, bmp.GetPixel(50, 5));
            Assert.Equal(SKColors.Lime, bmp.GetPixel(2, 97));
        }
    }

    [Fact]
    public void Bound_Path_AndMissingFile()
    {
        var vm = new Vm { Img = _blue };
        var s = Screen("<DvBox style=\"position:absolute; left:0; top:0; width:40; height:40; background-image:url({Img})\"/>", vm);
        using (var bmp = Render(s, 40, 40))
            Assert.Equal(SKColors.Blue, bmp.GetPixel(20, 20));

        vm.Img = Path.Combine(_dir, "missing.png");
        s.Invalidate();
        using (var bmp = Render(s, 40, 40))
            Assert.Equal(SKColors.White, bmp.GetPixel(20, 20)); // 없는 파일 = 그리지 않음(예외 없음)
    }

    // ── 입력 ──

    [Fact]
    public void PressStyle_SwapsTheImageWhilePressed_AndClicks()
    {
        // 그림은 상자의 일부라 누름을 가로막지 않고, press-style 로 누르는 동안만 다른 그림
        var vm = new Vm();
        var s = Screen($"<DvButton onclick=\"Hit\" style=\"position:absolute; left:0; top:0; width:60; height:40; padding:0; background-image:url({_red}); background-size:fill\" press-style=\"background-image:url({_blue})\"/>", vm);
        using (var bmp = Render(s, 60, 40))
            Assert.Equal(SKColors.Red, bmp.GetPixel(30, 20));

        s.PointerDown(new SKPoint(30, 20));
        using (var bmp = Render(s, 60, 40))
            Assert.Equal(SKColors.Blue, bmp.GetPixel(30, 20));

        s.PointerUp(new SKPoint(30, 20));
        Assert.Equal(1, vm.Clicks);
        using (var bmp = Render(s, 60, 40))
            Assert.Equal(SKColors.Red, bmp.GetPixel(30, 20));
    }
    // ── natural·background-image-offset — 판 밖으로 번지는 글로우·그림자 여백이 있는 그림 ──

    [Theory]
    [InlineData("background-image-offset: -16 -12", -16f, -12f)]
    [InlineData("background-image-offset: -8", -8f, -8f)]
    [InlineData("background-image-offset: -4px 6px", -4f, 6f)]
    public void Parse_Offset(string decl, float x, float y) => Assert.Equal((x, y), StyleParser.Parse("background-size:natural; " + decl).BackgroundOffset);

    [Fact]
    public void Natural_Centered_NotCutAtTheBox()
    {
        // 상자 20×20, 그림 40×10 → 가운데에 원래 크기로, 상자 밖(좌우 10px)까지 그린다. border-radius 로도 자르지 않는다
        var s = Screen($"<DvBox style=\"position:absolute; left:40; top:40; width:20; height:20; border-radius:10; background-image:url({_wide}); background-size:natural\"/>");
        using var bmp = Render(s, 100, 100);
        Assert.Equal(SKColors.Lime, bmp.GetPixel(32, 50));  // 상자 왼쪽 밖
        Assert.Equal(SKColors.Lime, bmp.GetPixel(68, 50));  // 상자 오른쪽 밖
        Assert.Equal(SKColors.Lime, bmp.GetPixel(41, 46));  // 둥근 모서리 자리
        Assert.Equal(SKColors.White, bmp.GetPixel(50, 43)); // 그림(높이 10) 위
    }

    [Fact]
    public void Natural_Offset_PlacesTheTopLeftCorner()
    {
        // 비대칭 여백 — 그림 왼쪽 위 = 상자 왼쪽 위 + (-30, 5)
        var s = Screen($"<DvBox style=\"position:absolute; left:40; top:40; width:20; height:20; background-image:url({_wide}); background-size:natural; background-image-offset:-30 5\"/>");
        using var bmp = Render(s, 100, 100);
        Assert.Equal(SKColors.Lime, bmp.GetPixel(11, 46));  // x 10..50, y 45..55
        Assert.Equal(SKColors.White, bmp.GetPixel(8, 50));
        Assert.Equal(SKColors.White, bmp.GetPixel(30, 43));
        Assert.Equal(SKColors.White, bmp.GetPixel(55, 50)); // 그림 오른쪽 끝(50) 밖
    }

    [Fact]
    public void Natural_PressAreaStaysTheBox()
    {
        // 그림이 넘쳐도 누름은 상자만 — 그림자 여백을 눌러도 클릭 아님
        var vm = new Vm();
        var s = Screen($"<DvButton onclick=\"Hit\" style=\"position:absolute; left:40; top:40; width:20; height:20; padding:0; background-image:url({_wide}); background-size:natural\"/>", vm);
        Render(s, 100, 100).Dispose();
        s.PointerDown(new SKPoint(33, 50));
        s.PointerUp(new SKPoint(33, 50));
        Assert.Equal(0, vm.Clicks);
        s.PointerDown(new SKPoint(50, 50));
        s.PointerUp(new SKPoint(50, 50));
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Offset_WithOtherSizes_Ignored()
    {
        var s = StyleParser.Parse("background-size: fill; background-image-offset: -10 -10");
        Assert.Equal(BackgroundFit.Fill, s.BackgroundSize);
        var pic = Screen($"<DvBox style=\"position:absolute; left:0; top:0; width:40; height:40; background-image:url({_red}); background-size:fill; background-image-offset:-10 -10\"/>");
        using var bmp = Render(pic, 60, 60);
        Assert.Equal(SKColors.Red, bmp.GetPixel(38, 38));   // 상자 안 그대로(옮기지 않음)
        Assert.Equal(SKColors.White, bmp.GetPixel(45, 20));
    }
}
