using System.Globalization;
using Devinno.UI.Layout;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    // 값 글자 — 정수면 정수, 아니면 소수 한 자리(계기 위젯과 같은 규칙)
    private static string ChartNumber(double v)
        => Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.0", CultureInfo.InvariantCulture);

    // 값 축 범위 — y-min·y-max, 없으면 데이터로(막대는 0 을 포함). 폭이 0 이면 1 로 벌린다
    private static (double Lo, double Hi) ChartRange(LayoutBox box, bool includeZero)
    {
        var v = box.ChartValues;
        var lo = box.ChartYMin ?? (v.Length == 0 ? 0 : v.Min());
        var hi = box.ChartYMax ?? (v.Length == 0 ? 1 : v.Max());
        if (includeZero && box.ChartYMin is null)
            lo = Math.Min(lo, 0);
        if (includeZero && box.ChartYMax is null)
            hi = Math.Max(hi, 0);
        if (hi - lo < 1e-12)
            hi = lo + 1;
        return (lo, hi);
    }

    // DvSparkline — 축·눈금 없는 작은 선. 마지막 점에 동그라미, area 면 선 아래 반투명 채움. 색 = tone-{tone}.
    private void PaintSparkline(SKCanvas canvas, LayoutBox box)
    {
        var v = box.ChartValues;
        if (v.Length == 0)
            return;
        var r = box.Rect;
        var pad = 3f;
        var (lo, hi) = ChartRange(box, includeZero: false);
        float X(int i) => v.Length == 1 ? r.MidX : r.Left + pad + (r.Width - pad * 2) * i / (v.Length - 1);
        float Y(double d) => (float)(r.Bottom - pad - (r.Height - pad * 2) * Math.Clamp((d - lo) / (hi - lo), 0, 1));
        var color = ToneToken("tone-" + box.Tone, new SKColor(0x25, 0x63, 0xeb));

        using var path = new SKPath();
        path.MoveTo(X(0), Y(v[0]));
        for (var i = 1; i < v.Length; i++)
            path.LineTo(X(i), Y(v[i]));

        if (box.ChartArea && v.Length > 1)
        {
            using var area = new SKPath(path);
            area.LineTo(X(v.Length - 1), r.Bottom - pad);
            area.LineTo(X(0), r.Bottom - pad);
            area.Close();
            using var fp = new SKPaint { IsAntialias = true, Color = color.WithAlpha(0x33) };
            canvas.DrawPath(area, fp);
        }

        using var lp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, StrokeJoin = SKStrokeJoin.Round, Color = color };
        canvas.DrawPath(path, lp);
        lp.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(X(v.Length - 1), Y(v[^1]), 3f, lp);
    }

    // DvBarChart — 세로 막대(색 = tone-{tone}), 아래 이름, 바닥선(값 0 또는 y-min). show-value 면 막대 위 값.
    private void PaintBarChart(SKCanvas canvas, LayoutBox box)
    {
        var v = box.ChartValues;
        if (v.Length == 0)
            return;
        var r = box.Rect;
        using var font = TextFont.Create(string.Concat(box.ChartLabels) + "0123456789", 12f, bold: false);
        var labelH = font.Spacing + 4f;
        var top = r.Top + (box.ShowValue ? font.Spacing + 4f : 4f);
        var bottom = r.Bottom - labelH;
        if (bottom <= top)
            return;
        var (lo, hi) = ChartRange(box, includeZero: true);
        float Y(double d) => (float)(bottom - (bottom - top) * Math.Clamp((d - lo) / (hi - lo), 0, 1));
        var cell = r.Width / v.Length;
        var barW = MathF.Min(cell * 0.6f, 48f);
        var color = ToneToken("tone-" + box.Tone, new SKColor(0x25, 0x63, 0xeb));
        var baseY = Y(Math.Clamp(0, lo, hi));

        using var axis = new SKPaint { IsAntialias = true, StrokeWidth = 1f, Color = ThemeColor("border-strong", new SKColor(0x94, 0xa3, 0xb8)) };
        canvas.DrawLine(r.Left, baseY, r.Right, baseY, axis);

        using var bp = new SKPaint { IsAntialias = true, Color = color };
        using var tp = new SKPaint { IsAntialias = true, Color = ThemeColor("text-muted", new SKColor(0x64, 0x74, 0x8b)) };
        using var vp = new SKPaint { IsAntialias = true, Color = ThemeColor("text", new SKColor(0x22, 0x22, 0x22)) };
        for (var i = 0; i < v.Length; i++)
        {
            var cx = r.Left + cell * (i + 0.5f);
            var y = Y(v[i]);
            var bar = new SKRect(cx - barW / 2f, MathF.Min(y, baseY), cx + barW / 2f, MathF.Max(y, baseY));
            canvas.DrawRoundRect(bar, 3f, 3f, bp);

            if (i < box.ChartLabels.Length && box.ChartLabels[i] is { Length: > 0 } name)
            {
                var shown = font.MeasureText(name) > cell - 4f ? Ellipsize(name, cell - 4f, font) : name;
                canvas.DrawText(shown, cx, r.Bottom - 4f - font.Metrics.Descent, SKTextAlign.Center, font, tp);
            }
            if (box.ShowValue)
                canvas.DrawText(ChartNumber(v[i]), cx, bar.Top - 4f - font.Metrics.Descent, SKTextAlign.Center, font, vp);
        }
    }

    // DvPieChart — 원(inner-radius 면 도넛) + 오른쪽 범례(색 점 · 이름 · 값 또는 백분율). 조각 색은 트렌드 기본 색을 차례로.
    // 0 이하 값은 그리지 않는다. 범례 자리(폭 - 높이 ≥ 100)가 없으면 원만.
    private void PaintPieChart(SKCanvas canvas, LayoutBox box)
    {
        var v = box.ChartValues;
        var total = v.Where(x => x > 0).Sum();
        var r = box.Rect;
        var legend = r.Width - r.Height >= 100f;
        var size = MathF.Min(legend ? r.Height : MathF.Min(r.Width, r.Height), r.Width);
        var radius = size / 2f - 4f;
        if (radius <= 0)
            return;
        var c = new SKPoint(r.Left + (legend ? size / 2f : r.Width / 2f), r.MidY);
        var oval = new SKRect(c.X - radius, c.Y - radius, c.X + radius, c.Y + radius);
        var palette = Trends.TrendSpec.Palette;

        using var p = new SKPaint { IsAntialias = true };
        if (total <= 0)
        {
            p.Style = SKPaintStyle.Stroke;
            p.StrokeWidth = 2f;
            p.Color = ThemeColor("border", new SKColor(0xe2, 0xe8, 0xf0));
            canvas.DrawCircle(c, radius - 1f, p);
        }
        else
        {
            var start = -90f;
            for (var i = 0; i < v.Length; i++)
            {
                if (v[i] <= 0)
                    continue;
                var sweep = (float)(v[i] / total * 360.0);
                p.Color = palette[i % palette.Length];
                using var slice = new SKPath();
                slice.MoveTo(c);
                slice.ArcTo(oval, start, MathF.Min(sweep, 359.99f), false);
                slice.Close();
                canvas.DrawPath(slice, p);
                start += sweep;
            }

            // 조각 사이 가는 틈(배경색 선)
            if (v.Count(x => x > 0) > 1)
            {
                using var gap = new SKPaint { IsAntialias = true, StrokeWidth = 2f, Color = ThemeColor("surface", SKColors.White) };
                var a = -90f;
                foreach (var x in v.Where(x => x > 0))
                {
                    var rad = a * MathF.PI / 180f;
                    canvas.DrawLine(c, new SKPoint(c.X + MathF.Cos(rad) * radius, c.Y + MathF.Sin(rad) * radius), gap);
                    a += (float)(x / total * 360.0);
                }
            }
        }

        if (box.ChartInner > 0)
        {
            p.Color = ThemeColor("surface", SKColors.White);
            p.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(c, radius * box.ChartInner, p);
            if (box.ShowValue && total > 0) // 도넛 가운데 합계
            {
                using var big = TextFont.Create("0123456789", MathF.Max(12f, radius * box.ChartInner * 0.5f), bold: true);
                using var bp = new SKPaint { IsAntialias = true, Color = ThemeColor("text", new SKColor(0x22, 0x22, 0x22)) };
                canvas.DrawText(ChartNumber(total), c.X, c.Y - (big.Metrics.Ascent + big.Metrics.Descent) / 2f, SKTextAlign.Center, big, bp);
            }
        }

        if (!legend)
            return;
        using var font = TextFont.Create(string.Concat(box.ChartLabels) + "0123456789%", 12f, bold: false);
        var lx = r.Left + size + 12f;
        var rows = v.Length;
        var lineH = font.Spacing + 6f;
        var ly = r.MidY - rows * lineH / 2f + lineH / 2f;
        using var tp = new SKPaint { IsAntialias = true, Color = ThemeColor("text", new SKColor(0x22, 0x22, 0x22)) };
        using var mp = new SKPaint { IsAntialias = true, Color = ThemeColor("text-muted", new SKColor(0x64, 0x74, 0x8b)) };
        for (var i = 0; i < rows; i++, ly += lineH)
        {
            if (ly - lineH / 2f < r.Top || ly + lineH / 2f > r.Bottom)
                continue; // 칸을 넘는 범례 줄은 그리지 않는다
            p.Color = palette[i % palette.Length];
            canvas.DrawCircle(lx + 5f, ly, 5f, p);
            var amount = box.ShowValue ? ChartNumber(v[i]) : total > 0 && v[i] > 0 ? $"{v[i] / total * 100:0}%" : "0%";
            var aw = font.MeasureText(amount);
            var name = i < box.ChartLabels.Length ? box.ChartLabels[i] : "";
            var room = r.Right - (lx + 16f) - aw - 8f;
            var shown = font.MeasureText(name) > room ? Ellipsize(name, MathF.Max(room, 0f), font) : name;
            var baseline = ly - (font.Metrics.Ascent + font.Metrics.Descent) / 2f;
            canvas.DrawText(shown, lx + 16f, baseline, SKTextAlign.Left, font, tp);
            canvas.DrawText(amount, r.Right, baseline, SKTextAlign.Right, font, mp);
        }
    }
}
