using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// 없는 속성 검사(DVML019) — 내장 태그에 그 태그의 속성도 공통 속성도 아닌 이름(오타 등)이 있으면 경고. 실행 때는 조용히 무시된다.
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
                    continue;
                if (isRoot && (name == "vm" || name.StartsWith("font-lang-", StringComparison.Ordinal)))
                    continue; // 루트만 받는 이름
                report(Diagnostics.UnknownAttribute, a, new object[] { tag, name, Suggest(name, own) });
            }
        }

        foreach (var c in e.Elements())
            Walk(c, false, report);
    }

    // 가장 가까운 이름(편집 거리 2 이내) — "txet" → "text"
    private static object Suggest(string name, string[] own)
    {
        var best = own.Concat(Common).Select(k => (k, d: StyleCheck.Distance(name, k))).OrderBy(x => x.d).FirstOrDefault();
        return best.k is not null && best.d <= 2 ? Diagnostics.Text("Style_DidYouMean", best.k) : (object)"";
    }
}
