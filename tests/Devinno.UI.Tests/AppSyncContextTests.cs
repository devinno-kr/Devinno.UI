using Devinno.UI;
using Devinno.UI.App;
using SkiaSharp;
using Xunit;

// 8c-1 Task 2 — 앱 루트 입력 진입점의 동기화 컨텍스트(D15): await 이어짐은 다음 프레임 입력 스레드, async void 예외는 HandlerError("async")
public class AppSyncContextTests
{
    private const float W = 800, H = 600;

    private const string Markup = """
        <DvScreen style="padding:0">
          <DvButton id="btn" text="P" onclick="Hit" style="position:absolute; left:100; top:50; width:80; height:40"/>
        </DvScreen>
        """;

    private sealed class YieldPage : DvPage
    {
        public int Before, After;
        public bool Resumed;
        protected override string MarkupSource => Markup;

        public async void Hit()
        {
            Before = Environment.CurrentManagedThreadId;
            await Task.Yield();
            After = Environment.CurrentManagedThreadId;
            Resumed = true;
        }
    }

    private sealed class ThrowAfterAwaitPage : DvPage
    {
        protected override string MarkupSource => Markup;

        public async void Hit()
        {
            await Task.Yield();
            throw new InvalidOperationException("x");
        }
    }

    private sealed class ThrowBeforeAwaitPage : DvPage
    {
        protected override string MarkupSource => Markup;

        public async void Hit()
        {
            if (Environment.TickCount64 != 0) // 컴파일러 경고(도달 불가 코드) 회피 — 항상 참
                throw new InvalidOperationException("y");
            await Task.Yield();
        }
    }

    private sealed class Vm
    {
        public SynchronizationContext? Seen;
        public bool Called;

        public void Hit()
        {
            Called = true;
            Seen = SynchronizationContext.Current;
        }
    }

    private static (DvApp App, DvSurface Root, List<DvHandlerErrorEventArgs> Errors) Start(DvPage page)
    {
        var app = new DvApp();
        app.Pages.Add(page);
        var errors = new List<DvHandlerErrorEventArgs>();
        app.HandlerError += (_, e) => errors.Add(e);
        var root = app.Surface;
        root.Layout(W, H);
        return (app, root, errors);
    }

    private static void Tap(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
    }

    [Fact]
    public void AwaitInHandler_ResumesOnNextFrame_OnInputThread()
    {
        var page = new YieldPage();
        var (_, root, errors) = Start(page);

        Tap(root, 140, 70);
        Assert.NotEqual(0, page.Before);
        Assert.False(page.Resumed); // Yield 는 컨텍스트로 Post — 아직 안 돌았다

        root.Layout(W, H);
        Assert.True(page.Resumed);
        Assert.Equal(page.Before, page.After);
        Assert.Empty(errors);
    }

    [Fact]
    public void AsyncVoid_ExceptionAfterAwait_GoesToHandlerError()
    {
        var (_, root, errors) = Start(new ThrowAfterAwaitPage());

        Tap(root, 140, 70);
        root.Layout(W, H); // 이어짐 실행 → 예외를 builder 가 Post
        root.Layout(W, H); // 그 Post 실행 → HandlerError

        var e = Assert.Single(errors);
        Assert.Equal("async", e.Kind);
        Assert.IsType<InvalidOperationException>(e.Exception);
        Assert.Equal("x", e.Exception.Message);
        Assert.Null(e.Member);
    }

    [Fact]
    public void AsyncVoid_ExceptionBeforeAwait_GoesToHandlerError()
    {
        var (_, root, errors) = Start(new ThrowBeforeAwaitPage());

        Tap(root, 140, 70);
        Assert.Empty(errors); // 동기 부분 예외도 builder 가 컨텍스트로 Post — 처리기 호출부(onclick)로는 오지 않는다
        root.Layout(W, H);

        var e = Assert.Single(errors);
        Assert.Equal("async", e.Kind);
        Assert.Equal("y", e.Exception.Message);
    }

    [Fact]
    public void PostDuringDrain_RunsNextFrame()
    {
        var (app, root, _) = Start(new YieldPage());
        var runs = 0;
        app.SyncContext.Post(_ =>
        {
            runs++;
            app.SyncContext.Post(_ => runs++, null);
        }, null);
        Assert.True(root.NeedsRender);

        root.Layout(W, H);
        Assert.Equal(1, runs);
        Assert.True(root.NeedsRender); // 실행 중 새로 Post 된 것은 다음 프레임

        root.Layout(W, H);
        Assert.Equal(2, runs);
    }

    [Fact]
    public void ContextRestoredAfterCall()
    {
        var page = new YieldPage();
        var (_, root, _) = Start(page);
        var before = SynchronizationContext.Current; // xUnit 이 테스트 동안 자기 컨텍스트를 깔 수 있다 — 호출 전 값과 비교

        Tap(root, 140, 70);
        Assert.Same(before, SynchronizationContext.Current);
        root.Layout(W, H);
        Assert.Same(before, SynchronizationContext.Current);
        root.RenderTo(new SKCanvas(new SKBitmap(8, 8)), W, H);
        Assert.Same(before, SynchronizationContext.Current);
        root.PointerMove(new SKPoint(1, 1));
        root.PointerWheel(new SKPoint(1, 1), 1);
        root.KeyInput(DvKey.Escape);
        root.TextInput("a");
        root.SetEditText(0, "a");
        root.CommitEdit(0, DvEditExit.Enter);
        root.CancelEdit(0);
        Assert.Same(before, SynchronizationContext.Current);
    }

    // 처리기가 다른 곳에서 완료되는 일을 await 한 뒤 VM 을 바꾼다 — App.Invalidate 없이
    private sealed class AwaitSetPage : DvPage
    {
        public readonly TaskCompletionSource Gate = new();
        public string Text { get; set; } = "before";

        protected override string MarkupSource => """
            <DvScreen style="padding:0">
              <DvButton id="btn" text="P" onclick="Hit" style="position:absolute; left:100; top:50; width:80; height:40"/>
              <DvLabel id="lbl" text="{Bind Data.Text}" style="position:absolute; left:100; top:200; width:200; height:40"/>
            </DvScreen>
            """;

        public async void Hit()
        {
            await Gate.Task;
            Text = "after";
        }
    }

    private static string? LabelText(DvSurface s)
    {
        static Devinno.UI.Layout.LayoutBox? Find(Devinno.UI.Layout.LayoutBox b)
        {
            if (b.Node.Attributes.GetValueOrDefault("id") == "lbl")
                return b;
            foreach (var c in b.Children)
                if (Find(c) is { } f)
                    return f;
            return null;
        }
        return Find(s.Root!)?.ResolvedText;
    }

    [Fact]
    public void VmChangeAfterAwait_RedrawnWithoutInvalidate()
    {
        var page = new AwaitSetPage();
        var (_, root, errors) = Start(page);

        Tap(root, 140, 70);
        root.Layout(W, H); // 클릭 반영 프레임 — 아직 기다리는 중
        Assert.Equal("before", LabelText(page.Surface!));

        page.Gate.SetResult(); // 테스트 스레드(컨텍스트 없음)에서 완료 → 이어짐은 앱 대기열로
        Assert.True(root.NeedsRender);
        root.Layout(W, H); // 한 프레임 — 이어짐이 VM 을 바꾸고 같은 프레임에 다시 읽는다

        Assert.Equal("after", page.Text);
        Assert.Equal("after", LabelText(page.Surface!));
        Assert.Empty(errors);
    }

    [Fact]
    public void PlainSurface_NoContext()
    {
        var vm = new Vm();
        var s = new DvSurface(Markup, vm);
        s.Layout(W, H);
        var before = SynchronizationContext.Current;

        Tap(s, 140, 70);

        Assert.True(vm.Called);
        Assert.Same(before, vm.Seen); // 라이브러리 컨텍스트를 깔지 않는다(기존 동작)
        Assert.IsNotType<DvSyncContext>(vm.Seen);
    }
}
