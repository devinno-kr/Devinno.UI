using System.Globalization;
using Devinno.UI.Alarms;
using Devinno.UI.Layout;
using Devinno.UI.Motion;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    internal const int AlarmBlinkMs = 500;

    // DvAlarmBanner — 상단 comm 스트립 + 하단 unACKed 영역(최우선 1건 + 미확인 건수). unACKed 는 공통 위상 점멸.
    private void PaintAlarmBanner(SKCanvas canvas, LayoutBox box, ComputedStyle st, long ts)
    {
        var rect = box.Rect;
        var r = st.BorderRadius;
        var on = MotionPhase.BlinkOn(ts, AlarmBlinkMs);
        var (strip, bottom) = AlarmBannerLayout.Split(rect, !string.IsNullOrEmpty(box.CommAlarm));

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(rect, r, r), SKClipOperation.Intersect, antialias: true);

        if (strip is { } s)
        {
            var comm = ThemeColor("comm-alarm");
            using (var p = new SKPaint { IsAntialias = true, Color = on ? comm : Mix(comm, SKColors.Black, 0.35f) })
                canvas.DrawRect(s, p);
            DrawBannerText(canvas, T("dv.alarm.comm") + " · " + box.CommAlarm, s, s.Left + 14f, ThemeColor("comm-alarm-fg"), 12f, bold: true);
        }

        var top = AlarmView.Top(box.AlarmItems);
        if (top is null)
        {
            using (var p = new SKPaint { IsAntialias = true, Color = ThemeColor("tone-success-soft") })
                canvas.DrawRect(bottom, p);
            var fg = ThemeColor("tone-success-fg");
            using (var dot = new SKPaint { IsAntialias = true, Color = ThemeColor("tone-success") })
                canvas.DrawCircle(bottom.Left + 20f, bottom.MidY, 5f, dot);
            DrawBannerText(canvas, T("dv.alarm.none"), bottom, bottom.Left + 34f, fg, 14f, bold: true);
        }
        else
        {
            var (prio, fg) = PriorityColors(top.Priority);
            using (var p = new SKPaint { IsAntialias = true, Color = on ? prio : Mix(prio, SKColors.Black, 0.35f) })
                canvas.DrawRect(bottom, p);

            // 등급 칩 — 배경을 살짝 어둡게
            var label = PriorityLabel(top.Priority);
            using var chipFont = TextFont.Create(label, 12f, bold: true);
            var chipW = chipFont.MeasureText(label) + 16f;
            var chipH = MathF.Min(22f, bottom.Height - 8f);
            var chip = SKRect.Create(bottom.Left + 12f, bottom.MidY - chipH / 2f, chipW, chipH);
            using (var cp = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 0x33) })
                canvas.DrawRoundRect(chip, chipH / 2f, chipH / 2f, cp);
            DrawBannerText(canvas, label, chip, chip.Left + 8f, fg, 12f, bold: true);

            // 오른쪽: 미확인 건수 + 시각
            var unacked = box.AlarmItems.Count(a => !a.Acked);
            var right = string.Format(T("dv.alarm.unacked"), unacked) + $"   {top.Time:HH:mm:ss}";
            using var rf = TextFont.Create(right, 12f, bold: false);
            var rw = rf.MeasureText(right);
            using (var rp = new SKPaint { IsAntialias = true, Color = fg.WithAlpha(0xd0) })
                canvas.DrawText(right, bottom.Right - 14f, Baseline(bottom, rf), SKTextAlign.Right, rf, rp);

            canvas.Save();
            canvas.ClipRect(new SKRect(chip.Right + 10f, bottom.Top, bottom.Right - 24f - rw, bottom.Bottom));
            DrawBannerText(canvas, AlarmText(top), bottom, chip.Right + 12f, fg, 14f, bold: true);
            canvas.Restore();
        }

        canvas.Restore();
    }

    // DvAlarmList — 헤더 + 정렬된 행(홀짝 줄무늬). unACKed = 좌측 우선순위 바(점멸) + 굵은 메시지 + ACK 버튼
    private void PaintAlarmList(SKCanvas canvas, LayoutBox box, long ts)
    {
        var showAck = box.AlarmShowAck;
        var layout = AlarmListLayout.Compute(box.Rect, box.AlarmItems, box.AlarmRowHeight, showAck);
        var on = MotionPhase.BlinkOn(ts, AlarmBlinkMs);
        var text = ThemeColor("text");
        var muted = ThemeColor("text-muted");
        var subtle = ThemeColor("text-subtle");
        var line = ThemeColor("border");

        var colTime = box.Rect.Left + 16f;
        var colPrio = colTime + 78f;
        var colMsg = colPrio + 62f;

        canvas.Save();
        canvas.ClipRect(box.Rect);

        // 헤더
        var h = layout.Header;
        using (var hf = TextFont.Create(T("dv.alarm.col.message"), 12f, bold: true))
        using (var hp = new SKPaint { IsAntialias = true, Color = muted })
        {
            var hb = Baseline(h, hf);
            canvas.DrawText(T("dv.alarm.col.time"), colTime, hb, SKTextAlign.Left, hf, hp);
            canvas.DrawText(T("dv.alarm.col.priority"), colPrio, hb, SKTextAlign.Left, hf, hp);
            canvas.DrawText(T("dv.alarm.col.message"), colMsg, hb, SKTextAlign.Left, hf, hp);
            if (showAck)
                canvas.DrawText(T("dv.alarm.col.ack"), h.Right - 12f - AlarmListLayout.AckButtonWidth / 2f, hb, SKTextAlign.Center, hf, hp);
        }

        using (var lp = new SKPaint { IsAntialias = true, Color = line, StrokeWidth = 1f })
            canvas.DrawLine(h.Left, h.Bottom - 0.5f, h.Right, h.Bottom - 0.5f, lp);

        if (layout.Rows.Count == 0)
        {
            var emptyText = T("dv.alarm.empty");
            using var ef = TextFont.Create(emptyText, 13f, bold: false);
            using var ep = new SKPaint { IsAntialias = true, Color = subtle };
            canvas.DrawText(emptyText, box.Rect.MidX, h.Bottom + 28f, SKTextAlign.Center, ef, ep);
        }

        for (var i = 0; i < layout.Rows.Count; i++)
        {
            var (row, rect, ack) = layout.Rows[i];
            var (prio, _) = PriorityColors(row.Priority);

            if (i % 2 == 1)
                using (var zp = new SKPaint { Color = text.WithAlpha(0x08) })
                    canvas.DrawRect(rect, zp);

            if (!row.Acked)
                using (var bar = new SKPaint { IsAntialias = true, Color = on ? prio : prio.WithAlpha(0x66) })
                    canvas.DrawRoundRect(SKRect.Create(rect.Left + 3f, rect.Top + 6f, 4f, rect.Height - 12f), 2f, 2f, bar);

            var rowInk = row.Acked ? subtle : text;
            using (var tf = TextFont.Create("00:00:00", 12f, bold: false))
            using (var tp = new SKPaint { IsAntialias = true, Color = row.Acked ? subtle : muted })
                canvas.DrawText(row.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture), colTime, Baseline(rect, tf),
                    SKTextAlign.Left, tf, tp);

            // 등급 칩
            var label = PriorityLabel(row.Priority);
            using (var cf = TextFont.Create(label, 11f, bold: true))
            {
                var cw = cf.MeasureText(label) + 14f;
                var chip = SKRect.Create(colPrio, rect.MidY - 10f, cw, 20f);
                using (var cb = new SKPaint { IsAntialias = true, Color = prio.WithAlpha(row.Acked ? (byte)0x14 : (byte)0x2a) })
                    canvas.DrawRoundRect(chip, 10f, 10f, cb);
                using var ct = new SKPaint { IsAntialias = true, Color = row.Acked ? subtle : prio };
                canvas.DrawText(label, chip.MidX, Baseline(chip, cf), SKTextAlign.Center, cf, ct);
            }

            // 메시지(열 폭으로 클립)
            var msgRight = showAck ? (ack?.Left ?? rect.Right - 12f - AlarmListLayout.AckButtonWidth) - 12f : rect.Right - 12f;
            canvas.Save();
            canvas.ClipRect(new SKRect(colMsg, rect.Top, msgRight, rect.Bottom));
            using (var mf = TextFont.Create(AlarmText(row), 13f, bold: !row.Acked))
            using (var mp = new SKPaint { IsAntialias = true, Color = rowInk })
                canvas.DrawText(AlarmText(row), colMsg, Baseline(rect, mf), SKTextAlign.Left, mf, mp);
            canvas.Restore();

            if (ack is { } b)
            {
                using (var bb = new SKPaint { IsAntialias = true, Color = prio.WithAlpha(0x26) })
                    canvas.DrawRoundRect(b, 8f, 8f, bb);
                using (var bs = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = prio })
                    canvas.DrawRoundRect(b, 8f, 8f, bs);
                var ackText = T("dv.alarm.ack");
                using var af = TextFont.Create(ackText, 12f, bold: true);
                using var ap = new SKPaint { IsAntialias = true, Color = prio };
                canvas.DrawText(ackText, b.MidX, Baseline(b, af), SKTextAlign.Center, af, ap);
            }
            else if (showAck && row.AckTime is { } at)
            {
                var ackText = string.Format(T("dv.alarm.acked"), at.ToString("HH:mm", CultureInfo.InvariantCulture));
                using var af = TextFont.Create(ackText, 11f, bold: false);
                using var ap = new SKPaint { IsAntialias = true, Color = subtle };
                canvas.DrawText(ackText,
                    rect.Right - 12f - AlarmListLayout.AckButtonWidth / 2f, Baseline(rect, af), SKTextAlign.Center, af, ap);
            }

            using (var lp = new SKPaint { Color = line.WithAlpha(0x60), StrokeWidth = 1f })
                canvas.DrawLine(rect.Left, rect.Bottom - 0.5f, rect.Right, rect.Bottom - 0.5f, lp);
        }

        canvas.Restore();
    }

    private string PriorityLabel(AlarmPriority p) => p switch
    {
        AlarmPriority.Critical => T("dv.alarm.critical"),
        AlarmPriority.Major => T("dv.alarm.major"),
        _ => T("dv.alarm.minor")
    };

    private (SKColor Color, SKColor Fg) PriorityColors(AlarmPriority p) => p switch
    {
        AlarmPriority.Critical => (ThemeColor("alarm-critical"),
            ThemeColor("alarm-critical-fg")),
        AlarmPriority.Major => (ThemeColor("alarm-major"),
            ThemeColor("alarm-major-fg")),
        _ => (ThemeColor("alarm-minor"), ThemeColor("alarm-minor-fg"))
    };

    private static float Baseline(SKRect r, SKFont f) => r.MidY - (f.Metrics.Ascent + f.Metrics.Descent) / 2f;

    private static void DrawBannerText(SKCanvas canvas, string text, SKRect area, float x, SKColor color, float size, bool bold)
    {
        using var font = TextFont.Create(text, size, bold);
        using var p = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(text, x, Baseline(area, font), SKTextAlign.Left, font, p);
    }

    // AlarmLog 이 스스로 올린 포화 알람만 화면 언어로(dv.alarm.saturated) — 나머지 알람 문구는 앱 데이터라 그대로
    internal string AlarmText(AlarmRow row) =>
        row.Source == "AlarmLog" && row.Message == Alarms.AlarmLog.SaturationMessage ? T("dv.alarm.saturated") : row.Message;
}
