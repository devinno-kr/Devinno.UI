namespace Devinno.UI.Controls;

/// <summary>
/// The interface implemented by view models that can get hold of markup <c>id</c> elements from code.
/// <see cref="Attach"/> is called once when the screen (<see cref="DvSurface"/>) is built. The source generator usually fills in the implementation.
/// </summary>
public interface IDvControlHost
{
    /// <summary>Called when the screen is built. For each field, connect the handle to this screen with <see cref="DvControlLookup.Bind{T}"/>.</summary>
    /// <param name="controls">A helper that finds this screen's <c>id</c> elements and connects them to handles.</param>
    void Attach(DvControlLookup controls);
}
