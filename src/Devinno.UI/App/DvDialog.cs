namespace Devinno.UI.App;

/// <summary>
/// The non-generic base class of dialogs that the app (<see cref="DvApp"/>) shows over pages (the element type of <see cref="DvApp.Dialogs"/>).
/// Do not inherit it directly; inherit <see cref="DvDialog{TResult}"/>, which defines the result type.
/// </summary>
/// <remarks>
/// <para><b>How to create</b> — the same as a page. Add <c>partial class LoginDialog : DvDialog&lt;string&gt;</c> and a <c>LoginDialog.dvml</c> whose root has
/// <c>vm="네임스페이스.LoginDialog"</c> (namespace-qualified name). The source generator fills in the <see cref="MarkupSource"/> override, the <c>id</c> fields and <c>OnLoaded()</c>.</para>
/// <para><b>Lifetime</b> — the app creates the instance inside <see cref="DvApp.ShowDialog{TDialog, TResult}"/> (there is no way to show an instance created with <c>new</c>).
/// From the moment it is registered, <see cref="IsOpen"/> is <see langword="true"/> and it appears in <see cref="DvApp.Dialogs"/>; its screen is built on the next frame on the input (UI) thread
/// (<c>OnLoaded()</c> runs once at that point). When closed, the screen is detached on the next frame, <see cref="OnClosed"/> is called, and then the result is delivered. An instance that has been closed cannot be shown again.</para>
/// <para><b>Threads</b> — the constructor runs on the thread that called <c>ShowDialog</c> (the screen does not exist yet, so do not touch the UI or <see cref="App"/>).
/// Handlers, <c>OnLoaded()</c> and <see cref="OnClosed"/> are called on the input thread (only <see cref="OnClosed"/> when the window closes runs on the closing thread).</para>
/// <para><b>Preventing duplicates</b> — the base class has no Id or key property. To avoid showing the same dialog twice, give the dialog class its own key property (e.g. <c>public int DeviceId</c>) and
/// check with <c>App.Dialogs.OfType&lt;DeviceDialog&gt;().Any(d =&gt; d.DeviceId == id)</c> before showing it.</para>
/// </remarks>
public abstract class DvDialog
{
    private volatile DvApp? _app;
    private volatile bool _open;
    private volatile DvSurface? _surface;

    /// <summary>The app that showed this dialog.</summary>
    /// <exception cref="InvalidOperationException">When read inside the dialog constructor (the app is connected right after construction).</exception>
    public DvApp App => _app ?? throw new InvalidOperationException($"{GetType().Name} 은(는) 아직 앱에 등록되지 않았습니다. 생성자가 아닌 처리기·OnLoaded 에서 App 을 쓰세요.");

    /// <summary>
    /// <see langword="true"/> while open — from the moment it is registered with <c>ShowDialog</c> until closing is requested (including while the screen has not been built yet). Can be read from any thread.
    /// </summary>
    public bool IsOpen => _open;

    /// <summary>
    /// Called once after the dialog has closed. Do cleanup here, such as unsubscribing from communication.
    /// </summary>
    /// <remarks>
    /// Called exactly once, whatever the way it closed. Usually it is called on the input thread on the frame after closing; when the window (app) closes, it is called in place on the closing thread (the adapter's UI thread).
    /// It is called after the screen is detached and before the result is delivered. A thrown exception is reported through <see cref="DvApp.HandlerError"/> (<c>Kind</c> = <c>"closed"</c>, <c>Member</c> = the dialog type name),
    /// and closing and result delivery continue.
    /// </remarks>
    protected virtual void OnClosed()
    {
    }

    /// <summary>
    /// The markup (dvml source) of this dialog. The source generator overrides it with the dvml's <c>Markup</c> constant, so you normally do not write it yourself.
    /// </summary>
    /// <exception cref="InvalidOperationException">When no dvml is connected to this dialog (no override).</exception>
    protected virtual string MarkupSource => throw new InvalidOperationException("dvml 이 연결되지 않았습니다");

    /// <summary>The owning app (null before registration).</summary>
    internal DvApp? AppOrNull { get => _app; set => _app = value; }

    /// <summary>Open flag — the app changes it under _gate.</summary>
    internal bool Open { set => _open = value; }

    /// <summary>Whether closing has been requested (result fixed) — read and written under the app's _gate.</summary>
    internal bool CloseRequested { get; set; }

    /// <summary>The screen of this dialog kept by the app (created on the apply frame). Volatile because <see cref="DvApp.Invalidate"/> on other threads reads it.</summary>
    internal DvSurface? Surface { get => _surface; set => _surface = value; }

    internal string GetMarkup() => MarkupSource;

    /// <summary>The absolute path of this screen's dvml file. The source generator fills it at build time, and <see cref="DvApp.HotReload"/> watches this file. <see langword="null"/> if none.</summary>
    protected virtual string? MarkupFile => null;

    internal string? GetMarkupFile() => MarkupFile;

    internal void RaiseClosed() => OnClosed();

    /// <summary>Right after the screen is attached to the window and laid out (input thread, same frame). Called again on every layout pass until it returns true (default = only the first time).
    /// This is where the built-in input box starts editing once it is on top, and where the built-in selection box scrolls the chosen row into view.</summary>
    internal virtual bool AfterFirstLayout() => true;

    /// <summary>Whether the dialog was shown on the lock layer (<see cref="DvApp.Lock{TDialog}"/>) — set by the app before registration.</summary>
    internal bool IsLockScreen { get; set; }

    /// <summary>Whether this is the open, topmost dialog that receives input. While locked, only the lock dialog. Any thread.</summary>
    internal bool IsTopOpen => IsOpen && AppOrNull is { } app &&
        (IsLockScreen ? ReferenceEquals(app.LockDialog, this) : !app.IsLocked && ReferenceEquals(app.TopDialog, this));

    /// <summary>Right after an on-screen edit is accepted and finished (input thread). This is where the built-in input box treats completion (Enter) as confirmation.</summary>
    internal virtual void AfterEditCommitted(DvEditExit exit)
    {
    }

    /// <summary>Completes the result Task with the canceled result (default); ignored if already completed.</summary>
    internal abstract void CompleteCanceled();

    /// <summary>Completes the result Task with the result fixed at the close request; ignored if already completed.</summary>
    internal abstract void CompletePending();
}

/// <summary>
/// The base class of dialogs whose result type is <typeparamref name="TResult"/>. Inside the dialog, set the result and close it with <see cref="Close(TResult)"/>.
/// </summary>
/// <typeparam name="TResult">
/// The result type. When closed from outside (<see cref="DvApp.CloseDialogs"/>, a dialog below it closing, the app closing), the result is <c>default</c>, so
/// a type that can be told apart from cancellation (a reference type, or a nullable type such as <c>bool?</c> or <c>int?</c>) is recommended.
/// </typeparam>
/// <remarks>
/// <para>The result is delivered exactly once, whatever the way it closed. Closing can be requested from any thread, and the value of the first request is the result (later requests are ignored).</para>
/// <para>The result is delivered on the input thread on the frame after the close request, after screen cleanup and <see cref="DvDialog.OnClosed"/> have finished.
/// If a page handler <c>await</c>ed it, execution continues on the input thread in the same frame, and VM values changed there are re-read automatically in that frame (no <see cref="DvApp.Invalidate"/> needed).
/// If it was <c>await</c>ed somewhere without a context, such as a communication thread, execution continues on the input thread that delivered the result (hand heavy work after that to <c>Task.Run</c>).
/// When the app (window) closes, the canceled result is delivered in place, and the handler's continuation also runs in place on the closing thread.</para>
/// </remarks>
/// <example>
/// <code>
/// public partial class ConfirmDialog : DvDialog&lt;bool?&gt;
/// {
///     public string Message { get; }
///     public ConfirmDialog(string message) =&gt; Message = message;
///     public void Ok() =&gt; Close(true);
///     public void No() =&gt; Close(false);
/// }
///
/// bool? ok = await App.ShowDialog&lt;ConfirmDialog, bool?&gt;("삭제할까요?");
/// </code>
/// </example>
public abstract class DvDialog<TResult> : DvDialog
{
    private readonly TaskCompletionSource<TResult?> _tcs = new(); // 옵션 없음 — 완료는 ApplyPending 이 상태 변경을 끝낸 뒤
    private TResult? _result; // 닫기 요청 때 확정 — 앱의 _gate 아래에서 쓰고, 완료(입력 스레드)에서 읽는다

    /// <summary>
    /// Sets the result and closes the dialog. Can be called from any thread. Does nothing if already closed (including when closing has been requested).
    /// </summary>
    /// <remarks>
    /// <see cref="DvDialog.IsOpen"/> becomes <see langword="false"/> immediately and the dialog is removed from <see cref="DvApp.Dialogs"/>. Dialogs stacked above this one are closed first as canceled.
    /// The screen is detached on the next frame, and any edit in progress is discarded without being written.
    /// A lock dialog (<see cref="DvApp.Lock{TDialog}"/>) closes only on <c>Close(true)</c> (= unlock); other values are ignored and the lock stays.
    /// </remarks>
    /// <param name="result">The result to return.</param>
    protected void Close(TResult? result)
    {
        if (IsLockScreen && !(result is bool b && b))
            return; // 잠금은 인증 성공(true)으로만 풀린다
        CloseWith(result);
    }

    /// <summary>Closes as canceled (<c>Close(default)</c>). The rules are the same as <see cref="Close(TResult)"/>.</summary>
    protected void Close() => Close(default);

    /// <summary>Sets the result and closes (without the lock rule — used by <see cref="DvApp.Unlock"/>).</summary>
    internal void CloseWith(TResult? result) => AppOrNull?.CloseDialog(this, () => _result = result);

    /// <summary>The result Task (returned by <c>ShowDialog</c>).</summary>
    internal Task<TResult?> Task => _tcs.Task;

    internal override void CompleteCanceled() => _tcs.TrySetResult(default);

    internal override void CompletePending() => _tcs.TrySetResult(_result);
}
