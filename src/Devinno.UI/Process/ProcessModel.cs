using System.Globalization;
using System.Runtime.CompilerServices;
using Devinno.UI.Binding;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Process;

/// <summary>One process diagram (<c>DvProcess</c>): equipment, lines and the layout result. Attached to <see cref="LayoutBox.Process"/>.</summary>
internal sealed class ProcessDiagram
{
    public bool Vertical;
    public bool Rich;
    public List<ProcessNode> Nodes { get; } = new();
    public List<ProcessLine> Lines { get; } = new();

    /// <summary>Instruments (<c>DvInstrument</c>): not part of the layout; placed beside the equipment they attach to (<c>on</c>).</summary>
    public List<ProcessInstrument> Instruments { get; } = new();

    /// <summary>Signal lines (<c>DvSignal</c>): drawn after all pipes are drawn. No effect on layout or flow.</summary>
    public List<ProcessSignal> Signals { get; } = new();

    /// <summary>The layout result (including the natural size at scale 1). <see langword="null"/> before layout.</summary>
    public ProcessGraph? Graph;

    /// <summary>Content origin (screen px), added to layout result coordinates when drawing.</summary>
    public SKPoint Origin;

    /// <summary>The warning message if the area was too small in the last layout.</summary>
    public string? Shortage;

    /// <summary>Pipe spacing (center to center); wider for rich, because its pipes are thick.</summary>
    public float Pitch => Rich ? 16f : 12f;

    internal ElementNode Source = null!;

    /// <summary>The inner process diagram of a unit (<c>DvUnit</c>): ports are fixed to the border, and size = the border.</summary>
    public bool IsUnit;

    /// <summary><c>flow-animation</c>: a moving dashed line over flowing pipes (off by default, ISA-101). Units follow the outer setting.</summary>
    public bool FlowAnimation;

    /// <summary>Whether this process diagram (including inside units) currently has a flowing pipe.</summary>
    internal bool AnyFlowing => Lines.Any(l => l.Flowing) || Nodes.Any(n => n.Unit?.AnyFlowing == true);

    /// <summary><c>align</c>: null = auto (staircase if a staircase fits the area, otherwise a single line). Units follow the outer setting.</summary>
    public ProcessAlign? Align;
}

/// <summary>One piece of equipment in a process diagram.</summary>
internal sealed class ProcessNode
{
    public required string Name;
    public required string Tag;
    public SymbolDef? Def;
    public bool Terminal;
    public string? Text;
    public string? Label;
    public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);
    public int Rotate;
    public bool FlipH, FlipV;
    public int? Row, Rank;
    public List<(SymbolDef Def, bool Running)> Accessories { get; } = new();
    public List<(PortSide Side, LayoutBox Box)> Attach { get; } = new();

    /// <summary>Instruments attached to this equipment (<c>DvInstrument on="이 장비"</c>), placed side by side on a free side within its footprint.</summary>
    public List<ProcessInstrument> Instruments { get; } = new();
    public LayoutBox Box = null!;
    public ProcessDiagram Owner = null!;

    // 차지 영역 기준 기하
    public SKRect Sym, Body, LabelRect;
    public SKMatrix Transform = SKMatrix.Identity; // 심볼 좌표 → 차지 영역 기준
    public List<(SKRect Rect, SymbolDef Def, bool Running)> AccessoryRects { get; } = new();
    public List<SKRect> AttachRects { get; } = new();
    public SKSize Footprint;

    /// <summary>Direction of an end point: true if it is the start of a line (coming in from outside), false if it is the end (going out).</summary>
    public bool TerminalSource;

    public bool Error;

    /// <summary>The inner process diagram if this is a unit (this equipment = one unit); otherwise <see langword="null"/>.</summary>
    public ProcessDiagram? Unit;

    /// <summary>Ports (<c>DvPort</c>) a unit exposes to the outside; outer lines connect only here, as <c>유닛.포트</c> (unit.port).</summary>
    public List<SymbolPort>? UnitPorts;

    /// <summary>Positions of the outer ports (<c>DvPort</c>) inside a unit; inner lines connect to them by name alone.</summary>
    public bool IsPort;
    public PortSide PortSide;
    public float PortAt = 0.5f;

    internal GraphNode G = null!;

    public bool IsVessel => Tag == "DvVessel";

    /// <summary>Equipment that lines connect to by name alone, without a port (end points and port positions inside a unit).</summary>
    public bool NameOnly => Terminal || IsPort;

    public IReadOnlyList<SymbolPort> Ports => (IReadOnlyList<SymbolPort>?)Def?.Ports ?? UnitPorts ?? [];

    public float Level => Values.GetValueOrDefault(Def?.FillValue ?? "level") is { } v && TryFloat(v, out var f) ? Math.Clamp(f, 0, 100) : 0;

    public bool On(string? param) => param is not null && Values.GetValueOrDefault(param) is { } v && Truthy(v);

    internal static bool Truthy(object v) => v switch
    {
        bool b => b,
        _ when TryFloat(v, out var f) => f > 0,
        _ => string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase),
    };

    internal static bool TryFloat(object v, out float f)
    {
        switch (v)
        {
            case float x: f = x; return true;
            case double d: f = (float)d; return true;
            case int i: f = i; return true;
            case long l: f = l; return true;
            case decimal m: f = (float)m; return true;
            default:
                return float.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }
    }
}

/// <summary>One line in a process diagram (<c>DvLine</c>).</summary>
internal sealed class ProcessLine
{
    public required string FromRef, ToRef;
    public int FromNode = -1, ToNode = -1;
    public string? FromPort, ToPort;
    public bool ArrowEnd, ArrowStart;
    public bool Error;

    /// <summary><c>flow</c> override (the actual flow known to the app). <see langword="null"/> if absent, meaning it is determined automatically.</summary>
    public bool? FlowOverride;

    /// <summary>Result: whether it is currently flowing (<see cref="ProcessFlow"/>).</summary>
    public bool Flowing;

    /// <summary>The medium specified in <c>medium</c> (one of <see cref="ProcessMedium.Names"/>). <see langword="null"/> if absent.</summary>
    public string? MediumSet;

    /// <summary>Result: the medium to paint (the one specified, or one propagated from a preceding line, <see cref="ProcessMedium"/>). <see langword="null"/> if none = default gray.</summary>
    public string? Medium;
    internal GraphLine? G;
}

/// <summary>
/// One instrument (<c>DvInstrument</c>): an ISA-5.1 bubble (<see cref="Letters"/> in the upper half of the circle, <see cref="Number"/> in the lower half, and a horizontal center line if <see cref="Panel"/>).
/// It is not part of the pipe flow layout; it goes into the footprint of the <see cref="Host"/> equipment and is placed on a free side next to it (pipes route around it).
/// </summary>
internal sealed class ProcessInstrument
{
    public required string Name;
    public string? Letters, Number, Value, OnRef;
    public bool Panel;
    public LayoutBox Box = null!;
    public ProcessDiagram Owner = null!;

    /// <summary>The equipment it is attached to. <see langword="null"/> if <c>on</c> is missing or wrong (not drawn).</summary>
    public ProcessNode? Host;

    /// <summary>Position of the bubble and value text, relative to the equipment's footprint.</summary>
    public SKRect Rect, ValueRect;
}

/// <summary>One signal line (<c>DvSignal</c>); both ends are names only (instruments or equipment).</summary>
internal sealed class ProcessSignal
{
    public required string FromRef, ToRef;
    public bool Error;
    internal GraphSignal? G;
}

/// <summary>
/// Markup → process diagram model, equipment geometry, layout computation (results are reused when the structure is the same), and on-screen placement.
/// </summary>
internal static class ProcessBuilder
{
    public const float LabelSize = 13f;
    private const float AttachGap = 8f;

    private static readonly HashSet<string> Warned = new();

    internal static void Warn(string message, bool error = false)
    {
        lock (Warned)
        {
            if (!Warned.Add(message))
                return;
        }

        if (error)
            System.Diagnostics.Trace.TraceError("Devinno.UI process: " + message);
        else
            System.Diagnostics.Trace.TraceWarning("Devinno.UI process: " + message);
    }

    /// <summary>Box factory passed in by the engine; builds a box from a context (the screen VM or a component parameter scope).</summary>
    internal sealed record Builders(
        Func<ElementNode, object?, LayoutBox> Self,
        Func<ElementNode, object?, LayoutBox?> Child,
        Func<ElementNode, object?, (ElementNode Unit, object? Scope)?> ExpandUnit);

    /// <summary>
    /// Builds the model from the children of <c>DvProcess</c>. Creates a box for each piece of equipment and adds it as a child of <paramref name="box"/> (so presses and onclick work like a normal control),
    /// and also builds the content inside <c>DvAttach</c> as normal boxes. For a unit (<c>DvUnit</c>, or a component whose template root is <c>DvUnit</c>),
    /// creates a unit box and puts the inner equipment boxes inside it.
    /// </summary>
    public static void Build(LayoutBox box, object? vm, ComponentRegistry? registry, Builders b)
    {
        var node = box.Node;
        var d = new ProcessDiagram
        {
            Source = node,
            Vertical = Direction(node, vm),
            Rich = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("look")) is "rich",
        };
        if (BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("look")) is { } look && look is not ("plain" or "rich"))
            Warn($"DvProcess look='{look}' is not a valid value (plain, rich); drawing as plain");
        d.Align = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("align")) switch
        {
            null or "" or "auto" => null,
            "step" => ProcessAlign.Step,
            "row" => ProcessAlign.Row,
            var a => WarnAlign(a),
        };

        d.FlowAnimation = BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("flow-animation")) is { } fa && ProcessNode.Truthy(fa);
        Fill(d, box, node, vm, registry, b);
        ProcessFlow.Compute(d);
        ProcessMedium.Compute(d);
        box.Process = d;
    }

    private static string? Medium(string? m)
    {
        if (string.IsNullOrEmpty(m))
            return null;
        if (ProcessMedium.Names.Contains(m))
            return m;
        Warn($"DvLine medium='{m}' is not a known medium ({string.Join(", ", ProcessMedium.Names)}); drawing with the default color");
        return null;
    }

    private static ProcessAlign? WarnAlign(string a)
    {
        Warn($"DvProcess align='{a}' is not a valid value (auto, step, row); laying out as auto");
        return null;
    }

    private static bool Direction(ElementNode node, object? vm)
    {
        var dir = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("direction"));
        if (dir is not (null or "" or "lr" or "tb"))
            Warn($"<{node.Tag}> direction='{dir}' is not a valid value (lr, tb); drawing as lr");
        return dir is "tb";
    }

    // 한 공정도(바깥 또는 유닛 안)의 자식 — 장비 상자·붙은 칸 상자는 owner 의 자식으로
    private static void Fill(ProcessDiagram d, LayoutBox owner, ElementNode container, object? vm, ComponentRegistry? registry, Builders b)
    {
        var attachBoxes = new List<LayoutBox>();
        var instruments = new List<(ProcessInstrument I, ElementNode Node)>();
        foreach (var ch in container.Children)
        {
            if (ch.Tag == "DvLine")
            {
                d.Lines.Add(new ProcessLine
                {
                    FromRef = ch.Attributes.GetValueOrDefault("from") ?? "",
                    ToRef = ch.Attributes.GetValueOrDefault("to") ?? "",
                    ArrowEnd = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("arrow")) is "end" or "both",
                    ArrowStart = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("arrow")) is "start" or "both",
                    FlowOverride = ch.Attributes.TryGetValue("flow", out var flowRaw) && BindingResolver.ResolveValueOrLiteral(vm, flowRaw) is { } fv
                        ? ProcessNode.Truthy(fv)
                        : null,
                    MediumSet = Medium(BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("medium"))),
                });
                continue;
            }

            if (ch.Tag == "DvSignal")
            {
                d.Signals.Add(new ProcessSignal
                {
                    FromRef = ch.Attributes.GetValueOrDefault("from") ?? "",
                    ToRef = ch.Attributes.GetValueOrDefault("to") ?? "",
                });
                continue;
            }

            if (ch.Tag == "DvStyle" || !Visible(ch, vm))
                continue;

            if (ch.Tag == "DvInstrument")
            {
                instruments.Add((new ProcessInstrument
                {
                    Owner = d,
                    Name = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("name")) ?? "",
                    Letters = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("tag")),
                    Number = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("number")),
                    Value = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("value")),
                    OnRef = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("on")),
                    Panel = BindingResolver.ResolveValueOrLiteral(vm, ch.Attributes.GetValueOrDefault("panel")) is { } pv && ProcessNode.Truthy(pv),
                }, ch));
                continue;
            }

            if (ch.Tag == "DvPort")
            {
                if (!d.IsUnit)
                {
                    Warn("<DvPort> can only be used inside <DvUnit> (port outside a unit); ignored", true);
                    continue;
                }

                var side = SymbolDef.ParseSide(BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("side")));
                if (side is null)
                    Warn($"<DvPort name='{ch.Attributes.GetValueOrDefault("name")}'> side must be one of left, top, right, bottom; using right", true);
                var atText = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("at"));
                var at = 0.5f;
                if (atText is { Length: > 0 } && (!float.TryParse(atText, NumberStyles.Float, CultureInfo.InvariantCulture, out at) || at < 0 || at > 1))
                {
                    Warn($"<DvPort name='{ch.Attributes.GetValueOrDefault("name")}'> at='{atText}' must be between 0 and 1; using 0.5", true);
                    at = 0.5f;
                }

                d.Nodes.Add(new ProcessNode
                {
                    Owner = d, Name = UniqueName(d, ch, vm), Tag = "DvPort", IsPort = true, PortSide = side ?? PortSide.Right, PortAt = at,
                });
                continue;
            }

            // 유닛 — DvUnit 그대로, 또는 템플릿 루트가 DvUnit 인 컴포넌트 인스턴스
            (ElementNode Unit, object? Scope)? unit = ch.Tag == "DvUnit" ? (ch, vm) : b.ExpandUnit(ch, vm);
            if (unit is { } u)
            {
                if (d.IsUnit)
                {
                    Warn($"<{ch.Tag}>: a unit cannot be placed inside another unit; not drawn", true);
                    continue;
                }

                AddUnit(d, owner, ch, u.Unit, u.Scope, vm, registry, b);
                continue;
            }

            var def = ResolveDef(ch, vm, registry, out var known);
            if (!known)
            {
                Warn($"<{ch.Tag}> is not process equipment (DvVessel, DvPump, DvValve, DvInline, DvTerminal, DvUnit, DvInstrument, a registered DvSymbol, or a DvUnit component); not drawn", true);
                continue;
            }

            var pn = new ProcessNode
            {
                Owner = d,
                Name = UniqueName(d, ch, vm),
                Tag = ch.Tag,
                Def = def,
                Terminal = ch.Tag == "DvTerminal",
                Text = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("text")),
                Label = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("label")),
                Rotate = ParseRotate(BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("rotate"))),
                Row = Int(BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("row"))),
                Rank = Int(BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("rank"))),
            };
            var flip = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("flip"));
            pn.FlipH = flip is "h" or "hv" or "vh";
            pn.FlipV = flip is "v" or "hv" or "vh";
            if (def is not null)
            {
                foreach (var p in def.Params)
                    pn.Values[p.Name] = ch.Attributes.TryGetValue(p.Name, out var raw) ? BindingResolver.ResolveValueOrLiteral(vm, raw) : p.Default;
            }

            pn.Box = b.Self(ch, vm);
            pn.Box.Equipment = pn;
            owner.Children.Add(pn.Box);

            foreach (var sub in ch.Children)
            {
                if (!Visible(sub, vm))
                    continue;
                if (BuiltInSymbols.IsAccessoryTag(sub.Tag))
                {
                    var running = sub.Attributes.TryGetValue("running", out var rr) &&
                                  BindingResolver.ResolveValueOrLiteral(vm, rr) is { } rv && ProcessNode.Truthy(rv);
                    pn.Accessories.Add((BuiltInSymbols.Get(sub.Tag, null)!, running));
                }
                else if (sub.Tag == "DvAttach")
                {
                    var side = SymbolDef.ParseSide(BindingResolver.ResolveText(vm, sub.Attributes.GetValueOrDefault("side"))) ?? PortSide.Right;
                    if (b.Child(sub, vm) is { } ab)
                    {
                        pn.Attach.Add((side, ab));
                        attachBoxes.Add(ab);
                    }
                }
                else
                {
                    Warn($"<{sub.Tag}> is not allowed inside <{ch.Tag}> (allowed: DvAttach, DvAgitator, DvMotor)", true);
                }
            }

            d.Nodes.Add(pn);
        }

        owner.Children.AddRange(attachBoxes);
        ResolveLines(d);
        ResolveInstruments(d, owner, instruments, vm, b);
        ResolveSignals(d);
    }

    // 계기 — 이름 확인, on 장비 찾기(장비가 뒤에 선언돼도 된다), 상자 만들기. on 이 틀리면 그리지 않는다
    private static void ResolveInstruments(ProcessDiagram d, LayoutBox owner, List<(ProcessInstrument I, ElementNode Node)> list, object? vm, Builders b)
    {
        foreach (var (inst, node) in list)
        {
            if (inst.Name.Length == 0)
            {
                Warn("<DvInstrument> has no name; it cannot be connected by signal lines", true);
                inst.Name = $"#i{d.Instruments.Count}";
            }
            else if (d.Nodes.Any(n => n.Name == inst.Name) || d.Instruments.Any(x => x.Name == inst.Name))
            {
                Warn($"Name '{inst.Name}' is used twice (equipment and instrument names must be unique within a diagram); this instrument cannot be connected by signal lines", true);
                inst.Name = $"{inst.Name}#i{d.Instruments.Count}";
            }

            var host = string.IsNullOrEmpty(inst.OnRef) ? null : d.Nodes.FirstOrDefault(n => n.Name == inst.OnRef && !n.IsPort);
            if (host is null)
            {
                Warn(string.IsNullOrEmpty(inst.OnRef)
                    ? $"<DvInstrument name='{inst.Name}'> has no on (name of the equipment to attach to); not drawn"
                    : $"<DvInstrument name='{inst.Name}'> on='{inst.OnRef}': no such equipment; not drawn", true);
                d.Instruments.Add(inst);
                continue;
            }

            inst.Host = host;
            inst.Box = b.Self(node, vm);
            inst.Box.Instrument = inst;
            owner.Children.Add(inst.Box);
            host.Instruments.Add(inst);
            d.Instruments.Add(inst);
        }
    }

    // 신호선 양 끝 — 계기 이름 또는 장비 이름(포트 없이)
    private static void ResolveSignals(ProcessDiagram d)
    {
        foreach (var s in d.Signals)
        {
            foreach (var raw in new[] { s.FromRef, s.ToRef })
            {
                if (SignalTarget(d, raw) is not null)
                    continue;
                Warn(raw.Contains('.')
                    ? $"DvSignal '{s.FromRef}'→'{s.ToRef}': signal lines use names only, without ports ('{raw[..raw.IndexOf('.')]}')"
                    : $"DvSignal '{s.FromRef}'→'{s.ToRef}': no instrument or equipment named '{raw}'", true);
                s.Error = true;
            }
        }
    }

    /// <summary>Signal line end name → an instrument (one that is drawn) or equipment (excluding end points and unit port positions). <see langword="null"/> if none.</summary>
    internal static object? SignalTarget(ProcessDiagram d, string name)
    {
        if (d.Instruments.FirstOrDefault(x => x.Name == name) is { Host: not null } inst)
            return inst;
        return d.Nodes.FirstOrDefault(n => n.Name == name && !n.NameOnly);
    }

    // 유닛 하나 — 이름·힌트는 인스턴스(바깥 VM 기준), 방향·제목·안쪽은 유닛 노드(유닛 범위 기준)
    private static void AddUnit(ProcessDiagram d, LayoutBox owner, ElementNode instance, ElementNode unitNode, object? scope,
        object? outerVm, ComponentRegistry? registry, Builders b)
    {
        string? Attr(string name) =>
            instance.Attributes.ContainsKey(name) ? BindingResolver.ResolveText(outerVm, instance.Attributes[name])
            : BindingResolver.ResolveText(scope, unitNode.Attributes.GetValueOrDefault(name));

        var inner = new ProcessDiagram { Source = unitNode, Vertical = Direction(unitNode, scope), Rich = d.Rich, IsUnit = true, FlowAnimation = d.FlowAnimation };
        var pn = new ProcessNode
        {
            Owner = d,
            Name = UniqueName(d, instance, outerVm),
            Tag = "DvUnit",
            Label = Attr("label"),
            Row = Int(Attr("row")),
            Rank = Int(Attr("rank")),
            Unit = inner,
        };

        // 유닛 상자(테두리·배경 style, onclick) — 안쪽 장비 상자는 그 자식
        pn.Box = b.Self(unitNode, scope);
        pn.Box.Equipment = pn;
        owner.Children.Add(pn.Box);

        Fill(inner, pn.Box, unitNode, scope, registry, b);
        pn.UnitPorts = inner.Nodes.Where(x => x.IsPort).Select(x => new SymbolPort(x.Name, x.PortSide, x.PortAt, "inout")).ToList();
        d.Nodes.Add(pn);
    }

    private static string UniqueName(ProcessDiagram d, ElementNode ch, object? vm)
    {
        var name = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("name"));
        if (string.IsNullOrEmpty(name))
        {
            Warn($"<{ch.Tag}> has no name; it cannot be connected by lines", true);
            return $"#{d.Nodes.Count}";
        }

        if (d.Nodes.Any(n => n.Name == name))
        {
            Warn($"Equipment name '{name}' is used twice; the later one cannot be connected by lines", true);
            return $"{name}#{d.Nodes.Count}";
        }

        return name;
    }

    private static bool Visible(ElementNode n, object? vm)
    {
        if (!n.Attributes.TryGetValue("visible", out var raw))
            return true;
        var v = BindingResolver.ResolveValueOrLiteral(vm, raw);
        return v is not null && ProcessNode.Truthy(v) || v is string s && s.Length > 0 && s != "false" && s != "0";
    }

    private static SymbolDef? ResolveDef(ElementNode ch, object? vm, ComponentRegistry? registry, out bool known)
    {
        known = true;
        if (ch.Tag == "DvTerminal")
            return null;
        if (BuiltInSymbols.Kinds.TryGetValue(ch.Tag, out var kinds))
        {
            var kind = BindingResolver.ResolveText(vm, ch.Attributes.GetValueOrDefault("kind"));
            if (BuiltInSymbols.Get(ch.Tag, kind) is { } def)
                return def;
            Warn($"<{ch.Tag} kind='{kind}'> is not a known kind ({string.Join(", ", kinds)}); drawing as {kinds[0]}");
            return BuiltInSymbols.Get(ch.Tag, null);
        }

        if (registry is not null && registry.TryGetSymbol(ch.Tag, out var user) && user is not null)
            return user;
        known = false;
        return null;
    }

    private static int ParseRotate(string? s) => s switch
    {
        null or "" or "0" => 0,
        "90" => 90,
        "180" => 180,
        "270" => 270,
        _ => WarnRotate(s),
    };

    private static int WarnRotate(string s)
    {
        Warn($"rotate='{s}' is not a valid value (0, 90, 180, 270); drawing as 0");
        return 0;
    }

    private static int? Int(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    // from/to 참조 확인 — "장비.포트", 포트가 하나뿐인 장비(끝점)는 "장비" 만
    private static void ResolveLines(ProcessDiagram d)
    {
        foreach (var l in d.Lines)
        {
            (l.FromNode, l.FromPort) = Ref(d, l.FromRef, l);
            (l.ToNode, l.ToPort) = Ref(d, l.ToRef, l);
            if (l.FromNode >= 0 && d.Nodes[l.FromNode].Terminal)
                d.Nodes[l.FromNode].TerminalSource = true;
        }
    }

    private static (int node, string? port) Ref(ProcessDiagram d, string raw, ProcessLine line)
    {
        var dot = raw.IndexOf('.');
        var name = dot < 0 ? raw : raw[..dot];
        var port = dot < 0 ? null : raw[(dot + 1)..];
        var idx = d.Nodes.FindIndex(n => n.Name == name);
        if (idx < 0)
        {
            Warn($"DvLine '{line.FromRef}'→'{line.ToRef}': no equipment named '{name}'", true);
            line.Error = true;
            return (-1, null);
        }

        var n = d.Nodes[idx];
        if (n.NameOnly)
        {
            if (port is not null)
                Warn(n.IsPort
                    ? $"DvLine '{raw}': a unit port (DvPort) is written by name only, as '{name}'"
                    : $"DvLine '{raw}': an endpoint (DvTerminal) is written without a port, as '{name}'");
            return (idx, "end");
        }

        if (n.Unit is not null && port is not null && port.Contains('.'))
        {
            Warn($"DvLine '{raw}': equipment inside a unit cannot be connected directly from outside. Add <DvPort name=\"…\" side=\"…\"/> to the unit, connect to that port inside, then connect from outside as '{name}.<port>' (current ports: {string.Join(", ", n.Ports.Select(p => p.Name))})", true);
            line.Error = true;
            n.Error = true;
            return (idx, null);
        }

        var ports = n.Ports;
        if (port is null)
        {
            if (ports.Count == 1)
                return (idx, ports[0].Name);
            Warn($"DvLine '{raw}': '{name}' requires a port ({string.Join(", ", ports.Select(p => p.Name))})", true);
            line.Error = true;
            n.Error = true;
            return (idx, null);
        }

        if (ports.All(p => p.Name != port))
        {
            Warn($"DvLine '{raw}': '{name}' has no port '{port}' ({string.Join(", ", ports.Select(p => p.Name))})", true);
            line.Error = true;
            n.Error = true;
            return (idx, null);
        }

        return (idx, port);
    }

    // ── 장비 기하(차지 영역 기준) ──

    private static SKMatrix SymbolTransform(SymbolDef def, ProcessNode n, out SKSize size)
    {
        float w = def.Width, h = def.Height;
        var m = SKMatrix.Identity;
        if (n.FlipH)
            m = m.PostConcat(new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1));
        if (n.FlipV)
            m = m.PostConcat(new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1));
        switch (n.Rotate)
        {
            case 90:
                m = m.PostConcat(new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1));
                size = new SKSize(h, w);
                break;
            case 180:
                m = m.PostConcat(new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1));
                size = new SKSize(w, h);
                break;
            case 270:
                m = m.PostConcat(new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1));
                size = new SKSize(h, w);
                break;
            default:
                size = new SKSize(w, h);
                break;
        }

        return m;
    }

    internal static PortSide MapSide(PortSide s, ProcessNode n)
    {
        if (n.FlipH && s is PortSide.Left or PortSide.Right)
            s = s == PortSide.Left ? PortSide.Right : PortSide.Left;
        if (n.FlipV && s is PortSide.Top or PortSide.Bottom)
            s = s == PortSide.Top ? PortSide.Bottom : PortSide.Top;
        for (var r = 0; r < n.Rotate; r += 90)
            s = (PortSide)(((int)s + 1) % 4);
        return s;
    }

    /// <summary>End point shape (depending on the diagram direction and start/end): the path and ports relative to the footprint.</summary>
    internal static (SKPath Path, SKPoint Port, PortSide Side) TerminalShape(ProcessNode n, bool vertical)
    {
        var r = n.Sym;
        var path = new SKPath();
        if (!vertical)
        {
            path.MoveTo(r.Left, r.Top);
            path.LineTo(r.Right - 14, r.Top);
            path.LineTo(r.Right, r.MidY);
            path.LineTo(r.Right - 14, r.Bottom);
            path.LineTo(r.Left, r.Bottom);
            path.Close();
            return n.TerminalSource ? (path, new SKPoint(r.Right, r.MidY), PortSide.Right) : (path, new SKPoint(r.Left, r.MidY), PortSide.Left);
        }

        path.MoveTo(r.Left, r.Top);
        path.LineTo(r.Right, r.Top);
        path.LineTo(r.Right, r.Bottom - 10);
        path.LineTo(r.MidX, r.Bottom);
        path.LineTo(r.Left, r.Bottom - 10);
        path.Close();
        return n.TerminalSource ? (path, new SKPoint(r.MidX, r.Bottom), PortSide.Bottom) : (path, new SKPoint(r.MidX, r.Top), PortSide.Top);
    }

    private static void Geometry(ProcessDiagram d, ProcessNode n, string? family, float spacing, ProcessAlign align)
    {
        if (n.IsPort)
        {
            // 유닛 포트 자리 — 크기 0, 배치 뒤 테두리에 고정
            n.Sym = n.Body = n.LabelRect = SKRect.Empty;
            n.Footprint = SKSize.Empty;
            n.G = new GraphNode { Pinned = true, PinSide = n.PortSide, PinAt = n.PortAt };
            return;
        }

        // 심볼 영역(0,0 기준으로 먼저 만들고 마지막에 차지 영역 기준으로 옮긴다)
        SKSize symSize;
        if (n.Unit is { } unit)
        {
            symSize = Layout(unit, family, spacing, align).Size; // 유닛 = 안쪽 배치 크기(테두리), 바깥과 같은 줄 맞춤
            n.Transform = SKMatrix.Identity;
        }
        else if (n.Terminal)
        {
            using var f = TextFont.Create(n.Text ?? "", 12, true, family);
            var tw = f.MeasureText(n.Text ?? "");
            symSize = d.Vertical ? new SKSize(MathF.Max(60, tw + 20), 38) : new SKSize(MathF.Max(64, tw + 34), 28);
            n.Transform = SKMatrix.Identity;
        }
        else
        {
            n.Transform = SymbolTransform(n.Def!, n, out symSize);
        }

        var sym = SKRect.Create(0, 0, symSize.Width, symSize.Height);
        var body = sym;

        // 부속 — 심볼 위 가운데(교반기는 아래쪽이 용기 안으로 들어간다)
        n.AccessoryRects.Clear();
        var top = sym.Top;
        foreach (var (def, running) in n.Accessories)
        {
            var above = def.Name == "agitator" ? 30f : def.Height;
            var r = SKRect.Create(sym.MidX - def.Width / 2, top - above, def.Width, def.Height);
            n.AccessoryRects.Add((r, def, running));
            body = SKRect.Union(body, new SKRect(r.Left, r.Top, r.Right, top));
            top -= above;
        }

        // 이름표 — 용기는 몸통 안 가운데, 그 밖은 포트를 쓰지 않는 면(아래 → 위 → 오른쪽 → 왼쪽, 다 쓰면 아래)
        n.LabelRect = SKRect.Empty;
        PortSide? labelSide = null;
        var extras = new List<SKRect>();
        if (!string.IsNullOrEmpty(n.Label) && !n.Terminal && !n.IsVessel)
        {
            using var f = TextFont.Create(n.Label, LabelSize, true, family);
            var lw = f.MeasureText(n.Label);
            var lh = f.Spacing;
            var used = n.Unit is not null ? new HashSet<PortSide> { PortSide.Bottom } : UsedSides(d, n); // 유닛 이름은 제목(위)
            var side = new[] { PortSide.Bottom, PortSide.Top, PortSide.Right, PortSide.Left }.FirstOrDefault(sd => !used.Contains(sd), PortSide.Bottom);
            labelSide = side;
            n.LabelRect = side switch
            {
                PortSide.Top when n.Unit is not null => SKRect.Create(sym.Left, body.Top - 4 - lh, lw, lh), // 유닛 제목 = 왼쪽 위
                PortSide.Top => SKRect.Create(sym.MidX - lw / 2, body.Top - 4 - lh, lw, lh),
                PortSide.Right => SKRect.Create(sym.Right + 6, sym.MidY - lh / 2, lw, lh),
                PortSide.Left => SKRect.Create(sym.Left - 6 - lw, sym.MidY - lh / 2, lw, lh),
                _ => SKRect.Create(sym.MidX - lw / 2, sym.Bottom + 4, lw, lh),
            };
            n.LabelRect = ClearOfPorts(n, side, n.LabelRect);
            extras.Add(n.LabelRect);
        }

        // 붙은 칸
        n.AttachRects.Clear();
        var below = MathF.Max(sym.Bottom, n.LabelRect.IsEmpty ? sym.Bottom : n.LabelRect.Bottom);
        var aboveTop = MathF.Min(body.Top, n.LabelRect.IsEmpty ? body.Top : n.LabelRect.Top);
        foreach (var (side, ab) in n.Attach)
        {
            var (w, h) = LayoutEngine.MeasureBox(ab);
            var r = side switch
            {
                PortSide.Left => SKRect.Create(sym.Left - AttachGap - w, sym.MidY - h / 2, w, h),
                PortSide.Top => SKRect.Create(sym.MidX - w / 2, aboveTop - AttachGap - h, w, h),
                PortSide.Bottom => SKRect.Create(sym.MidX - w / 2, below + AttachGap, w, h),
                _ => SKRect.Create(sym.Right + AttachGap, sym.MidY - h / 2, w, h),
            };
            if (side == PortSide.Top)
                aboveTop = r.Top;
            if (side == PortSide.Bottom)
                below = r.Bottom;
            n.AttachRects.Add(r);
            extras.Add(r);
        }

        PlaceInstruments(d, n, sym, body, labelSide, extras, family);

        var foot = extras.Aggregate(body, SKRect.Union);
        var dx = -foot.Left;
        var dy = -foot.Top;
        SKRect Move(SKRect r) => r.IsEmpty ? r : new SKRect(r.Left + dx, r.Top + dy, r.Right + dx, r.Bottom + dy);
        n.Sym = Move(sym);
        n.Body = Move(body);
        n.LabelRect = Move(n.LabelRect);
        for (var i = 0; i < n.AccessoryRects.Count; i++)
            n.AccessoryRects[i] = (Move(n.AccessoryRects[i].Rect), n.AccessoryRects[i].Def, n.AccessoryRects[i].Running);
        for (var i = 0; i < n.AttachRects.Count; i++)
            n.AttachRects[i] = Move(n.AttachRects[i]);
        foreach (var inst in n.Instruments)
        {
            inst.Rect = Move(inst.Rect);
            inst.ValueRect = Move(inst.ValueRect);
        }

        n.Transform = n.Transform.PostConcat(SKMatrix.CreateTranslation(n.Sym.Left, n.Sym.Top));
        n.Footprint = new SKSize(foot.Width, foot.Height);

        // 배관이 피할 몸통 = 심볼 사각형, 부속은 따로(합치면 용기 윗면 전체가 막힌다)
        var g = new GraphNode { W = foot.Width, H = foot.Height, Body = n.Sym, Row = n.Row, Rank = n.Rank, Name = n.Name };
        foreach (var (ar, _, _) in n.AccessoryRects)
            g.Extras.Add(new SKRect(ar.Left, ar.Top, ar.Right, MathF.Min(ar.Bottom, n.Sym.Top)));
        if (!n.LabelRect.IsEmpty)
            g.Extras.Add(n.LabelRect);
        g.Extras.AddRange(n.AttachRects);
        foreach (var inst in n.Instruments)
        {
            g.Extras.Add(inst.Rect);
            if (!inst.ValueRect.IsEmpty)
                g.Extras.Add(inst.ValueRect);
        }

        n.G = g;
    }

    // 이름표가 그 면의 포트(배관이 곧게 나가는 자리)를 가리면 면을 따라 비켜 놓는다 — 네 면을 다 쓰는 장비(열교환기)는
    // 이름표가 포트 있는 면에 놓이므로. 덜 움직이는 쪽(앞·뒤)으로, 포트에서 배관 간격 이상 떨어지게
    private static SKRect ClearOfPorts(ProcessNode n, PortSide side, SKRect label)
    {
        if (n.Def is not { } def || n.Unit is not null)
            return label;
        const float pad = 12f;
        var along = side is PortSide.Top or PortSide.Bottom;
        var at = new List<float>();
        foreach (var p in def.Ports)
        {
            if (p.Face || MapSide(p.Side, n) != side)
                continue;
            var pt = n.Transform.MapPoint(p.Side switch
            {
                PortSide.Left => new SKPoint(0, def.Height * p.At),
                PortSide.Right => new SKPoint(def.Width, def.Height * p.At),
                PortSide.Top => new SKPoint(def.Width * p.At, 0),
                _ => new SKPoint(def.Width * p.At, def.Height),
            });
            at.Add(along ? pt.X : pt.Y);
        }

        var (a, b) = along ? (label.Left, label.Right) : (label.Top, label.Bottom);
        if (at.All(x => x <= a - pad || x >= b + pad))
            return label;
        var before = at.Min() - pad - b;  // 앞(왼쪽·위)으로 옮길 거리(음수)
        var after = at.Max() + pad - a;   // 뒤(오른쪽·아래)로 옮길 거리
        var d = -before <= after ? before : after;
        return along ? new SKRect(label.Left + d, label.Top, label.Right + d, label.Bottom) : new SKRect(label.Left, label.Top + d, label.Right, label.Bottom + d);
    }

    /// <summary>Minimum instrument bubble diameter and text size.</summary>
    internal const float BubbleMin = 40f, LetterSize = 12f, NumberSize = 11f, ValueSize = 11f;

    /// <summary>Gap between the instrument row and the equipment (or the name label or attached slot on that side), leaving room for signal lines to run straight down.</summary>
    private const float InstrumentGap = 20f;

    private const float BubbleGap = 12f, ValueGap = 4f;

    /// <summary>Bubble diameter: the wider of the texts (tag, number) + padding, at least <see cref="BubbleMin"/>.</summary>
    internal static float BubbleSize(ProcessInstrument inst, string? family)
    {
        using var tf = TextFont.Create(inst.Letters ?? "", LetterSize, true, family);
        using var nf = TextFont.Create(inst.Number ?? "", NumberSize, true, family);
        return MathF.Max(BubbleMin, MathF.Max(tf.MeasureText(inst.Letters ?? ""), nf.MeasureText(inst.Number ?? "")) + 14);
    }

    // 계기 — 선·붙은 칸·이름표가 가장 적은 면(같으면 위 → 오른쪽 → 아래 → 왼쪽)에, 그 면 바깥 것(이름표·붙은 칸) 너머로 나란히.
    // 버블 하나면 장비 가운데 줄에 맞춘다(조절 밸브 구동부로 가는 신호선이 곧게). 값 글자는 버블 오른쪽
    private static void PlaceInstruments(ProcessDiagram d, ProcessNode n, SKRect sym, SKRect body, PortSide? labelSide, List<SKRect> extras, string? family)
    {
        if (n.Instruments.Count == 0)
            return;

        var load = new int[4];
        foreach (var s in SideEnds(d, n))
            load[(int)s]++;
        foreach (var (s, _) in n.Attach)
            load[(int)s]++;
        if (labelSide is { } ls)
            load[(int)ls]++;
        var side = new[] { PortSide.Top, PortSide.Right, PortSide.Bottom, PortSide.Left }.OrderBy(s => load[(int)s]).First();

        var slots = n.Instruments.Select(inst =>
        {
            var dia = BubbleSize(inst, family);
            SKSize v = SKSize.Empty;
            if (!string.IsNullOrEmpty(inst.Value))
            {
                using var vf = TextFont.Create(inst.Value, ValueSize, false, family);
                v = new SKSize(vf.MeasureText(inst.Value), vf.Spacing);
            }

            return (Inst: inst, D: dia, V: v, W: dia + (v.Width > 0 ? ValueGap + v.Width : 0));
        }).ToList();

        // 그 면 바깥에 이미 있는 것(이름표·붙은 칸)의 끝
        var outer = side switch
        {
            PortSide.Top => extras.Where(r => r.Bottom <= sym.Top + 1).Select(r => r.Top).Append(body.Top).Min(),
            PortSide.Bottom => extras.Where(r => r.Top >= sym.Bottom - 1).Select(r => r.Bottom).Append(sym.Bottom).Max(),
            PortSide.Left => extras.Where(r => r.Right <= sym.Left + 1).Select(r => r.Left).Append(sym.Left).Min(),
            _ => extras.Where(r => r.Left >= sym.Right - 1).Select(r => r.Right).Append(sym.Right).Max(),
        };

        void Put(ProcessInstrument inst, SKRect bubble, SKSize v)
        {
            inst.Rect = bubble;
            inst.ValueRect = v.Width > 0 ? SKRect.Create(bubble.Right + ValueGap, bubble.MidY - v.Height / 2, v.Width, v.Height) : SKRect.Empty;
            extras.Add(inst.Rect);
            if (!inst.ValueRect.IsEmpty)
                extras.Add(inst.ValueRect);
        }

        if (side is PortSide.Top or PortSide.Bottom)
        {
            var rowH = slots.Max(s => s.D);
            var mid = side == PortSide.Top ? outer - InstrumentGap - rowH / 2 : outer + InstrumentGap + rowH / 2;
            var x = slots.Count == 1 ? sym.MidX - slots[0].D / 2 : sym.MidX - (slots.Sum(s => s.W) + BubbleGap * (slots.Count - 1)) / 2;
            foreach (var s in slots)
            {
                Put(s.Inst, SKRect.Create(x, mid - s.D / 2, s.D, s.D), s.V);
                x += s.W + BubbleGap;
            }
        }
        else
        {
            var colW = slots.Max(s => s.W);
            var heights = slots.Select(s => MathF.Max(s.D, s.V.Height)).ToList();
            var y = slots.Count == 1 ? sym.MidY - heights[0] / 2 : sym.MidY - (heights.Sum() + BubbleGap * (slots.Count - 1)) / 2;
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var left = side == PortSide.Right ? outer + InstrumentGap : outer - InstrumentGap - colW;
                Put(s.Inst, SKRect.Create(left, y + (heights[i] - s.D) / 2, s.D, s.D), s.V);
                y += heights[i] + BubbleGap;
            }
        }
    }

    // 이 장비에서 선이 붙는 면(선 끝마다 하나, 좌우를 고르는 용기 side 는 양쪽)
    private static IEnumerable<PortSide> SideEnds(ProcessDiagram d, ProcessNode n)
    {
        var index = d.Nodes.IndexOf(n);
        foreach (var l in d.Lines)
        {
            foreach (var (node, port) in new[] { (l.FromNode, l.FromPort), (l.ToNode, l.ToPort) })
            {
                if (node != index || port is null)
                    continue;
                if (n.Terminal)
                {
                    yield return d.Vertical ? (n.TerminalSource ? PortSide.Bottom : PortSide.Top) : (n.TerminalSource ? PortSide.Right : PortSide.Left);
                    continue;
                }

                if (n.Ports.FirstOrDefault(p => p.Name == port) is not { } p)
                    continue;
                if (p.EitherSide)
                {
                    yield return PortSide.Left;
                    yield return PortSide.Right;
                }
                else
                {
                    yield return n.Unit is not null ? p.Side : MapSide(p.Side, n);
                }
            }
        }
    }

    // 이 장비에서 선이 쓰는 포트 면(좌우 중 하나를 고르는 용기 side 는 양쪽 다)
    private static HashSet<PortSide> UsedSides(ProcessDiagram d, ProcessNode n)
    {
        var index = d.Nodes.IndexOf(n);
        var used = new HashSet<PortSide>();
        foreach (var l in d.Lines)
        {
            foreach (var (node, port) in new[] { (l.FromNode, l.FromPort), (l.ToNode, l.ToPort) })
            {
                if (node != index || n.Def?.Port(port ?? "") is not { } p)
                    continue;
                if (p.EitherSide)
                {
                    used.Add(PortSide.Left);
                    used.Add(PortSide.Right);
                }
                else
                {
                    used.Add(MapSide(p.Side, n));
                }
            }
        }

        return used;
    }

    private static GraphEnd MakeEnd(ProcessDiagram d, ProcessLine l, int index, bool from)
    {
        var ni = from ? l.FromNode : l.ToNode;
        var n = d.Nodes[ni];
        var portName = from ? l.FromPort : l.ToPort;
        if (n.Terminal)
        {
            var (_, pt, side) = TerminalShape(n, d.Vertical);
            return new GraphEnd { Node = ni, Key = $"{n.Name}.end", Local = pt, Side = side };
        }

        if (n.IsPort)
        {
            // 유닛 안에서 본 포트 자리 — 테두리에서 안쪽으로 나간다
            var inward = n.PortSide switch
            {
                PortSide.Left => PortSide.Right,
                PortSide.Right => PortSide.Left,
                PortSide.Top => PortSide.Bottom,
                _ => PortSide.Top,
            };
            return new GraphEnd { Node = ni, Key = $"{n.Name}.end", Local = SKPoint.Empty, Side = inward };
        }

        if (n.Unit is not null)
        {
            // 바깥에서 본 유닛 포트 — 유닛 테두리(심볼 영역) 위 자리, 그 면 밖으로 나간다
            var up = n.UnitPorts!.First(x => x.Name == portName);
            var s = n.Sym;
            var at = up.Side switch
            {
                PortSide.Left => new SKPoint(s.Left, s.Top + s.Height * up.At),
                PortSide.Right => new SKPoint(s.Right, s.Top + s.Height * up.At),
                PortSide.Top => new SKPoint(s.Left + s.Width * up.At, s.Top),
                _ => new SKPoint(s.Left + s.Width * up.At, s.Bottom),
            };
            return new GraphEnd { Node = ni, Key = $"{n.Name}.{up.Name}", Local = at, Side = up.Side };
        }

        var p = n.Def!.Port(portName!)!;
        var side2 = MapSide(p.Side, n);
        if (p.Face)
        {
            return new GraphEnd
            {
                // 같은 면에 같은 방향(들어옴·나감)의 선은 한 묶음 — 한 노즐로 합쳐진다(side 는 고른 면이 키에 붙는다)
                Node = ni, Key = $"{n.Name}.{p.Name}.{(from ? "out" : "in")}", Face = true, EitherSide = p.EitherSide,
                Side = side2, Sym = n.Sym,
            };
        }

        var local = n.Transform.MapPoint(p.Side switch
        {
            PortSide.Left => new SKPoint(0, n.Def.Height * p.At),
            PortSide.Right => new SKPoint(n.Def.Width, n.Def.Height * p.At),
            PortSide.Top => new SKPoint(n.Def.Width * p.At, 0),
            _ => new SKPoint(n.Def.Width * p.At, n.Def.Height),
        });
        return new GraphEnd { Node = ni, Key = $"{n.Name}.{p.Name}", Local = local, Side = side2 };
    }

    // ── 배치 계산 + 결과 재사용(구조가 같으면 다시 계산하지 않는다 — 값만 바뀌는 갱신은 그대로) ──

    private sealed class CacheEntry
    {
        public readonly Dictionary<string, ProcessGraph> Graphs = new(StringComparer.Ordinal);
    }

    private static readonly ConditionalWeakTable<ElementNode, CacheEntry> Cache = new();

    /// <summary>The layout result at spacing scale <paramref name="spacing"/>. Returns the previously computed result for the same structure and scale.</summary>
    internal static ProcessGraph Layout(ProcessDiagram d, string? family, float spacing, ProcessAlign align)
    {
        foreach (var n in d.Nodes)
            Geometry(d, n, family, spacing, align);

        var lines = new List<(ProcessLine l, int i)>();
        for (var i = 0; i < d.Lines.Count; i++)
        {
            var l = d.Lines[i];
            if (!l.Error && l.FromNode >= 0 && l.ToNode >= 0 && l.FromPort is not null && l.ToPort is not null)
                lines.Add((l, i));
        }

        var signals = d.Signals.Where(s => !s.Error).ToList();
        var sig = align + "|" + Signature(d, lines.Select(x => x.l), signals, spacing);
        var entry = Cache.GetOrCreateValue(d.Source);
        lock (entry)
        {
            if (entry.Graphs.TryGetValue(sig, out var cached))
            {
                Attach(d, cached, lines, signals);
                return cached;
            }
        }

        var graph = new ProcessGraph
        {
            Nodes = d.Nodes.Select(n => n.G).ToList(),
            Lines = lines.Select(x => new GraphLine { From = MakeEnd(d, x.l, x.i, true), To = MakeEnd(d, x.l, x.i, false) }).ToList(),
            Signals = signals.Select(s => new GraphSignal { From = SignalEnd(d, s.FromRef), To = SignalEnd(d, s.ToRef) }).ToList(),
            Vertical = d.Vertical,
            Spacing = spacing,
            Pitch = d.Pitch,
            Unit = d.IsUnit,
            Align = align,
        };
        graph.Run();

        lock (entry)
        {
            if (entry.Graphs.Count > 8)
                entry.Graphs.Clear();
            entry.Graphs[sig] = graph;
        }

        Attach(d, graph, lines, signals);
        return graph;
    }

    /// <summary>Top of the actuator of a control valve (<c>DvValve</c>/<c>DvValve3</c> <c>kind="control"</c>), in symbol coordinates (both symbols are 60 wide with the actuator in the same place).</summary>
    private static readonly SKPoint ActuatorTop = new(30, 4.5f);

    private static bool HasActuator(ProcessNode n) =>
        (n.Tag == "DvValve" && n.Def?.Name == "control") || (n.Tag == "DvValve3" && n.Def?.Name == "valve3-control");

    // 신호선 끝 — 계기는 버블, 조절 밸브는 구동부 위, 그 밖의 장비는 심볼 영역(상대 쪽 면 가운데)
    private static GraphEnd SignalEnd(ProcessDiagram d, string name)
    {
        switch (SignalTarget(d, name))
        {
            case ProcessInstrument inst:
                return new GraphEnd { Node = d.Nodes.IndexOf(inst.Host!), Key = name, Pick = inst.Rect, PickNoRight = !inst.ValueRect.IsEmpty };
            case ProcessNode n when HasActuator(n):
                return new GraphEnd { Node = d.Nodes.IndexOf(n), Key = name, Local = n.Transform.MapPoint(ActuatorTop), Side = MapSide(PortSide.Top, n) };
            case ProcessNode n:
                return new GraphEnd { Node = d.Nodes.IndexOf(n), Key = name, Pick = n.Sym };
            default:
                throw new InvalidOperationException(name); // ResolveSignals 가 걸러 낸다
        }
    }

    private static void Attach(ProcessDiagram d, ProcessGraph g, List<(ProcessLine l, int i)> lines, List<ProcessSignal> signals)
    {
        for (var k = 0; k < signals.Count; k++)
            signals[k].G = g.Signals[k];
        d.Graph = g;
        for (var i = 0; i < d.Nodes.Count; i++)
        {
            d.Nodes[i].G.X = g.Nodes[i].X;
            d.Nodes[i].G.Y = g.Nodes[i].Y;
        }

        for (var k = 0; k < lines.Count; k++)
            lines[k].l.G = g.Lines[k];
    }

    private static string Signature(ProcessDiagram d, IEnumerable<ProcessLine> lines, IEnumerable<ProcessSignal> signals, float spacing)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(d.Vertical ? 'V' : 'H').Append(d.Pitch).Append('|').Append(spacing.ToString(CultureInfo.InvariantCulture)).Append('|');
        foreach (var n in d.Nodes)
        {
            sb.Append(n.Name).Append(':').Append(n.Def?.Name ?? "T").Append(n.TerminalSource ? 's' : 'd')
                .Append(n.Rotate).Append(n.FlipH ? 'h' : '-').Append(n.FlipV ? 'v' : '-')
                .Append(n.Row).Append(',').Append(n.Rank).Append(',')
                .Append(n.Footprint.Width.ToString("0.#", CultureInfo.InvariantCulture)).Append('x')
                .Append(n.Footprint.Height.ToString("0.#", CultureInfo.InvariantCulture)).Append(',')
                .Append(n.Body.ToString()).Append(n.LabelRect.ToString());
            foreach (var r in n.AttachRects)
                sb.Append(r.ToString());
            foreach (var inst in n.Instruments)
                sb.Append('I').Append(inst.Name).Append(inst.Rect.ToString()).Append(inst.ValueRect.ToString());
            sb.Append(n.Accessories.Count);
            if (n.IsPort)
                sb.Append('P').Append(n.PortSide).Append(n.PortAt.ToString(CultureInfo.InvariantCulture));
            if (n.UnitPorts is { } ups)
                foreach (var up in ups)
                    sb.Append('U').Append(up.Name).Append(up.Side).Append(up.At.ToString(CultureInfo.InvariantCulture));
            sb.Append(';');
        }

        foreach (var l in lines)
            sb.Append(l.FromRef).Append('>').Append(l.ToRef).Append(';');
        foreach (var s in signals)
            sb.Append('S').Append(s.FromRef).Append('>').Append(s.ToRef).Append(';');
        return sb.ToString();
    }

    // ── 화면 배치 ──

    /// <summary>Natural size (spacing scale 1): the inner size (excluding padding).</summary>
    internal static SKSize Natural(LayoutBox box)
    {
        var d = box.Process!;
        return Layout(d, box.Style.FontFamily, 1f, d.Align ?? ProcessAlign.Step).Size;
    }

    /// <summary>
    /// Quality diagnostics (Trace warnings): reports only the layout that is actually drawn (layout may be called several times during measurement, so this runs once at draw time).
    /// The same message is reported only once.
    /// </summary>
    internal static void Report(ProcessDiagram d)
    {
        if (d.Shortage is { } msg)
            Warn(msg);
        foreach (var n in d.Nodes)
        {
            if (n.Unit is { } u)
                Report(u);
        }

        if (d.Graph is { } g)
            foreach (var w in g.Warnings)
                Warn(w);

        foreach (var l in d.Lines)
        {
            if (l.G is not { } gl)
                continue;
            if (gl.Forced)
                Warn($"'{l.FromRef}'→'{l.ToRef}': no free path, so it overlaps other piping; move equipment with row/rank hints or enlarge the area");
            else if (gl.Crossings > 0)
                Warn($"'{l.FromRef}'→'{l.ToRef}': crosses other piping {gl.Crossings} time(s); a row hint can reduce this");
        }
    }

    /// <summary>
    /// Lays out within the inner area (<paramref name="inner"/>): if it does not fit, lays out again with reduced spacing (down to 0.5), and warns if it still does not fit.
    /// If there is space left over, centers it. Sets the positions of the equipment boxes and attached slot boxes.
    /// </summary>
    internal static void Arrange(LayoutBox box, SKRect inner)
    {
        var d = box.Process!;
        var family = box.Style.FontFamily;
        // 후보: (줄 맞춤) × (간격 배율). auto = 계단식이 맞으면 계단식, 아니면 같은 줄, 둘 다 넘치면 덜 넘치는 쪽
        var aligns = d.Align is { } fixedAlign ? new[] { fixedAlign } : new[] { ProcessAlign.Step, ProcessAlign.Row };
        var spacings = new[] { 1f, 0.85f, 0.7f, 0.6f, 0.5f };
        (ProcessAlign Align, float Spacing)? pick = null;
        (ProcessAlign Align, float Spacing) least = (aligns[0], spacings[^1]);
        var leastOver = float.PositiveInfinity;
        foreach (var align in aligns)
        {
            foreach (var spacing in spacings)
            {
                var c = Layout(d, family, spacing, align);
                var over = MathF.Max(0, c.Size.Width - inner.Width) + MathF.Max(0, c.Size.Height - inner.Height);
                if (over <= 0.5f)
                {
                    pick = (align, spacing);
                    break;
                }

                if (over < leastOver)
                {
                    leastOver = over;
                    least = (align, spacing);
                }
            }

            if (pick is not null)
                break;
        }

        var (useAlign, useSpacing) = pick ?? least;
        var g = Layout(d, family, useSpacing, useAlign); // 고른 후보로 다시(캐시) — 장비·선에 그 결과를 붙인다

        d.Shortage = g.Size.Width > inner.Width + 0.5f || g.Size.Height > inner.Height + 0.5f
            ? $"Not enough space: needs {g.Size.Width:0}×{g.Size.Height:0}, area is {inner.Width:0}×{inner.Height:0} (overflows even at minimum spacing). Enlarge the area, split the diagram over several screens, or use less equipment"
            : null;
        d.Graph = g;
        d.Origin = new SKPoint(inner.Left + MathF.Max(0, (inner.Width - g.Size.Width) / 2),
            inner.Top + MathF.Max(0, (inner.Height - g.Size.Height) / 2));

        Place(d);
    }

    // 장비 상자·붙은 칸 상자의 화면 위치(유닛은 안쪽까지)
    private static void Place(ProcessDiagram d)
    {
        foreach (var n in d.Nodes)
        {
            if (n.IsPort)
                continue;
            var ox = d.Origin.X + n.G.X;
            var oy = d.Origin.Y + n.G.Y;
            n.Box.Rect = new SKRect(ox + n.Body.Left, oy + n.Body.Top, ox + n.Body.Right, oy + n.Body.Bottom);
            for (var i = 0; i < n.Attach.Count; i++)
            {
                var r = n.AttachRects[i];
                LayoutEngine.ArrangeBox(n.Attach[i].Box, ox + r.Left, oy + r.Top, r.Width, r.Height);
            }

            foreach (var inst in n.Instruments)
                inst.Box.Rect = new SKRect(ox + inst.Rect.Left, oy + inst.Rect.Top, ox + inst.Rect.Right, oy + inst.Rect.Bottom);

            if (n.Unit is { } u)
            {
                u.Origin = new SKPoint(ox + n.Sym.Left, oy + n.Sym.Top);
                Place(u);
            }
        }
    }
}
