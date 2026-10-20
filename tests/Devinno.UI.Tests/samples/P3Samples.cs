using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P3Samples
{
    private sealed class Vm
    {
        public bool On { get; set; }
    }

    [Fact]
    public void Render_toggle_states()
    {
        Render("phase3-toggle-off.png", false);
        Render("phase3-toggle-on.png", true);
    }

    private static void Render(string name, bool on)
    {
        var vm = new Vm { On = on };
        var markup =
            "<DvScreen style=\"background:#eef1f5; padding:24; display:flex; flex-direction:column; gap:12\">" +
            "<DvLabel text=\"전원\" style=\"font-size:18; font-weight:bold\"/>" +
            "<DvToggle value=\"{Bind Data.On}\" style=\"width:56; height:30\"/></DvScreen>";
        var box = new LayoutEngine(DvTheme.Light).Layout(MarkupParser.Parse(markup), vm, 240, 140);
        using var surface = SKSurface.Create(new SKImageInfo(240, 140));
        surface.Canvas.Clear(SKColors.White);
        new Painter(DvTheme.Light).Paint(surface.Canvas, box, vm);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(ReportsDir(), name);
        File.WriteAllBytes(path, data.ToArray());
        Assert.True(new FileInfo(path).Length > 1000);
    }

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
