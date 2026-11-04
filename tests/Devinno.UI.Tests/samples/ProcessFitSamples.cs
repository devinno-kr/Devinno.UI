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

        // 값 글자 — 배치에 끼지 않음. 위 면 버블 여럿(값은 위), 긴 값
        const string values = """
            <DvTerminal name="IN" text="IN"/>
            <DvPump name="P1" label="P-1"/>
            <DvVessel name="T1" label="T-1" level="40"/>
            <DvTerminal name="OUT" text="OUT"/>
            <DvLine from="IN" to="P1.in"/>
            <DvLine from="P1.out" to="T1.top"/>
            <DvLine from="T1.bottom" to="OUT"/>
            <DvInstrument name="A" tag="PI" number="1" on="P1" value="1.2 bar"/>
            <DvInstrument name="B" tag="TI" number="2" on="P1" value="40 °C"/>
            <DvInstrument name="L" tag="LT" number="3" on="T1" value="1,284.5 L"/>
            <DvSignal from="A" to="B"/>
            """;
        Render($"<DvProcess>{values}</DvProcess>", vm, 900, 420, "fit-values.png");
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
