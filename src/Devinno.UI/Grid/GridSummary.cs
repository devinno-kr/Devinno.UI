using System.Globalization;

namespace Devinno.UI.Grid;

internal enum GridAggregate { None, Sum, Avg, Min, Max, Count }

/// <summary><c>DvSummary</c>: the aggregate of one column cell, or a value supplied by the app.</summary>
internal sealed record GridSummaryCell(GridAggregate Fn, string? Format, bool HasValue, object? Value);

/// <summary><c>DvSummaryRow</c>: a title (first column) + a row aggregate (all numeric columns) + per-column cells. <see cref="Texts"/> = the computed display strings (in column order).</summary>
internal sealed class GridSummaryRow
{
    public string Title { get; init; } = "";
    public GridAggregate Fn { get; init; }
    public IReadOnlyDictionary<int, GridSummaryCell> Cells { get; init; } = new Dictionary<int, GridSummaryCell>();
    public string?[] Texts { get; internal set; } = Array.Empty<string?>();
}

/// <summary>
/// Summary computation: for each summary row, walks the rows once and aggregates all of that row's cells together. Non-numeric values and null are skipped (count = number of non-null values).
/// Only visible rows (after filtering) are walked, in screen order.
/// </summary>
internal static class GridSummary
{
    public static GridAggregate Parse(string? fn) => fn?.Trim().ToLowerInvariant() switch
    {
        "sum" => GridAggregate.Sum,
        "avg" => GridAggregate.Avg,
        "min" => GridAggregate.Min,
        "max" => GridAggregate.Max,
        "count" => GridAggregate.Count,
        _ => GridAggregate.None
    };

    public static void Compute(GridSpec g)
    {
        GridSorter.Ensure(g);
        var view = g.ViewCount; // 보이는 행(거른 뒤)만 집계
        var items = g.Items;
        var n = items?.Count ?? 0;
        var cols = g.Columns;

        // 값 타입(숫자 열 판정)이 아직 없으면 첫 항목으로 확정
        if (n > 0)
        {
            for (var i = 0; i < n && i < 64; i++)
            {
                if (items![i] is not { } it)
                    continue;
                foreach (var c in cols)
                {
                    if (c.Reader.ValueType is null && c.Reader.Path.Length > 0)
                        c.Reader.Get(it);
                }

                break;
            }
        }

        foreach (var row in g.Summaries)
        {
            var texts = new string?[cols.Length];
            if (cols.Length > 0)
                texts[0] = row.Title;

            var active = new List<(int Col, GridAggregate Fn, string? Format)>();
            // decimal 열은 decimal 로 합산(정확), float 열은 float 로 표시(이진 오차 숫자 노출 방지)
            for (var c = 1; c < cols.Length; c++)
            {
                var cell = row.Cells.GetValueOrDefault(c)
                           ?? (row.Fn != GridAggregate.None && cols[c].IsNumericValue ? new GridSummaryCell(row.Fn, null, false, null) : null);
                if (cell is null)
                    continue;
                if (cell.HasValue)
                    texts[c] = Format(cell.Value, cell.Format ?? cols[c].Format, cols[c].Reader.Path); // 앱이 준 값
                else if (cell.Fn != GridAggregate.None)
                    active.Add((c, cell.Fn, cell.Fn == GridAggregate.Count ? cell.Format : cell.Format ?? cols[c].Format));
            }

            if (active.Count > 0)
            {
                var sum = new double[active.Count];
                var cnt = new long[active.Count];
                var min = new double[active.Count];
                var max = new double[active.Count];
                Array.Fill(min, double.PositiveInfinity);
                Array.Fill(max, double.NegativeInfinity);
                var isDec = new bool[active.Count];
                var isFlt = new bool[active.Count];
                var dsum = new decimal[active.Count];
                var dmin = new decimal[active.Count];
                var dmax = new decimal[active.Count];
                for (var a = 0; a < active.Count; a++)
                {
                    var vt = cols[active[a].Col].Reader.ValueType;
                    isDec[a] = vt == typeof(decimal);
                    isFlt[a] = vt == typeof(float);
                    dmin[a] = decimal.MaxValue;
                    dmax[a] = decimal.MinValue;
                }

                // 다른 스레드가 순회 중 목록을 줄여도 예외 없이(남은 행만)
                for (var vi = 0; vi < view; vi++)
                {
                    if (g.ItemAtCurrent(vi) is not { } it)
                        continue;
                    for (var a = 0; a < active.Count; a++)
                    {
                        var reader = cols[active[a].Col].Reader;
                        double d;
                        if (isDec[a] && active[a].Fn != GridAggregate.Count)
                        {
                            if (reader.Get(it) is decimal m)
                            {
                                dsum[a] += m;
                                cnt[a]++;
                                if (m < dmin[a]) dmin[a] = m;
                                if (m > dmax[a]) dmax[a] = m;
                            }

                            continue;
                        }

                        if (reader.TryGetDouble(it, out var typed))
                        {
                            // 숫자 속성 — 박싱 없음(30만 행이면 할당·GC 가 집계 시간의 대부분이었다)
                            if (double.IsNaN(typed))
                                continue; // null(또는 NaN)
                            if (active[a].Fn == GridAggregate.Count)
                            {
                                cnt[a]++;
                                continue;
                            }

                            d = typed;
                        }
                        else
                        {
                            var v = reader.Get(it);
                            if (v is null)
                                continue;
                            if (active[a].Fn == GridAggregate.Count)
                            {
                                cnt[a]++;
                                continue;
                            }

                            if (!IsNumber(v))
                                continue;
                            d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                        }

                        sum[a] += d;
                        cnt[a]++;
                        if (d < min[a]) min[a] = d;
                        if (d > max[a]) max[a] = d;
                    }
                }

                for (var a = 0; a < active.Count; a++)
                {
                    var (c, fn, format) = active[a];
                    if (fn == GridAggregate.Count)
                    {
                        texts[c] = Format(cnt[a], format);
                        continue;
                    }

                    if (cnt[a] == 0)
                    {
                        texts[c] = "";
                        continue;
                    }

                    if (isDec[a])
                    {
                        texts[c] = Format(fn switch
                        {
                            GridAggregate.Sum => dsum[a],
                            GridAggregate.Avg => dsum[a] / cnt[a],
                            GridAggregate.Min => dmin[a],
                            _ => dmax[a]
                        }, format);
                        continue;
                    }

                    var value = fn switch
                    {
                        GridAggregate.Sum => sum[a],
                        GridAggregate.Avg => sum[a] / cnt[a],
                        GridAggregate.Min => min[a],
                        _ => max[a]
                    };
                    texts[c] = isFlt[a] ? Format((float)value, format) : Format(value, format);
                }
            }

            row.Texts = texts;
        }

        g.SummaryDirty = false;
    }

    private static string Format(object? v, string? format, string? member = null)
    {
        if (v is null)
            return "";
        try
        {
            if (v is IFormattable f)
            {
                try
                {
                    return f.ToString(string.IsNullOrEmpty(format) ? null : format, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    return f.ToString(null, CultureInfo.InvariantCulture);
                }
            }

            return v.ToString() ?? "";
        }
        catch (Exception ex)
        {
            // 앱 값(DvSummary value) 형식의 ToString 예외 = 빈 칸
            Interaction.HandlerErrors.ReportRead(ex, "format", member, "DvDataGrid");
            return "";
        }
    }

    private static bool IsNumber(object v) => v is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
