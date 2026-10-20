namespace Devinno.UI;

/// <summary>
/// Arguments for <see cref="DvSurface.HandlerError"/>. Carries one exception thrown by app (view model) code, and what the library was calling that code for.
/// </summary>
/// <remarks>
/// <para>The library catches this exception and continues input handling, layout and drawing, so by the time you receive these arguments handling is already finished (no need to rethrow).
/// The outcome is fixed by <see cref="Kind"/>. A value write failure (<c>"setter"</c>) is handled the same as an <c>onbeforecommit</c> rejection,
/// and a handler exception is treated as the handler having stopped midway. A value read failure (<c>"getter"</c>, <c>"format"</c>, <c>"enumerate"</c>, <c>"compare"</c>)
/// treats the value as empty (<see langword="null"/>) and keeps drawing the screen.</para>
/// <para>A <see cref="System.Reflection.TargetInvocationException"/> wrapped by a reflection call is unwrapped, and the exception actually thrown by app code is put in <see cref="Exception"/>.</para>
/// </remarks>
/// <example>
/// <code>
/// surface.HandlerError += (_, e) =&gt;
///     log.Error(e.Exception, $"{e.Kind} {e.Member} ({e.Tag}) 실패");
/// // e.g. Kind = "onclick", Member = "Start", Tag = "DvButton", Exception = the InvalidOperationException thrown by the handler
/// </code>
/// </example>
public sealed class DvHandlerErrorEventArgs : EventArgs
{
    internal DvHandlerErrorEventArgs(Exception exception, string kind, string? member, string? tag)
    {
        Exception = exception;
        Kind = kind;
        Member = member;
        Tag = tag;
    }

    /// <summary>Gets the exception thrown by app code. If it was a <see cref="System.Reflection.TargetInvocationException"/>, this is the inner exception.</summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets what the library was doing when the exception occurred. One of the following values.
    /// <list type="bullet">
    /// <item><description><c>"onclick"</c> — an <c>onclick</c> handler (including <c>onclick</c> of a grid <c>button</c> cell).</description></item>
    /// <item><description><c>"Click"</c> — a subscriber of a control handle (<see cref="Controls.DvControl.Click"/>).</description></item>
    /// <item><description><c>"attach"</c> — <c>Attach</c> of a control-handle VM (<see cref="Controls.IDvControlHost"/>) (including the generator-made <c>OnLoaded</c>) threw while the surface was being built.
    /// <see cref="Member"/> = VM type name, <see cref="Tag"/> = <see langword="null"/>. Since you cannot subscribe inside the constructor, it is reported once at that surface's first <see cref="DvSurface.Layout(float, float)"/>.
    /// Handles connected before the throw keep working.</description></item>
    /// <item><description><c>"shown"</c>/<c>"hidden"</c> — a page transition notification (<c>OnShown</c>/<c>OnHidden</c> of <see cref="App.DvPage"/>) threw. <see cref="Member"/> = page type name,
    /// <see cref="Tag"/> = <see langword="null"/>. Reported only through <see cref="App.DvApp.HandlerError"/>; the transition continued.</description></item>
    /// <item><description><c>"onrowclick"</c> — the <c>onrowclick</c> handler of <c>DvDataGrid</c>.</description></item>
    /// <item><description><c>"onack"</c> — the <c>onack</c> handler of an alarm widget.</description></item>
    /// <item><description><c>"onbeforecommit"</c> — the check handler before a value write. The value was not written (rejected).</description></item>
    /// <item><description><c>"setter"</c> — writing a value to a view model property failed (setter exception, type conversion failure). The value did not change.</description></item>
    /// <item><description><c>"getter"</c> — a view model (or item) property getter threw during layout or drawing. The value was treated as empty
    /// (text = empty string, <c>visible</c> = hidden, grid cell = blank, sort key = null rank, summary = that value excluded).</description></item>
    /// <item><description><c>"format"</c> — converting a value to text (<see cref="object.ToString"/>, <see cref="IFormattable"/>) threw. Drawn as empty text.</description></item>
    /// <item><description><c>"enumerate"</c> — enumerating the list of a <c>DvRepeat</c>, <c>DvDataGrid</c>, <c>DvSelect</c>, alarm or trend threw. Treated as an empty list.</description></item>
    /// <item><description><c>"compare"</c> — <see cref="IComparable.CompareTo"/> of an item value threw during <c>DvDataGrid</c> sorting. Treated as equal (the order in that column is undefined).</description></item>
    /// </list>
    /// <para>Value reads (<c>"getter"</c>, <c>"format"</c>, <c>"enumerate"</c>, <c>"compare"</c>) report the same (<see cref="Kind"/>, <see cref="Member"/>, <see cref="Tag"/>) only once,
    /// even when read every frame as in a grid, until the surface is rebuilt (<see cref="DvSurface.Invalidate"/>).</para>
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the name of the method called, or the property path being written (or read). For example <c>"Start"</c>, <c>"Item.SetPoint"</c>.
    /// For a grid cell value read, it is the column's <c>field</c> path. For a value conversion with no known path (<c>"format"</c>), it may be <see langword="null"/>.
    /// </summary>
    public string? Member { get; }

    /// <summary>Gets the tag name of the element where the exception occurred. For example <c>"DvButton"</c>, <c>"DvInput"</c>, <c>"DvDataGrid"</c>. For grid cells (edit, check, button cells) and row selection or row click, it is <c>"DvDataGrid"</c>.</summary>
    public string? Tag { get; }
}
