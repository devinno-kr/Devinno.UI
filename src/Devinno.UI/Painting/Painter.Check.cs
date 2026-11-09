using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    /// <summary>The size (px) of the check box and radio circle. The text is to its right (left padding of <c>.checkbox</c> and <c>.radio</c> is 34 = 24 + gap 10).</summary>
    internal const float CheckSize = 24f;

    // DvCheckBox·DvRadio — 왼쪽 상자/원(세로 가운데). 켜짐 = primary 채움 + ✓(체크)·가운데 점(라디오), 꺼짐 = surface + border-strong 테두리.
    // 그리드 체크 셀과 같은 색. 누르는 중·올라감 = 테두리 primary, 비활성 = 반투명.
    private void PaintCheck(SKCanvas canvas, LayoutBox box, InteractionState? state)
    {
        var rect = box.Rect;
        var b = SKRect.Create(rect.Left, rect.MidY - CheckSize / 2f, CheckSize, CheckSize);
        var radio = box.Node.Tag == "DvRadio";
        var radius = radio ? CheckSize / 2f : 5f;
        var a = box.Disabled ? (byte)0x80 : (byte)0xff;
        var hot = !box.Disabled && state is not null && (ReferenceEquals(state.Press, box) || ReferenceEquals(state.Hover, box));
        var primary = ThemeColor("primary").WithAlpha(a);
        PaintReadOnlyEdge(canvas, box, b, radius);

        using var p = new SKPaint { IsAntialias = true };
        if (box.ToggleOn && !radio)
        {
            p.Color = primary;
            canvas.DrawRoundRect(b, radius, radius, p);
            using var ip = new SKPaint { IsAntialias = true, Color = ThemeColor("primary-fg").WithAlpha(a) };
            if (IconFont.TryResolve("fa-check", out var glyph))
                IconFont.Draw(canvas, glyph, new SKPoint(b.MidX, b.MidY), 15f, ip);
            return;
        }

        p.Color = ThemeColor("surface").WithAlpha(a);
        canvas.DrawRoundRect(b, radius, radius, p);
        p.Style = SKPaintStyle.Stroke;
        p.StrokeWidth = box.ToggleOn || hot ? 2f : 1.5f;
        p.Color = box.ToggleOn || hot ? primary : ThemeColor("border-strong").WithAlpha(a);
        var inset = p.StrokeWidth / 2f;
        var sb = SKRect.Inflate(b, -inset, -inset);
        canvas.DrawRoundRect(sb, radius - inset, radius - inset, p);

        if (box.ToggleOn) // 라디오 켜짐 — 가운데 점
        {
            p.Style = SKPaintStyle.Fill;
            p.Color = primary;
            canvas.DrawCircle(b.MidX, b.MidY, CheckSize * 0.25f, p);
        }
    }

    // 3단계 R5 — 읽기 전용 모양 하나: 값은 그대로 그리고 모양 둘레에 점선(border-strong, 입력 상자 .input-readonly 의 점선과 같은 뜻).
    // 흐리게 하지 않는다(흐림은 disabled 몫) — 비활성이면 점선 없이 반투명만.
    private void PaintReadOnlyEdge(SKCanvas canvas, LayoutBox box, SKRect shape, float radius)
    {
        if (box.Disabled || !box.ReadOnly)
            return;
        using var dash = SKPathEffect.CreateDash([4f, 3f], 0);
        using var edge = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            PathEffect = dash,
            Color = ThemeColor("border-strong")
        };
        var o = SKRect.Inflate(shape, 2.5f, 2.5f);
        canvas.DrawRoundRect(o, radius + 2.5f, radius + 2.5f, edge);
    }
}
