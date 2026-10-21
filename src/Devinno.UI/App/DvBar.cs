namespace Devinno.UI.App;

/// <summary>
/// Base class for bars (top bar, sidebar, etc.) placed in the app's fixed areas (<see cref="DvApp.Top"/>, <see cref="DvApp.Bottom"/>, <see cref="DvApp.Left"/>, <see cref="DvApp.Right"/>).
/// </summary>
/// <remarks>
/// <para>Like <see cref="DvPage"/>, it is built as <c>partial class X : DvBar</c> + dvml (<c>vm="네임스페이스.X"</c>, i.e. namespace.X) and uses bindings, <c>onclick</c>, <c>id</c> fields, <c>OnLoaded()</c> and <see cref="App"/>.
/// However, it is not in the page list and is not a <c>Show</c> target; since it is always visible, it has no <c>OnShown</c>/<c>OnHidden</c>.</para>
/// <para>A bar does not contain pages. To change pages, request <c>App.Show&lt;T&gt;()</c>; to display the current page, bind <see cref="DvApp.CurrentPage"/>.</para>
/// <para><see cref="App"/> is available after the bar is assigned to an area; reading it before that (including in the constructor) throws <see cref="InvalidOperationException"/>.</para>
/// <para><b>One instance = one place</b> — a bar is placed in only one area at a time. Assigning a bar already placed in another area throws <see cref="InvalidOperationException"/>;
/// to move it, first clear the original area to <see langword="null"/>. Even when removed from an area (<see langword="null"/> or replaced by another bar), the bar stays owned by that app
/// and keeps its screen and state (input values, scroll), so placing it again in an area of the same app restores it exactly as it was when it left. It cannot be placed in another <see cref="DvApp"/>.</para>
/// </remarks>
/// <example>
/// <code>
/// public partial class MainSideBar : DvBar
/// {
///     public void ShowMonitor() =&gt; App.Show&lt;MonitorPage&gt;();
/// }
///
/// app.Left = new MainSideBar();
/// </code>
/// </example>
public abstract class DvBar
{
    /// <summary>Gets the app this bar is placed in.</summary>
    /// <exception cref="InvalidOperationException">Read before the bar is assigned to an area (<c>app.Top</c>, etc.). Inside the bar constructor it is not yet assigned.</exception>
    public DvApp App => _app ?? throw new InvalidOperationException($"{GetType().Name} has not been placed in an app region yet. Use App after assigning it to app.Top, app.Left, etc. (in handlers, OnLoaded).");

    /// <summary>Owning app (<see langword="null"/> if none). Used for ownership checks inside the library.</summary>
    internal DvApp? AppOrNull { get => _app; set => _app = value; }

    private volatile DvApp? _app;

    /// <summary>
    /// Gets the markup of this bar (dvml source text). The source generator overrides it with the dvml's <c>Markup</c> constant, so it is normally not used directly.
    /// </summary>
    /// <exception cref="InvalidOperationException">No dvml is linked to this bar (no override).</exception>
    protected virtual string MarkupSource => throw new InvalidOperationException("No dvml markup is attached");

    /// <summary>This bar's surface, kept by the app (created when first needed). volatile because <see cref="DvApp.Invalidate"/> reads it from other threads.</summary>
    internal DvSurface? Surface { get => _surface; set => _surface = value; }

    private volatile DvSurface? _surface;

    internal string GetMarkup() => MarkupSource;

    /// <summary>Gets the absolute path of this screen's dvml file. Filled in by the source generator at build time; <see cref="DvApp.HotReload"/> watches this file. <see langword="null"/> if none.</summary>
    protected virtual string? MarkupFile => null;

    internal string? GetMarkupFile() => MarkupFile;
}
