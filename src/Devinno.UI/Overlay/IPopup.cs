using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Release (pointer-up) result; the surface handles closing, committing, or starting field editing.</summary>
internal enum PopupResult { None, Cancel, Commit, EditField }

/// <summary>A popup pushed onto the window surface's popup stack (window coordinates). Only the topmost popup receives input.</summary>
internal interface IPopup
{
    /// <summary>The surface that owns the anchor box (the window surface or a slot surface: a DvApp area (bar) or page).</summary>
    DvSurface Owner { get; }
    /// <summary>Anchor = the value slot (position, value, write).</summary>
    IValueSlot Slot { get; }
    SKRect Bounds { get; }
    void Place(SKRect anchor, float screenW, float screenH);
    void Down(SKPoint p);
    void Move(SKPoint p);
    PopupResult Up(SKPoint p);
    void Wheel(float delta);
    void Paint(SKCanvas canvas, DvTheme? theme, DvStrings? strings);
}

/// <summary>Common popup chrome: shadow + a rounded <c>surface-overlay</c> surface + a <c>border</c> outline.</summary>
internal static class PopupChrome
{
    public const float Radius = 8f;

    public static SKColor Tok(DvTheme? theme, string name, SKColor fallback)
        => (theme ?? DvTheme.Default).TryGet(name, out var raw) && DvColor.TryParse(raw, out var c) ? c.ToSK() : fallback;

    public static void Begin(SKCanvas canvas, SKRect bounds, DvTheme? theme)
    {
        using (var sp = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 0x40), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8) })
            canvas.DrawRoundRect(SKRect.Create(bounds.Left, bounds.Top + 4, bounds.Width, bounds.Height), Radius, Radius, sp);
        using (var bp = new SKPaint { IsAntialias = true, Color = Tok(theme, "surface-overlay", SKColors.White) })
            canvas.DrawRoundRect(bounds, Radius, Radius, bp);
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(bounds, Radius, Radius), antialias: true);
    }

    public static void End(SKCanvas canvas, SKRect bounds, DvTheme? theme)
    {
        canvas.Restore();
        using var bs = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = Tok(theme, "border", new SKColor(0xcb, 0xd5, 0xe1)) };
        canvas.DrawRoundRect(bounds, Radius, Radius, bs);
    }
}

/// <summary>Placement of popups with a fixed content size (pickers); the height is not reduced.</summary>
internal static class PopupPlacement
{
    public const float Gap = 4f;
    public const float Margin = 8f;

    public static SKRect Fixed(SKRect anchor, float w, float h, float screenW, float screenH)
    {
        float top;
        if (anchor.Bottom + Gap + h <= screenH - Margin)
            top = anchor.Bottom + Gap;
        else if (anchor.Top - Gap - h >= Margin)
            top = anchor.Top - Gap - h;
        else
            top = Math.Max(Margin, Math.Min(screenH - Margin - h, anchor.MidY - h / 2f)); // 화면 안 세로 맞춤, 위 여백 우선
        var left = Math.Max(Margin, Math.Min(anchor.Left, screenW - Margin - w));     // 넘치면 화면 안으로, 화면보다 크면 왼쪽 여백
        return SKRect.Create(left, top, w, h);
    }
}
