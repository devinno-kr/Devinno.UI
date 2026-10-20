using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI.Interaction;

/// <summary>Hit-testing component that finds which box of the layout tree a point in screen coordinates hits. Takes scroll box scroll offsets into account.</summary>
/// <remarks>An internal engine stage. App code normally does not use it directly and uses markup bindings and <see cref="DvSurface"/>/<see cref="DvRenderer"/> instead. Custom tools can use it to find the element under a point.</remarks>
public static class HitTester
{
    /// <summary>Finds the deepest box containing the point (the innermost one; when boxes overlap, the one drawn later).</summary>
    /// <param name="box">The root box to start testing from.</param>
    /// <param name="p">A point in the same coordinate system as the root box.</param>
    /// <returns>The hit box, or <see langword="null"/> if even the root is not hit.</returns>
    public static LayoutBox? HitTest(LayoutBox box, SKPoint p)
    {
        var path = HitPath(box, p);
        return path.Count == 0 ? null : path[^1].Box;
    }

    /// <summary>
    /// Finds the box that should receive input. Walking up from the deepest box toward the root, returns the first interactive box found (<c>onclick</c>, input widgets, grids,
    /// alarm widgets that can be acknowledged, boxes with hover/press styles); if there is none, returns the deepest box (the same rule as HTML event bubbling).
    /// </summary>
    /// <remarks>
    /// If a disabled box (<see cref="LayoutBox.Disabled"/>, including disabled state inherited from ancestors) is met on the way up, that box is returned and the walk stops.
    /// So input pressed on a disabled area is swallowed there and does not leak to an enabled ancestor outside it (<c>onclick</c>, etc.), just like a disabled button in HTML.
    /// A disabled box has no click, commit or hover/press styles, so in effect nothing happens.
    /// Only visible parts are hit. For <c>overflow:hidden</c> and <c>overflow:scroll</c> boxes (including grids), children are not tested outside the box's area, so clipped parts and children pushed outside a scroll box's viewport are not hit.
    /// Conversely, a child that sticks out of an <c>overflow:visible</c> (default) parent and is visible (for example a <c>position:absolute</c> badge) is hit.
    /// Among overlapping siblings, the one drawn later (visible on top) is hit first.
    /// </remarks>
    /// <param name="box">The root box to start testing from.</param>
    /// <param name="p">A point in the same coordinate system as the root box.</param>
    /// <returns>The target box, or <see langword="null"/> if even the root is not hit.</returns>
    public static LayoutBox? HitTarget(LayoutBox box, SKPoint p) => HitTarget(box, p, out _);

    /// <summary>Same as <see cref="HitTarget(LayoutBox, SKPoint)"/>, but also returns the point translated into the target box's coordinate system.</summary>
    /// <param name="box">The root box to start testing from.</param>
    /// <param name="p">A point in the same coordinate system as the root box.</param>
    /// <param name="local">The point translated into the same coordinate system as the target box's <see cref="LayoutBox.Rect"/> (scroll offsets applied inside scroll boxes). <paramref name="p"/> if there is no target.</param>
    /// <returns>The target box, or <see langword="null"/> if even the root is not hit.</returns>
    public static LayoutBox? HitTarget(LayoutBox box, SKPoint p, out SKPoint local)
    {
        var path = HitPath(box, p);
        local = p;
        if (path.Count == 0)
            return null;

        for (var i = path.Count - 1; i >= 0; i--)
        {
            // 비활성 상자에서 멈춘다 — 비활성 영역의 누름이 바깥 활성 조상의 onclick 으로 새지 않게(HTML disabled 와 같음)
            if (path[i].Box.Disabled || IsInteractive(path[i].Box))
            {
                local = path[i].Local;
                return path[i].Box;
            }
        }

        local = path[^1].Local;
        return path[^1].Box;
    }

    /// <summary>
    /// The path of boxes containing the point, starting from the root. <c>Local</c> = the point in that box's Rect coordinate system.
    /// When descending into the children of a scroll box, the scroll offset is added (a point outside the viewport is not contained by the box itself, so it never reaches the children).
    /// </summary>
    internal static List<(LayoutBox Box, SKPoint Local)> HitPath(LayoutBox root, SKPoint p)
    {
        var path = new List<(LayoutBox, SKPoint)>();
        Collect(root, p, path);
        return path;
    }

    // 보이는 곳 = 닿는 곳. 자르는 상자(overflow hidden·scroll, 그리드)는 자기 영역 밖이면 자식도 안 본다(Painter 의 클립과 같은 조건).
    // 자르지 않는 상자는 자기 영역 밖이어도 삐져나와 보이는 자식을 찾는다 — 그 경우 자식에 닿았을 때만 경로에 남는다.
    private static bool Collect(LayoutBox box, SKPoint p, List<(LayoutBox, SKPoint)> path)
    {
        var inside = box.Rect.Contains(p.X, p.Y);
        // 모양이 정해진 위젯(노브·토글 등)은 그려진 모양 안만 닿는다 — 늘어난 상자의 빈 곳을 눌러도 값이 바뀌지 않게
        if (inside && box.Children.Count == 0)
            inside = Painting.WidgetMetrics.ShapeRect(box).Contains(p.X, p.Y);
        if (!inside && (box.IsScroller || box.Style.Overflow == Overflow.Hidden))
            return false;

        var at = path.Count;
        path.Add((box, p));
        var inner = box.IsScroller ? new SKPoint(p.X + box.ScrollX, p.Y + box.ScrollY) : p;
        for (var i = box.Children.Count - 1; i >= 0; i--)
        {
            if (Collect(box.Children[i], inner, path))
                return true;
        }

        if (inside)
            return true;

        path.RemoveAt(at);
        return false;
    }

    private static bool IsInteractive(LayoutBox b)
        => !string.IsNullOrEmpty(b.OnClick) || b.HasCodeClick || b.Node.Tag is "DvToggle" or "DvCheckBox" or "DvRadio" or "DvKnob" or "DvSlider" or "DvInput" or "DvSelect" or "DvDateTimeInput" or "DvColorInput" || b.Grid is not null ||
           (!string.IsNullOrEmpty(b.OnAck) && b.AlarmShowAck && AlarmInteraction.IsAlarmWidget(b)) ||
           b.StyleHover is not null || b.StylePress is not null;
}
