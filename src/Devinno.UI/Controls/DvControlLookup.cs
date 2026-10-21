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

    // 핸들 형식 = 태그 이름(DvControl 은 아무 태그나). 생성기 DvmlModel.HandleType 과 같은 목록.
    private static readonly Dictionary<Type, (string Tag, Func<string, DvControl> New)> Kinds = new()
    {
        [typeof(DvLabel)] = ("DvLabel", id => new DvLabel(id)),
        [typeof(DvButton)] = ("DvButton", id => new DvButton(id)),
        [typeof(DvToggle)] = ("DvToggle", id => new DvToggle(id)),
        [typeof(DvKnob)] = ("DvKnob", id => new DvKnob(id)),
        [typeof(DvSlider)] = ("DvSlider", id => new DvSlider(id)),
        [typeof(DvListBox)] = ("DvListBox", id => new DvListBox(id)),
        [typeof(DvExpander)] = ("DvExpander", id => new DvExpander(id)),
        [typeof(DvTreeView)] = ("DvTreeView", id => new DvTreeView(id)),
        [typeof(DvInput)] = ("DvInput", id => new DvInput(id)),
        [typeof(DvSelect)] = ("DvSelect", id => new DvSelect(id)),
        [typeof(DvSegment)] = ("DvSegment", id => new DvSegment(id)),
        [typeof(DvDateTimeInput)] = ("DvDateTimeInput", id => new DvDateTimeInput(id)),
        [typeof(DvColorInput)] = ("DvColorInput", id => new DvColorInput(id)),
        [typeof(DvCheckBox)] = ("DvCheckBox", id => new DvCheckBox(id)),
        [typeof(DvRadio)] = ("DvRadio", id => new DvRadio(id)),
        [typeof(DvSwitch)] = ("DvSwitch", id => new DvSwitch(id)),
    };

    private static bool TagMatches(Type t, string tag) => !Kinds.TryGetValue(t, out var k) || k.Tag == tag;

    private static T Create<T>(string id) where T : DvControl
    {
        DvControl c = Kinds.TryGetValue(typeof(T), out var k) ? k.New(id)
            : typeof(T) == typeof(DvControl) ? new DvControl(id)
            : throw new NotSupportedException($"Devinno.UI: cannot create a {typeof(T).Name} handle.");
        return (T)c;
    }
}
