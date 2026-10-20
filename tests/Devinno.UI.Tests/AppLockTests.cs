using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// DvApp 잠금 층(Lock·Unlock·IsLocked)과 무입력 감지(IdleTimeout·Idle).
public class AppLockTests
{
    private const float W = 800, H = 600;
    private static readonly SKPoint Outside = new(400, 570); // 페이지 버튼 위, 가운데 잠금 칸과 왼쪽 위 라벨 밖

    private sealed class Page : DvPage
    {
        public int Clicks;
        public string Value { get; set; } = "처음";

        public void Hit() => Clicks++;

        // 창 전체를 덮는 버튼(어디를 눌러도 새면 잡힌다) + 값 라벨
        protected override string MarkupSource => """
            <DvScreen style="padding:0">
              <DvButton text="P" onclick="Hit" style="position:absolute; left:0; top:0; width:800; height:600"/>
              <DvLabel id="value" text="{Value}" style="position:absolute; left:10; top:10"/>
            </DvScreen>
            """;
    }

    public sealed class LockScreen : DvDialog<bool>
    {
        public static int Created;

        public LockScreen() => Created++;

        public void Ok() => Close(true);

        public void Wrong() => Close(false);

        public void Cancel() => Close();

        protected override string MarkupSource => """
            <DvDialogFrame title="잠금" style="width:240">
              <DvButton text="해제" onclick="Ok"/>
              <DvButton text="틀림" onclick="Wrong"/>
              <DvButton text="취소" onclick="Cancel"/>
            </DvDialogFrame>
            """;
    }

    // 넓은 다이얼로그 — 왼쪽 끝 버튼은 가운데 잠금 칸에 가려지지 않는다
    public sealed class WideDialog : DvDialog<string>
    {
        public int Clicks;

        public void Hit() => Clicks++;

        protected override string MarkupSource => """
            <DvDialogFrame style="width:700; height:300">
              <DvButton text="W" onclick="Hit" style="width:60"/>
            </DvDialogFrame>
            """;
    }

    private static (DvApp App, DvSurface Root, Page Page) Start()
    {
        var page = new Page();
        var app = new DvApp();
        app.Pages.Add(page);
        app.Surface.Layout(W, H);
        return (app, app.Surface, page);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    // 그 다이얼로그 화면 안에서 글자가 text 인 버튼을 창 좌표로 누른다
    private static void Click(DvSurface root, DvDialog dialog, string text)
    {
        var slot = root.Slots.Single(s => ReferenceEquals(s.Child, dialog.Surface)).Rect;
        var r = Flatten(dialog.Surface!.Root!).Single(b => b.Node.Tag == "DvButton" && b.ResolvedText == text).Rect;
        var p = new SKPoint(slot.Left + r.MidX, slot.Top + r.MidY);
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(W, H);
        root.Layout(W, H);
    }

    private static void Tap(DvSurface root, SKPoint p)
    {
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(W, H);
    }

    // ── 잠금 층 ─────────────────────────────────────────

    [Fact]
    public void Lock_BlocksPageInput()
    {
        var (app, root, page) = Start();
        _ = app.Lock<LockScreen>();
        Assert.True(app.IsLocked); // 요청 순간부터
        root.Layout(W, H);

        Tap(root, Outside); // 잠금 칸·라벨 밖
        Assert.Equal(0, page.Clicks);
        Assert.True(app.IsLocked);
    }

    [Fact]
    public void CloseTrue_Unlocks_TaskCompletes()
    {
        var (app, root, _) = Start();
        var t = app.Lock<LockScreen>();
        root.Layout(W, H);

        Click(root, app.LockDialog!, "해제");

        Assert.False(app.IsLocked);
        Assert.True(t.IsCompleted);
        Assert.DoesNotContain(root.Slots, s => s.Modal);
    }

    [Theory]
    [InlineData("틀림")]
    [InlineData("취소")]
    public void CloseNotTrue_StaysLocked(string button)
    {
        var (app, root, page) = Start();
        var t = app.Lock<LockScreen>();
        root.Layout(W, H);
        var lockDialog = app.LockDialog!;

        Click(root, lockDialog, button);

        Assert.True(app.IsLocked);
        Assert.False(t.IsCompleted);
        Assert.True(lockDialog.IsOpen);
        Tap(root, Outside);
        Assert.Equal(0, page.Clicks);
    }

    [Fact]
    public void CloseDialogs_DoesNotUnlock()
    {
        var (app, root, _) = Start();
        _ = app.Lock<LockScreen>();
        root.Layout(W, H);

        app.CloseDialogs();
        root.Layout(W, H);

        Assert.True(app.IsLocked);
        Assert.Empty(app.Dialogs); // 잠금 다이얼로그는 Dialogs 에 보이지 않는다
    }

    [Fact]
    public void Unlock_FromOutside()
    {
        var (app, root, page) = Start();
        var t = app.Lock<LockScreen>();
        root.Layout(W, H);

        app.Unlock();
        root.Layout(W, H);

        Assert.False(app.IsLocked);
        Assert.True(t.IsCompleted);
        Tap(root, Outside);
        Assert.Equal(1, page.Clicks);
    }

    [Fact]
    public void Unlock_WhenNotLocked_NoOp()
    {
        var (app, root, _) = Start();
        app.Unlock();
        root.Layout(W, H);
        Assert.False(app.IsLocked);
    }

    [Fact]
    public void LockTwice_SameTask_NoSecondInstance()
    {
        var (app, root, _) = Start();
        var before = LockScreen.Created;
        var a = app.Lock<LockScreen>();
        var b = app.Lock<LockScreen>();
        root.Layout(W, H);

        Assert.Same(a, b);
        Assert.Equal(before + 1, LockScreen.Created);
        Assert.Single(root.Slots, s => s.Modal);
    }

    [Fact]
    public void DialogShownWhileLocked_StaysUnderLock_NoInputUntilUnlock()
    {
        var (app, root, _) = Start();
        _ = app.Lock<LockScreen>();
        root.Layout(W, H);
        _ = app.ShowDialog<WideDialog, string>();
        root.Layout(W, H);

        var wide = Assert.IsType<WideDialog>(app.Dialogs.Top);
        var modal = root.Slots.Where(s => s.Modal).ToList();
        Assert.Equal(2, modal.Count);
        Assert.Same(app.LockDialog!.Surface, modal[^1].Child); // 잠금 층이 맨 위

        Click(root, wide, "W");
        Assert.Equal(0, wide.Clicks);

        app.Unlock();
        root.Layout(W, H);
        Click(root, wide, "W");
        Assert.Equal(1, wide.Clicks); // 풀리면 그 다이얼로그가 맨 위
    }

    [Fact]
    public void InputBoxUnderLock_DoesNotStartEditing_UntilUnlock()
    {
        var (app, root, _) = Start();
        _ = app.Lock<LockScreen>();
        root.Layout(W, H);
        _ = app.InputBox("이름", "펌프");
        root.Layout(W, H);

        Assert.False(root.Edit.IsEditing);

        app.Unlock();
        root.Layout(W, H);
        root.Layout(W, H);
        Assert.True(root.Edit.IsEditing);
    }

    [Fact]
    public void PageKeepsUpdatingWhileLocked()
    {
        var (app, root, page) = Start();
        _ = app.Lock<LockScreen>();
        root.Layout(W, H);

        page.Value = "갱신"; // 통신 스레드처럼 처리기 밖에서 바꿈
        app.Invalidate();
        using var bmp = new SKBitmap((int)W, (int)H);
        using var canvas = new SKCanvas(bmp);
        root.RenderTo(canvas, W, H, 0);

        var label = Flatten(page.Surface!.Root!).Single(b => b.Node.Tag == "DvLabel");
        Assert.Equal("갱신", label.ResolvedText);
        Assert.Contains(root.Slots, s => ReferenceEquals(s.Child, page.Surface) && !s.Modal); // 페이지 칸은 계속 그려진다
    }

    // ── 무입력 감지 ─────────────────────────────────────

    private static (DvApp App, DvSurface Root, Page Page, Func<long> Now, Action<long> Advance) StartClock()
    {
        long t = 1_000_000;
        var (app, root, page) = Start();
        app.Clock = () => t;
        return (app, root, page, () => t, d => t += d);
    }

    [Fact]
    public void Idle_RaisedOnceAfterTimeout()
    {
        var (app, root, _, _, advance) = StartClock();
        var raised = 0;
        app.Idle += (_, _) => raised++;
        app.IdleTimeout = TimeSpan.FromSeconds(5);

        advance(4999);
        root.Layout(W, H);
        Assert.Equal(0, raised);

        advance(2);
        root.Layout(W, H);
        Assert.Equal(1, raised);

        advance(10_000);
        root.Layout(W, H);
        Assert.Equal(1, raised); // 한 번만
    }

    [Fact]
    public void Input_RestartsCount()
    {
        var (app, root, _, _, advance) = StartClock();
        var raised = 0;
        app.Idle += (_, _) => raised++;
        app.IdleTimeout = TimeSpan.FromSeconds(5);

        advance(4000);
        root.PointerMove(new SKPoint(5, 5));
        advance(4000);
        root.Layout(W, H);
        Assert.Equal(0, raised);

        advance(1001);
        root.Layout(W, H);
        Assert.Equal(1, raised);

        root.PointerDown(new SKPoint(5, 5));
        root.PointerUp(new SKPoint(5, 5));
        advance(5000);
        root.Layout(W, H);
        Assert.Equal(2, raised); // 입력 뒤 다시 한 번
    }

    [Fact]
    public void ZeroTimeout_NeverRaised()
    {
        var (app, root, _, _, advance) = StartClock();
        var raised = 0;
        app.Idle += (_, _) => raised++;
        advance(10_000_000);
        root.Layout(W, H);
        Assert.Equal(0, raised);
        Assert.Null(root.NextAnimationDue(0));
    }

    [Fact]
    public void NextAnimationDue_IsIdleDeadline()
    {
        var (app, root, _, now, _) = StartClock();
        app.IdleTimeout = TimeSpan.FromSeconds(5);
        root.Layout(W, H);

        Assert.Equal(now() + 5000, root.NextAnimationDue(now()));
    }

    [Fact]
    public void IdleHandlerLocks_NotRaisedWhileLocked_CountsAgainAfterUnlock()
    {
        var (app, root, _, _, advance) = StartClock();
        var raised = 0;
        app.Idle += (_, _) => { raised++; _ = app.Lock<LockScreen>(); };
        app.IdleTimeout = TimeSpan.FromSeconds(5);

        advance(5000);
        root.Layout(W, H);
        root.Layout(W, H);
        Assert.True(app.IsLocked);
        Assert.Null(root.NextAnimationDue(0)); // 잠긴 동안은 세지 않는다

        advance(60_000);
        root.Layout(W, H);
        Assert.Equal(1, raised);

        app.Unlock();
        root.Layout(W, H);
        advance(4999);
        root.Layout(W, H);
        Assert.Equal(1, raised);
        advance(1);
        root.Layout(W, H);
        Assert.Equal(2, raised); // 풀린 때부터 다시 셈
    }

    [Fact]
    public void IdleHandlerThrows_HandlerErrorIdle()
    {
        var (app, root, _, _, advance) = StartClock();
        DvHandlerErrorEventArgs? error = null;
        app.HandlerError += (_, e) => error = e;
        app.Idle += (_, _) => throw new InvalidOperationException("boom");
        app.IdleTimeout = TimeSpan.FromSeconds(1);

        advance(1000);
        root.Layout(W, H);

        Assert.Equal("idle", error?.Kind);
        Assert.Equal("boom", error?.Exception.Message);
    }

    [Fact]
    public void NegativeTimeout_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DvApp().IdleTimeout = TimeSpan.FromSeconds(-1));
}

// 앱(창) 닫힘 — 잠금 Task 도 끝나고, 닫힌 앱의 Lock 은 바로 끝난다
[Collection("AttachedApp")]
public class AppLockCloseTests
{
    private sealed class Page : DvPage
    {
        protected override string MarkupSource => "<DvScreen/>";
    }

    [Fact]
    public void AppClose_CompletesLockTask_LaterLockCompletesImmediately()
    {
        DvApp.ResetForTests();
        var app = new DvApp();
        app.Pages.Add(new Page());
        var host = new object();
        app.Attach(host);
        try
        {
            app.Surface.Layout(800, 600);
            var t = app.Lock<AppLockTests.LockScreen>();
            app.Surface.Layout(800, 600);

            app.Close(host);

            Assert.True(t.IsCompleted);
            Assert.False(app.IsLocked);
            Assert.True(app.Lock<AppLockTests.LockScreen>().IsCompleted);
        }
        finally
        {
            app.Detach(host);
        }
    }
}
