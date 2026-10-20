using SkiaSharp;

namespace Devinno.UI.Alarms;

/// <summary>
/// Computes how a <c>DvAlarmBanner</c> area is split. A communication-fault strip is placed at the top only when there is a communication fault (<c>comm-alarm</c>),
/// and the rest below is used as the unacknowledged alarm area. The bottom area is guaranteed at least <see cref="MinBottom"/> pixels first.
/// </summary>
/// <remarks>
/// An internal engine stage (public so the painter and layout use the same numbers). App code normally does not use it directly and uses
/// <c>&lt;DvAlarmBanner items="…" comm-alarm="…"/&gt;</c> in markup instead.
/// </remarks>
public static class AlarmBannerLayout
{
    /// <summary>Height of the communication-fault strip (px, 24). It shrinks below this if the banner is not tall enough.</summary>
    public const float StripHeight = 24f;
    /// <summary>Minimum height (px, 24) kept for the lower alarm area even when the communication-fault strip is shown.</summary>
    public const float MinBottom = 24f;
    /// <summary>Default alarm area height (px, 40) for a banner with no specified height. When there is a communication fault, <see cref="StripHeight"/> is added to it.</summary>
    public const float DefaultBottom = 40f;

    /// <summary>Splits the banner area into the communication-fault strip and the alarm area.</summary>
    /// <param name="bounds">The whole banner area.</param>
    /// <param name="hasComm">Whether there is a communication-fault message.</param>
    /// <returns>
    /// <c>Strip</c> is the communication-fault strip area; <see langword="null"/> if <paramref name="hasComm"/> is <see langword="false"/> or there is no room for the strip because the height is <see cref="MinBottom"/> or less.
    /// <c>Bottom</c> is the alarm area (all of <paramref name="bounds"/> if there is no strip).
    /// </returns>
    public static (SKRect? Strip, SKRect Bottom) Split(SKRect bounds, bool hasComm)
    {
        if (!hasComm)
            return (null, bounds);

        var strip = MathF.Max(0f, MathF.Min(StripHeight, bounds.Height - MinBottom));
        if (strip <= 0f)
            return (null, bounds);

        return (new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + strip),
            new SKRect(bounds.Left, bounds.Top + strip, bounds.Right, bounds.Bottom));
    }
}

/// <summary>
/// The computed positions of the header, rows and acknowledge buttons of a <c>DvAlarmList</c>. Drawing and acknowledge-button hit testing use the same computation so they never disagree.
/// </summary>
/// <remarks>
/// An internal engine stage. App code normally does not use it directly and uses <c>&lt;DvAlarmList items="…" onack="…"/&gt;</c> in markup instead.
/// Rows are in <see cref="AlarmView.Ordered"/> order, and rows that would overflow below the area are not created (no scrolling).
/// </remarks>
public sealed class AlarmListLayout
{
    /// <summary>Width of the acknowledge (ACK) button (px, 56). The button is placed 12px in from the right edge of the row, with 6px margins above and below.</summary>
    public const float AckButtonWidth = 56f;

    /// <summary>The header row area. Its height is the smaller of the row height and 30.</summary>
    public required SKRect Header { get; init; }
    /// <summary>The alarm rows that fit on screen (in display order). Rows from the first one clipped at the bottom of the area onward are not included.</summary>
    public required IReadOnlyList<AlarmListRow> Rows { get; init; }

    /// <summary>Computes the positions of the header, rows and acknowledge buttons within the list area.</summary>
    /// <param name="bounds">The whole list area.</param>
    /// <param name="items">The alarms to show. This method sorts them into display order.</param>
    /// <param name="rowHeight">Row height (px). 24 is used if less than 24.</param>
    /// <param name="showAck">Whether to show acknowledge buttons. If <see langword="false"/>, <see cref="AlarmListRow.AckButton"/> is <see langword="null"/> for every row.</param>
    /// <returns>The computed layout.</returns>
    public static AlarmListLayout Compute(SKRect bounds, IEnumerable<AlarmRow> items, float rowHeight, bool showAck = true)
    {
        var rh = MathF.Max(24f, rowHeight);
        var header = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + MathF.Min(30f, rh));
        var rows = new List<AlarmListRow>();
        var y = header.Bottom;
        foreach (var row in AlarmView.Ordered(items))
        {
            if (y >= bounds.Bottom)
                break;

            var rect = new SKRect(bounds.Left, y, bounds.Right, y + rh);
            SKRect? ack = row.Acked || !showAck
                ? null
                : new SKRect(rect.Right - 12f - AckButtonWidth, rect.Top + 6f, rect.Right - 12f, rect.Bottom - 6f);
            rows.Add(new AlarmListRow(row, rect, ack));
            y += rh;
        }

        return new AlarmListLayout { Header = header, Rows = rows };
    }

    /// <summary>Finds the alarm whose acknowledge button is at point <paramref name="p"/>.</summary>
    /// <param name="p">The point to test (in the same coordinate system as the list area).</param>
    /// <returns>The alarm whose acknowledge button is at that position, or <see langword="null"/> if outside any button.</returns>
    public AlarmRow? AckTargetAt(SKPoint p)
        => Rows.FirstOrDefault(r => r.AckButton is { } b && b.Contains(p.X, p.Y))?.Row;
}

/// <summary>The layout of one row of a <c>DvAlarmList</c>. Created by <see cref="AlarmListLayout.Compute"/>.</summary>
/// <param name="Row">The alarm shown in this row.</param>
/// <param name="Rect">The whole row area.</param>
/// <param name="AckButton">The acknowledge button area. <see langword="null"/> if the alarm is already acknowledged or the button is hidden.</param>
public sealed record AlarmListRow(AlarmRow Row, SKRect Rect, SKRect? AckButton);
