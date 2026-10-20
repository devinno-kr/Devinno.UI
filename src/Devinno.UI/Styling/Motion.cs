using System.Globalization;

namespace Devinno.UI.Styling;

/// <summary>
/// The value of the style key <c>transform</c> — translate and scale only. Applied to drawing only; layout and hit areas stay as they are (centered on the box).
/// </summary>
/// <param name="Tx">Horizontal translation (px).</param>
/// <param name="Ty">Vertical translation (px).</param>
/// <param name="Sx">Horizontal scale factor.</param>
/// <param name="Sy">Vertical scale factor.</param>
public readonly record struct Transform2D(float Tx, float Ty, float Sx, float Sy)
{
    /// <summary>No change (<c>transform: none</c>).</summary>
    public static readonly Transform2D Identity = new(0, 0, 1, 1);

    /// <summary>Whether this value makes no change.</summary>
    public bool IsIdentity => Tx == 0 && Ty == 0 && Sx == 1 && Sy == 1;

    /// <summary>
    /// A value made of <c>translate(x[, y])</c> <c>translateX(x)</c> <c>translateY(y)</c> <c>scale(s[, sy])</c> <c>scaleX(s)</c> <c>scaleY(s)</c> joined with spaces.
    /// Regardless of the written order, they are combined as "translate, then scale around the center". <c>none</c> or an empty value gives <see cref="Identity"/>. Returns <see langword="null"/> if there is an unknown function.
    /// </summary>
    internal static Transform2D? Parse(string value)
    {
        var v = value.Trim();
        if (v.Length == 0 || v.Equals("none", StringComparison.OrdinalIgnoreCase))
            return Identity;
        float tx = 0, ty = 0, sx = 1, sy = 1;
        var i = 0;
        while (i < v.Length)
        {
            while (i < v.Length && char.IsWhiteSpace(v[i]))
                i++;
            if (i >= v.Length)
                break;
            var open = v.IndexOf('(', i);
            var close = open < 0 ? -1 : v.IndexOf(')', open);
            if (open < 0 || close < 0)
                return null;
            var name = v[i..open].Trim().ToLowerInvariant();
            var args = v[(open + 1)..close].Split(',', ' ', StringSplitOptions.RemoveEmptyEntries).Select(Num).ToArray();
            if (args.Length == 0 || args.Any(a => a is null))
                return null;
            switch (name)
            {
                case "translate": tx += args[0]!.Value; ty += args.Length > 1 ? args[1]!.Value : 0; break;
                case "translatex": tx += args[0]!.Value; break;
                case "translatey": ty += args[0]!.Value; break;
                case "scale": sx *= args[0]!.Value; sy *= args.Length > 1 ? args[1]!.Value : args[0]!.Value; break;
                case "scalex": sx *= args[0]!.Value; break;
                case "scaley": sy *= args[0]!.Value; break;
                default: return null;
            }
            i = close + 1;
        }
        return new Transform2D(tx, ty, sx, sy);
    }

    private static float? Num(string s)
    {
        s = s.Trim();
        if (s.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            s = s[..^2];
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null;
    }

    internal static Transform2D Lerp(Transform2D a, Transform2D b, float t)
        => new(a.Tx + (b.Tx - a.Tx) * t, a.Ty + (b.Ty - a.Ty) * t, a.Sx + (b.Sx - a.Sx) * t, a.Sy + (b.Sy - a.Sy) * t);
}

/// <summary>One entry of the style key <c>transition</c> — which property to change, for how long, and how.</summary>
/// <param name="Property">The property name (<c>background-color</c> <c>color</c> <c>border-color</c> <c>opacity</c> <c>transform</c> <c>all</c>).</param>
/// <param name="DurationMs">The duration (ms).</param>
/// <param name="Easing">The easing curve.</param>
/// <param name="DelayMs">The wait before starting (ms).</param>
public sealed record TransitionSpec(string Property, float DurationMs, Easing Easing, float DelayMs)
{
    internal static readonly string[] Properties = { "background-color", "background", "color", "border-color", "opacity", "transform", "all" };

    /// <summary>
    /// <c>속성 시간 [곡선] [지연], …</c> (property duration [easing] [delay], …; same as CSS). Duration is <c>200ms</c> or <c>0.2s</c>. Easing is <c>linear</c> <c>ease</c> (default) <c>ease-in</c> <c>ease-out</c> <c>ease-in-out</c>.
    /// <c>none</c> gives an empty list. Invalid entries are skipped (their source text goes to <paramref name="bad"/>).
    /// </summary>
    internal static List<TransitionSpec> Parse(string value, List<string>? bad = null)
    {
        var list = new List<TransitionSpec>();
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            return list;
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? prop = null;
            float? dur = null, delay = null;
            var easing = Easing.Ease;
            var ok = true;
            foreach (var w in part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var lower = w.ToLowerInvariant();
                if (Time(lower) is { } ms)
                {
                    if (dur is null) dur = ms;
                    else delay = ms;
                }
                else if (EasingOf(lower) is { } e)
                    easing = e;
                else if (prop is null && Properties.Contains(lower))
                    prop = lower == "background" ? "background-color" : lower;
                else
                    ok = false;
            }
            if (!ok || dur is null || dur <= 0)
            {
                bad?.Add(part);
                continue;
            }
            list.Add(new TransitionSpec(prop ?? "all", dur.Value, easing, delay ?? 0));
        }
        return list;
    }

    private static float? Time(string w)
    {
        if (w.EndsWith("ms") && float.TryParse(w[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
            return ms;
        if (w.EndsWith("s") && float.TryParse(w[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
            return s * 1000f;
        return null;
    }

    private static Easing? EasingOf(string w) => w switch
    {
        "linear" => Easing.Linear,
        "ease" => Easing.Ease,
        "ease-in" => Easing.EaseIn,
        "ease-out" => Easing.EaseOut,
        "ease-in-out" => Easing.EaseInOut,
        _ => null,
    };
}

/// <summary>Transition easing curve (the same cubic-bezier as the CSS keyword of the same name).</summary>
public enum Easing
{
    /// <summary>Constant speed (<c>linear</c>).</summary>
    Linear,
    /// <summary>Starts slowly, speeds up, then ends slowly (<c>ease</c>, the default).</summary>
    Ease,
    /// <summary>Starts slowly (<c>ease-in</c>).</summary>
    EaseIn,
    /// <summary>Ends slowly (<c>ease-out</c>).</summary>
    EaseOut,
    /// <summary>Starts slowly and ends slowly (<c>ease-in-out</c>).</summary>
    EaseInOut,
}

internal static class EasingMath
{
    /// <summary>Progress x (0–1) → curve value (0–1). Solves the CSS cubic-bezier with Newton's method.</summary>
    public static float Apply(Easing e, float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return e switch
        {
            Easing.Linear => x,
            Easing.Ease => Bezier(0.25f, 0.1f, 0.25f, 1f, x),
            Easing.EaseIn => Bezier(0.42f, 0f, 1f, 1f, x),
            Easing.EaseOut => Bezier(0f, 0f, 0.58f, 1f, x),
            _ => Bezier(0.42f, 0f, 0.58f, 1f, x),
        };
    }

    private static float Bezier(float x1, float y1, float x2, float y2, float x)
    {
        static float Curve(float a, float b, float t) => 3 * a * t * (1 - t) * (1 - t) + 3 * b * t * t * (1 - t) + t * t * t;
        static float Slope(float a, float b, float t) => 3 * a * (1 - t) * (1 - t) + 6 * (b - a) * t * (1 - t) + 3 * (1 - b) * t * t;
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var err = Curve(x1, x2, t) - x;
            if (MathF.Abs(err) < 1e-4f)
                break;
            var d = Slope(x1, x2, t);
            if (MathF.Abs(d) < 1e-6f)
                break;
            t = Math.Clamp(t - err / d, 0f, 1f);
        }
        return Curve(y1, y2, t);
    }
}
