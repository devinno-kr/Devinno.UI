using Devinno.UI;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class Samples
{
    private sealed class Vm
    {
        public string Title { get; set; } = "Device Dashboard";
        public string Status { get; set; } = "Normal";
        public string Operator { get; set; } = "Line A";
    }

    [Fact]
    public void Generate_card_dashboard()
        => Render("phase1-card.png", SampleCatalog.Card, new Vm());

    [Fact]
    public void Generate_form_dashboard()
        => Render("phase1-form.png", SampleCatalog.Form, new Vm());

    [Fact]
    public void Generate_list_dashboard()
        => Render("phase1-list.png", SampleCatalog.List, new Vm());

    [Fact]
    public void Generate_korean_dashboard()
        => Render("phase1-korean.png", SampleCatalog.Korean, new Vm());

    [Fact]
    public void Generate_korean_card_dashboard()
        => Render("phase1-card-ko.png", SampleCatalog.CardKo, new Vm());

    [Fact]
    public void Generate_korean_form_dashboard()
        => Render("phase1-form-ko.png", SampleCatalog.FormKo, new Vm());

    [Fact]
    public void Generate_korean_list_dashboard()
        => Render("phase1-list-ko.png", SampleCatalog.ListKo, new Vm());

    private static void Render(string fileName, string markup, object vm)
    {
        var path = Path.Combine(ReportsDir(), fileName);
        DvRenderer.RenderToFile(markup, vm, 420, 360, path);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
    }

    private static string ReportsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "superpowers", "reports");
            if (Directory.Exists(Path.Combine(dir.FullName, "docs", "superpowers")))
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate docs/superpowers from test output.");
    }
}

internal static class SampleCatalog
{
    public const string Card =
        "<DvScreen style=\"background:#eef1f5; padding:24; display:flex; flex-direction:column; gap:16\">" +
        "<DvLabel text=\"{Data.Title}\" style=\"font-size:26; font-weight:bold; color:#16202e\"/>" +
        "<DvBox style=\"width:260; background:#ffffff; border-radius:16; padding:20; box-shadow:0 6 18 #0000001f; display:flex; flex-direction:column; gap:8\">" +
        "<DvLabel text=\"Pump A\" style=\"font-size:18; font-weight:bold; color:#16202e\"/>" +
        "<DvLabel text=\"{Data.Status}\" style=\"font-size:15; color:#2e9e5b\"/>" +
        "</DvBox>" +
        "<DvButton text=\"Confirm\" style=\"width:140; background:#3A7BD5; border-radius:12; padding:14 24; font-size:16; font-weight:bold; color:#ffffff; text-align:center\"/>" +
        "</DvScreen>";

    public const string Form =
        "<DvScreen style=\"background:#f7f8fb; padding:24; display:flex; flex-direction:column; gap:14\">" +
        "<DvLabel text=\"Inspection Form\" style=\"font-size:24; font-weight:bold; color:#172033\"/>" +
        "<DvBox style=\"width:300; background:#ffffff; border-radius:14; padding:18; box-shadow:0 5 16 #00000018; display:flex; flex-direction:column; gap:12\">" +
        "<DvLabel text=\"Operator\" style=\"font-size:13; color:#6b7280\"/>" +
        "<DvBox style=\"width:260; height:42; background:#f1f5f9; border-radius:9; padding:10 12; border:1 solid #d8dee8\">" +
        "<DvLabel text=\"{Data.Operator}\" style=\"font-size:16; color:#172033\"/>" +
        "</DvBox>" +
        "<DvLabel text=\"Status\" style=\"font-size:13; color:#6b7280\"/>" +
        "<DvBox style=\"width:260; height:42; background:#ecfdf5; border-radius:9; padding:10 12; border:1 solid #9ee6bd\">" +
        "<DvLabel text=\"{Data.Status}\" style=\"font-size:16; font-weight:bold; color:#147d45\"/>" +
        "</DvBox>" +
        "</DvBox>" +
        "</DvScreen>";

    public const string List =
        "<DvScreen style=\"background:#edf2f7; padding:22; display:flex; flex-direction:column; gap:12\">" +
        "<DvLabel text=\"Alarm Queue\" style=\"font-size:24; font-weight:bold; color:#172033\"/>" +
        "<DvBox style=\"width:330; background:#ffffff; border-radius:14; padding:12; box-shadow:0 5 16 #00000018; display:flex; flex-direction:column; gap:8\">" +
        "<DvBox style=\"width:300; background:#fff7ed; border-radius:10; padding:12; border:1 solid #fdba74\">" +
        "<DvLabel text=\"High temperature - Pump A\" style=\"font-size:15; font-weight:bold; color:#9a3412\"/>" +
        "</DvBox>" +
        "<DvBox style=\"width:300; background:#f8fafc; border-radius:10; padding:12; border:1 solid #dbe3ec\">" +
        "<DvLabel text=\"Pressure stable - Tank 2\" style=\"font-size:15; color:#334155\"/>" +
        "</DvBox>" +
        "<DvBox style=\"width:300; background:#f8fafc; border-radius:10; padding:12; border:1 solid #dbe3ec\">" +
        "<DvLabel text=\"Valve check scheduled\" style=\"font-size:15; color:#334155\"/>" +
        "</DvBox>" +
        "</DvBox>" +
        "</DvScreen>";

    public const string Korean =
        "<DvScreen style=\"background:#eef3f8; padding:22; display:flex; flex-direction:column; gap:12\">" +
        "<DvLabel text=\"설비 대시보드\" style=\"font-size:25; font-weight:bold; color:#172033\"/>" +
        "<DvBox style=\"width:330; background:#ffffff; border-radius:14; padding:18; box-shadow:0 5 16 #00000018; display:flex; flex-direction:column; gap:10\">" +
        "<DvLabel text=\"펌프 A\" style=\"font-size:18; font-weight:bold; color:#172033\"/>" +
        "<DvLabel text=\"상태: 정상 가동\" style=\"font-size:16; color:#147d45\"/>" +
        "<DvBox style=\"width:290; background:#f8fafc; border-radius:10; padding:12; border:1 solid #dbe3ec\">" +
        "<DvLabel text=\"점검 예정: 오늘 15:30\" style=\"font-size:15; color:#334155\"/>" +
        "</DvBox>" +
        "</DvBox>" +
        "<DvButton text=\"확인\" style=\"width:130; background:#3A7BD5; border-radius:12; padding:14 24; font-size:16; font-weight:bold; color:#ffffff; text-align:center\"/>" +
        "</DvScreen>";

    public const string CardKo =
        "<DvScreen style=\"background:#eef1f5; padding:24; display:flex; flex-direction:column; gap:16\">" +
        "<DvLabel text=\"설비 대시보드\" style=\"font-size:26; font-weight:bold; color:#16202e\"/>" +
        "<DvBox style=\"width:290; background:#ffffff; border-radius:16; padding:20; box-shadow:0 6 18 #0000001f; display:flex; flex-direction:column; gap:8\">" +
        "<DvLabel text=\"펌프 A\" style=\"font-size:18; font-weight:bold; color:#16202e\"/>" +
        "<DvLabel text=\"정상 가동\" style=\"font-size:15; color:#2e9e5b\"/>" +
        "</DvBox>" +
        "<DvButton text=\"확인\" style=\"width:140; background:#3A7BD5; border-radius:12; padding:14 24; font-size:16; font-weight:bold; color:#ffffff; text-align:center\"/>" +
        "</DvScreen>";

    public const string FormKo =
        "<DvScreen style=\"background:#f7f8fb; padding:24; display:flex; flex-direction:column; gap:14\">" +
        "<DvLabel text=\"점검 입력\" style=\"font-size:24; font-weight:bold; color:#172033\"/>" +
        "<DvBox style=\"width:310; background:#ffffff; border-radius:14; padding:18; box-shadow:0 5 16 #00000018; display:flex; flex-direction:column; gap:12\">" +
        "<DvLabel text=\"작업자\" style=\"font-size:13; color:#6b7280\"/>" +
        "<DvBox style=\"width:270; height:42; background:#f1f5f9; border-radius:9; padding:10 12; border:1 solid #d8dee8\">" +
        "<DvLabel text=\"라인 A\" style=\"font-size:16; color:#172033\"/>" +
        "</DvBox>" +
        "<DvLabel text=\"상태\" style=\"font-size:13; color:#6b7280\"/>" +
        "<DvBox style=\"width:270; height:42; background:#ecfdf5; border-radius:9; padding:10 12; border:1 solid #9ee6bd\">" +
        "<DvLabel text=\"정상 가동\" style=\"font-size:16; font-weight:bold; color:#147d45\"/>" +
        "</DvBox>" +
        "</DvBox>" +
        "</DvScreen>";

    public const string ListKo =
        "<DvScreen style=\"background:#edf2f7; padding:22; display:flex; flex-direction:column; gap:12\">" +
        "<DvLabel text=\"알람 목록\" style=\"font-size:24; font-weight:bold; color:#172033\"/>" +
        "<DvBox style=\"width:330; background:#ffffff; border-radius:14; padding:12; box-shadow:0 5 16 #00000018; display:flex; flex-direction:column; gap:8\">" +
        "<DvBox style=\"width:300; background:#fff7ed; border-radius:10; padding:12; border:1 solid #fdba74\">" +
        "<DvLabel text=\"고온 경고 - 펌프 A\" style=\"font-size:15; font-weight:bold; color:#9a3412\"/>" +
        "</DvBox>" +
        "<DvBox style=\"width:300; background:#f8fafc; border-radius:10; padding:12; border:1 solid #dbe3ec\">" +
        "<DvLabel text=\"압력 안정 - 탱크 2\" style=\"font-size:15; color:#334155\"/>" +
        "</DvBox>" +
        "<DvBox style=\"width:300; background:#f8fafc; border-radius:10; padding:12; border:1 solid #dbe3ec\">" +
        "<DvLabel text=\"밸브 점검 예정\" style=\"font-size:15; color:#334155\"/>" +
        "</DvBox>" +
        "</DvBox>" +
        "</DvScreen>";
}
