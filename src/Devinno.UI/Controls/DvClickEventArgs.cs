using SkiaSharp;

namespace Devinno.UI.Controls;

/// <summary>Event data for the <see cref="DvControl.Click"/> event of a control handle.</summary>
public sealed class DvClickEventArgs : EventArgs
{
    internal DvClickEventArgs(SKPoint position) => Position = position;

    /// <summary>Gets the clicked position (window coordinates).</summary>
    public SKPoint Position { get; }
}
