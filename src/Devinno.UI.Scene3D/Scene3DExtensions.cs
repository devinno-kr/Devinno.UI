using Devinno.UI.Components;

namespace Devinno.UI.Scene3D;

/// <summary>Registers the 3D view tag on a <see cref="ComponentRegistry"/>.</summary>
public static class Scene3DExtensions
{
    /// <summary>
    /// Registers <see cref="SceneView"/> under the tag <c>SceneView</c>. Call once at startup, before the first screen is built, and set the registry on the <c>DvApp</c>.
    /// </summary>
    /// <param name="registry">The registry the app uses.</param>
    /// <returns>The same registry, for chaining.</returns>
    /// <example>
    /// <code>
    /// var registry = new ComponentRegistry().UseScene3D();
    /// var app = new DvApp { Registry = registry };
    /// </code>
    /// </example>
    public static ComponentRegistry UseScene3D(this ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.RegisterWidget<SceneView>("SceneView");
        return registry;
    }
}
