using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Widgets;

/// <summary>
/// What a <see cref="DvWidget"/> can see of its element: attribute values (bindings already resolved), the theme, the paint time, the element state, and text helpers.
/// </summary>
/// <remarks>Values are the snapshot taken when the screen was last built — the same as for built-in elements. A new context is passed on every call; do not keep it.</remarks>
public sealed class DvWidgetContext
{
    private readonly LayoutBox _box;
    private readonly DvTheme _theme;
    private readonly DvSurface? _owner;
    private readonly object? _vm;

    internal DvWidgetContext(LayoutBox box, DvTheme? theme, long timestampMs, DvSurface? owner = null, object? vm = null)
    {
        _box = box;
        _theme = theme ?? DvTheme.Default;
        TimestampMs = timestampMs;
        _owner = owner;
        _vm = vm;
        var r = box.Rect;
        var pad = box.Style.Padding;
        Bounds = r.IsEmpty ? SKRect.Empty : new SKRect(r.Left + pad.Left, r.Top + pad.Top, r.Right - pad.Right, r.Bottom - pad.Bottom);
    }

    /// <summary>
    /// Gets the content area (the element's area minus <c>padding</c>) — the same rectangle <see cref="DvWidget.Paint"/> receives, in the same coordinates as pointer points.
    /// <see cref="SKRect.Empty"/> in <see cref="DvWidget.Measure"/> (the element has no area yet).
    /// </summary>
    public SKRect Bounds { get; }

    /// <summary>
    /// Gets whether <see cref="Commit"/> can write: inside a pointer method, the element is enabled, and its <c>value</c> is a two-way binding (<c>{Bind …}</c>, or a two-way component parameter).
    /// </summary>
    public bool CanCommit => _owner is not null && !_box.Disabled && _box.ValueBindExpr is { } e && BindingResolver.IsTwoWay(e);

    /// <summary>
    /// Writes a value to the element's <c>value</c> binding, the same way built-in widgets do: <c>onbeforecommit="M"</c> is asked first (<c>bool M(T proposed)</c>, where
    /// <c>T</c> is the value's type or <see cref="object"/>), the value is converted to the property type, and inside <c>DvRepeat</c> it goes to the item.
    /// The screen is rebuilt afterwards.
    /// </summary>
    /// <param name="value">The new value.</param>
    /// <returns><see langword="true"/> if written; <see langword="false"/> if <see cref="CanCommit"/> is false, the gate refused, or the property setter failed (reported through <see cref="DvSurface.HandlerError"/>).</returns>
    /// <remarks>Only works inside pointer methods (<see cref="DvWidget.OnPointerDown"/> … <see cref="DvWidget.OnPointerUp"/>); elsewhere it returns <see langword="false"/>.</remarks>
    public bool Commit(object? value)
    {
        if (!CanCommit)
            return false;
        var tag = _box.Node.Tag;
        var types = value is null ? new[] { typeof(object) } : new[] { value.GetType(), typeof(object) };
        if (!Interaction.CommitGate.Allows(_owner, tag, _vm, _box.OnBeforeCommit, value, types))
            return false;
        if (!Interaction.HandlerErrors.TrySet(_owner, tag, _box.ValueContext(_vm), BindingResolver.ExtractPath(_box.ValueBindExpr!), value))
            return false;
        _owner!.NotifyValueWritten(_box);
        _owner.Invalidate();
        return true;
    }

    /// <summary>Gets the element's tag (the name the widget was registered under).</summary>
    public string Tag => _box.Node.Tag;

    /// <summary>Gets the theme of the screen.</summary>
    public DvTheme Theme => _theme;

    /// <summary>Gets the paint time in ms (the value passed to <c>RenderTo</c>; 0 outside painting). Use it for animation phases.</summary>
    public long TimestampMs { get; }

    /// <summary>Gets whether the element is disabled (its own <c>disabled</c> or a disabled ancestor).</summary>
    public bool Disabled => _box.Disabled;

    /// <summary>Gets the text color: the element's <c>color</c> if its style or class sets one, otherwise the theme's <c>text</c> token.</summary>
    public SKColor TextColor => _box.Style.Color.Equals(DefaultColor) ? Color("text", _box.Style.Color.ToSK()) : _box.Style.Color.ToSK();

    // 스타일이 color 를 정하지 않았을 때의 값(글자색은 상속되지 않는다)
    private static readonly DvColor DefaultColor = new ComputedStyle().Color;

    /// <summary>Gets the element's font size from its style (<c>font-size</c>).</summary>
    public float FontSize => _box.Style.FontSize;

    /// <summary>Gets whether the element has the attribute (literal or binding).</summary>
    /// <param name="attr">The attribute name.</param>
    public bool Has(string attr) => _box.WidgetValues?.ContainsKey(attr) == true;

    /// <summary>Gets an attribute's resolved value as the original object (a list, a number, a model object …). <see langword="null"/> if missing.</summary>
    /// <param name="attr">The attribute name.</param>
    public object? Value(string attr) => _box.WidgetValues?.GetValueOrDefault(attr).Value;

    /// <summary>Gets an attribute as text (bindings resolved, <c>format</c> of the binding applied). <see langword="null"/> if missing.</summary>
    /// <param name="attr">The attribute name.</param>
    public string? Text(string attr) =>
        _box.WidgetValues is { } values && values.TryGetValue(attr, out var v) ? BindingResolver.ToText(v.Value, v.Raw) : null;

    /// <summary>Gets an attribute as a number. Returns <paramref name="fallback"/> if it is missing or not a number.</summary>
    /// <param name="attr">The attribute name.</param>
    /// <param name="fallback">The value to use when there is no number.</param>
    public double Number(string attr, double fallback = 0) => Value(attr) switch
    {
        null => fallback,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        IConvertible c when c is not string => TryConvert(c, fallback),
        var o => double.TryParse(Text(attr) ?? o.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : fallback,
    };

    private static double TryConvert(IConvertible c, double fallback)
    {
        try
        {
            return c.ToDouble(CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            return fallback;
        }
    }

    /// <summary>Gets an attribute as a flag (<c>true</c>/<c>false</c>, a bool binding). Returns <paramref name="fallback"/> if missing.</summary>
    /// <param name="attr">The attribute name.</param>
    /// <param name="fallback">The value to use when the attribute is missing.</param>
    public bool Flag(string attr, bool fallback = false) => Value(attr) switch
    {
        null => fallback,
        bool b => b,
        _ => string.Equals(Text(attr), "true", StringComparison.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Gets a theme color token (<c>primary</c>, <c>text</c>, <c>tone-danger</c>, <c>gauge-track</c>, <c>state-run</c> …) as a color — the same tokens as <c>token(…)</c> in styles,
    /// so the widget follows light, dark and custom themes. Returns <paramref name="fallback"/> (magenta if not given) for an unknown token.
    /// </summary>
    /// <param name="token">The token name, with or without a leading <c>--</c>.</param>
    /// <param name="fallback">The color for an unknown token.</param>
    public SKColor Color(string token, SKColor? fallback = null) =>
        _theme.TryGet(token, out var raw) && DvColor.TryParse(raw, out var c) ? c.ToSK() : fallback ?? SKColors.Magenta;

    /// <summary>Measures the width of a single line of text in px (same fonts as built-in text, including the Korean fallback).</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">Font size in px.</param>
    /// <param name="bold">Whether bold.</param>
    public float MeasureText(string text, float size, bool bold = false)
    {
        using var font = TextFont.Create(text, size, bold, _box.Style.FontFamily);
        return font.MeasureText(text);
    }

    /// <summary>Draws a single line of text with its baseline at <paramref name="y"/> (same fonts as built-in text, including the Korean fallback).</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="text">The text.</param>
    /// <param name="x">The x of the anchor given by <paramref name="align"/>.</param>
    /// <param name="y">The baseline y.</param>
    /// <param name="size">Font size in px.</param>
    /// <param name="color">Text color (e.g. <see cref="TextColor"/> or <see cref="Color"/>).</param>
    /// <param name="align">Horizontal anchor.</param>
    /// <param name="bold">Whether bold.</param>
    public void DrawText(SKCanvas canvas, string text, float x, float y, float size, SKColor color, SKTextAlign align = SKTextAlign.Left, bool bold = false)
    {
        using var font = TextFont.Create(text, size, bold, _box.Style.FontFamily);
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(text, x, y, align, font, paint);
    }
}
