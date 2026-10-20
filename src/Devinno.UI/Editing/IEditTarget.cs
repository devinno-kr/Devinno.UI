using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Font, color and padding used to display the editor.</summary>
internal sealed record EditLook(string FontFamily, float FontSize, bool Bold, SKColor TextColor, SKColor Background,
    SKRect Padding, TextAlign Align);

/// <summary>Target of an edit session: a DvInput box (<see cref="BoxEditTarget"/>) or a cell inside a popup.</summary>
internal interface IEditTarget
{
    /// <summary>The edit target's rectangle and clip (in this surface's coordinates). Returns false if the target is gone.</summary>
    bool TryLocate(out SKRect rect, out SKRect clip);
    string InitialText { get; }
    DvEditKind Kind { get; }
    bool Multiline { get; }
    string? Placeholder { get; }
    DvNumericSpec? Numeric { get; }
    EditLook Look(string sample);
    /// <returns>True if written or nothing needed to be written; false if rejected (with a reason).</returns>
    bool TryWrite(string text, out string? error);
    /// <summary>Editing display (text in virtual keyboard mode; hideText = true in native mode).</summary>
    void Show(string? text, bool hideText, string? error);
    /// <summary>Session end (commit, cancel or disposal): clears the display state.</summary>
    void Ended();
    /// <summary>True if a rebuild is needed after writing (when the value was written to the VM).</summary>
    bool RebuildAfterWrite { get; }
    /// <summary>Whether this is the same edit target (pressing the same target again = keep the session).</summary>
    bool SameAs(IEditTarget other);
    /// <summary>After a rebuild, moves to the same target in the new tree. False = gone (the surface commits the entered value and drops the session).</summary>
    bool Remap(LayoutBox old, LayoutBox now);
    /// <summary>Scrolls so the target is fully visible when editing starts.</summary>
    void Reveal();
    /// <summary>Whether a press is over this target. <paramref name="p"/> = in the same coordinate system as the <paramref name="hitBox"/> Rect. If false, the surface commits with FocusLost before handling that press.</summary>
    bool Hits(LayoutBox? hitBox, SKPoint p);
    /// <summary>Continuous input: the target to edit next after a successful commit (used by the surface only in native editor mode). Null if none (editing ends).</summary>
    IEditTarget? Next(DvEditExit exit);
}
