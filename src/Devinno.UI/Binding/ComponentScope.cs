namespace Devinno.UI.Binding;

/// <summary>
/// Parameter scope of one component instance (name → value). The first segment of a template binding <c>{U.Level}</c> is the parameter name.
/// Boxes inside the template remember this scope — if an <c>onclick</c> method takes one parameter, the parameter value matching its type is passed
/// (same mechanism as <c>M(항목)</c> (M(item)) in <c>DvRepeat</c> — e.g. a Reset button in a unit template → <c>Reset(BlenderVm u)</c>).
/// </summary>
internal sealed class ComponentScope() : Dictionary<string, object?>(StringComparer.Ordinal)
{
    private Dictionary<string, string>? _formats;

    /// <summary>
    /// The display format the instance gave a parameter (<c>value="{Level:0.0}"</c> → <c>0.0</c>). The parameter keeps the value itself;
    /// a template binding that is exactly the parameter (<c>{value}</c>) takes this format when it becomes text (<see cref="BindingResolver.TextExpr"/>).
    /// </summary>
    public string? FormatOf(string name) => _formats?.GetValueOrDefault(name);

    public void SetFormat(string name, string format) => (_formats ??= new Dictionary<string, string>(StringComparer.Ordinal))[name] = format;
}
