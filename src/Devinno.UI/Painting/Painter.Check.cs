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
        var primary = ThemeColor("primary", new SKColor(0x25, 0x63, 0xeb)).WithAlpha(a);

        using var p = new SKPaint { IsAntialias = true };
        if (box.ToggleOn && !radio)
        {
            p.Color = primary;
            canvas.DrawRoundRect(b, radius, radius, p);
            using var ip = new SKPaint { IsAntialias = true, Color = ThemeColor("primary-fg", SKColors.White).WithAlpha(a) };
            if (IconFont.TryResolve("fa-check", out var glyph))
                IconFont.Draw(canvas, glyph, new SKPoint(b.MidX, b.MidY), 15f, ip);
            return;
        }

        p.Color = ThemeColor("surface", SKColors.White).WithAlpha(a);
        canvas.DrawRoundRect(b, radius, radius, p);
        p.Style = SKPaintStyle.Stroke;
        p.StrokeWidth = box.ToggleOn || hot ? 2f : 1.5f;
        p.Color = box.ToggleOn || hot ? primary : ThemeColor("border-strong", new SKColor(0x94, 0xa3, 0xb8)).WithAlpha(a);
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
}
