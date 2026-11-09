using Devinno.UI;
using Devinno.UI.Components;
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

        // 통째 축소 — 같은 영역(640×340)에서 fit 없음(넘침) / fit="scale-down" / font-size 를 키운 축소
        Render($"<DvProcess grow=\"1\" style=\"height:0; border: 1px solid token(border)\">{ProcessInstrumentTests.Loop}</DvProcess>", vm, 640, 340, "fit-overflow.png");
        Render($"<DvProcess fit=\"scale-down\" grow=\"1\" style=\"height:0; border: 1px solid token(border)\">{ProcessInstrumentTests.Loop}</DvProcess>", vm, 640, 340, "fit-shrink.png");
        Render($"<DvProcess fit=\"scale-down\" grow=\"1\" style=\"height:0; font-size:20; border: 1px solid token(border)\">{ProcessInstrumentTests.Loop}</DvProcess>", vm, 640, 340, "fit-shrink-font20.png");
        Render($"<DvProcess fit=\"scale-down\" look=\"rich\" direction=\"tb\" grow=\"1\" style=\"height:0; border: 1px solid token(border)\">{ProcessUnitTests.PlantMarkup}</DvProcess>",
            new ProcessUnitTests.Vm(), 700, 520, "fit-shrink-units.png", ProcessUnitTests.CreateRegistry());
        // 크기 결정 절차(dvml-process-layout.md#sizing-procedure) 그대로: 6.1px → 14 × 11 / 6.1 → font-size 26 → 10.3px(붙은 칸 버튼도 함께 커짐)
        Render($"<DvProcess fit=\"scale-down\" look=\"rich\" direction=\"tb\" grow=\"1\" style=\"height:0; font-size:26; border: 1px solid token(border)\">{ProcessUnitTests.PlantMarkup}</DvProcess>",
            new ProcessUnitTests.Vm(), 700, 520, "fit-shrink-units-font26.png", ProcessUnitTests.CreateRegistry());

        // 좌표 배치 — base-size 없이 영역보다 넓은 좌표
        const string fixedMarkup = """
            <DvTerminal name="IN" text="IN" x="60" y="80"/>
            <DvPump name="P1" label="P-1" x="420" y="80"/>
            <DvVessel name="T1" label="T-1" level="50" x="900" y="160"/>
            <DvLine from="IN" to="P1.in"/>
            <DvLine from="P1.out" to="T1.top"/>
            """;
        Render($"<DvProcess placement=\"fixed\" fit=\"scale-down\" grow=\"1\" style=\"height:0; border: 1px solid token(border)\">{fixedMarkup}</DvProcess>", vm, 600, 300, "fit-shrink-fixed.png");
    }

    private static void Render(string process, object vm, int w, int h, string file, ComponentRegistry? registry = null)
    {
        var path = Path.Combine(ReportsDir(), file);
        DvRenderer.RenderToFile($"<DvPageFrame style=\"padding:16\">{process}</DvPageFrame>", vm, w, h, path, DvTheme.Light, registry);
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
