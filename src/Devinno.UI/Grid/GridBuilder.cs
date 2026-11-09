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

    // filter — 공용 목록 규칙(모르는 값 = none + 경고)
    private static GridFilterSetting ParseFilter(string? value) => value switch
    {
        "auto" => GridFilterSetting.Auto,
        "text" => GridFilterSetting.Text,
        "values" => GridFilterSetting.Values,
        "range" => GridFilterSetting.Range,
        "date" => GridFilterSetting.Date,
        _ => GridFilterSetting.None,
    };

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
            double? Num(string name) => AttrRules.NumberOrNull(cr, name);
            bool Flag(string name) => AttrRules.Bool(cr, name);
            string? Enum(string name) => AttrRules.Enum(cr, name);
            var cellType = Enum("cell-type") switch
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
            // 입력 셀 min ≥ max = 경고 + 0~100 (3단계 R6 — DvInput 과 같은 규칙)
            var (min, max) = (Num("min"), Num("max"));
            if (cellType == GridCellType.Input)
                (min, max) = AttrRules.Range(min, max, ch.Tag, AttrRules.RangeRaw(cr));
            var col = new GridColumn
            {
                Header = BindingResolver.ResolveInline(vm, CA("header")) ?? "",
                Reader = new FieldReader(CA("field") ?? ""),
                Width = GridWidth.Parse(cr.Text("width")),
                Align = Enum("align") switch
                {
                    "left" => TextAlign.Left,
                    "center" => TextAlign.Center,
                    "right" => TextAlign.Right,
                    _ => null
                },
                Format = cr.Text("format"),
                Frozen = Flag("frozen"),
                Sortable = Flag("sortable"),
                CellClass = CA("cell-class") is { Length: > 0 } cc ? new FieldReader(cc) : null,
                CellType = cellType,
                ReadOnly = Flag("readonly"),
                OnBeforeCommit = CA("onbeforecommit"),
                OnColor = AttrRules.Color(cr.Value("on-color"), theme, ch.Tag, "on-color", "using the state-run color", AttrRules.RawOf(cr, "on-color")),
                Blink = Flag("blink"),
                ButtonText = (cr.Overridden("text") ? cr.Text("text") : BindingResolver.ResolveInline(vm, CA("text"))) ?? "", // 코드 Text 덮어쓰기는 그대로
                Icon = cr.Text("icon"),
                Variant = Enum("variant") ?? "primary",
                OnClick = CA("onclick"),
                Filter = ParseFilter(Enum("filter")),
                Enabled = CA("enabled") is { Length: > 0 } en ? new FieldReader(en) : null,
                Min = min,
                Max = max,
                Unit = BindingResolver.ResolveInline(vm, CA("unit")),
                Mode = AttrRules.Enum(cr, "mode", "using the value type's mode"),
                ShowSeconds = Flag("show-seconds"),
                OptionsReader = cellType == GridCellType.Select ? cr : null,
                InitialSortDesc = Enum("sort") switch
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
                cells[ci] = new GridSummaryCell(GridSummary.Parse(AttrRules.Enum(sr, "fn", "no aggregate")),
                    sr.Text("format"), hasValue,
                    hasValue ? sr.Value("value") : null);
            }

            var rr = r.Child(ch);
            summaries.Add(new GridSummaryRow
            {
                Title = BindingResolver.ResolveInline(vm, rr.Raw("title")) ?? "",
                Fn = GridSummary.Parse(AttrRules.Enum(rr, "fn", "no aggregate")),
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
            RowHeight = Positive(r, "row-height", 48f),
            HeaderHeight = Positive(r, "header-height", 40f),
            Mode = AttrRules.Enum(r, "selection-mode") switch
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

    // px 속성 — 공용 숫자 규칙(숫자 아님 = 기본값 + 경고), 0 이하 = 기본값(조용히)
    private static float Positive(AttrReader r, string attr, float fallback)
        => AttrRules.NumberOrNull(r, attr, "using " + fallback.ToString(CultureInfo.InvariantCulture)) is > 0 and var f ? (float)f : fallback;
}
