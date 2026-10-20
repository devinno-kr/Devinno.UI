using Devinno.UI.Interaction;
using SkiaSharp;

namespace Devinno.UI.Grid;

/// <summary>
/// Grid geometry: pure computation, no allocations. Row height is fixed, so visible rows and point → row come straight from division
/// (no list of row rectangles is built or searched).
/// </summary>
internal static class GridGeometry
{
    /// <summary>Width of a <c>*</c> column when there is no remaining width (so it does not shrink to 0; horizontal scrolling is used instead).</summary>
    public const float MinStarWidth = 48f;

    /// <summary>Resolves column widths and x positions: px → % → remaining width split by <c>*</c> weights. Returns the sum of column widths.</summary>
    public static float ResolveColumns(GridColumn[] cols, float avail)
    {
        avail = MathF.Max(0, avail);
        float used = 0, stars = 0;
        foreach (var c in cols)
        {
            switch (c.Width.Kind)
            {
                case GridWidthKind.Px: c.W = c.Width.Value; used += c.W; break;
                case GridWidthKind.Percent: c.W = avail * c.Width.Value / 100f; used += c.W; break;
                default: stars += c.Width.Value; break;
            }
        }

        var rest = avail - used;
        float x = 0;
        foreach (var c in cols)
        {
            if (c.Width.Kind == GridWidthKind.Star)
                c.W = MathF.Max(MinStarWidth, stars > 0 ? rest * c.Width.Value / stars : 0);
            c.X = x;
            x += c.W;
        }

        return x;
    }

    /// <summary>The range of rows visible in the body (inclusive). (0, -1) if there are no rows.</summary>
    public static (int First, int Last) VisibleRows(float scrollY, float bodyH, float rh, int count)
    {
        if (count <= 0 || rh <= 0 || bodyH <= 0)
            return (0, -1);
        var first = Math.Clamp((int)(scrollY / rh), 0, count - 1);
        var last = Math.Clamp((int)MathF.Ceiling((scrollY + bodyH) / rh) - 1, first, count - 1);
        return (first, last);
    }

    /// <summary>The row index at y, measured from the top of the grid. Header, outside the body, or below the last row = -1.</summary>
    public static int HitRow(float yInBox, float scrollY, float headerH, float bodyH, float rh, int count)
    {
        var y = yInBox - headerH;
        if (y < 0 || y >= bodyH || rh <= 0)
            return -1;
        var row = (int)((y + scrollY) / rh);
        return row < count ? row : -1;
    }

    /// <summary>
    /// The cell rectangle (in the coordinate system the grid box is placed in) + its visible part (intersection with the body's frozen/scrolling area; Empty if fully hidden).
    /// Frozen columns are unaffected by horizontal scrolling.
    /// </summary>
    public static SKRect CellRect(GridSpec g, SKRect grid, float scrollX, float scrollY, int view, int column, out SKRect clip)
    {
        var col = g.Columns[column];
        var frozen = column < g.FrozenCount;
        var bodyTop = grid.Top + g.HeaderTotal;
        var cell = SKRect.Create(grid.Left + col.X - (frozen ? 0 : scrollX), bodyTop + view * g.RowHeight - scrollY, col.W, g.RowHeight);
        var colsRight = grid.Right - (g.OverflowY ? ScrollBars.Gutter : 0);
        var area = frozen
            ? new SKRect(grid.Left, bodyTop, grid.Left + g.FrozenWidth, bodyTop + g.BodyHeight)
            : new SKRect(grid.Left + g.FrozenWidth, bodyTop, colsRight, bodyTop + g.BodyHeight);
        clip = SKRect.Intersect(cell, area);
        if (clip.IsEmpty)
            clip = SKRect.Empty;
        return cell;
    }

    /// <summary>Adjusts the scroll offset so the cell is fully visible in the body (vertical only for frozen columns). Range clamping is the caller's job (ClampScroll).</summary>
    public static void Reveal(GridSpec g, float gridWidth, int view, int column, ref float scrollX, ref float scrollY)
    {
        var top = view * g.RowHeight;
        if (top < scrollY)
            scrollY = top;
        else if (top + g.RowHeight > scrollY + g.BodyHeight)
            scrollY = top + g.RowHeight - g.BodyHeight;
        if (column < g.FrozenCount)
            return;

        var col = g.Columns[column];
        var viewW = gridWidth - (g.OverflowY ? ScrollBars.Gutter : 0) - g.FrozenWidth; // 스크롤 열 영역 폭
        var left = col.X - g.FrozenWidth;
        if (left < scrollX)
            scrollX = left;
        else if (left + col.W > scrollX + viewW)
            scrollX = Math.Min(left, left + col.W - viewW); // 영역보다 넓으면 왼쪽 맞춤
    }

    /// <summary>The column's cell in the filter row (grid box coordinates). Frozen columns are unaffected by horizontal scrolling.</summary>
    public static SKRect FilterCellRect(GridSpec g, SKRect grid, float scrollX, int column)
    {
        var col = g.Columns[column];
        var frozen = column < g.FrozenCount;
        return SKRect.Create(grid.Left + col.X - (frozen ? 0 : scrollX), grid.Top + g.HeaderBand, col.W, g.FilterRowHeight);
    }
}
