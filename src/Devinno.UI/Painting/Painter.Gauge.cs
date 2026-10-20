using System.Globalization;
using Devinno.UI.Layout;
using Devinno.UI.Text;
using Devinno.UI.Widgets;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    // 눈금: 트랙 안쪽에 큰/작은 눈금, 큰 눈금 숫자는 회전 없이 정립(아래쪽도 읽기 쉽게)
    private void DrawGaugeScale(SKCanvas canvas, LayoutBox box, float cx, float cy, WidgetMetrics.GaugeMetrics gm,
        float startAngle, float sweep)
    {
        var radius = gm.Radius;
        var range = box.GaugeMax - box.GaugeMin;
        if (range <= 0)
            return;

        var tickColor = ThemeColor("gauge-tick", new SKColor(0x94, 0xa3, 0xb8));
        float Angle(double v) => (startAngle + (float)((v - box.GaugeMin) / range) * sweep) * MathF.PI / 180f;

        using var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, Color = tickColor };

        if (box.GaugeMinorTicks > 0)
        {
            p.StrokeWidth = MathF.Max(1f, gm.Scale);
            foreach (var v in ScaleTicks.Values(box.GaugeMin, box.GaugeMax, box.GaugeMinorTicks))
            {
                var a = Angle(v);
                canvas.DrawLine(cx + MathF.Cos(a) * (radius - gm.TickInset), cy + MathF.Sin(a) * (radius - gm.TickInset),
                    cx + MathF.Cos(a) * (radius - gm.MinorTickEnd), cy + MathF.Sin(a) * (radius - gm.MinorTickEnd), p);
            }
        }

        var major = ScaleTicks.Values(box.GaugeMin, box.GaugeMax, box.GaugeTicks);
        p.StrokeWidth = MathF.Max(1f, 2f * gm.Scale);
        foreach (var v in major)
        {
            var a = Angle(v);
            canvas.DrawLine(cx + MathF.Cos(a) * (radius - gm.TickInset), cy + MathF.Sin(a) * (radius - gm.TickInset),
                cx + MathF.Cos(a) * (radius - gm.MajorTickEnd), cy + MathF.Sin(a) * (radius - gm.MajorTickEnd), p);
        }

        if (!box.GaugeTickLabels)
            return;

        var decimals = Interaction.KnobBinder.Decimals(box.GaugeTicks);
        using var font = TextFont.Create("0", MathF.Max(9f, radius * 0.15f), bold: false);
        using var tp = new SKPaint { IsAntialias = true, Color = ThemeColor("text-muted", new SKColor(0x64, 0x74, 0x8b)) };
        var labelR = radius - gm.MajorTickEnd - font.Size * 0.95f;
        foreach (var v in major)
        {
            var a = Angle(v);
            var text = v.ToString("F" + decimals, CultureInfo.InvariantCulture);
            var x = cx + MathF.Cos(a) * labelR;
            var y = cy + MathF.Sin(a) * labelR - (font.Metrics.Ascent + font.Metrics.Descent) / 2f;
            canvas.DrawText(text, x, y, SKTextAlign.Center, font, tp);
        }
    }

    // 바늘: 끝으로 가늘어지는 모양, 앞쪽 40% 강조색(현재 zone 색 → 없으면 gauge-needle-tip), 그림자, 2중 허브
    private void DrawGaugeNeedle(SKCanvas canvas, LayoutBox box, float cx, float cy, float length,
        float angleDeg, SKColor needleColor, WidgetMetrics.GaugeMetrics gm)
    {
        var a = angleDeg * MathF.PI / 180f;
        var dir = new SKPoint(MathF.Cos(a), MathF.Sin(a));
        var nrm = new SKPoint(-dir.Y, dir.X);
        var half = gm.NeedleHalf;
        var tail = gm.NeedleTail;
        var tailHalf = 1.5f * gm.Scale;

        using var path = new SKPath();
        path.MoveTo(cx + dir.X * length, cy + dir.Y * length);
        path.LineTo(cx + nrm.X * half, cy + nrm.Y * half);
        path.LineTo(cx - dir.X * tail + nrm.X * tailHalf, cy - dir.Y * tail + nrm.Y * tailHalf);
        path.LineTo(cx - dir.X * tail - nrm.X * tailHalf, cy - dir.Y * tail - nrm.Y * tailHalf);
        path.LineTo(cx - nrm.X * half, cy - nrm.Y * half);
        path.Close();

        var tip = ZoneColorAt(box) ?? ThemeColor("gauge-needle-tip", new SKColor(0xef, 0x44, 0x44));
        using (var p = new SKPaint { IsAntialias = true })
        {
            p.Shader = SKShader.CreateRadialGradient(new SKPoint(cx, cy), length,
                new[] { needleColor, needleColor, tip, tip }, new[] { 0f, 0.6f, 0.61f, 1f }, SKShaderTileMode.Clamp);
            var sh = 1.5f * gm.Scale;
            p.ImageFilter = SKImageFilter.CreateDropShadow(sh, sh, sh, sh, new SKColor(0, 0, 0, 0x60));
            canvas.DrawPath(path, p);
        }

        using (var hub = new SKPaint { IsAntialias = true, Color = needleColor })
            canvas.DrawCircle(cx, cy, gm.HubRadius, hub);
        using (var dot = new SKPaint { IsAntialias = true, Color = ThemeColor("gauge-track", new SKColor(0xe2, 0xe8, 0xf0)) })
            canvas.DrawCircle(cx, cy, gm.HubDot, dot);
    }
}
