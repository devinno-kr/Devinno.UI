using System.Globalization;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Editing;

internal enum KbMode
{
    Hangul,
    English,
    Symbols,
    Numeric
}

/// <summary>Result of a key press. The surface applies it to the session.</summary>
internal enum KbResult
{
    None,
    TextChanged,
    Commit
}

/// <summary>
/// Built-in core virtual keyboard (for touch-oriented adapters). It slides up from the bottom of the screen over 200ms and shows the value being typed in a preview row at the top.
/// text/password = QWERTY (Korean/English toggle, Shift, symbols, 2-set Korean automaton); numeric = numeric keypad (integer, sign and decimal modes).
/// Time is the ms value passed by the caller (surface clock); no background thread or timer.
/// </summary>
internal sealed class SoftKeyboard
{
    public const int AnimMs = 200;
    private const float Pad = 8f;
    private const float Gap = 6f;
    private const float PreviewH = 44f;
    private const float TextMaxW = 900f;
    private const float PadMaxW = 420f;

    private const string EnRow1 = "qwertyuiop", EnRow2 = "asdfghjkl", EnRow3 = "zxcvbnm";
    private const string KoRow1 = "ㅂㅈㄷㄱㅅㅛㅕㅑㅐㅔ", KoRow1Shift = "ㅃㅉㄸㄲㅆㅛㅕㅑㅒㅖ", KoRow2 = "ㅁㄴㅇㄹㅎㅗㅓㅏㅣ", KoRow3 = "ㅋㅌㅊㅍㅠㅜㅡ";
    private const string SymRow1 = "1234567890", SymRow2 = "-/:;()&@\"", SymRow3 = ".,?!'%*";

    // 특수 키 id
    public const string Shift = "shift", Back = "back", Lang = "lang", Sym = "sym", Space = "space", Done = "done",
        Sign = "sign", Dot = "dot", Clear = "clear";

    public sealed record Key(string Id, string Label, SKRect Rect, bool Special, bool Enabled = true);

    private readonly HangulComposer _composer = new();
    private string _num = "";
    private bool _shift;
    private KbMode _textMode = KbMode.Hangul;
    private long _openedAt = long.MinValue / 2;
    private long _closedAt = long.MinValue / 2;
    private bool _open;

    public int SessionId { get; private set; }
    public KbMode Mode { get; private set; }
    public DvEditKind Kind { get; private set; }
    public DvNumericSpec? Numeric { get; private set; }
    public bool ShiftOn => _shift;
    public string Text => Mode == KbMode.Numeric ? _num : _composer.Text;

    public void Open(DvEditState e, long now)
    {
        if (!_open)
            _openedAt = now;
        _open = true;
        SessionId = e.SessionId;
        Kind = e.Kind;
        Numeric = e.Numeric;
        _shift = false;
        if (e.Kind == DvEditKind.Numeric)
        {
            Mode = KbMode.Numeric;
            _num = e.InitialText;
        }
        else
        {
            _textMode = e.Kind == DvEditKind.Password ? KbMode.English : KbMode.Hangul;
            Mode = _textMode;
            _composer.Reset(e.InitialText);
        }
    }

    public void Close(long now)
    {
        if (!_open)
            return;
        _open = false;
        _closedAt = now;
        SessionId = 0;
    }

    /// <summary>Visibility 0–1 (including while sliding up or down).</summary>
    public float Progress(long now)
    {
        if (_open)
            return Math.Clamp((now - _openedAt) / (float)AnimMs, 0f, 1f);
        return 1f - Math.Clamp((now - _closedAt) / (float)AnimMs, 0f, 1f);
    }

    public bool IsOpen => _open;
    public bool IsShown(long now) => Progress(now) > 0f;
    public bool IsAnimating(long now) => _open ? Progress(now) < 1f : Progress(now) > 0f;

    public static float PanelHeight(float h) => Math.Min(h, Math.Max(260f, h * 0.4f));

    /// <summary>Panel (background) area, raised from the bottom according to the progress.</summary>
    public SKRect Panel(float w, float h, long now)
    {
        var ph = PanelHeight(h);
        var top = h - ph * Progress(now);
        return new SKRect(0, top, w, top + ph);
    }

    /// <summary>Amount to shift the screen up so the edit target is not covered (progress applied).</summary>
    public float Pan(SKRect target, float w, float h, long now)
    {
        var full = h - PanelHeight(h);
        var need = Math.Max(0f, target.Bottom + 12f - full);
        need = Math.Min(need, Math.Max(0f, target.Top - 12f)); // 대상 위가 화면 밖으로 나가지 않게
        return need * Progress(now);
    }

    public SKRect Preview(float w, float h, long now)
    {
        var p = Panel(w, h, now);
        var area = Area(p);
        return new SKRect(area.Left, p.Top + Pad, area.Right, p.Top + Pad + PreviewH);
    }

    private SKRect Area(SKRect panel)
    {
        var maxW = Mode == KbMode.Numeric ? PadMaxW : TextMaxW;
        var aw = Math.Min(panel.Width - 2 * Pad, maxW);
        var left = panel.MidX - aw / 2f;
        return new SKRect(left, panel.Top, left + aw, panel.Bottom);
    }

    public List<Key> Keys(float w, float h, long now)
    {
        var p = Panel(w, h, now);
        var area = Area(p);
        var top = p.Top + Pad + PreviewH + Gap;
        var rowsH = p.Bottom - Pad - top;
        var keys = new List<Key>();
        if (Mode == KbMode.Numeric)
        {
            var rh = (rowsH - 3 * Gap) / 4f;
            var cw = (area.Width - 3 * Gap) / 4f;
            SKRect Cell(int r, int c, int span = 1) => SKRect.Create(area.Left + c * (cw + Gap), top + r * (rh + Gap),
                cw * span + Gap * (span - 1), rh);

            string[] digits = { "789", "456", "123" };
            for (var r = 0; r < 3; r++)
            for (var c = 0; c < 3; c++)
                keys.Add(new Key(digits[r][c].ToString(), digits[r][c].ToString(), Cell(r, c), false));
            keys.Add(new Key(Back, "⌫", Cell(0, 3), true));
            keys.Add(new Key(Clear, "C", Cell(1, 3), true));
            keys.Add(new Key(Sign, "±", Cell(2, 3), true, Numeric?.IsSigned ?? true));
            keys.Add(new Key("0", "0", Cell(3, 0, 2), false));
            keys.Add(new Key(Dot, ".", Cell(3, 2), false, !(Numeric?.IsInteger ?? false)));
            keys.Add(new Key(Done, "", Cell(3, 3), true));
            return keys;
        }

        var rowH = (rowsH - 3 * Gap) / 4f;
        var unit = (area.Width - 9 * Gap) / 10f;
        float RowTop(int r) => top + r * (rowH + Gap);

        void Row(int r, IEnumerable<(string Id, string Label, float Units, bool Special)> items, float indentUnits = 0)
        {
            var x = area.Left + indentUnits * (unit + Gap);
            foreach (var (id, label, units, special) in items)
            {
                var kw = units * unit + (units - 1) * Gap;
                keys.Add(new Key(id, label, SKRect.Create(x, RowTop(r), kw, rowH), special));
                x += kw + Gap;
            }
        }

        IEnumerable<(string, string, float, bool)> Chars(string s) =>
            s.Select(ch =>
            {
                var shown = Mode == KbMode.English && _shift ? char.ToUpperInvariant(ch) : ch;
                return (shown.ToString(), shown.ToString(), 1f, false);
            });

        var (r1, r2, r3) = Mode switch
        {
            KbMode.English => (EnRow1, EnRow2, EnRow3),
            KbMode.Symbols => (SymRow1, SymRow2, SymRow3),
            _ => (_shift ? KoRow1Shift : KoRow1, KoRow2, KoRow3)
        };

        Row(0, Chars(r1));
        Row(1, Chars(r2), 0.5f);
        var row3 = new List<(string, string, float, bool)>();
        row3.Add(Mode == KbMode.Symbols ? ("=", "=", 1.5f, false) : (Shift, "⇧", 1.5f, true));
        row3.AddRange(Chars(r3));
        row3.Add((Back, "⌫", 1.5f, true));
        Row(2, row3);
        Row(3, new[]
        {
            (Lang, "한/영", 1.5f, true),
            (Sym, Mode == KbMode.Symbols ? (_textMode == KbMode.Hangul ? "가" : "ABC") : "?123", 1.5f, true),
            (Space, "", 4f, false),
            (".", ".", 1f, false),
            (Done, "", 2f, true)
        });
        return keys;
    }

    public Key? Hit(SKPoint p, float w, float h, long now)
        => Keys(w, h, now).FirstOrDefault(k => k.Enabled && k.Rect.Contains(p.X, p.Y));

    /// <summary>One on-screen key.</summary>
    public KbResult Press(string id)
    {
        switch (id)
        {
            case Done:
                if (Mode != KbMode.Numeric)
                    _composer.Finish();
                return KbResult.Commit;
            case Shift:
                _shift = !_shift;
                return KbResult.None;
            case Lang:
                _composer.Finish(); // 조합 중 글자 확정(빠뜨리면 전환 뒤 조합이 이어진다)
                _textMode = Mode == KbMode.Hangul || (Mode == KbMode.Symbols && _textMode == KbMode.Hangul)
                    ? KbMode.English : KbMode.Hangul;
                Mode = _textMode;
                _shift = false;
                return KbResult.None;
            case Sym:
                _composer.Finish();
                Mode = Mode == KbMode.Symbols ? _textMode : KbMode.Symbols;
                _shift = false;
                return KbResult.None;
            case Back:
                Backspace();
                return KbResult.TextChanged;
            case Space:
                return Type(' ');
            case Clear:
                _num = "";
                return KbResult.TextChanged;
            case Sign:
                _num = _num.StartsWith('-') ? _num[1..] : "-" + _num;
                return KbResult.TextChanged;
            case Dot:
                return Type('.');
            default:
                return id.Length == 1 ? Type(id[0]) : KbResult.None;
        }
    }

    /// <summary>One character (shared by on-screen keys and the physical keyboard).</summary>
    public KbResult Type(char c)
    {
        if (Mode == KbMode.Numeric)
        {
            if (char.IsDigit(c) || (c == '.' && !(Numeric?.IsInteger ?? false) && !_num.Contains('.')))
            {
                _num += c;
                return KbResult.TextChanged;
            }

            if (c == '-' && (Numeric?.IsSigned ?? true))
                return Press(Sign);
            return KbResult.None;
        }

        _composer.Type(c);
        _shift = false; // Shift 는 한 글자만(대문자·쌍자음)
        return KbResult.TextChanged;
    }

    public void Backspace()
    {
        if (Mode == KbMode.Numeric)
        {
            if (_num.Length > 0)
                _num = _num[..^1];
        }
        else
        {
            _composer.Backspace();
        }
    }

    // ── 그리기
    public void Paint(SKCanvas canvas, DvTheme? theme, DvStrings? strings, DvEditState e, float w, float h, long now,
        string? pressed)
    {
        if (!IsShown(now))
            return;

        SKColor Tok(string name, SKColor fb)
            => (theme ?? DvTheme.Default).TryGet(name, out var raw) && DvColor.TryParse(raw, out var c) ? c.ToSK() : fb;

        var bg = Tok("kbd-bg", new SKColor(0xe2, 0xe8, 0xf0));
        var keyBg = Tok("kbd-key", SKColors.White);
        var keyFg = Tok("kbd-key-fg", new SKColor(0x0f, 0x17, 0x2a));
        var spBg = Tok("kbd-key-special", new SKColor(0xcb, 0xd5, 0xe1));
        var spFg = Tok("kbd-key-special-fg", new SKColor(0x0f, 0x17, 0x2a));
        var primary = Tok("primary", new SKColor(0x25, 0x63, 0xeb));
        var primaryFg = Tok("primary-fg", SKColors.White);
        var surface = Tok("surface", SKColors.White);
        var border = Tok("border-strong", new SKColor(0x94, 0xa3, 0xb8));
        var muted = Tok("text-muted", new SKColor(0x64, 0x74, 0x8b));
        var subtle = Tok("text-subtle", new SKColor(0x94, 0xa3, 0xb8));
        var danger = Tok("tone-danger", new SKColor(0xdc, 0x26, 0x26));

        var panel = Panel(w, h, now);
        using (var bp = new SKPaint { Color = bg })
            canvas.DrawRect(panel, bp);
        using (var lp = new SKPaint { Color = border.WithAlpha(0x80), StrokeWidth = 1 })
            canvas.DrawLine(panel.Left, panel.Top + 0.5f, panel.Right, panel.Top + 0.5f, lp);

        // 미리보기 줄 — 값 + 끝 캐럿, 오른쪽에 범위·단위 또는 거절 사유
        var pv = Preview(w, h, now);
        using (var pp = new SKPaint { IsAntialias = true, Color = surface })
            canvas.DrawRoundRect(pv, 8, 8, pp);
        using (var ps = new SKPaint { IsAntialias = true, Color = e.Error is null ? border : danger, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            canvas.DrawRoundRect(pv, 8, 8, ps);

        var info = e.Error ?? RangeInfo();
        var infoRight = pv.Right - 12f;
        float infoW = 0;
        if (!string.IsNullOrEmpty(info))
        {
            using var inf = TextFont.Create(info, 13f, bold: false);
            using var ip = new SKPaint { IsAntialias = true, Color = e.Error is null ? muted : danger };
            canvas.DrawText(info, infoRight, Baseline(pv, inf), SKTextAlign.Right, inf, ip);
            infoW = inf.MeasureText(info) + 16f;
        }

        var shown = Kind == DvEditKind.Password ? new string(InputDisplay.PasswordDot, Text.Length) : Text;
        var textArea = new SKRect(pv.Left + 12f, pv.Top, infoRight - infoW, pv.Bottom);
        canvas.Save();
        canvas.ClipRect(textArea);
        var placeholder = shown.Length == 0 ? e.Placeholder : null;
        var drawText = placeholder ?? shown;
        using (var tf = TextFont.Create(drawText.Length > 0 ? drawText : "가", 18f, bold: false))
        {
            var tw = shown.Length > 0 ? tf.MeasureText(shown) : 0f;
            var x = Math.Min(textArea.Left, textArea.Right - 4f - tw); // 넘치면 끝이 보이게
            using var tp = new SKPaint { IsAntialias = true, Color = placeholder is null ? keyFg : subtle };
            if (drawText.Length > 0)
                canvas.DrawText(drawText, x, Baseline(pv, tf), SKTextAlign.Left, tf, tp);
            using var cp = new SKPaint { Color = primary, StrokeWidth = 2 };
            var cx = x + tw + 1f;
            canvas.DrawLine(cx, pv.MidY - 11f, cx, pv.MidY + 11f, cp);
        }

        canvas.Restore();

        foreach (var k in Keys(w, h, now))
        {
            var isDone = k.Id == Done;
            var on = k.Id == Shift && _shift;
            var fill = isDone ? primary : k.Special || on ? spBg : keyBg;
            if (on)
                fill = primary.WithAlpha(0x40);
            if (k.Id == pressed)
                fill = Darken(fill);
            if (!k.Enabled)
                fill = fill.WithAlpha(0x60);

            using (var kp = new SKPaint { IsAntialias = true, Color = fill })
                canvas.DrawRoundRect(k.Rect, 7, 7, kp);

            var ink = isDone ? primaryFg : k.Special ? spFg : keyFg;
            var iconName = k.Id switch { Back => "fa-delete-left", Shift => "fa-up-long", _ => null };
            if (iconName is not null && IconFont.TryResolve(iconName, out var icon))
            {
                using var ipaint = new SKPaint { IsAntialias = true, Color = ink };
                IconFont.Draw(canvas, icon, new SKPoint(k.Rect.MidX, k.Rect.MidY), 20f, ipaint);
                continue;
            }

            var label = isDone ? DvStrings.Text(strings, "dv.kbd.done", "완료") : k.Label;
            if (label.Length == 0)
                continue;
            // 호환 자모(ㄱ~ㅣ)는 한글 폰트에서 음절보다 작게 디자인돼 있어 키우고, 아이콘 키(⇧ ⌫ ±)도 키운다
            var size = label.Length == 1 && label[0] is >= 'ㄱ' and <= 'ㆎ' ? 24f
                : label is "±" ? 22f
                : k.Special ? 15f : 19f;
            using var lf = TextFont.Create(label, size, bold: isDone);
            using var lt = new SKPaint { IsAntialias = true, Color = k.Enabled ? ink : ink.WithAlpha(0x60) };
            canvas.DrawText(label, k.Rect.MidX, Baseline(k.Rect, lf), SKTextAlign.Center, lf, lt);
        }
    }

    private string? RangeInfo()
    {
        if (Numeric is not { } n)
            return null;
        var lo = n.Min?.ToString(CultureInfo.InvariantCulture);
        var hi = n.Max?.ToString(CultureInfo.InvariantCulture);
        var range = lo is null && hi is null ? "" : $"{lo} ~ {hi}";
        return string.IsNullOrEmpty(n.Unit) ? range : $"{range} {n.Unit}".Trim();
    }

    private static SKColor Darken(SKColor c)
        => new((byte)(c.Red * 0.85f), (byte)(c.Green * 0.85f), (byte)(c.Blue * 0.85f), c.Alpha);

    private static float Baseline(SKRect r, SKFont f) => r.MidY - (f.Metrics.Ascent + f.Metrics.Descent) / 2f;
}
