using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

// DvSelect 상자 — 배경·테두리는 .input 공통 경로. 여기서는 포커스 링(열림), 선택 항목 아이콘·텍스트(또는 placeholder), 펼침 화살표.
public sealed partial class Painter
{
    internal const float SelectArrowSize = 14f;

    private void PaintSelect(SKCanvas canvas, LayoutBox box, ComputedStyle st)
    {
        var rect = box.Rect;
        var r = st.BorderRadius;
        if (box.Editing) // 열려 있음
        {
            var ring = SKRect.Inflate(rect, FocusRingGap, FocusRingGap);
            using var rp = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = FocusRingWidth,
                Color = ThemeColor("focus", new SKColor(0x25, 0x63, 0xeb, 0x66))
            };
            canvas.DrawRoundRect(ring, r + FocusRingGap, r + FocusRingGap, rp);
        }

        var pad = st.Padding;
        var content = new SKRect(rect.Left + pad.Left, rect.Top + pad.Top, rect.Right - pad.Right, rect.Bottom - pad.Bottom);
        if (content.Width <= 0 || content.Height <= 0)
            return;

        var muted = ThemeColor("text-muted", new SKColor(0x64, 0x74, 0x8b));
        if (IconFont.TryResolve(box.Editing ? "fa-angle-up" : "fa-angle-down", out var arrow))
        {
            using var ap = new SKPaint { IsAntialias = true, Color = muted };
            IconFont.Draw(canvas, arrow, new SKPoint(content.Right - SelectArrowSize / 2f, content.MidY), SelectArrowSize, ap);
        }

        content.Right -= SelectArrowSize + 8f;
        var selected = SelectDisplay.Selected(box);
        var text = selected?.Text ?? box.InputPlaceholder ?? "";
        var ink = selected is null ? ThemeColor("text-subtle", new SKColor(0x94, 0xa3, 0xb8)) : st.Color.ToSK();

        canvas.Save();
        canvas.ClipRect(content);
        var x = content.Left;
        if (selected?.Icon is { } spec && IconFont.TryResolve(spec, out var icon))
        {
            using var ip = new SKPaint { IsAntialias = true, Color = ink };
            IconFont.Draw(canvas, icon, new SKPoint(x + st.FontSize / 2f, content.MidY), st.FontSize, ip);
            x += st.FontSize + LayoutEngine.IconTextGap;
        }

        if (text.Length > 0)
        {
            using var font = TextFont.Create(text, st.FontSize, st.Bold);
            using var tp = new SKPaint { IsAntialias = true, Color = ink };
            canvas.DrawText(text, x, Baseline(content, font), SKTextAlign.Left, font, tp);
        }

        canvas.Restore();
    }
}
