namespace Devinno.UI.Motion;

/// <summary>
/// A helper that computes animation phase from the render loop's timestamp (ms).
/// </summary>
/// <remarks>
/// It holds no state and computes from the timestamp alone, so blinking widgets (lamps, alarms, etc.) on one screen drawn with the same timestamp stay in sync with each other.
/// Built-in widgets use it internally, and you can use it when you want custom drawing to blink in the same rhythm.
/// </remarks>
public static class MotionPhase
{
    /// <summary>
    /// Computes whether the current time is in the on phase of a square-wave blink. <c>[0, interval)</c> is on and <c>[interval, 2·interval)</c> is off, repeating.
    /// </summary>
    /// <param name="timestampMs">The current time (ms) passed by the render loop. The whole screen must use the same value to stay in sync.</param>
    /// <param name="intervalMs">The length (ms) of each on and off phase. If 0 or less, it does not blink and is always on.</param>
    /// <returns><see langword="true"/> if in the on phase.</returns>
    public static bool BlinkOn(long timestampMs, int intervalMs)
        => intervalMs <= 0 || (timestampMs / intervalMs) % 2 == 0;
}
