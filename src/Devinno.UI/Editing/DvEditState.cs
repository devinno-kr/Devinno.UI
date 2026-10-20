using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>
/// The reason an edit session ended. The host passes it to <see cref="DvSurface.CommitEdit"/>, and the core uses it to decide how to handle a rejection and what to do next.
/// </summary>
/// <remarks>
/// If the commit is rejected (validation failure or <c>onbeforecommit</c> refusal), <see cref="FocusLost"/> closes the session without writing the value; all other reasons keep the session open and show the rejection reason.
/// </remarks>
public enum DvEditExit
{
    /// <summary>Confirmed with the Enter key (or the virtual keyboard's Done key). For a grid input cell in native editor mode, editing continues in the same column of the row below.</summary>
    Enter,
    /// <summary>Confirmed with the Tab key. For a grid input cell in native editor mode, editing continues in the next input column of the same row.</summary>
    Tab,
    /// <summary>Confirmed with Shift+Tab. For a grid input cell in native editor mode, editing continues in the previous input column of the same row.</summary>
    ShiftTab,
    /// <summary>Confirmed with the Up arrow key (single-line editor). Currently it only commits and does not move to another cell.</summary>
    Up,
    /// <summary>Confirmed with the Down arrow key (single-line editor). Currently it only commits and does not move to another cell.</summary>
    Down,
    /// <summary>
    /// The editor lost focus (a press elsewhere, a screen change, the edit target scrolled completely out of view, etc.).
    /// If the value is valid it is written; if rejected, it is not written and the session closes with the original value.
    /// </summary>
    FocusLost
}

/// <summary>
/// Input rules for a numeric edit session. Integer/floating-point and whether a sign is allowed come from the bound property's type; range and unit come from markup attributes.
/// Passed as <see cref="DvEditState.Numeric"/> when the edit kind is <see cref="DvEditKind.Numeric"/>.
/// </summary>
/// <remarks>The host uses it to restrict input in the native editor (whether a decimal point and sign are allowed) and to reserve room for the unit display. Final validation is done by the core at commit.</remarks>
/// <param name="IsInteger"><see langword="true"/> if only integers are accepted: <see langword="true"/> when the property type is <c>int</c>, <c>long</c>, <c>short</c>, <c>sbyte</c>, <c>uint</c>, <c>ulong</c>, <c>ushort</c> or <c>byte</c> (including nullable); <see langword="false"/> for <c>double</c>, <c>float</c>, <c>decimal</c>, etc. (decimal point allowed).</param>
/// <param name="IsSigned"><see langword="true"/> if negative numbers are accepted. Only unsigned integers (<c>uint</c>, <c>ulong</c>, <c>ushort</c>, <c>byte</c>) are <see langword="false"/>.</param>
/// <param name="Min">Minimum allowed value (<c>min</c> attribute). <see langword="null"/> means no limit. Out-of-range values cause the commit to be rejected (not clamped).</param>
/// <param name="Max">Maximum allowed value (<c>max</c> attribute). <see langword="null"/> means no limit. Out-of-range values cause the commit to be rejected.</param>
/// <param name="Unit">Unit string (<c>unit</c> attribute, for example <c>℃</c>). The core draws it at the right of the edit box, so the native editor must be narrowed by that amount. <see langword="null"/> if none.</param>
public sealed record DvNumericSpec(bool IsInteger, bool IsSigned, double? Min, double? Max, string? Unit);

/// <summary>
/// The state of the current edit session (<see cref="DvSurface.Edit"/>). This is the host adapter contract: the core updates it during layout, render and edit calls,
/// and a host that uses a native editor reads it right after rendering to sync the editor.
/// </summary>
/// <remarks>
/// <para>How the host reads it:</para>
/// <list type="bullet">
/// <item>If <see cref="IsEditing"/> is <see langword="false"/>, hide the editor.</item>
/// <item>If <see cref="SessionId"/> differs from the last value seen, it is a new edit target. Apply <see cref="InitialText"/>, <see cref="Kind"/>, <see cref="Multiline"/> and <see cref="Placeholder"/>,
/// sync the position and appearance, then give the editor focus.</item>
/// <item>If <see cref="SessionId"/> is the same and only <see cref="Version"/> changed, re-sync only the position, clip, colors, font and error. Do not touch the text being typed.</item>
/// </list>
/// <para>Coordinates are canvas coordinates as passed to <see cref="DvSurface.RenderTo(SKCanvas, float, float, long)"/>. Conversion to screen coordinates and DPI is done by the host.
/// All properties are read-only; read them on the UI thread.</para>
/// </remarks>
public sealed class DvEditState
{
    /// <summary>Gets whether an edit session is open (<see langword="true"/> if so).</summary>
    public bool IsEditing { get; private set; }

    /// <summary>
    /// Gets the edit session number. Each new session gets a new value increasing from 1, so the host may use 0 to mean "no session".
    /// Pass it to <see cref="DvSurface.SetEditText"/>, <see cref="DvSurface.CommitEdit"/> and <see cref="DvSurface.CancelEdit"/> so that late calls for an already finished session are ignored.
    /// </summary>
    public int SessionId { get; private set; }

    /// <summary>Gets the state version. It increases by 1 whenever a session starts or ends or any of the properties below changes. The host compares it with the last value seen to decide whether to re-sync.</summary>
    public int Version { get; private set; }

    /// <summary>Gets the area of the edit target box (or grid cell or popup input cell), in canvas coordinates with the scroll position applied.</summary>
    public SKRect Rect { get; private set; }

    /// <summary>
    /// Gets the part of <see cref="Rect"/> that is actually visible (its intersection with the clip areas of ancestors such as scroll boxes). The host must hide the part of the editor outside it.
    /// If the target is scrolled completely out of view, the core commits the session with <see cref="DvEditExit.FocusLost"/> and closes it.
    /// </summary>
    public SKRect Clip { get; private set; }

    /// <summary>Gets the input kind (text, numeric, password). Apply it to the editor's masking and input restrictions when a new session starts.</summary>
    public DvEditKind Kind { get; private set; }

    /// <summary>
    /// Gets whether this is a multi-line edit (<see langword="true"/> for <c>DvInput multiline="true"</c>). Always <see langword="false"/> for grid cells and popup input cells.
    /// The host decides whether to use a multi-line editor and treat Enter as a line break. The built-in virtual keyboard supports single-line input only.
    /// </summary>
    public bool Multiline { get; private set; }

    /// <summary>
    /// Gets the value at the start of editing: the original value converted with InvariantCulture without the display format (<c>format</c>); an empty string for passwords
    /// (two-digit numbers for the hour/minute/second cells of picker popups, <c>#RRGGBB</c> for color cells).
    /// Put it into the editor only when <see cref="SessionId"/> changes. Line breaks are <c>\n</c>.
    /// </summary>
    public string InitialText { get; private set; } = "";

    /// <summary>Gets the hint text shown when the value is empty (<c>placeholder</c> attribute). <see langword="null"/> if none.</summary>
    public string? Placeholder { get; private set; }

    /// <summary>Gets the font name actually used. Reflects any fallback font chosen for characters missing from the specified font (such as Korean).</summary>
    public string FontFamily { get; private set; } = "";

    /// <summary>Gets the font size (px, in canvas units).</summary>
    public float FontSize { get; private set; }

    /// <summary>Gets whether the font is bold (<see langword="true"/> if so).</summary>
    public bool Bold { get; private set; }

    /// <summary>Gets the text color.</summary>
    public SKColor TextColor { get; private set; }

    /// <summary>Gets the background color of the edit box: the first color for a gradient background, or the theme's <c>surface</c> color if there is no background. Used to paint the editor opaquely.</summary>
    public SKColor Background { get; private set; }

    /// <summary>
    /// Gets the box's inner padding. This is not a rectangle but holds the four padding values: <c>Left</c>, <c>Top</c>, <c>Right</c> and <c>Bottom</c> are the padding (px) on each side.
    /// Text area = <see cref="Rect"/> minus this padding.
    /// </summary>
    public SKRect Padding { get; private set; }

    /// <summary>Gets the horizontal text alignment (left, center, right).</summary>
    public TextAlign Align { get; private set; }

    /// <summary>Gets the numeric edit rules. Has a value only when <see cref="Kind"/> is <see cref="DvEditKind.Numeric"/>; otherwise <see langword="null"/>.</summary>
    public DvNumericSpec? Numeric { get; private set; }

    /// <summary>
    /// Gets the reason the last commit was rejected (text to show the user). <see langword="null"/> if there was no rejection.
    /// Cleared on the next <see cref="DvSurface.SetEditText"/> call. The core draws an error border on the edit box.
    /// </summary>
    public string? Error { get; private set; }

    internal void Begin(int sessionId, string initialText)
    {
        IsEditing = true;
        SessionId = sessionId;
        InitialText = initialText;
        Version++;
    }

    internal void End()
    {
        if (!IsEditing)
            return;
        IsEditing = false;
        Version++;
    }

    internal readonly record struct Snapshot(
        SKRect Rect, SKRect Clip, DvEditKind Kind, bool Multiline, string? Placeholder,
        string FontFamily, float FontSize, bool Bold, SKColor TextColor, SKColor Background,
        SKRect Padding, TextAlign Align, DvNumericSpec? Numeric, string? Error);

    private Snapshot? _last;

    internal Snapshot ToSnapshot() => new(Rect, Clip, Kind, Multiline, Placeholder, FontFamily, FontSize, Bold,
        TextColor, Background, Padding, Align, Numeric, Error);

    /// <summary>Increments <see cref="Version"/> if any value changes.</summary>
    internal void Apply(Snapshot s)
    {
        if (_last == s)
            return;
        _last = s;
        (Rect, Clip, Kind, Multiline, Placeholder) = (s.Rect, s.Clip, s.Kind, s.Multiline, s.Placeholder);
        (FontFamily, FontSize, Bold, TextColor, Background) = (s.FontFamily, s.FontSize, s.Bold, s.TextColor, s.Background);
        (Padding, Align, Numeric, Error) = (s.Padding, s.Align, s.Numeric, s.Error);
        Version++;
    }
}
