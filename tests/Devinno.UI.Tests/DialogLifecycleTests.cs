using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Controls;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 8c-1 Task 3 — DvDialog·ShowDialog·스택·결과(그리기 없이, spec §6.1). 앱을 붙이는 테스트가 섞여 있어 AttachedApp 컬렉션(비병렬).
[Collection("AttachedApp")]
public class DialogLifecycleTests
{
    private const float W = 800, H = 600;

    private const string DialogMarkup = """
        <DvStack style="width:300; height:200; padding:0; gap:0">
          <DvInput id="in" value="{Bind Data.Name}" style="width:200"/>
        </DvStack>
        """;

    private const string PageInput = """
        <DvScreen style="padding:0; gap:0">
          <DvInput id="in" value="{Bind Data.Name}" style="width:200"/>
        </DvScreen>
        """;

    public sealed class MyDlg : MarkupDialog<string>, IDvControlHost
    {
        public int No { get; }
        public string Name { get; set; } = "처음";
        public int Loaded, Closed;
        public Exception? ThrowOnClosed;

        public MyDlg() : base(DialogMarkup) { }

        public MyDlg(int no) : base(DialogMarkup) => No = no;

        void IDvControlHost.Attach(DvControlLookup controls) => Loaded++;

        protected override void OnClosed()
        {
            Closed++;
            if (ThrowOnClosed is { } ex)
                throw ex;
        }
    }

    public sealed class CountedDlg : MarkupDialog<string>
    {
        public static int Ctors;

        public CountedDlg() : base(DialogMarkup) => Ctors++;
    }

    private sealed class Page(string markup) : DvPage
    {
        public string Name { get; set; } = "처음";
        protected override string MarkupSource => markup;
    }

    private sealed class Vm
    {
        public string Name { get; set; } = "처음";
    }

    private static (DvApp App, DvSurface Root, Page Page, List<DvHandlerErrorEventArgs> Errors) Start(bool native = false)
    {
        var app = new DvApp();
        var page = new Page(PageInput);
        app.Pages.Add(page);
        var errors = new List<DvHandlerErrorEventArgs>();
        app.HandlerError += (_, e) => errors.Add(e);
        var root = app.Surface;
        root.UseNativeEditor = native;
        root.Layout(W, H);
        return (app, root, page, errors);
    }

    private static MyDlg Top(DvApp app) => Assert.IsType<MyDlg>(app.Dialogs.Top);

    private static void Click(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        s.Layout(W, H);
    }

    // 이미 완료된 결과 Task 의 값(완료 안 됐으면 실패 — 기다리지 않는다)
    private static T R<T>(Task<T> t)
    {
        Assert.True(t.IsCompletedSuccessfully);
        return t.Result;
    }

    // 완료 순서를 완료 시점에 기록(동기 이어짐)
    private static void Record<T>(Task<T> t, List<int> order, int no)
        => t.ContinueWith(_ => order.Add(no), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    [Fact]
    public void ShowDialog_RegistersImmediately()
    {
        var (app, _, _, _) = Start();
        var t = app.ShowDialog<MyDlg, string>();

        Assert.Single(app.Dialogs);
        var d = Top(app);
        Assert.Same(d, app.Dialogs[0]);
        Assert.Same(app, d.App);
        Assert.True(d.IsOpen);
        Assert.False(t.IsCompleted);
        Assert.Equal(0, d.Loaded);
    }

    [Fact]
    public void NextFrame_CreatesSurface_OnLoadedOnce()
    {
        var (app, root, _, _) = Start();
        app.ShowDialog<MyDlg, string>();
        var d = Top(app);

        root.Layout(W, H);
        Assert.Equal(1, d.Loaded);
        Assert.NotNull(d.Surface);
        Assert.Same(d, Assert.Single(app.AppliedDialogs));

        root.Layout(W, H);
        Assert.Equal(1, d.Loaded);
    }

    [Fact]
    public void Close_CompletesOnNextFrame_WithResult()
    {
        var (app, root, _, _) = Start();
        var t = app.ShowDialog<MyDlg, string>();
        var d = Top(app);
        root.Layout(W, H);

        d.Done("r");
        Assert.False(d.IsOpen);
        Assert.Empty(app.Dialogs);
        Assert.Null(app.Dialogs.Top);
        Assert.False(t.IsCompleted);

        root.Layout(W, H);
        Assert.True(t.IsCompletedSuccessfully);
        Assert.Equal("r", R(t));
        Assert.Equal(1, d.Closed);
        Assert.Empty(app.AppliedDialogs);
    }

    [Fact]
    public void CloseTwice_FirstWins()
    {
        var (app, root, _, _) = Start();
        var t = app.ShowDialog<MyDlg, string>();
        var d = Top(app);
        root.Layout(W, H);

        d.Done("a");
        d.Done("b");
        root.Layout(W, H);
        Assert.Equal("a", R(t));
        Assert.Equal(1, d.Closed);
    }

    [Fact]
    public void CloseDialogs_CancelsAllTopDown()
    {
        var (app, root, _, _) = Start();
        var order = new List<int>();
        var tasks = new[] { app.ShowDialog<MyDlg, string>(1), app.ShowDialog<MyDlg, string>(2), app.ShowDialog<MyDlg, string>(3) };
        for (var i = 0; i < 3; i++)
            Record(tasks[i], order, i + 1);
        root.Layout(W, H);
        Assert.Equal(3, app.AppliedDialogs.Count);

        app.CloseDialogs();
        Assert.Empty(app.Dialogs);
        root.Layout(W, H);

        Assert.All(tasks, t => Assert.Null(R(t)));
        Assert.Equal([3, 2, 1], order);
        Assert.Empty(app.AppliedDialogs);
    }

    [Fact]
    public void CloseMiddle_ClosesAboveFirst()
    {
        var (app, root, _, _) = Start();
        var order = new List<int>();
        var t1 = app.ShowDialog<MyDlg, string>(1);
        var t2 = app.ShowDialog<MyDlg, string>(2);
        var t3 = app.ShowDialog<MyDlg, string>(3);
        Record(t1, order, 1);
        Record(t2, order, 2);
        Record(t3, order, 3);
        root.Layout(W, H);
        var d1 = (MyDlg)app.Dialogs[0];
        var d2 = (MyDlg)app.Dialogs[1];
        var d3 = (MyDlg)app.Dialogs[2];

        d2.Done("x");
        Assert.Same(d1, Assert.Single(app.Dialogs));
        Assert.False(d3.IsOpen);
        d3.Done("late"); // 이미 닫힘 — 무시
        root.Layout(W, H);

        Assert.Null(R(t3));
        Assert.Equal("x", R(t2));
        Assert.False(t1.IsCompleted);
        Assert.True(d1.IsOpen);
        Assert.Equal([3, 2], order);
        Assert.Same(d1, Assert.Single(app.AppliedDialogs));
    }

    [Fact]
    public void ShowAndCloseSameFrame_ResultDelivered_NoSurface()
    {
        var (app, root, _, _) = Start();
        var t = app.ShowDialog<MyDlg, string>();
        var d = Top(app);
        d.Done("r");
        root.Layout(W, H);

        Assert.Equal("r", R(t));
        Assert.Equal(0, d.Loaded);
        Assert.Null(d.Surface);
        Assert.Equal(1, d.Closed);
    }

    [Fact]
    public void PageChange_DialogsStay()
    {
        var (app, root, _, _) = Start();
        var other = new Page(PageInput);
        app.ShowDialog<MyDlg, string>();
        root.Layout(W, H);

        app.Show(other);
        root.Layout(W, H);
        Assert.Same(other, app.CurrentPage);
        Assert.Single(app.Dialogs);
        Assert.Single(app.AppliedDialogs);
    }

    [Fact]
    public void AppClosed_ShowDialog_ImmediateCancel_NoCtor()
    {
        DvApp.ResetForTests();
        var app = new DvApp();
        var h = new object();
        app.Attach(h);
        try
        {
            app.Close(h);
            CountedDlg.Ctors = 0;
            var t = app.ShowDialog<CountedDlg, string>();
            Assert.True(t.IsCompletedSuccessfully);
            Assert.Null(R(t));
            Assert.Equal(0, CountedDlg.Ctors);
            Assert.Empty(app.Dialogs);
        }
        finally { app.Detach(h); }
    }

    [Fact]
    public void AppClose_CancelsOpenAndRequested_Synchronously()
    {
        DvApp.ResetForTests();
        var (app, root, _, _) = Start();
        var h = new object();
        app.Attach(h);
        try
        {
            var order = new List<int>();
            var t1 = app.ShowDialog<MyDlg, string>(1);
            root.Layout(W, H); // 1 = 열림(적용)
            var t2 = app.ShowDialog<MyDlg, string>(2); // 2 = 요청만
            Record(t1, order, 1);
            Record(t2, order, 2);

            app.Close(h);
            Assert.True(t1.IsCompletedSuccessfully);
            Assert.True(t2.IsCompletedSuccessfully);
            Assert.Null(R(t1));
            Assert.Null(R(t2));
            Assert.Equal([2, 1], order);
            Assert.Empty(app.Dialogs);
            Assert.Empty(app.AppliedDialogs);
        }
        finally { app.Detach(h); }
    }

    [Fact]
    public void AppClose_ContinuationShowsAgain_ImmediateCancel()
    {
        DvApp.ResetForTests();
        var (app, root, _, _) = Start();
        var h = new object();
        app.Attach(h);
        try
        {
            var t1 = app.ShowDialog<MyDlg, string>();
            root.Layout(W, H);
            Task<string?>? again = null;
            t1.ContinueWith(_ => again = app.ShowDialog<MyDlg, string>(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            app.Close(h); // t1 취소 완료 → 이어짐이 그 자리에서 다시 ShowDialog
            Assert.NotNull(again);
            Assert.Null(R(again));
            Assert.Empty(app.Dialogs);
        }
        finally { app.Detach(h); }
    }

    [Fact]
    public void AppClose_OnClosedOnceEach_PendingKeepsResult()
    {
        DvApp.ResetForTests();
        var (app, root, _, _) = Start();
        var h = new object();
        app.Attach(h);
        try
        {
            var t1 = app.ShowDialog<MyDlg, string>(1);
            var t2 = app.ShowDialog<MyDlg, string>(2);
            root.Layout(W, H);
            var d1 = (MyDlg)app.Dialogs[0];
            var d2 = (MyDlg)app.Dialogs[1];
            d2.Done("x"); // 닫기 대기(결과 확정, 아직 적용 전)
            var t3 = app.ShowDialog<MyDlg, string>(3); // 요청만
            var d3 = Top(app);

            app.Close(h);
            Assert.Equal(1, d1.Closed);
            Assert.Equal(1, d2.Closed);
            Assert.Equal(1, d3.Closed);
            Assert.Null(R(t1));
            Assert.Equal("x", R(t2));
            Assert.Null(R(t3));

            root.Layout(W, H); // 닫힌 뒤 프레임이 와도 다시 불리지 않는다
            Assert.Equal(1, d1.Closed);
            Assert.Equal(1, d2.Closed);
            Assert.Equal(1, d3.Closed);
        }
        finally { app.Detach(h); }
    }

    [Fact]
    public void MarkupError_ThrowsOnce_CancelsDialog_DrainStillRuns()
    {
        var (app, root, _, _) = Start();
        var t = app.ShowDialog<MarkupDialog<string>, string>("<DvStack"); // 잘못된 XML
        var ran = 0;
        app.SyncContext.Post(_ => ran++, null);

        Assert.ThrowsAny<System.Xml.XmlException>(() => root.Layout(W, H));
        Assert.Null(R(t));
        Assert.Empty(app.Dialogs);
        Assert.Empty(app.AppliedDialogs);
        Assert.Equal(1, ran); // 던져도 Post 대기열은 돈다

        app.SyncContext.Post(_ => ran++, null);
        root.Layout(W, H); // 다시 던지지 않는다
        Assert.Equal(2, ran);
    }

    [Fact]
    public void Detach_KeepsDialogs()
    {
        DvApp.ResetForTests();
        var (app, root, _, _) = Start();
        var h = new object();
        app.Attach(h);
        try
        {
            var t = app.ShowDialog<MyDlg, string>();
            root.Layout(W, H);
            var d = Top(app);

            app.Detach(h);
            Assert.False(t.IsCompleted);
            Assert.Single(app.Dialogs);

            app.Attach(h);
            root.Layout(W, H);
            Assert.False(t.IsCompleted);
            Assert.Same(d, Top(app));
            Assert.Same(d, Assert.Single(app.AppliedDialogs));
            Assert.Equal(1, d.Loaded);
        }
        finally { app.Detach(h); }
    }

    public sealed class SelectDlg : MarkupDialog<string>
    {
        public string Mode { get; set; } = "a";

        public SelectDlg() : base("""
            <DvStack style="width:300; height:200; padding:0; gap:0">
              <DvSelect id="sel" value="{Bind Data.Mode}" style="width:200">
                <DvOption value="a" text="에이"/>
                <DvOption value="b" text="비"/>
              </DvSelect>
            </DvStack>
            """) { }
    }

    // 다이얼로그 칸(창 가운데 300×200) 왼쪽 위 = (250, 200)
    private const float DlgX = 250, DlgY = 200;

    [Fact]
    public void AppClose_CancelsDialogEdit_NotWrittenAfterReattach()
    {
        DvApp.ResetForTests();
        var (app, root, _, _) = Start(native: true);
        var h = new object();
        app.Attach(h);
        try
        {
            app.ShowDialog<MyDlg, string>();
            root.Layout(W, H);
            var d = Top(app);
            Click(root, DlgX + 50, DlgY + 20); // 다이얼로그 입력
            Assert.True(root.Edit.IsEditing);
            root.SetEditText(root.Edit.SessionId, "바뀜");

            app.Close(h);
            app.Attach(h); // 같은 앱을 다시 붙임(예: 창 다시 열기)
            root.Layout(W, H);

            Assert.Equal("처음", d.Name); // 결과가 이미 취소로 정해졌다 — 입력 중 값은 쓰지 않는다
            Assert.False(root.Edit.IsEditing);
        }
        finally { app.Detach(h); }
    }

    [Fact]
    public void AppClose_ClosesDialogPopup()
    {
        DvApp.ResetForTests();
        var (app, root, _, _) = Start();
        var h = new object();
        app.Attach(h);
        try
        {
            app.ShowDialog<SelectDlg, string>();
            root.Layout(W, H);
            var d = Assert.IsType<SelectDlg>(app.Dialogs.Top);
            Click(root, DlgX + 100, DlgY + 20); // 드롭다운 열기
            Assert.NotNull(root.TopPopup);

            app.Close(h);
            Assert.Null(root.TopPopup); // 닫는 그 자리에서 팝업도 닫는다

            app.Attach(h);
            root.Layout(W, H);
            Assert.Null(root.TopPopup);
            Assert.Equal("a", d.Mode);
        }
        finally { app.Detach(h); }
    }

    private sealed class AskPage : DvPage
    {
        public bool After;
        public string? Result = "unset";

        protected override string MarkupSource => """
            <DvScreen style="padding:0">
              <DvButton id="btn" text="P" onclick="Ask" style="position:absolute; left:0; top:0; width:100; height:40"/>
            </DvScreen>
            """;

        public async void Ask()
        {
            try { Result = await App.ShowDialog<MyDlg, string>(); }
            finally { After = true; }
        }
    }

    [Fact]
    public void AppClose_RunsAwaitContinuationOfPageHandler()
    {
        DvApp.ResetForTests();
        var app = new DvApp();
        var page = new AskPage();
        app.Pages.Add(page);
        var root = app.Surface;
        root.Layout(W, H);
        var h = new object();
        app.Attach(h);
        try
        {
            Click(root, 50, 20); // 처리기가 다이얼로그를 띄우고 await(앱 컨텍스트)
            Assert.Single(app.Dialogs);
            Assert.False(page.After);

            app.Close(h); // 프레임이 더 오지 않는다 — 이어짐이 그 자리에서 돌아야 한다

            Assert.True(page.After);
            Assert.Null(page.Result);
        }
        finally { app.Detach(h); }
    }

    [Fact]
    public void NotYetAttached_Queued()
    {
        var app = new DvApp();
        app.Pages.Add(new Page(PageInput));
        app.ShowDialog<MyDlg, string>();
        var d = Top(app);

        app.Surface.Layout(W, H);
        Assert.Equal(1, d.Loaded);
    }

    [Fact]
    public void OnClosedThrows_HandlerErrorClosed_ResultStillDelivered()
    {
        var (app, root, _, errors) = Start();
        var t = app.ShowDialog<MyDlg, string>();
        var d = Top(app);
        d.ThrowOnClosed = new InvalidOperationException("boom");
        root.Layout(W, H);

        d.Done("r");
        root.Layout(W, H);

        var e = Assert.Single(errors);
        Assert.Equal("closed", e.Kind);
        Assert.Equal(nameof(MyDlg), e.Member);
        Assert.Equal("boom", e.Exception.Message);
        Assert.Equal("r", R(t));
    }

    [Fact]
    public void ThemeChange_RebuildsDialog()
    {
        var (app, root, _, _) = Start();
        app.ShowDialog<MyDlg, string>();
        var d = Top(app);
        root.Layout(W, H);

        app.Theme = DvTheme.Dark;
        root.Layout(W, H);
        Assert.Same(DvTheme.Dark, d.Surface!.Theme);
    }

    [Fact]
    public async Task AwaitFromOtherThread_ContinuesOnInputThread()
    {
        var (app, root, _, _) = Start();
        var tid = 0;
        var run = Task.Run(async () =>
        {
            await app.ShowDialog<MyDlg, string>();
            tid = Environment.CurrentManagedThreadId;
        });
        Assert.True(SpinWait.SpinUntil(() => app.Dialogs.Count == 1, 5000));
        root.Layout(W, H);

        Top(app).Done("x");
        root.Layout(W, H); // 결과 완료 — 컨텍스트 없는 await 는 이 호출 안에서 이어진다
        Assert.Equal(Environment.CurrentManagedThreadId, tid);
        await run.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ApplyDialog_CommitsPageEdit()
    {
        var (app, root, page, _) = Start(native: true);
        Click(root, 50, 20); // 페이지 입력
        Assert.True(root.Edit.IsEditing);
        root.SetEditText(root.Edit.SessionId, "새 값");

        app.ShowDialog<MyDlg, string>();
        root.Layout(W, H);

        Assert.Equal("새 값", page.Name);
        Assert.False(page.Surface!.Edit.IsEditing);
    }

    [Fact]
    public void CancelPendingEdit_EndsSession_WithoutWriting()
    {
        var vm = new Vm();
        var s = new DvSurface(PageInput, vm) { UseNativeEditor = true };
        s.Layout(W, H);
        Click(s, 50, 20);
        Assert.True(s.Edit.IsEditing);
        s.SetEditText(s.Edit.SessionId, "바뀜");

        s.CancelPendingEdit();
        Assert.False(s.Edit.IsEditing);
        s.Layout(W, H);
        Assert.Equal("처음", vm.Name);
    }
}
