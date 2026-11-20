using System.Numerics;
using SkiaSharp;

namespace Devinno.UI.Scene3D;

/// <summary>
/// Values for a <see cref="SceneView"/> written from code — a communication thread, a simulation or the UI. Bind it to the view's <c>state</c> attribute.
/// </summary>
/// <remarks>
/// <para><b>Threads</b>: every method can be called from any thread. After writing, call <c>Repaint()</c> on the app's surface: the view draws again
/// without rebuilding the screen. Several writes before the next frame are drawn once.</para>
/// <para><b>Priority</b>: a value written here replaces the markup value of the same kind (rotation, offset, color, clip) on the same node until <see cref="Reset"/>;
/// other kinds on that node still follow the markup.</para>
/// <para><b>Axes</b>: rotations and moves are in the node's own axes, added to the pose the model was exported with.</para>
/// </remarks>
public sealed class SceneState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Quaternion> _rotation = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector3> _offset = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Vector3 Position, Quaternion Rotation)> _world = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (SKColor Color, float Amount)> _color = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _clip = new(StringComparer.Ordinal);
    private int _version;
    private PoseSnapshot? _snapshot;

    /// <summary>Gets the name of the part last selected by a click (the click unit, see the view's <c>pick</c>). Set on the UI thread.</summary>
    public string? Selected { get; private set; }

    /// <summary>Occurs on the UI thread when a part is clicked, with the name of its click unit.</summary>
    public event Action<string>? PartClicked;

    /// <summary>Turns a node from its exported pose about its own axis. A new call for the same node replaces the previous rotation.</summary>
    /// <param name="node">glTF node name.</param>
    /// <param name="axis">Axis in the node's own axes (normalized here).</param>
    /// <param name="degrees">Angle in degrees, right-handed.</param>
    public void Rotate(string node, Vector3 axis, double degrees) =>
        Write(() => _rotation[node] = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), (float)(degrees * Math.PI / 180)));

    /// <summary>Moves a node from its exported position along its own axis. A new call for the same node replaces the previous offset.</summary>
    /// <param name="node">glTF node name.</param>
    /// <param name="axis">Direction in the node's own axes (normalized here).</param>
    /// <param name="meters">Distance in meters.</param>
    public void Move(string node, Vector3 axis, double meters) =>
        Write(() => _offset[node] = Vector3.Normalize(axis) * (float)meters);

    /// <summary>
    /// Sets the world position and rotation of a node, ignoring its parent — for poses computed by a physics simulation. Its children follow it.
    /// The node keeps its exported scale.
    /// </summary>
    /// <param name="node">glTF node name.</param>
    /// <param name="position">World position in meters (glTF axes: +Y up).</param>
    /// <param name="rotation">World rotation.</param>
    public void SetWorldPose(string node, Vector3 position, Quaternion rotation) =>
        Write(() => _world[node] = (position, rotation));

    /// <summary>Mixes the color of a node and its children toward <paramref name="color"/>. <see langword="null"/> removes the color written here.</summary>
    /// <param name="node">glTF node name.</param>
    /// <param name="color">The color, or <see langword="null"/>.</param>
    /// <param name="amount">0–1; at 1 the part is that color only.</param>
    public void Color(string node, SKColor? color, float amount = 1) =>
        Write(() =>
        {
            if (color is { } c)
                _color[node] = (c, Math.Clamp(amount, 0, 1));
            else
                _color.Remove(node);
        });

    /// <summary>Poses the model at a time of a glTF animation clip.</summary>
    /// <param name="clip">Clip name (the Blender action name).</param>
    /// <param name="seconds">Time from the start of the clip, clamped to its length.</param>
    public void SetClipTime(string clip, double seconds) => Write(() => _clip[clip] = (float)seconds);

    /// <summary>Removes the clip time written here.</summary>
    /// <param name="clip">Clip name.</param>
    public void ClearClip(string clip) => Write(() => _clip.Remove(clip));

    /// <summary>Writes several values as one change: the view never draws a frame with only some of them (parts that must move together).</summary>
    /// <param name="change">Calls on this state.</param>
    public void Batch(Action<SceneState> change)
    {
        lock (_gate)
            change(this);
    }

    /// <summary>Removes the rotation, offset, world pose and color written here for a node; the markup values apply again.</summary>
    /// <param name="node">glTF node name.</param>
    public void Reset(string node) =>
        Write(() =>
        {
            _rotation.Remove(node);
            _offset.Remove(node);
            _world.Remove(node);
            _color.Remove(node);
        });

    internal PoseSnapshot Snapshot()
    {
        lock (_gate)
        {
            return _snapshot ??= new PoseSnapshot(
                _version,
                new Dictionary<string, Quaternion>(_rotation),
                new Dictionary<string, Vector3>(_offset),
                new Dictionary<string, (Vector3, Quaternion)>(_world),
                new Dictionary<string, (SKColor, float)>(_color),
                new Dictionary<string, float>(_clip));
        }
    }

    internal void NotifyClicked(string node)
    {
        Selected = node;
        PartClicked?.Invoke(node);
    }

    private void Write(Action change)
    {
        lock (_gate)
        {
            change();
            _version++;
            _snapshot = null;
        }
    }
}

/// <summary>One set of values used for a draw. Never changes after it is made.</summary>
internal sealed record PoseSnapshot(
    int Version,
    IReadOnlyDictionary<string, Quaternion> Rotation,
    IReadOnlyDictionary<string, Vector3> Offset,
    IReadOnlyDictionary<string, (Vector3 Position, Quaternion Rotation)> World,
    IReadOnlyDictionary<string, (SKColor Color, float Amount)> Color,
    IReadOnlyDictionary<string, float> Clips)
{
    public static readonly PoseSnapshot Empty = new(-1,
        new Dictionary<string, Quaternion>(), new Dictionary<string, Vector3>(),
        new Dictionary<string, (Vector3, Quaternion)>(), new Dictionary<string, (SKColor, float)>(), new Dictionary<string, float>());
}
