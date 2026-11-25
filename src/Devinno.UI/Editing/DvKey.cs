namespace Devinno.UI;

/// <summary>
/// Physical keyboard editing keys that the host passes with <see cref="DvSurface.KeyInput"/> in virtual keyboard mode.
/// Character input is passed with <see cref="DvSurface.TextInput"/>, not with this enum.
/// </summary>
/// <remarks>Not used in native editor mode (<see cref="DvSurface.UseNativeEditor"/> = <see langword="true"/>).</remarks>
public enum DvKey
{
    /// <summary>Enter (including numeric keypad Enter). Same as the virtual keyboard's Done key (commit).</summary>
    Enter,
    /// <summary>Backspace. Deletes the last character (the last jamo while composing Korean with the Korean IME).</summary>
    Backspace,
    /// <summary>Esc. Cancels editing without writing to the VM, or writes the typed text for a <c>DvInput</c> with <c>escape="commit"</c>.</summary>
    Escape,
    /// <summary>Tab. Commits with the <see cref="DvEditExit.Tab"/> reason and edits the next field.</summary>
    Tab,
    /// <summary>Shift+Tab. Commits with the <see cref="DvEditExit.ShiftTab"/> reason and edits the previous field.</summary>
    ShiftTab
}
