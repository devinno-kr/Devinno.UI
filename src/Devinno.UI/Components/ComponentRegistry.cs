using Devinno.UI.Markup;

namespace Devinno.UI.Components;

/// <summary>
/// A registry that stores app-defined reusable components (<c>&lt;DvComponent&gt;</c> templates) by name.
/// Pass it to <see cref="DvSurface"/>, <see cref="DvRenderer"/> or <see cref="App.DvApp.Registry"/> and the markup of that screen can use component names as tags.
/// </summary>
/// <remarks>
/// <para><b>Definition format</b>: the root is <c>&lt;DvComponent name="이름"&gt;</c>, containing <c>&lt;DvParam name="…" default="…"/&gt;</c> elements and
/// one template root element (the first child that is not <c>DvParam</c> or <c>DvStyle</c> is the template; the rest are ignored).</para>
/// <para><b><c>DvStyle</c> inside a definition</b>: the rules of a <c>&lt;DvStyle&gt;</c> placed inside a definition (before or inside the template) apply globally to every screen given this registry
/// (the same as a <c>DvStyle</c> in screen markup, including screen elements that do not use that component). Precedence is theme &lt; component <c>DvStyle</c> (registration order) &lt; screen <c>DvStyle</c>, so
/// for the same class and property the screen wins. Definitions from <see cref="BuiltInComponents"/> are not included here.</para>
/// <para><b>Parameters</b>: <c>{이름}</c> inside the template is replaced with the parameter value. Instance attribute values are resolved against the outer screen's view model
/// (a binding such as <c>{Bind Data.X}</c> or <c>{Data.X}</c>, or a literal); if the attribute is omitted, <see cref="ComponentParam.Default"/> is used.
/// Inside the template only parameters are visible; outer view model properties cannot be read directly (pass needed values as parameters).</para>
/// <para><b>Two-way parameters</b>: if the instance gives a parameter <c>{Bind …}</c> and a template <c>DvToggle</c>/<c>DvKnob</c>/<c>DvInput</c>/<c>DvSelect</c>/<c>DvDateTimeInput</c>/<c>DvColorInput</c>
/// uses it with <c>value="{파라미터}"</c>, user changes are written directly to that path in the outer view model.</para>
/// <para><b>Slot</b>: the instance's child elements go in place of <c>&lt;DvSlot/&gt;</c> in the template (built against the outer view model). A <c>DvSlot</c> inside a nested component also receives the outer instance's children.</para>
/// <para><b>Attribute forwarding</b>: the instance's <c>class</c> (appended after the template classes), <c>grow</c>, <c>width</c>, <c>height</c>, <c>style</c> (merged with the template style, instance wins),
/// <c>onclick</c> and <c>disabled</c> are forwarded to the template root unless a parameter has the same name. For <c>onclick</c> and <c>disabled</c> the instance wins even if the template root has the same attribute.
/// The <c>onclick</c> method is looked up in the screen view model (<c>M(항목)</c> first if the instance is inside a <c>DvRepeat</c> item), and the <c>disabled</c> value is resolved against the outer view model.
/// <c>visible</c> applies to the instance itself (if false, it is not expanded).</para>
/// <para>If a name matches one in <see cref="BuiltInComponents"/>, this registry's definition takes precedence. Components that include each other infinitely throw at expansion depth 64.
/// Not thread-safe; finish registration before creating screens.</para>
/// </remarks>
/// <example>
/// <code>
/// using Devinno.UI;
/// using Devinno.UI.Components;
///
/// var registry = new ComponentRegistry();
/// registry.Register("""
///     &lt;DvComponent name="PumpCard"&gt;
///       &lt;DvParam name="Title" default="-"/&gt;
///       &lt;DvParam name="Run"/&gt;
///       &lt;DvCard title="{Title}"&gt;
///         &lt;DvToggle value="{Run}"/&gt;
///         &lt;DvSlot/&gt;
///       &lt;/DvCard&gt;
///     &lt;/DvComponent&gt;
///     """);
///
/// // Giving Run a {Bind …} makes toggle changes write to vm.Pump1.Running
/// var surface = new DvSurface("""
///     &lt;DvScreen&gt;
///       &lt;PumpCard Title="펌프 1" Run="{Bind Pump1.Running}"&gt;
///         &lt;DvLabel text="{Pump1.Flow}"/&gt;
///       &lt;/PumpCard&gt;
///     &lt;/DvScreen&gt;
///     """, vm, registry);
/// </code>
/// </example>
public sealed class ComponentRegistry
{
    private readonly Dictionary<string, ComponentDef> _defs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Process.SymbolDef> _symbols = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<Widgets.DvWidget>> _widgets = new(StringComparer.Ordinal);

    // 커스텀 위젯(C# 클래스) — 이름이 곧 태그
    internal bool TryGetWidget(string tag, out Func<Widgets.DvWidget>? create) => _widgets.TryGetValue(tag, out create);

    /// <summary>
    /// Registers a custom widget class (<see cref="Widgets.DvWidget"/>) under a tag, so markup can use <c>&lt;Tag …/&gt;</c>. Registering the same tag again replaces the class.
    /// </summary>
    /// <typeparam name="T">The widget class; a new instance is created for each element on screen.</typeparam>
    /// <param name="tag">The markup tag. Must not start with <c>Dv</c> (built-in names) and must not be a component registered here.</param>
    /// <exception cref="ArgumentException">The tag is empty, starts with <c>Dv</c>, or is already a component name.</exception>
    /// <remarks>Register before creating screens (not thread-safe). Screens already created are not updated.</remarks>
    public void RegisterWidget<T>(string tag) where T : Widgets.DvWidget, new()
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("A widget needs a tag name.", nameof(tag));
        if (tag.StartsWith("Dv", StringComparison.Ordinal))
            throw new ArgumentException($"'{tag}': names starting with 'Dv' are reserved for built-in elements.", nameof(tag));
        if (_defs.ContainsKey(tag))
            throw new ArgumentException($"'{tag}' is already a component name.", nameof(tag));
        _widgets[tag] = static () => new T();
    }

    // 공정도 사용자 심볼(<DvSymbol>) — 공정도 안에서 이름이 곧 태그
    internal bool TryGetSymbol(string name, out Process.SymbolDef? def) => _symbols.TryGetValue(name, out def);

    /// <summary>Finds a registered component definition by name (case-sensitive). Built-in components are not searched.</summary>
    /// <param name="name">The component name.</param>
    /// <param name="def">The definition found, or <see langword="null"/> if none.</param>
    /// <returns><see langword="true"/> if registered.</returns>
    public bool TryGet(string name, out ComponentDef? def) => _defs.TryGetValue(name, out def);

    /// <summary>Checks whether a tag name is a component registered in this registry (case-sensitive). Built-in components are not included.</summary>
    /// <param name="tag">The markup tag name.</param>
    /// <returns><see langword="true"/> if registered.</returns>
    public bool IsComponent(string tag) => _defs.ContainsKey(tag);

    // 레이아웃 엔진이 정의 안 DvStyle 을 모을 때 쓴다(등록 순서)
    internal IEnumerable<ComponentDef> Definitions => _defs.Values;

    /// <summary>
    /// Parses <c>&lt;DvComponent&gt;</c> markup and registers the component. If the root is <c>&lt;DvSymbol&gt;</c>, it is registered as a process diagram equipment symbol.
    /// If the name already exists, the new definition replaces it.
    /// </summary>
    /// <param name="componentMarkup">A markup string whose root is <c>&lt;DvComponent name="…"&gt;</c> or <c>&lt;DvSymbol name="…" view-box="…"&gt;</c>.</param>
    /// <exception cref="ArgumentException">
    /// The root is not <c>DvComponent</c> or <c>DvSymbol</c>, the <c>name</c> attribute is missing, or there is no template root (a child that is not <c>DvParam</c> or <c>DvStyle</c>).
    /// For a symbol, the definition is invalid (view-box; port side/position/role; duplicate port; a <c>DvPass</c> referring to a missing port; unknown <c>DvShape</c> class; path syntax). The message says what is wrong.
    /// </exception>
    /// <remarks>
    /// <para>If the markup syntax is invalid, the markup parser's exception propagates as is. Screens already created before registration are not updated automatically.</para>
    /// <para><b>Symbol</b>: inside <c>&lt;DvSymbol name="Cyclone" view-box="0 0 60 100"&gt;</c>, place <c>&lt;DvParam&gt;</c> (state values), <c>&lt;DvShape class path when&gt;</c> (drawing layers),
    /// <c>&lt;DvPort name side at role&gt;</c> (ports: side left/top/right/bottom, position along the side 0–1, role in/out/inout), <c>&lt;DvPass from to when&gt;</c> (flow rules)
    /// and <c>&lt;DvFill clip value&gt;</c> (level fill). Inside a process diagram (<c>DvProcess</c>), use the name as a tag, as in <c>&lt;Cyclone name="CY1" running="{Run}"/&gt;</c>.</para>
    /// </remarks>
    public void Register(string componentMarkup)
    {
        var node = MarkupParser.Parse(componentMarkup);
        if (node.Tag == "DvSymbol")
        {
            var symbol = Process.SymbolDef.Parse(node);
            _symbols[symbol.Name] = symbol;
            return;
        }

        if (node.Tag != "DvComponent")
            throw new ArgumentException("Root must be <DvComponent> or <DvSymbol>.", nameof(componentMarkup));
        if (!node.Attributes.TryGetValue("name", out var name))
            throw new ArgumentException("<DvComponent> requires a name attribute.", nameof(componentMarkup));

        if (_widgets.ContainsKey(name))
            throw new ArgumentException($"'{name}' is already a widget name.", nameof(componentMarkup));

        var template = node.Children.FirstOrDefault(c => c.Tag is not ("DvParam" or "DvStyle"))
            ?? throw new ArgumentException($"Component '{name}' requires one template root.", nameof(componentMarkup));

        var def = new ComponentDef { Name = name, Template = template };
        CollectStyles(node, def.Styles);
        foreach (var p in node.Children.Where(c => c.Tag == "DvParam"))
        {
            def.Params.Add(new ComponentParam
            {
                Name = p.Attributes.GetValueOrDefault("name") ?? "",
                Type = p.Attributes.GetValueOrDefault("type"),
                Default = p.Attributes.GetValueOrDefault("default")
            });
        }

        _defs[name] = def;
    }

    // 정의 안 어디에 있든 DvStyle 의 CSS 를 모은다(화면 마크업의 DvStyle 과 같은 규칙)
    private static void CollectStyles(ElementNode node, List<string> into)
    {
        foreach (var ch in node.Children)
        {
            if (ch.Tag == "DvStyle")
            {
                if (ch.Text is { Length: > 0 } css)
                    into.Add(css);
            }
            else
            {
                CollectStyles(ch, into);
            }
        }
    }
}
