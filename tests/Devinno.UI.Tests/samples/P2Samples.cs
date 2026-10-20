using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P2Samples
{
    [Fact]
    public void Render_button_states_and_grid()
    {
        var markup =
            "<DvScreen style=\"background:linear-gradient(180deg, #eef1f5, #dce8f7); padding:24; display:flex; flex-direction:column; gap:16\">" +
            "<DvBox style=\"background:#3A7BD5; border-radius:12; padding:14 24; align-items:center; display:flex\" " +
            "hover-style=\"background:#1f5fbf\" press-style=\"background:#174472\">" +
            "<DvLabel text=\"확인\" style=\"font-size:16; font-weight:bold; color:#ffffff\"/></DvBox>" +
            "<DvBox style=\"width:360; display:grid; grid-template-columns:1fr 1fr; gap:12\">" +
            "<DvBox style=\"background:#fff; border-radius:10; padding:16; box-shadow:0 4 12 #0000001f\"><DvLabel text=\"펌프 A\" style=\"font-size:15\"/></DvBox>" +
            "<DvBox style=\"background:#fff; border-radius:10; padding:16; box-shadow:0 4 12 #0000001f\"><DvLabel text=\"펌프 B\" style=\"font-size:15\"/></DvBox>" +
            "</DvBox></DvScreen>";
        var box = new LayoutEngine().Layout(MarkupParser.Parse(markup), null, 420, 220);
        var btn = box.Children[0];

        Render("phase2-base.png", InteractionState.Empty, box);
        Render("phase2-hover.png", new InteractionState { Hover = btn }, box);
        Render("phase2-press.png", new InteractionState { Press = btn }, box);
    }

    private static void Render(string name, InteractionState state, LayoutBox box)
    {
        using var surface = SKSurface.Create(new SKImageInfo(420, 220));
        surface.Canvas.Clear(SKColors.White);
        new Painter().Paint(surface.Canvas, box, null, state);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(ReportsDir(), name);
        File.WriteAllBytes(path, data.ToArray());
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
