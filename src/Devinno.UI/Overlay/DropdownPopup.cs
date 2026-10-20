using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>
/// DvSelect dropdown list (pushed on the window surface's popup stack). Coordinates are window-relative.
/// Placed below the anchor (above if there is not enough room; if neither fits, shrunk to the larger side), kept inside the screen horizontally, up to 8 items plus internal scrolling.
/// Pressing an item selects it; dragging beyond the threshold distance scrolls (no selection).
/// </summary>
internal sealed class DropdownPopup : IPopup
{
    public const float ItemHeight = 40f;
    public const float Pad = 4f;
    public const int MaxVisible = 8;
    private const float Gap = 4f;
    private const float Margin = 8f;
    private const float DragThreshold = 8f;
    private const float WheelStep = 48f;

    public DropdownPopup(DvSurface owner, IValueSlot slot)
    {
        Owner = owner;
        Slot = slot;
    }

    /// <summary>The surface that owns the anchor box (the window surface or a slot surface: a DvApp area or page).</summary>
    public DvSurface Owner { get; }
    public IValueSlot Slot { get; }
    public SKRect Bounds { get; private set; }
    public float ScrollY { get; private set; }
    public int Hover { get; private set; } = -1;
    /// <summary>Index of the item chosen on the last release (-1 if none).</summary>
    public int Picked { get; private set; } = -1;

    private bool _placed;
    private SKPoint? _down;
    private float _downScroll;
    private bool _dragging;

    private IReadOnlyList<SelectOption> Options => Slot.Options;
    private float ContentHeight => Options.Count * ItemHeight + 2 * Pad;
    private float MaxScroll => Math.Max(0, ContentHeight - Bounds.Height);

    /// <summary>Positions the list from the anchor (window coordinates) and the screen size. On first placement, scrolls so the selected item is visible.</summary>
    public void Place(SKRect anchor, float screenW, float screenH)
    {
        var want = Math.Min(Options.Count, MaxVisible) * ItemHeight + 2 * Pad;
        var below = screenH - anchor.Bottom - Gap - Margin;
        var above = anchor.Top - Gap - Margin;
        float top, h;
        if (want <= below)
            (top, h) = (anchor.Bottom + Gap, want);
        else if (want <= above)
            (top, h) = (anchor.Top - Gap - want, want);
        else if (below >= above)
            (top, h) = (anchor.Bottom + Gap, Math.Max(ItemHeight + 2 * Pad, below));
        else
        {
            h = Math.Max(ItemHeight + 2 * Pad, above);
            top = anchor.Top - Gap - h;
        }

        var width = Math.Min(Math.Max(anchor.Width, NaturalWidth()), screenW - 2 * Margin);
        // 앵커가 화면 안이면 앵커와 맞추고, 넘칠 때만 화면 안으로 민다
        var left = Math.Clamp(anchor.Left, 0, Math.Max(0, screenW - width));
        Bounds = SKRect.Create(left, top, width, h);

        if (!_placed)
        {
            _placed = true;
            if (Slot.SelectedIndex >= 0)
                ScrollY = Slot.SelectedIndex * ItemHeight + ItemHeight / 2f - (h - 2 * Pad) / 2f;
        }

        ScrollY = Math.Clamp(ScrollY, 0, MaxScroll);
    }

    private float NaturalWidth()
    {
        var size = Slot.Style.FontSize;
        float w = 0;
        foreach (var o in Options)
        {
            using var f = TextFont.Create(o.Text.Length > 0 ? o.Text : " ", size, false);
            var iw = o.Icon is not null ? size + LayoutEngine.IconTextGap : 0;
            w = Math.Max(w, f.MeasureText(o.Text) + iw);
        }

        return w + 12 + 12 + size + 8; // 좌우 여백 + 체크 칸
    }

    /// <summary>Rectangle of item i (window coordinates, scroll applied; may not be visible).</summary>
    public SKRect ItemRect(int i)
        => SKRect.Create(Bounds.Left, Bounds.Top + Pad + i * ItemHeight - ScrollY, Bounds.Width, ItemHeight);

    private int IndexAt(SKPoint p)
    {
        if (!Bounds.Contains(p.X, p.Y))
            return -1;
        var i = (int)MathF.Floor((p.Y - Bounds.Top - Pad + ScrollY) / ItemHeight);
        return i >= 0 && i < Options.Count ? i : -1;
    }

    public void Down(SKPoint p)
    {
        _down = p;
        _downScroll = ScrollY;
        _dragging = false;
        Hover = IndexAt(p);
    }

    public void Move(SKPoint p)
    {
        if (_down is not { } d)
        {
            Hover = IndexAt(p);
            return;
        }

        if (!_dragging && SKPoint.Distance(p, d) >= DragThreshold && MaxScroll > 0)
            _dragging = true;
        if (_dragging)
        {
            ScrollY = Math.Clamp(_downScroll - (p.Y - d.Y), 0, MaxScroll);
            Hover = -1;
        }
    }

    public PopupResult Up(SKPoint p)
    {
        Picked = _down is not null && !_dragging ? IndexAt(p) : -1;
        _down = null;
        _dragging = false;
        return Picked >= 0 ? PopupResult.Commit : PopupResult.None;
    }

    public void Wheel(float delta) => ScrollY = Math.Clamp(ScrollY - delta * WheelStep, 0, MaxScroll);

    public void Paint(SKCanvas canvas, DvTheme? theme, DvStrings? strings)
    {
        var text = PopupChrome.Tok(theme, "text", new SKColor(0x0f, 0x17, 0x2a));
        var primary = PopupChrome.Tok(theme, "primary", new SKColor(0x25, 0x63, 0xeb));

        PopupChrome.Begin(canvas, Bounds, theme);
        var size = Slot.Style.FontSize;
        for (var i = 0; i < Options.Count; i++)
        {
            var row = ItemRect(i);
            if (row.Bottom < Bounds.Top || row.Top > Bounds.Bottom)
                continue;

            var selected = i == Slot.SelectedIndex;
            if (selected || i == Hover)
            {
                using var hp = new SKPaint { IsAntialias = true, Color = primary.WithAlpha(selected ? (byte)0x26 : (byte)0x14) };
                canvas.DrawRoundRect(SKRect.Inflate(row, -Pad, -1), 6, 6, hp);
            }

            var o = Options[i];
            var x = row.Left + 12;
            using var ink = new SKPaint { IsAntialias = true, Color = selected ? primary : text };
            if (o.Icon is { } spec && IconFont.TryResolve(spec, out var icon))
            {
                IconFont.Draw(canvas, icon, new SKPoint(x + size / 2f, row.MidY), size, ink);
                x += size + LayoutEngine.IconTextGap;
            }

            if (o.Text.Length > 0)
            {
                using var f = TextFont.Create(o.Text, size, bold: selected);
                canvas.DrawText(o.Text, x, row.MidY - (f.Metrics.Ascent + f.Metrics.Descent) / 2f, SKTextAlign.Left, f, ink);
            }

            if (selected && IconFont.TryResolve("fa-check", out var check))
                IconFont.Draw(canvas, check, new SKPoint(row.Right - 12 - size / 2f, row.MidY), size, ink);
        }

        // 넘치면 얇은 스크롤 표시
        if (MaxScroll > 0)
        {
            var track = Bounds.Height - 2 * Pad;
            var thumb = Math.Max(24, track * Bounds.Height / ContentHeight);
            var y = Bounds.Top + Pad + (track - thumb) * (ScrollY / MaxScroll);
            using var tp = new SKPaint { IsAntialias = true, Color = PopupChrome.Tok(theme, "scrollbar-thumb", new SKColor(0x64, 0x74, 0x8b, 0x80)) };
            canvas.DrawRoundRect(SKRect.Create(Bounds.Right - 6, y, 3, thumb), 1.5f, 1.5f, tp);
        }

        PopupChrome.End(canvas, Bounds, theme);
    }
}
