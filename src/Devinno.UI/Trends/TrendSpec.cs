using SkiaSharp;

namespace Devinno.UI.Trends;

/// <summary>
/// The settings and channel data snapshot of a <c>DvTrend</c>, fixed at layout time. The layout engine builds it from markup attributes, and the painter draws from it alone.
/// </summary>
/// <remarks>
/// This is an internal engine stage. App code usually does not create it directly; it uses <c>&lt;DvTrend&gt;</c>/<c>&lt;DvTrendChannel&gt;</c> in markup and
/// <see cref="TrendBuffer"/>. The data is a copy taken at layout time, so to show new samples you must re-lay out with the host's <c>Invalidate()</c>.
/// </remarks>
public sealed class TrendSpec
{
    /// <summary>The 6 default colors (blue, cyan, purple, orange, green, red) cycled in channel order when a channel has no <c>color</c>.</summary>
    public static readonly SKColor[] Palette =
    {
        new(0x3b, 0x82, 0xf6), new(0x22, 0xd3, 0xee), new(0xa7, 0x8b, 0xfa),
        new(0xf5, 0x9e, 0x0b), new(0x10, 0xb9, 0x81), new(0xef, 0x44, 0x44)
    };

    /// <summary>Length of the visible time window (seconds, <c>window</c> attribute, default 60). A value of 0 or less becomes 60.</summary>
    public double WindowSeconds { get; init; } = 60;
    /// <summary>Time at the right edge of the window (the <see cref="DateTime"/> bound to the <c>end</c> attribute). If <see langword="null"/>, the latest sample time across all channels is used.</summary>
    public DateTime? End { get; init; }
    /// <summary>Lower bound of the value axis (<c>y-min</c>). If <see langword="null"/>, it is computed automatically from the visible data.</summary>
    public double? YMin { get; init; }
    /// <summary>Upper bound of the value axis (<c>y-max</c>). If <see langword="null"/>, it is computed automatically from the visible data.</summary>
    public double? YMax { get; init; }
    /// <summary>Whether to fill below the line with a translucent gradient (<c>area</c>, default <see langword="false"/>).</summary>
    public bool Area { get; init; }
    /// <summary>Whether to draw the line smoothly as a monotone cubic curve (<c>smooth</c>, default <see langword="true"/>). If <see langword="false"/>, a polyline is drawn.</summary>
    public bool Smooth { get; init; } = true;
    /// <summary>Channel data in the order of the child <c>DvTrendChannel</c> elements.</summary>
    public List<TrendChannelData> Channels { get; } = new();
}

/// <summary>
/// The fixed settings and sample snapshot of one <c>DvTrendChannel</c>. An item of <see cref="TrendSpec.Channels"/>.
/// </summary>
/// <remarks>This is an internal engine stage. App code usually does not create it directly.</remarks>
public sealed class TrendChannelData
{
    /// <summary>Channel name (<c>name</c>), the key used to look up samples in <see cref="TrendBuffer"/>. If not set, <c>ch0</c>, <c>ch1</c>, ….</summary>
    public required string Name { get; init; }
    /// <summary>Name shown in the legend (<c>label</c>). If <see langword="null"/>, the legend shows <see cref="Name"/>.</summary>
    public string? Label { get; init; }
    /// <summary>Unit appended after the latest value in the legend (<c>unit</c>). <see langword="null"/> if none.</summary>
    public string? Unit { get; init; }
    /// <summary>Line color (<c>color</c>; <c>#색</c> or <c>token(이름)</c> resolved against the theme). If not set or not resolvable, a color from <see cref="TrendSpec.Palette"/>.</summary>
    public required SKColor Color { get; init; }
    /// <summary>Samples to draw: the <c>points</c> binding value if present, otherwise <see cref="TrendBuffer.Snapshot"/> of the <c>source</c> buffer, or an empty array if neither exists.</summary>
    public required TrendPoint[] Points { get; init; }
}
