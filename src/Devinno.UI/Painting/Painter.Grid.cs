using Devinno.UI.Grid;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

public sealed partial class Painter
{
    internal const float GridCellPad = 12f;

    // DvDataGrid — 헤더(세로 고정) + 보이는 행만. 스크롤 열은 고정 열 오른쪽 영역에 클립, 고정 열은 가로 스크롤과 무관.
    // 행 관련 비용은 보이는 행 수에만 비례(전체 행 사각형 목록을 만들지 않는다).
    private void PaintGrid(SKCanvas canvas, LayoutBox box, ComputedStyle st, long timestampMs)
    {
        var g = box.Grid!;
        GridLayout.Update(box); // 행 수가 바뀌었을 수 있다(Repaint)
        GridSorter.Ensure(g); // 이 프레임의 행 순서 — 아래는 ItemAtCurrent 로 같은 순서를 쓴다

        var rect = box.Rect;
        // 그리드 색 속성 > 테마 토큰
        var gc = g.Colors;
        var cHeader = gc.Header?.ToSK() ?? ThemeColor("grid-header", new SKColor(0xee, 0xf2, 0xf6));
        var cHeaderFg = gc.HeaderText?.ToSK() ?? ThemeColor("grid-header-fg", new SKColor(0x47, 0x55, 0x69));
        var cRow = gc.Row?.ToSK() ?? ThemeColor("grid-row", SKColors.White);
        var cAlt = gc.RowAlt?.ToSK() ?? ThemeColor("grid-row-alt", new SKColor(0xf8, 0xfa, 0xfc));
        var cHover = gc.Hover?.ToSK() ?? ThemeColor("grid-row-hover", new SKColor(0xef, 0xf6, 0xff));
        var cSel = gc.Selected?.ToSK() ?? ThemeColor("grid-row-selected", new SKColor(0xdb, 0xea, 0xfe));
        var cLine = gc.Line?.ToSK() ?? ThemeColor("grid-line", new SKColor(0xe2, 0xe8, 0xf0));
        var cText = gc.Text?.ToSK() ?? ThemeColor("text", new SKColor(0x0f, 0x17, 0x2a));
        var cSummary = gc.Summary?.ToSK() ?? ThemeColor("grid-summary", new SKColor(0xe2, 0xe8, 0xf0));
        var cSummaryFg = gc.SummaryText?.ToSK() ?? ThemeColor("grid-summary-fg", cText);

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(rect, st.BorderRadius, st.BorderRadius), SKClipOperation.Intersect, antialias: true);

        using var fill = new SKPaint();
        using var ink = new SKPaint { IsAntialias = true };
        using var line = new SKPaint { Color = cLine, StrokeWidth = 1f };
        using var fonts = new GridFonts(st.FontSize, MathF.Max(9f, st.FontSize - 1f));

        var header = SKRect.Create(rect.Left, rect.Top, rect.Width, g.HeaderTotal);
        fill.Color = cHeader;
        canvas.DrawRect(header, fill);

        var bodyTop = header.Bottom;
        var body = new SKRect(rect.Left, bodyTop, rect.Right, bodyTop + g.BodyHeight);
        var scrollLeft = rect.Left + g.FrozenWidth;
        var colsRight = rect.Right - (g.OverflowY ? Interaction.ScrollBars.Gutter : 0);
        var (first, last) = GridGeometry.VisibleRows(box.ScrollY, g.BodyHeight, g.RowHeight, g.Count);
        var hoverOn = !box.Disabled && (g.Mode != GridSelectionMode.None || g.OnRowClick is not null);
        var rh = g.RowHeight;
        float RowTop(int i) => bodyTop + i * rh - box.ScrollY;

        canvas.Save();
        canvas.ClipRect(body);

        // ① 행 배경·구분선 — 선택 > row-class > hover > 줄무늬. 행 스타일은 ②③ 에서 다시 쓰게 프레임 배열에
        var visible = Math.Max(0, last - first + 1);
        if (g.FrameRowStyles.Length < visible)
            g.FrameRowStyles = new GridCellStyle?[Math.Max(visible, 16)];
        for (var i = first; i <= last; i++)
        {
            var item = g.ItemAtCurrent(i);
            var top = RowTop(i);
            var rs = g.RowClass is not null && item is not null ? g.StyleOf(HandlerErrors.Text(g.RowClass.Get(item), g.RowClass.Path, "DvDataGrid")) : null;
            g.FrameRowStyles[i - first] = rs;
            fill.Color = item is not null && g.IsSelected(item) ? cSel
                : rs?.Background?.ToSK()
                  ?? (hoverOn && i == g.HoverRow ? cHover
                      : i % 2 == 0 ? cRow : cAlt);
            canvas.DrawRect(new SKRect(rect.Left, top, rect.Right, top + rh), fill);
            canvas.DrawLine(rect.Left, top + rh - 0.5f, rect.Right, top + rh - 0.5f, line);
        }

        // ② 스크롤 열
        ink.Color = cText;
        canvas.Save();
        canvas.ClipRect(new SKRect(scrollLeft, body.Top, colsRight, body.Bottom));
        for (var c = g.FrozenCount; c < g.Columns.Length; c++)
        {
            var col = g.Columns[c];
            var x = rect.Left + col.X - box.ScrollX;
            if (x + col.W <= scrollLeft || x >= colsRight)
                continue;
            for (var i = first; i <= last; i++)
            {
                if (g.ItemAtCurrent(i) is { } item)
                    DrawBodyCell(canvas, g, c, item, SKRect.Create(x, RowTop(i), col.W, rh), g.FrameRowStyles[i - first], fonts, ink, fill, cText,
                        timestampMs);
            }
        }

        canvas.Restore();

        // ③ 고정 열
        for (var c = 0; c < g.FrozenCount; c++)
        {
            var col = g.Columns[c];
            for (var i = first; i <= last; i++)
            {
                if (g.ItemAtCurrent(i) is { } item)
                    DrawBodyCell(canvas, g, c, item, SKRect.Create(rect.Left + col.X, RowTop(i), col.W, rh), g.FrameRowStyles[i - first],
                        fonts, ink, fill, cText, timestampMs);
            }
        }

        canvas.Restore();

        // ③' 요약 행 — 몸통 아래 고정, 가로는 열과 함께(고정 열 유지)
        if (g.Summaries.Count > 0)
        {
            if (g.SummaryDirty)
                GridSummary.Compute(g);
            // 일반 행과 확실히 구분: 한 단계 진한 배경 + 위쪽 2px 진한 경계 + 굵은 글자 + 요약 행 사이 구분선
            var sTop = body.Bottom;
            var band = new SKRect(rect.Left, sTop, rect.Right, sTop + g.SummaryHeight);
            fill.Color = cSummary;
            canvas.DrawRect(band, fill);
            using (var edge = new SKPaint { Color = ThemeColor("border-strong", new SKColor(0x94, 0xa3, 0xb8)), StrokeWidth = 2f })
                canvas.DrawLine(rect.Left, sTop + 1f, rect.Right, sTop + 1f, edge);
            ink.Color = cSummaryFg;
            using var sumLine = new SKPaint { Color = ThemeColor("border", new SKColor(0xcb, 0xd5, 0xe1)), StrokeWidth = 1f };
            for (var si = 0; si < g.Summaries.Count; si++)
            {
                var texts = g.Summaries[si].Texts;
                var y = sTop + si * rh;
                if (si > 0) // grid-line 은 요약 배경과 같은 밝기라 안 보인다 — 한 단계 진한 border
                    canvas.DrawLine(rect.Left, y + 0.5f, rect.Right, y + 0.5f, sumLine);
                canvas.Save();
                canvas.ClipRect(new SKRect(scrollLeft, y, colsRight, y + rh));
                for (var c = g.FrozenCount; c < g.Columns.Length && c < texts.Length; c++)
                {
                    var col = g.Columns[c];
                    if (texts[c] is { Length: > 0 } t)
                        DrawGridCell(canvas, t, SKRect.Create(rect.Left + col.X - box.ScrollX, y, col.W, rh),
                            c == 0 ? TextAlign.Left : col.EffectiveAlign, fonts.CellBold, ink);
                }

                canvas.Restore();
                for (var c = 0; c < g.FrozenCount && c < texts.Length; c++)
                {
                    var col = g.Columns[c];
                    if (texts[c] is { Length: > 0 } t)
                        DrawGridCell(canvas, t, SKRect.Create(rect.Left + col.X, y, col.W, rh),
                            c == 0 ? TextAlign.Left : col.EffectiveAlign, fonts.CellBold, ink);
                }
            }
        }

        // ③'' 가로 스크롤바 자리(아래 거터) — 비워 두면 요약/행과 스크롤바 사이에 배경이 드러난다. 바로 위 색으로 이어 칠함
        var stripTop = body.Bottom + g.SummaryHeight;
        if (g.OverflowX && stripTop < rect.Bottom)
        {
            fill.Color = g.Summaries.Count > 0 ? cSummary : cRow;
            canvas.DrawRect(new SKRect(rect.Left, stripTop, rect.Right, rect.Bottom), fill);
        }

        // ④ 헤더 — 같은 두 영역(스크롤 열은 가로 오프셋, 고정 열은 그대로)
        // 헤더 글자는 헤더 단(HeaderBand)에만 — 필터 행이 있어도 그룹 세로선이 필터 행으로 내려오지 않음
        var headBand = SKRect.Create(rect.Left, rect.Top, rect.Width, g.HeaderBand);
        ink.Color = cHeaderFg;
        canvas.Save();
        canvas.ClipRect(new SKRect(scrollLeft, headBand.Top, colsRight, headBand.Bottom));
        PaintGridHeader(canvas, g, headBand, rect.Left - box.ScrollX, g.FrozenCount, g.Columns.Length, fonts, ink, line);
        canvas.Restore();
        if (g.FrozenCount > 0)
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(rect.Left, headBand.Top, scrollLeft, headBand.Bottom));
            PaintGridHeader(canvas, g, headBand, rect.Left, 0, g.FrozenCount, fonts, ink, line);
            canvas.Restore();
        }

        // ④' 필터 행 — 필터 쓰는 열 = 깔때기 + 요약(조건 있음 = 강조색), 아니면 빈 칸
        if (g.HasFilterRow)
        {
            var frow = new SKRect(rect.Left, headBand.Bottom, rect.Right, header.Bottom);
            fill.Color = ThemeColor("grid-filter", cHeader);
            canvas.DrawRect(frow, fill);
            var fg = ThemeColor("grid-filter-fg", cHeaderFg);
            var act = ThemeColor("grid-filter-active", new SKColor(0x25, 0x63, 0xeb));
            canvas.Save();
            canvas.ClipRect(new SKRect(scrollLeft, frow.Top, colsRight, frow.Bottom));
            PaintFilterCells(canvas, g, rect, box.ScrollX, g.FrozenCount, g.Columns.Length, fonts, ink, line, fg, act);
            canvas.Restore();
            if (g.FrozenCount > 0)
            {
                canvas.Save();
                canvas.ClipRect(new SKRect(rect.Left, frow.Top, scrollLeft, frow.Bottom));
                PaintFilterCells(canvas, g, rect, box.ScrollX, 0, g.FrozenCount, fonts, ink, line, fg, act);
                canvas.Restore();
            }

            canvas.DrawLine(rect.Left, frow.Top + 0.5f, rect.Right, frow.Top + 0.5f, line);
        }

        // ⑤ 헤더 아래 2px 진한 경계(요약 행 위 경계와 같음 — 헤더·행·요약이 서로 구분되게), 고정 경계선
        using (var edge = new SKPaint { Color = ThemeColor("border-strong", new SKColor(0x94, 0xa3, 0xb8)), StrokeWidth = 2f })
            canvas.DrawLine(rect.Left, header.Bottom - 1f, rect.Right, header.Bottom - 1f, edge);
        if (g.FrozenCount > 0)
            canvas.DrawLine(scrollLeft - 0.5f, rect.Top, scrollLeft - 0.5f, body.Bottom + g.SummaryHeight, line);

        canvas.Restore();
        DrawBorder(canvas, rect, st); // 둥근 모서리(border-radius) 클립 위에 테두리
    }

    // 몸통 셀 — cell-class 배경(행 구분선 1px 남김, 선택보다 우선), 글자색·굵기 = cell-class > row-class > 그리드 글자색
    private void DrawBodyCell(SKCanvas canvas, GridSpec g, int c, object item, SKRect cell, GridCellStyle? rowStyle,
        GridFonts fonts, SKPaint ink, SKPaint fill, SKColor text, long timestampMs)
    {
        var col = g.Columns[c];
        var cs = col.CellClass is null ? null : g.StyleOf(HandlerErrors.Text(col.CellClass.Get(item), col.CellClass.Path, "DvDataGrid"));
        if (cs?.Background is { } bg)
        {
            fill.Color = bg.ToSK();
            canvas.DrawRect(new SKRect(cell.Left, cell.Top, cell.Right, cell.Bottom - 1f), fill);
        }

        var mark = g.EditMark is { } m && m.Column == c && g.IsKey(item, m.Key) ? m : null;
        if (mark is not null && col.CellType == GridCellType.Input)
        {
            fill.Color = ThemeColor("surface", SKColors.White); // 입력 상자처럼(네이티브 편집기 배경과 같음)
            canvas.DrawRect(new SKRect(cell.Left, cell.Top, cell.Right, cell.Bottom - 1f), fill);
        }

        switch (col.CellType)
        {
            case GridCellType.Check:
                DrawCheckCell(canvas, col, item, cell, fill);
                return;
            case GridCellType.Lamp:
                DrawLampCell(canvas, col, item, cell, fill, timestampMs);
                return;
            case GridCellType.Button:
                DrawButtonCell(canvas, col, item, cell, fonts, ink, fill);
                return;
        }

        ink.Color = (cs?.Color ?? rowStyle?.Color)?.ToSK() ?? text;
        var bold = cs?.Bold ?? rowStyle?.Bold ?? false;
        var font = bold ? fonts.CellBold : fonts.Cell;
        switch (col.CellType)
        {
            case GridCellType.Select:
                DrawSelectCell(canvas, col, item, cell, font, ink);
                break;
            case GridCellType.DateTime:
                DrawGridCell(canvas, col.CellText(item), cell, col.EffectiveAlign, font, ink);
                break;
            case GridCellType.Color:
                DrawColorCell(canvas, col, item, cell, font, ink, fill);
                break;
            default:
                if (mark is { HideText: true })
                    break; // 네이티브 편집기가 셀 위에 겹침
                DrawGridCell(canvas, mark?.Text ?? col.Text(item), cell, col.EffectiveAlign, font, ink);
                break;
        }

        if (mark is not null)
            DrawEditMark(canvas, cell, mark);
    }

    // 편집 중·팝업이 열린 셀 — 셀 안쪽 2px 테두리(focus, 거절 사유가 있으면 tone-danger)
    private void DrawEditMark(SKCanvas canvas, SKRect cell, GridEditMark mark)
    {
        using var p = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f,
            Color = mark.Error is null
                ? ThemeColor("focus", new SKColor(0x25, 0x63, 0xeb, 0x66))
                : ThemeColor("tone-danger", new SKColor(0xdc, 0x26, 0x26))
        };
        canvas.DrawRect(new SKRect(cell.Left + 1.5f, cell.Top + 1.5f, cell.Right - 1.5f, cell.Bottom - 2.5f), p);
    }

    // select — 고른 항목의 [아이콘] 글자(맞는 항목 없음 = 빈 칸)
    private static void DrawSelectCell(SKCanvas canvas, GridColumn col, object item, SKRect cell, GridFonts.Slot font, SKPaint ink)
    {
        var i = col.OptionIndex(col.Reader.Get(item));
        if (i < 0)
            return;
        var o = col.Options[i];
        if (o.Icon is { } spec && IconFont.TryResolve(spec, out var icon))
        {
            var size = font.For(o.Text.Length > 0 ? o.Text : "A").Size;
            IconFont.Draw(canvas, icon, new SKPoint(cell.Left + GridCellPad + size / 2f, cell.MidY), size, ink);
            cell = new SKRect(cell.Left + size + LayoutEngine.IconTextGap, cell.Top, cell.Right, cell.Bottom);
        }

        DrawGridCell(canvas, o.Text, cell, col.EffectiveAlign, font, ink);
    }

    // color — 견본(20×20, border 테두리) + #RRGGBB(DvColorInput 상자와 같은 모양), 값 없음 = 빈 칸. 칠 붓 재사용(셀마다 할당 없음)
    private void DrawColorCell(SKCanvas canvas, GridColumn col, object item, SKRect cell, GridFonts.Slot font, SKPaint ink, SKPaint fill)
    {
        if (col.Reader.Get(item) is not { } v || !PickerValue.TryToColor(v, out var c))
            return;
        var sw = SKRect.Create(cell.Left + GridCellPad, cell.MidY - PickerSwatch / 2f, PickerSwatch, PickerSwatch);
        fill.IsAntialias = true;
        fill.Color = new SKColor(c.Red, c.Green, c.Blue);
        canvas.DrawRoundRect(sw, 4, 4, fill);
        fill.Style = SKPaintStyle.Stroke;
        fill.StrokeWidth = 1;
        fill.Color = ThemeColor("border", new SKColor(0xcb, 0xd5, 0xe1));
        canvas.DrawRoundRect(sw, 4, 4, fill);
        fill.Style = SKPaintStyle.Fill;
        fill.IsAntialias = false;
        DrawGridCell(canvas, PickerValue.Hex(c), new SKRect(sw.Right + LayoutEngine.IconTextGap - GridCellPad, cell.Top, cell.Right, cell.Bottom),
            TextAlign.Left, font, ink);
    }

    // 체크박스 — 셀 가운데 20×20, 체크 = primary 채움 + 흰 ✓, 표시 전용(readonly·bool 아님)이면 흐리게
    private void DrawCheckCell(SKCanvas canvas, GridColumn col, object item, SKRect cell, SKPaint fill)
    {
        const float size = 20f;
        var box = SKRect.Create(cell.MidX - size / 2f, cell.MidY - size / 2f, size, size);
        var on = col.Reader.Get(item) is true;
        var dim = col.ReadOnly || (col.Reader.ValueType is { } vt && (vt != typeof(bool) || !col.Reader.Writable));
        byte a = dim ? (byte)0x80 : (byte)0xff;
        fill.IsAntialias = true;

        if (on)
        {
            fill.Color = ThemeColor("primary", new SKColor(0x25, 0x63, 0xeb)).WithAlpha(a);
            canvas.DrawRoundRect(box, 4f, 4f, fill);
            using var ip = new SKPaint { IsAntialias = true, Color = ThemeColor("primary-fg", SKColors.White).WithAlpha(a) };
            if (IconFont.TryResolve("fa-check", out var glyph))
                IconFont.Draw(canvas, glyph, new SKPoint(box.MidX, box.MidY), 13f, ip);
        }
        else
        {
            fill.Color = ThemeColor("surface", SKColors.White).WithAlpha(a);
            canvas.DrawRoundRect(box, 4f, 4f, fill);
            using var sp = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f,
                Color = ThemeColor("border-strong", new SKColor(0x94, 0xa3, 0xb8)).WithAlpha(a)
            };
            canvas.DrawRoundRect(box, 4f, 4f, sp);
        }

        fill.IsAntialias = false;
    }

    // 램프 — bool = on-color(기본 state-run), 문자열·enum = state-{값} 토큰, 없으면 꺼짐. 점멸 = 화면 공통 위상
    private void DrawLampCell(SKCanvas canvas, GridColumn col, object item, SKRect cell, SKPaint fill, long timestampMs)
    {
        var v = col.Reader.Get(item);
        SKColor? lit = v switch
        {
            bool b => b ? col.OnColor?.ToSK() ?? ToneToken("state-run", new SKColor(0x16, 0xa3, 0x4a)) : null,
            null => null,
            _ => StateColor(HandlerErrors.Text(v, col.Reader.Path, "DvDataGrid"))
        };
        if (lit is not null && col.Blink && !Motion.MotionPhase.BlinkOn(timestampMs, AlarmBlinkMs))
            lit = null;

        var radius = MathF.Max(3f, MathF.Min(9f, MathF.Min(cell.Width, cell.Height) / 2f - 4f));
        fill.IsAntialias = true;
        fill.Color = lit ?? new SKColor(0x3a, 0x3f, 0x47); // DvLamp 꺼진 색과 같음
        canvas.DrawCircle(cell.MidX, cell.MidY, radius, fill);
        if (lit is not null)
        {
            fill.Color = new SKColor(0xff, 0xff, 0xff, 0x66);
            canvas.DrawCircle(cell.MidX - radius * 0.3f, cell.MidY - radius * 0.3f, radius * 0.35f, fill);
        }

        fill.IsAntialias = false;
    }

    private SKColor? StateColor(string? state)
    {
        if (string.IsNullOrEmpty(state))
            return null;
        var theme = _theme;
        return theme.TryGet("state-" + state.ToLowerInvariant(), out var raw) && DvColor.TryParse(raw, out var c) ? c.ToSK() : null;
    }

    // 버튼 — 셀 안쪽(가로 6·세로 8 여백) 둥근 사각형 + [아이콘] 글자, variant 색은 DvButton 과 같음, enabled=false 면 흐림
    private void DrawButtonCell(SKCanvas canvas, GridColumn col, object item, SKRect cell, GridFonts fonts, SKPaint ink, SKPaint fill)
    {
        var r = new SKRect(cell.Left + 6f, cell.Top + 8f, cell.Right - 6f, cell.Bottom - 8f);
        if (r.Width <= 4 || r.Height <= 4)
            return;
        var enabled = col.Enabled is null || col.Enabled.Get(item) is not false;
        SKColor bg, fg;
        SKColor? border = null;
        if (!enabled)
        {
            bg = ThemeColor("surface-raised", new SKColor(0xff, 0xff, 0xff));
            fg = ThemeColor("text-subtle", new SKColor(0x94, 0xa3, 0xb8));
        }
        else
        {
            switch (col.Variant)
            {
                case "secondary":
                    bg = ThemeColor("surface-raised", SKColors.White);
                    fg = ThemeColor("text", new SKColor(0x0f, 0x17, 0x2a));
                    border = ThemeColor("border-strong", new SKColor(0x94, 0xa3, 0xb8));
                    break;
                case "danger":
                    bg = ToneToken("tone-danger-soft", new SKColor(0xfe, 0xe2, 0xe2));
                    fg = ToneToken("tone-danger-fg", new SKColor(0xb9, 0x1c, 0x1c));
                    border = ToneToken("tone-danger", new SKColor(0xdc, 0x26, 0x26));
                    break;
                case "ghost":
                    bg = SKColors.Transparent;
                    fg = ThemeColor("text-muted", new SKColor(0x64, 0x74, 0x8b));
                    break;
                default:
                    bg = ThemeColor("primary", new SKColor(0x25, 0x63, 0xeb));
                    fg = ThemeColor("primary-fg", SKColors.White);
                    break;
            }
        }

        fill.IsAntialias = true;
        fill.Color = bg;
        canvas.DrawRoundRect(r, 6f, 6f, fill);
        fill.IsAntialias = false;
        if (border is { } bc)
        {
            using var bp = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = bc };
            canvas.DrawRoundRect(r, 6f, 6f, bp);
        }

        ink.Color = fg;
        var hasIcon = IconFont.TryResolve(col.Icon, out var glyph);
        if (!hasIcon)
        {
            DrawGridCell(canvas, col.ButtonText, new SKRect(r.Left - GridCellPad + 4f, r.Top, r.Right + GridCellPad - 4f, r.Bottom),
                TextAlign.Center, fonts.CellBold, ink);
            return;
        }

        var font = fonts.CellBold.For(col.ButtonText.Length > 0 ? col.ButtonText : "A");
        var iconSize = font.Size;
        var textW = col.ButtonText.Length > 0 ? font.MeasureText(col.ButtonText) : 0f;
        var gap = textW > 0 ? 6f : 0f;
        var total = MathF.Min(r.Width - 8f, iconSize + gap + textW);
        var x = r.MidX - total / 2f;
        IconFont.Draw(canvas, glyph, new SKPoint(x + iconSize / 2f, r.MidY), iconSize, ink);
        if (textW > 0)
        {
            canvas.Save();
            canvas.ClipRect(r);
            canvas.DrawText(col.ButtonText, x + iconSize + gap, Baseline(r, font), SKTextAlign.Left, font, ink);
            canvas.Restore();
        }
    }

    // 열 [from, to) 의 필터 칸 — 오른쪽 구분선, 필터 쓰는 열은 깔때기 + 조건 요약(조건 있으면 강조색)
    private void PaintFilterCells(SKCanvas canvas, GridSpec g, SKRect grid, float scrollX, int from, int to,
        GridFonts fonts, SKPaint ink, SKPaint line, SKColor fg, SKColor active)
    {
        for (var c = from; c < to; c++)
        {
            var cell = GridGeometry.FilterCellRect(g, grid, scrollX, c);
            canvas.DrawLine(cell.Right - 0.5f, cell.Top, cell.Right - 0.5f, cell.Bottom, line);
            if (g.Columns[c].Filter == GridFilterSetting.None)
                continue;
            var on = g.Filters.TryGetValue(c, out var cond);
            ink.Color = on ? active : fg;
            const float icon = 11f;
            var ix = cell.Left + GridCellPad + icon / 2f;
            if (IconFont.TryResolve("fa-filter", out var glyph))
                IconFont.Draw(canvas, glyph, new SKPoint(ix, cell.MidY), icon, ink);
            if (on && cond!.Summary.Length > 0)
                DrawGridCell(canvas, cond.Summary, new SKRect(ix + icon / 2f + 2f, cell.Top, cell.Right, cell.Bottom), TextAlign.Left, fonts.Header, ink);
        }
    }

    // 열 [from, to) 의 헤더 — 열 칸은 자기 깊이부터 맨 아래 단까지, 그룹 칸은 자기 단 한 칸(가운데 정렬) + 아래·양옆 구분선
    private void PaintGridHeader(SKCanvas canvas, GridSpec g, SKRect header, float originX, int from, int to,
        GridFonts fonts, SKPaint ink, SKPaint line)
    {
        var hh = g.HeaderHeight;
        var levels = g.Header.Levels;
        for (var c = from; c < to; c++)
        {
            var col = g.Columns[c];
            var depth = g.Header.DepthOf(c);
            var cell = SKRect.Create(originX + col.X, header.Top + depth * hh, col.W, (levels - depth) * hh);
            DrawGridHeaderColumn(canvas, g, c, cell, fonts, ink);
        }

        foreach (var gr in g.Header.Groups)
        {
            if (gr.First < from || gr.Last >= to)
                continue;
            var x0 = originX + g.Columns[gr.First].X;
            var x1 = originX + g.Columns[gr.Last].X + g.Columns[gr.Last].W;
            var cell = new SKRect(x0, header.Top + gr.Level * hh, x1, header.Top + (gr.Level + 1) * hh);
            DrawGridCell(canvas, gr.Header, cell, TextAlign.Center, fonts.Header, ink);
            canvas.DrawLine(x0, cell.Bottom - 0.5f, x1, cell.Bottom - 0.5f, line);
            canvas.DrawLine(x0 + 0.5f, cell.Top, x0 + 0.5f, header.Bottom, line);
            canvas.DrawLine(x1 - 0.5f, cell.Top, x1 - 0.5f, header.Bottom, line);
        }
    }

    private void DrawGridHeaderColumn(SKCanvas canvas, GridSpec g, int c, SKRect cell, GridFonts fonts, SKPaint ink)
    {
        var col = g.Columns[c];
        if (!col.Sortable)
        {
            DrawGridCell(canvas, col.Header, cell, col.EffectiveAlign, fonts.Header, ink);
            return;
        }

        // 정렬 표시(오른쪽): 정렬 안 됨 = 옅은 ↕, 오름 ▲, 내림 ▼, 정렬 열 2개 이상이면 우선순위 번호
        var k = g.Sort.FindIndex(s => s.Column == c);
        var spec = k < 0 ? "fa-sort" : g.Sort[k].Desc ? "fa-sort-down" : "fa-sort-up";
        var iconX = cell.Right - GridCellPad - 5f;
        var textRight = iconX - 9f;
        using (var ip = new SKPaint { IsAntialias = true, Color = k < 0 ? ink.Color.WithAlpha(0x55) : ink.Color })
        {
            if (IconFont.TryResolve(spec, out var glyph))
                IconFont.Draw(canvas, glyph, new SKPoint(iconX, cell.MidY), 11f, ip);
            if (k >= 0 && g.Sort.Count > 1)
            {
                var num = (k + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                using var nf = TextFont.Create(num, 10f, bold: true);
                var nx = iconX - 9f - nf.MeasureText(num);
                canvas.DrawText(num, nx, Baseline(cell, nf), SKTextAlign.Left, nf, ip);
                textRight = nx - 3f;
            }
        }

        DrawGridCell(canvas, col.Header, new SKRect(cell.Left, cell.Top, MathF.Max(cell.Left, textRight + GridCellPad), cell.Bottom),
            col.EffectiveAlign, fonts.Header, ink);
    }

    // 셀 글자 — 좌우 패딩, 넘치면 "…" 로 자름, 세로 가운데
    private static void DrawGridCell(SKCanvas canvas, string text, SKRect cell, TextAlign align, GridFonts.Slot slot, SKPaint ink)
    {
        var avail = cell.Width - GridCellPad * 2;
        if (text.Length == 0 || avail <= 0)
            return;

        var font = slot.For(text);
        var w = font.MeasureText(text);
        if (w > avail)
        {
            text = Ellipsize(text, avail, font);
            if (text.Length == 0)
                return;
            w = font.MeasureText(text);
        }

        var x = align switch
        {
            TextAlign.Right => cell.Right - GridCellPad - w,
            TextAlign.Center => cell.MidX - w / 2f,
            _ => cell.Left + GridCellPad
        };
        canvas.DrawText(text, x, Baseline(cell, font), SKTextAlign.Left, font, ink);
    }

    private static string Ellipsize(string text, float avail, SKFont font)
    {
        const string dots = "…";
        int lo = 0, hi = text.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (font.MeasureText(string.Concat(text.AsSpan(0, mid), dots)) <= avail)
                lo = mid;
            else
                hi = mid - 1;
        }

        return lo == 0 ? (font.MeasureText(dots) <= avail ? dots : "") : string.Concat(text.AsSpan(0, lo), dots);
    }

    /// <summary>
    /// Fonts used during one grid paint. They are not created per cell; a new one is made only when the typeface changes (e.g. Korean fallback).
    /// </summary>
    private sealed class GridFonts : IDisposable
    {
        public GridFonts(float cellSize, float headerSize)
        {
            Cell = new Slot(cellSize, bold: false);
            CellBold = new Slot(cellSize, bold: true);
            Header = new Slot(headerSize, bold: true);
        }

        public Slot Cell { get; }
        /// <summary>Summary row. The font is created on first use (no cost for grids without a summary).</summary>
        public Slot CellBold { get; }
        public Slot Header { get; }

        public void Dispose()
        {
            Cell.Dispose();
            CellBold.Dispose();
            Header.Dispose();
        }

        internal sealed class Slot : IDisposable
        {
            private readonly float _size;
            private readonly bool _bold;
            // 서체별 폰트(보통 1~2개: 라틴 기본 + 한글 폴백)
            private readonly List<(SKTypeface Face, SKFont Font)> _fonts = new(2);

            public Slot(float size, bool bold)
            {
                _size = size;
                _bold = bold;
            }

            public SKFont For(string text)
            {
                var face = TextFont.ResolveTypeface(text, TextFont.AmbientFamily);
                foreach (var (f, font) in _fonts)
                {
                    if (ReferenceEquals(f, face))
                        return font;
                }

                var made = new SKFont(face, _size) { Embolden = _bold };
                _fonts.Add((face, made));
                return made;
            }

            public void Dispose()
            {
                foreach (var (_, font) in _fonts)
                    font.Dispose();
            }
        }
    }
}
