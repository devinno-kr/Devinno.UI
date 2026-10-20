namespace Devinno.UI.App;

/// <summary>The animation used when switching pages (<see cref="DvApp.PageTransition"/>, <see cref="DvApp.Show(DvPage, DvPageTransition)"/>).</summary>
public enum DvPageTransition
{
    /// <summary>Switches immediately (default).</summary>
    None,
    /// <summary>The new page fades in over about 0.2 seconds (drawn over the old page, becoming more opaque).</summary>
    Fade,
    /// <summary>Over about 0.2 seconds, the new page slides in from the right while the old page slides out to the left.</summary>
    Slide,
}
