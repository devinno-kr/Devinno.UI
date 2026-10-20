namespace Devinno.UI.Binding;

/// <summary>
/// Parameter scope of one component instance (name → value). The first segment of a template binding <c>{U.Level}</c> is the parameter name.
/// Boxes inside the template remember this scope — if an <c>onclick</c> method takes one parameter, the parameter value matching its type is passed
/// (same mechanism as <c>M(항목)</c> (M(item)) in <c>DvRepeat</c> — e.g. a Reset button in a unit template → <c>Reset(BlenderVm u)</c>).
/// </summary>
internal sealed class ComponentScope() : Dictionary<string, object?>(StringComparer.Ordinal);
