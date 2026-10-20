using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// content-align — 글자(+아이콘) 9방향 배치. 대상 DvLabel·DvButton, 속성 = 클래스보다 강하고 인라인 style 보다 약함
public class ContentAlignTests
{
    public sealed class Vm
    {
        public string Align { get; set; } = "bottom-right";
    }

    private const float W = 400, H = 200;

    private static (DvSurface s, SKBitmap bmp) Render(string inner, object? vm = null)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:0\">{inner}</DvScreen>", vm ?? new Vm(), DvTheme.Light);
        var bmp = new SKBitmap((int)W, (int)H);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, W, H);
        return (s, bmp);
    }

    // 박스 안에서 기준색과 거의 같은 픽셀의 범위(글자 잉크)
    private static SKRect Ink(SKBitmap bmp, SKRect box, SKColor ink)
    {
        float l = float.MaxValue, t = float.MaxValue, r = float.MinValue, b = float.MinValue;
        for (var y = (int)box.Top + 2; y < (int)box.Bottom - 2; y++)
        for (var x = (int)box.Left + 2; x < (int)box.Right - 2; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (Math.Abs(p.Red - ink.Red) + Math.Abs(p.Green - ink.Green) + Math.Abs(p.Blue - ink.Blue) > 60)
                continue;
            l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x); b = Math.Max(b, y);
        }

        Assert.True(r >= l, "글자 잉크를 찾지 못함");
        return new SKRect(l, t, r + 1, b + 1);
    }

    private static LayoutBox First(DvSurface s) => s.Root!.Children[0];

    // 검은 바탕·흰 글자, 패딩 10 — 잉크 위치만 본다
    private const string Box = "width:300; height:80; padding:10; background:#000000; color:#ffffff";

    [Theory]
    [InlineData("top-left", -1, -1)]
    [InlineData("top-center", 0, -1)]
    [InlineData("top-right", 1, -1)]
    [InlineData("middle-left", -1, 0)]
    [InlineData("middle-center", 0, 0)]
    [InlineData("middle-right", 1, 0)]
    [InlineData("bottom-left", -1, 1)]
    [InlineData("bottom-center", 0, 1)]
    [InlineData("bottom-right", 1, 1)]
    public void Label_places_text_in_nine_positions(string align, int hx, int vy)
    {
        var (s, bmp) = Render($"""<DvLabel text="운전" content-align="{align}" style="{Box}"/>""");
        var rect = First(s).Rect;
        var ink = Ink(bmp, rect, SKColors.White);
        var content = new SKRect(rect.Left + 10, rect.Top + 10, rect.Right - 10, rect.Bottom - 10);

        var cx = hx switch { -1 => ink.Left - content.Left, 0 => ink.MidX - content.MidX, _ => content.Right - ink.Right };
        var cy = vy switch { -1 => ink.Top - content.Top, 0 => ink.MidY - content.MidY, _ => content.Bottom - ink.Bottom };
        Assert.InRange(cx, -3, 4);   // 왼/오른쪽 = 가장자리 가까이, 가운데 = 중심 근처
        Assert.InRange(cy, -3, 8);   // 위/아래 = 글꼴 여백(ascent·descent 안쪽 여유)만큼
        if (vy == 0)
            Assert.InRange(ink.MidY - content.MidY, -3, 3);
    }

    [Fact]
    public void Label_default_stays_top()
    {
        var (s, bmp) = Render($"""<DvLabel text="운전" style="{Box}"/>""");
        var ink = Ink(bmp, First(s).Rect, SKColors.White);
        Assert.True(ink.Top < First(s).Rect.Top + 30); // 위쪽(현재 동작 유지)
    }

    [Fact]
    public void Styled_button_defaults_to_middle_center()
    {
        var (s, bmp) = Render("""<DvButton variant="secondary" text="운전 시작" icon="fa-play" style="width:300; height:80"/>""");
        var rect = First(s).Rect;
        var ink = Ink(bmp, rect, SKColor.Parse(Ref("text")));
        Assert.InRange(ink.MidY - rect.MidY, -3, 3);
        Assert.InRange(ink.MidX - rect.MidX, -4, 4);
    }

    [Fact]
    public void Attribute_beats_class_and_inline_style_beats_attribute()
    {
        var (s, bmp) = Render("""<DvButton variant="secondary" text="운전" content-align="middle-left" style="width:300; height:80"/>""");
        var rect = First(s).Rect;
        var ink = Ink(bmp, rect, SKColor.Parse(Ref("text")));
        Assert.True(ink.Left < rect.Left + 20 + 6);  // .btn 의 middle-center 를 속성이 이김(왼쪽 패딩 20)
        Assert.InRange(ink.MidY - rect.MidY, -3, 3);

        (s, bmp) = Render("""<DvButton variant="secondary" text="운전" content-align="middle-left" style="width:300; height:80; content-align:middle-right"/>""");
        rect = First(s).Rect;
        ink = Ink(bmp, rect, SKColor.Parse(Ref("text")));
        Assert.True(ink.Right > rect.Right - 20 - 6); // 인라인 style 이 속성을 이김
    }

    [Fact]
    public void Bound_value_is_read_from_vm()
    {
        var (s, bmp) = Render("<DvLabel text=\"운전\" content-align=\"{Data.Align}\" style=\"" + Box + "\"/>");
        var rect = First(s).Rect;
        var ink = Ink(bmp, rect, SKColors.White);
        Assert.True(ink.Right > rect.Right - 10 - 4);
        Assert.True(ink.Bottom > rect.Bottom - 10 - 8);
    }

    [Fact]
    public void Invalid_value_is_ignored()
    {
        var (s, bmp) = Render($"""<DvLabel text="운전" content-align="MiddleCenter" style="{Box}"/>""");
        var ink = Ink(bmp, First(s).Rect, SKColors.White);
        Assert.True(ink.Top < First(s).Rect.Top + 30); // 케밥만 — 무시하고 기본(위쪽)
    }

    [Fact]
    public void Icon_only_unstyled_button_stays_centered()
    {
        var (s, bmp) = Render($"""<DvButton icon="fa-play" style="{Box}"/>""");
        var rect = First(s).Rect;
        var ink = Ink(bmp, rect, SKColors.White);
        Assert.InRange(ink.MidY - rect.MidY, -3, 3);
    }

    [Fact]
    public void Icon_follows_text_when_aligned_bottom()
    {
        var (s, bmp) = Render($"""<DvButton text="운전" icon="fa-play" content-align="bottom-left" style="{Box}"/>""");
        var rect = First(s).Rect;
        var ink = Ink(bmp, rect, SKColors.White);
        Assert.True(ink.Top > rect.MidY - 4); // 아이콘·글자 모두 아래쪽 절반
    }

    private static string Ref(string token)
        => DvTheme.Light.TryGet(token, out var raw) ? raw : "#000000";
}
