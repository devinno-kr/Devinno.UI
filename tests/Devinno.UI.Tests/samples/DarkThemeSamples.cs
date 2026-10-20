using Devinno.UI;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class DarkThemeSamples
{
    private sealed class Vm
    {
        public bool PumpEnabled { get; set; }
    }

    [Fact]
    public void Render_dark_theme_samples()
    {
        RenderToFile("dark-card.png", CardMarkup(), 420, 360);
        RenderToFile("dark-form.png", FormMarkup(), 420, 360);
        RenderToFile("dark-list.png", ListMarkup(), 420, 360);
        RenderState("dark-p2-states-base.png", null);
        RenderState("dark-p2-states-hover.png", StateKind.Hover);
        RenderState("dark-p2-states-press.png", StateKind.Press);
        RenderToggle("dark-p3-toggle-off.png", false);
        RenderToggle("dark-p3-toggle-on.png", true);
    }

    private static void RenderToFile(string name, string markup, int width, int height)
    {
        var path = Path.Combine(ReportsDir(), name);
        DvRenderer.RenderToFile(markup, null, width, height, path, DvTheme.Dark);
        Assert.True(new FileInfo(path).Length > 1000);
    }

    private static void RenderState(string name, StateKind? stateKind)
    {
        var root = MarkupParser.Parse(StatesMarkup());
        var box = new LayoutEngine(DvTheme.Dark).Layout(root, null, 420, 260);
        var target = box.Children[2];
        var state = new InteractionState
        {
            Hover = stateKind == StateKind.Hover ? target : null,
            Press = stateKind == StateKind.Press ? target : null
        };

        using var surface = SKSurface.Create(new SKImageInfo(420, 260));
        surface.Canvas.Clear(SKColors.White);
        new Painter(DvTheme.Dark).Paint(surface.Canvas, box, null, state);
        WritePng(surface, name);
    }

    private static void RenderToggle(string name, bool on)
    {
        var vm = new Vm { PumpEnabled = on };
        var root = MarkupParser.Parse(ToggleMarkup());
        var box = new LayoutEngine(DvTheme.Dark).Layout(root, vm, 260, 150);

        using var surface = SKSurface.Create(new SKImageInfo(260, 150));
        surface.Canvas.Clear(SKColors.White);
        new Painter(DvTheme.Dark).Paint(surface.Canvas, box, vm);
        WritePng(surface, name);
    }

    private static void WritePng(SKSurface surface, string name)
    {
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(ReportsDir(), name);
        File.WriteAllBytes(path, data.ToArray());
        Assert.True(new FileInfo(path).Length > 1000);
    }

    private static string CardMarkup() =>
        "<DvScreen style=\"background:linear-gradient(180deg, var(--bg), var(--bg2)); padding:24; display:flex; flex-direction:column; gap:16\">" +
        "<DvLabel text=\"다크 대시보드\" style=\"font-size:26; font-weight:bold; color:var(--text)\"/>" +
        "<DvBox style=\"width:288; background:var(--surface); border-radius:16; padding:20; box-shadow:0 8 22 var(--shadow); display:flex; flex-direction:column; gap:8; border:1 solid var(--border)\">" +
        "<DvLabel text=\"펌프 A\" style=\"font-size:18; font-weight:bold; color:var(--text)\"/>" +
        "<DvLabel text=\"정상\" style=\"font-size:15; color:var(--success)\"/>" +
        "<DvLabel text=\"유량 32.8 L/min\" style=\"font-size:14; color:var(--muted)\"/>" +
        "</DvBox>" +
        "<DvButton text=\"확인\" style=\"width:144; background:var(--primary); border-radius:12; padding:14 24; font-size:16; font-weight:bold; color:#ffffff; text-align:center\"/>" +
        "</DvScreen>";

    private static string FormMarkup() =>
        "<DvScreen style=\"background:var(--bg); padding:22; display:flex; flex-direction:column; gap:14\">" +
        "<DvLabel text=\"작업자 입력\" style=\"font-size:24; font-weight:bold; color:var(--text)\"/>" +
        "<DvBox style=\"width:328; background:var(--surface); border-radius:14; padding:18; border:1 solid var(--border); display:flex; flex-direction:column; gap:12\">" +
        "<DvLabel text=\"배치 번호\" style=\"font-size:13; color:var(--muted)\"/>" +
        "<DvBox style=\"height:42; background:var(--surface-alt); border-radius:10; border:1 solid var(--border); padding:10\"><DvLabel text=\"B-2026-0606\" style=\"font-size:15; color:var(--text)\"/></DvBox>" +
        "<DvLabel text=\"압력 제한\" style=\"font-size:13; color:var(--muted)\"/>" +
        "<DvBox style=\"height:42; background:var(--surface-alt); border-radius:10; border:1 solid var(--border-warn); padding:10\"><DvLabel text=\"8.4 bar\" style=\"font-size:15; color:var(--warn-text)\"/></DvBox>" +
        "</DvBox>" +
        "</DvScreen>";

    private static string ListMarkup() =>
        "<DvScreen style=\"background:var(--bg); padding:22; display:flex; flex-direction:column; gap:12\">" +
        "<DvLabel text=\"알람 목록\" style=\"font-size:24; font-weight:bold; color:var(--text)\"/>" +
        "<DvBox style=\"display:flex; flex-direction:column; gap:8\">" +
        "<DvBox style=\"width:330; background:var(--surface); border-radius:12; border:1 solid var(--border); padding:14; display:flex; flex-direction:column; gap:4\"><DvLabel text=\"믹서 준비 완료\" style=\"font-size:16; font-weight:bold; color:var(--text)\"/><DvLabel text=\"12초 전\" style=\"font-size:13; color:var(--muted)\"/></DvBox>" +
        "<DvBox style=\"width:330; background:var(--warn-bg); border-radius:12; border:1 solid var(--border-warn); padding:14; display:flex; flex-direction:column; gap:4\"><DvLabel text=\"압력 경고\" style=\"font-size:16; font-weight:bold; color:var(--warn-text)\"/><DvLabel text=\"B 라인 센서\" style=\"font-size:13; color:var(--warn-text)\"/></DvBox>" +
        "<DvBox style=\"width:330; background:var(--success-bg); border-radius:12; border:1 solid var(--success-border); padding:14; display:flex; flex-direction:column; gap:4\"><DvLabel text=\"검사 통과\" style=\"font-size:16; font-weight:bold; color:var(--success)\"/><DvLabel text=\"품질 게이트\" style=\"font-size:13; color:var(--success)\"/></DvBox>" +
        "</DvBox>" +
        "</DvScreen>";

    private static string StatesMarkup() =>
        "<DvScreen style=\"background:linear-gradient(180deg, var(--bg), var(--bg2)); padding:24; display:flex; flex-direction:column; gap:12\">" +
        "<DvLabel text=\"버튼 상태\" style=\"font-size:22; font-weight:bold; color:var(--text)\"/>" +
        "<DvLabel text=\"기본 / 호버 / 누름\" style=\"font-size:13; color:var(--muted)\"/>" +
        "<DvButton text=\"사이클 시작\" style=\"width:160; background:var(--primary); border-radius:12; padding:14 22; color:#ffffff; font-size:16; font-weight:bold; text-align:center; box-shadow:0 8 20 var(--shadow)\" hover-style=\"background:var(--primary-hover)\" press-style=\"background:var(--primary-press); opacity:0.9\"/>" +
        "</DvScreen>";

    private static string ToggleMarkup() =>
        "<DvScreen style=\"background:var(--bg); padding:24; display:flex; flex-direction:column; gap:14\">" +
        "<DvLabel text=\"펌프 전원\" style=\"font-size:20; font-weight:bold; color:var(--text)\"/>" +
        "<DvToggle value=\"{Bind Data.PumpEnabled}\" style=\"width:64; height:34\"/>" +
        "</DvScreen>";

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

    private enum StateKind
    {
        Hover,
        Press
    }
}
