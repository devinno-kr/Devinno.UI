using SkiaSharp;

namespace Devinno.UI.Video;

/// <summary>
/// A ready-made <see cref="IFrameSource"/>: publish pictures from any thread with <see cref="Publish"/>, and a <c>&lt;DvVideo source="{…}"/&gt;</c> element shows the latest one.
/// </summary>
/// <remarks>
/// <para>Only the latest frame is kept. Frames published faster than the screen paints are dropped (the screen always shows the newest), so a fast producer never builds up a queue.</para>
/// <para>All members can be called from any thread.</para>
/// </remarks>
/// <example>
/// <code>
/// // view model
/// public FrameSource Preview { get; } = new();
///
/// // camera or decoder thread
/// Preview.Publish(SKImage.FromPixelCopy(info, pixels, rowBytes));
///
/// // markup: &lt;DvVideo source="{Preview}" fit="cover"/&gt;
/// </code>
/// </example>
public sealed class FrameSource : IFrameSource, IDisposable
{
    private readonly object _gate = new();
    private SKImage? _frame;
    private long _version;
    private volatile bool _live = true;

    /// <inheritdoc/>
    public long FrameVersion => Interlocked.Read(ref _version);

    /// <summary>Whether new frames may still arrive. Default <see langword="true"/>. Set it to <see langword="false"/> when the producer stops so the screen stops checking for frames.</summary>
    public bool IsLive
    {
        get => _live;
        set => _live = value;
    }

    /// <summary>Makes <paramref name="frame"/> the current frame. The source takes ownership: it disposes the frame when a newer one replaces it, on <see cref="Clear"/> or on <see cref="Dispose"/>.</summary>
    /// <param name="frame">The new frame. Do not use or dispose it after this call.</param>
    /// <exception cref="ArgumentNullException"><paramref name="frame"/> is <see langword="null"/>.</exception>
    public void Publish(SKImage frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        SKImage? old;
        lock (_gate)
        {
            old = _frame;
            _frame = frame;
            Interlocked.Increment(ref _version);
        }
        old?.Dispose(); // 그리는 중이면 잠금 때문에 여기까지 오지 않는다 — 다 그린 뒤 해제
    }

    /// <summary>Removes the current frame (the element then shows only its background) and disposes it.</summary>
    public void Clear()
    {
        SKImage? old;
        lock (_gate)
        {
            old = _frame;
            _frame = null;
            Interlocked.Increment(ref _version);
        }
        old?.Dispose();
    }

    /// <inheritdoc/>
    public bool UseFrame(Action<SKImage> use)
    {
        ArgumentNullException.ThrowIfNull(use);
        lock (_gate)
        {
            if (_frame is null)
                return false;
            use(_frame);
            return true;
        }
    }

    /// <summary>Disposes the current frame and stops the source (<see cref="IsLive"/> = <see langword="false"/>).</summary>
    public void Dispose()
    {
        _live = false;
        Clear();
    }
}
