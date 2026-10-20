using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>
/// Edit target for a grid input cell — an edit session at the cell position (virtual keyboard, native editor). Numeric spec = property type + column min, max, unit;
/// write = slot (one item property write + Repaint, no rebuild).
/// </summary>
internal sealed class GridCellEditTarget : IEditTarget
{
    public GridCellEditTarget(GridCellSlot slot)
    {
        Slot = slot;
        InitialText = InputDisplay.Raw(slot.Value, null, "DvDataGrid");
    }

    public GridCellSlot Slot { get; }
    public string InitialText { get; }
    public bool Multiline => false;
    public string? Placeholder => null;
    public bool RebuildAfterWrite => false; // 슬롯이 Repaint 한다

    // CanEdit 이 string·숫자만 통과시킨다 — string 이 아니면 숫자
    private bool IsNumber => Slot.ValueType is { } t && t != typeof(string);
    public DvEditKind Kind => IsNumber ? DvEditKind.Numeric : DvEditKind.Text;

    public DvNumericSpec? Numeric
        => IsNumber ? NumericEdit.Spec(Slot.ValueType, Slot.Col.Min, Slot.Col.Max, Slot.Col.Unit) : null;

    public bool TryLocate(out SKRect rect, out SKRect clip) => Slot.TryLocate(out rect, out clip);

    public EditLook Look(string sample)
    {
        var st = Slot.Style;
        var family = TextFont.ResolveTypeface(sample, st.FontFamily).FamilyName;
        var text = Slot.GridBox.Grid!.Colors.Text?.ToSK() ?? Slot.Owner.Token("text", new SKColor(0x0f, 0x17, 0x2a));
        const float pad = Painting.Painter.GridCellPad;
        return new EditLook(family, st.FontSize, false, text, Slot.Owner.Token("surface", SKColors.White),
            new SKRect(pad, 0, pad, 0), Slot.Col.EffectiveAlign);
    }

    public bool TryWrite(string text, out string? error)
    {
        error = null;
        if (text == InitialText)
            return true;

        object? value = text;
        if (IsNumber)
        {
            if (!NumericEdit.TryParse(text, Slot.ValueType, Numeric!, Slot.Owner.Strings, out value, out var number, out error))
                return false;
            if (NumericEdit.SameNumber(Slot.Value, number))
                return true; // "07" → 7 처럼 표기만 다름
        }

        switch (Slot.Write(value))
        {
            case CommitResult.Rejected:
                error = DvStrings.Text(Slot.Owner.Strings, "dv.input.rejected", "허용되지 않는 값입니다");
                return false;
            case CommitResult.Failed: // setter 예외 — 예외 메시지는 화면에 내지 않는다(Trace·HandlerError)
                error = DvStrings.Text(Slot.Owner.Strings, "dv.input.write-failed", "값을 저장하지 못했습니다");
                return false;
            default:
                return true;
        }
    }

    public void Show(string? text, bool hideText, string? error) => Slot.ShowEdit(text, hideText, error);

    public void Ended() => Slot.Editing = false;

    public bool SameAs(IEditTarget other) => other is GridCellEditTarget o && Slot.SameCell(o.Slot);

    public bool Remap(LayoutBox old, LayoutBox now) => Slot.Remap(old, now);

    public void Reveal() => Slot.Reveal();

    public bool Hits(LayoutBox? hitBox, SKPoint p) => Slot.Hits(hitBox, p);

    /// <summary>
    /// The next target for continuous entry — Enter = same column in the row below, Tab/ShiftTab = next/previous input column in the same row.
    /// Non-editable and non-input cells are skipped; at the end, null (editing ends). Sorting is not redone (only values change = order kept).
    /// </summary>
    public IEditTarget? Next(DvEditExit exit)
    {
        if (!Slot.TryView(out var view))
            return null;
        var g = Slot.GridBox.Grid!;
        switch (exit)
        {
            case DvEditExit.Enter:
                // 다음 행 = 그리드가 정한 화면 순서(정렬·이후 필터 반영)에서 아래로 보이는 행
                var below = g.NextVisibleItem(view, it => IsInput(g, Slot.Column, it));
                return below is null ? null : Make(below, Slot.Column);
            case DvEditExit.Tab or DvEditExit.ShiftTab:
                var step = exit == DvEditExit.Tab ? 1 : -1;
                for (var c = Slot.Column + step; c >= 0 && c < g.Columns.Length; c += step)
                {
                    if (IsInput(g, c, Slot.Item))
                        return Make(Slot.Item, c);
                }

                return null;
            default:
                return null;
        }

        static bool IsInput(GridSpec g, int c, object item) => g.Columns[c].CellType == GridCellType.Input && g.Columns[c].CanEdit(item);
        GridCellEditTarget Make(object item, int c) => new(new GridCellSlot(Slot.Owner, Slot.GridBox, c, item));
    }
}
