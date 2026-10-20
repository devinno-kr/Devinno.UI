using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Controls;
using SkiaSharp;
using Xunit;

// 컨트롤 핸들 — IDvControlHost VM 은 화면이 만들어질 때 Attach 로 id 요소와 연결된다.
public class ControlAttachTests
{
    public sealed class HostVm : IDvControlHost
    {
        public DvButton? btn; public DvLabel? lbl; public int Loaded;
        public void Attach(DvControlLookup c)
        {
            var first = btn is null;
            btn = c.Bind(btn, "btn"); lbl = c.Bind(lbl, "lbl");
            if (first) Loaded++;
        }
    }

    public sealed class RepeatVm : IDvControlHost
    {
        public DvButton? btn;
        public List<int> Xs { get; } = new() { 1, 2 };
        public void Attach(DvControlLookup c) => btn = c.Bind(btn, "btn");
    }

    public sealed class ThrowVm : IDvControlHost
    {
        public void Attach(DvControlLookup c) => throw new InvalidOperationException("attach-boom");
    }

    private const string Page = "<DvStack><DvButton id=\"btn\" text=\"A\"/><DvLabel id=\"lbl\" text=\"L\"/></DvStack>";

    // Trace 리스너는 전역이라 병렬로 도는 다른 테스트 클래스의 경고도 들어온다 — 이 테스트 스레드에서 쓴 줄만 센다.
    private sealed class Capture : TraceListener
    {
        private readonly int _thread = Environment.CurrentManagedThreadId;
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (Environment.CurrentManagedThreadId == _thread && message is not null && message.Contains("Devinno.UI: id '"))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message)
            => WriteLine(message);
    }

    private static List<string> Warnings(Action act)
    {
        var cap = new Capture();
        Trace.Listeners.Add(cap);
        try { act(); }
        finally { Trace.Listeners.Remove(cap); }
        return cap.Lines;
    }

    [Fact]
    public void Attach_FillsFieldsWithIds()
    {
        var vm = new HostVm();
        var s = new DvSurface(Page, vm);
        Assert.Equal("btn", vm.btn!.Id);
        Assert.True(vm.btn.IsAttached);
        Assert.Equal("lbl", vm.lbl!.Id);
        Assert.Equal(1, vm.Loaded);
        Assert.Equal(2, s.ControlsByNode.Count);
    }

    [Fact]
    public void Reattach_ReusesHandles_LoadedOnce()
    {
        var vm = new HostVm();
        _ = new DvSurface(Page, vm);
        var first = vm.btn;
        var second = new DvSurface(Page, vm);
        Assert.Same(first, vm.btn);
        Assert.Equal(1, vm.Loaded);
        Assert.Same(second, vm.btn!.Surface);
    }

    [Fact]
    public void NonHostVm_Unchanged()
    {
        var s = new DvSurface(Page, new object());
        Assert.Empty(s.ControlsByNode);
        Assert.Empty(new DvSurface(Page, null).ControlsByNode);
    }

    [Fact]
    public void MissingId_InertHandle_NoThrow()
    {
        var vm = new HostVm();
        var lines = Warnings(() => new DvSurface("<DvStack><DvLabel id=\"lbl\"/></DvStack>", vm));
        Assert.False(vm.btn!.IsAttached);
        Assert.Equal(SKRect.Empty, vm.btn.Bounds);
        Assert.Single(lines);
        Assert.Contains("Devinno.UI: id 'btn'", lines[0]);
    }

    [Fact]
    public void TagMismatch_Inert()
    {
        var vm = new HostVm();
        var lines = Warnings(() => new DvSurface("<DvStack><DvLabel id=\"btn\"/><DvLabel id=\"lbl\"/></DvStack>", vm));
        Assert.False(vm.btn!.IsAttached);
        Assert.True(vm.lbl!.IsAttached);
        Assert.Single(lines);
    }

    [Fact]
    public void IdInsideRepeat_NotBound()
    {
        var vm = new RepeatVm();
        var lines = Warnings(() => new DvSurface("<DvStack><DvRepeat items=\"{Xs}\"><DvButton id=\"btn\"/></DvRepeat></DvStack>", vm));
        Assert.False(vm.btn!.IsAttached);
        Assert.Single(lines);
    }

    // OnLoaded 와 같은 모양 — 핸들을 연결한 뒤 던진다(생성기 Attach 의 OnLoaded 예외).
    public sealed class LoadedThrowVm : IDvControlHost
    {
        public DvButton? btn;
        public void Attach(DvControlLookup c)
        {
            btn = c.Bind(btn, "btn");
            throw new InvalidOperationException("loaded-boom");
        }
    }

    private static void AssertAttachError(List<DvHandlerErrorEventArgs> errors, string member, string message)
    {
        var e = Assert.Single(errors);
        Assert.Equal("attach", e.Kind);
        Assert.Equal(member, e.Member);
        Assert.Null(e.Tag);
        Assert.IsType<InvalidOperationException>(e.Exception);
        Assert.Equal(message, e.Exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachThrows_RaisedOnFirstLayout_Once(bool afterBind)
    {
        IDvControlHost vm = afterBind ? new LoadedThrowVm() : new ThrowVm();
        var s = new DvSurface(Page, vm); // 생성자 안에서 던져도 새지 않는다
        var errors = new List<DvHandlerErrorEventArgs>();
        s.HandlerError += (_, e) => errors.Add(e);
        Assert.Empty(errors);
        s.Layout(400, 300);
        AssertAttachError(errors, vm.GetType().Name, afterBind ? "loaded-boom" : "attach-boom");
        s.Invalidate();
        s.Layout(400, 300);
        s.Layout(500, 300);
        Assert.Single(errors);
        if (afterBind)
            Assert.True(((LoadedThrowVm)vm).btn!.IsAttached); // 던지기 전에 연결한 핸들은 동작
        else
            Assert.Empty(s.ControlsByNode);
    }

    // DvApp 칸(왼쪽 영역 옆 페이지)의 Attach 예외 — 앱 HandlerError 로 한 번, 루트 화면에도 같은 인자
    private sealed class ThrowPage(bool afterBind) : MarkupPage(Page), IDvControlHost
    {
        public DvButton? btn;

        void IDvControlHost.Attach(DvControlLookup c)
        {
            if (!afterBind)
                throw new InvalidOperationException("attach-boom");
            btn = c.Bind(btn, "btn");
            throw new InvalidOperationException("loaded-boom");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachThrows_InAppPage_ReachesAppOnce(bool afterBind)
    {
        var page = new ThrowPage(afterBind);
        var app = new Devinno.UI.App.DvApp();
        app.Pages.Add(page);
        app.Left = new MarkupBar("<DvStack style=\"width:100; padding:0\"/>");
        var errors = new List<DvHandlerErrorEventArgs>();
        var root = new List<DvHandlerErrorEventArgs>();
        app.HandlerError += (_, e) => errors.Add(e);
        app.Surface.HandlerError += (_, e) => root.Add(e);
        app.Surface.Layout(400, 300);
        AssertAttachError(errors, nameof(ThrowPage), afterBind ? "loaded-boom" : "attach-boom");
        Assert.Single(root);
        Assert.Same(errors[0], root[0]);
        app.Invalidate();
        app.Surface.Layout(400, 300);
        Assert.Single(errors);
        if (afterBind)
            Assert.True(page.btn!.IsAttached); // 던지기 전에 연결한 핸들은 동작
    }
}
