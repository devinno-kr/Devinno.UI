using Devinno.UI.Layout;

namespace Devinno.UI.Interaction;

/// <summary>Interaction state holding the box the pointer is currently over and the pressed box. Painters read it to pick hover and press styles.</summary>
/// <remarks>An internal engine stage. App code normally does not use it directly; it uses markup bindings and <see cref="DvSurface"/>/<see cref="DvRenderer"/>.</remarks>
public sealed class InteractionState
{
    /// <summary>The interactive box the pointer is over. <see langword="null"/> if none.</summary>
    public LayoutBox? Hover { get; set; }
    /// <summary>The interactive box currently pressed. <see langword="null"/> if none.</summary>
    public LayoutBox? Press { get; set; }

    /// <summary>The box being long-pressed (<c>hold</c>) — draws the progress band.</summary>
    internal LayoutBox? HoldBox { get; set; }

    /// <summary>The progress (0–1) of <see cref="HoldBox"/>.</summary>
    internal float HoldProgress { get; set; }

    /// <summary>A shared state with no hover or press (static image rendering, etc.). It is a shared instance, so do not change its values.</summary>
    public static readonly InteractionState Empty = new();
}
