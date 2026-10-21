using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

// DvDateTimeInput · DvColorInput 상자 — 배경·테두리는 .input 공통 경로. 포커스 링(열림), 오른쪽 아이콘, 값 텍스트(색은 앞에 견본) 또는 placeholder.
public sealed partial class Painter
{
    internal const float PickerIconSize = 16f;
    internal const float PickerSwatch = 20f;

    private void PaintPicker(SKCanvas canvas, LayoutBox box, ComputedStyle st)
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

        if (IconFont.TryResolve(PickerDisplay.Icon(box), out var icon))
        {
            using var ap = new SKPaint { IsAntialias = true, Color = ThemeColor("text-muted", new SKColor(0x64, 0x74, 0x8b)) };
            IconFont.Draw(canvas, icon, new SKPoint(content.Right - PickerIconSize / 2f, content.MidY), PickerIconSize, ap);
        }

        content.Right -= PickerIconSize + 8f;
        var text = PickerDisplay.Text(box);
        var empty = text.Length == 0;
        if (empty)
            text = box.InputPlaceholder ?? "";
        var ink = empty ? ThemeColor("text-subtle", new SKColor(0x94, 0xa3, 0xb8)) : st.Color.ToSK();

        canvas.Save();
        canvas.ClipRect(content);
        var x = content.Left;
        if (box.Node.Tag == "DvColorInput" && !empty && PickerValue.TryToColor(box.InputValue, out var c))
        {
            var sw = SKRect.Create(x, content.MidY - PickerSwatch / 2f, PickerSwatch, PickerSwatch);
            using (var fp = new SKPaint { IsAntialias = true, Color = new SKColor(c.Red, c.Green, c.Blue) })
                canvas.DrawRoundRect(sw, 4, 4, fp);
            using (var bp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = ThemeColor("border", new SKColor(0xcb, 0xd5, 0xe1)) })
                canvas.DrawRoundRect(sw, 4, 4, bp);
            x += PickerSwatch + LayoutEngine.IconTextGap;
        }

        if (text.Length > 0)
        {
            using var font = TextFont.Create(text, st.FontSize, st.Bold);
            using var tp = new SKPaint { IsAntialias = true, Color = ink };
            canvas.DrawText(text, x, Baseline(content, font), SKTextAlign.Left, font, tp);
        }

        canvas.Restore();
    }

    // ── 시간 팝업 본문 — 월 제목·화살표, 요일, 6주 달력, 시각 칸, 취소/확인
    internal static void PaintDateTimePopup(SKCanvas canvas, DateTimePopup p, DvTheme? theme, DvStrings? strings, float fontSize)
    {
        var b = p.Bounds;
        var st = p.State;
        var text = PopupChrome.Tok(theme, "text", new SKColor(0x0f, 0x17, 0x2a));
        var muted = PopupChrome.Tok(theme, "text-muted", new SKColor(0x64, 0x74, 0x8b));
        var subtle = PopupChrome.Tok(theme, "text-subtle", new SKColor(0x94, 0xa3, 0xb8));
        var primary = PopupChrome.Tok(theme, "primary", new SKColor(0x25, 0x63, 0xeb));
        var primaryFg = PopupChrome.Tok(theme, "primary-fg", SKColors.White);
        var fault = PopupChrome.Tok(theme, "state-fault", new SKColor(0xdc, 0x26, 0x26));
        var surface = PopupChrome.Tok(theme, "surface", SKColors.White);
        var strong = PopupChrome.Tok(theme, "border-strong", new SKColor(0x94, 0xa3, 0xb8)); // .input 테두리

        PopupChrome.Begin(canvas, b, theme);

        if (st.Mode != PickerMode.Time)
        {
            var title = string.Format(DvStrings.Builtin(strings, "dv.picker.month"), st.ViewYear, st.ViewMonth);
            CenterText(canvas, title, p.TitleRect, fontSize, true, text);
            using (var ip = new SKPaint { IsAntialias = true, Color = text })
            {
                if (IconFont.TryResolve("fa-angle-left", out var l))
                    IconFont.Draw(canvas, l, new SKPoint(p.PrevRect.MidX, p.PrevRect.MidY), 16f, ip);
                if (IconFont.TryResolve("fa-angle-right", out var r))
                    IconFont.Draw(canvas, r, new SKPoint(p.NextRect.MidX, p.NextRect.MidY), 16f, ip);
            }

            var dow = DvStrings.Builtin(strings, "dv.picker.dow").Split(',');
            for (var c = 0; c < 7 && c < dow.Length; c++)
                CenterText(canvas, dow[c], p.DowRect(c), fontSize - 2, false, c == 0 ? fault : c == 6 ? primary : muted);

            var today = DateOnly.FromDateTime(Editing.PickerClock.Today);
            for (var i = 0; i < 42; i++)
            {
                if (st.CellDate(i) is not { } d)
                    continue; // 범위 밖 칸
                var cell = p.DayRect(i);
                var center = new SKPoint(cell.MidX, cell.MidY);
                SKColor ink;
                if (d == st.Selected)
                {
                    using var fp = new SKPaint { IsAntialias = true, Color = primary };
                    canvas.DrawCircle(center, 16f, fp);
                    ink = primaryFg;
                }
                else
                {
                    if (d == today)
                    {
                        using var rp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = primary };
                        canvas.DrawCircle(center, 16f, rp);
                    }

                    ink = !st.InViewMonth(d) ? subtle : i % 7 == 0 ? fault : i % 7 == 6 ? primary : text;
                }

                CenterText(canvas, d.Day.ToString(), cell, fontSize, false, ink);
            }
        }

        // 시각 칸 — .input 모양, 칸 사이 ':'
        for (var i = 0; i < p.FieldCount; i++)
        {
            var f = p.FieldRect(i);
            var editing = p.FieldEdit.Field == i;
            using (var bg = new SKPaint { IsAntialias = true, Color = surface })
                canvas.DrawRoundRect(f, 6, 6, bg);
            using (var bp = new SKPaint
                   {
                       IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = editing ? 2 : 1,
                       Color = editing ? p.FieldEdit.Error is not null ? fault : primary : strong
                   })
                canvas.DrawRoundRect(f, 6, 6, bp);

            if (!(editing && p.FieldEdit.Hide)) // 네이티브 편집기가 덮는 동안은 비움
            {
                var v = editing && p.FieldEdit.Text is { } t ? t : p.FieldValue(i).ToString("D2");
                CenterText(canvas, v, f, fontSize, false, text);
            }
            if (i > 0)
            {
                var prev = p.FieldRect(i - 1);
                CenterText(canvas, ":", new SKRect(prev.Right, f.Top, f.Left, f.Bottom), fontSize, true, muted);
            }
        }

        PaintPickerButtons(canvas, p.CancelRect, p.OkRect, theme, strings, fontSize);

        PopupChrome.End(canvas, b, theme);
    }

    // 취소(DvButton secondary)·확인(primary 채움) — 두 팝업 공용
    private static void PaintPickerButtons(SKCanvas canvas, SKRect cancel, SKRect ok, DvTheme? theme, DvStrings? strings, float fontSize)
    {
        var text = PopupChrome.Tok(theme, "text", new SKColor(0x0f, 0x17, 0x2a));
        var primary = PopupChrome.Tok(theme, "primary", new SKColor(0x25, 0x63, 0xeb));
        var primaryFg = PopupChrome.Tok(theme, "primary-fg", SKColors.White);
        var raised = PopupChrome.Tok(theme, "surface-raised", SKColors.White);
        var strong = PopupChrome.Tok(theme, "border-strong", new SKColor(0x94, 0xa3, 0xb8));
        using (var fp = new SKPaint { IsAntialias = true, Color = raised })
            canvas.DrawRoundRect(cancel, 6, 6, fp);
        using (var cp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = strong })
            canvas.DrawRoundRect(cancel, 6, 6, cp);
        CenterText(canvas, DvStrings.Builtin(strings, "dv.picker.cancel"), cancel, fontSize, false, text);
        using (var op = new SKPaint { IsAntialias = true, Color = primary })
            canvas.DrawRoundRect(ok, 6, 6, op);
        CenterText(canvas, DvStrings.Builtin(strings, "dv.picker.ok"), ok, fontSize, true, primaryFg);
    }

    // ── 색 팝업 본문 — S/V 사각형, 색상 막대, 견본(원래|현재), # 칸, 취소/확인
    internal static void PaintColorPopup(SKCanvas canvas, ColorPopup p, DvTheme? theme, DvStrings? strings, float fontSize)
    {
        var b = p.Bounds;
        var st = p.State;
        var text = PopupChrome.Tok(theme, "text", new SKColor(0x0f, 0x17, 0x2a));
        var primary = PopupChrome.Tok(theme, "primary", new SKColor(0x25, 0x63, 0xeb));
        var fault = PopupChrome.Tok(theme, "state-fault", new SKColor(0xdc, 0x26, 0x26));
        var surface = PopupChrome.Tok(theme, "surface", SKColors.White);
        var border = PopupChrome.Tok(theme, "border", new SKColor(0xcb, 0xd5, 0xe1));
        var strong = PopupChrome.Tok(theme, "border-strong", new SKColor(0x94, 0xa3, 0xb8)); // .input 테두리
        var edge = new SKColor(0, 0, 0, 0x66);

        PopupChrome.Begin(canvas, b, theme);

        // 사각형 — 바닥 색상, 가로 흰→투명, 세로 투명→검정
        var sv = p.SvRect;
        canvas.Save();
        using (var clip = new SKPath())
        {
            clip.AddRoundRect(sv, 6, 6);
            canvas.ClipPath(clip, SKClipOperation.Intersect, true);
        }

        using (var bp = new SKPaint { Color = ColorPickerState.FromHsv(st.H, 1, 1) })
            canvas.DrawRect(sv, bp);
        using (var wp = new SKPaint
               {
                   Shader = SKShader.CreateLinearGradient(new SKPoint(sv.Left, 0), new SKPoint(sv.Right, 0),
                       new[] { SKColors.White, SKColors.White.WithAlpha(0) }, null, SKShaderTileMode.Clamp)
               })
            canvas.DrawRect(sv, wp);
        using (var kp = new SKPaint
               {
                   Shader = SKShader.CreateLinearGradient(new SKPoint(0, sv.Top), new SKPoint(0, sv.Bottom),
                       new[] { SKColors.Black.WithAlpha(0), SKColors.Black }, null, SKShaderTileMode.Clamp)
               })
            canvas.DrawRect(sv, kp);
        canvas.Restore();

        var cur = new SKPoint(sv.Left + (float)st.S * sv.Width, sv.Top + (float)(1 - st.V) * sv.Height);
        using (var op = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = edge })
            canvas.DrawCircle(cur, 8f, op);
        using (var wp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.White })
            canvas.DrawCircle(cur, 7f, wp);

        // 막대 — 위→아래 색상 0..360
        var bar = p.BarRect;
        var hues = new SKColor[7];
        for (var i = 0; i < 7; i++)
            hues[i] = ColorPickerState.FromHsv(i * 60, 1, 1);
        using (var hp = new SKPaint
               {
                   IsAntialias = true,
                   Shader = SKShader.CreateLinearGradient(new SKPoint(0, bar.Top), new SKPoint(0, bar.Bottom), hues, null, SKShaderTileMode.Clamp)
               })
            canvas.DrawRoundRect(bar, 6, 6, hp);
        var hy = bar.Top + (float)(st.H / 360d) * bar.Height;
        using (var op = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 5, Color = edge })
            canvas.DrawLine(bar.Left - 2, hy, bar.Right + 2, hy, op);
        using (var wp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = SKColors.White })
            canvas.DrawLine(bar.Left - 2, hy, bar.Right + 2, hy, wp);

        // 견본 — 왼쪽 원래, 오른쪽 현재
        var sw = p.SwatchRect;
        canvas.Save();
        using (var clip = new SKPath())
        {
            clip.AddRoundRect(sw, 6, 6);
            canvas.ClipPath(clip, SKClipOperation.Intersect, true);
        }

        using (var ip = new SKPaint { Color = st.Initial })
            canvas.DrawRect(new SKRect(sw.Left, sw.Top, sw.MidX, sw.Bottom), ip);
        using (var np = new SKPaint { Color = st.Rgb })
            canvas.DrawRect(new SKRect(sw.MidX, sw.Top, sw.Right, sw.Bottom), np);
        canvas.Restore();
        using (var bp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = border })
            canvas.DrawRoundRect(sw, 6, 6, bp);

        // # 칸 — .input 모양
        var hex = p.HexRect;
        var editing = p.HexEdit.Text is not null || p.HexEdit.Hide;
        var err = p.HexEdit.Error is not null;
        using (var bg = new SKPaint { IsAntialias = true, Color = surface })
            canvas.DrawRoundRect(hex, 6, 6, bg);
        using (var bp = new SKPaint
               {
                   IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = editing ? 2 : 1,
                   Color = editing ? err ? fault : primary : strong
               })
            canvas.DrawRoundRect(hex, 6, 6, bp);
        if (!p.HexEdit.Hide)
        {
            var label = p.HexEdit.Text ?? PickerValue.Hex(st.Rgb);
            using var font = TextFont.Create(label, fontSize, false);
            using var tp = new SKPaint { IsAntialias = true, Color = text };
            canvas.DrawText(label, hex.Left + 12f, hex.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2f, SKTextAlign.Left, font, tp);
        }

        if (err && IconFont.TryResolve("fa-circle-exclamation", out var ex))
        {
            using var xp = new SKPaint { IsAntialias = true, Color = fault };
            IconFont.Draw(canvas, ex, new SKPoint(hex.Right - 18f, hex.MidY), 16f, xp);
        }

        PaintPickerButtons(canvas, p.CancelRect, p.OkRect, theme, strings, fontSize);
        PopupChrome.End(canvas, b, theme);
    }

    private static void CenterText(SKCanvas canvas, string s, SKRect r, float size, bool bold, SKColor color)
    {
        if (s.Length == 0)
            return;
        using var font = TextFont.Create(s, size, bold);
        using var tp = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(s, r.MidX, r.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2f, SKTextAlign.Center, font, tp);
    }
}
