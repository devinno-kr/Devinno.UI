namespace Devinno.UI.Editing;

/// <summary>
/// The "now" used by pickers and date filters — for the today marker and the start date of an empty value. Kept in one place so sample PNGs and tests can pin the date.
/// </summary>
internal static class PickerClock
{
    internal static Func<DateTime> Now = () => DateTime.Now;

    internal static DateTime Today => Now().Date;
}
