using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// 스타일 검사 — 실행 때 조용히 무시되거나(없는 스타일 속성·클래스) 자홍색이 되는(없는 토큰) 이름을 빌드 때 알린다.
/// 바인딩·컴포넌트 파라미터(<c>{…}</c>)가 든 값은 건너뛴다. 코드(C#)에서 더한 토큰·클래스는 볼 수 없다(진단 문구에 끄는 법).
/// </summary>
internal static class StyleCheck
{
    /// <summary>스타일 속성 이름 — 라이브러리 <c>StyleParser</c> 와 같아야 한다(테스트가 비교).</summary>
    internal static readonly string[] Keys =
    {
        "background", "background-color", "color", "opacity", "border-radius", "padding",
        "margin", "font-size", "font-family", "font-weight", "text-align", "white-space",
        "content-align", "gap", "display", "grid-template-columns", "grid-template-rows", "aspect-ratio",
        "flex-direction", "justify-content", "align-items", "align-self", "overflow", "width",
        "height", "min-width", "max-width", "min-height", "max-height", "flex-grow",
        "flex-shrink", "flex", "flex-wrap", "align-content", "position", "left",
        "top", "right", "bottom", "border", "border-style", "box-shadow",
        "transform", "transition",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
    };

    /// <summary>내장 테마 토큰(Light·Dark 같음, 대소문자 무시) — 라이브러리 <c>DvTheme</c> 와 같아야 한다(테스트가 비교).</summary>
    internal static readonly string[] Tokens =
    {
        "alarm-critical", "alarm-critical-fg", "alarm-major", "alarm-major-fg", "alarm-minor", "alarm-minor-fg",
        "background", "background-alt", "bg", "bg2", "border", "border-strong",
        "border-warn", "comm-alarm", "focus", "font-body", "font-caption", "font-display",
        "font-heading", "font-label", "font-mono", "font-title", "font.body", "font.caption",
        "font.display", "font.heading", "font.label", "font.mono", "font.title", "gauge-needle",
        "gauge-needle-tip", "gauge-tick", "gauge-track", "grid-filter", "grid-filter-active", "grid-filter-fg",
        "grid-header", "grid-header-fg", "grid-line", "grid-row", "grid-row-alt", "grid-row-hover",
        "grid-row-selected", "grid-summary", "grid-summary-fg", "kbd-bg", "kbd-key", "kbd-key-fg",
        "kbd-key-special", "kbd-key-special-fg", "knob-face", "knob-fill", "knob-indicator", "muted",
        "primary", "primary-fg", "primary-hover", "primary-press", "radius-full", "radius-lg",
        "radius-md", "radius-sm", "radius.full", "radius.lg", "radius.md", "radius.sm",
        "scrim", "scrollbar-thumb", "scrollbar-track", "shadow", "shadow-lg", "shadow-md",
        "shadow-sm", "shadow.lg", "shadow.md", "shadow.sm", "spacing-2xl", "spacing-lg",
        "spacing-md", "spacing-sm", "spacing-xl", "spacing-xs", "spacing.2xl", "spacing.lg",
        "spacing.md", "spacing.sm", "spacing.xl", "spacing.xs", "state-fault", "state-fault-fg",
        "state-fault-soft", "state-manual", "state-manual-fg", "state-manual-soft", "state-off", "state-off-fg",
        "state-off-soft", "state-run", "state-run-fg", "state-run-soft", "state-stop", "state-stop-fg",
        "state-stop-soft", "state-warn", "state-warn-fg", "state-warn-soft", "success", "success-bg",
        "success-border", "surface", "surface-alt", "surface-overlay", "surface-raised", "tank-chip-bg",
        "tank-chip-fg", "tank-empty-bottom", "tank-empty-top", "tank-frame", "text", "text-muted",
        "text-subtle", "toggle-knob", "toggle-off", "toggle-on", "tone-danger", "tone-danger-fg",
        "tone-danger-soft", "tone-info", "tone-info-fg", "tone-info-soft", "tone-neutral", "tone-neutral-fg",
        "tone-neutral-soft", "tone-success", "tone-success-fg", "tone-success-soft", "tone-warning", "tone-warning-fg",
        "tone-warning-soft", "trend-grid", "warn-bg", "warn-text",
    };

    /// <summary>내장 클래스 — 라이브러리 <c>BuiltInStyles.Sheet</c> 와 같아야 한다(테스트가 비교).</summary>
    internal static readonly string[] Classes =
    {
        "body", "btn", "btn-danger", "btn-ghost", "btn-lg", "btn-on",
        "btn-primary", "btn-secondary", "btn-sm", "caption", "card", "card-flat", "checkbox", "expander", "expander-body", "expander-header",
        "chip", "dialog", "display", "heading", "icon", "input",
        "input-readonly", "label", "listbox", "listbox-item", "listbox-item-on", "mono", "muted", "page", "radio", "segment",
        "segment-item", "segment-item-on", "stat", "stat-accent", "stat-body", "stat-value",
        "status-item", "subtle", "switch-row", "tab", "tab-body", "tab-header",
        "tab-on", "tab-rule", "tabs", "title", "treeview", "treeview-item", "treeview-item-on",
    };

    private static readonly HashSet<string> KeySet = new(Keys, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TokenSet = new(Tokens, StringComparer.OrdinalIgnoreCase);
    internal static readonly HashSet<string> ClassSet = new(Classes, StringComparer.Ordinal);

    // 라이브러리 ThemeResolver 와 같은 꼴
    private static readonly Regex TokenRegex = new(@"token\(\s*([A-Za-z0-9_.-]+)\s*\)|var\(\s*--([A-Za-z0-9_.-]+)\s*\)");
    private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Singleline);

    /// <summary>파일 하나 — 스타일 속성·토큰을 검사하고, 쓰인 클래스와 <c>DvStyle</c> 이 정의한 클래스를 모은다(클래스는 프로젝트 전체를 본 뒤 판단).</summary>
    public static void Check(XElement root, Action<DiagnosticDescriptor, XObject, int, int, object[]> report,
        List<(string Name, XAttribute At)> classUses, HashSet<string> classDefs)
    {
        foreach (var e in root.DescendantsAndSelf())
        {
            foreach (var a in e.Attributes())
            {
                var name = a.Name.LocalName;
                if (name == "style" || name.EndsWith("-style", StringComparison.Ordinal))
                    Declarations(a, a.Value, 0, report);
                else if (name == "class")
                {
                    if (a.Value.IndexOf('{') >= 0)
                        continue;
                    foreach (var c in a.Value.Split(new[] { ' ', '	' }, StringSplitOptions.RemoveEmptyEntries))
                        classUses.Add((c, a));
                }
                else
                    Tokens_(a, a.Value, 0, report);
            }

            if (e.Name.LocalName == "DvStyle")
            {
                foreach (var t in e.Nodes().OfType<XText>())
                    Sheet(t, report, classDefs);
            }
        }
    }

    // DvStyle 본문 — ".a, .b:hover { 선언 }" 여러 개
    private static void Sheet(XText t, Action<DiagnosticDescriptor, XObject, int, int, object[]> report, HashSet<string> classDefs)
    {
        var css = Comment.Replace(t.Value, m => new string(' ', m.Length)); // 자리(오프셋)는 그대로
        var pos = 0;
        while (pos < css.Length)
        {
            var open = css.IndexOf('{', pos);
            if (open < 0)
                break;
            var close = css.IndexOf('}', open);
            if (close < 0)
                close = css.Length;
            foreach (var sel in css.Substring(pos, open - pos).Split(','))
            {
                var m = Regex.Match(sel.Trim(), @"^\.([A-Za-z0-9_-]+)");
                if (m.Success)
                    classDefs.Add(m.Groups[1].Value);
            }

            Declarations(t, css.Substring(open + 1, close - open - 1), open + 1, report);
            pos = close + 1;
        }
    }

    // 선언 목록 "key: value; …" — 없는 키(DVML012), 값 안의 토큰(DVML013). offset = 대상 문자열 안 시작 위치(위치 표시용)
    private static void Declarations(XObject at, string text, int offset, Action<DiagnosticDescriptor, XObject, int, int, object[]> report)
    {
        var whole = text.Trim();
        if (whole.StartsWith("{", StringComparison.Ordinal) && whole.EndsWith("}", StringComparison.Ordinal))
            return; // 스타일 전체가 바인딩 — 실행 때 정해진다(선언마다의 값 바인딩은 이름만 검사)
        var start = 0;
        foreach (var decl in text.Split(';'))
        {
            var colon = decl.IndexOf(':');
            if (colon > 0)
            {
                var raw = decl.Substring(0, colon);
                var key = raw.Trim();
                if (key.Length > 0 && key.IndexOf('{') < 0 && !KeySet.Contains(key))
                {
                    var lead = raw.Length - raw.TrimStart().Length;
                    report(Diagnostics.StyleUnknownKey, at, offset + start + lead, key.Length, new object[] { key, Suggest(key) });
                }

                Tokens_(at, decl.Substring(colon + 1), offset + start + colon + 1, report);
            }

            start += decl.Length + 1;
        }
    }

    private static void Tokens_(XObject at, string text, int offset, Action<DiagnosticDescriptor, XObject, int, int, object[]> report)
    {
        foreach (Match m in TokenRegex.Matches(text))
        {
            var name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (!TokenSet.Contains(name))
                report(Diagnostics.StyleUnknownToken, at, offset + m.Index, m.Length, new object[] { name });
        }
    }

    // 가장 가까운 키(편집 거리 2 이내) — "colr" → "color"
    private static object Suggest(string key)
    {
        var best = Keys.Select(k => (k, d: Distance(key.ToLowerInvariant(), k))).OrderBy(x => x.d).First();
        return best.d <= 2 ? Diagnostics.Text("Style_DidYouMean", best.k) : (object)"";
    }

    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}
