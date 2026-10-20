using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Interaction;
using Devinno.UI.Markup;
using Devinno.UI.Styling;

namespace Devinno.UI.Grid;

/// <summary><c>&lt;DvDataGrid&gt;</c> + <c>&lt;DvColumn&gt;</c> children → <see cref="GridSpec"/>. O(columns + selection count), independent of the row count.</summary>
internal static class GridBuilder
{
    private static readonly HashSet<string> FilterWarned = new(); // 알 수 없는 filter 값 — 값마다 경고 1회

    private static GridFilterSetting ParseFilter(string? raw, string? field)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case null or "" or "none": return GridFilterSetting.None;
            case "auto": return GridFilterSetting.Auto;
            case "text": return GridFilterSetting.Text;
            case "values": return GridFilterSetting.Values;
            case "range": return GridFilterSetting.Range;
            case "date": return GridFilterSetting.Date;
        }

        lock (FilterWarned)
        {
            if (FilterWarned.Add(raw!))
                Trace.TraceWarning($"Devinno.UI: DvColumn field '{field}' filter=\"{raw}\" — none·auto·text·values·range·date 중 하나가 아님, 필터 없음");
        }

        return GridFilterSetting.None;
    }

    private static readonly HashSet<(string, string)> NumWarned = new(); // 해석 못 한 min·max — (속성, 값)마다 경고 1회

    public static GridSpec Build(ElementNode node, object? vm, DvTheme? theme = null, Func<string, GridCellStyle?>? classStyle = null)
    {
        string? Attr(string name) => node.Attributes.GetValueOrDefault(name);
        object? Val(string name) => BindingResolver.ResolveValueOrLiteral(vm, Attr(name));

        var items = Items(Val("items"), Attr("items"));
        var first = FirstNonNull(items);

        var cols = new List<GridColumn>();
        var depths = new List<int>();
        var groups = new List<GridHeaderGroup>();
        var maxDepth = 0;
        Collect(node, 0);

        // DvColumn·DvColumnGroup 을 문서 순서로 펼친다 — 그룹은 감싼 열 범위·단계로
        void Collect(ElementNode parent, int depth)
        {
            foreach (var ch in parent.Children)
            {
                if (ch.Tag == "DvColumn")
                {
                    cols.Add(Column(ch));
                    depths.Add(depth);
                    maxDepth = Math.Max(maxDepth, depth);
                }
                else if (ch.Tag == "DvColumnGroup")
                {
                    var start = cols.Count;
                    Collect(ch, depth + 1);
                    if (cols.Count > start)
                        groups.Add(new GridHeaderGroup(BindingResolver.ResolveInline(vm, ch.Attributes.GetValueOrDefault("header")) ?? "",
                            depth, start, cols.Count - 1));
                }
            }
        }

        GridColumn Column(ElementNode ch)
        {
            string? CA(string name) => ch.Attributes.GetValueOrDefault(name);
            double? Num(string name)
            {
                if (BindingResolver.ResolveValueOrLiteral(vm, CA(name)) is not { } v)
                    return null;
                var text = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
                lock (NumWarned)
                {
                    if (NumWarned.Add((name, text)))
                        Trace.TraceWarning($"Devinno.UI: DvColumn field '{CA("field")}' {name}=\"{text}\" — 숫자로 해석 못 함, 무시");
                }

                return null;
            }
            var cellType = CA("cell-type")?.Trim().ToLowerInvariant() switch
            {
                "check" => GridCellType.Check,
                "lamp" => GridCellType.Lamp,
                "button" => GridCellType.Button,
                "input" => GridCellType.Input,
                "select" => GridCellType.Select,
                "datetime" => GridCellType.DateTime,
                "color" => GridCellType.Color,
                _ => GridCellType.Text
            };
            var col = new GridColumn
            {
                Header = BindingResolver.ResolveInline(vm, CA("header")) ?? "",
                Reader = new FieldReader(CA("field") ?? ""),
                Width = GridWidth.Parse(BindingResolver.ResolveText(vm, CA("width"))),
                Align = CA("align")?.Trim().ToLowerInvariant() switch
                {
                    "left" => TextAlign.Left,
                    "center" => TextAlign.Center,
                    "right" => TextAlign.Right,
                    _ => null
                },
                Format = CA("format"),
                Frozen = IsTrue(BindingResolver.ResolveValueOrLiteral(vm, CA("frozen"))),
                Sortable = IsTrue(BindingResolver.ResolveValueOrLiteral(vm, CA("sortable"))),
                CellClass = CA("cell-class") is { Length: > 0 } cc ? new FieldReader(cc) : null,
                CellType = cellType,
                ReadOnly = IsTrue(BindingResolver.ResolveValueOrLiteral(vm, CA("readonly"))),
                OnBeforeCommit = CA("onbeforecommit"),
                OnColor = Color(CA("on-color")),
                Blink = IsTrue(BindingResolver.ResolveValueOrLiteral(vm, CA("blink"))),
                ButtonText = BindingResolver.ResolveInline(vm, CA("text")) ?? "",
                Icon = CA("icon"),
                Variant = CA("variant")?.Trim().ToLowerInvariant() is { Length: > 0 } variant ? variant : "primary",
                OnClick = CA("onclick"),
                Filter = ParseFilter(BindingResolver.ResolveText(vm, CA("filter")), CA("field")),
                Enabled = CA("enabled") is { Length: > 0 } en ? new FieldReader(en) : null,
                Min = Num("min"),
                Max = Num("max"),
                Unit = BindingResolver.ResolveInline(vm, CA("unit")),
                Mode = BindingResolver.ResolveText(vm, CA("mode")),
                ShowSeconds = IsTrue(BindingResolver.ResolveValueOrLiteral(vm, CA("show-seconds"))),
                OptionsNode = cellType == GridCellType.Select ? ch : null,
                OptionsContext = cellType == GridCellType.Select ? vm : null,
                InitialSortDesc = CA("sort")?.Trim().ToLowerInvariant() switch
                {
                    "asc" => false,
                    "desc" => true,
                    _ => null
                }
            };
            if (first is not null && col.Reader.Path.Length > 0)
                col.Reader.Get(first); // 값 타입(기본 정렬)을 첫 그리기 전에 확정
            if (col.CellType == GridCellType.Select)
                _ = col.Options; // 레이아웃 중(다국어 @키 해석 가능)에 항목을 만들어 둔다
            return col;
        }

        DvColor? Color(string? raw)
        {
            if (BindingResolver.ResolveText(vm, raw) is not { Length: > 0 } text)
                return null;
            var resolved = ThemeResolver.ResolveVars(text, theme);
            if (resolved is not null && !resolved.Contains(ThemeResolver.MissingColor, StringComparison.OrdinalIgnoreCase) &&
                DvColor.TryParse(resolved, out var c))
                return c;
            Trace.TraceWarning($"Devinno.UI: DvColumn on-color=\"{text}\" — 색으로 해석 못 함, 기본 색 사용");
            return null;
        }

        var frozen = 0;
        while (frozen < cols.Count && cols[frozen].Frozen)
            frozen++;
        if (cols.Skip(frozen).Any(c => c.Frozen))
            Trace.TraceWarning("Devinno.UI: DvDataGrid frozen 은 앞쪽부터 연속인 열만 적용 — 중간 열의 frozen 무시");

        // 고정 열과 스크롤 열에 걸친 그룹은 경계에서 둘로(각 영역에서 따로 그려진다)
        var header = GridHeader.Single;
        if (groups.Count > 0)
        {
            var split = new List<GridHeaderGroup>();
            foreach (var gr in groups)
            {
                if (gr.First < frozen && gr.Last >= frozen)
                {
                    Trace.TraceWarning($"Devinno.UI: DvColumnGroup '{gr.Header}' 가 고정 열 경계에 걸침 — 둘로 나뉘어 그려짐");
                    split.Add(gr with { Last = frozen - 1 });
                    split.Add(gr with { First = frozen });
                }
                else
                    split.Add(gr);
            }

            header = new GridHeader(maxDepth + 1, split, depths.ToArray());
        }

        // 요약 행 — DvDataGrid 직속 DvSummaryRow, 칸은 field 로 열을 찾는다
        var summaries = new List<GridSummaryRow>();
        foreach (var ch in node.Children)
        {
            if (ch.Tag != "DvSummaryRow")
                continue;
            var cells = new Dictionary<int, GridSummaryCell>();
            foreach (var sc in ch.Children)
            {
                if (sc.Tag != "DvSummary")
                    continue;
                var field = sc.Attributes.GetValueOrDefault("field") ?? "";
                var ci = cols.FindIndex(c => c.Reader.Path == field);
                if (ci < 0)
                {
                    Trace.TraceWarning($"Devinno.UI: DvSummary field '{field}' 에 해당하는 열 없음 — 무시");
                    continue;
                }

                var hasValue = sc.Attributes.TryGetValue("value", out var rawValue);
                cells[ci] = new GridSummaryCell(GridSummary.Parse(sc.Attributes.GetValueOrDefault("fn")),
                    sc.Attributes.GetValueOrDefault("format"), hasValue,
                    hasValue ? BindingResolver.ResolveValueOrLiteral(vm, rawValue) : null);
            }

            summaries.Add(new GridSummaryRow
            {
                Title = BindingResolver.ResolveInline(vm, ch.Attributes.GetValueOrDefault("title")) ?? "",
                Fn = GridSummary.Parse(ch.Attributes.GetValueOrDefault("fn")),
                Cells = cells
            });
        }

        var key = Attr("row-key") is { Length: > 0 } k ? new FieldReader(k) : null;
        var selExpr = Attr("selection");
        var selSource = selExpr is null ? null : SelectionValue(vm, selExpr);
        var spec = new GridSpec
        {
            Items = items,
            Columns = cols.ToArray(),
            FrozenCount = frozen,
            Header = header,
            Summaries = summaries,
            RowHeight = Positive(Val("row-height"), 48f),
            HeaderHeight = Positive(Val("header-height"), 40f),
            Mode = BindingResolver.ToText(Val("selection-mode"), Attr("selection-mode"))?.Trim().ToLowerInvariant() switch
            {
                "single" => GridSelectionMode.Single,
                "multi" => GridSelectionMode.Multi,
                _ => GridSelectionMode.None
            },
            SelectionExpr = selExpr,
            SelectionSource = selSource,
            RowKey = key,
            RowClass = Attr("row-class") is { Length: > 0 } rc ? new FieldReader(rc) : null,
            Colors = GridColors.Parse(node, vm, theme),
            ClassStyle = classStyle,
            OnRowClick = Attr("onrowclick")
        };

        // 초기 정렬 — 열 순서가 우선순위(이후 탭은 화면 상태로 재빌드 때 옮겨진다)
        for (var c = 0; c < cols.Count; c++)
        {
            if (cols[c].InitialSortDesc is { } d)
                spec.Sort.Add(new GridSortKey(c, d));
        }

        if (spec.Mode != GridSelectionMode.None && selSource is not null)
            spec.SetSelection(Selected(selSource, selExpr!));
        return spec;
    }

    private static IList? Items(object? v, string? raw)
    {
        switch (v)
        {
            case null or string:
                return null;
            case IList list:
                return list;
            case IEnumerable seq:
                Trace.TraceWarning("Devinno.UI: DvDataGrid items 가 IList 가 아님 — 레이아웃마다 목록으로 복사(IList 권장)");
                return HandlerErrors.Snapshot(seq.Cast<object?>(), Member(raw), "DvDataGrid"); // 열거 중 VM 예외 = 빈 목록
            default:
                return null;
        }
    }

    private static object? FirstNonNull(IList? items)
    {
        if (items is null)
            return null;
        for (var i = 0; i < items.Count && i < 64; i++)
        {
            if (items[i] is { } it)
                return it;
        }

        return null;
    }

    private static object? SelectionValue(object? vm, string expr)
        => BindingResolver.IsTwoWay(expr)
            ? BindingResolver.Resolve(vm, "{" + BindingResolver.ExtractPath(expr) + "}")
            : BindingResolver.ResolveValueOrLiteral(vm, expr);

    private static IEnumerable<object> Selected(object v, string expr)
    {
        return v switch
        {
            null or string => Array.Empty<object>(),
            IEnumerable seq => HandlerErrors.Snapshot(seq.Cast<object?>().OfType<object>(), Member(expr), "DvDataGrid"),
            _ => new[] { v }
        };
    }

    private static string? Member(string? raw) => raw is not null && BindingResolver.IsBinding(raw) ? BindingResolver.ExtractPath(raw) : raw;

    private static float Positive(object? v, float fallback)
        => v is not null && float.TryParse(v is IConvertible ? Convert.ToString(v, CultureInfo.InvariantCulture) : HandlerErrors.Text(v, "row-height"),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0 ? f : fallback; // VM 형식의 ToString 예외 = 기본값

    private static bool IsTrue(object? v) => v is bool b ? b : string.Equals(BindingResolver.ToText(v, null), "true", StringComparison.OrdinalIgnoreCase);
}
