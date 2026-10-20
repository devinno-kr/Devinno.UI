using Devinno.UI.Alarms;
using Xunit;

public class AlarmLogTests
{
    private static (AlarmLog log, Func<DateTime> tick) Log(int max = 10)
    {
        var t = new DateTime(2026, 10, 12, 9, 0, 0);
        DateTime Next() => t = t.AddSeconds(1);
        return (new AlarmLog(max, Next), Next);
    }

    [Fact]
    public void Raise_assigns_increasing_ids_and_times()
    {
        var (log, _) = Log();
        var a = log.Raise(AlarmPriority.Major, "A");
        var b = log.Raise(AlarmPriority.Minor, "B", "PLC1");
        Assert.True(b.Id > a.Id);
        Assert.True(b.Time > a.Time);
        Assert.Equal("PLC1", b.Source);
        Assert.Equal(2, log.UnAckedCount);
    }

    [Fact]
    public void Ack_is_idempotent_and_records_time()
    {
        var (log, _) = Log();
        var a = log.Raise(AlarmPriority.Critical, "A");
        Assert.True(log.Ack(a.Id));
        Assert.False(log.Ack(a.Id));
        Assert.False(log.Ack(999));
        Assert.True(a.Acked);
        Assert.NotNull(a.AckTime);
    }

    [Fact]
    public void AckAll_acks_every_unacked()
    {
        var (log, _) = Log();
        log.Raise(AlarmPriority.Critical, "A");
        var b = log.Raise(AlarmPriority.Minor, "B");
        log.Ack(b.Id);
        log.Raise(AlarmPriority.Major, "C");
        Assert.Equal(2, log.AckAll());
        Assert.Equal(0, log.UnAckedCount);
    }

    [Fact]
    public void Ordered_puts_unacked_first_then_priority_then_newest()
    {
        var (log, _) = Log();
        var minorOld = log.Raise(AlarmPriority.Minor, "minor-old");
        var critAcked = log.Raise(AlarmPriority.Critical, "crit-acked");
        var majorNew = log.Raise(AlarmPriority.Major, "major");
        var critNew = log.Raise(AlarmPriority.Critical, "crit-new");
        log.Ack(critAcked.Id);

        var order = AlarmView.Ordered(log.Rows).Select(r => r.Message).ToList();
        Assert.Equal(new[] { "crit-new", "major", "minor-old", "crit-acked" }, order);
        Assert.Same(critNew, AlarmView.Top(log.Rows));
        Assert.NotNull(minorOld);
        Assert.NotNull(majorNew);
    }

    [Fact]
    public void Top_is_null_when_everything_is_acked()
    {
        var (log, _) = Log();
        log.Raise(AlarmPriority.Major, "A");
        log.AckAll();
        Assert.Null(AlarmView.Top(log.Rows));
    }

    [Fact]
    public void Over_capacity_drops_oldest_acked_first()
    {
        var (log, _) = Log(max: 3);
        var a = log.Raise(AlarmPriority.Minor, "a");
        var b = log.Raise(AlarmPriority.Minor, "b");
        var c = log.Raise(AlarmPriority.Minor, "c");
        log.Ack(a.Id);
        log.Ack(c.Id);

        log.Raise(AlarmPriority.Major, "d");
        Assert.Equal(new[] { "b", "c", "d" }, log.Rows.Select(r => r.Message));
        Assert.NotNull(b);
    }

    [Fact]
    public void Unacked_are_kept_over_capacity_and_saturation_alarm_raised_once()
    {
        var (log, _) = Log(max: 2);
        var saturated = 0;
        log.Saturated += (_, _) => saturated++;

        log.Raise(AlarmPriority.Minor, "a");
        log.Raise(AlarmPriority.Minor, "b");
        log.Raise(AlarmPriority.Minor, "c");
        log.Raise(AlarmPriority.Minor, "d");

        Assert.Equal(1, saturated);
        Assert.Equal(4, log.Rows.Count(r => r.Message.Length == 1)); // unACKed 는 하나도 버려지지 않음
        var sat = Assert.Single(log.Rows, r => r.Message == AlarmLog.SaturationMessage);
        Assert.Equal(AlarmPriority.Critical, sat.Priority);
        Assert.Same(sat, AlarmView.Top(log.Rows));
    }

    [Fact]
    public void Acking_after_saturation_trims_back_and_allows_a_new_saturation_alarm_later()
    {
        var (log, _) = Log(max: 2);
        var saturated = 0;
        log.Saturated += (_, _) => saturated++;
        log.Raise(AlarmPriority.Minor, "a");
        log.Raise(AlarmPriority.Minor, "b");
        log.Raise(AlarmPriority.Minor, "c"); // 포화 1

        log.AckAll();
        Assert.Equal(2, log.Rows.Count);

        log.Raise(AlarmPriority.Minor, "x");
        log.Raise(AlarmPriority.Minor, "y");
        log.Raise(AlarmPriority.Minor, "z"); // acked 소진 → 포화 2
        Assert.Equal(2, saturated);
    }

    [Fact]
    public void Rows_enumeration_is_a_snapshot_so_raising_meanwhile_does_not_throw()
    {
        var (log, _) = Log(max: 100);
        log.Raise(AlarmPriority.Minor, "a");
        log.Raise(AlarmPriority.Minor, "b");

        var seen = 0;
        foreach (var _ in log.Rows)
        {
            log.Raise(AlarmPriority.Major, "during");
            seen++;
        }

        Assert.Equal(2, seen);
        Assert.Equal(4, log.Rows.Count);
    }

    [Fact]
    public void Raise_and_ack_from_other_threads_while_rendering_is_safe()
    {
        var vm = new ThreadVm();
        var surface = new Devinno.UI.DvSurface(
            "<DvScreen><DvAlarmBanner items=\"{Alarms}\" onack=\"Ack\"/><DvAlarmList items=\"{Alarms}\" onack=\"Ack\" style=\"height:200\"/></DvScreen>", vm);
        using var bmp = new SkiaSharp.SKBitmap(600, 400);
        using var canvas = new SkiaSharp.SKCanvas(bmp);

        var stop = false;
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var writers = Enumerable.Range(0, 4).Select(t => new Thread(() =>
        {
            try
            {
                var i = 0;
                while (!Volatile.Read(ref stop))
                {
                    var row = vm.Log.Raise((AlarmPriority)(i % 3), $"T{t}-{i}");
                    if (i % 2 == 0)
                        vm.Log.Ack(row.Id);
                    if (i % 50 == 0)
                        vm.Log.AckAll();
                    _ = vm.Log.UnAckedCount;
                    i++;
                }
            }
            catch (Exception e)
            {
                errors.Enqueue(e);
            }
        })).ToList();
        writers.ForEach(w => w.Start());

        try
        {
            var until = Environment.TickCount64 + 700;
            while (Environment.TickCount64 < until)
            {
                surface.Invalidate();
                surface.RenderTo(canvas, 600, 400, Environment.TickCount64);
                surface.PointerDown(new SkiaSharp.SKPoint(300, 20)); // 배너 ACK 클릭 경로
                surface.PointerUp(new SkiaSharp.SKPoint(300, 20));
            }
        }
        finally
        {
            Volatile.Write(ref stop, true);
            writers.ForEach(w => w.Join());
        }

        Assert.Empty(errors);
        Assert.True(vm.Log.Rows.Count >= 1);
        Assert.True(vm.Log.Rows.Count <= vm.Log.MaxRows + 1 || vm.Log.Rows.All(r => !r.Acked));
    }

    public sealed class ThreadVm
    {
        public AlarmLog Log { get; } = new(maxRows: 50);
        public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
        public void Ack(AlarmRow row) => Log.Ack(row.Id);
    }
}
