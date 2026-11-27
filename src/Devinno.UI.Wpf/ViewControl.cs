using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Devinno.UI.App;
using SkiaSharp;

namespace Devinno.UI.Wpf;

/// <summary>
/// A control that puts a Devinno app (<see cref="DvApp"/>) in a WPF window. When an app is assigned to <see cref="App"/>, the control paints that app's screens and
/// forwards mouse and keyboard input to it. Use it to place a Devinno app in all or part of a WPF window.
/// </summary>
/// <remarks>
/// <para><b>Painting</b>: instead of creating a WPF element per widget, the whole screen is painted with a CPU raster (Skia) straight into one <see cref="WriteableBitmap"/> at the monitor's pixel density.
/// Painting, input handling and VM handler calls (<c>onclick</c>, etc.) all happen on the UI (dispatcher) thread. It paints only when something changed — input, an update request, a size or scale change,
/// or the time of a blink, scroll inertia or virtual keyboard animation — and frames that change only with time repaint and upload only the changed area (<see cref="DvSurface.PartialRender"/>).
/// There is no GPU mode.</para>
/// <para><b>Update requests</b>: if you changed VM values on another thread (a polling timer, etc.), call <see cref="MarkUpdated"/> (= <see cref="DvApp.Invalidate"/>, redo from layout) or <see cref="MarkRepaint"/> (repaint only).
/// Requests made on the app side (<see cref="DvApp.Invalidate"/>, <see cref="DvApp.Show(DvPage)"/>, etc.) also make this control repaint on the UI thread through <see cref="DvApp.RenderRequested"/>.
/// Both can be called from any thread. Changes made inside a handler called by a widget click are applied automatically.</para>
/// <para><b>Text input (default)</b>: when an input box is pressed, a WPF <see cref="TextBox"/> (a <see cref="PasswordBox"/> for passwords) appears over the box's text area and handles editing,
/// so the OS Korean IME, multiline input and copy/paste work as usual. The control paints the box's background, border and focus ring; the edit box has no border and takes the box's background color,
/// text color, font and alignment. When the box is clipped by scrolling, the edit box is clipped too.
/// Enter (Ctrl+Enter for multiline), Tab, Shift+Tab and the Up/Down arrows (single line only) commit the value — Tab and Shift+Tab then edit the next or previous input with its text selected —,
/// Esc cancels (or commits for <c>escape="commit"</c>), and losing focus by pressing elsewhere also commits. Three clicks select the whole text; Ctrl+Z undoes typing.
/// A numeric box rejects typed characters other than digits, <c>+</c>, <c>-</c> and <c>.</c> (the library does the final validation).</para>
/// <para><b>Touch mode</b>: when <see cref="TouchMode"/> is on, the library's built-in virtual keyboard is used instead of the edit box (panel PCs, touch-only screens). Characters from the physical keyboard and
/// Enter, Backspace, Esc, Tab and Shift+Tab are then routed to the virtual keyboard. Touch input arrives as the mouse (WPF promotes touch to mouse events).</para>
/// <para><b>App attachment</b>: an app attaches to only one adapter (window or control) at a time. If <see cref="App"/> is changed or set to <see langword="null"/>, the previous app is only detached (open dialogs are kept) and can be attached again.
/// When the window that contains the control closes (or the dispatcher shuts down), the app is <b>closed</b>: open dialogs close as canceled, and until the app is attached to an adapter again its
/// <see cref="DvApp.ShowDialog{TDialog, TResult}"/> returns a canceled result immediately. The app instance itself is not disposed, so it can be attached to another adapter.</para>
/// </remarks>
/// <example>
/// <code>
/// using System.Windows;
/// using Devinno.UI.App;
/// using Devinno.UI.Styling;
/// using Devinno.UI.Wpf;
///
/// public sealed class MainWindow : Window
/// {
///     private readonly DvApp _app = new() { Theme = DvTheme.Dark };
///
///     public MainWindow()
///     {
///         Width = 1280; Height = 800;
///         _app.Pages.Add(new MainPage());   // a page derived from DvPage
///         Content = new ViewControl { App = _app, ThemeWindow = true };
///         // new ViewControl { App = _app, TouchMode = true } uses the virtual keyboard on a touch panel
///     }
/// }
/// </code>
/// </example>
public sealed class ViewControl : UserControl
{
    private readonly Border _input;     // 포인터·키보드를 받는 바탕(편집 상자의 마우스 이벤트는 여기로 올라오지 않는다)
    private readonly Image _image;
    private readonly Canvas _overlay;   // 편집 상자 자리(논리 px = WPF DIP)
    private readonly TextBox _textBox;
    private readonly PasswordBox _passwordBox;
    private readonly TextBlock _placeholder;
    private readonly DispatcherTimer _animTimer;

    private WriteableBitmap? _bitmap;
    private SKRectI? _animArea;         // 마지막 부분 다시 그리기 영역(물리 px) — 시간으로만 바뀌는 프레임은 이만큼만 올린다
    private DvApp? _app;
    private DvSurface? _surface;        // _app.Surface
    private int _wakeQueued;            // RenderRequested 가 몰려도 BeginInvoke 는 하나만
    private bool _renderQueued;
    private bool _touchMode;
    private Window? _closingWindow;     // 이 창이 닫히면 앱을 닫는다

    // 편집 상자 동기화 상태
    private int _sessionId;
    private int _version;
    private bool _syncing;              // 코드에서 편집 상자를 바꾸는 중 — 이벤트를 코어로 보내지 않음
    private bool _password;             // 지금 세션이 PasswordBox 를 쓰는가
    private (string Family, float Size, bool Bold) _fontKey;

    // 왼쪽 누름 횟수 — 바탕(편집 시작)과 편집 상자를 이어서 센다(세 번 = 전체 선택)
    private int _clicks;
    private long _clickAt;
    private Point _clickPos;

    /// <summary>Creates an empty control. Until <see cref="App"/> is set, it paints only a white background.</summary>
    public ViewControl()
    {
        _image = new Image { Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _input = new Border { Background = Brushes.White, Focusable = true, FocusVisualStyle = null, Child = _image };
        KeyboardNavigation.SetIsTabStop(_input, true);

        _textBox = new TextBox { BorderThickness = new Thickness(0), Padding = new Thickness(0), Visibility = Visibility.Collapsed, IsTabStop = false };
        _passwordBox = new PasswordBox { BorderThickness = new Thickness(0), Padding = new Thickness(0), Visibility = Visibility.Collapsed, IsTabStop = false };
        _placeholder = new TextBlock { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        _overlay = new Canvas { ClipToBounds = true };
        _overlay.Children.Add(_placeholder);
        _overlay.Children.Add(_textBox);
        _overlay.Children.Add(_passwordBox);

        var root = new System.Windows.Controls.Grid();
        root.Children.Add(_input);
        root.Children.Add(_overlay); // 바탕(null) Canvas 는 빈 곳을 누르지 않는다 — 누름은 아래 바탕으로 간다
        Content = root;
        Focusable = false;
        UseLayoutRounding = true;

        _animTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher);
        _animTimer.Tick += (_, _) =>
        {
            _animTimer.Stop();
            Render();
        };

        _input.MouseDown += OnInputMouseDown;
        _input.MouseMove += (_, e) => Pointer(s => s.PointerMove(Pt(e), Mods()));
        _input.MouseUp += OnInputMouseUp;
        _input.MouseLeave += (_, _) =>
        {
            if (_input.IsMouseCaptured)
                return; // 끌기 중 — 놓을 때까지 계속 받는다
            _surface?.PointerLeave();
            RenderIfNeeded();
        };
        _input.MouseWheel += (_, e) =>
        {
            _surface?.PointerWheel(Pt(e), e.Delta / 120f, Mods());
            RenderIfNeeded();
            e.Handled = true;
        };
        _input.PreviewKeyDown += OnInputKeyDown;
        _input.TextInput += OnInputTextInput;

        foreach (var editor in new Control[] { _textBox, _passwordBox })
        {
            editor.PreviewKeyDown += OnEditorKeyDown;
            editor.LostKeyboardFocus += OnEditorLostFocus;
            editor.PreviewMouseLeftButtonDown += (_, e) => CountClick(e.GetPosition(this));
            editor.PreviewMouseLeftButtonUp += (_, _) =>
            {
                if (_clicks >= 3)
                    SelectAllText(); // 세 번 누름 = 전체 선택(편집을 연 바탕 누름부터 센다)
            };
        }
        _textBox.TextChanged += (_, _) => OnEditorTextChanged();
        _passwordBox.PasswordChanged += (_, _) => OnEditorTextChanged();
        _textBox.PreviewTextInput += OnEditorPreviewTextInput;

        SizeChanged += (_, _) => Render();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Dispatcher.ShutdownStarted += (_, _) => CloseApp();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private void CountClick(Point p)
    {
        var now = Environment.TickCount64;
        var size = new Size(SystemParameters.MinimumHorizontalDragDistance * 2, SystemParameters.MinimumVerticalDragDistance * 2);
        var near = Math.Abs(p.X - _clickPos.X) <= size.Width && Math.Abs(p.Y - _clickPos.Y) <= size.Height;
        _clicks = near && now - _clickAt <= GetDoubleClickTime() ? _clicks + 1 : 1;
        (_clickAt, _clickPos) = (now, p);
    }

    /// <summary>
    /// Gets or sets the app to paint. Default <see langword="null"/> (white background). When set, the edit mode is configured according to <see cref="TouchMode"/>,
    /// any edit box in progress is hidden, and the control repaints. Changing it detaches the previous app. Set it on the UI thread.
    /// </summary>
    /// <exception cref="InvalidOperationException">The app is already attached to another window or control (this control's state is unchanged).</exception>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DvApp? App
    {
        get => _app;
        set
        {
            if (ReferenceEquals(_app, value))
                return;
            var old = _app;
            old?.Detach(this); // 프로세스에 앱은 하나만 붙으므로 옛 앱을 먼저 뗀다(닫힘 아님 — 앱은 계속 쓰인다)
            try { value?.Attach(this); }
            catch
            {
                old?.Attach(this); // 실패하면 옛 앱을 도로 붙이고 예외를 다시 던진다
                throw;
            }
            if (old is not null)
            {
                old.HandlerError -= OnAppHandlerError;
                old.RenderRequested -= OnAppRenderRequested;
            }
            _app = value;
            _surface = value?.Surface;
            if (value is not null)
            {
                value.Surface.UseNativeEditor = !_touchMode;
                value.HandlerError += OnAppHandlerError;
                value.RenderRequested += OnAppRenderRequested;
            }
            HideEditor();
            _themeWindow?.Sync(value?.Theme);
            Render();
        }
    }

    /// <summary>
    /// Raised when app (view model) code of the current <see cref="App"/> throws an exception (forwards <see cref="DvApp.HandlerError"/> as is; only <c>sender</c> is this control). The exception has already been caught and does not escape the WPF event handler.
    /// </summary>
    /// <remarks>
    /// When <see cref="App"/> changes, the new app's exceptions are forwarded (the connection to the previous app is cut). Subscribing to <c>App.HandlerError</c> directly gives the same arguments.
    /// Raised on the UI thread. See <see cref="DvApp.HandlerError"/> for the detailed outcome rules.
    /// </remarks>
    /// <example>
    /// <code>
    /// view.App = app;
    /// view.HandlerError += (_, e) =&gt; MessageBox.Show($"{e.Member} failed: {e.Exception.Message}");
    /// </code>
    /// </example>
    public event EventHandler<DvHandlerErrorEventArgs>? HandlerError;

    private void OnAppHandlerError(object? sender, DvHandlerErrorEventArgs e) => HandlerError?.Invoke(this, e);

    // 어느 스레드에서나, 한 번에 여러 번 올 수 있다 — UI 스레드로 한 번만 넘긴다
    private void OnAppRenderRequested(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _wakeQueued, 1) != 0)
            return;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            Volatile.Write(ref _wakeQueued, 0);
            _themeWindow?.Sync(_app?.Theme); // DvApp.Theme 를 바꾸면 이 요청이 온다
            Render();
        });
    }

    /// <summary>
    /// If <see langword="true"/>, input uses the library's built-in virtual keyboard instead of the WPF edit box (panel PCs, touch-only screens). Default <see langword="false"/>.
    /// Changing it hides any edit box in progress and repaints. Set it on the UI thread.
    /// </summary>
    [DefaultValue(false)]
    public bool TouchMode
    {
        get => _touchMode;
        set
        {
            _touchMode = value;
            if (_surface is not null)
                _surface.UseNativeEditor = !value;
            HideEditor();
            Render();
        }
    }

    private WindowTheme? _themeWindow;

    /// <summary>
    /// If <see langword="true"/>, the title bar and border of the window that contains this control, and the app's WPF menus, take their colors from the app theme
    /// (<see cref="DvApp.Theme"/>), so the native window parts match the screen. Default <see langword="false"/> (the OS look).
    /// </summary>
    /// <remarks>
    /// <para>Only colors change: the title bar, its minimize/maximize/close buttons, snapping, the shadow and resizing stay the OS's own. When the app theme changes, the colors follow.</para>
    /// <para><b>Title bar</b>: background = theme <c>background</c> (the same color as the screen canvas), title text = <c>text</c>, window border = <c>border</c>.
    /// Custom colors need Windows 11; Windows 10 (1809 or later) only switches between a dark and a light title bar, chosen by how dark <c>background</c> is.
    /// The window used is the top-level window that contains the control (also a WinForms form when the control is hosted in an <c>ElementHost</c>).</para>
    /// <para><b>Menus</b>: adds implicit styles for <see cref="Menu"/>, <see cref="MenuItem"/>, <see cref="ContextMenu"/>, <see cref="Separator"/> in menus, <see cref="ToolBar"/> and
    /// <see cref="System.Windows.Controls.Primitives.StatusBar"/> to <c>Application.Current.Resources</c> (to the window's resources when there is no WPF <see cref="Application"/>),
    /// so every menu of the app that has no style of its own is drawn with theme colors: the menu bar in <c>background</c> (joined to the title bar),
    /// open menus in <c>surface</c>, the pointed item in <c>surface-raised</c>, text in <c>text</c> (disabled: <c>text-subtle</c>), borders and separators in <c>border</c>,
    /// check marks in <c>primary</c>, and the status bar in <c>background-alt</c>.
    /// Because the styles are application-wide, with several themed controls the most recently applied theme wins.</para>
    /// <para>Turning it off returns the title bar to the OS default and removes the styles. Common dialogs (open/save file) keep the OS look. Set it on the UI thread.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Content = new ViewControl { App = app, ThemeWindow = true };   // title bar, Menu and ContextMenu now use the theme colors
    /// </code>
    /// </example>
    [DefaultValue(false)]
    public bool ThemeWindow
    {
        get => _themeWindow is not null;
        set
        {
            if (value == ThemeWindow)
                return;
            if (value)
            {
                _themeWindow = new WindowTheme(this);
                _themeWindow.Sync(_app?.Theme);
            }
            else
            {
                _themeWindow!.Release();
                _themeWindow = null;
            }
        }
    }

    /// <summary>
    /// Signals that VM data has changed. On the next paint, bindings, styles and layout are recalculated from scratch (<see cref="DvApp.Invalidate"/> + a repaint request).
    /// Can be called from any thread, such as a polling timer; the repaint happens on the UI thread and the call returns immediately.
    /// </summary>
    /// <remarks>
    /// If only values read directly at paint time changed, use the cheaper <see cref="MarkRepaint"/>.
    /// The screen reads VM properties on the UI thread, so the app must keep values changed on other threads consistent (e.g. replace a list with a whole new list).
    /// Rebuilds the current page, the fixed areas (bars) and open dialogs (delegates to <see cref="DvApp.Invalidate"/>).
    /// </remarks>
    public void MarkUpdated()
    {
        _app?.Invalidate(); // RenderRequested 로 깨어난다
        OnAppRenderRequested(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests a repaint only, without re-layout (<see cref="DvSurface.Repaint"/> + a repaint request). Use it when data read directly at paint time changed,
    /// such as data grid cell values or row counts (cheaper than <see cref="MarkUpdated"/>). Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// Data copied at layout time (<c>items</c> of alarm widgets, <c>DvTrend</c> samples, etc.) is not updated by this; use <see cref="MarkUpdated"/>.
    /// </remarks>
    public void MarkRepaint()
    {
        _surface?.Repaint();
        OnAppRenderRequested(this, EventArgs.Empty);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (!ReferenceEquals(window, _closingWindow))
        {
            if (_closingWindow is not null)
                _closingWindow.Closed -= OnWindowClosed;
            _closingWindow = window;
            if (window is not null)
                window.Closed += OnWindowClosed;
        }
        _themeWindow?.Sync(_app?.Theme);
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _animTimer.Stop(); // 다시 붙으면 OnLoaded 가 그린다

    private void OnWindowClosed(object? sender, EventArgs e) => CloseApp();

    // 창이 닫히면 앱을 닫는다(App 교체는 setter 가 따로 뗀다)
    private void CloseApp()
    {
        if (_app is null)
            return;
        _animTimer.Stop();
        _app.HandlerError -= OnAppHandlerError;
        _app.RenderRequested -= OnAppRenderRequested;
        _app.Close(this);
        _app = null;
        _surface = null;
    }

    /// <summary>When the scale changes because the window moved to another monitor, repaints everything at the new pixel density.</summary>
    /// <param name="oldDpi">The previous scale.</param>
    /// <param name="newDpi">The new scale.</param>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _bitmap = null; // 새 그림 = 전체 그리기
        HideEditor();
        Render();
    }

    // ── 포인터
    private SKPoint Pt(MouseEventArgs e)
    {
        var p = e.GetPosition(_input);
        return new SKPoint((float)p.X, (float)p.Y); // WPF 좌표(DIP) = 논리 px
    }

    private static DvPointerButton Button(MouseButton b) => b switch
    {
        MouseButton.Left => DvPointerButton.Left,
        MouseButton.Right => DvPointerButton.Right,
        MouseButton.Middle => DvPointerButton.Middle,
        _ => DvPointerButton.None, // 뒤로·앞으로 버튼은 넘기지 않는다
    };

    private static DvModifiers Mods()
    {
        var k = Keyboard.Modifiers;
        return (k.HasFlag(ModifierKeys.Shift) ? DvModifiers.Shift : 0) | (k.HasFlag(ModifierKeys.Control) ? DvModifiers.Ctrl : 0) | (k.HasFlag(ModifierKeys.Alt) ? DvModifiers.Alt : 0);
    }

    private void OnInputMouseDown(object sender, MouseButtonEventArgs e)
    {
        var button = Button(e.ChangedButton);
        if (e.ChangedButton == MouseButton.Left)
        {
            CountClick(e.GetPosition(this));
            _input.Focus(); // 편집 중이면 편집 상자 포커스가 빠지며 먼저 커밋된다(FocusLost)
        }
        else
            _clicks = 0;
        if (_surface is null || button == DvPointerButton.None)
            return;
        _input.CaptureMouse(); // 바깥으로 끌어도 놓을 때까지 받는다(WinForms 컨트롤과 같음)
        _surface.PointerDown(Pt(e), button, Mods());
        RenderIfNeeded();
        e.Handled = true;
    }

    private void OnInputMouseUp(object sender, MouseButtonEventArgs e)
    {
        var button = Button(e.ChangedButton);
        if (_surface is not null && button != DvPointerButton.None)
        {
            _surface.PointerUp(Pt(e), button, Mods());
            RenderIfNeeded();
        }
        if (Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released && Mouse.MiddleButton == MouseButtonState.Released)
            _input.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Pointer(Action<DvSurface> act)
    {
        if (_surface is null)
            return;
        act(_surface);
        RenderIfNeeded();
    }

    private void RenderIfNeeded()
    {
        if (_surface?.NeedsRender == true)
            QueueRender();
    }

    // 입력이 몰려도 그리기는 한 번 — 다음 Render 우선순위에서
    private void QueueRender()
    {
        if (_renderQueued)
            return;
        _renderQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, Render);
    }

    // ── 그리기: WriteableBitmap 의 뒤 버퍼에 Skia 로 바로 그리고, 바뀐 영역만 화면에 올린다
    private void Render()
    {
        _renderQueued = false;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0 || !IsLoaded)
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var scale = (float)dpi.DpiScaleX;
        var pw = Math.Max(1, (int)Math.Round(w * dpi.DpiScaleX));
        var ph = Math.Max(1, (int)Math.Round(h * dpi.DpiScaleY));
        var lw = Math.Max(1, MathF.Round(pw / scale));
        var lh = Math.Max(1, MathF.Round(ph / scale));

        if (_surface is null)
        {
            _input.Background = Brushes.White;
            _image.Source = null;
            _bitmap = null;
            return;
        }

        // 대기 중인 앱 요청(페이지 전환·영역·전역 설정)을 그림 버퍼를 잡기 전에 적용 — 마크업 오류로 던져도 그림 버퍼가 잠긴 채 남지 않게
        _surface.Layout(lw, lh);

        var fresh = false;
        if (_bitmap is null || _bitmap.PixelWidth != pw || _bitmap.PixelHeight != ph)
        {
            _bitmap = new WriteableBitmap(pw, ph, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32, null);
            _image.Source = _bitmap; // DPI 를 배율에 맞춰 두면 그림의 DIP 크기 = 컨트롤 크기(늘이지 않음)
            fresh = true;
        }

        var now = Environment.TickCount64;
        _surface.PartialRender = !fresh; // 새 그림(첫 그리기·크기 변경)은 전체
        _bitmap.Lock();
        try
        {
            var info = new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var surface = SKSurface.Create(info, _bitmap.BackBuffer, _bitmap.BackBufferStride))
            {
                surface.Canvas.Scale(scale);
                _surface.RenderTo(surface.Canvas, lw, lh, now);
            }

            // 다시 그린 영역(논리 px) → 화면에 올릴 물리 px(바깥으로 반올림). null = 전체, 빈 사각형 = 바뀐 것 없음
            var drawn = _surface.LastRenderRect;
            SKRectI? px = drawn is { } d ? (d.IsEmpty ? SKRectI.Empty : ToPixels(d, scale, pw, ph)) : null;
            var dirty = px ?? new SKRectI(0, 0, pw, ph);
            if (!dirty.IsEmpty)
                _bitmap.AddDirtyRect(new Int32Rect(dirty.Left, dirty.Top, dirty.Width, dirty.Height));
            _animArea = px is { IsEmpty: false } a ? a : px is null ? null : _animArea;
        }
        finally
        {
            _bitmap.Unlock();
        }
        _input.Background = Brushes.Transparent;

        SyncEditor(); // 그림 버퍼 밖 — 편집 상자를 바꿔도 다시 그리기와 엉키지 않는다

        // 점멸·관성·키보드 슬라이드 — 다음 변화 시각에 다시 그림
        _animTimer.Stop();
        if (_surface.NextAnimationDue(now) is { } due)
        {
            _animTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(due - now, 1, int.MaxValue));
            _animTimer.Start();
        }
    }

    private static SKRectI ToPixels(SKRectI r, float s, int pw, int ph)
        => new(Math.Max(0, (int)MathF.Floor(r.Left * s)), Math.Max(0, (int)MathF.Floor(r.Top * s)),
            Math.Min(pw, (int)MathF.Ceiling(r.Right * s)), Math.Min(ph, (int)MathF.Ceiling(r.Bottom * s)));

    // ── 편집 상자 ↔ DvSurface.Edit (그린 직후)
    private Control Editor => _password ? _passwordBox : _textBox;

    private string EditorRawText => _password ? _passwordBox.Password : _textBox.Text;

    // VM 에는 LF 로 돌려준다(원래 값과 같은 줄바꿈 표기)
    private string EditorText => EditorRawText.ReplaceLineEndings("\n");

    private void SelectAllText()
    {
        if (_password)
            _passwordBox.SelectAll();
        else
            _textBox.SelectAll();
    }

    private void SyncEditor()
    {
        var e = _surface!.Edit;
        if (_touchMode || !e.IsEditing)
        {
            HideEditor();
            return;
        }

        _syncing = true;
        try
        {
            if (e.SessionId != _sessionId)
            {
                // 새 편집 대상 — 종류·초기값 적용
                if (_sessionId != 0)
                    Editor.Visibility = Visibility.Collapsed;
                _sessionId = e.SessionId;
                _password = e.Kind == DvEditKind.Password;
                if (_password)
                    _passwordBox.Password = e.InitialText;
                else
                {
                    _textBox.AcceptsReturn = e.Multiline;
                    _textBox.TextWrapping = e.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
                    _textBox.Text = e.InitialText.ReplaceLineEndings(Environment.NewLine);
                }
                _placeholder.Text = e.Placeholder ?? "";
                Place(e);
                Editor.Visibility = Visibility.Visible;
                UpdatePlaceholder();
                Editor.Focus();
                if (e.SelectAll)
                    SelectAllText(); // Tab 으로 옮겨 온 칸, select-on-focus
                else if (!_password)
                    _textBox.CaretIndex = _textBox.Text.Length;
            }
            else if (e.Version != _version)
            {
                Place(e); // 위치·클립·색·폰트만(입력 중 텍스트는 그대로)
            }

            _version = e.Version;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static SolidColorBrush Brush(SKColor c, byte? alpha = null)
    {
        var b = new SolidColorBrush(Color.FromArgb(alpha ?? 255, c.Red, c.Green, c.Blue));
        b.Freeze();
        return b;
    }

    private void Place(DvEditState e)
    {
        var editor = Editor;
        var key = (e.FontFamily, e.FontSize, e.Bold);
        if (key != _fontKey)
        {
            var family = new FontFamily(e.FontFamily);
            foreach (var c in new Control[] { _textBox, _passwordBox })
            {
                c.FontFamily = family;
                c.FontSize = Math.Max(1, e.FontSize); // WPF 글자 크기 단위(DIP) = 논리 px
                c.FontWeight = e.Bold ? FontWeights.Bold : FontWeights.Normal;
            }
            _placeholder.FontFamily = family;
            _placeholder.FontSize = Math.Max(1, e.FontSize);
            _placeholder.FontWeight = e.Bold ? FontWeights.Bold : FontWeights.Normal;
            _fontKey = key;
        }

        var fg = Brush(e.TextColor);
        editor.Background = Brush(e.Background);
        editor.Foreground = fg;
        if (editor is TextBox tb)
        {
            tb.CaretBrush = fg;
            tb.TextAlignment = e.Align switch
            {
                Styling.TextAlign.Center => TextAlignment.Center,
                Styling.TextAlign.Right => TextAlignment.Right,
                _ => TextAlignment.Left
            };
        }
        else if (editor is PasswordBox pb)
            pb.CaretBrush = fg;
        _placeholder.Foreground = Brush(e.TextColor, 115);
        _placeholder.TextAlignment = _textBox.TextAlignment;

        // 글자 영역 = 상자 − 안쪽 여백(논리 px = DIP)
        var c2 = new SKRect(e.Rect.Left + e.Padding.Left, e.Rect.Top + e.Padding.Top, e.Rect.Right - e.Padding.Right, e.Rect.Bottom - e.Padding.Bottom);
        if (e.Numeric is { Unit: { Length: > 0 } unit })
        {
            using var f = new SKFont(SKTypeface.FromFamilyName(e.FontFamily), e.FontSize);
            c2.Right -= f.MeasureText(unit) + 6f; // 단위는 캔버스가 오른쪽에 그린다
        }

        var bounds = new Rect(c2.Left, c2.Top, Math.Max(1, c2.Width), Math.Max(1, c2.Height));
        editor.VerticalContentAlignment = e.Multiline ? VerticalAlignment.Top : VerticalAlignment.Center;
        foreach (var el in new FrameworkElement[] { editor, _placeholder })
        {
            Canvas.SetLeft(el, bounds.X);
            Canvas.SetTop(el, bounds.Y);
            el.Width = bounds.Width;
        }
        editor.Height = bounds.Height;
        _placeholder.Height = double.NaN;
        Canvas.SetTop(_placeholder, e.Multiline ? bounds.Y : bounds.Y + (bounds.Height - e.FontSize * 1.33) / 2);

        // 스크롤 상자 등으로 잘린 부분은 숨김(편집 상자 기준 좌표)
        var clip = Rect.Intersect(bounds, new Rect(e.Clip.Left, e.Clip.Top, Math.Max(0, e.Clip.Width), Math.Max(0, e.Clip.Height)));
        if (clip.IsEmpty)
            clip = new Rect(bounds.X, bounds.Y, 0, 0);
        editor.Clip = clip == bounds ? null : new RectangleGeometry(new Rect(clip.X - bounds.X, clip.Y - bounds.Y, clip.Width, clip.Height));
        _placeholder.Clip = editor.Clip;
    }

    private void UpdatePlaceholder()
        => _placeholder.Visibility = _sessionId != 0 && !_password && _placeholder.Text.Length > 0 && _textBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void HideEditor()
    {
        _sessionId = 0;
        _placeholder.Visibility = Visibility.Collapsed;
        if (_textBox.Visibility != Visibility.Visible && _passwordBox.Visibility != Visibility.Visible)
            return;
        _syncing = true;
        try
        {
            var hadFocus = _textBox.IsKeyboardFocusWithin || _passwordBox.IsKeyboardFocusWithin;
            _textBox.Visibility = Visibility.Collapsed;
            _passwordBox.Visibility = Visibility.Collapsed;
            _passwordBox.Password = ""; // 지난 암호를 남기지 않는다
            if (hadFocus)
                _input.Focus();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnEditorTextChanged()
    {
        UpdatePlaceholder();
        if (_syncing || _surface is null || _sessionId == 0)
            return;
        _surface.SetEditText(_sessionId, EditorText);
        RenderIfNeeded();
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (_surface is null || _sessionId == 0)
            return;

        var multiline = !_password && _textBox.AcceptsReturn;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        DvEditExit? exit = e.Key switch
        {
            Key.Enter when !multiline || ctrl => DvEditExit.Enter,
            Key.Tab => shift ? DvEditExit.ShiftTab : DvEditExit.Tab,
            Key.Up when !multiline => DvEditExit.Up,
            Key.Down when !multiline => DvEditExit.Down,
            _ => null
        };

        if (e.Key == Key.Escape)
        {
            _surface.SetEditText(_sessionId, EditorText);
            _surface.EscapeEdit(_sessionId); // 취소, 또는 escape="commit" 이면 친 글을 쓴다
            e.Handled = true;
        }
        else if (exit is { } x)
        {
            _surface.SetEditText(_sessionId, EditorText);
            _surface.CommitEdit(_sessionId, x); // 거절이면 세션 유지(캔버스가 오류 테두리)
            e.Handled = true;
        }
        else
            return;

        Render(); // 다음 칸(Tab)으로 편집 상자를 바로 옮긴다
    }

    // numeric 은 숫자·부호·점만 받는다(편의 — 최종 검증은 코어)
    private void OnEditorPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_surface?.Edit is { IsEditing: true, Kind: DvEditKind.Numeric } && e.Text.Any(ch => !(char.IsDigit(ch) || ch is '-' or '+' or '.')))
            e.Handled = true;
    }

    private void OnEditorLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_syncing || _surface is null || _sessionId == 0 || !ReferenceEquals(sender, Editor) || Editor.Visibility != Visibility.Visible)
            return;
        _surface.SetEditText(_sessionId, EditorText);
        _surface.CommitEdit(_sessionId, DvEditExit.FocusLost);
        QueueRender();
    }

    // ── 터치 모드: 물리 키보드 → 코어 가상 키보드
    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (_surface is null || !_touchMode)
            return;
        DvKey? key = e.Key switch
        {
            Key.Enter => DvKey.Enter,
            Key.Back => DvKey.Backspace,
            Key.Escape => DvKey.Escape,
            Key.Tab => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? DvKey.ShiftTab : DvKey.Tab,
            _ => null
        };
        if (key is { } k)
        {
            _surface.KeyInput(k);
            e.Handled = true; // Tab 이 WPF 포커스 이동으로 가지 않게
            QueueRender();
        }
    }

    private void OnInputTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_surface is null || !_touchMode || string.IsNullOrEmpty(e.Text) || e.Text.Any(char.IsControl))
            return;
        _surface.TextInput(e.Text);
        e.Handled = true;
        QueueRender();
    }
}
