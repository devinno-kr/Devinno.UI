using System.Globalization;
using Devinno.UI.Markup;
using SkiaSharp;

namespace Devinno.UI.Process;

/// <summary>The side a symbol port attaches to.</summary>
internal enum PortSide
{
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>
/// A symbol port — side (<see cref="Side"/>) + position on that side (<see cref="At"/>, 0–1) + role.
/// <see cref="Face"/> is a port that accepts multiple lines across the whole side and divides positions among them (top and side of built-in vessels);
/// <see cref="EitherSide"/> is a port that picks whichever of left or right faces the connected equipment (vessel side).
/// </summary>
internal sealed record SymbolPort(string Name, PortSide Side, float At, string Role, bool Face = false, bool EitherSide = false);

/// <summary>A symbol drawing layer — fill kind (<see cref="Class"/>) and path. <see cref="When"/> is the parameter a state-color layer follows.</summary>
internal sealed record SymbolLayer(string Class, SKPath Path, string? When);

/// <summary>A flow rule — flow passes from the <see cref="From"/> port to the <see cref="To"/> port (only when the <see cref="When"/> parameter is true).</summary>
internal sealed record SymbolPass(string From, string To, string? When);

/// <summary>A symbol parameter (a state value received from outside).</summary>
internal sealed record SymbolParam(string Name, string? Type, string? Default);

/// <summary>
/// A <c>&lt;DvSymbol&gt;</c> definition — the shape, ports and flow rules of process diagram equipment. Built-in equipment is defined in the same format (<see cref="BuiltInSymbols"/>).
/// </summary>
internal sealed class SymbolDef
{
    public required string Name { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public List<SymbolParam> Params { get; } = new();
    public List<SymbolLayer> Layers { get; } = new();
    public List<SymbolPort> Ports { get; } = new();
    public List<SymbolPass> Passes { get; } = new();

    /// <summary>Level fill — clipped to the shape of this class layer and filled by the <see cref="FillValue"/> parameter (0–100).</summary>
    public string? FillClip { get; init; }
    public string? FillValue { get; init; }

    public SymbolPort? Port(string name) => Ports.FirstOrDefault(p => p.Name == name);

    private static readonly HashSet<string> LayerClasses = new(StringComparer.Ordinal)
    {
        "vessel", "state", "accent", "detail", "dash", "screen", "glass",
    };

    /// <summary>
    /// Reads a <c>&lt;DvSymbol name view-box&gt;</c> node. If the definition is invalid, throws an <see cref="ArgumentException"/> describing what is wrong.
    /// </summary>
    public static SymbolDef Parse(ElementNode node)
    {
        if (node.Tag != "DvSymbol")
            throw new ArgumentException("Root must be <DvSymbol>.");
        var name = node.Attributes.GetValueOrDefault("name");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("<DvSymbol> requires a name attribute.");

        var vb = (node.Attributes.GetValueOrDefault("view-box") ?? "")
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (vb.Length != 4 || !TryNum(vb[2], out var w) || !TryNum(vb[3], out var h) || w <= 0 || h <= 0)
            throw new ArgumentException($"Symbol '{name}': view-box must be \"0 0 width height\" with positive size.");

        var fill = node.Children.FirstOrDefault(c => c.Tag == "DvFill");
        var def = new SymbolDef
        {
            Name = name, Width = w, Height = h,
            FillClip = fill?.Attributes.GetValueOrDefault("clip"),
            FillValue = fill?.Attributes.GetValueOrDefault("value"),
        };

        foreach (var ch in node.Children)
        {
            switch (ch.Tag)
            {
                case "DvParam":
                    def.Params.Add(new SymbolParam(ch.Attributes.GetValueOrDefault("name") ?? "",
                        ch.Attributes.GetValueOrDefault("type"), ch.Attributes.GetValueOrDefault("default")));
                    break;
                case "DvShape":
                {
                    var cls = ch.Attributes.GetValueOrDefault("class") ?? "";
                    if (!LayerClasses.Contains(cls))
                        throw new ArgumentException($"Symbol '{name}': DvShape class '{cls}' is not one of {string.Join(", ", LayerClasses)}.");
                    var data = ch.Attributes.GetValueOrDefault("path") ?? "";
                    var path = SKPath.ParseSvgPathData(data)
                               ?? throw new ArgumentException($"Symbol '{name}': DvShape path is not valid SVG path data: '{data}'.");
                    def.Layers.Add(new SymbolLayer(cls, path, ch.Attributes.GetValueOrDefault("when")));
                    break;
                }
                case "DvPort":
                {
                    var pn = ch.Attributes.GetValueOrDefault("name");
                    if (string.IsNullOrWhiteSpace(pn))
                        throw new ArgumentException($"Symbol '{name}': DvPort requires a name.");
                    if (def.Ports.Any(p => p.Name == pn))
                        throw new ArgumentException($"Symbol '{name}': duplicate port '{pn}'.");
                    var side = ParseSide(ch.Attributes.GetValueOrDefault("side"))
                               ?? throw new ArgumentException($"Symbol '{name}': port '{pn}' side must be left, top, right or bottom.");
                    var atRaw = ch.Attributes.GetValueOrDefault("at") ?? "0.5";
                    if (!TryNum(atRaw, out var at) || at < 0 || at > 1)
                        throw new ArgumentException($"Symbol '{name}': port '{pn}' at must be 0..1 (got '{atRaw}').");
                    var role = ch.Attributes.GetValueOrDefault("role") ?? "inout";
                    if (role is not ("in" or "out" or "inout"))
                        throw new ArgumentException($"Symbol '{name}': port '{pn}' role must be in, out or inout.");
                    def.Ports.Add(new SymbolPort(pn, side, at, role));
                    break;
                }
                case "DvPass":
                    def.Passes.Add(new SymbolPass(ch.Attributes.GetValueOrDefault("from") ?? "",
                        ch.Attributes.GetValueOrDefault("to") ?? "", ch.Attributes.GetValueOrDefault("when")));
                    break;
                case "DvFill":
                    break;
                default:
                    throw new ArgumentException($"Symbol '{name}': unknown child <{ch.Tag}> (DvParam, DvShape, DvPort, DvPass, DvFill).");
            }
        }

        foreach (var pass in def.Passes)
        {
            if (def.Port(pass.From) is null || def.Port(pass.To) is null)
                throw new ArgumentException($"Symbol '{name}': DvPass {pass.From}→{pass.To} names a port that does not exist.");
        }

        if (def.FillClip is not null && def.Layers.All(l => l.Class != def.FillClip))
            throw new ArgumentException($"Symbol '{name}': DvFill clip '{def.FillClip}' matches no DvShape class.");
        return def;
    }

    internal static PortSide? ParseSide(string? s) => s switch
    {
        "left" => PortSide.Left,
        "top" => PortSide.Top,
        "right" => PortSide.Right,
        "bottom" => PortSide.Bottom,
        _ => null,
    };

    private static bool TryNum(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}
