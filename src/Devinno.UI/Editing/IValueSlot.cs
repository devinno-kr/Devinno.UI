using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Result of a slot write. Failed = the write failed, e.g. the setter threw (handled like a rejection, but input cells show different text).</summary>
internal enum CommitResult { Same, Written, Rejected, Failed }

/// <summary>
/// The place where a popup writes the edited value: an existing widget box (<see cref="BoxSlot"/>) or a grid cell (GridCellSlot).
/// The popup reads the anchor position, current value, items and picker settings from here, and calls <see cref="Write"/> on confirm.
/// </summary>
internal interface IValueSlot
{
    DvSurface Owner { get; }
    /// <summary>The rectangle and visible part in Owner surface coordinates (clip = Empty if fully hidden). False if it has disappeared.</summary>
    bool TryLocate(out SKRect rect, out SKRect clip);
    object? Value { get; }
    /// <summary>Type of the bound property (formerly SelectValueType/PickerValueType).</summary>
    Type? ValueType { get; }
    /// <summary>Popup font (size and font name).</summary>
    ComputedStyle Style { get; }
    IReadOnlyList<SelectOption> Options { get; }
    int SelectedIndex { get; }
    PickerMode PickerMode { get; }
    bool PickerSeconds { get; }
    /// <summary>Open indicator (focus ring, highlight).</summary>
    bool Editing { get; set; }
    /// <summary>Same-value check → gate → write → repaint request.</summary>
    CommitResult Write(object? value);
    /// <summary>Moves to the new tree after a rebuild. False = disappeared (popup closes).</summary>
    bool Remap(LayoutBox old, LayoutBox now);
}
