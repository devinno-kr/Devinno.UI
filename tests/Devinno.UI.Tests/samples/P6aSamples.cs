using Devinno.UI;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P6aSamples
{
    private static string Cards() =>
        "<DvScreen style=\"background:#eef2f7; padding:20\">" +
        "<DvBox style=\"display:flex; flex-direction:row; flex-wrap:wrap; align-content:start; gap:14; width:560\">" +
        Card("가동률", "98%", "#16a34a") +
        Card("생산량", "1,240", "#2563eb") +
        Card("불량", "3", "#dc2626") +
        Card("온도", "72C", "#ea580c") +
        Card("압력", "4.2bar", "#7c3aed") +
        Card("가동", "ON", "#0891b2") +
        "</DvBox></DvScreen>";

    private static string Card(string title, string value, string tone) =>
        "<DvBox style=\"position:relative; width:170; height:96; background:#ffffff; border-radius:12; " +
        "padding:16; box-shadow:0 4 12 #0000001f; display:flex; flex-direction:column; gap:6\">" +
        $"<DvBox style=\"position:absolute; top:12; right:12; width:10; height:10; border-radius:5; background:{tone}\"/>" +
        $"<DvLabel text=\"{title}\" style=\"font-size:13; color:#64748b\"/>" +
        $"<DvLabel text=\"{value}\" style=\"font-size:24; font-weight:bold; color:{tone}\"/>" +
        "</DvBox>";

    private static string Toolbar() =>
        "<DvScreen style=\"background:#f8fafc; padding:20; display:flex; flex-direction:column; gap:12\">" +
        "<DvBox style=\"display:flex; flex-direction:row; align-items:center; gap:10; height:48; background:#0f172a; border-radius:10; padding:0 14\">" +
        "<DvLabel text=\"설비 모니터\" style=\"color:#ffffff; font-size:16; font-weight:bold\"/>" +
        "<DvBox style=\"flex-grow:1\"/>" +
        "<DvBox style=\"width:90; height:30; background:#2563eb; border-radius:6\"/>" +
        "</DvBox>" +
        "<DvBox style=\"max-height:80; overflow:hidden; background:#ffffff; border-radius:10; border:1 solid #e2e8f0; padding:12; display:flex; flex-direction:column; gap:6\">" +
        "<DvLabel text=\"라인 1 - 정상\" style=\"font-size:14; color:#15803d\"/>" +
        "<DvLabel text=\"라인 2 - 정상\" style=\"font-size:14; color:#15803d\"/>" +
        "<DvLabel text=\"라인 3 - 경고\" style=\"font-size:14; color:#b45309\"/>" +
        "<DvLabel text=\"라인 4 - 정상\" style=\"font-size:14; color:#15803d\"/>" +
        "<DvLabel text=\"라인 5 - 정상\" style=\"font-size:14; color:#15803d\"/>" +
        "</DvBox></DvScreen>";

    [Fact]
    public void Render_layout_primitives()
    {
        var dir = ReportsDir();
        DvRenderer.RenderToFile(Cards(), null, 600, 320, Path.Combine(dir, "phase6a-responsive-wrap.png"));
        DvRenderer.RenderToFile(Toolbar(), null, 600, 200, Path.Combine(dir, "phase6a-grow-clamp.png"));

        Assert.True(new FileInfo(Path.Combine(dir, "phase6a-responsive-wrap.png")).Length > 1000);
        Assert.True(new FileInfo(Path.Combine(dir, "phase6a-grow-clamp.png")).Length > 1000);
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
