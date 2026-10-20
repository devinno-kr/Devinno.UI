using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Devinno.UI.Binding;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI.Interaction;

/// <summary>DvDataGrid input — row tap (selection, <c>onrowclick</c>), hover row.</summary>
internal static class GridInteraction
{
    /// <summary>The row index at the point (box Rect coordinates). Header, scrollbar area, below the last row = -1.</summary>
    public static int RowAt(LayoutBox box, SKPoint local)
    {
        if (box.Grid is not { } g)
            return -1;
        var r = box.Rect;
        if (local.X < r.Left || local.X >= r.Right - (g.OverflowY ? ScrollBars.Gutter : 0))
            return -1;
        GridSorter.Ensure(g);
        return GridGeometry.HitRow(local.Y - r.Top, box.ScrollY, g.HeaderTotal, g.BodyHeight, g.RowHeight, g.ViewCount);
    }

    /// <summary>The header column at the point (group cell, outside the header, scrollbar area = -1). Frozen columns are unaffected by horizontal scrolling.</summary>
    public static int HeaderColumnAt(LayoutBox box, SKPoint local)
    {
        if (box.Grid is not { } g)
            return -1;
        var r = box.Rect;
        var y = local.Y - r.Top;
        if (y < 0 || y >= g.HeaderBand || local.X < r.Left || local.X >= r.Right - (g.OverflowY ? ScrollBars.Gutter : 0))
            return -1;

        var c = ColumnAtX(box, local.X);
        return c >= 0 && (int)(y / g.HeaderHeight) >= g.Header.DepthOf(c) ? c : -1;
    }

    /// <summary>The column index if the point is on a filter cell of the filter row (a column that uses a filter); otherwise -1. Disabled grid = -1.</summary>
    public static int FilterColumnAt(LayoutBox box, SKPoint local)
    {
        if (box.Grid is not { HasFilterRow: true } g || box.Disabled)
            return -1;
        var r = box.Rect;
        var y = local.Y - r.Top;
        if (y < g.HeaderBand || y >= g.HeaderTotal || local.X < r.Left || local.X >= r.Right - (g.OverflowY ? ScrollBars.Gutter : 0))
            return -1;
        var c = ColumnAtX(box, local.X);
        return c >= 0 && g.Columns[c].Filter != GridFilterSetting.None ? c : -1;
    }

    /// <summary>The column at the point's x (box Rect coordinates) — frozen columns are unaffected by horizontal scrolling. Outside the columns = -1.</summary>
    public static int ColumnAtX(LayoutBox box, float localX)
    {
        if (box.Grid is not { } g)
            return -1;
        var x = localX - box.Rect.Left;
        return x < g.FrozenWidth ? Find(g, 0, g.FrozenCount, x) : Find(g, g.FrozenCount, g.Columns.Length, x + box.ScrollX);

        static int Find(GridSpec g, int from, int to, float cx)
        {
            for (var i = from; i < to; i++)
            {
                var col = g.Columns[i];
                if (cx >= col.X && cx < col.X + col.W)
                    return i;
            }

            return -1;
        }
    }

    /// <summary>Whether the grid draws hover (only when it is selectable or has a row click).</summary>
    public static bool ShowsHover(GridSpec g) => g.Mode != GridSelectionMode.None || !string.IsNullOrEmpty(g.OnRowClick);

    /// <summary>(item, column) if the point is on an editable edit cell (input, select, datetime, color); otherwise null (a normal row tap).</summary>
    public static (object Item, int Column)? EditCellAt(LayoutBox box, SKPoint local)
    {
        if (box.Grid is not { } g || box.Disabled)
            return null;
        var row = RowAt(box, local);
        if (row < 0 || g.ItemAt(row) is not { } item)
            return null;
        var ci = ColumnAtX(box, local.X);
        return ci >= 0 && g.Columns[ci].CanEdit(item) ? (item, ci) : null;
    }

    /// <returns>True if a row was tapped.</returns>
    // owner = 앱 코드 예외(setter·처리기)를 알릴 서피스(HandlerError). null 이면 Trace 만.
    public static bool Click(LayoutBox box, SKPoint local, object? vm, DvSurface? owner = null)
    {
        if (box.Grid is not { } g || box.Disabled)
            return false;
        if (HeaderColumnAt(box, local) is >= 0 and var column)
            return GridSorter.Toggle(g, column);
        var row = RowAt(box, local);
        if (row < 0 || g.ItemAt(row) is not { } item)
            return false;

        // 즉시 셀 — 그 동작만(행 선택·onrowclick 없음)
        if (ColumnAtX(box, local.X) is >= 0 and var ci)
        {
            var col = g.Columns[ci];
            if (col.CellType == GridCellType.Check)
            {
                if (vm is not null || col.OnBeforeCommit is null)
                    CommitCheck(col, item, vm, owner, box.Node.Tag);
                return true;
            }

            if (col.CellType == GridCellType.Button)
            {
                if (vm is not null && col.OnClick is { Length: > 0 } m && (col.Enabled is null || col.Enabled.Get(item) is not false))
                    InvokeRowClick(vm, m, item, owner, "onclick", box.Node.Tag);
                return true;
            }
        }

        if (g.Mode != GridSelectionMode.None)
        {
            var before = g.SelectedItems.ToList();
            g.SetSelection(Next(g, item));
            if (!Write(box, g, vm, owner))
                g.SetSelection(before); // setter 예외 = 이전 선택 유지
        }

        if (vm is not null && g.OnRowClick is { Length: > 0 } method)
            InvokeRowClick(vm, method, item, owner, "onrowclick", box.Node.Tag);
        return true;
    }

    // single = 탭한 행만(다시 탭해도 유지), multi = 토글
    private static List<object> Next(GridSpec g, object item)
    {
        if (g.Mode == GridSelectionMode.Single)
            return new List<object> { item };

        if (!g.IsSelected(item))
            return new List<object>(g.SelectedItems) { item };

        var key = g.KeyOf(item);
        var keep = new List<object>(g.SelectedItems.Count);
        foreach (var it in g.SelectedItems)
        {
            var k = g.KeyOf(it);
            if (g.RowKey is null ? !ReferenceEquals(k, key) : !Equals(k, key))
                keep.Add(it);
        }

        return keep;
    }

    // 양방향이면 새 스냅샷을 VM 에 — 이미 쓴 목록은 이후 바뀌지 않는다(설계서 OnBatch 스냅샷 계약 대체).
    // false = setter 예외로 쓰지 못함(알림은 TrySet 이).
    internal static bool Write(LayoutBox box, GridSpec g, object? vm, DvSurface? owner)
    {
        if (vm is null || !BindingResolver.IsTwoWay(g.SelectionExpr))
            return true;

        var ctx = (object?)box.ItemScope ?? vm;
        var path = BindingResolver.ExtractPath(g.SelectionExpr!);
        var (ok, value) = Snapshot(BindingResolver.ResolvePropertyType(ctx, path), g.SelectedItems);
        if (ok)
            return HandlerErrors.TrySet(owner, box.Node.Tag, ctx, path, value);
        Trace.TraceWarning($"Devinno.UI: DvDataGrid selection '{path}' — 지원하지 않는 속성 타입(T[]·List<T>·IList<T>·IReadOnlyList<T>·IEnumerable<T>, single 은 T)");
        return true;
    }

    /// <summary>Selection list → a new value of the target property type. ok = false for unsupported types.</summary>
    public static (bool ok, object? value) Snapshot(Type? target, IReadOnlyList<object> items)
    {
        if (target is null)
            return (false, null);
        target = Nullable.GetUnderlyingType(target) ?? target;

        if (target == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(target))
        {
            // 항목형 속성(single) — 첫 항목 또는 null
            var one = items.Count > 0 ? items[0] : null;
            return one is null ? (!target.IsValueType, null) : target.IsInstanceOfType(one) ? (true, one) : (false, null);
        }

        var elem = ElementType(target);
        if (target.IsArray)
        {
            var arr = Array.CreateInstance(elem, items.Count(elem.IsInstanceOfType));
            var n = 0;
            foreach (var it in items)
            {
                if (elem.IsInstanceOfType(it))
                    arr.SetValue(it, n++);
            }

            return (true, arr);
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elem))!;
        foreach (var it in items)
        {
            if (elem.IsInstanceOfType(it))
                list.Add(it);
        }

        return target.IsInstanceOfType(list) ? (true, list) : (false, null);
    }

    internal static Type ElementType(Type t)
    {
        if (t.IsArray)
            return t.GetElementType()!;
        var en = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? t
            : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return en?.GetGenericArguments()[0] ?? typeof(object);
    }

    private static readonly HashSet<(Type, string)> NonBoolWarned = new();

    // 체크 반전 → onbeforecommit(항목, bool) 또는 (bool) 확인 → 항목 속성에 바로 씀. bool 아닌 속성·readonly = 쓰지 않음
    private static void CommitCheck(GridColumn col, object item, object? vm, DvSurface? owner, string tag)
    {
        if (col.ReadOnly || col.Reader.Path.Length == 0)
            return;
        var current = col.Reader.Get(item);
        if (col.Reader.ValueType != typeof(bool) || !col.Reader.Writable)
        {
            lock (NonBoolWarned)
            {
                if (NonBoolWarned.Add((item.GetType(), col.Reader.Path)))
                    Trace.TraceWarning($"Devinno.UI: DvColumn cell-type=\"check\" field '{col.Reader.Path}' 가 쓸 수 있는 bool 속성이 아님 — 쓰지 않음");
            }

            return;
        }

        var proposed = current is not true;
        if (vm is not null && col.OnBeforeCommit is { Length: > 0 } gate)
        {
            var methods = vm.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == gate).ToList();
            var withItem = methods.FirstOrDefault(m => m.GetParameters() is { Length: 2 } ps &&
                                                        ps[0].ParameterType.IsInstanceOfType(item) && ps[1].ParameterType == typeof(bool));
            var boolOnly = methods.FirstOrDefault(m => m.GetParameters() is { Length: 1 } ps && ps[0].ParameterType == typeof(bool));
            var target = withItem ?? boolOnly;
            if (target is null)
            {
                // 이름 오타·시그니처 불일치 — 확인을 건너뛰고 쓰지 않는다(확인 없이 쓰면 위험)
                Trace.TraceWarning($"Devinno.UI: onbeforecommit '{gate}' — bool {gate}(항목, bool) 또는 bool {gate}(bool) 없음, 쓰지 않음");
                return;
            }

            object? result;
            try
            {
                result = target.Invoke(vm, withItem is not null ? new[] { item, proposed } : new object[] { proposed });
            }
            catch (TargetInvocationException ex)
            {
                HandlerErrors.Report(owner, ex, "onbeforecommit", gate, tag); // 거절로 처리
                return;
            }

            if (result is not true)
                return; // false 또는 bool 아님(void 등) = 쓰지 않음
        }

        HandlerErrors.TrySet(owner, tag, item, col.Reader.Path, proposed); // setter 예외 = 쓰지 않음(알림은 TrySet 이)
    }

    // DvRepeat onclick 과 같은 규칙: M(항목형) 우선, 없으면 M(). kind = "onrowclick" 또는 button 셀 "onclick"
    private static void InvokeRowClick(object vm, string method, object item, DvSurface? owner, string kind, string tag)
    {
        var withItem = vm.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m =>
            m.Name == method && m.GetParameters() is { Length: 1 } ps && ps[0].ParameterType.IsInstanceOfType(item));
        try
        {
            if (withItem is not null)
            {
                withItem.Invoke(vm, new[] { item });
                return;
            }

            vm.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)?.Invoke(vm, null);
        }
        catch (TargetInvocationException ex)
        {
            // 앱 처리기 예외가 입력 경로를 끊지 않게(눌림 상태가 남는 것 방지)
            HandlerErrors.Report(owner, ex, kind, method, tag);
        }
    }
}
