using System.Diagnostics;
using System.Numerics;
using Devinno.UI;
using Devinno.UI.Widgets;
using SkiaSharp;

namespace Devinno.UI.Scene3D;

/// <summary>
/// Shows a glTF model (<c>.glb</c>) in a screen and moves, colors and picks its parts from bound values. Register the tag with
/// <see cref="Scene3DExtensions.UseScene3D"/> and use <c>&lt;SceneView&gt;</c> in markup.
/// </summary>
/// <remarks>
/// <para><b>Attributes of <c>SceneView</c></b>:</para>
/// <list type="bullet">
/// <item><c>src</c> — path of the <c>.glb</c> file; a relative path starts at the working directory. A model is read once per path and kept.</item>
/// <item><c>camera</c> — name of a camera in the model (a Blender camera object). The view moves to it in 300 ms when the name changes.
/// A name the model does not have (for example <c>Free</c>) shows the whole model.</item>
/// <item><c>value</c> — name of the selected part. With a two-way binding (<c>{Bind Selected}</c>) a click writes the part name.</item>
/// <item><c>pick</c> — node names that are click units, comma-separated. A click on any part below a unit selects the unit. Without it, each root node is a unit.</item>
/// <item><c>state</c> — a <see cref="SceneState"/> for values written from code. Without it, only the markup moves parts.</item>
/// </list>
/// <para><b>Child elements</b> (definitions, read as settings — they are not drawn): each names a node of the model by its glTF name (the Blender object name).
/// Every definition skips itself when <c>disabled</c> is true. Values are read when the screen is rebuilt (<c>Invalidate</c>), like other bound attributes.</para>
/// <list type="bullet">
/// <item><c>&lt;Rotate node axis value scale offset follow ratio/&gt;</c> — turns the node about its own origin by <c>value × scale + offset</c> degrees.</item>
/// <item><c>&lt;Move node axis value scale offset follow ratio/&gt;</c> — moves the node along its own axis by <c>value × scale + offset</c> meters.</item>
/// <item><c>&lt;Color node value amount/&gt;</c> — mixes the color of the node and its children toward <c>value</c> (<c>#RRGGBB(AA)</c> or a theme token) by <c>amount</c>
/// (0–1, default 1; at 1 the part is that color only, its texture hidden). An empty value keeps the model's color.</item>
/// <item><c>&lt;Clip name value min max/&gt;</c> — poses the model at a point of a glTF animation: <c>value</c> from <c>min</c> to <c>max</c> maps to the start and end of the clip
/// (defaults <c>min</c> 0, <c>max</c> = clip length in seconds).</item>
/// </list>
/// <para><c>axis</c> is <c>x</c>, <c>y</c>, <c>z</c>, <c>-x</c>, <c>-y</c>, <c>-z</c> or three numbers (<c>"0 1 0"</c>), in the node's own axes as exported.
/// <c>follow</c> takes the amount of another definition of the same kind on the named node and multiplies it by <c>ratio</c> (meshed gears, mirrored fingers);
/// <c>value</c>, <c>scale</c> and <c>offset</c> are then not used. Several <c>Rotate</c>s on one node add up. Children follow their parent node.</para>
/// <para><b>Input</b>: drag turns the view around its target, the wheel zooms, a click (moving less than 6 px) selects, hovering highlights the click unit.</para>
/// <para><b>Drawing</b>: needs desktop OpenGL 3.3. On a GPU host canvas the scene is drawn in the host's GL context straight into a texture;
/// on a CPU canvas it is drawn in a hidden GL context and read into a bitmap. The scene is drawn again only when a value, the camera, the size,
/// the selection or the highlight changes. A window that uses OpenGL ES (<c>gles: true</c>) shows a message in the view instead.</para>
/// <para><b>Errors</b>: a file that cannot be read, or no OpenGL 3.3, shows the reason inside the view. A node, clip or attribute name the model or the tag does not know,
/// an axis that cannot be read and a <c>follow</c> without its source are skipped with a Trace warning, once each — check the Trace output while writing markup.</para>
/// </remarks>
/// <example>
/// <code>
/// &lt;SceneView src="Models/cell.glb" camera="{Camera}" value="{Bind Selected}" pick="Robot,Pump1" grow="1"&gt;
///   &lt;Rotate node="Turret" axis="y" value="{TurretDeg}"/&gt;
///   &lt;Move   node="LiftRod" axis="y" value="{LiftMm}" scale="0.001"/&gt;
///   &lt;Rotate node="GearB" axis="y" follow="GearA" ratio="-2"/&gt;
///   &lt;Color  node="PumpLamp" value="{LampColor}"/&gt;
/// &lt;/SceneView&gt;
/// </code>
/// </example>
public sealed class SceneView : DvDefinitionWidget
{
    /// <inheritdoc/>
    public override IReadOnlyCollection<string> DefinitionTags => MarkupPose.Tags;

    private static readonly Dictionary<string, SceneModel> Models = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Vector4 SelectTint = new(0.10f, 0.45f, 1.00f, 0.45f);
    private static readonly Vector4 HoverTint = new(0.90f, 0.90f, 1.00f, 0.25f);

    private readonly OrbitCamera _camera = new();
    private SceneModel? _model;
    private string? _appliedCamera;
    private bool _cameraReady;
    private readonly SceneTarget _target = new();
    private bool _drawn;
    private object? _rendered;
    private Matrix4x4[] _world = Array.Empty<Matrix4x4>();
    private int _pixelW, _pixelH;
    private float _scale = 1;
    private SKRect _bounds;
    private int[] _group = Array.Empty<int>();
    private string? _pickSpec;
    private SKPoint _down, _last;
    private bool _pressed, _dragging;
    private int _hover = -1;
    private long _lastHoverPick;

    /// <summary>Test measurements.</summary>
    internal static double LastRenderMs { get; private set; }
    internal static double LastPickMs { get; private set; }
    internal static int RenderCount { get; private set; }

    /// <summary>Test values: widget area, camera matrix and node world matrices of the last draw.</summary>
    internal static SKRect LastBounds { get; private set; }
    internal static Matrix4x4 LastViewProj { get; private set; }
    internal static Matrix4x4[] LastWorld { get; private set; } = Array.Empty<Matrix4x4>();
    internal static SceneModel? LastModel { get; private set; }
    internal static SceneTarget? LastTarget { get; private set; }

    /// <inheritdoc/>
    public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(640, 480);

    /// <inheritdoc/>
    public override int? FrameInterval(DvWidgetContext ctx) => _camera.Animating ? 16 : null;

    /// <inheritdoc/>
    public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
    {
        _bounds = bounds;
        LastBounds = ctx.Bounds;
        var src = ctx.Text("src");
        if (string.IsNullOrEmpty(src) || bounds.Width < 2 || bounds.Height < 2)
            return;

        SceneModel model;
        try
        {
            if (!Models.TryGetValue(src, out model!))
                Models[src] = model = SceneModel.Load(src);
        }
        catch (Exception e)
        {
            Message(canvas, bounds, ctx, $"Model could not be loaded: {e.Message}");
            return;
        }

        if (!ReferenceEquals(model, _model))
        {
            _model = model;
            _cameraReady = false;
            _pickSpec = null;
        }
        Groups(model, ctx.Text("pick"));

        var cameraName = ctx.Text("camera");
        if (!_cameraReady)
        {
            _camera.Set(_camera.Preset(model, cameraName));
            _appliedCamera = cameraName;
            _cameraReady = true;
        }
        else if (cameraName != _appliedCamera)
        {
            _camera.MoveTo(_camera.Preset(model, cameraName));
            _appliedCamera = cameraName;
        }
        _camera.Update(ctx.TimestampMs);

        var state = ctx.Value("state") as SceneState;
        var statePose = state?.Snapshot() ?? PoseSnapshot.Empty;
        var (pose, markup) = MarkupPose.Build(model, ctx.Definitions, statePose, text => Color(ctx, text));
        var selected = ctx.Has("value") ? ctx.Text("value") : state?.Selected;
        var selectedNode = !string.IsNullOrEmpty(selected) && model.NodeByName.TryGetValue(selected, out var sn) ? sn : -1;

        _scale = canvas.TotalMatrix.ScaleX > 0 ? canvas.TotalMatrix.ScaleX : 1;
        var w = Math.Min((int)MathF.Ceiling(bounds.Width * _scale), 2048);
        var h = Math.Min((int)MathF.Ceiling(bounds.Height * _scale), 2048);

        // 캔버스(GRContext 또는 래스터)가 바뀌면 그리는 길이 바뀌므로 다시 그린다 (호스트가 GPU 모드를 켜고 끔)
        var key = (statePose.Version, markup, _camera.Version, w, h, selectedNode, _hover, model, canvas.Context);
        // 대상이 정리되었으면(오래 안 보였던 뷰) 다시 그린다
        if (!_drawn || _target.Fbo == 0 || !Equals(key, _rendered))
        {
            try
            {
                var sw = Stopwatch.StartNew();
                _world = ScenePose.Compute(model, pose);
                var tint = Tints(model, pose, selectedNode, _hover);
                var view = _camera.View;
                var proj = _camera.Projection((float)w / h);
                SceneRenderer.Shared.Render(_target, canvas, model, _world, tint, view, proj, _camera.Eye, w, h);
                LastRenderMs = sw.Elapsed.TotalMilliseconds;
                RenderCount++;
                LastTarget = _target;
                _drawn = true;
                _rendered = key;
                _pixelW = w;
                _pixelH = h;
                LastViewProj = view * proj;
                LastWorld = _world;
                LastModel = model;
            }
            catch (Exception e)
            {
                Message(canvas, bounds, ctx, e.Message);
                return;
            }
        }

        using var paint = new SKPaint { IsAntialias = true };
        _target.Draw(canvas, bounds, paint);
    }

    /// <inheritdoc/>
    public override bool OnPointerDown(DvWidgetContext ctx, SKPoint point)
    {
        _pressed = true;
        _dragging = false;
        _down = _last = point;
        return true;
    }

    /// <inheritdoc/>
    public override void OnPointerMove(DvWidgetContext ctx, SKPoint point)
    {
        if (!_pressed)
            return;
        if (!_dragging && SKPoint.Distance(point, _down) >= 6)
            _dragging = true;
        if (_dragging)
            _camera.Orbit(point.X - _last.X, point.Y - _last.Y);
        _last = point;
    }

    /// <inheritdoc/>
    public override void OnPointerUp(DvWidgetContext ctx, SKPoint point)
    {
        if (!_pressed)
            return;
        _pressed = false;
        if (_dragging)
            return;

        // 끌지 않고 뗐으면 클릭: 그 자리의 부품을 클릭 단위로 올려서 선택한다
        var node = PickAt(ctx, point);
        var group = node >= 0 ? _group[node] : -1;
        if (group < 0 || _model is null)
            return;
        var name = _model.Nodes[group].Name;
        if (ctx.CanCommit)
            ctx.Commit(name);
        (ctx.Value("state") as SceneState)?.NotifyClicked(name);
    }

    /// <inheritdoc/>
    public override void OnPointerCancel(DvWidgetContext ctx) => _pressed = _dragging = false;

    /// <inheritdoc/>
    public override bool OnPointerHover(DvWidgetContext ctx, DvPointerArgs e)
    {
        var now = Environment.TickCount64;
        if (now - _lastHoverPick < 33)
            return false;
        _lastHoverPick = now;
        var node = PickAt(ctx, e.Point);
        var group = node >= 0 ? _group[node] : -1;
        if (group == _hover)
            return false;
        _hover = group;
        return true;
    }

    /// <inheritdoc/>
    public override void OnPointerLeave(DvWidgetContext ctx) => _hover = -1;

    /// <inheritdoc/>
    public override bool OnPointerWheel(DvWidgetContext ctx, DvPointerArgs e, float delta)
    {
        _camera.Zoom(delta);
        return true;
    }

    private int PickAt(DvWidgetContext ctx, SKPoint point)
    {
        if (_model is null || _pixelW == 0)
            return -1;
        var b = ctx.Bounds;
        var x = (int)((point.X - b.Left) / b.Width * _pixelW);
        var y = (int)((point.Y - b.Top) / b.Height * _pixelH);
        var sw = Stopwatch.StartNew();
        var node = SceneRenderer.Shared.Pick(_target, _model, _world, _camera.View, _camera.Projection((float)_pixelW / _pixelH), x, y);
        LastPickMs = sw.Elapsed.TotalMilliseconds;
        return node;
    }

    // 노드마다 클릭 단위(자기 또는 가장 가까운 단위 조상)
    private void Groups(SceneModel model, string? spec)
    {
        if (_pickSpec == spec && _group.Length == model.Nodes.Count)
            return;
        _pickSpec = spec;
        var units = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(spec))
        {
            foreach (var name in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (model.NodeByName.TryGetValue(name, out var i))
                    units.Add(i);
                else
                    Trace.TraceWarning($"Scene3D: pick node '{name}' is not in the model.");
            }
        }
        else
        {
            foreach (var n in model.Nodes.Where(n => n.Parent < 0))
                units.Add(n.Index);
        }

        _group = new int[model.Nodes.Count];
        for (var i = 0; i < _group.Length; i++)
        {
            var n = i;
            while (n >= 0 && !units.Contains(n))
                n = model.Nodes[n].Parent;
            _group[i] = n;
        }
    }

    // 노드별 색 덮기: 상태의 색(자기 또는 조상), 그 위에 선택·호버 강조
    private Vector4[] Tints(SceneModel model, PoseSnapshot pose, int selected, int hover)
    {
        var tint = new Vector4[model.Nodes.Count];
        var own = new Dictionary<int, Vector4>();
        foreach (var (name, (color, amount)) in pose.Color)
            if (model.NodeByName.TryGetValue(name, out var i))
                own[i] = new Vector4(Linear(color.Red), Linear(color.Green), Linear(color.Blue), amount);

        foreach (var i in model.Order)
        {
            var parent = model.Nodes[i].Parent;
            tint[i] = own.TryGetValue(i, out var t) ? t : parent >= 0 ? tint[parent] : Vector4.Zero;
        }

        for (var i = 0; i < tint.Length; i++)
        {
            var g = _group.Length == tint.Length ? _group[i] : -1;
            if (g >= 0 && g == selected)
                tint[i] = Blend(tint[i], SelectTint);
            else if (g >= 0 && g == hover)
                tint[i] = Blend(tint[i], HoverTint);
        }
        return tint;
    }

    private static Vector4 Blend(Vector4 under, Vector4 over)
    {
        // 아래 색 덮기를 먼저 적용한 결과 위에 강조 색을 섞는 것과 같게 합친다
        var rgb = new Vector3(under.X, under.Y, under.Z) * under.W * (1 - over.W) + new Vector3(over.X, over.Y, over.Z) * over.W;
        var amount = under.W + over.W - under.W * over.W;
        return amount <= 0 ? Vector4.Zero : new Vector4(rgb / amount, amount);
    }

    private static float Linear(byte c) => MathF.Pow(c / 255f, 2.2f);

    // #RRGGBB(AA) 또는 테마 토큰(tone-success …)
    private static SKColor? Color(DvWidgetContext ctx, string text)
    {
        if (Devinno.UI.Styling.DvColor.TryParse(text, out var c))
            return c.ToSK();
        var token = ctx.Color(text, SKColors.Empty);
        return token == SKColors.Empty ? null : token;
    }

    private static void Message(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx, string text) =>
        ctx.DrawText(canvas, text, bounds.MidX, bounds.MidY, 14, ctx.TextColor, SKTextAlign.Center);
}
