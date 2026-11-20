namespace Devinno.UI.Widgets;

/// <summary>
/// A custom widget that reads its child elements as definitions: settings the widget uses while painting (which part moves, which value drives it …),
/// not elements on the screen. Read them through <see cref="DvWidgetContext.Definitions"/>.
/// </summary>
/// <remarks>
/// <para><b>Not a container</b>: definitions get no box, size or position, are not painted and receive no input. The widget alone paints and handles the pointer,
/// exactly as a <see cref="DvWidget"/>.</para>
/// <para><b>Tags</b>: <see cref="DefinitionTags"/> lists the child tags the widget reads. They are names inside this widget only (like <c>DvOption</c> inside <c>DvSegment</c>), so they are not registered
/// and may repeat across widgets. A child with another tag is skipped with a Trace warning, and children of a definition are not used (Trace warning).</para>
/// <para><b>Values</b>: each attribute of a definition is resolved like the widget's own attributes (literal or binding, the screen VM, a <c>DvRepeat</c> item or component parameters),
/// as a snapshot taken when the screen is built. A VM change reaches the definitions on the next rebuild (<c>Invalidate</c>), the same as for built-in elements.
/// No attribute has a meaning of its own: <c>id</c>, <c>visible</c>, <c>style</c> … are values like any other.</para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class Rig : DvDefinitionWidget
/// {
///     public override IReadOnlyCollection&lt;string&gt; DefinitionTags { get; } = ["Rotate"];
///
///     public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
///     {
///         foreach (var d in ctx.Definitions)
///             Turn(d.Text("node"), d.Number("value"));
///         …
///     }
/// }
///
/// registry.RegisterWidget&lt;Rig&gt;("Rig");
/// // markup:
/// // &lt;Rig src="robot.glb"&gt;
/// //   &lt;Rotate node="Turret" value="{Turret}"/&gt;
/// // &lt;/Rig&gt;
/// </code>
/// </example>
public abstract class DvDefinitionWidget : DvWidget
{
    /// <summary>Gets the child tags this widget reads as definitions (case-sensitive). Children with other tags are skipped with a Trace warning.</summary>
    public abstract IReadOnlyCollection<string> DefinitionTags { get; }
}
