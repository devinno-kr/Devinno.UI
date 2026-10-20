namespace Devinno.UI.Styling;

/// <summary>
/// Main-axis direction of a flex container. Comes from the style key <c>flex-direction</c>.
/// </summary>
public enum FlexDirection
{
    /// <summary>Lays out children horizontally (<c>flex-direction: row</c>, the default).</summary>
    Row,
    /// <summary>Lays out children vertically (<c>flex-direction: column</c>).</summary>
    Column
}

/// <summary>
/// Defines how a flex container distributes free space along the main axis. Comes from the style key <c>justify-content</c>.
/// </summary>
public enum JustifyContent
{
    /// <summary>Packs items at the start of the main axis (<c>start</c>, <c>flex-start</c>, and unrecognized values; the default).</summary>
    Start,
    /// <summary>Centers items on the main axis (<c>center</c>).</summary>
    Center,
    /// <summary>Packs items at the end of the main axis (<c>end</c>, <c>flex-end</c>).</summary>
    End,
    /// <summary>Places the first and last child at both ends and distributes the remaining space evenly between children (<c>space-between</c>).</summary>
    SpaceBetween
}

/// <summary>
/// Cross-axis alignment. Used both by the container's <c>align-items</c> and by a child's own <c>align-self</c>.
/// </summary>
public enum AlignItems
{
    /// <summary>Aligns to the start of the cross axis (<c>start</c>, <c>flex-start</c>; the default of <c>align-items</c>).</summary>
    Start,
    /// <summary>Centers on the cross axis (<c>center</c>).</summary>
    Center,
    /// <summary>Aligns to the end of the cross axis (<c>end</c>, <c>flex-end</c>).</summary>
    End,
    /// <summary>Stretches the cross-axis size to fill the container (<c>stretch</c>).</summary>
    Stretch
}

/// <summary>
/// Defines how content that exceeds the box bounds is handled. Comes from the style key <c>overflow</c>.
/// </summary>
public enum Overflow
{
    /// <summary>Draws content as-is without clipping (<c>visible</c>, and unrecognized values; the default).</summary>
    Visible,
    /// <summary>Clips children at the box bounds, including rounded corners (<c>hidden</c>).</summary>
    Hidden,
    /// <summary>Clips at the bounds and scrolls along the axis where content overflows (wheel, drag, inertia, scroll bars). The value is <c>scroll</c>.</summary>
    Scroll
}

/// <summary>
/// Defines how the lines of a multi-line flex container (<c>flex-wrap: wrap</c>) are placed on the cross axis.
/// Comes from the style key <c>align-content</c>.
/// </summary>
public enum AlignContent
{
    /// <summary>Packs lines at the start of the cross axis (the default, and unrecognized values).</summary>
    Start,
    /// <summary>Centers the group of lines on the cross axis (<c>center</c>).</summary>
    Center,
    /// <summary>Packs the group of lines at the end of the cross axis (<c>end</c>, <c>flex-end</c>).</summary>
    End,
    /// <summary>With two or more lines, distributes the remaining space evenly between lines (<c>space-between</c>).</summary>
    SpaceBetween,
    /// <summary>
    /// Parsed from the value <c>stretch</c>, but the current layout engine has no dedicated handling for it, so lines are placed as with <see cref="Start"/>.
    /// </summary>
    Stretch
}

/// <summary>
/// Positioning scheme of an element. Comes from the style key <c>position</c>.
/// </summary>
public enum Position
{
    /// <summary>Placed by the normal flow (flex, grid, stack); the default.</summary>
    Static,
    /// <summary>
    /// Parsed from the value <c>relative</c>, but the current layout engine treats it exactly like <see cref="Static"/>
    /// (<c>left</c>/<c>top</c> offsets are not applied).
    /// </summary>
    Relative,
    /// <summary>
    /// Taken out of the normal flow and placed at <c>left</c>/<c>top</c>/<c>right</c>/<c>bottom</c> relative to the parent's padding box.
    /// Giving both <c>left</c> and <c>right</c> sets the width, and both <c>top</c> and <c>bottom</c> sets the height, to the span between them.
    /// </summary>
    Absolute
}

/// <summary>
/// Horizontal alignment of text (and button icons). Comes from the style key <c>text-align</c> and the horizontal part of <c>content-align</c>.
/// </summary>
public enum TextAlign
{
    /// <summary>Left-aligned (the default, and unrecognized values).</summary>
    Left,
    /// <summary>Centered (<c>center</c>).</summary>
    Center,
    /// <summary>Right-aligned (<c>right</c>).</summary>
    Right
}

/// <summary>Vertical axis of <c>content-align</c> (the horizontal axis is <see cref="TextAlign"/>).</summary>
internal enum ContentVAlign
{
    Top,
    Middle,
    Bottom
}

/// <summary>
/// A single box shadow. Created from the style key <c>box-shadow: dx dy blur color</c>.
/// </summary>
/// <remarks>
/// <para>The value is exactly <c>dx dy blur color</c>, separated by spaces (e.g. <c>box-shadow: 0 8 24 #00000040</c>).
/// Spread, inset and multiple shadows are not supported. The first three values are numbers (px; the <c>px</c>/<c>dp</c> suffix is allowed) and become 0 if they cannot be parsed.
/// The fourth value must be a color accepted by <see cref="DvColor.TryParse"/>; if there are fewer than four values or the color cannot be parsed, the whole declaration is ignored.</para>
/// <para><c>box-shadow: none</c> clears a shadow set earlier (e.g. by a class).</para>
/// <para>When drawn, the same rounded rectangle as the box is offset by (<paramref name="Dx"/>, <paramref name="Dy"/>) and filled with a blur sigma of <c>Blur / 2</c>.</para>
/// </remarks>
/// <param name="Dx">Horizontal offset (px). Positive values shift right.</param>
/// <param name="Dy">Vertical offset (px). Positive values shift down.</param>
/// <param name="Blur">Blur radius (px). 0 gives a solid shadow with no blur.</param>
/// <param name="Color">Shadow color. A semi-transparent alpha (<c>#RRGGBBAA</c>) is typical.</param>
public readonly record struct BoxShadow(float Dx, float Dy, float Blur, DvColor Color);

/// <summary>
/// The final set of style values applied to one element. Filled by <see cref="StyleParser.Parse"/> from the declaration strings
/// collected from classes, the inline <c>style</c>, and so on.
/// </summary>
/// <remarks>
/// <para>This is an internal engine stage. App code normally does not create this object directly; it uses <c>style</c>/<c>class</c>/<c>&lt;DvStyle&gt;</c> in markup.
/// The result is used by <see cref="DvSurface"/>/<see cref="DvRenderer"/> for layout and drawing.</para>
/// <para>The "style key" in each property description is the CSS-like key that sets that value. All length values are numbers in px; the <c>px</c>/<c>dp</c> suffix is stripped when reading.
/// Units such as <c>%</c>, <c>auto</c> and <c>em</c> are not parsed as numbers, so they mostly become 0 (grid tracks are the exception).
/// If the same key appears more than once, the later declaration wins.</para>
/// <para>The properties are settable, but the engine creates a new instance on every layout. Changing values after rendering is not reflected on screen.</para>
/// </remarks>
public sealed class ComputedStyle
{
    /// <summary>
    /// Solid background. Set when the style key <c>background</c> or <c>background-color</c> is given a color (<c>#RGB</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c>, <c>transparent</c>).
    /// <c>background-color</c> is an alias that accepts solid colors only (use <c>background</c> for gradients); if both keys are present, the later declaration wins, as in CSS.
    /// Default <see langword="null"/> = no background is painted. Setting this value clears <see cref="Gradient"/> to <see langword="null"/>.
    /// </summary>
    public DvColor? Background { get; set; }

    /// <summary>
    /// Linear gradient background. Comes from the style key <c>background: linear-gradient(...)</c> (format: see <see cref="LinearGradient.TryParse"/>).
    /// Default <see langword="null"/>. Setting this value clears <see cref="Background"/>; if both are present, the gradient is drawn.
    /// If the gradient fails to parse, the declaration is ignored and the previous background remains.
    /// </summary>
    public LinearGradient? Gradient { get; set; }

    /// <summary>
    /// Text color (icon color for icons). Style key <c>color</c>. Default <c>#222222</c> (opaque).
    /// If the color cannot be parsed, the declaration is ignored and the previous value remains.
    /// </summary>
    public DvColor Color { get; set; } = new(0x22, 0x22, 0x22, 255);

    /// <summary>
    /// Opacity (0 to 1). Style key <c>opacity</c>; default 1, and 1 if parsing fails. The box and all its children are composited as one layer and drawn semi-transparent (same as CSS).
    /// Applies to drawing only; layout and hit areas are unchanged.
    /// </summary>
    public float Opacity { get; set; } = 1f;

    /// <summary>
    /// Drawing transform (translate, scale). Style key <c>transform</c>; default is no change. Children are transformed too; layout and hit areas are unchanged.
    /// </summary>
    public Transform2D Transform { get; set; } = Transform2D.Identity;

    /// <summary>
    /// Properties whose values change gradually when state changes (hover, press, disabled, class, bound value). Style key <c>transition</c>; default none (changes immediately).
    /// </summary>
    public IReadOnlyList<TransitionSpec> Transitions { get; set; } = Array.Empty<TransitionSpec>();

    /// <summary>Shallow copy used to draw with changed values (transition intermediate values).</summary>
    internal ComputedStyle Copy() => (ComputedStyle)MemberwiseClone();

    /// <summary>Corner radius (px). Style key <c>border-radius</c>, shared by all four corners. Default 0, and 0 if parsing fails.</summary>
    public float BorderRadius { get; set; }

    /// <summary>
    /// Border thickness (px). The first value of the style key <c>border</c> (e.g. <c>border: 1 #cbd5e1</c> → 1). Default 0.
    /// There is no separate <c>border-width</c> key.
    /// </summary>
    public float BorderWidth { get; set; }

    /// <summary>
    /// Border color. Set when the last value of the style key <c>border</c> is a color (e.g. <c>border: 1 solid #cbd5e1</c>).
    /// Default <see langword="null"/>. If the last value is not a color (e.g. <c>border: 0</c>), the previous value remains. There is no separate <c>border-color</c> key.
    /// </summary>
    public DvColor? BorderColor { get; set; }

    /// <summary>
    /// Whether the border is drawn dashed. <see langword="true"/> when <c>dashed</c> is present, as in <c>border-style: dashed</c> or <c>border: 1 dashed #c</c>;
    /// other <c>border-style</c> values or <c>solid</c> inside <c>border</c> reset it to <see langword="false"/>. Default <see langword="false"/>.
    /// </summary>
    public bool BorderDashed { get; set; }

    /// <summary>
    /// Box shadow. Style key <c>box-shadow</c> (format: see <see cref="BoxShadow"/>). Default <see langword="null"/> = no shadow;
    /// it can be cleared with <c>box-shadow: none</c>.
    /// </summary>
    public BoxShadow? Shadow { get; set; }

    /// <summary>
    /// Inner spacing (px). Style key <c>padding</c>, 1 to 4 values (format: see <see cref="Thickness.TryParse"/>). Default <see cref="Thickness.Zero"/>.
    /// If parsing fails (including 3 values), the declaration is ignored.
    /// </summary>
    public Thickness Padding { get; set; } = Thickness.Zero;

    /// <summary>
    /// Outer spacing (px). Style key <c>margin</c>; same format as <see cref="Padding"/>. Default <see cref="Thickness.Zero"/>.
    /// It is parsed only and not applied by the current layout. For spacing, use <see cref="Gap"/> or the parent's <see cref="Padding"/>.
    /// </summary>
    public Thickness Margin { get; set; } = Thickness.Zero;

    /// <summary>
    /// Font size (px). Style key <c>font-size</c>. Default 14, and 14 if parsing fails.
    /// For icons (<c>DvIcon</c>) it is used as the icon size.
    /// </summary>
    public float FontSize { get; set; } = 14f;

    /// <summary>
    /// List of font names. Style key <c>font-family</c>; several can be given separated by commas, and quotes are stripped.
    /// The first font in the list that is installed and has every character of the string is used; otherwise a fallback font is found per character.
    /// Default <see langword="null"/> = inherited from the parent (the system default font if none up to the root).
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// Whether the font is bold. <see langword="true"/> when the style key <c>font-weight</c> is <c>bold</c> or a number of 600 or more;
    /// other values (<c>normal</c>, etc.) are <see langword="false"/>. Default <see langword="false"/>.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Horizontal text alignment. Style key <c>text-align</c> (<c>left</c>, <c>center</c>, <c>right</c>; case-sensitive) or the horizontal part of <c>content-align</c>.
    /// Within one declaration list, whichever of the two comes later wins. Default <see cref="TextAlign.Left"/>.
    /// </summary>
    public TextAlign TextAlign { get; set; } = TextAlign.Left;

    /// <summary>
    /// Style key <c>white-space</c>: <c>nowrap</c> gives <see langword="true"/> (one line even if the width is too small; breaks only at <c>\n</c>), <c>normal</c> gives <see langword="false"/> (wraps to the laid-out width).
    /// <see langword="null"/> = not specified — inherited from the parent (<c>normal</c> if none up to the root). Inherited as in CSS.
    /// </summary>
    internal bool? NoWrap { get; set; }

    /// <summary>Vertical axis of <c>content-align</c>. null = widget default (centered for icon-only, otherwise top).</summary>
    internal ContentVAlign? ContentVertical { get; set; }

    /// <summary>
    /// Spacing between children (px). Style key <c>gap</c>. In flex it goes between children and between lines; in grid, between tracks.
    /// Default 0, and 0 if parsing fails.
    /// </summary>
    public float Gap { get; set; }

    /// <summary>
    /// Whether this is a flex container. <see langword="true"/> for the style key <c>display: flex</c>. Other <c>display</c> values reset it to
    /// <see langword="false"/>. Default <see langword="false"/> (<c>DvStack</c>/<c>DvRow</c> are already flex as layout sugar).
    /// </summary>
    public bool IsFlex { get; set; }

    /// <summary>
    /// Whether this is a grid container. <see langword="true"/> for the style key <c>display: grid</c>. Default <see langword="false"/>.
    /// Grid layout is applied only when <see cref="GridColumns"/> or <see cref="GridRows"/> has at least one track.
    /// </summary>
    public bool IsGrid { get; set; }

    /// <summary>
    /// Column track list. Style key <c>grid-template-columns</c>, separated by spaces; the whole value may be wrapped in quotes.
    /// Each track is a number (px), <c>Nfr</c> (share of remaining space), <c>N%</c> (share of the space left after fixed tracks), or <c>auto</c> (content size).
    /// The default is an empty array.
    /// </summary>
    public string[] GridColumns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Row track list. Style key <c>grid-template-rows</c>. Same format as <see cref="GridColumns"/>; rows beyond the template are <c>auto</c>.
    /// The default is an empty array.
    /// </summary>
    public string[] GridRows { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Width/height ratio. Style key <c>aspect-ratio</c> (<c>1</c>, <c>1.5</c>, <c>16/9</c>). <see langword="null"/> if 0 or less, or if it cannot be parsed.
    /// The box is fitted to the largest box of that ratio inside the assigned area (contain, centered). If only one of width or height is specified, the other is computed from the ratio.
    /// Default <see langword="null"/> = no fixed ratio.
    /// </summary>
    public float? AspectRatio { get; set; }

    /// <summary>Flex main-axis direction. Style key <c>flex-direction</c> (<c>column</c> is vertical, anything else horizontal). Default <see cref="FlexDirection.Row"/>.</summary>
    public FlexDirection FlexDirection { get; set; } = FlexDirection.Row;

    /// <summary>
    /// Main-axis alignment. Style key <c>justify-content</c> (<c>center</c>, <c>end</c>, <c>flex-end</c>, <c>space-between</c>; anything else is start). Default <see cref="JustifyContent.Start"/>.
    /// </summary>
    public JustifyContent Justify { get; set; } = JustifyContent.Start;

    /// <summary>
    /// Cross-axis alignment (all children). Style key <c>align-items</c> (<c>start</c>, <c>flex-start</c>, <c>center</c>, <c>end</c>, <c>flex-end</c>, <c>stretch</c>). Default <see cref="AlignItems.Stretch"/> — as in CSS, stretches children that do not specify a cross-axis size (width/height).
    /// </summary>
    public AlignItems Align { get; set; } = AlignItems.Stretch;

    /// <summary>
    /// Cross-axis alignment for this element only. Style key <c>align-self</c> (<c>start</c>, <c>flex-start</c>, <c>center</c>, <c>end</c>, <c>flex-end</c>, <c>stretch</c>).
    /// Default <see langword="null"/> = follows the parent's <see cref="Align"/>. Unrecognized values are also <see langword="null"/>.
    /// </summary>
    public AlignItems? AlignSelf { get; set; }

    /// <summary>Overflow handling. Style key <c>overflow</c> (<c>hidden</c>, <c>scroll</c>; anything else is visible). Default <see cref="Overflow.Visible"/>.</summary>
    public Overflow Overflow { get; set; } = Overflow.Visible;

    /// <summary>
    /// Specified width (px). Style key <c>width</c>. Default <see langword="null"/> = determined by content and layout.
    /// Note that values that cannot be parsed as numbers (<c>50%</c>, <c>auto</c>, etc.) become 0, not <see langword="null"/>.
    /// </summary>
    public float? Width { get; set; }

    /// <summary>
    /// Specified height (px). Style key <c>height</c>. Default <see langword="null"/> = determined by content and layout.
    /// Values that cannot be parsed as numbers become 0.
    /// </summary>
    public float? Height { get; set; }

    /// <summary>Minimum width (px). Style key <c>min-width</c>. Default <see langword="null"/> = no limit. Unparsable values become 0.</summary>
    public float? MinWidth { get; set; }

    /// <summary>Maximum width (px). Style key <c>max-width</c>. Default <see langword="null"/> = no limit. Unparsable values become 0.</summary>
    public float? MaxWidth { get; set; }

    /// <summary>Minimum height (px). Style key <c>min-height</c>. Default <see langword="null"/> = no limit. Unparsable values become 0.</summary>
    public float? MinHeight { get; set; }

    /// <summary>Maximum height (px). Style key <c>max-height</c>. Default <see langword="null"/> = no limit. Unparsable values become 0.</summary>
    public float? MaxHeight { get; set; }

    /// <summary>
    /// Share of the remaining main-axis space this element receives. Style key <c>flex-grow</c> or the first value of <c>flex</c>. Default 0 (does not grow).
    /// </summary>
    public float FlexGrow { get; set; }

    /// <summary>
    /// Rate at which this element shrinks when main-axis space is insufficient. Style key <c>flex-shrink</c> or the second value of <c>flex</c> (1 when there is only one value, as in <c>flex: N</c>).
    /// The default is 0, so unlike CSS (default 1), elements do not shrink unless specified.
    /// However, an <c>overflow:scroll</c> box is treated as 1 when unspecified, as in CSS, so it shrinks when space is short; a flex box that contains such a scroll box (when unspecified)
    /// also shrinks along with it, as far as the inner scroll box can shrink (cases where CSS would need <c>min-height:0</c>).
    /// </summary>
    public float FlexShrink
    {
        get => _flexShrink ?? 0f;
        set => _flexShrink = value;
    }

    private float? _flexShrink;

    /// <summary>Whether <c>flex-shrink</c> (or <c>flex</c>) was specified — used to decide the default shrink of 1 for scroll boxes.</summary>
    internal bool FlexShrinkSet => _flexShrink.HasValue;

    /// <summary>
    /// Whether overflowing children wrap to the next line. <see langword="true"/> for the style key <c>flex-wrap: wrap</c>; other values give <see langword="false"/>.
    /// Has an effect only when <see cref="IsFlex"/>. Default <see langword="false"/>.
    /// </summary>
    public bool Wrap { get; set; }

    /// <summary>Cross-axis placement of the line group in multi-line flex. Style key <c>align-content</c>. Default <see cref="AlignContent.Start"/>.</summary>
    public AlignContent AlignContent { get; set; } = AlignContent.Start;

    /// <summary>Positioning scheme. Style key <c>position</c> (<c>absolute</c>, <c>relative</c>; anything else is normal flow). Default <see cref="Position.Static"/>.</summary>
    public Position Position { get; set; } = Position.Static;

    /// <summary>
    /// Distance (px) from the left edge of the parent's padding box. Style key <c>left</c>. Used only with <see cref="Position.Absolute"/>. Default <see langword="null"/>.
    /// </summary>
    public float? Left { get; set; }

    /// <summary>
    /// Distance (px) from the top edge of the parent's padding box. Style key <c>top</c>. Used only with <see cref="Position.Absolute"/>. Default <see langword="null"/>.
    /// </summary>
    public float? Top { get; set; }

    /// <summary>
    /// Distance (px) from the right edge of the parent's padding box. Style key <c>right</c>. With <see cref="Position.Absolute"/>, sets the position when <see cref="Left"/> is absent,
    /// or the width when both are present. Default <see langword="null"/>.
    /// </summary>
    public float? Right { get; set; }

    /// <summary>
    /// Distance (px) from the bottom edge of the parent's padding box. Style key <c>bottom</c>. With <see cref="Position.Absolute"/>, sets the position when <see cref="Top"/> is absent,
    /// or the height when both are present. Default <see langword="null"/>.
    /// </summary>
    public float? Bottom { get; set; }
}
