using Devinno.UI;
using Devinno.UI.Components;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P4Samples
{
    [Fact]
    public void Render_reusable_cards()
    {
        var reg = new ComponentRegistry();
        reg.Register(
            "<DvComponent name=\"StatCard\">" +
            "<DvParam name=\"Title\" default=\"-\"/>" +
            "<DvParam name=\"Value\" default=\"-\"/>" +
            "<DvParam name=\"Tone\" default=\"#16202e\"/>" +
            "<DvBox style=\"background:#ffffff; border-radius:12; padding:16; box-shadow:0 4 12 #0000001f; display:flex; flex-direction:column; gap:6\">" +
            "<DvLabel text=\"{Title}\" style=\"font-size:13; color:#64748b\"/>" +
            "<DvLabel text=\"{Value}\" style=\"font-size:22; font-weight:bold; color:{Tone}\"/>" +
            "</DvBox>" +
            "</DvComponent>");

        var markup =
            "<DvScreen style=\"background:#eef1f5; padding:20; display:flex; flex-direction:row; gap:14\">" +
            "<StatCard title=\"펌프 A\" value=\"정상\" tone=\"#15803d\"/>" +
            "<StatCard title=\"펌프 B\" value=\"경고\" tone=\"#b45309\"/>" +
            "<StatCard title=\"라인\" value=\"가동\" tone=\"#2563eb\"/>" +
            "</DvScreen>";

        var path = Path.Combine(ReportsDir(), "phase4-cards.png");
        DvRenderer.RenderToFile(markup, null, 460, 130, path, reg);

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
