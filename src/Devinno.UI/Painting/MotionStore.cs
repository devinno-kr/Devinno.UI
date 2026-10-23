using Devinno.UI.Layout;
using Devinno.UI.Styling;

namespace Devinno.UI.Painting;

/// <summary>
/// CSS <c>transition</c> state — held by one surface (<see cref="DvSurface"/>) and passed to <see cref="Painter"/> every frame.
/// It remembers the currently visible values per box (keyed by tree path), and when the values of the style chosen at draw time (default, hover, press, disabled) differ,
/// it gradually moves from the current value to the new value according to that style's <c>transition</c>. A box seen for the first time gets the value immediately (no animation on first draw — same as CSS).
/// </summary>
internal sealed class MotionStore
{
    private sealed class Channel<T>(Func<T, T, float, T> lerp) where T : struct
    {
        public T Shown, From, To;
        public long Start;
        public TransitionSpec? Spec;
        public bool Moving;

        public void Init(T v)
        {
            Shown = From = To = v;
            Moving = false;
        }

        // 목표가 바뀌면 지금 보이는 값에서 새로 시작(전환이 없으면 바로). settle = 진행 중인 것까지 끝난 값으로
        public T Step(T target, TransitionSpec? spec, long now, bool settle)
        {
            if (settle)
            {
                Init(target);
                return Shown;
            }

            if (!EqualityComparer<T>.Default.Equals(target, To))
            {
                From = Shown;
                To = target;
                Start = now;
                Spec = spec;
                Moving = spec is not null;
                if (!Moving)
                    Shown = target;
            }

            if (!Moving || Spec is not { } s)
                return Shown;
            var elapsed = now - Start - s.DelayMs;
            if (elapsed <= 0)
                return Shown = From;
            var x = elapsed / s.DurationMs;
            if (x >= 1)
            {
                Moving = false;
                return Shown = To;
            }
            return Shown = lerp(From, To, EasingMath.Apply(s.Easing, x));
        }
    }

    private sealed class Box
    {
        public object Origin = null!;
        public long Seen;
        public readonly Channel<Color4> Bg = new(Color4.Lerp), Fg = new(Color4.Lerp), Border = new(Color4.Lerp);
        public readonly Channel<float> Opacity = new((a, b, t) => a + (b - a) * t);
        public readonly Channel<Transform2D> Xf = new(Transform2D.Lerp);
    }

    // 색은 RGBA 실수로 보간(널 = 투명)
    private readonly record struct Color4(float R, float G, float B, float A)
    {
        public static Color4 Of(DvColor? c) => c is { } v ? new(v.R, v.G, v.B, v.A) : new(0, 0, 0, 0);
        public DvColor ToColor() => new((byte)MathF.Round(R), (byte)MathF.Round(G), (byte)MathF.Round(B), (byte)MathF.Round(A));
        public static Color4 Lerp(Color4 a, Color4 b, float t)
            => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
    }

    private readonly Dictionary<long, Box> _boxes = new();
    private long _frame;

    /// <summary>Whether a transition was still in progress in the last frame — the surface then requests the next frame.</summary>
    public bool Active { get; private set; }

    /// <summary>When <see langword="true"/>, every value jumps to its target this frame (transitions in progress finish) — for offscreen renders that show the current state.</summary>
    public bool Settle { get; set; }

    public void BeginFrame()
    {
        _frame++;
        Active = false;
    }

    /// <summary>Discards the memory of boxes not visible in this frame (so it does not accumulate as pages change).</summary>
    public void EndFrame()
    {
        if (_boxes.Count == 0)
            return;
        List<long>? gone = null;
        foreach (var (k, b) in _boxes)
        {
            if (b.Seen != _frame)
                (gone ??= new()).Add(k);
        }
        if (gone is not null)
            foreach (var k in gone)
                _boxes.Remove(k);
    }

    /// <summary>Whether the box has a transition (any state style has a transition).</summary>
    public static bool Wants(LayoutBox box)
        => box.Style.Transitions.Count > 0 || box.StyleHover?.Transitions.Count > 0 ||
           box.StylePress?.Transitions.Count > 0 || box.StyleDisabled?.Transitions.Count > 0;

    /// <summary>Turns the chosen style <paramref name="st"/> into the values to show now — unchanged if nothing changed, or a copy with intermediate values during a transition.</summary>
    public ComputedStyle Apply(long key, LayoutBox box, ComputedStyle st, long now)
    {
        var origin = (object)box.Origin;
        if (!_boxes.TryGetValue(key, out var b) || !ReferenceEquals(b.Origin, origin))
        {
            b = new Box { Origin = origin };
            b.Bg.Init(Color4.Of(st.Gradient is null ? st.Background : null));
            b.Fg.Init(Color4.Of(st.Color));
            b.Border.Init(Color4.Of(st.BorderColor));
            b.Opacity.Init(st.Opacity);
            b.Xf.Init(st.Transform);
            _boxes[key] = b;
        }
        b.Seen = _frame;

        TransitionSpec? Spec(string prop)
            => st.Transitions.LastOrDefault(t => t.Property == prop || t.Property == "all");

        var bg = b.Bg.Step(Color4.Of(st.Gradient is null ? st.Background : null), st.Gradient is null ? Spec("background-color") : null, now, Settle);
        var fg = b.Fg.Step(Color4.Of(st.Color), Spec("color"), now, Settle);
        var bc = b.Border.Step(Color4.Of(st.BorderColor), Spec("border-color"), now, Settle);
        var op = b.Opacity.Step(st.Opacity, Spec("opacity"), now, Settle);
        var xf = b.Xf.Step(st.Transform, Spec("transform"), now, Settle);
        if (b.Bg.Moving || b.Fg.Moving || b.Border.Moving || b.Opacity.Moving || b.Xf.Moving)
            Active = true;

        var changed = st.Gradient is null && bg != Color4.Of(st.Background) || fg != Color4.Of(st.Color) ||
                      bc != Color4.Of(st.BorderColor) || op != st.Opacity || xf != st.Transform;
        if (!changed)
            return st;
        var c = st.Copy();
        if (st.Gradient is null)
            c.Background = bg.A <= 0 && st.Background is null && !b.Bg.Moving ? null : bg.ToColor();
        c.Color = fg.ToColor();
        if (st.BorderColor is not null || b.Border.Moving)
            c.BorderColor = bc.ToColor();
        c.Opacity = op;
        c.Transform = xf;
        return c;
    }
}
