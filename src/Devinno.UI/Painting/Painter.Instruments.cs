using System.Globalization;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    // DvProgress — 알약 트랙 + 진행 방향 채움(트랙 모양으로 클립) + 선택적 2색 대비 값 표시
    private void PaintProgress(SKCanvas canvas, LayoutBox box, ComputedStyle st)
    {
        var rect = box.Rect;
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        var pill = MathF.Min(rect.Width, rect.Height) / 2f;
        var r = st.BorderRadius > 0 ? MathF.Min(st.BorderRadius, pill) : pill;
        var t = Fraction(box);

        using (var track = new SKPaint { IsAntialias = true, Color = ThemeColor("gauge-track", new SKColor(0xe2, 0xe8, 0xf0)) })
            canvas.DrawRoundRect(rect, r, r, track);

        var fill = box.Direction switch
        {
            "left" => new SKRect(rect.Right - rect.Width * t, rect.Top, rect.Right, rect.Bottom),
            "up" => new SKRect(rect.Left, rect.Bottom - rect.Height * t, rect.Right, rect.Bottom),
            "down" => new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + rect.Height * t),
            _ => new SKRect(rect.Left, rect.Top, rect.Left + rect.Width * t, rect.Bottom)
        };
        var (from, to) = box.Direction switch
        {
            "left" => (new SKPoint(rect.Right, rect.MidY), new SKPoint(fill.Left, rect.MidY)),
            "up" => (new SKPoint(rect.MidX, rect.Bottom), new SKPoint(rect.MidX, fill.Top)),
            "down" => (new SKPoint(rect.MidX, rect.Top), new SKPoint(rect.MidX, fill.Bottom)),
            _ => (new SKPoint(rect.Left, rect.MidY), new SKPoint(fill.Right, rect.MidY))
        };

        var color = ValueColor(box);
        if (t > 0f)
        {
            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(rect, r, r), SKClipOperation.Intersect, antialias: true);
            using var p = new SKPaint { IsAntialias = true };
            p.Shader = SKShader.CreateLinearGradient(from, to, new[] { color, Mix(color, SKColors.White, 0.25f) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(fill, r, r, p);
            canvas.Restore();
        }

        if (!box.ShowValue || box.Direction is "up" or "down" || rect.Height < 14f)
            return;

        // 값 텍스트: 채움 위는 흰색, 빈 트랙 위는 본문색 — 경계를 넘어가도 항상 읽힘
        var text = FormatValue(box.GaugeValue, box.ValueFormat) + (string.IsNullOrEmpty(box.Unit) ? "" : " " + box.Unit);
        using var font = TextFont.Create(text, MathF.Min(14f, rect.Height * 0.62f), bold: true);
        var baseline = rect.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2f;

        canvas.Save();
        canvas.ClipRect(fill);
        using (var onFill = new SKPaint { IsAntialias = true, Color = SKColors.White })
            canvas.DrawText(text, rect.MidX, baseline, SKTextAlign.Center, font, onFill);
        canvas.Restore();

        canvas.Save();
        canvas.ClipRect(rect);
        canvas.ClipRect(fill, SKClipOperation.Difference);
        using (var onTrack = new SKPaint { IsAntialias = true, Color = ThemeColor("text", new SKColor(0x0f, 0x17, 0x2a)) })
            canvas.DrawText(text, rect.MidX, baseline, SKTextAlign.Center, font, onTrack);
        canvas.Restore();
    }

    // DvRing — 굵은 호 채움(둥근 끝) + 그라디언트(이음매는 빈 구간 가운데로) + 은은한 발광 + 중앙 값/캡션
    private void PaintRing(SKCanvas canvas, LayoutBox box)
    {
        var rect = box.Rect;
        var size = MathF.Min(rect.Width, rect.Height);
        if (size <= 4f)
            return;

        var th = box.RingThickness ?? size * 0.11f;
        var cx = rect.MidX;
        var cy = rect.MidY;
        var r = size / 2f - th / 2f - 1f;
        var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);
        var sweep = box.RingSweep;
        var start = sweep >= 360f ? -90f : 90f + (360f - sweep) / 2f;
        var t = Fraction(box);

        using (var track = new SKPaint
               {
                   IsAntialias = true,
                   Style = SKPaintStyle.Stroke,
                   StrokeWidth = th,
                   StrokeCap = SKStrokeCap.Round,
                   Color = ThemeColor("gauge-track", new SKColor(0xe2, 0xe8, 0xf0))
               })
        {
            if (sweep >= 360f)
                canvas.DrawCircle(cx, cy, r, track);
            else
                canvas.DrawArc(oval, start, sweep, false, track);
        }

        var color = ValueColor(box);
        if (t > 0f)
        {
            var valueSweep = sweep * t;
            // 이음매를 채움 시작보다 앞(빈 구간 가운데, 360° 면 둥근 끝 각도 + 1°)에 둔다
            var capDeg = th / 2f / r * 180f / MathF.PI;
            var pre = sweep >= 360f ? capDeg + 1f : (360f - sweep) / 2f;
            using var p = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = th,
                StrokeCap = SKStrokeCap.Round
            };
            // 로컬 0°(이음매)를 빈 구간 가운데에 두도록 회전 — 채움 시작 모서리에서 색이 튀지 않음
            p.Shader = SKShader.CreateSweepGradient(new SKPoint(cx, cy),
                new[] { Mix(color, SKColors.Black, 0.08f), Mix(color, SKColors.White, 0.28f) },
                new[] { pre / 360f, MathF.Min(1f, (pre + valueSweep) / 360f) },
                SKShaderTileMode.Clamp, 0f, 360f,
                SKMatrix.CreateRotationDegrees(start - pre, cx, cy));
            p.ImageFilter = SKImageFilter.CreateDropShadow(0, 0, 3.5f, 3.5f, color.WithAlpha(0x55));
            canvas.DrawArc(oval, start, valueSweep, false, p);
        }

        var text = FormatValue(box.GaugeValue, box.ValueFormat) + (string.IsNullOrEmpty(box.Unit) ? "" : " " + box.Unit);
        var hasLabel = !string.IsNullOrEmpty(box.Label);
        using var font = TextFont.Create(text, MathF.Max(10f, r * 0.42f), bold: true);
        using var tp = new SKPaint { IsAntialias = true, Color = ThemeColor("text", new SKColor(0x0f, 0x17, 0x2a)) };
        var valueY = cy - (hasLabel ? r * 0.10f : 0f);
        canvas.DrawText(text, cx, valueY - (font.Metrics.Ascent + font.Metrics.Descent) / 2f, SKTextAlign.Center, font, tp);

        if (hasLabel)
        {
            using var lf = TextFont.Create(box.Label!, MathF.Max(9f, r * 0.16f), bold: true);
            using var lp = new SKPaint { IsAntialias = true, Color = ThemeColor("text-muted", new SKColor(0x64, 0x74, 0x8b)) };
            canvas.DrawText(box.Label!, cx, cy + r * 0.36f, SKTextAlign.Center, lf, lp);
        }
    }

    private static float Fraction(LayoutBox box)
    {
        var range = box.GaugeMax - box.GaugeMin;
        return range <= 0 ? 0f : Math.Clamp((box.GaugeValue - box.GaugeMin) / range, 0f, 1f);
    }

    // 현재값 zone 색 → 없으면 tone 토큰(무테마면 DvTheme.Default 값)
    private SKColor ValueColor(LayoutBox box)
    {
        if (ZoneColorAt(box) is { } z)
            return z;
        var theme = _theme;
        return theme.TryGet("tone-" + box.Tone, out var raw) && DvColor.TryParse(raw, out var c)
            ? c.ToSK()
            : new SKColor(0x25, 0x63, 0xeb);
    }

    // format(.NET 형식, InvariantCulture)이 있으면 그대로, 없거나 쓸 수 없으면 정수는 정수·아니면 소수 한 자리
    internal static string FormatValue(float v, string? format = null)
    {
        if (!string.IsNullOrEmpty(format))
        {
            try
            {
                return v.ToString(format, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
            }
        }

        return MathF.Abs(v - MathF.Round(v)) < 0.005f
            ? MathF.Round(v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
