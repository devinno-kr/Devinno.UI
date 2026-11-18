using SkiaSharp;

namespace Devinno.UI;

/// <summary>A pointer button, as passed to <see cref="DvSurface.PointerDown"/> and <see cref="DvSurface.PointerUp"/> and reported in <see cref="DvPointerArgs.Button"/>.</summary>
public enum DvPointerButton
{
    /// <summary>No button: a move without a press (hover) or a wheel turn.</summary>
    None,
    /// <summary>The left mouse button, or a touch.</summary>
    Left,
    /// <summary>The right mouse button.</summary>
    Right,
    /// <summary>The middle mouse button (wheel press).</summary>
    Middle,
}

/// <summary>Modifier keys held during a pointer event. Values can be combined.</summary>
[Flags]
public enum DvModifiers
{
    /// <summary>No modifier key.</summary>
    None = 0,
    /// <summary>Shift.</summary>
    Shift = 1,
    /// <summary>Ctrl (Control).</summary>
    Ctrl = 2,
    /// <summary>Alt.</summary>
    Alt = 4,
}

/// <summary>What a custom widget (<see cref="Widgets.DvWidget"/>) receives with a pointer event.</summary>
/// <param name="Point">The pointer position, in the same coordinates as <see cref="Widgets.DvWidgetContext.Bounds"/>.</param>
/// <param name="Button">The button of the press or gesture; <see cref="DvPointerButton.None"/> for hover and wheel.</param>
/// <param name="Modifiers">The modifier keys the host reported with this event.</param>
/// <param name="ClickCount">For a press, its release and the moves in between: 1 for a single press, 2 for a double press, 3 for a triple …
/// (presses of the same button within 500 ms and 4 px of the previous one). 0 for hover and wheel.</param>
public readonly record struct DvPointerArgs(SKPoint Point, DvPointerButton Button, DvModifiers Modifiers, int ClickCount);
