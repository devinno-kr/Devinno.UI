using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Controls;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Xunit;

// DvApp 페이지 관리(보관·전환·수명 알림·스레드·전역 설정) — 그리기는 Task 3
public class AppPagesTests
{
    // 생성기 대신 손으로 MarkupSource·Attach 를 채운 테스트 페이지. loaded 는 생성 코드처럼 인스턴스당 1회.
    private class TestPage(string name, List<string>? log = null) : DvPage, IDvControlHost
    {
        private bool _loaded;
        public string Name { get; } = name;
        public List<string> Log { get; } = log ?? new();
        public List<int> Threads { get; } = new();
        public Exception? ThrowOnShown { get; set; }
        public Exception? ThrowOnHidden { get; set; }
        public Exception? ThrowOnLoaded { get; set; }

        protected override string MarkupSource => "<DvScreen><DvButton id=\"btn\" text=\"" + Name + "\"/></DvScreen>";

        void IDvControlHost.Attach(DvControlLookup controls)
        {
            if (_loaded) return;
            _loaded = true;
            Record("loaded");
            if (ThrowOnLoaded is { } ex) throw ex;
        }

        protected override void OnShown()
        {
            Record("shown");
            if (ThrowOnShown is { } ex) throw ex;
        }

        protected override void OnHidden()
        {
            Record("hidden");
            if (ThrowOnHidden is { } ex) throw ex;
        }

        private void Record(string what)
        {
            Log.Add(Name + "." + what);
            Threads.Add(Environment.CurrentManagedThreadId);
        }
    }

    private sealed class OtherPage(string name, List<string>? log = null) : TestPage(name, log);

    private sealed class TestBar : DvBar
    {
        protected override string MarkupSource => "<DvStack style=\"height:40\"/>";
    }

    [Fact]
    public void Page_App_ThrowsBeforeAdd()
    {
        var app = new DvApp();
        var a = new TestPage("A");
        Assert.Throws<InvalidOperationException>(() => a.App);
        app.Pages.Add(a);
        Assert.Same(app, a.App);
    }

    [Fact]
    public void Add_Duplicate_Ignored()
    {
        var app = new DvApp();
        var a = new TestPage("A");
        app.Pages.Add(a);
        app.Pages.Add(a);
        Assert.Equal(1, app.Pages.Count);
        Assert.True(app.Pages.Contains(a));
        Assert.Equal(new DvPage[] { a }, app.Pages.ToArray());
    }

    [Fact]
    public void Add_OtherApp_Throws()
    {
        var a = new TestPage("A");
        new DvApp().Pages.Add(a);
        var other = new DvApp();
        Assert.Throws<InvalidOperationException>(() => other.Pages.Add(a));
        Assert.Equal(0, other.Pages.Count);
        Assert.Throws<InvalidOperationException>(() => other.Show(a));
    }

    [Fact]
    public void Get_None_Throws()
    {
        var app = new DvApp();
        app.Pages.Add(new TestPage("A"));
        Assert.Throws<InvalidOperationException>(() => app.Pages.Get<OtherPage>());
        Assert.Throws<InvalidOperationException>(() => app.Show<OtherPage>());
    }

    [Fact]
    public void Get_Two_Throws()
    {
        var app = new DvApp();
        app.Pages.Add(new OtherPage("P1"));
        app.Pages.Add(new OtherPage("P2"));
        Assert.Throws<InvalidOperationException>(() => app.Pages.Get<OtherPage>());

        var single = new DvApp();
        var p = new OtherPage("P");
        single.Pages.Add(p);
        Assert.Same(p, single.Pages.Get<OtherPage>());
    }

    [Fact]
    public void Show_NotAdded_AddsAutomatically()
    {
        var app = new DvApp();
        var a = new TestPage("A");
        app.Show(a);
        Assert.True(app.Pages.Contains(a));
        Assert.Same(app, a.App);
        app.ApplyPending();
        Assert.Same(a, app.CurrentPage);
    }

    [Fact]
    public void Show_Same_NoCallbacks()
    {
        var app = new DvApp();
        var a = new TestPage("A");
        app.Show(a);
        app.ApplyPending();
        a.Log.Clear();

        app.Show(a);
        app.ApplyPending();
        Assert.Empty(a.Log);
        Assert.Same(a, app.CurrentPage);
    }

    [Fact]
    public void StartPage_IsFirstAdded()
    {
        var app = new DvApp();
        var a = new TestPage("A");
        var b = new OtherPage("B");
        app.Pages.Add(a);
        app.Pages.Add(b);
        Assert.Null(app.CurrentPage);

        app.ApplyPending();
        Assert.Same(a, app.CurrentPage);
        Assert.Equal(new[] { "A.loaded", "A.shown" }, a.Log);
        Assert.Empty(b.Log);
    }

    [Fact]
    public void StartPage_EmptyApp_StaysNull()
    {
        var app = new DvApp();
        app.ApplyPending();
        Assert.Null(app.CurrentPage);
    }

    [Fact]
    public void Lifecycle_Order()
    {
        var log = new List<string>();
        var app = new DvApp();
        var a = new TestPage("A", log);
        var b = new OtherPage("B", log);
        app.Pages.Add(a);
        app.Pages.Add(b);

        app.ApplyPending();               // 시작 페이지 A
        app.Show<OtherPage>();
        app.ApplyPending();
        app.Show(a);
        app.ApplyPending();

        Assert.Equal(new[] { "A.loaded", "A.shown", "A.hidden", "B.loaded", "B.shown", "B.hidden", "A.shown" }, log);
        Assert.Same(a, app.CurrentPage);
        Assert.Same(app.SurfaceOf(a), app.SurfaceOf(a)); // 보관 — 다시 만들지 않음
    }

    [Fact]
    public void Show_FromOtherThread_AppliedOnNextApply_OnCallingThread()
    {
        var app = new DvApp();
        var a = new TestPage("A");
        var b = new OtherPage("B");
        app.Pages.Add(a);
        app.Pages.Add(b);
        app.ApplyPending();
        a.Log.Clear(); a.Threads.Clear();

        var bg = new Thread(() => app.Show<OtherPage>());
        bg.Start();
        bg.Join();

        Assert.Empty(a.Log);
        Assert.Empty(b.Log);
        Assert.Same(a, app.CurrentPage);

        app.ApplyPending();
        var main = Environment.CurrentManagedThreadId;
        Assert.Same(b, app.CurrentPage);
        Assert.Equal(new[] { "A.hidden" }, a.Log);
        Assert.Equal(new[] { "B.loaded", "B.shown" }, b.Log);
        Assert.All(a.Threads.Concat(b.Threads), id => Assert.Equal(main, id));
    }

    [Fact]
    public void Shown_Throws_ReportsAndContinues()
    {
        var app = new DvApp();
        var errors = new List<DvHandlerErrorEventArgs>();
        app.HandlerError += (_, e) => errors.Add(e);
        var a = new TestPage("A") { ThrowOnHidden = new InvalidOperationException("h") };
        var b = new OtherPage("B") { ThrowOnShown = new InvalidOperationException("s") };
        app.Pages.Add(a);
        app.Pages.Add(b);
        app.ApplyPending();

        app.Show(b);
        app.ApplyPending();

        Assert.Same(b, app.CurrentPage);
        Assert.Equal(new[] { "B.loaded", "B.shown" }, b.Log);
        Assert.Equal(new[] { "hidden", "shown" }, errors.Select(e => e.Kind));
        Assert.Equal("h", errors[0].Exception.Message);
        Assert.Equal("s", errors[1].Exception.Message);
        Assert.Equal(nameof(TestPage), errors[0].Member);
        Assert.Equal(nameof(OtherPage), errors[1].Member);
    }

    [Fact]
    public void AttachError_ForwardedToAppHandlerError()
    {
        var app = new DvApp();
        var errors = new List<DvHandlerErrorEventArgs>();
        app.HandlerError += (_, e) => errors.Add(e);
        var a = new TestPage("A") { ThrowOnLoaded = new InvalidOperationException("l") };
        app.Pages.Add(a);
        app.ApplyPending();
        Assert.Same(a, app.CurrentPage);
        Assert.Equal(new[] { "A.loaded", "A.shown" }, a.Log);

        app.SurfaceOf(a).Layout(200, 100); // 생성자 Attach 예외는 첫 Layout 에서 알림
        var e = Assert.Single(errors);
        Assert.Equal("attach", e.Kind);
        Assert.Equal("l", e.Exception.Message);
    }

    [Fact]
    public void Theme_Change_RetunesAllKeptSurfaces()
    {
        var app = new DvApp { Theme = DvTheme.Dark };
        var a = new TestPage("A");
        var b = new OtherPage("B");
        app.Pages.Add(a);
        app.Pages.Add(b);
        app.ApplyPending();
        app.Show(b);
        app.ApplyPending();
        var sa = app.SurfaceOf(a);
        var sb = app.SurfaceOf(b);
        Assert.Same(DvTheme.Dark, sa.Theme);
        Assert.Same(DvTheme.Dark, sb.Theme);

        var strings = new DvStrings();
        var registry = new Devinno.UI.Components.ComponentRegistry();
        app.Theme = DvTheme.Light;
        app.Strings = strings;
        app.Registry = registry;
        Assert.Same(DvTheme.Light, app.Theme);
        Assert.Same(DvTheme.Dark, sa.Theme); // 다른 스레드에서 바꿔도 되도록 — 적용은 다음 ApplyPending

        app.ApplyPending();
        foreach (var s in new[] { sa, sb })
        {
            Assert.Same(DvTheme.Light, s.Theme);
            Assert.Same(strings, s.Strings);
            Assert.Same(registry, s.Registry);
        }
        Assert.Same(sa, app.SurfaceOf(a)); // 같은 화면을 재빌드(새로 만들지 않음)
    }

    [Fact]
    public void NewSurface_UsesCurrentSettings()
    {
        var strings = new DvStrings();
        var app = new DvApp { Theme = DvTheme.Light, Strings = strings };
        var a = new TestPage("A");
        app.Pages.Add(a);
        app.ApplyPending();
        var s = app.SurfaceOf(a);
        Assert.Same(DvTheme.Light, s.Theme);
        Assert.Same(strings, s.Strings);
    }

    [Fact]
    public void Region_Assign_Queued_SetsApp()
    {
        var app = new DvApp();
        var bar = new TestBar();
        Assert.Throws<InvalidOperationException>(() => bar.App);
        app.Left = bar;
        Assert.Same(bar, app.Left);
        Assert.Same(app, bar.App);
        Assert.Null(app.AppliedLeft);

        app.ApplyPending();
        Assert.Same(bar, app.AppliedLeft);
        Assert.Same(app.SurfaceOf(bar), app.SurfaceOf(bar));

        app.Left = null;
        Assert.Same(bar, app.AppliedLeft);
        app.ApplyPending();
        Assert.Null(app.AppliedLeft);

        Assert.Throws<InvalidOperationException>(() => new DvApp().Top = bar);
    }
    private sealed class BrokenPage : DvPage
    {
        protected override string MarkupSource => "<DvScreen><DvButton text=\"x\"></DvScreen>"; // 닫는 태그 불일치
    }

    // 마크업 오류 페이지로 전환 → 루트 Layout 이 XmlException 을 그대로 던지고 현재 페이지는 이전 페이지로 남는다
    // (이전 페이지 OnHidden 은 이미 불렸다 — 문서화된 동작). 다음 프레임은 정상.
    [Fact]
    public void Show_InvalidMarkup_LayoutThrows_CurrentPageUnchanged()
    {
        var app = new DvApp();
        var log = new List<string>();
        var a = new TestPage("A", log);
        app.Pages.Add(a);
        app.Surface.Layout(800, 600);
        Assert.Same(a, app.CurrentPage);
        log.Clear();

        var broken = new BrokenPage();
        app.Show(broken);
        Assert.Throws<System.Xml.XmlException>(() => app.Surface.Layout(800, 600));
        Assert.Same(a, app.CurrentPage);
        Assert.Null(broken.Surface);
        Assert.Equal(new[] { "A.hidden" }, log);

        app.Surface.Layout(800, 600); // 요청은 소비됨 — 다시 던지지 않는다
        Assert.Same(a, app.CurrentPage);
    }
}
