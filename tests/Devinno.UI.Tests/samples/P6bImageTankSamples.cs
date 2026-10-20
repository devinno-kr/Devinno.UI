using Devinno.UI;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P6bImageTankSamples
{
    [Fact]
    public void Render_image_and_tanks()
    {
        var dir = ReportsDir();

        var bgPath = Path.Combine(dir, "phase6b-bg.png");
        using (var bmp = new SKBitmap(560, 300, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using (var c = new SKCanvas(bmp))
            using (var p = new SKPaint())
            {
                p.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0), new SKPoint(560, 300),
                    new[] { new SKColor(0x0f, 0x17, 0x2a), new SKColor(0x1e, 0x29, 0x3b) },
                    null, SKShaderTileMode.Clamp);
                c.DrawRect(new SKRect(0, 0, 560, 300), p);
            }

            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(bgPath, data.ToArray());
        }

        var src = bgPath.Replace("\\", "/");
        var markup =
            "<DvScreen style=\"position:relative; width:560; height:300\">" +
            $"<DvImage src=\"{src}\" fit=\"cover\" style=\"position:absolute; left:0; top:0; width:560; height:300\"/>" +
            "<DvBox style=\"position:absolute; left:0; top:0; width:560; height:300; padding:24; " +
            "display:flex; flex-direction:column; gap:14\">" +
            "<DvLabel text=\"저장 탱크\" style=\"color:#e2e8f0; font-size:18; font-weight:bold\"/>" +
            "<DvBox style=\"display:flex; flex-direction:row; gap:28\">" +
            Tank("탱크 A", 32) + Tank("탱크 B", 72) + Tank("탱크 C", 94) +
            "</DvBox></DvBox></DvScreen>";

        DvRenderer.RenderToFile(markup, null, 560, 300, Path.Combine(dir, "phase6b-image-tank.png"),
            Devinno.UI.Styling.DvTheme.Dark);
        Assert.True(new FileInfo(Path.Combine(dir, "phase6b-image-tank.png")).Length > 1000);
    }

    private static string Tank(string name, int v) =>
        "<DvBox style=\"display:flex; flex-direction:column; align-items:center; gap:6\">" +
        $"<DvTank value=\"{v}\" min=\"0\" max=\"100\" unit=\"%\" " +
        "alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\" style=\"width:84; height:150\"/>" +
        $"<DvLabel text=\"{name}\" style=\"color:#cbd5e1; font-size:13\"/></DvBox>";

    private static string ReportsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "docs", "superpowers");
            if (Directory.Exists(marker))
            {
                var reports = Path.Combine(marker, "reports");
                Directory.CreateDirectory(reports);
                return reports;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate docs/superpowers from test output.");
    }
}
