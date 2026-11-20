using System.Diagnostics;
using System.Numerics;

namespace Devinno.UI.Scene3D;

/// <summary>기본 자세 + 애니메이션 + 덮어쓴 값으로 노드별 월드 행렬을 계산한다. GL 을 쓰지 않는다.</summary>
public static class ScenePose
{
    private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);

    public static Matrix4x4[] Compute(SceneModel model, PoseSnapshot pose)
    {
        var count = model.Nodes.Count;
        var t = new Vector3[count];
        var r = new Quaternion[count];
        var s = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            t[i] = model.Nodes[i].Translation;
            r[i] = model.Nodes[i].Rotation;
            s[i] = model.Nodes[i].Scale;
        }

        // 애니메이션 클립: 기본 자세를 그 시점의 값으로 바꾼다
        foreach (var (name, time) in pose.Clips)
        {
            var clip = model.Clips.FirstOrDefault(c => c.Name == name);
            if (clip is null)
            {
                Warn($"clip '{name}'");
                continue;
            }
            var at = Math.Clamp(time, 0, clip.Duration);
            foreach (var ch in clip.Channels)
            {
                if (ch.Translation is { } ft)
                    t[ch.Node] = ft(at);
                if (ch.Rotation is { } fr)
                    r[ch.Node] = Quaternion.Normalize(fr(at));
                if (ch.Scale is { } fs)
                    s[ch.Node] = fs(at);
            }
        }

        // 덮어쓴 이동·회전은 노드 자기 축 기준으로 더한다 (이동은 덧붙인 회전 전의 축 기준)
        foreach (var (name, offset) in pose.Offset)
            if (Index(model, name) is { } i)
                t[i] += Vector3.Transform(offset, r[i]);
        foreach (var (name, delta) in pose.Rotation)
            if (Index(model, name) is { } i)
                r[i] = r[i] * delta; // delta 를 먼저(자기 축), 그다음 기본 회전

        var world = new Matrix4x4[count];
        var fixedWorld = new Dictionary<int, Matrix4x4>();
        foreach (var (name, p) in pose.World)
            if (Index(model, name) is { } i)
                fixedWorld[i] = Matrix4x4.CreateFromQuaternion(p.Rotation) * Matrix4x4.CreateTranslation(p.Position);

        foreach (var i in model.Order)
        {
            if (fixedWorld.TryGetValue(i, out var w))
            {
                world[i] = Matrix4x4.CreateScale(s[i]) * w;
                continue;
            }
            var local = Matrix4x4.CreateScale(s[i]) * Matrix4x4.CreateFromQuaternion(r[i]) * Matrix4x4.CreateTranslation(t[i]);
            var parent = model.Nodes[i].Parent;
            world[i] = parent < 0 ? local : local * world[parent];
        }
        return world;
    }

    private static int? Index(SceneModel model, string name)
    {
        if (model.NodeByName.TryGetValue(name, out var i))
            return i;
        Warn($"node '{name}'");
        return null;
    }

    private static void Warn(string what)
    {
        lock (Warned)
        {
            if (Warned.Add(what))
                Trace.TraceWarning($"Scene3D: {what} is not in the model.");
        }
    }
}
