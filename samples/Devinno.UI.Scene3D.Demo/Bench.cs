using System.Diagnostics;
using System.Numerics;
using Devinno.UI.App;
using Devinno.UI.Components;
using Devinno.UI.Styling;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;

namespace Devinno.UI.Scene3D.Demo;

/// <summary>
/// 3D 뷰 성능 측정. 실제 앱과 같은 길(화면 → 위젯 → 렌더러 → 캔버스)로 그리고, 매 프레임 값을 바꿔 3D 를 다시 그리게 한다.
/// 캔버스 두 가지: CPU(래스터, ViewWindow CPU 모드와 같음), GPU(숨은 GLFW 창의 GL 컨텍스트 + GRContext, ViewWindow GPU 모드와 같음).
/// 1차: 측정 끔 — 프레임 전체 시간. 2차: 측정 켬(단계 사이 GL.Finish) — 단계별 시간.
/// </summary>
internal static unsafe class Bench
{
    private static readonly (int W, int H)[] Sizes = [(912, 594), (1280, 720), (1920, 1080), (2048, 2048)];
    private const int Warmup = 15, Frames = 80;

    public static int Run()
    {
        var registry = new ComponentRegistry();
        registry.RegisterWidget<SceneView>("SceneView");
        var page = new BenchPage();
        var app = new DvApp { Theme = DvTheme.Dark, Registry = registry };
        app.Pages.Add(page);

        // GPU 캔버스용 호스트 컨텍스트 (ViewWindow 처럼 GL 3.3 core)
        GLFWProvider.EnsureInitialized();
        GLFW.WindowHint(WindowHintBool.Visible, false);
        GLFW.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        GLFW.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        GLFW.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
        GLFW.WindowHint(WindowHintBool.OpenGLForwardCompat, true);
        var host = GLFW.CreateWindow(16, 16, "bench host", null, null);
        GLFW.DefaultWindowHints();
        GLFW.MakeContextCurrent(host);
        GL.LoadBindings(new GLFWBindingsContext());
        using var gli = GRGlInterface.Create(name => GLFW.GetProcAddress(name));
        using var gr = GRContext.CreateGl(gli);
        Console.WriteLine($"INFO GL {GL.GetString(StringName.Renderer)} | {GL.GetString(StringName.Version)}");

        Console.WriteLine("mode  size        frame ms (p50 / p90)   3D draw   read   convert   canvas   alloc/frame   GPU targets   CPU buffers");
        foreach (var gpu in new[] { false, true })
            foreach (var (w, h) in Sizes)
            {
                var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var surface = gpu ? SKSurface.Create(gr, true, info) : SKSurface.Create(info);
                var s = app.Surface;
                var angle = 0.0;

                // 한 프레임: 값 바꾸기 → 그리기 → 캔버스를 GPU 까지 내보내기
                (double Frame, double Canvas) One()
                {
                    page.State.Rotate("Turret", Vector3.UnitY, angle += 1);
                    var sw = Stopwatch.StartNew();
                    s.Layout(w, h);
                    s.RenderTo(surface.Canvas, w, h, (long)angle * 16);
                    var painted = sw.Elapsed.TotalMilliseconds;
                    if (gpu)
                        gr.Flush(true, true);
                    else
                        surface.Flush();
                    var total = sw.Elapsed.TotalMilliseconds;
                    return (total, total - painted);
                }

                SceneRenderer.Profile = false;
                for (var i = 0; i < Warmup; i++)
                    One();
                var frame = new List<double>();
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < Frames; i++)
                    frame.Add(One().Frame);
                var alloc = (GC.GetAllocatedBytesForCurrentThread() - before) / Frames;

                SceneRenderer.Profile = true;
                var draw = new List<double>();
                var read = new List<double>();
                var convert = new List<double>();
                var canvas = new List<double>();
                for (var i = 0; i < Frames; i++)
                {
                    canvas.Add(One().Canvas);
                    var st = SceneRenderer.LastStats;
                    draw.Add(st.Draw);
                    read.Add(st.Read);
                    convert.Add(st.Convert);
                }
                SceneRenderer.Profile = false;

                var t = SceneView.LastTarget!;
                var gpuBytes = SceneRenderer.Shared.SharedBytes(t) + t.GpuBytes;
                Console.WriteLine($"{(gpu ? "GPU" : "CPU"),-5} {w,4}x{h,-6} {P(frame, 50),6:0.00} / {P(frame, 90),6:0.00}      " +
                                  $"{P(draw, 50),6:0.00}  {P(read, 50),5:0.00}  {P(convert, 50),6:0.00}   {P(canvas, 50),6:0.00}   " +
                                  $"{alloc / 1024.0 / 1024,7:0.0} MB   {gpuBytes / 1024.0 / 1024,7:0.0} MB   {t.CpuBytes / 1024.0 / 1024,7:0.0} MB");
            }
        return Compare(app, page, gr);
    }

    // 같은 프레임을 CPU 캔버스와 GPU 캔버스(직접 텍스처)로 그려 PNG 로 남기고 픽셀 차이를 센다
    private static int Compare(DvApp app, BenchPage page, GRContext gr)
    {
        const int w = 1280, h = 720;
        var outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output");
        Directory.CreateDirectory(outDir);
        page.State.Rotate("Turret", Vector3.UnitY, 40);
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        SKBitmap Shot(SKSurface surface, string name)
        {
            app.Surface.Layout(w, h);
            app.Surface.RenderTo(surface.Canvas, w, h, 0);
            gr.Flush(true, true);
            var bitmap = new SKBitmap(info);
            surface.ReadPixels(info, bitmap.GetPixels(), info.RowBytes, 0, 0);
            using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
            using (var file = File.Create(Path.Combine(outDir, name)))
                data.SaveTo(file);
            return bitmap;
        }

        using var cpuSurface = SKSurface.Create(info);
        using var gpuSurface = SKSurface.Create(gr, true, info);
        using var cpu = Shot(cpuSurface, "bench_cpu.png");
        // 호스트(Skia)가 남겼을 법한 상태로 흐트러뜨린다 — 렌더러가 자기 상태를 직접 정하는지 확인 (실제 창에서 깊이 쓰기가 꺼져 있던 일이 있었다)
        GL.DepthMask(false);
        GL.Enable(EnableCap.StencilTest);
        GL.Enable(EnableCap.ScissorTest);
        GL.Scissor(0, 0, 1, 1);
        GL.ColorMask(false, true, true, true);
        GL.PixelStore(PixelStoreParameter.PackRowLength, 7);
        gr.ResetContext(GRGlBackendState.All);
        using var gpu = Shot(gpuSurface, "bench_gpu.png");
        int differ = 0, worst = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                SKColor a = cpu.GetPixel(x, y), b = gpu.GetPixel(x, y);
                var d = Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)), Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));
                worst = Math.Max(worst, d);
                if (d > 8)
                    differ++;
            }
        // 피킹: 두 길에서 같은 점(기어 중심)을 눌러 같은 부품이 골라지는지 (GPU 길은 호스트 컨텍스트에서 피킹)
        string? Click(SKSurface surface)
        {
            app.Surface.Layout(w, h);
            app.Surface.RenderTo(surface.Canvas, w, h, 0);
            gr.Flush(true, true);
            var c = Vector4.Transform(new Vector4(SceneView.LastWorld[SceneView.LastModel!.NodeByName["GearA"]].Translation, 1), SceneView.LastViewProj);
            var b = SceneView.LastBounds;
            var p = new SKPoint(b.Left + (c.X / c.W * 0.5f + 0.5f) * b.Width, b.Top + (0.5f - c.Y / c.W * 0.5f) * b.Height);
            app.Surface.PointerDown(p);
            app.Surface.PointerUp(p);
            return page.State.Selected;
        }
        var cpuPick = Click(cpuSurface);
        var gpuPick = Click(gpuSurface);
        Console.WriteLine($"{(cpuPick is not null && cpuPick == gpuPick ? "OK  " : "FAIL")} pick matches on both canvases: CPU '{cpuPick}', GPU '{gpuPick}'");
        if (cpuPick is null || cpuPick != gpuPick)
            return 1;

        var share = differ * 100.0 / (w * h);
        Console.WriteLine($"{(share < 0.5 ? "OK  " : "FAIL")} CPU and GPU canvases match: {share:0.000}% pixels differ by more than 8 (worst {worst})");
        return share < 0.5 ? 0 : 1;
    }

    private static double P(List<double> values, int percent)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted[Math.Min(sorted.Length - 1, sorted.Length * percent / 100)];
    }
}
