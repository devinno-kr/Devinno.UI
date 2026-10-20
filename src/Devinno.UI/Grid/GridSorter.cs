using System.Collections;
using System.Runtime.CompilerServices;

namespace Devinno.UI.Grid;

/// <summary>One sort column — its priority is its position in <see cref="GridSpec.Sort"/> (the order tapped).</summary>
internal readonly record struct GridSortKey(int Column, bool Desc);

/// <summary>
/// Grid-internal sorting — leaves the source list as is and builds only the row order <c>int[]</c>. Keys are read once per column (accessor),
/// and equal keys keep the source order (stable). Values are not re-read through reflection on every update.
/// </summary>
internal static class GridSorter
{
    /// <summary>Header tap — none → ascending → descending → none. A newly sorted column goes last in priority. Returns false for a column that cannot be sorted.</summary>
    public static bool Toggle(GridSpec g, int column)
    {
        if ((uint)column >= (uint)g.Columns.Length || !g.Columns[column].Sortable)
            return false;

        var i = g.Sort.FindIndex(k => k.Column == column);
        if (i < 0)
            g.Sort.Add(new GridSortKey(column, false));
        else if (!g.Sort[i].Desc)
            g.Sort[i] = g.Sort[i] with { Desc = true };
        else
            g.Sort.RemoveAt(i);
        g.SortVersion++;
        return true;
    }

    /// <summary>
    /// Rebuilds the order if it does not match the current state (sort columns, list reference, row count). Does not re-sort when only values changed (so rows do not jump).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)] // 키 추출 루프 — 첫 정렬부터 최적화 코드
    public static void Ensure(GridSpec g)
    {
        var items = g.Items;
        var filtering = g.Filters.Count > 0;
        if (items is null || (g.Sort.Count == 0 && !filtering))
        {
            g.Order = null;
            g.FilterDirty = false;
            return;
        }

        var n = items.Count;
        if (g.Order is not null && !(filtering && g.FilterDirty) && g.OrderCount == n && ReferenceEquals(g.OrderItems, items) &&
            g.OrderVersion == g.SortVersion && g.OrderFilterVersion == g.FilterVersion)
            return;

        var order = filtering ? FilterRows(g, items, n) : Identity(n);
        if (g.Sort.Count > 0 && order.Length > 1)
        {
            var keys = new SortKeys[g.Sort.Count];
            for (var k = 0; k < keys.Length; k++)
                keys[k] = SortKeys.Build(items, n, g.Columns[g.Sort[k].Column].Reader, g.Sort[k].Desc);
            // 정렬 열 하나 + 타입이 정해진 키 = 기본형 키 정렬(비교 대리자 없음), 그 외 = 비교자 정렬
            if (keys.Length != 1 || !keys[0].TrySortSingle(order))
                Array.Sort(order, new OrderComparer(keys));
        }

        var refiltered = filtering; // 순서를 다시 만들 때마다 현재 값으로 걸렀다(정렬 헤더 탭 포함)
        g.Order = order;
        g.OrderItems = items;
        g.OrderVersion = g.SortVersion;
        g.OrderFilterVersion = g.FilterVersion;
        g.OrderCount = n;
        g.FilterDirty = false;
        if (refiltered)
        {
            g.SummaryDirty = true;
            PruneSelection(g, items, order);
        }
    }

    private static int[] Identity(int n)
    {
        var o = new int[n];
        for (var i = 0; i < n; i++)
            o[i] = i;
        return o;
    }

    // 보이는 원본 인덱스(원본 순서). 편집 중인 셀의 행은 조건과 무관하게 남긴다.
    private static int[] FilterRows(GridSpec g, IList items, int n)
    {
        var conds = new List<(GridColumn Col, GridFilterKind Kind, GridFilterCondition Cond)>(g.Filters.Count);
        foreach (var (ci, c) in g.Filters)
        {
            if ((uint)ci < (uint)g.Columns.Length && g.Columns[ci].FilterKind is var k and not GridFilterKind.None && GridFilter.IsActive(k, c))
                conds.Add((g.Columns[ci], k, c));
        }

        var pinned = g.EditMark?.Key;
        var buf = new int[n];
        var m = 0;
        for (var i = 0; i < n && i < items.Count; i++)
        {
            if (items[i] is not { } it)
                continue;
            var keep = pinned is not null && g.IsKey(it, pinned);
            if (!keep)
            {
                keep = true;
                foreach (var (col, kind, cond) in conds)
                {
                    if (!GridFilter.Matches(col, kind, cond, it))
                    {
                        keep = false;
                        break;
                    }
                }
            }

            if (keep)
                buf[m++] = i;
        }

        Array.Resize(ref buf, m);
        return buf;
    }

    // 다시 거르기로 숨겨진 행은 선택에서 뺀다
    private static void PruneSelection(GridSpec g, IList items, int[] order)
    {
        if (!g.HasSelection)
            return;
        var visible = g.RowKey is null ? new HashSet<object>(ReferenceEqualityComparer.Instance) : new HashSet<object>();
        foreach (var i in order)
        {
            if (i < items.Count && items[i] is { } it)
                visible.Add(g.KeyOf(it));
        }

        var kept = g.SelectedItems.Where(it => visible.Contains(g.KeyOf(it))).ToList();
        if (kept.Count == g.SelectedItems.Count)
            return;
        g.SetSelection(kept);
        g.SelectionPruned = true;
    }

    /// <summary>
    /// Total-order comparison — null is the smallest, and when types are mixed they are first split by rank (number &lt; string &lt; other comparable &lt; the rest)
    /// (Array.Sort throws on inconsistent comparisons). Strings use ordinal comparison (fast, and matches Korean alphabetical (가나다) order).
    /// </summary>
    internal static int Compare(object? a, object? b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a is null)
            return -1;
        if (b is null)
            return 1;
        if (a is string sa && b is string sb)
            return string.CompareOrdinal(sa, sb);

        var ta = a.GetType();
        if (ta == b.GetType() && a is IComparable same)
            return same.CompareTo(b);

        var ra = Rank(a);
        var rb = Rank(b);
        if (ra != rb)
            return ra.CompareTo(rb);
        return ra switch
        {
            0 => Convert.ToDouble(a).CompareTo(Convert.ToDouble(b)),
            2 => string.CompareOrdinal(ta.FullName, b.GetType().FullName),
            _ => string.CompareOrdinal(a.ToString(), b.ToString())
        };
    }

    // 정렬 열들을 우선순위대로 비교, 같으면 원본 인덱스(안정)
    private sealed class OrderComparer(SortKeys[] keys) : IComparer<int>
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)] // 첫 정렬부터 최적화 코드(티어 0 비교 루프는 2~3배 느림)
        public int Compare(int a, int b)
        {
            foreach (var k in keys)
            {
                var c = k.Compare(a, b);
                if (c != 0)
                    return k.Desc ? -c : c;
            }

            return a.CompareTo(b);
        }
    }

    /// <summary>
    /// The keys of one sort column — if all column values are integer-like (including dates and times) they are extracted to <c>long[]</c>, numbers to <c>double[]</c>, strings to <c>string[]</c>,
    /// and compared without boxing or interface calls. If mixed, <see cref="GridSorter.Compare"/> (total order) is used.
    /// </summary>
    private abstract class SortKeys
    {
        public bool Desc { get; private set; }

        public abstract int Compare(int a, int b);

        /// <summary>Sorts by this single column (same result as comparer sorting: null is the smallest, equal keys keep the source order). Returns false if it cannot.</summary>
        public virtual bool TrySortSingle(int[] order) => false;

        /// <summary>
        /// Only non-null rows go through <c>Array.Sort(keys, index)</c> on the key array (primitive sort — unstable) → reversed if descending → runs of equal keys are
        /// re-sorted into source order to restore stability. Null rows go first when ascending and last when descending (in source order).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        protected bool SortTyped<T>(int[] order, Func<int, T> key, Func<int, bool> isNull, IComparer<T>? comparer, Func<T, T, bool> equal)
        {
            var nulls = new List<int>();
            var m = 0;
            foreach (var i in order)
            {
                if (isNull(i))
                    nulls.Add(i);
                else
                    m++;
            }

            var kv = new T[m];
            var ov = new int[m];
            var w = 0;
            foreach (var i in order)
            {
                if (isNull(i))
                    continue;
                kv[w] = key(i);
                ov[w++] = i;
            }

            Array.Sort(kv, ov, comparer);
            if (Desc)
            {
                Array.Reverse(kv);
                Array.Reverse(ov);
            }

            for (var i = 0; i < m;)
            {
                var j = i + 1;
                while (j < m && equal(kv[i], kv[j]))
                    j++;
                if (j - i > 1)
                    Array.Sort(ov, i, j - i);
                i = j;
            }

            var p = 0;
            if (!Desc)
                foreach (var i in nulls)
                    order[p++] = i;
            foreach (var i in ov)
                order[p++] = i;
            if (Desc)
                foreach (var i in nulls)
                    order[p++] = i;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static SortKeys Build(IList items, int n, FieldReader reader, bool desc)
        {
            var raw = new object?[n];
            var kinds = 0;
            for (var i = 0; i < n; i++)
            {
                // 읽는 중 목록이 줄었으면(다른 스레드) 남은 자리는 null — 예외 없이 다음 행 수 변경 때 다시 정렬된다
                var v = i < items.Count && items[i] is { } it ? reader.Get(it) : null;
                raw[i] = v;
                if (v is not null)
                    kinds |= Kind(v);
            }

            // 한 종류(또는 정수·실수 숫자만)일 때만 기본형 키 — DateTime 과 int 처럼 섞이면 전순서 비교(타입 순위)로
            const int integral = 1, floating = 2;
            var ints = kinds is 0 or integral or KDate or KSpan or KBool;
            var nums = (kinds & ~(integral | floating)) == 0;
            var strs = kinds == KString;

            SortKeys keys;
            if (strs)
            {
                var s = new string?[n];
                for (var i = 0; i < n; i++)
                    s[i] = (string?)raw[i];
                keys = new StringKeys(s);
            }
            else if (ints)
            {
                var l = new long[n];
                var nul = new bool[n];
                for (var i = 0; i < n; i++)
                {
                    switch (raw[i])
                    {
                        case null: nul[i] = true; break;
                        case DateTime dt: l[i] = dt.Ticks; break;
                        case TimeSpan ts: l[i] = ts.Ticks; break;
                        case bool bo: l[i] = bo ? 1 : 0; break;
                        case var v: l[i] = Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture); break;
                    }
                }

                keys = new LongKeys(l, nul);
            }
            else if (nums)
            {
                var d = new double[n];
                var nul = new bool[n];
                for (var i = 0; i < n; i++)
                {
                    if (raw[i] is { } v)
                        d[i] = Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
                    else
                        nul[i] = true;
                }

                keys = new DoubleKeys(d, nul);
            }
            else
                keys = new ObjectKeys(raw, reader.Path);

            keys.Desc = desc;
            return keys;
        }

        private const int KDate = 4, KSpan = 8, KBool = 16, KString = 32;

        // 정수(1)·실수(2)·DateTime·TimeSpan·bool·string, 그 외(decimal·ulong·enum 등 — double 로 바꾸면 정밀도를 잃음) = 64
        private static int Kind(object v) => v switch
        {
            int or long or short or sbyte or byte or ushort or uint => 1,
            double or float => 2,
            DateTime => KDate,
            TimeSpan => KSpan,
            bool => KBool,
            string => KString,
            _ => 64
        };
    }

    private sealed class LongKeys(long[] v, bool[] nul) : SortKeys
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override int Compare(int a, int b)
            => nul[a] || nul[b] ? nul[b].CompareTo(nul[a]) : v[a].CompareTo(v[b]);

        public override bool TrySortSingle(int[] order) => SortTyped(order, i => v[i], i => nul[i], null, (x, y) => x == y);
    }

    private sealed class DoubleKeys(double[] v, bool[] nul) : SortKeys
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override int Compare(int a, int b)
            => nul[a] || nul[b] ? nul[b].CompareTo(nul[a]) : v[a].CompareTo(v[b]);

        public override bool TrySortSingle(int[] order) => SortTyped(order, i => v[i], i => nul[i], null, (x, y) => x.CompareTo(y) == 0);
    }

    private sealed class StringKeys(string?[] v) : SortKeys
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override int Compare(int a, int b) => string.CompareOrdinal(v[a], v[b]);

        public override bool TrySortSingle(int[] order)
            => SortTyped(order, i => v[i]!, i => v[i] is null, StringComparer.Ordinal, string.Equals);
    }

    // 그 외 타입 — 비교에 VM 코드(IComparable.CompareTo·ToString)가 끼므로 예외는 알리고 같음(0)으로 본다(순서는 정해지지 않음)
    private sealed class ObjectKeys(object?[] v, string path) : SortKeys
    {
        public override int Compare(int a, int b)
        {
            try
            {
                return GridSorter.Compare(v[a], v[b]);
            }
            catch (Exception ex)
            {
                Interaction.HandlerErrors.ReportRead(ex, "compare", path, "DvDataGrid");
                return 0;
            }
        }
    }

    private static int Rank(object v) => v switch
    {
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => 0,
        string => 1,
        IComparable => 2,
        _ => 3
    };
}
