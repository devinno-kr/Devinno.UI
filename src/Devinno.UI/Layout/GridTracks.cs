namespace Devinno.UI.Layout;

/// <summary>
/// The resolved tracks of a grid box (<c>DvGrid</c>, or <c>display:grid</c> with a track list), from <see cref="LayoutBox.GridTracks"/>.
/// Every track is listed, including template tracks with no child in them and <c>auto</c> tracks added past the template.
/// </summary>
/// <remarks>
/// Positions are measured from the top-left of the grid box's <see cref="LayoutBox.Rect"/> (its outer edge, so they include the padding),
/// so they stay valid when a tool moves the box. A screen editor can draw the empty cells with them and turn a pointer position into a cell.
/// The gap lies between one track's end and the next track's start; an empty <c>auto</c> track has size 0 but keeps the gaps around it.
/// Like <see cref="LayoutBox.Rect"/>, these are layout coordinates before scrolling: find where the grid box is drawn with
/// <see cref="Interaction.HitTester.TryGetDrawnRect"/> (ancestor scrolling and content scale), and add the grid's own
/// <see cref="LayoutBox.ScrollX"/>/<see cref="LayoutBox.ScrollY"/> when it scrolls.
/// </remarks>
/// <example>
/// <code>
/// var tracks = gridBox.GridTracks!;
/// foreach (var col in tracks.Columns)
///     canvas.DrawLine(gridBox.Rect.Left + col.Start, gridBox.Rect.Top, gridBox.Rect.Left + col.Start, gridBox.Rect.Bottom, pen);
/// </code>
/// </example>
public sealed class GridTracks
{
    internal GridTracks(GridTrack[] columns, GridTrack[] rows) => (Columns, Rows) = (columns, rows);

    /// <summary>Column tracks, left to right (column 1 first).</summary>
    public IReadOnlyList<GridTrack> Columns { get; }

    /// <summary>Row tracks, top to bottom (row 1 first).</summary>
    public IReadOnlyList<GridTrack> Rows { get; }
}

/// <summary>One grid track: <paramref name="Start"/> = distance (px) from the grid box's left (column) or top (row) edge, <paramref name="Size"/> = its width or height (px).</summary>
/// <param name="Start">Distance from the grid box's outer left or top edge, px.</param>
/// <param name="Size">Width (column) or height (row), px.</param>
public readonly record struct GridTrack(float Start, float Size);

/// <summary>
/// The cell a grid child occupies, from <see cref="LayoutBox.GridArea"/>: the same numbers as the <c>grid-row</c>/<c>grid-col</c>/<c>row-span</c>/<c>col-span</c> attributes,
/// counted from 1, also for a child that was placed automatically.
/// </summary>
/// <param name="Row">First row, from 1.</param>
/// <param name="Column">First column, from 1.</param>
/// <param name="RowSpan">Rows covered, 1 or more.</param>
/// <param name="ColumnSpan">Columns covered, 1 or more.</param>
public readonly record struct GridArea(int Row, int Column, int RowSpan, int ColumnSpan);
