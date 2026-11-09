using System.Diagnostics;
using Devinno.UI.Layout;
using Devinno.UI.Styling;

namespace Devinno.UI.Grid;

/// <summary>Per-grid color attributes — only the ones specified have values; missing (null) slots use theme tokens.</summary>
internal sealed record GridColors(
    DvColor? Header, DvColor? HeaderText, DvColor? Text, DvColor? Row, DvColor? RowAlt, DvColor? Hover, DvColor? Selected,
    DvColor? Line, DvColor? Summary, DvColor? SummaryText)
{
    public static readonly GridColors None = new(null, null, null, null, null, null, null, null, null, null);

    /// <summary><c>header-color</c>, etc. — the value is <c>#rrggbb[aa]</c> or <c>token(이름)</c> (token name). Parse failure = null (theme) + warning.</summary>
    public static GridColors Parse(AttrReader r, DvTheme? theme)
    {
        if (!Names.Any(r.Has))
            return None;

        DvColor? C(string name)
        {
            if (r.Text(name) is not { Length: > 0 } raw)
                return null;
            var resolved = ThemeResolver.ResolveVars(raw, theme);
            // 없는 토큰은 ResolveVars 가 자홍(MissingColor)으로 바꾼다 — 그리드 색 속성은 spec 대로 테마 색으로 둔다
            var missingToken = resolved is not null &&
                               resolved.Contains(ThemeResolver.MissingColor, StringComparison.OrdinalIgnoreCase) &&
                               !raw.Contains(ThemeResolver.MissingColor, StringComparison.OrdinalIgnoreCase);
            if (!missingToken && DvColor.TryParse(resolved, out var c))
                return c;
            Trace.TraceWarning($"Devinno.UI: DvDataGrid {name}=\"{raw}\": not a valid color; using the theme color");
            return null;
        }

        return new GridColors(C("header-color"), C("header-text-color"), C("text-color"), C("row-color"), C("row-alt-color"),
            C("hover-color"), C("selected-color"), C("line-color"), C("summary-color"), C("summary-text-color"));
    }

    // 색 속성 이름 — 하나도 없으면 None(전부 테마 색)
    private static readonly string[] Names =
    [
        "header-color", "header-text-color", "text-color", "row-color", "row-alt-color",
        "hover-color", "selected-color", "line-color", "summary-color", "summary-text-color",
    ];
}

/// <summary>The style used by <c>row-class</c> and <c>cell-class</c> classes — only specified properties have values (null = not specified).</summary>
internal sealed record GridCellStyle(DvColor? Background, DvColor? Color, bool? Bold)
{
    /// <summary>Properties specified by the latter (other) take precedence.</summary>
    public GridCellStyle Merge(GridCellStyle other)
        => new(other.Background ?? Background, other.Color ?? Color, other.Bold ?? Bold);
}
