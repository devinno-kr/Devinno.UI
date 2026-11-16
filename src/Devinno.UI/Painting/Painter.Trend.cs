using System.Globalization;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using Devinno.UI.Trends;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    // DvTrend — 범례(최신값) + Y 그리드/라벨 + 시간 눈금 + 채널 곡선(선택: 영역 채움) + 마지막 점 발광
    private void PaintTrend(SKCanvas canvas, LayoutBox box, ComputedStyle st)
    {
        if (box.Trend is not { } spec)
            return;

        var content = new SKRect(box.Rect.Left + st.Padding.Left, box.Rect.Top + st.Padding.Top,
            box.Rect.Right - st.Padding.Right, box.Rect.Bottom - st.Padding.Bottom);
        var muted = ThemeColor("text-muted");
        var ink = ThemeColor("text");
        var grid = ThemeColor("trend-grid");

        // ── 시간 창
        var latest = spec.Channels.SelectMany(c => c.Points).Select(p => p.Time).DefaultIfEmpty(DateTime.MinValue).Max();
        var hasData = latest != DateTime.MinValue || spec.End is not null;
        // 데이터가 아직 없으면 임의 기준 시각(라벨은 그리지 않음) — MinValue 에서 창을 빼면 범위 예외
        var end = spec.End ?? (latest != DateTime.MinValue ? latest : DateTime.Today.AddHours(12));
        var window = Math.Max(0.001, spec.WindowSeconds);
        var start = end.AddSeconds(-window);

        var visible = spec.Channels.Select(c => (c, pts: Visible(c.Points, start, end))).ToList();
        var values = visible.SelectMany(v => v.pts).Select(p => p.Value).Where(double.IsFinite).ToList();

        // ── Y 범위·눈금 (지정값 우선, 없으면 보이는 데이터로) — 눈금 간격은 플롯 높이에 맞춤(위 26 · 아래 20 은 범례·시간 글자)
        var (lo, hi, yStep) = TrendAxis(spec.YMin, spec.YMax, values, content.Height - 46f);
        // 눈금은 간격의 배수(0 을 지나는 값)에 둔다 — y-min=-120 이어도 -100, -50, 0 … 으로
        var yTicks = Widgets.ScaleTicks.Values(Math.Ceiling(lo / yStep - 1e-9) * yStep, hi, yStep);

        using var axisFont = TextFont.Create("0", Roles.Font("caption"), weight: Roles.Weight("regular"));
        var labelW = yTicks.Select(v => axisFont.MeasureText(FormatTick(v))).DefaultIfEmpty(0).Max();
        var plot = new SKRect(content.Left + labelW + 8f, content.Top + 26f, content.Right - 6f, content.Bottom - 20f);
        if (plot.Width <= 10 || plot.Height <= 10)
            return;

        float X(DateTime t) => plot.Left + (float)((t - start).TotalSeconds / window) * plot.Width;
        float Y(double v) => plot.Bottom - (float)((v - lo) / (hi - lo)) * plot.Height;

        using (var gp = new SKPaint { IsAntialias = true, Color = grid, StrokeWidth = Roles.Stroke("hairline") })
        using (var lp = new SKPaint { IsAntialias = true, Color = muted })
        {
            foreach (var v in yTicks)
            {
                var y = Y(v);
                canvas.DrawLine(plot.Left, y, plot.Right, y, gp);
                canvas.DrawText(FormatTick(v), plot.Left - 6f, y - (axisFont.Metrics.Ascent + axisFont.Metrics.Descent) / 2f,
                    SKTextAlign.Right, axisFont, lp);
            }

            if (hasData)
            {
                // 틱(100ns) 정수 연산 — 밀리초 간격에서도 부동소수 누적 오차 없음
                var step = TrendScale.TimeStep(window);
                var stepTicks = Math.Max(1L, (long)Math.Round(step * TimeSpan.TicksPerSecond));
                var firstTicks = (start.Ticks + stepTicks - 1) / stepTicks * stepTicks;
                for (var ticks = firstTicks; ticks <= end.Ticks; ticks += stepTicks)
                {
                    // 시간 눈금은 글자만 — 세로 격자 없음(가로 격자만, 조사 4.2)
                    var t = new DateTime(ticks);
                    var x = X(t);
                    canvas.DrawText(TrendScale.TimeLabel(t, step), x, plot.Bottom + 15f, SKTextAlign.Center, axisFont, lp);
                }
            }
        }

        if (!hasData || values.Count == 0)
        {
            var empty = T("dv.trend.empty");
            using var ef = TextFont.Create(empty, Roles.Font("label"), weight: Roles.Weight("regular"));
            using var ep = new SKPaint { IsAntialias = true, Color = muted };
            canvas.DrawText(empty, plot.MidX, plot.MidY, SKTextAlign.Center, ef, ep);
        }

        // ── 채널 곡선. 영역 채움은 0 부터 — 0 이 범위 밖이거나(하한이 0 이 아닌 채움은 양을 잘못 보여 줌) 채널이 여럿이면(겹침) 생략
        var withData = visible.Count(v => v.pts.Any(p => double.IsFinite(p.Value)));
        var fill = spec.Area && withData == 1 && lo <= 0 && hi >= 0;
        var zeroY = Y(0);
        canvas.Save();
        canvas.ClipRect(new SKRect(plot.Left, plot.Top - 4f, plot.Right + 4f, plot.Bottom));
        foreach (var (ch, pts) in visible)
        {
            foreach (var run in Runs(pts))
            {
                var mapped = run.Select(p => new SKPoint(X(p.Time), Y(p.Value))).ToList();
                using var path = TrendPath.Build(mapped, spec.Smooth);

                if (fill && mapped.Count > 1)
                {
                    using var area = new SKPath(path);
                    area.LineTo(mapped[^1].X, zeroY);
                    area.LineTo(mapped[0].X, zeroY);
                    area.Close();
                    // 영역 가장자리는 위에 그리는 선이 덮으므로 AA 생략 — 큰 면적 채움 비용 절감
                    using var ap = new SKPaint { IsAntialias = false };
                    ap.Shader = SKShader.CreateLinearGradient(new SKPoint(0, plot.Top), new SKPoint(0, zeroY),
                        new[] { ch.Color.WithAlpha(Roles.Alpha("alpha.dim")), ch.Color.WithAlpha(0x00) }, null, SKShaderTileMode.Clamp);
                    canvas.DrawPath(area, ap);
                }

                using var sp = new SKPaint
                {
                    IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Roles.Stroke("emphasis"),
                    StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round, Color = ch.Color
                };
                canvas.DrawPath(path, sp);
            }

            // 마지막 점 — 발광 + 점
            if (pts.LastOrDefault(p => double.IsFinite(p.Value)) is { Time.Ticks: > 0 } last)
            {
                var c = new SKPoint(X(last.Time), Y(last.Value));
                if (Roles.Look.Texture == LookTexture.Rich) // 마지막 점 글로우 — 질감 rich 에서만
                using (var glow = new SKPaint { IsAntialias = true, Color = Fade(ch.Color.WithAlpha(0x55), Roles.Depth) })
                {
                    glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
                    canvas.DrawCircle(c, 6f, glow);
                }

                using (var dot = new SKPaint { IsAntialias = true, Color = ch.Color })
                    canvas.DrawCircle(c, 3.5f, dot);
            }
        }

        canvas.Restore();

        // ── 범례: 색 점 + 이름(흐림) + 최신값(strong, 고정폭 숫자) + 단위(작게·흐림) — 값 문법 H6
        var lx = plot.Left;
        var ly = content.Top + 10f;
        foreach (var (ch, pts) in visible)
        {
            var name = ch.Label ?? ch.Name;
            var lastVal = pts.LastOrDefault(p => double.IsFinite(p.Value));
            var has = lastVal.Time.Ticks > 0;
            var val = has ? TrendLegendNumber(lastVal.Value, ch) : "-";
            var unit = has ? ch.Unit ?? "" : "";

            using (var dot = new SKPaint { IsAntialias = true, Color = ch.Color })
                canvas.DrawCircle(lx + 4f, ly, 4f, dot);
            using var nf = TextFont.Create(name, Roles.Font("meta"), weight: Roles.Weight("regular"));
            using var vf = TextFont.Create(val, Roles.Font("meta"), weight: Roles.Weight("strong"));
            using var np = new SKPaint { IsAntialias = true, Color = muted };
            using var vp = new SKPaint { IsAntialias = true, Color = ink };
            var baseY = ly - (nf.Metrics.Ascent + nf.Metrics.Descent) / 2f;
            canvas.DrawText(name, lx + 13f, baseY, SKTextAlign.Left, nf, np);
            var x = lx + 13f + nf.MeasureText(name) + 6f;
            TextFont.DrawNumber(canvas, val, x, baseY, SKTextAlign.Left, vf, vp);
            x += TextFont.MeasureNumber(val, vf);
            if (unit.Length > 0)
            {
                // 값에 붙는 단위(%·°)는 바로, 그 밖은 한 칸(WithUnit 과 같은 규칙)
                x += UnitJoinsValue(unit) ? 1f : 3f;
                using var uf = TextFont.Create(unit, Roles.Font("caption"), weight: Roles.Weight("regular"));
                canvas.DrawText(unit, x, baseY, SKTextAlign.Left, uf, np);
                x += uf.MeasureText(unit);
            }
            lx = x + 18f;
        }
    }

    // 창 안의 점 + 창 왼쪽 경계 직전 1점(선이 왼쪽 끝까지 이어지게)
    private static List<TrendPoint> Visible(TrendPoint[] points, DateTime start, DateTime end)
    {
        var list = new List<TrendPoint>();
        TrendPoint? before = null;
        foreach (var p in points)
        {
            if (p.Time < start)
                before = p;
            else if (p.Time <= end)
                list.Add(p);
        }

        if (before is { } b)
            list.Insert(0, b);
        return list;
    }

    // NaN 으로 끊긴 연속 구간
    private static IEnumerable<List<TrendPoint>> Runs(List<TrendPoint> pts)
    {
        var run = new List<TrendPoint>();
        foreach (var p in pts)
        {
            if (double.IsFinite(p.Value))
            {
                run.Add(p);
                continue;
            }

            if (run.Count > 0)
                yield return run;
            run = new List<TrendPoint>();
        }

        if (run.Count > 0)
            yield return run;
    }

    // 범례 최신값 숫자 — 채널 format(공용 규칙)이 있으면 그것, 없거나 쓸 수 없으면 눈금과 같은 규칙
    internal static string TrendLegendNumber(double v, TrendChannelData ch)
        => AttrRules.FormatOrNull(v, ch.Format, "DvTrendChannel") ?? FormatTick(Math.Round(v, 2));

    // Y 축 범위·눈금 간격 — 지정값 우선, 없으면 보이는 데이터로 보기 좋은 범위
    // 눈금 수 = 높이 40px 당 하나(최대 5), 그래도 간격이 28px 보다 좁으면 줄인다(작은 패널에서 눈금 과밀 방지)
    internal static (double Lo, double Hi, double Step) TrendAxis(double? yMin, double? yMax, IReadOnlyList<double> values, float plotHeight)
    {
        const float minGap = 28f;
        for (var target = Math.Clamp((int)(plotHeight / 40f), 1, 5); ; target--)
        {
            var auto = values.Count > 0 ? TrendScale.Nice(values.Min(), values.Max(), target) : TrendScale.Nice(0, 100, target);
            var lo = yMin ?? auto.Lo;
            var hi = yMax ?? auto.Hi;
            if (hi <= lo)
                hi = lo + 1;
            var step = yMin is null && yMax is null ? auto.Step : TrendScale.Nice(lo, hi, target).Step;
            if (target <= 1 || plotHeight * step / (hi - lo) >= minGap)
                return (lo, hi, step);
        }
    }

    private static string FormatTick(double v)
        => Math.Abs(v - Math.Round(v)) < 1e-9
            ? Math.Round(v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.###", CultureInfo.InvariantCulture);
}
