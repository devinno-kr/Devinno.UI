using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    // DvSlider — 막대(두께 6, gauge-track) + 최솟값 쪽부터 손잡이까지 채움(knob-fill) + 손잡이(지름 24, surface 채움 + 채움색 테두리).
    // show-value 면 손잡이 위(가로)·오른쪽(세로)에 값(+unit). 누르는 중·올라감 = 손잡이 테두리 굵게, 비활성 = 반투명.
    private void PaintSlider(SKCanvas canvas, LayoutBox box, InteractionState? state)
    {
        const float bar = 6f;
        var (min, max) = SliderMetrics.Track(box);
        var at = SliderMetrics.PointAt(box, box.KnobValue);
        var a = box.Disabled ? (byte)0x80 : (byte)0xff;
        var hot = !box.Disabled && state is not null && (ReferenceEquals(state.Press, box) || ReferenceEquals(state.Hover, box));
        var fill = ThemeColor("knob-fill", new SKColor(0x25, 0x63, 0xeb)).WithAlpha(a);

        using var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = bar, StrokeCap = SKStrokeCap.Round };
        p.Color = ThemeColor("gauge-track", new SKColor(0xe2, 0xe8, 0xf0)).WithAlpha(a);
        canvas.DrawLine(min, max, p);
        p.Color = fill;
        canvas.DrawLine(min, at, p);

        p.StrokeCap = SKStrokeCap.Butt;
        p.Style = SKPaintStyle.Fill;
        p.Color = ThemeColor("surface", SKColors.White).WithAlpha(a);
        canvas.DrawCircle(at, SliderMetrics.Thumb, p);
        p.Style = SKPaintStyle.Stroke;
        p.StrokeWidth = hot ? 4f : 3f;
        p.Color = fill;
        canvas.DrawCircle(at, SliderMetrics.Thumb - p.StrokeWidth / 2f, p);

        if (!box.ShowValue)
            return;

        var label = box.KnobValue.ToString("F" + KnobBinder.Decimals(box.KnobStep), System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(box.KnobUnit))
            label += " " + box.KnobUnit;
        using var font = TextFont.Create(label, 13f, bold: true);
        using var tp = new SKPaint { IsAntialias = true, Color = ThemeColor("text", new SKColor(0x22, 0x22, 0x22)).WithAlpha(a) };
        var mid = -(font.Metrics.Ascent + font.Metrics.Descent) / 2f;
        if (SliderMetrics.Vertical(box))
        {
            canvas.DrawText(label, at.X + SliderMetrics.Thumb + 8f, at.Y + mid, SKTextAlign.Left, font, tp);
            return;
        }

        // 가로 — 손잡이 위 가운데, 상자 밖으로 나가지 않게 양 끝에서 밀어 넣음
        var w = font.MeasureText(label);
        var x = Math.Clamp(at.X, box.Rect.Left + w / 2f, box.Rect.Right - w / 2f);
        canvas.DrawText(label, x, at.Y - SliderMetrics.Thumb - 6f + font.Metrics.Descent, SKTextAlign.Center, font, tp);
    }
}
