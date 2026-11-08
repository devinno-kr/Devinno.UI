using Devinno.UI.Markup;

namespace Devinno.UI.Video;

/// <summary>
/// Keeps the players made for <c>&lt;DvVideo src="…"/&gt;</c> between rebuilds of one screen (same key as <see cref="Widgets.WidgetStore"/>: element + how many times it was built earlier in the build).
/// A player whose element disappeared, or whose <c>src</c> changed, is disposed at <see cref="End"/>.
/// </summary>
internal sealed class VideoStore
{
    private Dictionary<(ElementNode Node, int Nth), VideoPlayer> _kept = new();
    private Dictionary<(ElementNode Node, int Nth), VideoPlayer> _next = new();
    private readonly Dictionary<ElementNode, int> _count = new();

    public void Begin()
    {
        _next.Clear();
        _count.Clear();
    }

    /// <param name="node">The element.</param>
    /// <param name="path">The <c>src</c> path.</param>
    /// <param name="live">A surface keeps the store (real playback). Otherwise (one-off render) only the first frame is decoded and the decoder is closed at once.</param>
    public VideoPlayer Get(ElementNode node, string path, bool live)
    {
        var nth = _count.GetValueOrDefault(node);
        _count[node] = nth + 1;
        var key = (node, nth);
        if (!_kept.TryGetValue(key, out var player) || player.Path != path)
            player = new VideoPlayer(path, poster: !live);
        _next[key] = player;
        return player;
    }

    public void End()
    {
        foreach (var (key, player) in _kept)
        {
            if (!_next.TryGetValue(key, out var kept) || !ReferenceEquals(kept, player))
                player.Dispose();
        }
        _kept.Clear();
        (_kept, _next) = (_next, _kept);
    }
}
