using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Devinno.UI.Widgets;
using SkiaSharp;

namespace Devinno.UI.Scene3D;

/// <summary>
/// Turns the definitions (child elements) of a SceneView into pose, color and clip values.
/// <code>
/// &lt;Rotate node="Turret" axis="y" value="{Turret}"/&gt;                     degrees, value × scale + offset
/// &lt;Rotate node="GearB"  axis="y" follow="GearA" ratio="-2"/&gt;            follower: amount of the same kind on GearA × ratio
/// &lt;Move   node="LiftRod" axis="y" value="{Lift}" scale="0.001"/&gt;         meters
/// &lt;Color  node="PumpLamp" value="{LampColor}" amount="1"/&gt;              #RRGGBB(AA) or a theme token, empty = model color
/// &lt;Clip   name="CrankCycle" value="{Crank}" min="0" max="360"/&gt;          value min..max = clip start..end
/// </code>
/// axis is x, y, z, -x, -y, -z or a vector such as "0 1 0". A definition with disabled true is skipped.
/// Values written through SceneState replace the markup value of the same kind on the same node.
/// </summary>
internal static class MarkupPose
{
    public static readonly string[] Tags = ["Rotate", "Move", "Color", "Clip"];

    private static readonly Dictionary<string, HashSet<string>> Allowed = new()
    {
        ["Rotate"] = ["node", "axis", "value", "scale", "offset", "follow", "ratio", "disabled"],
        ["Move"] = ["node", "axis", "value", "scale", "offset", "follow", "ratio", "disabled"],
        ["Color"] = ["node", "value", "amount", "disabled"],
        ["Clip"] = ["name", "value", "min", "max", "disabled"],
    };

    private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);

    /// <summary>Merges the markup values with the values written from code (code wins). Hash changes when a markup value changes.</summary>
    public static (PoseSnapshot Pose, int Hash) Build(SceneModel model, IReadOnlyList<DvDefinition> defs, PoseSnapshot state,
        Func<string, SKColor?> color)
    {
        var rotation = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
        var offset = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        var tint = new Dictionary<string, (SKColor, float)>(StringComparer.Ordinal);
        var clips = new Dictionary<string, float>(StringComparer.Ordinal);
        var hash = new HashCode();

        foreach (var d in defs)
        {
            foreach (var name in d.Names)
                if (!Allowed[d.Tag].Contains(name))
                    Warn($"<{d.Tag}> has no attribute '{name}' (it reads {string.Join(", ", Allowed[d.Tag])})");
            if (d.Flag("disabled"))
                continue;

            switch (d.Tag)
            {
                case "Rotate" or "Move":
                {
                    var node = d.Text("node");
                    if (string.IsNullOrEmpty(node) || Axis(d) is not { } axis || Amount(defs, d, 0) is not { } amount)
                        continue;
                    hash.Add(d.Tag);
                    hash.Add(node);
                    hash.Add(axis);
                    hash.Add(amount);
                    if (d.Tag == "Rotate")
                    {
                        var q = Quaternion.CreateFromAxisAngle(axis, (float)(amount * Math.PI / 180));
                        rotation[node] = rotation.TryGetValue(node, out var r) ? r * q : q;
                    }
                    else
                    {
                        offset[node] = offset.GetValueOrDefault(node) + axis * (float)amount;
                    }
                    break;
                }
                case "Color":
                {
                    var node = d.Text("node");
                    var text = d.Text("value");
                    if (string.IsNullOrEmpty(node) || string.IsNullOrWhiteSpace(text))
                        continue; // 빈 값 = 원래 색
                    if (color(text.Trim()) is not { } c)
                    {
                        Warn($"<Color node=\"{node}\">: '{text}' is not a color or theme token");
                        continue;
                    }
                    var amount = (float)Math.Clamp(d.Number("amount", 1), 0, 1);
                    tint[node] = (c, amount);
                    hash.Add(node);
                    hash.Add(c);
                    hash.Add(amount);
                    break;
                }
                case "Clip":
                {
                    var name = d.Text("name");
                    var clip = model.Clips.FirstOrDefault(c => c.Name == name);
                    if (clip is null)
                    {
                        Warn($"<Clip name=\"{name}\">: the model has no such animation");
                        continue;
                    }
                    var value = d.Number("value");
                    var min = d.Number("min", 0);
                    var max = d.Number("max", clip.Duration);
                    var seconds = max > min ? (value - min) / (max - min) * clip.Duration : 0;
                    clips[clip.Name] = (float)seconds;
                    hash.Add(clip.Name);
                    hash.Add(seconds);
                    break;
                }
            }
        }

        // 코드에서 쓴 값이 우선 (같은 노드, 같은 종류)
        foreach (var (k, v) in state.Rotation)
            rotation[k] = v;
        foreach (var (k, v) in state.Offset)
            offset[k] = v;
        foreach (var (k, v) in state.Color)
            tint[k] = v;
        foreach (var (k, v) in state.Clips)
            clips[k] = v;
        var world = new Dictionary<string, (Vector3, Quaternion)>(state.World);

        return (new PoseSnapshot(state.Version, rotation, offset, world, tint, clips), hash.ToHashCode());
    }

    // value × scale + offset, 또는 follow 한 정의의 값 × ratio
    private static double? Amount(IReadOnlyList<DvDefinition> defs, DvDefinition d, int depth)
    {
        if (d.Text("follow") is { Length: > 0 } follow)
        {
            if (depth > 8)
            {
                Warn($"<{d.Tag} node=\"{d.Text("node")}\" follow=\"{follow}\">: follow goes round in a circle");
                return null;
            }
            var source = defs.FirstOrDefault(x => x.Tag == d.Tag && x.Text("node") == follow && !ReferenceEquals(x, d) && !x.Flag("disabled"));
            if (source is null)
            {
                Warn($"<{d.Tag} node=\"{d.Text("node")}\" follow=\"{follow}\">: there is no enabled <{d.Tag} node=\"{follow}\">");
                return null;
            }
            return Amount(defs, source, depth + 1) * d.Number("ratio", 1);
        }
        return d.Number("value") * d.Number("scale", 1) + d.Number("offset", 0);
    }

    private static Vector3? Axis(DvDefinition d)
    {
        var text = d.Text("axis")?.Trim().ToLowerInvariant();
        Vector3? axis = text switch
        {
            "x" => Vector3.UnitX, "-x" => -Vector3.UnitX,
            "y" => Vector3.UnitY, "-y" => -Vector3.UnitY,
            "z" => Vector3.UnitZ, "-z" => -Vector3.UnitZ,
            _ => Vector(text),
        };
        if (axis is null)
            Warn($"<{d.Tag} node=\"{d.Text("node")}\">: axis '{text}' — use x, y, z, -x, -y, -z or three numbers");
        return axis;
    }

    private static Vector3? Vector(string? text)
    {
        var parts = text?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: 3 })
            return null;
        var v = new float[3];
        for (var i = 0; i < 3; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]))
                return null;
        var vec = new Vector3(v[0], v[1], v[2]);
        return vec.LengthSquared() > 1e-12f ? Vector3.Normalize(vec) : null;
    }

    private static void Warn(string message)
    {
        lock (Warned)
            if (Warned.Add(message))
                Trace.TraceWarning("Scene3D: " + message);
    }
}
