using Devinno.UI.App;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 컨트롤 시트(samples/Devinno.UI.Demo ControlSheetPage) — 생김새 작업의 기준 화면.
// 네 내장 테마 모두에서 빠진 토큰(마젠타) 없이 그려지는지 확인한다.
public class ControlSheetTests
{
    public static TheoryData<string> Themes => new() { "Light", "Dark", "Graphite", "Carbon" };

    [Theory]
    [MemberData(nameof(Themes))]
    public void Sheet_renders_in_every_built_in_theme_without_missing_tokens(string themeName)
    {
        var theme = themeName switch
        {
            "Light" => DvTheme.Light,
            "Graphite" => DvTheme.Graphite,
            "Carbon" => DvTheme.Carbon,
            _ => DvTheme.Dark,
        };
        var app = new DvApp { Theme = theme };
        app.Pages.Add(new ControlSheetPage());

        // 빠진 토큰은 마젠타로 그려진다 — 경고(Trace)는 병렬로 도는 다른 테스트의 것까지 섞이므로 픽셀로만 판단한다.
        // 토큰 키가 네 테마에 모두 있는지는 ThemeTokenContractTests 가 따로 지킨다.
        int magenta;
        {
            using var bmp = new SKBitmap(ControlSheetPage.Width, ControlSheetPage.Height);
            using (var canvas = new SKCanvas(bmp))
            {
                app.Surface.RenderTo(canvas, ControlSheetPage.Width, ControlSheetPage.Height); // 페이지 전환 프레임
                app.Surface.RenderTo(canvas, ControlSheetPage.Width, ControlSheetPage.Height);
            }
            magenta = 0;
            for (var y = 0; y < bmp.Height; y += 2)
                for (var x = 0; x < bmp.Width; x += 2)
                    if (bmp.GetPixel(x, y) == new SKColor(0xff, 0x00, 0xff))
                        magenta++;
        }

        Assert.Equal(0, magenta);
    }

}
