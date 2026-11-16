using Devinno.UI.Layout;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

// 공정도(DvProcess) — 배관(겹별로 한 번에), 화살표, 장비 심볼, 이름표. 모양은 look(plain·rich)
public sealed partial class Painter
{
    /// <summary>
    /// Process diagram look — colors from the theme's <c>process-*</c> tokens. plain = flat fills (ISA-101 style), rich = metallic gradients,
    /// drop shadows and thick pipes computed from the same colors. Which one a diagram uses is <see cref="ProcessDiagram.Rich"/> (the <c>look</c> attribute, else the theme's <c>look.texture</c>).
    /// </summary>
    private sealed class ProcessLook
    {
        public bool Rich;
        public SKColor Stroke, Detail, DetailOn, BodyColor, BodyOn, BodyOff, AccentColor, LiquidColor, Screen, Glass, TagFill, TagStroke, TagText, Arrow,
            Pipe, Water, Steam, Air, Gas, Oil, Acid, Flow, Error;
        public float StrokeW, Corner, Hop;
        public float Depth; // texture.depth — 입체 음영 깊이(0 = 음영 없음)

        private static SKShader HGrad(SKRect r, params (SKColor c, float p)[] stops) =>
            SKShader.CreateLinearGradient(new SKPoint(r.Left, 0), new SKPoint(r.Right, 0),
                stops.Select(s => s.c).ToArray(), stops.Select(s => s.p).ToArray(), SKShaderTileMode.Clamp);

        // 금속 원통 — 바탕색에서 어두운 가장자리 · 밝은 띠 · 반사광을 계산. 깊이 = 테마 texture.depth
        private SKShader Metal(SKRect r, SKColor c) => HGrad(r,
            (Mix(c, SKColors.Black, 0.45f * Depth), 0), (Mix(c, SKColors.White, 0.35f * Depth), .28f), (Mix(c, SKColors.White, 0.78f * Depth), .4f),
            (Mix(c, SKColors.Black, 0.10f * Depth), .7f), (Mix(c, SKColors.Black, 0.55f * Depth), 1));

        // on: null = 상태 없는 몸통(vessel), true/false = 운전·정지 몸통
        public SKShader Body(SKRect r, bool? on)
        {
            var c = on switch { true => BodyOn, false => BodyOff, null => BodyColor };
            return Rich ? Metal(r, c) : SKShader.CreateColor(c);
        }

        public SKShader Accent(SKRect r) => Rich
            ? HGrad(r, (Mix(AccentColor, SKColors.Black, 0.6f * Depth), 0), (Mix(AccentColor, SKColors.Black, 0.2f * Depth), .35f), (AccentColor, .45f), (Mix(AccentColor, SKColors.Black, 0.5f * Depth), 1))
            : SKShader.CreateColor(AccentColor);

        public SKShader Liquid(SKRect r) => Rich
            ? SKShader.CreateLinearGradient(new SKPoint(0, r.Top), new SKPoint(0, r.Bottom),
                [Mix(LiquidColor, SKColors.White, 0.25f * Depth), Mix(LiquidColor, SKColors.Black, 0.3f * Depth)], SKShaderTileMode.Clamp)
            : SKShader.CreateColor(LiquidColor);

        public SKColor LiquidSurface => Rich ? Mix(LiquidColor, SKColors.White, 0.5f) : Mix(LiquidColor, SKColors.Black, 0.3f);

        // 매체 색(process-pipe-{매체}) — rich 는 금속 관 가운데 줄이라 조금 밝게
        public SKColor PipeColor(string? medium)
        {
            var c = medium switch
            {
                "water" => Water, "steam" => Steam, "air" => Air, "gas" => Gas, "oil" => Oil, "acid" => Acid, _ => Pipe,
            };
            return Rich ? Mix(c, SKColors.White, 0.12f) : c;
        }

        // 배관 — 겹별로 모든 관을 한 번에(T 자 이음매가 보이지 않게). 매체가 있는 관은 그 색
        public void Pipes(SKCanvas c, IReadOnlyList<(SKPath Path, string? Medium)> paths)
        {
            using var body = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Rich ? 7 : 3 };
            if (Rich)
            {
                // 관 껍질 = 매체 없는 배관 색을 깊이만큼 어둡게(밝은 테마에서 검은 관이 되지 않게), 가운데 반사 줄 = 깊이만큼
                using var a = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 11, Color = Mix(Pipe, SKColors.Black, 0.45f * Depth) };
                using var h = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = Fade(new SKColor(0xff, 0xff, 0xff, 0x99), Depth) };
                foreach (var (p, _) in paths) c.DrawPath(p, a);
                foreach (var (p, m) in paths)
                {
                    body.Color = PipeColor(m);
                    c.DrawPath(p, body);
                }

                foreach (var (p, _) in paths) c.DrawPath(p, h);
            }
            else
            {
                foreach (var (p, m) in paths)
                {
                    body.Color = PipeColor(m);
                    c.DrawPath(p, body);
                }
            }
        }
    }

    // 테마 토큰으로 만든 모양 — 평면·입체 하나씩, 이 Painter(테마 하나)에서 재사용
    private ProcessLook? _plainLook, _richLook;

    private ProcessLook LookOf(ProcessDiagram d) => d.Rich ? _richLook ??= BuildLook(true) : _plainLook ??= BuildLook(false);

    private ProcessLook BuildLook(bool rich) => new()
    {
        Rich = rich,
        Stroke = ThemeColor("process-line"), Detail = ThemeColor("process-detail"), DetailOn = ThemeColor("process-detail-on"),
        BodyColor = ThemeColor("process-body"), BodyOn = ThemeColor("process-body-on"), BodyOff = ThemeColor("process-body-off"),
        AccentColor = ThemeColor("process-accent"), LiquidColor = ThemeColor("process-liquid"),
        Screen = ThemeColor("process-screen"), Glass = ThemeColor("process-glass"),
        TagFill = ThemeColor("process-tag-bg"), TagStroke = ThemeColor("process-tag-border"), TagText = ThemeColor("process-tag-fg"),
        Arrow = ThemeColor("process-arrow"), Pipe = ThemeColor("process-pipe"),
        Water = ThemeColor("process-pipe-water"), Steam = ThemeColor("process-pipe-steam"), Air = ThemeColor("process-pipe-air"),
        Gas = ThemeColor("process-pipe-gas"), Oil = ThemeColor("process-pipe-oil"), Acid = ThemeColor("process-pipe-acid"),
        Flow = ThemeColor("primary"), Error = ThemeColor("tone-danger"), Depth = Roles.Depth,
        StrokeW = rich ? 1.2f : Roles.Stroke("outline"), Corner = rich ? 10 : 6, Hop = rich ? 13 : 7,
    };

    // 공정도 상자 — 배관·화살표·오류 선(배경은 앱 테마·style 그대로). 장비는 자식 상자로 그 위에 그린다
    private void PaintProcess(SKCanvas canvas, LayoutBox box, long timestampMs)
    {
        var d = box.Process!;
        if (d.Graph is null)
            return;
        ProcessBuilder.Report(d);
        PaintPipes(canvas, d, LookOf(d), timestampMs, ThemeColor("--text"), box.Style.FontFamily);
    }

    /// <summary>Flow animation frame interval (ms) — capped at 20 frames per second (saves CPU, since the whole screen is redrawn).</summary>
    internal const int FlowFrameMs = 50;

    /// <summary>Speed of the moving flow dashes (px/s).</summary>
    private const float FlowSpeed = 40f;

    // 흐르는 배관 위 표시 — 바탕 줄(rich 어둡게·plain 밝게) 위에 굵은 점선(테마 primary)이 흐름 방향(from → to)으로 움직인다.
    // 매체가 있는 관은 바탕 줄·점선을 그 매체 색의 톤으로(고정색이면 매체 색이 가려진다).
    // 점선과 간격의 명암 대비가 커서 금속 관(rich)·회색 관(plain) 위에서도 또렷하다. 위상은 시각에서 계산(같은 시각 = 같은 그림)
    private static void PaintFlow(SKCanvas canvas, IReadOnlyList<(SKPath Path, bool Flowing, string? Medium)> pipes, ProcessLook look, long timestampMs)
    {
        var rich = look.Rich;
        var (dash, gap, width) = rich ? (14f, 10f, 5f) : (10f, 8f, 4f);
        var period = dash + gap;
        // double 로 계산 — 시각(부팅 후 ms, 수십억)을 float 로 바꾸면 수백 ms 단위로 뭉개져 점선이 몇 프레임씩 멈췄다 툭 뛴다(30일이면 256ms)
        var offset = (float)(timestampMs * (double)FlowSpeed / 1000d % period);
        using var under = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width,
            Color = FlowUnder(look, look.Flow),
        };
        using var effect = SKPathEffect.CreateDash([dash, gap], period - offset); // 위상을 줄이면 무늬가 경로 앞쪽으로 나아간다
        using var paint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, PathEffect = effect, StrokeWidth = width,
            Color = FlowDash(look, look.Flow),
        };
        var flowColor = paint.Color;
        foreach (var (path, flowing, medium) in pipes)
        {
            if (!flowing)
                continue;
            if (medium is null)
            {
                under.Color = FlowUnder(look, look.Flow);
                paint.Color = flowColor;
                canvas.DrawPath(path, under);
                canvas.DrawPath(path, paint);
            }
            else
            {
                var m = look.PipeColor(medium);
                under.Color = FlowUnder(look, m);
                paint.Color = FlowDash(look, m);
                canvas.DrawPath(path, under);
                canvas.DrawPath(path, paint);
            }
        }
    }

    // 흐름 바탕 줄·점선 색 — 점선과 간격의 명암 대비가 크게(금속 관·회색 관 위에서도 또렷)
    private static SKColor FlowUnder(ProcessLook look, SKColor c) => look.Rich ? Mix(c, SKColors.Black, 0.55f) : Mix(c, SKColors.White, 0.8f);
    private static SKColor FlowDash(ProcessLook look, SKColor c) => look.Rich ? Mix(c, SKColors.White, 0.35f) : c;

    // text·family = 라인 번호·신호선 라벨 글자(이름표처럼 테마 글자색)
    private static void PaintPipes(SKCanvas canvas, ProcessDiagram d, ProcessLook look, long timestampMs, SKColor text, string? family)
    {
        canvas.Save();
        canvas.Translate(d.Origin.X, d.Origin.Y);
        var paths = new List<SKPath>();
        try
        {
            var flowing = new List<(SKPath, bool, string?)>();
            var pipes = new List<(SKPath, string?)>();
            foreach (var l in d.Lines)
            {
                if (l.G is { } g)
                {
                    var path = PipePath(g, look, !d.Vertical);
                    paths.Add(path);
                    flowing.Add((path, l.Flowing, l.Medium));
                    pipes.Add((path, l.Medium));
                }
            }

            // 계기 연결선(tap) — 배관·장비 밑에. 면 위 자리는 몸통 안쪽까지 그어 심볼 테두리 안쪽 여백(둥근 머리 등)에서도 끊겨 보이지 않게(장비 그림이 덮는다)
            using (var tap = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = SignalWidth, Color = SignalColor(text) })
            {
                foreach (var inst in d.Instruments)
                {
                    if (inst.Host is not { } host || inst.TapPort is null)
                        continue;
                    var (a, b) = (inst.TapFrom, inst.TapTo);
                    if (ProcessBuilder.IsFaceAt(inst.TapPort, out _, out _))
                    {
                        var len = MathF.Max(1, SKPoint.Distance(a, b));
                        a = new SKPoint(a.X - (b.X - a.X) / len * 12, a.Y - (b.Y - a.Y) / len * 12);
                    }

                    canvas.DrawLine(host.G.X + a.X, host.G.Y + a.Y, host.G.X + b.X, host.G.Y + b.Y, tap);
                }
            }

            look.Pipes(canvas, pipes);
            if (d.FlowAnimation)
                PaintFlow(canvas, flowing, look, timestampMs);
            PaintSignals(canvas, d, look, text, family);

            using var arrow = new SKPaint { IsAntialias = true, Color = look.Arrow };
            foreach (var l in d.Lines)
            {
                if (l.G is not { } g)
                    continue;
                if (l.ArrowEnd)
                    DrawArrow(canvas, g.To, look, arrow);
                if (l.ArrowStart)
                    DrawArrow(canvas, g.From, look, arrow);
            }

            // 라인 번호(label) — 가장 긴 구간 가운데, 가로면 배관 아래·세로면 오른쪽
            foreach (var l in d.Lines)
            {
                if (l.G is not { Points.Count: >= 2 } g || string.IsNullOrEmpty(l.Label))
                    continue;
                var k = Enumerable.Range(1, g.Points.Count - 1).MaxBy(i => SKPoint.Distance(g.Points[i - 1], g.Points[i]));
                var (a, b) = (g.Points[k - 1], g.Points[k]);
                using var font = TextFont.Create(l.Label, d.Text(11), Roles.Weight("regular"), family);
                using var tp = new SKPaint { IsAntialias = true, Color = text };
                var off = look.Rich ? 9f : 5f;
                if (MathF.Abs(a.Y - b.Y) < 0.5f)
                    canvas.DrawText(l.Label, (a.X + b.X) / 2, a.Y + off - font.Metrics.Ascent, SKTextAlign.Center, font, tp);
                else
                    canvas.DrawText(l.Label, a.X + off, (a.Y + b.Y) / 2 - (font.Metrics.Ascent + font.Metrics.Descent) / 2, SKTextAlign.Left, font, tp);
            }

            // 잘못된 선 — 두 장비 가운데를 빨간 점선으로
            using var err = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = look.Error,
                PathEffect = SKPathEffect.CreateDash([6, 4], 0),
            };
            foreach (var l in d.Lines.Where(l => l.Error && l.FromNode >= 0 && l.ToNode >= 0))
            {
                var a = d.Nodes[l.FromNode];
                var b = d.Nodes[l.ToNode];
                canvas.DrawLine(a.G.X + a.Body.MidX, a.G.Y + a.Body.MidY, b.G.X + b.Body.MidX, b.G.Y + b.Body.MidY, err);
            }
        }
        finally
        {
            foreach (var p in paths)
                p.Dispose();
            canvas.Restore();
        }
    }

    /// <summary>Stroke width of signal lines and instrument connection lines.</summary>
    private const float SignalWidth = 1.6f;

    // 신호선·연결선 색 — 테마 글자색(모양에 고정하면 plain + 어두운 테마, rich + 밝은 테마에서 배경에 묻힌다). 배관보다 한 단계 약하게
    private static SKColor SignalColor(SKColor text) => text.WithAlpha(0xD9);

    /// <summary>Where the slash pairs of a pneumatic signal go: along each straight segment, away from its ends, about every <see cref="SignalMarkPitch"/> px. A line too short for that still gets one, in the middle of its longest segment.</summary>
    internal static List<(SKPoint At, bool Flat)> SignalMarks(IReadOnlyList<SKPoint> pts)
    {
        var marks = new List<(SKPoint At, bool Flat)>();
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            var (a, b) = (pts[k], pts[k + 1]);
            var len = SKPoint.Distance(a, b);
            var usable = len - 2 * SignalMarkMargin;
            var count = (int)(usable / SignalMarkPitch);
            if (usable >= SignalMarkMin && count == 0)
                count = 1;
            for (var i = 0; i < count; i++)
            {
                var t = (SignalMarkMargin + usable * (i + 0.5f) / count) / len;
                marks.Add((new SKPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t), MathF.Abs(a.Y - b.Y) < 0.5f));
            }
        }

        if (marks.Count == 0 && pts.Count >= 2)
        {
            var k = Enumerable.Range(0, pts.Count - 1).MaxBy(i => SKPoint.Distance(pts[i], pts[i + 1]));
            var (a, b) = (pts[k], pts[k + 1]);
            marks.Add((new SKPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2), MathF.Abs(a.Y - b.Y) < 0.5f));
        }

        return marks;
    }

    // 짧은 구간(양 끝 빼고 80 미만)에는 넣지 않는다 — 장비에 바짝 돌아가는 곳에서 이름표·배관과 엉겨 어수선해진다
    private const float SignalMarkPitch = 60f, SignalMarkMargin = 12f, SignalMarkMin = 80f;

    // 공압 표시 // — 선을 가로지르는 짧은 빗금 두 개. 기울기는 선 방향과 상관없이 같은 쪽
    private static void DrawSignalMark(SKCanvas canvas, SKPoint at, bool flat, SKPaint paint)
    {
        const float gap = 3f, along = 3f, across = 6f;
        foreach (var o in new[] { -gap, gap })
        {
            if (flat)
                canvas.DrawLine(at.X + o - along, at.Y + across, at.X + o + along, at.Y - across, paint);
            else
                canvas.DrawLine(at.X - across, at.Y + o + along, at.X + across, at.Y + o - along, paint);
        }
    }

    // 신호선 — 직교, 점프 없음. 전기(기본) = 점선, 공압 = 실선 + //(ISA-5.1). 배관 위에 긋는다(엇갈리는 곳에서 끊겨 보이지 않게)
    private static void PaintSignals(SKCanvas canvas, ProcessDiagram d, ProcessLook look, SKColor text, string? family)
    {
        foreach (var s in d.Signals)
        {
            if (s.G is not { Points.Count: >= 2 } g)
                continue;
            var color = s.Color ?? SignalColor(text);
            var width = s.Width ?? SignalWidth;
            var k = MathF.Max(1f, width / SignalWidth); // 굵을수록 점선도 길게 — 점으로 뭉치지 않게
            using var dashed = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width, Color = color,
                PathEffect = SKPathEffect.CreateDash([6 * k, 4 * k], 0),
            };
            using var solid = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width, Color = color };
            using var path = new SKPath();
            path.MoveTo(g.Points[0]);
            for (var i = 1; i < g.Points.Count; i++)
                path.LineTo(g.Points[i]);
            canvas.DrawPath(path, s.Pneumatic ? solid : dashed);
            if (s.Pneumatic)
                foreach (var (at, flat) in SignalMarks(g.Points))
                    DrawSignalMark(canvas, at, flat, solid);

            // 화살표(작게) · 라벨(from 끝 옆)
            using var fill = new SKPaint { IsAntialias = true, Color = color };
            if (s.ArrowEnd)
                DrawArrow(canvas, g.To, look, fill, 8, 4);
            if (s.ArrowStart)
                DrawArrow(canvas, g.From, look, fill, 8, 4);
            if (!string.IsNullOrEmpty(s.Label))
            {
                using var font = TextFont.Create(s.Label, d.Text(11), Roles.Weight("regular"), family);
                using var tp = new SKPaint { IsAntialias = true, Color = text };
                var r = SignalLabelRect(d, g, font.MeasureText(s.Label), font.Metrics.Descent - font.Metrics.Ascent);
                canvas.DrawText(s.Label, r.Left, r.Top - font.Metrics.Ascent, SKTextAlign.Left, font, tp);
            }
        }
    }

    // 신호선 라벨 자리 — 첫 구간 시작 옆. 다음 구간이 꺾여 나가는 쪽의 반대편이 먼저, 그다음 꺾이는 쪽.
    // 배관·신호선·계기 버블과 겹치지 않는 첫 자리(다 겹치면 첫 자리)
    internal static SKRect SignalLabelRect(ProcessDiagram d, GraphSignal g, float w, float h)
    {
        const float gap = 3f;
        var (a, b) = (g.Points[0], g.Points[1]);
        var flat = MathF.Abs(a.Y - b.Y) < 0.5f;
        var c = g.Points.Count > 2 ? g.Points[2] : b;
        var candidates = new List<SKRect>();
        if (flat)
        {
            var x = b.X >= a.X ? a.X + gap : a.X - gap - w;
            var above = SKRect.Create(x, a.Y - gap - h, w, h);
            var below = SKRect.Create(x, a.Y + gap, w, h);
            candidates.AddRange(c.Y < b.Y - 0.5f ? [below, above] : [above, below]);
        }
        else
        {
            var y = b.Y >= a.Y ? a.Y + gap : a.Y - gap - h;
            var right = SKRect.Create(a.X + gap, y, w, h);
            var left = SKRect.Create(a.X - gap - w, y, w, h);
            candidates.AddRange(c.X > b.X + 0.5f ? [left, right] : [right, left]);
        }

        var busy = new List<SKRect>();
        void Segments(List<SKPoint> pts, float half)
        {
            for (var k = 0; k + 1 < pts.Count; k++)
                busy.Add(new SKRect(MathF.Min(pts[k].X, pts[k + 1].X) - half, MathF.Min(pts[k].Y, pts[k + 1].Y) - half,
                    MathF.Max(pts[k].X, pts[k + 1].X) + half, MathF.Max(pts[k].Y, pts[k + 1].Y) + half));
        }

        foreach (var l in d.Lines)
            if (l.G is { } gl)
                Segments(gl.Points, 5);
        foreach (var s in d.Signals)
            if (s.G is { } gs)
                Segments(gs.Points, 1);
        foreach (var n in d.Nodes)
            foreach (var inst in n.Instruments)
                if (!inst.Rect.IsEmpty)
                    busy.Add(SKRect.Create(n.G.X + inst.Rect.Left, n.G.Y + inst.Rect.Top, inst.Rect.Width, inst.Rect.Height));

        return candidates.FirstOrDefault(r => !busy.Any(o => o.IntersectsWith(r)), candidates[0]);
    }

    // 계기 버블 — ISA-5.1: 원 위 tag 글자, 아래 number, panel 이면 가운데 가로줄. 값은 버블 오른쪽 작은 글자
    private void PaintInstrument(SKCanvas canvas, LayoutBox box, ComputedStyle st)
    {
        var inst = box.Instrument!;
        if (inst.Host is not { } host)
            return;
        var d = inst.Owner;
        var look = LookOf(d);
        var r = box.Rect;
        using (var fill = new SKPaint { IsAntialias = true, Color = look.TagFill })
            canvas.DrawOval(r, fill);
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f, Color = look.TagStroke };
        canvas.DrawOval(r, stroke);
        if (inst.Panel)
            canvas.DrawLine(r.Left, r.MidY, r.Right, r.MidY, stroke);

        using var tp = new SKPaint { IsAntialias = true, Color = look.TagText };
        if (!string.IsNullOrEmpty(inst.Letters))
        {
            using var f = TextFont.Create(inst.Letters, d.Text(ProcessBuilder.LetterSize), Roles.Weight("strong"), st.FontFamily);
            canvas.DrawText(inst.Letters, r.MidX, r.MidY - d.Text(3) - f.Metrics.Descent, SKTextAlign.Center, f, tp);
        }

        if (!string.IsNullOrEmpty(inst.Number))
        {
            using var f = TextFont.Create(inst.Number, d.Text(ProcessBuilder.NumberSize), Roles.Weight("strong"), st.FontFamily);
            canvas.DrawText(inst.Number, r.MidX, r.MidY + d.Text(3) - f.Metrics.Ascent, SKTextAlign.Center, f, tp);
        }

        if (!string.IsNullOrEmpty(inst.Value))
        {
            var ox = d.Origin.X + host.G.X;
            var oy = d.Origin.Y + host.G.Y;
            using var f = TextFont.Create(inst.Value, d.Text(ProcessBuilder.ValueSize), Roles.Weight("regular"), st.FontFamily);
            using var vp = new SKPaint { IsAntialias = true, Color = ThemeColor("--text") };
            canvas.DrawText(inst.Value, ox + inst.ValueRect.Left, oy + inst.ValueRect.Top - f.Metrics.Ascent, SKTextAlign.Left, f, vp);
        }
    }

    // 꺾은선 → 둥근 모서리 + 교차 점프(반원) path. 끝은 장비 안으로 조금 더 넣어 몸통 밑에 숨긴다
    private static SKPath PipePath(GraphLine g, ProcessLook look, bool hopVertical)
    {
        var pts = g.Points.ToList();
        var path = new SKPath();
        if (pts.Count < 2)
            return path;
        pts[0] = Inward(pts[0], pts[1], g.From.Face ? 24 : 6);
        pts[^1] = Inward(pts[^1], pts[^2], g.To.Face ? 24 : 6);

        path.MoveTo(pts[0]);
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            var len = Dist(a, b);
            var dir = len > 0 ? new SKPoint((b.X - a.X) / len, (b.Y - a.Y) / len) : new SKPoint(0, 0);

            // 이 구간의 점프(시작에서 가까운 순)
            var vertical = MathF.Abs(a.X - b.X) < 0.01f;
            if (vertical == hopVertical)
            {
                foreach (var h in g.Hops.Where(h => OnSegment(h, a, b)).OrderBy(h => Dist(a, h)))
                {
                    var r = look.Hop;
                    var p0 = new SKPoint(h.X - dir.X * r, h.Y - dir.Y * r);
                    var p1 = new SKPoint(h.X + dir.X * r, h.Y + dir.Y * r);
                    // 볼록한 쪽: 세로선은 오른쪽, 가로선은 위
                    var nrm = vertical ? new SKPoint(1, 0) : new SKPoint(0, -1);
                    var k = r * 4f / 3f;
                    path.LineTo(p0);
                    path.CubicTo(p0.X + nrm.X * k, p0.Y + nrm.Y * k, p1.X + nrm.X * k, p1.Y + nrm.Y * k, p1.X, p1.Y);
                }
            }

            if (i + 2 < pts.Count)
            {
                var c = pts[i + 2];
                var radius = MathF.Min(look.Corner, MathF.Min(len, Dist(b, c)) / 2);
                if (radius > 0.5f)
                    path.ArcTo(b, c, radius);
                else
                    path.LineTo(b);
            }
            else
            {
                path.LineTo(b);
            }
        }

        return path;
    }

    private static SKPoint Inward(SKPoint end, SKPoint next, float by)
    {
        var len = Dist(end, next);
        if (len <= 0)
            return end;
        return new SKPoint(end.X - (next.X - end.X) / len * by, end.Y - (next.Y - end.Y) / len * by);
    }

    private static float Dist(SKPoint a, SKPoint b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static bool OnSegment(SKPoint p, SKPoint a, SKPoint b) =>
        MathF.Abs(a.X - b.X) < 0.01f
            ? MathF.Abs(p.X - a.X) < 0.01f && p.Y >= MathF.Min(a.Y, b.Y) && p.Y <= MathF.Max(a.Y, b.Y)
            : MathF.Abs(p.Y - a.Y) < 0.01f && p.X >= MathF.Min(a.X, b.X) && p.X <= MathF.Max(a.X, b.X);

    // 포트로 들어가는 화살표 — 끝이 포트 점에 닿게
    private static void DrawArrow(SKCanvas canvas, GraphEnd end, ProcessLook look, SKPaint paint, float? length = null, float? halfWidth = null)
    {
        var (dx, dy) = end.Out switch
        {
            PortSide.Left => (-1f, 0f),
            PortSide.Right => (1f, 0f),
            PortSide.Top => (0f, -1f),
            _ => (0f, 1f),
        };
        float len = length ?? (look.Rich ? 14 : 10), half = halfWidth ?? (look.Rich ? 8 : 5);
        var tip = end.Point;
        var bx = tip.X + dx * len;
        var by = tip.Y + dy * len;
        using var p = new SKPath();
        p.MoveTo(tip);
        p.LineTo(bx - dy * half, by + dx * half);
        p.LineTo(bx + dy * half, by - dx * half);
        p.Close();
        canvas.DrawPath(p, paint);
    }

    // 장비 상자 — 심볼·부속·이름표(상자 Rect = 몸통, 차지 영역 원점 = 공정도 원점 + 배치 위치)
    private void PaintEquipment(SKCanvas canvas, LayoutBox box, ComputedStyle st, long timestampMs)
    {
        var n = box.Equipment!;
        var d = n.Owner;
        var look = LookOf(d);
        if (n.Unit is { } unit)
        {
            // 유닛 — 제목(왼쪽 위) + 안쪽 배관. 안쪽 장비는 이 상자의 자식으로 그 위에 그린다. 테두리·배경은 style
            if (!string.IsNullOrEmpty(n.Label))
            {
                using var font = TextFont.Create(n.Label, d.Text(ProcessBuilder.LabelSize), Roles.Weight("strong"), st.FontFamily);
                using var tp = new SKPaint { IsAntialias = true, Color = ThemeColor("--text") };
                canvas.DrawText(n.Label, d.Origin.X + n.G.X + n.LabelRect.Left, d.Origin.Y + n.G.Y + n.LabelRect.Top - font.Metrics.Ascent,
                    SKTextAlign.Left, font, tp);
            }

            if (unit.Graph is not null)
                PaintPipes(canvas, unit, LookOf(unit), timestampMs, ThemeColor("--text"), st.FontFamily);
            return;
        }

        canvas.Save();
        canvas.Translate(d.Origin.X + n.G.X, d.Origin.Y + n.G.Y);
        try
        {
            if (n.Terminal)
            {
                var (shape, _, _) = ProcessBuilder.TerminalShape(n, d.Vertical);
                using (shape)
                {
                    using var fill = new SKPaint { IsAntialias = true, Color = look.TagFill };
                    using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f, Color = look.TagStroke };
                    canvas.DrawPath(shape, fill);
                    canvas.DrawPath(shape, stroke);
                }

                var lines = ProcessBuilder.TerminalLines(n);
                using var font = TextFont.Create(n.Text ?? "", d.Text(12), Roles.Weight("strong"), st.FontFamily);
                using var tp = new SKPaint { IsAntialias = true, Color = look.TagText };
                var cx = d.Vertical ? n.Sym.MidX : n.Sym.Left + (n.Sym.Width - d.Text(14)) / 2;
                var cy = (d.Vertical ? n.Sym.Top + (n.Sym.Height - d.Text(10)) / 2 : n.Sym.MidY) - (font.Metrics.Ascent + font.Metrics.Descent) / 2
                    - (lines.Length - 1) * font.Spacing / 2; // 여러 줄이면 묶음 가운데를 맞춘다
                foreach (var line in lines)
                {
                    canvas.DrawText(line, cx, cy, SKTextAlign.Center, font, tp);
                    cy += font.Spacing;
                }
            }
            else
            {
                // 모터 축 — 모터 아래에서 장비 가운데까지, 장비 그림 밑에 그려 보이는 건 둘 사이 틈만
                foreach (var (r, def, _) in n.AccessoryRects)
                {
                    if (def.Name != "motor")
                        continue;
                    using var shaft = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = look.Rich ? 4 : 2, Color = look.Stroke };
                    canvas.DrawLine(r.MidX, r.Bottom - 1, r.MidX, n.Sym.MidY, shaft);
                }

                canvas.Save();
                canvas.Concat(n.Transform);
                DrawSymbol(canvas, n.Def!, look, n.On, n.Def!.FillValue is not null ? n.Level : null);
                canvas.Restore();

                foreach (var (r, def, running) in n.AccessoryRects)
                {
                    canvas.Save();
                    canvas.Translate(r.Left, r.Top);
                    DrawSymbol(canvas, def, look, p => p == "running" && running, null);
                    canvas.Restore();
                }

                PaintEquipmentLabel(canvas, n, look, st, ThemeColor("--text"));
            }

            if (n.Error)
            {
                using var ep = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = look.Error };
                var r = n.Body;
                r.Inflate(4, 4);
                canvas.DrawRoundRect(r, 4, 4, ep);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private static void PaintEquipmentLabel(SKCanvas canvas, ProcessNode n, ProcessLook look, ComputedStyle st, SKColor text)
    {
        if (string.IsNullOrEmpty(n.Label))
            return;
        if (n.IsVessel)
        {
            // 용기 이름표 — 몸통 가운데 칩(액체·금속 위에서도 읽히게)
            var d = n.Owner;
            using var font = TextFont.Create(n.Label, d.Text(14), Roles.Weight("strong"), st.FontFamily);
            var tw = font.MeasureText(n.Label);
            var chip = SKRect.Create(n.Sym.MidX - tw / 2 - d.Text(8), n.Sym.MidY - font.Spacing / 2 - d.Text(2), tw + d.Text(16), font.Spacing + d.Text(4));
            using var bg = new SKPaint { IsAntialias = true, Color = look.TagFill.WithAlpha(0xDD) };
            canvas.DrawRoundRect(chip, chip.Height / 2, chip.Height / 2, bg);
            using var tp = new SKPaint { IsAntialias = true, Color = look.TagText };
            canvas.DrawText(n.Label, chip.MidX, chip.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2, SKTextAlign.Center, font, tp);
            return;
        }

        using (var font = TextFont.Create(n.Label, n.Owner.Text(ProcessBuilder.LabelSize), Roles.Weight("strong"), st.FontFamily))
        using (var tp = new SKPaint { IsAntialias = true, Color = text })
            canvas.DrawText(n.Label, n.LabelRect.MidX, n.LabelRect.Top - font.Metrics.Ascent, SKTextAlign.Center, font, tp);
    }

    // 심볼 레이어 — 심볼 좌표계(0,0 ~ view-box 크기)에서 그린다
    private static void DrawSymbol(SKCanvas canvas, SymbolDef def, ProcessLook look, Func<string?, bool> on, float? level)
    {
        using var stroke = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = look.StrokeW, Color = look.Stroke, StrokeJoin = SKStrokeJoin.Round,
        };
        using var onBody = new SKPath(); // 켜진 state 층 — 어두운 몸통 위의 detail 만 밝게 그린다
        foreach (var l in def.Layers.Where(l => l.Class == "state" && on(l.When)))
            onBody.AddPath(l.Path);
        var liquidDone = false;
        foreach (var l in def.Layers)
        {
            var b = l.Path.Bounds;
            switch (l.Class)
            {
                case "vessel" or "state":
                {
                    bool? state = l.Class == "state" ? on(l.When) : null;
                    using (var fill = new SKPaint { IsAntialias = true, Shader = look.Body(b, state) })
                    {
                        if (look.Rich)
                            fill.ImageFilter = SKImageFilter.CreateDropShadow(0, 3, 3, 3, Fade(new SKColor(0x66000000), look.Depth));
                        canvas.DrawPath(l.Path, fill);
                    }

                    if (level is { } lv && !liquidDone && l.Class == def.FillClip)
                    {
                        liquidDone = true;
                        DrawLiquid(canvas, def, look, lv);
                    }

                    canvas.DrawPath(l.Path, stroke);
                    break;
                }
                case "accent":
                    using (var fill = new SKPaint { IsAntialias = true, Shader = look.Accent(b) })
                        canvas.DrawPath(l.Path, fill);
                    canvas.DrawPath(l.Path, stroke);
                    break;
                case "screen" or "glass":
                    using (var fill = new SKPaint { IsAntialias = true, Color = l.Class == "screen" ? look.Screen : look.Glass })
                        canvas.DrawPath(l.Path, fill);
                    canvas.DrawPath(l.Path, stroke);
                    break;
                case "detail" or "dash":
                    using (var dp = new SKPaint
                           {
                               IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f, StrokeCap = SKStrokeCap.Round,
                               Color = look.Detail,
                           })
                    {
                        if (l.Class == "dash")
                            dp.PathEffect = SKPathEffect.CreateDash([4, 3], 0);
                        if (onBody.IsEmpty)
                        {
                            canvas.DrawPath(l.Path, dp);
                            break;
                        }

                        // 몸통 밖(밸브 줄기 등)은 바탕 위라 평소 색이어야 보인다
                        canvas.Save();
                        canvas.ClipPath(onBody, SKClipOperation.Difference, true);
                        canvas.DrawPath(l.Path, dp);
                        canvas.Restore();
                        canvas.Save();
                        canvas.ClipPath(onBody, SKClipOperation.Intersect, true);
                        dp.Color = look.DetailOn;
                        canvas.DrawPath(l.Path, dp);
                        canvas.Restore();
                    }

                    break;
            }
        }
    }

    // 수위 — 채움 class 레이어들의 합친 모양으로 잘라 아래부터 채운다
    private static void DrawLiquid(SKCanvas canvas, SymbolDef def, ProcessLook look, float level)
    {
        if (level <= 0)
            return;
        using var clip = new SKPath();
        foreach (var l in def.Layers.Where(l => l.Class == def.FillClip))
            clip.AddPath(l.Path);
        var b = clip.Bounds;
        var top = b.Bottom - b.Height * level / 100f;
        var lr = new SKRect(b.Left, top, b.Right, b.Bottom);
        canvas.Save();
        canvas.ClipPath(clip, antialias: true);
        using (var lp = new SKPaint { IsAntialias = true, Shader = look.Liquid(lr) })
            canvas.DrawRect(lr, lp);
        if (look.Rich)
        {
            using var sheen = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(new SKPoint(b.Left, 0), new SKPoint(b.Right, 0),
                    [Fade(new SKColor(0x55000000), look.Depth), new SKColor(0x00FFFFFF), Fade(new SKColor(0x40FFFFFF), look.Depth), new SKColor(0x00FFFFFF), Fade(new SKColor(0x66000000), look.Depth)],
                    [0, .3f, .4f, .62f, 1], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(lr, sheen);
        }

        using (var surf = new SKPaint
               {
                   IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f,
                   Color = look.LiquidSurface,
               })
            canvas.DrawLine(b.Left, top, b.Right, top, surf);
        canvas.Restore();
    }
}
