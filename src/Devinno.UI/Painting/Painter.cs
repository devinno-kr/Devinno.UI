using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Painting;

/// <summary>
/// A painter that paints an arranged <see cref="LayoutBox"/> tree onto a SkiaSharp canvas. Paints backgrounds, borders, text and each widget (gauge, tank, trend, grid, input box, etc.).
/// </summary>
/// <remarks>
/// <para>This is an internal engine stage. App code normally does not use it directly; it uses <see cref="DvSurface"/>/<see cref="DvRenderer"/> instead.
/// Use it when you want to paint a <see cref="LayoutEngine"/> result directly onto your own <see cref="SKCanvas"/> (an offscreen image, printing, etc.).</para>
/// <para>Painting only reads box values and does not change the tree. The order in which a state style is chosen for each box is disabled → pressed → selected → hover → base.
/// Disabled boxes (including those inheriting it from an ancestor) do not use pressed or hover styles. They use <c>disabled-style</c> if present; otherwise the selected style (when selected) or the base style.
/// The hover/press brightness of a knob face and the hover row of a grid are also not painted when disabled.
/// <c>overflow:hidden</c>/<c>overflow:scroll</c> boxes clip their children to inside their corners, and scroll boxes paint children shifted by the offset and then draw the scrollbars on top.</para>
/// <para>Pass the same theme that was given to <see cref="LayoutEngine"/>. If the theme is <see langword="null"/>, the widget color tokens of
/// <see cref="DvTheme.Default"/> are used, as in <see cref="LayoutEngine"/>. Only widget color tokens missing from the theme are painted with fallback colors built into the code. Use on a single UI thread only.</para>
/// </remarks>
/// <example>
/// <code>
/// using Devinno.UI.Layout;
/// using Devinno.UI.Markup;
/// using Devinno.UI.Painting;
/// using Devinno.UI.Styling;
/// using SkiaSharp;
///
/// var vm = new { Speed = 42.0 };
/// var theme = DvTheme.Default;
/// var root = MarkupParser.Parse("&lt;DvStack&gt;&lt;DvGauge value=\"{Speed}\"/&gt;&lt;/DvStack&gt;");
/// var tree = new LayoutEngine(theme).Layout(root, vm, 400, 300);
///
/// using var surface = SKSurface.Create(new SKImageInfo(400, 300));
/// surface.Canvas.Clear(SKColors.White);
/// new Painter(theme).Paint(surface.Canvas, tree, vm);
/// </code>
/// </example>
public sealed partial class Painter
{
    private readonly DvTheme _theme;

    /// <summary>Creates a painter without a theme. Widget colors use the tokens of <see cref="DvTheme.Default"/>.</summary>
    public Painter() : this(null)
    {
    }

    private readonly Localization.DvStrings? _strings;

    /// <summary>Creates a painter with a theme and a localization table.</summary>
    /// <param name="theme">The theme to read color tokens from. If <see langword="null"/>, <see cref="DvTheme.Default"/> is used.</param>
    /// <param name="strings">Localization table for text that widgets paint themselves (alarm list headers, etc.). If a <c>dv.*</c> key is missing or the table is <see langword="null"/>, the default text is used.</param>
    public Painter(DvTheme? theme, Localization.DvStrings? strings) : this(theme)
    {
        _strings = strings;
    }

    /// <summary>Built-in widget text for a <c>dv.*</c> key: the app's table if it has the key, otherwise the built-in English (or Korean) text.</summary>
    internal string T(string key) => Localization.DvStrings.Builtin(_strings, key);

    /// <summary>Creates a painter with a theme (no localization table).</summary>
    /// <param name="theme">The theme to read color tokens from. If <see langword="null"/>, <see cref="DvTheme.Default"/> is used.</param>
    public Painter(DvTheme? theme)
    {
        _theme = theme ?? DvTheme.Default;
    }

    /// <summary>
    /// Paints <paramref name="box"/> and all its children onto the canvas. Coordinates are each box's <see cref="LayoutBox.Rect"/> (px) as is.
    /// </summary>
    /// <param name="canvas">The canvas to paint on. The background is not cleared, so call <see cref="SKCanvas.Clear(SKColor)"/> first if needed.</param>
    /// <param name="box">The box to paint (usually the root returned by <see cref="LayoutEngine.Layout"/>).</param>
    /// <param name="vm">The view model used for layout. Currently not used for painting; it is passed down to children as is (may be <see langword="null"/>). Values are already filled into the boxes at layout time.</param>
    /// <param name="state">The hover/press target boxes. If <see langword="null"/>, painting uses no state styles.</param>
    /// <param name="timestampMs">Paint time (ms). Used only to set the blink phase of lamps and alarms. The same time produces the same image, so pass a fixed value for screen captures and tests.</param>
    public void Paint(SKCanvas canvas, LayoutBox box, object? vm, InteractionState? state = null, long timestampMs = 0)
        => PaintAt(canvas, box, vm, state, timestampMs, 17);

    /// <summary>CSS <c>transition</c> state (held by the surface between frames). If null, the chosen style is used as is, without transitions.</summary>
    internal MotionStore? MotionState { get; set; }

    // key = 트리 경로 열쇠(부모 열쇠·자식 번호) — 전환 상태를 프레임 사이에 같은 상자로 잇는다
    private void PaintAt(SKCanvas canvas, LayoutBox box, object? vm, InteractionState? state, long timestampMs, long key)
    {
        var st = Effective(box, state);
        if (MotionState is { } motion && MotionStore.Wants(box))
            st = motion.Apply(key, box, st, timestampMs);
        var opacity = Math.Clamp(st.Opacity, 0f, 1f);
        if (opacity <= 0f)
            return; // 다 투명 — 자리는 그대로(배치에는 영향 없음)

        // opacity·transform — 상자와 자식 전체를 함께(CSS 와 같음). 그리기에만 적용
        var saved = -1;
        if (opacity < 1f || !st.Transform.IsIdentity)
        {
            saved = canvas.Save();
            if (!st.Transform.IsIdentity)
            {
                var xf = st.Transform;
                var c = new SKPoint(box.Rect.MidX, box.Rect.MidY);
                canvas.Translate(c.X + xf.Tx, c.Y + xf.Ty);
                canvas.Scale(xf.Sx, xf.Sy);
                canvas.Translate(-c.X, -c.Y);
            }
            if (opacity < 1f)
            {
                using var layer = new SKPaint { Color = SKColors.White.WithAlpha((byte)MathF.Round(opacity * 255f)) };
                canvas.SaveLayer(layer);
            }
        }

        // 요소의 (상속된) font-family 를 위젯 내부 문구에도 적용
        var prevFamily = TextFont.AmbientFamily;
        TextFont.AmbientFamily = box.Style.FontFamily ?? prevFamily;
        try
        {
            PaintBox(canvas, box, st, vm, state, timestampMs, key);
        }
        finally
        {
            TextFont.AmbientFamily = prevFamily;
            if (saved >= 0)
                canvas.RestoreToCount(saved);
        }
    }

    private void PaintBox(SKCanvas canvas, LayoutBox box, ComputedStyle st, object? vm, InteractionState? state, long timestampMs, long key)
    {
        var rect = box.Node.Tag == "DvSegment" && !box.SegmentFill ? SegmentFitRect(box, st) : box.Rect;
        var r = st.BorderRadius;

        if (st.Shadow is { } sh)
        {
            using var sp = new SKPaint { IsAntialias = true, Color = sh.Color.ToSK() };
            // 마스크 블러 — 이미지 필터 블러(매 프레임 오프스크린 CPU 블러)보다 훨씬 싸다(둥근 사각형 전용 경로)
            if (sh.Blur > 0)
                sp.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sh.Blur / 2);
            var srect = new SKRect(rect.Left + sh.Dx, rect.Top + sh.Dy, rect.Right + sh.Dx, rect.Bottom + sh.Dy);
            canvas.DrawRoundRect(srect, r, r, sp);
        }

        if (st.Gradient is { } grad)
        {
            using var p = new SKPaint { IsAntialias = true, Shader = MakeShader(grad, rect) };
            canvas.DrawRoundRect(rect, r, r, p);
        }
        else if (st.Background is { } bg)
        {
            using var p = new SKPaint { IsAntialias = true, Color = bg.ToSK() };
            canvas.DrawRoundRect(rect, r, r, p);
        }

        // 길게 누르기(hold) 진행 — 배경 위·글자 아래에 왼쪽부터 차오르는 띠(글자색 반투명), 상자 모양으로 자른다
        if (state is not null && ReferenceEquals(state.HoldBox, box) && state.HoldProgress > 0)
        {
            canvas.Save();
            using (var holdClip = new SKRoundRect(rect, r, r))
                canvas.ClipRoundRect(holdClip, antialias: true);
            using var hp = new SKPaint { IsAntialias = true, Color = st.Color.ToSK().WithAlpha(0x40) };
            canvas.DrawRect(new SKRect(rect.Left, rect.Top, rect.Left + rect.Width * state.HoldProgress, rect.Bottom), hp);
            canvas.Restore();
        }

        if (box.Grid is null) // 그리드는 내용 위에 마지막으로 그린다(PaintGrid) — 셀 배경이 테두리 안쪽을 덮지 않게
            DrawBorder(canvas, rect, st);

        if (box.ResolvedText is not null && IconFont.TryResolve(box.Icon, out var btnIcon))
        {
            PaintIconText(canvas, box, st, btnIcon);
        }
        else if (box.ResolvedText is { Length: > 0 } text)
        {
            // 측정과 같은 글꼴(font-family)·같은 줄 — 측정 때 나눈 줄을 쓰고, 그리는 폭이 다르면 그 폭으로 다시 나눈다(높이는 그대로)
            using var font = TextFont.Create(text, st.FontSize, st.Bold, st.FontFamily);
            using var tp = new SKPaint { IsAntialias = true, Color = st.Color.ToSK() };
            var lines = LayoutEngine.TextLinesFor(box, st, font, rect.Width - st.Padding.Horizontal);
            var tx = rect.Left + st.Padding.Left;
            var baseline = ContentBaseline(box, st, font, lines.Count);
            var align = st.TextAlign switch
            {
                TextAlign.Center => SKTextAlign.Center,
                TextAlign.Right => SKTextAlign.Right,
                _ => SKTextAlign.Left
            };

            if (align == SKTextAlign.Center)
                tx = rect.MidX;
            else if (align == SKTextAlign.Right)
                tx = rect.Right - st.Padding.Right;

            // 줄마다 text-align 으로 가로 정렬
            for (var i = 0; i < lines.Count; i++)
                canvas.DrawText(lines[i], tx, baseline + i * font.Spacing, align, font, tp);
        }

        if (box.Node.Tag == "DvToggle")
        {
            var tr = WidgetMetrics.ShapeRect(box); // 늘어난 상자에서도 자기 크기로 가운데
            var radius = tr.Height / 2f;
            // 비활성 = 반투명(체크박스·슬라이더와 같음), 읽기 전용(단방향·고정 값) = 점선 테두리(입력 상자 읽기 전용과 같음)
            var a = box.Disabled ? (byte)0x80 : (byte)0xff;
            var readOnly = !box.Disabled && !Binding.BindingResolver.IsTwoWay(box.ValueBindExpr);
            var trackOn = ThemeColor("--toggle-on", new SKColor(0x3A, 0x7B, 0xD5)).WithAlpha(a);
            var trackOff = ThemeColor("--toggle-off", new SKColor(0xC7, 0xCD, 0xD5)).WithAlpha(a);
            using (var trackPaint = new SKPaint
                   {
                       IsAntialias = true,
                       Color = box.ToggleOn ? trackOn : trackOff
                   })
            {
                canvas.DrawRoundRect(tr, radius, radius, trackPaint);
            }

            if (readOnly)
            {
                using var dash = SKPathEffect.CreateDash([4f, 3f], 0);
                using var edge = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.5f,
                    PathEffect = dash,
                    Color = ThemeColor("border-strong", new SKColor(0x94, 0xa3, 0xb8))
                };
                var o = SKRect.Inflate(tr, 2.5f, 2.5f);
                canvas.DrawRoundRect(o, radius + 2.5f, radius + 2.5f, edge);
            }

            var knobDiameter = tr.Height - 4;
            var knobX = box.ToggleOn ? tr.Right - knobDiameter - 2 : tr.Left + 2;
            using var knobPaint = new SKPaint
            {
                IsAntialias = true,
                Color = ThemeColor("--toggle-knob", SKColors.White).WithAlpha(a)
            };
            canvas.DrawRoundRect(
                SKRect.Create(knobX, tr.Top + 2, knobDiameter, knobDiameter),
                knobDiameter / 2,
                knobDiameter / 2,
                knobPaint);
        }

        if (box.Node.Tag == "DvLamp")
        {
            var lit = box.LampOn &&
                      (!box.LampBlink || Motion.MotionPhase.BlinkOn(timestampMs, box.BlinkIntervalMs));
            // color 속성 > state(run/fault… → state-* 토큰) > style color
            var onColor = box.LampColor?.ToSK()
                          ?? (box.LampState is { Length: > 0 } ls ? ToneToken("state-" + ls, st.Color.ToSK()) : st.Color.ToSK());
            var offColor = new SKColor(0x3a, 0x3f, 0x47);
            var cx = rect.MidX;
            var cy = rect.MidY;
            var radius = MathF.Max(0, MathF.Min(rect.Width, rect.Height) / 2f - 1f);

            using var glow = new SKPaint { IsAntialias = true, Color = lit ? onColor : offColor };
            canvas.DrawCircle(cx, cy, radius, glow);

            if (lit)
            {
                using var hi = new SKPaint
                {
                    IsAntialias = true,
                    Color = new SKColor(0xff, 0xff, 0xff, 0x66)
                };
                canvas.DrawCircle(cx - radius * 0.3f, cy - radius * 0.3f, radius * 0.35f, hi);
            }
        }

        if (box.Node.Tag == "DvGauge")
        {
            const float startAngle = 135f;
            const float sweep = 270f;
            var cx = rect.MidX;
            var cy = rect.MidY;
            var gm = WidgetMetrics.Gauge(rect, box.GaugeTicks > 0 && box.GaugeTickLabels);
            var radius = gm.Radius;
            var oval = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

            using (var track = new SKPaint
                   {
                       IsAntialias = true,
                       Style = SKPaintStyle.Stroke,
                       StrokeWidth = gm.Stroke,
                       StrokeCap = SKStrokeCap.Butt,
                       Color = ThemeColor("gauge-track", new SKColor(0xe2, 0xe8, 0xf0))
                   })
            using (var path = new SKPath())
            {
                path.AddArc(oval, startAngle, sweep);
                canvas.DrawPath(path, track);
            }

            var range = box.GaugeMax - box.GaugeMin;
            var t = range <= 0 ? 0f : Math.Clamp((box.GaugeValue - box.GaugeMin) / range, 0f, 1f);

            DrawGaugeZones(canvas, box, oval, startAngle, sweep, gm.Stroke);

            var needleColor = ThemeColor("gauge-needle", new SKColor(0x0f, 0x17, 0x2a));
            if (box.GaugeTicks > 0)
                DrawGaugeScale(canvas, box, cx, cy, gm, startAngle, sweep);
            DrawGaugeNeedle(canvas, box, cx, cy, radius * 0.9f, startAngle + t * sweep, needleColor, gm);

            var label = FormatValue(box.GaugeValue, box.ValueFormat);
            using var font = TextFont.Create(label, gm.ValueFont, bold: true);
            var valColor = ZoneColorAt(box) ?? needleColor;
            using var tp = new SKPaint { IsAntialias = true, Color = valColor };
            canvas.DrawText(label, cx, cy + gm.ValueBaseline, SKTextAlign.Center, font, tp);
        }

        if (box.Node.Tag == "DvImage")
        {
            var image = Widgets.ImageCache.Get(box.ImageSrc);
            if (image is not null)
            {
                var iw = (float)image.Width;
                var ih = (float)image.Height;
                SKRect dest;
                if (box.ImageFit == Widgets.ImageFit.Fill || iw <= 0 || ih <= 0)
                {
                    dest = rect;
                }
                else
                {
                    var scale = box.ImageFit == Widgets.ImageFit.Cover
                        ? MathF.Max(rect.Width / iw, rect.Height / ih)
                        : MathF.Min(rect.Width / iw, rect.Height / ih);
                    var dw = iw * scale;
                    var dh = ih * scale;
                    var dx = rect.Left + (rect.Width - dw) / 2f;
                    var dy = rect.Top + (rect.Height - dh) / 2f;
                    dest = new SKRect(dx, dy, dx + dw, dy + dh);
                }

                canvas.Save();
                canvas.ClipRect(rect);
                using var paint = new SKPaint { IsAntialias = true };
                canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear), paint);
                canvas.Restore();
            }
        }

        if (box.Node.Tag == "DvSparkline")
            PaintSparkline(canvas, box);

        if (box.Node.Tag == "DvBarChart")
            PaintBarChart(canvas, box);

        if (box.Node.Tag == "DvPieChart")
            PaintPieChart(canvas, box);

        if (box.Node.Tag == "DvStepBar")
            PaintStepBar(canvas, box);

        if (box.Node.Tag == "DvSlider")
            PaintSlider(canvas, box, state);

        if (box.Node.Tag == "DvKnob")
            PaintKnob(canvas, box, state);

        if (box.Node.Tag is "DvCheckBox" or "DvRadio")
            PaintCheck(canvas, box, state);

        if (box.Node.Tag == "DvInput")
            PaintInput(canvas, box, st);

        if (box.Node.Tag == "DvSelect")
            PaintSelect(canvas, box, st);

        if (box.IsPicker)
            PaintPicker(canvas, box, st);

        if (box.Node.Tag == "DvIcon" && IconFont.TryResolve(box.Icon, out var icon))
        {
            using var ip = new SKPaint { IsAntialias = true, Color = st.Color.ToSK() };
            var c = new SKRect(rect.Left + st.Padding.Left, rect.Top + st.Padding.Top,
                rect.Right - st.Padding.Right, rect.Bottom - st.Padding.Bottom);
            IconFont.Draw(canvas, icon, new SKPoint(c.MidX, c.MidY), st.FontSize, ip);
        }

        if (box.Node.Tag == "DvProgress")
            PaintProgress(canvas, box, st);

        if (box.Node.Tag == "DvRing")
            PaintRing(canvas, box);

        if (box.Node.Tag == "DvAlarmBanner")
            PaintAlarmBanner(canvas, box, st, timestampMs);

        if (box.Node.Tag == "DvAlarmList")
            PaintAlarmList(canvas, box, timestampMs);

        if (box.Grid is not null)
            PaintGrid(canvas, box, st, timestampMs);

        if (box.Node.Tag == "DvTrend")
            PaintTrend(canvas, box, st);

        if (box.Node.Tag == "DvTank")
        {
            var tm = WidgetMetrics.Tank(rect);
            var range = box.TankMax - box.TankMin;

            // 임계 경계값 라벨(우측 여백) — zone 시작점 중 바닥(min) 초과만
            var marks = range > 0 && box.TankZoneMarks
                ? box.TankZones.Where(z => z.From > box.TankMin).Select(z => (z.From, z.Color)).ToList()
                : new List<(float From, DvColor Color)>();
            // 레이아웃: [벽] 3px 간격 · 6px 눈금 · 4px 간격 · 숫자
            float tickGap = tm.TickGap, tickLen = tm.TickLen, labelGap = tm.LabelGap;
            using var markFont = TextFont.Create("0", tm.MarkFont, bold: false);
            var gutter = marks.Count == 0
                ? 0f
                : marks.Max(m => markFont.MeasureText(m.From.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)))
                  + tickGap + tickLen + labelGap + 1f;

            var inset = tm.FrameWidth;
            var body = new SKRect(rect.Left + inset, rect.Top + inset, rect.Right - inset - gutter, rect.Bottom - inset);
            var bodyR = MathF.Min(tm.CornerRadius, MathF.Max(0f, body.Width / 2f));
            var t = range <= 0 ? 0f : Math.Clamp((box.TankValue - box.TankMin) / range, 0f, 1f);

            var fillColor = TankFillColor(box);
            var levelTop = body.Bottom - t * body.Height;

            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(body, bodyR, bodyR), SKClipOperation.Intersect, antialias: true);

            // 빈 용기 — 은은한 세로 그라데이션(유리 느낌)
            using (var empty = new SKPaint { IsAntialias = true })
            {
                empty.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(body.Left, body.Top), new SKPoint(body.Left, body.Bottom),
                    new[]
                    {
                        ThemeColor("tank-empty-top", new SKColor(0xf5, 0xf8, 0xfc)),
                        ThemeColor("tank-empty-bottom", new SKColor(0xe6, 0xec, 0xf3))
                    },
                    null, SKShaderTileMode.Clamp);
                canvas.DrawRect(body, empty);
            }

            // 액체 — 밝은 윗면→짙은 바닥 그라데이션 + 액면 하이라이트
            if (t > 0f)
            {
                using (var fill = new SKPaint { IsAntialias = true })
                {
                    fill.Shader = SKShader.CreateLinearGradient(
                        new SKPoint(body.Left, levelTop), new SKPoint(body.Left, body.Bottom),
                        new[] { Lighten(fillColor, 0.22f), fillColor },
                        null, SKShaderTileMode.Clamp);
                    canvas.DrawRect(new SKRect(body.Left, levelTop, body.Right, body.Bottom), fill);
                }

                using (var surface = new SKPaint
                       {
                           IsAntialias = true,
                           Style = SKPaintStyle.Stroke,
                           StrokeWidth = 2.5f * tm.Scale,
                           Color = Lighten(fillColor, 0.5f)
                       })
                {
                    canvas.DrawLine(body.Left, levelTop, body.Right, levelTop, surface);
                }
            }

            // 원통 음영 — 가장자리 어둡게, 왼쪽 1/3 하이라이트 (FsCylinderTank 참고)
            using (var shade = new SKPaint { IsAntialias = true })
            {
                shade.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(body.Left, body.Top), new SKPoint(body.Right, body.Top),
                    new[]
                    {
                        new SKColor(0, 0, 0, 0x38), new SKColor(0xff, 0xff, 0xff, 0x1f),
                        new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 0x30)
                    },
                    new[] { 0f, 0.3f, 0.55f, 1f }, SKShaderTileMode.Clamp);
                canvas.DrawRect(body, shade);
            }

            canvas.Restore();

            // 테두리 — 얇고 부드럽게
            using (var frame = new SKPaint
                   {
                       IsAntialias = true,
                       Style = SKPaintStyle.Stroke,
                       StrokeWidth = tm.FrameWidth,
                       Color = ThemeColor("tank-frame", new SKColor(0xcb, 0xd5, 0xe1))
                   })
            {
                canvas.DrawRoundRect(body, bodyR, bodyR, frame);
            }

            // 임계 경계 — 용기 바깥 우측에만 짧은 눈금 + 경계값 숫자(zone 색). 벽·액체와 겹치지 않게.
            if (marks.Count > 0)
            {
                using var tick = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = tm.FrameWidth,
                    StrokeCap = SKStrokeCap.Butt
                };
                using var markText = new SKPaint { IsAntialias = true };
                foreach (var (from, color) in marks)
                {
                    var zy = body.Bottom - Math.Clamp((from - box.TankMin) / range, 0f, 1f) * body.Height;
                    tick.Color = color.ToSK();
                    var tx0 = body.Right + tickGap;
                    canvas.DrawLine(tx0, zy, tx0 + tickLen, zy, tick);

                    markText.Color = color.ToSK();
                    var markLabel = from.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                    var mb = zy - (markFont.Metrics.Ascent + markFont.Metrics.Descent) / 2f;
                    canvas.DrawText(markLabel, tx0 + tickLen + labelGap, mb, SKTextAlign.Left, markFont, markText);
                }
            }

            // 값 칩 — 반투명 배경 + 대비 텍스트(채움색과 무관하게 항상 가독, 색은 테마 토큰)
            var label = FormatValue(box.TankValue, box.ValueFormat);
            if (!string.IsNullOrEmpty(box.TankUnit))
                label += " " + box.TankUnit;

            // 칩 글씨는 탱크 크기에 비례, 몸통 폭을 넘으면 폭에 맞춰 줄인다(최소 9)
            var chipPad = 16f * tm.Scale;
            var chipSize = tm.ChipFont;
            using (var probe = TextFont.Create(label, chipSize, bold: true))
            {
                var need = probe.MeasureText(label) + chipPad;
                var avail = body.Width - 4f * tm.Scale;
                if (need > avail && need > 0)
                    chipSize = MathF.Max(9f, chipSize * avail / need);
            }

            using (var font = TextFont.Create(label, chipSize, bold: true))
            {
                var tw = font.MeasureText(label);
                var chipW = tw + chipPad * chipSize / tm.ChipFont;
                var chipH = font.Spacing + 6f * chipSize / 15f;
                var chip = new SKRect(
                    body.MidX - chipW / 2f, body.MidY - chipH / 2f,
                    body.MidX + chipW / 2f, body.MidY + chipH / 2f);
                using (var chipBg = new SKPaint
                       {
                           IsAntialias = true,
                           Color = ThemeColor("tank-chip-bg", new SKColor(0xff, 0xff, 0xff, 0xdd))
                       })
                    canvas.DrawRoundRect(chip, chipH / 2f, chipH / 2f, chipBg);

                using var tp = new SKPaint
                {
                    IsAntialias = true,
                    Color = ThemeColor("tank-chip-fg", new SKColor(0x0f, 0x17, 0x2a))
                };
                var baseline = body.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2f;
                canvas.DrawText(label, body.MidX, baseline, SKTextAlign.Center, font, tp);
            }
        }

        if (box.Process is not null)
            PaintProcess(canvas, box, timestampMs);

        if (box.Equipment is not null)
            PaintEquipment(canvas, box, st, timestampMs);

        if (box.Instrument is not null)
            PaintInstrument(canvas, box, st);

        var clip = st.Overflow is Overflow.Hidden or Overflow.Scroll && box.Children.Count > 0;
        if (clip)
        {
            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(rect, r, r), SKClipOperation.Intersect, antialias: true);
            if (box.IsScroller)
                canvas.Translate(-box.ScrollX, -box.ScrollY);
        }

        for (var i = 0; i < box.Children.Count; i++)
            PaintAt(canvas, box.Children[i], vm, state, timestampMs, unchecked(key * 31 + i + 1));

        if (clip)
            canvas.Restore();

        if (box.IsScroller)
        {
            // 둥근 모서리 상자면 스크롤바 트랙도 모서리 안으로(그리드 오른쪽 아래 등에서 삐져나오지 않게)
            canvas.Save();
            if (r > 0)
                canvas.ClipRoundRect(new SKRoundRect(rect, r, r), SKClipOperation.Intersect, antialias: true);
            PaintScrollBars(canvas, box);
            canvas.Restore();
        }
    }

    // DvSegment(fill 아님) — 상자가 grow·stretch 로 넓어져도 배경·테두리·그림자는 항목까지만(뒤에 빈 배경이 남지 않게)
    internal static SKRect SegmentFitRect(LayoutBox box, ComputedStyle st)
    {
        var rect = box.Rect;
        if (box.Children.Count == 0)
            return rect;
        var right = box.Children.Max(c => c.Rect.Right) + st.Padding.Right;
        return right < rect.Right ? new SKRect(rect.Left, rect.Top, right, rect.Bottom) : rect;
    }

    private static void DrawBorder(SKCanvas canvas, SKRect rect, ComputedStyle st)
    {
        if (st.BorderWidth <= 0 || st.BorderColor is not { } bc)
            return;
        using var p = new SKPaint
        {
            IsAntialias = true,
            Color = bc.ToSK(),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = st.BorderWidth,
            PathEffect = st.BorderDashed
                ? SKPathEffect.CreateDash(new[] { 4f + st.BorderWidth * 2, 3f + st.BorderWidth }, 0)
                : null
        };
        canvas.DrawRoundRect(rect, st.BorderRadius, st.BorderRadius, p);
    }

    // 넘치는 축에만 — 옅은 트랙 + 둥근 손잡이(뷰포트 안쪽 가장자리에 겹침)
    private void PaintScrollBars(SKCanvas canvas, LayoutBox box)
    {
        using var track = new SKPaint { IsAntialias = true, Color = ThemeColor("scrollbar-track", new SKColor(0x0f, 0x17, 0x2a, 0x0f)) };
        using var thumb = new SKPaint { IsAntialias = true, Color = ThemeColor("scrollbar-thumb", new SKColor(0x64, 0x74, 0x8b, 0x80)) };
        var rr = Interaction.ScrollBars.Thickness / 2f;

        if (Interaction.ScrollBars.VerticalTrack(box) is { } vt)
            canvas.DrawRoundRect(vt, rr, rr, track);
        if (Interaction.ScrollBars.VerticalThumb(box) is { } vth)
            canvas.DrawRoundRect(vth, rr, rr, thumb);
        if (Interaction.ScrollBars.HorizontalTrack(box) is { } ht)
            canvas.DrawRoundRect(ht, rr, rr, track);
        if (Interaction.ScrollBars.HorizontalThumb(box) is { } hth)
            canvas.DrawRoundRect(hth, rr, rr, thumb);
    }

    private SKColor ThemeColor(string token, SKColor fallback)
    {
        if (_theme.TryGet(token, out var raw) &&
            DvColor.TryParse(raw, out var color))
            return color.ToSK();

        return fallback;
    }

    private static ComputedStyle Effective(LayoutBox box, InteractionState? state)
    {
        // 비활성 상자는 포인터 반응(press·hover)을 그리지 않는다 — disabled-style 이 없으면 기본 스타일
        if (box.Disabled)
            return box.StyleDisabled ?? box.Style;

        if (state is not null && ReferenceEquals(state.Press, box) && box.StylePress is { } p)
            return p;

        if (state is not null && ReferenceEquals(state.Hover, box) && box.StyleHover is { } h)
            return h;

        return box.Style;
    }

    private static SKShader MakeShader(LinearGradient g, SKRect rect)
    {
        var rad = (g.AngleDeg - 90) * MathF.PI / 180f;
        var cx = rect.MidX;
        var cy = rect.MidY;
        var hx = MathF.Cos(rad) * rect.Width / 2;
        var hy = MathF.Sin(rad) * rect.Height / 2;
        var p0 = new SKPoint(cx - hx, cy - hy);
        var p1 = new SKPoint(cx + hx, cy + hy);
        var colors = g.Stops.Select(c => c.ToSK()).ToArray();
        return SKShader.CreateLinearGradient(p0, p1, colors, null, SKShaderTileMode.Clamp);
    }

    private static void DrawGaugeZones(SKCanvas canvas, LayoutBox box, SKRect oval, float startAngle, float sweep, float stroke)
    {
        if (box.GaugeZones.Count == 0)
            return;

        var range = box.GaugeMax - box.GaugeMin;
        if (range <= 0)
            return;

        foreach (var z in box.GaugeZones)
        {
            var z0 = Math.Clamp((z.From - box.GaugeMin) / range, 0f, 1f);
            var z1 = Math.Clamp((z.To - box.GaugeMin) / range, 0f, 1f);
            using var zonePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = stroke,
                StrokeCap = SKStrokeCap.Butt,
                Color = z.Color.ToSK()
            };
            using var path = new SKPath();
            path.AddArc(oval, startAngle + z0 * sweep, (z1 - z0) * sweep);
            canvas.DrawPath(path, zonePaint);
        }
    }

    private static SKColor? ZoneColorAt(LayoutBox box)
    {
        foreach (var z in box.GaugeZones)
            if (box.GaugeValue >= z.From && box.GaugeValue <= z.To)
                return z.Color.ToSK();

        return null;
    }

    private static SKColor TankFillColor(LayoutBox box)
    {
        foreach (var z in box.TankZones)
            if (box.TankValue >= z.From && box.TankValue <= z.To)
                return z.Color.ToSK();

        return new SKColor(0x3a, 0x7b, 0xd5);
    }

    private static SKColor Lighten(SKColor c, float f) => new(
        (byte)(c.Red + (255 - c.Red) * f),
        (byte)(c.Green + (255 - c.Green) * f),
        (byte)(c.Blue + (255 - c.Blue) * f),
        c.Alpha);
}
