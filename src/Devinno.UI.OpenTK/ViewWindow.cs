using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Devinno.UI.App;
using Devinno.UI.Components;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI.OpenTK;

/// <summary>
/// A host that shows one Devinno app (<see cref="DvApp"/>) in an OpenTK (GLFW) window. Create it with the app and call <c>Run()</c> to open the window.
/// Use it for standalone apps that draw the whole screen with Devinno, such as panel PCs and touch HMIs.
/// </summary>
/// <remarks>
/// <para><b>Running</b>: it inherits <see cref="GameWindow"/>, so <c>Run()</c> is an event loop that does not return until the window closes. Per GLFW rules, create the window and
/// call <c>Run()</c> on the program's main thread. When you are done with the window, clean it up with <c>Dispose()</c> (usually <c>using var</c>).</para>
/// <para><b>Event-driven rendering</b>: it does not draw every frame continuously; it wakes only on input, <see cref="Invalidate"/>, <see cref="Repaint"/>, window resizes and blink-animation times,
/// and draws only when something changed. So when the screen is idle, CPU usage is almost 0.</para>
/// <para><b>Thread rules</b>: layout, drawing, input handling, and calls to VM handlers such as <c>onclick</c> all happen on the thread that called <c>Run()</c>.
/// If you changed VM values on another thread, such as a polling timer, call <see cref="Invalidate"/> (redo from layout) or <see cref="Repaint"/> (redraw only).
/// Both methods can be called from any thread. However, the screen reads VM properties on the render thread, so the app must keep values changed on other threads consistent
/// (for example, do not modify a list in place; swap in a whole new list). <see cref="Trends.TrendBuffer"/> has its own lock and can be used as is.</para>
/// <para><b>Input</b>: the left mouse button (including touch when the OS converts touch to mouse) is passed as the pointer, and the wheel as scrolling. This host has no native editor, so
/// tapping an input box brings up the library's built-in virtual keyboard (Korean/English QWERTY, numeric keypad). With a physical keyboard connected, completed characters (including Korean composed by the IME) and
/// Enter, Backspace, Esc, Tab and Shift+Tab are passed through as well.</para>
/// <para><b>Drawing mode</b>: the default is CPU raster (Skia draws into a bitmap, which is then uploaded as a texture); turning on <see cref="Gpu"/> makes Skia draw directly to the window with OpenGL.
/// CPU raster keeps the bitmap between frames, so frames that change only with time (flow animation, blinking) redraw only the changed region and upload only that region to the texture (<see cref="DvSurface.PartialRender"/>). GPU mode also draws the same way to an offscreen GPU surface, then copies it whole to the window every frame.</para>
/// <para><b>Devices</b>: choose these with constructor arguments, because they must be fixed when the window is created. <c>gles</c> uses OpenGL ES 3.0 instead of desktop OpenGL 3.3 (embedded boards such as ARM touch panels).
/// <c>fullscreen</c> opens a borderless full-screen window on the current monitor (kiosk). <c>fit</c> lays the screen out at the constructor size and scales it to the window, keeping the aspect ratio:
/// a screen made for 1280×750 shows whole on a 1024×600 panel, and when the ratios differ it is centered with margins in the theme background color (<c>--bg</c>). Pointer coordinates are converted to the same layout size.</para>
/// </remarks>
/// <example>
/// <code>
/// using Devinno.UI.App;
/// using Devinno.UI.OpenTK;
/// using Devinno.UI.Styling;
///
/// var app = new DvApp { Theme = DvTheme.Dark };
/// app.Pages.Add(new MainPage());   // a page derived from DvPage (markup + view model role)
/// using var window = new ViewWindow(app, 1280, 800, "My HMI") { Gpu = true };
///
/// // change values on another thread and request a screen refresh
/// using var poll = new System.Threading.Timer(_ =&gt; app.Invalidate(), null, 1000, 1000);
///
/// window.Run();   // does not return until the window closes
/// </code>
/// </example>
public sealed class ViewWindow : GameWindow
{
    private readonly DvApp _app;
    private readonly bool _gles; // OpenGL ES 3.0 컨텍스트 — 셰이더 머리·Skia GL 인터페이스가 다르다
    private readonly Vector2i? _fit; // fit 모드의 설계 크기(논리 px) — 레이아웃은 늘 이 크기, 창에 비율 유지로 맞춘다
    private readonly DvSurface _surface; // _app.Surface — 루트 화면 하나(고정)
    private int _texture;
    private Vector2i _texSize; // 텍스처에 마지막으로 통째로 올린 크기 — 같을 때만 일부 갱신
    private int _vao;
    private int _vbo;
    private int _program;
    private SKBitmap? _bitmap; // 프레임마다 새로 만들지 않고 크기가 바뀔 때만 재생성

    // GPU 경로: Skia 가 창의 기본 프레임버퍼(FBO 0)에 직접 그린다 — CPU 래스터·텍스처 업로드 없음
    private GRGlInterface? _gli;
    private GRContext? _gr;
    private GRBackendRenderTarget? _target;
    private SKSurface? _gpuSurface;
    private SKSurface? _gpuLayer; // GPU 그림을 프레임 사이에 남겨 두는 화면 밖 표면(부분 다시 그리기) — 매 프레임 창 프레임버퍼로 통째 복사
    private bool _layerWait; // 크기가 막 바뀐 프레임 — 화면 밖 표면을 만들지 않는다(창 크기를 계속 바꾸는 동안 매 프레임 새로 만들지 않게)
    private int _stencilBits;
    private bool _loaded; // OnLoad 전 OnResize 에서 그리지 않도록
    private Vector2i _paintedSize; // 마지막으로 그린 클라이언트 크기
    private float _scale = 1f; // 화면 배율(모니터 DPI) — 레이아웃·포인터는 논리 px(= 클라이언트 px ÷ 배율), 그리기는 배율을 곱해 물리 px 로

    // 이벤트 구동 렌더: 입력·Invalidate·점멸 시각에만 깨어나고, 그려야 할 때만 그린다(유휴 CPU ≈ 0)
    private bool _forcePaint = true; // 첫 프레임·크기 변경·노출(expose) — 내용이 같아도 다시 올려야 함
    private long? _animDue;
    private readonly System.Threading.Timer _animTimer;

    /// <summary>Creates a window that shows one app (<see cref="DvApp"/>). The window becomes visible only when <c>Run()</c> is called.</summary>
    /// <remarks>An app attaches to only one window at a time — if it is already attached to another window or control, an exception is thrown. When the window closes (<c>OnUnload</c>), the app detaches and can be attached to another adapter.
    /// Set the theme, localization and registry with <see cref="DvApp.Theme"/>, <see cref="DvApp.Strings"/> and <see cref="DvApp.Registry"/>.</remarks>
    /// <param name="app">The app to show.</param>
    /// <param name="width">The width of the window client area (logical px — at 150% display scaling, physical px is 1.5 times this).</param>
    /// <param name="height">The height of the window client area (logical px).</param>
    /// <param name="title">The window title.</param>
    /// <param name="gles">If <see langword="true"/>, creates an OpenGL ES 3.0 context instead of desktop OpenGL 3.3 (for boards that only have GLES, such as linux-arm64 touch panels). Applies to both CPU raster and <see cref="Gpu"/> mode.</param>
    /// <param name="fullscreen">If <see langword="true"/>, opens a borderless window that fills the current monitor (kiosk). The window size is the monitor resolution, so <paramref name="width"/> and <paramref name="height"/> only matter as the layout size when <paramref name="fit"/> is on.</param>
    /// <param name="fit">If <see langword="true"/>, the screen is always laid out at <paramref name="width"/>×<paramref name="height"/> and scaled to the window keeping the aspect ratio (centered, with margins in the theme <c>--bg</c> color when the ratios differ). Pointer coordinates are converted to that size, so pages work as if the window were exactly that size.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="app"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">When <paramref name="app"/> is already attached to another window or control.</exception>
    /// <example>
    /// <code>
    /// // a screen made for 1280×750 on a 1024×600 GLES touch panel, full screen
    /// using var window = new ViewWindow(app, 1280, 750, "HMI", gles: true, fullscreen: true, fit: true);
    /// window.Run();
    /// </code>
    /// </example>
    public ViewWindow(DvApp app, int width, int height, string title, bool gles = false, bool fullscreen = false, bool fit = false)
        : base(GameWindowSettings.Default, CheckAvailable(app, Settings(width, height, title, gles, fullscreen)))
    {
        try
        {
            app.Attach(this); // 미리 검사했어도 그 사이 다른 어댑터가 붙었을 수 있다
        }
        catch
        {
            base.Dispose(); // 이미 만들어진 네이티브 창을 남기지 않는다(호출자는 참조를 못 받는다)
            throw;
        }

        _app = app;
        _gles = gles;
        _fit = fit ? new Vector2i(Math.Max(width, 1), Math.Max(height, 1)) : null;
        _surface = app.Surface;
        _surface.UseNativeEditor = false; // 이전에 ViewControl(네이티브 편집기)에 붙었던 앱이어도 이 창은 내장 가상 키보드
        _app.HandlerError += OnAppHandlerError;
        _app.RenderRequested += OnAppRenderRequested;
        _animTimer = new System.Threading.Timer(_ => Wake());
        IsEventDriven = true;
    }

    // 컨텍스트 종류·창 모양은 창을 만들 때 정해진다 — 생성 뒤에는 못 바꾼다
    private static NativeWindowSettings Settings(int width, int height, string title, bool gles, bool fullscreen)
    {
        var settings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(width, height),
            Title = title
        };
        if (gles)
        {
            settings.API = ContextAPI.OpenGLES;
            settings.APIVersion = new Version(3, 0);
            settings.Profile = ContextProfile.Any; // 프로필은 데스크톱 GL 전용
        }

        if (fullscreen)
        {
            settings.WindowBorder = WindowBorder.Hidden;
            settings.WindowState = WindowState.Fullscreen; // 현재 모니터 해상도로
            settings.AutoIconify = false; // GLFW 기본은 포커스를 잃으면 최소화 — 키오스크는 늘 떠 있어야 한다
        }

        return settings;
    }

    // 기반 생성자(네이티브 창 생성) 인자로 평가 — 앱이 없거나 이미 붙어 있으면 창을 만들기 전에 거절한다
    private static NativeWindowSettings CheckAvailable(DvApp app, NativeWindowSettings settings)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.IsAttached)
            throw new InvalidOperationException("This DvApp is already attached to another window or control. Close that one first, or detach it by changing its App.");
        return settings;
    }

    private void OnAppHandlerError(object? sender, DvHandlerErrorEventArgs e) => HandlerError?.Invoke(this, e); // 구독자 예외는 DvApp 이 삼킨다

    private void OnAppRenderRequested(object? sender, EventArgs e) => Wake();

    /// <summary>
    /// Raised when app (view model) code called by the screen throws an exception (<see cref="DvApp.HandlerError"/> forwarded as is; only <c>sender</c> is this window). The exception has already been caught and does not leave the event loop.
    /// </summary>
    /// <remarks>
    /// Covers exceptions from <c>onclick</c>, <c>onrowclick</c>, <c>onack</c> and <c>onbeforecommit</c> handlers, setters of properties that widgets write to, and page transition notifications (<c>OnShown</c>, <c>OnHidden</c>).
    /// A Trace error is written even with no subscribers. Raised on the render thread (main thread), and <c>sender</c> is this window.
    /// For the detailed outcome rules, see <see cref="DvApp.HandlerError"/> and <see cref="DvSurface.HandlerError"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var window = new ViewWindow(app, 1280, 800, "HMI");
    /// window.HandlerError += (_, e) =&gt; Log.Error(e.Exception, $"{e.Kind} {e.Member} ({e.Tag})");
    /// window.Run();
    /// </code>
    /// </example>
    public event EventHandler<DvHandlerErrorEventArgs>? HandlerError;

    /// <summary>
    /// Signals that VM data has changed. On the next frame, bindings, styles and layout are recomputed from scratch and drawn.
    /// Can be called from any thread, such as a polling timer. It only sets a flag, wakes the render loop and returns immediately.
    /// </summary>
    /// <remarks>
    /// Use it when values that affect layout change, such as screen structure or text and list contents. If only values read directly at draw time changed, use the cheaper <see cref="Repaint"/>.
    /// Changes made inside a VM handler called by a widget click are applied automatically, so there is no need to call it. If the window does not exist yet or has closed, the wake-up is skipped.
    /// Rebuilds both the current page and the fixed areas (delegates to <see cref="DvApp.Invalidate"/>).
    /// </remarks>
    public void Invalidate()
    {
        _app.Invalidate(); // 현재 페이지·영역 화면을 다시 만들고 RenderRequested 로 깨운다
        Wake();
    }

    /// <summary>
    /// Requests a redraw only, without re-layout. Use it when data read directly at draw time changes, such as data grid cell values or row counts
    /// (cheaper than <see cref="Invalidate"/>). Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// Data copied at layout time (the <c>items</c> of alarm widgets, the samples of <c>DvTrend</c>, etc.) is not updated by this, so use <see cref="Invalidate"/> for it.
    /// </remarks>
    public void Repaint()
    {
        _surface.Repaint();
        Wake();
    }

    // 다른 스레드에서 이벤트 루프 깨우기 — GLFW 가 유일하게 모든 스레드에서 허용하는 호출
    private void Wake()
    {
        if (!Exists)
            return;
        try
        {
            GLFW.PostEmptyEvent();
        }
        catch (GLFWException)
        {
            // 창이 닫히는 중 — 깨울 루프가 없다
        }
    }

    /// <summary>
    /// If <see langword="true"/>, Skia draws directly to the window with OpenGL (GPU). Default <see langword="false"/> (CPU raster).
    /// Greatly reduces the per-frame cost on screens with many gradients and shadows.
    /// </summary>
    /// <remarks>
    /// It is <c>init</c>-only, so set it with an object initializer at creation (<c>new ViewWindow(...) { Gpu = true }</c>). It is applied once when the window first loads;
    /// if GPU initialization fails, a <see cref="System.Diagnostics.Trace"/> warning is written and drawing falls back to CPU raster (no exception).
    /// </remarks>
    public bool Gpu { get; init; }

    /// <summary>When the window first loads, prepares the drawing mode (GPU or CPU) and wakes the loop to draw the first frame.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();
        _scale = CurrentScale();
        if (_scale != 1f && WindowState != WindowState.Fullscreen) // 전체 화면은 모니터 크기 그대로
            ClientSize = new Vector2i((int)MathF.Round(ClientSize.X * _scale), (int)MathF.Round(ClientSize.Y * _scale)); // 생성자 크기는 논리 px
        if (!(Gpu && TryInitGpu()))
            InitCpu();
        WatchContentScale();
        _loaded = true;
        Wake(); // 이벤트 구동 루프는 첫 이벤트까지 기다린다 — 첫 프레임은 입력 없이도 바로 그리도록
    }

    // 실행 중 배율이 바뀜(다른 배율의 모니터로 옮김, 디스플레이 배율 변경) — 창 크기는 그대로라 OnResize 가 오지 않으므로
    // GLFW 콜백으로 받는다. Windows 의 PerMonitorV2 창처럼 논리 크기를 유지하도록 창을 새 배율로 키우거나 줄이고 다시 그린다.
    private GLFWCallbacks.WindowContentScaleCallback? _contentScaleChanged; // 네이티브가 부르는 동안 살아 있게

    private unsafe void WatchContentScale()
    {
        _contentScaleChanged = (_, _, _) => OnContentScaleChanged();
        GLFW.SetWindowContentScaleCallback(WindowPtr, _contentScaleChanged);
    }

    private void OnContentScaleChanged()
    {
        var scale = CurrentScale();
        if (scale == _scale)
            return;
        var k = scale / _scale;
        _scale = scale;
        _forcePaint = true;
        if (WindowState != WindowState.Fullscreen)
            ClientSize = new Vector2i((int)MathF.Round(ClientSize.X * k), (int)MathF.Round(ClientSize.Y * k)); // OnResize 가 새 배율로 그린다
        Wake();
    }

    private bool TryInitGpu()
    {
        try
        {
            var bindings = new GLFWBindingsContext();
            _gli = _gles
                ? GRGlInterface.CreateGles(name => bindings.GetProcAddress(name))
                : GRGlInterface.CreateOpenGl(name => bindings.GetProcAddress(name));
            _gr = _gli is null ? null : GRContext.CreateGl(_gli);
            if (_gr is null)
                throw new InvalidOperationException("GRContext.CreateGl returned null");

            // 기본 프레임버퍼의 실제 스텐실 비트(Skia 의 클리핑·패스 채움에 쓰임). 조회 실패 시 GLFW 기본 8.
            GL.GetFramebufferAttachmentParameter(FramebufferTarget.Framebuffer, FramebufferAttachment.Stencil,
                FramebufferParameterName.FramebufferAttachmentStencilSize, out _stencilBits);
            if (GL.GetError() != global::OpenTK.Graphics.OpenGL4.ErrorCode.NoError)
                _stencilBits = 8;

            if (!EnsureGpuSurface())
                throw new InvalidOperationException("SKSurface.Create(GRContext) returned null");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Devinno.UI: GPU render initialization failed; falling back to CPU. {ex.Message}");
            DisposeGpu();
            return false;
        }
    }

    // 프레임버퍼 크기가 바뀌었을 때만 렌더 타깃·서피스를 다시 만든다
    private bool EnsureGpuSurface()
    {
        var w = Math.Max(FramebufferSize.X, 1);
        var h = Math.Max(FramebufferSize.Y, 1);
        if (_gpuSurface is not null && _target is not null && _target.Width == w && _target.Height == h)
            return true;

        _gpuSurface?.Dispose();
        _gpuLayer?.Dispose();
        _gpuLayer = null; // 크기가 바뀌었다 — 크기가 멈춘 뒤 새로 만들고 전체를 그린다
        _layerWait = true;
        _target?.Dispose();
        _target = new GRBackendRenderTarget(w, h, 0, _stencilBits,
            new GRGlFramebufferInfo(0, SKColorType.Rgba8888.ToGlSizedFormat()));
        _gpuSurface = SKSurface.Create(_gr, _target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        return _gpuSurface is not null;
    }

    private void DisposeGpu()
    {
        _gpuLayer?.Dispose();
        _gpuLayer = null;
        _gpuSurface?.Dispose();
        _gpuSurface = null;
        _target?.Dispose();
        _target = null;
        _gr?.Dispose();
        _gr = null;
        _gli?.Dispose();
        _gli = null;
    }

    private void InitCpu()
    {
        _texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        CreateQuadResources();
        GL.ClearColor(Color4.White);
    }

    /// <summary>Called each time the render loop wakes. Draws the screen and swaps buffers only when something changed or an animation time has come.</summary>
    /// <param name="args">Frame information.</param>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        if (FrameRendered is not { } report)
        {
            Render();
            return;
        }

        // 장시간 스모크 도구(tools/Smoke)용 — 실제로 화면을 바꾼 프레임만 걸린 시간(ms)을 알린다
        var swaps = _swaps;
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Render();
        if (_swaps != swaps)
            report(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
    }

    /// <summary>The frame time (ms) of each frame that changed the screen — for measurement tools only.</summary>
    internal Action<double>? FrameRendered;
    private long _swaps;

    private void Render()
    {
        var now = Environment.TickCount64;
        var animate = _animDue is { } due && now >= due;
        if (!_forcePaint && !animate && !_surface.NeedsRender)
        {
            ScheduleAnimation(now);
            return; // 화면은 마지막 프레임 그대로 — SwapBuffers 안 함
        }

        var scale = CurrentScale();
        if (scale != _scale)
        {
            _scale = scale; // 다른 배율의 모니터로 옮겨짐 — 새 논리 크기로 전체
            _forcePaint = true;
        }

        var force = _forcePaint;
        _forcePaint = false;
        var pixelW = Math.Max(ClientSize.X, 1);
        var pixelH = Math.Max(ClientSize.Y, 1);
        var view = ViewTransform(pixelW, pixelH, scale, _fit); // 레이아웃은 논리 px(fit 이면 설계 크기)
        var width = view.Width;
        var height = view.Height;
        _paintedSize = ClientSize;

        if (_gr is not null && EnsureGpuSurface())
        {
            // 창 프레임버퍼는 버퍼 교체 뒤 내용이 남지 않는다 — 화면 밖 표면에 그려 두고(시간으로만 바뀌는 프레임은 바뀐 영역만)
            // 매 프레임 그 표면을 프레임버퍼로 통째 복사한다. 화면 밖 표면을 못 만들면 프레임버퍼에 바로 전체를 그린다
            var newLayer = false;
            if (_layerWait)
                _layerWait = false; // 이번 프레임은 프레임버퍼에 바로 전체 — 다음 프레임까지 크기가 그대로면 그때 만든다
            else if (_gpuLayer is null)
            {
                _gpuLayer = SKSurface.Create(_gr, true, new SKImageInfo(_target!.Width, _target.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                newLayer = true;
            }

            var layer = _gpuLayer ?? _gpuSurface!;
            _surface.PartialRender = _gpuLayer is not null && !force && !newLayer;
            var lc = layer.Canvas;
            lc.Save();
            // 클라이언트 px → 프레임버퍼 px(macOS 처럼 둘이 다르면 늘린다), 그다음 논리 px → 클라이언트 px
            lc.Scale((float)_target!.Width / pixelW, (float)_target.Height / pixelH);
            PrepareView(lc, view);
            _surface.RenderTo(lc, width, height, now);
            lc.Restore();
            if (_surface.LastRenderRect is { IsEmpty: true })
            {
                ScheduleAnimation(now);
                return; // 다시 그린 것이 없다 — 화면은 마지막 프레임 그대로
            }

            var canvas = _gpuSurface!.Canvas;
            if (_gpuLayer is not null)
                canvas.DrawSurface(_gpuLayer, 0, 0);
            canvas.Flush();
            _gr.Flush();
            ScheduleAnimation(now);
            _swaps++;
            SwapBuffers();
            return;
        }

        var fresh = false;
        if (_bitmap is null || _bitmap.Width != pixelW || _bitmap.Height != pixelH)
        {
            _bitmap?.Dispose();
            _bitmap = new SKBitmap(pixelW, pixelH, SKColorType.Rgba8888, SKAlphaType.Premul);
            fresh = true;
        }

        // 비트맵은 프레임 사이에 그대로 남는다 — 시간으로만 바뀌는 프레임은 바뀐 영역만 그리고 그 영역만 텍스처에 올린다.
        // 비트맵을 새로 만들었거나(크기 변경) 강제로 그리는 프레임(첫 프레임·노출)은 전체
        var texReady = _texSize.X == pixelW && _texSize.Y == pixelH;
        var bitmap = _bitmap;
        _surface.PartialRender = !force && !fresh && texReady;
        using (var canvas = new SKCanvas(bitmap))
        {
            PrepareView(canvas, view);
            _surface.RenderTo(canvas, width, height, now);
        }
        ScheduleAnimation(now);

        var dirty = ToPixels(_surface.LastRenderRect, view.Scale, view.X, view.Y, pixelW, pixelH);
        if (dirty is { IsEmpty: true })
            return; // 다시 그린 것이 없다 — 화면은 마지막 프레임 그대로

        GL.Viewport(0, 0, pixelW, pixelH);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        if (dirty is { } d && texReady)
        {
            // 비트맵의 d 영역만 — 한 줄 길이는 비트맵 폭
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, pixelW);
            GL.PixelStore(PixelStoreParameter.UnpackSkipPixels, d.Left);
            GL.PixelStore(PixelStoreParameter.UnpackSkipRows, d.Top);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, d.Left, d.Top, d.Width, d.Height, PixelFormat.Rgba, PixelType.UnsignedByte, bitmap.GetPixels());
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
            GL.PixelStore(PixelStoreParameter.UnpackSkipPixels, 0);
            GL.PixelStore(PixelStoreParameter.UnpackSkipRows, 0);
        }
        else
        {
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                pixelW,
                pixelH,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                bitmap.GetPixels());
            _texSize = new Vector2i(pixelW, pixelH);
        }

        DrawFullscreenQuad();
        _swaps++;
        SwapBuffers();
    }

    private SKPoint Logical(float x, float y)
    {
        var view = ViewTransform(Math.Max(ClientSize.X, 1), Math.Max(ClientSize.Y, 1), _scale, _fit);
        return new((x - view.X) / view.Scale, (y - view.Y) / view.Scale);
    }

    /// <summary>
    /// The layout size (logical px) for a client area, and the scale and offset that map it to client px. Without fit, the layout is the client size ÷ monitor scale at offset 0;
    /// with fit, the layout is the design size, scaled uniformly to fit and centered.
    /// </summary>
    internal static (int Width, int Height, float Scale, int X, int Y) ViewTransform(int pixelW, int pixelH, float scale, Vector2i? fit)
    {
        if (fit is not { } design)
            return (Math.Max((int)MathF.Round(pixelW / scale), 1), Math.Max((int)MathF.Round(pixelH / scale), 1), scale, 0, 0);
        var k = MathF.Min((float)pixelW / design.X, (float)pixelH / design.Y);
        return (design.X, design.Y, k, (int)MathF.Round((pixelW - design.X * k) / 2), (int)MathF.Round((pixelH - design.Y * k) / 2));
    }

    // 캔버스를 논리 px 로 맞춘다. fit 이면 전체를 다시 그리는 프레임에 여백을 테마 배경색으로 칠하고,
    // 화면이 지우는(Clear) 범위를 설계 크기 안으로 묶는다 — Clear 는 변환을 무시하고 클립 전체를 칠한다
    private void PrepareView(SKCanvas canvas, (int Width, int Height, float Scale, int X, int Y) view)
    {
        if (_fit is not null && !_surface.PartialRender)
            canvas.Clear(MarginColor());
        canvas.Translate(view.X, view.Y);
        canvas.Scale(view.Scale);
        if (_fit is not null)
            canvas.ClipRect(SKRect.Create(0, 0, view.Width, view.Height));
    }

    private SKColor MarginColor() =>
        (_app.Theme ?? DvTheme.Default).TryGet("--bg", out var value) && SKColor.TryParse(value, out var color) ? color : SKColors.Black;

    /// <summary>For testing — draws at this scale instead of the monitor scale (to check without a real high-DPI monitor). If null, the monitor scale is used.</summary>
    internal float? ScaleOverride { get; init; }

    // 모니터 배율(Windows 의 디스플레이 배율 = GLFW content scale). 클라이언트 크기가 이미 논리 단위인 플랫폼(macOS — 프레임버퍼가 따로 큼)은 1
    private float CurrentScale()
    {
        if (ScaleOverride is { } forced and > 0)
            return forced;
        if (FramebufferSize != ClientSize)
            return 1f;
        return TryGetCurrentMonitorScale(out var x, out _) && x > 0 ? x : 1f;
    }

    /// <summary>Redrawn region in logical px → physical px (scaled, moved by the fit offset, rounded outward, clipped to the bitmap). null = everything, empty rectangle = nothing.</summary>
    internal static SKRectI? ToPixels(SKRectI? logical, float scale, int offsetX, int offsetY, int pixelW, int pixelH)
    {
        if (logical is not { } r || r.IsEmpty || (scale == 1f && offsetX == 0 && offsetY == 0))
            return logical;
        var px = SKRectI.Create(0, 0, pixelW, pixelH);
        var scaled = new SKRectI(offsetX + (int)MathF.Floor(r.Left * scale), offsetY + (int)MathF.Floor(r.Top * scale),
            offsetX + (int)MathF.Ceiling(r.Right * scale), offsetY + (int)MathF.Ceiling(r.Bottom * scale));
        return SKRectI.Intersect(scaled, px);
    }

    // 점멸 위젯이 있으면 다음 위상 전환 시각에 루프를 깨운다. 없으면 타이머 정지.
    private void ScheduleAnimation(long now)
    {
        _animDue = _surface.NextAnimationDue(now);
        if (_animDue is { } due)
            _animTimer.Change(Math.Max(1, due - now), Timeout.Infinite);
        else
            _animTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    // Windows 에서 창 크기 조절·이동 중에는 OS 모달 루프가 WaitEvents 안에서 돌아 OnRenderFrame 이 오지 않는다.
    // 그동안에도 OnResize 는 메인 스레드(GL 컨텍스트 current)에서 오므로 여기서 바로 그린다.
    /// <summary>When the window size changes, redraws immediately at the new size (so the content keeps up even while resizing).</summary>
    /// <param name="e">The new size information.</param>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        _forcePaint = true;
        if (_loaded)
            Render();
    }

    // Refresh(WM_PAINT) 는 리사이즈마다 따라온다. 방금 그 크기로 그렸으면 다시 그리지 않는다
    // (DWM 합성 환경에서는 창 내용이 보존된다). 크기가 다르거나 아직 안 그렸으면 루프에 맡긴다.
    /// <summary>Called when the OS asks for the window contents again. Requests a redraw only when the size differs from the last drawn size.</summary>
    protected override void OnRefresh()
    {
        base.OnRefresh();
        if (ClientSize == _paintedSize)
            return;
        _forcePaint = true;
        Wake();
    }

    /// <summary>Passes mouse movement to the screen as pointer movement (hover, drag).</summary>
    /// <param name="e">The mouse position information.</param>
    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        _surface.PointerMove(Logical(e.X, e.Y));
    }

    /// <summary>Passes the mouse wheel as scrolling at the current pointer position.</summary>
    /// <param name="e">The wheel delta.</param>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _surface.PointerWheel(Logical(MousePosition.X, MousePosition.Y), e.OffsetY);
    }

    /// <summary>Passes a left-button press to the screen as a pointer press. Other buttons are ignored.</summary>
    /// <param name="e">The button information.</param>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Left)
            _surface.PointerDown(Logical(MousePosition.X, MousePosition.Y));
    }

    /// <summary>Passes a left-button release to the screen as a pointer release (click detection). Other buttons are ignored.</summary>
    /// <param name="e">The button information.</param>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButton.Left)
            _surface.PointerUp(Logical(MousePosition.X, MousePosition.Y));
    }

    // 입력 — OpenTK 는 터치 전제라 네이티브 편집기 없이 코어 가상 키보드를 쓴다.
    // 물리 키보드를 꽂았으면 완성 문자(IME 가 완성한 한글 포함)와 Enter·Backspace·Esc·Tab 만 키보드로 넘긴다.
    /// <summary>Passes completed characters typed on a physical keyboard (including Korean composed by the IME) to the input box being edited.</summary>
    /// <param name="e">The typed character.</param>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _surface.TextInput(e.AsString);
    }

    /// <summary>Passes only Enter (including the keypad Enter), Backspace, Esc, Tab and Shift+Tab as editing keys. Other keys are ignored.</summary>
    /// <param name="e">The key information.</param>
    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        DvKey? key = e.Key switch
        {
            Keys.Enter or Keys.KeyPadEnter => DvKey.Enter,
            Keys.Backspace => DvKey.Backspace,
            Keys.Escape => DvKey.Escape,
            Keys.Tab => e.Shift ? DvKey.ShiftTab : DvKey.Tab,
            _ => null
        };
        if (key is { } k)
            _surface.KeyInput(k);
    }

    /// <summary>Closes and detaches the app while disposing the window (so the app can be reused even if the window is disposed without <c>Run()</c>).</summary>
    /// <param name="disposing"><see langword="true"/> to also release managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_app is { } app) // 생성자가 Attach 에서 실패해 해제되는 경우 _app 은 아직 null
            {
                app.HandlerError -= OnAppHandlerError;
                app.RenderRequested -= OnAppRenderRequested;
                app.Close(this);
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>When the window closes, releases the animation timer and drawing resources such as the GPU context, textures and shaders.</summary>
    protected override void OnUnload()
    {
        if (_app is { } app)
        {
            app.HandlerError -= OnAppHandlerError;
            app.RenderRequested -= OnAppRenderRequested;
            app.Close(this);
        }

        _animTimer.Dispose();
        DisposeGpu();
        _bitmap?.Dispose();
        _bitmap = null;
        if (_texture != 0)
            GL.DeleteTexture(_texture);
        if (_vbo != 0)
            GL.DeleteBuffer(_vbo);
        if (_vao != 0)
            GL.DeleteVertexArray(_vao);
        if (_program != 0)
            GL.DeleteProgram(_program);
        base.OnUnload();
    }

    private void CreateQuadResources()
    {
        float[] vertices =
        {
            -1f, -1f, 0f, 1f,
             1f, -1f, 1f, 1f,
             1f,  1f, 1f, 0f,
            -1f, -1f, 0f, 1f,
             1f,  1f, 1f, 0f,
            -1f,  1f, 0f, 0f
        };

        _program = CreateProgram(_gles);
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    private void DrawFullscreenQuad()
    {
        GL.UseProgram(_program);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.Uniform1(GL.GetUniformLocation(_program, "uTexture"), 0);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);
        GL.UseProgram(0);
    }

    private static int CreateProgram(bool gles)
    {
        // 데스크톱 GL 3.3 core / OpenGL ES 3.0 — 본문은 같고 머리만 다르다(ES 프래그먼트는 float 정밀도 지정 필수)
        var header = gles ? "#version 300 es\nprecision mediump float;\n" : "#version 330 core\n";
        var vertex = header + """
            layout (location = 0) in vec2 aPos;
            layout (location = 1) in vec2 aTex;
            out vec2 vTex;
            void main()
            {
                vTex = aTex;
                gl_Position = vec4(aPos, 0.0, 1.0);
            }
            """;

        var fragment = header + """
            in vec2 vTex;
            out vec4 FragColor;
            uniform sampler2D uTexture;
            void main()
            {
                FragColor = texture(uTexture, vTex);
            }
            """;

        var vs = CompileShader(ShaderType.VertexShader, vertex);
        var fs = CompileShader(ShaderType.FragmentShader, fragment);
        var program = GL.CreateProgram();
        GL.AttachShader(program, vs);
        GL.AttachShader(program, fs);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var status);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);

        if (status == 0)
            throw new InvalidOperationException(GL.GetProgramInfoLog(program));

        return program;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        var shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var status);

        if (status == 0)
            throw new InvalidOperationException(GL.GetShaderInfoLog(shader));

        return shader;
    }
}
