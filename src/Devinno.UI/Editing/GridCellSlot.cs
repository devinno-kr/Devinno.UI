using System.Diagnostics;
using System.Reflection;
using Devinno.UI.Binding;
using Devinno.UI.Grid;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>
/// Grid edit cell = (grid box, row key (row-key, or the item reference if none), column index). The value is written to a property of that row's item.
/// After a rebuild, <see cref="Remap"/> picks up the new grid box; after sorting or Repaint, the row is looked up again by key each time its position is needed.
/// </summary>
internal sealed class GridCellSlot : IValueSlot
{
    private static readonly HashSet<(Type, string)> GateWarned = new();
    private int _view = -1;        // 직전 화면 행(다음 찾기의 힌트)
    private GridEditMark? _mark;   // 이 슬롯이 켠 강조

    public GridCellSlot(DvSurface owner, LayoutBox grid, int column, object item)
    {
        Owner = owner;
        GridBox = grid;
        Column = column;
        Item = item;
        Key = grid.Grid!.KeyOf(item);
    }

    public DvSurface Owner { get; }
    public LayoutBox GridBox { get; private set; }
    public int Column { get; }
    /// <summary>The current item of that row (the new object if the row object was replaced, matched by row-key).</summary>
    public object Item { get; private set; }
    public object Key { get; }
    private GridSpec G => GridBox.Grid!;
    public GridColumn Col => G.Columns[Column];

    public object? Value => Col.Reader.Get(Item);

    public Type? ValueType
    {
        get
        {
            Col.Reader.Get(Item); // 이 항목 타입의 접근자
            return Col.Reader.ValueType;
        }
    }

    public ComputedStyle Style => GridBox.Style;
    public IReadOnlyList<SelectOption> Options => Col.Options;
    public int SelectedIndex => Col.OptionIndex(Value);
    public PickerMode PickerMode => Col.DateTimeMode;
    public bool PickerSeconds => Col.ShowSeconds;

    public bool Editing
    {
        get => _mark is not null;
        set
        {
            if (value)
            {
                _mark ??= new GridEditMark { Key = Key, Column = Column };
                G.EditMark = _mark;
            }
            else if (_mark is not null)
            {
                if (ReferenceEquals(G.EditMark, _mark))
                    G.EditMark = null;
                _mark = null;
            }
        }
    }

    /// <summary>Finds the current on-screen row by key and updates <see cref="Item"/>. Returns false if the row is gone.</summary>
    public bool TryView(out int view)
    {
        view = Column < G.Columns.Length ? G.ViewIndexOf(Key, _view) : -1;
        if (view < 0)
            return false;
        _view = view;
        Item = G.ItemAtCurrent(view)!;
        return true;
    }

    public bool TryLocate(out SKRect rect, out SKRect clip)
    {
        rect = clip = SKRect.Empty;
        if (Owner.Root is not { } root || !TryView(out var view) ||
            !EditGeometry.TryLocate(root, GridBox, out var grid, out var gridClip))
            return false;
        rect = GridGeometry.CellRect(G, grid, GridBox.ScrollX, GridBox.ScrollY, view, Column, out var cellClip);
        clip = SKRect.Intersect(cellClip, gridClip);
        if (clip.IsEmpty)
            clip = SKRect.Empty;
        return true;
    }

    public bool Remap(LayoutBox old, LayoutBox now)
    {
        if (DvSurface.Counterpart(old, now, GridBox) is not { Grid: { } ng } nb || Column >= ng.Columns.Length)
            return false;
        GridBox = nb;
        if (_mark is not null)
            ng.EditMark = _mark;
        return true;
    }

    public bool SameCell(GridCellSlot other)
        => ReferenceEquals(other.GridBox, GridBox) && other.Column == Column && G.IsKey(other.Item, Key);

    /// <summary>Editing display: turns on the highlight and shows the text being typed (virtual keyboard), hides it (native), or shows the rejection reason.</summary>
    public void ShowEdit(string? text, bool hideText, string? error)
    {
        Editing = true;
        _mark!.Text = text;
        _mark.HideText = hideText;
        _mark.Error = error;
    }

    /// <summary>Scrolls the grid so the cell is fully visible, then scrolls the outer scroll box so the cell is visible (based on the cell, not the whole grid).</summary>
    public void Reveal()
    {
        if (!TryView(out var view))
            return;
        float sx = GridBox.ScrollX, sy = GridBox.ScrollY;
        GridGeometry.Reveal(G, GridBox.Rect.Width, view, Column, ref sx, ref sy);
        GridBox.ScrollX = sx;
        GridBox.ScrollY = sy;
        GridBox.ClampScroll();
        if (TryLocate(out var cell, out _))
            Owner.ScrollIntoView(GridBox, cell); // 클립 전 셀 사각형
    }

    /// <summary>Whether a press (in grid Rect coordinates) is over this cell.</summary>
    public bool Hits(LayoutBox? hitBox, SKPoint p)
        => ReferenceEquals(hitBox, GridBox) && GridInteraction.RowAt(GridBox, p) is var row and >= 0 &&
           G.ItemAt(row) is { } it && G.IsKey(it, Key) && GridInteraction.ColumnAtX(GridBox, p.X) == Column;

    public CommitResult Write(object? value)
    {
        if (!TryView(out _))
            return CommitResult.Same; // 행이 사라짐 — 쓰지 않는다(편집·팝업은 다음 위치 확인에서 닫힘)
        var col = Col;
        if (Equals(col.Reader.Get(Item), value))
            return CommitResult.Same;
        if (col.OnBeforeCommit is { Length: > 0 } gate && !Gate(gate, value))
            return CommitResult.Rejected;

        if (!HandlerErrors.TrySet(Owner, GridBox.Node.Tag, Item, col.Reader.Path, value))
            return CommitResult.Failed; // setter 예외 = 거절과 같게(알림은 TrySet 이)
        Owner.Repaint(); // 재빌드 없이 다시 그림 + 요약 재계산
        return CommitResult.Written;
    }

    // bool M(항목, 값) 우선, 없으면 bool M(값) — 값 = 속성의 기저 타입(또는 그 nullable).
    // 둘 다 없음 = 쓰지 않음 + 경고 1회, VM 없음·예외·true 아닌 반환 = 거절(3c 규칙)
    private bool Gate(string gate, object? value)
    {
        var vm = Owner.Vm;
        if (vm is null)
            return false;
        var t = ValueType;
        bool Fits(Type p) => p == t || Nullable.GetUnderlyingType(p) == t;
        var methods = vm.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == gate).ToList();
        var withItem = methods.FirstOrDefault(m => m.GetParameters() is { Length: 2 } ps &&
                                                   ps[0].ParameterType.IsInstanceOfType(Item) && Fits(ps[1].ParameterType));
        var valueOnly = methods.FirstOrDefault(m => m.GetParameters() is { Length: 1 } ps && Fits(ps[0].ParameterType));
        var target = withItem ?? valueOnly;
        if (target is null)
        {
            lock (GateWarned)
            {
                if (GateWarned.Add((vm.GetType(), gate)))
                    Trace.TraceWarning($"Devinno.UI: DvColumn onbeforecommit '{gate}' — bool {gate}(항목, {t?.Name}) 또는 bool {gate}({t?.Name}) 없음, 쓰지 않음");
            }

            return false;
        }

        try
        {
            return target.Invoke(vm, withItem is not null ? new[] { Item, value } : new[] { value }) is true;
        }
        catch (Exception ex) // 처리기 예외(TargetInvocationException)·호출 자체 실패(인수 불일치 등) 모두 거절
        {
            HandlerErrors.Report(Owner, ex, "onbeforecommit", gate, GridBox.Node.Tag);
            return false;
        }
    }
}
