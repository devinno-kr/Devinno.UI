using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Xunit;

// 핫리로드(P7-4) — DvApp.HotReload 가 화면 dvml 파일을 감시하다 저장하면 다음 프레임에 그 화면을 새 마크업으로 바꿔 끼운다.
public class HotReloadTests
{
    private const float W = 600, H = 400;

    private sealed class FilePage(string file) : DvPage, IDvControlHost
    {
        public DvLabel? lbl;
        public int Attaches;
        public string Name { get; set; } = "펌프";
        protected override string MarkupSource => File.ReadAllText(file); // 빌드 때 원문 흉내
        protected override string? MarkupFile => file;
        public void Attach(DvControlLookup c)
        {
            lbl = c.Bind(lbl, "lbl");
            Attaches++;
        }
    }

    private static string TempDvml(string markup)
    {
        var dir = Path.Combine(Path.GetTempPath(), "dvui-hot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Page.dvml");
        File.WriteAllText(path, markup);
        return path;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static string? LabelText(DvPage page)
        => page.Surface?.Root is { } r ? Flatten(r).FirstOrDefault(b => b.Node.Tag == "DvLabel")?.ResolvedText : null;

    [Fact]
    public void Off_by_default_and_not_yet_built_pages_read_latest_file()
    {
        var path = TempDvml("<DvStack><DvLabel text=\"빌드 때\"/></DvStack>");
        var off = new DvApp();
        Assert.False(off.HotReload);

        var page = new FilePage(path);
        var other = new FilePage(TempDvml("<DvStack><DvLabel text=\"처음\"/></DvStack>"));
        var app = new DvApp { HotReload = true };
        app.Pages.Add(other);
        app.Pages.Add(page);
        app.Show(other);
        app.Surface.Layout(W, H);
        File.WriteAllText(path, "<DvStack><DvLabel text=\"저장한 것\"/></DvStack>");
        app.Show(page);
        app.Surface.Layout(W, H);
        Assert.Equal("저장한 것", LabelText(page));
        app.HotReload = false;
    }
}
