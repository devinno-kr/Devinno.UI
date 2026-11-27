using System.Globalization;

namespace Devinno.UI.Styling;

/// <summary>
/// Parses a CSS-like declaration list string (<c>key: value; key: value</c>) into a <see cref="ComputedStyle"/>.
/// </summary>
/// <remarks>
/// <para>This is an internal engine stage. App code usually does not call it directly; it uses <c>style</c>, <c>class</c> and <c>&lt;DvStyle&gt;</c> in markup,
/// and parsing is handled by <see cref="DvSurface"/>/<see cref="DvRenderer"/>. Supported keys and defaults are described on each property of <see cref="ComputedStyle"/>.</para>
/// <para>Rules: declarations are split at <c>;</c> outside quotes and parentheses (so <c>url('a;b.png')</c> stays whole) and the text before the first <c>:</c> is the key (keys are case-insensitive; value case sensitivity depends on the key).
/// For duplicate keys, the later declaration wins. Unsupported keys are ignored without a warning; invalid values are ignored or set to the default/0, depending on the key.
/// Only <c>content-align</c> logs a one-time warning (<see cref="System.Diagnostics.Trace"/>) for an invalid value.</para>
/// <para>Theme tokens (<c>token(...)</c>) and bindings (<c>{Prop}</c>) are not resolved here. They must already be substituted (<see cref="ThemeResolver.ResolveVars"/>).</para>
/// <para>Tools that change style text (a screen editor moving an element changes only <c>left</c>/<c>top</c>) use <see cref="GetDeclaration"/>, <see cref="SetDeclaration"/> and
/// <see cref="RemoveDeclaration"/>, which split declarations by the same rules and leave the rest of the text as written.</para>
/// <para>Threading: there is no shared state, so it can be called from multiple threads (only the warning de-duplication set is protected by a lock).</para>
/// </remarks>
/// <example>
/// <code>
/// var st = StyleParser.Parse("background:#1e293b; padding: 8 12; border: 1 dashed #475569; font-weight:bold");
/// // st.Background = #1e293b, st.Padding = (8,12,8,12), st.BorderWidth = 1, st.BorderStyle = BorderLine.Dashed, st.Bold = true
/// </code>
/// </example>
public static class StyleParser
{
    /// <summary>Parses a declaration list string into a new <see cref="ComputedStyle"/>.</summary>
    /// <param name="style">The declaration list (e.g. <c>color:#fff; padding:8</c>). If <see langword="null"/> or whitespace, returns a style with all default values.</param>
    /// <returns>A newly created <see cref="ComputedStyle"/>. Does not throw.</returns>
    public static ComputedStyle Parse(string? style) => ParseInto(new ComputedStyle(), style);

    // 주어진 스타일 위에 선언을 덮는다 — 쓸 수 없는 값은 그 키를 건드리지 않는다(::part() 가 쓸 수 있는 값만 고를 때 씀)
    internal static ComputedStyle ParseInto(ComputedStyle s, string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
            return s;

        foreach (var (start, end) in Declarations(style))
        {
            var decl = style[start..end];
            var idx = decl.IndexOf(':');
            if (idx <= 0)
                continue;

            var key = decl[..idx].Trim().ToLowerInvariant();
            var val = decl[(idx + 1)..].Trim();
            Apply(s, key, val);
        }

        // offset 은 natural 에서만 — 다른 맞춤과 함께면 쓰지 않는다(선언 순서와 무관하게 다 읽은 뒤 판단)
        if (s.BackgroundOffset is not null && s.BackgroundSize != BackgroundFit.Natural)
            Interaction.Warnings.Warn($"Devinno.UI: background-image-offset is used only with background-size: natural; ignored (style: {style.Trim()})");

        return s;
    }

    /// <summary>
    /// Returns the value of one declaration in a declaration list: the value of the last declaration with that key (the one that applies), trimmed.
    /// </summary>
    /// <param name="style">A declaration list such as the text of a <c>style</c> attribute; <see langword="null"/> or empty is allowed.</param>
    /// <param name="key">The key, compared ignoring case (e.g. <c>left</c>).</param>
    /// <returns>The value as written, or <see langword="null"/> if no declaration has the key.</returns>
    /// <remarks>Declarations are split exactly as <see cref="Parse"/> splits them. The value is the raw text: tokens and bindings are not resolved.</remarks>
    public static string? GetDeclaration(string? style, string key)
    {
        if (string.IsNullOrEmpty(style))
            return null;
        return Find(style, key) is { } d ? style[d.ValueStart..d.ValueEnd] : null;
    }

    /// <summary>
    /// Sets one declaration in a declaration list and returns the new list. If the key is there, only the characters of its value change
    /// (of the last declaration with that key, the one that applies); otherwise <c>key:value</c> is appended. The order, spacing and other declarations stay as written.
    /// </summary>
    /// <param name="style">A declaration list such as the text of a <c>style</c> attribute; <see langword="null"/> or empty is allowed.</param>
    /// <param name="key">The key (e.g. <c>left</c>), compared ignoring case. An appended key is written as given.</param>
    /// <param name="value">The new value (e.g. <c>120</c>).</param>
    /// <returns>The new declaration list.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or contains <c>:</c> or <c>;</c>, or <paramref name="value"/> contains a <c>;</c> outside quotes and parentheses (it would split into two declarations).</exception>
    /// <example>
    /// <code>
    /// StyleParser.SetDeclaration("position:absolute; left:10; color:#fff", "left", "120"); // "position:absolute; left:120; color:#fff"
    /// StyleParser.SetDeclaration("color:#fff", "top", "8");                                // "color:#fff; top:8"
    /// </code>
    /// </example>
    public static string SetDeclaration(string? style, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        key = CheckKey(key);
        value = value.Trim();
        if (Cuts(value).Count > 0)
            throw new ArgumentException($"the value '{value}' contains a ';' that would split the declaration", nameof(value));

        style ??= "";
        if (Find(style, key) is { } d)
            return string.Concat(style.AsSpan(0, d.ValueStart), value, style.AsSpan(d.ValueEnd));

        var t = style.TrimEnd();
        if (t.EndsWith(';'))
            t = t[..^1].TrimEnd();
        return t.Length == 0 ? $"{key}:{value}" : $"{t}; {key}:{value}";
    }

    /// <summary>
    /// Removes every declaration with the key from a declaration list, together with its <c>;</c> separator, and returns the new list. The rest stays as written.
    /// </summary>
    /// <param name="style">A declaration list such as the text of a <c>style</c> attribute; <see langword="null"/> or empty is allowed.</param>
    /// <param name="key">The key, compared ignoring case.</param>
    /// <returns>The new declaration list (empty if nothing is left; <paramref name="style"/> unchanged if no declaration has the key).</returns>
    public static string RemoveDeclaration(string? style, string key)
    {
        if (string.IsNullOrEmpty(style))
            return "";
        var removed = false;
        while (Find(style, key) is { } d)
        {
            removed = true;
            var from = d.Start;
            while (from < d.End && char.IsWhiteSpace(style[from]))
                from++;
            var to = d.End;
            if (to < style.Length)
            {
                // 뒤에 ; 가 있다 — 그 ; 와 다음 선언 앞 공백까지
                to++;
                while (to < style.Length && char.IsWhiteSpace(style[to]))
                    to++;
            }
            else
            {
                // 마지막 선언 — 앞의 공백과 ; 까지
                while (from > 0 && char.IsWhiteSpace(style[from - 1]))
                    from--;
                if (from > 0 && style[from - 1] == ';')
                    from--;
            }

            style = string.Concat(style.AsSpan(0, from), style.AsSpan(to));
        }

        return removed ? style.Trim() : style;
    }

    private static string CheckKey(string key)
    {
        var k = key?.Trim() ?? "";
        if (k.Length == 0 || k.Contains(':') || k.Contains(';'))
            throw new ArgumentException($"'{key}' is not a style key", nameof(key));
        return k;
    }

    // 키가 같은 마지막 선언 — Start..End = 선언(앞뒤 공백 포함, ; 제외), Value = 값(앞뒤 공백 뺀 범위)
    private static (int Start, int End, int ValueStart, int ValueEnd)? Find(string style, string key)
    {
        var k = key.Trim();
        (int, int, int, int)? found = null;
        foreach (var (start, end) in Declarations(style))
        {
            var colon = style.IndexOf(':', start, end - start);
            if (colon < 0 || !style.AsSpan(start, colon - start).Trim().Equals(k, StringComparison.OrdinalIgnoreCase))
                continue;
            var vs = colon + 1;
            var ve = end;
            while (vs < ve && char.IsWhiteSpace(style[vs]))
                vs++;
            while (ve > vs && char.IsWhiteSpace(style[ve - 1]))
                ve--;
            found = (start, end, vs, ve);
        }

        return found;
    }

    // 선언 범위들 — ';' 로 나누되 따옴표·괄호 안의 ';' 는 나누지 않는다. 따옴표·괄호가 닫히지 않으면 단순히 ';' 로 나눈다(뒤 선언을 잃지 않게).
    // 빈 선언(공백뿐)은 건너뛴다.
    internal static IEnumerable<(int Start, int End)> Declarations(string style)
    {
        var from = 0;
        foreach (var cut in Cuts(style).Append(style.Length))
        {
            if (!string.IsNullOrWhiteSpace(style[from..cut]))
                yield return (from, cut);
            from = cut + 1;
        }
    }

    // 선언을 나누는 ';' 위치들
    private static List<int> Cuts(string style)
    {
        var cuts = new List<int>();
        char quote = '\0';
        var depth = 0;
        for (var i = 0; i < style.Length; i++)
        {
            var c = style[i];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
            }
            else if (c is '"' or '\'')
                quote = c;
            else if (c == '(')
                depth++;
            else if (c == ')' && depth > 0)
                depth--;
            else if (c == ';' && depth == 0)
                cuts.Add(i);
        }

        if (quote != '\0' || depth != 0)
        {
            cuts.Clear();
            for (var i = 0; i < style.Length; i++)
                if (style[i] == ';')
                    cuts.Add(i);
        }

        return cuts;
    }

    private static bool TryNumber(string v, out float f) =>
        float.TryParse(v.Replace("px", "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f) && float.IsFinite(f);

    // 케밥 9값만(대소문자 구분) — top-left … bottom-right
    private static bool TryContentAlign(string v, out ContentVAlign vertical, out TextAlign horizontal)
    {
        vertical = ContentVAlign.Top;
        horizontal = TextAlign.Left;
        var dash = v.IndexOf('-');
        if (dash <= 0)
            return false;

        ContentVAlign? vv = v[..dash] switch { "top" => ContentVAlign.Top, "middle" => ContentVAlign.Middle, "bottom" => ContentVAlign.Bottom, _ => null };
        TextAlign? hh = v[(dash + 1)..] switch { "left" => TextAlign.Left, "center" => TextAlign.Center, "right" => TextAlign.Right, _ => null };
        if (vv is null || hh is null)
            return false;
        (vertical, horizontal) = (vv.Value, hh.Value);
        return true;
    }

    private static void Apply(ComputedStyle s, string key, string v)
    {
        switch (key)
        {
            case "background":
                // 뒤 선언이 앞 선언을 대체(클래스 그라디언트를 inline 단색으로 덮을 수 있게). CSS 처럼 묶음 표기는 그림까지 바꾼다
                if (v.StartsWith("linear-gradient", StringComparison.OrdinalIgnoreCase))
                {
                    if (LinearGradient.TryParse(v) is { } g)
                    {
                        s.Gradient = g;
                        s.Background = null;
                        s.BackgroundImage = null;
                    }
                }
                else if (TryUrl(v, out var url))
                {
                    s.BackgroundImage = url;
                    s.Background = null;
                    s.Gradient = null;
                }
                else if (DvColor.TryParse(v, out var bg))
                {
                    s.Background = bg;
                    s.Gradient = null;
                    s.BackgroundImage = null;
                }
                break;
            case "background-image":
                if (v.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
                    s.BackgroundImage = null;
                else if (TryUrl(v, out var image))
                    s.BackgroundImage = image;
                break;
            case "background-size":
                // CSS 의 늘이기(100% 100%)도 fill 로. 그 밖 값(auto·길이)은 무시
                var size = v.Trim().ToLowerInvariant();
                if (size is "cover")
                    s.BackgroundSize = BackgroundFit.Cover;
                else if (size is "contain")
                    s.BackgroundSize = BackgroundFit.Contain;
                else if (size is "fill" || System.Text.RegularExpressions.Regex.IsMatch(size, @"^100%\s+100%$"))
                    s.BackgroundSize = BackgroundFit.Fill;
                else if (size is "natural")
                    s.BackgroundSize = BackgroundFit.Natural;
                break;
            case "background-image-offset":
                // X Y(px) — 값 하나면 둘 다. 숫자가 아니면 무시
                var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length is 1 or 2 && TryNumber(parts[0], out var ox) && TryNumber(parts[^1], out var oy))
                    s.BackgroundOffset = (ox, oy);
                break;
            case "background-color":
                // background 의 단색 별칭 — 그라디언트는 받지 않음. 선언 순서상 뒤가 이김
                if (DvColor.TryParse(v, out var bgc))
                {
                    s.Background = bgc;
                    s.Gradient = null;
                }
                break;
            case "color":
                if (DvColor.TryParse(v, out var c))
                    s.Color = c;
                break;
            case "opacity":
                s.Opacity = F(v, 1f);
                break;
            case "border-radius":
                s.BorderRadius = F(v);
                break;
            case "padding":
                if (Thickness.TryParse(v, out var p))
                    s.Padding = p;
                break;
            case "margin":
                if (Thickness.TryParse(v, out var m))
                    s.Margin = m;
                break;
            // 한쪽만 — 앞서 준 padding 의 그쪽 값만 바꾼다(CSS 와 같이 뒤에 온 선언이 이김)
            case "padding-top":
            case "padding-right":
            case "padding-bottom":
            case "padding-left":
                if (Thickness.TryParse(v, out var ps) && ps.Top == ps.Bottom && ps.Left == ps.Right && ps.Top == ps.Left)
                    s.Padding = s.Padding.WithSide(key[8..], ps.Top);
                break;
            case "font-size":
                s.FontSize = F(v, s.FontSize); // 쓸 수 없는 값 = 그대로(새 스타일이면 기본 14)
                break;
            case "font-family":
                s.FontFamily = v.Trim();
                break;
            case "font-variant-numeric":
                s.TabularNums = v.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("tabular-nums");
                break;
            case "font-weight":
                if (v.Equals("bold", StringComparison.OrdinalIgnoreCase))
                    s.FontWeight = 700;
                else if (v.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    s.FontWeight = 400;
                else if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) && float.IsFinite(w))
                    s.FontWeight = (int)Math.Round(Math.Clamp(w, 100f, 900f) / 100f, MidpointRounding.AwayFromZero) * 100;
                break;
            case "text-align":
                s.TextAlign = v switch
                {
                    "center" => TextAlign.Center,
                    "right" => TextAlign.Right,
                    _ => TextAlign.Left
                };
                break;
            case "white-space":
                // normal | nowrap (그 밖의 값은 normal, text-align 처럼 대소문자 구분)
                s.NoWrap = v == "nowrap";
                break;
            case "content-align":
                if (TryContentAlign(v.Trim(), out var cv, out var ch))
                    (s.ContentVertical, s.TextAlign) = (cv, ch);
                else
                    Interaction.Warnings.Warn($"Devinno.UI: content-align '{v}' ignored; expected top|middle|bottom-left|center|right (e.g. middle-center)");
                break;
            case "gap":
                s.Gap = F(v);
                break;
            case "display":
                s.IsFlex = v.Equals("flex", StringComparison.OrdinalIgnoreCase);
                s.IsGrid = v.Equals("grid", StringComparison.OrdinalIgnoreCase);
                break;
            case "grid-template-columns":
                s.GridColumns = Tracks(v);
                break;
            case "grid-template-rows":
                s.GridRows = Tracks(v);
                break;
            case "grid-row":
            case "grid-column":
                if (TryGridLine(v, out var line))
                {
                    if (key == "grid-row")
                        s.GridRow = line;
                    else
                        s.GridColumn = line;
                }
                else
                {
                    Interaction.Warnings.Warn($"Devinno.UI: {key} '{v.Trim()}' ignored; expected N, span K, N / span K or N / M (lines from 1 to {MaxGridLine}, K from 1)");
                }
                break;
            case "aspect-ratio":
                s.AspectRatio = Ratio(v);
                break;
            case "flex-direction":
                s.FlexDirection = v.Equals("column", StringComparison.OrdinalIgnoreCase)
                    ? FlexDirection.Column
                    : FlexDirection.Row;
                break;
            case "justify-content":
                s.Justify = v switch
                {
                    "center" => JustifyContent.Center,
                    "end" or "flex-end" => JustifyContent.End,
                    "space-between" => JustifyContent.SpaceBetween,
                    _ => JustifyContent.Start
                };
                s.JustifyItems = v switch
                {
                    "start" or "flex-start" => AlignItems.Start,
                    "center" => AlignItems.Center,
                    "end" or "flex-end" => AlignItems.End,
                    _ => AlignItems.Stretch
                };
                break;
            case "align-items":
                s.Align = v switch
                {
                    "center" => AlignItems.Center,
                    "end" or "flex-end" => AlignItems.End,
                    "stretch" => AlignItems.Stretch,
                    _ => AlignItems.Start
                };
                break;
            case "align-self":
                s.AlignSelf = v switch
                {
                    "center" => AlignItems.Center,
                    "end" or "flex-end" => AlignItems.End,
                    "stretch" => AlignItems.Stretch,
                    "start" or "flex-start" => AlignItems.Start,
                    _ => null
                };
                break;
            case "overflow":
                s.Overflow = v.Trim().ToLowerInvariant() switch
                {
                    "hidden" => Overflow.Hidden,
                    "scroll" => Overflow.Scroll,
                    _ => Overflow.Visible
                };
                break;
            case "width":
                s.Width = F(v);
                break;
            case "height":
                s.Height = F(v);
                break;
            case "min-width":
                s.MinWidth = F(v);
                break;
            case "max-width":
                s.MaxWidth = F(v);
                break;
            case "min-height":
                s.MinHeight = F(v);
                break;
            case "max-height":
                s.MaxHeight = F(v);
                break;
            case "flex-grow":
                s.FlexGrow = F(v);
                break;
            case "flex-shrink":
                s.FlexShrink = F(v);
                break;
            case "flex":
                ParseFlex(s, v);
                break;
            case "flex-wrap":
                s.Wrap = v.Equals("wrap", StringComparison.OrdinalIgnoreCase);
                break;
            case "align-content":
                s.AlignContent = v switch
                {
                    "center" => AlignContent.Center,
                    "end" or "flex-end" => AlignContent.End,
                    "space-between" => AlignContent.SpaceBetween,
                    "stretch" => AlignContent.Stretch,
                    _ => AlignContent.Start
                };
                break;
            case "position":
                s.Position = v switch
                {
                    "absolute" => Position.Absolute,
                    "relative" => Position.Relative,
                    _ => Position.Static
                };
                break;
            case "left":
                s.Left = F(v);
                break;
            case "top":
                s.Top = F(v);
                break;
            case "right":
                s.Right = F(v);
                break;
            case "bottom":
                s.Bottom = F(v);
                break;
            case "border":
                ParseBorder(s, v);
                break;
            case "border-style":
                s.BorderStyle = LineWord(v.Trim()) ?? BorderLine.Solid;
                break;
            case "border-width":
                s.BorderWidth = F(v);
                break;
            case "border-color":
                if (DvColor.TryParse(v.Trim(), out var bcol))
                    s.BorderColor = bcol;
                break;
            case "box-shadow":
                ParseShadow(s, v);
                break;
            case "transform":
                if (Transform2D.Parse(v) is { } xf)
                    s.Transform = xf;
                else
                    Interaction.Warnings.Warn($"Devinno.UI: could not parse transform '{v}' (only translate, translateX, translateY, scale, scaleX, scaleY); ignored");
                break;
            case "transition":
            {
                var bad = new List<string>();
                s.Transitions = TransitionSpec.Parse(v, bad);
                foreach (var x in bad)
                    Interaction.Warnings.Warn($"Devinno.UI: could not parse transition '{x}' (expected: property duration [easing] [delay]); skipped");
                break;
            }
        }
    }

    private static void ParseBorder(ComputedStyle s, string v)
    {
        var p = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length >= 1)
            s.BorderWidth = F(p[0]);

        var last = p[^1];
        if (DvColor.TryParse(last, out var bc))
            s.BorderColor = bc;
        if (p.Select(LineWord).FirstOrDefault(w => w is not null) is { } line)
            s.BorderStyle = line;
    }

    private static BorderLine? LineWord(string w) => w switch
    {
        "solid" => BorderLine.Solid,
        "dashed" => BorderLine.Dashed,
        "dotted" => BorderLine.Dotted,
        _ => null,
    };

    // 트랙 목록: "1fr 2fr 120" 처럼 따옴표로 감싸도 된다
    private static string[] Tracks(string v)
        => v.Trim().Trim('"', '\'').Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Largest grid line number or span accepted by <c>grid-row</c>/<c>grid-column</c> (a larger one is ignored with a warning).</summary>
    internal const int MaxGridLine = 1000;

    // grid-row·grid-column 값 — auto | N | span K | (N|auto) / span K | N / M (CSS 와 같이 1부터, M 이 N 보다 앞이면 바꿔 읽고 같으면 1칸).
    // 음수(끝에서 세기)·이름 붙은 줄·span K / N 은 없음 = false
    internal static bool TryGridLine(string v, out (int? Start, int Span) line)
    {
        line = (null, 1);
        var parts = v.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length > 2 || parts.Any(p => p.Length == 0))
            return false;

        int? start = null;
        var span = 1;
        if (parts[0].Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
        }
        else if (Span(parts[0]) is { } k0)
        {
            if (parts.Length > 1)
                return false; // span K / N 은 쓰지 않는다
            span = k0;
        }
        else if (Line(parts[0]) is { } n)
        {
            start = n;
        }
        else
        {
            return false;
        }

        if (parts.Length == 2)
        {
            if (Span(parts[1]) is { } k)
                span = k;
            else if (start is { } a && Line(parts[1]) is { } b)
                (start, span) = b == a ? (a, 1) : (Math.Min(a, b), Math.Abs(b - a));
            else if (!parts[1].Equals("auto", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        line = (start, span);
        return true;

        static int? Line(string t) => int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= MaxGridLine ? n : null;

        static int? Span(string t)
        {
            var w = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return w.Length == 2 && w[0].Equals("span", StringComparison.OrdinalIgnoreCase) ? Line(w[1]) : null;
        }
    }

    // "1", "1.5", "16/9" → 너비/높이. 0 이하·해석 불가면 없음
    private static float? Ratio(string v)
    {
        var p = v.Split('/', StringSplitOptions.TrimEntries);
        var w = F(p[0]);
        var h = p.Length > 1 ? F(p[1]) : 1f;
        return w > 0 && h > 0 ? w / h : null;
    }

    // url(경로) — 따옴표는 선택. 빈 url() 은 그림 없음(바인딩이 빈 문자열일 때)
    private static bool TryUrl(string v, out string? path)
    {
        path = null;
        var t = v.Trim();
        if (!t.StartsWith("url(", StringComparison.OrdinalIgnoreCase) || !t.EndsWith(')'))
            return false;
        var inner = t[4..^1].Trim();
        if (inner.Length >= 2 && (inner[0] == '"' || inner[0] == '\'') && inner[^1] == inner[0])
            inner = inner[1..^1];
        path = inner.Length > 0 ? inner : null;
        return true;
    }

    private static void ParseShadow(ComputedStyle s, string v)
    {
        if (v.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            s.Shadow = null;
            return;
        }

        var p = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 4)
            return;

        if (DvColor.TryParse(p[3], out var col))
            s.Shadow = new BoxShadow(F(p[0]), F(p[1]), F(p[2]), col);
    }

    private static void ParseFlex(ComputedStyle s, string v)
    {
        var p = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length == 0)
            return;

        if (p.Length == 1)
        {
            s.FlexGrow = F(p[0]);
            s.FlexShrink = 1f;
            return;
        }

        s.FlexGrow = F(p[0]);
        s.FlexShrink = F(p[1]);
    }

    private static float F(string v, float dflt = 0f)
    {
        return float.TryParse(v.Replace("px", "").Replace("dp", "").Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var f) && float.IsFinite(f) // NaN·무한대 = 쓸 수 없는 값
            ? f
            : dflt;
    }
}
