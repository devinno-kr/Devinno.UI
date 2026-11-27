using System.Globalization;
using Devinno.UI.Interaction;
using SkiaSharp;

namespace Devinno.UI.Layout;

// 도형 태그 빌더 (DvPolyline DvPolygon DvEllipse) — 점·화살표·끝 모양. 칠·외곽선은 style(LayoutSugar 가 속성을 선언으로 바꾼다).
public sealed partial class LayoutEngine
{
    private static void BuildShape(LayoutBox box, AttrReader r)
    {
        var tag = r.Node.Tag;
        var text = r.Text("points");
        SKPoint[]? pts = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            pts = ParsePoints(text);
            if (pts is null)
                Warnings.Warn($"Devinno.UI: {tag} points='{text}' is not a list of x,y pairs (e.g. \"0,0 100,40\"); not drawn");
        }

        var (min, rule) = tag switch
        {
            "DvPolyline" => (2, "at least 2 points"),
            "DvPolygon" => (3, "at least 3 points"),
            _ => (0, "0 or 2 points"),
        };
        var count = pts?.Length ?? 0;
        var ok = tag == "DvEllipse" ? count is 0 or 2 : count >= min;
        if (pts is not null && !ok)
        {
            Warnings.Warn($"Devinno.UI: {tag} points='{text}' has {count} point(s); it needs {rule}; not drawn");
            pts = null;
        }

        // 그리지 않는 도형 = 점 없음 + 그리지 않음 표시(DvEllipse 의 점 없음은 상자를 채우는 정상 경우)
        box.ShapePoints = pts;
        box.ShapeDrawn = tag == "DvEllipse" ? text is null || string.IsNullOrWhiteSpace(text) || pts is not null : pts is not null;
        if (tag == "DvPolyline")
        {
            box.ShapeArrow = AttrRules.Enum(r, "arrow") ?? "none";
            box.ShapeCap = AttrRules.Enum(r, "cap") switch
            {
                "round" => SKStrokeCap.Round,
                "square" => SKStrokeCap.Square,
                _ => SKStrokeCap.Butt,
            };
        }
    }

    // "x,y x,y …" — 공백으로 나눈 x,y 쌍(Invariant 숫자). 하나라도 못 읽으면 null
    internal static SKPoint[]? ParsePoints(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var pts = new SKPoint[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var xy = parts[i].Split(',');
            if (xy.Length != 2 ||
                !float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !float.IsFinite(x) || !float.IsFinite(y))
                return null;
            pts[i] = new SKPoint(x, y);
        }

        return pts;
    }
}
