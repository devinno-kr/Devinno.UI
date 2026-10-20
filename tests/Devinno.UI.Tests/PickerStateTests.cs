using Devinno.UI;
using SkiaSharp;
using Xunit;

// P8d-3 시간 선택 상태·값 변환
public class PickerStateTests
{
    [Fact]
    public void Grid_starts_on_sunday_of_first_week()
    {
        var s = new DateTimePickerState(new DateTime(2026, 10, 14), PickerMode.Date, false); // 2026-10-01 = 목
        Assert.Equal(new DateOnly(2026, 9, 27), s.CellDate(0));
        Assert.Equal(new DateOnly(2026, 11, 7), s.CellDate(41));
    }

    [Fact]
    public void Month_starting_on_sunday_has_no_previous_week()
    {
        var s = new DateTimePickerState(new DateTime(2026, 2, 10), PickerMode.Date, false); // 2026-02-01 = 일
        Assert.Equal(new DateOnly(2026, 2, 1), s.CellDate(0));
    }

    [Fact]
    public void Leap_february_has_29_days()
    {
        var s = new DateTimePickerState(new DateTime(2028, 2, 1), PickerMode.Date, false);
        Assert.Contains(Enumerable.Range(0, 42).Select(s.CellDate), d => d == new DateOnly(2028, 2, 29));
        Assert.True(s.InViewMonth(new DateOnly(2028, 2, 29)));
        Assert.False(s.InViewMonth(new DateOnly(2028, 3, 1)));
    }

    [Fact]
    public void Opens_on_value_month()
    {
        var s = new DateTimePickerState(new DateTime(2024, 3, 5), PickerMode.Date, false);
        Assert.Equal((2024, 3), (s.ViewYear, s.ViewMonth));
    }

    [Fact]
    public void Tapping_other_month_day_selects_and_moves()
    {
        var s = new DateTimePickerState(new DateTime(2026, 10, 14), PickerMode.Date, false);
        s.TapDay(new DateOnly(2026, 9, 28));
        Assert.Equal(new DateOnly(2026, 9, 28), s.Selected);
        Assert.Equal((2026, 9), (s.ViewYear, s.ViewMonth));
    }

    [Fact]
    public void Month_navigation_keeps_selection_and_wraps_year()
    {
        var s = new DateTimePickerState(new DateTime(2026, 12, 31), PickerMode.Date, false);
        s.NextMonth();
        Assert.Equal((2027, 1), (s.ViewYear, s.ViewMonth));
        Assert.Equal(new DateOnly(2026, 12, 31), s.Selected);
        s.PrevMonth(); s.PrevMonth();
        Assert.Equal((2026, 11), (s.ViewYear, s.ViewMonth));
    }

    [Fact]
    public void Max_month_view_is_safe()
    {
        var s = new DateTimePickerState(DateTime.MaxValue, PickerMode.DateTime, false); // 9999-12-01 = 수
        Assert.Equal(new DateOnly(9999, 11, 28), s.CellDate(0));
        Assert.Equal(new DateOnly(9999, 12, 31), s.CellDate(33));
        Assert.Null(s.CellDate(34));
        Assert.Null(s.CellDate(41));
        s.NextMonth();
        Assert.Equal((9999, 12), (s.ViewYear, s.ViewMonth));
        s.PrevMonth();
        Assert.Equal((9999, 11), (s.ViewYear, s.ViewMonth));
    }

    [Fact]
    public void Min_month_view_is_safe()
    {
        var s = new DateTimePickerState(DateTime.MinValue, PickerMode.Date, false); // 0001-01-01 = 월
        Assert.Null(s.CellDate(0));
        Assert.Equal(new DateOnly(1, 1, 1), s.CellDate(1));
        Assert.Equal(new DateOnly(1, 2, 10), s.CellDate(41));
        s.PrevMonth();
        Assert.Equal((1, 1), (s.ViewYear, s.ViewMonth));
        s.NextMonth();
        Assert.Equal((1, 2), (s.ViewYear, s.ViewMonth));
    }

    [Fact]
    public void Date_mode_keeps_time_and_kind()
    {
        var original = new DateTime(2026, 10, 14, 9, 30, 15, DateTimeKind.Local);
        var s = new DateTimePickerState(original, PickerMode.Date, false);
        s.TapDay(new DateOnly(2026, 10, 20));
        var r = s.Result(original);
        Assert.Equal(new DateTime(2026, 10, 20, 9, 30, 15), r);
        Assert.Equal(DateTimeKind.Local, r.Kind);
    }

    [Fact]
    public void Time_mode_keeps_date()
    {
        var original = new DateTime(2026, 10, 14, 9, 30, 0);
        var s = new DateTimePickerState(original, PickerMode.Time, false);
        s.SetHour(22); s.SetMinute(5);
        Assert.Equal(new DateTime(2026, 10, 14, 22, 5, 0), s.Result(original));
    }

    [Fact]
    public void Seconds_hidden_keeps_original_seconds()
    {
        var original = new DateTime(2026, 10, 14, 9, 30, 42);
        var s = new DateTimePickerState(original, PickerMode.DateTime, seconds: false);
        s.SetMinute(31);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 31, 42), s.Result(original));
        var s2 = new DateTimePickerState(original, PickerMode.DateTime, seconds: true);
        s2.SetSecond(7);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 7), s2.Result(original));
    }

    [Fact]
    public void Result_keeps_sub_second_ticks()
    {
        var original = new DateTime(2026, 10, 14, 9, 30, 15, 123).AddTicks(4567);
        var frac = original.Ticks % TimeSpan.TicksPerSecond;
        var dt = new DateTimePickerState(original, PickerMode.DateTime, seconds: false);
        dt.SetMinute(31);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 31, 15).AddTicks(frac), dt.Result(original));
        var dts = new DateTimePickerState(original, PickerMode.DateTime, seconds: true);
        Assert.Equal(original, dts.Result(original)); // 안 바꾸면 그대로
        var t = new DateTimePickerState(original, PickerMode.Time, seconds: true);
        t.SetMinute(31);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 31, 15).AddTicks(frac), t.Result(original));
    }

    [Theory]
    [InlineData(typeof(DateOnly), "Date")]
    [InlineData(typeof(DateOnly?), "Date")]
    [InlineData(typeof(TimeOnly), "Time")]
    [InlineData(typeof(DateTime), "DateTime")]
    [InlineData(typeof(DateTime?), "DateTime")]
    public void Default_mode_from_type(Type t, string expected)
        => Assert.Equal(expected, PickerValue.DefaultMode(t).ToString());

    [Fact]
    public void Mismatched_mode_falls_back_to_type_default()
    {
        Assert.Equal(PickerMode.Date, PickerValue.ResolveMode("time", typeof(DateOnly)));
        Assert.Equal(PickerMode.Time, PickerValue.ResolveMode("date", typeof(TimeOnly)));
        Assert.Equal(PickerMode.Date, PickerValue.ResolveMode("date", typeof(DateTime)));
        Assert.Equal(PickerMode.DateTime, PickerValue.ResolveMode("bogus", typeof(DateTime)));
    }

    [Fact]
    public void Converts_between_bound_types()
    {
        var dt = new DateTime(2026, 10, 14, 9, 30, 15);
        Assert.Equal(new DateOnly(2026, 10, 14), PickerValue.FromDateTime(dt, typeof(DateOnly?)));
        Assert.Equal(new TimeOnly(9, 30, 15), PickerValue.FromDateTime(dt, typeof(TimeOnly)));
        Assert.Equal(dt, PickerValue.FromDateTime(dt, typeof(DateTime)));
        Assert.True(PickerValue.TryToDateTime(new TimeOnly(9, 30), out var t));
        Assert.Equal(new TimeSpan(9, 30, 0), t.TimeOfDay);
        Assert.False(PickerValue.TryToDateTime(null, out _));
    }

    [Fact]
    public void Display_uses_mode_default_or_format()
    {
        var dt = new DateTime(2026, 10, 14, 9, 5, 7);
        Assert.Equal("2026-10-14 09:05", PickerValue.Display(dt, PickerMode.DateTime, false, null));
        Assert.Equal("09:05:07", PickerValue.Display(new TimeOnly(9, 5, 7), PickerMode.Time, true, null));
        Assert.Equal("2026.10.14", PickerValue.Display(dt, PickerMode.Date, false, "yyyy.MM.dd"));
        Assert.Equal("", PickerValue.Display(null, PickerMode.Date, false, null));
    }

    [Fact]
    public void Display_invalid_format_falls_back_to_default()
    {
        var dt = new DateTime(2026, 10, 14, 9, 5, 7);
        Assert.Equal("2026-10-14 09:05", PickerValue.Display(dt, PickerMode.DateTime, false, "%"));
        Assert.Equal("09:05:07", PickerValue.Display(dt, PickerMode.Time, true, "%")); // 두 번째도 같은 결과(경고는 한 번)
    }

    [Fact]
    public void Color_values_round_trip_and_keep_alpha()
    {
        Assert.True(PickerValue.TryToColor("#3b82f6", out var c));
        Assert.Equal(new SKColor(0x3b, 0x82, 0xf6), c);
        Assert.Equal("#FF0000", PickerValue.FromColor(new SKColor(255, 0, 0), "#00ff00", typeof(string)));
        Assert.Equal(new SKColor(255, 0, 0, 0x80), PickerValue.FromColor(new SKColor(255, 0, 0), new SKColor(0, 0, 0, 0x80), typeof(SKColor)));
        Assert.Equal(new SKColor(255, 0, 0, 255), PickerValue.FromColor(new SKColor(255, 0, 0), null, typeof(SKColor?)));
    }
}
