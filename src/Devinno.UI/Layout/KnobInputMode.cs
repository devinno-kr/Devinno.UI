namespace Devinno.UI.Layout;

/// <summary><c>input-mode</c> of <c>DvKnob</c>: drag to turn, tap for keypad, or both.</summary>
internal enum KnobInputMode
{
    /// <summary>Drag to turn only (default).</summary>
    Drag,

    /// <summary>Tap opens the numeric keypad (no turning).</summary>
    Tap,

    /// <summary>Dragging turns; a tap without movement opens the numeric keypad.</summary>
    Both,
}
