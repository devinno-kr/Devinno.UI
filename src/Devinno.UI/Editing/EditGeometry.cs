using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>The on-screen position of the edit target — same rules as the Painter (children of scroll boxes are shifted by the offset; hidden/scroll clip to their own area).</summary>
internal static class EditGeometry
{
    /// <returns>True if found in the tree. <paramref name="clip"/> = the visible part of the box (Empty if fully hidden).</returns>
    public static bool TryLocate(LayoutBox root, LayoutBox target, out SKRect rect, out SKRect clip)
    {
        rect = clip = SKRect.Empty;
        return Walk(root, target, 0, 0, root.Rect, ref rect, ref clip);
    }

    private static bool Walk(LayoutBox box, LayoutBox target, float dx, float dy, SKRect visible,
        ref SKRect rect, ref SKRect clip)
    {
        var screen = new SKRect(box.Rect.Left + dx, box.Rect.Top + dy, box.Rect.Right + dx, box.Rect.Bottom + dy);
        if (ReferenceEquals(box, target))
        {
            rect = screen;
            clip = SKRect.Intersect(screen, visible);
            if (clip.IsEmpty)
                clip = SKRect.Empty;
            return true;
        }

        if (box.Style.Overflow is Overflow.Hidden or Overflow.Scroll)
            visible = SKRect.Intersect(visible, screen);
        if (box.IsScroller)
        {
            dx -= box.ScrollX;
            dy -= box.ScrollY;
        }

        foreach (var c in box.Children)
        {
            if (Walk(c, target, dx, dy, visible, ref rect, ref clip))
                return true;
        }

        return false;
    }
}
