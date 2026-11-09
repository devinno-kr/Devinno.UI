using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// 없는 속성 검사(DVML019) — 내장 태그에 그 태그의 속성도 공통 속성도 아닌 이름(오타 등)이 있으면 경고. 실행 때는 조용히 무시된다.
/// 값 검사(DVML020) — 참/거짓·숫자 속성에 리터럴로 쓴 값이 그 종류가 아니면 경고(실행 때는 기본값 + Trace). 바인딩(<c>{</c> 포함)은 건너뛴다.
/// 표에 없는 태그(사용자 컴포넌트 인스턴스·등록 위젯·심볼·DvPageFrame 같은 내장 컴포넌트)와 <c>DvSymbol</c> 정의 안은 건너뛴다.
/// </summary>
internal static class AttributeCheck
{
    /// <summary>모든 요소가 받는 속성 — 라이브러리 <c>TagSchema.Common</c> 의 이름과 같아야 한다(테스트가 비교).</summary>
    internal static readonly string[] Common =
    [
        "id", "class", "style", "hover-style", "press-style", "disabled-style", "content-align", "grow", "width", "height", "visible", "disabled",
        "onclick", "hold", "onbeforecommit",
    ];

    /// <summary>태그별 속성 이름 — 라이브러리 <c>TagSchema.Tags</c> 의 이름만 복사(테스트가 비교). 정의에 "unused"·"dead read" 로 적힌 것도 아는 이름이다.</summary>
    internal static readonly Dictionary<string, string[]> Tags = new(StringComparer.Ordinal)
    {
        // 공통 + 컨테이너
        ["DvStack"] = ["gap", "padding", "align", "justify"],
        ["DvRow"] = ["gap", "padding", "align", "justify"],
        ["DvGrid"] = ["columns", "rows", "gap", "padding", "align", "justify"],
        ["DvSpacer"] = ["gap", "padding", "align", "justify"],
        ["DvRepeat"] = ["items", "gap", "padding", "align", "justify"],
        ["DvBox"] = [],
        ["DvSlot"] = [],
        ["DvStyle"] = [],
        ["DvTabs"] = ["value", "readonly", "placeholder", "items", "item-value", "item-text", "item-icon"],
        ["DvTab"] = ["value", "text", "disabled"],
        ["DvExpander"] = ["expanded", "header"],
        ["DvStepBar"] = ["steps", "current"],

        // 기본·입력
        ["DvLabel"] = ["text"],
        ["DvButton"] = ["text", "icon", "variant", "size", "value"],
        ["DvIcon"] = ["icon"],
        ["DvImage"] = ["src", "fit"],
        ["DvVideo"] = ["src", "source", "fit", "playing", "loop", "position", "duration"],
        ["DvToggle"] = ["value"],
        ["DvCheckBox"] = ["value", "text"],
        ["DvRadio"] = ["option", "value", "group", "text"],
        ["DvKnob"] = ["value", "min", "max", "step", "unit", "input-mode"],
        ["DvSlider"] = ["value", "min", "max", "step", "unit", "direction", "show-value"],
        ["DvInput"] = ["value", "keyboard-type", "multiline", "placeholder", "format", "min", "max", "unit", "readonly", "step", "show-stepper"],
        ["DvSelect"] = ["value", "placeholder", "readonly", "items", "item-value", "item-text", "item-icon"],
        ["DvOption"] = ["value", "text", "icon"],
        ["DvSegment"] = ["value", "readonly", "fill", "placeholder", "items", "item-value", "item-text", "item-icon"],
        ["DvListBox"] = ["selection-mode", "value", "selected-items", "readonly", "items", "item-value", "item-text", "item-icon"],
        ["DvTreeView"] = ["items", "item-children", "item-text", "item-value", "value", "readonly"],
        ["DvDateTimeInput"] = ["value", "placeholder", "readonly", "mode", "show-seconds", "format"],
        ["DvColorInput"] = ["value", "placeholder", "readonly"],
        ["DvProgress"] = ["value", "min", "max", "tone", "direction", "show-value", "unit", "format", "alarm-zones", "ticks", "minor-ticks", "show-tick-labels", "label", "sweep", "thickness"],

        // 계기·차트·알람
        ["DvGauge"] = ["value", "min", "max", "alarm-zones", "ticks", "minor-ticks", "show-tick-labels", "format", "unit", "tone", "direction", "show-value", "label", "sweep", "thickness"],
        ["DvRing"] = ["value", "min", "max", "alarm-zones", "tone", "unit", "label", "sweep", "thickness", "format", "show-value", "ticks", "minor-ticks", "show-tick-labels", "direction"],
        ["DvLamp"] = ["on", "blink", "blink-when", "blink-interval", "color", "state"],
        ["DvTank"] = ["value", "min", "max", "alarm-zones", "unit", "format", "show-zone-marks"],
        ["DvSparkline"] = ["values", "tone", "y-min", "y-max", "area", "show-value", "inner-radius"],
        ["DvBarChart"] = ["items", "item-value", "item-text", "tone", "y-min", "y-max", "show-value", "area", "inner-radius"],
        ["DvPieChart"] = ["items", "item-value", "item-text", "show-value", "inner-radius", "tone", "y-min", "y-max", "area"],
        ["DvTrend"] = ["source", "window", "end", "y-min", "y-max", "area", "smooth"],
        ["DvTrendChannel"] = ["name", "label", "unit", "color", "points"],
        ["DvAlarmBanner"] = ["items", "onack", "show-ack", "comm-alarm", "row-height"],
        ["DvAlarmList"] = ["items", "onack", "show-ack", "row-height", "comm-alarm"],

        // 그리드
        ["DvDataGrid"] =
        [
            "items", "row-height", "header-height", "selection-mode", "selection", "row-key", "row-class", "onrowclick",
            "header-color", "header-text-color", "text-color", "row-color", "row-alt-color", "hover-color", "selected-color", "line-color", "summary-color", "summary-text-color",
        ],
        ["DvColumn"] =
        [
            "header", "field", "width", "align", "format", "frozen", "sortable", "sort", "cell-class", "cell-type", "filter", "readonly", "onbeforecommit",
            "on-color", "blink", "text", "icon", "variant", "onclick", "enabled", "min", "max", "unit", "mode", "show-seconds", "items", "item-value", "item-text", "item-icon",
        ],
        ["DvColumnGroup"] = ["header"],
        ["DvSummaryRow"] = ["title", "fn"],
        ["DvSummary"] = ["field", "fn", "format", "value"],

        // 공정도
        ["DvProcess"] = ["direction", "look", "align", "placement", "base-size", "flow-animation", "fit"],
        ["DvUnit"] = ["name", "direction", "label", "row", "rank", "x", "y", "placement", "visible"],
        ["DvPort"] = ["name", "side", "at", "visible"],
        ["DvVessel"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "text", "visible", "width", "height", "level"],
        ["DvPump"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "text", "visible", "running"],
        ["DvValve"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "text", "visible", "open"],
        ["DvValve3"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "text", "visible", "open-b", "open-c"],
        ["DvInline"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "text", "visible"],
        ["DvExchanger"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "text", "visible"],
        ["DvTerminal"] = ["name", "label", "rotate", "flip", "row", "rank", "x", "y", "text", "visible"],
        ["DvAgitator"] = ["running", "kind", "visible"],
        ["DvMotor"] = ["running", "kind", "visible"],
        ["DvAttach"] = ["side", "visible"],
        ["DvLine"] = ["from", "to", "arrow", "flow", "medium", "via", "label", "visible"],
        ["DvSignal"] = ["from", "to", "arrow", "label", "kind", "color", "width", "visible"],
        ["DvInstrument"] = ["name", "on", "tap", "tag", "number", "value", "panel", "x", "y", "visible"],
    };

    /// <summary>참/거짓 속성 — 라이브러리 <c>TagSchema</c> 에서 Kind 가 Bool 인 이름(테스트가 비교). 공통은 <see cref="CommonBool"/>.</summary>
    internal static readonly string[] CommonBool = ["visible", "disabled"];

    /// <summary>태그별 참/거짓 속성(<c>AttrKind.Bool</c>).</summary>
    internal static readonly Dictionary<string, string[]> Bool = new(StringComparer.Ordinal)
    {
        ["DvTabs"] = ["readonly"],
        ["DvTab"] = ["disabled"],
        ["DvExpander"] = ["expanded"],
        ["DvButton"] = ["value"],
        ["DvVideo"] = ["playing", "loop"],
        ["DvToggle"] = ["value"],
        ["DvCheckBox"] = ["value"],
        ["DvSlider"] = ["show-value"],
        ["DvInput"] = ["multiline", "readonly", "show-stepper"],
        ["DvSelect"] = ["readonly"],
        ["DvSegment"] = ["readonly", "fill"],
        ["DvListBox"] = ["readonly"],
        ["DvTreeView"] = ["readonly"],
        ["DvDateTimeInput"] = ["readonly", "show-seconds"],
        ["DvColorInput"] = ["readonly"],
        ["DvProgress"] = ["show-value", "show-tick-labels"],
        ["DvGauge"] = ["show-tick-labels", "show-value"],
        ["DvRing"] = ["show-value", "show-tick-labels"],
        ["DvLamp"] = ["on", "blink", "blink-when"],
        ["DvTank"] = ["show-zone-marks"],
        ["DvSparkline"] = ["area", "show-value"],
        ["DvBarChart"] = ["show-value", "area"],
        ["DvPieChart"] = ["show-value", "area"],
        ["DvTrend"] = ["area", "smooth"],
        ["DvAlarmBanner"] = ["show-ack"],
        ["DvAlarmList"] = ["show-ack"],
        ["DvColumn"] = ["frozen", "sortable", "readonly", "blink", "show-seconds"],
        ["DvProcess"] = ["flow-animation"],
        ["DvUnit"] = ["visible"],
        ["DvPort"] = ["visible"],
        ["DvVessel"] = ["visible"],
        ["DvPump"] = ["visible", "running"],
        ["DvValve"] = ["visible", "open"],
        ["DvValve3"] = ["visible", "open-b", "open-c"],
        ["DvInline"] = ["visible"],
        ["DvExchanger"] = ["visible"],
        ["DvTerminal"] = ["visible"],
        ["DvAgitator"] = ["running", "visible"],
        ["DvMotor"] = ["running", "visible"],
        ["DvAttach"] = ["visible"],
        ["DvLine"] = ["flow", "visible"],
        ["DvSignal"] = ["visible"],
        ["DvInstrument"] = ["panel", "visible"],
    };

    /// <summary>숫자 속성 — 라이브러리 <c>TagSchema</c> 에서 Kind 가 Number 인 이름(테스트가 비교). 공통은 <see cref="CommonNumber"/>.</summary>
    internal static readonly string[] CommonNumber = ["grow", "hold"];

    /// <summary>태그별 숫자 속성(<c>AttrKind.Number</c>).</summary>
    internal static readonly Dictionary<string, string[]> Number = new(StringComparer.Ordinal)
    {
        ["DvStepBar"] = ["current"],
        ["DvVideo"] = ["position", "duration"],
        ["DvKnob"] = ["value", "min", "max", "step"],
        ["DvSlider"] = ["value", "min", "max", "step"],
        ["DvInput"] = ["min", "max", "step"],
        ["DvProgress"] = ["value", "min", "max", "ticks", "minor-ticks", "sweep", "thickness"],
        ["DvGauge"] = ["value", "min", "max", "ticks", "minor-ticks", "sweep", "thickness"],
        ["DvRing"] = ["value", "min", "max", "sweep", "thickness", "ticks", "minor-ticks"],
        ["DvLamp"] = ["blink-interval"],
        ["DvTank"] = ["value", "min", "max"],
        ["DvSparkline"] = ["y-min", "y-max", "inner-radius"],
        ["DvBarChart"] = ["y-min", "y-max", "inner-radius"],
        ["DvPieChart"] = ["inner-radius", "y-min", "y-max"],
        ["DvTrend"] = ["window", "y-min", "y-max"],
        ["DvAlarmBanner"] = ["row-height"],
        ["DvAlarmList"] = ["row-height"],
        ["DvDataGrid"] = ["row-height", "header-height"],
        ["DvColumn"] = ["min", "max"],
        ["DvUnit"] = ["row", "rank", "x", "y"],
        ["DvPort"] = ["at"],
        ["DvVessel"] = ["row", "rank", "x", "y", "width", "height", "level"],
        ["DvPump"] = ["row", "rank", "x", "y"],
        ["DvValve"] = ["row", "rank", "x", "y"],
        ["DvValve3"] = ["row", "rank", "x", "y"],
        ["DvInline"] = ["row", "rank", "x", "y"],
        ["DvExchanger"] = ["row", "rank", "x", "y"],
        ["DvTerminal"] = ["row", "rank", "x", "y"],
        ["DvSignal"] = ["width"],
        ["DvInstrument"] = ["x", "y"],
    };

    private static readonly HashSet<string> CommonSet = new(Common, StringComparer.Ordinal);

    public static void Check(XElement root, Action<DiagnosticDescriptor, XObject, object[]> report) => Walk(root, true, report);

    private static void Walk(XElement e, bool isRoot, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var tag = e.Name.LocalName;
        if (tag == "DvSymbol")
            return; // 심볼 정의 — 그 안의 요소는 그림 정의라 속성 규칙이 다르다

        if (Tags.TryGetValue(tag, out var own))
        {
            foreach (var a in e.Attributes())
            {
                if (a.IsNamespaceDeclaration || a.Name.Namespace != XNamespace.None)
                    continue;
                var name = a.Name.LocalName;
                if (CommonSet.Contains(name) || Array.IndexOf(own, name) >= 0)
                {
                    CheckValue(tag, name, a, report);
                    continue;
                }
                if (isRoot && (name == "vm" || name.StartsWith("font-lang-", StringComparison.Ordinal)))
                    continue; // 루트만 받는 이름
                report(Diagnostics.UnknownAttribute, a, new object[] { tag, name, Suggest(name, own) });
            }
        }

        foreach (var c in e.Elements())
            Walk(c, false, report);
    }

    // DVML020 — 리터럴 값이 속성 종류에 맞는지. 규칙은 라이브러리 Layout.AttrRules 와 같다:
    // 참/거짓 = true·false(대소문자 무시)·숫자·빈 값, 숫자 = Invariant 숫자·빈 값(없음과 같음). 앞뒤 공백 무시.
    private static void CheckValue(string tag, string name, XAttribute a, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var value = a.Value;
        if (value.IndexOf('{') >= 0)
            return; // 바인딩·보간 — 실행 때 값이 정해진다
        if (Has(Bool, CommonBool, tag, name) && !IsBool(value))
            report(Diagnostics.InvalidBool, a, new object[] { tag, name, value });
        else if (Has(Number, CommonNumber, tag, name) && !IsNumber(value))
            report(Diagnostics.InvalidNumber, a, new object[] { tag, name, value });
    }

    // 태그 정의가 같은 이름을 다른 종류로 가지면(DvVessel width = 숫자) 태그 정의가 이긴다 — TagSchema.Find 와 같은 순서
    private static bool Has(Dictionary<string, string[]> table, string[] common, string tag, string name)
    {
        if (Tags.TryGetValue(tag, out var own) && Array.IndexOf(own, name) >= 0)
            return table.TryGetValue(tag, out var names) && Array.IndexOf(names, name) >= 0;
        return Array.IndexOf(common, name) >= 0;
    }

    internal static bool IsBool(string value)
    {
        var s = value.Trim();
        return s.Length == 0 || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !double.IsNaN(d);
    }

    internal static bool IsNumber(string value)
    {
        var s = value.Trim();
        return s.Length == 0 ||
               double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !double.IsNaN(d) && !double.IsInfinity(d);
    }

    // 가장 가까운 이름(편집 거리 2 이내) — "txet" → "text"
    private static object Suggest(string name, string[] own)
    {
        var best = own.Concat(Common).Select(k => (k, d: StyleCheck.Distance(name, k))).OrderBy(x => x.d).FirstOrDefault();
        return best.k is not null && best.d <= 2 ? Diagnostics.Text("Style_DidYouMean", best.k) : (object)"";
    }
}
