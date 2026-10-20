namespace Devinno.UI;

/// <summary>
/// String constants for appearance and display keywords shared by several widgets. Lets C# bound properties use markup keywords without typos.
/// </summary>
/// <remarks>
/// <para>Each nested class name is the markup attribute/style key converted to PascalCase (<c>content-align</c> → <see cref="ContentAlign"/>).
/// Constant values are the same keywords written in markup (lowercase, kebab-case).</para>
/// <para>Keywords used by only one widget family are in <see cref="Values"/>.</para>
/// </remarks>
/// <example>
/// <code>
/// public class PumpVm
/// {
///     public bool Running { get; set; }
///     public string Tone =&gt; Running ? Styles.Tone.Success : Styles.Tone.Danger;   // &lt;DvBadge tone="{Tone}"/&gt;
///     public string LampState { get; set; } = Styles.State.Run;                    // &lt;DvLamp state="{LampState}"/&gt;
///     public string Align { get; set; } = Styles.ContentAlign.MiddleLeft;          // &lt;DvButton content-align="{Align}"/&gt;
/// }
/// </code>
/// </example>
public static class Styles
{
    /// <summary>
    /// <c>content-align</c> keywords (shared by attribute and style): where text (and the icon, for buttons) is placed inside the padding area of <c>DvLabel</c>/<c>DvButton</c>.
    /// Nine values join vertical (top/middle/bottom) and horizontal (left/center/right) with a hyphen; other values are ignored with a one-time warning.
    /// </summary>
    public static class ContentAlign
    {
        /// <summary><c>top-left</c>: top, left. The default for <c>DvLabel</c> and for <c>DvButton</c> without <c>variant</c>.</summary>
        public const string TopLeft = "top-left";
        /// <summary><c>top-center</c>: top, center.</summary>
        public const string TopCenter = "top-center";
        /// <summary><c>top-right</c>: top, right.</summary>
        public const string TopRight = "top-right";
        /// <summary><c>middle-left</c>: vertically centered, left.</summary>
        public const string MiddleLeft = "middle-left";
        /// <summary><c>middle-center</c>: dead center. The default for <c>DvButton</c> with <c>variant</c>.</summary>
        public const string MiddleCenter = "middle-center";
        /// <summary><c>middle-right</c>: vertically centered, right.</summary>
        public const string MiddleRight = "middle-right";
        /// <summary><c>bottom-left</c>: bottom, left.</summary>
        public const string BottomLeft = "bottom-left";
        /// <summary><c>bottom-center</c>: bottom, center.</summary>
        public const string BottomCenter = "bottom-center";
        /// <summary><c>bottom-right</c>: bottom, right.</summary>
        public const string BottomRight = "bottom-right";
    }

    /// <summary><c>text-align</c> style keywords: horizontal text alignment. To also set the vertical position, use <see cref="ContentAlign"/>.</summary>
    public static class TextAlign
    {
        /// <summary><c>left</c>: left-aligned.</summary>
        public const string Left = "left";
        /// <summary><c>center</c>: centered.</summary>
        public const string Center = "center";
        /// <summary><c>right</c>: right-aligned.</summary>
        public const string Right = "right";
    }

    /// <summary><c>overflow</c> style keywords: handling when children overflow the box. Unknown values are treated as <see cref="Visible"/>.</summary>
    public static class Overflow
    {
        /// <summary><c>visible</c>: no clipping (the default).</summary>
        public const string Visible = "visible";
        /// <summary><c>hidden</c>: clips children to the box's rounded border.</summary>
        public const string Hidden = "hidden";
        /// <summary><c>scroll</c>: clips and scrolls along the overflowing axis (wheel, drag, inertia, scroll thumb). Space for the thumb is reserved on the overflowing axis.</summary>
        public const string Scroll = "scroll";
    }

    /// <summary><c>border-style</c> style keywords: border line style (can also be written inside <c>border</c>, as in <c>border: 1 dashed #cbd5e1</c>).</summary>
    public static class BorderStyle
    {
        /// <summary><c>solid</c>: solid line (the default).</summary>
        public const string Solid = "solid";
        /// <summary><c>dashed</c>: dashed line.</summary>
        public const string Dashed = "dashed";
    }

    /// <summary>
    /// <c>tone</c> attribute keywords (<c>DvBadge</c>, <c>DvStat</c>, <c>DvRing</c>, <c>DvProgress</c>): semantic accent colors.
    /// Colors come from the theme tokens <c>tone-{값}</c>, <c>tone-{값}-fg</c> and <c>tone-{값}-soft</c>, where the placeholder is the tone value.
    /// </summary>
    public static class Tone
    {
        /// <summary><c>neutral</c>: neutral (no particular meaning). The default for <c>DvBadge</c>.</summary>
        public const string Neutral = "neutral";
        /// <summary><c>info</c>: information. The default for <c>DvStat</c>, <c>DvRing</c> and <c>DvProgress</c>.</summary>
        public const string Info = "info";
        /// <summary><c>success</c>: normal, success.</summary>
        public const string Success = "success";
        /// <summary><c>warning</c>: caution.</summary>
        public const string Warning = "warning";
        /// <summary><c>danger</c>: danger, abnormal.</summary>
        public const string Danger = "danger";
    }

    /// <summary>
    /// <c>state</c> attribute keywords (<c>DvLamp</c>, <c>DvStatusItem</c>, and string values of grid <c>lamp</c> cells): equipment operating state.
    /// Colors come from the theme tokens <c>state-{값}</c>, <c>state-{값}-fg</c> and <c>state-{값}-soft</c>, where the placeholder is the state value.
    /// </summary>
    public static class State
    {
        /// <summary><c>run</c>: running.</summary>
        public const string Run = "run";
        /// <summary><c>stop</c>: stopped.</summary>
        public const string Stop = "stop";
        /// <summary><c>fault</c>: fault.</summary>
        public const string Fault = "fault";
        /// <summary><c>warn</c>: warning.</summary>
        public const string Warn = "warn";
        /// <summary><c>manual</c>: manual operation.</summary>
        public const string Manual = "manual";
        /// <summary><c>off</c>: off (power cut, unused). The default for <c>DvStatusItem</c>.</summary>
        public const string Off = "off";
    }

    /// <summary>
    /// <c>variant</c> attribute keywords (<c>DvButton</c>, grid columns with <c>cell-type="button"</c>): button appearance type.
    /// A <c>DvButton</c> without <c>variant</c> is a plain, undecorated text box.
    /// </summary>
    public static class Variant
    {
        /// <summary><c>primary</c>: primary action button. Background in the theme's <c>primary</c> color family.</summary>
        public const string Primary = "primary";
        /// <summary><c>secondary</c>: secondary action button. Surface-color background with a border.</summary>
        public const string Secondary = "secondary";
        /// <summary><c>danger</c>: button for dangerous actions (stop, delete, etc.). Uses the <c>tone-danger</c> color family.</summary>
        public const string Danger = "danger";
        /// <summary><c>ghost</c>: button without a background. Used for less important actions.</summary>
        public const string Ghost = "ghost";
    }

    /// <summary>
    /// <c>size</c> attribute keywords of <c>DvButton</c>: button size (font size and padding). Because the attribute name is common, the widget name is included in the class name.
    /// When omitted, the button is normal size; applies only to buttons with <c>variant</c>.
    /// </summary>
    public static class ButtonSize
    {
        /// <summary><c>sm</c>: small button (font 12, padding 7·12).</summary>
        public const string Sm = "sm";
        /// <summary><c>lg</c>: large button (font 16, padding 14·26).</summary>
        public const string Lg = "lg";
    }
}
