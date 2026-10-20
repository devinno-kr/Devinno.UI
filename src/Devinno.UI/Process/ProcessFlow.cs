using System.Runtime.CompilerServices;

namespace Devinno.UI.Process;

/// <summary>
/// Flow inference — which pipes currently have flow (an inference for display, not a measurement).
/// </summary>
/// <remarks>
/// <para><b>Sources</b>: end points used as a line's <c>from</c> (coming in from outside), and vessels with a level greater than 0. <b>Sinks</b>: end points used as a line's <c>to</c>, and vessels.</para>
/// <para>Paths: lines go in the <c>from</c> → <c>to</c> direction; inside equipment, through the symbol's <c>DvPass</c> (when its condition parameter is true — pump running, valve open); unit ports connect inside and outside.
/// Vessels are buffers and do not pass through internally (inflow and outflow are separate).</para>
/// <para>A line flows <b>when it is reached from a source (forward) and continues to a sink (backward)</b> — a pipe that ends at a closed valve does not flow.
/// A line with <c>flow</c> set follows that value: <c>false</c> blocks the line and also stops what is downstream, and <c>true</c> makes the line flow and connects it both forward and backward.</para>
/// <para>Computed each time values change and the screen is rebuilt (independent of layout, a few microseconds).</para>
/// </remarks>
internal static class ProcessFlow
{
    public static void Compute(ProcessDiagram root)
    {
        var fwd = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var bwd = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var sources = new List<string>();
        var sinks = new List<string>();
        var lines = new List<(ProcessLine Line, string A, string B)>();

        static string K(ProcessDiagram d, int node, string port) => $"{RuntimeHelpers.GetHashCode(d)}:{node}:{port}";

        void Edge(string a, string b)
        {
            Add(fwd, a, b);
            Add(bwd, b, a);
        }

        static void Add(Dictionary<string, List<string>> map, string a, string b)
        {
            if (!map.TryGetValue(a, out var list))
                map[a] = list = new List<string>();
            list.Add(b);
        }

        void Walk(ProcessDiagram d)
        {
            for (var i = 0; i < d.Nodes.Count; i++)
            {
                var n = d.Nodes[i];
                if (n.Terminal)
                {
                    (n.TerminalSource ? sources : sinks).Add(K(d, i, "end"));
                    continue;
                }

                if (n.Unit is { } unit)
                {
                    Walk(unit);
                    // 유닛 포트 = 바깥 선 끝(유닛.포트) ↔ 안쪽 포트 자리 — 양쪽으로 통한다
                    for (var j = 0; j < unit.Nodes.Count; j++)
                    {
                        if (!unit.Nodes[j].IsPort)
                            continue;
                        var outer = K(d, i, unit.Nodes[j].Name);
                        var inner = K(unit, j, "end");
                        Edge(outer, inner);
                        Edge(inner, outer);
                    }

                    continue;
                }

                if (n.Def is not { } def)
                    continue;

                foreach (var pass in def.Passes)
                {
                    if (pass.When is null || n.On(pass.When))
                        Edge(K(d, i, pass.From), K(d, i, pass.To));
                }

                // 용기(채움이 있는 심볼) — 모든 포트가 받는 곳, 수위가 있으면 소스
                if (def.FillValue is not null)
                {
                    foreach (var p in def.Ports)
                    {
                        sinks.Add(K(d, i, p.Name));
                        if (n.Level > 0)
                            sources.Add(K(d, i, p.Name));
                    }
                }
            }

            foreach (var l in d.Lines)
            {
                l.Flowing = false;
                if (l.Error || l.FromNode < 0 || l.ToNode < 0 || l.FromPort is null || l.ToPort is null)
                    continue;
                var a = K(d, l.FromNode, l.FromPort);
                var b = K(d, l.ToNode, l.ToPort);
                lines.Add((l, a, b));
                if (l.FlowOverride == false)
                    continue; // 막힘 — 뒤쪽도 멈춘다
                Edge(a, b);
                if (l.FlowOverride == true)
                {
                    sources.Add(b); // 흐름이 있다고 알려 준 선 — 뒤쪽으로 이어지고
                    sinks.Add(a);   // 앞쪽에서 들어온 것으로 본다
                }
            }
        }

        Walk(root);
        var reached = Reach(fwd, sources);
        var drained = Reach(bwd, sinks);
        foreach (var (l, a, b) in lines)
            l.Flowing = l.FlowOverride ?? (reached.Contains(a) && drained.Contains(b));
    }

    private static HashSet<string> Reach(Dictionary<string, List<string>> map, List<string> seeds)
    {
        var seen = new HashSet<string>(seeds, StringComparer.Ordinal);
        var queue = new Queue<string>(seen);
        while (queue.Count > 0)
        {
            var k = queue.Dequeue();
            if (!map.TryGetValue(k, out var next))
                continue;
            foreach (var m in next)
            {
                if (seen.Add(m))
                    queue.Enqueue(m);
            }
        }

        return seen;
    }
}
