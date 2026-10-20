namespace Devinno.UI.Trends;

/// <summary>
/// One time-series sample of a trend. Bind it as <c>IEnumerable&lt;TrendPoint&gt;</c> to <c>points</c> of a <c>DvTrendChannel</c>, or add it to a <see cref="TrendBuffer"/>.
/// </summary>
/// <param name="Time">Sample time. Determines the position on the horizontal axis.</param>
/// <param name="Value">Sample value. <see cref="double.NaN"/> is treated as "no value" and breaks the line at that point (e.g. to show a communication loss period).</param>
/// <example>
/// <code>
/// var history = new List&lt;TrendPoint&gt;
/// {
///     new(new DateTime(2026, 10, 14, 9, 0, 0), 41.2),
///     new(new DateTime(2026, 10, 14, 9, 0, 1), double.NaN),   // the line breaks here
///     new(new DateTime(2026, 10, 14, 9, 0, 2), 41.8),
/// };
/// </code>
/// </example>
public readonly record struct TrendPoint(DateTime Time, double Value);

/// <summary>
/// <b>Optional</b>: a per-channel ring buffer for live trends. When the app adds values read from polling, a Web API or a PLC under each channel name,
/// <c>DvTrend</c> reads each channel's recent samples and paints them when the screen is laid out.
/// </summary>
/// <remarks>
/// <para><b>Ring buffer</b>: each channel keeps up to <see cref="Capacity"/> samples; when full, the oldest samples are overwritten first. A channel is created on its first <see cref="Add"/>.
/// Samples are kept in insertion order and are not sorted by time, so add them in time order.</para>
/// <para><b>Threads</b>: every method uses an internal lock, so it is safe to <see cref="Add"/> on a polling thread and <see cref="Snapshot"/> on the UI thread.
/// The lock protects only the data inside this buffer.</para>
/// <para><b>Screen connection</b>: expose the buffer as a VM property, bind it to <c>&lt;DvTrend source="{Trend}"&gt;</c>, and match the <c>name</c> of each child
/// <c>&lt;DvTrendChannel name="temp"/&gt;</c> to a channel name. <c>DvTrend</c> takes a <see cref="Snapshot"/> at layout time, so
/// after adding samples you must call <c>ViewWindow.Invalidate()</c> (on WinForms, <c>ViewControl.MarkUpdated()</c>), not a repaint (<c>Repaint()</c>), for the new values to appear.
/// Both may be called from another thread.</para>
/// <para>For data that already exists in full, such as a history query, do not put it in the buffer; bind an <c>IEnumerable&lt;TrendPoint&gt;</c> directly to <c>points</c> of the <c>DvTrendChannel</c>.
/// A channel with <c>points</c> bound does not read the <c>source</c> buffer. Long-term storage of the data is also the app's responsibility; this buffer lives in memory only.</para>
/// </remarks>
/// <example>
/// <code>
/// // markup
/// // &lt;DvTrend source="{Trend}" window="120" area="true"&gt;
/// //   &lt;DvTrendChannel name="temp" label="온도" unit="C" color="#22d3ee"/&gt;
/// //   &lt;DvTrendChannel name="press" label="압력" unit="bar"/&gt;
/// // &lt;/DvTrend&gt;
///
/// public sealed class TrendVm
/// {
///     public TrendBuffer Trend { get; } = new(capacity: 2000);
/// }
///
/// // polling thread (e.g. a System.Threading.Timer callback). vm = TrendVm, window = the ViewWindow showing the screen
/// var now = DateTime.Now;
/// vm.Trend.Add("temp", now, 62.4);
/// vm.Trend.AddRange("press", new[] { new TrendPoint(now.AddMilliseconds(-500), 41.0), new TrendPoint(now, 41.3) });
/// window.Invalidate();   // on WinForms: viewControl.MarkUpdated()
/// </code>
/// </example>
public sealed class TrendBuffer
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Ring> _channels = new(StringComparer.Ordinal);

    /// <summary>Creates an empty buffer.</summary>
    /// <param name="capacity">Maximum number of samples kept per channel (default 3600). Values under 2 are raised to 2. With 1-second samples, 3600 = 1 hour.</param>
    public TrendBuffer(int capacity = 3600) => Capacity = Math.Max(2, capacity);

    /// <summary>Gets the maximum number of samples kept per channel (2 or more). When full, the oldest samples are dropped first.</summary>
    public int Capacity { get; }

    /// <summary>Adds one sample to a channel, creating the channel if it does not exist. Can be called from any thread.</summary>
    /// <param name="channel">Channel name. Must match the <c>name</c> of the <c>DvTrendChannel</c> exactly, including case.</param>
    /// <param name="time">Sample time.</param>
    /// <param name="value">Sample value. <see cref="double.NaN"/> breaks the line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public void Add(string channel, DateTime time, double value)
    {
        lock (_gate)
        {
            if (!_channels.TryGetValue(channel, out var ring))
                _channels[channel] = ring = new Ring(Capacity);
            ring.Add(new TrendPoint(time, value));
        }
    }

    /// <summary>
    /// Adds several samples to a channel in order. All are added under a single lock, so a <see cref="Snapshot"/> cannot interleave.
    /// Use it to add a backlog of samples at once.
    /// </summary>
    /// <param name="channel">Channel name.</param>
    /// <param name="points">Samples to add (in time order).</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> or <paramref name="points"/> is <see langword="null"/>.</exception>
    public void AddRange(string channel, IEnumerable<TrendPoint> points)
    {
        lock (_gate)
        {
            foreach (var p in points)
                Add(channel, p.Time, p.Value);
        }
    }

    /// <summary>Clears all channels and samples. To reflect this on screen, call <c>Invalidate()</c> afterwards.</summary>
    public void Clear()
    {
        lock (_gate)
            _channels.Clear();
    }

    /// <summary>Returns a copy of the channel's current samples. The returned array is unaffected by later additions, so it can be used freely.</summary>
    /// <param name="channel">Channel name.</param>
    /// <returns>A copy in insertion order, oldest first. An empty array if the channel does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public TrendPoint[] Snapshot(string channel)
    {
        lock (_gate)
            return _channels.TryGetValue(channel, out var ring) ? ring.ToArray() : Array.Empty<TrendPoint>();
    }

    private sealed class Ring
    {
        private readonly TrendPoint[] _items;
        private int _start;
        private int _count;

        public Ring(int capacity) => _items = new TrendPoint[capacity];

        public void Add(TrendPoint p)
        {
            if (_count < _items.Length)
            {
                _items[(_start + _count) % _items.Length] = p;
                _count++;
            }
            else
            {
                _items[_start] = p;
                _start = (_start + 1) % _items.Length;
            }
        }

        public TrendPoint[] ToArray()
        {
            var a = new TrendPoint[_count];
            for (var i = 0; i < _count; i++)
                a[i] = _items[(_start + i) % _items.Length];
            return a;
        }
    }
}
