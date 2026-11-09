using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// 공정도(<c>DvProcess</c>·<c>DvUnit</c>) 빌드 때 검사 — 실행 때 Trace 로 알리는 연결 오류를 dvml 안에서 미리 알린다.
/// 빌드 때 확실한 것만: 바인딩(<c>{…}</c>)이 든 값, 포트를 모르는 태그(등록 심볼·컴포넌트 유닛)는 건너뛴다.
/// 이름·참조(name·from·to·on·tap)는 바인딩되지 않는다(3단계 D3) — <c>{</c> 가 있어도 글자 그대로 검사하고 DVML011 로 알린다(실행 때 경고와 같다).
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
        ["DvInline:vent"] = ["in"],  // 포트가 다른 kind 는 "태그:kind" 로
        ["DvInline:blind"] = ["in"],
    };

    /// <summary>내장 장비의 kind(첫 값이 기본) — 라이브러리와 같아야 한다(테스트가 비교).</summary>
    internal static readonly Dictionary<string, string[]> Kinds = new(StringComparer.Ordinal)
    {
        ["DvVessel"] = ["tank", "reactor", "silo", "hopper", "drum", "column", "open"],
        ["DvPump"] = ["centrifugal", "gear", "compressor", "blower", "isa"],
        ["DvValve"] = ["gate", "ball", "butterfly", "control", "check", "globe", "plug", "diaphragm", "angle", "relief"],
        ["DvInline"] = ["flowmeter", "filter", "strainer", "sight", "reducer", "orifice", "mixer", "vent", "blind"],
        ["DvValve3"] = ["ball", "control"],
        ["DvExchanger"] = ["shell", "plate"],
    };

    /// <summary>내장 장비 포트 — kind 별로 다르면 그것, 아니면 태그의 것. 모르면 null(검사 안 함, 바인딩된 kind 포함).</summary>
    internal static string[]? PortsOf(string tag, string? kind)
    {
        if (kind is not null && Dynamic(kind))
            return null;
        if (kind is not null && Ports.TryGetValue(tag + ":" + kind, out var k))
            return k;
        return Ports.TryGetValue(tag, out var p) ? p : null;
    }

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

    // 이름·참조에 { — 바인딩이 아니라 글자 그대로 쓰인다(실행 때 ProcessBuilder.RawName 경고와 같다)
    private static void Literal(XElement e, XAttribute? a, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        if (a is not null && Dynamic(a.Value))
            report(Diagnostics.ProcessLiteralName, a, [$"<{e.Name.LocalName}> {a.Name.LocalName}", a.Value]);
    }

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
        if (a is null || Dynamic(a.Value) || AttributeCheck.IsEnum(a.Value, allowed)) // 라이브러리 목록 값 규칙(앞뒤 공백·대소문자 무시, 빈 값 = 없음)
            return;
        report(Diagnostics.ProcessUnknownValue, a, [$"<{e.Name.LocalName}> {attr}", a.Value, string.Join("·", allowed)]);
    }

    private static bool IsNumber(string s) =>
        double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

    // 좌표 한 개(계기 x·y)
    private static void Coordinate(XElement e, string attr, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = e.Attribute(attr);
        if (a is null || a.Value.Length == 0 || Dynamic(a.Value) || IsNumber(a.Value))
            return;
        report(Diagnostics.ProcessUnknownValue, a, [$"<{e.Name.LocalName}> {attr}", a.Value, "px"]);
    }

    // 신호선 색 — 라이브러리 색 규칙(AttributeCheck.IsColor: #3·6·8자리, transparent, token(이름)). 토큰 이름은 앱 테마에 달렸으므로 형식만
    private static void SignalColor(XElement signal, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = signal.Attribute("color");
        if (a is null || Dynamic(a.Value) || AttributeCheck.IsColor(a.Value))
            return;
        report(Diagnostics.ProcessUnknownValue, a, ["<DvSignal> color", a.Value, "#RRGGBB·token(name)"]);
    }

    // 신호선 두께 — 0 보다 큰 수
    private static void SignalWidth(XElement signal, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = signal.Attribute("width");
        if (a is null || a.Value.Trim().Length == 0 || Dynamic(a.Value))
            return;
        if (!double.TryParse(a.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) || w <= 0)
            report(Diagnostics.ProcessUnknownValue, a, ["<DvSignal> width", a.Value, "px > 0"]);
    }

    // 배관 꺾임점 — "x,y x,y …"
    private static void Via(XElement line, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = line.Attribute("via");
        if (a is null || a.Value.Trim().Length == 0 || Dynamic(a.Value))
            return;
        var ok = a.Value.Split([' '], StringSplitOptions.RemoveEmptyEntries).All(part =>
        {
            var xy = part.Split(',');
            return xy.Length == 2 && IsNumber(xy[0]) && IsNumber(xy[1]);
        });
        if (!ok)
            report(Diagnostics.ProcessUnknownValue, a, ["<DvLine> via", a.Value, "x,y x,y …"]);
    }

    // 한 공정도(바깥 또는 유닛 안) — 이름은 이 범위 안에서만 보인다
    private static void Scope(XElement container, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var items = new Dictionary<string, Item>(StringComparer.Ordinal);
        foreach (var ch in container.Elements())
        {
            var tag = ch.Name.LocalName;
            if (tag is "DvLine" or "DvSignal" or "DvStyle")
                continue;

            if (Kinds.TryGetValue(tag, out var kinds))
                Value(ch, "kind", kinds, report);
            if (tag is not ("DvPort" or "DvInstrument" or "DvTerminal")) // 끝점은 rotate 가 없다(DVML019)
                Value(ch, "rotate", ["0", "90", "180", "270"], report);

            var nameAttr = ch.Attribute("name");
            if (nameAttr is null)
                continue; // 이름 없음은 실행 때 오류로 알린다(선으로 이을 수 없을 뿐)
            Literal(ch, nameAttr, report);

            var item = new Item
            {
                Name = nameAttr.Value,
                Element = ch,
                Terminal = tag == "DvTerminal",
                Port = tag == "DvPort",
                Instrument = tag == "DvInstrument",
                Ports = tag == "DvUnit"
                    ? ch.Elements().Where(x => x.Name.LocalName == "DvPort").Select(x => (string?)x.Attribute("name")).Where(n => n is not null).Select(n => n!).ToArray()
                    : PortsOf(tag, (string?)ch.Attribute("kind")),
            };

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
                    LineEnd(ch, "from", items, report);
                    LineEnd(ch, "to", items, report);
                    Value(ch, "medium", Media, report);
                    Via(ch, report);
                    break;
                case "DvSignal":
                    SignalEnd(ch, "from", items, report);
                    SignalEnd(ch, "to", items, report);
                    Value(ch, "kind", ["electric", "pneumatic"], report);
                    SignalColor(ch, report);
                    SignalWidth(ch, report);
                    break;
                case "DvInstrument":
                {
                    var on = ch.Attribute("on");
                    if (ch.Attribute("tap") is not null)
                        LineEnd(ch, "tap", items, report); // 연결선이 있으면 그 장비에 붙는다(on 은 없어도 된다)
                    else if (on is null)
                        report(Diagnostics.ProcessNameNotFound, (XObject?)ch.Attribute("name") ?? ch, ["<DvInstrument>", "on", Diagnostics.Text("Proc_InstrumentOnRequired")]);
                    else
                    {
                        Literal(ch, on, report);
                        if (!items.TryGetValue(on.Value, out var host) || host.Port || host.Instrument)
                            report(Diagnostics.ProcessNameNotFound, on, ["<DvInstrument on>", on.Value, Diagnostics.Text("Proc_InstrumentOnEquipment")]);
                    }
                    Coordinate(ch, "x", report);
                    Coordinate(ch, "y", report);
                    break;
                }
            }
        }
    }

    private static void LineEnd(XElement line, string attr, Dictionary<string, Item> items, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = line.Attribute(attr);
        if (a is null)
            return;
        Literal(line, a, report);
        var raw = a.Value;
        var dot = raw.IndexOf('.');
        var name = dot < 0 ? raw : raw.Substring(0, dot);
        var port = dot < 0 ? null : raw.Substring(dot + 1);
        var where = $"{line.Name.LocalName} {attr}='{raw}'";
        if (!items.TryGetValue(name, out var item) || item.Instrument)
        {
            report(Diagnostics.ProcessNameNotFound, a, [where, name, item?.Instrument == true ? Diagnostics.Text("Proc_InstrumentNotPiped") : (object)""]);
            return;
        }

        if (item.Terminal || item.Port || item.Ports is null)
            return; // 이름만 쓰는 끝(점을 붙이면 실행 때 경고) · 포트를 모르는 장비
        if (item.Element.Name.LocalName == "DvUnit" && port is not null && port.IndexOf('.') >= 0)
        {
            report(Diagnostics.ProcessBadPort, a, [where, Diagnostics.Text("Proc_UnitInnerDirect", name, Join(item.Ports))]);
            return;
        }

        if (port is not null && port.IndexOf('@') >= 0 && item.Element.Name.LocalName != "DvUnit")
        {
            // 면 위 자리(left@0.85) — 면 이름과 0~1 위치(범위 밖은 실행 때 경고하고 맞춘다)
            var at = port.IndexOf('@');
            if (port.Substring(0, at) is not ("top" or "bottom" or "left" or "right"))
                report(Diagnostics.ProcessBadPort, a, [where, Diagnostics.Text("Proc_BadFace", port.Substring(0, at))]);
            else if (!float.TryParse(port.Substring(at + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                report(Diagnostics.ProcessBadPort, a, [where, Diagnostics.Text("Proc_BadFaceAt", port.Substring(at + 1))]);
            return;
        }

        if (port is null)
        {
            if (item.Ports.Length != 1)
                report(Diagnostics.ProcessBadPort, a, [where, Diagnostics.Text("Proc_ChoosePort", name, Join(item.Ports))]);
            return;
        }

        if (!item.Ports.Contains(port))
            report(Diagnostics.ProcessBadPort, a, [where, Diagnostics.Text("Proc_NoSuchPort", name, port, Join(item.Ports))]);
    }

    private static void SignalEnd(XElement signal, string attr, Dictionary<string, Item> items, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        var a = signal.Attribute(attr);
        if (a is null)
            return;
        Literal(signal, a, report);
        var where = $"DvSignal {attr}='{a.Value}'";
        if (a.Value.IndexOf('.') >= 0)
        {
            report(Diagnostics.ProcessBadPort, a, [where, Diagnostics.Text("Proc_SignalNoPort")]);
            return;
        }

        if (!items.TryGetValue(a.Value, out var item))
        {
            report(Diagnostics.ProcessNameNotFound, a, [where, a.Value, ""]);
            return;
        }

        if (item.Terminal || item.Port)
            report(Diagnostics.ProcessBadPort, a, [where, Diagnostics.Text("Proc_SignalNoTerminal")]);
    }

    private static string Join(string[] ports) => ports.Length == 0 ? "-" : string.Join("·", ports);
}
