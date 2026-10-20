using Devinno.UI.Grid;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI;

// DvDataGrid — hover 행 갱신, 재빌드 때 화면 전용 상태(VM 에 쓰지 않는 선택·hover) 옮기기
public sealed partial class DvSurface
{
    /// <returns>True if a repaint is needed (the row changed in a grid that paints hover).</returns>
    private bool GridHover(LayoutBox? hover, SKPoint local)
    {
        var changed = false;
        if (_state.Hover is { Grid: { } old } oldBox && !ReferenceEquals(oldBox, hover) && old.HoverRow >= 0)
        {
            old.HoverRow = -1;
            changed = GridInteraction.ShowsHover(old);
        }

        if (hover is { Grid: { } g, Disabled: false }) // 비활성 그리드는 hover 행 없음
        {
            var row = GridInteraction.RowAt(hover, local);
            if (row != g.HoverRow)
            {
                g.HoverRow = row;
                changed |= GridInteraction.ShowsHover(g);
            }
        }

        return changed;
    }

    // 그리드가 스크롤되면 포인터 아래 행이 바뀐다 — 다음 포인터 이동까지 hover 를 끈다(옛 행 번호가 내용과 함께 움직이지 않게)
    private static void ClearGridHover(LayoutBox box)
    {
        if (box.Grid is { HoverRow: >= 0 } g)
            g.HoverRow = -1;
    }

    /// <summary>Tap on an editable cell. Input opens an edit session at the cell position; other types open a popup below the cell (window surface stack).</summary>
    private void BeginGridCell(LayoutBox grid, object item, int column)
    {
        var slot = new GridCellSlot(this, grid, column, item);
        switch (grid.Grid!.Columns[column].CellType)
        {
            case GridCellType.Input:
                BeginEdit(new GridCellEditTarget(slot)); // 열려 있던 다른 편집은 BeginEdit 이 먼저 FocusLost 커밋
                break;
            case GridCellType.Select:
                Window.OpenPopup(new DropdownPopup(this, slot));
                break;
            case GridCellType.DateTime:
                Window.OpenPopup(CreatePicker(slot, color: false));
                break;
            case GridCellType.Color:
                Window.OpenPopup(CreatePicker(slot, color: true));
                break;
        }
    }

    /// <summary>Tap on a filter cell. Opens that column's filter popup (window surface stack). Any open edit is committed first.</summary>
    private void OpenGridFilter(LayoutBox grid, int column)
    {
        CommitCurrent(DvEditExit.FocusLost);
        Window.OpenPopup(new GridFilterPopup(this, new GridFilterCellSlot(this, grid, column), Strings));
    }

    private static void MarkGridData(LayoutBox box)
    {
        if (box.Grid is { } g)
        {
            g.SummaryDirty = true;
            g.FilterDirty = true; // Repaint·Invalidate = 값이 바뀌었을 수 있음 → 다시 거른다
        }

        foreach (var c in box.Children)
            MarkGridData(c);
    }

    // 다시 거르기로 숨겨진 선택 행을 뺀 그리드 — 양방향이면 새 선택을 VM 에 쓴다(그리기 경로 밖에서)
    private void FlushPrunedSelections(LayoutBox? box)
    {
        if (box is null)
            return;
        if (box.Grid is { SelectionPruned: true } g)
        {
            g.SelectionPruned = false;
            GridInteraction.Write(box, g, _vm, this);
        }

        foreach (var c in box.Children)
            FlushPrunedSelections(c);
    }

    // 필터 조건을 걸 열 — 같은 번호·같은 field 경로가 우선, 아니면 같은 경로의 첫 열. 없으면 -1.
    // kind 를 주면 그 필터 종류인 열만(팝업 확인), 안 주면 필터를 쓰는 열이면 된다(재빌드 이전)
    private static int FilterTarget(GridSpec g, int column, string path, GridFilterKind? kind = null)
    {
        bool Fits(GridColumn c) => c.Reader.Path == path && (kind is { } k ? c.FilterKind == k : c.Filter != GridFilterSetting.None);
        if ((uint)column < (uint)g.Columns.Length && Fits(g.Columns[column]))
            return column;
        return Array.FindIndex(g.Columns, Fits);
    }

    private void RemapGrids(LayoutBox old, LayoutBox now)
    {
        Copy(old);

        void Copy(LayoutBox b)
        {
            if (b.Grid is { } g && Counterpart(old, now, b) is { Grid: { } ng })
            {
                ng.HoverRow = g.HoverRow;
                // 정렬 상태·결과 — 행 탭(재빌드)마다 다시 정렬하지 않게. 열 구성이 바뀌었으면 새 빌드 상태(초기 sort) 유지.
                if (ng.Columns.Length == g.Columns.Length)
                {
                    ng.Sort.Clear();
                    ng.Sort.AddRange(g.Sort);
                    ng.SortVersion = g.SortVersion;
                    ng.Order = g.Order;
                    ng.OrderItems = g.OrderItems;
                    ng.OrderVersion = g.OrderVersion;
                    ng.OrderCount = g.OrderCount;
                    ng.FilterVersion = g.FilterVersion;
                    ng.OrderFilterVersion = g.OrderFilterVersion; // 필터 없는 정렬 그리드는 캐시 적중(값만 바뀌면 순서 유지)
                }

                // 필터 — 같은 field 경로이고 새 열도 필터를 쓰면 조건을 옮긴다(열이 빠졌거나 none 이면 버림).
                // 같은 field 를 쓰는 열이 여럿이면 같은 번호 열이 우선(앞쪽 다른 열로 옮겨 가지 않게).
                // 필터 종류가 바뀌어 새 종류로는 아무것도 거르지 않는 조건이면 버린다(활성으로 칠해지지 않게)
                foreach (var (ci, cond) in g.Filters)
                {
                    var ni = FilterTarget(ng, ci, g.Columns[ci].Reader.Path);
                    if (ni >= 0 && GridFilter.IsActive(ng.Columns[ni].FilterKind, cond))
                        ng.Filters[ni] = cond;
                }

                if (ng.Filters.Count != g.Filters.Count)
                    ng.FilterVersion++; // 조건이 빠졌다 — 옮겨 온 순서 캐시가 맞지 않으니 다시 만든다
                if (ng.Filters.Count > 0)
                    ng.FilterDirty = true; // 재빌드 = 값이 바뀌었을 수 있음 → 다시 거른다

                // 양방향이면 선택은 VM 에서 다시 읽었다. 단방향·미지정은 탭한 화면 선택을 옮기되,
                // 단방향 원본 값이 바뀌었으면(VM 이 새 값을 넣음) 새로 읽은 선택을 따른다.
                if (g.Mode != Grid.GridSelectionMode.None && !Binding.BindingResolver.IsTwoWay(ng.SelectionExpr) &&
                    ReferenceEquals(g.SelectionSource, ng.SelectionSource))
                    ng.SetSelection(g.SelectedItems);
            }

            foreach (var c in b.Children)
                Copy(c);
        }
    }
}
