using Devinno.UI;
using Devinno.UI.Styling;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P6bInstrumentSamples
{
    public const string Markup = """
        <DvPageFrame title="계기 위젯" subtitle="DvGauge 눈금 · DvRing · DvProgress · DvTank 원통 음영">
          <DvGrid columns="3">
            <DvCard title="압력 게이지" badge="ticks">
              <DvRow justify="between">
                <DvGauge value="42" ticks="20" minor-ticks="5"
                         alarm-zones="0:60:#10b981 60:85:#f59e0b 85:100:#ef4444" style="width:150; height:150"/>
                <DvGauge value="7.3" min="0" max="10" ticks="2" minor-ticks="0.5"
                         alarm-zones="0:6:#10b981 6:8.5:#f59e0b 8.5:10:#ef4444" style="width:150; height:150"/>
              </DvRow>
            </DvCard>
            <DvCard title="링 게이지" badge="DvRing">
              <DvRow justify="between">
                <DvRing value="75" unit="%" label="LINE 1" style="width:140; height:140"/>
                <DvRing value="50" sweep="360" unit="%" label="LINE 2" tone="success" style="width:140; height:140"/>
              </DvRow>
            </DvCard>
            <DvCard title="저장 탱크" badge="원통 음영">
              <DvRow justify="between" padding="none md">
                <DvTank value="64" unit="%" alarm-zones="0:70:#3b82f6 70:88:#f59e0b 88:100:#ef4444" style="width:84; height:150"/>
                <DvTank value="93" unit="%" alarm-zones="0:70:#3b82f6 70:88:#f59e0b 88:100:#ef4444" style="width:84; height:150"/>
                <DvTank value="35" unit="%" show-zone-marks="false" alarm-zones="0:70:#10b981 70:88:#f59e0b 88:100:#ef4444" style="width:60; height:150"/>
              </DvRow>
            </DvCard>
          </DvGrid>
          <DvCard title="진행 바" badge="DvProgress">
            <DvRow align="stretch" gap="xl">
              <DvStack grow="1" gap="md">
                <DvRow gap="sm"><DvLabel class="label muted" text="배치 진행" width="70"/><DvProgress value="62" show-value="true" unit="%" grow="1" style="height:18"/></DvRow>
                <DvRow gap="sm"><DvLabel class="label muted" text="세척" width="70"/><DvProgress value="35" tone="success" grow="1"/></DvRow>
                <DvRow gap="sm"><DvLabel class="label muted" text="역방향" width="70"/><DvProgress value="45" direction="left" tone="warning" grow="1"/></DvRow>
                <DvRow gap="sm"><DvLabel class="label muted" text="부하" width="70"/><DvProgress value="92" alarm-zones="0:60:#10b981 60:85:#f59e0b 85:100:#ef4444" show-value="true" unit="%" grow="1" style="height:18"/></DvRow>
              </DvStack>
              <DvRow gap="lg" align="end">
                <DvProgress value="80" direction="up" style="height:110"/>
                <DvProgress value="55" direction="up" tone="success" style="height:110"/>
                <DvProgress value="25" direction="up" tone="warning" style="height:110"/>
                <DvProgress value="92" direction="up" tone="danger" style="width:16; height:110"/>
              </DvRow>
            </DvRow>
          </DvCard>
        </DvPageFrame>
        """;

    [Fact]
    public void Render_instruments()
    {
        var dir = ReportsDir();
        DvRenderer.RenderToFile(Markup, null, 1280, 620, Path.Combine(dir, "phase6b-instruments-dark.png"), DvTheme.Dark);
        DvRenderer.RenderToFile(Markup, null, 1280, 620, Path.Combine(dir, "phase6b-instruments-light.png"), DvTheme.Light);
        Assert.True(new FileInfo(Path.Combine(dir, "phase6b-instruments-dark.png")).Length > 1000);
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
