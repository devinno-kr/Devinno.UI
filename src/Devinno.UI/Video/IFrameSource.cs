using SkiaSharp;

namespace Devinno.UI.Video;

/// <summary>
/// A source of changing pictures (video frames, camera images) that a <c>&lt;DvVideo source="{…}"/&gt;</c> element shows.
/// Use <see cref="FrameSource"/> (publish frames from any thread) or <see cref="VideoPlayer"/> (plays a video file) instead of implementing this yourself.
/// </summary>
/// <remarks>
/// <para><b>How the screen uses it</b>: while <see cref="IsLive"/> is <see langword="true"/>, the screen checks <see cref="FrameVersion"/> about every 16 ms and, when it changed,
/// repaints only the video element and calls <see cref="UseFrame"/> on the UI thread. Nothing else on the screen is repainted.</para>
/// <para><b>Threads</b>: <see cref="FrameVersion"/> and <see cref="IsLive"/> are read on the UI thread while frames may be produced on another thread, so implementations must be thread-safe.</para>
/// </remarks>
public interface IFrameSource
{
    /// <summary>A number that changes every time a new frame is available. The screen repaints the element when it differs from the frame it painted last.</summary>
    long FrameVersion { get; }

    /// <summary>
    /// Whether new frames may still arrive (playing, streaming). While <see langword="true"/>, the screen keeps checking <see cref="FrameVersion"/>;
    /// while <see langword="false"/> (paused, stopped), it does not, so a frame published then appears only after <c>Repaint</c> or <c>Invalidate</c>.
    /// </summary>
    bool IsLive { get; }

    /// <summary>
    /// Calls <paramref name="use"/> with the current frame, keeping the frame alive until it returns. The image belongs to the source: do not dispose it or keep it after the call.
    /// </summary>
    /// <param name="use">Code that paints the frame. Called on the caller's thread, at most once.</param>
    /// <returns><see langword="true"/> if there was a frame and <paramref name="use"/> was called; <see langword="false"/> if there is no frame yet.</returns>
    bool UseFrame(Action<SKImage> use);
}
