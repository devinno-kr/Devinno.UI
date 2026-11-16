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
            DrawBannerText(canvas, T("dv.alarm.comm") + " · " + box.CommAlarm, s, s.Left + Roles.Font("body"), ThemeColor("comm-alarm-fg"), Roles.Font("meta"), bold: true);
        }

        var top = AlarmView.Top(box.AlarmItems);
        if (top is null)
        {
            // 알람 없음 — 정상 색(S2 look.normal-color)이 on 이면 정상 틴트, off 면 중립 면
            var normal = Roles.Look.NormalColor;
            using (var p = new SKPaint { IsAntialias = true, Color = normal ? ThemeColor("tone-success-soft") : ThemeColor("surface-raised") })
                canvas.DrawRect(bottom, p);
            var fg = normal ? ThemeColor("tone-success-fg") : ThemeColor("text-muted");
            using (var dot = new SKPaint { IsAntialias = true, Color = normal ? ThemeColor("normal") : ThemeColor("text-muted") })
                canvas.DrawCircle(bottom.Left + 20f, bottom.MidY, 5f, dot);
            DrawBannerText(canvas, T("dv.alarm.none"), bottom, bottom.Left + 34f, fg, Roles.Font("body"), bold: true);
        }
        else
        {
            var (prio, fg) = PriorityColors(top.Priority);
            using (var p = new SKPaint { IsAntialias = true, Color = on ? prio : Mix(prio, SKColors.Black, 0.35f) })
                canvas.DrawRect(bottom, p);

            // 등급 칩 — 등급색 면 위라 뒤집은 칩(대비 글자색 채움 + 등급색 글자)
            var chipH = MathF.Min(22f, bottom.Height - 8f);
            var chipW = DrawSeverityChip(canvas, top.Priority, bottom.Left + 12f, bottom.MidY, chipH, blinkOff: false, inverted: true);
            var chip = SKRect.Create(bottom.Left + 12f, bottom.MidY - chipH / 2f, chipW, chipH);

            // 오른쪽: 미확인 건수 + 시각
            var unacked = box.AlarmItems.Count(a => !a.Acked);
            var right = string.Format(T("dv.alarm.unacked"), unacked) + $"   {top.Time:HH:mm:ss}";
            using var rf = TextFont.Create(right, Roles.Font("meta"), weight: Roles.Weight("regular"));
            var rw = rf.MeasureText(right);
            using (var rp = new SKPaint { IsAntialias = true, Color = fg.WithAlpha(0xd0) })
                canvas.DrawText(right, bottom.Right - 14f, Baseline(bottom, rf), SKTextAlign.Right, rf, rp);

            canvas.Save();
            canvas.ClipRect(new SKRect(chip.Right + 10f, bottom.Top, bottom.Right - 24f - rw, bottom.Bottom));
            var bannerMsg = AlarmText(top);
            var msgLeft = chip.Right + Roles.Font("meta");
            using (var bf = TextFont.Create(bannerMsg, Roles.Font("body"), Roles.Weight("bold")))
            {
                var room = bottom.Right - 24f - rw - msgLeft;
                if (bf.MeasureText(bannerMsg) > room)
                    bannerMsg = Ellipsize(bannerMsg, MathF.Max(0f, room), bf); // 글자 중간에서 잘리지 않게 "…"
            }
            DrawBannerText(canvas, bannerMsg, bottom, msgLeft, fg, Roles.Font("body"), bold: true);
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
        // 메시지 열 = 가장 넓은 등급 칩(아이콘 포함) 뒤
        var chipMax = MathF.Max(46f, new[] { AlarmPriority.Critical, AlarmPriority.Major, AlarmPriority.Minor }.Max(SeverityChipWidth));
        var colMsg = colPrio + chipMax + 16f;

        canvas.Save();
        canvas.ClipRect(box.Rect);

        // 헤더
        var h = layout.Header;
        using (var hf = TextFont.Create(T("dv.alarm.col.message"), Roles.Font("meta"), weight: Roles.Weight("bold")))
        using (var hp = new SKPaint { IsAntialias = true, Color = muted })
        {
            var hb = Baseline(h, hf);
            canvas.DrawText(T("dv.alarm.col.time"), colTime, hb, SKTextAlign.Left, hf, hp);
            canvas.DrawText(T("dv.alarm.col.priority"), colPrio, hb, SKTextAlign.Left, hf, hp);
            canvas.DrawText(T("dv.alarm.col.message"), colMsg, hb, SKTextAlign.Left, hf, hp);
            if (showAck)
                canvas.DrawText(T("dv.alarm.col.ack"), h.Right - 12f - AlarmListLayout.AckButtonWidth / 2f, hb, SKTextAlign.Center, hf, hp);
        }

        using (var lp = new SKPaint { IsAntialias = true, Color = line, StrokeWidth = Roles.Stroke("hairline") })
            canvas.DrawLine(h.Left, h.Bottom - 0.5f, h.Right, h.Bottom - 0.5f, lp);

        if (layout.Rows.Count == 0)
        {
            var emptyText = T("dv.alarm.empty");
            using var ef = TextFont.Create(emptyText, Roles.Font("label"), weight: Roles.Weight("regular"));
            using var ep = new SKPaint { IsAntialias = true, Color = subtle };
            canvas.DrawText(emptyText, box.Rect.MidX, h.Bottom + 28f, SKTextAlign.Center, ef, ep);
        }

        for (var i = 0; i < layout.Rows.Count; i++)
        {
            var (row, rect, ack) = layout.Rows[i];

            if (i % 2 == 1)
                using (var zp = new SKPaint { Color = text.WithAlpha(0x08) })
                    canvas.DrawRect(rect, zp);

            var rowInk = row.Acked ? subtle : text;
            using (var tf = TextFont.Create("00:00:00", Roles.Font("meta"), weight: Roles.Weight("regular")))
            using (var tp = new SKPaint { IsAntialias = true, Color = row.Acked ? subtle : muted })
                canvas.DrawText(row.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture), colTime, Baseline(rect, tf),
                    SKTextAlign.Left, tf, tp);

            // 등급 칩 — 행의 색 표시는 이것 하나. 미확인만 깜빡임, 확인은 색 유지
            DrawSeverityChip(canvas, row.Priority, colPrio, rect.MidY, 20f, blinkOff: !row.Acked && !on);

            // 메시지(열 폭으로 클립)
            var msgRight = showAck ? (ack?.Left ?? rect.Right - 12f - AlarmListLayout.AckButtonWidth) - 12f : rect.Right - 12f;
            canvas.Save();
            canvas.ClipRect(new SKRect(colMsg, rect.Top, msgRight, rect.Bottom));
            using (var mf = TextFont.Create(AlarmText(row), Roles.Font("label"), weight: !row.Acked ? Roles.Weight("bold") : Roles.Weight("regular")))
            using (var mp = new SKPaint { IsAntialias = true, Color = rowInk })
            {
                // 열보다 긴 메시지는 "…" 로 줄인다(글자 중간에서 잘려 보이지 않게)
                var msg = AlarmText(row);
                if (mf.MeasureText(msg) > msgRight - colMsg)
                    msg = Ellipsize(msg, MathF.Max(0f, msgRight - colMsg), mf);
                canvas.DrawText(msg, colMsg, Baseline(rect, mf), SKTextAlign.Left, mf, mp);
            }
            canvas.Restore();

            if (ack is { } b)
            {
                // ACK — 중립 작은 버튼(등급색은 등급 칩에만)
                var br = Roles.Number("radius.sm");
                using (var bb = new SKPaint { IsAntialias = true, Color = ThemeColor("surface-raised") })
                    canvas.DrawRoundRect(b, br, br, bb);
                var hw = Roles.Stroke("hairline");
                using (var bs = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = hw, Color = ThemeColor("border-strong") })
                    canvas.DrawRoundRect(SKRect.Inflate(b, -hw / 2f, -hw / 2f), br - hw / 2f, br - hw / 2f, bs);
                var ackText = T("dv.alarm.ack");
                using var af = TextFont.Create(ackText, Roles.Font("meta"), weight: Roles.Weight("strong"));
                using var ap = new SKPaint { IsAntialias = true, Color = text };
                canvas.DrawText(ackText, b.MidX, Baseline(b, af), SKTextAlign.Center, af, ap);
            }
            else if (showAck && row.AckTime is { } at)
            {
                var ackText = string.Format(T("dv.alarm.acked"), at.ToString("HH:mm", CultureInfo.InvariantCulture));
                using var af = TextFont.Create(ackText, Roles.Font("caption"), weight: Roles.Weight("regular"));
                using var ap = new SKPaint { IsAntialias = true, Color = subtle };
                canvas.DrawText(ackText,
                    rect.Right - 12f - AlarmListLayout.AckButtonWidth / 2f, Baseline(rect, af), SKTextAlign.Center, af, ap);
            }

            using (var lp = new SKPaint { Color = line.WithAlpha(Roles.Alpha("alpha.dim")), StrokeWidth = Roles.Stroke("hairline") })
                canvas.DrawLine(rect.Left, rect.Bottom - 0.5f, rect.Right, rect.Bottom - 0.5f, lp);
        }

        canvas.Restore();
    }

    // 등급 표시 한 벌(H8) — 배너·알람 목록이 같은 칩을 쓴다. 칩 = 등급색 채움 + 대비 글자(alarm-*-fg, 굵기 strong),
    // 테마의 alarm-*-icon(아이콘 이름, 기본 빈 값)이 있으면 글자 앞에 아이콘. blinkOff = 미확인의 꺼짐 위상(등급색을 표면 쪽으로 흐림).
    // inverted = 등급색 면 위에 놓는 칩(대비 글자색 채움 + 등급색 글자). 돌려주는 값 = 칩 폭.
    private float DrawSeverityChip(SKCanvas canvas, AlarmPriority p, float left, float midY, float height, bool blinkOff, bool inverted = false)
    {
        var (prio, fg) = PriorityColors(p);
        if (blinkOff)
            prio = Mix(prio, ThemeColor("surface"), 0.6f);
        var (bg, ink) = inverted ? (fg, prio) : (prio, fg);
        var label = PriorityLabel(p);
        var size = Roles.Font("caption");
        using var font = TextFont.Create(label, size, Roles.Weight("strong"));
        var hasIcon = SeverityIcon(p, out var glyph);
        var w = SeverityChipWidth(p);
        var chip = SKRect.Create(left, midY - height / 2f, w, height);
        using var paint = new SKPaint { IsAntialias = true, Color = bg };
        canvas.DrawRoundRect(chip, height / 2f, height / 2f, paint);
        paint.Color = ink;
        var x = chip.Left + 8f;
        if (hasIcon)
        {
            IconFont.Draw(canvas, glyph, new SKPoint(x + size / 2f, midY), size, paint);
            x += size + 4f;
        }
        canvas.DrawText(label, x, Baseline(chip, font), SKTextAlign.Left, font, paint);
        return w;
    }

    private float SeverityChipWidth(AlarmPriority p)
    {
        var label = PriorityLabel(p);
        var size = Roles.Font("caption");
        using var font = TextFont.Create(label, size, Roles.Weight("strong"));
        return font.MeasureText(label) + 16f + (SeverityIcon(p, out _) ? size + 4f : 0f);
    }

    private bool SeverityIcon(AlarmPriority p, out IconGlyph glyph)
    {
        glyph = default;
        var token = p switch { AlarmPriority.Critical => "alarm-critical-icon", AlarmPriority.Major => "alarm-major-icon", _ => "alarm-minor-icon" };
        return _theme.TryGet(token, out var name) && !string.IsNullOrWhiteSpace(name) && IconFont.TryResolve(name.Trim(), out glyph);
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
        using var font = TextFont.Create(text, size, bold ? Roles.Weight("bold") : Roles.Weight("regular"));
        using var p = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(text, x, Baseline(area, font), SKTextAlign.Left, font, p);
    }

    // AlarmLog 이 스스로 올린 포화 알람만 화면 언어로(dv.alarm.saturated) — 나머지 알람 문구는 앱 데이터라 그대로
    internal string AlarmText(AlarmRow row) =>
        row.Source == "AlarmLog" && row.Message == Alarms.AlarmLog.SaturationMessage ? T("dv.alarm.saturated") : row.Message;
}
