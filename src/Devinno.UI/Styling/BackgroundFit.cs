namespace Devinno.UI.Styling;

/// <summary>How a background picture (<c>background-image</c>) meets its box: style key <c>background-size</c>.</summary>
public enum BackgroundFit
{
    /// <summary><c>cover</c> (default). Fills the box keeping the proportions; the overflow is cut off, centered.</summary>
    Cover,
    /// <summary><c>contain</c>. The whole picture inside the box keeping the proportions, centered; the rest stays empty.</summary>
    Contain,
    /// <summary><c>fill</c> (also <c>100% 100%</c>). Stretched to the box.</summary>
    Fill,
    /// <summary>
    /// <c>natural</c>. The picture at its own pixel size, <b>not cut off</b> at the box edge (nor by <c>border-radius</c>): centered on the box,
    /// or with its top-left corner at <c>background-image-offset</c>. Meant for a picture with a glow or shadow margin around a plate the size of the box.
    /// </summary>
    Natural,
}
