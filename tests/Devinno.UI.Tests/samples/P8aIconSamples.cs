using Devinno.UI;
using Devinno.UI.Styling;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P8aIconSamples
{
    public const string Markup = """
        <DvPageFrame title="아이콘" subtitle="Font Awesome 6 Free — fa-이름 / far fa-이름 / fab fa-이름">
          <DvGrid columns="2">
            <DvCard title="DvIcon">
              <DvStack gap="md">
                <DvRow gap="lg">
                  <DvIcon icon="fa-gear"/>
                  <DvIcon icon="fa-fan"/>
                  <DvIcon icon="fa-temperature-half"/>
                  <DvIcon icon="fa-bolt"/>
                  <DvIcon icon="fa-triangle-exclamation" style="color:token(tone-warning)"/>
                  <DvIcon icon="fa-circle-xmark" style="color:token(tone-danger)"/>
                  <DvIcon icon="fa-circle-check" style="color:token(tone-success)"/>
                </DvRow>
                <DvRow gap="lg">
                  <DvIcon icon="far fa-star"/>
                  <DvIcon icon="fa-star"/>
                  <DvIcon icon="far fa-bell"/>
                  <DvIcon icon="fa-bell"/>
                  <DvIcon icon="fab fa-windows"/>
                  <DvIcon icon="fab fa-linux"/>
                </DvRow>
                <DvRow gap="lg" align="end">
                  <DvIcon icon="fa-industry" style="font-size:16"/>
                  <DvIcon icon="fa-industry"/>
                  <DvIcon icon="fa-industry" style="font-size:36"/>
                  <DvIcon icon="fa-industry" style="font-size:48; color:token(primary)"/>
                </DvRow>
              </DvStack>
            </DvCard>
            <DvCard title="DvButton icon">
              <DvStack gap="sm">
                <DvRow>
                  <DvButton text="저장" icon="fa-floppy-disk" variant="primary"/>
                  <DvButton text="설정" icon="fa-gear" variant="secondary"/>
                  <DvButton text="정지" icon="fa-stop" variant="danger"/>
                </DvRow>
                <DvRow>
                  <DvButton icon="fa-play" variant="primary"/>
                  <DvButton icon="fa-pause" variant="secondary"/>
                  <DvButton icon="fa-rotate-right" variant="ghost"/>
                  <DvButton text="새로고침" icon="fa-rotate-right" variant="ghost" size="sm"/>
                </DvRow>
                <DvButton text="큰 버튼" icon="fa-power-off" variant="primary" size="lg"/>
              </DvStack>
            </DvCard>
          </DvGrid>
        </DvPageFrame>
        """;

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_icons(string themeName)
    {
        var path = Path.Combine(ReportsDir(), $"phase8a-icons-{themeName}.png");
        DvRenderer.RenderToFile(Markup, null, 800, 380, path, themeName == "light" ? DvTheme.Light : DvTheme.Dark);
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
