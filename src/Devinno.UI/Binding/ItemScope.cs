namespace Devinno.UI.Binding;

/// <summary>
/// The binding scope of one DvRepeat item. <c>{Item}</c>/<c>{Item.X}</c> = the current item, <c>{Index}</c> = 0-based index;
/// any other path is passed to the outer context (<see cref="Outer"/>: the screen VM, a component parameter scope, or an outer item scope).
/// </summary>
internal sealed class ItemScope(object? outer, object? item, int index)
{
    public object? Outer { get; } = outer;
    public object? Item { get; } = item;
    public int Index { get; } = index;
}
