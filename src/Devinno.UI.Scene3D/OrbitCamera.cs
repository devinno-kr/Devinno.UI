using System.Numerics;

namespace Devinno.UI.Scene3D;

/// <summary>대상 주위를 도는 카메라. glTF 카메라 시점으로 짧게 옮겨 갈 수 있다.</summary>
internal sealed class OrbitCamera
{
    private const float TransitionMs = 300;

    private (Vector3 Target, float Yaw, float Pitch, float Distance, float Fov) _from, _to;
    private long? _start;
    private bool _pending;

    public Vector3 Target { get; private set; }
    public float Yaw { get; private set; } = 35;
    public float Pitch { get; private set; } = 25;
    public float Distance { get; private set; } = 5;
    public float Fov { get; private set; } = 40;

    /// <summary>바뀔 때마다 늘어난다 (다시 그릴지 판단용).</summary>
    public int Version { get; private set; }

    public bool Animating => _pending || _start is not null;

    public Vector3 Eye
    {
        get
        {
            float y = Yaw * MathF.PI / 180, p = Pitch * MathF.PI / 180;
            return Target + new Vector3(MathF.Cos(p) * MathF.Sin(y), MathF.Sin(p), MathF.Cos(p) * MathF.Cos(y)) * Distance;
        }
    }

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitY);

    public Matrix4x4 Projection(float aspect) =>
        Matrix4x4.CreatePerspectiveFieldOfView(Fov * MathF.PI / 180, aspect, Distance / 200, Distance * 20);

    /// <summary>모델 전체가 보이는 기본 시점.</summary>
    public (Vector3, float, float, float, float) Home(SceneModel model)
    {
        const float fov = 40;
        return (model.Center, 35, 25, model.Radius / MathF.Sin(fov * MathF.PI / 360) * 1.05f, fov);
    }

    /// <summary>glTF 카메라 이름의 시점. 없으면 기본 시점.</summary>
    public (Vector3, float, float, float, float) Preset(SceneModel model, string? name)
    {
        var cam = model.Cameras.FirstOrDefault(c => c.Name == name);
        if (cam is null)
            return Home(model);

        var world = ScenePose.Compute(model, PoseSnapshot.Empty)[cam.Node];
        var eye = new Vector3(world.M41, world.M42, world.M43);
        var forward = Vector3.Normalize(-new Vector3(world.M31, world.M32, world.M33)); // 카메라는 자기 -Z 를 본다
        var distance = MathF.Max(Vector3.Dot(model.Center - eye, forward), model.Radius * 0.3f);
        var target = eye + forward * distance;
        var dir = (eye - target) / distance;
        var pitch = Math.Clamp(MathF.Asin(Math.Clamp(dir.Y, -1, 1)) * 180 / MathF.PI, -89, 89);
        var yaw = MathF.Atan2(dir.X, dir.Z) * 180 / MathF.PI;
        return (target, yaw, pitch, distance, cam.VerticalFov * 180 / MathF.PI);
    }

    public void Set((Vector3 Target, float Yaw, float Pitch, float Distance, float Fov) v)
    {
        (Target, Yaw, Pitch, Distance, Fov) = v;
        _start = null;
        _pending = false;
        Version++;
    }

    /// <summary>다음 프레임부터 짧게 옮겨 간다.</summary>
    public void MoveTo((Vector3 Target, float Yaw, float Pitch, float Distance, float Fov) v)
    {
        _from = (Target, Yaw, Pitch, Distance, Fov);
        _to = v;
        // 가까운 쪽으로 돌도록
        var dy = (_to.Yaw - _from.Yaw) % 360;
        if (dy > 180)
            dy -= 360;
        if (dy < -180)
            dy += 360;
        _to.Yaw = _from.Yaw + dy;
        _pending = true;
        _start = null;
        Version++;
    }

    public void Update(long nowMs)
    {
        if (_pending)
        {
            _start = nowMs;
            _pending = false;
        }
        if (_start is not { } start)
            return;
        var t = Math.Clamp((nowMs - start) / TransitionMs, 0, 1);
        var e = t * t * (3 - 2 * t);
        Target = Vector3.Lerp(_from.Target, _to.Target, e);
        Yaw = _from.Yaw + (_to.Yaw - _from.Yaw) * e;
        Pitch = _from.Pitch + (_to.Pitch - _from.Pitch) * e;
        Distance = _from.Distance + (_to.Distance - _from.Distance) * e;
        Fov = _from.Fov + (_to.Fov - _from.Fov) * e;
        if (t >= 1)
            _start = null;
        Version++;
    }

    public void Orbit(float dx, float dy)
    {
        _start = null;
        _pending = false;
        Yaw -= dx * 0.4f;
        Pitch = Math.Clamp(Pitch + dy * 0.4f, -89, 89);
        Version++;
    }

    public void Zoom(float notches)
    {
        _start = null;
        _pending = false;
        Distance = Math.Clamp(Distance * MathF.Pow(0.88f, notches), 0.05f, 1000);
        Version++;
    }
}
