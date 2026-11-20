using System.Diagnostics;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;

namespace Devinno.UI.Scene3D;

/// <summary>
/// 장면을 OpenGL 3.3 으로 그린다. 길은 두 가지이고 뷰마다 캔버스를 보고 고른다(<see cref="SceneTarget"/>).
/// <list type="bullet">
/// <item>GPU 캔버스(호스트가 GPU 모드, 데스크톱 GL): 호스트 컨텍스트에서 뷰의 텍스처에 그리고 Skia 이미지로 감싼다. 픽셀을 읽지 않는다.
/// 직접 GL 을 부른 뒤에는 <see cref="GRContext.ResetContext(GRGlBackendState)"/> 로 Skia 에 상태가 바뀌었음을 알린다.</item>
/// <item>그 밖(CPU 캔버스, 창 없이 실행): 숨은 GLFW 창의 컨텍스트에서 위아래를 뒤집어 그리고, 픽셀을 뷰의 비트맵으로 바로 읽는다(복사 없음).</item>
/// </list>
/// GL 자원(셰이더, 모델, 멀티샘플 대상)은 컨텍스트마다 따로 둔다. GLFW 규칙대로 UI(메인) 스레드에서만 쓴다.
/// </summary>
internal sealed unsafe class SceneRenderer
{
    // 멀티샘플: 보통 4배, 그리는 픽셀이 많으면 2배 (메모리의 대부분이 멀티샘플 대상이다)
    private const int Samples = 4, LargeSamples = 2, LargePixels = 1600 * 1000;

    private const string VertexShader = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aUv;
        uniform mat4 uModel;
        uniform mat4 uViewProj;
        out vec3 vWorld;
        out vec3 vNormal;
        out vec2 vUv;
        void main()
        {
            vec4 world = uModel * vec4(aPos, 1.0);
            vWorld = world.xyz;
            vNormal = mat3(uModel) * aNormal;
            vUv = aUv;
            gl_Position = uViewProj * world;
        }
        """;

    private const string ColorShader = """
        #version 330 core
        in vec3 vWorld;
        in vec3 vNormal;
        in vec2 vUv;
        uniform vec4 uBase;      // linear
        uniform sampler2D uTex;  // sRGB texture, reads as linear
        uniform int uHasTex;
        uniform vec4 uTint;      // rgb linear, a = amount
        uniform int uAlphaMode;  // 0 opaque, 1 mask, 2 blend
        uniform float uCutoff;
        uniform vec3 uEye;
        uniform vec3 uKey;
        uniform vec3 uFill;
        out vec4 fragColor;
        void main()
        {
            vec4 base = uBase;
            if (uHasTex == 1)
                base *= texture(uTex, vUv);
            if (uAlphaMode == 1 && base.a < uCutoff)
                discard;
            vec3 albedo = mix(base.rgb, uTint.rgb, uTint.a);
            vec3 n = normalize(vNormal);
            if (!gl_FrontFacing)
                n = -n;
            vec3 v = normalize(uEye - vWorld);
            float key = max(dot(n, uKey), 0.0);
            float fill = max(dot(n, uFill), 0.0);
            float spec = pow(max(dot(n, normalize(uKey + v)), 0.0), 40.0) * 0.18;
            vec3 c = albedo * (0.16 + 0.80 * key + 0.25 * fill) + vec3(spec);
            c = pow(clamp(c, 0.0, 1.0), vec3(1.0 / 2.2));
            float a = uAlphaMode == 2 ? base.a : 1.0;
            fragColor = vec4(c * a, a);
        }
        """;

    private const string PickShader = """
        #version 330 core
        uniform vec4 uId;
        out vec4 fragColor;
        void main() { fragColor = uId; }
        """;

    private sealed record GpuPart(int Vao, int Count, int Node, SceneModel.MaterialInfo Material, int Texture);

    /// <summary>한 GL 컨텍스트 안의 자원. 멀티샘플 대상은 그 컨텍스트의 뷰들이 함께 쓴다(그린 즉시 뷰의 대상으로 옮기므로).</summary>
    private sealed class GlResources
    {
        public int ColorProgram, PickProgram;
        public readonly Dictionary<SceneModel, List<GpuPart>> Uploaded = new();
        public int MsFbo, MsColor, MsDepth, MsWidth, MsHeight, MsSamples;
        public int PickFbo, PickColor, PickDepth;
        public bool Unusable; // 호스트 컨텍스트에서 셰이더를 만들지 못함 → 숨은 컨텍스트로 그린다
    }

    private static SceneRenderer? _shared;

    /// <summary>모든 3D 뷰가 함께 쓰는 렌더러. 처음 쓸 때 만든다.</summary>
    public static SceneRenderer Shared => _shared ??= new SceneRenderer();

    private readonly Dictionary<nint, GlResources> _contexts = new();
    private Window* _hidden;

    /// <summary>측정용: 켜면 단계 사이에 GL.Finish 를 넣어 단계별 시간을 <see cref="LastStats"/> 에 남긴다(느려진다).</summary>
    internal static bool Profile { get; set; }

    /// <summary>측정용: 마지막 그리기의 단계별 시간(ms). Profile 이 켜져 있을 때만 채운다. GPU 길은 Read·Convert 가 0.</summary>
    internal static (double Draw, double Read, double Convert) LastStats { get; private set; }

    /// <summary>측정용: 모든 컨텍스트의 멀티샘플·피킹 대상 바이트 수(뷰의 대상은 <see cref="SceneTarget.GpuBytes"/>).</summary>
    internal long SharedBytes(SceneTarget target) =>
        target.Context != null && _contexts.TryGetValue((nint)target.Context, out var g) ? (long)g.MsWidth * g.MsHeight * g.MsSamples * 8 + 8 : 0;

    private SceneRenderer()
    {
    }

    /// <summary>
    /// 장면을 뷰의 대상에 그린다. 캔버스가 GPU(데스크톱 GL)면 호스트 컨텍스트에서, 아니면 숨은 컨텍스트에서 그리고 픽셀을 읽는다.
    /// 그 뒤 <see cref="SceneTarget.Draw"/> 로 캔버스에 놓는다.
    /// </summary>
    /// <param name="tint">노드별 색 덮기(rgb 선형, a = 양). 길이는 노드 수.</param>
    public void Render(SceneTarget target, SKCanvas canvas, SceneModel model, Matrix4x4[] world, Vector4[] tint,
        Matrix4x4 view, Matrix4x4 proj, Vector3 eye, int w, int h)
    {
        var previous = GLFW.GetCurrentContext();
        var gr = HostGr(canvas, previous);
        var context = gr is not null ? previous : Hidden();
        if (context != previous)
            GLFW.MakeContextCurrent(context);
        try
        {
            var g = Resources(context);
            target.Prepare(context, gr, w, h);
            var clock = Profile ? Stopwatch.StartNew() : null;

            ResetState();
            var parts = Upload(g, model);
            EnsureMultisample(g, w, h);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, g.MsFbo);
            GL.Viewport(0, 0, w, h);
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);

            GL.UseProgram(g.ColorProgram);
            // CPU 길: 위아래를 뒤집어 그려서 GL 의 아래 줄부터 순서가 곧 이미지의 위 줄부터가 되게 한다(읽은 뒤 뒤집거나 뒤집어 그리지 않는다).
            // 뒤집으면 삼각형 감김 방향도 바뀌므로 앞면을 시계 방향으로 본다
            var flip = target.Bitmap is not null;
            var viewProj = flip ? view * proj * Matrix4x4.CreateScale(1, -1, 1) : view * proj;
            GL.FrontFace(flip ? FrontFaceDirection.Cw : FrontFaceDirection.Ccw);
            SetMatrix(g.ColorProgram, "uViewProj", viewProj);
            // 조명은 카메라를 따라간다: 앞 위에서 주광, 반대편에서 보조광
            Matrix4x4.Invert(view, out var camera);
            var back = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, camera));
            var right = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, camera));
            var up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, camera));
            var key = Vector3.Normalize(back + up * 0.9f + right * 0.4f);
            var fill = Vector3.Normalize(-back + up * 0.3f - right * 0.6f);
            GL.Uniform3(Loc(g.ColorProgram, "uEye"), eye.X, eye.Y, eye.Z);
            GL.Uniform3(Loc(g.ColorProgram, "uKey"), key.X, key.Y, key.Z);
            GL.Uniform3(Loc(g.ColorProgram, "uFill"), fill.X, fill.Y, fill.Z);
            GL.Uniform1(Loc(g.ColorProgram, "uTex"), 0);

            // 불투명·마스크 먼저, 반투명은 나중에 (정렬하지 않음)
            GL.Disable(EnableCap.Blend);
            foreach (var part in parts)
                if (part.Material.Alpha != AlphaBlend.Blend)
                    DrawColor(g, part, world, tint);
            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false);
            foreach (var part in parts)
                if (part.Material.Alpha == AlphaBlend.Blend)
                    DrawColor(g, part, world, tint);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.BindVertexArray(0);

            // 멀티샘플 → 뷰의 대상 (해상도 맞추기)
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, g.MsFbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, target.Fbo);
            GL.BlitFramebuffer(0, 0, w, h, 0, 0, w, h, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            if (clock is not null)
                GL.Finish();
            var draw = clock?.Elapsed.TotalMilliseconds ?? 0;

            if (target.Bitmap is { } bitmap)
            {
                // CPU 캔버스: 비트맵으로 바로 읽는다 (뒤집어 그렸으므로 위 줄부터)
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, target.Fbo);
                GL.ReadPixels(0, 0, w, h, PixelFormat.Rgba, PixelType.UnsignedByte, bitmap.GetPixels());
                bitmap.NotifyPixelsChanged();
                target.PixelsChanged();
            }
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            if (clock is not null)
                LastStats = (draw, clock.Elapsed.TotalMilliseconds - draw, 0);
        }
        finally
        {
            gr?.ResetContext(GRGlBackendState.All);
            if (context != previous)
                GLFW.MakeContextCurrent(previous);
        }
    }

    /// <summary>뷰 픽셀 (x, y)(위에서부터)에 보이는 노드 번호. 없으면 -1. 마지막으로 그린 컨텍스트에서 1×1 대상에 그려 한 픽셀을 읽는다.</summary>
    public int Pick(SceneTarget target, SceneModel model, Matrix4x4[] world, Matrix4x4 view, Matrix4x4 proj, int x, int y)
    {
        int w = target.Width, h = target.Height;
        if (target.Context == null || x < 0 || y < 0 || x >= w || y >= h)
            return -1;
        var previous = GLFW.GetCurrentContext();
        var context = target.Context;
        if (context != previous)
            GLFW.MakeContextCurrent(context);
        try
        {
            var g = Resources(context);
            ResetState();
            var parts = Upload(g, model);
            EnsurePick(g);

            // 그 픽셀만 1×1 대상을 채우도록 투영을 키운다: NDC 에서 픽셀 중심으로 옮기고 w·h 배 확대
            var glY = h - 1 - y;
            float cx = 2f * (x + 0.5f) / w - 1, cy = 2f * (glY + 0.5f) / h - 1;
            var zoom = Matrix4x4.Identity;
            zoom.M11 = w;
            zoom.M22 = h;
            zoom.M41 = -cx * w;
            zoom.M42 = -cy * h;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, g.PickFbo);
            GL.Viewport(0, 0, 1, 1);
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.UseProgram(g.PickProgram);
            SetMatrix(g.PickProgram, "uViewProj", view * proj * zoom);
            var idLoc = Loc(g.PickProgram, "uId");
            foreach (var part in parts)
            {
                var id = part.Node + 1;
                GL.Uniform4(idLoc, (id & 0xFF) / 255f, ((id >> 8) & 0xFF) / 255f, ((id >> 16) & 0xFF) / 255f, 1f);
                SetMatrix(g.PickProgram, "uModel", world[part.Node]);
                Cull(part.Material.DoubleSided);
                GL.BindVertexArray(part.Vao);
                GL.DrawElements(PrimitiveType.Triangles, part.Count, DrawElementsType.UnsignedInt, 0);
            }
            GL.BindVertexArray(0);

            var pixel = 0u;
            GL.ReadPixels(0, 0, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)(&pixel));
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            return (int)(pixel & 0xFFFFFF) - 1;
        }
        finally
        {
            target.Gr?.ResetContext(GRGlBackendState.All);
            if (context != previous)
                GLFW.MakeContextCurrent(previous);
        }
    }

    /// <summary>뷰의 대상 GL 자원을 그 컨텍스트에서 지운다.</summary>
    internal static void Delete(Window* context, int fbo, int texture, int renderbuffer)
    {
        var previous = GLFW.GetCurrentContext();
        if (context != previous)
            GLFW.MakeContextCurrent(context);
        try
        {
            GL.DeleteFramebuffer(fbo);
            if (texture != 0)
                GL.DeleteTexture(texture);
            if (renderbuffer != 0)
                GL.DeleteRenderbuffer(renderbuffer);
        }
        finally
        {
            if (context != previous)
                GLFW.MakeContextCurrent(previous);
        }
    }

    // GPU 길을 쓸 수 있으면 호스트의 GRContext: 캔버스가 GL GRContext 이고, GL 컨텍스트가 현재이며, 그 컨텍스트에서 셰이더를 만들 수 있을 때
    private GRContext? HostGr(SKCanvas canvas, Window* current)
    {
        if (canvas.Context is not GRContext { Backend: GRBackend.OpenGL } gr || current == null)
            return null;
        if (!_contexts.TryGetValue((nint)current, out var g))
        {
            g = new GlResources();
            var version = GL.GetString(StringName.Version) ?? "";
            if (version.StartsWith("OpenGL ES", StringComparison.Ordinal))
            {
                g.Unusable = true;
                Trace.TraceWarning("Scene3D: the host uses OpenGL ES; the 3D view draws in its own OpenGL 3.3 context and reads the pixels back.");
            }
            else
            {
                try
                {
                    Create(g);
                }
                catch (Exception e)
                {
                    g.Unusable = true;
                    Trace.TraceWarning($"Scene3D: the host GL context cannot run the 3D shaders ({e.Message}); the 3D view draws in its own context and reads the pixels back.");
                }
                gr.ResetContext(GRGlBackendState.All);
            }
            _contexts[(nint)current] = g;
        }
        return g.Unusable ? null : gr;
    }

    private Window* Hidden()
    {
        if (_hidden != null)
            return _hidden;
        GLFWProvider.EnsureInitialized();
        var previous = GLFW.GetCurrentContext();
        GLFW.DefaultWindowHints();
        GLFW.WindowHint(WindowHintBool.Visible, false);
        GLFW.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        GLFW.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        GLFW.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
        GLFW.WindowHint(WindowHintBool.OpenGLForwardCompat, true);
        var window = GLFW.CreateWindow(16, 16, "scene3d offscreen", null, null);
        GLFW.DefaultWindowHints();
        if (window == null)
            throw new InvalidOperationException("OpenGL 3.3 is required for the 3D view.");

        GLFW.MakeContextCurrent(window);
        try
        {
            // 화면 창의 컨텍스트가 이미 있으면 OpenTK 가 함수 주소를 불러 둔 상태다(같은 드라이버면 주소가 같다).
            // gles 호스트의 주소를 데스크톱 GL 주소로 덮지 않도록, 컨텍스트가 없을 때만 불러온다.
            if (previous == null)
                GL.LoadBindings(new GLFWBindingsContext());
            var g = new GlResources();
            Create(g);
            _contexts[(nint)window] = g;
        }
        finally
        {
            GLFW.MakeContextCurrent(previous);
        }
        _hidden = window;
        return window;
    }

    private GlResources Resources(Window* context) => _contexts[(nint)context];

    private static void Create(GlResources g)
    {
        g.ColorProgram = Program(VertexShader, ColorShader);
        g.PickProgram = Program(VertexShader, PickShader);
    }

    // 호스트(Skia)가 남긴 상태 중 우리 그리기에 영향을 주는 것을 정해 둔다
    private static void ResetState()
    {
        GL.Disable(EnableCap.ScissorTest);
        GL.Disable(EnableCap.StencilTest);
        GL.Disable(EnableCap.PolygonOffsetFill);
        GL.Disable(EnableCap.SampleAlphaToCoverage);
        GL.Disable(EnableCap.FramebufferSrgb);
        GL.Disable(EnableCap.Dither);
        GL.ColorMask(true, true, true, true);
        GL.DepthMask(true); // Clear 가 깊이를 지우려면 먼저 켜져 있어야 한다 (Skia 는 끈 채로 둘 수 있다)
        GL.ClearDepth(1.0);
        GL.DepthRange(0.0, 1.0);
        GL.Disable(EnableCap.RasterizerDiscard);
        GL.FrontFace(FrontFaceDirection.Ccw);
        GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
        GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
        GL.PixelStore(PixelStoreParameter.PackRowLength, 0);
        GL.PixelStore(PixelStoreParameter.PackSkipPixels, 0);
        GL.PixelStore(PixelStoreParameter.PackSkipRows, 0);
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
        GL.PixelStore(PixelStoreParameter.UnpackSkipPixels, 0);
        GL.PixelStore(PixelStoreParameter.UnpackSkipRows, 0);
        GL.ActiveTexture(TextureUnit.Texture0);
    }

    private static void DrawColor(GlResources g, GpuPart part, Matrix4x4[] world, Vector4[] tint)
    {
        var m = part.Material;
        var p = g.ColorProgram;
        SetMatrix(p, "uModel", world[part.Node]);
        GL.Uniform4(Loc(p, "uBase"), m.BaseColor.X, m.BaseColor.Y, m.BaseColor.Z, m.BaseColor.W);
        var t = tint[part.Node];
        GL.Uniform4(Loc(p, "uTint"), t.X, t.Y, t.Z, t.W);
        GL.Uniform1(Loc(p, "uAlphaMode"), (int)m.Alpha);
        GL.Uniform1(Loc(p, "uCutoff"), m.AlphaCutoff);
        GL.Uniform1(Loc(p, "uHasTex"), part.Texture != 0 ? 1 : 0);
        GL.BindTexture(TextureTarget.Texture2D, part.Texture);
        Cull(m.DoubleSided);
        GL.BindVertexArray(part.Vao);
        GL.DrawElements(PrimitiveType.Triangles, part.Count, DrawElementsType.UnsignedInt, 0);
    }

    private static void Cull(bool doubleSided)
    {
        if (doubleSided)
        {
            GL.Disable(EnableCap.CullFace);
        }
        else
        {
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
        }
    }

    private static List<GpuPart> Upload(GlResources g, SceneModel model)
    {
        if (g.Uploaded.TryGetValue(model, out var parts))
            return parts;

        var textures = model.Textures.Select(tex =>
        {
            var id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Srgb8Alpha8, tex.Width, tex.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, tex.Rgba);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            return id;
        }).ToArray();

        parts = new List<GpuPart>();
        foreach (var part in model.Parts)
        {
            var vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);
            var vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, part.Vertices.Length * sizeof(float), part.Vertices, BufferUsageHint.StaticDraw);
            var ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, part.Indices.Length * sizeof(uint), part.Indices, BufferUsageHint.StaticDraw);
            const int stride = 8 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.BindVertexArray(0);
            var material = model.Materials[part.Material];
            parts.Add(new GpuPart(vao, part.Indices.Length, part.Node, material, material.Texture >= 0 ? textures[material.Texture] : 0));
        }
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        g.Uploaded[model] = parts;
        return parts;
    }

    // 멀티샘플 대상은 늘리기만 한다(뷰 여러 개가 크기가 달라도 매 프레임 다시 만들지 않게). 큰 대상은 2배 샘플
    private static void EnsureMultisample(GlResources g, int w, int h)
    {
        if (w <= g.MsWidth && h <= g.MsHeight)
            return;
        int nw = Math.Max(w, g.MsWidth), nh = Math.Max(h, g.MsHeight);
        if (g.MsFbo != 0)
        {
            GL.DeleteFramebuffer(g.MsFbo);
            GL.DeleteRenderbuffer(g.MsColor);
            GL.DeleteRenderbuffer(g.MsDepth);
        }
        GL.GetInteger(GetPName.MaxSamples, out var max);
        var samples = Math.Clamp((long)nw * nh >= LargePixels ? LargeSamples : Samples, 1, Math.Max(1, max));
        g.MsFbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, g.MsFbo);
        g.MsColor = Renderbuffer(samples, RenderbufferStorage.Rgba8, nw, nh, FramebufferAttachment.ColorAttachment0);
        g.MsDepth = Renderbuffer(samples, RenderbufferStorage.Depth24Stencil8, nw, nh, FramebufferAttachment.DepthStencilAttachment);
        Complete();
        (g.MsWidth, g.MsHeight, g.MsSamples) = (nw, nh, samples);
    }

    private static void EnsurePick(GlResources g)
    {
        if (g.PickFbo != 0)
            return;
        g.PickFbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, g.PickFbo);
        g.PickColor = Renderbuffer(0, RenderbufferStorage.Rgba8, 1, 1, FramebufferAttachment.ColorAttachment0);
        g.PickDepth = Renderbuffer(0, RenderbufferStorage.Depth24Stencil8, 1, 1, FramebufferAttachment.DepthStencilAttachment);
        Complete();
    }

    /// <summary>현재 컨텍스트에 뷰의 대상(해상도 맞춘 색)을 만든다. GPU 길은 텍스처(Skia 가 감쌀 것), CPU 길은 렌더버퍼.</summary>
    internal static (int Fbo, int Texture, int Renderbuffer) CreateTarget(bool texture, int w, int h)
    {
        var fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        int tex = 0, rb = 0;
        if (texture)
        {
            tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        else
        {
            rb = Renderbuffer(0, RenderbufferStorage.Rgba8, w, h, FramebufferAttachment.ColorAttachment0);
        }
        Complete();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return (fbo, tex, rb);
    }

    private static int Renderbuffer(int samples, RenderbufferStorage format, int w, int h, FramebufferAttachment attachment)
    {
        var rb = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rb);
        if (samples > 0)
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, format, w, h);
        else
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, format, w, h);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, attachment, RenderbufferTarget.Renderbuffer, rb);
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        return rb;
    }

    private static void Complete()
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException($"Offscreen framebuffer is incomplete: {status}");
    }

    private static int Program(string vertex, string fragment)
    {
        int Compile(ShaderType type, string source)
        {
            var shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out var ok);
            if (ok == 0)
                throw new InvalidOperationException($"{type} failed: {GL.GetShaderInfoLog(shader)}");
            return shader;
        }

        var vs = Compile(ShaderType.VertexShader, vertex);
        var fs = Compile(ShaderType.FragmentShader, fragment);
        var program = GL.CreateProgram();
        GL.AttachShader(program, vs);
        GL.AttachShader(program, fs);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var linked);
        if (linked == 0)
            throw new InvalidOperationException($"Shader link failed: {GL.GetProgramInfoLog(program)}");
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
        return program;
    }

    private static int Loc(int program, string name) => GL.GetUniformLocation(program, name);

    private static void SetMatrix(int program, string name, Matrix4x4 m) =>
        GL.UniformMatrix4(GL.GetUniformLocation(program, name), 1, false, &m.M11);
}
