using Devinno.UI.Binding;
using Devinno.UI.Markup;

namespace Devinno.UI.Layout;

/// <summary>
/// Layout sugar: expands DvStack/DvRow/DvGrid/DvSpacer into flex/grid declarations (there is no separate layout path).
/// Sugar declarations have the lowest cascade priority, so class/style can override them.
/// Attribute values are converted after bindings are resolved, so <c>columns="{N}"</c> and <c>gap="{G}"</c> are interpreted exactly like literals
/// (column count, spacing step name, <c>between</c>). An attribute whose value is null is treated as not specified.
/// </summary>
internal static class LayoutSugar
{
    public static string? Decls(ElementNode node, object? vm)
    {
        var a = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in Names)
        {
            if (node.Attributes.TryGetValue(k, out var raw) &&
                BindingResolver.ResolveText(vm, raw) is { } v)
                a[k] = v;
        }
        var parts = new List<string>();

        var isSugar = true;
        switch (node.Tag)
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
            default:
                isSugar = false;
                break;
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

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    // 슈가가 읽는 속성 — 이것만 바인딩을 풀어 둔다
    private static readonly string[] Names = { "columns", "rows", "gap", "padding", "align", "justify", "grow", "width", "height" };

    /// <summary>Spacing step name (xs to 2xl) → token, none → 0, any other number is used as is.</summary>
    private static string Space(string v) => v switch
    {
        "none" => "0",
        "xs" or "sm" or "md" or "lg" or "xl" or "2xl" => $"token(spacing.{v})",
        _ => v
    };
}
