using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// 공정도(<c>DvProcess</c>·<c>DvUnit</c>) 빌드 때 검사 — 실행 때 Trace 로 알리는 연결 오류를 dvml 안에서 미리 알린다.
/// 빌드 때 확실한 것만: 바인딩(<c>{…}</c>)이 든 값, 포트를 모르는 태그(등록 심볼·컴포넌트 유닛)는 건너뛴다.
/// </summary>
internal static class ProcessCheck
{
    /// <summary>내장 장비의 포트(kind 와 상관없이 같다) — 라이브러리 <c>BuiltInSymbols</c> 와 같아야 한다(테스트가 비교).</summary>
    internal static readonly Dictionary<string, string[]> Ports = new(StringComparer.Ordinal)
    {
        ["DvVessel"] = ["top", "bottom", "side"],
        ["DvPump"] = ["in", "out"],
        ["DvValve"] = ["in", "out"],
        ["DvInline"] = ["in", "out"],
        ["DvValve3"] = ["a", "b", "c"],
        ["DvExchanger"] = ["in", "out", "in2", "out2"],
    };

    /// <summary>내장 장비의 kind(첫 값이 기본) — 라이브러리와 같아야 한다(테스트가 비교).</summary>
    internal static readonly Dictionary<string, string[]> Kinds = new(StringComparer.Ordinal)
    {
        ["DvVessel"] = ["tank", "reactor", "silo", "hopper"],
        ["DvPump"] = ["centrifugal", "gear", "compressor", "blower"],
        ["DvValve"] = ["gate", "ball", "butterfly", "control", "check"],
        ["DvInline"] = ["flowmeter", "filter", "strainer", "sight"],
        ["DvValve3"] = ["ball", "control"],
        ["DvExchanger"] = ["shell", "plate"],
    };

    internal static readonly string[] Media = ["water", "steam", "air", "gas", "oil", "acid"];

    private sealed class Item
    {
        public string Name = "";
        public XElement Element = null!;
        public bool Terminal, Port, Instrument;
        public string[]? Ports; // null = 모름(검사 안 함)
    }

    public static void Check(XElement root, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        foreach (var e in root.DescendantsAndSelf())
        {
            var tag = e.Name.LocalName;
            if (tag is "DvProcess")
            {
                Values(e, report);
                Scope(e, report);
            }
            else if (tag is "DvUnit")
            {
                Scope(e, report);
            }
        }
    }

    private static bool Dynamic(string? v) => v is not null && v.IndexOf('{') >= 0;

    // DvProcess 속성의 값
    private static void Values(XElement process, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        Value(process, "direction", ["lr", "tb"], report);
        Value(process, "look", ["plain", "rich"], report);
        Value(process, "align", ["auto", "step", "row"], report);
    }

    private static void Value(XElement e, string attr, string[] allowed, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = e.Attribute(attr);
        if (a is null || a.Value.Length == 0 || Dynamic(a.Value) || allowed.Contains(a.Value))
            return;
        report(Diagnostics.ProcessUnknownValue, a, [$"<{e.Name.LocalName}> {attr}", a.Value, string.Join("·", allowed)]);
    }

    // 한 공정도(바깥 또는 유닛 안) — 이름은 이 범위 안에서만 보인다
    private static void Scope(XElement container, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var items = new Dictionary<string, Item>(StringComparer.Ordinal);
        var dynamicNames = false;
        foreach (var ch in container.Elements())
        {
            var tag = ch.Name.LocalName;
            if (tag is "DvLine" or "DvSignal" or "DvStyle")
                continue;

            if (Kinds.TryGetValue(tag, out var kinds))
                Value(ch, "kind", kinds, report);
            if (tag is not ("DvPort" or "DvInstrument"))
                Value(ch, "rotate", ["0", "90", "180", "270"], report);

            var nameAttr = ch.Attribute("name");
            if (nameAttr is null || Dynamic(nameAttr.Value))
            {
                dynamicNames |= nameAttr is not null;
                continue; // 이름 없음은 실행 때 오류로 알린다(선으로 이을 수 없을 뿐)
            }

            var item = new Item
            {
                Name = nameAttr.Value,
                Element = ch,
                Terminal = tag == "DvTerminal",
                Port = tag == "DvPort",
                Instrument = tag == "DvInstrument",
                Ports = tag == "DvUnit"
                    ? ch.Elements().Where(x => x.Name.LocalName == "DvPort").Select(x => (string?)x.Attribute("name")).Where(n => n is not null && !Dynamic(n)).Select(n => n!).ToArray()
                    : Ports.TryGetValue(tag, out var p) ? p : null,
            };
            if (tag == "DvUnit" && ch.Elements().Any(x => x.Name.LocalName == "DvPort" && Dynamic((string?)x.Attribute("name"))))
                item.Ports = null;

            if (items.ContainsKey(item.Name))
                report(Diagnostics.ProcessDuplicateName, nameAttr, [item.Name]);
            else
                items[item.Name] = item;
        }

        foreach (var ch in container.Elements())
        {
            switch (ch.Name.LocalName)
            {
                case "DvLine":
                    LineEnd(ch, "from", items, dynamicNames, report);
                    LineEnd(ch, "to", items, dynamicNames, report);
                    Value(ch, "medium", Media, report);
                    break;
                case "DvSignal":
                    SignalEnd(ch, "from", items, dynamicNames, report);
                    SignalEnd(ch, "to", items, dynamicNames, report);
                    break;
                case "DvInstrument":
                {
                    var on = ch.Attribute("on");
                    if (on is null)
                        report(Diagnostics.ProcessNameNotFound, (XObject?)ch.Attribute("name") ?? ch, ["<DvInstrument>", "on", " — on(붙을 장비 이름)은 꼭 씁니다"]);
                    else if (!Dynamic(on.Value) && !dynamicNames && (!items.TryGetValue(on.Value, out var host) || host.Port || host.Instrument))
                        report(Diagnostics.ProcessNameNotFound, on, ["<DvInstrument on>", on.Value, " — 같은 공정도 안의 장비 이름이어야 합니다"]);
                    break;
                }
            }
        }
    }

    private static void LineEnd(XElement line, string attr, Dictionary<string, Item> items, bool dynamicNames, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = line.Attribute(attr);
        if (a is null || Dynamic(a.Value))
            return;
        var raw = a.Value;
        var dot = raw.IndexOf('.');
        var name = dot < 0 ? raw : raw.Substring(0, dot);
        var port = dot < 0 ? null : raw.Substring(dot + 1);
        var where = $"DvLine {attr}='{raw}'";
        if (!items.TryGetValue(name, out var item) || item.Instrument)
        {
            if (!dynamicNames)
                report(Diagnostics.ProcessNameNotFound, a, [where, name, item?.Instrument == true ? " — 계기는 배관으로 잇지 않습니다(DvSignal)" : ""]);
            return;
        }

        if (item.Terminal || item.Port || item.Ports is null)
            return; // 이름만 쓰는 끝(점을 붙이면 실행 때 경고) · 포트를 모르는 장비
        if (item.Element.Name.LocalName == "DvUnit" && port is not null && port.IndexOf('.') >= 0)
        {
            report(Diagnostics.ProcessBadPort, a, [where, $"유닛 안 장비는 바깥에서 직접 잇지 않습니다 — 유닛에 <DvPort> 를 두고 '{name}.포트' 로 잇습니다(지금 포트: {Join(item.Ports)})"]);
            return;
        }

        if (port is null)
        {
            if (item.Ports.Length != 1)
                report(Diagnostics.ProcessBadPort, a, [where, $"'{name}' 은(는) 포트를 골라야 합니다({Join(item.Ports)})"]);
            return;
        }

        if (!item.Ports.Contains(port))
            report(Diagnostics.ProcessBadPort, a, [where, $"'{name}' 에 포트 '{port}' 이(가) 없습니다({Join(item.Ports)})"]);
    }

    private static void SignalEnd(XElement signal, string attr, Dictionary<string, Item> items, bool dynamicNames, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = signal.Attribute(attr);
        if (a is null || Dynamic(a.Value))
            return;
        var where = $"DvSignal {attr}='{a.Value}'";
        if (a.Value.IndexOf('.') >= 0)
        {
            report(Diagnostics.ProcessBadPort, a, [where, "신호선은 포트 없이 계기·장비 이름만 씁니다"]);
            return;
        }

        if (!items.TryGetValue(a.Value, out var item))
        {
            if (!dynamicNames)
                report(Diagnostics.ProcessNameNotFound, a, [where, a.Value, ""]);
            return;
        }

        if (item.Terminal || item.Port)
            report(Diagnostics.ProcessBadPort, a, [where, "끝점(DvTerminal)·유닛 포트(DvPort)는 신호선으로 잇지 않습니다"]);
    }

    private static string Join(string[] ports) => ports.Length == 0 ? "없음" : string.Join("·", ports);
}
