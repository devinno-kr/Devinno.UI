using Devinno.UI.Components;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>
/// Renders one markup screen to a PNG image without a window. Use it to check that a screen looks as intended, or to make snapshots for tests and documentation.
/// </summary>
/// <remarks>
/// <para>Overloads that take markup parse and lay out from scratch on every call. There is no interaction state (hover, press, scroll, editing),
/// and the background is cleared to white before drawing. The overload that takes a <see cref="DvSurface"/> draws that screen's current state as is.</para>
/// <para>Sizes are in px and must be 1 or more. If no theme is given, <see cref="DvTheme.Default"/> (the dark theme) is used.
/// These are static methods with no shared state, so the markup overloads may be called from any thread (as long as no other thread modifies the passed VM at the same time).</para>
/// </remarks>
/// <example>
/// <code>
/// // Get the bytes (for comparison in tests, etc.)
/// byte[] png = DvRenderer.RenderToPng(markup, vm, 1280, 800);
///
/// // Save to a file (light theme)
/// DvRenderer.RenderToFile(markup, vm, 1280, 800, "screen.png", DvTheme.Light);
///
/// // To see another phase of a blinking widget, specify the time (DvLamp default blink period 500ms)
/// DvRenderer.RenderToFile(markup, vm, 1280, 800, "blink-phase2.png", null, null, timestampMs: 500);
/// </code>
/// </example>
public static class DvRenderer
{
    /// <summary>Renders a markup screen with the default theme and returns the PNG bytes.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <returns>The PNG file contents.</returns>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty, or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    public static byte[] RenderToPng(string markup, object? vm, int width, int height)
        => RenderToPng(markup, vm, width, height, theme: null, registry: null);

    /// <summary>Renders a markup screen with the specified theme and returns the PNG bytes.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="theme">The theme. <see cref="DvTheme.Default"/> if <see langword="null"/>.</param>
    /// <returns>The PNG file contents.</returns>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty, or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    public static byte[] RenderToPng(string markup, object? vm, int width, int height, DvTheme? theme)
        => RenderToPng(markup, vm, width, height, theme, registry: null);

    /// <summary>Renders a markup screen with the default theme and a user component registry, and returns the PNG bytes.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="registry">The registry holding the user components used in the markup (may be <see langword="null"/>).</param>
    /// <returns>The PNG file contents.</returns>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty, or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    public static byte[] RenderToPng(string markup, object? vm, int width, int height, ComponentRegistry? registry)
        => RenderToPng(markup, vm, width, height, theme: null, registry);

    /// <summary>
    /// Renders a markup screen with the theme, registry, time and multilingual table all specified, and returns the PNG bytes. All other markup overloads delegate to this method.
    /// </summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="theme">The theme. <see cref="DvTheme.Default"/> if <see langword="null"/>.</param>
    /// <param name="registry">The user component registry (may be <see langword="null"/>).</param>
    /// <param name="timestampMs">The time to render at (ms). Determines the on/off phase of blinking widgets. Default 0.</param>
    /// <param name="strings">The multilingual table used for <c>{L10n.key}</c> and <c>@key</c> in the markup (may be <see langword="null"/>).</param>
    /// <returns>The PNG file contents.</returns>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is <see langword="null"/>, empty, or whitespace only.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    public static byte[] RenderToPng(string markup, object? vm, int width, int height,
        DvTheme? theme, ComponentRegistry? registry, long timestampMs = 0, Localization.DvStrings? strings = null)
    {
        var root = MarkupParser.Parse(markup);
        // VM 읽기 예외는 Trace 오류로만(알릴 화면 없음) — 이 한 장을 그리는 동안 같은 대상은 한 번
        var prevRead = HandlerErrors.EnterRead(new HandlerErrors.ReadScope(null));
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        try
        {
            var box = new LayoutEngine(theme, strings).Layout(root, vm, width, height, registry);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            new Painter(theme, strings).Paint(canvas, box, vm, null, timestampMs);
            canvas.Flush();
        }
        finally
        {
            HandlerErrors.ExitRead(prevRead);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Renders a markup screen with the default theme and saves it as a PNG file. An existing file at the same path is overwritten.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="path">The file path to save to.</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is empty or <paramref name="path"/> is invalid.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void RenderToFile(string markup, object? vm, int width, int height, string path)
        => File.WriteAllBytes(path, RenderToPng(markup, vm, width, height));

    /// <summary>Renders a markup screen with the specified theme and saves it as a PNG file. An existing file at the same path is overwritten.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="path">The file path to save to.</param>
    /// <param name="theme">The theme. <see cref="DvTheme.Default"/> if <see langword="null"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is empty or <paramref name="path"/> is invalid.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void RenderToFile(string markup, object? vm, int width, int height, string path, DvTheme? theme)
        => File.WriteAllBytes(path, RenderToPng(markup, vm, width, height, theme));

    /// <summary>Renders a markup screen with the default theme and a user component registry, and saves it as a PNG file. An existing file at the same path is overwritten.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="path">The file path to save to.</param>
    /// <param name="registry">The user component registry (may be <see langword="null"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is empty or <paramref name="path"/> is invalid.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void RenderToFile(string markup, object? vm, int width, int height, string path,
        ComponentRegistry? registry)
        => File.WriteAllBytes(path, RenderToPng(markup, vm, width, height, registry));

    /// <summary>Renders a markup screen with the specified theme and user component registry, and saves it as a PNG file. An existing file at the same path is overwritten.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="path">The file path to save to.</param>
    /// <param name="theme">The theme. <see cref="DvTheme.Default"/> if <see langword="null"/>.</param>
    /// <param name="registry">The user component registry (may be <see langword="null"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is empty or <paramref name="path"/> is invalid.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void RenderToFile(string markup, object? vm, int width, int height, string path,
        DvTheme? theme, ComponentRegistry? registry)
        => File.WriteAllBytes(path, RenderToPng(markup, vm, width, height, theme, registry));

    /// <summary>Renders a markup screen with the theme, registry, time and multilingual table all specified, and saves it as a PNG file. An existing file at the same path is overwritten.</summary>
    /// <param name="markup">Screen markup (XML) with a single root element.</param>
    /// <param name="vm">The view model to bind (may be <see langword="null"/>).</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="path">The file path to save to.</param>
    /// <param name="theme">The theme. <see cref="DvTheme.Default"/> if <see langword="null"/>.</param>
    /// <param name="registry">The user component registry (may be <see langword="null"/>).</param>
    /// <param name="timestampMs">The time to render at (ms). Determines the on/off phase of blinking widgets.</param>
    /// <param name="strings">The multilingual table (may be <see langword="null"/>).</param>
    /// <exception cref="ArgumentException"><paramref name="markup"/> is empty or <paramref name="path"/> is invalid.</exception>
    /// <exception cref="System.Xml.XmlException">The markup has invalid XML syntax.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void RenderToFile(string markup, object? vm, int width, int height, string path,
        DvTheme? theme, ComponentRegistry? registry, long timestampMs, Localization.DvStrings? strings = null)
        => File.WriteAllBytes(path, RenderToPng(markup, vm, width, height, theme, registry, timestampMs, strings));

    /// <summary>
    /// Renders an existing <see cref="DvSurface"/> in its current state (scroll, editing, popups, virtual keyboard, and for <see cref="App.DvApp.Surface"/> also its region and page slots) and saves it as a PNG file.
    /// An existing file at the same path is overwritten.
    /// </summary>
    /// <remarks>
    /// <para>Motion in progress is drawn finished: CSS <c>transition</c>s, page transitions (<see cref="App.DvPageTransition"/>) and <c>DvExpander</c> height changes jump to their end state,
    /// so the image shows the result of the last change (for example the new tab highlight after a page switch). Blinking is drawn at time 0.
    /// Rendering in a window (<see cref="DvSurface.RenderTo(SKCanvas, float, float, long)"/>) still animates.</para>
    /// <para>Internally calls <see cref="DvSurface.RenderTo(SKCanvas, float, float)"/> (time 0), so that screen's layout size changes to <paramref name="width"/>×<paramref name="height"/>
    /// and <see cref="DvSurface.NeedsRender"/> is reset. Call on the thread that owns the screen (the UI thread).</para>
    /// </remarks>
    /// <param name="surface">The screen to render.</param>
    /// <param name="width">Image width (px).</param>
    /// <param name="height">Image height (px).</param>
    /// <param name="path">The file path to save to.</param>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void RenderToFile(DvSurface surface, int width, int height, string path)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var sk = SKSurface.Create(info);
        surface.SettleMotion = true; // 시각 0 으로만 그리므로 — 움직임을 끝내지 않으면 시작점(바뀌기 전 모습)에 멈춘 그림이 된다
        try
        {
            surface.RenderTo(sk.Canvas, width, height);
        }
        finally
        {
            surface.SettleMotion = false;
        }
        sk.Canvas.Flush();

        using var image = sk.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
