using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI.Interaction;

/// <summary>
/// Scroll bar geometry of a scroll box (shared by painting and thumb dragging). Present only on axes where content overflows; on that axis layout
/// leaves <see cref="Gutter"/> of the content area empty, so the scroll bar does not touch the content.
/// Coordinates are in the same coordinate system as the scroll box Rect.
/// </summary>
internal static class ScrollBars
{
    public const float Thickness = 6f;
    public const float MinThumb = 30f;

    /// <summary>Width by which the content layout area shrinks on an axis with a scroll bar (thumb + spacing).</summary>
    public const float Gutter = Thickness + 6f;

    /// <summary>Widens the thumb hit area inward so it is easier to grab by touch.</summary>
    private const float GrabExtra = 10f;

    public static SKRect? VerticalTrack(LayoutBox b)
    {
        if (!b.IsScroller || b.MaxScrollY <= 0)
            return null;
        var r = b.Rect;
        var h = r.Height - b.ScrollInsetTop - b.ScrollInsetBottom - (b.MaxScrollX > 0 ? Thickness : 0);
        return SKRect.Create(r.Right - Thickness, r.Top + b.ScrollInsetTop, Thickness, h);
    }

    public static SKRect? HorizontalTrack(LayoutBox b)
    {
        if (!b.IsScroller || b.MaxScrollX <= 0)
            return null;
        var r = b.Rect;
        var w = r.Width - (b.MaxScrollY > 0 ? Thickness : 0);
        return SKRect.Create(r.Left, r.Bottom - Thickness, w, Thickness);
    }

    public static SKRect? VerticalThumb(LayoutBox b)
    {
        if (VerticalTrack(b) is not { } t)
            return null;
        // 보이는 비율 = 스크롤되는 영역 기준(그리드는 헤더 제외)
        var view = b.Rect.Height - b.ScrollInsetTop - b.ScrollInsetBottom;
        var content = b.ContentHeight - b.ScrollInsetTop - b.ScrollInsetBottom;
        var len = MathF.Min(t.Height, MathF.Max(MinThumb, content > 0 ? t.Height * view / content : t.Height));
        var top = t.Top + (t.Height - len) * (b.ScrollY / b.MaxScrollY);
        return SKRect.Create(t.Left, top, t.Width, len);
    }

    public static SKRect? HorizontalThumb(LayoutBox b)
    {
        if (HorizontalTrack(b) is not { } t)
            return null;
        var len = MathF.Min(t.Width, MathF.Max(MinThumb, t.Width * b.Rect.Width / b.ContentWidth));
        var left = t.Left + (t.Width - len) * (b.ScrollX / b.MaxScrollX);
        return SKRect.Create(left, t.Top, len, t.Height);
    }

    /// <summary>If the point (scroll box coordinates) is in a thumb hit area, returns the axis (true = vertical).</summary>
    public static bool? HitThumb(LayoutBox b, SKPoint p)
    {
        if (VerticalThumb(b) is { } v &&
            new SKRect(v.Left - GrabExtra, v.Top, v.Right, v.Bottom).Contains(p.X, p.Y))
            return true;
        if (HorizontalThumb(b) is { } h &&
            new SKRect(h.Left, h.Top - GrabExtra, h.Right, h.Bottom).Contains(p.X, p.Y))
            return false;
        return null;
    }

    /// <summary>Change in scroll offset when the thumb is moved by delta along the axis.</summary>
    public static float OffsetPerThumbPixel(LayoutBox b, bool vertical)
    {
        if (vertical)
        {
            if (VerticalTrack(b) is not { } t || VerticalThumb(b) is not { } th)
                return 0;
            var travel = t.Height - th.Height;
            return travel > 0 ? b.MaxScrollY / travel : 0;
        }

        if (HorizontalTrack(b) is not { } ht || HorizontalThumb(b) is not { } hth)
            return 0;
        var htravel = ht.Width - hth.Width;
        return htravel > 0 ? b.MaxScrollX / htravel : 0;
    }
}
