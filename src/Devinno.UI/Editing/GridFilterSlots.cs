using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Anchor of the filter popup = one cell of the grid's filter row. The value members are not used (the popup writes the condition directly to GridSpec).</summary>
internal sealed class GridFilterCellSlot(DvSurface owner, LayoutBox grid, int column) : IValueSlot
{
    public DvSurface Owner { get; } = owner;
    public LayoutBox Grid { get; private set; } = grid;
    public int Column { get; } = column;

    public bool TryLocate(out SKRect rect, out SKRect clip)
    {
        rect = clip = SKRect.Empty;
        // GridCellSlot.TryLocate 와 같은 방식: 트리에서 그리드의 화면 위치·보이는 부분을 찾은 뒤 칸 사각형
        if (Grid.Grid is not { HasFilterRow: true } g || Column >= g.Columns.Length || Owner.Root is not { } root ||
            !EditGeometry.TryLocate(root, Grid, out var gridRect, out var gridClip))
            return false;
        rect = GridGeometry.FilterCellRect(g, gridRect, Grid.ScrollX, Column);
        clip = SKRect.Intersect(rect, gridClip);
        if (clip.IsEmpty)
            clip = SKRect.Empty;
        return true;
    }

    public object? Value => null;
    public Type? ValueType => null;
    public ComputedStyle Style => Grid.Style;
    public IReadOnlyList<SelectOption> Options => Array.Empty<SelectOption>();
    public int SelectedIndex => -1;
    public PickerMode PickerMode => PickerMode.DateTime;
    public bool PickerSeconds => false;
    public bool Editing { get; set; }
    public CommitResult Write(object? value) => CommitResult.Same;

    public bool Remap(LayoutBox old, LayoutBox now)
    {
        if (DvSurface.Counterpart(old, now, Grid) is not { Grid: not null } ng)
            return false;
        Grid = ng;
        return true;
    }
}

/// <summary>The start (1) and end (2) date cells of the filter popup. The date picker writes to the popup's temporary value (not the VM).</summary>
internal sealed class GridFilterDateSlot(GridFilterPopup popup, int field) : IValueSlot
{
    public DvSurface Owner => popup.Owner;

    public bool TryLocate(out SKRect rect, out SKRect clip)
    {
        rect = clip = popup.FieldRect(field); // 필터 팝업이 닫히면 ClosePopup 이 위의 선택기도 함께 정리한다
        return !popup.Bounds.IsEmpty;
    }

    public object? Value => field == 1 ? popup.MinValue : popup.MaxValue;

    public Type? ValueType => popup.Column.Reader.ValueType is { } t ? Nullable.GetUnderlyingType(t) ?? t : typeof(DateTime);

    public ComputedStyle Style => popup.Slot.Style;
    public IReadOnlyList<SelectOption> Options => Array.Empty<SelectOption>();
    public int SelectedIndex => -1;

    public PickerMode PickerMode => ValueType == typeof(DateOnly) ? PickerMode.Date : ValueType == typeof(TimeOnly) ? PickerMode.Time : PickerMode.DateTime;

    public bool PickerSeconds => false;
    public bool Editing { get; set; }

    public CommitResult Write(object? value)
    {
        if (field == 1)
            popup.MinValue = value;
        else
            popup.MaxValue = value;
        return CommitResult.Written;
    }

    public bool Remap(LayoutBox old, LayoutBox now) => true;
}
