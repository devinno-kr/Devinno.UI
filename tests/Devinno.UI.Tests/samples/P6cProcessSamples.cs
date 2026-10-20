using Devinno.UI;
using Devinno.UI.Styling;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P6cProcessSamples
{
    [Theory]
    [InlineData("plain", false)]
    [InlineData("rich", true)]
    public void Render_process(string look, bool dark)
    {
        var dir = ReportsDir();
        var theme = dark ? DvTheme.Dark : DvTheme.Light;
        var vm = new ProcessTests.Vm();

        var serial = $"<DvScreen style=\"padding:16; background: token(bg)\"><DvProcess look=\"{look}\">{ProcessTests.Serial}</DvProcess></DvScreen>";
        var serialPath = Path.Combine(dir, $"phase6c-serial-{look}.png");
        DvRenderer.RenderToFile(serial, vm, 1080, 560, serialPath, theme);
        Assert.True(new FileInfo(serialPath).Length > 1000);

        var blender = $"<DvScreen style=\"padding:16; background: token(bg)\"><DvProcess direction=\"tb\" look=\"{look}\">{ProcessTests.Blender}</DvProcess></DvScreen>";
        var blenderPath = Path.Combine(dir, $"phase6c-blender-{look}.png");
        DvRenderer.RenderToFile(blender, vm, 520, 980, blenderPath, theme);
        Assert.True(new FileInfo(blenderPath).Length > 1000);

        // 유닛 — 같은 블렌더 유닛 3기(컴포넌트) → RMT
        var units = $"<DvScreen style=\"padding:16; background: token(bg)\"><DvProcess direction=\"tb\" look=\"{look}\">{ProcessUnitTests.PlantMarkup}</DvProcess></DvScreen>";
        var unitsPath = Path.Combine(dir, $"phase6c-units-{look}.png");
        DvRenderer.RenderToFile(units, new ProcessUnitTests.Vm(), 1000, 1020, unitsPath, theme, ProcessUnitTests.CreateRegistry());
        Assert.True(new FileInfo(unitsPath).Length > 1000);

        // 계기·신호선 — 유량 제어 고리(좌→우), 수위 제어 고리(위→아래)
        var loop = $"<DvScreen style=\"padding:16; background: token(bg)\"><DvProcess look=\"{look}\">{ProcessInstrumentTests.Loop}</DvProcess></DvScreen>";
        var loopPath = Path.Combine(dir, $"phase6c-instruments-{look}.png");
        DvRenderer.RenderToFile(loop, vm, 1180, 600, loopPath, theme);
        Assert.True(new FileInfo(loopPath).Length > 1000);

        var bloop = $"<DvScreen style=\"padding:16; background: token(bg)\"><DvProcess direction=\"tb\" look=\"{look}\">{ProcessInstrumentTests.BlenderLoop}</DvProcess></DvScreen>";
        var bloopPath = Path.Combine(dir, $"phase6c-instruments-blender-{look}.png");
        DvRenderer.RenderToFile(bloop, vm, 620, 1000, bloopPath, theme);
        Assert.True(new FileInfo(bloopPath).Length > 1000);

        // 3방 밸브·열교환기 — 분기(좌→우), 혼합 + 판형
        var v3 = $"<DvScreen style=\"padding:16; background: token(bg)\"><DvProcess look=\"{look}\">{ProcessValve3ExchangerTests.Divert}</DvProcess></DvScreen>";
        var v3Path = Path.Combine(dir, $"phase6c-valve3-divert-{look}.png");
        DvRenderer.RenderToFile(v3, new ProcessValve3ExchangerTests.Vm(), 900, 460, v3Path, theme);
        Assert.True(new FileInfo(v3Path).Length > 1000);

        var mix = $"<DvScreen style=\"padding:16; background: token(bg)\"><DvProcess look=\"{look}\">{ProcessValve3ExchangerTests.Mix}</DvProcess></DvScreen>";
        var mixPath = Path.Combine(dir, $"phase6c-valve3-mix-{look}.png");
        DvRenderer.RenderToFile(mix, new ProcessValve3ExchangerTests.Vm { C = true }, 700, 340, mixPath, theme);
        Assert.True(new FileInfo(mixPath).Length > 1000);

        // 매체 색 — 공급수(water) 가 펌프·밸브·열교환기를 지나 탱크까지, 열매체는 steam
        var med = $"<DvScreen style=\"padding:16; background: token(bg)\"><DvProcess look=\"{look}\">{ProcessMediumTests.Cooling}</DvProcess></DvScreen>";
        var medPath = Path.Combine(dir, $"phase6c-medium-{look}.png");
        DvRenderer.RenderToFile(med, vm, 1100, 420, medPath, theme);
        Assert.True(new FileInfo(medPath).Length > 1000);
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
