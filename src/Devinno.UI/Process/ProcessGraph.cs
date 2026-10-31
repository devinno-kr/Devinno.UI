using SkiaSharp;

namespace Devinno.UI.Process;

/// <summary>
/// Row alignment of the next equipment connected to a port perpendicular to the flow (the bottom port in left→right flow, the right port in top→bottom flow).
/// <see cref="Step"/> = align outside the port (stair-step, one bend), <see cref="Row"/> = align to the equipment's center row (low and wide, U-shaped bend).
/// Ports entering from above (from the front face) are aligned outside in both modes.
/// </summary>
internal enum ProcessAlign
{
    Step,
    Row,
}

/// <summary>Layout input equipment — footprint size and, within it, the body, name label and attached cells (areas pipes avoid).</summary>
internal sealed class GraphNode
{
    /// <summary>Footprint size (px).</summary>
    public float W, H;

    /// <summary>Symbol area (including accessories) — coordinates relative to the footprint. Pipes do not pass through it.</summary>
    public SKRect Body;

    /// <summary>Name label and attached cell areas — relative to the footprint. Pipes avoid them.</summary>
    public List<SKRect> Extras { get; } = new();

    public int? Row, Rank;

    /// <summary>Coordinate placement: where the center of <see cref="Body"/> goes (px, already scaled). <see langword="null"/> in automatic placement.</summary>
    public SKPoint? Pos;

    /// <summary>The equipment name used in diagnostic messages.</summary>
    public string? Name;

    /// <summary>
    /// A unit outer port (<c>DvPort</c>) — does not take part in layout; after the in-unit layout finishes, it is placed on the <see cref="PinSide"/> face of the unit frame (<see cref="ProcessGraph.Frame"/>)
    /// at position <see cref="PinAt"/> (0–1). Size 0, not an obstacle.
    /// </summary>
    public bool Pinned;
    public PortSide PinSide;
    public float PinAt;

    /// <summary>Result: top-left of the footprint (px, relative to the process diagram content).</summary>
    public float X, Y;

    internal SKRect Abs(SKRect local) => new(X + local.Left, Y + local.Top, X + local.Right, Y + local.Bottom);
}

/// <summary>A line end — a fixed port (point + face) or a face port (gets a slot shared out along the face).</summary>
internal sealed class GraphEnd
{
    public int Node;
    public required string Key;

    /// <summary>Fixed port: the port point (relative to the footprint). Face port: unused.</summary>
    public SKPoint Local;
    public PortSide Side;

    /// <summary>Face port — the symbol area (relative to the footprint). Slots are shared out avoiding things attached outside the face (accessories, attached cells).</summary>
    public bool Face, EitherSide;
    public SKRect Sym;

    /// <summary>Result: the port point (relative to the process diagram content) and the exit face.</summary>
    public SKPoint Point;
    public PortSide Out;

    /// <summary>
    /// Signal line end — the position is picked after layout: the center of the face of this rectangle (relative to the footprint; an instrument bubble or equipment symbol area) that faces the other end.
    /// If <see langword="null"/>, a fixed end (<see cref="Local"/>, <see cref="Side"/>).
    /// </summary>
    public SKRect? Pick;

    /// <summary>The picked end does not use the right face (the instrument value text is to the right of the bubble) — the face opposite the equipment is used instead.</summary>
    public bool PickNoRight;

    /// <summary>Length of the straight segment out of the port (default 18; 8 for unit frame ports — so it does not overlap the straight ends of inner equipment). Nearly equal coordinates are aligned by lengthening or shortening it.</summary>
    public float StubLen = 18f;

    public SKPoint StubEnd
    {
        get
        {
            var (dx, dy) = Out switch { PortSide.Left => (-1f, 0f), PortSide.Right => (1f, 0f), PortSide.Top => (0f, -1f), _ => (0f, 1f) };
            return new SKPoint(Point.X + dx * StubLen, Point.Y + dy * StubLen);
        }
    }
}

internal sealed class GraphLine
{
    public required GraphEnd From, To;
    public int Net;

    /// <summary>Result: orthogonal polyline from port point to port point.</summary>
    public List<SKPoint> Points { get; } = new();

    /// <summary>Result: points where this line crosses other pipes (where jumps are drawn).</summary>
    public List<SKPoint> Hops { get; } = new();

    public int Crossings, Bends;
    public bool Forced;
}

/// <summary>Signal line — after all pipes are routed, connects orthogonally while avoiding equipment bodies, name labels and instruments. No jumps where it crosses pipes.</summary>
internal sealed class GraphSignal
{
    public required GraphEnd From, To;

    /// <summary>Result: orthogonal polyline from end point to end point.</summary>
    public List<SKPoint> Points { get; } = new();
}

/// <summary>
/// Process diagram layout and routing — layered layout (cycle removal → rank assignment → crossing reduction → coordinates), then orthogonal paths (A*) on a grid.
/// The same input always gives the same result (no randomness). Different pipe groups (nets) do not overlap each other and do not pass through equipment bodies.
/// </summary>
internal sealed class ProcessGraph
{
    public required List<GraphNode> Nodes;
    public required List<GraphLine> Lines;

    /// <summary>Signal lines — they do not take part in equipment layout.</summary>
    public List<GraphSignal> Signals { get; init; } = new();
    public bool Vertical;

    /// <summary>Spacing scale (1 = default; reduced when space is short).</summary>
    public float Spacing = 1f;

    /// <summary>Automatic placement with <see cref="Shape"/>: the vertical position scale (<see cref="Spacing"/> is the horizontal one) — each axis is stretched or shrunk as far as the area needs.</summary>
    public float? SpacingY;

    /// <summary>Minimum gap between parallel pipes (center to center).</summary>
    public float Pitch = 16f;

    /// <summary>Row alignment mode for perpendicular ports.</summary>
    public ProcessAlign Align = ProcessAlign.Step;

    /// <summary>Coordinate placement — nodes go to <see cref="GraphNode.Pos"/> instead of the layered layout, and coordinates are not shifted (origin = the area's top left).</summary>
    public bool Fixed;

    /// <summary>
    /// The layout already routed at the reference size — <c>base-size</c> in coordinate placement, the natural size (spacing 1) in automatic placement.
    /// Pipes and signal lines are not routed again — their bends are kept and only stretched to the scaled positions, so the drawing keeps its shape at every area size.
    /// </summary>
    public ProcessGraph? Shape;

    /// <summary>Result (automatic placement with <see cref="Shape"/>): the shape does not fit at this scale (a segment folds back, a pipe runs into equipment or pipe groups touch) — nothing is routed.</summary>
    public bool Unfit;

    // 좌표 배치에서 고른 맞춤 쌍: 장비 → (들어오는 선 번호, x 맞춤 여부)
    private Dictionary<int, (int Line, bool AlongX)> SnapAnchors { get; } = new();

    /// <summary>Coordinate placement: two facing fixed ports this close (px) across the line are lined up by moving the downstream equipment — removes small jogs from ports at different heights.</summary>
    private const float SnapTolerance = 16f;

    public const float Clearance = 10f;

    /// <summary>Margin outside the content (so equipment does not touch the background edge).</summary>
    public const float Margin = 12f;

    /// <summary>Gap between the unit frame and the inner equipment — room for pipes to come straight out of frame ports and bend.</summary>
    public const float PortMargin = Stub + 2 * Lattice;

    /// <summary>In-unit layout — pins ports to the frame, restricts paths to inside the frame, and size = frame.</summary>
    public bool Unit;

    /// <summary>Result (unit): frame = inner equipment footprint + <see cref="PortMargin"/>. Starts at (0, 0) after normalization.</summary>
    public SKRect Frame;
    public const float Lattice = 8f;
    private const float Stub = Clearance + Lattice;
    private const float BendCost = 30f, CrossCost = 40f, ReuseFactor = 0.2f;

    /// <summary>Result: total content size (px).</summary>
    public SKSize Size;

    public List<string> Warnings { get; } = new();

    public void Run()
    {
        if (Nodes.All(n => n.Pinned))
        {
            // 장비가 없다(빈 공정도·포트만 있는 유닛) — 유닛은 최소 테두리
            if (Unit)
            {
                Frame = new SKRect(0, 0, 2 * PortMargin, 2 * PortMargin);
                PinPorts();
                Size = new SKSize(Frame.Width, Frame.Height);
            }

            return;
        }

        if (Shape is not null)
        {
            if (Fixed)
                PlaceFixed();
            else
                PlaceScaled(Shape);
            if (Stretch(Shape))
            {
                Normalize();
                return;
            }

            if (!Fixed)
            {
                Unfit = true; // 자동 배치는 간격·줄 맞춤 후보로 다시 배치한다(ProcessBuilder.Arrange)
                return;
            }

            // 영역이 기준 크기보다 작아 그 모양이 안 들어간다(구간이 거꾸로 접힘) — 이 크기에서 길을 다시 찾는다
            foreach (var l in Lines)
            {
                l.Points.Clear();
                l.Hops.Clear();
                l.Crossings = 0;
            }

            foreach (var s in Signals)
                s.Points.Clear();
            Warnings.Add("The area is smaller than base-size and the piping no longer fits its base-size shape, so it is routed again and may look different; set base-size to the smallest area the screen is shown at");
        }

        AssignNets();
        Place();
        PinPorts();
        ResolveEnds();
        AssignNets(); // 면 포트의 키가 고른 면(좌/우)까지 정해졌다 — 묶음 다시
        TrimStubs();
        ResolveSignalEnds();
        Route();
        RouteSignals();
        Normalize();
    }

    // ── 배관 묶음: 같은 포트에 닿는 선끼리(분기·합류) 한 묶음 ──
    private void AssignNets()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        string Find(string k)
        {
            while (parent.TryGetValue(k, out var p) && p != k)
                k = p;
            return k;
        }

        foreach (var l in Lines)
        {
            parent.TryAdd(l.From.Key, l.From.Key);
            parent.TryAdd(l.To.Key, l.To.Key);
            var a = Find(l.From.Key);
            var b = Find(l.To.Key);
            if (a != b)
                parent[b] = a;
        }

        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var l in Lines)
        {
            var root = Find(l.From.Key);
            if (!ids.TryGetValue(root, out var id))
                ids[root] = id = ids.Count;
            l.Net = id;
        }
    }

    // ── 계층 배치 ──
    private float FlowSize(GraphNode n) => Vertical ? n.H : n.W;
    private float CrossSize(GraphNode n) => Vertical ? n.W : n.H;

    // 끝의 교차축 위치(차지 영역 기준) — 정렬 기준. 교차축 쪽 면(흐름이 좌→우일 때 위·아래 포트)은
    // 포트 앞 끝 바깥 자리를 기준으로 한다 — 상대 포트가 그 높이에 오면 한 번만 꺾어 들어간다
    private float CrossOf(GraphEnd e)
    {
        float c;
        if (!e.Face)
            c = Vertical ? e.Local.X : e.Local.Y;
        else
            c = e.Side is PortSide.Top or PortSide.Bottom
                ? (Vertical ? e.Sym.MidX : (e.Side == PortSide.Top ? e.Sym.Top : e.Sym.Bottom))
                : (Vertical ? (e.Side == PortSide.Left ? e.Sym.Left : e.Sym.Right) : e.Sym.MidY);
        var o = Dir(e.Side);
        var crossOut = Vertical ? o.X : o.Y;
        // 같은 줄 맞춤 — 앞(+) 교차 방향으로 나가는 포트(좌→우의 아래, 위→아래의 오른쪽)는 장비 가운데 줄에 맞춘다
        if (crossOut > 0 && Align == ProcessAlign.Row)
        {
            var b = Nodes[e.Node].Body;
            return Vertical ? b.MidX : b.MidY;
        }
        return c + crossOut * (Stub + Lattice);
    }


    private sealed class Elem
    {
        public int Node = -1; // -1 = 가상 점(긴 선의 중간)
        public float Size;
        public int? Row;
        public int Decl;
        public float Pos;     // 교차축 위치(왼쪽/위)
        public int Index;     // 층 안 순서
        public int Layer;
    }

    // 층 사이 연결: 위층 a(교차축 붙는 자리 aOff) → 아래층 b(bOff)
    // Back = 되돌아가는 선(재순환) — 순서(교차 수)에는 들어가지만 자리 맞춤에는 쓰지 않는다
    private readonly record struct Link(Elem A, float AOff, Elem B, float BOff, int Net, bool Back = false);

    // ── 좌표 배치: 몸통 가운데 = Pos, 마주 보는 포트의 작은 어긋남은 하류를 옮겨 맞춤, 겹치면 알림 ──
    private void PlaceFixed()
    {
        foreach (var node in Nodes.Where(x => !x.Pinned && x.Pos is not null))
        {
            node.X = node.Pos!.Value.X - node.Body.MidX;
            node.Y = node.Pos!.Value.Y - node.Body.MidY;
        }

        // 장비마다 기준선 하나(그 장비로 들어오는 첫 선 중 맞출 수 있는 것) — 처음 좌표로 고르고, 상류가 옮겨지면 따라가도록 안정될 때까지 반복.
        // 기준 크기 모양을 늘일 때는 기준 크기에서 고른 쌍을 그대로(배율에 따라 맞춤 여부가 바뀌면 곧은 선이 꺾인다)
        var anchor = new Dictionary<int, (GraphLine Line, bool AlongX)>();
        if (Shape is not null)
        {
            foreach (var (v, (k, ax)) in Shape.SnapAnchors)
                anchor[v] = (Lines[k], ax);
        }
        else
        {
            foreach (var l in Lines)
            {
                var (a, b) = (l.From, l.To);
                if (a.Face || b.Face || a.Node == b.Node || anchor.ContainsKey(b.Node) || Nodes[a.Node].Pinned || Nodes[b.Node].Pinned)
                    continue;
                bool? alongX = (a.Side, b.Side) switch
                {
                    (PortSide.Right, PortSide.Left) or (PortSide.Left, PortSide.Right) => false, // 좌우로 마주 봄 → y 맞춤
                    (PortSide.Bottom, PortSide.Top) or (PortSide.Top, PortSide.Bottom) => true,  // 위아래로 마주 봄 → x 맞춤
                    _ => null,
                };
                // 처음부터 맞아 있는 쌍(어긋남 0)도 넣는다 — 상류가 옮겨지면 같이 따라가야 일직선이 유지된다
                if (alongX is { } ax && MathF.Abs(SnapDelta(l, ax)) <= SnapTolerance)
                    anchor[b.Node] = (l, ax);
            }
        }

        foreach (var (v, (line, ax)) in anchor)
            SnapAnchors[v] = (Lines.IndexOf(line), ax);

        for (var pass = 0; pass < Nodes.Count; pass++)
        {
            var changed = false;
            foreach (var (v, (line, alongX)) in anchor)
            {
                var delta = SnapDelta(line, alongX);
                if (MathF.Abs(delta) <= 0.01f)
                    continue;
                if (alongX)
                    Nodes[v].X += delta;
                else
                    Nodes[v].Y += delta;
                changed = true;
            }

            if (!changed)
                break;
        }

        // 겹침 — 몸통과 이름표·붙은 칸·계기
        var real = Enumerable.Range(0, Nodes.Count).Where(i => !Nodes[i].Pinned).ToList();
        foreach (var i in real)
        {
            foreach (var j in real.Where(j => j > i))
            {
                var hit = Areas(Nodes[i]).Any(r => Areas(Nodes[j]).Any(o => r.IntersectsWith(o)));
                if (hit)
                    Warnings.Add($"'{Nodes[i].Name}' and '{Nodes[j].Name}' overlap; move one of them (x/y) or enlarge base-size");
            }
        }

        IEnumerable<SKRect> Areas(GraphNode g) => g.Extras.Prepend(g.Body).Where(r => !r.IsEmpty).Select(g.Abs);
    }

    // 자동 배치 모양 늘이기·줄이기 — 장비 몸통 가운데를 축마다 배율만큼 옮긴다(장비 크기 그대로). 곧은 배관 하나로 이어진 장비끼리는
    // 그 축에서 한 덩어리(가운데 평균을 옮긴다) — 일직선이 꺾이지 않게
    private void PlaceScaled(ProcessGraph shape)
    {
        var dx = Shifts(shape, true, Spacing);
        var dy = Shifts(shape, false, SpacingY ?? Spacing);
        for (var i = 0; i < Nodes.Count; i++)
        {
            Nodes[i].X = shape.Nodes[i].X + dx[i];
            Nodes[i].Y = shape.Nodes[i].Y + dy[i];
        }
    }

    /// <summary>Automatic placement: the spread of the equipment groups along each axis — how much the size changes per unit of position scale.</summary>
    internal static (float X, float Y) Spread(ProcessGraph g) => (Shifts(g, true, 2f).DefaultIfEmpty().Max() - Shifts(g, true, 2f).DefaultIfEmpty().Min(),
        Shifts(g, false, 2f).DefaultIfEmpty().Max() - Shifts(g, false, 2f).DefaultIfEmpty().Min());

    private static float[] Shifts(ProcessGraph shape, bool alongX, float scale)
    {
        var nodes = shape.Nodes;
        var parent = Enumerable.Range(0, nodes.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        foreach (var l in shape.Lines)
        {
            if (l.Points.Count != 2)
                continue;
            var flat = MathF.Abs(l.Points[0].Y - l.Points[1].Y) < 0.01f;
            if (flat != alongX) // 가로 직선이면 세로 위치를, 세로 직선이면 가로 위치를 같이
                parent[Find(l.From.Node)] = Find(l.To.Node);
        }

        float Center(int i) => alongX ? nodes[i].X + nodes[i].Body.MidX : nodes[i].Y + nodes[i].Body.MidY;
        var mean = Enumerable.Range(0, nodes.Count).Where(i => !nodes[i].Pinned).GroupBy(Find).ToDictionary(g => g.Key, g => g.Average(Center));
        return Enumerable.Range(0, nodes.Count).Select(i => nodes[i].Pinned ? 0f : mean[Find(i)] * (scale - 1f)).ToArray();
    }

    // 하류 끝이 상류 끝과 일직선이 되려면 하류 장비를 옮길 거리
    private float SnapDelta(GraphLine l, bool alongX)
    {
        var (a, b) = (l.From, l.To);
        var (na, nb) = (Nodes[a.Node], Nodes[b.Node]);
        return alongX ? na.X + a.Local.X - (nb.X + b.Local.X) : na.Y + a.Local.Y - (nb.Y + b.Local.Y);
    }

    private void Place()
    {
        if (Fixed)
        {
            PlaceFixed();
            return;
        }

        var n = Nodes.Count;
        var edges = new List<(int u, int v, GraphLine line)>();
        foreach (var l in Lines)
        {
            if (l.From.Node != l.To.Node && !Nodes[l.From.Node].Pinned && !Nodes[l.To.Node].Pinned)
                edges.Add((l.From.Node, l.To.Node, l));
        }

        // 순환 제거 — 선언 순서 DFS 의 되돌아가는 선은 계산에서만 뒤집는다
        var succ = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        for (var i = 0; i < edges.Count; i++)
            succ[edges[i].u].Add(i);
        var state = new int[n];
        var reversed = new bool[edges.Count];
        for (var s = 0; s < n; s++)
        {
            if (state[s] != 0)
                continue;
            var stack = new Stack<(int v, int next)>();
            stack.Push((s, 0));
            state[s] = 1;
            while (stack.Count > 0)
            {
                var (v, next) = stack.Pop();
                if (next < succ[v].Count)
                {
                    stack.Push((v, next + 1));
                    var ei = succ[v][next];
                    var w = edges[ei].v;
                    if (state[w] == 1)
                        reversed[ei] = true;
                    else if (state[w] == 0)
                    {
                        state[w] = 1;
                        stack.Push((w, 0));
                    }
                }
                else
                {
                    state[v] = 2;
                }
            }
        }

        var dag = edges.Select((e, i) => reversed[i] ? (u: e.v, v: e.u, e.line, back: true) : (e.u, e.v, e.line, back: false)).ToList();

        // 열(rank): 가장 긴 경로, 힌트가 있으면 그 값. 앞쪽이 없는 장비는 뒤 장비 바로 앞으로 당긴다
        var preds = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        var succs = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        var indeg = new int[n];
        foreach (var (u, v, _, _) in dag)
        {
            preds[v].Add(u);
            succs[u].Add(v);
            indeg[v]++;
        }

        var topo = new List<int>();
        var ready = new SortedSet<int>(Enumerable.Range(0, n).Where(i => indeg[i] == 0));
        while (ready.Count > 0)
        {
            var v = ready.Min;
            ready.Remove(v);
            topo.Add(v);
            foreach (var w in succs[v])
            {
                if (--indeg[w] == 0)
                    ready.Add(w);
            }
        }

        var rank = new int[n];
        foreach (var v in topo)
            rank[v] = Nodes[v].Rank ?? (preds[v].Count == 0 ? 0 : preds[v].Max(u => rank[u]) + 1);
        for (var i = topo.Count - 1; i >= 0; i--)
        {
            var v = topo[i];
            if (Nodes[v].Rank is null && preds[v].Count == 0 && succs[v].Count > 0)
                rank[v] = Math.Max(rank[v], succs[v].Min(w => rank[w]) - 1);
        }

        var stackHead = Enumerable.Range(0, n).ToArray();
        StackParallel(rank, preds, succs, stackHead);

        var minRank = Enumerable.Range(0, n).Where(i => !Nodes[i].Pinned).Min(i => rank[i]);
        for (var i = 0; i < n; i++)
            rank[i] = Math.Max(0, rank[i] - minRank);
        var layerCount = Enumerable.Range(0, n).Where(i => !Nodes[i].Pinned).Max(i => rank[i]) + 1;

        // 층 + 가상 점
        var layers = Enumerable.Range(0, layerCount).Select(_ => new List<Elem>()).ToArray();
        var elemOf = new Elem[n];
        for (var i = 0; i < n; i++)
        {
            var e = new Elem { Node = i, Size = CrossSize(Nodes[i]), Row = Nodes[i].Row, Decl = i, Layer = rank[i] };
            elemOf[i] = e;
            if (!Nodes[i].Pinned)
                layers[rank[i]].Add(e);
        }

        var links = new List<Link>();
        var decl = n;
        foreach (var (u0, v0, line, back) in dag)
        {
            int u = u0, v = v0;
            var ue = line.From.Node == u ? line.From : line.To;
            var ve = line.From.Node == v ? line.From : line.To;
            if (rank[u] == rank[v])
                continue;
            if (rank[u] > rank[v])
            {
                (u, v) = (v, u);
                (ue, ve) = (ve, ue);
            }

            var prev = elemOf[u];
            var prevOff = CrossOf(ue);
            for (var r = rank[u] + 1; r < rank[v]; r++)
            {
                var d = new Elem { Size = Pitch, Decl = decl++, Layer = r };
                layers[r].Add(d);
                links.Add(new Link(prev, prevOff, d, Pitch / 2, line.Net, back));
                prev = d;
                prevOff = Pitch / 2;
            }

            links.Add(new Link(prev, prevOff, elemOf[v], CrossOf(ve), line.Net, back));
        }

        // 층 안 순서 — 처음은 선언 순서, 그다음 위/아래 이웃의 평균 위치로 정렬(row 힌트는 고정 키), 교차가 가장 적은 순서를 쓴다
        foreach (var layer in layers)
        {
            layer.Sort((a, b) => a.Decl.CompareTo(b.Decl));
            Reindex(layer);
        }

        var up = links.ToLookup(l => l.B);
        var down = links.ToLookup(l => l.A);
        // 자리 맞춤용 — 앞으로 가는 선만(되돌아가는 선은 그 끝 면이 아직 정해지지 않았고, 흐름 줄을 비틀어 놓는다). 앞으로 가는 선이 없으면 전부
        var upA = links.Where(l => !l.Back || !links.Any(f => !f.Back && f.B == l.B)).ToLookup(l => l.B);
        var downA = links.Where(l => !l.Back || !links.Any(f => !f.Back && f.A == l.A)).ToLookup(l => l.A);
        var best = layers.Select(l => l.ToList()).ToArray();
        var bestCross = Crossings(links);
        for (var iter = 0; iter < 8 && bestCross > 0; iter++)
        {
            var downward = iter % 2 == 0;
            for (var k = 1; k < layerCount; k++)
            {
                var r = downward ? k : layerCount - 1 - k;
                var layer = layers[r];
                var keys = layer.ToDictionary(e => e, e =>
                {
                    if (e.Row is { } row)
                        return (double)row;
                    var nb = downward ? up[e].Select(l => Slot(l.A, l.AOff)).ToList() : down[e].Select(l => Slot(l.B, l.BOff)).ToList();
                    return nb.Count == 0 ? e.Index : nb.Average();
                });
                var sorted = layer.OrderBy(e => keys[e]).ThenBy(e => e.Index).ToList();
                layer.Clear();
                layer.AddRange(sorted);
                Reindex(layer);
            }

            var c = Crossings(links);
            if (c < bestCross)
            {
                bestCross = c;
                best = layers.Select(l => l.ToList()).ToArray();
            }
        }

        for (var r = 0; r < layerCount; r++)
        {
            layers[r].Clear();
            layers[r].AddRange(best[r]);
            Reindex(layers[r]);
        }

        // 이웃 바꾸기 — 평균 위치 정렬이 못 찾는 순서(옆면 포트 쪽 장비)를 이웃 둘씩 바꿔 본다. 두 번 바꿔야 풀리는 경우가 있어
        // 교차가 같은 바꾸기도 받아 나가되(한 쌍은 한 번만), 끝에 처음보다 줄지 않았으면 처음 순서로 되돌린다(이미 깨끗한 공정도는 그대로)
        var startCross = Crossings(links);
        if (startCross > 0)
        {
            var start = layers.Select(l => l.ToList()).ToArray();
            var crossNow = startCross;
            var tried = new HashSet<(Elem, Elem)>();
            for (var sweep = 0; sweep < 4 && crossNow > 0; sweep++)
            {
                var moved = false;
                foreach (var layer in layers)
                {
                    for (var i = 0; i + 1 < layer.Count && crossNow > 0; i++)
                    {
                        if (layer[i].Row is not null && layer[i + 1].Row is not null)
                            continue;
                        (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);
                        Reindex(layer);
                        var c = Crossings(links);
                        if (c < crossNow || (c == crossNow && tried.Add((layer[i], layer[i + 1]))))
                        {
                            moved |= c < crossNow || c == crossNow;
                            crossNow = c;
                        }
                        else
                        {
                            (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);
                            Reindex(layer);
                        }
                    }
                }

                if (!moved)
                    break;
            }

            if (crossNow >= startCross)
            {
                for (var r = 0; r < layerCount; r++)
                {
                    layers[r].Clear();
                    layers[r].AddRange(start[r]);
                    Reindex(layers[r]);
                }
            }
        }

        if (layers.Any(l => l.Any(e => e.Row is not null)))
        {
            // row 힌트는 순서를 정하는 키 — 힌트끼리의 순서는 언제나 지킨다
            foreach (var layer in layers)
            {
                var sorted = layer.OrderBy(e => e.Row ?? (double)e.Index).ThenBy(e => e.Index).ToList();
                layer.Clear();
                layer.AddRange(sorted);
                Reindex(layer);
            }
        }

        // 교차축 좌표 — 위층 이웃의 포트와 같은 줄(일직선)을 목표로, 순서와 간격을 지키며 놓는다
        var gap = MathF.Max(2 * Clearance + Lattice, 40f * Spacing);
        foreach (var layer in layers)
            PlaceLayer(layer, layer.Select(_ => (float?)null).ToList(), gap);
        // 위층 이웃에 맞추고(A) → 앞 장비가 없는 장비를 아래층에 맞추고(B) → 다시 A(여러 앞 장비가 한 장비로 모이면 그 장비를 가운데로)
        void AlignDown()
        {
            for (var r = 1; r < layerCount; r++)
            {
                var layer = layers[r];
                // 쌓인 병렬 장비는 맨 위(앞) 장비와 같은 줄 — 입구·출구 줄기를 함께 쓴다
                var desired = layer.Select(e => e.Node >= 0 && stackHead[e.Node] != e.Node
                    ? elemOf[stackHead[e.Node]].Pos
                    : upA[e].Any() ? upA[e].Average(l => l.A.Pos + l.AOff - l.BOff) : (float?)null).ToList();
                PlaceLayer(layer, desired, gap);
            }
        }

        void AlignUp()
        {
            for (var r = layerCount - 2; r >= 0; r--)
            {
                var layer = layers[r];
                var desired = layer.Select(e => (float?)(upA[e].Any() || !downA[e].Any() || (e.Node >= 0 && stackHead[e.Node] != e.Node)
                    ? e.Pos
                    : downA[e].Average(l => l.B.Pos + l.BOff - l.AOff))).ToList();
                PlaceLayer(layer, desired, gap);
            }
        }

        AlignDown();
        AlignUp();
        AlignDown();

        // 흐름축 좌표 — 열 폭 = 가장 큰 장비, 열 사이 통로 = 기본 + 지나는 선 수만큼 레일
        var colW = new float[layerCount];
        foreach (var layer in layers)
            foreach (var e in layer.Where(e => e.Node >= 0))
                colW[rank[e.Node]] = MathF.Max(colW[rank[e.Node]], FlowSize(Nodes[e.Node]));
        var colStart = new float[layerCount];
        for (var r = 1; r < layerCount; r++)
        {
            var tracks = links.Where(l => l.A.Layer == r - 1).Select(l => l.Net).Distinct().Count(); // 같은 묶음(분기·합류)은 레일 하나
            var channel = MathF.Max(2 * Stub + Lattice, 48f * Spacing) + tracks * Pitch;
            colStart[r] = colStart[r - 1] + colW[r - 1] + channel;
        }

        for (var i = 0; i < n; i++)
        {
            var node = Nodes[i];
            if (node.Pinned)
                continue;
            var flow = colStart[rank[i]] + (colW[rank[i]] - FlowSize(node)) / 2;
            var cross = elemOf[i].Pos;
            (node.X, node.Y) = Vertical ? (cross, flow) : (flow, cross);
        }
    }

    // 병렬 장비(같은 앞·뒤 장비, 포트가 모두 흐름 방향 면)는 흐름 방향으로 차례로 쌓는다 — 위→아래 흐름에서
    // 좌→우 포트 펌프 두 대가 옆으로 나란히 서면 배관이 얽힌다(현장 화면처럼 위아래로 쌓고 입구·출구 줄기를 공유)
    private void StackParallel(int[] rank, List<int>[] preds, List<int>[] succs, int[] stackHead)
    {
        var flowSides = Vertical ? new[] { PortSide.Left, PortSide.Right } : new[] { PortSide.Top, PortSide.Bottom };
        bool AlongFlowPorts(int v) => Lines.All(l =>
            (l.From.Node != v || (!l.From.Face && flowSides.Contains(l.From.Side))) &&
            (l.To.Node != v || (!l.To.Face && flowSides.Contains(l.To.Side)))) &&
            Lines.Any(l => l.From.Node == v || l.To.Node == v);

        string Key(int v) => rank[v] + "|" + string.Join(",", preds[v].Distinct().OrderBy(x => x)) + "|" + string.Join(",", succs[v].Distinct().OrderBy(x => x));

        while (true)
        {
            var group = Enumerable.Range(0, Nodes.Count)
                .Where(v => Nodes[v].Rank is null && !Nodes[v].Pinned && AlongFlowPorts(v))
                .GroupBy(Key)
                .Where(g => g.Count() > 1)
                .OrderBy(g => rank[g.First()])
                .FirstOrDefault();
            if (group is null)
                return;
            var members = group.OrderBy(v => v).ToList();
            var r = rank[members[0]];
            var extra = members.Count - 1;
            for (var v = 0; v < Nodes.Count; v++)
            {
                if (rank[v] > r && Nodes[v].Rank is null) // rank 힌트는 최종 열 번호 그대로
                    rank[v] += extra;
            }

            for (var k = 0; k < members.Count; k++)
            {
                rank[members[k]] = r + k;
                stackHead[members[k]] = members[0];
            }
        }
    }

    // 유닛: 테두리 = 안쪽 장비 차지 영역 + PortMargin, 포트는 테두리 면 위 자리에
    private void PinPorts()
    {
        if (!Unit)
            return;
        var real = Nodes.Where(n => !n.Pinned).ToList();
        if (real.Count > 0)
        {
            var r = real.Select(n => SKRect.Create(n.X, n.Y, n.W, n.H)).Aggregate(SKRect.Union);
            r.Inflate(PortMargin, PortMargin);
            Frame = r;
        }

        foreach (var p in Nodes.Where(n => n.Pinned))
        {
            (p.X, p.Y) = p.PinSide switch
            {
                PortSide.Left => (Frame.Left, Frame.Top + Frame.Height * p.PinAt),
                PortSide.Right => (Frame.Right, Frame.Top + Frame.Height * p.PinAt),
                PortSide.Top => (Frame.Left + Frame.Width * p.PinAt, Frame.Top),
                _ => (Frame.Left + Frame.Width * p.PinAt, Frame.Bottom),
            };
        }
    }

    private static void Reindex(List<Elem> layer)
    {
        for (var i = 0; i < layer.Count; i++)
            layer[i].Index = i;
    }

    // 층 안 장비를 순서대로 놓는다 — 원하는 자리(desired)를 따르되 앞 장비와 gap 이상 떨어지게.
    // 가상 점(긴 선의 중간)은 자리를 차지하지 않는다(경로는 장애물을 피해 따로 찾는다 — 가상 점이 장비를 밀면 병렬 장비 줄이 어긋난다)
    private static void PlaceLayer(List<Elem> layer, List<float?> desired, float gap)
    {
        float? end = null;
        for (var i = 0; i < layer.Count; i++)
        {
            var e = layer[i];
            if (e.Node < 0)
            {
                e.Pos = desired[i] ?? end ?? 0f;
                continue;
            }

            var min = end is { } en ? en + gap : float.NegativeInfinity;
            var p = desired[i] ?? (end is null ? 0f : min);
            e.Pos = MathF.Max(p, min);
            end = e.Pos + e.Size;
        }
    }

    // 층 안 자리 + 포트가 붙은 자리(장비 교차축 크기 대비) — 위·왼쪽 면 포트는 앞, 아래·오른쪽 면 포트는 뒤.
    // 순서 정하기·교차 세기에 쓴다(3방 밸브 가지·열교환기 열매체처럼 옆면 포트에 이어진 장비가 그 쪽에 오게). ±0.45 안이라 층 순서는 넘지 않는다
    private static double Slot(Elem e, float off) => e.Index + Math.Clamp(off / MathF.Max(e.Size, 1f) - 0.5, -1.5, 1.5) * 0.3;

    private static int Crossings(List<Link> links)
    {
        var count = 0;
        foreach (var g in links.GroupBy(l => l.A.Layer))
        {
            var list = g.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if ((Slot(a.A, a.AOff) - Slot(b.A, b.AOff)) * (Slot(a.B, a.BOff) - Slot(b.B, b.BOff)) < -1e-6)
                        count++;
                }
            }
        }

        return count;
    }

    // ── 끝 확정: 고정 포트는 점, 면 포트는 상대 위치 순서대로 면 위 자리를 나눈다 ──
    private void ResolveEnds()
    {
        var ends = Lines.SelectMany(l => new[] { (end: l.From, other: l.To), (end: l.To, other: l.From) }).ToList();
        foreach (var (e, _) in ends.Where(x => !x.end.Face))
        {
            var node = Nodes[e.Node];
            e.Point = new SKPoint(node.X + e.Local.X, node.Y + e.Local.Y);
            e.Out = e.Side;
        }

        SKPoint Center(GraphEnd e) => Nodes[e.Node].Abs(e.Face ? e.Sym : Nodes[e.Node].Body) is var r ? new SKPoint(r.MidX, r.MidY) : default;
        SKPoint OtherPoint(GraphEnd o) => o.Face ? Center(o) : o.Point;

        var faces = ends.Where(x => x.end.Face).ToList();
        foreach (var (e, other) in faces)
        {
            e.Out = e.Side;
            if (e.EitherSide)
            {
                // 상대 쪽 면 — 단, 그 면이 붙은 칸에 거의 가려져 있으면 반대 면
                var node = Nodes[e.Node];
                var sym = node.Abs(e.Sym);
                var near = OtherPoint(other).X < Center(e).X ? PortSide.Left : PortSide.Right;
                var far = near == PortSide.Left ? PortSide.Right : PortSide.Left;
                e.Out = FreeLength(node, sym, near) < 0.3f * FreeLength(node, sym, far) ? far : near;
                e.Key += "." + e.Out; // 왼쪽·오른쪽 면은 따로 합친다
            }
        }

        // 같은 면으로 들어오는 선끼리·나가는 선끼리 한 노즐로 합친다(같은 키 = 한 묶음 → 탱크 앞에서 T·십자로 모임).
        // 면 위 노즐 순서 = 상대가 있는 방향(장비 가운데 기준 각도)을 면을 따라 돈 순서
        foreach (var group in faces.GroupBy(x => (x.end.Node, x.end.Out)))
        {
            var node = Nodes[group.Key.Node];
            var center = Center(group.First().end);
            float Key(GraphEnd other)
            {
                var o = OtherPoint(other);
                var phi = MathF.Atan2(o.Y - center.Y, o.X - center.X) * 180f / MathF.PI;
                var k = group.Key.Out switch
                {
                    PortSide.Top => phi - 90,
                    PortSide.Bottom => 270 - phi,
                    PortSide.Left => -phi,
                    _ => phi - 180,
                };
                return ((k % 360) + 360) % 360;
            }

            var nozzles = group.GroupBy(x => x.end.Key)
                .Select(g => (Items: g.ToList(), Order: g.Average(x => Key(x.other)), First: g.Min(x => Lines.FindIndex(l => l.From == x.end || l.To == x.end))))
                .OrderBy(n => n.Order).ThenBy(n => n.First)
                .ToList();

            var sym = node.Abs(group.First().end.Sym);
            var positions = FaceSlots(node, sym, group.Key.Out, nozzles.Count);
            for (var k = 1; k < positions.Count; k++)
            {
                if (MathF.Abs(positions[k] - positions[k - 1]) < Pitch - 0.5f)
                {
                    var face = group.Key.Out.ToString().ToLowerInvariant();
                    Warnings.Add($"'{node.Name}': not enough room on the {face} face for {nozzles.Count} nozzles (incoming and outgoing lines), excluding space covered by accessories (such as an agitator) and DvAttach boxes. Connect one of them to another port (side, etc.), or move the DvAttach box to another side");
                    break;
                }
            }

            for (var i = 0; i < nozzles.Count; i++)
            {
                var t = positions[i];
                var point = group.Key.Out switch
                {
                    PortSide.Top => new SKPoint(t, sym.Top),
                    PortSide.Bottom => new SKPoint(t, sym.Bottom),
                    PortSide.Left => new SKPoint(sym.Left, t),
                    _ => new SKPoint(sym.Right, t),
                };
                foreach (var (end, _) in nozzles[i].Items)
                    end.Point = point;
            }
        }
    }

    // 곧은 끝 다듬기 — 유닛 테두리 포트는 짧게(8), 서로 4px 안쪽으로 거의 같은 곧은 끝 좌표는 가운데로 맞춘다
    // (2px 어긋난 두 끝이 U턴을 피하려고 작은 지그재그를 만들지 않게)
    private void TrimStubs()
    {
        var ends = Lines.SelectMany(l => new[] { l.From, l.To }).ToList();
        foreach (var e in ends)
            e.StubLen = Nodes[e.Node].Pinned ? Lattice : Stub;

        foreach (var horizontalOut in new[] { true, false })
        {
            var group = ends.Where(e => (e.Out is PortSide.Left or PortSide.Right) == horizontalOut)
                .OrderBy(e => horizontalOut ? e.StubEnd.X : e.StubEnd.Y).ToList();
            var i = 0;
            while (i < group.Count)
            {
                var j = i + 1;
                float At(GraphEnd e) => horizontalOut ? e.StubEnd.X : e.StubEnd.Y;
                while (j < group.Count && At(group[j]) - At(group[i]) < Lattice / 2)
                    j++;
                if (j - i > 1)
                {
                    var target = group.Skip(i).Take(j - i).Average(At);
                    foreach (var e in group.Skip(i).Take(j - i))
                    {
                        var port = horizontalOut ? e.Point.X : e.Point.Y;
                        e.StubLen = MathF.Max(Lattice, MathF.Abs(target - port));
                    }
                }

                i = j;
            }
        }

        // 마주 보는 두 끝이 곧은 끝 둘보다 가까우면(고정 좌표) 곧은 끝이 서로 지나쳐 U턴 없이는 못 이어
        // 한 바퀴 도는 고리가 생긴다 — 간격의 절반씩으로 줄여 곧게(어긋나 있으면 Z 로) 잇는다
        foreach (var l in Lines)
        {
            var (a, b) = (l.From, l.To);
            if (DirIndex(a.Out) != (DirIndex(b.Out) + 2) % 4)
                continue;
            var gap = a.Out switch
            {
                PortSide.Right => b.Point.X - a.Point.X,
                PortSide.Left => a.Point.X - b.Point.X,
                PortSide.Bottom => b.Point.Y - a.Point.Y,
                _ => a.Point.Y - b.Point.Y,
            };
            if (gap > 0 && gap < a.StubLen + b.StubLen)
                a.StubLen = b.StubLen = gap / 2;
        }
    }

    // 면 위 빈 구간 — 면 범위(FaceStart~FaceEnd)에서 그 면 바깥에 붙은 것(부속·붙은 칸)이 가리는 구간을 뺀다
    private List<(float a, float b)> FreeSpans(GraphNode node, SKRect sym, PortSide face)
    {
        var horizontal = face is PortSide.Top or PortSide.Bottom;
        var s0 = horizontal ? sym.Left : sym.Top;
        var len = horizontal ? sym.Width : sym.Height;
        var spans = new List<(float a, float b)> { (s0 + len * BuiltInSymbols.FaceStart, s0 + len * BuiltInSymbols.FaceEnd) };
        foreach (var x in node.Extras)
        {
            var r = node.Abs(x);
            var outside = face switch
            {
                PortSide.Top => r.Bottom <= sym.Top + 1,
                PortSide.Bottom => r.Top >= sym.Bottom - 1,
                PortSide.Left => r.Right <= sym.Left + 1,
                _ => r.Left >= sym.Right - 1,
            };
            if (!outside)
                continue;
            var pad = Pitch / 2 + 2;
            var (ba, bb) = horizontal ? (r.Left - pad, r.Right + pad) : (r.Top - pad, r.Bottom + pad);
            spans = spans.SelectMany(sp => bb <= sp.a || ba >= sp.b
                ? new[] { sp }
                : new[] { (sp.a, MathF.Min(ba, sp.b)), (MathF.Max(bb, sp.a), sp.b) }.Where(q => q.Item2 - q.Item1 > 1)).ToList();
        }

        return spans;
    }

    private float FreeLength(GraphNode node, SKRect sym, PortSide face) => FreeSpans(node, sym, face).Sum(sp => sp.b - sp.a);

    // 면 위 자리(면 좌표) — 한 줄이면 가장 긴 빈 구간 가운데, 여러 줄이면 빈 구간 전체 길이에 고르게
    private List<float> FaceSlots(GraphNode node, SKRect sym, PortSide face, int count)
    {
        var spans = FreeSpans(node, sym, face);
        if (spans.Count == 0)
        {
            var horizontal = face is PortSide.Top or PortSide.Bottom;
            var c = horizontal ? sym.MidX : sym.MidY;
            return Enumerable.Repeat(c, count).ToList();
        }

        if (count == 1)
        {
            var longest = spans.OrderByDescending(sp => sp.b - sp.a).First();
            return [(longest.a + longest.b) / 2];
        }

        var total = spans.Sum(sp => sp.b - sp.a);
        var result = new List<float>();
        for (var i = 0; i < count; i++)
        {
            var at = total * (i + 0.5f) / count;
            foreach (var (a, b) in spans)
            {
                if (at <= b - a)
                {
                    result.Add(a + at);
                    break;
                }

                at -= b - a;
            }
        }

        return result;
    }

    // ── 경로 ──
    private float[] _xs = [], _ys = [];
    private bool[] _blocked = [];
    private List<SKRect> _obstacles = new();
    private readonly Dictionary<int, List<(float a, float b, int net)>> _hOcc = new(), _vOcc = new();
    private readonly Dictionary<int, HashSet<int>> _vertexNets = new();
    private readonly Dictionary<int, HashSet<int>> _cornerNets = new();

    private static SKPoint Dir(PortSide s) => s switch
    {
        PortSide.Left => new SKPoint(-1, 0),
        PortSide.Right => new SKPoint(1, 0),
        PortSide.Top => new SKPoint(0, -1),
        _ => new SKPoint(0, 1),
    };

    private void Route()
    {
        // 장애물 = 몸통(여유 포함)·이름표·붙은 칸
        _obstacles = new List<SKRect>();
        foreach (var node in Nodes)
        {
            if (node.Pinned)
                continue;
            var b = node.Abs(node.Body);
            b.Inflate(Clearance, Clearance);
            _obstacles.Add(b);
            foreach (var x in node.Extras)
            {
                var r = node.Abs(x);
                r.Inflate(4, 4);
                _obstacles.Add(r);
            }
        }

        // 격자: 고른 간격 + 포트·포트 앞 끝 좌표(정확히)
        SKRect area;
        if (Unit)
        {
            area = Frame; // 유닛 안 배관은 테두리 밖으로 나가지 않는다
        }
        else
        {
            area = _obstacles.Aggregate((a, b) => SKRect.Union(a, b));
            area.Inflate(5 * Lattice + Pitch * 2, 5 * Lattice + Pitch * 2);
        }
        var specialX = new SortedSet<float>();
        var specialY = new SortedSet<float>();
        foreach (var e in Lines.SelectMany(l => new[] { l.From, l.To }).Concat(Signals.SelectMany(s => new[] { s.From, s.To })))
        {
            {
                var s = e.StubEnd;
                specialX.Add(e.Point.X);
                specialX.Add(s.X);
                specialY.Add(e.Point.Y);
                specialY.Add(s.Y);
            }
        }

        _xs = Axis(area.Left, area.Right, specialX);
        _ys = Axis(area.Top, area.Bottom, specialY);
        _blocked = new bool[_xs.Length * _ys.Length];
        for (var i = 0; i < _xs.Length; i++)
        {
            for (var j = 0; j < _ys.Length; j++)
                _blocked[V(i, j)] = Inside(_xs[i], _ys[j]);
        }

        // 긋는 순서 — 먼저 그은 선이 좋은 길(안쪽 레일)을 차지한다. 묶음 선언 순서로 긋고, 겹침·교차가 남으면
        // 거꾸로 한 번 더 그어 더 나은 쪽을 쓴다(같은 입력이면 같은 결과)
        var order = Lines.Select((l, i) => (l, i)).OrderBy(x => x.l.Net).ThenBy(x => x.i).Select(x => x.l).ToList();
        RouteAll(order);
        FindHops();
        var score = Score();
        if (score > 0)
        {
            var first = Snapshot();
            ResetRoutes();
            order.Reverse();
            RouteAll(order);
            FindHops();
            if (Score() >= score)
                Restore(first);
        }
    }

    // ── 신호선 ──

    /// <summary>Length of the straight segment out of a signal line end — short (so it connects straight within the <c>20px</c> between the instrument row and the equipment).</summary>
    private const float SignalStub = 10f;

    // 신호선 끝 확정 — 고정 끝(조절 밸브 구동부)은 그 점, 고르는 끝은 상대를 가장 잘 향하는 면 가운데
    // (붙은 장비 쪽·값 글자 쪽·이미 다른 신호선이 쓰는 면은 뒤로)
    private void ResolveSignalEnds()
    {
        SKPoint Center(GraphEnd e)
        {
            var node = Nodes[e.Node];
            if (e.Pick is not { } pick)
                return new SKPoint(node.X + e.Local.X, node.Y + e.Local.Y);
            var r = node.Abs(pick);
            return new SKPoint(r.MidX, r.MidY);
        }

        var used = new HashSet<(int, SKRect, PortSide)>();
        foreach (var s in Signals)
        {
            foreach (var (e, other) in new[] { (s.From, s.To), (s.To, s.From) })
            {
                e.StubLen = SignalStub;
                var node = Nodes[e.Node];
                if (e.Pick is not { } pick)
                {
                    e.Point = new SKPoint(node.X + e.Local.X, node.Y + e.Local.Y);
                    e.Out = e.Side;
                    continue;
                }

                var r = node.Abs(pick);
                var t = Center(other);
                var len = MathF.Max(1, MathF.Sqrt((t.X - r.MidX) * (t.X - r.MidX) + (t.Y - r.MidY) * (t.Y - r.MidY)));
                var (dx, dy) = ((t.X - r.MidX) / len, (t.Y - r.MidY) / len);
                // 붙은 장비 쪽 면 — 상대가 그 장비 자신이 아니면 피한다(장비를 돌아 나가야 한다)
                var body = node.Abs(node.Body);
                var (bx, by) = (body.MidX - r.MidX, body.MidY - r.MidY);
                var hostSide = MathF.Abs(bx) >= MathF.Abs(by) ? (bx >= 0 ? PortSide.Right : PortSide.Left) : (by >= 0 ? PortSide.Bottom : PortSide.Top);
                float Score(PortSide sd)
                {
                    var o = Dir(sd);
                    var score = o.X * dx + o.Y * dy;
                    if (sd == hostSide && other.Node != e.Node)
                        score -= 2;
                    if (sd == PortSide.Right && e.PickNoRight)
                        score -= 10; // 값 글자 쪽
                    if (used.Contains((e.Node, pick, sd)))
                        score -= 4; // 같은 면을 다른 신호선이 이미 쓴다
                    return score;
                }

                e.Out = new[] { PortSide.Top, PortSide.Right, PortSide.Bottom, PortSide.Left }.OrderByDescending(Score).First();
                used.Add((e.Node, pick, e.Out));
                e.Point = e.Out switch
                {
                    PortSide.Left => new SKPoint(r.Left, r.MidY),
                    PortSide.Right => new SKPoint(r.Right, r.MidY),
                    PortSide.Top => new SKPoint(r.MidX, r.Top),
                    _ => new SKPoint(r.MidX, r.Bottom),
                };
            }
        }
    }

    // 배관을 다 그은 뒤 — 몸통·이름표·붙은 칸·계기를 피하고, 배관·다른 신호선과 나란히 겹치는 길은 비싸게. 엇갈림은 그대로
    private void RouteSignals()
    {
        if (Signals.Count == 0)
            return;

        var obstacles = new List<SKRect>();
        foreach (var node in Nodes)
        {
            if (node.Pinned)
                continue;
            var b = node.Abs(node.Body);
            b.Inflate(3, 3);
            obstacles.Add(b);
            foreach (var x in node.Extras)
            {
                var r = node.Abs(x);
                r.Inflate(2, 2);
                obstacles.Add(r);
            }
        }

        bool Inside(float x, float y) =>
            obstacles.Any(r => x > r.Left + 0.01f && x < r.Right - 0.01f && y > r.Top + 0.01f && y < r.Bottom - 0.01f);

        var blocked = new bool[_xs.Length * _ys.Length];
        for (var i = 0; i < _xs.Length; i++)
        {
            for (var j = 0; j < _ys.Length; j++)
                blocked[V(i, j)] = Inside(_xs[i], _ys[j]);
        }

        var hOcc = new Dictionary<int, List<(float a, float b, int net)>>();
        var vOcc = new Dictionary<int, List<(float a, float b, int net)>>();
        foreach (var s in Signals)
        {
            if (!RouteSignal(s, blocked, Inside, hOcc, vOcc))
            {
                // 길이 막혔을 때 — 곧은 끝끼리 ㄱ자
                s.Points.Clear();
                s.Points.Add(s.From.Point);
                AddPoint(s.Points, s.From.StubEnd);
                AddPoint(s.Points, new SKPoint(s.To.StubEnd.X, s.From.StubEnd.Y));
                AddPoint(s.Points, s.To.StubEnd);
                AddPoint(s.Points, s.To.Point);
                Warnings.Add($"Signal line '{s.From.Key}'→'{s.To.Key}': no free path, so it is drawn across equipment; attach the instrument to other equipment (on) or enlarge the area");
            }

            for (var k = 0; k + 1 < s.Points.Count; k++)
            {
                var p = s.Points[k];
                var q = s.Points[k + 1];
                if (MathF.Abs(p.Y - q.Y) < 0.01f && Array.BinarySearch(_ys, p.Y) is var j and >= 0)
                    Add(hOcc, j, MathF.Min(p.X, q.X), MathF.Max(p.X, q.X));
                else if (MathF.Abs(p.X - q.X) < 0.01f && Array.BinarySearch(_xs, p.X) is var i and >= 0)
                    Add(vOcc, i, MathF.Min(p.Y, q.Y), MathF.Max(p.Y, q.Y));
            }
        }

        static void Add(Dictionary<int, List<(float, float, int)>> occ, int key, float a, float b)
        {
            if (!occ.TryGetValue(key, out var list))
                occ[key] = list = new();
            list.Add((a, b, -2));
        }
    }

    private bool RouteSignal(GraphSignal s, bool[] blocked, Func<float, float, bool> inside,
        Dictionary<int, List<(float a, float b, int net)>> hSig, Dictionary<int, List<(float a, float b, int net)>> vSig)
    {
        var a = s.From;
        var b = s.To;
        var (si, sj) = Snap(a.StubEnd);
        var (ti, tj) = Snap(b.StubEnd);
        var start = V(si, sj);
        var target = V(ti, tj);
        var forbidEnd = DirIndex(b.Out);
        var count = _xs.Length * _ys.Length * 4;
        var dist = new float[count];
        Array.Fill(dist, float.PositiveInfinity);
        var prev = new int[count];
        Array.Fill(prev, -1);
        var pq = new PriorityQueue<int, float>();
        var s0 = start * 4 + DirIndex(a.Out);
        dist[s0] = 0;
        pq.Enqueue(s0, H(si, sj, ti, tj));
        var found = -1;
        while (pq.TryDequeue(out var cur, out var pri))
        {
            var v = cur / 4;
            var dir = cur % 4;
            var g = dist[cur];
            if (pri - H(v / _ys.Length, v % _ys.Length, ti, tj) > g + 0.01f)
                continue;
            if (v == target)
            {
                if (dir == forbidEnd)
                    continue;
                found = cur;
                break;
            }

            var vi = v / _ys.Length;
            var vj = v % _ys.Length;
            for (var nd = 0; nd < 4; nd++)
            {
                if (nd == (dir + 2) % 4)
                    continue;
                var (di, dj) = Steps[nd];
                var ni = vi + di;
                var nj = vj + dj;
                if (ni < 0 || nj < 0 || ni >= _xs.Length || nj >= _ys.Length)
                    continue;
                var w = V(ni, nj);
                if (blocked[w] && w != target)
                    continue;
                float x0 = _xs[vi], y0 = _ys[vj], x1 = _xs[ni], y1 = _ys[nj];
                if (inside((x0 + x1) / 2, (y0 + y1) / 2))
                    continue;
                var len = MathF.Abs(x1 - x0) + MathF.Abs(y1 - y0);
                var cost = len;
                var horizontal = dj == 0;
                // 배관·다른 신호선 바로 옆을 나란히 가면 겹쳐 보인다
                var pipe = horizontal ? Check(_hOcc, _ys, vj, MathF.Min(x0, x1), MathF.Max(x0, x1), -1)
                    : Check(_vOcc, _xs, vi, MathF.Min(y0, y1), MathF.Max(y0, y1), -1);
                if (pipe != SegState.Free)
                    cost += 3 * len + 20;
                var sig = horizontal ? Check(hSig, _ys, vj, MathF.Min(x0, x1), MathF.Max(x0, x1), -1)
                    : Check(vSig, _xs, vi, MathF.Min(y0, y1), MathF.Max(y0, y1), -1);
                if (sig != SegState.Free)
                    cost += 2 * len + 10;
                if (nd != dir)
                    cost += BendCost;
                if (w == target && nd != (forbidEnd + 2) % 4)
                    cost += BendCost;
                var ns = w * 4 + nd;
                var nc = g + cost;
                if (nc < dist[ns])
                {
                    dist[ns] = nc;
                    prev[ns] = cur;
                    pq.Enqueue(ns, nc + H(ni, nj, ti, tj));
                }
            }
        }

        if (found < 0)
            return false;

        var cells = new List<int>();
        for (var c = found; c >= 0; c = prev[c])
            cells.Add(c / 4);
        cells.Reverse();
        s.Points.Clear();
        s.Points.Add(a.Point);
        foreach (var c in cells)
            AddPoint(s.Points, new SKPoint(_xs[c / _ys.Length], _ys[c % _ys.Length]));
        AddPoint(s.Points, b.Point);
        return true;
    }

    private int Score() => Lines.Sum(l => (l.Forced ? 1000 : 0) + l.Crossings * 10);

    private List<(List<SKPoint> Pts, List<SKPoint> Hops, int Cross, int Bends, bool Forced)> Snapshot() =>
        Lines.Select(l => (l.Points.ToList(), l.Hops.ToList(), l.Crossings, l.Bends, l.Forced)).ToList();

    private void Restore(List<(List<SKPoint> Pts, List<SKPoint> Hops, int Cross, int Bends, bool Forced)> snap)
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            var l = Lines[i];
            l.Points.Clear();
            l.Points.AddRange(snap[i].Pts);
            l.Hops.Clear();
            l.Hops.AddRange(snap[i].Hops);
            l.Crossings = snap[i].Cross;
            l.Bends = snap[i].Bends;
            l.Forced = snap[i].Forced;
        }
    }

    private void ResetRoutes()
    {
        _hOcc.Clear();
        _vOcc.Clear();
        _vertexNets.Clear();
        _cornerNets.Clear();
        foreach (var l in Lines)
        {
            l.Points.Clear();
            l.Hops.Clear();
            l.Crossings = 0;
            l.Bends = 0;
            l.Forced = false;
        }
    }

    private void RouteAll(List<GraphLine> order)
    {
        foreach (var line in order)
        {
            if (!RouteLine(line, strict: true))
            {
                line.Forced = true;
                if (!RouteLine(line, strict: false))
                    FallbackLine(line);
            }

            Occupy(line);
        }
    }

    private float[] Axis(float from, float to, SortedSet<float> special)
    {
        var list = new List<float>(special);
        for (var v = MathF.Floor(from / Lattice) * Lattice; v <= to; v += Lattice)
        {
            if (special.All(s => MathF.Abs(s - v) >= Lattice / 2))
                list.Add(v);
        }

        list.Sort();
        return list.ToArray();
    }

    private int V(int i, int j) => i * _ys.Length + j;

    private bool Inside(float x, float y)
    {
        foreach (var r in _obstacles)
        {
            if (x > r.Left + 0.01f && x < r.Right - 0.01f && y > r.Top + 0.01f && y < r.Bottom - 0.01f)
                return true;
        }

        return false;
    }

    private (int i, int j) Snap(SKPoint p) =>
        (Array.BinarySearch(_xs, p.X) is var i and >= 0 ? i : Nearest(_xs, p.X), Array.BinarySearch(_ys, p.Y) is var j and >= 0 ? j : Nearest(_ys, p.Y));

    private static int Nearest(float[] a, float v)
    {
        var best = 0;
        for (var k = 1; k < a.Length; k++)
        {
            if (MathF.Abs(a[k] - v) < MathF.Abs(a[best] - v))
                best = k;
        }

        return best;
    }

    // 방향: 0 +x, 1 +y, 2 -x, 3 -y
    private static readonly (int di, int dj)[] Steps = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    private static int DirIndex(PortSide s) => s switch
    {
        PortSide.Right => 0,
        PortSide.Bottom => 1,
        PortSide.Left => 2,
        _ => 3,
    };

    private bool RouteLine(GraphLine line, bool strict)
    {
        var a = line.From;
        var b = line.To;
        var sa = a.StubEnd;
        var sb = b.StubEnd;
        var (si, sj) = Snap(sa);
        var (ti, tj) = Snap(sb);
        var start = V(si, sj);
        var target = V(ti, tj);
        var startDir = DirIndex(a.Out);
        var forbidEnd = DirIndex(b.Out); // 도착 방향이 포트에서 나가는 방향이면 U턴

        var count = _xs.Length * _ys.Length * 4;
        var dist = new float[count];
        Array.Fill(dist, float.PositiveInfinity);
        var prev = new int[count];
        Array.Fill(prev, -1);
        var pq = new PriorityQueue<int, float>();
        var s0 = start * 4 + startDir;
        dist[s0] = 0;
        pq.Enqueue(s0, H(si, sj, ti, tj));
        var found = -1;

        while (pq.TryDequeue(out var cur, out var pri))
        {
            var v = cur / 4;
            var dir = cur % 4;
            var g = dist[cur];
            if (pri - H(v / _ys.Length, v % _ys.Length, ti, tj) > g + 0.01f)
                continue;
            if (v == target)
            {
                if (dir == forbidEnd)
                    continue;
                found = cur;
                break;
            }

            var vi = v / _ys.Length;
            var vj = v % _ys.Length;
            for (var nd = 0; nd < 4; nd++)
            {
                if (nd == (dir + 2) % 4)
                    continue;
                var turn = nd != dir;
                if (turn && strict && Others(_vertexNets, v, line.Net) && v != start)
                    continue; // 다른 배관 위에서 꺾지 않는다(닿아 보임)
                var (di, dj) = Steps[nd];
                var ni = vi + di;
                var nj = vj + dj;
                if (ni < 0 || nj < 0 || ni >= _xs.Length || nj >= _ys.Length)
                    continue;
                var w = V(ni, nj);
                if (_blocked[w] && w != target)
                    continue;
                var x0 = _xs[vi];
                var y0 = _ys[vj];
                var x1 = _xs[ni];
                var y1 = _ys[nj];
                if (Inside((x0 + x1) / 2, (y0 + y1) / 2))
                    continue;
                var horizontal = dj == 0;
                var cost = MathF.Abs(x1 - x0) + MathF.Abs(y1 - y0);
                var seg = horizontal ? Check(_hOcc, _ys, vj, MathF.Min(x0, x1), MathF.Max(x0, x1), line.Net)
                    : Check(_vOcc, _xs, vi, MathF.Min(y0, y1), MathF.Max(y0, y1), line.Net);
                if (seg == SegState.Blocked)
                {
                    if (strict)
                        continue;
                    cost += 400;
                }
                else if (seg == SegState.Reuse)
                {
                    cost *= ReuseFactor;
                }

                if (Others(_cornerNets, w, line.Net) && w != target)
                {
                    if (strict)
                        continue;
                    cost += 400;
                }
                else if (seg != SegState.Reuse && Others(_vertexNets, w, line.Net))
                {
                    cost += CrossCost;
                }

                if (turn)
                    cost += BendCost;
                if (w == target && nd != (forbidEnd + 2) % 4)
                    cost += BendCost; // 포트로 들어가며 한 번 더 꺾임
                var ns = w * 4 + nd;
                var nc = g + cost;
                if (nc < dist[ns])
                {
                    dist[ns] = nc;
                    prev[ns] = cur;
                    pq.Enqueue(ns, nc + H(ni, nj, ti, tj));
                }
            }
        }

        if (found < 0)
            return false;

        var cells = new List<int>();
        for (var c = found; c >= 0; c = prev[c])
            cells.Add(c / 4);
        cells.Reverse();
        line.Points.Clear();
        line.Points.Add(a.Point);
        foreach (var c in cells)
            AddPoint(line.Points, new SKPoint(_xs[c / _ys.Length], _ys[c % _ys.Length]));
        AddPoint(line.Points, b.Point);
        line.Bends = Math.Max(0, line.Points.Count - 2);
        return true;
    }

    private float H(int i, int j, int ti, int tj) => MathF.Abs(_xs[i] - _xs[ti]) + MathF.Abs(_ys[j] - _ys[tj]);

    // 꺾은선에 점 추가 — 같은 점은 버리고, 일직선 위 중간 점은 합친다
    private static void AddPoint(List<SKPoint> pts, SKPoint p)
    {
        if (pts.Count > 0 && Near(pts[^1], p))
            return;
        if (pts.Count >= 2)
        {
            var a = pts[^2];
            var b = pts[^1];
            if ((MathF.Abs(a.X - b.X) < 0.01f && MathF.Abs(b.X - p.X) < 0.01f) ||
                (MathF.Abs(a.Y - b.Y) < 0.01f && MathF.Abs(b.Y - p.Y) < 0.01f))
            {
                pts[^1] = p;
                return;
            }
        }

        pts.Add(p);
    }

    private static bool Near(SKPoint a, SKPoint b) => MathF.Abs(a.X - b.X) < 0.01f && MathF.Abs(a.Y - b.Y) < 0.01f;

    private enum SegState { Free, Reuse, Blocked }

    // 구간 검사 — 같은 줄에 같은 묶음이 이미 있으면 재사용, 가까운 줄(Pitch 미만)에 겹치는 구간이 있으면 막힘(다른 묶음·같은 묶음의 옆 줄)
    private SegState Check(Dictionary<int, List<(float a, float b, int net)>> occ, float[] lines, int index, float a, float b, int net)
    {
        var at = lines[index];
        var reuse = false;
        for (var k = index; k >= 0 && at - lines[k] < Pitch - 0.01f; k--)
        {
            if (Hit(k, out var r))
                return SegState.Blocked;
            reuse |= r;
        }

        for (var k = index + 1; k < lines.Length && lines[k] - at < Pitch - 0.01f; k++)
        {
            if (Hit(k, out _))
                return SegState.Blocked;
        }

        return reuse ? SegState.Reuse : SegState.Free;

        bool Hit(int k, out bool sameLineReuse)
        {
            sameLineReuse = false;
            if (!occ.TryGetValue(k, out var list))
                return false;
            foreach (var (x0, x1, n) in list)
            {
                if (x0 < b - 0.01f && a < x1 - 0.01f)
                {
                    if (n == net && k == index)
                    {
                        sameLineReuse = true;
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }
    }

    private static bool Others(Dictionary<int, HashSet<int>> map, int v, int net) =>
        map.TryGetValue(v, out var set) && set.Any(n => n != net);

    private void Occupy(GraphLine line)
    {
        var pts = line.Points;
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            var p = pts[k];
            var q = pts[k + 1];
            if (MathF.Abs(p.Y - q.Y) < 0.01f && Array.BinarySearch(_ys, p.Y) is var j and >= 0)
                Add(_hOcc, j, MathF.Min(p.X, q.X), MathF.Max(p.X, q.X), line.Net);
            else if (MathF.Abs(p.X - q.X) < 0.01f && Array.BinarySearch(_xs, p.X) is var i and >= 0)
                Add(_vOcc, i, MathF.Min(p.Y, q.Y), MathF.Max(p.Y, q.Y), line.Net);

            // 지나는 꼭짓점
            var (ai, aj) = Snap(p);
            var (bi, bj) = Snap(q);
            for (var x = Math.Min(ai, bi); x <= Math.Max(ai, bi); x++)
            {
                for (var y = Math.Min(aj, bj); y <= Math.Max(aj, bj); y++)
                    Mark(_vertexNets, V(x, y), line.Net);
            }
        }

        for (var k = 0; k < pts.Count; k++)
        {
            if (k == 0 || k == pts.Count - 1 || !Collinear(pts[k - 1], pts[k], pts[k + 1]))
            {
                var (ci, cj) = Snap(pts[k]);
                Mark(_cornerNets, V(ci, cj), line.Net);
            }
        }

        static void Add(Dictionary<int, List<(float, float, int)>> occ, int key, float a, float b, int net)
        {
            if (!occ.TryGetValue(key, out var list))
                occ[key] = list = new();
            list.Add((a, b, net));
        }

        static void Mark(Dictionary<int, HashSet<int>> map, int v, int net)
        {
            if (!map.TryGetValue(v, out var set))
                map[v] = set = new();
            set.Add(net);
        }
    }

    private static bool Collinear(SKPoint a, SKPoint b, SKPoint c) =>
        (MathF.Abs(a.X - b.X) < 0.01f && MathF.Abs(b.X - c.X) < 0.01f) || (MathF.Abs(a.Y - b.Y) < 0.01f && MathF.Abs(b.Y - c.Y) < 0.01f);

    // 길이 막혔을 때 — 포트 앞 끝끼리 ㄱ자로 잇는다(경고)
    private void FallbackLine(GraphLine line)
    {
        var a = line.From;
        var b = line.To;
        var sa = a.StubEnd;
        var sb = b.StubEnd;
        line.Points.Clear();
        line.Points.Add(a.Point);
        AddPoint(line.Points, sa);
        AddPoint(line.Points, new SKPoint(sb.X, sa.Y));
        AddPoint(line.Points, sb);
        AddPoint(line.Points, b.Point);
    }

    // ── 기준 크기 모양 늘이기: 꺾임 수·순서·도는 쪽은 base-size 에서 정한 그대로, 꺾인 자리는 양옆 장비 사이 같은 비율 자리로 ──
    // (창 크기마다 다시 길을 찾으면 비용이 같은 두 길 중 이기는 쪽이 바뀌어 도면 모양이 달라진다)
    private bool Stretch(ProcessGraph shape)
    {
        var dx = new float[Nodes.Count];
        var dy = new float[Nodes.Count];
        var xs = new List<(float From, float To, bool Port)>();
        var ys = new List<(float From, float To, bool Port)>();
        for (var i = 0; i < Nodes.Count; i++)
        {
            dx[i] = Nodes[i].X - shape.Nodes[i].X;
            dy[i] = Nodes[i].Y - shape.Nodes[i].Y;
            if (Nodes[i].Pinned)
                continue;
            foreach (var r in Nodes[i].Extras.Prepend(Nodes[i].Body).Prepend(new SKRect(0, 0, Nodes[i].W, Nodes[i].H)).Where(r => !r.IsEmpty))
            {
                var a = shape.Nodes[i].Abs(r);
                foreach (var x in new[] { a.Left, a.MidX, a.Right })
                    xs.Add((x, x + dx[i], false));
                foreach (var y in new[] { a.Top, a.MidY, a.Bottom })
                    ys.Add((y, y + dy[i], false));
            }
        }

        // 선 끝(포트) — 가장 앞선 기준점. 갈라지는 선들이 같이 지나던 줄이 끝 자리와 따로 놀지 않게
        foreach (var e in shape.Lines.SelectMany(l => new[] { l.From, l.To }).Concat(shape.Signals.SelectMany(g => new[] { g.From, g.To })))
        {
            xs.Add((e.Point.X, e.Point.X + dx[e.Node], true));
            ys.Add((e.Point.Y, e.Point.Y + dy[e.Node], true));
        }

        var fx = AxisMap(xs);
        var fy = AxisMap(ys);

        void CopyEnd(GraphEnd src, GraphEnd dst)
        {
            dst.Point = new SKPoint(src.Point.X + dx[src.Node], src.Point.Y + dy[src.Node]);
            dst.Out = src.Out;
            dst.StubLen = src.StubLen;
        }

        var folded = false;
        List<SKPoint> Map(List<SKPoint> src, SKPoint a, SKPoint b)
        {
            var pts = Fit(src, a, b);
            for (var k = 0; k + 1 < src.Count && k + 1 < pts.Count && pts.Count == src.Count; k++)
            {
                var (u, v) = (src[k + 1] - src[k], pts[k + 1] - pts[k]);
                if (u.X * v.X < -0.25f || u.Y * v.Y < -0.25f)
                    folded = true;
            }

            return pts;
        }

        List<SKPoint> Fit(List<SKPoint> src, SKPoint a, SKPoint b)
        {
            var pts = src.Select(p => new SKPoint(fx(p.X), fy(p.Y))).ToList();
            if (pts.Count < 2)
                return pts;
            bool Flat(int k) => MathF.Abs(src[k].Y - src[k + 1].Y) < 0.01f; // 원래 가로 구간
            pts[0] = a;
            pts[^1] = b;
            if (pts.Count == 2)
            {
                // 곧은 선인데 양 끝이 어긋났다(맞춤이 비율대로 안 따라옴) — 가운데에서 한 번 비킨다
                if (Flat(0) && MathF.Abs(a.Y - b.Y) > 0.01f)
                    pts.InsertRange(1, [new SKPoint((a.X + b.X) / 2, a.Y), new SKPoint((a.X + b.X) / 2, b.Y)]);
                else if (!Flat(0) && MathF.Abs(a.X - b.X) > 0.01f)
                    pts.InsertRange(1, [new SKPoint(a.X, (a.Y + b.Y) / 2), new SKPoint(b.X, (a.Y + b.Y) / 2)]);
                return pts;
            }

            // 끝 구간이 비스듬해지지 않게 — 포트 옆 점을 포트 줄에 맞춘다
            pts[1] = Flat(0) ? new SKPoint(pts[1].X, a.Y) : new SKPoint(a.X, pts[1].Y);
            var n = pts.Count;
            pts[n - 2] = Flat(n - 2) ? new SKPoint(pts[n - 2].X, b.Y) : new SKPoint(b.X, pts[n - 2].Y);
            return pts;
        }

        for (var k = 0; k < Lines.Count; k++)
        {
            var (src, dst) = (shape.Lines[k], Lines[k]);
            CopyEnd(src.From, dst.From);
            CopyEnd(src.To, dst.To);
            dst.Net = src.Net;
            dst.Bends = src.Bends;
            dst.Forced = src.Forced;
            dst.Points.AddRange(Map(src.Points, dst.From.Point, dst.To.Point));
        }

        for (var k = 0; k < Signals.Count; k++)
        {
            var (src, dst) = (shape.Signals[k], Signals[k]);
            CopyEnd(src.From, dst.From);
            CopyEnd(src.To, dst.To);
            dst.Points.AddRange(Map(src.Points, dst.From.Point, dst.To.Point));
        }

        // 줄이면서 배관이 장비를 지나거나 다른 묶음끼리 붙으면 그 모양은 이 크기에 안 들어간다(기준 모양에 이미 있던 만큼은 괜찮다)
        var (hits0, gap0, same0) = Clash(shape.Nodes, shape.Lines, shape.Signals);
        var (hits, gap, same) = Clash(Nodes, Lines, Signals);
        if (folded || hits > hits0 || gap < MathF.Min(Pitch / 2, gap0 - 0.5f) || same < MathF.Min(Pitch - 0.5f, same0 - 0.5f))
            return false;
        FindHops();
        foreach (var w in shape.Warnings.Where(w => !Warnings.Contains(w)))
            Warnings.Add(w);
        return true;
    }

    // 배관·신호선이 장비 몸통을 지나는 수, 다른 묶음 배관의 나란한 구간 사이 가장 좁은 간격,
    // 같은 묶음 배관이 합쳐지지 않고 나란히 가는 가장 좁은 간격(겹쳐 하나로 보이는 0 은 뺀다)
    private static (int Hits, float Gap, float Same) Clash(List<GraphNode> nodes, List<GraphLine> lines, List<GraphSignal> signals)
    {
        static IEnumerable<(SKPoint A, SKPoint B)> Segs(List<SKPoint> pts) => pts.Zip(pts.Skip(1));
        var bodies = nodes.Where(n => !n.Pinned).Select(n =>
        {
            var r = n.Abs(n.Body);
            r.Inflate(-2, -2);
            return r;
        }).ToList();
        var hits = 0;
        foreach (var (a, b) in lines.SelectMany(l => Segs(l.Points)).Concat(signals.SelectMany(s => Segs(s.Points))))
        {
            var seg = new SKRect(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
            hits += bodies.Count(r => seg.Left < r.Right && seg.Right > r.Left && seg.Top < r.Bottom && seg.Bottom > r.Top);
        }

        var gap = float.PositiveInfinity;
        var same = float.PositiveInfinity;
        var segs = lines.SelectMany(l => Segs(l.Points).Select(s => (l.Net, s.A, s.B))).ToList();
        for (var i = 0; i < segs.Count; i++)
        {
            for (var j = i + 1; j < segs.Count; j++)
            {
                var (p, q) = (segs[i], segs[j]);
                var flat = MathF.Abs(p.A.Y - p.B.Y) < 0.01f;
                if (flat != MathF.Abs(q.A.Y - q.B.Y) < 0.01f)
                    continue;
                float Lo(SKPoint u, SKPoint v) => flat ? MathF.Min(u.X, v.X) : MathF.Min(u.Y, v.Y);
                float Hi(SKPoint u, SKPoint v) => flat ? MathF.Max(u.X, v.X) : MathF.Max(u.Y, v.Y);
                if (MathF.Min(Hi(p.A, p.B), Hi(q.A, q.B)) - MathF.Max(Lo(p.A, p.B), Lo(q.A, q.B)) <= 0.5f)
                    continue; // 나란히 겹치는 구간이 없다
                var apart = flat ? MathF.Abs(p.A.Y - q.A.Y) : MathF.Abs(p.A.X - q.A.X);
                if (p.Net != q.Net)
                    gap = MathF.Min(gap, apart);
                else if (apart > 0.01f)
                    same = MathF.Min(same, apart);
            }
        }

        return (hits, gap, same);
    }

    // 기준 좌표 → 지금 좌표(같은 기준 좌표는 평균 — 선 끝이 있으면 선 끝끼리만, 순서는 지킨다). 기준점 사이는 비율대로, 밖은 가장 가까운 기준점만큼 옮긴다.
    // 선 끝은 그 자리 그대로 지키고, 순서를 깨는(앞 선 끝보다 작거나 뒤 선 끝보다 큰) 장비 기준점은 뺀다 — 장비 묶음마다 옮긴 양이 달라 생긴다
    private static Func<float, float> AxisMap(List<(float From, float To, bool Port)> pairs)
    {
        var all = pairs.GroupBy(p => MathF.Round(p.From * 100))
            .Select(g => g.Any(p => p.Port) ? g.Where(p => p.Port).ToList() : g.ToList())
            .Select(g => (From: g.Average(p => p.From), To: g.Average(p => p.To), Port: g[0].Port))
            .OrderBy(p => p.From).ToList();
        var ports = all.Where(p => p.Port).ToList();
        // 선 끝과 같은 좌표는 그 선 끝 자리 그대로(아래 순서 맞춤 전) — 갈라지는 선들이 같이 지나던 줄이 끝 자리와 어긋나지 않게
        var exact = ports.ToDictionary(p => MathF.Round(p.From * 100), p => p.To);
        for (var i = 1; i < ports.Count; i++)
            if (ports[i].To < ports[i - 1].To)
                ports[i] = (ports[i].From, ports[i - 1].To, true); // 줄어든 창에서 장비가 겹쳐도 거꾸로 접히지 않게
        var pts = ports.Select(p => (p.From, p.To)).ToList();
        foreach (var (from, to, _) in all.Where(p => !p.Port))
        {
            var before = ports.LastOrDefault(p => p.From < from);
            var after = ports.FirstOrDefault(p => p.From > from);
            if ((!before.Port || to >= before.To) && (!after.Port || to <= after.To))
                pts.Add((from, to));
        }

        pts = pts.OrderBy(p => p.From).ToList();
        for (var i = 1; i < pts.Count; i++)
            if (pts[i].To < pts[i - 1].To)
                pts[i] = (pts[i].From, pts[i - 1].To);
        if (pts.Count == 0)
            return v => v;
        return v =>
        {
            if (exact.TryGetValue(MathF.Round(v * 100), out var at))
                return at;
            if (v <= pts[0].From)
                return v + pts[0].To - pts[0].From;
            if (v >= pts[^1].From)
                return v + pts[^1].To - pts[^1].From;
            var i = pts.FindLastIndex(p => p.From <= v);
            var (a, b) = (pts[i], pts[i + 1]);
            return a.To + (v - a.From) / (b.From - a.From) * (b.To - a.To);
        };
    }

    // ── 교차 점프: 다른 묶음의 가로선을 지나는 세로선(위→아래 흐름이면 세로선을 지나는 가로선)에 반원 ──
    private void FindHops()
    {
        var segs = new List<(GraphLine line, SKPoint a, SKPoint b)>();
        foreach (var l in Lines)
        {
            for (var k = 0; k + 1 < l.Points.Count; k++)
                segs.Add((l, l.Points[k], l.Points[k + 1]));
        }

        foreach (var (line, a, b) in segs)
        {
            var hopVertical = !Vertical;
            var isVertical = MathF.Abs(a.X - b.X) < 0.01f;
            if (isVertical != hopVertical)
                continue;
            foreach (var (other, c, d) in segs)
            {
                if (other.Net == line.Net)
                    continue;
                var otherVertical = MathF.Abs(c.X - d.X) < 0.01f;
                if (otherVertical == isVertical)
                    continue;
                // 교점 — 둘 다 끝점이 아닌 안쪽
                var p = isVertical ? new SKPoint(a.X, c.Y) : new SKPoint(c.X, a.Y);
                if (Between(p, a, b) && Between(p, c, d))
                {
                    line.Hops.Add(p);
                    line.Crossings++;
                    other.Crossings++;
                }
            }
        }

        static bool Between(SKPoint p, SKPoint a, SKPoint b)
        {
            const float m = 4f;
            return MathF.Abs(a.X - b.X) < 0.01f
                ? MathF.Abs(p.X - a.X) < 0.01f && p.Y > MathF.Min(a.Y, b.Y) + m && p.Y < MathF.Max(a.Y, b.Y) - m
                : MathF.Abs(p.Y - a.Y) < 0.01f && p.X > MathF.Min(a.X, b.X) + m && p.X < MathF.Max(a.X, b.X) - m;
        }
    }

    // 모두를 (여백, 여백) 부터 시작하게 옮기고 전체 크기를 정한다
    private void Normalize()
    {
        var bounds = SKRect.Empty;
        var first = true;
        void Union(SKRect r)
        {
            bounds = first ? r : SKRect.Union(bounds, r);
            first = false;
        }

        foreach (var n in Nodes)
            Union(SKRect.Create(n.X, n.Y, n.W, n.H));
        foreach (var l in Lines)
        {
            foreach (var p in l.Points)
                Union(new SKRect(p.X - Pitch / 2, p.Y - Pitch / 2, p.X + Pitch / 2, p.Y + Pitch / 2));
        }

        foreach (var s in Signals)
        {
            foreach (var p in s.Points)
                Union(new SKRect(p.X - 2, p.Y - 2, p.X + 2, p.Y + 2));
        }

        if (Fixed)
        {
            // 좌표 배치 — 좌표가 곧 영역 기준이라 옮기지 않는다. 크기 = 원점부터 내용 끝 + 여백
            Size = new SKSize(MathF.Max(0, bounds.Right) + Margin, MathF.Max(0, bounds.Bottom) + Margin);
            return;
        }

        if (Unit)
            bounds = Frame; // 유닛 크기 = 테두리(포트가 테두리 위에 있어야 바깥 배관과 만난다)
        else
            bounds.Inflate(Margin, Margin);
        var dx = -bounds.Left;
        var dy = -bounds.Top;
        Frame = new SKRect(Frame.Left + dx, Frame.Top + dy, Frame.Right + dx, Frame.Bottom + dy);
        foreach (var n in Nodes)
        {
            n.X += dx;
            n.Y += dy;
        }

        foreach (var l in Lines)
        {
            for (var k = 0; k < l.Points.Count; k++)
                l.Points[k] = new SKPoint(l.Points[k].X + dx, l.Points[k].Y + dy);
            for (var k = 0; k < l.Hops.Count; k++)
                l.Hops[k] = new SKPoint(l.Hops[k].X + dx, l.Hops[k].Y + dy);
            l.From.Point = new SKPoint(l.From.Point.X + dx, l.From.Point.Y + dy);
            l.To.Point = new SKPoint(l.To.Point.X + dx, l.To.Point.Y + dy);
        }

        foreach (var s in Signals)
        {
            for (var k = 0; k < s.Points.Count; k++)
                s.Points[k] = new SKPoint(s.Points[k].X + dx, s.Points[k].Y + dy);
            s.From.Point = new SKPoint(s.From.Point.X + dx, s.From.Point.Y + dy);
            s.To.Point = new SKPoint(s.To.Point.X + dx, s.To.Point.Y + dy);
        }

        Size = new SKSize(bounds.Width, bounds.Height);
    }
}
