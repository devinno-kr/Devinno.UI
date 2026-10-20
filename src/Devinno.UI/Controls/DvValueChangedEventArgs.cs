namespace Devinno.UI.Controls;

/// <summary>Data for the change events of value handles (<c>CheckedChanged</c>, <c>ValueChanged</c>, <c>TextChanged</c>, <c>SelectionChanged</c>).</summary>
public sealed class DvValueChangedEventArgs : EventArgs
{
    internal DvValueChangedEventArgs(bool byUser) => ByUser = byUser;

    /// <summary>
    /// <see langword="true"/> if the user changed it on screen. <see langword="false"/> if code wrote the handle value, or if a change to a bound view model property was noticed when the screen was rebuilt.
    /// </summary>
    public bool ByUser { get; }
}
