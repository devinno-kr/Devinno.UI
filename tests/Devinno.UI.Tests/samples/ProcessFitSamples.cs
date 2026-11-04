using Devinno.UI;
using Devinno.UI.Styling;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class ProcessFitSamples
{
    [Fact]
    public void Render_fit()
    {
        var vm = new ProcessTests.Vm();

        // 글자 크기 — font-size 18(기본 14)
        Render($"<DvProcess style=\"font-size:18\">{ProcessInstrumentTests.Loop}</DvProcess>", vm, 1280, 680, "fit-font18.png");
    }

    private static void Render(string process, object vm, int w, int h, string file)
    {
        var path = Path.Combine(ReportsDir(), file);
        DvRenderer.RenderToFile($"<DvScreen style=\"padding:16; background: token(bg)\">{process}</DvScreen>", vm, w, h, path, DvTheme.Light);
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
