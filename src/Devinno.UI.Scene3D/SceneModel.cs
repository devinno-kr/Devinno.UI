using System.Numerics;
using SharpGLTF.Schema2;
using SkiaSharp;

namespace Devinno.UI.Scene3D;

public enum AlphaBlend { Opaque, Mask, Blend }

/// <summary>
/// glTF 파일을 그리기와 무관한 데이터로 읽는다: 노드 트리(기본 자세), 노드별 메시, 재질, 텍스처, 카메라, 애니메이션.
/// 메시 정점은 노드 자기 좌표계 그대로 두어, 노드 자세를 바꾸면 따라 움직이게 한다.
/// </summary>
public sealed class SceneModel
{
    public sealed class Node
    {
        public required int Index { get; init; }
        public required string Name { get; init; }
        public int Parent { get; internal set; } = -1;
        public List<int> Children { get; } = new();
        public Vector3 Translation { get; init; }
        public Quaternion Rotation { get; init; } = Quaternion.Identity;
        public Vector3 Scale { get; init; } = Vector3.One;
        public List<int> Parts { get; } = new();

        public Matrix4x4 RestLocal =>
            Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Translation);
    }

    /// <summary>정점은 위치 3, 법선 3, UV 2 를 이어 붙인 배열이다.</summary>
    public sealed record Part(int Node, float[] Vertices, uint[] Indices, int Material);

    public sealed record MaterialInfo(string Name, Vector4 BaseColor, int Texture, bool DoubleSided, AlphaBlend Alpha, float AlphaCutoff);

    /// <summary>RGBA 8비트, 위 행부터. 색 공간은 sRGB.</summary>
    public sealed record TextureImage(int Width, int Height, byte[] Rgba);

    public sealed record CameraInfo(string Name, int Node, float VerticalFov);

    public sealed record Channel(int Node, Func<float, Vector3>? Translation, Func<float, Quaternion>? Rotation, Func<float, Vector3>? Scale);

    public sealed record Clip(string Name, float Duration, IReadOnlyList<Channel> Channels);

    public List<Node> Nodes { get; } = new();
    public List<Part> Parts { get; } = new();
    public List<MaterialInfo> Materials { get; } = new();
    public List<TextureImage> Textures { get; } = new();
    public List<CameraInfo> Cameras { get; } = new();
    public List<Clip> Clips { get; } = new();
    public Dictionary<string, int> NodeByName { get; } = new(StringComparer.Ordinal);

    /// <summary>부모가 자식보다 먼저 오는 순서.</summary>
    public int[] Order { get; private set; } = Array.Empty<int>();

    public Vector3 Center { get; private set; }
    public float Radius { get; private set; } = 1;
    public int Triangles => Parts.Sum(p => p.Indices.Length / 3);

    public static SceneModel Load(string path)
    {
        var root = ModelRoot.Load(path);
        var model = new SceneModel();

        foreach (var src in root.LogicalNodes)
        {
            Matrix4x4.Decompose(src.LocalMatrix, out var s, out var r, out var t);
            var name = string.IsNullOrEmpty(src.Name) ? $"node{src.LogicalIndex}" : src.Name;
            model.Nodes.Add(new Node { Index = src.LogicalIndex, Name = name, Translation = t, Rotation = r, Scale = s });
            model.NodeByName.TryAdd(name, src.LogicalIndex);
        }

        foreach (var src in root.LogicalNodes)
        {
            var node = model.Nodes[src.LogicalIndex];
            foreach (var child in src.VisualChildren)
            {
                node.Children.Add(child.LogicalIndex);
                model.Nodes[child.LogicalIndex].Parent = src.LogicalIndex;
            }
        }

        var textureIndex = new Dictionary<int, int>();
        foreach (var mat in root.LogicalMaterials)
        {
            var channel = mat.FindChannel("BaseColor");
            var color = channel?.Color ?? Vector4.One;
            var texture = -1;
            if (channel?.Texture is { } tex && tex.PrimaryImage is { } image)
            {
                if (!textureIndex.TryGetValue(image.LogicalIndex, out texture))
                {
                    texture = model.Textures.Count;
                    model.Textures.Add(Decode(image.Content.Content.ToArray()));
                    textureIndex[image.LogicalIndex] = texture;
                }
            }
            var alpha = mat.Alpha switch { AlphaMode.MASK => AlphaBlend.Mask, AlphaMode.BLEND => AlphaBlend.Blend, _ => AlphaBlend.Opaque };
            model.Materials.Add(new MaterialInfo(mat.Name ?? $"material{mat.LogicalIndex}", color, texture, mat.DoubleSided, alpha, mat.AlphaCutoff));
        }
        var defaultMaterial = -1;

        foreach (var src in root.LogicalNodes)
        {
            if (src.Mesh is { } mesh)
            {
                foreach (var prim in mesh.Primitives)
                {
                    if (prim.DrawPrimitiveType != PrimitiveType.TRIANGLES)
                        continue;
                    var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                    if (positions is null)
                        continue;
                    var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                    var uvs = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                    var indices = prim.GetIndices()?.ToArray() ?? Enumerable.Range(0, positions.Count).Select(i => (uint)i).ToArray();

                    var vertices = new float[positions.Count * 8];
                    for (var i = 0; i < positions.Count; i++)
                    {
                        positions[i].CopyTo(vertices, i * 8);
                        (normals is null ? Vector3.UnitY : normals[i]).CopyTo(vertices, i * 8 + 3);
                        if (uvs is not null)
                            uvs[i].CopyTo(vertices, i * 8 + 6);
                    }

                    var material = prim.Material?.LogicalIndex ?? -1;
                    if (material < 0)
                    {
                        if (defaultMaterial < 0)
                        {
                            defaultMaterial = model.Materials.Count;
                            model.Materials.Add(new MaterialInfo("default", Vector4.One, -1, false, AlphaBlend.Opaque, 0.5f));
                        }
                        material = defaultMaterial;
                    }
                    model.Nodes[src.LogicalIndex].Parts.Add(model.Parts.Count);
                    model.Parts.Add(new Part(src.LogicalIndex, vertices, indices, material));
                }
            }

            if (src.Camera?.Settings is CameraPerspective perspective)
                model.Cameras.Add(new CameraInfo(src.Name ?? src.Camera.Name ?? $"camera{src.LogicalIndex}", src.LogicalIndex, perspective.VerticalFOV));
        }

        foreach (var anim in root.LogicalAnimations)
        {
            var channels = new List<Channel>();
            foreach (var ch in anim.Channels)
            {
                var target = ch.TargetNode.LogicalIndex;
                switch (ch.TargetNodePath)
                {
                    case PropertyPath.translation:
                        var t = ch.GetTranslationSampler().CreateCurveSampler();
                        channels.Add(new Channel(target, time => t.GetPoint(time), null, null));
                        break;
                    case PropertyPath.rotation:
                        var r = ch.GetRotationSampler().CreateCurveSampler();
                        channels.Add(new Channel(target, null, time => r.GetPoint(time), null));
                        break;
                    case PropertyPath.scale:
                        var s = ch.GetScaleSampler().CreateCurveSampler();
                        channels.Add(new Channel(target, null, null, time => s.GetPoint(time)));
                        break;
                }
            }
            model.Clips.Add(new Clip(anim.Name ?? $"clip{anim.LogicalIndex}", anim.Duration, channels));
        }

        model.Finish();
        return model;
    }

    private void Finish()
    {
        var order = new List<int>();
        void Visit(int i)
        {
            order.Add(i);
            foreach (var c in Nodes[i].Children)
                Visit(c);
        }
        foreach (var n in Nodes.Where(n => n.Parent < 0))
            Visit(n.Index);
        Order = order.ToArray();

        // 기본 자세의 경계 (메시가 있는 노드만)
        var world = new Matrix4x4[Nodes.Count];
        foreach (var i in Order)
            world[i] = Nodes[i].Parent < 0 ? Nodes[i].RestLocal : Nodes[i].RestLocal * world[Nodes[i].Parent];
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var part in Parts)
        {
            for (var i = 0; i < part.Vertices.Length; i += 8)
            {
                var p = Vector3.Transform(new Vector3(part.Vertices[i], part.Vertices[i + 1], part.Vertices[i + 2]), world[part.Node]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        if (Parts.Count > 0)
        {
            Center = (min + max) / 2;
            Radius = MathF.Max(Vector3.Distance(min, max) / 2, 0.01f);
        }
    }

    private static TextureImage Decode(byte[] encoded)
    {
        using var image = SKImage.FromEncodedData(encoded) ?? throw new InvalidDataException("A texture image could not be decoded.");
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var pixels = new byte[info.BytesSize];
        unsafe
        {
            fixed (byte* p = pixels)
                image.ReadPixels(info, (IntPtr)p, info.RowBytes, 0, 0);
        }
        return new TextureImage(image.Width, image.Height, pixels);
    }
}
