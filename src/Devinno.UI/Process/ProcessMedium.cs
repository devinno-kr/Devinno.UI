using System.Runtime.CompilerServices;

namespace Devinno.UI.Process;

/// <summary>
/// Medium color: propagates <c>DvLine medium</c> along the flow direction (color only; unrelated to layout or flow decisions).
/// </summary>
/// <remarks>
/// <para>Starting from the lines that specify a medium, it spreads in the <c>from</c> → <c>to</c> direction; inside equipment it goes through every <c>DvPass</c> of the symbol (regardless of state, since the pipe behind a closed valve is still the same pipe),
/// and unit ports connect inside and outside. It stops at vessels (where contents mix), end points, and lines that specify their own medium.</para>
/// <para>Media are propagated in the order of the lines that specify them (declaration order); a line already reached by another medium keeps it (the earlier one wins) and a Trace warning is written.</para>
/// </remarks>
internal static class ProcessMedium
{
    /// <summary>Available media, based on the KS A 0503 pipe identification colors.</summary>
    internal static readonly string[] Names = ["water", "steam", "air", "gas", "oil", "acid"];

    public static void Compute(ProcessDiagram root)
    {
        var next = new Dictionary<string, List<string>>(StringComparer.Ordinal); // 포트 → 포트(장비 안·유닛 포트)
        var outgoing = new Dictionary<string, List<ProcessLine>>(StringComparer.Ordinal); // 포트 → 그 포트에서 나가는 선
        var ends = new Dictionary<ProcessLine, string>();
        var seeds = new List<ProcessLine>();

        static string K(ProcessDiagram d, int node, string port) => $"{RuntimeHelpers.GetHashCode(d)}:{node}:{port}";

        static void Add<T>(Dictionary<string, List<T>> map, string a, T b)
        {
            if (!map.TryGetValue(a, out var list))
                map[a] = list = new List<T>();
            list.Add(b);
        }

        void Walk(ProcessDiagram d)
        {
            for (var i = 0; i < d.Nodes.Count; i++)
            {
                var n = d.Nodes[i];
                if (n.Unit is { } unit)
                {
                    Walk(unit);
                    for (var j = 0; j < unit.Nodes.Count; j++)
                    {
                        if (!unit.Nodes[j].IsPort)
                            continue;
                        Add(next, K(d, i, unit.Nodes[j].Name), K(unit, j, "end"));
                        Add(next, K(unit, j, "end"), K(d, i, unit.Nodes[j].Name));
                    }

                    continue;
                }

                if (n.Def is { } def)
                {
                    foreach (var pass in def.Passes)
                        Add(next, K(d, i, pass.From), K(d, i, pass.To));
                }
            }

            foreach (var l in d.Lines)
            {
                l.Medium = null;
                if (l.Error || l.FromNode < 0 || l.ToNode < 0 || l.FromPort is null || l.ToPort is null)
                    continue;
                Add(outgoing, K(d, l.FromNode, l.FromPort), l);
                ends[l] = K(d, l.ToNode, l.ToPort);
                if (l.MediumSet is not null)
                    seeds.Add(l);
            }
        }

        Walk(root);

        foreach (var seed in seeds)
        {
            var medium = seed.MediumSet!;
            seed.Medium = medium;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            if (ends.TryGetValue(seed, out var first))
                queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var port = queue.Dequeue();
                if (!seen.Add(port))
                    continue;
                if (next.TryGetValue(port, out var inside))
                {
                    foreach (var p in inside)
                        queue.Enqueue(p);
                }

                if (!outgoing.TryGetValue(port, out var lines))
                    continue;
                foreach (var l in lines)
                {
                    if (l.MediumSet is not null)
                        continue; // 자기 매체를 적은 선 — 거기서부터는 그 매체
                    if (l.Medium is { } other)
                    {
                        if (other != medium)
                            ProcessBuilder.Warn($"'{l.FromRef}'→'{l.ToRef}': media '{other}' and '{medium}' meet; painting with '{other}', which was written first. Set medium directly on the lines after the junction");
                        continue;
                    }

                    l.Medium = medium;
                    if (ends.TryGetValue(l, out var e))
                        queue.Enqueue(e);
                }
            }
        }
    }
}
