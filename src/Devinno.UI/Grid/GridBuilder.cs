using System.Collections;
using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;

namespace Devinno.UI.Grid;

/// <summary><c>&lt;DvDataGrid&gt;</c> + <c>&lt;DvColumn&gt;</c> children → <see cref="GridSpec"/>. O(columns + selection count), independent of the row count.</summary>
internal static class GridBuilder
{

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

        Warnings.Warn($"Devinno.UI: DvColumn field '{field}' filter=\"{raw}\": not one of none, auto, text, values, range, date; no filter");

        return GridFilterSetting.None;
    }

    /// <summary>Builds the grid of <c>r.Node</c>; every attribute (the grid's and each child's) is read through an <see cref="AttrReader"/>.</summary>
    public static GridSpec Build(AttrReader r, DvTheme? theme = null, Func<string, GridCellStyle?>? classStyle = null)
    {
        var node = r.Node;
        var vm = r.Context;

        var items = Items(r.Value("items"), r.Raw("items"));
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
                    // 자식 노드는 그 노드의 리더로(그 노드 핸들의 덮어쓰기를 함께 본다)
                    if (cols.Count > start)
                        groups.Add(new GridHeaderGroup(BindingResolver.ResolveInline(vm, r.Child(ch).Raw("header")) ?? "",
                            depth, start, cols.Count - 1));
                }
            }
        }

        GridColumn Column(ElementNode ch)
        {
            var cr = r.Child(ch);
            string? CA(string name) => cr.Raw(name);
            double? Num(string name)
            {
                if (cr.Value(name) is not { } v)
                    return null;
                var text = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
                Warnings.Warn($"Devinno.UI: DvColumn field '{CA("field")}' {name}=\"{text}\": not a number; ignored");

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
                Width = GridWidth.Parse(cr.Text("width")),
                Align = CA("align")?.Trim().ToLowerInvariant() switch
                {
                    "left" => TextAlign.Left,
                    "center" => TextAlign.Center,
                    "right" => TextAlign.Right,
                    _ => null
                },
                Format = CA("format"),
                Frozen = IsTrue(cr.Value("frozen")),
                Sortable = IsTrue(cr.Value("sortable")),
                CellClass = CA("cell-class") is { Length: > 0 } cc ? new FieldReader(cc) : null,
                CellType = cellType,
                ReadOnly = IsTrue(cr.Value("readonly")),
                OnBeforeCommit = CA("onbeforecommit"),
                OnColor = Color(cr.Text("on-color")),
                Blink = IsTrue(cr.Value("blink")),
                ButtonText = (cr.Overridden("text") ? cr.Text("text") : BindingResolver.ResolveInline(vm, CA("text"))) ?? "", // 코드 Text 덮어쓰기는 그대로
                Icon = CA("icon"),
                Variant = CA("variant")?.Trim().ToLowerInvariant() is { Length: > 0 } variant ? variant : "primary",
                OnClick = CA("onclick"),
                Filter = ParseFilter(cr.Text("filter"), CA("field")),
                Enabled = CA("enabled") is { Length: > 0 } en ? new FieldReader(en) : null,
                Min = Num("min"),
                Max = Num("max"),
                Unit = BindingResolver.ResolveInline(vm, CA("unit")),
                Mode = cr.Text("mode"),
                ShowSeconds = IsTrue(cr.Value("show-seconds")),
                OptionsReader = cellType == GridCellType.Select ? cr : null,
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

        // on-color — 바인딩을 푼 글자(리더의 Text)
        DvColor? Color(string? resolvedText)
        {
            if (resolvedText is not { Length: > 0 } text)
                return null;
            var resolved = ThemeResolver.ResolveVars(text, theme);
            if (resolved is not null && !resolved.Contains(ThemeResolver.MissingColor, StringComparison.OrdinalIgnoreCase) &&
                DvColor.TryParse(resolved, out var c))
                return c;
            Warnings.Warn($"Devinno.UI: DvColumn on-color=\"{text}\": not a valid color; using the default color");
            return null;
        }

        var frozen = 0;
        while (frozen < cols.Count && cols[frozen].Frozen)
            frozen++;
        if (cols.Skip(frozen).Any(c => c.Frozen))
            Warnings.Warn("Devinno.UI: DvDataGrid frozen applies only to a contiguous run of columns from the start; frozen on a middle column is ignored");

        // 고정 열과 스크롤 열에 걸친 그룹은 경계에서 둘로(각 영역에서 따로 그려진다)
        var header = GridHeader.Single;
        if (groups.Count > 0)
        {
            var split = new List<GridHeaderGroup>();
            foreach (var gr in groups)
            {
                if (gr.First < frozen && gr.Last >= frozen)
                {
                    Warnings.Warn($"Devinno.UI: DvColumnGroup '{gr.Header}' spans the frozen column boundary; it is drawn split in two");
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
                var sr = r.Child(sc);
                var field = sr.Raw("field") ?? "";
                var ci = cols.FindIndex(c => c.Reader.Path == field);
                if (ci < 0)
                {
                    Warnings.Warn($"Devinno.UI: DvSummary field '{field}': no matching column; ignored");
                    continue;
                }

                var hasValue = sr.Has("value");
                cells[ci] = new GridSummaryCell(GridSummary.Parse(sr.Raw("fn")),
                    sr.Raw("format"), hasValue,
                    hasValue ? sr.Value("value") : null);
            }

            var rr = r.Child(ch);
            summaries.Add(new GridSummaryRow
            {
                Title = BindingResolver.ResolveInline(vm, rr.Raw("title")) ?? "",
                Fn = GridSummary.Parse(rr.Raw("fn")),
                Cells = cells
            });
        }

        var key = r.Raw("row-key") is { Length: > 0 } k ? new FieldReader(k) : null;
        var selExpr = r.Raw("selection");
        var selSource = selExpr is null ? null : SelectionValue(vm, selExpr);
        var spec = new GridSpec
        {
            Items = items,
            Columns = cols.ToArray(),
            FrozenCount = frozen,
            Header = header,
            Summaries = summaries,
            RowHeight = Positive(r.Value("row-height"), 48f),
            HeaderHeight = Positive(r.Value("header-height"), 40f),
            Mode = BindingResolver.ToText(r.Value("selection-mode"), r.Raw("selection-mode"))?.Trim().ToLowerInvariant() switch
            {
                "single" => GridSelectionMode.Single,
                "multi" => GridSelectionMode.Multi,
                _ => GridSelectionMode.None
            },
            SelectionExpr = selExpr,
            SelectionSource = selSource,
            RowKey = key,
            RowClass = r.Raw("row-class") is { Length: > 0 } rc ? new FieldReader(rc) : null,
            Colors = GridColors.Parse(r, theme),
            ClassStyle = classStyle,
            OnRowClick = r.Raw("onrowclick")
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
                Warnings.Warn("Devinno.UI: DvDataGrid items is not an IList; it is copied to a list on every layout (IList recommended)");
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
