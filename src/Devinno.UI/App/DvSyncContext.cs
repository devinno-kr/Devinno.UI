namespace Devinno.UI.App;

/// <summary>
/// Synchronization context installed during the input-thread entry points of the app's root surface (D15). Code after <c>await</c> in page and dialog handlers
/// continues on the input thread in the next frame, and exceptions from <c>async void</c> handlers do not kill the process but go to <see cref="DvApp.HandlerError"/> (<c>"async"</c>).
/// </summary>
/// <remarks>
/// <see cref="Post"/> can be called from any thread — it enqueues to the app queue and wakes the adapter. The queue is drained on the input thread by <see cref="DvApp.DrainPosted"/> every frame
/// (at the end of <c>ApplyPending</c>). <see cref="Send"/> works only on the input thread (while this context is installed), to prevent deadlocks.
/// </remarks>
internal sealed class DvSyncContext(DvApp app) : SynchronizationContext
{
    /// <summary>Enqueues the callback to the app queue and requests the next frame. From any thread.</summary>
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        app.EnqueuePosted(d, state);
        app.Surface.RequestFrame();
    }

    /// <summary>Runs immediately on the input thread where this context is installed; otherwise throws <see cref="NotSupportedException"/> (waiting could deadlock).</summary>
    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (!ReferenceEquals(Current, this))
            throw new NotSupportedException("Devinno.UI 동기화 컨텍스트의 Send 는 입력 스레드에서만 쓸 수 있습니다. 다른 스레드에서는 Post 를 쓰세요.");
        d(state);
    }

    /// <summary>Returns the same app's context (does not create a copy — comparison and nesting checks work by reference).</summary>
    public override SynchronizationContext CreateCopy() => this;
}

/// <summary>
/// Scope that installs and restores <see cref="DvSyncContext"/> at the root surface's public input entry points. Allocation-free (<c>using var _ = SyncScope.Enter(this);</c>).
/// </summary>
internal readonly struct SyncScope : IDisposable
{
    private readonly SynchronizationContext? _prev;
    private readonly bool _set;

    private SyncScope(SynchronizationContext? prev)
    {
        _prev = prev;
        _set = true;
    }

    /// <summary>
    /// If <paramref name="surface"/> is a <see cref="DvApp"/> root surface, installs that app's context. Does nothing for other surfaces (plain or slot surfaces) or if the same context is already installed (nested call).
    /// </summary>
    public static SyncScope Enter(DvSurface surface)
    {
        if (surface.Composer is not DvApp app)
            return default;
        var prev = SynchronizationContext.Current;
        var ctx = app.SyncContext;
        if (ReferenceEquals(prev, ctx))
            return default;
        SynchronizationContext.SetSynchronizationContext(ctx);
        return new SyncScope(prev);
    }

    /// <summary>Restores the original context, only if this scope installed one.</summary>
    public void Dispose()
    {
        if (_set)
            SynchronizationContext.SetSynchronizationContext(_prev);
    }
}
