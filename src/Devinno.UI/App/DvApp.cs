using System.Diagnostics;
using Devinno.UI.Components;
using Devinno.UI.Interaction;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI.App;

/// <summary>
/// App manager that holds the pages and switches the current page. One app = one window; the adapter (window or control) takes this object and only draws it and forwards input.
/// </summary>
/// <remarks>
/// <para><b>Pages</b> — Add <see cref="DvPage"/> instances to <see cref="Pages"/> and switch with <see cref="Show{T}"/> or <see cref="Show(DvPage)"/>. A page's surface is built once, when it is first shown,
/// and kept, so when you come back it is exactly as you left it (scroll, input values, editing). When there is no <see cref="CurrentPage"/>, the first page added is the start page.</para>
/// <para><b>Bars (fixed areas)</b> — Place bars (<see cref="DvBar"/>) in <see cref="Top"/>, <see cref="Bottom"/>, <see cref="Left"/> and <see cref="Right"/>. All are optional; the page area is the remaining space.</para>
/// <para><b>Threads</b> — <see cref="Show{T}"/>, <see cref="Show(DvPage)"/>, area assignment, setting <see cref="Theme"/>, <see cref="Strings"/> or <see cref="Registry"/>, and <see cref="Invalidate"/>
/// can be called from any thread (for example, switching to an alarm page from a communication callback). Requests are queued and applied on the input (UI) thread at the adapter's next frame;
/// <see cref="DvPage.OnShown"/>, <see cref="DvPage.OnHidden"/> and <c>OnLoaded()</c> are also always called on that thread. <see cref="CurrentPage"/> is the value after a request is applied.</para>
/// <para><b>Global settings</b> — Changing <see cref="Theme"/>, <see cref="Strings"/> or <see cref="Registry"/> rebuilds every kept page and bar surface with the new settings, keeping the same screen
/// (scroll, editing, input values, control values written from code, and <c>Click</c> subscriptions are kept).</para>
/// </remarks>
/// <example>
/// <code>
/// var app = new DvApp { Theme = DvTheme.Dark };
/// app.Pages.Add(new HomePage());
/// app.Pages.Add(new MonitorPage());
/// app.Left = new MainSideBar();
/// app.HandlerError += (_, e) =&gt; Log.Error(e.Exception, $"{e.Kind} {e.Member}");
/// app.Show&lt;HomePage&gt;();
/// </code>
/// </example>
public sealed class DvApp : ISurfaceComposer
{
    private const string RootMarkup = "<DvAppRoot/>"; // 루트 화면 자체는 빈 상자 — 칸(영역·페이지)은 Compose 가 코드로 배치
    /// <summary>Lock for deciding which app a page or bar belongs to — shared across apps.</summary>
    internal static readonly object OwnerLock = new();

    private readonly object _gate = new(); // 대기열(다른 스레드 요청) 잠금
    private object? _host; // 붙어 있는 어댑터(창·컨트롤) — _gate 아래
    private bool _closed; // Close 이후 다시 Attach 하기 전까지 true — _gate 아래

    // 프로세스 전체에서 창·컨트롤에 붙은 앱(없으면 null)과 마지막으로 붙었던 앱 — s_lock 아래
    private static DvApp? s_attached, s_last;
    private static readonly object s_lock = new();

    // 요청 값(어느 스레드에서나 씀) — _gate 아래
    private DvPage? _requestedPage;
    private DvBar? _top, _bottom, _left, _right;
    private bool _regionsChanged;
    private DvTheme? _theme;
    private DvStrings? _strings;
    private ComponentRegistry? _registry;
    private bool _settingsChanged;

    // 적용된 값(입력 스레드에서만 씀)
    private volatile DvPage? _current;
    private volatile DvBar? _appliedTop, _appliedBottom, _appliedLeft, _appliedRight;
    private DvTheme? _appliedTheme;
    private DvStrings? _appliedStrings;
    private ComponentRegistry? _appliedRegistry;
    private readonly List<DvSurface> _kept = new(); // 만든 화면 전부(페이지·틀·다이얼로그) — 입력 스레드에서만

    // 다이얼로그(D1·D4): 요청 스택(열림 = 등록~닫기 요청, 아래→위)과 닫기 대기(결과 확정, 위→아래 순으로 쌓임)는 _gate 아래, 적용 스택(화면 있음)은 입력 스레드에서만
    private readonly List<DvDialog> _requestedDialogs = new(), _closingDialogs = new();
    private bool _dialogsChanged; // _gate 아래
    private readonly List<DvDialog> _appliedDialogs = new();
    private DvDialog<bool>? _lock; // 요청된 잠금 다이얼로그(모든 다이얼로그 위 층) — _gate 아래
    private DvDialog? _appliedLock; // 화면이 적용된 잠금 다이얼로그 — 입력 스레드에서만

    // 무입력 감지 — _gate 아래
    private long _idleTimeoutMs;
    private long _lastInputMs;
    private bool _idleRaised;

    /// <summary>Clock for idle detection (ms, <see cref="Environment.TickCount64"/> — same base as the adapter's timestampMs). Tests replace it.</summary>
    internal Func<long> Clock = () => Environment.TickCount64;

    // 동기화 컨텍스트(D15) Post 대기열 — _posted 는 _gate 아래, _draining 은 입력 스레드에서만(둘을 맞바꿔 할당 없이 비운다)
    private List<(SendOrPostCallback Callback, object? State)> _posted = new(), _draining = new();
    private bool _drainBusy; // DrainPosted 재진입 방지(콜백이 루트 Layout 을 부르는 경우) — 입력 스레드에서만

    /// <summary>Creates an empty app. Add pages to <see cref="Pages"/>.</summary>
    public DvApp()
    {
        Pages = new PageCollection(this);
        Dialogs = new DialogCollection(this);
        Surface = new DvSurface(RootMarkup, null) { Composer = this, Wake = RaiseRenderRequested };
        SyncContext = new DvSyncContext(this);
    }

    /// <summary>
    /// The root surface the adapter (window or control) draws and forwards input to (read-only, fixed to one per app). The bars and the current page are combined into one surface.
    /// </summary>
    /// <remarks>
    /// <para>The adapter only needs to drive this one surface with <see cref="DvSurface.RenderTo(SkiaSharp.SKCanvas, float, float, long)"/>, pointer and key input, and the edit methods.
    /// On each layout it first applies pending requests (<see cref="Show(DvPage)"/>, area assignment, global settings), then arranges the bars and the page into cells.</para>
    /// <para><b>Arrangement</b> — <see cref="Top"/> and <see cref="Bottom"/> span the full window width; <see cref="Left"/> and <see cref="Right"/> take the height between them. Each size is the size of that bar's dvml root
    /// (height for Top/Bottom, width for Left/Right — the content size unless set with <c>style</c>). The page fills the remaining space. With no bars, the page is the whole window.</para>
    /// <para><b>Input</b> — A pointer goes to the surface of the cell it was pressed in, and while dragging that cell keeps receiving it until release. Dropdown and picker popups and the virtual keyboard are drawn by this root surface relative to the whole window.
    /// The edit state of bars and pages is exposed on this surface's <see cref="DvSurface.Edit"/> in window coordinates. Handle <c>Bounds</c> are also window-relative.</para>
    /// <para>Exceptions from bars and pages also reach this surface's <see cref="DvSurface.HandlerError"/>, but the app should subscribe only to <see cref="HandlerError"/> (each exception arrives once).</para>
    /// </remarks>
    public DvSurface Surface { get; }

    /// <summary>
    /// Raised when a redraw is needed (the adapter uses it to wake its render loop). May be raised on any thread. <c>sender</c> is this <see cref="DvApp"/>.
    /// </summary>
    /// <remarks>
    /// Raised by <see cref="DvSurface.Invalidate"/> and <see cref="DvSurface.Repaint"/> of bar and page surfaces (click handling, handle value writes, polling timers, etc.), by <see cref="Invalidate"/>,
    /// and by <see cref="Show(DvPage)"/>, area assignment and global-setting requests. Exceptions thrown by subscribers are swallowed and only written to Trace.
    /// Keep handlers short (just request a render) — do drawing and input on the adapter's UI thread.
    /// </remarks>
    public event EventHandler? RenderRequested;

    /// <summary>The list of kept pages. The first page added is the start page.</summary>
    public PageCollection Pages { get; }

    /// <summary>
    /// Gets the current page (read-only — use <see cref="Show(DvPage)"/> to change it). This is the value after the request is applied, so it becomes the new page at the next frame, not right after <see cref="Show(DvPage)"/>.
    /// <see langword="null"/> if no page has been shown yet.
    /// </summary>
    public DvPage? CurrentPage => _current;

    /// <summary>Requests the single page of type <typeparamref name="T"/> (<c>Pages.Get&lt;T&gt;()</c>) as the current page. Can be called from any thread.</summary>
    /// <typeparam name="T">The page type to show.</typeparam>
    /// <exception cref="InvalidOperationException">When there is no page of that type, or more than one (<see cref="PageCollection.Get{T}"/>).</exception>
    public void Show<T>() where T : DvPage => Show(Pages.Get<T>());

    /// <summary>Switches to the page of type <typeparamref name="T"/>, using the <paramref name="transition"/> animation this one time (instead of <see cref="PageTransition"/>).</summary>
    /// <typeparam name="T">The page type to show.</typeparam>
    /// <param name="transition">The animation for this transition.</param>
    /// <exception cref="InvalidOperationException">When there is no page of that type, or more than one (<see cref="PageCollection.Get{T}"/>).</exception>
    public void Show<T>(DvPageTransition transition) where T : DvPage => Show(Pages.Get<T>(), transition);

    /// <summary>Switches to this page, using the <paramref name="transition"/> animation this one time (instead of <see cref="PageTransition"/>). Otherwise the same as <see cref="Show(DvPage)"/>.</summary>
    /// <param name="page">The page to show.</param>
    /// <param name="transition">The animation for this transition.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">When <paramref name="page"/> belongs to another <see cref="DvApp"/>.</exception>
    public void Show(DvPage page, DvPageTransition transition)
    {
        Pages.Add(page);
        lock (_gate)
        {
            _requestedPage = page;
            _requestedTransition = transition;
        }
        Surface.RequestFrame();
    }

    /// <summary>
    /// Gets or sets the default animation used when switching pages. Default <see cref="DvPageTransition.None"/> (switch immediately). Use <see cref="Show(DvPage, DvPageTransition)"/> to override it once.
    /// </summary>
    /// <remarks>
    /// During the animation (about 0.2 s) the old and new pages are drawn together. The new page receives input from the start. The first page, and re-showing the same page, are not animated.
    /// Bars (<see cref="DvBar"/>) and dialogs do not move. Can be changed from any thread; takes effect from the next transition.
    /// </remarks>
    public DvPageTransition PageTransition
    {
        get { lock (_gate) return _pageTransition; }
        set { lock (_gate) _pageTransition = value; }
    }

    private DvPageTransition _pageTransition;
    private DvPageTransition? _requestedTransition;
    private DvPageTransition _transitionToTake; // 적용한 전환 — 루트 화면이 한 번 가져간다

    /// <summary>
    /// Requests this page instance as the current page. If it is not in the list, it is added first (<see cref="PageCollection.Add"/>). Does nothing if it is already the current page.
    /// </summary>
    /// <remarks>
    /// Can be called from any thread. The switch happens on the input thread at the next frame; if called several times in between, only the last request is applied.
    /// The order is previous page <see cref="DvPage.OnHidden"/> → (if the surface is new: build → <c>OnLoaded()</c>) → new page <see cref="DvPage.OnShown"/>;
    /// exceptions thrown by these notifications are reported through <see cref="HandlerError"/> (<c>Kind</c> = <c>"hidden"</c>/<c>"shown"</c>) and the switch continues.
    /// </remarks>
    /// <param name="page">The page to show.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">When <paramref name="page"/> belongs to another <see cref="DvApp"/>.</exception>
    public void Show(DvPage page)
    {
        Pages.Add(page); // 이미 있으면 무시, 다른 앱 소속이면 예외
        lock (_gate)
        {
            _requestedPage = page;
            _requestedTransition = null; // 기본 움직임(PageTransition)
        }
        Surface.RequestFrame(); // 어댑터를 깨운다 — 다음 프레임에 전환
    }

    /// <summary>Gets or sets the top bar (full window width, height = the bar's dvml root height). <see langword="null"/> if none (takes no space). Assignment is applied at the next frame.</summary>
    /// <remarks>A bar can be placed in only one position (see <see cref="DvBar"/> for the rules).</remarks>
    /// <exception cref="InvalidOperationException">When the assigned bar has been placed in another <see cref="DvApp"/>, or is placed in another area of this app.</exception>
    public DvBar? Top { get { lock (_gate) return _top; } set => SetRegion(ref _top, value); }

    /// <summary>Gets or sets the bottom bar (full window width, height = the bar's dvml root height). <see langword="null"/> if none (takes no space). Assignment is applied at the next frame.</summary>
    /// <remarks>A bar can be placed in only one position (see <see cref="DvBar"/> for the rules).</remarks>
    /// <exception cref="InvalidOperationException">When the assigned bar has been placed in another <see cref="DvApp"/>, or is placed in another area of this app.</exception>
    public DvBar? Bottom { get { lock (_gate) return _bottom; } set => SetRegion(ref _bottom, value); }

    /// <summary>Gets or sets the left bar (height between the top and bottom bars, width = the bar's dvml root width). <see langword="null"/> if none (takes no space). Assignment is applied at the next frame.</summary>
    /// <remarks>A bar can be placed in only one position (see <see cref="DvBar"/> for the rules).</remarks>
    /// <exception cref="InvalidOperationException">When the assigned bar has been placed in another <see cref="DvApp"/>, or is placed in another area of this app.</exception>
    public DvBar? Left { get { lock (_gate) return _left; } set => SetRegion(ref _left, value); }

    /// <summary>Gets or sets the right bar (height between the top and bottom bars, width = the bar's dvml root width). <see langword="null"/> if none (takes no space). Assignment is applied at the next frame.</summary>
    /// <remarks>A bar can be placed in only one position (see <see cref="DvBar"/> for the rules).</remarks>
    /// <exception cref="InvalidOperationException">When the assigned bar has been placed in another <see cref="DvApp"/>, or is placed in another area of this app.</exception>
    public DvBar? Right { get { lock (_gate) return _right; } set => SetRegion(ref _right, value); }

    /// <summary>
    /// Gets or sets the theme for all page and bar surfaces. <see langword="null"/> means <see cref="DvTheme.Default"/>. Changing it rebuilds the kept surfaces at the next frame, keeping their state.
    /// </summary>
    public DvTheme? Theme
    {
        get { lock (_gate) return _theme; }
        set { lock (_gate) { _theme = value; _settingsChanged = true; } Surface.RequestFrame(); }
    }

    /// <summary>
    /// Gets or sets the localized string table for all page and bar surfaces (same rules as <see cref="DvSurface.Strings"/>). Changing it rebuilds the kept surfaces at the next frame.
    /// </summary>
    public DvStrings? Strings
    {
        get { lock (_gate) return _strings; }
        set { lock (_gate) { _strings = value; _settingsChanged = true; } Surface.RequestFrame(); }
    }

    /// <summary>
    /// Gets or sets the user component (<c>DvComponent</c>) registry used by all page and bar surfaces. Changing it rebuilds the kept surfaces at the next frame.
    /// </summary>
    public ComponentRegistry? Registry
    {
        get { lock (_gate) return _registry; }
        set { lock (_gate) { _registry = value; _settingsChanged = true; } Surface.RequestFrame(); }
    }

    /// <summary>
    /// Signals that the surfaces of the current page, the bars and the open dialogs should be rebuilt (when VM values were changed outside a handler, such as from a timer or a communication thread). Can be called from any thread.
    /// </summary>
    /// <remarks>Values changed inside a handler, or after an <c>await</c> in a handler (which continues on the input thread), are re-read automatically, so there is no need to call this.
    /// Hidden kept pages always re-read the VM when shown again (when <see cref="Show(DvPage)"/> is applied), so they need no separate notification.</remarks>
    public void Invalidate()
    {
        _current?.Surface?.Invalidate();
        foreach (var bar in AppliedBars())
            bar.Surface?.Invalidate();
        foreach (var d in DialogSnapshot())
            d.Surface?.Invalidate(); // 아직 화면이 없는(요청만 된) 다이얼로그는 적용 때 새로 읽는다
        LockDialog?.Surface?.Invalidate();
        Surface.RequestFrame(); // 아직 화면이 없어도 어댑터를 깨운다
    }

    /// <summary>
    /// Creates a <typeparamref name="TDialog"/> dialog, shows it on top, and returns a Task that yields the result when it closes. Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// <para><b>Creation</b> — The app picks the one public constructor whose parameter count and types match <paramref name="args"/> and creates the instance on the calling thread (the surface is built at the next frame,
    /// so do not touch the UI in the constructor). <c>ShowDialog&lt;X, R&gt;(null)</c> means a single null argument. Passing one array (<c>string[] names</c>) makes that array a single argument;
    /// to pass one <c>object[]</c> as an argument, wrap it as <c>(object)items</c>.</para>
    /// <para><b>Lifetime</b> — The dialog is registered on top of <see cref="Dialogs"/> immediately (<see cref="DvDialog.IsOpen"/>), and its surface is built on the input thread at the next frame (at that point, in-progress edits on pages and bars are committed).
    /// The dialog stays when the page changes. The result is the value passed to <c>Close(result)</c> by the dialog, or <c>default</c> if it is closed from outside (<see cref="CloseDialogs"/>, a dialog below closing, the window closing),
    /// and it is delivered exactly once whichever path is taken. A reference type or a nullable type (<c>bool?</c>, <c>int?</c>) is recommended as the result type so it can be told apart from cancellation.</para>
    /// <para><b>Closed app</b> — Called on an app whose window is closed, it does not call the constructor and returns an already completed cancel result (in place, on the calling thread — so communication callbacks during shutdown end quietly).
    /// Results are carried by frames, so for an app not yet attached to a window the dialog waits and appears at the first frame, and <c>await</c> waits until then. For a detached (not closed) app it waits until the app is attached again.</para>
    /// <para><b>Continuation</b> — Code after an <c>await</c> in a page, bar or dialog handler continues on the input thread, and the app re-reads the visible surfaces in that frame, so VM values changed there
    /// are reflected without <see cref="Invalidate"/>. When the window closes, that continuation runs in place on the closing thread. Code that <c>await</c>s where there is no context, such as a communication thread, continues on the
    /// input thread that delivered the result — move heavy work after that to <c>Task.Run</c>. VM values changed outside handlers like this (communication, timers) are signaled with <see cref="Invalidate"/>.</para>
    /// <para>The instance is not returned. If you need the instance outside the dialog, find it through <see cref="Dialogs"/>.</para>
    /// </remarks>
    /// <typeparam name="TDialog">The dialog type to show.</typeparam>
    /// <typeparam name="TResult">The result type (the type argument of the <see cref="DvDialog{TResult}"/> that <typeparamref name="TDialog"/> derives from).</typeparam>
    /// <param name="args">Constructor arguments.</param>
    /// <returns>A Task that completes with the result at the frame after the dialog closes (in place if the window closes).</returns>
    /// <exception cref="ArgumentException">When there is no public constructor matching the arguments, or more than one (the message lists the constructors). The dialog is not registered in that case.</exception>
    /// <example>
    /// <code>
    /// string? name = await App.ShowDialog&lt;RenameDialog, string&gt;(device.Name);
    /// if (name is not null) device.Name = name;
    /// </code>
    /// </example>
    public Task<TResult?> ShowDialog<TDialog, TResult>(params object?[] args) where TDialog : DvDialog<TResult>
    {
        if (IsClosed)
            return Task.FromResult<TResult?>(default);
        var dialog = DialogFactory.Create<TDialog>(args); // 생성자 예외·인자 불일치는 여기서 그대로 — 스택에 넣지 않는다
        dialog.AppOrNull = this;
        lock (_gate)
        {
            if (_closed) // 생성 중에 창이 닫힘
                return Task.FromResult<TResult?>(default);
            dialog.Open = true;
            _requestedDialogs.Add(dialog);
            _dialogsChanged = true;
        }
        Surface.RequestFrame();
        return dialog.Task;
    }

    /// <summary>The list of open dialogs (bottom to top, including those whose surface is not applied yet). Can be read from any thread.</summary>
    public DialogCollection Dialogs { get; }

    /// <summary>
    /// Closes all open dialogs as canceled (from the top). Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// <see cref="Dialogs"/> becomes empty immediately; surface cleanup, <see cref="DvDialog.OnClosed"/> and result (<c>default</c>) delivery happen on the input thread at the next frame, from top to bottom.
    /// If only a specific dialog must be closed from outside, give that dialog its own public method that wraps <c>Close</c>.
    /// </remarks>
    public void CloseDialogs()
    {
        lock (_gate)
        {
            if (_requestedDialogs.Count == 0)
                return;
            for (var i = _requestedDialogs.Count - 1; i >= 0; i--)
                MarkClosing(_requestedDialogs[i]);
            _requestedDialogs.Clear();
        }
        Surface.RequestFrame();
    }

    /// <summary>
    /// Locks the app: shows the <typeparamref name="TDialog"/> dialog (a login or password screen made by the app) on <b>a lock layer above all dialogs</b>,
    /// and everything below it (pages, bars, other dialogs) receives no input until it is unlocked. Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// <para><b>Unlocking</b> — Call <c>Close(true)</c> inside the lock dialog when authentication succeeds. The lock is then released and the returned Task completes.
    /// <c>Close(false)</c> and <c>Close()</c> are ignored and the lock stays; <see cref="CloseDialogs"/> does not close it either.
    /// To unlock from outside the dialog (card reader signal, remote release, etc.), call <see cref="Unlock"/>. The library does not verify authentication (the app's responsibility).</para>
    /// <para><b>While locked</b> — Drawing continues (instruments, trends and alarm blinking keep moving, and <see cref="Invalidate"/> is applied). Dialogs shown after locking (communication alarms, etc.)
    /// stack below the lock layer and receive no input. If already locked, no new dialog is shown and the current lock's Task is returned (the constructor is not called either).
    /// When the app (window) closes, the lock dialog also closes and the Task completes.</para>
    /// <para><b>Constructor</b> — Calls the public constructor matching <paramref name="args"/> on the calling thread, with the same rules as <see cref="ShowDialog{TDialog, TResult}"/>.</para>
    /// </remarks>
    /// <typeparam name="TDialog">The lock screen dialog (<c>DvDialog&lt;bool&gt;</c>).</typeparam>
    /// <param name="args">Constructor arguments.</param>
    /// <returns>A Task that completes when the lock is released (or the app closes).</returns>
    /// <exception cref="ArgumentException">When there is no public constructor matching the arguments, or more than one (the app is not locked).</exception>
    /// <example>
    /// <code>
    /// app.IdleTimeout = TimeSpan.FromMinutes(5);
    /// app.Idle += (_, _) =&gt; _ = app.Lock&lt;LoginDialog&gt;(auth);   // LoginDialog calls Close(true) when the password is correct
    /// </code>
    /// </example>
    public Task Lock<TDialog>(params object?[] args) where TDialog : DvDialog<bool>
    {
        lock (_gate)
        {
            if (_closed)
                return Task.CompletedTask;
            if (_lock is { } current)
                return current.Task;
        }
        var dialog = DialogFactory.Create<TDialog>(args);
        dialog.AppOrNull = this;
        dialog.IsLockScreen = true;
        lock (_gate)
        {
            if (_closed)
                return Task.CompletedTask;
            if (_lock is { } current) // 그 사이 다른 스레드가 잠갔다
                return current.Task;
            dialog.Open = true;
            _lock = dialog;
            _dialogsChanged = true;
        }
        Surface.RequestFrame();
        return dialog.Task;
    }

    /// <summary>
    /// Releases the lock (same as calling <c>Close(true)</c> on the lock dialog). Does nothing if not locked. Can be called from any thread.
    /// </summary>
    /// <remarks>Use it when authentication finished outside the dialog, such as with a card reader or remote release. It unlocks without verification, so the app must authenticate before calling it.</remarks>
    public void Unlock()
    {
        DvDialog<bool>? current;
        lock (_gate)
            current = _lock;
        current?.CloseWith(true);
    }

    /// <summary><see langword="true"/> while locked — from <see cref="Lock{TDialog}"/> until unlocked (including before the surface is applied). Can be read from any thread.</summary>
    public bool IsLocked { get { lock (_gate) return _lock is not null; } }

    /// <summary>
    /// Gets or sets the idle time: if there is no input (touch, mouse, key, editing) for this long, <see cref="Idle"/> is raised once. <see cref="TimeSpan.Zero"/> (default) disables detection. Can be changed from any thread.
    /// </summary>
    /// <remarks>Counting restarts from the moment it is changed. It does not count while locked (<see cref="IsLocked"/>), and starts counting again when unlocked.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">When negative.</exception>
    public TimeSpan IdleTimeout
    {
        get { lock (_gate) return TimeSpan.FromMilliseconds(_idleTimeoutMs); }
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "무입력 시간은 0 이상이어야 합니다.");
            lock (_gate)
            {
                _idleTimeoutMs = (long)value.TotalMilliseconds;
                _lastInputMs = Clock();
                _idleRaised = false;
            }
            Surface.RequestFrame();
        }
    }

    /// <summary>
    /// Raised once when there is no input for <see cref="IdleTimeout"/> (and again after the same time passes following new input). Raised on the input (UI) thread. <c>sender</c> is this <see cref="DvApp"/>.
    /// </summary>
    /// <remarks>
    /// The app decides what to do, such as locking (<c>app.Lock&lt;LoginDialog&gt;()</c>) or returning to the home page. Not raised while locked.
    /// Handler exceptions are reported through <see cref="HandlerError"/> (<c>Kind</c> = <c>"idle"</c>).
    /// </remarks>
    public event EventHandler? Idle;

    /// <summary>
    /// Shows a message box with text and buttons, and returns the button the user pressed. Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// <para>Each call creates and stacks a new box (visible in <see cref="Dialogs"/>). Even when several are shown in a row, each returns its own result.</para>
    /// <para><b>Result</b> — The pressed button. If closed from outside (<see cref="CloseDialogs"/>, a dialog below closing, the window closing), it is
    /// <see cref="DvDialogResult.Cancel"/> even when the button set has no cancel button. The continuation thread rules are the same as <see cref="ShowDialog{TDialog, TResult}"/>.</para>
    /// <para><b>Text</b> — Button labels can be changed with the <c>dv.dialog.ok</c>, <c>dv.dialog.cancel</c>, <c>dv.dialog.yes</c> and <c>dv.dialog.no</c>
    /// keys in <see cref="Strings"/>; otherwise they are "확인" (OK), "취소" (Cancel), "예" (Yes) and "아니요" (No). The box width defaults to 360; long text widens it up to the window width − 32 and then wraps (font size is the theme's <c>font.body</c>). Lines are not re-wrapped if the window shrinks after the box is shown.</para>
    /// </remarks>
    /// <param name="text">The body text. Use newline characters to split lines.</param>
    /// <param name="caption">The title. If <see langword="null"/>, there is no title row.</param>
    /// <param name="buttons">The button set (default: a single OK).</param>
    /// <returns>A Task that completes with the pressed button.</returns>
    /// <example>
    /// <code>
    /// if (await App.MessageBox("레시피를 삭제할까요?", "삭제", DvMessageButtons.YesNo) == DvDialogResult.Yes)
    ///     DeleteRecipe();
    /// </code>
    /// </example>
    public Task<DvDialogResult> MessageBox(string text, string? caption = null, DvMessageButtons buttons = DvMessageButtons.Ok)
        => ShowDialog<DvMessageBoxDialog, DvDialogResult?>(text, caption, buttons)
            .ContinueWith(t => t.Result ?? DvDialogResult.Cancel, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    /// <summary>
    /// Shows a box that asks for a text value, and returns the entered text when OK is pressed. Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// <para>A title row + one input box + [Cancel][OK] buttons (width 360). Editing of the input box starts as soon as it appears (in virtual keyboard mode the keyboard appears right away —
    /// or, if another dialog appeared on top at the same time, once that one closes and this box is on top).
    /// When input is accepted with Done (Enter — the virtual keyboard's Done key, or a <see cref="DvEditExit.Enter"/> commit from the native editor), the box is confirmed with that value immediately.
    /// In native editor mode, pressing OK while editing first commits the text being typed and then handles OK. Pressing OK while the virtual keyboard is open
    /// cancels editing, as on other screens, and that press is not handled (the box stays).</para>
    /// <para><b>Result</b> — OK = the input box's current value (may be an empty string). The Cancel button and closing from outside (<see cref="CloseDialogs"/>, a dialog below closing, the window closing) give
    /// <see langword="null"/>. Each call stacks a new box and each returns its own result. The continuation thread rules are the same as <see cref="ShowDialog{TDialog, TResult}"/>.</para>
    /// <para><b>Text</b> — Button labels can be changed with the <c>dv.dialog.ok</c> and <c>dv.dialog.cancel</c> keys in <see cref="Strings"/>; otherwise they are "확인" (OK) and "취소" (Cancel).</para>
    /// </remarks>
    /// <param name="caption">The title.</param>
    /// <param name="value">The initial value. <see langword="null"/> means an empty string.</param>
    /// <returns>A Task that completes with the entered text on OK, or <see langword="null"/> on cancel or closing from outside.</returns>
    /// <example>
    /// <code>
    /// if (await App.InputBox("레시피 이름", recipe.Name) is { } name)
    ///     recipe.Name = name;
    /// </code>
    /// </example>
    public Task<string?> InputBox(string caption, string? value = null)
        => ShowDialog<DvInputBoxDialog, string?>(caption, value);

    /// <summary>
    /// Shows a box that asks for a number, and returns the value when OK is pressed. Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// <para>A title row + one numeric input box + [Cancel][OK] buttons (width 360). Editing starts as soon as it appears (in virtual keyboard mode the numeric keypad appears right away —
    /// once it is on top, as with <see cref="InputBox"/>).
    /// The editing rules are the same as <c>DvInput keyboard-type="numeric"</c> — input that is not a number or is outside the <paramref name="min"/>–<paramref name="max"/> range is not written to the value,
    /// so the OK result is always a value within range (the last accepted value). When input is accepted with Done (Enter) the box is confirmed immediately; if it is rejected as out of range, the box stays.</para>
    /// <para><b>Result</b> — OK = the current value. The Cancel button and closing from outside (<see cref="CloseDialogs"/>, a dialog below closing, the window closing) give <see langword="null"/>.
    /// The continuation thread rules are the same as <see cref="ShowDialog{TDialog, TResult}"/>, and the button label keys are the same as <see cref="InputBox"/>.</para>
    /// </remarks>
    /// <param name="caption">The title.</param>
    /// <param name="value">The initial value. Must be a finite number within <paramref name="min"/>–<paramref name="max"/> (bounds inclusive).</param>
    /// <param name="min">The minimum allowed value. <see langword="null"/> means no limit.</param>
    /// <param name="max">The maximum allowed value. <see langword="null"/> means no limit.</param>
    /// <returns>A Task that completes with the entered value on OK, or <see langword="null"/> on cancel or closing from outside.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="value"/> is NaN or infinite, when <paramref name="min"/> is greater than <paramref name="max"/>,
    /// or when <paramref name="value"/> is out of range. The box is not registered (pass values that may be out of range through <see cref="Math.Clamp(double, double, double)"/>).</exception>
    /// <example>
    /// <code>
    /// if (await App.InputNumber("설정 온도", SetPoint, 0, 100) is { } v)
    ///     SetPoint = v;
    /// </code>
    /// </example>
    public Task<double?> InputNumber(string caption, double value, double? min = null, double? max = null)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "처음 값은 유한한 숫자여야 합니다.");
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min), min, $"min({min}) 이 max({max}) 보다 큽니다.");
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"처음 값이 범위({min}~{max}) 밖입니다.");
        return ShowDialog<DvNumberBoxDialog, double?>(caption, value, min, max);
    }

    /// <summary>
    /// Shows a box for choosing one item from a list, and returns the index of the chosen item when OK is pressed. Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// <para>A title row + an item list (one per row, tap to mark it selected) + [Cancel][OK] buttons (width 360). With many items, the list area stops at a maximum height of 320 and scrolls
    /// (on a short window it shrinks further so the buttons stay inside the window, and it re-fits if the window size changes while shown).
    /// When no item is chosen (a <paramref name="selected"/> out of range counts as none), the OK button is disabled.</para>
    /// <para><b>Result</b> — OK = the index of the chosen item. The Cancel button and closing from outside (<see cref="CloseDialogs"/>, a dialog below closing, the window closing) give <see langword="null"/>.
    /// The continuation thread rules are the same as <see cref="ShowDialog{TDialog, TResult}"/>, and the button label keys are the same as <see cref="InputBox"/>.</para>
    /// </remarks>
    /// <param name="caption">The title.</param>
    /// <param name="items">The item texts. The contents are copied at call time, so later changes are not reflected in the box.</param>
    /// <param name="selected">The index of the initially selected item. -1 (default) or out of range means no selection.</param>
    /// <returns>A Task that completes with the chosen index on OK, or <see langword="null"/> on cancel or closing from outside.</returns>
    /// <exception cref="ArgumentException">When <paramref name="items"/> is empty. The box is not registered.</exception>
    /// <example>
    /// <code>
    /// if (await App.SelectBox("모드", ["자동", "수동", "정지"], mode) is { } i)
    ///     mode = i;
    /// </code>
    /// </example>
    public Task<int?> SelectBox(string caption, IReadOnlyList<string> items, int selected = -1)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            throw new ArgumentException("항목이 하나도 없습니다.", nameof(items));
        return ShowDialog<DvSelectBoxDialog, int?>(caption, items, selected);
    }

    /// <summary>
    /// All exceptions thrown by app (view model) code in this app's pages and bars are collected here. <c>sender</c> is this <see cref="DvApp"/>.
    /// </summary>
    /// <remarks>
    /// Reports, with the same arguments, each surface's <see cref="DvSurface.HandlerError"/> (handlers, setters, value reads, <c>"attach"</c>), exceptions from page transition notifications (<c>Kind</c> = <c>"hidden"</c>/<c>"shown"</c>,
    /// <c>Member</c> = page type name), exceptions from dialog <see cref="DvDialog.OnClosed"/> (<c>Kind</c> = <c>"closed"</c>, <c>Member</c> = dialog type name),
    /// exceptions from code continuing after an <c>await</c> in a handler or from <c>async void</c> handlers (<c>Kind</c> = <c>"async"</c>, <c>Member</c> = <see langword="null"/>), and exceptions from <see cref="Idle"/> handlers (<c>Kind</c> = <c>"idle"</c>). Raised on the input thread (<c>"closed"</c> when the window closes is raised on the closing thread). Exceptions thrown by subscribers themselves are swallowed and only written to Trace.
    /// </remarks>
    public event EventHandler<DvHandlerErrorEventArgs>? HandlerError;

    /// <summary>
    /// Gets the app most recently attached to a window or control. If one is attached now, it is that app; after it closes, the last app remains (calling <see cref="ShowDialog{TDialog, TResult}"/> on it gives an immediate cancel result).
    /// </summary>
    /// <remarks>
    /// Only one app per process can be attached to a window or control (attaching another app throws), so it can be used anywhere without passing the app instance around.
    /// If the app is detached and another app is attached, this value changes to the new app. Can be read from any thread.
    /// </remarks>
    /// <exception cref="InvalidOperationException">When no app has ever been attached to a window or control.</exception>
    public static DvApp Current
    {
        get
        {
            lock (s_lock)
                return s_last ?? throw new InvalidOperationException("아직 창·컨트롤에 붙은 DvApp 이 없습니다. new ViewWindow(app, …) 등으로 붙인 뒤 쓰거나, 앱 인스턴스를 직접 넘기세요.");
        }
    }

    // ── 내부(어댑터·루트 화면용) ─────────────────────────────────────────

    /// <summary>Attaches an adapter (window or control) to this app. One app to one adapter at a time, and one app per process — otherwise throws.</summary>
    internal void Attach(object host)
    {
        ArgumentNullException.ThrowIfNull(host);
        lock (s_lock) // 잠금 순서: s_lock → _gate
        {
            lock (_gate)
            {
                if (_host is not null)
                    throw new InvalidOperationException("이 DvApp 은 이미 다른 창·컨트롤에 붙어 있습니다. 먼저 그쪽을 닫거나 App 을 바꿔 떼어 내세요.");
                if (s_attached is not null && !ReferenceEquals(s_attached, this))
                    throw new InvalidOperationException("한 프로세스에 DvApp 은 하나만 창·컨트롤에 붙을 수 있습니다. 다른 앱을 먼저 닫거나 떼세요.");
                _host = host;
                _closed = false;
            }
            s_attached = s_last = this;
        }
    }

    /// <summary>Whether an adapter is attached (used by adapters to reject early, before creating a native window).</summary>
    internal bool IsAttached { get { lock (_gate) return _host is not null; } }

    /// <summary>
    /// Whether the app is closed (true after <see cref="Close"/> until the next <see cref="Attach"/>). Simply detaching (<see cref="Detach"/>) is not closing.
    /// </summary>
    internal bool IsClosed { get { lock (_gate) return _closed; } }

    /// <summary>Detaches the attached adapter (when the app stays in use, e.g. when App is replaced). Open dialogs are left alone. Ignored if it is not the attached adapter — it can be attached again later.</summary>
    internal void Detach(object host) => Release(host);

    /// <summary>
    /// Closes the attached adapter (window closed, Dispose). In addition to detaching (<see cref="Detach"/>), calls <see cref="OnClosing"/> and sets <see cref="IsClosed"/> to true.
    /// Ignored if it is not the attached adapter.
    /// </summary>
    internal void Close(object host)
    {
        if (!Release(host))
            return;
        HotReload = false; // 감시 멈춤
        OnClosing(); // _closed 도 여기서 켠다
        using (SyncScope.Enter(Surface))
            DrainPosted(); // 프레임이 더 오지 않는다 — 방금 취소로 완료된 await 의 이어짐(finally 포함)을 그 자리에서 돌린다
    }

    // 창이 닫힘 — 다이얼로그를 그 자리(닫는 스레드 = 어댑터 UI 스레드)에서 정리하고 결과를 완료한다(위→아래). 프레임이 더 오지 않으므로 다음 프레임을 기다리지 않는다.
    // 닫기 대기 중이던 것(결과 확정, 이미 요청 스택에서 빠짐 — 남은 것들보다 위였다)은 확정된 결과로, 열려 있던 것은 취소로.
    // OnClosed 도 이 스레드에서 각 1회(위→아래), 결과 완료 전에. 닫힌 상태는 완료 전에 같은 잠금 안에서 켠다 — 완료의 인라인 이어짐이나
    // 다른 스레드가 그 사이 ShowDialog 를 불러도 즉시 취소 경로로 가서 끝나지 않는 다이얼로그가 생기지 않게.
    private void OnClosing()
    {
        DvDialog[] closing, open;
        lock (_gate)
        {
            _closed = true;
            closing = _closingDialogs.ToArray();
            _closingDialogs.Clear();
            open = _requestedDialogs.ToArray();
            if (_lock is { } lk) // 잠금 층은 맨 위 — 취소로 닫힘(Task 가 끝난다)
            {
                open = [.. open, lk];
                _lock = null;
            }
            foreach (var d in open)
            {
                d.CloseRequested = true;
                d.Open = false;
            }
            _requestedDialogs.Clear();
            _dialogsChanged = false;
        }
        if (_appliedLock is { } al)
        {
            _appliedDialogs.Add(al); // 아래 정리 루프에 같이 태운다
            _appliedLock = null;
        }
        foreach (var d in _appliedDialogs)
        {
            if (d.Surface is { } s)
            {
                s.CancelPendingEdit(); // 결과는 취소 — 입력 중 값은 쓰지 않고, 띄운 팝업(창이 그림)도 닫는다
                _kept.Remove(s); // 화면 자체는 더 그려지지 않는다
            }
        }
        _appliedDialogs.Clear();

        foreach (var d in closing)
            NotifyClosed(d);
        for (var i = open.Length - 1; i >= 0; i--)
            NotifyClosed(open[i]);
        foreach (var d in closing)
            d.CompletePending();
        for (var i = open.Length - 1; i >= 0; i--)
            open[i].CompleteCanceled();
    }

    // host 가 붙어 있던 어댑터면 떼고 true. 정적 붙은 앱 표시도 같이 비운다(s_last 는 유지).
    private bool Release(object host)
    {
        lock (s_lock)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_host, host))
                    return false;
                _host = null;
            }
            if (ReferenceEquals(s_attached, this))
                s_attached = null;
            return true;
        }
    }

    /// <summary>For tests — clears the static state (attached app, last app).</summary>
    internal static void ResetForTests()
    {
        lock (s_lock)
            s_attached = s_last = null;
    }

    /// <summary>This app's synchronization context, installed during root surface input entry points (D15).</summary>
    internal DvSyncContext SyncContext { get; }

    /// <summary>Queues a synchronization context Post (from any thread). Waking is done by the caller (<see cref="DvSyncContext.Post"/>).</summary>
    internal void EnqueuePosted(SendOrPostCallback callback, object? state)
    {
        lock (_gate)
            _posted.Add((callback, state));
    }

    /// <summary>
    /// Drains and runs the Post queue. Called on the input thread at the end of <see cref="ApplyPending"/> (and once when the window closes). Only the items present now are taken and run;
    /// items newly posted while running run at the next frame (prevents infinite loops). Callback exceptions are reported through <see cref="HandlerError"/> (<c>Kind</c> = <c>"async"</c>) and processing continues.
    /// If at least one item ran, the visible surfaces (current page, bars, dialogs) are marked for rebuild — so VM values changed after an <c>await</c> are reflected
    /// in the same frame without <see cref="Invalidate"/> (the root Layout fits the cell surfaces after this).
    /// </summary>
    internal void DrainPosted()
    {
        if (_drainBusy)
            return;
        List<(SendOrPostCallback Callback, object? State)> batch;
        lock (_gate)
        {
            if (_posted.Count == 0)
                return;
            batch = _posted;
            _posted = _draining; // 비어 있는 다른 목록으로 바꿔 끼운다
            _draining = batch;
        }
        _drainBusy = true;
        try
        {
            foreach (var (callback, state) in batch)
            {
                try { callback(state); }
                catch (Exception ex)
                {
                    HandlerErrors.Report(null, ex, "async", null, null); // Trace
                    RaiseHandlerError(new DvHandlerErrorEventArgs(HandlerErrors.Unwrap(ex), "async", null, null));
                }
            }
        }
        finally
        {
            batch.Clear();
            _drainBusy = false;
            MarkShownDirty();
        }
    }

    // 보이는 칸 화면을 다시 만들게 표시만 한다(깨우지 않음 — 지금 프레임 안). 할당 없이 필드를 직접 돈다.
    private void MarkShownDirty()
    {
        _current?.Surface?.MarkDirty();
        _appliedTop?.Surface?.MarkDirty();
        _appliedBottom?.Surface?.MarkDirty();
        _appliedLeft?.Surface?.MarkDirty();
        _appliedRight?.Surface?.MarkDirty();
        foreach (var d in _appliedDialogs)
            d.Surface?.MarkDirty();
        _appliedLock?.Surface?.MarkDirty();
    }

    /// <summary>The applied bars (as seen by the input thread). The public Top etc. hold the last assigned values.</summary>
    internal DvBar? AppliedTop => _appliedTop;
    internal DvBar? AppliedBottom => _appliedBottom;
    internal DvBar? AppliedLeft => _appliedLeft;
    internal DvBar? AppliedRight => _appliedRight;

    /// <summary>Dialogs whose surface is applied (bottom to top). Input thread only — used by root composition.</summary>
    internal IReadOnlyList<DvDialog> AppliedDialogs => _appliedDialogs;

    /// <summary>Whether the request stack (open dialogs, including unapplied ones) is non-empty. Any thread — used to block input to the cells below.</summary>
    internal bool HasRequestedDialogs { get { lock (_gate) return _requestedDialogs.Count > 0; } }

    internal int DialogCount { get { lock (_gate) return _requestedDialogs.Count; } }

    internal DvDialog DialogAt(int index) { lock (_gate) return _requestedDialogs[index]; }

    internal DvDialog? TopDialog { get { lock (_gate) return _requestedDialogs.Count > 0 ? _requestedDialogs[^1] : null; } }

    /// <summary>The requested lock dialog (null if none). Any thread.</summary>
    internal DvDialog<bool>? LockDialog { get { lock (_gate) return _lock; } }

    internal DvDialog[] DialogSnapshot() { lock (_gate) return _requestedDialogs.ToArray(); }

    /// <summary>
    /// Requests closing a dialog (<c>Close</c> of <see cref="DvDialog{TResult}"/>). Any thread. Ignored if already closed or not an open dialog of this app.
    /// Dialogs stacked above it are closed as canceled first, then the result is fixed with <paramref name="setResult"/> (under _gate — the first request wins).
    /// </summary>
    internal void CloseDialog(DvDialog dialog, Action setResult)
    {
        int at;
        lock (_gate)
        {
            if (dialog.CloseRequested)
                return;
            if (ReferenceEquals(dialog, _lock))
            {
                // 잠금 해제 — 잠금 층은 요청 스택 밖. 아래 다이얼로그는 그대로 둔다.
                setResult();
                MarkClosing(dialog);
                _lock = null;
                _lastInputMs = Clock(); // 풀린 때부터 무입력을 다시 센다
                _idleRaised = false;
                at = -1;
            }
            else
            {
                at = _requestedDialogs.IndexOf(dialog);
                if (at < 0)
                    return;
                for (var i = _requestedDialogs.Count - 1; i > at; i--)
                    MarkClosing(_requestedDialogs[i]); // 결과 = default
                setResult();
                MarkClosing(dialog);
                _requestedDialogs.RemoveRange(at, _requestedDialogs.Count - at);
            }
        }
        Surface.RequestFrame();
    }

    // _gate 아래: 닫기 확정 표시 + 닫기 대기열(위→아래 순으로 쌓인다)
    private void MarkClosing(DvDialog d)
    {
        d.CloseRequested = true;
        d.Open = false;
        _closingDialogs.Add(d);
        _dialogsChanged = true;
    }

    /// <summary>
    /// Applies pending requests (global settings, bars, page switch, dialogs). Called on the input thread every frame (before the root Layout).
    /// If there is no current page, the first page added is shown as the start page.
    /// Order: settings, bars, page → dialog closes → new dialogs → result completion (top to bottom) → Post queue.
    /// </summary>
    internal void ApplyPending()
    {
        DvPage? target;
        DvPageTransition transition;
        bool settings, regions;
        DvTheme? theme; DvStrings? strings; ComponentRegistry? registry;
        DvBar? top, bottom, left, right;
        DvDialog[]? closing = null, open = null;
        DvDialog? lockRequested = null;
        var raiseIdle = false;
        lock (_gate)
        {
            if (_dialogsChanged)
            {
                _dialogsChanged = false;
                closing = _closingDialogs.ToArray();
                _closingDialogs.Clear();
                open = _requestedDialogs.ToArray();
                lockRequested = _lock;
            }
            if (_idleTimeoutMs > 0 && !_idleRaised && _lock is null && Clock() - _lastInputMs >= _idleTimeoutMs)
                raiseIdle = _idleRaised = true;
            target = _requestedPage;
            _requestedPage = null;
            transition = _requestedTransition ?? _pageTransition;
            _requestedTransition = null;
            settings = _settingsChanged;
            regions = _regionsChanged;
            _settingsChanged = _regionsChanged = false;
            (theme, strings, registry) = (_theme, _strings, _registry);
            (top, bottom, left, right) = (_top, _bottom, _left, _right);
        }

        if (settings)
        {
            (_appliedTheme, _appliedStrings, _appliedRegistry) = (theme, strings, registry);
            foreach (var s in _kept.Prepend(Surface)) // 루트도 — 팝업·가상 키보드를 루트가 그린다
            {
                s.SetTheme(theme);
                s.SetRegistry(registry);
                s.Strings = strings;
            }
        }

        if (regions)
            (_appliedTop, _appliedBottom, _appliedLeft, _appliedRight) = (top, bottom, left, right);

        if (target is null && _current is null)
            target = Pages.First;
        try
        {
            if (target is not null && !ReferenceEquals(target, _current))
            {
                var had = _current?.Surface is not null;
                Transition(target);
                _transitionToTake = had ? transition : DvPageTransition.None; // 첫 페이지는 움직이지 않음
            } // 페이지 마크업 오류는 던진다 — 그래도 이 프레임 다이얼로그 변경(이미 스냅숏으로 비웠다)은 아래에서 적용한다
        }
        finally
        {
            try
            {
                if (closing is not null)
                    ApplyDialogs(closing, open!, lockRequested); // 새 다이얼로그 마크업 오류는 던지지만, 기다리는 이어짐은 아래에서 돈다
            }
            finally
            {
                if (raiseIdle)
                    RaiseIdle(); // 처리기가 Lock 을 부르면 다음 프레임에 잠금 층이 뜬다
                DrainPosted(); // 마지막 — 상태 적용을 모두 끝낸 뒤 await 이어짐·async void 예외를 돈다
            }
        }
    }

    /// <summary>A page's surface — built on the first request and kept (Attach → OnLoaded once at that time). On the input thread.</summary>
    internal DvSurface SurfaceOf(DvPage page)
    {
        if (page.Surface is { } s)
            return s;
        s = Create(MarkupOf(page.GetMarkup(), page.GetMarkupFile()), page);
        page.Surface = s;
        return s;
    }

    /// <summary>A bar's surface — built on the first request and kept. On the input thread.</summary>
    internal DvSurface SurfaceOf(DvBar bar)
    {
        if (bar.Surface is { } s)
            return s;
        s = Create(MarkupOf(bar.GetMarkup(), bar.GetMarkupFile()), bar);
        bar.Surface = s;
        return s;
    }

    /// <summary>A dialog's surface — built at the frame it is applied and kept until closed (Attach → OnLoaded once at that time). On the input thread.</summary>
    internal DvSurface SurfaceOf(DvDialog dialog)
    {
        if (dialog.Surface is { } s)
            return s;
        s = Create(MarkupOf(dialog.GetMarkup(), dialog.GetMarkupFile()), dialog);
        s.AfterFirstSlotLayout = dialog.AfterFirstLayout;
        s.EditCommitted = dialog.AfterEditCommitted;
        dialog.Surface = s;
        return s;
    }

    // ── 루트 화면 합성(ISurfaceComposer — 루트 Layout 이 입력 스레드에서 부름) ─────────────

    void ISurfaceComposer.BeforeLayout() => ApplyPending();

    bool ISurfaceComposer.HasPending
    {
        get { lock (_gate) return _requestedPage is not null || _regionsChanged || _settingsChanged || _dialogsChanged || _posted.Count > 0; }
    }

    bool ISurfaceComposer.BlocksBelow { get { lock (_gate) return _requestedDialogs.Count > 0 || _lock is not null; } }

    // 잠겨 있으면 잠금 다이얼로그만(화면 적용 전이면 아무도) 입력을 받는다
    DvSurface? ISurfaceComposer.PageSurface => _current?.Surface;

    DvPageTransition ISurfaceComposer.TakePageTransition()
    {
        var t = _transitionToTake;
        _transitionToTake = DvPageTransition.None;
        return t;
    }

    DvSurface? ISurfaceComposer.ModalInput
    {
        get
        {
            lock (_gate)
                return _lock is { } lk ? lk.Surface : _requestedDialogs.Count > 0 ? _requestedDialogs[^1].Surface : null;
        }
    }

    void ISurfaceComposer.NoteInput()
    {
        lock (_gate)
        {
            _lastInputMs = Clock();
            _idleRaised = false;
        }
    }

    long? ISurfaceComposer.NextDue(long nowMs)
    {
        lock (_gate)
            return _idleTimeoutMs <= 0 || _idleRaised || _lock is not null ? null : Math.Max(nowMs, _lastInputMs + _idleTimeoutMs);
    }

    // 다이얼로그 칸 여백 — 창 각 변에서 이만큼 안쪽으로 크기를 제한한다
    private const float DialogMargin = 16;

    // 칸 배치: Top·Bottom = 창 폭 전체(높이 = 틀 루트 높이), Left·Right = 그 사이 높이(폭 = 틀 루트 폭), 페이지 = 나머지.
    // 창보다 크면 Top → Bottom, Left → Right 순으로 남은 만큼만 준다. 영역이 없으면 칸도 없다.
    // 적용된 다이얼로그는 맨 뒤에 아래→위 모달 칸으로: 크기 = 루트 측정 크기(창 폭 − 32 에서 줄바꿈, 창 − 여백 16×2 로 제한), 위치 = 창 전체 가운데.
    // 높이가 잘리면 그 칸 높이로 배치될 때 안의 스크롤 상자가 줄어든다(flex shrink).
    IReadOnlyList<(SKRect Rect, DvSurface Child, bool Modal)> ISurfaceComposer.Compose(float w, float h)
    {
        var slots = new List<(SKRect, DvSurface, bool)>(5 + _appliedDialogs.Count);
        var top = _appliedTop is { } t ? SurfaceOf(t) : null;
        var bottom = _appliedBottom is { } b ? SurfaceOf(b) : null;
        var left = _appliedLeft is { } l ? SurfaceOf(l) : null;
        var right = _appliedRight is { } r ? SurfaceOf(r) : null;

        // Top·Bottom 은 창 폭으로 배치되므로 그 폭에서 줄바꿈해 높이를 잰다
        var th = top is null ? 0 : Math.Clamp(top.MeasureRoot(w, h, w).Height, 0, h);
        var bh = bottom is null ? 0 : Math.Clamp(bottom.MeasureRoot(w, h, w).Height, 0, h - th);
        var midH = h - th - bh;
        var lw = left is null ? 0 : Math.Clamp(left.MeasureRoot(w, midH).Width, 0, w);
        var rw = right is null ? 0 : Math.Clamp(right.MeasureRoot(w, midH).Width, 0, w - lw);

        if (top is not null)
            slots.Add((new SKRect(0, 0, w, th), top, false));
        if (bottom is not null)
            slots.Add((new SKRect(0, h - bh, w, h), bottom, false));
        if (left is not null)
            slots.Add((new SKRect(0, th, lw, th + midH), left, false));
        if (right is not null)
            slots.Add((new SKRect(w - rw, th, w, th + midH), right, false));
        if (_current?.Surface is { } page)
            slots.Add((new SKRect(lw, th, w - rw, th + midH), page, false));

        foreach (var d in _appliedDialogs)
        {
            if (d.Surface is { } ds)
                slots.Add((DialogRect(ds, w, h), ds, true));
        }
        if (_appliedLock?.Surface is { } lockSurface)
            slots.Add((DialogRect(lockSurface, w, h), lockSurface, true)); // 잠금 층 = 맨 위
        return slots;
    }

    // 다이얼로그 칸: 가용 폭 = 칸 최대 폭 — 글자는 이 폭에서 줄바꿈해 재고, 칸에 배치할 때도 같은 폭이라 같은 줄바꿈. 창 − 여백 16×2 로 제한, 창 가운데.
    private static SKRect DialogRect(DvSurface ds, float w, float h)
    {
        var size = ds.MeasureRoot(w, h, Math.Max(0, w - 2 * DialogMargin));
        var dw = Math.Clamp(size.Width, 0, Math.Max(0, w - 2 * DialogMargin));
        var dh = Math.Clamp(size.Height, 0, Math.Max(0, h - 2 * DialogMargin));
        var x = (w - dw) / 2;
        var y = (h - dh) / 2;
        return new SKRect(x, y, x + dw, y + dh);
    }

    // ── 구현 ────────────────────────────────────────────────────────────

    // 틀은 한 자리에만: 같은 앱의 다른 영역(요청 값 기준)에 놓여 있으면 거절(옮기려면 먼저 그 영역을 null 로). 한 번 놓인 틀은 이 앱 소속으로 남는다.
    private void SetRegion(ref DvBar? slot, DvBar? value)
    {
        lock (OwnerLock)
        {
            if (value is not null && value.AppOrNull is not null && !ReferenceEquals(value.AppOrNull, this))
                throw new InvalidOperationException($"틀 '{value.GetType().Name}' 는 이미 다른 DvApp 에 놓여 있습니다.");
            lock (_gate)
            {
                if (value is not null && !ReferenceEquals(slot, value) &&
                    (ReferenceEquals(_top, value) || ReferenceEquals(_bottom, value) || ReferenceEquals(_left, value) || ReferenceEquals(_right, value)))
                    throw new InvalidOperationException($"틀 '{value.GetType().Name}' 는 이미 이 앱의 다른 영역에 놓여 있습니다. 옮기려면 그 영역을 먼저 null 로 비우세요.");
                if (value is not null)
                    value.AppOrNull = this;
                slot = value;
                _regionsChanged = true;
            }
        }
        Surface.RequestFrame();
    }

    private void RaiseRenderRequested()
    {
        if (RenderRequested is not { } handlers)
            return;
        foreach (var d in handlers.GetInvocationList())
        {
            try { ((EventHandler)d)(this, EventArgs.Empty); }
            catch (Exception ex)
            {
                Trace.TraceError($"Devinno.UI: RenderRequested 구독자 예외(무시함): {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // ── 핫리로드 ──

    private HotReloader? _hot;

    /// <summary>
    /// When <see langword="true"/>, during development the screens' dvml files are watched, and on save that page, bar or dialog is rebuilt from the new markup (hot reload). Default <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Development only.</b> Do not enable it in a deployed app (wrapping it in <c>#if DEBUG</c> is recommended). File paths are absolute dvml paths embedded by the source generator at build time, so it works only on the PC that built the app.</para>
    /// <para>About 0.15 s after saving, the screen is swapped at the next frame. VM objects and values and <c>id</c> handles stay the same (<c>OnLoaded</c> is not called again), and the surface is rebuilt as with <see cref="Invalidate"/>.
    /// Changes that require C# changes, such as handle fields for newly added <c>id</c>s or new VM properties, need a rebuild. Element-based state remembered by the surface (tab index without <c>value</c>, collapsible panel and tree expansion) resets to the initial state.</para>
    /// <para>If saved with invalid XML, the previous screen is kept and the error is reported through <see cref="HandlerError"/> (<c>Kind</c> = <c>"hotreload"</c>, <c>Member</c> = file path). It is applied again once fixed and saved.
    /// Surfaces not built yet read the latest file when first built. User components (<c>DvComponent</c>) and styles registered from code are not watched.</para>
    /// <para>Can be changed from any thread. Watching stops when the app closes.</para>
    /// </remarks>
    public bool HotReload
    {
        get { lock (_gate) return _hot is not null; }
        set
        {
            HotReloader? old = null;
            lock (_gate)
            {
                if (value == (_hot is not null) || (value && _closed))
                    return;
                if (value)
                    _hot = new HotReloader(this);
                else
                {
                    old = _hot;
                    _hot = null;
                }
            }
            old?.Dispose();
            if (value)
                foreach (var file in KnownMarkupFiles())
                    _hot!.Watch(file);
        }
    }

    // 지금 앱이 아는 화면의 dvml 파일(페이지·영역·열린 다이얼로그)
    private IEnumerable<string> KnownMarkupFiles()
    {
        var list = new List<string?>();
        lock (_gate)
        {
            list.AddRange(Pages.Select(p => p.GetMarkupFile()));
            list.AddRange(new[] { _top, _bottom, _left, _right }.Select(b => b?.GetMarkupFile()));
            list.AddRange(_requestedDialogs.Concat(_appliedDialogs).Select(d => d.GetMarkupFile()));
        }
        return list.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // 화면을 처음 만들 때의 마크업 — 핫리로드 중이면 그 파일을 감시하고 최신 파일 원문을(읽지 못하면 빌드 때 원문)
    private string MarkupOf(string compiled, string? file)
    {
        if (file is null || _hot is not { } hot)
            return compiled;
        hot.Watch(file);
        return File.Exists(file) && HotReloader.Read(file) is { } text ? text : compiled;
    }

    /// <summary>Swaps the (already built) surfaces that use the changed dvml file to the new source. On the input thread (Post queue).</summary>
    internal void ReloadFiles(IReadOnlyCollection<string> paths)
    {
        var screens = new List<(string? File, DvSurface? Surface)>();
        lock (_gate)
        {
            screens.AddRange(Pages.Select(p => (p.GetMarkupFile(), p.Surface)));
            screens.AddRange(new[] { _top, _bottom, _left, _right }.OfType<DvBar>().Select(b => (b.GetMarkupFile(), b.Surface)));
            screens.AddRange(_appliedDialogs.Select(d => (d.GetMarkupFile(), d.Surface)));
            if (_lock is { } lk)
                screens.Add((lk.GetMarkupFile(), lk.Surface));
        }

        foreach (var path in paths)
        {
            var hits = screens.Where(x => x.Surface is not null && x.File is { } f &&
                                          string.Equals(HotReloader.Normalize(f), path, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count == 0 || HotReloader.Read(path) is not { } text)
                continue;
            foreach (var (_, surface) in hits)
            {
                if (surface!.ReloadMarkup(text) is { } error)
                    RaiseHandlerError(new DvHandlerErrorEventArgs(error, "hotreload", path, null));
                else
                    Trace.TraceInformation($"Devinno.UI: 핫리로드 — {Path.GetFileName(path)}");
            }
        }
    }

    private IEnumerable<DvBar> AppliedBars()
    {
        if (_appliedTop is { } t) yield return t;
        if (_appliedBottom is { } b) yield return b;
        if (_appliedLeft is { } l) yield return l;
        if (_appliedRight is { } r) yield return r;
    }

    private DvSurface Create(string markup, object vm)
    {
        var s = new DvSurface(markup, vm, _appliedTheme, _appliedRegistry);
        s.Strings = _appliedStrings;
        s.HandlerError += (_, e) => RaiseHandlerError(e); // 루트(ParentSurface)로도 올라가지만 앱은 루트를 구독하지 않는다 — 한 번만
        s.Wake = RaiseRenderRequested; // 핸들 쓰기·클릭·폴링 Invalidate → 어댑터 깨우기
        _kept.Add(s);
        return s;
    }

    // 이전 OnHidden → (새 화면이면 생성 → OnLoaded) → 새 OnShown. 알림 예외는 HandlerError 로 알리고 계속한다.
    private void Transition(DvPage target)
    {
        var old = _current;
        if (old is not null)
        {
            old.Surface?.CommitPendingEdit(); // 떠나는 페이지의 입력 중 값은 살리고(nav 전환과 같은 규칙) 띄운 팝업은 닫는다 — OnHidden 이 커밋된 값을 본다
            Notify(old, "hidden", static p => p.RaiseHidden());
        }
        SurfaceOf(target); // 마크업 오류(XmlException 등)는 개발자 오류 — 그대로 던진다(현재 페이지는 그대로)
        _current = target;
        target.Surface?.Invalidate(); // 보관된 페이지 — 숨겨진 동안 바뀐 VM 값·OnShown 의 변경을 다시 읽는다(스크롤·편집·컨트롤 값은 Remap 으로 유지)
        Notify(target, "shown", static p => p.RaiseShown());
        foreach (var bar in AppliedBars())
            bar.Surface?.Invalidate(); // CurrentPage 바인딩(선택 표시) 갱신
    }

    // 닫힌 다이얼로그 정리(편집 취소·팝업 닫기, _kept 에서 제거, OnClosed) → 새 다이얼로그 적용(아래 칸 편집 커밋, 화면 생성) → 결과 완료(위→아래).
    // closing 은 위→아래 순, open 은 이 프레임의 요청 스택(아래→위). 같은 프레임에 열고 닫힌 것은 open 에 없으므로 화면을 만들지 않는다.
    private void ApplyDialogs(DvDialog[] closing, DvDialog[] open, DvDialog? lockRequested)
    {
        foreach (var d in closing)
        {
            var applied = _appliedDialogs.Remove(d);
            if (ReferenceEquals(d, _appliedLock))
            {
                _appliedLock = null;
                applied = true;
            }
            if (applied && d.Surface is { } s)
            {
                s.CancelPendingEdit(); // 결과가 이미 정해졌다 — 입력 중 값은 쓰지 않는다
                _kept.Remove(s);
            }
            NotifyClosed(d);
        }

        List<DvDialog>? failed = null; // 화면 생성에 실패해 취소로 닫은 것(위→아래)
        try
        {
            var committed = false;
            void CommitBelow()
            {
                if (committed)
                    return;
                committed = true; // 아래 칸(페이지·영역·아래 다이얼로그)의 입력 중 값은 살리고 팝업은 닫는다 — 편집 중인 칸은 맨 위 다이얼로그뿐이게
                _current?.Surface?.CommitPendingEdit();
                foreach (var bar in AppliedBars())
                    bar.Surface?.CommitPendingEdit();
                foreach (var below in _appliedDialogs)
                    below.Surface?.CommitPendingEdit();
                ClearHoverBelow(); // 막 아래에는 hover 가 남지 않는다 — 포인터가 움직이지 않아도
            }

            foreach (var d in open)
            {
                if (_appliedDialogs.Contains(d))
                    continue;
                if (_appliedLock is null)
                    CommitBelow(); // 잠긴 뒤 뜬 다이얼로그는 잠금 층 아래 — 잠금 다이얼로그의 입력(비밀번호)은 건드리지 않는다
                try { SurfaceOf(d); }
                catch
                {
                    // 마크업 오류는 개발자 오류 — 한 번 그대로 던진다(페이지 전환 실패와 같이). 다만 기다리는 await 가 매달리지 않게
                    // 그 다이얼로그(와 그 위에 요청된 것들)는 스택에서 빼고 취소로 닫는다. 결과 완료·DrainPosted 는 finally 에서 계속.
                    failed = new List<DvDialog>();
                    lock (_gate)
                    {
                        var at = _requestedDialogs.IndexOf(d);
                        if (at >= 0)
                        {
                            for (var i = _requestedDialogs.Count - 1; i >= at; i--)
                            {
                                var x = _requestedDialogs[i];
                                x.CloseRequested = true;
                                x.Open = false;
                                failed.Add(x);
                            }
                            _requestedDialogs.RemoveRange(at, _requestedDialogs.Count - at);
                        }
                    }
                    foreach (var x in failed)
                        NotifyClosed(x);
                    throw;
                }
                _appliedDialogs.Add(d);
            }

            if (lockRequested is not null && !ReferenceEquals(_appliedLock, lockRequested))
            {
                CommitBelow();
                try { SurfaceOf(lockRequested); }
                catch
                {
                    // 잠금 화면 마크업 오류 — 잠그지 못한 채 한 번 던진다(기다리는 Task 는 아래에서 끝낸다)
                    lock (_gate)
                    {
                        if (ReferenceEquals(_lock, lockRequested))
                            _lock = null;
                        lockRequested.CloseRequested = true;
                        lockRequested.Open = false;
                    }
                    NotifyClosed(lockRequested);
                    (failed ??= new List<DvDialog>()).Add(lockRequested);
                    throw;
                }
                _appliedLock = lockRequested;
            }
        }
        finally
        {
            Complete(closing); // 새 다이얼로그 적용이 던져도 닫힌 것의 결과는 정확히 한 번 전달
            if (failed is not null)
                Complete([.. failed]); // 결과 = default(닫기 요청 결과를 정한 적 없음)
        }
    }

    // 무입력 알림 — 처리기 예외는 HandlerError("idle")로 알리고 계속한다.
    private void RaiseIdle()
    {
        if (Idle is not { } handlers)
            return;
        foreach (var d in handlers.GetInvocationList())
        {
            try { ((EventHandler)d)(this, EventArgs.Empty); }
            catch (Exception ex)
            {
                HandlerErrors.Report(null, ex, "idle", null, null); // Trace
                RaiseHandlerError(new DvHandlerErrorEventArgs(HandlerErrors.Unwrap(ex), "idle", null, null));
            }
        }
    }

    // 아래 칸(페이지·영역·이미 뜬 다이얼로그)의 hover 를 지운다 — 새 다이얼로그가 덮을 때.
    private void ClearHoverBelow()
    {
        _current?.Surface?.ClearHover();
        foreach (var bar in AppliedBars())
            bar.Surface?.ClearHover();
        foreach (var below in _appliedDialogs)
            below.Surface?.ClearHover();
    }

    // OnClosed 알림 — 예외는 HandlerError("closed", 다이얼로그 형식 이름)로 알리고 계속한다.
    private void NotifyClosed(DvDialog d)
    {
        try { d.RaiseClosed(); }
        catch (Exception ex)
        {
            var member = d.GetType().Name;
            HandlerErrors.Report(null, ex, "closed", member, null); // Trace
            RaiseHandlerError(new DvHandlerErrorEventArgs(HandlerErrors.Unwrap(ex), "closed", member, null));
        }
    }

    // 결과 완료 — 상태 변경이 모두 끝난 뒤(위→아래). 컨텍스트 없는 곳(통신 스레드)의 await 는 여기서 바로 이어지고(입력 스레드),
    // 앱 컨텍스트에서 await 한 것은 Post 되어 곧이은 DrainPosted 에서 돈다. 그러려면 완료하는 동안 앱 컨텍스트를 걷어야 한다
    // (컨텍스트가 깔려 있으면 런타임이 컨텍스트 없는 이어짐을 인라인하지 않고 스레드 풀로 보낸다).
    private static void Complete(DvDialog[] closing)
    {
        if (closing.Length == 0)
            return;
        var prev = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            foreach (var d in closing)
                d.CompletePending();
        }
        finally { SynchronizationContext.SetSynchronizationContext(prev); }
    }

    private void Notify(DvPage page, string kind, Action<DvPage> call)
    {
        try { call(page); }
        catch (Exception ex)
        {
            var member = page.GetType().Name;
            HandlerErrors.Report(null, ex, kind, member, null); // Trace
            RaiseHandlerError(new DvHandlerErrorEventArgs(HandlerErrors.Unwrap(ex), kind, member, null));
        }
    }

    private void RaiseHandlerError(DvHandlerErrorEventArgs e)
    {
        if (HandlerError is not { } handlers)
            return;
        foreach (var d in handlers.GetInvocationList())
        {
            try { ((EventHandler<DvHandlerErrorEventArgs>)d)(this, e); }
            catch (Exception ex)
            {
                Trace.TraceError($"Devinno.UI: HandlerError 구독자 예외(무시함): {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
