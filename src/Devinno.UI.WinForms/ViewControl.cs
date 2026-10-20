using System.ComponentModel;
using Devinno.UI.App;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Devinno.UI.WinForms;

/// <summary>
/// A control that attaches a Devinno app (<see cref="DvApp"/>) to a WinForms form. When an app is assigned to <see cref="App"/>, the control paints that app's screens and
/// forwards mouse and keyboard input to it. Use it to place a Devinno app in part of an existing WinForms app.
/// </summary>
/// <remarks>
/// <para><b>Painting</b>: instead of creating a WinForms control per widget, the whole screen is painted with a CPU raster into a single canvas (<see cref="SKControl"/>). Painting, input handling and
/// VM handler calls (<c>onclick</c>, etc.) all happen on the UI thread. When there is blinking, scroll inertia or a virtual keyboard animation, a WinForms timer repaints only at the times needed.
/// The image is kept in a buffer owned by the control, so frames that change only with time (flow animation, blinking) repaint and invalidate only the changed area (<see cref="DvSurface.PartialRender"/>).</para>
/// <para><b>Update requests</b>: if you changed VM values on another thread (a polling timer, etc.), call <see cref="MarkUpdated"/> (= <see cref="DvApp.Invalidate"/>, redo from layout) or <see cref="MarkRepaint"/> (repaint only). Requests made on the app side (<see cref="DvApp.Invalidate"/>, <see cref="DvApp.Show(DvPage)"/>, etc.) also make this control repaint on the UI thread through <see cref="DvApp.RenderRequested"/>.
/// Both can be called from any thread; when needed they are passed to the UI thread with <c>BeginInvoke</c>. Changes made inside a handler called by a widget click are applied automatically.</para>
/// <para><b>Text input (default)</b>: when an input box is pressed, a single hidden <see cref="TextBox"/> appears over the box's text area and handles editing. This lets the OS Korean IME, multiline input and
/// copy/paste work as usual. The canvas paints the box's background, border and focus ring; the TextBox has no border and matches the box's background color, text color, font and alignment
/// (a WinForms TextBox cannot be made transparent). When the box is clipped by scrolling, the TextBox is clipped too.
/// Enter (Ctrl+Enter for multiline), Tab, Shift+Tab and the Up/Down arrows (single line only) commit the value, Esc cancels, and losing focus by pressing elsewhere also commits.
/// A numeric box rejects characters other than digits, <c>+</c>, <c>-</c> and <c>.</c> (the library does the final validation). A password box masks text with the system password character.</para>
/// <para><b>Touch mode</b>: when <see cref="TouchMode"/> is on, the library's built-in virtual keyboard is used instead of the TextBox (panel PCs, touch-only screens). Characters from the physical keyboard and
/// Enter, Backspace, Esc, Tab and Shift+Tab are then routed to the virtual keyboard.</para>
/// <para><b>App attachment</b>: an app attaches to only one adapter (window or control) at a time. If <see cref="App"/> is changed or set to <see langword="null"/>, the previous app is only detached (open dialogs are kept) and can be attached again.
/// Disposing the control (<c>Dispose</c>) <b>closes</b> the app: open dialogs close as canceled, and until the app is attached to an adapter again its <see cref="DvApp.ShowDialog{TDialog, TResult}"/> returns a canceled result immediately. The app instance itself is not disposed, so it can be attached to another adapter.</para>
/// </remarks>
/// <example>
/// <code>
/// using Devinno.UI.App;
/// using Devinno.UI.Styling;
/// using Devinno.UI.WinForms;
///
/// public sealed class MainForm : Form
/// {
///     private readonly ViewControl _view = new() { Dock = DockStyle.Fill };
///     private readonly DvApp _app = new() { Theme = DvTheme.Dark };
///     private readonly System.Threading.Timer _poll;
///
///     public MainForm()
///     {
///         ClientSize = new Size(1280, 800);
///         _app.Pages.Add(new MainPage());   // a page derived from DvPage
///         _view.App = _app;
///         // _view.TouchMode = true;   // use the virtual keyboard on a touch panel
///         Controls.Add(_view);
///
///         // change values on another thread and request an update
///         _poll = new System.Threading.Timer(_ =&gt; { _app.Invalidate(); }, null, 1000, 1000);
///         FormClosed += (_, _) =&gt; _poll.Dispose();
///     }
/// }
/// </code>
/// </example>
public sealed class ViewControl : UserControl
{
    private readonly SKControl _canvas;
    private readonly TextBox _editor;
    private readonly System.Windows.Forms.Timer _animTimer = new();

    // 그림 버퍼 — 프레임 사이에 남겨 두고(DvSurface.PartialRender) 매 그리기에 캔버스로 복사한다(SKControl 의 비트맵 유지 여부에 기대지 않음)
    private SKBitmap? _buffer;
    private SKRectI? _animArea;   // 마지막 부분 다시 그리기 영역(다음 애니메이션 틱에 그만큼만 무효화)
    private SKRectI? _invalidated; // 이번 그리기를 부른 무효화 영역(null = 전체)
    private DvApp? _app;
    private DvSurface? _surface; // _app.Surface
    private int _wakeQueued; // RenderRequested 가 몰려도 BeginInvoke 는 하나만
    private bool _touchMode;

    // 편집기 동기화 상태
    private int _sessionId;
    private int _version;
    private bool _syncing; // 코드에서 TextBox 를 바꾸는 중 — 이벤트를 코어로 보내지 않음
    private bool _syncQueued;
    private Font? _font;
    private (string Family, float Size, bool Bold) _fontKey;

    /// <summary>Creates an empty control. Until <see cref="App"/> is set, it paints only a white background.</summary>
    public ViewControl()
    {
        _canvas = new SKControl { Dock = DockStyle.Fill, TabStop = true };
        _editor = new TextBox { BorderStyle = BorderStyle.None, Visible = false, TabStop = false };
        _canvas.Controls.Add(_editor); // 캔버스 위에 겹침
        Controls.Add(_canvas);

        _canvas.PaintSurface += OnPaintSurface;
        _canvas.MouseDown += (_, e) => Pointer(e, s => s.PointerDown(Pt(e)), focus: true);
        _canvas.MouseMove += (_, e) => Pointer(e, s => s.PointerMove(Pt(e)), any: true);
        _canvas.MouseUp += (_, e) => Pointer(e, s => s.PointerUp(Pt(e)));
        _canvas.MouseWheel += (_, e) =>
        {
            _surface?.PointerWheel(Pt(e), e.Delta / 120f);
            RenderIfNeeded();
        };
        _canvas.PreviewKeyDown += (_, e) => e.IsInputKey = e.KeyCode is Keys.Tab or Keys.Enter or Keys.Escape;
        _canvas.KeyDown += OnCanvasKeyDown;
        _canvas.KeyPress += OnCanvasKeyPress;

        _editor.TextChanged += OnEditorTextChanged;
        _editor.PreviewKeyDown += (_, e) => e.IsInputKey = e.KeyCode is Keys.Tab or Keys.Enter or Keys.Escape or Keys.Up or Keys.Down;
        _editor.KeyDown += OnEditorKeyDown;
        _editor.KeyPress += OnEditorKeyPress;
        _editor.LostFocus += OnEditorLostFocus;

        _animTimer.Tick += (_, _) =>
        {
            _animTimer.Stop();
            // 시간으로만 바뀌는 프레임 — 지난번 부분 다시 그리기 영역을 알면 창도 그만큼만 다시 칠한다(모르면 전체)
            if (_animArea is { } a)
            {
                _invalidated = a;
                _canvas.Invalidate(new System.Drawing.Rectangle(a.Left, a.Top, a.Width, a.Height));
            }
            else
            {
                _invalidated = null;
                _canvas.Invalidate();
            }
        };
    }

    /// <summary>
    /// Gets or sets the app to paint. Default <see langword="null"/> (white background). When set, the edit mode is configured according to <see cref="TouchMode"/>,
    /// any edit box in progress is hidden, and the control repaints. Changing it detaches the previous app. Set it on the UI thread. Not shown in the designer.
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
                old?.Attach(this); // 실패하면 옛 앱을 도로 붙이고(구독·필드는 아직 그대로) 예외를 다시 던진다
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
            ApplyPendingOutsidePaint(); // 시작 페이지(OnLoaded·OnShown)를 그리기 전에
            _canvas.Invalidate();
        }
    }

    /// <summary>When the handle is (re)created, clears the pending-wake flag and applies pending app requests (the start page, etc.) before the first paint.</summary>
    /// <param name="e">The event arguments.</param>
    protected override void OnHandleCreated(EventArgs e)
    {
        Volatile.Write(ref _wakeQueued, 0); // 이전 핸들에 걸린 BeginInvoke 가 버려졌을 수 있음
        base.OnHandleCreated(e);
        ApplyPendingOutsidePaint();
    }

    // 대기 중인 앱 요청(페이지 전환·영역·전역 설정)을 그리기(WM_PAINT) 밖, UI 스레드에서 적용한다 — 루트 Layout 이 적용하므로 캔버스 크기로 미리 부른다.
    // 그리기 안에서 적용하면 OnHidden·OnLoaded·OnShown(과 페이지 마크업 오류)이 OnPaintSurface 안에서 돌아 중첩 WM_PAINT 로 SKControl 비트맵 잠금이 깨질 수 있다.
    // 같은 크기로 이어지는 RenderTo 의 레이아웃은 할 일이 없다. 핸들이 없거나 크기가 0 이면 건너뛴다(첫 그리기가 따라잡는다).
    private void ApplyPendingOutsidePaint()
    {
        if (_surface is null || IsDisposed || !IsHandleCreated || _canvas.Width <= 0 || _canvas.Height <= 0)
            return;
        _surface.Layout(Logical(_canvas.Width), Logical(_canvas.Height)); // SKControl 은 컨트롤 Width×Height(물리 px)로 그린다 — 레이아웃은 논리 px
    }

    // Dispose 에서 앱을 닫는다(App 교체는 setter 가 따로 뗀다).
    private void CloseApp()
    {
        if (_app is null)
            return;
        _app.HandlerError -= OnAppHandlerError;
        _app.RenderRequested -= OnAppRenderRequested;
        _app.Close(this);
        _app = null;
        _surface = null;
    }

    /// <summary>
    /// Raised when app (view model) code of the current <see cref="App"/> throws an exception (forwards <see cref="DvApp.HandlerError"/> as is; only <c>sender</c> is this control). The exception has already been caught and does not escape the WinForms event handler.
    /// </summary>
    /// <remarks>
    /// When <see cref="App"/> changes, the new app's exceptions are forwarded (the connection to the previous app is cut). Subscribing to <c>App.HandlerError</c> directly gives the same arguments.
    /// Raised on the UI thread. See <see cref="DvApp.HandlerError"/> for the detailed outcome rules.
    /// </remarks>
    /// <example>
    /// <code>
    /// _view.App = app;
    /// _view.HandlerError += (_, e) =&gt; MessageBox.Show($"{e.Member} 실패: {e.Exception.Message}");
    /// </code>
    /// </example>
    public event EventHandler<DvHandlerErrorEventArgs>? HandlerError;

    private void OnAppHandlerError(object? sender, DvHandlerErrorEventArgs e) => HandlerError?.Invoke(this, e);

    // 어느 스레드에서나, 한 번에 여러 번 올 수 있다 — UI 스레드로 한 번만 넘긴다. 핸들 전/후·해제 경합은 삼킨다(첫 그리기가 따라잡는다).
    private void OnAppRenderRequested(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        if (Interlocked.Exchange(ref _wakeQueued, 1) != 0)
            return;
        try
        {
            BeginInvoke(() =>
            {
                Volatile.Write(ref _wakeQueued, 0);
                if (IsDisposed)
                    return;
                try { ApplyPendingOutsidePaint(); } // 전환 알림을 그리기 밖에서
                finally { _canvas.Invalidate(); } // 마크업 오류로 던져도 이전 페이지는 다시 그린다
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            Volatile.Write(ref _wakeQueued, 0); // 핸들이 사라지는 중
        }
    }

    /// <summary>
    /// If <see langword="true"/>, input uses the library's built-in virtual keyboard instead of the hidden TextBox (panel PCs, touch-only screens). Default <see langword="false"/>.
    /// Changing it hides any TextBox edit box in progress and repaints. Set it on the UI thread.
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
            _canvas.Invalidate();
        }
    }

    /// <summary>
    /// Signals that VM data has changed. On the next paint, bindings, styles and layout are recalculated from scratch (<see cref="DvApp.Invalidate"/> + a repaint request).
    /// Can be called from any thread, such as a polling timer. When called from another thread, the repaint request is passed to the UI thread with <c>BeginInvoke</c> and the call returns immediately.
    /// </summary>
    /// <remarks>
    /// If the control handle has not been created yet, only a flag is set and the change is applied on the first paint. If only values read directly at paint time changed, use the cheaper <see cref="MarkRepaint"/>.
    /// The screen reads VM properties on the UI thread, so the app must keep values changed on other threads consistent (e.g. replace a list with a whole new list).
    /// Rebuilds the current page, the fixed areas (bars) and open dialogs (delegates to <see cref="DvApp.Invalidate"/>).
    /// </remarks>
    public void MarkUpdated()
    {
        _app?.Invalidate();
        if (!IsHandleCreated)
            return;
        if (InvokeRequired)
            BeginInvoke(_canvas.Invalidate);
        else
            _canvas.Invalidate();
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
        if (!IsHandleCreated)
            return;
        if (InvokeRequired)
            BeginInvoke(_canvas.Invalidate);
        else
            _canvas.Invalidate();
    }

    private SKPoint Pt(MouseEventArgs e) => new(e.X / UiScale, e.Y / UiScale);

    /// <summary>For testing: paints at this scale instead of the monitor scale (DeviceDpi). If null, DeviceDpi / 96.</summary>
    internal float? ScaleOverride = null;

    // 화면 배율 — 레이아웃·포인터는 논리 px(= 물리 px ÷ 배율), 그리기는 배율을 곱한다(CSS 의 devicePixelRatio 와 같음)
    private float UiScale => ScaleOverride is { } f and > 0 ? f : DeviceDpi > 0 ? DeviceDpi / 96f : 1f;

    private float Logical(int px) => Math.Max(1, MathF.Round(px / UiScale));

    // 논리 px 사각형 → 물리 px(바깥으로 반올림)
    private System.Drawing.Rectangle ToPixels(SKRectI r)
    {
        var s = UiScale;
        var left = (int)MathF.Floor(r.Left * s);
        var top = (int)MathF.Floor(r.Top * s);
        return System.Drawing.Rectangle.FromLTRB(left, top, (int)MathF.Ceiling(r.Right * s), (int)MathF.Ceiling(r.Bottom * s));
    }

    /// <summary>When the scale changes because the window moved to another monitor, repaints everything at the new logical size.</summary>
    /// <param name="e">The event arguments.</param>
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        _buffer?.Dispose();
        _buffer = null; // 새 버퍼 = 전체 그리기
        HideEditor();
        _canvas.Invalidate();
    }

    private void Pointer(MouseEventArgs e, Action<DvSurface> act, bool focus = false, bool any = false)
    {
        if (_surface is null || (!any && e.Button != MouseButtons.Left))
            return;
        if (focus && !_editor.Focused)
            _canvas.Focus(); // 편집 중이면 TextBox 포커스가 빠지며 먼저 커밋된다(FocusLost)
        act(_surface);
        RenderIfNeeded();
    }

    private void RenderIfNeeded()
    {
        if (_surface?.NeedsRender == true)
            _canvas.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_surface is null)
        {
            e.Surface.Canvas.Clear(SKColors.White);
            return;
        }

        var now = Environment.TickCount64;
        var fresh = false;
        if (_buffer is null || _buffer.Width != e.Info.Width || _buffer.Height != e.Info.Height)
        {
            _buffer?.Dispose();
            _buffer = new SKBitmap(e.Info);
            fresh = true;
        }

        _surface.PartialRender = !fresh; // 새 버퍼(첫 그리기·크기 변경)는 전체
        var scale = UiScale;
        using (var bufferCanvas = new SKCanvas(_buffer))
        {
            bufferCanvas.Scale(scale);
            _surface.RenderTo(bufferCanvas, Logical(e.Info.Width), Logical(e.Info.Height), now);
        }
        e.Surface.Canvas.DrawBitmap(_buffer, 0, 0);

        // 다시 그린 영역(논리 px) → 창 무효화용 물리 px
        var drawn = _surface.LastRenderRect;
        if (drawn is { IsEmpty: false } lr && scale != 1f)
        {
            var px = ToPixels(lr);
            drawn = SKRectI.Create(px.X, px.Y, px.Width, px.Height);
        }
        _animArea = drawn is { IsEmpty: false } d ? d : drawn is null ? null : _animArea;
        var asked = _invalidated;
        _invalidated = null;
        if (asked is { } inv)
        {
            // 일부만 무효화했는데 그보다 넓게 바뀌었다(관성 스크롤 등으로 전체를 그린 프레임 포함) — 나머지도 화면에
            if (drawn is null)
                _canvas.Invalidate();
            else if (drawn is { IsEmpty: false } got && !inv.Contains(got))
                _canvas.Invalidate(new System.Drawing.Rectangle(got.Left, got.Top, got.Width, got.Height));
        }

        // TextBox 는 그리기가 끝난 뒤 맞춘다 — 그리는 중에 바꾸면(Multiline 전환 = 핸들 재생성, 포커스) 캔버스에
        // 중첩 WM_PAINT 가 와서 SKControl 비트맵 잠금 예외가 나고 동기화가 끊긴다
        if (!_syncQueued)
        {
            _syncQueued = true;
            BeginInvoke(() =>
            {
                _syncQueued = false;
                if (_surface is not null)
                    SyncEditor();
            });
        }

        // 점멸·관성·키보드 슬라이드 — 다음 변화 시각에 다시 그림
        _animTimer.Stop();
        if (_surface.NextAnimationDue(now) is { } due)
        {
            _animTimer.Interval = (int)Math.Clamp(due - now, 1, int.MaxValue);
            _animTimer.Start();
        }
    }

    // ── 숨긴 TextBox ↔ DvSurface.Edit (렌더 직후)
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
                _sessionId = e.SessionId;
                _editor.Multiline = e.Multiline;
                _editor.AcceptsReturn = e.Multiline;
                _editor.WordWrap = e.Multiline;
                _editor.ScrollBars = ScrollBars.None;
                _editor.UseSystemPasswordChar = e.Kind == DvEditKind.Password;
                _editor.PlaceholderText = e.Placeholder ?? "";
                _editor.Text = e.InitialText.ReplaceLineEndings(Environment.NewLine); // TextBox 는 CRLF 만 줄바꿈으로 보인다
                Place(e);
                _editor.Visible = true;
                _editor.Focus();
                _editor.SelectionStart = _editor.TextLength;
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

    private void Place(DvEditState e)
    {
        var key = (e.FontFamily, e.FontSize, e.Bold);
        if (_font is null || key != _fontKey)
        {
            _font?.Dispose();
            _font = new Font(e.FontFamily, e.FontSize * UiScale, e.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            _fontKey = key;
            _editor.Font = _font;
        }

        _editor.BackColor = Color.FromArgb(255, e.Background.Red, e.Background.Green, e.Background.Blue);
        _editor.ForeColor = Color.FromArgb(255, e.TextColor.Red, e.TextColor.Green, e.TextColor.Blue);
        _editor.TextAlign = e.Align switch
        {
            Styling.TextAlign.Center => HorizontalAlignment.Center,
            Styling.TextAlign.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };

        // 글자 영역 = 상자 − 안쪽 여백. 한 줄은 그 안에서 세로 가운데. 편집 상태는 논리 px — TextBox 는 물리 px 라 배율을 곱한다
        var k = UiScale;
        var c = new SKRect((e.Rect.Left + e.Padding.Left) * k, (e.Rect.Top + e.Padding.Top) * k,
            (e.Rect.Right - e.Padding.Right) * k, (e.Rect.Bottom - e.Padding.Bottom) * k);
        if (e.Numeric is { Unit: { Length: > 0 } unit })
        {
            using var f = new SKFont(SKTypeface.FromFamilyName(e.FontFamily), e.FontSize);
            c.Right -= (f.MeasureText(unit) + 6f) * k; // 단위는 캔버스가 오른쪽에 그린다
        }

        var h = e.Multiline ? c.Height : _editor.PreferredHeight;
        var bounds = new Rectangle((int)MathF.Round(c.Left), (int)MathF.Round(c.MidY - h / 2f),
            Math.Max(1, (int)MathF.Round(c.Width)), Math.Max(1, (int)MathF.Round(h)));
        if (e.Multiline)
            bounds.Y = (int)MathF.Round(c.Top);
        _editor.Bounds = bounds;

        // 스크롤 상자 등으로 잘린 부분은 숨김
        var clip = Rectangle.Intersect(bounds, new Rectangle((int)(e.Clip.Left * k), (int)(e.Clip.Top * k),
            (int)MathF.Ceiling(e.Clip.Width * k), (int)MathF.Ceiling(e.Clip.Height * k)));
        clip.Offset(-bounds.X, -bounds.Y);
        _editor.Region = clip.Size == bounds.Size ? null : new Region(clip);
    }

    private void HideEditor()
    {
        _sessionId = 0;
        if (!_editor.Visible)
            return;
        _syncing = true;
        try
        {
            var hadFocus = _editor.Focused;
            _editor.Visible = false;
            if (hadFocus)
                _canvas.Focus();
        }
        finally
        {
            _syncing = false;
        }
    }

    // VM 에는 LF 로 돌려준다(원래 값과 같은 줄바꿈 표기)
    private string EditorText => _editor.Text.ReplaceLineEndings("\n");

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_syncing || _surface is null || _sessionId == 0)
            return;
        _surface.SetEditText(_sessionId, EditorText);
        RenderIfNeeded();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_surface is null || _sessionId == 0)
            return;

        DvEditExit? exit = e.KeyCode switch
        {
            Keys.Enter when !_editor.Multiline || e.Control => DvEditExit.Enter,
            Keys.Tab => e.Shift ? DvEditExit.ShiftTab : DvEditExit.Tab,
            Keys.Up when !_editor.Multiline => DvEditExit.Up,
            Keys.Down when !_editor.Multiline => DvEditExit.Down,
            _ => null
        };

        if (e.KeyCode == Keys.Escape)
        {
            _surface.CancelEdit(_sessionId);
            e.SuppressKeyPress = true;
        }
        else if (exit is { } x)
        {
            _surface.SetEditText(_sessionId, EditorText);
            _surface.CommitEdit(_sessionId, x); // 거절이면 세션 유지(캔버스가 오류 테두리)
            e.SuppressKeyPress = true;
        }

        _canvas.Invalidate();
    }

    // numeric 은 숫자·부호·점만 받는다(편의 — 최종 검증은 코어)
    private void OnEditorKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_surface?.Edit is { IsEditing: true, Kind: DvEditKind.Numeric } && !char.IsControl(e.KeyChar) &&
            !(char.IsDigit(e.KeyChar) || e.KeyChar is '-' or '+' or '.'))
            e.Handled = true;
    }

    private void OnEditorLostFocus(object? sender, EventArgs e)
    {
        if (_syncing || _surface is null || _sessionId == 0 || !_editor.Visible)
            return;
        _surface.SetEditText(_sessionId, EditorText);
        _surface.CommitEdit(_sessionId, DvEditExit.FocusLost);
        _canvas.Invalidate();
    }

    // ── 터치 모드: 물리 키보드 → 코어 가상 키보드
    private void OnCanvasKeyDown(object? sender, KeyEventArgs e)
    {
        if (_surface is null || !_touchMode)
            return;
        DvKey? key = e.KeyCode switch
        {
            Keys.Enter => DvKey.Enter,
            Keys.Back => DvKey.Backspace,
            Keys.Escape => DvKey.Escape,
            Keys.Tab => e.Shift ? DvKey.ShiftTab : DvKey.Tab,
            _ => null
        };
        if (key is { } k)
        {
            _surface.KeyInput(k);
            e.SuppressKeyPress = true;
            _canvas.Invalidate();
        }
    }

    private void OnCanvasKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_surface is null || !_touchMode || char.IsControl(e.KeyChar))
            return;
        _surface.TextInput(e.KeyChar.ToString());
        _canvas.Invalidate();
    }

    /// <summary>
    /// Closes the app (open dialogs close as canceled, and until it is attached again <see cref="DvApp.ShowDialog{TDialog, TResult}"/> returns a canceled result immediately) and releases the animation timer and the edit box font.
    /// The <see cref="App"/> instance itself is not disposed.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to also release managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseApp();
            _animTimer.Dispose();
            _font?.Dispose();
            _buffer?.Dispose();
        }

        base.Dispose(disposing);
    }
}
