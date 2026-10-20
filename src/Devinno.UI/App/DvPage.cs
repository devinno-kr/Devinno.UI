namespace Devinno.UI.App;

/// <summary>
/// Base class for one screen (page) that the app (<see cref="DvApp"/>) keeps and switches between. The page class itself is the markup's view model (the target of bindings and <c>onclick</c>).
/// </summary>
/// <remarks>
/// <para><b>How to create</b>: add <c>partial class HomePage : DvPage</c> and a <c>HomePage.dvml</c> whose root has <c>vm="네임스페이스.HomePage"</c> (namespace.HomePage).
/// The source generator fills in the markup link (an override of <see cref="MarkupSource"/>), the <c>id</c> fields and <c>partial void OnLoaded()</c>.</para>
/// <para><b>Retention</b>: a page's screen is created once when first shown and then kept. Scroll positions, input values and edit state stay the same after navigating to another page and back.</para>
/// <para><b>Lifecycle notifications</b>: when <c>App.Show(b)</c> is applied, the order is <see cref="OnHidden"/> of the previous page → (if b's screen is new: creation → <c>OnLoaded()</c> once)
/// → <see cref="OnShown"/> of b. All are called on the input (UI) thread; exceptions they throw are reported through <see cref="DvApp.HandlerError"/> and the switch continues.</para>
/// <para><b><see cref="App"/></b>: usable after <c>app.Pages.Add(page)</c>; reading it before that (including inside the page constructor) throws <see cref="InvalidOperationException"/>.
/// Put code that uses the app in handlers, <c>OnLoaded</c> or <see cref="OnShown"/>.</para>
/// </remarks>
/// <example>
/// <code>
/// public partial class MonitorPage : DvPage
/// {
///     public double Temperature { get; set; }
///     public void Back() =&gt; App.Show&lt;HomePage&gt;();
///     protected override void OnShown() { /* 보일 때 */ }
///     protected override void OnHidden() { /* 숨겨질 때 */ }
/// }
/// </code>
/// </example>
public abstract class DvPage
{
    /// <summary>Gets the app that contains this page.</summary>
    /// <exception cref="InvalidOperationException">Read before <c>app.Pages.Add(page)</c> (or <c>app.Show(page)</c>). Inside the page constructor the page has not been added to the app yet either.</exception>
    public DvApp App => _app ?? throw new InvalidOperationException($"{GetType().Name} 은(는) 아직 앱에 추가되지 않았습니다. app.Pages.Add 뒤(처리기·OnLoaded·OnShown)에서 App 을 쓰세요.");

    /// <summary>The owning app (<see langword="null"/> if none). Used for ownership checks inside the library.</summary>
    internal DvApp? AppOrNull { get => _app; set => _app = value; }

    private volatile DvApp? _app;

    /// <summary>
    /// Gets the markup of this page (raw dvml). The source generator overrides it with the dvml's <c>Markup</c> constant, so you normally do not write it yourself.
    /// </summary>
    /// <exception cref="InvalidOperationException">No dvml is linked to this page (no override).</exception>
    protected virtual string MarkupSource => throw new InvalidOperationException("dvml 이 연결되지 않았습니다");

    /// <summary>Called right after this page becomes the current page (input thread). On first show, it is called after <c>OnLoaded()</c>.</summary>
    protected virtual void OnShown()
    {
    }

    /// <summary>Called when this page is hidden because another page is shown (input thread). The screen and its state are kept.</summary>
    protected virtual void OnHidden()
    {
    }

    /// <summary>This page's surface kept by the app (created on first show). Volatile because <see cref="DvApp.Invalidate"/> reads it from other threads.</summary>
    internal DvSurface? Surface { get => _surface; set => _surface = value; }

    private volatile DvSurface? _surface;

    internal string GetMarkup() => MarkupSource;

    /// <summary>Gets the absolute path of this screen's dvml file. Filled by the source generator at build time; <see cref="DvApp.HotReload"/> watches this file. <see langword="null"/> if none.</summary>
    protected virtual string? MarkupFile => null;

    internal string? GetMarkupFile() => MarkupFile;

    internal void RaiseShown() => OnShown();

    internal void RaiseHidden() => OnHidden();
}
