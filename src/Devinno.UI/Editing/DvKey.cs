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
    /// <summary>Esc. Cancels editing (does not write to the VM).</summary>
    Escape,
    /// <summary>Tab. Commits with the <see cref="DvEditExit.Tab"/> reason.</summary>
    Tab,
    /// <summary>Shift+Tab. Commits with the <see cref="DvEditExit.ShiftTab"/> reason.</summary>
    ShiftTab
}
