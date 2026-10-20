using Devinno.UI;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P6bMotionSamples
{
    private static readonly string Panel =
        "<DvScreen style=\"background:#0f172a; padding:24; display:flex; flex-direction:column; gap:20\">" +
        "<DvLabel text=\"설비 상태\" style=\"color:#e2e8f0; font-size:18; font-weight:bold\"/>" +
        "<DvBox style=\"display:flex; flex-direction:row; align-items:center; gap:24\">" +
        "<DvBox style=\"display:flex; flex-direction:row; align-items:center; gap:10\">" +
        "<DvLamp on=\"true\" style=\"color:#22c55e; width:28; height:28\"/>" +
        "<DvLabel text=\"운전\" style=\"color:#cbd5e1; font-size:14\"/></DvBox>" +
        "<DvBox style=\"display:flex; flex-direction:row; align-items:center; gap:10\">" +
        "<DvLamp on=\"false\" style=\"color:#22c55e; width:28; height:28\"/>" +
        "<DvLabel text=\"정지\" style=\"color:#cbd5e1; font-size:14\"/></DvBox>" +
        "<DvBox style=\"display:flex; flex-direction:row; align-items:center; gap:10\">" +
        "<DvLamp on=\"true\" blink=\"true\" blink-interval=\"500\" style=\"color:#ef4444; width:28; height:28\"/>" +
        "<DvLabel text=\"경보\" style=\"color:#cbd5e1; font-size:14\"/></DvBox>" +
        "</DvBox>" +
        "<DvBox style=\"display:flex; flex-direction:row; gap:18\">" +
        Gauge(42) + Gauge(72) + Gauge(93) +
        "</DvBox></DvScreen>";

    private static string Gauge(int v) =>
        "<DvBox style=\"background:#1e293b; border-radius:14; padding:12; display:flex; " +
        "flex-direction:column; align-items:center; gap:4\">" +
        $"<DvGauge value=\"{v}\" min=\"0\" max=\"100\" " +
        "alarm-zones=\"0:60:#16a34a 60:85:#eab308 85:100:#dc2626\" style=\"width:130; height:130\"/>" +
        "<DvLabel text=\"탱크 압력\" style=\"color:#94a3b8; font-size:12\"/></DvBox>";

    [Fact]
    public void Render_motion_widgets()
    {
        var dir = ReportsDir();
        DvRenderer.RenderToFile(Panel, null, 560, 320, Path.Combine(dir, "phase6b-panel-blink-on.png"), null, null, 0L);
        DvRenderer.RenderToFile(Panel, null, 560, 320, Path.Combine(dir, "phase6b-panel-blink-off.png"), null, null, 600L);

        Assert.True(new FileInfo(Path.Combine(dir, "phase6b-panel-blink-on.png")).Length > 1000);
        Assert.True(new FileInfo(Path.Combine(dir, "phase6b-panel-blink-off.png")).Length > 1000);
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
