using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

// DvInput — 상자(배경·테두리)는 .input 클래스로 공통 경로가 그리고, 여기서는 값·placeholder·단위·포커스 링·오류 테두리.
public sealed partial class Painter
{

    // show-stepper — 양 끝 −/+ 와 안쪽 구분선. 비활성·읽기 전용이면 흐린 글자색.
    private void PaintStepper(SKCanvas canvas, LayoutBox box, ComputedStyle st)
    {
        var rect = box.Rect;
        var w = InputDisplay.StepperWidth;
        var dim = box.Disabled || box.ReadOnly;
        using var tp = new SKPaint
        {
            IsAntialias = true, StrokeWidth = Roles.Stroke("emphasis"), StrokeCap = SKStrokeCap.Round,
            Color = dim ? ThemeColor("text-subtle") : st.Color.ToSK()
        };
        using var lp = new SKPaint { IsAntialias = true, StrokeWidth = Roles.Stroke("hairline"), Color = ThemeColor("border") };
        var cy = rect.MidY;
        var arm = MathF.Min(6f, rect.Height / 5f);
        var lx = rect.Left + w / 2;
        var rx = rect.Right - w / 2;
        canvas.DrawLine(lx - arm, cy, lx + arm, cy, tp);              // −
        canvas.DrawLine(rx - arm, cy, rx + arm, cy, tp);              // +
        canvas.DrawLine(rx, cy - arm, rx, cy + arm, tp);
        canvas.DrawLine(rect.Left + w, rect.Top + 6, rect.Left + w, rect.Bottom - 6, lp);   // 구분선
        canvas.DrawLine(rect.Right - w, rect.Top + 6, rect.Right - w, rect.Bottom - 6, lp);
    }

    private void PaintInput(SKCanvas canvas, LayoutBox box, ComputedStyle st)
    {
        var rect = box.Rect;
        var r = st.BorderRadius;

        if (box.InputStepper)
            PaintStepper(canvas, box, st);

        if (box.Editing)
        {
            // 편집 중·열림 = 테두리 자체를 강조색·강조 두께로(바깥 링 없음, H4) — 상자 안쪽에
            var bw = Roles.Stroke("emphasis");
            using var rp = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = bw,
                Color = ThemeColor("primary")
            };
            canvas.DrawRoundRect(SKRect.Inflate(rect, -bw / 2f, -bw / 2f), MathF.Max(0f, r - bw / 2f), MathF.Max(0f, r - bw / 2f), rp);
        }

        if (box.EditError is not null)
        {
            using var ep = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Roles.Stroke("outline"),
                Color = ThemeColor("tone-danger")
            };
            canvas.DrawRoundRect(rect, r, r, ep);
        }

        var pad = st.Padding;
        var content = new SKRect(rect.Left + pad.Left, rect.Top + pad.Top, rect.Right - pad.Right, rect.Bottom - pad.Bottom);
        if (content.Width <= 0 || content.Height <= 0)
            return;

        var muted = ThemeColor("text-subtle");

        // 단위(한 줄만) — 오른쪽, 흐린 색
        if (!box.InputMultiline && box.Unit is { Length: > 0 } unit)
        {
            using var uf = TextFont.Create(unit, st.FontSize, weight: Roles.Weight("regular"));
            using var up = new SKPaint { IsAntialias = true, Color = ThemeColor("text-muted") };
            canvas.DrawText(unit, content.Right, Baseline(content, uf), SKTextAlign.Right, uf, up);
            content.Right -= uf.MeasureText(unit) + 6f;
        }

        if (box.EditHideText)
            return; // 네이티브 편집기가 그 자리에 글자를 그린다

        var value = box.EditText is { } et
            ? box.InputKind == DvEditKind.Password ? new string(InputDisplay.PasswordDot, et.Length) : et
            : InputDisplay.Text(box);
        var placeholder = value.Length == 0 && !string.IsNullOrEmpty(box.InputPlaceholder);
        var text = placeholder ? box.InputPlaceholder! : value;
        if (text.Length == 0)
            return;

        using var font = TextFont.Create(text, st.FontSize, st.FontWeight);
        using var tp = new SKPaint { IsAntialias = true, Color = placeholder ? muted : st.Color.ToSK() };

        canvas.Save();
        canvas.ClipRect(content);
        if (box.InputMultiline)
        {
            var lineH = font.Spacing;
            var lines = InputDisplay.Wrap(text, font, content.Width);
            var scroll = Math.Clamp(box.InputScrollY, 0f, Math.Max(0f, lines.Count * lineH - content.Height)); // 휠로 내린 만큼(글이 줄면 끝에 맞춤)
            var y = content.Top - font.Metrics.Ascent - scroll;
            foreach (var line in lines)
            {
                if (y + font.Metrics.Ascent > content.Bottom)
                    break;
                if (y + font.Metrics.Descent < content.Top)
                {
                    y += lineH; // 위로 밀려난 줄
                    continue;
                }
                canvas.DrawText(line, AlignX(content, st.TextAlign), y, ToSk(st.TextAlign), font, tp);
                y += lineH;
            }
        }
        else
        {
            canvas.DrawText(text, AlignX(content, st.TextAlign), Baseline(content, font), ToSk(st.TextAlign), font, tp);
        }

        canvas.Restore();
    }

    // 아이콘 + 글자(DvButton icon) — 아이콘은 글자 크기 정사각, 글자 왼쪽, 묶음 전체를 text-align 으로 정렬
    private void PaintIconText(SKCanvas canvas, LayoutBox box, ComputedStyle st, IconGlyph icon)
    {
        var rect = box.Rect;
        var text = box.ResolvedText ?? "";
        using var font = TextFont.Create(text.Length > 0 ? text : " ", st.FontSize, st.FontWeight);
        var size = st.FontSize;
        var gap = text.Length > 0 ? LayoutEngine.IconTextGap : 0f;
        var total = size + gap + (text.Length > 0 ? font.MeasureText(text) : 0f);
        var x = st.TextAlign switch
        {
            TextAlign.Center => rect.MidX - total / 2f,
            TextAlign.Right => rect.Right - st.Padding.Right - total,
            _ => rect.Left + st.Padding.Left
        };
        var baseline = ContentBaseline(box, st, font);
        float midY;
        if (text.Length > 0)
        {
            font.MeasureText(text, out var ink);
            midY = baseline + ink.MidY; // 글자 잉크의 세로 가운데에 아이콘 잉크 가운데를 맞춤
        }
        else
        {
            var top = rect.Top + st.Padding.Top;
            var bottom = rect.Bottom - st.Padding.Bottom;
            midY = (st.ContentVertical ?? ContentVAlign.Middle) switch // 아이콘만 = 기본 가운데
            {
                ContentVAlign.Top => top + size / 2f,
                ContentVAlign.Bottom => bottom - size / 2f,
                _ => (top + bottom) / 2f
            };
        }

        using var p = new SKPaint { IsAntialias = true, Color = st.Color.ToSK() };
        IconFont.Draw(canvas, icon, new SKPoint(x + size / 2f, midY), size, p);
        if (text.Length > 0)
            canvas.DrawText(text, x + size + gap, baseline, SKTextAlign.Left, font, p);
    }

    // content-align 세로 축의 첫 줄 baseline(내용 영역 = 패딩 안쪽). 미지정 = 위.
    // 여러 줄이면 줄 간격(font.Spacing)으로 쌓은 묶음 전체를 정렬한다(한 줄이면 기존과 같음).
    private static float ContentBaseline(LayoutBox box, ComputedStyle st, SKFont font, int lineCount = 1)
    {
        var top = box.Rect.Top + st.Padding.Top;
        var bottom = box.Rect.Bottom - st.Padding.Bottom;
        var m = font.Metrics;
        var extra = (lineCount - 1) * font.Spacing; // 첫 줄 baseline 에서 마지막 줄 baseline 까지
        return (st.ContentVertical ?? ContentVAlign.Top) switch
        {
            ContentVAlign.Middle => (top + bottom) / 2f - (m.Ascent + m.Descent + extra) / 2f,
            ContentVAlign.Bottom => bottom - m.Descent - extra,
            _ => top - m.Ascent
        };
    }

    private static float AlignX(SKRect r, TextAlign a) => a switch
    {
        TextAlign.Center => r.MidX,
        TextAlign.Right => r.Right,
        _ => r.Left
    };

    private static SKTextAlign ToSk(TextAlign a) => a switch
    {
        TextAlign.Center => SKTextAlign.Center,
        TextAlign.Right => SKTextAlign.Right,
        _ => SKTextAlign.Left
    };
}
