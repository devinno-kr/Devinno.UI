using System.Diagnostics;
using Devinno.UI.Markup;

namespace Devinno.UI.Controls;

/// <summary>
/// A helper that finds <c>id</c> elements on the screen and connects them to the view model's handles. Used only inside <see cref="IDvControlHost.Attach"/>.
/// Elements inside <c>DvRepeat</c> or <c>DvComponent</c> are not searched.
/// </summary>
public sealed class DvControlLookup
{
    private readonly DvSurface _surface;
    private readonly Dictionary<string, ElementNode> _byId = new(StringComparer.Ordinal);

    internal DvControlLookup(DvSurface surface, ElementNode root)
    {
        _surface = surface;
        Scan(root);
    }

    private void Scan(ElementNode node)
    {
        if (node.Tag is "DvRepeat" or "DvComponent")
            return;
        if (node.Attributes.TryGetValue("id", out var id) && id.Length > 0)
            _byId.TryAdd(id, node);
        foreach (var c in node.Children)
            Scan(c);
    }

    /// <summary>
    /// Connects the <paramref name="id"/> element to the <paramref name="existing"/> handle and returns it. Creates a new handle if there is none.
    /// If the element is missing or of a different kind, writes a warning and returns an unconnected handle.
    /// </summary>
    /// <typeparam name="T">The handle kind (<see cref="DvLabel"/>, <see cref="DvButton"/>, <see cref="DvControl"/>).</typeparam>
    /// <param name="existing">The handle used by the previous screen (<see langword="null"/> if none).</param>
    /// <param name="id">The <c>id</c> in markup.</param>
    public T Bind<T>(T? existing, string id) where T : DvControl
    {
        var control = existing ?? Create<T>(id);
        if (!_byId.TryGetValue(id, out var node))
        {
            Detach(control);
            Trace.TraceWarning($"Devinno.UI: id '{id}': no element with this id in the markup; the handle is not connected.");
            return control;
        }
        if (!TagMatches(typeof(T), node.Tag))
        {
            Detach(control);
            Trace.TraceWarning($"Devinno.UI: id '{id}': element <{node.Tag}> does not match the {typeof(T).Name} handle type; the handle is not connected.");
            return control;
        }
        control.Surface = _surface;
        control.Node = node;
        control.OnAttached();
        _surface.RegisterControl(node, control);
        return control;
    }

    private static void Detach(DvControl c)
    {
        c.Surface = null;
        c.Node = null;
    }

    // 핸들 형식 = 태그 이름(DvControl 은 아무 태그나) — 목록은 TagCatalog 한 곳(생성기 DvmlModel.HandleTags 와 테스트로 대조).
    private static IReadOnlyDictionary<Type, (string Tag, Func<string, DvControl> New)> Kinds => Markup.TagCatalog.HandleKinds;

    private static bool TagMatches(Type t, string tag) => !Kinds.TryGetValue(t, out var k) || k.Tag == tag;

    private static T Create<T>(string id) where T : DvControl
    {
        DvControl c = Kinds.TryGetValue(typeof(T), out var k) ? k.New(id)
            : typeof(T) == typeof(DvControl) ? new DvControl(id)
            : throw new NotSupportedException($"Devinno.UI: cannot create a {typeof(T).Name} handle.");
        return (T)c;
    }
}
