using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>The on-screen position of the edit target — same rules as the Painter (children of scroll boxes are shifted by the offset, children of a shrunk process diagram are scaled; hidden/scroll clip to their own area).</summary>
internal static class EditGeometry
{
    /// <returns>True if found in the tree. <paramref name="clip"/> = the visible part of the box (Empty if fully hidden).</returns>
    public static bool TryLocate(LayoutBox root, LayoutBox target, out SKRect rect, out SKRect clip)
        => TryLocate(root, target, out rect, out clip, out _);

    // map = target 좌표(그 Rect 공간) → root 좌표
    public static bool TryLocate(LayoutBox root, LayoutBox target, out SKRect rect, out SKRect clip, out ScreenMap map)
    {
        rect = clip = SKRect.Empty;
        map = ScreenMap.Identity;
        return Walk(root, target, ScreenMap.Identity, root.Rect, ref rect, ref clip, ref map);
    }

    private static bool Walk(LayoutBox box, LayoutBox target, ScreenMap map, SKRect visible,
        ref SKRect rect, ref SKRect clip, ref ScreenMap found)
    {
        var screen = map.Map(box.Rect);
        if (ReferenceEquals(box, target))
        {
            found = map;
            rect = screen;
            clip = SKRect.Intersect(screen, visible);
            if (clip.IsEmpty)
                clip = SKRect.Empty;
            return true;
        }

        if (box.Style.Overflow is Overflow.Hidden or Overflow.Scroll)
            visible = SKRect.Intersect(visible, screen);
        var inner = map.Into(box);
        foreach (var c in box.Children)
        {
            if (Walk(c, target, inner, visible, ref rect, ref clip, ref found))
                return true;
        }

        return false;
    }
}
