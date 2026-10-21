using System.Diagnostics;

namespace Devinno.UI.Alarms;

/// <summary>
/// The priority of an alarm. A smaller value means a higher priority. Within the same acknowledgement state, <see cref="AlarmView.Ordered"/>
/// sorts in the order <see cref="Critical"/> → <see cref="Major"/> → <see cref="Minor"/>.
/// </summary>
/// <remarks>Widgets paint each priority with the theme color tokens <c>alarm-critical</c>, <c>alarm-major</c> and <c>alarm-minor</c> (text color: <c>-fg</c>).</remarks>
public enum AlarmPriority
{
    /// <summary>Critical. The highest priority; an alarm that needs immediate action.</summary>
    Critical,
    /// <summary>Major. Needs action, but at a lower priority than Critical.</summary>
    Major,
    /// <summary>Minor. The lowest priority; an alarm that recommends checking or inspection.</summary>
    Minor
}

/// <summary>
/// Display data for one alarm. Bind a list of this type to the <c>items</c> of <c>DvAlarmBanner</c> or <c>DvAlarmList</c>.
/// </summary>
/// <remarks>
/// <para>You can build it by mapping alarms received from a server (Web API, DB) as they are (already acknowledged alarms may be included). Keeping the source data,
/// persistence and acknowledgement (ACK) are the responsibility of the app or server; when the acknowledge button is pressed, the widget only calls the VM method named in <c>onack</c> (<c>M(AlarmRow)</c> or <c>M(long)</c>)
/// and never changes this object itself.</para>
/// <para><see cref="Id"/>, <see cref="Time"/>, <see cref="Priority"/> and <see cref="Message"/> are <c>required</c>, so they must be set in the object initializer.</para>
/// </remarks>
/// <example>
/// <code>
/// var row = new AlarmRow
/// {
///     Id = 1001,
///     Time = DateTime.Now,
///     Priority = AlarmPriority.Critical,
///     Message = "취수 펌프 #2 과전류 트립",
///     Source = "PLC1"
/// };
/// </code>
/// </example>
public sealed class AlarmRow
{
    /// <summary>The number that identifies the alarm. If the <c>onack</c> method takes a <see cref="long"/>, this value is passed. Keep it unique within one list.</summary>
    public required long Id { get; init; }
    /// <summary>The time the alarm occurred. Shown in the list; within the same acknowledgement state and priority, the latest time comes first.</summary>
    public required DateTime Time { get; init; }
    /// <summary>The priority of the alarm. Determines its color and sort order.</summary>
    public required AlarmPriority Priority { get; init; }
    /// <summary>The alarm text shown on screen (displayed on one line).</summary>
    public required string Message { get; init; }
    /// <summary>The source of the alarm, such as a device or tag. <see langword="null"/> if none.</summary>
    public string? Source { get; init; }
    /// <summary>Whether the alarm has been acknowledged (ACK). An acknowledged alarm loses its acknowledge button and moves behind unacknowledged alarms in the list.</summary>
    public bool Acked { get; set; }
    /// <summary>The time the alarm was acknowledged. <see langword="null"/> before acknowledgement. <see cref="AlarmLog.Ack"/> fills this value when it acknowledges.</summary>
    public DateTime? AckTime { get; set; }
}

/// <summary>
/// <b>Optional</b> — an in-memory alarm list for HMIs and demos that run standalone without a server. It only raises alarms (<see cref="Raise"/>),
/// acknowledges them (<see cref="Ack"/>, <see cref="AckAll"/>) and manages capacity.
/// </summary>
/// <remarks>
/// <para><b>When to use</b>: keeping the source data, persistence and acknowledgement of alarms are, in principle, the responsibility of the app or server. In a server (Web API) architecture, do not use this class;
/// map the API results to a list of <see cref="AlarmRow"/> and bind it directly. This class stores nothing on disk or in a DB, so
/// the history is lost when the process ends.</para>
/// <para><b>Binding</b>: put <c>public IReadOnlyList&lt;AlarmRow&gt; Alarms =&gt; Log.Rows;</c> in the VM and connect it in markup like
/// <c>&lt;DvAlarmBanner items="{Alarms}" onack="Ack"/&gt;</c> or <c>&lt;DvAlarmList items="{Alarms}" onack="Ack"/&gt;</c>.
/// Widgets take a snapshot of the list (a copy made under the lock) at layout time, so after <see cref="Raise"/> or <see cref="Ack"/> from outside the screen (a timer, communication thread, etc.) you must call the host's <c>Invalidate()</c>
/// (<c>ViewWindow.Invalidate()</c>; in WinForms, <c>ViewControl.MarkUpdated()</c>) for the change to show. Changes made inside an <c>onack</c> or <c>onclick</c>
/// method called by a widget click are re-laid out by the screen automatically.</para>
/// <para><b>Over capacity</b>: when the item count exceeds <see cref="MaxRows"/>, alarms are removed starting from the <b>oldest acknowledged alarm</b>. Unacknowledged alarms are never removed, so
/// if unacknowledged alarms alone overflow it, the list can grow beyond <see cref="MaxRows"/>. In that case, once only, it adds a Critical alarm "Alarm buffer saturated" (<see cref="SaturationMessage"/>; alarm widgets show it in the screen language with the <c>dv.alarm.saturated</c> text,
/// source <c>"AlarmLog"</c>), writes a <see cref="System.Diagnostics.Trace"/> error, and raises <see cref="Saturated"/>.
/// After the saturation alarm is acknowledged, it notifies again the next time the list saturates. Capacity is also re-trimmed at the moment of acknowledgement, so if the list was overflowing, the alarm just acknowledged may drop out of the list immediately.</para>
/// <para><b>Threads</b>: every member uses an internal lock, so you can <see cref="Raise"/>, <see cref="Ack"/> or <see cref="AckAll"/> from any thread (including communication and polling threads) and then
/// call <c>Invalidate()</c>. At layout time the screen enumerates <see cref="Rows"/> and takes a snapshot under the lock, so it does not collide with additions or removals during drawing.
/// The snapshot holds the same <see cref="AlarmRow"/> objects rather than copies, so do not change the acknowledgement state (<see cref="AlarmRow.Acked"/>, <see cref="AlarmRow.AckTime"/>) directly;
/// always change it through <see cref="Ack"/> or <see cref="AckAll"/>. <see cref="Saturated"/> is raised after the lock is released.</para>
/// </remarks>
/// <example>
/// <code>
/// // Markup
/// // &lt;DvAlarmBanner items="{Alarms}" onack="Ack"/&gt;
/// // &lt;DvAlarmList items="{Alarms}" onack="Ack" grow="1"/&gt;
///
/// public sealed class AlarmVm
/// {
///     public AlarmLog Log { get; } = new(maxRows: 200);
///     public IReadOnlyList&lt;AlarmRow&gt; Alarms =&gt; Log.Rows;
///     public string UnAckedText =&gt; $"미확인 {Log.UnAckedCount}건";
///
///     public void Ack(AlarmRow row) =&gt; Log.Ack(row.Id);   // acknowledge button of the banner and list
///     public void AckAll() =&gt; Log.AckAll();               // acknowledge-all button (onclick)
///     public void Trip() =&gt; Log.Raise(AlarmPriority.Critical, "취수 펌프 #2 과전류 트립", "PLC1");
/// }
/// </code>
/// </example>
public sealed class AlarmLog
{
    /// <summary>The text of the Critical alarm added automatically when unacknowledged alarms alone exceed <see cref="MaxRows"/>.</summary>
    public const string SaturationMessage = "Alarm buffer saturated: acknowledge (ACK) the unacknowledged alarms";

    private readonly object _gate = new();
    private readonly List<AlarmRow> _rows = new();
    private readonly RowsView _view;
    private readonly Func<DateTime> _clock;
    private long _nextId = 1;
    private AlarmRow? _saturation;

    /// <summary>Creates an empty alarm list.</summary>
    /// <param name="maxRows">The maximum number of alarms to keep (default 500). Values below 1 are clamped to 1. See the class remarks for the overflow behavior.</param>
    /// <param name="clock">A function that returns the time used for raising and acknowledging. If <see langword="null"/>, <see cref="DateTime.Now"/> is used. Pass it to fix the time in tests.</param>
    public AlarmLog(int maxRows = 500, Func<DateTime>? clock = null)
    {
        MaxRows = Math.Max(1, maxRows);
        _clock = clock ?? (() => DateTime.Now);
        _view = new RowsView(this);
    }

    /// <summary>The maximum number of alarms to keep (1 or more). Only acknowledged alarms are removed to fit, so with many unacknowledged alarms <see cref="Rows"/> can be longer than this.</summary>
    public int MaxRows { get; }

    /// <summary>
    /// All alarms in the order they were raised (oldest first), including acknowledged ones. The on-screen order is determined by <see cref="AlarmView.Ordered"/>.
    /// </summary>
    /// <remarks>
    /// A read-only view wrapping the internal list. Each enumeration (<c>foreach</c>, LINQ) enumerates a snapshot of that moment taken under the lock, so a <see cref="Raise"/> on another thread does not break the enumeration.
    /// <c>Count</c> and the indexer each lock separately per call, so the list may change between them when used together. If you need a consistent list, copy it at once with <c>Rows.ToList()</c>.
    /// </remarks>
    public IReadOnlyList<AlarmRow> Rows => _view;

    /// <summary>The number of alarms not yet acknowledged. Counts the list under the lock on every read.</summary>
    public int UnAckedCount
    {
        get
        {
            lock (_gate)
                return CountUnAcked();
        }
    }

    /// <summary>
    /// Raised when <see cref="MaxRows"/> cannot be met even after removing all acknowledged alarms (all remaining alarms are unacknowledged). It is not raised again until the saturation alarm is acknowledged.
    /// Invoked synchronously on the thread that called <see cref="Raise"/> or <see cref="Ack"/>, after the lock is released.
    /// </summary>
    public event EventHandler? Saturated;

    /// <summary>Adds a new alarm to the end of the list and trims to capacity if it overflows.</summary>
    /// <param name="priority">The alarm priority.</param>
    /// <param name="message">The alarm text to display.</param>
    /// <param name="source">The source (device or tag name, etc.). <see langword="null"/> if none.</param>
    /// <returns>The added alarm. <see cref="AlarmRow.Id"/> is a number starting at 1 and increasing by 1, and <see cref="AlarmRow.Time"/> is the current time of the clock passed to the constructor.</returns>
    public AlarmRow Raise(AlarmPriority priority, string message, string? source = null)
    {
        AlarmRow row;
        bool saturated;
        lock (_gate)
        {
            row = Add(priority, message, source);
            saturated = Trim();
        }

        if (saturated)
            Saturated?.Invoke(this, EventArgs.Empty);
        return row;
    }

    /// <summary>
    /// Acknowledges (ACK) one alarm. Sets <see cref="AlarmRow.Acked"/> to <see langword="true"/> and <see cref="AlarmRow.AckTime"/> to the current time,
    /// then trims to capacity again. Safe to call more than once.
    /// </summary>
    /// <param name="id">The <see cref="AlarmRow.Id"/> of the alarm to acknowledge.</param>
    /// <returns><see langword="true"/> if this call acknowledged it; <see langword="false"/> if it was already acknowledged or the number is not in the list.</returns>
    public bool Ack(long id)
    {
        bool acked;
        var saturated = false;
        lock (_gate)
        {
            acked = AckCore(id);
            if (acked)
                saturated = Trim();
        }

        if (saturated)
            Saturated?.Invoke(this, EventArgs.Empty);
        return acked;
    }

    /// <summary>Acknowledges all unacknowledged alarms (including the saturation alarm). Equivalent to calling <see cref="Ack"/> on each alarm in turn.</summary>
    /// <returns>The number of alarms acknowledged by this call.</returns>
    public int AckAll()
    {
        var n = 0;
        var saturated = false;
        lock (_gate) // 한 번에 — 중간에 다른 스레드의 Raise 가 끼어들지 않는다
        {
            foreach (var r in _rows.Where(r => !r.Acked).ToList())
            {
                if (AckCore(r.Id))
                {
                    n++;
                    saturated |= Trim();
                }
            }
        }

        if (saturated)
            Saturated?.Invoke(this, EventArgs.Empty);
        return n;
    }

    // 잠금 안에서만
    private bool AckCore(long id)
    {
        var row = _rows.FirstOrDefault(r => r.Id == id);
        if (row is null || row.Acked)
            return false;

        row.AckTime = _clock();
        row.Acked = true;
        if (ReferenceEquals(row, _saturation))
            _saturation = null;
        return true;
    }

    private int CountUnAcked() => _rows.Count(r => !r.Acked);

    private AlarmRow[] Snapshot()
    {
        lock (_gate)
            return _rows.ToArray();
    }

    private AlarmRow Add(AlarmPriority priority, string message, string? source)
    {
        var row = new AlarmRow
        {
            Id = _nextId++,
            Time = _clock(),
            Priority = priority,
            Message = message,
            Source = source
        };
        _rows.Add(row);
        return row;
    }

    // 잠금 안에서만. 포화 알람을 새로 넣었으면 true(Saturated 는 호출자가 잠금 밖에서 발생)
    private bool Trim()
    {
        while (_rows.Count > MaxRows)
        {
            var oldestAcked = _rows.FirstOrDefault(r => r.Acked);
            if (oldestAcked is null)
            {
                if (_saturation is null)
                {
                    _saturation = Add(AlarmPriority.Critical, SaturationMessage, "AlarmLog");
                    Trace.TraceError($"Devinno.UI: {SaturationMessage} (MaxRows={MaxRows}, unACKed={CountUnAcked()})");
                    return true;
                }

                return false;
            }

            _rows.Remove(oldestAcked);
        }

        return false;
    }

    // Rows — 열거는 잠금 아래 스냅숏, Count·인덱서는 호출마다 잠금
    private sealed class RowsView(AlarmLog log) : IReadOnlyList<AlarmRow>
    {
        public int Count
        {
            get
            {
                lock (log._gate)
                    return log._rows.Count;
            }
        }

        public AlarmRow this[int index]
        {
            get
            {
                lock (log._gate)
                    return log._rows[index];
            }
        }

        public IEnumerator<AlarmRow> GetEnumerator() => ((IEnumerable<AlarmRow>)log.Snapshot()).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>
/// The display order rule used by alarm widgets: unacknowledged first → higher priority first (<see cref="AlarmPriority.Critical"/>) → latest time first → larger <see cref="AlarmRow.Id"/> first.
/// </summary>
/// <remarks>The row order of <c>DvAlarmList</c> and the representative alarm of <c>DvAlarmBanner</c> follow this rule. Apps can use it as is to build other screens (reports, logs, etc.) in the same order.</remarks>
public static class AlarmView
{
    /// <summary>Returns a new list of the alarms sorted in display order. The original is not changed.</summary>
    /// <param name="rows">The alarms to sort.</param>
    /// <returns>A sorted copy.</returns>
    public static IReadOnlyList<AlarmRow> Ordered(IEnumerable<AlarmRow> rows)
        => rows.OrderBy(r => r.Acked)
            .ThenBy(r => r.Priority)
            .ThenByDescending(r => r.Time)
            .ThenByDescending(r => r.Id)
            .ToList();

    /// <summary>Returns the representative alarm shown by <c>DvAlarmBanner</c>: the unacknowledged alarm with the highest priority and the latest time.</summary>
    /// <param name="rows">The alarms to search.</param>
    /// <returns>The representative alarm, or <see langword="null"/> if there are no unacknowledged alarms.</returns>
    public static AlarmRow? Top(IEnumerable<AlarmRow> rows)
        => Ordered(rows).FirstOrDefault(r => !r.Acked);
}
