namespace Devinno.UI.Grid;

/// <summary>The value of the DvColumn <c>filter</c> attribute. <see cref="Auto"/> picks the kind from the value type.</summary>
internal enum GridFilterSetting { None, Auto, Text, Values, Range, Date }

/// <summary>The actual kind of a column filter (the result of resolving auto).</summary>
internal enum GridFilterKind { None, Text, Values, Range, Date }

/// <summary>
/// The filter condition of one column (grid screen state — not written to the VM). Only the values matching the kind are used:
/// text = <see cref="Text"/>, values = <see cref="Values"/> (null = empty value), range/date = <see cref="Min"/>/<see cref="Max"/> (column value type; one side alone is allowed).
/// <see cref="Summary"/> is the text shown in the filter row (built localized by the popup on confirm).
/// </summary>
internal sealed class GridFilterCondition
{
    public string? Text { get; init; }
    public IReadOnlySet<object?>? Values { get; init; }
    public object? Min { get; init; }
    public object? Max { get; init; }
    public string Summary { get; init; } = "";
}

internal static class GridFilter
{
    /// <summary>Whether the condition actually filters anything (empty text or an empty range means no condition; so does a condition of a different kind than the column).</summary>
    public static bool IsActive(GridFilterKind kind, GridFilterCondition c) => kind switch
    {
        GridFilterKind.Text => !string.IsNullOrWhiteSpace(c.Text),
        GridFilterKind.Values => c.Values is not null,
        GridFilterKind.Range or GridFilterKind.Date => c.Min is not null || c.Max is not null,
        _ => false
    };

    /// <summary>Whether one item row matches the condition.</summary>
    public static bool Matches(GridColumn col, GridFilterKind kind, GridFilterCondition c, object item)
    {
        switch (kind)
        {
            case GridFilterKind.Text:
                return col.CellText(item).Contains(c.Text!.Trim(), StringComparison.OrdinalIgnoreCase);
            case GridFilterKind.Values:
            {
                var v = col.Reader.Get(item);
                return c.Values!.Contains(v is "" ? null : v);
            }
            case GridFilterKind.Range:
            case GridFilterKind.Date:
            {
                if (col.Reader.Get(item) is not { } v)
                    return false;
                return (c.Min is null || GridSorter.Compare(v, c.Min) >= 0) && (c.Max is null || GridSorter.Compare(v, c.Max) <= 0);
            }
            default:
                return true;
        }
    }

    /// <summary>The cap on values items — beyond it, only the first this many are shown, plus a notice line.</summary>
    public const int MaxItems = 1000;

    /// <summary>Values items — the distinct values of rows that pass the other columns' filters (empty value = a single null). Sorting by display text is up to the caller.</summary>
    public static List<object?> DistinctValues(GridSpec g, int column, int max, out bool truncated)
    {
        truncated = false;
        var items = g.Items;
        var result = new List<object?>();
        if (items is null)
            return result;
        var col = g.Columns[column];
        var others = g.Filters.Where(kv => kv.Key != column && (uint)kv.Key < (uint)g.Columns.Length)
            .Select(kv => (Col: g.Columns[kv.Key], Kind: g.Columns[kv.Key].FilterKind, Cond: kv.Value))
            .Where(x => x.Kind != GridFilterKind.None && IsActive(x.Kind, x.Cond)).ToList();
        var seen = new HashSet<object?>();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not { } it || !MatchesAll(others, it))
                continue;
            var v = col.Reader.Get(it);
            if (v is "")
                v = null;
            if (seen.Add(v))
            {
                if (result.Count == max)
                {
                    truncated = true;
                    break;
                }

                result.Add(v);
            }
        }

        return result;
    }

    // 행마다 클로저를 만들지 않게 평범한 반복
    private static bool MatchesAll(List<(GridColumn Col, GridFilterKind Kind, GridFilterCondition Cond)> conds, object item)
    {
        foreach (var (col, kind, cond) in conds)
        {
            if (!Matches(col, kind, cond, item))
                return false;
        }

        return true;
    }

    /// <summary>The filter row summary text (built on confirm and stored in the condition).</summary>
    public static string SummaryOf(GridColumn col, GridFilterKind kind, GridFilterCondition c, Localization.DvStrings? strings)
    {
        switch (kind)
        {
            case GridFilterKind.Text:
                return c.Text?.Trim() ?? "";
            case GridFilterKind.Values:
                return string.Join(", ", c.Values!.Select(v => ItemText(col, v, strings)).OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
            case GridFilterKind.Range:
            case GridFilterKind.Date:
                var lo = c.Min is null ? null : BoundText(col, kind, c.Min);
                var hi = c.Max is null ? null : BoundText(col, kind, c.Max);
                return lo is not null && hi is not null ? $"{lo} ~ {hi}" : lo is not null ? $"≥ {lo}" : hi is not null ? $"≤ {hi}" : "";
            default:
                return "";
        }
    }

    /// <summary>Display text of a values item — empty values and bools use localized strings; others use the same text as the cell (select column = option text).</summary>
    public static string ItemText(GridColumn col, object? v, Localization.DvStrings? strings) => v switch
    {
        null => Localization.DvStrings.Builtin(strings, "dv.filter.empty"),
        bool b when col.CellType != GridCellType.Select => b ? Localization.DvStrings.Builtin(strings, "dv.filter.true")
                                                              : Localization.DvStrings.Builtin(strings, "dv.filter.false"),
        _ => col.ValueText(v)
    };

    /// <summary>Range/date bound text — for date, when the column has no format: yyyy-MM-dd for date-only values, yyyy-MM-dd HH:mm when there is a time.</summary>
    internal static string BoundText(GridColumn col, GridFilterKind kind, object v)
    {
        if (kind == GridFilterKind.Date && col.Format is null)
            return v switch
            {
                DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                                                             : dt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                DateOnly d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                TimeOnly t => t.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                _ => col.ValueText(v)
            };
        return col.ValueText(v);
    }
}
