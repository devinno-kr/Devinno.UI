using System.Globalization;

namespace Devinno.UI.Styling;

/// <summary>
/// Parses a CSS-like declaration list string (<c>key: value; key: value</c>) into a <see cref="ComputedStyle"/>.
/// </summary>
/// <remarks>
/// <para>This is an internal engine stage. App code usually does not call it directly; it uses <c>style</c>, <c>class</c> and <c>&lt;DvStyle&gt;</c> in markup,
/// and parsing is handled by <see cref="DvSurface"/>/<see cref="DvRenderer"/>. Supported keys and defaults are described on each property of <see cref="ComputedStyle"/>.</para>
/// <para>Rules: declarations are split at <c>;</c> and the text before the first <c>:</c> is the key (keys are case-insensitive; value case sensitivity depends on the key).
/// For duplicate keys, the later declaration wins. Unsupported keys are ignored without a warning; invalid values are ignored or set to the default/0, depending on the key.
/// Only <c>content-align</c> logs a one-time warning (<see cref="System.Diagnostics.Trace"/>) for an invalid value.</para>
/// <para>Theme tokens (<c>token(...)</c>) and bindings (<c>{Prop}</c>) are not resolved here. They must already be substituted (<see cref="ThemeResolver.ResolveVars"/>).</para>
/// <para>Threading: there is no shared state, so it can be called from multiple threads (only the warning de-duplication set is protected by a lock).</para>
/// </remarks>
/// <example>
/// <code>
/// var st = StyleParser.Parse("background:#1e293b; padding: 8 12; border: 1 dashed #475569; font-weight:bold");
/// // st.Background = #1e293b, st.Padding = (8,12,8,12), st.BorderWidth = 1, st.BorderDashed = true, st.Bold = true
/// </code>
/// </example>
public static class StyleParser
{
    /// <summary>Parses a declaration list string into a new <see cref="ComputedStyle"/>.</summary>
    /// <param name="style">The declaration list (e.g. <c>color:#fff; padding:8</c>). If <see langword="null"/> or whitespace, returns a style with all default values.</param>
    /// <returns>A newly created <see cref="ComputedStyle"/>. Does not throw.</returns>
    public static ComputedStyle Parse(string? style)
    {
        var s = new ComputedStyle();
        if (string.IsNullOrWhiteSpace(style))
            return s;

        foreach (var decl in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = decl.IndexOf(':');
            if (idx <= 0)
                continue;

            var key = decl[..idx].Trim().ToLowerInvariant();
            var val = decl[(idx + 1)..].Trim();
            Apply(s, key, val);
        }

        return s;
    }

    private static readonly HashSet<string> Warned = new();

    private static void WarnOnce(string msg)
    {
        lock (Warned)
        {
            if (Warned.Add(msg))
                System.Diagnostics.Trace.TraceWarning(msg);
        }
    }

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
                // 뒤 선언이 앞 선언을 대체(클래스 그라디언트를 inline 단색으로 덮을 수 있게)
                if (v.StartsWith("linear-gradient", StringComparison.OrdinalIgnoreCase))
                {
                    if (LinearGradient.TryParse(v) is { } g)
                    {
                        s.Gradient = g;
                        s.Background = null;
                    }
                }
                else if (DvColor.TryParse(v, out var bg))
                {
                    s.Background = bg;
                    s.Gradient = null;
                }
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
                s.FontSize = F(v, 14f);
                break;
            case "font-family":
                s.FontFamily = v.Trim();
                break;
            case "font-weight":
                s.Bold = v.Equals("bold", StringComparison.OrdinalIgnoreCase) ||
                         (float.TryParse(v, out var w) && w >= 600);
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
                    WarnOnce($"Devinno.UI: content-align '{v}' 무시 — top|middle|bottom-left|center|right (예: middle-center)");
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
                s.BorderDashed = v.Trim() == "dashed";
                break;
            case "box-shadow":
                ParseShadow(s, v);
                break;
            case "transform":
                if (Transform2D.Parse(v) is { } xf)
                    s.Transform = xf;
                else
                    WarnOnce($"Devinno.UI: transform '{v}' 를 읽지 못했습니다(translate·translateX·translateY·scale·scaleX·scaleY 만) — 무시");
                break;
            case "transition":
            {
                var bad = new List<string>();
                s.Transitions = TransitionSpec.Parse(v, bad);
                foreach (var x in bad)
                    WarnOnce($"Devinno.UI: transition '{x}' 를 읽지 못했습니다(속성 시간 [곡선] [지연]) — 건너뜀");
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
        if (p.Contains("dashed"))
            s.BorderDashed = true;
        else if (p.Contains("solid"))
            s.BorderDashed = false;
    }

    // 트랙 목록: "1fr 2fr 120" 처럼 따옴표로 감싸도 된다
    private static string[] Tracks(string v)
        => v.Trim().Trim('"', '\'').Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // "1", "1.5", "16/9" → 너비/높이. 0 이하·해석 불가면 없음
    private static float? Ratio(string v)
    {
        var p = v.Split('/', StringSplitOptions.TrimEntries);
        var w = F(p[0]);
        var h = p.Length > 1 ? F(p[1]) : 1f;
        return w > 0 && h > 0 ? w / h : null;
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
            out var f)
            ? f
            : dflt;
    }
}
