using Devinno.UI.Interaction;
using Devinno.UI.Layout;

namespace Devinno.UI.Grid;

/// <summary>
/// Content size and column arrangement of a grid box. Called at arrange time and right before every paint (so a changed row count is reflected by <c>Repaint()</c> alone).
/// No allocations; the row count is read from <c>Count</c> only once.
/// </summary>
internal static class GridLayout
{
    public static void Update(LayoutBox box)
    {
        if (box.Grid is not { } g)
            return;

        var r = box.Rect;
        // 필터가 걸렸을 때만 보이는 행 수를 거른 뒤로 센다. 필터 없는 그리드는 정렬하지 않는다 — 재빌드 배치는
        // 정렬 상태를 옮겨 오기 전이라, 여기서 정렬하면 버려질 정렬을 한 번 더 한다.
        int count;
        if (g.Filters.Count > 0)
        {
            GridSorter.Ensure(g);
            count = g.ViewCount;
        }
        else
            count = g.Items?.Count ?? 0;
        var rowsH = count * g.RowHeight;
        var viewH = MathF.Max(0, r.Height - g.HeaderTotal - g.SummaryHeight);
        const float gutter = ScrollBars.Gutter;

        // 세로 넘침 → 열 배분 폭 감소 → 가로 넘침 → 몸통 높이 감소 → 세로 넘침 재판정(한 번이면 수렴)
        var overflowY = rowsH > viewH;
        var total = GridGeometry.ResolveColumns(g.Columns, r.Width - (overflowY ? gutter : 0));
        var overflowX = total > r.Width - (overflowY ? gutter : 0) + 0.01f;
        if (overflowX && !overflowY && rowsH > viewH - gutter)
        {
            overflowY = true;
            total = GridGeometry.ResolveColumns(g.Columns, r.Width - gutter);
        }

        float frozenW = 0;
        for (var i = 0; i < g.FrozenCount; i++)
            frozenW += g.Columns[i].W;

        g.Count = count;
        g.OverflowX = overflowX;
        g.OverflowY = overflowY;
        g.ColumnsWidth = total;
        g.FrozenWidth = frozenW;
        g.BodyHeight = MathF.Max(0, viewH - (overflowX ? gutter : 0));

        box.ScrollInsetTop = g.HeaderTotal;
        box.ScrollInsetBottom = g.SummaryHeight;
        box.ContentWidth = overflowX ? total + (overflowY ? gutter : 0) : r.Width;
        box.ContentHeight = MathF.Max(r.Height, g.HeaderTotal + rowsH + g.SummaryHeight + (overflowX ? gutter : 0));
        box.ClampScroll();
    }
}
