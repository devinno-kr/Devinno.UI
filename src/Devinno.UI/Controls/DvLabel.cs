namespace Devinno.UI.Controls;

/// <summary>Handle for a <c>DvLabel</c> element.</summary>
public sealed class DvLabel : DvControl
{
    internal DvLabel(string id) : base(id) { }

    /// <summary>
    /// Gets or sets the text. Setting it overrides the markup value with the literal text; setting <see langword="null"/> returns to the markup value.
    /// Getting it returns the override value, or if none, the markup/binding value from the last layout (<see langword="null"/> if not attached or not laid out yet).
    /// </summary>
    public string? Text { get => CurrentText; set => SetOverrideText(value); }
}
