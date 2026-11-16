using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Devinno.UI.Styling;

/// <summary>
/// Role values for built-in drawing — font weights, small text sizes, stroke widths and alphas read from the theme's role tokens
/// (<c>weight.*</c>, <c>font.*</c>, <c>stroke.*</c>, <c>alpha.*</c> …) instead of numbers written in the drawing code.
/// </summary>
/// <remarks>
/// The theme is the one of the current layout or paint pass (<see cref="Ambient"/>, set by the layout engine and the painter on their thread);
/// outside a pass, <see cref="DvTheme.Default"/>. Values are cached per theme (themes are immutable).
/// </remarks>
internal static class Roles
{
    /// <summary>The theme of the layout or paint pass running on this thread. Assumes a single UI thread per pass.</summary>
    [ThreadStatic] internal static DvTheme? Ambient;

    private static readonly ConditionalWeakTable<DvTheme, ConcurrentDictionary<string, float>> Cache = new();

    /// <summary>A number role token of the ambient theme (0 with a warning if missing).</summary>
    internal static float Number(string token)
    {
        var theme = Ambient ?? DvTheme.Default;
        var map = Cache.GetValue(theme, _ => new ConcurrentDictionary<string, float>(StringComparer.OrdinalIgnoreCase));
        return map.GetOrAdd(token, t => ThemeResolver.Number(theme, t));
    }

    /// <summary>Font weight of role <c>weight.{role}</c> (<c>regular</c>, <c>medium</c>, <c>strong</c>, <c>bold</c>).</summary>
    internal static int Weight(string role) => (int)Number("weight." + role);

    /// <summary>Text size of role <c>font.{role}</c> (<c>caption</c>, <c>meta</c>, <c>label</c>, <c>body</c> …).</summary>
    internal static float Font(string role) => Number("font." + role);

    /// <summary>Stroke width of role <c>stroke.{role}</c> (<c>hairline</c>, <c>outline</c>, <c>emphasis</c>, <c>focus</c>).</summary>
    internal static float Stroke(string role) => Number("stroke." + role);

    /// <summary>An alpha role (<c>alpha.selected</c>, <c>overlay.hover</c>, <c>opacity.disabled</c> …) as a byte 0–255.</summary>
    internal static byte Alpha(string token) => (byte)Math.Clamp(MathF.Round(Number(token) * 255f), 0f, 255f);

    /// <summary>Shading depth of the rich texture (<c>texture.depth</c>, clamped to 0..1) — multiplies the gradients, shadows and gloss of rich drawing.</summary>
    internal static float Depth => Math.Clamp(Number("texture.depth"), 0f, 1f);

    /// <summary>The style switches of the ambient theme.</summary>
    internal static ThemeLook Look => LookCache.GetValue(Ambient ?? DvTheme.Default, ThemeLook.Of);

    private static readonly ConditionalWeakTable<DvTheme, ThemeLook> LookCache = new();

    /// <summary>Sets <see cref="Ambient"/> for a pass and restores the previous value when disposed.</summary>
    internal static Scope Enter(DvTheme? theme) => new(theme);

    internal readonly struct Scope : IDisposable
    {
        private readonly DvTheme? _previous;

        public Scope(DvTheme? theme)
        {
            _previous = Ambient;
            Ambient = theme ?? DvTheme.Default;
        }

        public void Dispose() => Ambient = _previous;
    }
}
