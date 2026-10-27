using Devinno.UI.Markup;

namespace Devinno.UI.Widgets;

/// <summary>
/// Keeps custom widget instances between rebuilds of one screen. Key = (markup element, how many times that element was built earlier in the same build) —
/// an element inside <c>DvRepeat</c> is built once per item, so each item keeps its own instance by position.
/// </summary>
internal sealed class WidgetStore
{
    private Dictionary<(ElementNode Node, int Nth), DvWidget> _kept = new();
    private Dictionary<(ElementNode Node, int Nth), DvWidget> _next = new();
    private readonly Dictionary<ElementNode, int> _count = new();

    /// <summary>Starts a build: instances not asked for during this build are dropped at <see cref="End"/>.</summary>
    public void Begin()
    {
        _next.Clear();
        _count.Clear();
    }

    public DvWidget Get(ElementNode node, Func<DvWidget> create)
    {
        var nth = _count.GetValueOrDefault(node);
        _count[node] = nth + 1;
        var key = (node, nth);
        if (!_kept.TryGetValue(key, out var widget))
            widget = create();
        _next[key] = widget;
        return widget;
    }

    public void End() => (_kept, _next) = (_next, _kept);
}
