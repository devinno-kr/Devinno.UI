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

    // 파일 감시 이벤트는 다른 스레드에서 늦게 온다 — 고정 시간 대신 최대 10초까지 프레임(Layout → 대기열 비우기)을 돌며 기다린다
    private static bool LayoutUntil(DvApp app, Func<bool> done)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            app.Surface.Layout(W, H);
            if (done())
                return true;
            Thread.Sleep(20);
        }
        return false;
    }

    private static (DvApp app, FilePage page, string path) Shown(string text)
    {
        var path = TempDvml($"<DvStack><DvLabel id=\"lbl\" text=\"{text}\"/></DvStack>");
        var page = new FilePage(path);
        var app = new DvApp { HotReload = true };
        app.Pages.Add(page);
        app.Show(page);
        app.Surface.Layout(W, H);
        Assert.Equal(text, LabelText(page));
        return (app, page, path);
    }

    [Fact]
    public void Saving_the_file_reloads_the_shown_page()
    {
        var (app, page, path) = Shown("처음");
        try
        {
            var attaches = page.Attaches;
            File.WriteAllText(path, "<DvStack><DvLabel id=\"lbl\" text=\"저장한 것\"/></DvStack>");
            Assert.True(LayoutUntil(app, () => LabelText(page) == "저장한 것"), $"not reloaded: '{LabelText(page)}'");
            Assert.True(page.Attaches > attaches); // 핸들을 새 상자에 다시 묶는다
            Assert.Equal("저장한 것", page.lbl?.Text);
        }
        finally
        {
            app.HotReload = false;
        }
    }

    // 편집기가 임시 파일에 쓰고 이름을 바꿔 덮어쓰는 저장(Renamed 이벤트)
    [Fact]
    public void Replacing_the_file_by_rename_reloads_the_shown_page()
    {
        var (app, page, path) = Shown("처음");
        try
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, "<DvStack><DvLabel id=\"lbl\" text=\"바꾼 것\"/></DvStack>");
            File.Move(temp, path, overwrite: true);
            Assert.True(LayoutUntil(app, () => LabelText(page) == "바꾼 것"), $"not reloaded: '{LabelText(page)}'");
        }
        finally
        {
            app.HotReload = false;
        }
    }
}
