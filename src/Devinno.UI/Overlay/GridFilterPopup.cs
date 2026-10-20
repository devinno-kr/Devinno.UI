using System.Globalization;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>
/// Grid filter popup — below the filter cell, a body per kind (text input field · values checklist · two fields for range/date) + Cancel, Clear, OK.
/// Edited values are kept only in temporary values; on OK, <see cref="TryBuild"/> builds the condition (pressing outside = cancel).
/// Values items are collected once when opened (the list stays the same even if the data changes while it is open).
/// </summary>
internal sealed class GridFilterPopup : IPopup
{
    public const float Pad = 8f, Row = 40f, ButtonH = 40f, LabelW = 56f, MinFieldW = 160f, ErrorH = 20f;
    private const int MaxVisibleItems = 8;
    private const float DragThreshold = 8f, WheelStep = 48f;

    public GridFilterPopup(DvSurface owner, GridFilterCellSlot slot, DvStrings? strings)
    {
        Owner = owner;
        Slot = slot;
        CellSlot = slot;
        var grid = Grid;
        Column = grid.Columns[slot.Column];
        Kind = Column.FilterKind;
        grid.Filters.TryGetValue(slot.Column, out var cur);
        switch (Kind)
        {
            case GridFilterKind.Text:
                TextValue = cur?.Text ?? "";
                break;
            case GridFilterKind.Values:
                _initial = cur;
                var vals = GridFilter.DistinctValues(grid, slot.Column, GridFilter.MaxItems, out var truncated);
                Truncated = truncated;
                if (truncated)
                    MoreText = string.Format(DvStrings.Text(strings, "dv.filter.more", "값이 많아 처음 {0}개만 표시"), GridFilter.MaxItems);
                Items = vals.Select(v => (v, GridFilter.ItemText(Column, v, strings)))
                    .OrderBy(x => x.v is null ? 0 : 1).ThenBy(x => x.Item2, StringComparer.OrdinalIgnoreCase).ToList();
                Checked = cur?.Values is { } set ? new HashSet<object?>(Items.Select(i => i.Value).Where(set.Contains))
                                                 : new HashSet<object?>(Items.Select(i => i.Value));
                break;
            case GridFilterKind.Range:
                // 칸 = 키패드가 다시 읽을 수 있는 글자(InvariantCulture 원값) — 열 format(천 단위·소수 자리)은 요약에만
                MinText = cur?.Min is { } mn ? Convert.ToString(mn, CultureInfo.InvariantCulture) ?? "" : "";
                MaxText = cur?.Max is { } mx ? Convert.ToString(mx, CultureInfo.InvariantCulture) ?? "" : "";
                break;
            case GridFilterKind.Date:
                MinValue = cur?.Min;
                MaxValue = cur?.Max;
                break;
        }
    }

    public DvSurface Owner { get; }
    public IValueSlot Slot { get; }
    public GridFilterCellSlot CellSlot { get; }
    /// <summary>Current grid (after a rebuild, the new grid the slot moved to).</summary>
    public GridSpec Grid => CellSlot.Grid.Grid!;
    /// <summary>Column definition at open time (display text, value type).</summary>
    public GridColumn Column { get; }
    public GridFilterKind Kind { get; }
    public SKRect Bounds { get; private set; }

    // 임시 값(확인 때만 조건으로)
    public string TextValue { get; set; } = "";
    public IReadOnlyList<(object? Value, string Text)> Items { get; } = Array.Empty<(object?, string)>();
    public HashSet<object?> Checked { get; } = new();
    public bool Truncated { get; }
    /// <summary>Notice text for the item limit (empty unless <see cref="Truncated"/>).</summary>
    public string MoreText { get; } = "";
    public string MinText { get; set; } = "";
    public string MaxText { get; set; } = "";
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }

    public int PendingField { get; private set; } = -1;
    /// <summary>Display of the field being edited (virtual keyboard text, Hide if native, rejection reason). Filled by the field edit target.</summary>
    public (int Field, string? Text, bool Hide, string? Error) FieldEdit { get; set; } = (-1, null, false, null);
    /// <summary>Committed via Clear — removes the condition.</summary>
    public bool Cleared { get; private set; }
    public float ScrollY { get; private set; }

    private SKPoint? _down;
    private GridFilterCondition? _initial; // values — 연 때 조건(체크를 하나도 안 바꾸고 확인하면 그대로)
    private bool _toggled;
    private float _downScroll;
    private bool _dragging;

    private float FontSize => Slot.Style.FontSize;
    /// <summary>Font size of the notice line (smaller than the body).</summary>
    public float MoreFontSize => FontSize - 2;
    private int VisibleItems => Math.Min(Items.Count + 1, MaxVisibleItems); // +1 = 전체 선택
    private float ListHeight => VisibleItems * Row;
    private float ContentHeight => (Items.Count + 1) * Row;
    public float MaxScroll => Math.Max(0, ContentHeight - ListHeight);

    private float BodyHeight => Kind switch
    {
        GridFilterKind.Text => Row,
        GridFilterKind.Values => ListHeight + (Truncated ? Row * 0.75f : 0),
        _ => Row * 2 + Pad
    } + (ShowsError ? ErrorH : 0);

    /// <summary>Whether there is a field rejection-reason line (the space below the fields grows only then — field positions stay the same).</summary>
    public bool ShowsError => Kind != GridFilterKind.Values && FieldEdit.Error is not null;

    /// <summary>Rejection-reason line — directly below the fields, at the same left edge as the fields.</summary>
    public SKRect ErrorRect => Kind == GridFilterKind.Text
        ? SKRect.Create(Bounds.Left + Pad, BodyTop + Row, Inner, ErrorH)
        : SKRect.Create(Bounds.Left + Pad + LabelW, BodyTop + 2 * Row + Pad, Inner - LabelW, ErrorH);

    public void Place(SKRect anchor, float screenW, float screenH)
    {
        _naturalW ??= NaturalWidth(); // 항목은 열 때의 스냅샷 — 글자 폭은 한 번만 잰다
        var w = Math.Min(Math.Max(anchor.Width, _naturalW.Value), screenW - 2 * PopupPlacement.Margin);
        var h = Pad + BodyHeight + Pad + ButtonH + Pad;
        Bounds = PopupPlacement.Fixed(anchor, w, h, screenW, screenH);
        ScrollY = Math.Clamp(ScrollY, 0, MaxScroll);
    }

    private float? _naturalW;

    // 내용이 잘리지 않는 폭 — 항목 글자·날짜+시각 전체·버튼 3개
    private float NaturalWidth()
    {
        float Measure(string s, float size)
        {
            using var f = TextFont.Create(s.Length > 0 ? s : " ", size, false);
            return f.MeasureText(s);
        }

        var buttons = 3 * 72f + 2 * Pad + 2 * Pad;
        return Kind switch
        {
            GridFilterKind.Values => Math.Max(Math.Max(buttons, 2 * Pad + Measure(MoreText, MoreFontSize)),
                2 * Pad + 12 + FontSize + 8 + Items.Select(i => Measure(i.Text, FontSize)).DefaultIfEmpty(0).Max() + 12),
            GridFilterKind.Date => Math.Max(buttons, 2 * Pad + LabelW + Measure("2026-10-14 09:00:00", FontSize) + 40),
            GridFilterKind.Range => Math.Max(buttons, 2 * Pad + LabelW + MinFieldW),
            _ => Math.Max(buttons, 2 * Pad + MinFieldW + 40)
        };
    }

    private float Inner => Bounds.Width - 2 * Pad;
    private float BodyTop => Bounds.Top + Pad;

    /// <summary>Field: 0 = text input, 1 = min/start, 2 = max/end.</summary>
    public SKRect FieldRect(int field) => field == 0
        ? SKRect.Create(Bounds.Left + Pad, BodyTop, Inner, Row)
        : SKRect.Create(Bounds.Left + Pad + LabelW, BodyTop + (field - 1) * (Row + Pad), Inner - LabelW, Row);

    public SKRect LabelRect(int field) => SKRect.Create(Bounds.Left + Pad, BodyTop + (field - 1) * (Row + Pad), LabelW, Row);

    public SKRect AllRect => SKRect.Create(Bounds.Left, BodyTop - ScrollY, Bounds.Width, Row);
    public SKRect ItemRect(int i) => SKRect.Create(Bounds.Left, BodyTop + (i + 1) * Row - ScrollY, Bounds.Width, Row);
    public SKRect ListRect => SKRect.Create(Bounds.Left, BodyTop, Bounds.Width, ListHeight);
    /// <summary>Item-limit notice line (values, when <see cref="Truncated"/>).</summary>
    public SKRect MoreRect => SKRect.Create(Bounds.Left + Pad, ListRect.Bottom, Inner, Row * 0.75f);

    public SKRect CancelRect => SKRect.Create(Bounds.Left + Pad, Bounds.Bottom - Pad - ButtonH, (Inner - 2 * Pad) / 3f, ButtonH);
    public SKRect ClearRect => SKRect.Create(CancelRect.Right + Pad, CancelRect.Top, CancelRect.Width, ButtonH);
    public SKRect OkRect => SKRect.Create(ClearRect.Right + Pad, CancelRect.Top, CancelRect.Width, ButtonH);

    public void Down(SKPoint p)
    {
        _down = p;
        _downScroll = ScrollY;
        _dragging = false;
    }

    public void Move(SKPoint p)
    {
        if (_down is { } d && Kind == GridFilterKind.Values && !_dragging && SKPoint.Distance(p, d) >= DragThreshold && MaxScroll > 0)
            _dragging = true;
        if (_dragging && _down is { } d2)
            ScrollY = Math.Clamp(_downScroll - (p.Y - d2.Y), 0, MaxScroll);
    }

    public PopupResult Up(SKPoint p)
    {
        var tap = _down is not null && !_dragging;
        _down = null;
        _dragging = false;
        if (!tap)
            return PopupResult.None;
        Cleared = false; // 지우기는 이번 누름이 지우기일 때만(앞 누름의 지우기가 막혔어도 남지 않게)
        if (CancelRect.Contains(p.X, p.Y))
            return PopupResult.Cancel;
        if (ClearRect.Contains(p.X, p.Y))
        {
            Cleared = true;
            return PopupResult.Commit;
        }

        if (OkRect.Contains(p.X, p.Y))
            return PopupResult.Commit;

        switch (Kind)
        {
            case GridFilterKind.Values when ListRect.Contains(p.X, p.Y):
                _toggled = true;
                if (AllRect.Contains(p.X, p.Y))
                {
                    if (Checked.Count == Items.Count)
                        Checked.Clear();
                    else
                        foreach (var it in Items)
                            Checked.Add(it.Value);
                    return PopupResult.None;
                }

                var i = (int)MathF.Floor((p.Y - BodyTop + ScrollY) / Row) - 1;
                if (i >= 0 && i < Items.Count && !Checked.Remove(Items[i].Value))
                    Checked.Add(Items[i].Value);
                return PopupResult.None;
            case GridFilterKind.Text when FieldRect(0).Contains(p.X, p.Y):
                PendingField = 0;
                return PopupResult.EditField;
            case GridFilterKind.Range or GridFilterKind.Date:
                for (var f = 1; f <= 2; f++)
                {
                    if (FieldRect(f).Contains(p.X, p.Y))
                    {
                        PendingField = f;
                        return PopupResult.EditField;
                    }
                }

                break;
        }

        return PopupResult.None;
    }

    public void Wheel(float delta)
    {
        if (Kind == GridFilterKind.Values)
            ScrollY = Math.Clamp(ScrollY - delta * WheelStep, 0, MaxScroll);
    }

    public void Paint(SKCanvas canvas, DvTheme? theme, DvStrings? strings)
        => Painting.Painter.PaintGridFilterPopup(canvas, this, theme, strings, FontSize);

    /// <summary>Temporary values → condition (Clear, all checked, or empty input = null). Number parse failure = false + field and reason.</summary>
    public bool TryBuild(DvStrings? strings, out GridFilterCondition? cond, out int errorField, out string? error)
    {
        cond = null;
        errorField = -1;
        error = null;
        if (Cleared)
            return true;
        switch (Kind)
        {
            case GridFilterKind.Text:
                if (!string.IsNullOrWhiteSpace(TextValue))
                    cond = Make(new GridFilterCondition { Text = TextValue.Trim() });
                return true;
            case GridFilterKind.Values:
                if (!_toggled)
                    cond = _initial; // 다른 열 필터로 목록이 고른 값만 남아 전부 체크로 보여도 조건 유지
                else if (Checked.Count < Items.Count)
                    cond = Make(new GridFilterCondition { Values = new HashSet<object?>(Checked) });
                return true;
            case GridFilterKind.Range:
                if (!ParseBound(MinText, 1, out var min, ref errorField, ref error) || !ParseBound(MaxText, 2, out var max, ref errorField, ref error))
                    return false;
                if (min is not null || max is not null)
                    cond = Make(new GridFilterCondition { Min = min, Max = max });
                return true;
            case GridFilterKind.Date:
                if (MinValue is not null || MaxValue is not null)
                    cond = Make(new GridFilterCondition { Min = MinValue, Max = MaxValue });
                return true;
        }

        return true;

        GridFilterCondition Make(GridFilterCondition c)
            => new() { Text = c.Text, Values = c.Values, Min = c.Min, Max = c.Max, Summary = GridFilter.SummaryOf(Column, Kind, c, strings) };

        bool ParseBound(string text, int field, out object? value, ref int errField, ref string? err)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            var bound = Column.Reader.ValueType;
            var spec = NumericEdit.Spec(bound, null, null, null);
            if (NumericEdit.TryParse(text, bound, spec, strings, out value, out _, out var e))
                return true;
            errField = field;
            err = e;
            return false;
        }
    }
}
