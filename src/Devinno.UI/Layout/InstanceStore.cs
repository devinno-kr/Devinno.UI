using Devinno.UI.Markup;

namespace Devinno.UI.Layout;

/// <summary>
/// Which build of a markup element a box is: the element and how many times that element was built earlier in the same build.
/// An element inside <c>DvRepeat</c> or a component template is built once per item or instance, so each gets its own number (by position).
/// </summary>
internal readonly record struct InstanceKey(ElementNode Node, int Nth);

/// <summary>
/// Keeps one object per element instance between rebuilds of one screen (custom widget instances, video players).
/// With <c>disposeDropped</c>, objects not asked for during a build, or replaced because <c>reuse</c> said no, are disposed at <see cref="End"/> when they are <see cref="IDisposable"/>.
/// </summary>
internal sealed class InstanceStore<T>(bool disposeDropped = false) where T : class
{
    private Dictionary<InstanceKey, T> _kept = new();
    private Dictionary<InstanceKey, T> _next = new();

    /// <summary>Starts a build.</summary>
    public void Begin() => _next.Clear();

    /// <summary>The object kept for <paramref name="key"/>, or a new one from <paramref name="create"/> when there is none or <paramref name="reuse"/> rejects it.</summary>
    public T Get(InstanceKey key, Func<T> create, Func<T, bool>? reuse = null)
    {
        if (!_kept.TryGetValue(key, out var item) || reuse is not null && !reuse(item))
            item = create();
        _next[key] = item;
        return item;
    }

    /// <summary>Ends a build: drops (and with <c>disposeDropped</c>, disposes) what was not carried over.</summary>
    public void End()
    {
        foreach (var (key, item) in _kept)
        {
            if (disposeDropped && item is IDisposable d && (!_next.TryGetValue(key, out var kept) || !ReferenceEquals(kept, item)))
                d.Dispose();
        }
        _kept.Clear();
        (_kept, _next) = (_next, _kept);
    }
}
