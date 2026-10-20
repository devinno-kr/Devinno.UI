using SkiaSharp;

namespace Devinno.UI;

/// <summary>
/// The party that decides the slot composition of the root screen (DvApp). Called by the root <see cref="DvSurface.Layout(float, float)"/> on the input thread.
/// </summary>
internal interface ISurfaceComposer
{
    /// <summary>Right before layout — applies pending requests (page transition, areas, global settings).</summary>
    void BeforeLayout();

    /// <summary>
    /// Whether there are requests not yet applied (readable from any thread). Added to the root <see cref="DvSurface.NeedsRender"/> —
    /// so that the next frame is not missed even if a request arrives after the <see cref="BeforeLayout"/> snapshot and that layout clears dirty.
    /// </summary>
    bool HasPending { get; }

    /// <summary>Input (pointer, wheel, key, edit) arrived at the root screen — the idle detector restarts counting. On the input thread.</summary>
    void NoteInput();

    /// <summary>Next time to wake up (idle notification), or null if none. Added to the root <see cref="DvSurface.NextAnimationDue"/>.</summary>
    long? NextDue(long nowMs);

    /// <summary>
    /// Slots for window size <paramref name="w"/>×<paramref name="h"/> (window-relative rect + the surface in that place + whether it is a modal layer).
    /// Non-modal slots (areas, page) do not overlap each other. Modal slots (dialogs, and the lock layer above them) come last in bottom-to-top order and are drawn over the slots below.
    /// </summary>
    IReadOnlyList<(SKRect Rect, DvSurface Child, bool Modal)> Compose(float w, float h);

    /// <summary>
    /// Whether pointer and wheel input to the lower slots (non-modal slots and modal slots other than the topmost) must be blocked — includes dialogs that are requested but not yet applied (readable from any thread).
    /// Blocking from the moment of the request prevents a double tap from running the same handler twice.
    /// </summary>
    bool BlocksBelow { get; }

    /// <summary>
    /// Surface that receives pointer and wheel input while blocked — the lock dialog when locked; otherwise the surface of the topmost dialog on the request stack if it has already been created; otherwise <see langword="null"/> (swallows everything).
    /// It is null while the topmost is only requested, or after a close request that is not yet applied, so the dialog below does not receive input in between either.
    /// </summary>
    DvSurface? ModalInput { get; }

    /// <summary>Surface of the current page slot (null if none) — the root uses it to notice that the page changed and draws the transition animation.</summary>
    DvSurface? PageSurface { get; }

    /// <summary>Takes the animation of the just-applied page transition once (taking it resets it to None).</summary>
    App.DvPageTransition TakePageTransition();
}
