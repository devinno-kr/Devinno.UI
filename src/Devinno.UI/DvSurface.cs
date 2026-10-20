using Devinno.UI.Components;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Devinno.UI.App;

namespace Devinno.UI;

/// <summary>
/// A screen object that binds one markup screen to a view model (VM) and handles layout, painting and input.
/// The host (window or control) only needs to pass it a canvas and pointer/key input.
/// </summary>
/// <remarks>
/// <para><b>Creation</b>: the constructor parses the markup immediately, so syntax errors surface as exceptions at construction time. Layout (including reading bindings)
/// first happens on the first <see cref="RenderTo(SKCanvas, float, float, long)"/> or <see cref="Layout(float, float)"/> call.
/// If no theme is given, the default theme (<see cref="DvTheme.Default"/>, a dark theme) is used.
/// For apps made of multiple pages and fixed areas (sidebar, etc.), use <see cref="App.DvApp"/>; the app's root screen (<see cref="App.DvApp.Surface"/>) is also of this type.</para>
/// <para><b>Painting</b>: the host calls <see cref="RenderTo(SKCanvas, float, float, long)"/> with the screen size (canvas units).
/// It re-lays out internally only when the size changed or after <see cref="Invalidate"/>, so it is fine to call it every frame.
/// To reduce unnecessary painting, skip it when <see cref="NeedsRender"/> is <see langword="false"/>,
/// and repaint blinking, inertial scrolling and virtual keyboard animation at the time reported by <see cref="NextAnimationDue"/>.</para>
/// <para><b>Input</b>: pass pointer coordinates to <see cref="PointerDown"/>, <see cref="PointerMove"/>, <see cref="PointerUp"/> and <see cref="PointerWheel"/>
/// in the same coordinate system as the size passed to <see cref="RenderTo(SKCanvas, float, float, long)"/> (top-left origin).
/// A click occurs only when the pressed and released targets are the same. Screen coordinate conversion such as DPI scaling is the host's responsibility.</para>
/// <para><b>Invalidate and Repaint</b>: <see cref="Invalidate"/> rebuilds the tree, bindings and styles from scratch on the next frame (when VM values
/// changed). <see cref="Repaint"/> keeps the layout and only requests a repaint (when data read directly during painting, such as grid cell values or row count,
/// changed; cheaper).</para>
/// <para><b>Editing</b>: pressing a <c>DvInput</c>, a grid input cell, etc. opens an edit session, and <see cref="Edit"/> (<see cref="DvEditState"/>) carries its position, font and kind.
/// While typing, only the session buffer changes; the VM is written once when the session ends. The host chooses one of two modes.
/// (1) Default (<see cref="UseNativeEditor"/> = <see langword="false"/>): the built-in virtual keyboard appears at the bottom of the screen, and physical keyboard input is
/// passed to <see cref="KeyInput"/>/<see cref="TextInput"/>. (2) Native editor (<see cref="UseNativeEditor"/> = <see langword="true"/>):
/// right after rendering, read <see cref="Edit"/>, position a hidden text box there, and use <see cref="SetEditText"/>, <see cref="CommitEdit"/> and <see cref="CancelEdit"/>
/// to return the result.</para>
/// <para><b>Threading</b>: this type is not thread-safe. Call <see cref="RenderTo(SKCanvas, float, float, long)"/>, the input methods, the edit methods and property setters
/// all from one thread (the host's UI/render thread). The exceptions are <see cref="Invalidate"/> (only sets a flag) and <see cref="Repaint"/> (atomic increment),
/// which may be called from other threads such as a polling timer. These two only schedule a repaint, so the host must wake its render loop separately.</para>
/// </remarks>
/// <example>
/// <code>
/// // Create (UI thread)
/// var surface = new DvSurface(markup, vm, DvTheme.Light);
///
/// // Paint, in the host's paint callback
/// var now = Environment.TickCount64;
/// if (surface.NeedsRender)
///     surface.RenderTo(canvas, width, height, now);
/// long? due = surface.NextAnimationDue(now); // schedule a timer to repaint at this time (null = nothing to schedule)
///
/// // Input, from mouse/touch events (canvas coordinates)
/// surface.PointerDown(new SKPoint(x, y));
/// surface.PointerMove(new SKPoint(x, y));
/// surface.PointerUp(new SKPoint(x, y));
/// surface.PointerWheel(new SKPoint(x, y), 1f); // one notch up
///
/// // Forward the physical keyboard in virtual keyboard mode
/// surface.KeyInput(DvKey.Enter);
/// surface.TextInput("a");
///
/// // The VM changed (also allowed from another thread); then wake the host render loop
/// surface.Invalidate();
/// </code>
/// </example>
public sealed partial class DvSurface
{
    private ElementNode _root; // 핫리로드(ReloadMarkup)가 바꾼다
    private readonly object? _vm;
    private DvTheme? _theme;
    private ComponentRegistry? _registry;
    private readonly InteractionState _state = new();
    private LayoutBox? _box;
    private LayoutBox? _pressOrigin;
    private float _w;
    private float _h;

    private volatile bool _dirty = true; // Invalidate() 가 다른 스레드에서 세운다
    private readonly HandlerErrors.ReadScope _reads; // 레이아웃·그리기 중 VM 읽기 예외 알림(재빌드마다 중복 억제를 비움)

    // 칸 — 이 화면 위 한 영역에 붙은 자식 화면(DvApp 루트의 영역·페이지 칸들).
    // 레이아웃 때 통째 교체(다른 스레드의 Invalidate·Repaint 가 늘 완성된 한 벌을 본다).
    // Modal = 다이얼로그 층(맨 뒤에 아래→위) — 아래 칸 위에 겹쳐 그리고, 맨 위 모달 칸 직전에 막(scrim)을 깐다.
    internal readonly record struct ChildSlot(SKRect Rect, DvSurface Child, bool Modal = false);
    private volatile ChildSlot[] _slots = Array.Empty<ChildSlot>();
    private DvSurface? _capture; // 누른 칸의 자식 — 떼기까지 이동·뗌을 그 칸이 받는다

    /// <summary>The side that defines the slot layout (the DvApp root screen). If null, there are no slots.</summary>
    internal ISurfaceComposer? Composer { get; set; }

    /// <summary>The currently attached slots (for tests).</summary>
    internal IReadOnlyList<ChildSlot> Slots => _slots;

    // DvKnob 회전 드래그 — 미리보기 값은 LayoutBox 에만, VM 은 pointer-up 에서 1회 커밋
    private IValueDrag? _knobDrag; // 노브·슬라이더 끌기
    private SKPoint _knobShift; // 노브 좌표계(스크롤 상자 안이면 오프셋 반영) − 화면 좌표

    /// <summary>Creates a screen with the default theme. User components are not used.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model in which bindings and event handlers are looked up. May be <see langword="null"/> for a screen without bindings.</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The markup's XML syntax is invalid.</exception>
    public DvSurface(string markup, object? vm)
        : this(markup, vm, theme: null, registry: null)
    {
    }

    /// <summary>Creates a screen with the specified theme.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model in which bindings and event handlers are looked up (may be <see langword="null"/>).</param>
    /// <param name="theme">The theme used to resolve <c>token(...)</c> values. If <see langword="null"/>, <see cref="DvTheme.Default"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The markup's XML syntax is invalid.</exception>
    public DvSurface(string markup, object? vm, DvTheme? theme)
        : this(markup, vm, theme, registry: null)
    {
    }

    /// <summary>Creates a screen with the default theme and a user component (<c>DvComponent</c>) registry attached.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model in which bindings and event handlers are looked up (may be <see langword="null"/>).</param>
    /// <param name="registry">The registry holding the user components used in the markup (may be <see langword="null"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The markup's XML syntax is invalid.</exception>
    public DvSurface(string markup, object? vm, ComponentRegistry? registry)
        : this(markup, vm, theme: null, registry)
    {
    }

    /// <summary>Creates a screen with the specified theme and user component registry. All other constructors chain to this one.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model in which bindings and event handlers are looked up (may be <see langword="null"/>).</param>
    /// <param name="theme">The theme. If <see langword="null"/>, <see cref="DvTheme.Default"/>.</param>
    /// <param name="registry">The user component registry (may be <see langword="null"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The markup's XML syntax is invalid.</exception>
    public DvSurface(string markup, object? vm, DvTheme? theme, ComponentRegistry? registry)
    {
        _root = MarkupParser.Parse(markup);
        _vm = vm;
        _reads = new HandlerErrors.ReadScope(this);
        _theme = theme;
        _registry = registry;
        if (vm is Controls.IDvControlHost host)
        {
            try { host.Attach(new Controls.DvControlLookup(this, _root)); }
            catch (Exception ex)
            {
                // 생성자 안에서는 아직 아무도 HandlerError 를 구독할 수 없다 — Trace 는 지금, 이벤트는 첫 Layout 에서(칸 화면이면 루트에도 닿도록 ParentSurface 연결 뒤).
                HandlerErrors.Report(null, ex, "attach", vm.GetType().Name, null);
                _pendingAttachError = new DvHandlerErrorEventArgs(HandlerErrors.Unwrap(ex), "attach", vm.GetType().Name, null);
            }
        }
    }

    private DvHandlerErrorEventArgs? _pendingAttachError; // 생성자의 Attach 예외 — 첫 Layout 에서 한 번 HandlerError 로

    // CSS transition — 상자마다 지금 보이는 값(프레임 사이에 유지). 진행 중이면 다음 프레임을 요청하고 그 프레임은 전체를 그린다
    private readonly Painting.MotionStore _motion = new();

    // DvApp 페이지 전환 움직임(루트만) — 시작 시각은 첫 그리기 때 정한다(호스트의 시각 기준)
    private sealed class PageMotion(DvSurface old, DvSurface @new, App.DvPageTransition kind)
    {
        public DvSurface Old { get; } = old;
        public DvSurface New { get; } = @new;
        public App.DvPageTransition Kind { get; } = kind;
        public long? Start;
    }

    internal const int PageMotionMs = 200;
    private PageMotion? _pageMotion;
    private DvSurface? _lastPage;

    // 페이지 칸 — 옛 페이지와 새 페이지를 함께(Fade: 새 것을 겹쳐 서서히 진하게, Slide: 새 것이 오른쪽에서 밀고 들어옴). 끝나면 새 페이지만
    private void PaintPageMotion(SKCanvas canvas, SKRect slot, PageMotion pm, long now)
    {
        pm.Start ??= now;
        var x = Math.Clamp((now - pm.Start.Value) / (float)PageMotionMs, 0f, 1f);
        var t = Styling.EasingMath.Apply(Styling.Easing.EaseOut, x);
        if (x >= 1f)
            _pageMotion = null;

        void Draw(DvSurface page, float dx, byte alpha)
        {
            var saved = canvas.Save();
            var visible = SKRect.Intersect(slot, new SKRect(slot.Left + dx, slot.Top, slot.Right + dx, slot.Bottom));
            if (!visible.IsEmpty)
            {
                canvas.ClipRect(visible);
                canvas.Translate(slot.Left + dx, slot.Top);
                if (alpha < 255)
                {
                    using var layer = new SKPaint { Color = SKColors.White.WithAlpha(alpha) };
                    canvas.SaveLayer(layer);
                }
                page.RenderTo(canvas, slot.Width, slot.Height, now);
            }
            canvas.RestoreToCount(saved);
        }

        if (x >= 1f)
            Draw(pm.New, 0, 255);
        else if (pm.Kind == App.DvPageTransition.Slide)
        {
            Draw(pm.Old, -slot.Width * t, 255);
            Draw(pm.New, slot.Width * (1 - t), 255);
        }
        else
        {
            Draw(pm.Old, 0, 255);
            Draw(pm.New, 0, (byte)MathF.Round(t * 255f));
        }
    }

    /// <summary>
    /// Hot reload (<see cref="App.DvApp.HotReload"/>): swaps in new markup source. The VM and handle objects stay the same, and handles are re-attached to the new elements
    /// (<c>OnLoaded</c> is not called again). Per-element state remembered by the screen (tab index without a value, collapsible panels, tree expansion, standalone radios) resets because the elements are new.
    /// If the XML is invalid, nothing is changed and the exception is returned. On the input thread.
    /// </summary>
    internal Exception? ReloadMarkup(string markup)
    {
        ElementNode root;
        try
        {
            root = MarkupParser.Parse(markup);
        }
        catch (Exception ex)
        {
            return ex;
        }

        CommitPendingEdit(); // 입력 중이던 값은 옛 화면에서 먼저 반영
        CancelPress();
        _root = root;
        _controlsByNode.Clear();
        _controlBoxes = null;
        _tabIndex.Clear();
        lock (_expanders)
            _expanders.Clear();
        lock (_treeOpen)
            _treeOpen.Clear();
        lock (_radioGate)
            foreach (var key in _radioGroups.Keys.Where(k => k is ElementNode).ToList())
                _radioGroups.Remove(key);
        if (_vm is Controls.IDvControlHost host)
        {
            try { host.Attach(new Controls.DvControlLookup(this, _root)); }
            catch (Exception ex) { HandlerErrors.Report(this, ex, "attach", _vm.GetType().Name, null); }
        }
        Invalidate();
        return null;
    }

    private readonly Dictionary<ElementNode, Controls.DvControl> _controlsByNode = new();

    /// <summary>Control handles attached to this screen (keyed by element node). Empty unless the VM has handles.</summary>
    internal IReadOnlyDictionary<ElementNode, Controls.DvControl> ControlsByNode => _controlsByNode;

    internal void RegisterControl(ElementNode node, Controls.DvControl control) => _controlsByNode[node] = control;

    /// <summary>
    /// Signals that the VM changed. On the next <see cref="RenderTo(SKCanvas, float, float, long)"/> (or <see cref="Layout(float, float)"/>), the tree, bindings and styles are
    /// rebuilt from scratch. Call it when the VM was changed outside a click handler (polling, communication callbacks, etc.).
    /// </summary>
    /// <remarks>
    /// <para>It only sets a flag, so it may be called from other threads. It only schedules a repaint, though, so the host's render loop must be woken separately.</para>
    /// <para>Scroll positions, the pressed target and the edit session are carried over to the same position in the rebuilt tree. The value being edited takes precedence over the VM value.</para>
    /// <para>On the root screen of <see cref="App.DvApp"/>, it is also forwarded to the currently attached area and page screens, which are rebuilt too (the same way as <see cref="Repaint"/>).</para>
    /// </remarks>
    public void Invalidate()
    {
        _dirty = true;
        foreach (var s in _slots)
            s.Child.Invalidate();
        Wake?.Invoke();
    }

    /// <summary>
    /// Marks only this screen to be rebuilt on the next layout. Does not wake the host; used when called inside a frame already in progress (DvApp's Post queue processing).
    /// </summary>
    internal void MarkDirty() => _dirty = true;

    /// <summary>Makes only this screen re-fit on the next frame, leaving child slots alone (a DvApp queue request; other threads allowed).</summary>
    internal void RequestFrame()
    {
        _dirty = true;
        Wake?.Invoke();
    }

    /// <summary>
    /// Repaints the next frame without re-layout. Use it when data read directly during painting, such as grid cell values or row count, changed
    /// (cheaper than <see cref="Invalidate"/>). Grid summary rows are recomputed too.
    /// </summary>
    /// <remarks>
    /// <para>If layout (tree, bindings, styles) must be redone as well, use <see cref="Invalidate"/>. For example, if you replaced the list that a grid's <c>items</c>
    /// points to, <see cref="Invalidate"/> is required.</para>
    /// <para>It increments a request number atomically, so it may be called from other threads, and requests arriving during painting are not lost.
    /// On the <see cref="App.DvApp"/> root screen, it is also forwarded to the attached area and page screens.</para>
    /// </remarks>
    public void Repaint()
    {
        Interlocked.Increment(ref _repaintRequested);
        foreach (var s in _slots)
            s.Child.Repaint();
        Wake?.Invoke();
    }

    /// <summary>Hook that wakes the host on Invalidate/Repaint (from any thread). Set by DvApp, which handles the adapter connection.</summary>
    internal volatile Action? Wake;

    /// <summary>Changes the theme at run time. The same screen is rebuilt on the next Layout, so scroll, edit, press and control values written from code are kept (same rule as Strings).</summary>
    internal void SetTheme(DvTheme? theme)
    {
        if (ReferenceEquals(_theme, theme))
            return;
        _theme = theme;
        Invalidate();
    }

    /// <summary>Changes the component registry at run time. Rebuilds the same way as <see cref="SetTheme"/>.</summary>
    internal void SetRegistry(ComponentRegistry? registry)
    {
        if (ReferenceEquals(_registry, registry))
            return;
        _registry = registry;
        Invalidate();
    }

    // Repaint 요청 번호 — 그리는 도중(다른 스레드) 들어온 요청이 프레임 끝의 _needsRender = false 에 지워지지 않게
    // 그리기 시작 시점 번호까지만 처리한 것으로 기록한다.
    private int _repaintRequested;
    private int _repaintPainted;

    /// <summary>The current layout tree (for tests).</summary>
    internal LayoutBox? Root => _box;

    internal DvTheme? Theme => _theme;
    internal ComponentRegistry? Registry => _registry;

    // 레이아웃 외의 시각 변화(hover/press/드래그 미리보기). 레이아웃 변화는 _dirty 가 맡는다.
    private bool _needsRender = true;

    /// <summary>
    /// Gets whether the next <see cref="RenderTo(SKCanvas, float, float, long)"/> may produce a different image from the last frame (<see langword="true"/>).
    /// If <see langword="false"/>, the host can skip painting and keep the last frame.
    /// </summary>
    /// <remarks>
    /// Includes <see cref="Invalidate"/>/<see cref="Repaint"/> requests, hover/press changes, knob drags, scrolling, edit input, virtual keyboard animation,
    /// and, on the <see cref="App.DvApp"/> root screen, changes in the area and page screens. Time-dependent images such as blinking are not included; check them separately with <see cref="NextAnimationDue"/>.
    /// Window size changes are not included either, so the host must repaint itself when the size changes.
    /// </remarks>
    public bool NeedsRender =>
        _dirty || _needsRender || Volatile.Read(ref _repaintRequested) != _repaintPainted || (_kb?.IsAnimating(Clock()) ?? false) ||
        ChildNeedsRender() || (Composer?.HasPending ?? false); // DvApp 대기열 — 스냅숏 뒤에 들어온 요청도 다음 프레임에

    private bool ChildNeedsRender()
    {
        foreach (var s in _slots)
        {
            if (s.Child.NeedsRender)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the next time at which the image changes over time. The host can set a timer for that time and repaint.
    /// </summary>
    /// <remarks>
    /// Computes the next blink phase change of blinking widgets (<c>DvLamp blink</c>, a <c>DvDataGrid</c> with blinking lamp cells, a <c>DvAlarmBanner</c>/<c>DvAlarmList</c> with unacknowledged alarms or a communication fault);
    /// returns 50ms later (20 frames per second) if a process diagram with flow animation enabled (<c>DvProcess flow-animation</c>) has a flowing pipe,
    /// and the next frame (16ms later) while inertial scrolling or a virtual keyboard slide is in progress.
    /// On the <see cref="App.DvApp"/> root screen, it compares with the area and page screens and returns the earliest. It is based on the last layout result, so call it right after <see cref="RenderTo(SKCanvas, float, float, long)"/>.
    /// </remarks>
    /// <param name="timestampMs">The current time (ms). Must use the same base as the value passed to <see cref="RenderTo(SKCanvas, float, float, long)"/> (e.g. <see cref="Environment.TickCount64"/>).</param>
    /// <returns>The time to repaint (same base as <paramref name="timestampMs"/>, greater than it). <see langword="null"/> if nothing changes over time.</returns>
    public long? NextAnimationDue(long timestampMs)
    {
        long? due = _box is null ? null : NextDue(_box, timestampMs);
        if (_fling is not null || (_kb?.IsAnimating(Clock()) ?? false) || (_holdBox is not null && !_holdFired) || _motion.Active || _pageMotion is not null || _expAnims.Count > 0)
            due = due is null ? timestampMs + 16 : Math.Min(due.Value, timestampMs + 16); // 관성·키보드 슬라이드·CSS 전환 중엔 다음 프레임
        foreach (var s in _slots)
        {
            var child = s.Child.NextAnimationDue(timestampMs);
            due = due is null ? child : child is null ? due : Math.Min(due.Value, child.Value);
        }
        if (Composer?.NextDue(timestampMs) is { } idle) // DvApp 무입력 알림 시각 — 그때 깨어나 프레임을 돌린다
            due = due is null ? idle : Math.Min(due.Value, idle);
        return due;
    }

    private static long? NextDue(LayoutBox box, long ts)
    {
        long? due = null;
        var interval = BlinkInterval(box);
        if (interval > 0)
            due = (ts / interval + 1) * interval;

        foreach (var c in box.Children)
        {
            var d = NextDue(c, ts);
            if (d is not null && (due is null || d < due))
                due = d;
        }

        return due;
    }

    // Painter 의 점멸 조건과 같아야 한다(램프: on && blink, 알람: 통신 이상 또는 미확인 1건 이상)
    private static int BlinkInterval(LayoutBox box) => box.Node.Tag switch
    {
        "DvLamp" when box.LampOn && box.LampBlink => box.BlinkIntervalMs,
        "DvDataGrid" when box.Grid is { HasBlink: true } => Painter.AlarmBlinkMs, // 점멸 램프 셀(공통 위상)
        "DvAlarmBanner" or "DvAlarmList"
            when !string.IsNullOrEmpty(box.CommAlarm) || box.AlarmItems.Any(a => !a.Acked) => Painter.AlarmBlinkMs,
        "DvProcess" when box.Process is { FlowAnimation: true } p && p.AnyFlowing => Painter.FlowFrameMs, // 흐름 애니메이션(초당 20프레임 상한)
        _ => 0
    };

    private Localization.DvStrings? _strings;

    /// <summary>
    /// Gets or sets the localized string table used for <c>{L10n.key}</c>/<c>@key</c> in markup and for built-in texts. The default is <see langword="null"/>, in which case built-in texts (validation rejection messages, etc.) show the default texts in code.
    /// </summary>
    /// <remarks>
    /// Setting it re-lays out on the next frame, and afterwards it automatically calls <see cref="Invalidate"/> whenever the table raises <see cref="Localization.DvStrings.Changed"/> (language switch, entries added to the current language).
    /// Set it on the UI thread. With <see cref="App.DvApp"/>, the app's single <see cref="App.DvApp.Strings"/> is passed to all screens.
    /// </remarks>
    public Localization.DvStrings? Strings
    {
        get => _strings;
        set
        {
            if (ReferenceEquals(_strings, value))
                return;
            if (_strings is not null)
                _strings.Changed -= OnStringsChanged;
            _strings = value;
            if (_strings is not null)
                _strings.Changed += OnStringsChanged;
            Invalidate();
        }
    }

    private void OnStringsChanged(object? sender, EventArgs e) => Invalidate();

    /// <summary>
    /// Fits the layout to the given size. <see cref="RenderTo(SKCanvas, float, float, long)"/> always calls it first, so the host usually does not need to call it directly.
    /// </summary>
    /// <remarks>
    /// After <see cref="Invalidate"/> or the first time, it rebuilds the tree, bindings and styles; if only the size changed, it recomputes placement only; otherwise it does nothing.
    /// On the <see cref="App.DvApp"/> root screen, it also fits the area and page screen layouts, the edit session position (<see cref="Edit"/>) and open popup positions. Call it only on the UI thread.
    /// </remarks>
    /// <param name="w">Screen width (canvas units, usually px).</param>
    /// <param name="h">Screen height (canvas units, usually px).</param>
    public void Layout(float w, float h)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.BeforeLayout(); // DvApp 루트 — 대기 중인 전환·영역·설정을 이 스레드(입력)에서 적용
        if (_pendingAttachError is { } attachError)
        {
            _pendingAttachError = null;
            RaiseHandlerError(attachError);
        }
        var prevRead = HandlerErrors.EnterRead(_reads); // 레이아웃 중 VM 읽기 예외는 이 화면의 HandlerError 로
        try
        {
            LayoutCore(w, h);
            FlushPrunedSelections(_box);
        }
        finally
        {
            HandlerErrors.ExitRead(prevRead);
        }
    }

    private void LayoutCore(float w, float h)
    {
        if (!_dirty && _box is not null && _w == w && _h == h)
        {
            if (Composer is not null)
            {
                SyncChildren(); // 루트는 칸 크기·연결을 매 프레임 확인(페이지 전환·영역 교체)
                PlacePopups(); // 칸이 옮겨졌거나 칸 화면이 다시 만들어졌으면 칸 팝업(드롭다운 등)도 앵커를 따라간다
            }
            PublishEdit(); // 칸 화면이 바뀌었을 수 있다(페이지 전환)
            return;
        }

        _w = w;
        _h = h;
        _layoutVersion++; // 부분 다시 그리기 — 배치가 바뀐 프레임은 전체
        if (!_dirty && _box is not null)
        {
            // 크기만 바뀜(창 리사이즈) — 트리·바인딩·스타일은 그대로, 배치만 다시. 상호작용 대상도 같은 박스 그대로.
            LayoutEngine.Rearrange(_box, w, h);
            _knobDrag?.Retarget(_knobDrag.Box); // 노브 위치가 옮겨졌으면 기준 각도 재설정
        }
        else
        {
            _dirty = false;
            _reads.Seen.Clear(); // 재빌드 — 같은 VM 읽기 예외도 다시 한 번 알린다
            var old = _box;
            var engine = new LayoutEngine(_theme, _strings);
            if (_controlsByNode.Count > 0)
                engine.ControlOf = n => _controlsByNode.GetValueOrDefault(n);
            if (_tabIndex.Count > 0)
                engine.TabIndexOf = n => _tabIndex.TryGetValue(n, out var i) ? i : null;
            engine.RadioGroupOf = RadioGroupValue;
            engine.ExpanderOf = ExpanderValue;
            engine.ExpanderRevealOf = ExpanderReveal;
            _expSeen.Clear();
            engine.TreeExpandedOf = TreeExpanded;
            _box = engine.Layout(_root, _vm, w, h, _registry);
            PruneExpanderState();
            Remap(old);
        }

        SyncChildren();

        SyncEditState();
        PublishEdit();
        PlacePopups();
        BuildControlBoxes();
    }

    // 핸들 → (박스, 조상 스크롤 상자들). 레이아웃(재빌드·리사이즈) 끝에 새 사전으로 통째 교체 — 다른 스레드의 읽기가 늘 완성된 한 벌을 본다.
    // 스크롤 오프셋은 저장하지 않는다(스크롤만 바뀌어도 읽을 때 현재 값을 더한다).
    private volatile Dictionary<Controls.DvControl, (LayoutBox Box, LayoutBox[] Scrollers)>? _controlBoxes;

    private void BuildControlBoxes()
    {
        if (_controlsByNode.Count == 0 || _box is null)
            return;
        var table = new Dictionary<Controls.DvControl, (LayoutBox, LayoutBox[])>();
        var scrollers = new List<LayoutBox>();
        void Walk(LayoutBox b)
        {
            if (_controlsByNode.TryGetValue(b.Origin, out var c))
                table.TryAdd(c, (b, scrollers.ToArray()));
            if (b.IsScroller)
                scrollers.Add(b);
            foreach (var ch in b.Children)
                Walk(ch);
            if (b.IsScroller)
                scrollers.RemoveAt(scrollers.Count - 1);
        }
        Walk(_box);
        _controlBoxes = table;

        // 값 핸들 — 다시 만든 화면에서 값이 바뀌었으면(VM 속성을 코드가 직접 바꿈 등) 바뀜 이벤트, 배치 전에 쓴 선택 번호 적용
        foreach (var (c, _) in table)
        {
            if (c.Slot is null || !ReferenceEquals(c.Surface, this))
                continue;
            c.ApplyPending();
            c.ObserveValue(byUser: false);
        }
    }

    /// <summary>After a user action wrote a value, raises the changed event (<c>ByUser</c>) on that element's value handle.</summary>
    internal void NotifyValueWritten(LayoutBox box)
    {
        if (box.Node.Tag == "DvRadio")
        {
            ObserveRadios(byUser: true); // 하나를 고르면 같은 묶음의 다른 버튼도 꺼진다
            return;
        }
        if (_controlsByNode.TryGetValue(box.ValueOrigin ?? box.Origin, out var c) && ReferenceEquals(c.Surface, this))
            c.ObserveValue(byUser: true);
    }

    /// <summary>Checks all radio handles of this screen (events only for those that changed).</summary>
    internal void ObserveRadios(bool byUser, Controls.DvControl? except = null)
    {
        foreach (var c in _controlsByNode.Values)
            if (c is Controls.DvRadio && !ReferenceEquals(c, except) && ReferenceEquals(c.Surface, this))
                c.ObserveValue(byUser);
    }

    /// <summary>Top-left position where this screen is drawn, relative to the window (top-level screen). For a slot screen (DvApp area/page), the root sets it to the slot position.</summary>
    internal SKPoint WindowOffset { get; set; }

    /// <summary>Whether a layout has completed (the handle box table exists).</summary>
    internal bool HasControlBoxes => _controlBoxes is not null;

    /// <summary>The handle's current box and the sum of ancestor scroll offsets. <see langword="false"/> before layout or if there is no box (<c>visible</c> false, etc.).</summary>
    internal bool TryGetBox(Controls.DvControl c, out LayoutBox box, out SKPoint scroll)
    {
        if (_controlBoxes is { } t && t.TryGetValue(c, out var e))
        {
            float sx = 0, sy = 0;
            foreach (var s in e.Scrollers)
            {
                sx += s.ScrollX;
                sy += s.ScrollY;
            }
            box = e.Box;
            scroll = new SKPoint(sx, sy);
            return true;
        }
        box = null!;
        scroll = default;
        return false;
    }

    // 칸 구성 갱신 — DvApp 루트면 Composer 가 정한 칸들(아니면 칸 없음).
    // 칸마다 자식 화면을 연결(창 서피스·창 기준 위치·편집기 모드)하고 그 크기로 레이아웃한다. 빠진 자식은 남은 편집을 커밋하고 뗀다.
    private void SyncChildren()
    {
        if (Composer is not { } composer)
            return;
        var next = composer.Compose(_w, _h).Select(c => new ChildSlot(c.Rect, c.Child, c.Modal)).ToArray();

        foreach (var old in _slots)
        {
            if (!Array.Exists(next, n => ReferenceEquals(n.Child, old.Child)))
                old.Child.DetachFromParent();
        }

        // 페이지가 바뀜 — 앱이 정한 움직임이 있으면 옛 페이지와 함께 그린다(옛 화면은 앱이 보관 중이라 그대로 그릴 수 있다)
        var page = composer.PageSurface;
        if (!ReferenceEquals(page, _lastPage))
        {
            var kind = composer.TakePageTransition();
            _pageMotion = _lastPage is not null && page is not null && kind != App.DvPageTransition.None
                ? new PageMotion(_lastPage, page, kind)
                : null;
            _lastPage = page;
        }
        if (_capture is not null && !Array.Exists(next, n => ReferenceEquals(n.Child, _capture)))
            _capture = null; // 누른 칸의 화면이 바뀜(전환) — 남은 이동·뗌은 아무도 받지 않는다

        foreach (var s in next)
        {
            var child = s.Child;
            child.UseNativeEditor = UseNativeEditor;
            child.ParentSurface = this;
            child.IsChildSurface = true;
            child.IsOverlay = s.Modal;
            child.WindowOffset = WindowOffset + s.Rect.Location; // 자식 핸들의 Bounds 를 창 기준으로
            if (!s.Rect.IsEmpty)
            {
                child.Layout(s.Rect.Width, s.Rect.Height);
                if (child.AfterFirstSlotLayout is { } first && first())
                    child.AfterFirstSlotLayout = null; // true 를 돌려준 뒤로는 부르지 않는다(그 전까지는 칸 배치마다)
            }
        }
        _slots = next;
    }

    /// <summary>Callback invoked right after this screen is attached to a slot and laid out at the slot size. Called on every slot placement until it returns true; on true the root clears it.
    /// Used by the built-in input box to "start editing once it is on top". After this, the root loads the edit in the same frame.</summary>
    internal Func<bool>? AfterFirstSlotLayout { get; set; }

    // 칸에서 빠짐(페이지 전환·영역 교체) — 이 화면의 편집을 커밋하고 띄운 팝업을 닫은 뒤(창 서피스 기준) 연결을 끊는다.
    private void DetachFromParent()
    {
        CommitPendingEdit();
        ClearHover();
        _state.Press = null;
        _pressOrigin = null;
        ParentSurface = null;
    }

    /// <summary>
    /// The intrinsic size of this screen's root (style width/height, or the content size if none). If there is no tree or it must be rebuilt, lays out first
    /// (at the last size, or <paramref name="w"/>/<paramref name="h"/> the first time). Used to compute DvApp area and dialog slot sizes.
    /// If <paramref name="availableWidth"/> is given, text is wrapped to that width for measuring (dialog = window width − 32), matching the wrapping used when placed at that slot width.
    /// </summary>
    internal SKSize MeasureRoot(float w, float h, float? availableWidth = null)
    {
        if (_box is null || _dirty)
            Layout(_box is null ? w : _w, _box is null ? h : _h);
        var (mw, mh) = LayoutEngine.MeasureBox(_box!, availableWidth);
        return new SKSize(mw, mh);
    }

    private ChildSlot? SlotAt(SKPoint p)
    {
        foreach (var s in _slots)
        {
            if (s.Rect.Contains(p.X, p.Y))
                return s;
        }
        return null;
    }

    private ChildSlot? SlotOf(DvSurface child)
    {
        foreach (var s in _slots)
        {
            if (ReferenceEquals(s.Child, child))
                return s;
        }
        return null;
    }

    // 누름 중이면 누른 칸(그 칸이 사라졌으면 없음), 아니면 점 아래 칸
    private ChildSlot? InputSlot(SKPoint p) => _capture is { } c ? SlotOf(c) : SlotAt(p);

    // ── 모달(다이얼로그) 입력 — 다이얼로그가 요청돼 있으면 아래 칸은 포인터·휠을 받지 않는다(spec §6.3) ──

    /// <summary>
    /// Whether input to the slots below must be blocked (DvApp root only): a dialog is on the request stack, or a close was requested but it is still in its slot and being drawn.
    /// </summary>
    private bool Blocked => Composer is { } c && (_slots is [.., { Modal: true }] || c.BlocksBelow);

    // 막혔을 때 입력을 받을 칸 — 맨 위(마지막) 칸이 요청 스택 맨 위 다이얼로그의 칸일 때만. 아니면 null(전부 삼킴 — 적용 전·닫힘 적용 전).
    private ChildSlot? ModalSlot()
    {
        var slots = _slots;
        return slots is [.., { Modal: true } top] && Composer?.ModalInput is { } input && ReferenceEquals(top.Child, input) ? top : null;
    }

    // 막혔을 때 점 아래 칸 — 맨 위 다이얼로그 칸 안이면 그 칸, 밖이면 null(삼킴)
    private ChildSlot? ModalSlotAt(SKPoint p) => ModalSlot() is { } s && s.Rect.Contains(p.X, p.Y) ? s : null;

    // 막힌 뒤에도 남은 누름(다이얼로그가 뜨기 전에 누른 아래 칸)은 버린다 — 떼기가 클릭을 만들지 않게 그 칸의 press 도 푼다
    private void DropBlockedCapture()
    {
        if (_capture is not { } c || (ModalSlot() is { } top && ReferenceEquals(top.Child, c)))
            return;
        c.CancelPress();
        _capture = null;
    }

    /// <summary>Discards the press in progress (press, knob or scroll drag) and hover without a click, when a dialog opened on top and this slot lost input.</summary>
    internal void CancelPress()
    {
        if (PressVisible(_state.Press))
            _needsRender = true;
        _state.Press = null;
        _pressOrigin = null;
        if (_knobDrag is not null)
        {
            _knobDrag = null;
            Invalidate(); // 드래그 미리보기 값을 버리고 VM 값으로 다시 빌드
        }
        _scroll = null;
        _thumb = null;
        ClearHover();
    }

    private static SKPoint ToChild(SKPoint p, ChildSlot s) => new(p.X - s.Rect.Left, p.Y - s.Rect.Top);

    // 상호작용 중 재빌드(폴링 Invalidate 등) — 옛 트리 박스를 계속 가리키면 노브가 멈추고 클릭(down/up 동일성)이 사라진다.
    // 새 트리의 같은 위치 박스로 옮긴다.
    private void Remap(LayoutBox? old)
    {
        if (old is not null && _box is not null)
        {
            if (_knobDrag is not null)
            {
                if (Counterpart(old, _box, _knobDrag.Box) is { } knob)
                    _knobDrag.Retarget(knob);
                else
                    _knobDrag = null;
            }

            _pressOrigin = _pressOrigin is null ? null : Counterpart(old, _box, _pressOrigin);
            _holdBox = _holdBox is null ? null : Counterpart(old, _box, _holdBox);
            _state.Press = _state.Press is null ? null : Counterpart(old, _box, _state.Press);
            _state.Hover = _state.Hover is null ? null : Counterpart(old, _box, _state.Hover);
            RemapScroll(old, _box);
            RemapGrids(old, _box);
            RemapEdit(old, _box);
            RemapPopups(old, _box);
        }
    }

    /// <summary>
    /// Paints the screen at time 0. Blinking widgets are always drawn in the same phase, so this is for snapshots and tests;
    /// real hosts should use <see cref="RenderTo(SKCanvas, float, float, long)"/>.
    /// </summary>
    /// <param name="canvas">The canvas to paint on. It is cleared to white before painting.</param>
    /// <param name="w">Screen width (canvas units).</param>
    /// <param name="h">Screen height (canvas units).</param>
    public void RenderTo(SKCanvas canvas, float w, float h) => RenderTo(canvas, w, h, 0L);

    /// <summary>
    /// Re-lays out if needed, then paints the whole screen to the canvas. Called on the UI thread from the host's paint callback.
    /// </summary>
    /// <remarks>
    /// <para>Order: layout (<see cref="Layout(float, float)"/>) → advance inertial scrolling → clear the canvas to white → screen, slot screens (DvApp areas/pages), popups → virtual keyboard.
    /// If the virtual keyboard covers the edit target, the whole screen is drawn shifted up.</para>
    /// <para>Afterwards <see cref="NeedsRender"/> becomes <see langword="false"/> and <see cref="Edit"/> is updated for this frame.
    /// Hosts using a native editor should read <see cref="Edit"/> right after this call.</para>
    /// <para>It follows any transform already set on the canvas, so with DPI scaling the host applies <c>canvas.Scale</c> beforehand and passes <paramref name="w"/>/<paramref name="h"/>
    /// in the same logical units as pointer coordinates.</para>
    /// </remarks>
    /// <param name="canvas">The canvas to paint on.</param>
    /// <param name="w">Screen width (canvas units), the same units as pointer coordinates.</param>
    /// <param name="h">Screen height (canvas units).</param>
    /// <param name="timestampMs">The current time (ms), used to compute blink phases. Use the same base as <see cref="NextAnimationDue"/> (e.g. <see cref="Environment.TickCount64"/>).</param>
    public void RenderTo(SKCanvas canvas, float w, float h, long timestampMs)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        var prevRead = HandlerErrors.EnterRead(_reads); // 그리기 중(그리드 셀·정렬·요약) VM 읽기 예외도 이 화면으로 — 재빌드 전까지 같은 대상은 한 번만
        try
        {
            RenderCore(canvas, w, h, timestampMs);
            FlushPrunedSelections(_box);
        }
        finally
        {
            HandlerErrors.ExitRead(prevRead);
        }
    }

    private void RenderCore(SKCanvas canvas, float w, float h, long timestampMs)
    {
        var repaint = Volatile.Read(ref _repaintRequested);
        var changed = NeedsRender; // 레이아웃이 플래그를 지우기 전에 — 시간 말고 바뀐 것이 있는 프레임은 전체
        Layout(w, h);
        AdvanceExpanders(timestampMs);
        TickHold();
        if (repaint != _repaintPainted && _box is not null)
            MarkGridData(_box); // 앱이 그리드 데이터를 바꿨다 — 요약 다시 계산
        AdvanceFling();
        SyncEditState(); // 스크롤·관성으로 대상이 움직였을 수 있다
        SyncKeyboard();
        var pan = KeyboardPan;
        var area = PartialArea(changed, w, h, pan);
        LastRenderRect = area is { } a ? (a.IsEmpty ? SKRectI.Empty : SKRectI.Ceiling(a, outwards: true)) : null;
        if (area is { IsEmpty: true })
        {
            FinishFrame(repaint, w, h, pan); // 시간으로 바뀌는 것이 없다 — 지난 그림 그대로
            return;
        }

        var clipped = area is not null;
        if (clipped)
        {
            canvas.Save();
            canvas.ClipRect(SKRect.Create(LastRenderRect!.Value.Left, LastRenderRect.Value.Top, LastRenderRect.Value.Width, LastRenderRect.Value.Height));
        }

        if (!IsOverlay)
            canvas.Clear(SKColors.White); // 다이얼로그 칸은 지우지 않는다 — 둥근 모서리 밖·그림자 자리에 아래(막)가 비친다
        canvas.Save();
        if (pan > 0)
            canvas.Translate(0, -pan); // 키보드가 입력 대상을 가리지 않게 화면을 올림
        if (_box is not null)
        {
            _motion.BeginFrame();
            new Painter(_theme, _strings) { MotionState = _motion }.Paint(canvas, _box, _vm, _state, timestampMs);
            _motion.EndFrame();
        }

        var slots = _slots;
        for (var i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s.Modal && i == slots.Length - 1)
                PaintScrim(canvas, w, h + pan); // 맨 위 다이얼로그 바로 아래 창 전체 막 한 장(아래 다이얼로그도 어두워진다). pan 만큼 올라가도 아래까지 덮게
            if (s.Rect.IsEmpty)
                continue;
            if (_pageMotion is { } pm && ReferenceEquals(s.Child, pm.New))
            {
                PaintPageMotion(canvas, s.Rect, pm, timestampMs);
                continue;
            }
            canvas.Save();
            canvas.ClipRect(s.Modal ? OverlayClip(s) : s.Rect);
            canvas.Translate(s.Rect.Left, s.Rect.Top);
            s.Child.RenderTo(canvas, s.Rect.Width, s.Rect.Height, timestampMs); // 칸 크기로 레이아웃(바뀐 게 없으면 그대로) 후 그림
            canvas.Restore();
        }
        PublishEdit(); // 칸 화면의 스크롤 등으로 바뀐 세션 위치
        PaintPopups(canvas); // 팝업도 키보드 pan 과 함께 올라간다(팝업 칸 편집)
        canvas.Restore();
        SyncKeyboard();
        PaintKeyboard(canvas, w, h);
        if (clipped)
            canvas.Restore();
        FinishFrame(repaint, w, h, pan);
    }

    private void FinishFrame(int repaint, float w, float h, float pan)
    {
        _needsRender = false;
        _repaintPainted = repaint;
        if (!IsChildSurface)
            _painted = (FrameKey(), w, h, pan);
    }

    /// <summary>
    /// Gets or sets whether the host keeps the previous frame's image instead of clearing it (allows partial repaint). Default <see langword="false"/> = always paints the whole screen.
    /// </summary>
    /// <remarks>
    /// <para>If <see langword="true"/>, frames that change only with time (process diagram flow animation, lamp/alarm/grid blinking) repaint only the clipped region containing those boxes.
    /// Inside that region the background, pages, popups and the dialog scrim are repainted in the same order, so the result is pixel-identical to painting the whole screen.
    /// Frames with input, value changes (<see cref="Invalidate"/>/<see cref="Repaint"/>), placement, size, virtual keyboard or inertial scrolling, and the first frame, are always full.</para>
    /// <para>The canvas must still hold the last frame's image, so do not enable it on hosts that create a new bitmap every frame or lose content on buffer swaps (e.g. the OpenGL default framebuffer).
    /// For a frame whose image was lost (e.g. a new buffer was created), set it to <see langword="false"/> for that frame only to paint everything.
    /// The repainted region is available from <see cref="LastRenderRect"/>. Applies only to the <see cref="App.DvApp"/> root screen (slot screens are drawn by the root).</para>
    /// </remarks>
    public bool PartialRender { get; set; }

    /// <summary>
    /// Gets the region repainted by the last <see cref="RenderTo(SKCanvas, float, float, long)"/> (canvas units, the same as <c>w</c>/<c>h</c>).
    /// <see langword="null"/> means the whole screen; <see cref="SKRectI.Empty"/> means nothing was repainted (the previous image is unchanged).
    /// </summary>
    /// <remarks>The host only needs to present this region (partial texture update, partial window invalidation). Always <see langword="null"/> when <see cref="PartialRender"/> is <see langword="false"/>.</remarks>
    public SKRectI? LastRenderRect { get; private set; }

    // 부분 다시 그리기 — 마지막으로 그린 프레임의 배치 열쇠·크기·키보드 밀림
    private (int Key, float W, float H, float Pan)? _painted;
    private long _layoutVersion;

    // 점멸 상자의 그림이 상자 밖으로 번질 수 있는 만큼(램프 빛·그림자)
    private const float AnimationBleed = 24f;

    // 이 프레임을 잘라 그릴 영역. null = 전체
    private SKRect? PartialArea(bool changed, float w, float h, float pan)
    {
        if (!PartialRender || IsChildSurface || changed || _needsRender || InMotion() ||
            _painted is not { } p || p.W != w || p.H != h || p.Pan != pan || p.Key != FrameKey())
            return null;

        var screen = SKRect.Create(0, 0, w, h);
        var area = SKRect.Empty;
        CollectAnimated(new SKPoint(0, -pan), screen, r =>
        {
            area = area.IsEmpty ? r : SKRect.Union(area, r);
        });
        return area.IsEmpty ? SKRect.Empty : SKRect.Intersect(area, screen);
    }

    // 상자 점멸 말고 시간에 따라 움직이는 것 — 관성 스크롤·가상 키보드 슬라이드·길게 누르기. 칸 화면(DvApp 페이지·영역)까지 본다
    // (페이지 칸 안의 관성 스크롤을 루트가 못 보면 그 프레임을 잘라 그려 화면이 멈춘다)
    private bool InMotion()
    {
        if (_fling is not null || _holdBox is not null || (_kb?.IsAnimating(Clock()) ?? false) || _motion.Active || _pageMotion is not null || _expAnims.Count > 0)
            return true;
        foreach (var s in _slots)
        {
            if (s.Child.InMotion())
                return true;
        }

        return false;
    }

    // 배치 열쇠 — 이 화면과 칸 화면의 배치 판(다시 만들기·다시 배치)·칸 자리. 다르면 지난 그림과 상자 자리가 다르다
    private int FrameKey()
    {
        var hc = new HashCode();
        hc.Add(_layoutVersion);
        hc.Add(_box is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_box));
        foreach (var s in _slots)
        {
            hc.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s.Child));
            hc.Add(s.Rect);
            hc.Add(s.Modal);
            hc.Add(s.Child.FrameKey());
        }

        return hc.ToHashCode();
    }

    // 시간으로 바뀌는 상자(NextDue 와 같은 조건)의 창 기준 영역 — 스크롤 상자는 보이는 칸으로 자르고 안쪽은 스크롤만큼 옮긴다
    private void CollectAnimated(SKPoint offset, SKRect clip, Action<SKRect> add)
    {
        if (_box is not null)
            Walk(_box, offset, clip);
        foreach (var s in _slots)
        {
            if (s.Rect.IsEmpty)
                continue;
            var slotClip = s.Modal ? OverlayClip(s) : s.Rect;
            slotClip.Offset(offset);
            slotClip = SKRect.Intersect(slotClip, clip);
            if (!slotClip.IsEmpty)
                s.Child.CollectAnimated(new SKPoint(offset.X + s.Rect.Left, offset.Y + s.Rect.Top), slotClip, add);
        }

        void Walk(LayoutBox b, SKPoint off, SKRect cl)
        {
            if (BlinkInterval(b) > 0)
            {
                var r = b.Rect;
                if (b.Process is { Graph: { } g } d)
                    r = SKRect.Union(r, SKRect.Create(d.Origin.X, d.Origin.Y, g.Size.Width, g.Size.Height)); // 영역이 모자라 넘친 배관
                r.Offset(off);
                r.Inflate(AnimationBleed, AnimationBleed);
                r = SKRect.Intersect(r, cl);
                if (!r.IsEmpty)
                    add(r);
            }

            if (b.IsScroller)
            {
                var view = b.Rect;
                view.Offset(off);
                cl = SKRect.Intersect(cl, view);
                if (cl.IsEmpty)
                    return;
                off = new SKPoint(off.X - b.ScrollX, off.Y - b.ScrollY);
            }

            foreach (var c in b.Children)
                Walk(c, off, cl);
        }
    }

    /// <summary>Dialog slot screen: does not clear the background (set by the root when configuring the slot).</summary>
    internal bool IsOverlay { get; private set; }

    // 다이얼로그 그림자(shadow.lg) 가 칸 밖으로 번지는 만큼 — 내용이 칸에 들어가면 이만큼 넓혀 클립한다
    private const float OverlayShadowRoom = 48;

    // 다이얼로그 칸 클립 — 루트 상자가 칸 안에 들어가면 그림자 자리만큼 넓히고, 창보다 커서 잘린 다이얼로그는 칸 그대로(넘친 내용이 새지 않게)
    private static SKRect OverlayClip(ChildSlot s)
    {
        if (s.Child.Root is not { } rb || rb.Rect.Width > s.Rect.Width + 0.5f || rb.Rect.Height > s.Rect.Height + 0.5f)
            return s.Rect;
        var r = s.Rect;
        r.Inflate(OverlayShadowRoom, OverlayShadowRoom);
        return r;
    }

    // 다이얼로그 막 — 테마 토큰 scrim(없으면 반투명 남색)
    private void PaintScrim(SKCanvas canvas, float w, float h)
    {
        var color = (_theme ?? DvTheme.Default).TryGet("scrim", out var raw) && DvColor.TryParse(raw, out var c)
            ? c.ToSK()
            : new SKColor(0x0f, 0x17, 0x2a, 0x66);
        using var paint = new SKPaint { Color = color };
        canvas.DrawRect(0, 0, w, h, paint);
    }

    /// <summary>
    /// Forwards a pointer (mouse/touch) move. Also pass moves while not pressed (used for hover styles and grid hover).
    /// </summary>
    /// <remarks>
    /// Moving while pressed is handled as knob rotation, scroll drag (beyond a certain distance it is judged a scroll and the press is canceled), or scroll thumb drag.
    /// If the virtual keyboard or a popup is open, it receives the move first and hover on the screen below is cleared. Moving between <see cref="App.DvApp"/> area and page slots clears hover on the slot left behind.
    /// Disabled boxes do not draw hover styles, and no hover occurs on clipped invisible parts or outside a scroll viewport. Call it on the UI thread.
    /// </remarks>
    /// <param name="p">Pointer position (same coordinate system as <see cref="RenderTo(SKCanvas, float, float, long)"/>, top-left origin).</param>
    public void PointerMove(SKPoint p)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { PointerMoveCore(p); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private void PointerMoveCore(SKPoint p)
    {
        if (KeyboardPointerMove(p) || PopupPointerMove(p))
        {
            ClearHover(); // 키보드·팝업이 가린 아래 화면에는 hover 가 남지 않는다
            return;
        }

        if (_holdBox is { } held && _box is not null && !ReferenceEquals(HitTester.HitTarget(_box, p, out _), held))
            CancelHold(); // 누른 채 상자 밖으로 나감 — 길게 누르기 취소

        if (_knobDrag is not null)
        {
            _knobDrag.Move(p + _knobShift);
            _needsRender = true;
            return;
        }

        if (ScrollPointerMove(p))
            return;

        var blocked = Blocked;
        if (blocked)
            DropBlockedCapture();
        if ((blocked ? (_capture is null ? ModalSlotAt(p) : ModalSlot()) : InputSlot(p)) is { } slot)
        {
            ClearHover(body: false); // 이 화면에서 칸으로 넘어감 — 이 화면 쪽 hover 끄기
            foreach (var s in _slots)
            {
                if (!ReferenceEquals(s.Child, slot.Child))
                    s.Child.ClearHover(); // 다른 칸에서 넘어옴
            }
            slot.Child.PointerMove(ToChild(p, slot));
            return;
        }

        foreach (var s in _slots)
            s.Child.ClearHover(); // 칸에서 이 화면으로 넘어옴 — 칸 쪽 hover 끄기
        if (blocked)
        {
            ClearHover(body: false); // 다이얼로그 밖 — 막 아래에는 hover 가 생기지 않는다
            return;
        }
        UpdateHover(p);
    }

    // 점 아래의 hover 대상을 다시 정한다(포인터 이동, 휠 스크롤로 내용이 포인터 밑에서 움직였을 때)
    private void UpdateHover(SKPoint p)
    {
        if (_box is null)
            return;

        var hover = HitTester.HitTarget(_box, p, out var hoverLocal);
        if (GridHover(hover, hoverLocal))
            _needsRender = true;
        if (ReferenceEquals(hover, _state.Hover))
            return;

        // 그림이 바뀌는 건 hover 를 그리는 박스에 들어오거나 나갈 때뿐
        if (HoverVisible(_state.Hover) || HoverVisible(hover))
            _needsRender = true;
        _state.Hover = hover;
    }

    /// <summary>The current hover target box (null if none), for tests.</summary>
    internal LayoutBox? HoverBox => _state.Hover;

    /// <summary>Clears hover (the pointer left this screen or it is covered). For body, also clears hover on the slot screens.</summary>
    internal void ClearHover(bool body = true)
    {
        if (GridHover(null, default))
            _needsRender = true;
        if (HoverVisible(_state.Hover))
            _needsRender = true;
        _state.Hover = null;
        if (body)
            foreach (var s in _slots)
                s.Child.ClearHover();
    }

    // hover·press 때 그림이 바뀌는 상자인지 — 비활성은 상태 스타일·노브 밝기를 그리지 않는다(Painter.Effective 와 같아야 함)
    private static bool HoverVisible(LayoutBox? b) => b is { Disabled: false } && (b.StyleHover is not null || b.Node.Tag == "DvKnob");
    private static bool PressVisible(LayoutBox? b) => b is { Disabled: false } && (b.StylePress is not null || b.Node.Tag == "DvKnob");

    /// <summary>
    /// Forwards a pointer press (left mouse button down or touch start).
    /// </summary>
    /// <remarks>
    /// <para>Remembers the pressed target and turns on its press style. A click occurs in <see cref="PointerUp"/> when released over the same target.
    /// Only visible boxes can be targets (parts clipped by a parent's <c>overflow</c>, outside a scroll viewport, or <c>visible</c>-false elements are not hit; when overlapping, the one drawn on top wins).
    /// Pressing a disabled area (including disabled inherited from ancestors) makes that disabled box the target, so nothing happens, and the press does not leak to an outer enabled ancestor's <c>onclick</c>.
    /// Pressing on a knob starts a rotation drag; pressing inside a scroll box starts a scroll gesture. Any inertial scroll in progress stops.</para>
    /// <para>While editing, pressing somewhere other than the edit target commits the edit with <see cref="DvEditExit.FocusLost"/> before the press is handled.
    /// If the virtual keyboard is open, a press outside it cancels the edit and is not passed below. If a popup is open, a press outside it only closes the popup.</para>
    /// </remarks>
    /// <param name="p">Pointer position (same coordinate system as <see cref="RenderTo(SKCanvas, float, float, long)"/>).</param>
    public void PointerDown(SKPoint p)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { PointerDownCore(p); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private void PointerDownCore(SKPoint p)
    {
        _capture = null; // 앞 누름의 뗌을 키보드·팝업이 가져갔으면 남아 있을 수 있다
        if (KeyboardPointerDown(p) || PopupPointerDown(p))
            return;

        // 편집 중 다른 곳을 누르면 그 처리(버튼 등)보다 먼저 세션을 끝낸다
        if (_editTarget is { } et && _box is not null)
        {
            var hit = HitTester.HitTarget(_box, p, out var hitLocal);
            if (!et.Hits(hit, hitLocal))
                CommitCurrent(DvEditExit.FocusLost);
        }
        var blocked = Blocked;
        var target = blocked ? ModalSlotAt(p) : SlotAt(p); // 다이얼로그가 있으면 맨 위 다이얼로그 칸 안만, 밖은 삼킨다
        foreach (var s in _slots)
        {
            if (!ReferenceEquals(s.Child, target?.Child) && s.Child.Edit.IsEditing)
                s.Child.CommitPendingEdit(); // 그 칸 밖(이 화면·다른 칸)을 누름 — 그 칸의 편집을 먼저 끝낸다
        }

        if (target is { } slot)
        {
            _capture = slot.Child; // 떼기까지 이 칸이 받는다
            slot.Child.PointerDown(ToChild(p, slot));
            return;
        }

        if (_box is null || blocked)
            return;

        _fling = null; // 다시 누르면 관성 정지
        var path = HitTester.HitPath(_box, p);
        if (BeginThumbDrag(path, p))
            return;

        _pressOrigin = HitTester.HitTarget(_box, p, out var local);
        _state.Press = _pressOrigin;
        StartHold(_pressOrigin, p);
        if (PressVisible(_pressOrigin))
            _needsRender = true;

        if (_pressOrigin is { Node.Tag: "DvKnob" } knob && KnobBinder.CanDrag(knob) && knob.KnobInput != KnobInputMode.Tap)
        {
            // 노브가 우선 — 노브 위에서 누른 드래그는 스크롤하지 않는다
            _knobShift = local - p;
            _knobDrag = new KnobDrag(knob, local);
        }
        else if (_pressOrigin is { Node.Tag: "DvSlider" } slider && KnobBinder.CanDrag(slider))
        {
            // 슬라이더도 노브처럼 스크롤보다 우선 — 누른 자리로 값이 옮겨 가고 끄는 대로 따라간다
            _knobShift = local - p;
            _knobDrag = new SliderDrag(slider, local);
            _needsRender = true;
        }
        else
        {
            BeginScrollGesture(path, p);
        }
    }

    /// <summary>
    /// Forwards a pointer release (left mouse button up or touch end). Releasing over the same target that was pressed is handled as a click.
    /// </summary>
    /// <remarks>
    /// <para>Click handling: invert a <c>DvToggle</c> value (written to the VM), start <c>DvInput</c> editing, open a <c>DvSelect</c> dropdown or a <c>DvDateTimeInput</c>/<c>DvColorInput</c> picker popup,
    /// grid row selection and cell actions, alarm widget acknowledgment, and otherwise calling the <c>onclick</c> handler.
    /// If the target is disabled (including inherited from ancestors), none of these happen (including grid header sorting).
    /// When a click occurs, <see cref="Invalidate"/> is called automatically, so VM values changed inside the handler are reflected on the next frame.</para>
    /// <para>Exceptions thrown by app code in handlers or property setters do not escape this method. A Trace error is logged and reported via <see cref="HandlerError"/>.</para>
    /// <para>A knob drag is committed to the VM once here. A scroll drag ends without a click, and a fast release continues with inertial scrolling.</para>
    /// </remarks>
    /// <param name="p">Pointer position (same coordinate system as <see cref="RenderTo(SKCanvas, float, float, long)"/>).</param>
    public void PointerUp(SKPoint p)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { PointerUpCore(p); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private void PointerUpCore(SKPoint p)
    {
        var blocked = Blocked;
        if (blocked)
            DropBlockedCapture(); // 다이얼로그가 뜨기 전에 누른 아래 칸 — 떼기가 클릭을 만들지 않는다
        // 막혔으면 누름을 받은 맨 위 다이얼로그 칸만(누름이 삼켜졌으면 떼기도 삼킨다 — 누름~떼기 한 묶음)
        var slot = blocked ? (_capture is null ? null : ModalSlot()) : InputSlot(p);
        _capture = null;
        if (KeyboardPointerUp(p) || PopupPointerUp(p))
            return;

        // 어느 경로든 press 는 풀린다 → press 스타일이 보이고 있었다면 다시 그려야 함
        if (PressVisible(_state.Press))
            _needsRender = true;

        if (_knobDrag is not null)
        {
            var drag = _knobDrag;
            _knobDrag = null;
            if (drag.Active)
            {
                // 커밋 거절·실패 시 Invalidate 로 VM 값에서 다시 빌드되어 원래 값으로 복귀
                if (drag.Changed)
                    KnobBinder.Commit(drag.Box, _vm, drag.Value, this);
                Invalidate();
                _state.Press = null;
                _pressOrigin = null;
                return;
            }
        }

        if (ScrollPointerUp(p))
            return;

        if (slot is { } s)
        {
            s.Child.PointerUp(ToChild(p, s));
            _dirty = true; // 이 화면만 — 칸의 화면은 자기 클릭을 스스로 반영한다
            _state.Press = null;
            _pressOrigin = null;
            return;
        }

        if (_box is not null && !blocked)
        {
            var up = HitTester.HitTarget(_box, p, out var upLocal);
            if (up is not null && ReferenceEquals(up, _pressOrigin))
            {
                if (up.Node.Tag is "DvToggle" or "DvCheckBox")
                    TwoWayBinder.Commit(up, _vm, this);
                else if (up.Node.Tag == "DvRadio")
                    CommitRadio(up);
                else if (up.Node.Tag == "DvInput")
                {
                    if (InputDisplay.StepperZone(up, upLocal.X) is var zone and not 0)
                        StepInput(up, zone);
                    else if (InputDisplay.IsEditable(up))
                        BeginEdit(up);
                }
                else if (up.Node.Tag == "DvSelect")
                {
                    if (SelectDisplay.IsOpenable(up))
                        Window.OpenPopup(new DropdownPopup(this, new BoxSlot(this, up)));
                }
                else if (up.IsPicker)
                {
                    if (PickerDisplay.IsOpenable(up))
                        Window.OpenPopup(CreatePicker(new BoxSlot(this, up), up.Node.Tag == "DvColorInput"));
                }
                else if (up.Grid is not null)
                {
                    if (GridInteraction.FilterColumnAt(up, upLocal) is >= 0 and var fc)
                        OpenGridFilter(up, fc); // 필터 칸 = 그 열 필터 팝업
                    else if (GridInteraction.EditCellAt(up, upLocal) is { } cell)
                        BeginGridCell(up, cell.Item, cell.Column); // 편집 셀 = 바로 편집(행 선택·onrowclick 없음)
                    else
                        GridInteraction.Click(up, upLocal, _vm, this);
                }
                else if (up.Node.Tag == "DvKnob" && up.KnobInput != KnobInputMode.Drag)
                {
                    if (KnobBinder.CanDrag(up))
                        OpenKnobKeypad(up); // 끌지 않은 탭 = 숫자 키패드(onclick 대신)
                }
                else if (AlarmInteraction.IsAlarmWidget(up))
                {
                    try
                    {
                        AlarmInteraction.Click(up, upLocal, _vm);
                    }
                    catch (Exception ex)
                    {
                        HandlerErrors.Report(this, ex, "onack", up.OnAck, up.Node.Tag);
                    }
                }
                else if (up.SegmentOwner is { } segment)
                {
                    if (!up.Disabled) // 비활성 항목(탭 disabled)
                        CommitSegment(segment, up.SegmentIndex);
                }
                else if (up.ListOwner is { } list)
                {
                    if (!up.Disabled)
                        CommitListItem(list, up.ListIndex);
                }
                else if (up.ExpanderOwner is { } expander)
                {
                    if (!up.Disabled)
                        ToggleExpander(expander);
                }
                else if (up.TreeOwner is { } tree)
                {
                    if (!up.Disabled)
                        TapTreeRow(tree, up, upLocal);
                }
                else if (!IsHoldTarget(up))
                    ClickPlain(up, p); // hold 상자는 떼기로 클릭하지 않는다 — 누른 시간이 차면 그때 이미 눌렸다(TickHold)
                Invalidate();
            }
        }

        _state.Press = null;
        _pressOrigin = null;
    }

    // show-stepper 의 −/+ — 편집 중이면 먼저 끝내고(입력한 값 반영), 그 값에서 step 만큼 옮겨 숫자 입력과 같은 경로(형식·범위·onbeforecommit)로 쓴다.
    private void StepInput(LayoutBox input, int dir)
    {
        if (!InputDisplay.IsEditable(input))
            return; // 비활성·읽기 전용·단방향
        if (_editTarget is not null)
            CommitCurrent(DvEditExit.FocusLost); // 입력 중이던 값을 먼저 반영 — 아래에서 VM 값을 다시 읽는다
        var ctx = input.ValueContext(_vm);
        var current = Binding.BindingResolver.Resolve(ctx, $"{{{Binding.BindingResolver.ExtractPath(input.ValueBindExpr!)}}}");
        new BoxEditTarget(this, input).TryWrite(InputDisplay.Stepped(input, current, dir), out _);
        Invalidate();
    }

    // ── 길게 누르기(hold) ──
    // 누른 상자가 hold 대상이면 시각을 잡고, 프레임마다 TickHold 가 진행을 그리다 시간이 차면 그 자리에서(떼기 전) 클릭을 낸다.
    // 떼기·상자 밖으로 나감·스크롤 판정(누름 취소)·비활성은 취소. 떼기는 hold 상자에 클릭을 내지 않는다.
    private LayoutBox? _holdBox;
    private long _holdStart;
    private bool _holdFired;
    private SKPoint _holdPoint;

    // 일반 클릭 상자(토글·입력·선택·피커·그리드·알람·노브가 아닌 것)이고 hold 가 있는지
    private static bool IsHoldTarget(LayoutBox? b) =>
        b is { HoldMs: > 0 } && b.Node.Tag is not ("DvToggle" or "DvCheckBox" or "DvRadio" or "DvInput" or "DvSelect" or "DvKnob" or "DvSlider") &&
        !b.IsPicker && b.Grid is null && !AlarmInteraction.IsAlarmWidget(b);

    private void StartHold(LayoutBox? pressed, SKPoint p)
    {
        _holdFired = false;
        if (pressed is { Disabled: false } b && IsHoldTarget(b))
        {
            _holdBox = b;
            _holdStart = Clock();
            _holdPoint = p;
            _needsRender = true;
        }
        else
            _holdBox = null;
        _state.HoldBox = null;
        _state.HoldProgress = 0;
    }

    private void CancelHold()
    {
        _holdBox = null;
        _state.HoldBox = null;
        _state.HoldProgress = 0;
        _needsRender = true;
    }

    private void TickHold()
    {
        if (_holdBox is not { } hb)
        {
            if (_state.HoldBox is not null && _pressOrigin is null)
                CancelHold(); // 떼었다 — 진행 띠 지우기
            return;
        }
        if (!ReferenceEquals(_pressOrigin, hb) || hb.Disabled)
        {
            CancelHold(); // 떼기·스크롤 판정·재빌드로 대상이 사라짐·비활성
            return;
        }
        var elapsed = Clock() - _holdStart;
        _state.HoldBox = hb;
        _state.HoldProgress = Math.Clamp(elapsed / (float)hb.HoldMs, 0f, 1f);
        _needsRender = true;
        if (elapsed >= hb.HoldMs && !_holdFired)
        {
            _holdFired = true;
            ClickPlain(hb, _holdPoint);
            Invalidate();
        }
    }

    // value 없는 DvRadio 묶음(group 이름 또는 혼자인 요소 노드) → 고른 option. 다시 만들어도 유지(탭 번호와 같은 방식)
    private readonly Dictionary<object, string> _radioGroups = new();
    private readonly object _radioGate = new();

    internal string? RadioGroupValue(object key)
    {
        lock (_radioGate)
            return _radioGroups.TryGetValue(key, out var v) ? v : null;
    }

    /// <summary>Writes the group value of radios without value (null = nothing selected). Shared by handle writes (other threads allowed) and screen presses.</summary>
    internal void SetRadioGroupValue(object key, string? option)
    {
        lock (_radioGate)
        {
            if (option is null)
                _radioGroups.Remove(key);
            else
                _radioGroups[key] = option;
        }
        Invalidate();
    }

    // 라디오 누름 — 이미 켜져 있으면 그대로(라디오는 눌러서 끌 수 없다). 바인딩이면 option 을 속성 형식으로 바꿔 게이트·쓰기(DvSelect 와 같은 경로),
    // value 가 없으면 화면이 기억하는 묶음 값. 단방향·고정 값·묶음 없음은 읽기 전용.
    private void CommitRadio(LayoutBox radio)
    {
        if (radio.Disabled || radio.ToggleOn || radio.RadioOption is not { } option)
            return;
        if (radio.ValueBindExpr is { } ve && Binding.BindingResolver.IsTwoWay(ve))
        {
            if (!SelectValue.TryConvert(option, radio.SelectValueType, out var value))
            {
                System.Diagnostics.Trace.TraceWarning($"Devinno.UI: DvRadio option '{option}' 을 {radio.SelectValueType?.Name} 로 바꿀 수 없습니다 — 쓰지 않습니다");
                return;
            }
            if (!CommitGate.Allows(this, radio.Node.Tag, _vm, radio.OnBeforeCommit, value, value?.GetType() ?? typeof(object), typeof(object)))
                return;
            if (!HandlerErrors.TrySet(this, radio.Node.Tag, radio.ValueContext(_vm), Binding.BindingResolver.ExtractPath(ve), value))
                return;
        }
        else if (radio.ValueBindExpr is null && radio.RadioGroup is { } key)
        {
            if (!CommitGate.Allows(this, radio.Node.Tag, _vm, radio.OnBeforeCommit, option, typeof(string), typeof(object)))
                return;
            SetRadioGroupValue(key, option);
        }
        else
            return;

        Invalidate();
        NotifyValueWritten(radio);
    }

    // 목록 행 누름 — 한 개 고르기는 그 선택지 값을 value 에(DvSelect 와 같은 게이트·쓰기, 고른 것을 다시 누르면 그대로),
    // 여럿 고르기는 그 값을 넣거나 빼서 selected-items 에 새 목록으로(그리드 selection 과 같은 형식 규칙). 읽기 전용·비활성은 무시.
    private void CommitListItem(LayoutBox list, int index)
    {
        if (list.Disabled || list.ReadOnly || list.ValueBindExpr is not { } ve || !Binding.BindingResolver.IsTwoWay(ve) ||
            index < 0 || index >= list.SelectOptions.Count)
            return;
        // DvOption 값은 글자 그대로 — 속성 형식(enum·숫자)으로 바꿔 쓴다(DvSelect 드롭다운과 같음)
        object? Converted(object? v) => SelectValue.TryConvert(v, list.SelectValueType, out var c) ? c : v;
        if (!list.ListMulti)
        {
            if (index != list.SelectedIndex)
                new BoxSlot(this, list).Write(Converted(list.SelectOptions[index].Value));
            return;
        }

        var ctx = list.ValueContext(_vm);
        var path = Binding.BindingResolver.ExtractPath(ve);
        var picked = list.SelectOptions.Where((_, i) => list.ListSelected is { } s && i < s.Length && (i == index ? !s[i] : s[i]))
            .Select(o => Converted(o.Value)).ToList();
        var (ok, value) = GridInteraction.Snapshot(Binding.BindingResolver.ResolvePropertyType(ctx, path), picked.OfType<object>().ToList());
        if (!ok)
        {
            System.Diagnostics.Trace.TraceWarning($"Devinno.UI: DvListBox selected-items '{path}' — 지원하지 않는 속성 형식(T[]·List<T>·IList<T>·IReadOnlyList<T>·IEnumerable<T>)");
            return;
        }
        if (!CommitGate.Allows(this, list.Node.Tag, _vm, list.OnBeforeCommit, value, value?.GetType() ?? typeof(object), typeof(object)))
            return;
        if (!HandlerErrors.TrySet(this, list.Node.Tag, ctx, path, value))
            return;
        NotifyValueWritten(list);
        Invalidate();
    }

    // DvTreeView 의 펼친 항목(트리 요소 노드 → 항목 객체들). 다시 만들어도 유지 — 항목 객체가 같으면(Equals) 같은 가지.
    private readonly Dictionary<Markup.ElementNode, HashSet<object>> _treeOpen = new();

    internal bool TreeExpanded(Markup.ElementNode tree, object item)
    {
        lock (_treeOpen)
            return _treeOpen.TryGetValue(tree, out var set) && set.Contains(item);
    }

    internal void SetTreeExpanded(Markup.ElementNode tree, object item, bool open)
    {
        lock (_treeOpen)
        {
            if (!_treeOpen.TryGetValue(tree, out var set))
                _treeOpen[tree] = set = new HashSet<object>();
            if (open)
                set.Add(item);
            else
                set.Remove(item);
        }
        Invalidate();
    }

    // 트리 행 누름 — 가지의 ▶/▼ 자리(들여쓰기 + 아이콘)를 누르면 펼치기·접기(읽기 전용이어도 됨), 그 밖은 고르기(value 에 그 항목의 값).
    private void TapTreeRow(LayoutBox tree, LayoutBox row, SKPoint local)
    {
        if (tree.Disabled || row.TreeIndex < 0 || row.TreeIndex >= tree.TreeRows.Count)
            return;
        var r = tree.TreeRows[row.TreeIndex];
        var chevronRight = row.Rect.Left + row.Style.Padding.Left + LayoutEngine.TreeChevron + LayoutEngine.IconTextGap;
        if (r.HasChildren && local.X <= chevronRight)
        {
            SetTreeExpanded(tree.Node, r.Item, !r.Open);
            return;
        }

        if (tree.ReadOnly || tree.ValueBindExpr is not { } ve || !Binding.BindingResolver.IsTwoWay(ve) || row.TreeIndex == tree.SelectedIndex)
            return;
        var value = SelectValue.TryConvert(r.Value, tree.SelectValueType, out var c) ? c : r.Value;
        new BoxSlot(this, tree).Write(value);
    }

    // ── 접는 패널 펼침 움직임(0.2초, ease-out) ──
    // 다시 만들 때 패널마다(요소 + 반복 항목) 지난 펼침과 비교해 바뀌었으면 움직임을 시작하고, 프레임마다 그리기 시각으로 보이는 비율을
    // 내용 상자에 넣어 다시 배치한다(아래 내용이 함께 밀림). 첫 화면은 움직이지 않는다. 끝나면 한 번 다시 만들어 내용·스타일을 원래대로.
    private const int ExpanderMotionMs = 200;

    private sealed class ExpanderMotion(bool opening, float from)
    {
        public bool Opening { get; } = opening;
        public float From { get; } = from;
        public long? Start { get; set; }
        public float Now { get; set; } = from;
    }

    private readonly Dictionary<(Markup.ElementNode, object?), ExpanderMotion> _expAnims = new();
    private readonly Dictionary<(Markup.ElementNode, object?), bool> _expOpen = new();
    private readonly HashSet<(Markup.ElementNode, object?)> _expSeen = new();

    private float? ExpanderReveal(Markup.ElementNode node, object? item, bool open)
    {
        var key = (node, item);
        _expSeen.Add(key);
        if (_expOpen.TryGetValue(key, out var was) && was != open)
            _expAnims[key] = new ExpanderMotion(open, _expAnims.TryGetValue(key, out var running) ? running.Now : open ? 0f : 1f); // 되돌림은 지금 비율에서
        _expOpen[key] = open;
        return _expAnims.TryGetValue(key, out var m) ? m.Now : null;
    }

    // 이번 화면에 없는 패널(반복 항목이 빠짐 등)의 기억은 버린다
    private void PruneExpanderState()
    {
        foreach (var k in _expOpen.Keys.Where(k => !_expSeen.Contains(k)).ToList())
        {
            _expOpen.Remove(k);
            _expAnims.Remove(k);
        }
    }

    private void AdvanceExpanders(long now)
    {
        if (_expAnims.Count == 0 || _box is null)
            return;
        var finished = false;
        foreach (var m in _expAnims.Values)
        {
            m.Start ??= now;
            var t = Math.Clamp((now - m.Start.Value) / (float)ExpanderMotionMs, 0f, 1f);
            var target = m.Opening ? 1f : 0f;
            m.Now = m.From + (target - m.From) * EasingMath.Apply(Easing.EaseOut, t);
            finished |= t >= 1f;
        }

        SetReveals(_box);
        LayoutEngine.ClearMeasure(_box);
        LayoutEngine.Rearrange(_box, _w, _h);
        _layoutVersion++; // 배치가 바뀐 프레임은 전체 다시 그리기

        if (finished)
        {
            foreach (var k in _expAnims.Where(p => p.Value.Now == (p.Value.Opening ? 1f : 0f)).Select(p => p.Key).ToList())
                _expAnims.Remove(k);
            Invalidate(); // 접힌 내용은 빼고, 펼친 내용은 잘림(overflow) 없이 다시 만든다
        }
    }

    private void SetReveals(LayoutBox b)
    {
        if (b.RevealKey is ValueTuple<Markup.ElementNode, object?> key && b.Reveal is not null && _expAnims.TryGetValue(key, out var m))
            b.Reveal = m.Now;
        foreach (var c in b.Children)
            SetReveals(c);
    }

    // expanded 없는 DvExpander 의 펼침(마크업 노드 기준 — 다시 만들어도 유지, 탭 번호와 같은 방식)
    private readonly Dictionary<Markup.ElementNode, bool> _expanders = new();

    internal bool? ExpanderValue(Markup.ElementNode node)
    {
        lock (_expanders)
            return _expanders.TryGetValue(node, out var v) ? v : null;
    }

    /// <summary>Writes the expanded state of a collapsible panel without expanded. Shared by handle writes (other threads allowed) and header presses.</summary>
    internal void SetExpanderValue(Markup.ElementNode node, bool expanded)
    {
        lock (_expanders)
            _expanders[node] = expanded;
        Invalidate();
    }

    // 제목 줄 누름 — 펼침을 뒤집는다. expanded 가 양방향이면 VM 에, 없으면 화면이 기억, 단방향·고정 값이면 그대로(읽기 전용).
    private void ToggleExpander(LayoutBox expander)
    {
        if (expander.Disabled)
            return;
        var next = !expander.ToggleOn;
        if (expander.ValueBindExpr is { } ve && Binding.BindingResolver.IsTwoWay(ve))
        {
            if (!HandlerErrors.TrySet(this, expander.Node.Tag, expander.ValueContext(_vm), Binding.BindingResolver.ExtractPath(ve), next))
                return;
            Invalidate();
        }
        else if (!expander.Node.Attributes.ContainsKey("expanded"))
            SetExpanderValue(expander.Node, next);
        else
            return;
        NotifyValueWritten(expander);
    }

    // value 없는 DvTabs 의 선택 번호(마크업 노드 기준 — 다시 만들어도 유지)
    private readonly Dictionary<Markup.ElementNode, int> _tabIndex = new();

    // 세그먼트·탭 머리글 — 그 선택지 값을 value 에 쓴다(DvSelect 와 같은 게이트·쓰기). 읽기 전용·단방향·비활성은 무시.
    // value 가 없는 DvTabs 는 화면이 선택 번호를 기억한다.
    private void CommitSegment(LayoutBox segment, int index)
    {
        if (segment.Node.Tag == "DvTabs" && !segment.Node.Attributes.ContainsKey("value"))
        {
            if (!segment.Disabled && index >= 0 && index < segment.SelectOptions.Count)
            {
                _tabIndex[segment.Node] = index;
                Invalidate();
            }
            return;
        }
        if (segment.Disabled || segment.ReadOnly || segment.ValueBindExpr is not { } ve || !Binding.BindingResolver.IsTwoWay(ve) ||
            index < 0 || index >= segment.SelectOptions.Count)
            return;
        new BoxSlot(this, segment).Write(segment.SelectOptions[index].Value);
    }

    // 일반 클릭: (래칭 버튼이면 값 뒤집기) + onclick 처리기 + 코드 핸들 Click. 예외는 HandlerError 로.
    private void ClickPlain(LayoutBox up, SKPoint p)
    {
        if (up.Node.Tag == "DvButton" && up.ValueBindExpr is { } latch && Binding.BindingResolver.IsTwoWay(latch))
            TwoWayBinder.Commit(up, _vm, this);
        try
        {
            EventDispatcher.Click(up, _vm);
        }
        catch (Exception ex)
        {
            HandlerErrors.Report(this, ex, "onclick", up.OnClick, up.Node.Tag);
        }

        // 같은 VM 으로 새 화면을 만들면 핸들은 새 화면으로 옮겨 간다 — 옛 화면의 탭은 Click 을 내지 않는다.
        if (!up.Disabled && _controlsByNode.TryGetValue(up.Origin, out var handle) && ReferenceEquals(handle.Surface, this))
        {
            try
            {
                handle.RaiseClick(new SKPoint(p.X + WindowOffset.X, p.Y + WindowOffset.Y));
            }
            catch (Exception ex)
            {
                HandlerErrors.Report(this, ex, "Click", handle.Id, up.Node.Tag);
            }
        }
    }

    // 노브 탭(input-mode tap·both) — 앱의 숫자 키패드로 값을 받아 드래그와 같은 커밋 경로(step 반올림·범위·onbeforecommit·양방향 쓰기)로 쓴다.
    // 취소면 아무것도 안 한다. 다이얼로그는 DvApp 화면에서만 뜨므로 그 밖이면 경고만.
    private async void OpenKnobKeypad(LayoutBox knob)
    {
        if (Window.Composer is not App.DvApp app)
        {
            System.Diagnostics.Trace.TraceWarning("Devinno.UI: DvKnob input-mode tap·both 의 키패드는 DvApp 화면(페이지·바·다이얼로그)에서만 뜹니다 — 이 탭은 무시합니다");
            return;
        }

        double lo = Math.Min(knob.KnobMin, knob.KnobMax), hi = Math.Max(knob.KnobMin, knob.KnobMax);
        var current = Math.Clamp(Math.Round(knob.KnobValue, KnobBinder.Decimals(knob.KnobStep), MidpointRounding.AwayFromZero), lo, hi);
        var vm = _vm;
        double? result;
        try
        {
            result = await app.ShowDialog<App.DvNumberBoxDialog, double?>("", current, lo, hi, knob.KnobUnit);
        }
        catch (Exception ex)
        {
            HandlerErrors.Report(this, ex, "keypad", knob.ValueBindExpr, knob.Node.Tag);
            return;
        }

        if (result is not { } value)
            return;
        KnobBinder.Commit(knob, vm, KnobBinder.Snap(knob, value), this);
        Invalidate();
    }

    /// <summary>Finds the same box in the new tree by following target's child index path in the old tree (null if the structure changed).</summary>
    internal static LayoutBox? Counterpart(LayoutBox oldRoot, LayoutBox newRoot, LayoutBox target)
    {
        var path = new List<int>();
        if (!PathTo(oldRoot, target, path))
            return null;

        var cur = newRoot;
        foreach (var i in path)
        {
            if (i >= cur.Children.Count)
                return null;
            cur = cur.Children[i];
        }

        return cur.Node.Tag == target.Node.Tag && cur.ValueBindExpr == target.ValueBindExpr ? cur : null;
    }

    private static bool PathTo(LayoutBox box, LayoutBox target, List<int> path)
    {
        if (ReferenceEquals(box, target))
            return true;

        for (var i = 0; i < box.Children.Count; i++)
        {
            path.Add(i);
            if (PathTo(box.Children[i], target, path))
                return true;
            path.RemoveAt(path.Count - 1);
        }

        return false;
    }
}
