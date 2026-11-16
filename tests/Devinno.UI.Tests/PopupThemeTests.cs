using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 팝업(드롭다운)도 화면의 테마 역할 값으로 그린다 — 화면 그리기가 끝난 뒤에 그려도 기본 테마(Dark)로 새지 않는다
public class PopupThemeTests
{
    public sealed class Vm { public int Count { get; set; } = 2; }

    private const string Form = """
        <DvScreen style="padding:0; gap:0; align-items:start">
          <DvSelect id="sel" value="{Bind Count}">
            <DvOption value="1" text="one"/>
            <DvOption value="2" text="two"/>
          </DvSelect>
        </DvScreen>
        """;

    [Fact]
    public void Dropdown_uses_the_surface_theme_roles()
    {
        var theme = DvTheme.Light.With(new Dictionary<string, string> { ["alpha.selected"] = "1" });
        var s = new DvSurface(Form, new Vm(), theme);
        s.Layout(400, 300);
        s.PointerDown(new SKPoint(50, 20));
        s.PointerUp(new SKPoint(50, 20));
        s.Layout(400, 300);
        var d = s.Dropdown!;
        var r = d.ItemRect(1); // 고른 항목 "two"
        using var bmp = new SKBitmap(400, 300);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 400, 300);
        var px = bmp.GetPixel((int)(r.Left + r.Width * 0.85f), (int)r.MidY); // 글자 없는 오른쪽
        Assert.Equal(ThemeResolver.Color(theme, "primary"), px); // alpha.selected = 1 → 불투명 강조색
    }
}
