using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 코드에서 덮어쓴 Enabled/Visible/Text 가 마크업·바인딩 값보다 우선해 레이아웃에 반영된다.
public class ControlOverrideTests
{
    public sealed class Vm : IDvControlHost
    {
        public DvButton? btn; public DvLabel? lbl;
        public int Went; public bool Locked { get; set; }
        public string X { get; set; } = "x";
        public void Go() => Went++;
        public void Attach(DvControlLookup c) { btn = c.Bind(btn, "btn"); lbl = c.Bind(lbl, "lbl"); }
    }

    private const string Page =
        "<DvStack><DvButton id=\"btn\" text=\"A\" disabled=\"{Locked}\" onclick=\"Go\"/><DvLabel id=\"lbl\" text=\"{X}!\"/></DvStack>";

    private static (Vm vm, DvSurface s) Make(string markup = Page)
    {
        var vm = new Vm();
        var s = new DvSurface(markup, vm);
        s.Layout(400, 300);
        return (vm, s);
    }

    private static LayoutBox? Box(DvSurface s, string tag) => LayoutQuery.FindByTag(s.Root!, tag);

    [Fact]
    public void Enabled_False_DisablesAndOverridesBinding()
    {
        var (vm, s) = Make();
        Assert.False(Box(s, "DvButton")!.Disabled);
        vm.btn!.Enabled = false;
        s.Layout(400, 300);
        var b = Box(s, "DvButton")!;
        Assert.True(b.Disabled);
        var c = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(c); s.PointerUp(c);
        Assert.Equal(0, vm.Went);
    }

    [Fact]
    public void Enabled_True_BeatsMarkupDisabled()
    {
        var vm = new Vm { Locked = true };
        var s = new DvSurface(Page, vm);
        s.Layout(400, 300);
        Assert.True(Box(s, "DvButton")!.Disabled);
        vm.btn!.Enabled = true;
        s.Layout(400, 300);
        Assert.False(Box(s, "DvButton")!.Disabled);
    }

    [Fact]
    public void Visible_False_RemovesBox()
    {
        var (vm, s) = Make();
        vm.btn!.Visible = false;
        s.Layout(400, 300);
        Assert.Null(Box(s, "DvButton"));
    }

    [Fact]
    public void Visible_True_BeatsMarkupFalse()
    {
        var (vm, s) = Make("<DvStack><DvButton id=\"btn\" visible=\"false\"/><DvLabel id=\"lbl\"/></DvStack>");
        Assert.Null(Box(s, "DvButton"));
        vm.btn!.Visible = true;
        s.Layout(400, 300);
        Assert.NotNull(Box(s, "DvButton"));
    }

    [Fact]
    public void Text_IsLiteral_NoBinding()
    {
        var (vm, s) = Make();
        Assert.Equal("x!", Box(s, "DvLabel")!.ResolvedText);
        vm.lbl!.Text = "{X} 그대로";
        s.Layout(400, 300);
        Assert.Equal("{X} 그대로", Box(s, "DvLabel")!.ResolvedText);
    }

    [Fact]
    public void Text_Null_ReturnsToMarkup()
    {
        var (vm, s) = Make();
        vm.lbl!.Text = "zz";
        s.Layout(400, 300);
        vm.lbl.Text = null;
        s.Layout(400, 300);
        Assert.Equal("x!", Box(s, "DvLabel")!.ResolvedText);
    }

    [Fact]
    public void ResetOverrides_ReturnsAll()
    {
        var (vm, s) = Make();
        vm.btn!.Enabled = false; vm.btn.Visible = false; vm.btn.Text = "T";
        s.Layout(400, 300);
        vm.btn.ResetOverrides();
        s.Layout(400, 300);
        var b = Box(s, "DvButton")!;
        Assert.False(b.Disabled);
        Assert.Equal("A", b.ResolvedText);
    }

    [Fact]
    public void Override_SurvivesNewSurface()
    {
        var (vm, _) = Make();
        vm.btn!.Enabled = false;
        var s2 = new DvSurface(Page, vm);
        s2.Layout(400, 300);
        Assert.True(Box(s2, "DvButton")!.Disabled);
    }

    private sealed class OverridePage() : MarkupPage(Page), IDvControlHost
    {
        public DvButton? btn;
        public bool Locked { get; set; }
        public string X { get; set; } = "x";
        public void Go() { }
        void IDvControlHost.Attach(DvControlLookup c) => btn = c.Bind(btn, "btn");
    }

    [Fact]
    public void Override_SurvivesAppThemeChange()
    {
        var page = new OverridePage();
        var app = new Devinno.UI.App.DvApp();
        app.Pages.Add(page);
        app.Surface.Layout(400, 300);
        page.btn!.Enabled = false;
        app.Theme = DvTheme.Light; // 같은 화면을 다음 Layout 에서 재빌드
        app.Surface.Layout(400, 300);
        Assert.Same(DvTheme.Light, page.Surface!.Theme);
        Assert.True(Box(page.Surface, "DvButton")!.Disabled);
    }

    [Fact]
    public void ConcurrentWrites_NoTearing()
    {
        var (vm, s) = Make();
        var stop = false;
        var layouts = 0;
        Exception? err = null;
        using var started = new ManualResetEventSlim();
        var t = new Thread(() =>
        {
            try
            {
                started.Set();
                while (!Volatile.Read(ref stop)) { s.Invalidate(); s.Layout(400, 300); Interlocked.Increment(ref layouts); }
            }
            catch (Exception e) { err = e; }
        });
        t.Start();
        Assert.True(started.Wait(5000));
        // 쓰기 1000회를 마치고도 레이아웃이 50번 이상 돌 때까지 계속 써서 겹침을 보장한다
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; (i < 1000 || Volatile.Read(ref layouts) < 50) && sw.ElapsedMilliseconds < 10000; i++)
        {
            vm.btn!.Text = "t" + i;
            vm.btn.Enabled = i % 2 == 0;
            vm.lbl!.Visible = i % 3 != 0;
        }
        Volatile.Write(ref stop, true);
        t.Join();
        Assert.Null(err);
        Assert.True(layouts >= 50, $"layouts={layouts}");
    }

    [Fact]
    public void Write_Invalidates()
    {
        var (vm, s) = Make();
        s.RenderTo(new SKBitmap(400, 300) is var bmp ? new SKCanvas(bmp) : null!, 400, 300, 0);
        Assert.False(s.NeedsRender);
        vm.btn!.Enabled = false;
        Assert.True(s.NeedsRender);
    }
}
