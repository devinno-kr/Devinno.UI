using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using SkiaSharp;

namespace Devinno.UI.Process;

/// <summary>
/// One drawn pipe of a laid-out process diagram (<see cref="ProcessInspector.PipesOf"/>).
/// </summary>
/// <param name="Line">The <c>DvLine</c> element the pipe was drawn for.</param>
/// <param name="Points">The pipe as drawn: an orthogonal polyline from the start nozzle (<c>from</c>) to the end nozzle (<c>to</c>), in the content coordinates of the
/// <c>DvProcess</c> box — the same space as the equipment boxes' <see cref="LayoutBox.Rect"/> (drawn = point × <see cref="LayoutBox.ContentScale"/> + <see cref="LayoutBox.ContentOffset"/>).
/// The first and last points are the real nozzle positions, face ports included.</param>
public sealed record ProcessPipe(ElementNode Line, IReadOnlyList<SKPoint> Points);

/// <summary>
/// Reads process diagram definitions and layout results — for tools such as screen editors that draw port handles or edit pipe routes.
/// </summary>
public static class ProcessInspector
{
    /// <summary>
    /// The ports of an equipment symbol: a built-in tag (<c>DvVessel</c>, <c>DvPump</c>, <c>DvValve</c>, <c>DvValve3</c>, <c>DvInline</c>, <c>DvExchanger</c>) with its <c>kind</c>,
    /// or a <c>DvSymbol</c> registered in <paramref name="registry"/> — the same lookup a <c>DvProcess</c> uses. Positions are relative to the symbol's drawing box,
    /// before the equipment's <c>flip</c> and <c>rotate</c> (flip first, then rotate clockwise).
    /// </summary>
    /// <param name="tag">The equipment tag (<c>DvValve</c>, or a registered symbol name).</param>
    /// <param name="kind">The <c>kind</c> attribute; <see langword="null"/> or empty = the tag's default kind. Ignored for registered symbols.</param>
    /// <param name="registry">The registry with the app's <c>DvSymbol</c> definitions, or <see langword="null"/> for built-in equipment only.</param>
    /// <returns>The ports in definition order, or <see langword="null"/> for an unknown tag or kind. Process units (<c>DvUnit</c> components) are not symbols: their ports are their <c>DvPort</c> children.</returns>
    /// <example><c>PortsOf("DvValve", "ball")</c> → <c>in</c> (Left, 0.667), <c>out</c> (Right, 0.667).</example>
    public static IReadOnlyList<SymbolPort>? PortsOf(string tag, string? kind = null, ComponentRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (BuiltInSymbols.Kinds.ContainsKey(tag))
            return BuiltInSymbols.Get(tag, kind)?.Ports.ToArray();
        if (registry is not null && registry.TryGetSymbol(tag, out var user) && user is not null)
            return user.Ports.ToArray();
        return null;
    }

    /// <summary>
    /// The pipes of a laid-out <c>DvProcess</c>, as drawn — automatic routes and <c>via</c> routes alike. Lines that could not be drawn (unknown <c>from</c>/<c>to</c>) are left out.
    /// Pipes inside process units are not included.
    /// </summary>
    /// <param name="process">The <c>DvProcess</c> box in a tree from <see cref="LayoutEngine.Layout(Markup.ElementNode, object?, float, float, ComponentRegistry?)"/>.</param>
    /// <returns>One entry per drawn <c>DvLine</c>, in markup order; empty for any other box.</returns>
    public static IReadOnlyList<ProcessPipe> PipesOf(LayoutBox process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (process.Process is not { } d)
            return [];
        var list = new List<ProcessPipe>();
        foreach (var l in d.Lines)
        {
            if (l.Node is { } node && l.G is { Points.Count: >= 2 } g)
                list.Add(new ProcessPipe(node, g.Points.Select(p => new SKPoint(d.Origin.X + p.X, d.Origin.Y + p.Y)).ToArray()));
        }
        return list;
    }
}
