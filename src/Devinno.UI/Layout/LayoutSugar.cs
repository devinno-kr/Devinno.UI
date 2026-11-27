using Devinno.UI.Binding;
using Devinno.UI.Markup;

namespace Devinno.UI.Layout;

/// <summary>
/// Layout sugar: expands DvStack/DvRow/DvGrid/DvSpacer into flex/grid declarations (there is no separate layout path),
/// and the grid cell attributes of any element (<c>grid-row</c> <c>grid-col</c> <c>row-span</c> <c>col-span</c>) into <c>grid-row</c>/<c>grid-column</c>.
/// Sugar declarations have the lowest cascade priority, so class/style can override them.
/// Attribute values are converted after bindings are resolved, so <c>columns="{N}"</c> and <c>gap="{G}"</c> are interpreted exactly like literals
/// (column count, spacing step name, <c>between</c>). An attribute whose value is null is treated as not specified.
/// </summary>
internal static class LayoutSugar
{
    public static string? Decls(AttrReader r)
    {
        // 그 태그가 쓰는 속성만 읽는다(TagSchema 정의와 같게): grow·width·height·grid-row·grid-col·row-span·col-span = 모든 요소, gap·padding·align·justify = 슈가 태그, columns·rows = DvGrid
        var tag = r.Node.Tag;
        var isSugar = tag is "DvStack" or "DvRepeat" or "DvRow" or "DvGrid" or "DvSpacer";
        var isShape = IsShape(tag);
        var a = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in Names)
        {
            if ((tag == "DvGrid" || !GridNames.Contains(k)) && (isSugar || !SugarNames.Contains(k)) && (isShape || !ShapeNames.Contains(k)) &&
                (k is "align" or "justify" ? (r.Has(k) ? AttrRules.Enum(r, k) : null) : r.Text(k)) is { } v) // align·justify = 공용 목록 규칙
                a[k] = v;
        }
        var parts = new List<string>();

        switch (tag)
        {
            case "DvStack":
            case "DvRepeat": // 기본은 세로 스택 — style 로 row/grid 도 가능
                parts.Add("display:flex; flex-direction:column; align-items:stretch; gap:token(spacing.md)");
                break;
            case "DvRow":
                parts.Add("display:flex; flex-direction:row; align-items:center; gap:token(spacing.md)");
                break;
            case "DvGrid":
                // columns = 개수(같은 폭) 또는 트랙 목록("1fr 2fr 25% 120 auto"), rows = 트랙 목록
                var colSpec = a.TryGetValue("columns", out var c) && !string.IsNullOrWhiteSpace(c) ? c.Trim() : "2";
                var colTracks = int.TryParse(colSpec, out var cols)
                    ? string.Join(' ', Enumerable.Repeat("1fr", cols > 0 ? cols : 2))
                    : colSpec;
                parts.Add($"display:grid; grid-template-columns:{colTracks}; gap:token(spacing.lg)");
                if (a.TryGetValue("rows", out var rowSpec) && !string.IsNullOrWhiteSpace(rowSpec))
                    parts.Add($"grid-template-rows:{rowSpec.Trim()}");
                break;
            case "DvSpacer":
                parts.Add("flex-grow:1");
                break;
        }

        // 도형의 칠·외곽선 — 기본 외곽선 다음에 속성(border 단축 → 한 쪽 키 순서라 한 쪽 키가 이긴다). 도형 태그만 읽는다(DvPolyline 은 칠이 없다)
        if (isShape)
        {
            parts.Add("border:2 solid token(text)");
            if (tag != "DvPolyline" && a.TryGetValue("background", out var fill))
                parts.Add("background:" + fill);
            if (a.TryGetValue("border", out var border))
                parts.Add("border:" + border);
            if (a.ContainsKey("border-width") && AttrRules.NumberOrNull(r, "border-width", "ignored") is { } bw)
                parts.Add("border-width:" + bw.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (a.TryGetValue("border-color", out var bc))
                parts.Add("border-color:" + bc);
            if (a.ContainsKey("border-style") && AttrRules.Enum(r, "border-style") is { } bs)
                parts.Add("border-style:" + bs);
        }

        if (isSugar)
        {
            if (a.TryGetValue("gap", out var gap))
                parts.Add("gap:" + Space(gap));
            if (a.TryGetValue("padding", out var pad))
                parts.Add("padding:" + string.Join(' ', pad.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Space)));
            if (a.TryGetValue("align", out var align))
                parts.Add("align-items:" + align);
            if (a.TryGetValue("justify", out var justify))
                parts.Add("justify-content:" + (justify == "between" ? "space-between" : justify));
        }

        // 모든 요소 공통 레이아웃 속성
        if (a.TryGetValue("grow", out var grow))
            parts.Add("flex-grow:" + grow);
        if (a.TryGetValue("width", out var w))
            parts.Add("width:" + w);
        if (a.TryGetValue("height", out var h))
            parts.Add("height:" + h);
        // grid 칸 = style grid-row·grid-column 과 같은 선언(가장 낮은 우선순위라 같은 축의 class·style 이 두 속성을 통째로 덮는다)
        if (GridLine(a, "grid-row", "row-span") is { } gr)
            parts.Add("grid-row:" + gr);
        if (GridLine(a, "grid-col", "col-span") is { } gc)
            parts.Add("grid-column:" + gc);

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    // 시작 줄·칸 수 속성 → "N / span K"(시작이 없으면 auto). 둘 다 없으면 없음
    private static string? GridLine(Dictionary<string, string> a, string startName, string spanName)
    {
        var start = a.TryGetValue(startName, out var st) ? st.Trim() : null;
        var span = a.TryGetValue(spanName, out var sp) ? sp.Trim() : null;
        if (start is null && span is null)
            return null;
        return $"{(string.IsNullOrEmpty(start) ? "auto" : start)} / span {(string.IsNullOrEmpty(span) ? "1" : span)}";
    }

    // 슈가가 읽는 속성 — 이것만 바인딩을 풀어 둔다
    private static readonly string[] Names =
        { "columns", "rows", "gap", "padding", "align", "justify", "grow", "width", "height", "grid-row", "grid-col", "row-span", "col-span",
          "background", "border", "border-width", "border-color", "border-style" };
    private static readonly string[] GridNames = { "columns", "rows" };
    private static readonly string[] SugarNames = { "columns", "rows", "gap", "padding", "align", "justify" };
    private static readonly string[] ShapeNames = { "background", "border", "border-width", "border-color", "border-style" };

    /// <summary>The shape tags (<c>DvPolyline</c> <c>DvPolygon</c> <c>DvEllipse</c>): they draw their own outline and fill instead of the box background and border.</summary>
    internal static bool IsShape(string tag) => tag is "DvPolyline" or "DvPolygon" or "DvEllipse";

    /// <summary>Spacing step name (xs to 2xl) → token, none → 0, any other number is used as is.</summary>
    private static string Space(string v) => v switch
    {
        "none" => "0",
        "xs" or "sm" or "md" or "lg" or "xl" or "2xl" => $"token(spacing.{v})",
        _ => v
    };
}
