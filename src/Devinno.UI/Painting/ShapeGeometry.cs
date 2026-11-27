using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI.Painting;

// 도형(DvPolyline·DvPolygon·DvEllipse)의 모양 — 그리기·누름·그리기 범위가 같은 계산을 쓴다.
// 점 = 상자 Rect 왼쪽 위 기준 px(늘이지 않음). 선은 점을 잇는 선의 가운데에 그리고(SVG), 타원은 외곽선이 상자 안에 들게 두께 절반만큼 줄인다(CSS 테두리).
// 화살촉 = 외곽선 색으로 채운 삼각형, 길이 max(8, 3 × 두께 + 6), 밑변 = 길이. 화살촉 쪽 선은 밑변까지만 그어 끝이 촉 밖으로 나오지 않게 한다.
internal static class ShapeGeometry
{
    /// <summary>Touch reach from a drawn line (px), at least.</summary>
    internal const float TouchReach = 6f;

    internal static bool IsShape(LayoutBox box) => LayoutSugar.IsShape(box.Node.Tag);

    internal static bool Closed(LayoutBox box) => box.Node.Tag != "DvPolyline";

    // 외곽선을 그리는가 — 두께 > 0 이고 색이 있을 때
    internal static bool Stroked(ComputedStyle st) => st.BorderWidth > 0 && st.BorderColor is not null;

    // 칠하는가 — 닫힌 모양이고 색(투명 아님)이나 그라데이션이 있을 때
    internal static bool Filled(LayoutBox box, ComputedStyle st) =>
        Closed(box) && (st.Gradient is not null || st.Background is { A: > 0 });

    internal static float ArrowLength(float width) => MathF.Max(8f, 3f * width + 6f);

    /// <summary>The outline path (or the line), and the arrow heads of a polyline; <see langword="null"/> when the shape is not drawn.</summary>
    internal static (SKPath Path, List<SKPath> Heads)? Build(LayoutBox box, ComputedStyle st)
    {
        if (!box.ShapeDrawn)
            return null;
        var o = new SKPoint(box.Rect.Left, box.Rect.Top);
        var path = new SKPath();
        var heads = new List<SKPath>();
        var pts = box.ShapePoints;
        switch (box.Node.Tag)
        {
            case "DvEllipse":
            {
                var r = pts is { Length: 2 }
                    ? new SKRect(MathF.Min(pts[0].X, pts[1].X), MathF.Min(pts[0].Y, pts[1].Y), MathF.Max(pts[0].X, pts[1].X), MathF.Max(pts[0].Y, pts[1].Y))
                    : SKRect.Create(box.Rect.Width, box.Rect.Height);
                r.Offset(o);
                var half = Stroked(st) ? st.BorderWidth / 2f : 0f;
                r = SKRect.Inflate(r, -MathF.Min(half, r.Width / 2f), -MathF.Min(half, r.Height / 2f));
                path.AddOval(r);
                break;
            }
            case "DvPolygon":
                path.AddPoly(pts!.Select(p => p + o).ToArray(), close: true);
                break;
            default:
            {
                var line = pts!.Select(p => p + o).ToArray();
                if (Stroked(st))
                {
                    var len = ArrowLength(st.BorderWidth);
                    if (box.ShapeArrow is "start" or "both")
                        heads.Add(Head(line, start: true, len));
                    if (box.ShapeArrow is "end" or "both")
                        heads.Add(Head(line, start: false, len));
                    if (box.ShapeArrow is "start" or "both")
                        Pull(line, start: true, len);
                    if (box.ShapeArrow is "end" or "both")
                        Pull(line, start: false, len);
                }

                path.AddPoly(line, close: false);
                break;
            }
        }

        return (path, heads);
    }

    // 끝점에서 앞 점 쪽으로 화살촉 — 끝과 같은 자리의 앞 점은 건너뛴다(방향을 정할 수 없으면 오른쪽)
    private static SKPath Head(SKPoint[] line, bool start, float len)
    {
        var (tip, dir) = Direction(line, start);
        var bx = tip.X - dir.X * len;
        var by = tip.Y - dir.Y * len;
        var half = len / 2f;
        var p = new SKPath();
        p.MoveTo(tip);
        p.LineTo(bx - dir.Y * half, by + dir.X * half);
        p.LineTo(bx + dir.Y * half, by - dir.X * half);
        p.Close();
        return p;
    }

    // 화살촉 쪽 끝점을 밑변까지 당긴다(그 선분보다 길면 앞 점까지)
    private static void Pull(SKPoint[] line, bool start, float len)
    {
        var i = start ? 0 : line.Length - 1;
        var (tip, dir) = Direction(line, start);
        var prev = start ? line[1] : line[^2];
        var room = SKPoint.Distance(tip, prev);
        var d = MathF.Min(len * 0.9f, room);
        line[i] = new SKPoint(tip.X - dir.X * d, tip.Y - dir.Y * d);
    }

    // 끝점과 바깥 방향(단위 벡터)
    private static (SKPoint Tip, SKPoint Dir) Direction(SKPoint[] line, bool start)
    {
        var tip = start ? line[0] : line[^1];
        var n = line.Length;
        for (var k = 1; k < n; k++)
        {
            var other = start ? line[k] : line[n - 1 - k];
            var dx = tip.X - other.X;
            var dy = tip.Y - other.Y;
            var d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 0.001f)
                return (tip, new SKPoint(dx / d, dy / d));
        }

        return (tip, new SKPoint(start ? -1 : 1, 0));
    }

    internal static void Paint(SKCanvas canvas, LayoutBox box, ComputedStyle st, Func<LinearGradient, SKRect, SKShader?> gradient)
    {
        if (Build(box, st) is not { } built)
            return;
        var (path, heads) = built;
        using (path)
        {
            if (Filled(box, st))
            {
                using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
                using var shader = st.Gradient is { } g ? gradient(g, path.Bounds) : null;
                if (shader is not null)
                    fill.Shader = shader;
                else
                    fill.Color = st.Background!.Value.ToSK();
                canvas.DrawPath(path, fill);
            }

            if (Stroked(st))
            {
                using var stroke = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = st.BorderWidth,
                    Color = st.BorderColor!.Value.ToSK(),
                    StrokeCap = Closed(box) ? SKStrokeCap.Butt : box.ShapeCap,
                };
                stroke.StrokeJoin = stroke.StrokeCap == SKStrokeCap.Round ? SKStrokeJoin.Round : SKStrokeJoin.Miter;
                using var dash = Painter.LineDash(st.BorderStyle, st.BorderWidth, stroke);
                stroke.PathEffect = dash;
                canvas.DrawPath(path, stroke);

                using var head = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = st.BorderColor!.Value.ToSK() };
                foreach (var h in heads)
                    canvas.DrawPath(h, head);
            }

            foreach (var h in heads)
                h.Dispose();
        }
    }

    // 그려진 모양에 닿는가 — 선은 max(두께/2, 6) 안, 화살촉, 칠한 모양은 안쪽
    internal static bool Hit(LayoutBox box, SKPoint p)
    {
        var st = box.Style;
        if (Build(box, st) is not { } built)
            return false;
        var (path, heads) = built;
        using (path)
        {
            try
            {
                if (Filled(box, st) && path.Contains(p.X, p.Y))
                    return true;
                foreach (var h in heads)
                    if (h.Contains(p.X, p.Y))
                        return true;
                var reach = MathF.Max(Stroked(st) ? st.BorderWidth : 0f, TouchReach * 2f);
                using var band = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = reach,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round,
                };
                using var area = new SKPath();
                band.GetFillPath(path, area);
                return area.Contains(p.X, p.Y);
            }
            finally
            {
                foreach (var h in heads)
                    h.Dispose();
            }
        }
    }

    // 그리기 범위 — 모양(점이 상자 밖이어도) + 두께(모서리 미터 포함)·화살촉·누름 범위만큼
    internal static SKRect Bounds(LayoutBox box)
    {
        var st = box.Style;
        if (Build(box, st) is not { } built)
            return box.Rect;
        var (path, heads) = built;
        using (path)
        {
            var b = path.Bounds;
            foreach (var h in heads)
            {
                b.Union(h.Bounds);
                h.Dispose();
            }

            var w = Stroked(st) ? st.BorderWidth : 0f;
            b.Inflate(w * 2f + 1f, w * 2f + 1f);
            b.Union(box.Rect);
            return b;
        }
    }
}
