using System.Numerics;
using SkiaSharp;

namespace Devinno.UI.Scene3D;

/// <summary>
/// 3D 뷰에 보낼 값 저장소. 통신 스레드, 시뮬레이션 스레드, UI 어디서 써도 된다.
/// 값을 쓴 뒤 <c>App.Surface.Repaint()</c> 를 부르면 화면을 다시 만들지 않고 다시 그린다.
/// </summary>
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

    /// <summary>클릭으로 선택된 부품 이름 (UI 스레드에서 바뀜).</summary>
    public string? Selected { get; private set; }

    /// <summary>부품을 클릭하면 UI 스레드에서 발생한다.</summary>
    public event Action<string>? PartClicked;

    /// <summary>노드를 기본 자세에서 자기 축 기준으로 돌린다. 같은 노드에 다시 쓰면 바꿔 쓴다.</summary>
    public void Rotate(string node, Vector3 axis, double degrees) =>
        Write(() => _rotation[node] = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), (float)(degrees * Math.PI / 180)));

    /// <summary>노드를 기본 위치에서 자기 축 방향으로 옮긴다(미터).</summary>
    public void Move(string node, Vector3 axis, double meters) =>
        Write(() => _offset[node] = Vector3.Normalize(axis) * (float)meters);

    /// <summary>노드의 월드 자세를 통째로 정한다 (시뮬레이션 결과용). 자식은 이 자세를 따라간다.</summary>
    public void SetWorldPose(string node, Vector3 position, Quaternion rotation) =>
        Write(() => _world[node] = (position, rotation));

    /// <summary>노드와 그 아래 부품 색을 amount(0~1)만큼 바꾼다. null 이면 원래 색.</summary>
    public void Color(string node, SKColor? color, float amount = 1) =>
        Write(() =>
        {
            if (color is { } c)
                _color[node] = (c, Math.Clamp(amount, 0, 1));
            else
                _color.Remove(node);
        });

    /// <summary>glTF 애니메이션 클립을 그 시점(초)의 자세로 고정한다.</summary>
    public void SetClipTime(string clip, double seconds) => Write(() => _clip[clip] = (float)seconds);

    public void ClearClip(string clip) => Write(() => _clip.Remove(clip));

    /// <summary>여러 값을 한 번에 바꾼다. 그리는 쪽은 바뀌는 도중의 값을 보지 않는다 (맞물린 부품용).</summary>
    public void Batch(Action<SceneState> change)
    {
        lock (_gate)
            change(this);
    }

    /// <summary>노드에 쓴 회전·이동·월드 자세·색을 지운다.</summary>
    public void Reset(string node) =>
        Write(() =>
        {
            _rotation.Remove(node);
            _offset.Remove(node);
            _world.Remove(node);
            _color.Remove(node);
        });

    public PoseSnapshot Snapshot()
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

/// <summary>그릴 때 쓰는 한 벌의 값. 만든 뒤에는 바뀌지 않는다.</summary>
public sealed record PoseSnapshot(
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
