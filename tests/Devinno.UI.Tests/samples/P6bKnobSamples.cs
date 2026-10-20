using Devinno.UI;
using Devinno.UI.Styling;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P6bKnobSamples
{
    public const string Markup = """
        <DvPageFrame title="설정값 조정" subtitle="노브를 잡고 돌려 값을 바꾸고 놓으면 반영됩니다">
          <DvGrid columns="3">
            <DvCard title="밸브 개도" badge="%">
              <DvStack align="center" gap="sm">
                <DvKnob value="{Bind Data.Valve}" unit="%" style="width:150; height:150"/>
                <DvLabel class="caption" text="0 ~ 100"/>
              </DvStack>
            </DvCard>
            <DvCard title="펌프 속도" badge="rpm">
              <DvStack align="center" gap="sm">
                <DvKnob value="{Bind Data.Speed}" min="0" max="3600" step="10" style="width:150; height:150"/>
                <DvLabel class="caption" text="0 ~ 3600, step 10"/>
              </DvStack>
            </DvCard>
            <DvCard title="온도 설정" badge="C">
              <DvStack align="center" gap="sm">
                <DvKnob value="{Bind Data.Temp}" min="0" max="80" step="0.5" style="width:150; height:150"/>
                <DvLabel class="caption" text="0 ~ 80, step 0.5"/>
              </DvStack>
            </DvCard>
          </DvGrid>
        </DvPageFrame>
        """;

    public sealed class Vm
    {
        public double Valve { get; set; } = 45;
        public double Speed { get; set; } = 3600;
        public double Temp { get; set; } = 0;
    }

    [Fact]
    public void Render_knobs()
    {
        var path = Path.Combine(ReportsDir(), "phase6b-knob.png");
        DvRenderer.RenderToFile(Markup, new Vm(), 760, 340, path, DvTheme.Dark);
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
