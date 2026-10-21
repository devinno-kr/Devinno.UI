namespace Devinno.UI;

/// <summary>
/// Input kind of an edit session (<c>keyboard-type</c> of <c>DvInput</c>; for grid input cells it is determined by the property type).
/// Determines the virtual keyboard layout and the masking and input character restrictions that hosts using a native editor should apply.
/// </summary>
/// <remarks>Hosts read it from <see cref="DvEditState.Kind"/> and apply it to the editor on a new session (when <see cref="DvEditState.SessionId"/> changes).</remarks>
public enum DvEditKind
{
    /// <summary>Plain text. The virtual keyboard opens in the English layout (the Korean layout when the screen's <see cref="Localization.DvStrings.Language"/> is <c>ko</c>) and can always switch Korean/English and symbols.</summary>
    Text,
    /// <summary>
    /// Number. The virtual keyboard opens as a numeric keypad. Whether integer/real and sign are allowed, and the range, are in <see cref="DvEditState.Numeric"/>.
    /// It is convenient for a native editor to filter input to digits, sign and decimal point only, but final validation is done by the core at commit.
    /// </summary>
    Numeric,
    /// <summary>
    /// Password. Input characters must be masked (enable masking in a native editor). The virtual keyboard opens in the English layout.
    /// Editing starts with an empty value, and committing an empty value does not change the VM.
    /// </summary>
    Password
}
