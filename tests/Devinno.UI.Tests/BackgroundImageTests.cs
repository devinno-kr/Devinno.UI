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
    [InlineData("", ImageFit.Cover)]
    [InlineData("background-size: contain", ImageFit.Contain)]
    [InlineData("background-size: fill", ImageFit.Fill)]
    [InlineData("background-size: 100% 100%", ImageFit.Fill)]
    [InlineData("background-size: contain; background-size: auto", ImageFit.Contain)] // 모르는 값은 무시
    public void Parse_Size(string decl, ImageFit expected) => Assert.Equal(expected, StyleParser.Parse(decl).BackgroundSize);

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
}
