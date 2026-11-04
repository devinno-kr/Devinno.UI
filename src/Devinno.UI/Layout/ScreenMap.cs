using SkiaSharp;

namespace Devinno.UI.Layout;

/// <summary>Box coordinates → screen coordinates: point × <see cref="Scale"/> + <see cref="Offset"/>. Only translation and uniform scaling occur (scroll offsets and <see cref="LayoutBox.ContentScale"/>).</summary>
internal readonly record struct ScreenMap(float Scale, SKPoint Offset)
{
    public static readonly ScreenMap Identity = new(1f, default);

    public SKPoint Map(SKPoint p) => new(p.X * Scale + Offset.X, p.Y * Scale + Offset.Y);

    public SKRect Map(SKRect r) => new(r.Left * Scale + Offset.X, r.Top * Scale + Offset.Y, r.Right * Scale + Offset.X, r.Bottom * Scale + Offset.Y);

    /// <summary>The map for the children of <paramref name="box"/>: its scroll offset first, then its content transform (the same order the painter applies them).</summary>
    public ScreenMap Into(LayoutBox box)
    {
        var m = this;
        if (box.IsScroller)
            m = new ScreenMap(m.Scale, new SKPoint(m.Offset.X - box.ScrollX * m.Scale, m.Offset.Y - box.ScrollY * m.Scale));
        if (box.HasContentTransform)
            m = new ScreenMap(m.Scale * box.ContentScale, new SKPoint(m.Offset.X + box.ContentOffset.X * m.Scale, m.Offset.Y + box.ContentOffset.Y * m.Scale));
        return m;
    }
}
