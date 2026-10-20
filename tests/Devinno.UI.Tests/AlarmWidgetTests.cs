using Devinno.UI;
using Devinno.UI.Alarms;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

public class AlarmWidgetTests
{
    public sealed class Vm
    {
        private DateTime _t = new(2026, 10, 12, 9, 0, 0);

        public Vm(int max = 50) => Log = new AlarmLog(max, () => _t = _t.AddSeconds(1));

        public AlarmLog Log { get; }
        public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
        public string Comm { get; set; } = "";
        public List<long> AckedIds { get; } = new();

        public void AckAlarm(AlarmRow row)
        {
            AckedIds.Add(row.Id);
            Log.Ack(row.Id);
        }

        public void AckById(long id) => Log.Ack(id);
    }

    private static LayoutBox Box(string markup, object vm, float w = 600, float h = 400)
        => Flatten(new LayoutEngine().Layout(MarkupParser.Parse(markup), vm, w, h))
            .First(b => b.Node.Tag.StartsWith("DvAlarm", StringComparison.Ordinal));

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static SKBitmap Render(string markup, object vm, int w, int h, long ts = 0, DvTheme? theme = null)
    {
        var box = new LayoutEngine(theme).Layout(MarkupParser.Parse(markup), vm, w, h);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        new Painter(theme).Paint(canvas, box, vm, null, ts);
        return bmp;
    }

    // ── 배너 레이아웃
    [Fact]
    public void Banner_height_is_strip_plus_bottom_only_when_comm_alarm_present()
    {
        var vm = new Vm();
        Assert.Equal(40, Box("<DvScreen><DvAlarmBanner items=\"{Alarms}\" comm-alarm=\"{Comm}\"/></DvScreen>", vm).Rect.Height);
        vm.Comm = "PLC1 응답 없음";
        Assert.Equal(64, Box("<DvScreen><DvAlarmBanner items=\"{Alarms}\" comm-alarm=\"{Comm}\"/></DvScreen>", vm).Rect.Height);
    }

    [Fact]
    public void Fixed_height_split_keeps_bottom_area_minimum()
    {
        var (strip, bottom) = AlarmBannerLayout.Split(SKRect.Create(0, 0, 400, 40), hasComm: true);
        Assert.Equal(16, strip!.Value.Height);
        Assert.Equal(24, bottom.Height);

        var (none, all) = AlarmBannerLayout.Split(SKRect.Create(0, 0, 400, 20), hasComm: true);
        Assert.Null(none);
        Assert.Equal(20, all.Height);
    }

    // ── 배너 렌더
    private const string Banner =
        "<DvAlarmBanner items=\"{Alarms}\" comm-alarm=\"{Comm}\" onack=\"AckAlarm\" style=\"width:400; height:40\"/>";

    [Fact]
    public void Banner_without_unacked_shows_calm_state()
    {
        using var bmp = Render(Banner, new Vm(), 400, 40);
        var bg = bmp.GetPixel(300, 4);
        Assert.True(bg.Green > bg.Red && bg.Green > 200, $"bg={bg}");
    }

    [Fact]
    public void Banner_shows_top_priority_color_and_blinks()
    {
        var vm = new Vm();
        vm.Log.Raise(AlarmPriority.Minor, "minor");
        vm.Log.Raise(AlarmPriority.Critical, "critical");

        using var on = Render(Banner, vm, 400, 40, ts: 0);
        using var off = Render(Banner, vm, 400, 40, ts: 600);
        var a = on.GetPixel(300, 4);
        var b = off.GetPixel(300, 4);
        Assert.True(a.Red > 180 && a.Green < 80, $"on={a}");
        Assert.True(b.Red < a.Red - 40, $"off={b}");
    }

    [Fact]
    public void Comm_alarm_strip_appears_on_top_without_hiding_unacked_area()
    {
        var vm = new Vm { Comm = "PLC1" };
        vm.Log.Raise(AlarmPriority.Critical, "critical");
        using var bmp = Render(
            "<DvAlarmBanner items=\"{Alarms}\" comm-alarm=\"{Comm}\" style=\"width:400; height:64\"/>", vm, 400, 64);
        var strip = bmp.GetPixel(300, 4);
        var bottom = bmp.GetPixel(300, 50);
        Assert.True(strip.Red > 0x60 && strip.Blue > 0x90 && strip.Green < 0x40, $"strip={strip}");
        Assert.True(bottom.Red > 180 && bottom.Green < 80, $"bottom={bottom}");
    }

    // ── 입력
    [Fact]
    public void Clicking_banner_acks_top_priority_alarm()
    {
        var vm = new Vm();
        vm.Log.Raise(AlarmPriority.Minor, "minor");
        var crit = vm.Log.Raise(AlarmPriority.Critical, "critical");
        var s = new DvSurface($"<DvScreen>{Banner}</DvScreen>", vm);
        s.Layout(600, 200);

        s.PointerDown(new SKPoint(200, 20));
        s.PointerUp(new SKPoint(200, 20));
        Assert.Equal(new[] { crit.Id }, vm.AckedIds);
    }

    private const string List =
        "<DvScreen><DvAlarmList items=\"{Alarms}\" onack=\"AckAlarm\" style=\"width:600; height:300\"/></DvScreen>";

    [Fact]
    public void List_ack_button_acks_the_row_shown_there_in_display_order()
    {
        var vm = new Vm();
        var minor = vm.Log.Raise(AlarmPriority.Minor, "minor");
        var crit = vm.Log.Raise(AlarmPriority.Critical, "critical");
        var s = new DvSurface(List, vm);
        s.Layout(600, 400);

        var layout = AlarmListLayout.Compute(SKRect.Create(0, 0, 600, 300), vm.Alarms, 36);
        Assert.Same(crit, layout.Rows[0].Row);
        var second = layout.Rows[1].AckButton!.Value;

        s.PointerDown(new SKPoint(second.MidX, second.MidY));
        s.PointerUp(new SKPoint(second.MidX, second.MidY));
        Assert.Equal(new[] { minor.Id }, vm.AckedIds);
        Assert.False(crit.Acked);
    }

    [Fact]
    public void Clicking_message_area_or_acked_row_does_nothing()
    {
        var vm = new Vm();
        var a = vm.Log.Raise(AlarmPriority.Major, "a");
        vm.Log.Ack(a.Id);
        vm.Log.Raise(AlarmPriority.Minor, "b");
        var s = new DvSurface(List, vm);
        s.Layout(600, 400);

        var layout = AlarmListLayout.Compute(SKRect.Create(0, 0, 600, 300), vm.Alarms, 36);
        var ackedRow = layout.Rows.Single(r => r.Row.Acked);
        Assert.Null(ackedRow.AckButton);

        foreach (var p in new[] { new SKPoint(300, layout.Rows[0].Rect.MidY), new SKPoint(560, ackedRow.Rect.MidY) })
        {
            s.PointerDown(p);
            s.PointerUp(p);
        }

        Assert.Empty(vm.AckedIds);
    }

    [Fact]
    public void Onack_can_take_alarm_id()
    {
        var vm = new Vm();
        var a = vm.Log.Raise(AlarmPriority.Major, "a");
        var s = new DvSurface(
            "<DvScreen><DvAlarmBanner items=\"{Alarms}\" onack=\"AckById\" style=\"width:400; height:40\"/></DvScreen>", vm);
        s.Layout(600, 200);
        s.PointerDown(new SKPoint(100, 20));
        s.PointerUp(new SKPoint(100, 20));
        Assert.True(a.Acked);
    }

    [Fact]
    public void After_ack_relayout_moves_row_below_unacked()
    {
        var vm = new Vm();
        var a = vm.Log.Raise(AlarmPriority.Critical, "a");
        vm.Log.Raise(AlarmPriority.Minor, "b");
        var s = new DvSurface(List, vm);
        s.Layout(600, 400);
        var btn = AlarmListLayout.Compute(SKRect.Create(0, 0, 600, 300), vm.Alarms, 36).Rows[0].AckButton!.Value;
        s.PointerDown(new SKPoint(btn.MidX, btn.MidY));
        s.PointerUp(new SKPoint(btn.MidX, btn.MidY));

        var after = AlarmListLayout.Compute(SKRect.Create(0, 0, 600, 300), vm.Alarms, 36);
        Assert.Equal("b", after.Rows[0].Row.Message);
        Assert.Same(a, after.Rows[1].Row);
    }
}

public class AlarmAckVisibilityTests
{
    public sealed class Vm
    {
        public List<AlarmRow> Alarms { get; } = new()
        {
            new AlarmRow { Id = 1, Time = new DateTime(2026, 10, 12, 9, 0, 0), Priority = AlarmPriority.Critical, Message = "unacked" },
            new AlarmRow
            {
                Id = 2, Time = new DateTime(2026, 10, 12, 8, 0, 0), Priority = AlarmPriority.Major, Message = "from api, acked",
                Acked = true, AckTime = new DateTime(2026, 10, 12, 8, 5, 0)
            }
        };

        public bool CanAck { get; set; }
        public List<long> Acked { get; } = new();
        public void Ack(long id) => Acked.Add(id);
    }

    private static Devinno.UI.Layout.LayoutBox Box(string markup, object vm)
        => new Devinno.UI.Layout.LayoutEngine().Layout(Devinno.UI.Markup.MarkupParser.Parse(markup), vm, 600, 400).Children[0];

    [Fact]
    public void Rows_from_an_api_can_be_created_already_acked()
    {
        var row = new Vm().Alarms[1];
        Assert.True(row.Acked);
        Assert.Null(AlarmView.Top(new[] { row }));
    }

    [Theory]
    [InlineData("onack=\"Ack\"", true)]                       // onack 있으면 기본 표시
    [InlineData("", false)]                                   // onack 없으면 기본 숨김
    [InlineData("onack=\"Ack\" show-ack=\"false\"", false)]   // 명시적으로 숨김
    [InlineData("onack=\"Ack\" show-ack=\"{CanAck}\"", false)] // 바인딩(false)
    public void Show_ack_controls_ack_column(string attrs, bool expected)
    {
        var box = Box($"<DvScreen><DvAlarmList items=\"{{Alarms}}\" {attrs} style=\"width:600; height:300\"/></DvScreen>", new Vm());
        Assert.Equal(expected, box.AlarmShowAck);
        var layout = AlarmListLayout.Compute(box.Rect, box.AlarmItems, box.AlarmRowHeight, box.AlarmShowAck);
        Assert.Equal(expected, layout.Rows.Any(r => r.AckButton is not null));
    }

    [Fact]
    public void Hidden_ack_ignores_clicks_on_list_and_banner()
    {
        var vm = new Vm();
        var s = new DvSurface(
            "<DvScreen><DvAlarmBanner items=\"{Alarms}\" onack=\"Ack\" show-ack=\"{CanAck}\" style=\"width:600; height:40\"/>" +
            "<DvAlarmList items=\"{Alarms}\" onack=\"Ack\" show-ack=\"{CanAck}\" style=\"width:600; height:300\"/></DvScreen>", vm);
        s.Layout(600, 400);

        foreach (var p in new[] { new SKPoint(300, 20), new SKPoint(560, 40 + 30 + 18) })
        {
            s.PointerDown(p);
            s.PointerUp(p);
        }

        Assert.Empty(vm.Acked);

        vm.CanAck = true;
        s.Invalidate();
        s.Layout(600, 400);
        s.PointerDown(new SKPoint(300, 20));
        s.PointerUp(new SKPoint(300, 20));
        Assert.Equal(new long[] { 1 }, vm.Acked);
    }
}
