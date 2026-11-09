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

    /// <summary><c>placement="fixed"</c>: equipment sits at its <c>x</c>·<c>y</c> (symbol center); pipes are still routed automatically. Units are always laid out automatically inside.</summary>
    public bool Fixed;

    /// <summary><c>base-size</c> (coordinate placement only): the size the coordinates were taken at. Positions (not equipment) are scaled to fit the area.</summary>
    public SKSize? BaseSize;

    /// <summary><c>fit="shrink"</c>: if the diagram still overflows the area after laying out, it is drawn smaller as a whole (equipment, text and lines, keeping the proportions). Never enlarged.</summary>
    public bool Shrink;

    /// <summary><c>fit="contain"</c> (coordinate placement): the drawing at <c>base-size</c> (or, without it, at its own extent) is scaled as a whole to the area, down or up, keeping the proportions, and centered — like an image.</summary>
    public bool Contain;

    /// <summary>The drawn extent of the last layout (screen px before <c>fit="shrink"</c> scaling).</summary>
    internal SKRect Drawn;

    /// <summary>Text scale: <c>font-size</c> of <c>DvProcess</c> / 14. <c>font-size</c> is not inherited, so only a value set on <c>DvProcess</c> itself changes it. Units follow the outer value.</summary>
    public float TextScale = 1f;

    /// <summary>A text size (or text-sized length) of the diagram at <see cref="TextScale"/>.</summary>
    internal float Text(float size) => size * TextScale;
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

    /// <summary>Coordinate placement: the symbol center (<c>x</c>·<c>y</c>, px at base size). <see langword="null"/> in automatic placement.</summary>
    public SKPoint? Pos;
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

    /// <summary><c>via</c> (coordinate placement only): the points the pipe runs through, in the same space as equipment coordinates. <see langword="null"/> = routed automatically.</summary>
    public List<SKPoint>? Via;

    /// <summary><c>label</c>: small text beside the pipe (line number such as <c>01-100-PE-N</c>), at the middle of its longest segment.</summary>
    public string? Label;

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
    public string? Letters, Number, Value, OnRef, TapRef;
    public bool Panel;

    /// <summary>The port of <see cref="Host"/> the connection line (<c>tap</c>) goes to: a fixed port name or a face position (<c>left@0.8</c>). <see langword="null"/> = no connection line.</summary>
    public string? TapPort;

    /// <summary>Connection line ends, relative to the equipment's footprint: on the equipment (or pipe) and on the bubble.</summary>
    public SKPoint TapFrom, TapTo;

    /// <summary><c>x</c>·<c>y</c> (coordinate placement only): the bubble center, in the same space as equipment coordinates. <see langword="null"/> = placed beside <see cref="Host"/>.</summary>
    public SKPoint? Pos;

    /// <summary>With <see cref="Pos"/>: the bubble's own layout node, so its position does not depend on the host's footprint (pipes and the overlap check treat it like equipment). It comes after the equipment in the graph.</summary>
    public GraphNode? Free;

    /// <summary>Index of <see cref="Free"/> among the free instruments of this diagram.</summary>
    public int FreeIndex;

    /// <summary>With <see cref="Pos"/>: the value text relative to the bubble's top-left.</summary>
    public SKRect FreeValue;

    public LayoutBox Box = null!;
    public ProcessDiagram Owner = null!;

    /// <summary>The equipment it is attached to. <see langword="null"/> if <c>on</c> is missing or wrong (not drawn).</summary>
    public ProcessNode? Host;

    /// <summary>Position of the bubble and value text, relative to the equipment's footprint. The value text is not part of the layout (footprint, pipes), so a changing value never moves anything.</summary>
    public SKRect Rect, ValueRect;

    /// <summary>The side of the bubble the value text is on (the outer side, away from the equipment). Signal lines avoid it.</summary>
    public PortSide ValueSide = PortSide.Right;
}

/// <summary>One signal line (<c>DvSignal</c>); both ends are names only (instruments or equipment).</summary>
internal sealed class ProcessSignal
{
    public required string FromRef, ToRef;

    /// <summary><c>arrow</c> (same values as <c>DvLine</c>) and <c>label</c> (small text beside the <c>from</c> end, e.g. <c>SH</c>).</summary>
    public bool ArrowEnd, ArrowStart;
    public string? Label;
    public bool Error;

    /// <summary><c>kind="pneumatic"</c>: drawn as a solid line with pairs of slashes (ISA-5.1). Otherwise electric: a dashed line. Only the drawing differs; the route is the same.</summary>
    public bool Pneumatic;

    /// <summary><c>color</c> and <c>width</c>: the line's color and thickness. <see langword="null"/> = the default (theme text color, default thickness). Only the drawing differs.</summary>
    public SKColor? Color;

    /// <inheritdoc cref="Color"/>
    public float? Width;
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

    // 배치 품질 경고(Report) — 공정도 요소(파싱한 마크업)마다 같은 문구 한 번. 프로세스 전체로 한 번이면
    // 같은 프로세스에서 마크업을 고쳐 다시 렌더할 때 남은 문제가 보이지 않는다
    private static readonly ConditionalWeakTable<object, HashSet<string>> WarnedFor = new();

    internal static void Warn(string message, bool error = false, object? scope = null)
    {
        var seen = scope is null ? Warned : WarnedFor.GetValue(scope, _ => new HashSet<string>());
        lock (seen)
        {
            if (!seen.Add(message))
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
        Func<ElementNode, object?, (ElementNode Unit, ElementNode BoxNode, object? Scope)?> ExpandUnit,
        Func<string?, SKColor?> Color,
        Func<ElementNode, bool?> VisibleOverride,
        Action<LayoutBox, ElementNode> LinkInstance);

    /// <summary>
    /// Builds the model from the children of <c>DvProcess</c>. Creates a box for each piece of equipment and adds it as a child of <paramref name="box"/> (so presses and onclick work like a normal control),
    /// and also builds the content inside <c>DvAttach</c> as normal boxes. For a unit (<c>DvUnit</c>, or a component whose template root is <c>DvUnit</c>),
    /// creates a unit box and puts the inner equipment boxes inside it.
    /// </summary>
    public static void Build(LayoutBox box, object? vm, ComponentRegistry? registry, Builders b)
    {
        var node = box.Node;
        var r = new AttrReader(node, vm, null);
        var d = new ProcessDiagram
        {
            Source = node,
            Vertical = Direction(r),
            Rich = r.Text("look") is "rich",
        };
        if (r.Text("look") is { } look && look is not ("plain" or "rich"))
            Warn($"DvProcess look='{look}' is not a valid value (plain, rich); drawing as plain");
        d.Align = r.Text("align") switch
        {
            null or "" or "auto" => null,
            "step" => ProcessAlign.Step,
            "row" => ProcessAlign.Row,
            var a => WarnAlign(a),
        };

        Placement(d, r);
        d.FlowAnimation = r.Value("flow-animation") is { } fa && ProcessNode.Truthy(fa);
        var fit = r.Text("fit");
        d.Shrink = fit is "shrink";
        d.Contain = fit is "contain" && d.Fixed;
        if (fit is "contain" && !d.Fixed)
        {
            Warn("DvProcess fit=\"contain\" is used only with placement=\"fixed\" (coordinates have a base size); shrinking only when it overflows, as with fit=\"shrink\"");
            d.Shrink = true;
        }
        if (fit is not (null or "" or "none" or "shrink" or "contain"))
            Warn($"DvProcess fit='{fit}' is not a valid value (none, shrink, contain); not scaling");
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

    // placement·base-size — 좌표 배치면 align 은 쓰지 않는다
    private static void Placement(ProcessDiagram d, AttrReader r)
    {
        var placement = r.Text("placement");
        d.Fixed = placement is "fixed";
        if (placement is not (null or "" or "auto" or "fixed"))
            Warn($"DvProcess placement='{placement}' is not a valid value (auto, fixed); laying out as auto");

        var baseText = r.Text("base-size");
        if (!d.Fixed)
        {
            if (!string.IsNullOrEmpty(baseText))
                Warn("DvProcess base-size is used only with placement=\"fixed\"; ignored");
            return;
        }

        if (d.Align is not null)
        {
            Warn("DvProcess align is not used with placement=\"fixed\" (positions come from x/y); ignored");
            d.Align = null;
        }

        if (string.IsNullOrEmpty(baseText))
            return;
        var parts = baseText.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && TryCoord(parts[0], out var w) && TryCoord(parts[1], out var h) && w > 0 && h > 0)
            d.BaseSize = new SKSize(w, h);
        else
            Warn($"DvProcess base-size='{baseText}' must be two positive numbers (width height, e.g. \"1400 800\"); ignored");
    }

    private static bool TryCoord(string? s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && float.IsFinite(v);

    // 좌표(x·y) — 좌표 배치면 둘 다 있어야 그린다(false = 그리지 않음), 자동 배치면 무시하고 알린다
    private static bool Coordinates(ProcessDiagram d, string tag, Func<string, string?> attr, out SKPoint? pos)
    {
        pos = null;
        var name = attr("name");
        var xs = attr("x");
        var ys = attr("y");
        if (!d.Fixed)
        {
            if (!string.IsNullOrEmpty(xs) || !string.IsNullOrEmpty(ys))
                Warn($"<{tag} name='{name}'>: x/y are used only with <DvProcess placement=\"fixed\">; ignored");
            return true;
        }

        foreach (var hint in new[] { "row", "rank" })
        {
            if (!string.IsNullOrEmpty(attr(hint)))
                Warn($"<{tag} name='{name}'>: {hint} is not used with placement=\"fixed\" (positions come from x/y); ignored");
        }

        if (TryCoord(xs, out var x) && TryCoord(ys, out var y))
        {
            pos = new SKPoint(x, y);
            return true;
        }

        Warn($"<{tag} name='{name}'>: placement=\"fixed\" needs both x and y (symbol center, px); not drawn", true);
        return false;
    }

    // 신호 종류(kind) — electric(기본) · pneumatic. 틀리면 알리고 전기로
    private static bool SignalKind(AttrReader r)
    {
        var kind = r.Text("kind");
        if (kind is not (null or "" or "electric" or "pneumatic"))
            Warn($"DvSignal '{r.Raw("from")}'→'{r.Raw("to")}' kind='{kind}' is not a known kind (electric, pneumatic); drawing as electric");
        return kind is "pneumatic";
    }

    // 신호선 색(color) — #색 또는 token(이름). 안 쓰면 기본(테마 글자색), 틀리면 알리고 기본으로
    private static SKColor? SignalColor(AttrReader r, Builders b)
    {
        var text = r.Text("color");
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (b.Color(text) is { } color)
            return color;
        Warn($"DvSignal '{r.Raw("from")}'→'{r.Raw("to")}' color='{text}' is not a color (#RRGGBB or token(name)); drawing with the default color");
        return null;
    }

    // 신호선 두께(width) — 0 보다 큰 px. 안 쓰면 기본, 틀리면 알리고 기본으로
    private static float? SignalWidth(AttrReader r)
    {
        var text = r.Text("width");
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (TryCoord(text, out var width) && width > 0)
            return width;
        Warn($"DvSignal '{r.Raw("from")}'→'{r.Raw("to")}' width='{text}' must be a number greater than 0; drawing with the default width");
        return null;
    }

    // 계기 좌표(x·y) — 좌표 배치에서 둘 다 있을 때만. 아니면 알리고 장비 옆 자동 자리로(계기는 좌표가 없어도 그린다)
    private static SKPoint? InstrumentPos(ProcessDiagram d, AttrReader r)
    {
        var xs = r.Text("x");
        var ys = r.Text("y");
        if (string.IsNullOrEmpty(xs) && string.IsNullOrEmpty(ys))
            return null;
        var name = r.Text("name");
        if (!d.Fixed)
        {
            Warn($"<DvInstrument name='{name}'>: x/y are used only with <DvProcess placement=\"fixed\">; ignored");
            return null;
        }

        if (TryCoord(xs, out var x) && TryCoord(ys, out var y))
            return new SKPoint(x, y);
        Warn($"<DvInstrument name='{name}'>: needs both x and y (bubble center, px); placed automatically");
        return null;
    }

    // 배관 꺾임점(via) — "x,y x,y …". 좌표 배치 전용, 틀리면 알리고 자동 경로로
    private static List<SKPoint>? Via(ProcessDiagram d, AttrReader r)
    {
        var text = r.Text("via");
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var where = $"DvLine '{r.Raw("from")}'→'{r.Raw("to")}' via='{text}'";
        if (!d.Fixed)
        {
            Warn($"{where}: via is used only with <DvProcess placement=\"fixed\">; ignored");
            return null;
        }

        var points = new List<SKPoint>();
        foreach (var part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var xy = part.Split(',');
            if (xy.Length != 2 || !TryCoord(xy[0], out var x) || !TryCoord(xy[1], out var y))
            {
                Warn($"{where}: write points as x,y separated by spaces (\"230,190 230,74\"); routed automatically");
                return null;
            }

            points.Add(new SKPoint(x, y));
        }

        return points;
    }

    private static bool Direction(AttrReader r)
    {
        var dir = r.Text("direction");
        if (dir is not (null or "" or "lr" or "tb"))
            Warn($"<{r.Node.Tag}> direction='{dir}' is not a valid value (lr, tb); drawing as lr");
        return dir is "tb";
    }

    // 한 공정도(바깥 또는 유닛 안)의 자식 — 장비 상자·붙은 칸 상자는 owner 의 자식으로
    private static void Fill(ProcessDiagram d, LayoutBox owner, ElementNode container, object? vm, ComponentRegistry? registry, Builders b)
    {
        var attachBoxes = new List<LayoutBox>();
        var instruments = new List<(ProcessInstrument I, ElementNode Node)>();
        foreach (var ch in container.Children)
        {
            var r = new AttrReader(ch, vm, null);
            if (ch.Tag == "DvLine")
            {
                d.Lines.Add(new ProcessLine
                {
                    FromRef = r.Raw("from") ?? "",
                    ToRef = r.Raw("to") ?? "",
                    ArrowEnd = r.Text("arrow") is "end" or "both",
                    ArrowStart = r.Text("arrow") is "start" or "both",
                    FlowOverride = r.Has("flow") && r.Value("flow") is { } fv
                        ? ProcessNode.Truthy(fv)
                        : null,
                    MediumSet = Medium(r.Text("medium")),
                    Label = r.Text("label"),
                    Via = Via(d, r),
                });
                continue;
            }

            if (ch.Tag == "DvSignal")
            {
                d.Signals.Add(new ProcessSignal
                {
                    FromRef = r.Raw("from") ?? "",
                    ToRef = r.Raw("to") ?? "",
                    ArrowEnd = r.Text("arrow") is "end" or "both",
                    ArrowStart = r.Text("arrow") is "start" or "both",
                    Label = r.Text("label"),
                    Pneumatic = SignalKind(r),
                    Color = SignalColor(r, b),
                    Width = SignalWidth(r),
                });
                continue;
            }

            if (ch.Tag == "DvStyle" || !Visible(r, b))
                continue;

            if (ch.Tag == "DvInstrument")
            {
                instruments.Add((new ProcessInstrument
                {
                    Owner = d,
                    Name = r.Text("name") ?? "",
                    Letters = r.Text("tag"),
                    Number = r.Text("number"),
                    Value = r.Text("value"),
                    OnRef = r.Text("on"),
                    TapRef = r.Text("tap"),
                    Panel = r.Value("panel") is { } pv && ProcessNode.Truthy(pv),
                    Pos = InstrumentPos(d, r),
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

                var side = SymbolDef.ParseSide(r.Text("side"));
                if (side is null)
                    Warn($"<DvPort name='{r.Raw("name")}'> side must be one of left, top, right, bottom; using right", true);
                var atText = r.Text("at");
                var at = 0.5f;
                if (atText is { Length: > 0 } && (!float.TryParse(atText, NumberStyles.Float, CultureInfo.InvariantCulture, out at) || at < 0 || at > 1))
                {
                    Warn($"<DvPort name='{r.Raw("name")}'> at='{atText}' must be between 0 and 1; using 0.5", true);
                    at = 0.5f;
                }

                d.Nodes.Add(new ProcessNode
                {
                    Owner = d, Name = UniqueName(d, r), Tag = "DvPort", IsPort = true, PortSide = side ?? PortSide.Right, PortAt = at,
                });
                continue;
            }

            // 유닛 — DvUnit 그대로, 또는 템플릿 루트가 DvUnit 인 컴포넌트 인스턴스
            (ElementNode Unit, ElementNode BoxNode, object? Scope)? unit = ch.Tag == "DvUnit" ? (ch, ch, vm) : b.ExpandUnit(ch, vm);
            if (unit is { } u)
            {
                if (d.IsUnit)
                {
                    Warn($"<{ch.Tag}>: a unit cannot be placed inside another unit; not drawn", true);
                    continue;
                }

                AddUnit(d, owner, ch, u.Unit, u.BoxNode, u.Scope, vm, registry, b);
                continue;
            }

            var def = ResolveDef(r, registry, out var known);
            if (!known)
            {
                Warn($"<{ch.Tag}> is not process equipment (DvVessel, DvPump, DvValve, DvInline, DvTerminal, DvUnit, DvInstrument, a registered DvSymbol, or a DvUnit component); not drawn", true);
                continue;
            }

            if (!Coordinates(d, ch.Tag, k => r.Text(k), out var pos))
                continue;

            var pn = new ProcessNode
            {
                Owner = d,
                Name = UniqueName(d, r),
                Tag = ch.Tag,
                Def = def,
                Terminal = ch.Tag == "DvTerminal",
                Text = r.Text("text"),
                Label = r.Text("label"),
                Rotate = ParseRotate(r.Text("rotate")),
                Row = Int(r.Text("row")),
                Rank = Int(r.Text("rank")),
                Pos = pos,
            };
            var flip = r.Text("flip");
            pn.FlipH = flip is "h" or "hv" or "vh";
            pn.FlipV = flip is "v" or "hv" or "vh";
            if (def is not null)
            {
                foreach (var p in def.Params)
                    pn.Values[p.Name] = r.Has(p.Name) ? r.Value(p.Name) : p.Default;
            }

            pn.Box = b.Self(ch, vm);
            pn.Box.Equipment = pn;
            owner.Children.Add(pn.Box);

            foreach (var sub in ch.Children)
            {
                var sr = new AttrReader(sub, vm, null);
                if (!Visible(sr, b))
                    continue;
                if (BuiltInSymbols.IsAccessoryTag(sub.Tag))
                {
                    var running = sr.Has("running") && sr.Value("running") is { } rv && ProcessNode.Truthy(rv);
                    var accKind = sr.Text("kind");
                    var acc = BuiltInSymbols.Get(sub.Tag, accKind);
                    if (acc is null)
                    {
                        Warn($"<{sub.Tag} kind='{accKind}'> is not a known kind (isa); drawing the default");
                        acc = BuiltInSymbols.Get(sub.Tag, null)!;
                    }

                    pn.Accessories.Add((acc, running));
                }
                else if (sub.Tag == "DvAttach")
                {
                    var side = SymbolDef.ParseSide(sr.Text("side")) ?? PortSide.Right;
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

            ProcessNode? host;
            if (!string.IsNullOrEmpty(inst.TapRef))
            {
                host = Tap(d, inst);
            }
            else
            {
                host = string.IsNullOrEmpty(inst.OnRef) ? null : d.Nodes.FirstOrDefault(n => n.Name == inst.OnRef && !n.IsPort);
                if (host is null)
                    Warn(string.IsNullOrEmpty(inst.OnRef)
                        ? $"<DvInstrument name='{inst.Name}'> has no on (name of the equipment to attach to) or tap; not drawn"
                        : $"<DvInstrument name='{inst.Name}'> on='{inst.OnRef}': no such equipment; not drawn", true);
            }

            if (host is null)
            {
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

    // 연결선(tap) 대상 — "장비.고정포트" 또는 "장비.면@위치". 그 장비가 계기의 장비(on 보다 앞선다). 틀리면 null(그리지 않음)
    private static ProcessNode? Tap(ProcessDiagram d, ProcessInstrument inst)
    {
        var raw = inst.TapRef!;
        var where = $"<DvInstrument name='{inst.Name}'> tap='{raw}'";
        var dot = raw.IndexOf('.');
        var name = dot < 0 ? raw : raw[..dot];
        var port = dot < 0 ? null : raw[(dot + 1)..];
        var n = d.Nodes.FirstOrDefault(x => x.Name == name);
        if (n is null || n.NameOnly || n.Unit is not null)
        {
            Warn(n is null
                ? $"{where}: no equipment named '{name}'; not drawn"
                : $"{where}: '{name}' is an endpoint or a unit; tap a piece of equipment; not drawn", true);
            return null;
        }

        if (port is null || n.Def is not { } def)
        {
            Warn($"{where}: write the equipment with a port ('{name}.in') or a face position ('{name}.left@0.5'); not drawn", true);
            return null;
        }

        if (port.Contains('@'))
        {
            if (!FaceAt(port, where, out var side, out var at))
                return null;
            inst.TapPort = FaceAtName(side, at);
        }
        else if (def.Port(port) is not { Face: false })
        {
            Warn(def.Port(port) is null
                ? $"{where}: '{name}' has no port '{port}' ({string.Join(", ", def.Ports.Select(p => p.Name))}); not drawn"
                : $"{where}: '{port}' has no fixed point on the face; write a position like '{name}.left@0.5'; not drawn", true);
            return null;
        }
        else
        {
            inst.TapPort = port;
        }

        if (!string.IsNullOrEmpty(inst.OnRef) && inst.OnRef != name)
            Warn($"<DvInstrument name='{inst.Name}'> on='{inst.OnRef}' and tap='{raw}' name different equipment; using the tap equipment '{name}'");
        return n;
    }

    // ── 면 위 자리 "면@위치"(left@0.85) — 면은 화면에 보이는 심볼 사각형의 면, 위치는 그 면을 따라 0~1(왼쪽→오른쪽, 위→아래) ──

    /// <summary>Parses a face position (<c>left@0.85</c>) with diagnostics: a wrong face or number is an error (<see langword="false"/>); a position outside 0–1 is clamped with a warning.</summary>
    internal static bool FaceAt(string port, string where, out PortSide side, out float at)
    {
        var i = port.IndexOf('@');
        var face = port[..i];
        var num = port[(i + 1)..];
        at = 0;
        side = default;
        if (face is not ("top" or "bottom" or "left" or "right"))
        {
            Warn($"{where}: '{face}' is not a face (top·bottom·left·right)", true);
            return false;
        }

        if (!float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out at) || !float.IsFinite(at))
        {
            Warn($"{where}: '@{num}' is not a position (a number 0–1 along the face)", true);
            return false;
        }

        if (at is < 0 or > 1)
        {
            Warn($"{where}: position {num} is outside 0–1; using {Math.Clamp(at, 0, 1)}");
            at = Math.Clamp(at, 0, 1);
        }

        side = Enum.Parse<PortSide>(face, true);
        return true;
    }

    /// <summary>The canonical name of a face position (the same point always gives the same name, so lines on it share one nozzle).</summary>
    internal static string FaceAtName(PortSide side, float at) => $"{side.ToString().ToLowerInvariant()}@{at.ToString("0.###", CultureInfo.InvariantCulture)}";

    /// <summary>Reads back a canonical face position name. <see langword="false"/> for an ordinary port name.</summary>
    internal static bool IsFaceAt(string? port, out PortSide side, out float at)
    {
        side = default;
        at = 0;
        var i = port?.IndexOf('@') ?? -1;
        return i > 0 && Enum.TryParse(port![..i], true, out side)
            && float.TryParse(port[(i + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out at);
    }

    /// <summary>The point at position <paramref name="at"/> (0–1) along face <paramref name="side"/> of <paramref name="r"/>.</summary>
    internal static SKPoint OnFace(SKRect r, PortSide side, float at) => side switch
    {
        PortSide.Left => new SKPoint(r.Left, r.Top + r.Height * at),
        PortSide.Right => new SKPoint(r.Right, r.Top + r.Height * at),
        PortSide.Top => new SKPoint(r.Left + r.Width * at, r.Top),
        _ => new SKPoint(r.Left + r.Width * at, r.Bottom),
    };

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
    private static void AddUnit(ProcessDiagram d, LayoutBox owner, ElementNode instance, ElementNode unitNode, ElementNode boxNode, object? scope,
        object? outerVm, ComponentRegistry? registry, Builders b)
    {
        var ri = new AttrReader(instance, outerVm, null);
        var ru = new AttrReader(unitNode, scope, null);
        string? Attr(string name) => ri.Has(name) ? ri.Text(name) : ru.Text(name);

        if (!Coordinates(d, instance.Tag, Attr, out var pos))
            return;
        if (ru.Text("placement") is { Length: > 0 } up && up != "auto")
            Warn($"<{instance.Tag} name='{Attr("name")}'>: placement='{up}' is not supported inside a unit; the inside is laid out automatically");

        var inner = new ProcessDiagram { Source = unitNode, Vertical = Direction(ru), Rich = d.Rich, IsUnit = true, FlowAnimation = d.FlowAnimation };
        var pn = new ProcessNode
        {
            Owner = d,
            Name = UniqueName(d, ri),
            Tag = "DvUnit",
            Label = Attr("label"),
            Row = Int(Attr("row")),
            Rank = Int(Attr("rank")),
            Pos = pos,
            Unit = inner,
        };

        // 유닛 상자(테두리·배경 style, onclick) — 안쪽 장비 상자는 그 자식
        pn.Box = b.Self(boxNode, scope);
        if (!ReferenceEquals(instance, unitNode))
            b.LinkInstance(pn.Box, instance); // 유닛 컴포넌트 인스턴스 — id 핸들·Enabled·Click 이 이 상자로(일반 컴포넌트와 같음)
        pn.Box.Equipment = pn;
        owner.Children.Add(pn.Box);

        Fill(inner, pn.Box, unitNode, scope, registry, b);
        pn.UnitPorts = inner.Nodes.Where(x => x.IsPort).Select(x => new SymbolPort(x.Name, x.PortSide, x.PortAt, "inout")).ToList();
        d.Nodes.Add(pn);
    }

    private static string UniqueName(ProcessDiagram d, AttrReader r)
    {
        var name = r.Text("name");
        if (string.IsNullOrEmpty(name))
        {
            Warn($"<{r.Node.Tag}> has no name; it cannot be connected by lines", true);
            return $"#{d.Nodes.Count}";
        }

        if (d.Nodes.Any(n => n.Name == name))
        {
            Warn($"Equipment name '{name}' is used twice; the later one cannot be connected by lines", true);
            return $"{name}#{d.Nodes.Count}";
        }

        return name;
    }

    // 핸들의 Visible 덮어쓰기가 마크업 visible 보다 우선(일반 요소와 같음)
    private static bool Visible(AttrReader r, Builders b)
    {
        if (b.VisibleOverride(r.Node) is { } ov)
            return ov;
        if (!r.Has("visible"))
            return true;
        var v = r.Value("visible");
        return v is not null && ProcessNode.Truthy(v) || v is string s && s.Length > 0 && s != "false" && s != "0";
    }

    private static SymbolDef? ResolveDef(AttrReader r, ComponentRegistry? registry, out bool known)
    {
        var ch = r.Node;
        known = true;
        if (ch.Tag == "DvTerminal")
            return null;
        if (BuiltInSymbols.Kinds.TryGetValue(ch.Tag, out var kinds))
        {
            var kind = r.Text("kind");
            var def = BuiltInSymbols.Get(ch.Tag, kind);
            if (def is null)
            {
                Warn($"<{ch.Tag} kind='{kind}'> is not a known kind ({string.Join(", ", kinds)}); drawing as {kinds[0]}");
                def = BuiltInSymbols.Get(ch.Tag, null)!;
            }

            // 용기 크기(width·height) — 머리·바닥 모양은 그대로 두고 몸통만 늘리거나 줄인다(원래 크기의 0.6~4배)
            if (ch.Tag == "DvVessel")
            {
                float Size(string attr, float natural)
                {
                    if (r.Text(attr) is not { Length: > 0 } text)
                        return natural;
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !float.IsFinite(v))
                    {
                        Warn($"<DvVessel {attr}='{text}'> is not a number (px); drawing at {natural:0}");
                        return natural;
                    }

                    var clamped = Math.Clamp(v, natural * 0.6f, natural * 4);
                    if (MathF.Abs(clamped - v) > 0.01f) // 끝값(0.6배)은 소수 오차로 조금 넘어 보인다
                        Warn($"<DvVessel {attr}='{text}'> is outside {natural * 0.6f:0}–{natural * 4:0} for kind '{def.Name}'; using {clamped:0}");
                    return clamped;
                }

                var (w, h) = (Size("width", def.Width), Size("height", def.Height));
                if (w != def.Width || h != def.Height)
                    def = BuiltInSymbols.Stretch(def, w, h);
            }

            return def;
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

        if (port is not null && port.Contains('@'))
        {
            var where = $"DvLine '{line.FromRef}'→'{line.ToRef}'";
            if (n.Unit is not null)
            {
                Warn($"{where}: a unit is connected only at its ports (DvPort), not at a face position ('{raw}')", true);
                line.Error = true;
                return (idx, null);
            }

            if (!FaceAt(port, where, out var side, out var at))
            {
                line.Error = true;
                return (idx, null);
            }

            return (idx, FaceAtName(side, at));
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

    /// <summary>Terminal text lines — <c>text</c> split at line breaks (<c>&amp;#10;</c> in markup).</summary>
    internal static string[] TerminalLines(ProcessNode n) => (n.Text ?? "").Replace("\r", "").Split('\n');

    /// <summary>End point shape (depending on the diagram direction and start/end): the path and ports relative to the footprint.</summary>
    internal static (SKPath Path, SKPoint Port, PortSide Side) TerminalShape(ProcessNode n, bool vertical)
    {
        var r = n.Sym;
        var path = new SKPath();
        if (!vertical)
        {
            var tip = n.Owner.Text(14);
            path.MoveTo(r.Left, r.Top);
            path.LineTo(r.Right - tip, r.Top);
            path.LineTo(r.Right, r.MidY);
            path.LineTo(r.Right - tip, r.Bottom);
            path.LineTo(r.Left, r.Bottom);
            path.Close();
            return n.TerminalSource ? (path, new SKPoint(r.Right, r.MidY), PortSide.Right) : (path, new SKPoint(r.Left, r.MidY), PortSide.Left);
        }

        path.MoveTo(r.Left, r.Top);
        path.LineTo(r.Right, r.Top);
        path.LineTo(r.Right, r.Bottom - n.Owner.Text(10));
        path.LineTo(r.MidX, r.Bottom);
        path.LineTo(r.Left, r.Bottom - n.Owner.Text(10));
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
            // 유닛 = 안쪽 배치 크기(테두리), 바깥과 같은 줄 맞춤. 좌표 배치의 배율은 위치에만 — 유닛 안쪽은 간격 1
            symSize = Layout(unit, family, d.Fixed ? 1f : spacing, align).Size;
            n.Transform = SKMatrix.Identity;
        }
        else if (n.Terminal)
        {
            var lines = TerminalLines(n);
            using var f = TextFont.Create(n.Text ?? "", d.Text(12), true, family);
            var tw = lines.Max(l => f.MeasureText(l));
            var more = (lines.Length - 1) * f.Spacing; // 둘째 줄부터 한 줄씩 높아진다
            symSize = d.Vertical ? new SKSize(MathF.Max(d.Text(60), tw + d.Text(20)), d.Text(38) + more) : new SKSize(MathF.Max(d.Text(64), tw + d.Text(34)), d.Text(28) + more);
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
        foreach (var (accessory, running) in n.Accessories)
        {
            var def = accessory;
            // 크기를 바꾼 용기(width·height)의 교반기 — 축은 같은 깊이 비율까지, 날개는 용기 폭만큼
            if (def.Name == "agitator" && n.Tag == "DvVessel" && n.Def is { } vessel && BuiltInSymbols.Get("DvVessel", vessel.Name) is { } natural
                && (vessel.Width != natural.Width || vessel.Height != natural.Height))
                def = BuiltInSymbols.Agitator(def, vessel.Width / natural.Width, vessel.Height / natural.Height);
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
            using var f = TextFont.Create(n.Label, d.Text(LabelSize), true, family);
            var lw = f.MeasureText(n.Label);
            var lh = f.Spacing;
            var used = n.Unit is not null ? new HashSet<PortSide> { PortSide.Bottom } : UsedSides(d, n); // 유닛 이름은 제목(위)
            if (HasActuator(n))
                used.Add(MapSide(PortSide.Top, n)); // 구동부 쪽은 신호선이 닿는 자리
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
            // 붙은 칸도 공정도 글자 배율만큼(font-size) — 자연 크기로 배치하고 Place 에서 그 배율로 그린다
            var (w, h) = LayoutEngine.MeasureBox(ab);
            (w, h) = (d.Text(w), d.Text(h));
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
        foreach (var inst in n.Instruments.Where(x => x.Pos is null))
        {
            inst.Rect = Move(inst.Rect);
            inst.ValueRect = Move(inst.ValueRect);
            inst.TapFrom = new SKPoint(inst.TapFrom.X + dx, inst.TapFrom.Y + dy);
            inst.TapTo = new SKPoint(inst.TapTo.X + dx, inst.TapTo.Y + dy);
        }

        n.Transform = n.Transform.PostConcat(SKMatrix.CreateTranslation(n.Sym.Left, n.Sym.Top));
        n.Footprint = new SKSize(foot.Width, foot.Height);

        // 배관이 피할 몸통 = 심볼 사각형, 부속은 따로(합치면 용기 윗면 전체가 막힌다)
        var g = new GraphNode { W = foot.Width, H = foot.Height, Body = n.Sym, Row = n.Row, Rank = n.Rank, Name = n.Name };
        if (d.Fixed && n.Pos is { } pos)
            g.Pos = new SKPoint(pos.X * spacing, pos.Y * spacing); // 좌표 배치: spacing = 위치 배율(base-size → 영역)
        foreach (var (ar, _, _) in n.AccessoryRects)
            g.Extras.Add(new SKRect(ar.Left, ar.Top, ar.Right, MathF.Min(ar.Bottom, n.Sym.Top)));
        if (!n.LabelRect.IsEmpty)
            g.Extras.Add(n.LabelRect);
        g.Extras.AddRange(n.AttachRects);
        foreach (var inst in n.Instruments.Where(x => x.Pos is null))
        {
            g.Extras.Add(inst.Rect);
            if (!inst.ValueRect.IsEmpty)
                g.SignalExtras.Add(inst.ValueRect); // 신호선만 피한다 — 배관·차지 영역은 값과 무관
            if (inst.TapPort is not null)
                g.Extras.Add(TapArea(inst));
        }

        n.G = g;
    }

    // 연결선(tap) 자리 — 배관이 장비와 계기 사이로 지나며 연결선을 가로지르지 않게. 격자(8)보다 넓게(10) 잡아야 사이로 빠지지 않고,
    // 붙는 쪽 끝은 조금 띄운다(배관 위 tap 이면 그 배관 자체를 막지 않게)
    private static SKRect TapArea(ProcessInstrument inst)
    {
        var (a, b) = (inst.TapFrom, inst.TapTo);
        var len = MathF.Max(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y));
        if (len < 0.01f)
            return SKRect.Empty;
        var start = new SKPoint(a.X + (b.X - a.X) / len * 3, a.Y + (b.Y - a.Y) / len * 3);
        var r = new SKRect(MathF.Min(start.X, b.X), MathF.Min(start.Y, b.Y), MathF.Max(start.X, b.X), MathF.Max(start.Y, b.Y));
        r.Inflate(MathF.Abs(a.Y - b.Y) > 0.01f ? 5 : 0, MathF.Abs(a.X - b.X) > 0.01f ? 5 : 0);
        return r;
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

    /// <summary>Length of an instrument connection line (<c>tap</c>) from the tap point to the bubble.</summary>
    internal const float TapLength = 24f;

    /// <summary>How far along the pipe from a fixed port the connection line is tapped (inside the port's straight end).</summary>
    private const float TapAlong = 12f;

    private static SKPoint Dir(PortSide s) => s switch
    {
        PortSide.Left => new SKPoint(-1, 0),
        PortSide.Right => new SKPoint(1, 0),
        PortSide.Top => new SKPoint(0, -1),
        _ => new SKPoint(0, 1),
    };

    /// <summary>Bubble diameter: the wider of the texts (tag, number) + padding, at least <see cref="BubbleMin"/>.</summary>
    internal static float BubbleSize(ProcessInstrument inst, string? family)
    {
        var d = inst.Owner;
        using var tf = TextFont.Create(inst.Letters ?? "", d.Text(LetterSize), true, family);
        using var nf = TextFont.Create(inst.Number ?? "", d.Text(NumberSize), true, family);
        return MathF.Max(d.Text(BubbleMin), MathF.Max(tf.MeasureText(inst.Letters ?? ""), nf.MeasureText(inst.Number ?? "")) + d.Text(14));
    }

    // 값 글자는 배치에 끼지 않는다 — 버블 자리·차지 영역은 지름만으로(값이 바뀌어도 아무것도 움직이지 않게)
    private static (float D, SKSize V) InstrumentSize(ProcessDiagram d, ProcessInstrument inst, string? family)
    {
        var dia = BubbleSize(inst, family);
        SKSize v = SKSize.Empty;
        if (!string.IsNullOrEmpty(inst.Value))
        {
            using var vf = TextFont.Create(inst.Value, d.Text(ValueSize), false, family);
            v = new SKSize(vf.MeasureText(inst.Value), vf.Spacing);
        }

        return (dia, v);
    }

    // 값 글자 = 버블의 valueSide 쪽(장비 반대쪽). below = 오른쪽이되 아래 절반(오른쪽으로 나가는 연결선을 가리지 않게)
    private static SKRect ValueBox(ProcessDiagram d, SKRect bubble, SKSize v, PortSide valueSide, bool below)
    {
        var gap = d.Text(ValueGap);
        return v.Width <= 0 ? SKRect.Empty : valueSide switch
        {
            PortSide.Left => SKRect.Create(bubble.Left - gap - v.Width, bubble.MidY - v.Height / 2, v.Width, v.Height),
            PortSide.Top => SKRect.Create(bubble.MidX - v.Width / 2, bubble.Top - gap - v.Height, v.Width, v.Height),
            PortSide.Bottom => SKRect.Create(bubble.MidX - v.Width / 2, bubble.Bottom + gap, v.Width, v.Height),
            _ => SKRect.Create(bubble.Right + gap, below ? bubble.MidY + 2 : bubble.MidY - v.Height / 2, v.Width, v.Height),
        };
    }

    // 좌표(x·y)를 받은 계기 — 장비 차지 영역에 넣지 않고 자기 배치 노드로(차지 영역이 배율 따라 변하면 기준 크기 모양 늘이기가 어긋난다).
    // 몸통 = 버블이라 배관은 돌아가고 겹침은 장비처럼 알린다
    private static void FreeGeometry(ProcessDiagram d, ProcessInstrument inst, string? family, float spacing)
    {
        var (dia, v) = InstrumentSize(d, inst, family);
        var bubble = SKRect.Create(0, 0, dia, dia);
        var pos = inst.Pos!.Value;
        // 연결선이 버블 오른쪽으로 나갈 듯하면 값 글자는 아래 절반으로
        var below = inst.TapPort is not null && inst.Host!.Pos is { } host && host.X > pos.X;
        inst.ValueSide = PortSide.Right;
        inst.FreeValue = ValueBox(d, bubble, v, PortSide.Right, below);
        var g = new GraphNode { W = dia, H = dia, Body = bubble, Name = inst.Name, Pos = new SKPoint(pos.X * spacing, pos.Y * spacing) };
        if (!inst.FreeValue.IsEmpty)
            g.SignalExtras.Add(inst.FreeValue);
        inst.Free = g;
    }

    // 좌표 계기의 결과 — 버블·값 글자·연결선을 장비 기준으로(그리기·점검은 다른 계기와 같은 값을 읽는다)
    private static void PlaceFree(ProcessInstrument inst, ProcessNode host, GraphNode node, ProcessGraph g)
    {
        var (ox, oy) = (node.X - host.G.X, node.Y - host.G.Y);
        var (w, h) = (node.Body.Width, node.Body.Height);
        inst.Rect = SKRect.Create(ox, oy, w, h);
        var v = inst.FreeValue;
        inst.ValueRect = v.IsEmpty ? SKRect.Empty : SKRect.Create(ox + v.Left, oy + v.Top, v.Width, v.Height);
        if (inst.TapPort is null)
            return;

        var c = new SKPoint(node.X + w / 2, node.Y + h / 2);
        var tap = FreeTapPoint(inst, host, c, g);
        var (dx, dy) = (tap.X - c.X, tap.Y - c.Y);
        var len = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
        inst.TapFrom = new SKPoint(tap.X - host.G.X, tap.Y - host.G.Y);
        inst.TapTo = new SKPoint(c.X + dx / len * w / 2 - host.G.X, c.Y + dy / len * h / 2 - host.G.Y);
    }

    /// <summary>A face position this close across the line out of the bubble is lined up with the bubble, so the connection line stays straight despite small offsets.</summary>
    private const float TapAlignTolerance = 16f;

    // 좌표 계기의 연결선이 닿는 점 — 면 위 자리는 그 점(조금 어긋나면 버블 줄로), 고정 포트는 그 포트의 배관에 수직으로 내린 가장 가까운 점
    private static SKPoint FreeTapPoint(ProcessInstrument inst, ProcessNode host, SKPoint c, ProcessGraph g)
    {
        var sym = host.G.Abs(host.Sym);
        if (IsFaceAt(inst.TapPort, out var side, out var at))
        {
            var p = OnFace(sym, side, at);
            if (side is PortSide.Top or PortSide.Bottom)
                return MathF.Abs(c.X - p.X) <= TapAlignTolerance && c.X >= sym.Left && c.X <= sym.Right ? new SKPoint(c.X, p.Y) : p;
            return MathF.Abs(c.Y - p.Y) <= TapAlignTolerance && c.Y >= sym.Top && c.Y <= sym.Bottom ? new SKPoint(p.X, c.Y) : p;
        }

        var key = $"{host.Name}.{inst.TapPort}";
        SKPoint? best = null;
        var bestDist = float.PositiveInfinity;
        foreach (var l in g.Lines.Where(l => l.From.Key == key || l.To.Key == key))
        {
            for (var k = 0; k + 1 < l.Points.Count; k++)
            {
                var (a, b) = (l.Points[k], l.Points[k + 1]);
                var flat = MathF.Abs(a.Y - b.Y) < 0.01f;
                var (cAlong, lo, hi) = flat ? (c.X, MathF.Min(a.X, b.X), MathF.Max(a.X, b.X)) : (c.Y, MathF.Min(a.Y, b.Y), MathF.Max(a.Y, b.Y));
                if (cAlong < lo || cAlong > hi)
                    continue;
                var foot = flat ? new SKPoint(c.X, a.Y) : new SKPoint(a.X, c.Y);
                var dist = flat ? MathF.Abs(c.Y - a.Y) : MathF.Abs(c.X - a.X);
                if (dist < bestDist)
                    (best, bestDist) = (foot, dist);
            }
        }

        if (best is { } onPipe)
            return onPipe;

        // 버블이 배관 옆에 없다 — 포트에서 조금 나간 자리
        var port = host.Def!.Port(inst.TapPort!)!;
        var pd = Dir(MapSide(port.Side, host));
        var pt = host.Transform.MapPoint(port.Side switch
        {
            PortSide.Left => new SKPoint(0, host.Def.Height * port.At),
            PortSide.Right => new SKPoint(host.Def.Width, host.Def.Height * port.At),
            PortSide.Top => new SKPoint(host.Def.Width * port.At, 0),
            _ => new SKPoint(host.Def.Width * port.At, host.Def.Height),
        });
        return new SKPoint(host.G.X + pt.X + pd.X * TapAlong, host.G.Y + pt.Y + pd.Y * TapAlong);
    }

    // 계기 — 선·붙은 칸·이름표가 가장 적은 면(같으면 위 → 오른쪽 → 아래 → 왼쪽)에, 그 면 바깥 것(이름표·붙은 칸) 너머로 나란히.
    // 버블 하나면 장비 가운데 줄에 맞춘다(조절 밸브 구동부로 가는 신호선이 곧게). 값 글자는 버블 오른쪽
    private static void PlaceInstruments(ProcessDiagram d, ProcessNode n, SKRect sym, SKRect body, PortSide? labelSide, List<SKRect> extras, string? family)
    {
        (float D, SKSize V) Size(ProcessInstrument inst) => InstrumentSize(d, inst, family);

        void Put(ProcessInstrument inst, SKRect bubble, SKSize v, PortSide valueSide = PortSide.Right, bool below = false)
        {
            inst.Rect = bubble;
            inst.ValueSide = valueSide;
            inst.ValueRect = ValueBox(d, bubble, v, valueSide, below);
            extras.Add(inst.Rect);
        }

        // 연결선(tap) 계기 — 면 위 자리면 그 면 바깥쪽으로, 고정 포트면 포트에서 배관으로 조금 나간 자리에서 배관 위(세로 배관이면 오른쪽)로 TapLength 띄운다
        foreach (var inst in n.Instruments.Where(x => x.TapPort is not null && x.Pos is null))
        {
            SKPoint tap, dir;
            if (IsFaceAt(inst.TapPort, out var tapSide, out var tapAt))
            {
                tap = OnFace(sym, tapSide, tapAt);
                dir = Dir(tapSide);
            }
            else
            {
                var p = n.Def!.Port(inst.TapPort!)!;
                var pd = Dir(MapSide(p.Side, n));
                var pt = n.Transform.MapPoint(p.Side switch
                {
                    PortSide.Left => new SKPoint(0, n.Def.Height * p.At),
                    PortSide.Right => new SKPoint(n.Def.Width, n.Def.Height * p.At),
                    PortSide.Top => new SKPoint(n.Def.Width * p.At, 0),
                    _ => new SKPoint(n.Def.Width * p.At, n.Def.Height),
                });
                tap = new SKPoint(pt.X + pd.X * TapAlong, pt.Y + pd.Y * TapAlong);
                dir = pd.Y == 0 ? new SKPoint(0, -1) : new SKPoint(1, 0);
            }

            var (dia, v) = Size(inst);
            var c = new SKPoint(tap.X + dir.X * (TapLength + dia / 2), tap.Y + dir.Y * (TapLength + dia / 2));
            Put(inst, SKRect.Create(c.X - dia / 2, c.Y - dia / 2, dia, dia), v, below: dir.X < 0);
            inst.TapFrom = tap;
            inst.TapTo = new SKPoint(c.X - dir.X * dia / 2, c.Y - dir.Y * dia / 2);
        }

        var auto = n.Instruments.Where(x => x.TapPort is null && x.Pos is null).ToList();
        if (auto.Count == 0)
            return;

        var load = new int[4];
        foreach (var s in SideEnds(d, n))
            load[(int)s]++;
        foreach (var (s, _) in n.Attach)
            load[(int)s]++;
        if (labelSide is { } ls)
            load[(int)ls]++;
        var side = new[] { PortSide.Top, PortSide.Right, PortSide.Bottom, PortSide.Left }.OrderBy(s => load[(int)s]).First();

        var slots = auto.Select(inst =>
        {
            var (dia, v) = Size(inst);
            return (Inst: inst, D: dia, V: v);
        }).ToList();

        // 그 면 바깥에 이미 있는 것(이름표·붙은 칸)의 끝
        var outer = side switch
        {
            PortSide.Top => extras.Where(r => r.Bottom <= sym.Top + 1).Select(r => r.Top).Append(body.Top).Min(),
            PortSide.Bottom => extras.Where(r => r.Top >= sym.Bottom - 1).Select(r => r.Bottom).Append(sym.Bottom).Max(),
            PortSide.Left => extras.Where(r => r.Right <= sym.Left + 1).Select(r => r.Left).Append(sym.Left).Min(),
            _ => extras.Where(r => r.Left >= sym.Right - 1).Select(r => r.Right).Append(sym.Right).Max(),
        };

        if (side is PortSide.Top or PortSide.Bottom)
        {
            var rowH = slots.Max(s => s.D);
            var mid = side == PortSide.Top ? outer - InstrumentGap - rowH / 2 : outer + InstrumentGap + rowH / 2;
            var x = sym.MidX - (slots.Sum(s => s.D) + BubbleGap * (slots.Count - 1)) / 2;
            // 버블 하나면 값 글자는 오른쪽, 여럿이면 옆 버블과 겹치지 않게 바깥(위 면은 위, 아래 면은 아래)
            var valueSide = slots.Count == 1 ? PortSide.Right : side;
            foreach (var s in slots)
            {
                Put(s.Inst, SKRect.Create(x, mid - s.D / 2, s.D, s.D), s.V, valueSide);
                x += s.D + BubbleGap;
            }
        }
        else
        {
            var colW = slots.Max(s => s.D);
            var y = sym.MidY - (slots.Sum(s => s.D) + BubbleGap * (slots.Count - 1)) / 2;
            foreach (var s in slots)
            {
                // 버블은 열 가운데, 값 글자는 바깥쪽(왼쪽 면이면 버블 왼쪽) — 장비 쪽으로 파고들지 않게
                var left = (side == PortSide.Right ? outer + InstrumentGap : outer - InstrumentGap - colW) + (colW - s.D) / 2;
                Put(s.Inst, SKRect.Create(left, y, s.D, s.D), s.V, side);
                y += s.D + BubbleGap;
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

                if (IsFaceAt(port, out var faceSide, out _))
                {
                    yield return faceSide;
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
                if (node == index && IsFaceAt(port, out var faceSide, out _))
                {
                    used.Add(faceSide);
                    continue;
                }

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

        if (IsFaceAt(portName, out var faceSide, out var faceAt))
            return new GraphEnd { Node = ni, Key = $"{n.Name}.{portName}", Local = OnFace(n.Sym, faceSide, faceAt), Side = faceSide };

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
        public readonly Queue<string> Order = new();
        public int Runs;
    }

    // 한 번의 Arrange 가 시도하는 후보(줄 맞춤 2 × 간격 5 = 10)가 모두 남아 있어야 한다 — 모자라면 매 갱신마다 다음 후보가 지운 것을 다시 계산한다.
    // 같은 유닛 컴포넌트를 여러 번 쓰면 인스턴스들이 한 항목을 나눠 쓰므로(안쪽 이름표가 다르면 인스턴스마다 10개) 넉넉히
    private const int CacheSize = 64;

    private static readonly ConditionalWeakTable<ElementNode, CacheEntry> Cache = new();

    /// <summary>How many times the layout of the diagram from <paramref name="source"/> has actually been computed (not reused) — for tests.</summary>
    internal static int LayoutRuns(ElementNode source) => Cache.TryGetValue(source, out var e) ? e.Runs : 0;

    /// <summary>
    /// The layout result at spacing scale <paramref name="spacing"/>. Returns the previously computed result for the same structure and scale.
    /// With <paramref name="shape"/> (the base-size layout in coordinate placement, the natural-size layout in automatic placement), pipes are not routed again
    /// but stretched from that layout; in automatic placement <paramref name="spacing"/> and <paramref name="spacingY"/> are the horizontal and vertical position scales.
    /// </summary>
    internal static ProcessGraph Layout(ProcessDiagram d, string? family, float spacing, ProcessAlign align, ProcessGraph? shape = null, float? spacingY = null)
    {
        foreach (var n in d.Nodes)
            Geometry(d, n, family, shape is not null && !d.Fixed ? 1f : spacing, align); // 자동 배치 모양 줄이기: 크기는 자연 크기 그대로

        var free = d.Instruments.Where(x => x.Host is not null && x.Pos is not null).ToList();
        for (var k = 0; k < free.Count; k++)
        {
            free[k].FreeIndex = k;
            FreeGeometry(d, free[k], family, spacing);
        }

        var lines = new List<(ProcessLine l, int i)>();
        for (var i = 0; i < d.Lines.Count; i++)
        {
            var l = d.Lines[i];
            if (!l.Error && l.FromNode >= 0 && l.ToNode >= 0 && l.FromPort is not null && l.ToPort is not null)
                lines.Add((l, i));
        }

        var signals = d.Signals.Where(s => !s.Error).ToList();
        var sig = align + (shape is null ? "|" : $"|S{spacingY?.ToString(CultureInfo.InvariantCulture)}|") + Signature(d, lines.Select(x => x.l), signals, spacing);
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
            Nodes = d.Nodes.Select(n => n.G).Concat(free.Select(x => x.Free!)).ToList(),
            Lines = lines.Select(x => new GraphLine
            {
                From = MakeEnd(d, x.l, x.i, true),
                To = MakeEnd(d, x.l, x.i, false),
                Via = x.l.Via?.Select(p => new SKPoint(p.X * spacing, p.Y * spacing)).ToList(),
            }).ToList(),
            Signals = signals.Select(s => new GraphSignal { From = SignalEnd(d, s.FromRef), To = SignalEnd(d, s.ToRef) }).ToList(),
            Vertical = d.Vertical,
            Spacing = spacing,
            Pitch = d.Pitch,
            Unit = d.IsUnit,
            Align = align,
            Fixed = d.Fixed,
            Shape = shape,
            SpacingY = spacingY,
        };
        graph.Run();

        lock (entry)
        {
            if (entry.Graphs.TryAdd(sig, graph))
            {
                entry.Order.Enqueue(sig);
                while (entry.Order.Count > CacheSize) // 오래된 것부터(값 글자 폭이 바뀌어 생긴 이전 배치 등)
                    entry.Graphs.Remove(entry.Order.Dequeue());
            }
            else
                entry.Graphs[sig] = graph;
            entry.Runs++;
        }

        Attach(d, graph, lines, signals);
        return graph;
    }

    /// <summary>Top of the actuator of a control valve (<c>DvValve</c>/<c>DvValve3</c> <c>kind="control"</c>), in symbol coordinates (both symbols are 60 wide with the actuator in the same place).</summary>
    private static readonly SKPoint ActuatorTop = new(30, 4.5f);

    private static bool HasActuator(ProcessNode n) =>
        (n.Tag == "DvValve" && n.Def?.Name == "control") || (n.Tag == "DvValve3" && n.Def?.Name == "valve3-control");

    // 신호선 끝 — 계기는 버블, 조절 밸브는 구동부 위, 그 밖의 장비는 몸통(심볼 + 부속, 상대 쪽 면 가운데)
    private static GraphEnd SignalEnd(ProcessDiagram d, string name)
    {
        switch (SignalTarget(d, name))
        {
            case ProcessInstrument { Free: { } free } inst:
                return new GraphEnd { Node = d.Nodes.Count + inst.FreeIndex, Key = name, Pick = free.Body, PickBlocked = string.IsNullOrEmpty(inst.Value) ? null : inst.ValueSide };
            case ProcessInstrument inst:
                return new GraphEnd { Node = d.Nodes.IndexOf(inst.Host!), Key = name, Pick = inst.Rect, PickBlocked = string.IsNullOrEmpty(inst.Value) ? null : inst.ValueSide };
            case ProcessNode n when HasActuator(n):
                return new GraphEnd { Node = d.Nodes.IndexOf(n), Key = name, Local = n.Transform.MapPoint(ActuatorTop), Side = MapSide(PortSide.Top, n) };
            case ProcessNode n:
                return new GraphEnd { Node = d.Nodes.IndexOf(n), Key = name, Pick = n.Body }; // 부속(모터 등) 포함 — 심볼 윗면은 부속에 덮여 길이 없다
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

        foreach (var inst in d.Instruments.Where(x => x.Free is not null))
            PlaceFree(inst, inst.Host!, g.Nodes[d.Nodes.Count + inst.FreeIndex], g);
    }

    private static string Signature(ProcessDiagram d, IEnumerable<ProcessLine> lines, IEnumerable<ProcessSignal> signals, float spacing)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(d.Vertical ? 'V' : 'H').Append(d.Fixed ? 'F' : 'A').Append(d.Pitch).Append('T').Append(d.TextScale.ToString(CultureInfo.InvariantCulture)).Append('|').Append(spacing.ToString(CultureInfo.InvariantCulture)).Append('|');
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
            {
                if (inst.Free is { } free) // 자리는 배치가 끝나야 정해진다 — 입력만
                    sb.Append('X').Append(inst.Name).Append('@').Append(inst.Pos.ToString()).Append(free.Body.ToString()).Append(inst.FreeValue.ToString());
                else
                    sb.Append('I').Append(inst.Name).Append(inst.Rect.ToString()).Append(inst.ValueRect.ToString()).Append(inst.ValueSide); // 값 글자는 신호선 경로에만 영향
            }

            sb.Append(n.Accessories.Count);
            if (n.Pos is { } pos)
                sb.Append('@').Append(pos.X.ToString(CultureInfo.InvariantCulture)).Append(',').Append(pos.Y.ToString(CultureInfo.InvariantCulture));
            if (n.IsPort)
                sb.Append('P').Append(n.PortSide).Append(n.PortAt.ToString(CultureInfo.InvariantCulture));
            if (n.UnitPorts is { } ups)
                foreach (var up in ups)
                    sb.Append('U').Append(up.Name).Append(up.Side).Append(up.At.ToString(CultureInfo.InvariantCulture));
            sb.Append(';');
        }

        foreach (var l in lines)
        {
            sb.Append(l.FromRef).Append('>').Append(l.ToRef);
            if (l.Via is { } via)
                foreach (var p in via)
                    sb.Append('~').Append(p.X.ToString(CultureInfo.InvariantCulture)).Append(',').Append(p.Y.ToString(CultureInfo.InvariantCulture));
            sb.Append(';');
        }

        foreach (var s in signals)
            sb.Append('S').Append(s.FromRef).Append('>').Append(s.ToRef).Append(';');
        return sb.ToString();
    }

    // ── 화면 배치 ──

    /// <summary>The <c>font-size</c> at which the diagram's text is drawn at its base sizes (<see cref="ProcessDiagram.TextScale"/> 1).</summary>
    private const float DefaultFontSize = 14f;

    // 글자 배율 — 유닛 안쪽도 바깥과 같게
    private static void SetTextScale(ProcessDiagram d, float k)
    {
        d.TextScale = k;
        foreach (var n in d.Nodes)
            if (n.Unit is { } unit)
                SetTextScale(unit, k);
    }

    /// <summary>Natural size (spacing scale 1): the inner size (excluding padding).</summary>
    internal static SKSize Natural(LayoutBox box)
    {
        var d = box.Process!;
        SetTextScale(d, box.Style.FontSize > 0 ? box.Style.FontSize / DefaultFontSize : 1f);
        if (d.Fixed && d.BaseSize is { } baseSize)
            return baseSize;
        return Layout(d, box.Style.FontFamily, 1f, d.Align ?? ProcessAlign.Step).Size;
    }

    /// <summary>
    /// Quality diagnostics (Trace warnings): reports only the layout that is actually drawn (layout may be called several times during measurement, so this runs once at draw time).
    /// The same message is reported only once.
    /// </summary>
    internal static void Report(ProcessDiagram d)
    {
        if (d.Shortage is { } msg)
            Warn(msg, scope: d.Source);
        foreach (var n in d.Nodes)
        {
            if (n.Unit is { } u)
                Report(u);
        }

        if (d.Graph is { } g)
            foreach (var w in g.Warnings)
                Warn(w, scope: d.Source);

        ReportValueOverlap(d);

        foreach (var l in d.Lines)
        {
            if (l.G is not { } gl)
                continue;
            if (gl.Forced)
                Warn(d.Fixed
                    ? $"'{l.FromRef}'→'{l.ToRef}': no free path, so it overlaps other piping; move equipment (x/y) to open a gap, or move an attached box (DvAttach) standing in front of a port to another face"
                    : $"'{l.FromRef}'→'{l.ToRef}': no free path, so it overlaps other piping; move equipment with row/rank hints, move an attached box (DvAttach) standing in front of a port to another face, or enlarge the area", scope: d.Source);
            else if (gl.Crossings > 0)
                Warn(d.Fixed
                    ? $"'{l.FromRef}'→'{l.ToRef}': crosses other piping {gl.Crossings} time(s); moving equipment (x/y) can reduce this"
                    : $"'{l.FromRef}'→'{l.ToRef}': crosses other piping {gl.Crossings} time(s); a row hint can reduce this", scope: d.Source);
        }
    }

    // 값 글자는 배치에 끼지 않으므로 길면 다른 장비·이름표·계기를 덮을 수 있다 — 덮은 것을 알린다(같은 문구는 한 번)
    private static void ReportValueOverlap(ProcessDiagram d)
    {
        static SKRect Abs(ProcessNode n, SKRect r) => new(n.G.X + r.Left, n.G.Y + r.Top, n.G.X + r.Right, n.G.Y + r.Bottom);
        foreach (var inst in d.Instruments)
        {
            if (inst.Host is not { } host || inst.ValueRect.IsEmpty)
                continue;
            var v = Abs(host, inst.ValueRect);
            var hit = d.Nodes.Where(n => !n.IsPort && (n.G is not null) && (Abs(n, n.Sym).IntersectsWith(v) || !n.LabelRect.IsEmpty && Abs(n, n.LabelRect).IntersectsWith(v)))
                .Select(n => n.Name)
                .Concat(d.Instruments.Where(o => o != inst && o.Host is { } oh && (Abs(oh, o.Rect).IntersectsWith(v) || !o.ValueRect.IsEmpty && Abs(oh, o.ValueRect).IntersectsWith(v))).Select(o => o.Name))
                .FirstOrDefault();
            // 배관 — 굵기 절반만큼 넓힌 구간과 겹치면
            var half = d.Rich ? 4f : 2f;
            hit ??= d.Lines.Where(l => l.G is { Points.Count: >= 2 } && Enumerable.Range(1, l.G.Points.Count - 1).Any(k =>
                {
                    var (a, b) = (l.G.Points[k - 1], l.G.Points[k]);
                    return new SKRect(MathF.Min(a.X, b.X) - half, MathF.Min(a.Y, b.Y) - half, MathF.Max(a.X, b.X) + half, MathF.Max(a.Y, b.Y) + half).IntersectsWith(v);
                }))
                .Select(l => $"{l.FromRef}'→'{l.ToRef}")
                .FirstOrDefault();
            if (hit is not null)
                Warn($"'{inst.Name}' value text overlaps '{hit}'; value text is not part of the layout, so keep it short or move the equipment apart", scope: d.Source);
        }
    }

    /// <summary>
    /// Lays out within the inner area (<paramref name="inner"/>): if it does not fit, lays out again with reduced spacing (down to 0.5), and warns if it still does not fit.
    /// If there is space left over, centers it. Sets the positions of the equipment boxes and attached slot boxes.
    /// </summary>
    internal static void Arrange(LayoutBox box, SKRect inner)
    {
        var d = box.Process!;
        SetTextScale(d, box.Style.FontSize > 0 ? box.Style.FontSize / DefaultFontSize : 1f);
        box.ContentScale = 1f;
        box.ContentOffset = default;
        if (d.Fixed)
            ArrangeFixed(d, box.Style.FontFamily, inner);
        else
            ArrangeAuto(d, box.Style.FontFamily, inner);
        if (d.Contain)
            ScaleToFit(box, d, inner, enlarge: true);
        else if (d.Shrink && d.Shortage is not null)
            ScaleToFit(box, d, inner, enlarge: false);
    }

    /// <summary>The smallest text size the diagram draws at <see cref="ProcessDiagram.TextScale"/> 1 (instrument number and value, line and signal labels).</summary>
    internal const float SmallestText = 11f;

    // fit="shrink"(넘치면 줄이기)·fit="contain"(기준 크기 그대로 줄이기·키우기) — 그린 범위(Drawn) 전체를 영역에 맞게(비율 유지) 가운데에.
    // 장비 상자·배관은 내용 좌표 그대로, 페인트·히트테스트가 공정도 상자의 내용 배율로 옮긴다. 줄였으면 줄인 정도와 가장 작은 글자 크기를 알린다(읽히는지는 앱이 판단)
    private static void ScaleToFit(LayoutBox box, ProcessDiagram d, SKRect inner, bool enlarge)
    {
        var c = d.Drawn;
        if (c.Width <= 0 || c.Height <= 0 || inner.Width <= 0 || inner.Height <= 0)
            return;
        var s = MathF.Min(inner.Width / c.Width, inner.Height / c.Height);
        if (!enlarge && s >= 1f)
            return;
        box.ContentScale = s;
        box.ContentOffset = new SKPoint(inner.Left + (inner.Width - c.Width * s) / 2 - c.Left * s, inner.Top + (inner.Height - c.Height * s) / 2 - c.Top * s);
        if (s >= 1f)
            return; // 키운 경우 — 알릴 것 없음(기준 크기 밖으로 나간 장비 경고는 그대로)
        d.Shortage = $"Shrunk to {s * 100:0}% to fit (needs {c.Width:0}×{c.Height:0}, area is {inner.Width:0}×{inner.Height:0}); the smallest diagram text is now {SmallestDrawnText(d) * s:0.#}px. "
            + "If that is too small to read at the smallest size this screen is shown, raise DvProcess font-size, enlarge the area or split the diagram over several screens";
    }

    // 축소 전 가장 작은 글자 — 공정도가 그리는 글자와 붙은 칸(DvAttach) 안 글자(글자 배율 적용) 중 작은 것, 유닛 안쪽 포함
    private static float SmallestDrawnText(ProcessDiagram d)
    {
        var smallest = d.Text(SmallestText);
        foreach (var n in d.Nodes)
        {
            foreach (var (_, ab) in n.Attach)
                smallest = MathF.Min(smallest, SmallestBoxText(ab) * d.TextScale);
            if (n.Unit is { } u)
                smallest = MathF.Min(smallest, SmallestDrawnText(u));
        }

        return smallest;

        static float SmallestBoxText(LayoutBox b)
        {
            var m = b.ResolvedText is { Length: > 0 } ? b.Style.FontSize : float.PositiveInfinity;
            foreach (var c in b.Children)
                m = MathF.Min(m, SmallestBoxText(c));
            return m;
        }
    }

    private static void ArrangeAuto(ProcessDiagram d, string? family, SKRect inner)
    {

        // 배관 모양은 자연 크기(간격 배율 1)에서 한 번 정하고, 영역에 맞춰 위치만 축마다 늘이거나 줄인다(장비·글자 크기 그대로).
        // 창 크기마다 다시 배치하면 꺾임·갈림 자리가 바뀐다. 줄인 모양이 안 들어갈 때만 아래 후보로 다시 배치한다
        var natural = d.Align ?? ProcessAlign.Step;
        var shape = Layout(d, family, 1f, natural);
        ProcessGraph? fit = null, closest = null;
        var closestOver = float.PositiveInfinity;
        // 자동 배치 위치에는 가로세로 비율의 뜻이 없다 — 축마다 영역만큼. 장비 크기는 그대로라
        // 크기 ≈ 자연 크기 + 장비 퍼짐 × (배율 - 1). 배관이 장비 밖으로 나간 자리 등으로 어긋나면 남은 만큼 몇 번 더 맞춘다
        var (gx, gy) = ProcessGraph.Spread(shape);
        float Next(float s, float size, float target, float gaps) => gaps > 0.5f ? s + (target - size) / gaps : s;
        var sx = Next(1f, shape.Size.Width, inner.Width, gx);
        var sy = Next(1f, shape.Size.Height, inner.Height, gy);
        var (okX, okY) = (1f, 1f); // 모양이 지켜진 가장 작은 배율
        for (var k = 0; k < 10; k++)
        {
            (sx, sy) = (MathF.Max(sx, MinPositionScale), MathF.Max(sy, MinPositionScale));
            var st = MathF.Abs(sx - 1f) < 0.001f && MathF.Abs(sy - 1f) < 0.001f ? shape : Layout(d, family, sx, natural, shape, sy);
            if (st.Unfit)
            {
                // 줄여서 접혔다 — 줄인 축을 마지막으로 들어간 배율(처음엔 자연 크기) 쪽으로 반씩 되돌려 모양이 지켜지는 가장 작은 배율을 찾는다
                if (sx >= 1f && sy >= 1f)
                    break;
                (sx, sy) = ((sx + MathF.Max(sx, okX)) / 2, (sy + MathF.Max(sy, okY)) / 2);
                continue;
            }

            (okX, okY) = (MathF.Min(okX, sx), MathF.Min(okY, sy));

            var over = MathF.Max(0, st.Size.Width - inner.Width) + MathF.Max(0, st.Size.Height - inner.Height);
            if (over < closestOver)
                (closest, closestOver) = (st, over);
            var (ox, oy) = (st.Size.Width / inner.Width, st.Size.Height / inner.Height);
            if (ox <= 1.0005f && oy <= 1.0005f)
            {
                fit = st;
                if (ox >= 0.99f && oy >= 0.99f)
                    break; // 거의 꽉 찼다
            }

            var (nx, ny) = (MathF.Max(Next(sx, st.Size.Width, inner.Width, gx), MinPositionScale), MathF.Max(Next(sy, st.Size.Height, inner.Height, gy), MinPositionScale));
            if (MathF.Abs(nx - sx) < 0.0005f && MathF.Abs(ny - sy) < 0.0005f)
                break; // 더 맞출 수 없다(최소 배율이거나 퍼짐이 없다)
            (sx, sy) = (nx, ny);
        }

        if (fit is not null)
        {
            Use(fit, null);
            return;
        }

        // 마지막으로 계산한 배치가 붙어 있으므로 고른 것으로 다시(캐시) — 같은 모양이면 그것을, 아니면 기존 결과를 쓴다
        void Use(ProcessGraph g, string? shortage)
        {
            g = g == shape ? Layout(d, family, 1f, natural) : g.Shape is not null ? Layout(d, family, g.Spacing, natural, shape, g.SpacingY) : g;
            d.Shortage = shortage;
            d.Graph = g;
            d.Origin = new SKPoint(inner.Left + MathF.Max(0, (inner.Width - g.Size.Width) / 2),
                inner.Top + MathF.Max(0, (inner.Height - g.Size.Height) / 2));
            d.Drawn = SKRect.Create(d.Origin.X, d.Origin.Y, g.Size.Width, g.Size.Height);
            Place(d);
        }

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

        // 어느 후보도 안 들어가면 모양을 바꿀 까닭이 없다 — 자연 크기 모양을 가장 많이 줄인 것으로(넘친다)
        if (pick is null && closest is not null)
        {
            Use(closest, $"Not enough space: needs {closest.Size.Width:0}×{closest.Size.Height:0}, area is {inner.Width:0}×{inner.Height:0} (overflows even at minimum spacing). Enlarge the area, split the diagram over several screens, use less equipment, or set fit=\"shrink\" to draw it smaller");
            return;
        }

        var (useAlign, useSpacing) = pick ?? least;
        var g = Layout(d, family, useSpacing, useAlign); // 고른 후보로 다시(캐시) — 장비·선에 그 결과를 붙인다
        // 다시 배치해서 들어갔을 때만 — 어느 후보도 안 들어가면 아래 '공간 부족' 하나로 알린다
        const string reshaped = "The piping no longer fits the diagram's natural-size shape in this area, so it is laid out again and may look different; resize the area to keep the shape";
        if (pick is not null && !g.Warnings.Contains(reshaped))
            g.Warnings.Add(reshaped);

        d.Shortage = g.Size.Width > inner.Width + 0.5f || g.Size.Height > inner.Height + 0.5f
            ? $"Not enough space: needs {g.Size.Width:0}×{g.Size.Height:0}, area is {inner.Width:0}×{inner.Height:0} (overflows even at minimum spacing). Enlarge the area, split the diagram over several screens, use less equipment, or set fit=\"shrink\" to draw it smaller"
            : null;
        d.Graph = g;
        d.Origin = new SKPoint(inner.Left + MathF.Max(0, (inner.Width - g.Size.Width) / 2),
            inner.Top + MathF.Max(0, (inner.Height - g.Size.Height) / 2));
        d.Drawn = SKRect.Create(d.Origin.X, d.Origin.Y, g.Size.Width, g.Size.Height);

        Place(d);
    }

    // 자동 배치 모양 줄이기: 위치를 이 배율 아래로는 줄이지 않는다(그 전에 대개 모양이 안 들어간다)
    private const float MinPositionScale = 0.2f;

    private static bool Fits(ProcessGraph g, SKRect inner) => g.Size.Width <= inner.Width + 0.5f && g.Size.Height <= inner.Height + 0.5f;

    // 좌표 배치 — 원점 = 영역 왼쪽 위. base-size 가 있으면 위치만 영역에 맞춰 비례(장비·글자 크기 그대로)하고 그 상자를 가운데에
    private static void ArrangeFixed(ProcessDiagram d, string? family, SKRect inner)
    {
        var scale = 1f;
        var origin = new SKPoint(inner.Left, inner.Top);
        if (d.Contain)
        {
            ArrangeFixedContain(d, family, origin);
            return;
        }

        if (d.BaseSize is { } b && inner.Width > 0 && inner.Height > 0)
        {
            scale = MathF.Min(inner.Width / b.Width, inner.Height / b.Height);
            origin = new SKPoint(inner.Left + (inner.Width - b.Width * scale) / 2, inner.Top + (inner.Height - b.Height * scale) / 2);
        }

        // 배관 모양(꺾임)은 기준 크기에서 한 번 정하고, 영역 크기는 그 모양을 늘이기만 한다 — 창 크기마다 모양이 바뀌지 않게
        var g = d.BaseSize is not null && MathF.Abs(scale - 1f) > 0.0001f
            ? Layout(d, family, scale, ProcessAlign.Step, Layout(d, family, 1f, ProcessAlign.Step))
            : Layout(d, family, scale, ProcessAlign.Step);
        var right = origin.X + g.Size.Width - ProcessGraph.Margin;
        var bottom = origin.Y + g.Size.Height - ProcessGraph.Margin;
        var real = g.Nodes.Where(n => !n.Pinned).ToList();
        var left = real.Count > 0 ? origin.X + real.Min(n => n.X) : inner.Left;
        var top = real.Count > 0 ? origin.Y + real.Min(n => n.Y) : inner.Top;
        d.Shortage = right > inner.Right + 0.5f || bottom > inner.Bottom + 0.5f || left < inner.Left - 0.5f || top < inner.Top - 0.5f
            ? $"Not enough space: equipment reaches {right - inner.Left:0}×{bottom - inner.Top:0}, area is {inner.Width:0}×{inner.Height:0}. Move equipment (x/y), set base-size to the size the coordinates were taken at, enlarge the area, or set fit=\"shrink\" to draw it smaller"
            : null;
        d.Graph = g;
        d.Origin = origin;
        d.Drawn = new SKRect(MathF.Min(inner.Left, left), MathF.Min(inner.Top, top), MathF.Max(inner.Right, right), MathF.Max(inner.Bottom, bottom));
        Place(d);
    }

    // 붙은 칸 — 자리(r, 글자 배율을 곱한 크기)의 왼쪽 위에 자연 크기로 배치하고, 상자는 그 자리, 안쪽은 글자 배율로 그린다
    private static void PlaceAttach(ProcessDiagram d, LayoutBox ab, SKRect r, float ox, float oy)
    {
        var k = d.TextScale;
        var (x, y) = (ox + r.Left, oy + r.Top);
        LayoutEngine.ArrangeBox(ab, x, y, r.Width / k, r.Height / k);
        ab.ContentScale = 1f;
        ab.ContentOffset = default;
        if (MathF.Abs(k - 1f) < 0.0001f)
            return;
        ab.Rect = SKRect.Create(x, y, r.Width, r.Height);
        ab.ContentScale = k;
        ab.ContentOffset = new SKPoint(x - x * k, y - y * k); // 왼쪽 위(x, y) 기준
    }

    // 좌표 배치 + fit="contain" — 좌표 그대로(배율 1) 배치하고, 그릴 범위 = base-size 상자(없으면 원점부터 내용 끝 + 여백).
    // 영역에 맞추는 것은 Arrange 의 ScaleToFit(줄이기·키우기, 가운데)
    private static void ArrangeFixedContain(ProcessDiagram d, string? family, SKPoint origin)
    {
        var g = Layout(d, family, 1f, ProcessAlign.Step);
        var size = d.BaseSize ?? g.Size;
        var right = g.Size.Width - ProcessGraph.Margin;
        var bottom = g.Size.Height - ProcessGraph.Margin;
        d.Shortage = d.BaseSize is { } b && (right > b.Width + 0.5f || bottom > b.Height + 0.5f)
            ? $"Equipment reaches {right:0}×{bottom:0}, outside base-size {b.Width:0}×{b.Height:0}; it is drawn outside the scaled drawing. Move equipment (x/y) or enlarge base-size"
            : null;
        d.Graph = g;
        d.Origin = origin;
        d.Drawn = SKRect.Create(origin.X, origin.Y, size.Width, size.Height);
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
                PlaceAttach(d, n.Attach[i].Box, n.AttachRects[i], ox, oy);

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
