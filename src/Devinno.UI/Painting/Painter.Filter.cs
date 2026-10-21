using Devinno.UI.Grid;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

// 그리드 필터 팝업 — text 입력 칸, values 체크 목록(전체 선택 + 항목, 스크롤), range·date 라벨 + 두 칸, 취소·지우기·확인
public sealed partial class Painter
{
    internal static void PaintGridFilterPopup(SKCanvas canvas, GridFilterPopup p, DvTheme? theme, DvStrings? strings, float fontSize)
    {
        var b = p.Bounds;
        var muted = PopupChrome.Tok(theme, "text-muted", new SKColor(0x64, 0x74, 0x8b));

        PopupChrome.Begin(canvas, b, theme);

        switch (p.Kind)
        {
            case GridFilterKind.Text:
                PaintFilterField(canvas, p, 0, p.TextValue, false, theme, fontSize);
                break;
            case GridFilterKind.Values:
                PaintFilterValues(canvas, p, theme, strings, fontSize);
                break;
            case GridFilterKind.Range:
            case GridFilterKind.Date:
            {
                var date = p.Kind == GridFilterKind.Date;
                for (var f = 1; f <= 2; f++)
                {
                    var key = date ? f == 1 ? "dv.filter.from" : "dv.filter.to" : f == 1 ? "dv.filter.min" : "dv.filter.max";
                    var label = p.LabelRect(f);
                    var s = DvStrings.Builtin(strings, key);
                    using (var font = TextFont.Create(s, fontSize, false))
                    using (var lp = new SKPaint { IsAntialias = true, Color = muted })
                        canvas.DrawText(s, label.Left, Baseline(label, font), SKTextAlign.Left, font, lp);

                    if (date)
                    {
                        var v = f == 1 ? p.MinValue : p.MaxValue;
                        PaintFilterField(canvas, p, f, v is null ? "—" : GridFilter.BoundText(p.Column, GridFilterKind.Date, v), v is null, theme, fontSize);
                    }
                    else
                        PaintFilterField(canvas, p, f, f == 1 ? p.MinText : p.MaxText, false, theme, fontSize);
                }

                break;
            }
        }

        if (p.ShowsError) // 칸 거절 사유 — 작은 글자, state-fault
        {
            var msg = p.FieldEdit.Error!;
            var r = p.ErrorRect;
            using var font = TextFont.Create(msg, fontSize - 2, false);
            using var ep = new SKPaint { IsAntialias = true, Color = PopupChrome.Tok(theme, "state-fault", new SKColor(0xdc, 0x26, 0x26)) };
            canvas.DrawText(font.MeasureText(msg) > r.Width ? Ellipsize(msg, r.Width, font) : msg, r.Left, Baseline(r, font), SKTextAlign.Left, font, ep);
        }

        PaintFilterButtons(canvas, p, theme, strings, fontSize);
        PopupChrome.End(canvas, b, theme);
    }

    // 입력 칸 — 시간 팝업 시·분 칸과 같은 .input 모양(편집 중 2px primary, 오류 state-fault), 글자는 왼쪽 정렬
    private static void PaintFilterField(SKCanvas canvas, GridFilterPopup p, int field, string value, bool dim, DvTheme? theme, float fontSize)
    {
        var f = p.FieldRect(field);
        var editing = p.FieldEdit.Field == field;
        var ink = dim ? PopupChrome.Tok(theme, "text-subtle", new SKColor(0x94, 0xa3, 0xb8)) : PopupChrome.Tok(theme, "text", new SKColor(0x0f, 0x17, 0x2a));
        var fault = PopupChrome.Tok(theme, "state-fault", new SKColor(0xdc, 0x26, 0x26));
        var primary = PopupChrome.Tok(theme, "primary", new SKColor(0x25, 0x63, 0xeb));
        using (var bg = new SKPaint { IsAntialias = true, Color = PopupChrome.Tok(theme, "surface", SKColors.White) })
            canvas.DrawRoundRect(f, 6, 6, bg);
        using (var bp = new SKPaint
               {
                   IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = editing ? 2 : 1,
                   Color = editing ? p.FieldEdit.Error is not null ? fault : primary : PopupChrome.Tok(theme, "border-strong", new SKColor(0x94, 0xa3, 0xb8))
               })
            canvas.DrawRoundRect(f, 6, 6, bp);

        if (editing && p.FieldEdit.Hide) // 네이티브 편집기가 덮는 동안은 비움
            return;
        var s = editing && p.FieldEdit.Text is { } t ? t : value;
        if (s.Length == 0)
            return;
        var area = new SKRect(f.Left + 12f, f.Top, f.Right - 12f, f.Bottom);
        using var font = TextFont.Create(s, fontSize, false);
        using var tp = new SKPaint { IsAntialias = true, Color = editing ? PopupChrome.Tok(theme, "text", new SKColor(0x0f, 0x17, 0x2a)) : ink };
        canvas.Save();
        canvas.ClipRect(area);
        canvas.DrawText(s, area.Left, Baseline(area, font), SKTextAlign.Left, font, tp);
        canvas.Restore();
    }

    // values 목록 — 전체 선택 줄 + 항목 줄(체크 상자 + 글자, 넘치면 …), 넘치면 얇은 스크롤 표시, 상한이면 안내 줄
    private static void PaintFilterValues(SKCanvas canvas, GridFilterPopup p, DvTheme? theme, DvStrings? strings, float fontSize)
    {
        var text = PopupChrome.Tok(theme, "text", new SKColor(0x0f, 0x17, 0x2a));
        var list = p.ListRect;
        canvas.Save();
        canvas.ClipRect(list);

        var all = p.Checked.Count == p.Items.Count && p.Items.Count > 0;
        var none = p.Checked.Count == 0;
        PaintFilterRow(canvas, p.AllRect, DvStrings.Builtin(strings, "dv.filter.all"), all ? 1 : none ? 0 : 2, true, theme, fontSize, text);
        for (var i = 0; i < p.Items.Count; i++)
        {
            var row = p.ItemRect(i);
            if (row.Bottom < list.Top || row.Top > list.Bottom)
                continue;
            var it = p.Items[i];
            PaintFilterRow(canvas, row, it.Text, p.Checked.Contains(it.Value) ? 1 : 0, false, theme, fontSize, text);
        }

        canvas.Restore();

        if (p.MaxScroll > 0)
        {
            var content = (p.Items.Count + 1) * GridFilterPopup.Row;
            var thumb = Math.Max(24, list.Height * list.Height / content);
            var y = list.Top + (list.Height - thumb) * (p.ScrollY / p.MaxScroll);
            using var tp = new SKPaint { IsAntialias = true, Color = PopupChrome.Tok(theme, "scrollbar-thumb", new SKColor(0x64, 0x74, 0x8b, 0x80)) };
            canvas.DrawRoundRect(SKRect.Create(list.Right - 6, y, 3, thumb), 1.5f, 1.5f, tp);
        }

        if (p.Truncated)
        {
            var more = p.MoreText;
            var r = p.MoreRect;
            using var font = TextFont.Create(more, p.MoreFontSize, false);
            using var mp = new SKPaint { IsAntialias = true, Color = PopupChrome.Tok(theme, "text-muted", new SKColor(0x64, 0x74, 0x8b)) };
            canvas.DrawText(font.MeasureText(more) > r.Width ? Ellipsize(more, r.Width, font) : more, r.Left, Baseline(r, font), SKTextAlign.Left, font, mp);
        }
    }

    // 체크 상자(0 빈 상자, 1 체크, 2 일부 = 가로줄) + 글자
    private static void PaintFilterRow(SKCanvas canvas, SKRect row, string label, int state, bool bold, DvTheme? theme, float fontSize, SKColor ink)
    {
        var primary = PopupChrome.Tok(theme, "primary", new SKColor(0x25, 0x63, 0xeb));
        var box = SKRect.Create(row.Left + 12f, row.MidY - fontSize / 2f, fontSize, fontSize);
        if (state == 0)
        {
            using var bp = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f,
                Color = PopupChrome.Tok(theme, "border-strong", new SKColor(0x94, 0xa3, 0xb8))
            };
            canvas.DrawRoundRect(SKRect.Inflate(box, -0.75f, -0.75f), 3, 3, bp);
        }
        else
        {
            using (var fp = new SKPaint { IsAntialias = true, Color = primary })
                canvas.DrawRoundRect(box, 3, 3, fp);
            using var mp = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
                Color = PopupChrome.Tok(theme, "primary-fg", SKColors.White)
            };
            if (state == 1)
            {
                using var path = new SKPath();
                path.MoveTo(box.Left + box.Width * 0.24f, box.MidY);
                path.LineTo(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.70f);
                path.LineTo(box.Left + box.Width * 0.76f, box.Top + box.Height * 0.32f);
                canvas.DrawPath(path, mp);
            }
            else
                canvas.DrawLine(box.Left + box.Width * 0.25f, box.MidY, box.Right - box.Width * 0.25f, box.MidY, mp);
        }

        var x = box.Right + 8f;
        var avail = row.Right - 12f - x;
        if (label.Length == 0 || avail <= 0)
            return;
        using var font = TextFont.Create(label, fontSize, bold);
        if (font.MeasureText(label) > avail)
            label = Ellipsize(label, avail, font);
        if (label.Length == 0)
            return;
        using var tp = new SKPaint { IsAntialias = true, Color = ink };
        canvas.DrawText(label, x, Baseline(row, font), SKTextAlign.Left, font, tp);
    }

    // 취소·지우기(DvButton secondary)·확인(primary 채움) — 선택기 버튼과 같은 색 규칙
    private static void PaintFilterButtons(SKCanvas canvas, GridFilterPopup p, DvTheme? theme, DvStrings? strings, float fontSize)
    {
        var text = PopupChrome.Tok(theme, "text", new SKColor(0x0f, 0x17, 0x2a));
        var raised = PopupChrome.Tok(theme, "surface-raised", SKColors.White);
        var strong = PopupChrome.Tok(theme, "border-strong", new SKColor(0x94, 0xa3, 0xb8));
        foreach (var (r, key) in new[] { (p.CancelRect, "dv.filter.cancel"), (p.ClearRect, "dv.filter.clear") })
        {
            using (var fp = new SKPaint { IsAntialias = true, Color = raised })
                canvas.DrawRoundRect(r, 6, 6, fp);
            using (var cp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = strong })
                canvas.DrawRoundRect(r, 6, 6, cp);
            CenterText(canvas, DvStrings.Builtin(strings, key), r, fontSize, false, text);
        }

        using (var op = new SKPaint { IsAntialias = true, Color = PopupChrome.Tok(theme, "primary", new SKColor(0x25, 0x63, 0xeb)) })
            canvas.DrawRoundRect(p.OkRect, 6, 6, op);
        CenterText(canvas, DvStrings.Builtin(strings, "dv.filter.ok"), p.OkRect, fontSize, true, PopupChrome.Tok(theme, "primary-fg", SKColors.White));
    }
}
