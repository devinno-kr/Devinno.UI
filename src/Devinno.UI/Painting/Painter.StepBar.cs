using Devinno.UI.Layout;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    // DvStepBar — 단계마다 같은 폭 칸, 가운데 점(지름 28) + 아래 이름. 점 사이는 선으로 잇는다.
    // 끝난 단계 = primary 채움 + ✓, 진행 중 = primary 테두리 + 번호(굵게), 대기 = border-strong 테두리 + 흐린 번호.
    // 선은 양쪽 점이 모두 끝났거나(진행 중 포함) 지나간 구간이면 primary, 아니면 border.
    private void PaintStepBar(SKCanvas canvas, LayoutBox box)
    {
        const float dot = 28f;
        var names = box.StepNames;
        if (names.Length == 0)
            return;
        var rect = box.Rect;
        var cell = rect.Width / names.Length;
        var cy = rect.Top + 2f + dot / 2f;
        var current = box.StepCurrent;
        var primary = ThemeColor("primary", new SKColor(0x25, 0x63, 0xeb));
        var border = ThemeColor("border-strong", new SKColor(0x94, 0xa3, 0xb8));
        var text = ThemeColor("text", new SKColor(0x22, 0x22, 0x22));
        var muted = ThemeColor("text-muted", new SKColor(0x64, 0x74, 0x8b));

        using var p = new SKPaint { IsAntialias = true, StrokeWidth = 3f };
        for (var i = 0; i + 1 < names.Length; i++)
        {
            var x0 = rect.Left + cell * (i + 0.5f) + dot / 2f + 4f;
            var x1 = rect.Left + cell * (i + 1.5f) - dot / 2f - 4f;
            if (x1 <= x0)
                continue;
            p.Color = i + 2 <= current ? primary : ThemeColor("border", border);
            canvas.DrawLine(x0, cy, x1, cy, p);
        }

        using var num = TextFont.Create("0123456789", 13f, bold: true);
        for (var i = 0; i < names.Length; i++)
        {
            var step = i + 1;
            var cx = rect.Left + cell * (i + 0.5f);
            var done = step < current;
            var now = step == current;

            p.Style = SKPaintStyle.Fill;
            p.Color = done ? primary : ThemeColor("surface", SKColors.White);
            canvas.DrawCircle(cx, cy, dot / 2f, p);
            if (!done)
            {
                p.Style = SKPaintStyle.Stroke;
                p.StrokeWidth = now ? 3f : 1.5f;
                p.Color = now ? primary : border;
                canvas.DrawCircle(cx, cy, dot / 2f - p.StrokeWidth / 2f, p);
            }

            using var ink = new SKPaint { IsAntialias = true };
            if (done)
            {
                ink.Color = ThemeColor("primary-fg", SKColors.White);
                if (IconFont.TryResolve("fa-check", out var glyph))
                    IconFont.Draw(canvas, glyph, new SKPoint(cx, cy), 13f, ink);
            }
            else
            {
                ink.Color = now ? primary : muted;
                var label = step.ToString(System.Globalization.CultureInfo.InvariantCulture);
                canvas.DrawText(label, cx, cy - (num.Metrics.Ascent + num.Metrics.Descent) / 2f, SKTextAlign.Center, num, ink);
            }

            // 이름 — 칸 폭에 맞춰 줄임표, 진행 중은 굵게
            var name = names[i];
            using var font = TextFont.Create(name, 12f, bold: now);
            var shown = font.MeasureText(name) > cell - 6f ? Ellipsize(name, cell - 6f, font) : name;
            ink.Color = step > current ? muted : text;
            canvas.DrawText(shown, cx, cy + dot / 2f + 8f - font.Metrics.Ascent, SKTextAlign.Center, font, ink);
        }
    }
}
