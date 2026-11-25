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
        var path = Path(box, p);
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
        var path = Path(box, p);
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
    /// The path of boxes containing the point, from the root down to the deepest hit box. Same visibility rules as <see cref="HitTarget(LayoutBox, SKPoint)"/>, without the walk up to an interactive box.
    /// </summary>
    /// <remarks>
    /// <para>A screen editor uses it to find which element of the edited file was clicked: walk the path from the end toward the root and take the first box whose
    /// <see cref="LayoutBox.Origin"/> is an element of the file. Inside a user component the boxes come from the component's template; the component's root box
    /// has the instance element as its <see cref="LayoutBox.Origin"/>.</para>
    /// <para>Scroll offsets of scroll boxes and the shrinking of process diagrams (<c>fit="scale-down"</c>) are followed. The <c>transform</c> style is not (it moves the drawing only).</para>
    /// </remarks>
    /// <param name="root">The root box to start testing from.</param>
    /// <param name="p">A point in the same coordinate system as the root box.</param>
    /// <param name="wholeBox">If <see langword="true"/>, a widget with a fixed drawn shape (knob, toggle, …) is hit anywhere in its box; by default only inside the drawn shape, as for input.</param>
    /// <returns>The boxes from the root to the deepest hit box, each with <c>Local</c> = the point in that box's <see cref="LayoutBox.Rect"/> coordinate system. Empty if even the root is not hit.</returns>
    public static IReadOnlyList<(LayoutBox Box, SKPoint Local)> HitPath(LayoutBox root, SKPoint p, bool wholeBox = false) => Path(root, p, wholeBox);

    // 입력용 경로(모양 안만). 스크롤 상자 자식으로 내려갈 때 스크롤 위치를 더한다(뷰포트 밖 점은 그 상자부터 안 맞으므로 자식에 닿지 않는다).
    internal static List<(LayoutBox Box, SKPoint Local)> Path(LayoutBox root, SKPoint p, bool wholeBox = false)
    {
        var path = new List<(LayoutBox, SKPoint)>();
        Collect(root, p, path, wholeBox);
        return path;
    }

    // 보이는 곳 = 닿는 곳. 자르는 상자(overflow hidden·scroll, 그리드)는 자기 영역 밖이면 자식도 안 본다(Painter 의 클립과 같은 조건).
    // 자르지 않는 상자는 자기 영역 밖이어도 삐져나와 보이는 자식을 찾는다 — 그 경우 자식에 닿았을 때만 경로에 남는다.
    private static bool Collect(LayoutBox box, SKPoint p, List<(LayoutBox, SKPoint)> path, bool wholeBox)
    {
        var inside = box.Rect.Contains(p.X, p.Y);
        // 모양이 정해진 위젯(노브·토글 등)은 그려진 모양 안만 닿는다 — 늘어난 상자의 빈 곳을 눌러도 값이 바뀌지 않게
        if (inside && box.Children.Count == 0 && !wholeBox)
            inside = Painting.WidgetMetrics.ShapeRect(box).Contains(p.X, p.Y);
        if (!inside && (box.IsScroller || box.Style.Overflow == Overflow.Hidden))
            return false;

        var at = path.Count;
        path.Add((box, p));
        var inner = box.IsScroller ? new SKPoint(p.X + box.ScrollX, p.Y + box.ScrollY) : p;
        if (box.HasContentTransform)
            inner = new SKPoint((inner.X - box.ContentOffset.X) / box.ContentScale, (inner.Y - box.ContentOffset.Y) / box.ContentScale); // 줄여 그린 내용(공정도 fit="scale-down")
        for (var i = box.Children.Count - 1; i >= 0; i--)
        {
            if (Collect(box.Children[i], inner, path, wholeBox))
                return true;
        }

        if (inside)
            return true;

        path.RemoveAt(at);
        return false;
    }

    /// <summary>
    /// Where <paramref name="target"/> is drawn, in the coordinates of <paramref name="root"/> (the surface for its root box) — the same mapping drawing and hit testing use:
    /// scroll offsets of the enclosing scroll boxes and the content scale of a shrunk process diagram (<see cref="LayoutBox.ContentScale"/>) are applied.
    /// </summary>
    /// <param name="root">The box to measure from, usually the root returned by <see cref="Layout.LayoutEngine.Layout(Markup.ElementNode, object?, float, float, Components.ComponentRegistry?)"/>.</param>
    /// <param name="target">A box in that tree.</param>
    /// <param name="rect">The drawn rectangle of <paramref name="target"/>.</param>
    /// <param name="visible">The part of <paramref name="rect"/> not clipped by an enclosing scroll box or <c>overflow:hidden</c> box; <see cref="SKRect.Empty"/> when fully hidden.</param>
    /// <returns><see langword="false"/> if <paramref name="target"/> is not in the tree of <paramref name="root"/>.</returns>
    public static bool TryGetDrawnRect(LayoutBox root, LayoutBox target, out SKRect rect, out SKRect visible)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(target);
        return EditGeometry.TryLocate(root, target, out rect, out visible);
    }

    /// <summary>The drawing scale of <paramref name="target"/> on the hit path (product of the content scales of the boxes above it) — screen distance = box distance × scale.</summary>
    internal static float ScaleOf(IReadOnlyList<(LayoutBox Box, SKPoint Local)> path, LayoutBox? target)
    {
        var scale = 1f;
        foreach (var (b, _) in path)
        {
            if (ReferenceEquals(b, target))
                break;
            scale *= b.ContentScale;
        }

        return scale;
    }

    private static bool IsInteractive(LayoutBox b)
        => !string.IsNullOrEmpty(b.OnClick) || b.HasCodeClick || b.Node.Tag is "DvToggle" or "DvCheckBox" or "DvRadio" or "DvKnob" or "DvSlider" or "DvInput" or "DvSelect" or "DvDateTimeInput" or "DvColorInput" || b.Grid is not null ||
           (!string.IsNullOrEmpty(b.OnAck) && b.AlarmShowAck && AlarmInteraction.IsAlarmWidget(b)) ||
           b.StyleHover is not null || b.StylePress is not null;
}
