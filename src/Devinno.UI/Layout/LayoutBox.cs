using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using SkiaSharp;

namespace Devinno.UI.Layout;

/// <summary>
/// The layout result for one screen element. Holds the style, position and size, plus the widget values obtained by resolving bindings (text, gauge value, lamp state, and so on).
/// </summary>
/// <remarks>
/// <para>This is internal engine-stage data. App code normally does not use it directly; it uses <see cref="DvSurface"/>/<see cref="DvRenderer"/> instead.
/// Use it in tests and tools to inspect a <see cref="LayoutEngine.Layout"/> result, or to check the position and values of a box found with <see cref="LayoutQuery"/>.</para>
/// <para>Fields are filled in the following stages.
/// <b>Build</b> (once per element inside <see cref="LayoutEngine.Layout"/>): <see cref="Node"/>, <see cref="Style"/>, the state styles, and the per-tag widget values.
/// Widget values are filled only for the matching tag; for other tags they keep their defaults. Bound values are a snapshot taken at build time, so this box does not change when the view model changes.
/// <b>Arrange</b> (right after build, and again when the window size changes): <see cref="Rect"/>, <see cref="ContentWidth"/>, <see cref="ContentHeight"/>.
/// <b>Interaction</b> (changed by <see cref="DvSurface"/> while the screen is shown): scroll offsets and edit state (<see cref="Editing"/>, etc.).
/// <b>Painting</b> (<see cref="Painting.Painter"/>) only reads these values.</para>
/// <para>All coordinates and lengths are in px. Access only on the UI thread.</para>
/// </remarks>
public sealed class LayoutBox
{
    /// <summary>The markup element that produced this box. The tag name and raw attributes can be read from it. Set at build time.</summary>
    public required ElementNode Node { get; init; }

    private ElementNode? _origin;
    /// <summary>
    /// The original screen element of this box (the key for handle lookup). Defaults to <see cref="Node"/>. For the root box of a component instance, it points to the instance node instead of the shared template node.
    /// </summary>
    internal ElementNode Origin { get => _origin ?? Node; set => _origin = value; }
    /// <summary>
    /// The computed style with no state (hover, press, etc.). Set at build time.
    /// Precedence (low → high): layout attributes (<c>gap</c> of <c>DvStack</c>, etc.) &lt; <c>class</c> &lt; the <c>content-align</c> attribute &lt; the <c>style</c> attribute.
    /// </summary>
    public required ComputedStyle Style { get; init; }
    /// <summary>
    /// The outer area of the box (px, coordinates relative to the top-left of the screen). Filled in the arrange stage.
    /// Children inside a scroll box use unscrolled content coordinates; their actual screen position is offset by the parent's <see cref="ScrollX"/>/<see cref="ScrollY"/>.
    /// </summary>
    public SKRect Rect { get; set; }

    /// <summary>
    /// Cache of the natural size measured without an available width (style width and max-width are applied). It depends only on content and style, so it is measured only once while the tree lives
    /// (the tree is rebuilt on Invalidate).
    /// </summary>
    internal (float w, float h)? MeasureCache;

    /// <summary>
    /// Cache of the result measured with an available width (only the last available width). If the natural width fits in the available width, the natural size is used as is,
    /// so this is filled only for boxes that need line wrapping (or vertical stacks and grids with such children). Re-measured when the window size changes the width.
    /// </summary>
    internal (float w, float h)? MeasureCacheAt;
    internal float MeasureCacheAvail = float.NaN;

    /// <summary>
    /// The parent does not follow this box's style width when arranging (stretch in a vertical stack, a grid cell, grow in a horizontal stack, absolute with both left and right set).
    /// In that case width is not the final width, so it is not used as the text wrapping width (so that <c>width:0; flex-grow:1</c> does not wrap at zero width).
    /// </summary>
    internal bool WidthOverridden;

    /// <summary>
    /// When an available width is given, measure at that width instead of shortcutting to the natural size: for boxes whose width is overridden (natural width is not the content width) and
    /// for ancestors that pass width down to such children. Set once right after build.
    /// </summary>
    internal bool MeasureAtAvail;

    /// <summary>Number of actual measurements (excluding cache hits), for test instrumentation.</summary>
    internal int MeasureCount;

    /// <summary>Text line information (shared by measuring and painting). Created during measuring; painting does not re-split it when the width is the same.</summary>
    internal Devinno.UI.Text.TextLines? TextLines;

    // ── overflow:scroll — 자식은 내용 좌표로 한 번 배치하고, 스크롤은 페인트·히트테스트에서 오프셋으로만 적용
    /// <summary>Horizontal scroll offset (px): the distance from the left edge of the content to the left edge of the visible area, from 0 to <see cref="MaxScrollX"/>. Changed by <see cref="DvSurface"/> when the user drags or turns the wheel.</summary>
    public float ScrollX { get; internal set; }
    /// <summary>Vertical scroll offset (px), from 0 to <see cref="MaxScrollY"/>. Changed by <see cref="DvSurface"/> when the user drags or turns the wheel.</summary>
    public float ScrollY { get; internal set; }
    /// <summary>Content width of a scroll box (px, including padding and the scrollbar gutter). If smaller than the visible area, equals the <see cref="Rect"/> width. Filled in the arrange stage for scroll boxes only; 0 otherwise.</summary>
    public float ContentWidth { get; internal set; }
    /// <summary>Content height of a scroll box (px, including padding and the scrollbar gutter). If smaller than the visible area, equals the <see cref="Rect"/> height. Filled in the arrange stage for scroll boxes only; 0 otherwise.</summary>
    public float ContentHeight { get; internal set; }
    /// <summary>Maximum horizontal scroll distance (px) = <see cref="ContentWidth"/> − <see cref="Rect"/> width, at least 0.</summary>
    public float MaxScrollX => MathF.Max(0, ContentWidth - Rect.Width);
    /// <summary>Maximum vertical scroll distance (px) = <see cref="ContentHeight"/> − <see cref="Rect"/> height, at least 0.</summary>
    public float MaxScrollY => MathF.Max(0, ContentHeight - Rect.Height);
    /// <summary><see langword="true"/> for a scroll box: the style is <c>overflow:scroll</c>, or the element is a <c>DvDataGrid</c>.</summary>
    public bool IsScroller => Style.Overflow == Overflow.Scroll || Grid is not null;

    /// <summary>DvDataGrid definition and state (null otherwise). A grid is a scroll box that paints itself without child boxes.</summary>
    internal Grid.GridSpec? Grid { get; set; }
    /// <summary>Top inset where the vertical scrollbar starts (grid header height). 0 for ordinary scroll boxes.</summary>
    internal float ScrollInsetTop;
    /// <summary>Bottom inset where the vertical scrollbar ends (grid summary row height).</summary>
    internal float ScrollInsetBottom;

    /// <summary>The item scope, if this box was created inside a DvRepeat item scope; clicks and two-way commits apply to that item.</summary>
    internal Binding.ItemScope? ItemScope { get; init; }
    /// <summary>The instance's parameter scope, if this box was created inside a component template; <c>onclick</c> receives parameter values matching the format.</summary>
    internal Binding.ComponentScope? ComponentScope { get; init; }
    /// <summary>Scrollbar gutter on the overflowing axis; subtracted from the child arrange area (0 if not overflowing).</summary>
    internal float GutterX, GutterY;

    internal void ClampScroll()
    {
        ScrollX = Math.Clamp(ScrollX, 0, MaxScrollX);
        ScrollY = Math.Clamp(ScrollY, 0, MaxScrollY);
    }
    /// <summary>
    /// The text to paint. For <c>DvLabel</c>/<c>DvButton</c>, the binding-resolved value of the <c>text</c> attribute (or the element's inner text if absent); an empty string if there is no value.
    /// <see langword="null"/> for other tags. Filled at build time.
    /// </summary>
    public string? ResolvedText { get; set; }
    /// <summary>The complete style used while a finger or pointer is over the element (the class <c>:hover</c> rule + the <c>hover-style</c> attribute merged over the base style). <see langword="null"/> if not defined, in which case <see cref="Style"/> is used.</summary>
    public ComputedStyle? StyleHover { get; set; }
    /// <summary>The complete style used while pressed (the class <c>:press</c>/<c>:active</c> rules + the <c>press-style</c> attribute). <see langword="null"/> if not defined.</summary>
    public ComputedStyle? StylePress { get; set; }
    /// <summary>The complete style used when <see cref="Disabled"/> (the class <c>:disabled</c> rule + the <c>disabled-style</c> attribute). <see langword="null"/> if not defined.</summary>
    public ComputedStyle? StyleDisabled { get; set; }
    /// <summary>
    /// Whether the box is disabled. <see langword="true"/> if its own <c>disabled</c> attribute (bindable) is <c>true</c> (case-insensitive), or <b>if any ancestor is disabled</b>
    /// (a disabled ancestor disables it even if its own value is <c>false</c>, like HTML <c>fieldset</c> and WPF <c>IsEnabled</c>). Filled at build time.
    /// A disabled box gets no click, edit, popup, commit, ACK or page navigation, and input pressed inside it does not leak out to an enabled ancestor;
    /// hover and press styles are not painted either (<see cref="StyleDisabled"/> is used if present). Scrolling still works.
    /// </summary>
    public bool Disabled { get; set; }
    /// <summary>Whether code subscribed to <see cref="Controls.DvControl.Click"/> for this element (filled at build time). Such an element counts as clickable.</summary>
    internal bool HasCodeClick { get; set; }
    /// <summary>The raw text of the <c>onclick</c> attribute (a view model command name, etc., before resolution). <see langword="null"/> if absent. Filled at build time. For a component template root, the <c>onclick</c> given to the instance takes precedence over the template's.</summary>
    public string? OnClick { get; set; }
    /// <summary>The <c>hold</c> attribute (ms). If greater than 0, the element must be held this long before a click occurs (long press, guards against accidental operation).</summary>
    internal int HoldMs { get; set; }
    /// <summary><c>show-stepper</c> of a numeric <c>DvInput</c>: −/+ buttons at both ends.</summary>
    internal bool InputStepper { get; set; }
    /// <summary><c>step</c> of a numeric <c>DvInput</c> (default 1): the amount changed by one −/+ press.</summary>
    internal double InputStep { get; set; } = 1;
    /// <summary>The segment box, if this is an item box created by <c>DvSegment</c> (null otherwise).</summary>
    internal LayoutBox? SegmentOwner { get; set; }
    /// <summary>The option number of the segment item (index into <see cref="SelectOptions"/>).</summary>
    internal int SegmentIndex { get; set; }
    /// <summary>
    /// The raw <c>value</c> attribute of a value-writing widget (<c>DvToggle</c>, <c>DvKnob</c>, <c>DvInput</c>, <c>DvSelect</c>, <c>DvDateTimeInput</c>, <c>DvColorInput</c>), e.g. <c>{Bind Speed}</c>.
    /// Inside a component it is the expression rewritten to the outer view model path. When the user changes the value, it is written to this path. <see langword="null"/> for other tags.
    /// </summary>
    public string? ValueBindExpr { get; set; }
    /// <summary>The value slot of a code handle, when the read/write target is that slot instead of the view model (no <c>value</c> + an <c>id</c> handle). Null otherwise.</summary>
    internal object? ValueTarget { get; set; }
    /// <summary>The component instance node, if this is a template widget that received its value through a two-way component parameter; the user's changed value is reported to that instance's handle. Null otherwise.</summary>
    internal ElementNode? ValueOrigin { get; set; }
    /// <summary>The target for reading and writing the <see cref="ValueBindExpr"/> path: the handle slot, then the repeat item scope, then the screen view model.</summary>
    internal object? ValueContext(object? vm) => ValueTarget ?? (object?)ItemScope ?? vm;
    /// <summary>Whether a <c>DvToggle</c> is on. On when <c>value</c> is <see langword="true"/> or the string <c>true</c>. Filled at build time.</summary>
    public bool ToggleOn { get; set; }
    /// <summary>Chart values (DvSparkline, DvBarChart, DvPieChart).</summary>
    internal double[] ChartValues { get; set; } = Array.Empty<double>();
    /// <summary>Chart item names (bar and pie).</summary>
    internal string[] ChartLabels { get; set; } = Array.Empty<string>();
    /// <summary>Value axis lower and upper bounds (<c>y-min</c>/<c>y-max</c>, null = automatic).</summary>
    internal double? ChartYMin { get; set; }
    internal double? ChartYMax { get; set; }
    /// <summary>Fill under the sparkline (<c>area</c>).</summary>
    internal bool ChartArea { get; set; }
    /// <summary>Ratio of the pie's center hole (<c>inner-radius</c>, 0 = full pie, up to 0.9).</summary>
    internal float ChartInner { get; set; }
    /// <summary><c>DvStepBar</c> step names.</summary>
    internal string[] StepNames { get; set; } = Array.Empty<string>();
    /// <summary><c>DvStepBar</c> current step (1-based; 0 = not started; greater than the count = all done).</summary>
    internal int StepCurrent { get; set; }
    /// <summary>The collapsible panel, if this is the header row of a <c>DvExpander</c> (null otherwise).</summary>
    internal LayoutBox? ExpanderOwner { get; set; }
    /// <summary>The visible fraction (0–1) while a <c>DvExpander</c> content box is expanding or collapsing; multiplied into the measured height (overflowing content is clipped). Null otherwise.</summary>
    internal float? Reveal { get; set; }
    /// <summary>Expand animation key (element + repeat item); used by the surface (<see cref="DvSurface"/>) to find the box when it updates <see cref="Reveal"/> each frame.</summary>
    internal object? RevealKey { get; set; }
    /// <summary>The tree, if this is a row created by <c>DvTreeView</c> (null otherwise).</summary>
    internal LayoutBox? TreeOwner { get; set; }
    /// <summary>Tree row number (index into <see cref="TreeRows"/>).</summary>
    internal int TreeIndex { get; set; }
    /// <summary>Visible rows of a <c>DvTreeView</c> (expanded branches only).</summary>
    internal List<TreeRow> TreeRows { get; set; } = new();
    /// <summary>All items of a <c>DvTreeView</c> (including collapsed branches; used when a handle finds an item by value and expands its ancestors).</summary>
    internal List<TreeRow> TreeAll { get; set; } = new();
    /// <summary>The list box, if this is a row created by <c>DvListBox</c> (null otherwise).</summary>
    internal LayoutBox? ListOwner { get; set; }
    /// <summary>Option number of the list row (index into <see cref="SelectOptions"/>).</summary>
    internal int ListIndex { get; set; }
    /// <summary>Whether a <c>DvListBox</c> allows multiple selection (<c>selection-mode="multi"</c>).</summary>
    internal bool ListMulti { get; set; }
    /// <summary>Whether each option of a <c>DvListBox</c> is selected.</summary>
    internal bool[]? ListSelected { get; set; }
    /// <summary>The <c>option</c> of a <c>DvRadio</c> (the value this button selects, as text).</summary>
    internal string? RadioOption { get; set; }
    /// <summary>The group key of a <c>DvRadio</c> without <c>value</c>: the <c>group</c> name, or the element node (if it only has a handle). Null if neither (cannot be pressed).</summary>
    internal object? RadioGroup { get; set; }
    /// <summary>The <c>on</c> attribute value of a <c>DvLamp</c> (whether it is lit). Filled at build time.</summary>
    public bool LampOn { get; set; }
    /// <summary>Whether a <c>DvLamp</c> blinks (<c>blink-when</c>, or the <c>blink</c> attribute if absent). Meaningful only while lit; the phase is set by the paint time.</summary>
    public bool LampBlink { get; set; }
    /// <summary><c>DvLamp</c> blink period (ms, <c>blink-interval</c> attribute, default 500). Values of 0 or less and non-numbers are ignored and the default is used.</summary>
    public int BlinkIntervalMs { get; set; } = 500;
    /// <summary>The lit color set by the <c>color</c> attribute of a <c>DvLamp</c> (<c>#색</c> or <c>token(이름)</c> resolved against the theme). <see langword="null"/> if not set or not resolvable, in which case <see cref="LampState"/> or the text color is used.</summary>
    public DvColor? LampColor { get; set; }
    /// <summary>The <c>state</c> attribute value of a <c>DvLamp</c> (e.g. <c>run</c>, <c>fault</c>). When <see cref="LampColor"/> is absent, the lamp lights with the theme token <c>state-{값}</c> color.</summary>
    public string? LampState { get; set; }
    /// <summary>Current value of <c>DvGauge</c>/<c>DvRing</c>/<c>DvProgress</c> (<c>value</c> attribute, default 0). 0 if it cannot be converted to a number.</summary>
    public float GaugeValue { get; set; }
    /// <summary>Minimum of <c>DvGauge</c>/<c>DvRing</c>/<c>DvProgress</c> (<c>min</c>, default 0).</summary>
    public float GaugeMin { get; set; }
    /// <summary>Maximum of <c>DvGauge</c>/<c>DvRing</c>/<c>DvProgress</c> (<c>max</c>, default 100).</summary>
    public float GaugeMax { get; set; } = 100f;
    /// <summary>Alarm zones of <c>DvGauge</c>/<c>DvRing</c>/<c>DvProgress</c> (the parsed <c>alarm-zones</c> attribute). Empty list if none.</summary>
    public List<GaugeZone> GaugeZones { get; set; } = new();
    /// <summary><c>DvGauge</c> major tick interval (in value units, <c>ticks</c> attribute). If 0, major ticks are not painted.</summary>
    public float GaugeTicks { get; set; }
    /// <summary><c>DvGauge</c> minor tick interval (in value units, <c>minor-ticks</c> attribute). If 0, minor ticks are not painted.</summary>
    public float GaugeMinorTicks { get; set; }
    /// <summary>Whether <c>DvGauge</c> shows major tick numbers (<c>show-tick-labels</c>, default <see langword="true"/>). Turned off only when the value is <c>false</c>.</summary>
    public bool GaugeTickLabels { get; set; } = true;
    // DvRing / DvProgress (값·범위·zone 은 Gauge* 필드 공용)
    /// <summary>Color tone of <c>DvRing</c>/<c>DvProgress</c> (<c>tone</c>, default <c>info</c>). Uses the color of the theme token <c>tone-{값}</c>.</summary>
    public string Tone { get; set; } = "info";
    /// <summary>Fill direction of <c>DvProgress</c> (<c>direction</c>: <c>right</c> (default), <c>left</c>, <c>up</c>, <c>down</c>). Unknown values are painted like <c>right</c>.</summary>
    public string Direction { get; set; } = "right";
    /// <summary>Whether to show the value text on <c>DvRing</c>/<c>DvProgress</c> (<c>show-value</c>). <c>DvProgress</c> does not show it when vertical or when its height is under 14px.</summary>
    public bool ShowValue { get; set; }
    /// <summary>Unit appended after the value (<c>unit</c> attribute). Used by <c>DvRing</c>, <c>DvProgress</c> and <c>DvInput</c>.</summary>
    public string? Unit { get; set; }
    /// <summary>Name shown inside a <c>DvRing</c> (<c>label</c> attribute). <see langword="null"/> if absent.</summary>
    public string? Label { get; set; }
    /// <summary>Arc angle of a <c>DvRing</c> (degrees, <c>sweep</c>, default 270). Clamped to 1–360.</summary>
    public float RingSweep { get; set; } = 270f;
    /// <summary>Arc thickness of a <c>DvRing</c> (px, <c>thickness</c>). If <see langword="null"/>, 11% of the size is used. A value of 0 or less becomes <see langword="null"/>.</summary>
    public float? RingThickness { get; set; }
    // DvAlarmBanner / DvAlarmList
    /// <summary>Alarm rows of <c>DvAlarmBanner</c>/<c>DvAlarmList</c> (a snapshot of the <c>items</c> binding copied at build time). Empty list if the bound value is not a list of alarm rows.</summary>
    public IReadOnlyList<Alarms.AlarmRow> AlarmItems { get; set; } = Array.Empty<Alarms.AlarmRow>();
    /// <summary>Communication alarm message of <c>DvAlarmBanner</c>/<c>DvAlarmList</c> (<c>comm-alarm</c>). <see langword="null"/> if absent.</summary>
    public string? CommAlarm { get; set; }
    /// <summary>Acknowledge (ACK) command of <c>DvAlarmBanner</c>/<c>DvAlarmList</c> (raw <c>onack</c> attribute). <see langword="null"/> if absent.</summary>
    public string? OnAck { get; set; }
    /// <summary><c>DvAlarmList</c> row height (px, <c>row-height</c>, default 36). Values of 0 or less are replaced with the default.</summary>
    public float AlarmRowHeight { get; set; } = 36f;
    /// <summary>Whether to show the acknowledge (ACK) button. If <c>show-ack</c> is set, its value is used (shown unless <c>false</c>); otherwise shown only when <see cref="OnAck"/> is present.</summary>
    public bool AlarmShowAck { get; set; }
    /// <summary><c>DvProcess</c> process diagram model (equipment, lines, arrange result). <see langword="null"/> for other tags.</summary>
    internal Process.ProcessDiagram? Process { get; set; }
    /// <summary>Equipment information of a process diagram equipment box (symbol, state values, geometry). <see langword="null"/> for other boxes.</summary>
    internal Process.ProcessNode? Equipment { get; set; }
    /// <summary>Instrument information of a process diagram instrument box (<c>DvInstrument</c>). <see langword="null"/> for other boxes.</summary>
    internal Process.ProcessInstrument? Instrument { get; set; }
    /// <summary>Settings and channel data of a <c>DvTrend</c> (a snapshot copied at build time). <see langword="null"/> for other tags.</summary>
    public Trends.TrendSpec? Trend { get; set; }
    /// <summary>Image path of a <c>DvImage</c> (<c>src</c>, bindable). <see langword="null"/> if absent.</summary>
    public string? ImageSrc { get; set; }
    /// <summary>Fit mode of a <c>DvImage</c> (<c>fit</c>, default <see cref="Widgets.ImageFit.Contain"/>).</summary>
    public ImageFit ImageFit { get; set; } = ImageFit.Contain;
    /// <summary>Current value of a <c>DvTank</c> (<c>value</c>, default 0).</summary>
    public float TankValue { get; set; }
    /// <summary>Minimum of a <c>DvTank</c> (<c>min</c>, default 0).</summary>
    public float TankMin { get; set; }
    /// <summary>Maximum of a <c>DvTank</c> (<c>max</c>, default 100).</summary>
    public float TankMax { get; set; } = 100f;
    /// <summary>Unit of a <c>DvTank</c> (<c>unit</c> attribute, binding resolved).</summary>
    public string? TankUnit { get; set; }
    /// <summary>Alarm zones of a <c>DvTank</c> (<c>alarm-zones</c>). Empty list if none.</summary>
    public List<GaugeZone> TankZones { get; set; } = new();
    /// <summary>Whether a <c>DvTank</c> shows zone boundary ticks and numbers (<c>show-zone-marks</c>, default <see langword="true"/>). Zone colors are still painted when off.</summary>
    public bool TankZoneMarks { get; set; } = true;
    /// <summary>Current value of a <c>DvKnob</c> (<c>value</c>, default 0).</summary>
    public float KnobValue { get; set; }
    /// <summary>Minimum of a <c>DvKnob</c> (<c>min</c>, default 0).</summary>
    public float KnobMin { get; set; }
    /// <summary>Maximum of a <c>DvKnob</c> (<c>max</c>, default 100).</summary>
    public float KnobMax { get; set; } = 100f;
    /// <summary>Value step of a <c>DvKnob</c> (<c>step</c>, default 1). Values of 0 or less become 1.</summary>
    public float KnobStep { get; set; } = 1f;
    /// <summary>Unit of a <c>DvKnob</c> (<c>unit</c> attribute, binding resolved).</summary>
    public string? KnobUnit { get; set; }
    /// <summary>Input mode of a <c>DvKnob</c> (<c>input-mode</c>). With tap or both, a tap without dragging opens the numeric keypad.</summary>
    internal KnobInputMode KnobInput { get; set; }
    /// <summary>
    /// Name of the view model method called just before the value is written (raw <c>onbeforecommit</c> attribute). The method receives the new value as its argument, and the value is written only when it returns <see langword="true"/>. If the method is missing, its signature does not match, or it throws, the value is rejected (Trace warning/error).
    /// <see langword="null"/> if absent.
    /// </summary>
    public string? OnBeforeCommit { get; set; }
    /// <summary>Icon notation (<c>fa-gear</c>, <c>far fa-star</c>, etc.). The <c>icon</c> attribute (bindable) of <c>DvIcon</c>/<c>DvButton</c>; <see langword="null"/> if absent.</summary>
    public string? Icon { get; set; }
    // DvInput (unit 은 Unit 공용)
    /// <summary>Current value of <c>DvInput</c>/<c>DvSelect</c>/<c>DvDateTimeInput</c>/<c>DvColorInput</c> (the value in its original type, read at build time). <see langword="null"/> if absent.</summary>
    public object? InputValue { get; set; }
    /// <summary>Input kind of a <c>DvInput</c> (<c>keyboard-type</c>: <c>numeric</c>, <c>password</c>; any other value means text).</summary>
    public DvEditKind InputKind { get; set; }
    /// <summary>Whether a <c>DvInput</c> is multiline (<c>multiline</c>).</summary>
    public bool InputMultiline { get; set; }
    /// <summary>Hint text shown when the value is empty (<c>placeholder</c>). Used by <c>DvInput</c>, <c>DvSelect</c>, <c>DvDateTimeInput</c> and <c>DvColorInput</c>.</summary>
    public string? InputPlaceholder { get; set; }
    /// <summary>Display format string of a <c>DvInput</c> (<c>format</c>). Used to display the value for numeric input.</summary>
    public string? InputFormat { get; set; }
    /// <summary>Minimum of a numeric <c>DvInput</c> (<c>min</c>). <see langword="null"/> if not set or not a number.</summary>
    public double? InputMin { get; set; }
    /// <summary>Maximum of a numeric <c>DvInput</c> (<c>max</c>). <see langword="null"/> if not set or not a number.</summary>
    public double? InputMax { get; set; }
    // DvSelect
    internal List<SelectOption> SelectOptions { get; set; } = new();
    internal int SelectedIndex { get; set; } = -1;
    /// <summary>Type of the bound property (for value conversion). Null if unknown.</summary>
    internal Type? SelectValueType { get; set; }
    // DvDateTimeInput · DvColorInput
    internal bool IsPicker => Node.Tag is "DvDateTimeInput" or "DvColorInput";
    internal PickerMode PickerMode { get; set; }
    internal bool PickerSeconds { get; set; }
    internal string? PickerFormat { get; set; }
    /// <summary>Type of the bound property (for one-way binding, the type of the current value). Null if unknown.</summary>
    internal Type? PickerValueType { get; set; }
    /// <summary>Whether the element is read-only. <see langword="true"/> if <c>readonly</c> is true or the value is not a two-way binding (one-way or literal). Filled at build time.</summary>
    public bool ReadOnly { get; set; }
    /// <summary><see langword="true"/> while being edited (the target of an edit session) or while its selection popup is open. A focus ring is painted. Changed by <see cref="DvSurface"/>.</summary>
    public bool Editing { get; set; }
    /// <summary>Reason the last value write was rejected. When set, a danger-colored border is painted. <see langword="null"/> once editing ends.</summary>
    public string? EditError { get; set; }
    /// <summary>The in-progress text while editing with the built-in virtual keyboard (shown instead of the value). If <see langword="null"/>, <see cref="InputValue"/> is shown.</summary>
    public string? EditText { get; set; }
    /// <summary><see langword="true"/> while the host's native editor is drawing text over the box. The box's own text is not painted then.</summary>
    public bool EditHideText { get; set; }
    /// <summary>Child boxes, in markup order. Elements whose <c>visible</c> is false are omitted, and <c>DvRepeat</c> is expanded once per item. Filled at build time.</summary>
    public List<LayoutBox> Children { get; } = new();
}

/// <summary>One <c>DvTreeView</c> item: item object, value, text, parent item, depth, has-children flag, and expanded flag.</summary>
internal sealed record TreeRow(object Item, object? Value, string Text, object? Parent, int Depth, bool HasChildren, bool Open);
