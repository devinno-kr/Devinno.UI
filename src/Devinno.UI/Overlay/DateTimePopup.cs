using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Date/time popup — month navigation, weekdays, 6-week calendar, time fields, Cancel/OK. State is in <see cref="DateTimePickerState"/>.</summary>
internal sealed class DateTimePopup : IPopup
{
    public const float Width = 320f, TimeOnlyWidth = 240f, Pad = 8f, RowGap = 8f,
        MonthRow = 40f, DowRow = 24f, DayRow = 40f, FieldW = 64f, FieldH = 40f, ButtonH = 40f;

    // 누를 수 있는 요소 id: -1 없음, 0..41 날짜, 100 이전, 101 다음, 200+i 칸, 300 취소, 301 확인
    private const int Prev = 100, Next = 101, Field0 = 200, Cancel = 300, Ok = 301;
    private int _down = -1;

    public DateTimePopup(DvSurface owner, IValueSlot slot, DateTimePickerState state, DateTime original, bool hadValue)
    {
        Owner = owner;
        Slot = slot;
        State = state;
        Original = original;
        HadValue = hadValue;
    }

    public DvSurface Owner { get; }
    public IValueSlot Slot { get; }
    public SKRect Bounds { get; private set; }
    public DateTimePickerState State { get; }
    public DateTime Original { get; }
    public bool HadValue { get; }
    public int PendingField { get; private set; } = -1;
    public int FieldCount => State.Mode == PickerMode.Date ? 0 : State.Seconds ? 3 : 2;
    private bool HasCalendar => State.Mode != PickerMode.Time;

    public static SKSize SizeFor(PickerMode mode, bool seconds = false)
    {
        var w = mode == PickerMode.Time ? TimeOnlyWidth : Width;
        var h = Pad + ButtonH + Pad;
        if (mode != PickerMode.Time)
            h += MonthRow + DowRow + 6 * DayRow + RowGap;
        if (mode != PickerMode.Date)
            h += FieldH + RowGap;
        return new SKSize(w, h);
    }

    public void Place(SKRect anchor, float screenW, float screenH)
    {
        var size = SizeFor(State.Mode, State.Seconds);
        Bounds = PopupPlacement.Fixed(anchor, size.Width, size.Height, screenW, screenH);
    }

    private float Inner => Bounds.Width - 2 * Pad;
    private float CalTop => Bounds.Top + Pad;
    private float DaysTop => CalTop + MonthRow + DowRow;
    private float FieldsTop => HasCalendar ? DaysTop + 6 * DayRow + RowGap : Bounds.Top + Pad;

    public SKRect PrevRect => SKRect.Create(Bounds.Left + Pad, CalTop, 40, MonthRow);
    public SKRect NextRect => SKRect.Create(Bounds.Right - Pad - 40, CalTop, 40, MonthRow);
    public SKRect TitleRect => new(PrevRect.Right, CalTop, NextRect.Left, CalTop + MonthRow);
    public SKRect DowRect(int col) => SKRect.Create(Bounds.Left + Pad + col * Inner / 7f, CalTop + MonthRow, Inner / 7f, DowRow);
    public SKRect DayRect(int index) => SKRect.Create(Bounds.Left + Pad + index % 7 * Inner / 7f, DaysTop + index / 7 * DayRow, Inner / 7f, DayRow);

    public SKRect FieldRect(int i)
    {
        const float colon = 16f;
        var total = FieldCount * FieldW + (FieldCount - 1) * colon;
        var left = Bounds.MidX - total / 2f + i * (FieldW + colon);
        return SKRect.Create(left, FieldsTop, FieldW, FieldH);
    }

    public SKRect CancelRect => SKRect.Create(Bounds.Left + Pad, Bounds.Bottom - Pad - ButtonH, (Inner - Pad) / 2f, ButtonH);
    public SKRect OkRect => SKRect.Create(CancelRect.Right + Pad, CancelRect.Top, (Inner - Pad) / 2f, ButtonH);

    private int HitId(SKPoint p)
    {
        if (CancelRect.Contains(p.X, p.Y)) return Cancel;
        if (OkRect.Contains(p.X, p.Y)) return Ok;
        for (var i = 0; i < FieldCount; i++)
            if (FieldRect(i).Contains(p.X, p.Y)) return Field0 + i;
        if (!HasCalendar) return -1;
        if (PrevRect.Contains(p.X, p.Y)) return Prev;
        if (NextRect.Contains(p.X, p.Y)) return Next;
        for (var i = 0; i < 42; i++)
            if (DayRect(i).Contains(p.X, p.Y)) return State.CellDate(i) is null ? -1 : i; // 범위 밖 칸은 빈칸
        return -1;
    }

    public void Down(SKPoint p) => _down = HitId(p);
    public void Move(SKPoint p) { }
    public void Wheel(float delta) { }

    public PopupResult Up(SKPoint p)
    {
        var id = HitId(p);
        var down = _down;
        _down = -1;
        if (id < 0 || id != down)
            return PopupResult.None;

        switch (id)
        {
            case Cancel: return PopupResult.Cancel;
            case Ok: return PopupResult.Commit;
            case Prev: State.PrevMonth(); return PopupResult.None;
            case Next: State.NextMonth(); return PopupResult.None;
            case >= Field0:
                PendingField = id - Field0;
                return PopupResult.EditField;
            default:
                State.TapDay(State.CellDate(id)!.Value);
                return PopupResult.None;
        }
    }

    public int FieldValue(int i) => i switch { 0 => State.Hour, 1 => State.Minute, _ => State.Second };

    /// <summary>Display of the field being edited (virtual keyboard text, Hide if native). Filled by the field edit target.</summary>
    public (int Field, string? Text, bool Hide, string? Error) FieldEdit { get; set; } = (-1, null, false, null);

    public void Paint(SKCanvas canvas, DvTheme? theme, DvStrings? strings)
        => Painting.Painter.PaintDateTimePopup(canvas, this, theme, strings, Slot.Style.FontSize);
}
