using System.Collections;

namespace Devinno.UI.App;

/// <summary>
/// The list of pages kept by the app (<see cref="DvApp"/>) (<see cref="DvApp.Pages"/>). Enumerated in the order added.
/// </summary>
/// <remarks>
/// Once added, a page is kept for good (there is no remove). The first page added is the start page used when there is no <see cref="DvApp.CurrentPage"/>.
/// Can be called from any thread.
/// </remarks>
public sealed class PageCollection : IEnumerable<DvPage>
{
    private readonly DvApp _app;
    private readonly List<DvPage> _pages = new();

    internal PageCollection(DvApp app) => _app = app;

    // 소속(App) 판정까지 한 잠금 — 두 앱이 같은 페이지를 동시에 Add 해도 한쪽만 성공.
    private static object Sync => DvApp.OwnerLock;

    /// <summary>Gets the number of pages kept.</summary>
    public int Count
    {
        get { lock (Sync) return _pages.Count; }
    }

    /// <summary>
    /// Adds a page and connects it to this app (sets <see cref="DvPage.App"/>). Does nothing if the same instance is already in the list.
    /// </summary>
    /// <param name="page">The page to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="page"/> already belongs to another <see cref="DvApp"/>.</exception>
    public void Add(DvPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        lock (Sync)
        {
            if (ReferenceEquals(page.AppOrNull, _app))
                return;
            if (page.AppOrNull is not null)
                throw new InvalidOperationException($"페이지 '{page.GetType().Name}' 는 이미 다른 DvApp 에 들어 있습니다.");
            page.AppOrNull = _app;
            _pages.Add(page);
        }
    }

    /// <summary>
    /// Returns the single page of type <typeparamref name="T"/> (including derived types).
    /// </summary>
    /// <typeparam name="T">The page type to find.</typeparam>
    /// <exception cref="InvalidOperationException">There is no page of that type, or more than one. If there are several pages of the same class, specify the instance (<see cref="DvApp.Show(DvPage)"/>).</exception>
    public T Get<T>() where T : DvPage
    {
        lock (Sync)
        {
            T? found = null;
            foreach (var p in _pages)
            {
                if (p is not T t)
                    continue;
                if (found is not null)
                    throw new InvalidOperationException(
                        $"'{typeof(T).Name}' 형식의 페이지가 둘 이상입니다. 인스턴스로 지정하세요(App.Show(page)).");
                found = t;
            }
            return found ?? throw new InvalidOperationException(
                $"'{typeof(T).Name}' 형식의 페이지가 없습니다. 먼저 app.Pages.Add 로 넣으세요.");
        }
    }

    /// <summary>Returns <see langword="true"/> if this page instance is in the list.</summary>
    /// <param name="page">The page to check.</param>
    public bool Contains(DvPage page)
    {
        lock (Sync) return _pages.Contains(page);
    }

    /// <summary>Enumerates the pages in the order added (a copy taken at call time).</summary>
    public IEnumerator<DvPage> GetEnumerator()
    {
        DvPage[] copy;
        lock (Sync) copy = _pages.ToArray();
        return ((IEnumerable<DvPage>)copy).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>The first page added (null if none): the start page.</summary>
    internal DvPage? First
    {
        get { lock (Sync) return _pages.Count > 0 ? _pages[0] : null; }
    }
}
