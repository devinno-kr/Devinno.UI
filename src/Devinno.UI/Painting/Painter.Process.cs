using Devinno.UI.Layout;
using Devinno.UI.Process;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

// 공정도(DvProcess) — 배관(겹별로 한 번에), 화살표, 장비 심볼, 이름표. 모양은 look(plain·rich)
public sealed partial class Painter
{
    /// <summary>Process diagram look — plain (ISA-101 style gray, color only for states) · rich (dark background, metallic gradients, thick pipes).</summary>
    private sealed class ProcessLook
    {
        public bool Rich;
        public SKColor Stroke, Detail, DetailOn, Screen, Glass, TagFill, TagStroke, TagText, Arrow;
        public float StrokeW, Corner, Hop;

        public static readonly ProcessLook Plain = new()
        {
            Stroke = new(0xFF464B51), StrokeW = 1.5f, Detail = new(0xFF464B51), DetailOn = new(0xFFE6E8EA),
            Screen = new(0xFFF4F5F6), Glass = new(0xFFE9EEF2), TagFill = new(0xFFF1F2F3), TagStroke = new(0xFF464B51),
            TagText = new(0xFF2E3338), Arrow = new(0xFF5F656C), Corner = 6, Hop = 7,
        };

        public static readonly ProcessLook RichLook = new()
        {
            Rich = true, Stroke = new(0xFF0E151D), StrokeW = 1.2f, Detail = new(0xFF1A222B), DetailOn = new(0xFF0E3D24),
            Screen = new(0xFF0B1620), Glass = new(0xFF7FD3F7), TagFill = new(0xFF243242), TagStroke = new(0xFF7F95AB),
            TagText = new(0xFFDCE4EC), Arrow = new(0xFFC7D0D9), Corner = 10, Hop = 13,
        };

        private static SKShader HGrad(SKRect r, params (uint c, float p)[] stops) =>
            SKShader.CreateLinearGradient(new SKPoint(r.Left, 0), new SKPoint(r.Right, 0),
                stops.Select(s => new SKColor(s.c)).ToArray(), stops.Select(s => s.p).ToArray(), SKShaderTileMode.Clamp);

        // on: null = 상태 없는 몸통(vessel), true/false = 상태색 몸통
        public SKShader Body(SKRect r, bool? on) => Rich
            ? on switch
            {
                null => HGrad(r, (0xFF5E6975, 0), (0xFFB9C4CF, .28f), (0xFFEEF3F7, .4f), (0xFFA3AFBB, .7f), (0xFF4C5662, 1)),
                true => HGrad(r, (0xFF16703F, 0), (0xFF35C27A, .28f), (0xFF8AF0BC, .4f), (0xFF2AA463, .7f), (0xFF125C34, 1)),
                false => HGrad(r, (0xFF4A525C, 0), (0xFF8F98A2, .28f), (0xFFC2C9D0, .4f), (0xFF7D8690, .7f), (0xFF3E464F, 1)),
            }
            : SKShader.CreateColor(new SKColor(on switch { true => 0xFF4A5057u, false => 0xFFF1F2F3u, null => 0xFFC3C8CDu }));

        public SKShader Accent(SKRect r) => Rich
            ? HGrad(r, (0xFF2B333D, 0), (0xFF5D6874, .35f), (0xFF7B8794, .45f), (0xFF3A434E, 1))
            : SKShader.CreateColor(new SKColor(0xFF8E959C));

        public SKShader Liquid(SKRect r) => Rich
            ? SKShader.CreateLinearGradient(new SKPoint(0, r.Top), new SKPoint(0, r.Bottom), [new SKColor(0xFF4FB8F0), new SKColor(0xFF1E64A8)], SKShaderTileMode.Clamp)
            : SKShader.CreateColor(new SKColor(0xFF93A9BF));

        // 매체 색(KS A 0503 배관 식별색 계열) — plain 은 밝은 바탕에서 읽히는 차분한 톤, rich 는 금속 관 가운데 줄
        public SKColor PipeColor(string? medium) => (medium, Rich) switch
        {
            ("water", false) => new SKColor(0xFF3B6EA8), ("water", true) => new SKColor(0xFF4F86C6),
            ("steam", false) => new SKColor(0xFF9B3B3B), ("steam", true) => new SKColor(0xFFB0524F),
            ("air", false) => new SKColor(0xFFAAB5BE), ("air", true) => new SKColor(0xFFE6ECF0),
            ("gas", false) => new SKColor(0xFFC29A1F), ("gas", true) => new SKColor(0xFFE3C04A),
            ("oil", false) => new SKColor(0xFFB5652B), ("oil", true) => new SKColor(0xFFC9783C),
            ("acid", false) => new SKColor(0xFF7E6A93), ("acid", true) => new SKColor(0xFF9A86B0),
            (_, false) => new SKColor(0xFF5F656C),
            (_, true) => new SKColor(0xFF8C98A5),
        };

        // 배관 — 겹별로 모든 관을 한 번에(T 자 이음매가 보이지 않게). 매체가 있는 관은 그 색
        public void Pipes(SKCanvas c, IReadOnlyList<(SKPath Path, string? Medium)> paths)
        {
            using var body = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Rich ? 7 : 3 };
            if (Rich)
            {
                using var a = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 11, Color = new SKColor(0xFF3D4753) };
                using var h = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = new SKColor(0x99E3EAF0) };
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

    private static ProcessLook LookOf(ProcessDiagram d) => d.Rich ? ProcessLook.RichLook : ProcessLook.Plain;

    // 공정도 상자 — 배관·화살표·오류 선(배경은 앱 테마·style 그대로). 장비는 자식 상자로 그 위에 그린다
    private void PaintProcess(SKCanvas canvas, LayoutBox box, long timestampMs)
    {
        var d = box.Process!;
        if (d.Graph is null)
            return;
        ProcessBuilder.Report(d);
        PaintPipes(canvas, d, timestampMs);
    }

    /// <summary>Flow animation frame interval (ms) — capped at 20 frames per second (saves CPU, since the whole screen is redrawn).</summary>
    internal const int FlowFrameMs = 50;

    /// <summary>Speed of the moving flow dashes (px/s).</summary>
    private const float FlowSpeed = 40f;

    // 흐르는 배관 위 표시 — 바탕 줄(rich 어둡게·plain 밝게) 위에 굵은 점선이 흐름 방향(from → to)으로 움직인다.
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
            Color = rich ? new SKColor(0xFF0B3A4A) : new SKColor(0xFFDCE8FF),
        };
        using var effect = SKPathEffect.CreateDash([dash, gap], period - offset); // 위상을 줄이면 무늬가 경로 앞쪽으로 나아간다
        using var paint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, PathEffect = effect, StrokeWidth = width,
            Color = rich ? new SKColor(0xFF22D3EE) : new SKColor(0xFF2563EB),
        };
        var flowColor = paint.Color;
        foreach (var (path, flowing, medium) in pipes)
        {
            if (!flowing)
                continue;
            if (medium is null)
            {
                under.Color = rich ? new SKColor(0xFF0B3A4A) : new SKColor(0xFFDCE8FF);
                paint.Color = flowColor;
                canvas.DrawPath(path, under);
                canvas.DrawPath(path, paint);
            }
            else
            {
                var m = look.PipeColor(medium);
                under.Color = rich ? Mix(m, SKColors.Black, 0.55f) : Mix(m, SKColors.White, 0.72f);
                paint.Color = rich ? Mix(m, SKColors.White, 0.35f) : m;
                canvas.DrawPath(path, under);
                canvas.DrawPath(path, paint);
            }
        }
    }

    private static void PaintPipes(SKCanvas canvas, ProcessDiagram d, long timestampMs)
    {
        var look = LookOf(d);
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

            look.Pipes(canvas, pipes);
            if (d.FlowAnimation)
                PaintFlow(canvas, flowing, look, timestampMs);
            PaintSignals(canvas, d, look);

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

            // 잘못된 선 — 두 장비 가운데를 빨간 점선으로
            using var err = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = new SKColor(0xFFE5484D),
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

    // 신호선 — 가는 점선 직교(ISA-5.1 전기 신호), 점프 없음. 배관 위에 긋는다(엇갈리는 곳에서 끊겨 보이지 않게)
    private static void PaintSignals(SKCanvas canvas, ProcessDiagram d, ProcessLook look)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = look.Arrow,
            PathEffect = SKPathEffect.CreateDash([5, 3], 0),
        };
        foreach (var s in d.Signals)
        {
            if (s.G is not { Points.Count: >= 2 } g)
                continue;
            using var path = new SKPath();
            path.MoveTo(g.Points[0]);
            for (var i = 1; i < g.Points.Count; i++)
                path.LineTo(g.Points[i]);
            canvas.DrawPath(path, paint);
        }
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
            using var f = TextFont.Create(inst.Letters, ProcessBuilder.LetterSize, true, st.FontFamily);
            canvas.DrawText(inst.Letters, r.MidX, r.MidY - 3 - f.Metrics.Descent, SKTextAlign.Center, f, tp);
        }

        if (!string.IsNullOrEmpty(inst.Number))
        {
            using var f = TextFont.Create(inst.Number, ProcessBuilder.NumberSize, true, st.FontFamily);
            canvas.DrawText(inst.Number, r.MidX, r.MidY + 3 - f.Metrics.Ascent, SKTextAlign.Center, f, tp);
        }

        if (!string.IsNullOrEmpty(inst.Value))
        {
            var ox = d.Origin.X + host.G.X;
            var oy = d.Origin.Y + host.G.Y;
            using var f = TextFont.Create(inst.Value, ProcessBuilder.ValueSize, false, st.FontFamily);
            using var vp = new SKPaint { IsAntialias = true, Color = ThemeColor("--text", st.Color.ToSK()) };
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
    private static void DrawArrow(SKCanvas canvas, GraphEnd end, ProcessLook look, SKPaint paint)
    {
        var (dx, dy) = end.Out switch
        {
            PortSide.Left => (-1f, 0f),
            PortSide.Right => (1f, 0f),
            PortSide.Top => (0f, -1f),
            _ => (0f, 1f),
        };
        float len = look.Rich ? 14 : 10, half = look.Rich ? 8 : 5;
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
                using var font = TextFont.Create(n.Label, ProcessBuilder.LabelSize, true, st.FontFamily);
                using var tp = new SKPaint { IsAntialias = true, Color = ThemeColor("--text", st.Color.ToSK()) };
                canvas.DrawText(n.Label, d.Origin.X + n.G.X + n.LabelRect.Left, d.Origin.Y + n.G.Y + n.LabelRect.Top - font.Metrics.Ascent,
                    SKTextAlign.Left, font, tp);
            }

            if (unit.Graph is not null)
                PaintPipes(canvas, unit, timestampMs);
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

                var text = n.Text ?? "";
                using var font = TextFont.Create(text, 12, true, st.FontFamily);
                using var tp = new SKPaint { IsAntialias = true, Color = look.TagText };
                var cx = d.Vertical ? n.Sym.MidX : n.Sym.Left + (n.Sym.Width - 14) / 2;
                var cy = (d.Vertical ? n.Sym.Top + (n.Sym.Height - 10) / 2 : n.Sym.MidY) - (font.Metrics.Ascent + font.Metrics.Descent) / 2;
                canvas.DrawText(text, cx, cy, SKTextAlign.Center, font, tp);
            }
            else
            {
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

                PaintEquipmentLabel(canvas, n, look, st, ThemeColor("--text", st.Color.ToSK()));
            }

            if (n.Error)
            {
                using var ep = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = new SKColor(0xFFE5484D) };
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
            using var font = TextFont.Create(n.Label, 14, true, st.FontFamily);
            var tw = font.MeasureText(n.Label);
            var chip = SKRect.Create(n.Sym.MidX - tw / 2 - 8, n.Sym.MidY - font.Spacing / 2 - 2, tw + 16, font.Spacing + 4);
            using var bg = new SKPaint { IsAntialias = true, Color = look.Rich ? new SKColor(0xCC0E151D) : new SKColor(0xDDFFFFFF) };
            canvas.DrawRoundRect(chip, chip.Height / 2, chip.Height / 2, bg);
            using var tp = new SKPaint { IsAntialias = true, Color = look.Rich ? new SKColor(0xFFDCE4EC) : new SKColor(0xFF2E3338) };
            canvas.DrawText(n.Label, chip.MidX, chip.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2, SKTextAlign.Center, font, tp);
            return;
        }

        using (var font = TextFont.Create(n.Label, ProcessBuilder.LabelSize, true, st.FontFamily))
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
        var anyStateOn = def.Layers.Any(l => l.Class == "state" && on(l.When));
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
                            fill.ImageFilter = SKImageFilter.CreateDropShadow(0, 3, 3, 3, new SKColor(0x66000000));
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
                               Color = anyStateOn ? look.DetailOn : look.Detail,
                           })
                    {
                        if (l.Class == "dash")
                            dp.PathEffect = SKPathEffect.CreateDash([4, 3], 0);
                        canvas.DrawPath(l.Path, dp);
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
                    [new SKColor(0x55000000), new SKColor(0x00FFFFFF), new SKColor(0x40FFFFFF), new SKColor(0x00FFFFFF), new SKColor(0x66000000)],
                    [0, .3f, .4f, .62f, 1], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(lr, sheen);
        }

        using (var surf = new SKPaint
               {
                   IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f,
                   Color = look.Rich ? new SKColor(0xCCA8E2FF) : new SKColor(0xFF6E8399),
               })
            canvas.DrawLine(b.Left, top, b.Right, top, surf);
        canvas.Restore();
    }
}
