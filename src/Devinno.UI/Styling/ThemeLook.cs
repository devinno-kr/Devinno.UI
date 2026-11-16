namespace Devinno.UI.Styling;

/// <summary>How a theme draws depth: <see cref="Flat"/> (solid fills, no gloss or shadows) or <see cref="Rich"/> (gloss, gradients and shadows on instruments). Theme token <c>look.texture</c>.</summary>
public enum LookTexture
{
    /// <summary>Solid fills; no gloss, gradients, shadows or glow on instruments. Token value <c>flat</c>.</summary>
    Flat,
    /// <summary>Gloss, gradients, shadows and glow on instruments. Token value <c>rich</c>.</summary>
    Rich
}

/// <summary>How a theme shows the chosen item among several (segment, list, tree, grid row). Theme token <c>look.selection</c>.</summary>
public enum LookSelection
{
    /// <summary>A raised surface, a light tint or a thin bar. Token value <c>quiet</c>.</summary>
    Quiet,
    /// <summary>Filled with the selection color. Token value <c>strong</c>.</summary>
    Strong
}

/// <summary>
/// The style switches of a theme — the few choices that change how widgets are drawn rather than only their values.
/// Read from the theme tokens <c>look.texture</c> (<c>flat</c>|<c>rich</c>), <c>look.normal-color</c> (<c>on</c>|<c>off</c>) and <c>look.selection</c> (<c>quiet</c>|<c>strong</c>).
/// </summary>
/// <param name="Texture">Flat or rich depth (gloss, gradients, shadows).</param>
/// <param name="NormalColor">Whether normal states (running, normal range, no alarm) are shown in color; <see langword="false"/> = gray, color only for abnormal states.</param>
/// <param name="Selection">Quiet or strong selection among several items.</param>
public sealed record ThemeLook(LookTexture Texture, bool NormalColor, LookSelection Selection)
{
    /// <summary>The switches of <paramref name="theme"/> (<see langword="null"/> = <see cref="DvTheme.Default"/>). A missing or unknown value reads as rich, on, strong.</summary>
    public static ThemeLook Of(DvTheme? theme)
    {
        theme ??= DvTheme.Default;
        string Read(string key) => theme.TryResolve(key, out var v) ? v.Trim().ToLowerInvariant() : "";
        return new ThemeLook(
            Read("look.texture") == "flat" ? LookTexture.Flat : LookTexture.Rich,
            Read("look.normal-color") != "off",
            Read("look.selection") == "quiet" ? LookSelection.Quiet : LookSelection.Strong);
    }
}
