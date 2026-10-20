using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Edit target for a field inside a popup: hour/minute/second in the time popup (numeric keypad), #RRGGBB in the color popup, and the text/range fields in the filter popup. Values are written only to the popup state (the VM is written when the popup is confirmed).</summary>
internal sealed class PopupFieldTarget : IEditTarget
{
    private readonly DvSurface _w;

    /// <param name="window">The window surface that owns the popup stack.</param>
    /// <param name="popup">The popup the field belongs to.</param>
    /// <param name="field">Time popup: 0 hour, 1 minute, 2 second. Filter popup: 0 text, 1 minimum, 2 maximum. Ignored for the color popup (hex).</param>
    public PopupFieldTarget(DvSurface window, IPopup popup, int field)
    {
        _w = window;
        Popup = popup;
        Field = field;
        InitialText = popup switch
        {
            DateTimePopup t => t.FieldValue(field).ToString("D2"),
            ColorPopup c => PickerValue.Hex(c.State.Rgb),
            GridFilterPopup f => field == 0 ? f.TextValue : field == 1 ? f.MinText : f.MaxText,
            _ => ""
        };
    }

    public IPopup Popup { get; }
    public int Field { get; }
    public string InitialText { get; }
    public bool RebuildAfterWrite => false;
    public bool Multiline => false;
    public string? Placeholder => null;
    private bool IsTime => Popup is DateTimePopup;
    private bool IsRange => Popup is GridFilterPopup { Kind: GridFilterKind.Range } && Field > 0;
    public DvEditKind Kind => IsRange || IsTime ? DvEditKind.Numeric : DvEditKind.Text;

    public DvNumericSpec? Numeric => Popup switch
    {
        DateTimePopup => new DvNumericSpec(IsInteger: true, IsSigned: false, Min: 0, Max: Field == 0 ? 23 : 59, Unit: null),
        GridFilterPopup f when IsRange => NumericEdit.Spec(f.Column.Reader.ValueType, null, null, null),
        _ => null
    };

    public bool TryLocate(out SKRect rect, out SKRect clip)
    {
        rect = clip = SKRect.Empty;
        if (!_w.IsPopupOpen(Popup))
            return false;
        rect = Popup switch
        {
            DateTimePopup t => t.FieldRect(Field),
            ColorPopup c => c.HexRect,
            GridFilterPopup f => f.FieldRect(Field),
            _ => SKRect.Empty
        };
        clip = rect;
        return !rect.IsEmpty;
    }

    public EditLook Look(string sample)
    {
        var st = Popup.Slot.Style;
        var family = TextFont.ResolveTypeface(sample, st.FontFamily).FamilyName;
        var text = _w.Token("text", new SKColor(0x0f, 0x17, 0x2a));
        var surface = _w.Token("surface", SKColors.White);
        return IsTime
            ? new EditLook(family, st.FontSize, false, text, surface, SKRect.Empty, TextAlign.Center)
            : new EditLook(family, st.FontSize, false, text, surface, new SKRect(12, 0, 0, 0), TextAlign.Left);
    }

    public bool TryWrite(string text, out string? error)
    {
        error = null;
        switch (Popup)
        {
            case DateTimePopup t:
                if (!NumericEdit.TryParse(text, typeof(int), Numeric!, _w.Strings, out var v, out _, out error))
                    return false;
                var n = (int)v!;
                switch (Field)
                {
                    case 0: t.State.SetHour(n); break;
                    case 1: t.State.SetMinute(n); break;
                    default: t.State.SetSecond(n); break;
                }
                return true;
            case ColorPopup c:
                if (c.State.TrySetHex(text))
                    return true;
                error = DvStrings.Text(_w.Strings, "dv.picker.hex", "#RRGGBB 형식이 아닙니다");
                return false;
            case GridFilterPopup f:
                if (Field == 0)
                {
                    f.TextValue = text;
                    return true;
                }

                // 빈 칸 = 그 쪽 경계 없음, 그 외는 열 값 타입 숫자여야 함
                if (text.Trim().Length > 0 && !NumericEdit.TryParse(text, f.Column.Reader.ValueType, Numeric!, _w.Strings, out _, out _, out error))
                    return false;
                if (Field == 1)
                    f.MinText = text.Trim();
                else
                    f.MaxText = text.Trim();
                return true;
            default:
                return true;
        }
    }

    public void Show(string? text, bool hideText, string? error)
    {
        if (Popup is DateTimePopup t)
            t.FieldEdit = (Field, hideText ? "" : text, hideText, error);
        else if (Popup is ColorPopup c)
            c.HexEdit = (text, hideText, error);
        else if (Popup is GridFilterPopup f)
            f.FieldEdit = (Field, hideText ? "" : text, hideText, error);
    }

    public void Ended()
    {
        if (Popup is DateTimePopup t)
            t.FieldEdit = (-1, null, false, null);
        else if (Popup is ColorPopup c)
            c.HexEdit = default;
        else if (Popup is GridFilterPopup f)
            f.FieldEdit = (-1, null, false, null);
    }

    public bool SameAs(IEditTarget other) => other is PopupFieldTarget o && ReferenceEquals(o.Popup, Popup) && o.Field == Field;

    // 팝업이 자기 앵커를 옮긴다(앵커가 사라지면 ClosePopup 이 칸 편집도 끝낸다) — 칸 대상은 그대로
    public bool Remap(LayoutBox old, LayoutBox now) => true;

    public void Reveal() { }

    // 팝업이 열려 있는 동안 누름은 팝업 스택이 먼저 가져간다 — 본문 누름 경로에 오지 않는다
    public bool Hits(LayoutBox? hitBox, SKPoint p) => true;

    public IEditTarget? Next(DvEditExit exit) => null; // 연속 입력 없음
}
