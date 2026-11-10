using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// 없는 속성 검사(DVML019) — 내장 태그에 그 태그의 속성도 공통 속성도 아닌 이름(오타 등)이 있으면 경고. 실행 때는 조용히 무시된다.
/// 값 검사(DVML020) — 참/거짓·숫자·목록 값·색 속성에 리터럴로 쓴 값이 그 종류가 아니면 경고(실행 때는 기본값 + Trace). 바인딩(<c>{</c> 포함)은 건너뛴다.
/// 표에 없는 태그(사용자 컴포넌트 인스턴스·등록 위젯·심볼·DvCard 같은 내장 컴포넌트)와 <c>DvSymbol</c> 정의 안은 건너뛴다.
/// 표에 있는 내장 컴포넌트(DvPageFrame·DvDialogFrame — 매개변수 title·subtitle)는 앱이 C# 으로 바꿀 수 있어(생성기는 모른다) 없는 속성은 알리지 않고 공통 속성 값만 본다.
/// 그 진단은 따로 모아(reportFrame) 프로젝트의 어느 dvml 이든 그 이름의 DvComponent 로 바꾸면 생성기가 버린다(DvmlGenerator).
/// 내장 태그 검사(DVML021) — <c>&lt;DvComponent name&gt;</c> 이 내장 태그 이름이면 오류(라이브러리 ComponentRegistry.Register 가 예외).
/// </summary>
internal static class AttributeCheck
{
    /// <summary>모든 요소가 받는 속성 — 라이브러리 <c>TagSchema.Common</c> 의 이름과 같아야 한다(테스트가 비교).</summary>
    internal static readonly string[] Common =
    [
        "id", "class", "style", "hover-style", "press-style", "disabled-style", "content-align", "grow", "width", "height", "visible", "disabled",
        "onclick", "hold", "onbeforecommit",
    ];

    /// <summary>
    /// 상자가 아닌 하위 요소 — 공통 속성은 <c>id</c> 만 받는다(쓰는 것은 그 태그 정의에). 라이브러리 <c>TagSchema.ChildTags</c> 와 같아야 한다(테스트가 비교).
    /// </summary>
    internal static readonly string[] ChildTags =
    [
        "DvOption", "DvColumn", "DvColumnGroup", "DvSummaryRow", "DvSummary", "DvTrendChannel",
        "DvPort", "DvAgitator", "DvMotor", "DvLine", "DvSignal", "DvStyle",
    ];

    /// <summary>표에 있지만 내장 컴포넌트인 태그 — 앱이 같은 이름의 컴포넌트로 바꿀 수 있다(DVML021 아님). 라이브러리 <c>TagSchema.ComponentTags</c> 와 같아야 한다(테스트가 비교).</summary>
    internal static readonly string[] ComponentTags = ["DvPageFrame", "DvDialogFrame"];

    /// <summary>태그별 속성 이름 — 라이브러리 <c>TagSchema.Tags</c> 의 이름만 복사(테스트가 비교). 정의는 그 태그가 실제로 쓰는 속성만이다(3단계 R9).</summary>
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
        ["DvScreen"] = [],
        ["DvPageFrame"] = ["title", "subtitle"],
        ["DvDialogFrame"] = ["title", "subtitle"],
        ["DvTabs"] = ["value", "readonly"],
        ["DvTab"] = ["value", "text", "disabled"],
        ["DvExpander"] = ["expanded", "title"],
        ["DvStepBar"] = ["steps", "current"],

        // 기본·입력
        ["DvLabel"] = ["text"],
        ["DvButton"] = ["text", "icon", "variant", "size", "value"],
        ["DvIcon"] = ["icon"],
        ["DvImage"] = ["src", "fit"],
        ["DvVideo"] = ["src", "source", "fit", "playing", "loop", "volume", "muted", "position", "duration"],
        ["DvToggle"] = ["value", "readonly"],
        ["DvCheckBox"] = ["value", "text", "readonly"],
        ["DvRadio"] = ["option", "value", "group", "text", "readonly"],
        ["DvKnob"] = ["value", "min", "max", "step", "unit", "readonly", "input-mode"],
        ["DvSlider"] = ["value", "min", "max", "step", "unit", "readonly", "direction", "show-value", "format"],
        ["DvInput"] = ["value", "keyboard-type", "multiline", "placeholder", "format", "min", "max", "unit", "readonly", "step", "show-stepper"],
        ["DvSelect"] = ["value", "placeholder", "readonly", "items", "item-value", "item-text", "item-icon"],
        ["DvOption"] = ["value", "text", "icon"],
        ["DvSegment"] = ["value", "readonly", "fill", "items", "item-value", "item-text", "item-icon"],
        ["DvListBox"] = ["selection-mode", "value", "selected-items", "readonly", "items", "item-value", "item-text", "item-icon"],
        ["DvTreeView"] = ["items", "item-children", "item-text", "item-value", "value", "readonly"],
        ["DvDateTimeInput"] = ["value", "placeholder", "readonly", "mode", "show-seconds", "format"],
        ["DvColorInput"] = ["value", "placeholder", "readonly"],
        ["DvProgress"] = ["value", "min", "max", "tone", "direction", "show-value", "unit", "format", "alarm-zones"],

        // 계기·차트·알람
        ["DvGauge"] = ["value", "min", "max", "alarm-zones", "ticks", "minor-ticks", "show-tick-labels", "format", "unit", "show-value"],
        ["DvRing"] = ["value", "min", "max", "alarm-zones", "tone", "unit", "label", "sweep", "thickness", "format", "show-value"],
        ["DvLamp"] = ["on", "blink", "blink-interval", "color", "state"],
        ["DvTank"] = ["value", "min", "max", "alarm-zones", "unit", "format", "show-zone-marks"],
        ["DvSparkline"] = ["values", "tone", "y-min", "y-max", "area"],
        ["DvBarChart"] = ["items", "item-value", "item-text", "tone", "y-min", "y-max", "show-value", "format"],
        ["DvPieChart"] = ["items", "item-value", "item-text", "show-value", "format", "inner-radius"],
        ["DvTrend"] = ["source", "window", "end", "y-min", "y-max", "area", "smooth"],
        ["DvTrendChannel"] = ["name", "label", "unit", "color", "points"],
        ["DvAlarmBanner"] = ["items", "onack", "show-ack", "comm-alarm"],
        ["DvAlarmList"] = ["items", "onack", "show-ack", "row-height"],

        // 그리드
        ["DvDataGrid"] =
        [
            "items", "row-height", "header-height", "selection-mode", "selected-items", "row-key", "row-class", "onrowclick",
            "header-color", "header-text-color", "text-color", "row-color", "row-alt-color", "hover-color", "selected-color", "line-color", "summary-color", "summary-text-color",
        ],
        ["DvColumn"] =
        [
            "header", "field", "width", "align", "format", "frozen", "sortable", "sort", "cell-class", "cell-type", "filter", "readonly", "onbeforecommit",
            "on-color", "blink", "text", "icon", "variant", "onclick", "cell-disabled", "min", "max", "unit", "mode", "show-seconds", "items", "item-value", "item-text", "item-icon",
        ],
        ["DvColumnGroup"] = ["header"],
        ["DvSummaryRow"] = ["label", "fn"],
        ["DvSummary"] = ["field", "fn", "format", "value"],

        // 공정도
        ["DvProcess"] = ["direction", "look", "align", "placement", "base-size", "flow-animation", "fit"],
        ["DvUnit"] = ["name", "direction", "label", "row", "rank", "x", "y", "placement", "visible"],
        ["DvPort"] = ["name", "side", "at", "visible"],
        ["DvVessel"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "visible", "width", "height", "level"],
        ["DvPump"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "visible", "running"],
        ["DvValve"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "visible", "open"],
        ["DvValve3"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "visible", "open-b", "open-c"],
        ["DvInline"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "visible"],
        ["DvExchanger"] = ["name", "label", "kind", "rotate", "flip", "row", "rank", "x", "y", "visible"],
        ["DvTerminal"] = ["name", "text", "flip", "row", "rank", "x", "y", "visible"],
        ["DvAgitator"] = ["running", "kind", "visible"],
        ["DvMotor"] = ["running", "kind", "visible"],
        ["DvAttach"] = ["side", "visible"],
        ["DvLine"] = ["from", "to", "arrow", "flow", "medium", "via", "label", "visible"],
        ["DvSignal"] = ["from", "to", "arrow", "label", "kind", "color", "thickness", "visible"],
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
        ["DvVideo"] = ["playing", "loop", "muted"],
        ["DvToggle"] = ["value", "readonly"],
        ["DvCheckBox"] = ["value", "readonly"],
        ["DvRadio"] = ["readonly"],
        ["DvKnob"] = ["readonly"],
        ["DvSlider"] = ["readonly", "show-value"],
        ["DvInput"] = ["multiline", "readonly", "show-stepper"],
        ["DvSelect"] = ["readonly"],
        ["DvSegment"] = ["readonly", "fill"],
        ["DvListBox"] = ["readonly"],
        ["DvTreeView"] = ["readonly"],
        ["DvDateTimeInput"] = ["readonly", "show-seconds"],
        ["DvColorInput"] = ["readonly"],
        ["DvProgress"] = ["show-value"],
        ["DvGauge"] = ["show-tick-labels", "show-value"],
        ["DvRing"] = ["show-value"],
        ["DvLamp"] = ["on", "blink"],
        ["DvTank"] = ["show-zone-marks"],
        ["DvSparkline"] = ["area"],
        ["DvBarChart"] = ["show-value"],
        ["DvPieChart"] = ["show-value"],
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
        ["DvVideo"] = ["volume", "position", "duration"],
        ["DvKnob"] = ["value", "min", "max", "step"],
        ["DvSlider"] = ["value", "min", "max", "step"],
        ["DvInput"] = ["min", "max", "step"],
        ["DvProgress"] = ["value", "min", "max"],
        ["DvGauge"] = ["value", "min", "max", "ticks", "minor-ticks"],
        ["DvRing"] = ["value", "min", "max", "sweep", "thickness"],
        ["DvLamp"] = ["blink-interval"],
        ["DvTank"] = ["value", "min", "max"],
        ["DvSparkline"] = ["y-min", "y-max"],
        ["DvBarChart"] = ["y-min", "y-max"],
        ["DvPieChart"] = ["inner-radius"],
        ["DvTrend"] = ["window", "y-min", "y-max"],
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
        ["DvSignal"] = ["thickness"],
        ["DvInstrument"] = ["x", "y"],
    };

    /// <summary>
    /// 목록 값 속성 — 라이브러리 <c>TagSchema</c> 에서 Kind 가 Enum 인 이름과 그 Values(테스트가 비교). 공통은 <see cref="CommonEnum"/>.
    /// <c>tone</c>·<c>DvLamp state</c> 는 없다 — 값이 앱 테마의 토큰(tone-값·state-값)에 달려 있어 빌드 때 알 수 없다(실행 때 검사).
    /// </summary>
    internal static readonly Dictionary<string, string[]> CommonEnum = new(StringComparer.Ordinal)
    {
        ["content-align"] = ["top-left", "top-center", "top-right", "middle-left", "middle-center", "middle-right", "bottom-left", "bottom-center", "bottom-right"],
    };

    /// <summary>태그별 목록 값 속성(<c>AttrKind.Enum</c>) → 받는 값.</summary>
    internal static readonly Dictionary<string, Dictionary<string, string[]>> Enum = new(StringComparer.Ordinal)
    {
        ["DvStack"] = new(StringComparer.Ordinal) { ["align"] = ["start", "center", "end", "stretch"], ["justify"] = ["start", "center", "end", "between"] },
        ["DvRow"] = new(StringComparer.Ordinal) { ["align"] = ["start", "center", "end", "stretch"], ["justify"] = ["start", "center", "end", "between"] },
        ["DvGrid"] = new(StringComparer.Ordinal) { ["align"] = ["start", "center", "end", "stretch"], ["justify"] = ["start", "center", "end", "stretch"] },
        ["DvSpacer"] = new(StringComparer.Ordinal) { ["align"] = ["start", "center", "end", "stretch"], ["justify"] = ["start", "center", "end", "between"] },
        ["DvRepeat"] = new(StringComparer.Ordinal) { ["align"] = ["start", "center", "end", "stretch"], ["justify"] = ["start", "center", "end", "between"] },
        ["DvButton"] = new(StringComparer.Ordinal) { ["variant"] = ["primary", "secondary", "danger", "ghost"], ["size"] = ["sm", "lg"] },
        ["DvImage"] = new(StringComparer.Ordinal) { ["fit"] = ["contain", "cover", "fill"] },
        ["DvVideo"] = new(StringComparer.Ordinal) { ["fit"] = ["contain", "cover", "fill"] },
        ["DvKnob"] = new(StringComparer.Ordinal) { ["input-mode"] = ["drag", "tap", "both"] },
        ["DvSlider"] = new(StringComparer.Ordinal) { ["direction"] = ["right", "left", "up", "down"] },
        ["DvInput"] = new(StringComparer.Ordinal) { ["keyboard-type"] = ["text", "numeric", "password"] },
        ["DvListBox"] = new(StringComparer.Ordinal) { ["selection-mode"] = ["single", "multi"] },
        ["DvDateTimeInput"] = new(StringComparer.Ordinal) { ["mode"] = ["date", "time", "datetime"] },
        ["DvProgress"] = new(StringComparer.Ordinal) { ["direction"] = ["right", "left", "up", "down"] },
        ["DvDataGrid"] = new(StringComparer.Ordinal) { ["selection-mode"] = ["none", "single", "multi"] },
        ["DvColumn"] = new(StringComparer.Ordinal) { ["align"] = ["left", "center", "right"], ["sort"] = ["asc", "desc"], ["cell-type"] = ["text", "check", "lamp", "button", "input", "select", "datetime", "color"], ["filter"] = ["none", "auto", "text", "values", "range", "date"], ["variant"] = ["primary", "secondary", "danger", "ghost"], ["mode"] = ["date", "time", "datetime"] },
        ["DvSummaryRow"] = new(StringComparer.Ordinal) { ["fn"] = ["sum", "avg", "min", "max", "count"] },
        ["DvSummary"] = new(StringComparer.Ordinal) { ["fn"] = ["sum", "avg", "min", "max", "count"] },
        ["DvProcess"] = new(StringComparer.Ordinal) { ["direction"] = ["lr", "tb"], ["look"] = ["plain", "rich"], ["align"] = ["auto", "step", "row"], ["placement"] = ["auto", "fixed"], ["fit"] = ["none", "scale-down", "contain"] },
        ["DvUnit"] = new(StringComparer.Ordinal) { ["direction"] = ["lr", "tb"], ["placement"] = ["auto"] },
        ["DvPort"] = new(StringComparer.Ordinal) { ["side"] = ["left", "top", "right", "bottom"] },
        ["DvVessel"] = new(StringComparer.Ordinal) { ["kind"] = ["tank", "reactor", "silo", "hopper", "drum", "column", "open"], ["rotate"] = ["0", "90", "180", "270"], ["flip"] = ["h", "v", "hv"] },
        ["DvPump"] = new(StringComparer.Ordinal) { ["kind"] = ["centrifugal", "gear", "compressor", "blower", "isa"], ["rotate"] = ["0", "90", "180", "270"], ["flip"] = ["h", "v", "hv"] },
        ["DvValve"] = new(StringComparer.Ordinal) { ["kind"] = ["gate", "ball", "butterfly", "control", "check", "globe", "plug", "diaphragm", "angle", "relief"], ["rotate"] = ["0", "90", "180", "270"], ["flip"] = ["h", "v", "hv"] },
        ["DvValve3"] = new(StringComparer.Ordinal) { ["kind"] = ["ball", "control"], ["rotate"] = ["0", "90", "180", "270"], ["flip"] = ["h", "v", "hv"] },
        ["DvInline"] = new(StringComparer.Ordinal) { ["kind"] = ["flowmeter", "filter", "strainer", "sight", "reducer", "orifice", "mixer", "vent", "blind"], ["rotate"] = ["0", "90", "180", "270"], ["flip"] = ["h", "v", "hv"] },
        ["DvExchanger"] = new(StringComparer.Ordinal) { ["kind"] = ["shell", "plate"], ["rotate"] = ["0", "90", "180", "270"], ["flip"] = ["h", "v", "hv"] },
        ["DvTerminal"] = new(StringComparer.Ordinal) { ["flip"] = ["h", "v", "hv"] },
        ["DvAgitator"] = new(StringComparer.Ordinal) { ["kind"] = [] },
        ["DvMotor"] = new(StringComparer.Ordinal) { ["kind"] = ["isa"] },
        ["DvAttach"] = new(StringComparer.Ordinal) { ["side"] = ["left", "right", "top", "bottom"] },
        ["DvLine"] = new(StringComparer.Ordinal) { ["arrow"] = ["end", "start", "both", "none"], ["medium"] = ["water", "steam", "air", "gas", "oil", "acid"] },
        ["DvSignal"] = new(StringComparer.Ordinal) { ["arrow"] = ["end", "start", "both", "none"], ["kind"] = ["electric", "pneumatic"] },
    };

    /// <summary>태그별 색 속성(<c>AttrKind.Color</c>) — 라이브러리 <c>TagSchema</c> 와 같아야 한다(테스트가 비교). 공통 색 속성은 없다.</summary>
    internal static readonly Dictionary<string, string[]> Color = new(StringComparer.Ordinal)
    {
        ["DvLamp"] = ["color"],
        ["DvTrendChannel"] = ["color"],
        ["DvDataGrid"] = ["header-color", "header-text-color", "text-color", "row-color", "row-alt-color", "hover-color", "selected-color", "line-color", "summary-color", "summary-text-color"],
        ["DvColumn"] = ["on-color"],
        ["DvSignal"] = ["color"],
    };

    // 다른 진단이 이미 값을 검사하는 속성 — DVML011(공정도 ProcessCheck) · DVML017(WidgetCheck). DVML020 은 같은 값을 두 번 알리지 않는다
    private static bool CheckedElsewhere(string tag, string name) =>
        (tag, name) is ("DvProcess", "direction" or "look" or "align") or ("DvLine", "medium") or ("DvSignal", "kind" or "color" or "thickness")
            or ("DvInstrument", "x" or "y")
            or ("DvListBox", "selection-mode") or ("DvSlider", "direction")
        || name == "kind" && ProcessCheck.Kinds.ContainsKey(tag)
        || name == "rotate" && ProcessCheck.Kinds.ContainsKey(tag);

    private static readonly HashSet<string> CommonSet = new(Common, StringComparer.Ordinal);
    private static readonly HashSet<string> ChildSet = new(ChildTags, StringComparer.Ordinal);

    // 그 태그가 받는 공통 속성인가 — 상자가 아닌 하위 요소는 id 만
    private static bool TakesCommon(string tag, string name) => CommonSet.Contains(name) && (name == "id" || !ChildSet.Contains(tag));

    /// <summary>
    /// Checks one dvml. <paramref name="reportFrame"/> gets the diagnostics on <see cref="ComponentTags"/> instances with the tag
    /// (the generator drops them when any dvml of the project replaces that tag); <paramref name="components"/> gets the names of this file's <c>DvComponent</c>s.
    /// </summary>
    public static void Check(XElement root, Action<DiagnosticDescriptor, XObject, object[]> report,
        Action<string, DiagnosticDescriptor, XObject, object[]> reportFrame, ISet<string> components)
    {
        // 이 파일이 정의한 컴포넌트 — 내장 컴포넌트 이름(DvPageFrame…)을 바꾼 것이면 그 인스턴스 속성은 그 정의의 매개변수다(다른 파일의 것도 — 생성기가 모아서 거른다)
        foreach (var c in root.DescendantsAndSelf().Where(x => x.Name.LocalName == "DvComponent"))
        {
            if (c.Attribute("name") is not { } n)
                continue;
            var name = n.Value.Trim();
            if (Tags.ContainsKey(name) && Array.IndexOf(ComponentTags, name) < 0)
                report(Diagnostics.ComponentBuiltInName, n, new object[] { name });
            else
                components.Add(name);
        }

        Walk(root, true, report, reportFrame);
    }

    private static void Walk(XElement e, bool isRoot, Action<DiagnosticDescriptor, XObject, object[]> report, Action<string, DiagnosticDescriptor, XObject, object[]> reportFrame)
    {
        var tag = e.Name.LocalName;
        if (tag == "DvSymbol")
            return; // 심볼 정의 — 그 안의 요소는 그림 정의라 속성 규칙이 다르다

        if (Array.IndexOf(ComponentTags, tag) >= 0)
        {
            // 틀 — C# 으로 바꾼 컴포넌트는 생성기가 모르므로 없는 속성(매개변수일 수 있다)은 알리지 않고, 공통 속성 값만
            foreach (var a in e.Attributes())
            {
                if (a.IsNamespaceDeclaration || a.Name.Namespace != XNamespace.None || !TakesCommon(tag, a.Name.LocalName))
                    continue;
                CheckValue(tag, a.Name.LocalName, a, (d, at, args) => reportFrame(tag, d, at, args));
            }
        }
        else if (Tags.TryGetValue(tag, out var own))
        {
            foreach (var a in e.Attributes())
            {
                if (a.IsNamespaceDeclaration || a.Name.Namespace != XNamespace.None)
                    continue;
                var name = a.Name.LocalName;
                if (Array.IndexOf(own, name) >= 0 || TakesCommon(tag, name))
                {
                    CheckValue(tag, name, a, report);
                    continue;
                }
                if (isRoot && (name == "vm" || name.StartsWith("font-lang-", StringComparison.Ordinal)))
                    continue; // 루트만 받는 이름
                report(Diagnostics.UnknownAttribute, a, new object[] { tag, name, Renamed.TryGetValue((tag, name), out var now) ? RenamedText(name, now) : Suggest(name, own, tag) });
            }
        }

        foreach (var c in e.Elements())
            Walk(c, false, report, reportFrame);
    }

    // DVML020 — 리터럴 값이 속성 종류에 맞는지. 규칙은 라이브러리 Layout.AttrRules 와 같다:
    // 참/거짓 = true·false(대소문자 무시)·숫자·빈 값, 숫자 = Invariant 숫자·빈 값(없음과 같음),
    // 목록 값 = 정의된 값(대소문자 무시)·빈 값, 색 = #RGB·#RRGGBB·#RRGGBBAA·transparent·token(이름)·빈 값. 앞뒤 공백 무시.
    private static void CheckValue(string tag, string name, XAttribute a, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var value = a.Value;
        if (value.IndexOf('{') >= 0)
            return; // 바인딩·보간 — 실행 때 값이 정해진다
        if (Has(Bool, CommonBool, tag, name) && !IsBool(value))
            report(Diagnostics.InvalidBool, a, new object[] { tag, name, value });
        else if (Has(Number, CommonNumber, tag, name) && !CheckedElsewhere(tag, name) && !IsNumber(value))
            report(Diagnostics.InvalidNumber, a, new object[] { tag, name, value });
        else if (EnumValues(tag, name) is { } values && !CheckedElsewhere(tag, name) && !IsEnum(value, values, tag, name))
            report(Diagnostics.InvalidEnum, a, new object[] { tag, name, value, values.Length == 0 ? "-" : string.Join(", ", values) });
        else if (Has(Color, Array.Empty<string>(), tag, name) && !CheckedElsewhere(tag, name) && !IsColor(value))
            report(Diagnostics.InvalidColor, a, new object[] { tag, name, value });
    }

    // 목록 값 속성의 값 목록 — 태그 정의가 먼저(Has 와 같은 순서), 아니면 공통. 목록 속성이 아니면 null
    private static string[]? EnumValues(string tag, string name)
    {
        if (Tags.TryGetValue(tag, out var own) && Array.IndexOf(own, name) >= 0)
            return Enum.TryGetValue(tag, out var attrs) && attrs.TryGetValue(name, out var v) ? v : null;
        return CommonEnum.TryGetValue(name, out var c) ? c : null;
    }

    internal static bool IsEnum(string value, string[] values, string tag = "", string name = "")
    {
        var s = value.Trim();
        if (s.Length == 0 || values.Any(v => string.Equals(v, s, StringComparison.OrdinalIgnoreCase)))
            return true;
        return name == "flip" && s.Equals("vh", StringComparison.OrdinalIgnoreCase); // 공정 장비 flip — vh 는 hv 의 별칭
    }

    private static readonly System.Text.RegularExpressions.Regex TokenOnly =
        new(@"^(?:token\(\s*[A-Za-z0-9_.-]+\s*\)|var\(\s*--[A-Za-z0-9_.-]+\s*\))$");

    // 색 — 라이브러리 DvColor.TryParse(#3·6·8자리, transparent) 또는 토큰 하나. 토큰 이름은 앱 테마에 달렸으므로 형식만(이름은 DVML013 이 본다)
    internal static bool IsColor(string value)
    {
        var s = value.Trim();
        if (s.Length == 0 || s.Equals("transparent", StringComparison.OrdinalIgnoreCase) || TokenOnly.IsMatch(s))
            return true;
        return s.Length is 4 or 7 or 9 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit);
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

    /// <summary>
    /// Old attribute names (no aliases: the old name is an unknown attribute) — DVML019 names the attribute that replaced it instead of a similar-name guess.
    /// Key = (tag, old name), value = (new name, the meaning is reversed). A copy of the library <c>TagSchema.Renamed</c> (a test compares them).
    /// </summary>
    internal static readonly Dictionary<(string Tag, string Old), (string New, bool Reversed)> Renamed = new()
    {
        [("DvExpander", "header")] = ("title", false),
        [("DvSummaryRow", "title")] = ("label", false),
        [("DvSignal", "width")] = ("thickness", false),
        [("DvColumn", "enabled")] = ("cell-disabled", true),
        [("DvLamp", "blink-when")] = ("blink", false),
        [("DvDataGrid", "selection")] = ("selected-items", false),
    };

    // 뜻이 뒤집힌 것(enabled → cell-disabled)은 그것까지 알린다 — (태그, 옛 이름) 으로 정한다
    private static object RenamedText(string old, (string New, bool Reversed) now) =>
        Diagnostics.Text(now.Reversed ? "Attr_RenamedInverted" : "Attr_Renamed", old, now.New);

    // 가장 가까운 이름(편집 거리 2 이내) — "txet" → "text"
    private static object Suggest(string name, string[] own, string tag)
    {
        var best = own.Concat(Common.Where(c => TakesCommon(tag, c))).Select(k => (k, d: StyleCheck.Distance(name, k))).OrderBy(x => x.d).FirstOrDefault();
        return best.k is not null && best.d <= 2 ? Diagnostics.Text("Style_DidYouMean", best.k) : (object)"";
    }
}
