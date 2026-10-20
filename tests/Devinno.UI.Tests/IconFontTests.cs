using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;
using Xunit;

// 아이콘 체계 — Font Awesome 6 Free(solid/regular/brands), "fa-x" / "far fa-x" / "fab fa-x" 표기, DvIcon, DvButton icon
public class IconFontTests
{
    [Theory]
    [InlineData("fa-delete-left", "Solid", 0xf55a)]
    [InlineData("fas fa-gear", "Solid", 0xf013)]
    [InlineData("far fa-star", "Regular", 0xf005)]
    [InlineData("fab fa-windows", "Brands", 0xf17a)]
    [InlineData("  FA-PLAY ", "Solid", 0xf04b)]
    public void Resolves_icon_strings(string spec, string style, int code)
    {
        Assert.True(IconFont.TryResolve(spec, out var icon));
        Assert.Equal(style, icon.Style.ToString());
        Assert.Equal(char.ConvertFromUtf32(code), icon.Glyph);
        Assert.True(icon.Typeface.ContainsGlyph(code));
    }

    [Theory]
    [InlineData("fa-no-such-icon")]
    [InlineData("far fa-gear")]      // regular 에 없는 아이콘(Free)
    [InlineData("xyz fa-gear")]
    [InlineData("gear")]             // fa- 접두 없음
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_specs_do_not_resolve(string? spec)
        => Assert.False(IconFont.TryResolve(spec, out _));

    [Fact]
    public void Draw_puts_ink_at_the_center()
    {
        using var bmp = new SKBitmap(40, 40);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.White);
            using var p = new SKPaint { IsAntialias = true, Color = SKColors.Black };
            Assert.True(IconFont.TryResolve("fa-square", out var icon));
            IconFont.Draw(c, icon, new SKPoint(20, 20), 24, p);
        }

        Assert.Equal(SKColors.Black, bmp.GetPixel(20, 20));
        Assert.Equal(SKColors.White, bmp.GetPixel(2, 2));
    }

    // ── DvIcon
    private static LayoutBox Lay(string markup, object? vm = null)
        => new LayoutEngine(DvTheme.Light).Layout(MarkupParser.Parse(markup), vm, 300, 200);

    private static LayoutBox Find(LayoutBox root, string tag)
        => Flatten(root).First(b => b.Node.Tag == tag);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    public sealed class Vm
    {
        public string State { get; set; } = "fa-circle-check";
    }

    [Fact]
    public void DvIcon_is_a_font_size_square_default_24()
    {
        var b = Find(Lay("<DvScreen style=\"align-items:start\"><DvIcon icon=\"fa-gear\"/></DvScreen>"), "DvIcon");
        Assert.Equal((24f, 24f), (b.Rect.Width, b.Rect.Height));
        var big = Find(Lay("<DvScreen style=\"align-items:start\"><DvIcon icon=\"fa-gear\" style=\"font-size:40\"/></DvScreen>"), "DvIcon");
        Assert.Equal(40f, big.Rect.Width);
    }

    [Fact]
    public void DvIcon_binds_and_unknown_icon_does_not_throw()
    {
        var b = Find(Lay("<DvScreen style=\"align-items:start\"><DvIcon icon=\"{Data.State}\"/></DvScreen>", new Vm()), "DvIcon");
        Assert.Equal("fa-circle-check", b.Icon);
        var bad = Find(Lay("<DvScreen style=\"align-items:start\"><DvIcon icon=\"fa-nope\"/></DvScreen>"), "DvIcon");
        Assert.Equal(24f, bad.Rect.Width);
        Render(Lay("<DvScreen style=\"align-items:start\"><DvIcon icon=\"fa-nope\"/></DvScreen>")).Dispose();
    }

    private static SKBitmap Render(LayoutBox root)
    {
        var bmp = new SKBitmap(300, 200);
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.White);
        new Painter(DvTheme.Light).Paint(c, root, null);
        return bmp;
    }

    [Fact]
    public void DvIcon_paints_in_its_color()
    {
        var root = Lay("<DvScreen style=\"padding:0\"><DvIcon icon=\"fa-square\" style=\"color:#ff0000; font-size:40\"/></DvScreen>");
        var b = Find(root, "DvIcon");
        using var bmp = Render(root);
        var px = bmp.GetPixel((int)b.Rect.MidX, (int)b.Rect.MidY);
        Assert.True(px.Red > 200 && px.Green < 60, px.ToString());
    }

    // ── DvButton icon
    [Fact]
    public void Button_icon_widens_the_button_and_icon_only_is_square_content()
    {
        var plain = Find(Lay("<DvScreen style=\"align-items:start\"><DvButton text=\"저장\" variant=\"primary\"/></DvScreen>"), "DvButton");
        var withIcon = Find(Lay("<DvScreen style=\"align-items:start\"><DvButton text=\"저장\" icon=\"fa-floppy-disk\" variant=\"primary\"/></DvScreen>"), "DvButton");
        Assert.Equal(plain.Rect.Width + 14 + 8, withIcon.Rect.Width, 1); // 아이콘(글자 크기 14) + 간격 8
        Assert.Equal(plain.Rect.Height, withIcon.Rect.Height);

        var only = Find(Lay("<DvScreen style=\"align-items:start\"><DvButton icon=\"fa-play\" variant=\"ghost\"/></DvScreen>"), "DvButton");
        Assert.Equal(14 + 40, only.Rect.Width, 1); // 아이콘 14 + 좌우 padding 20*2
    }

    [Fact]
    public void Button_paints_icon_left_of_text()
    {
        var root = Lay("""
            <DvScreen style="padding:0">
              <DvButton text="저장" icon="fa-square" style="color:#ff0000; padding:10; font-size:20"/>
            </DvScreen>
            """);
        var b = Find(root, "DvButton");
        using var bmp = Render(root);
        var px = bmp.GetPixel((int)b.Rect.Left + 10 + 10, (int)b.Rect.MidY); // 아이콘 칸 가운데
        Assert.True(px.Red > 200 && px.Green < 60, px.ToString());
    }
}
