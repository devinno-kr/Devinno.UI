using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    // DvKnob — 270° 트랙 + 값 호 + 면(face) + 지시선 + 값/단위. 색은 테마 토큰(knob-*), 없으면 Light 계열.
    private void PaintKnob(SKCanvas canvas, LayoutBox box, InteractionState? state)
    {
        const float startAngle = 135f;
        const float sweep = 270f;

        var rect = box.Rect;
        var cx = rect.MidX;
        var cy = rect.MidY;
        var km = WidgetMetrics.Knob(rect);
        var stroke = km.Stroke;
        var radius = km.Radius;
        if (radius <= 0)
            return;

        var oval = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        var range = box.KnobMax - box.KnobMin;
        var t = range <= 0 ? 0f : Math.Clamp((box.KnobValue - box.KnobMin) / range, 0f, 1f);

        var track = ThemeColor("gauge-track", new SKColor(0xe2, 0xe8, 0xf0));
        var fill = ThemeColor("knob-fill", new SKColor(0x25, 0x63, 0xeb));
        var face = ThemeColor("knob-face", new SKColor(0xf8, 0xfa, 0xfc));
        var ink = ThemeColor("knob-indicator", new SKColor(0x0f, 0x17, 0x2a));

        using (var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, Color = track })
        using (var path = new SKPath())
        {
            path.AddArc(oval, startAngle, sweep);
            canvas.DrawPath(path, p);
        }

        if (t > 0f)
        {
            using var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, Color = fill };
            using var path = new SKPath();
            path.AddArc(oval, startAngle, sweep * t);
            canvas.DrawPath(path, p);
        }

        // 면 — 그림자 + 손잡이 홈(knurl) 질감 + 테두리. 홈은 값 각도와 함께 회전.
        var faceR = radius - km.FaceInset;
        var valueAngle = startAngle + t * sweep;
        if (faceR > 4f * km.Scale)
        {
            // 상호작용 피드백: hover 6%, 드래그(press) 10% 밝게 — 비활성이면 없음
            if (!box.Disabled && state is not null && ReferenceEquals(state.Press, box))
                face = Mix(face, SKColors.White, 0.10f);
            else if (!box.Disabled && state is not null && ReferenceEquals(state.Hover, box))
                face = Mix(face, SKColors.White, 0.06f);

            using (var sh = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 0x48) })
            {
                sh.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, km.ShadowBlur);
                canvas.DrawCircle(cx, cy + km.ShadowDy, faceR, sh);
            }

            var light = Mix(face, SKColors.White, 0.07f);
            var dark = Mix(face, SKColors.Black, 0.10f);
            using (var p = new SKPaint { IsAntialias = true })
            {
                p.Shader = SKShader.CreateSweepGradient(
                    new SKPoint(cx, cy),
                    new[] { light, dark, light, dark, light, dark, light, dark, light, dark, light },
                    null, SKShaderTileMode.Repeat, valueAngle, valueAngle + 360f);
                canvas.DrawCircle(cx, cy, faceR, p);
            }

            var isDarkFace = face.Red * 0.299f + face.Green * 0.587f + face.Blue * 0.114f < 128f;
            using (var rim = new SKPaint
                   {
                       IsAntialias = true,
                       Style = SKPaintStyle.Stroke,
                       StrokeWidth = km.RimWidth,
                       Color = isDarkFace ? Mix(face, SKColors.White, 0.18f) : Mix(face, SKColors.Black, 0.14f)
                   })
                canvas.DrawCircle(cx, cy, faceR, rim);

            // 지시선 — 캡슐(둥근 굵은 막대) + 작은 그림자
            var a = valueAngle * MathF.PI / 180f;
            using var ip = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = MathF.Max(3f * km.Scale, faceR * 0.16f),
                StrokeCap = SKStrokeCap.Round,
                Color = fill,
                ImageFilter = SKImageFilter.CreateDropShadow(0.8f * km.Scale, 0.8f * km.Scale, 1.2f * km.Scale, 1.2f * km.Scale,
                    new SKColor(0, 0, 0, 0x90))
            };
            canvas.DrawLine(
                cx + MathF.Cos(a) * faceR * 0.52f, cy + MathF.Sin(a) * faceR * 0.52f,
                cx + MathF.Cos(a) * faceR * 0.80f, cy + MathF.Sin(a) * faceR * 0.80f, ip);
        }

        var label = box.KnobValue.ToString("F" + Interaction.KnobBinder.Decimals(box.KnobStep),
            System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(box.KnobUnit))
            label += " " + box.KnobUnit;

        using var font = TextFont.Create(label, MathF.Max(11f, radius * 0.34f), bold: true);
        using var tp = new SKPaint { IsAntialias = true, Color = ink };
        var baseline = cy - (font.Metrics.Ascent + font.Metrics.Descent) / 2f;
        canvas.DrawText(label, cx, baseline, SKTextAlign.Center, font, tp);
    }

    private static SKColor Mix(SKColor a, SKColor b, float t) => new(
        (byte)(a.Red + (b.Red - a.Red) * t),
        (byte)(a.Green + (b.Green - a.Green) * t),
        (byte)(a.Blue + (b.Blue - a.Blue) * t),
        a.Alpha);
}
