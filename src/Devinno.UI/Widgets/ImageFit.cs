namespace Devinno.UI.Widgets;

/// <summary>The value of the <c>fit</c> attribute of <c>DvImage</c>; determines how the image fits inside the box. The image is always clipped to the box area.</summary>
public enum ImageFit
{
    /// <summary><c>fit="fill"</c>. Stretches the image to fill the box, ignoring the aspect ratio.</summary>
    Fill,
    /// <summary><c>fit="contain"</c> (default). Keeps the aspect ratio, scales the image down or up so the whole image fits inside the box, and centers it. The remaining space is left empty.</summary>
    Contain,
    /// <summary><c>fit="cover"</c>. Keeps the aspect ratio, scales the image up to cover the box completely, and centers it. Overflowing parts are clipped.</summary>
    Cover
}

/// <summary>Converts a <c>fit</c> attribute string to <see cref="ImageFit"/>.</summary>
/// <remarks>This is an internal engine stage. App code normally does not use it directly; it uses <c>&lt;DvImage fit="cover"/&gt;</c> in markup.</remarks>
public static class ImageFitParser
{
    /// <summary>Parses the string. Recognizes <c>"fill"</c> and <c>"cover"</c>, ignoring case and leading/trailing whitespace; every other value (<see langword="null"/>, <c>"contain"</c>, typos) gives <see cref="ImageFit.Contain"/>.</summary>
    /// <param name="v">The <c>fit</c> attribute value.</param>
    /// <returns>The parsed fit mode.</returns>
    public static ImageFit Parse(string? v) => v?.Trim().ToLowerInvariant() switch
    {
        "fill" => ImageFit.Fill,
        "cover" => ImageFit.Cover,
        _ => ImageFit.Contain
    };
}
