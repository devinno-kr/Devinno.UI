namespace Devinno.UI.Components;

/// <summary>
/// Definition of one component parameter (<c>&lt;DvParam name="…" type="…" default="…"/&gt;</c>).
/// </summary>
public sealed class ComponentParam
{
    /// <summary>
    /// Gets the parameter name. Inside the template it is read as <c>{이름}</c> ({name}; case-sensitive); on an instance the value is passed as an attribute of the same name (attribute names are case-insensitive).
    /// Empty string if there is no <c>name</c> attribute.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>Gets the <c>type</c> attribute value. <see langword="null"/> if absent. The current version only stores the value and does not use it for type checking or conversion.</summary>
    public string? Type { get; init; }
    /// <summary>Gets the string value used when an instance does not give this parameter attribute. <see langword="null"/> (empty) if there is no <c>default</c> attribute.</summary>
    public string? Default { get; init; }
}
