namespace Devinno.UI.Grid;

/// <summary>The header cell of one <c>DvColumnGroup</c>: tier <see cref="Level"/> (0 = top), columns <see cref="First"/> to <see cref="Last"/>.</summary>
internal sealed record GridHeaderGroup(string Header, int Level, int First, int Last);

/// <summary>
/// Multi-tier header: the number of tiers, the group cells, and the depth of each column (the number of groups enclosing it). A column cell is merged vertically from its own depth down to the bottom tier.
/// </summary>
internal sealed record GridHeader(int Levels, IReadOnlyList<GridHeaderGroup> Groups, int[] Depth)
{
    public static readonly GridHeader Single = new(1, Array.Empty<GridHeaderGroup>(), Array.Empty<int>());

    public int DepthOf(int column) => column < Depth.Length ? Depth[column] : 0;
}
