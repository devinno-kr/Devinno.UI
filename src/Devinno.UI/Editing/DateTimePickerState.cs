namespace Devinno.UI;

internal enum PickerMode { Date, Time, DateTime }

/// <summary>Temporary state of the date/time popup (not drawn). On open it shows the value's month; on OK, <see cref="Result"/> combines it with the original value.</summary>
internal sealed class DateTimePickerState
{
    public DateTimePickerState(DateTime initial, PickerMode mode, bool seconds)
    {
        Mode = mode;
        Seconds = seconds;
        Selected = DateOnly.FromDateTime(initial);
        (ViewYear, ViewMonth) = (initial.Year, initial.Month);
        (Hour, Minute, Second) = (initial.Hour, initial.Minute, initial.Second);
    }

    public PickerMode Mode { get; }
    public bool Seconds { get; }
    public int ViewYear { get; private set; }
    public int ViewMonth { get; private set; }
    public DateOnly Selected { get; private set; }
    public int Hour { get; private set; }
    public int Minute { get; private set; }
    public int Second { get; private set; }

    /// <summary>Cell date — cells before 0001-01-01 or after 9999-12-31 are null (not drawn and not pressable).</summary>
    public DateOnly? CellDate(int index)
    {
        var first = new DateOnly(ViewYear, ViewMonth, 1);
        var n = first.DayNumber - (int)first.DayOfWeek + index;
        return n < DateOnly.MinValue.DayNumber || n > DateOnly.MaxValue.DayNumber ? null : DateOnly.FromDayNumber(n);
    }

    public bool InViewMonth(DateOnly d) => d.Year == ViewYear && d.Month == ViewMonth;

    public void PrevMonth() => Shift(-1);
    public void NextMonth() => Shift(1);

    private void Shift(int months)
    {
        var m = ViewYear * 12 + ViewMonth - 1 + months;
        if (m < 1 * 12 || m > 9999 * 12 + 11)
            return; // 0001-01 ~ 9999-12 밖은 무시
        (ViewYear, ViewMonth) = (m / 12, m % 12 + 1);
    }

    public void TapDay(DateOnly d)
    {
        Selected = d;
        (ViewYear, ViewMonth) = (d.Year, d.Month);
    }

    public void SetHour(int h) => Hour = h;
    public void SetMinute(int m) => Minute = m;
    public void SetSecond(int s) => Second = s;

    public DateTime Result(DateTime original)
    {
        var date = Mode == PickerMode.Time ? DateOnly.FromDateTime(original) : Selected;
        var time = Mode == PickerMode.Date
            ? TimeOnly.FromDateTime(original)
            : new TimeOnly(Hour, Minute, Seconds ? Second : original.Second).Add(TimeSpan.FromTicks(original.Ticks % TimeSpan.TicksPerSecond)); // 초 미만은 원래 값 유지
        return DateTime.SpecifyKind(date.ToDateTime(time), original.Kind);
    }
}
