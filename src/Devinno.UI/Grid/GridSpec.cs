using System.Collections;

namespace Devinno.UI.Grid;

internal enum GridSelectionMode { None, Single, Multi }

/// <summary>
/// DvDataGrid definition (built at layout) + layout result + selection state. Row data is not copied; it is only referenced through <see cref="Items"/>.
/// </summary>
internal sealed class GridSpec
{
    public IList? Items { get; init; }
    public GridColumn[] Columns { get; init; } = Array.Empty<GridColumn>();
    /// <summary>Number of consecutive frozen columns from the start.</summary>
    public int FrozenCount { get; init; }
    public float RowHeight { get; init; } = 48f;
    /// <summary>Height of one header level (<c>header-height</c>).</summary>
    public float HeaderHeight { get; init; } = 40f;
    public GridHeader Header { get; init; } = GridHeader.Single;
    /// <summary>Height of the header band (column titles and groups).</summary>
    public float HeaderBand => HeaderHeight * Header.Levels;

    private bool? _hasFilterRow;

    /// <summary>If any column uses a filter, a filter row appears below the header.</summary>
    public bool HasFilterRow => _hasFilterRow ??= Columns.Any(c => c.Filter != GridFilterSetting.None);

    /// <summary>Filter row height (same as one header level; 0 if there are no filter columns).</summary>
    public float FilterRowHeight => HasFilterRow ? HeaderHeight : 0f;

    /// <summary>Total height above the body (header band + filter row).</summary>
    public float HeaderTotal => HeaderBand + FilterRowHeight;

    // ── 요약 행(몸통 아래 고정)
    public IReadOnlyList<GridSummaryRow> Summaries { get; init; } = Array.Empty<GridSummaryRow>();
    public float SummaryHeight => Summaries.Count * RowHeight;
    /// <summary>Recompute the summaries on the next draw (right after build, after Repaint).</summary>
    public bool SummaryDirty { get; set; } = true;
    public GridSelectionMode Mode { get; init; }
    /// <summary>Raw <c>selection</c> attribute text (<c>{Bind …}</c> if two-way).</summary>
    public string? SelectionExpr { get; init; }
    /// <summary>Source value read from <c>selection</c> at build (used to tell whether a one-way binding changed — the same reference keeps the on-screen selection).</summary>
    public object? SelectionSource { get; init; }
    public FieldReader? RowKey { get; init; }
    /// <summary><c>row-class</c> — item property value = row class name.</summary>
    public FieldReader? RowClass { get; init; }
    public GridColors Colors { get; init; } = GridColors.None;
    /// <summary>true if there is a blinking lamp column — the screen is redrawn at the blink interval.</summary>
    public bool HasBlink => Columns.Any(c => c.CellType == GridCellType.Lamp && c.Blink);
    /// <summary>Class name → style (resolved via stylesheet and theme). null means classes are ignored.</summary>
    internal Func<string, GridCellStyle?>? ClassStyle { get; init; }
    private readonly Dictionary<string, GridCellStyle?> _styles = new();
    private const int MaxCachedClassStrings = 256;
    /// <summary>Visible row styles for one drawn frame (reused array — avoids allocating per frame).</summary>
    internal GridCellStyle?[] FrameRowStyles = Array.Empty<GridCellStyle?>();

    /// <summary>
    /// Class string (several separated by spaces, later wins) → merged style. Missing classes or an empty string = null. Cached per string
    /// (there are only a few distinct values, so even with 300,000 rows it is parsed only a few times).
    /// </summary>
    public GridCellStyle? StyleOf(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes) || ClassStyle is null)
            return null;
        if (_styles.TryGetValue(classes, out var cached))
            return cached;

        GridCellStyle? merged = null;
        foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ClassStyle(name) is { } st)
                merged = merged is null ? st : merged.Merge(st);
        }

        if (_styles.Count < MaxCachedClassStrings) // 행마다 다른 문자열을 주는 앱에서도 캐시가 끝없이 커지지 않게
            _styles[classes] = merged;
        return merged;
    }
    public string? OnRowClick { get; init; }

    // ── 정렬(화면 상태 — 재빌드 때 옮긴다)
    /// <summary>Sort columns — earlier ones take priority (in tap order).</summary>
    public List<GridSortKey> Sort { get; } = new();
    public int SortVersion { get; internal set; }
    /// <summary>View order → source index. null = source order.</summary>
    public int[]? Order { get; internal set; }
    internal System.Collections.IList? OrderItems { get; set; }
    internal int OrderVersion { get; set; }

    // ── 필터(화면 상태 — 재빌드 때 같은 field 의 열로 옮긴다)
    /// <summary>Column index → filter condition.</summary>
    public Dictionary<int, GridFilterCondition> Filters { get; } = new();
    public int FilterVersion { get; internal set; }

    /// <summary>Whether to filter again — set on Repaint, rebuild, condition change, or when the edit mark is cleared; cleared after filtering.</summary>
    public bool FilterDirty { get; set; } = true;
    internal int OrderFilterVersion { get; set; } = -1;
    internal int OrderCount { get; set; } = -1;

    /// <summary>Rows hidden by refiltering were removed from the selection — the surface writes the two-way selection to the VM and clears this.</summary>
    internal bool SelectionPruned { get; set; }

    /// <summary>Sets a column's filter condition (null or a non-filtering condition = clear).</summary>
    public void SetFilter(int column, GridFilterCondition? condition)
    {
        if (condition is null || !GridFilter.IsActive(Columns[column].FilterKind, condition))
            Filters.Remove(column);
        else
            Filters[column] = condition;
        FilterVersion++;
        FilterDirty = true;
        SummaryDirty = true;
    }

    /// <summary>Number of rows visible on screen (after filtering). Read after <see cref="GridSorter.Ensure"/>.</summary>
    public int ViewCount => Order?.Length ?? Items?.Count ?? 0;

    /// <summary>Item at view position <paramref name="view"/> (sorting applied; null if out of range or the list shrank).</summary>
    public object? ItemAt(int view)
    {
        if (Items is null)
            return null;
        GridSorter.Ensure(this);
        return ItemAtCurrent(view);
    }

    /// <summary>Uses the current order as-is (does not recheck sorting) — drawing sorts once at frame start and then uses this (calling Ensure per row could
    /// re-sort several times when the row count changes mid-frame, and the background and text could use different orders).</summary>
    internal object? ItemAtCurrent(int view)
    {
        var items = Items;
        if (items is null)
            return null;
        var o = Order;
        var i = o is null ? view : (uint)view < (uint)o.Length ? o[view] : -1;
        return (uint)i < (uint)items.Count ? items[i] : null;
    }

    /// <summary>
    /// First visible item below view position <paramref name="view"/> (from the next row) that satisfies <paramref name="accept"/> (null if none).
    /// The "next row" decision lives only here — if the visible row count or order (sorting, then filtering) changes, adjust only this function.
    /// </summary>
    internal object? NextVisibleItem(int view, Func<object, bool> accept)
    {
        GridSorter.Ensure(this);
        var n = ViewCount;
        for (var r = view + 1; r < n; r++)
        {
            if (ItemAtCurrent(r) is { } it && accept(it))
                return it;
        }

        return null;
    }

    /// <summary>Row under the pointer (-1 if none). Drawing-only state.</summary>
    public int HoverRow { get; set; } = -1;

    /// <summary>Cell being edited or with an open popup — set and cleared by the slot (drawing only).</summary>
    private GridEditMark? _editMark;

    internal GridEditMark? EditMark
    {
        get => _editMark;
        set
        {
            if (Filters.Count > 0 && (_editMark?.Key, _editMark?.Column) != (value?.Key, value?.Column))
                FilterDirty = true; // 편집 대상이 바뀜(끝남·다른 행으로 옮김) — 남겨 둔 행을 다시 판정
            _editMark = value;
        }
    }

    /// <summary>Whether the item is the row for key (the row-key value, or the item reference if none).</summary>
    internal bool IsKey(object item, object key) => RowKey is null ? ReferenceEquals(item, key) : Equals(KeyOf(item), key);

    /// <summary>
    /// Row number of key in view order (-1 if none). Checks <paramref name="hint"/> (the previous number) first — usually O(1);
    /// a full search happens only when sorting or row additions/removals have shifted it.
    /// </summary>
    public int ViewIndexOf(object key, int hint)
    {
        GridSorter.Ensure(this);
        var n = ViewCount;
        if ((uint)hint < (uint)n && ItemAtCurrent(hint) is { } h && IsKey(h, key))
            return hint;
        for (var i = 0; i < n; i++)
        {
            if (ItemAtCurrent(i) is { } it && IsKey(it, key))
                return i;
        }

        return -1;
    }

    // ── 배치 결과(GridLayout.Update)
    public int Count { get; internal set; }
    public float BodyHeight { get; internal set; }
    public float FrozenWidth { get; internal set; }
    public float ColumnsWidth { get; internal set; }
    public bool OverflowX { get; internal set; }
    public bool OverflowY { get; internal set; }

    // ── 선택: 순서 있는 항목 목록 + 판정용 집합(키 또는 참조)
    private readonly List<object> _selected = new();
    private HashSet<object>? _set;

    public IReadOnlyList<object> SelectedItems => _selected;

    public bool HasSelection => _selected.Count > 0;

    public object KeyOf(object item) => RowKey is null ? item : RowKey.Get(item) ?? item;

    public bool IsSelected(object item) => _set is not null && _set.Contains(KeyOf(item));

    public void SetSelection(IEnumerable<object> items)
    {
        _selected.Clear();
        _selected.AddRange(items);
        if (_selected.Count == 0)
        {
            _set = null;
            return;
        }

        _set = RowKey is null ? new HashSet<object>(ReferenceEqualityComparer.Instance) : new HashSet<object>();
        foreach (var it in _selected)
            _set.Add(KeyOf(it));
    }
}

/// <summary>Cell being edited or with an open popup (draw highlight). Text = text being typed on the virtual keyboard; HideText = a native editor overlays the cell.</summary>
internal sealed class GridEditMark
{
    public required object Key { get; init; }
    public required int Column { get; init; }
    public string? Text { get; set; }
    public bool HideText { get; set; }
    public string? Error { get; set; }
}
