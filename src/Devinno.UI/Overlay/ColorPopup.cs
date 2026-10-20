using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Color popup — saturation/value square, hue bar, swatches, # field, Cancel/OK. State is in <see cref="ColorPickerState"/>.</summary>
internal sealed class ColorPopup : IPopup
{
    public const float Width = 300f, Pad = 8f, SvW = 240f, SvH = 200f, BarGap = 12f, BarW = 32f,
        SwatchSize = 40f, FieldH = 40f, ButtonH = 40f, RowGap = 8f;
    private const int None = 0, Sv = 1, Bar = 2, Hex = 3, Cancel = 4, Ok = 5;
    private int _down;

    public ColorPopup(DvSurface owner, IValueSlot slot, ColorPickerState state)
    {
        Owner = owner;
        Slot = slot;
        State = state;
        HadValue = slot.Value is not null;
    }

    public DvSurface Owner { get; }
    public IValueSlot Slot { get; }
    public SKRect Bounds { get; private set; }
    public ColorPickerState State { get; }
    public bool HadValue { get; }
    public (string? Text, bool Hide, string? Error) HexEdit { get; set; }

    public static float Height => Pad + SvH + RowGap + FieldH + RowGap + ButtonH + Pad;

    public void Place(SKRect anchor, float screenW, float screenH)
        => Bounds = PopupPlacement.Fixed(anchor, Width, Height, screenW, screenH);

    public SKRect SvRect => SKRect.Create(Bounds.Left + Pad, Bounds.Top + Pad, SvW, SvH);
    public SKRect BarRect => SKRect.Create(SvRect.Right + BarGap, SvRect.Top, BarW, SvH);
    public SKRect SwatchRect => SKRect.Create(Bounds.Left + Pad, SvRect.Bottom + RowGap, SwatchSize, FieldH);
    public SKRect HexRect => new(SwatchRect.Right + Pad, SwatchRect.Top, Bounds.Right - Pad, SwatchRect.Bottom);
    public SKRect CancelRect => SKRect.Create(Bounds.Left + Pad, Bounds.Bottom - Pad - ButtonH, (Width - 3 * Pad) / 2f, ButtonH);
    public SKRect OkRect => SKRect.Create(CancelRect.Right + Pad, CancelRect.Top, (Width - 3 * Pad) / 2f, ButtonH);

    private int HitId(SKPoint p)
        => SvRect.Contains(p.X, p.Y) ? Sv : BarRect.Contains(p.X, p.Y) ? Bar : HexRect.Contains(p.X, p.Y) ? Hex
         : CancelRect.Contains(p.X, p.Y) ? Cancel : OkRect.Contains(p.X, p.Y) ? Ok : None;

    // 누른 영역에 고정 — 영역 밖은 가장자리로 클램프
    private void Drag(SKPoint p)
    {
        if (_down == Sv)
            State.SetSV((p.X - SvRect.Left) / SvRect.Width, 1 - (p.Y - SvRect.Top) / SvRect.Height);
        else if (_down == Bar)
            State.SetHue((p.Y - BarRect.Top) / BarRect.Height * 360);
    }

    public void Down(SKPoint p) { _down = HitId(p); Drag(p); }
    public void Move(SKPoint p) => Drag(p);
    public void Wheel(float delta) { }

    public PopupResult Up(SKPoint p)
    {
        var down = _down;
        _down = None;
        if (down is None or Sv or Bar || HitId(p) != down)
            return PopupResult.None;
        return down switch { Cancel => PopupResult.Cancel, Ok => PopupResult.Commit, Hex => PopupResult.EditField, _ => PopupResult.None };
    }

    public void Paint(SKCanvas canvas, DvTheme? theme, DvStrings? strings)
        => Painting.Painter.PaintColorPopup(canvas, this, theme, strings, Slot.Style.FontSize);
}
