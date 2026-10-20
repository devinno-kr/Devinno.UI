using Devinno.UI;
using Devinno.UI.Alarms;
using SkiaSharp;
using Xunit;

// 호스트가 "바뀐 게 없으면 그리지 않기" 를 할 수 있도록 DvSurface 가 알려 주는 신호
public class RenderLoopTests
{
    public sealed class Vm
    {
        public double Level { get; set; } = 40;
        public AlarmLog Log { get; } = new();
        public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
        public void Noop() { }
    }

    private static void Render(DvSurface s, int w = 300, int h = 300)
    {
        using var bmp = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, w, h, 1000);
    }

    [Fact]
    public void New_surface_needs_render_and_rendering_clears_it()
    {
        var s = new DvSurface("<DvScreen><DvLabel text=\"a\"/></DvScreen>", null);
        Assert.True(s.NeedsRender);
        Render(s);
        Assert.False(s.NeedsRender);
    }

    [Fact]
    public void Invalidate_sets_needs_render()
    {
        var s = new DvSurface("<DvScreen><DvLabel text=\"a\"/></DvScreen>", null);
        Render(s);
        s.Invalidate();
        Assert.True(s.NeedsRender);
    }

    [Fact]
    public void Hover_change_needs_render_only_when_a_hover_style_is_involved()
    {
        var s = new DvSurface(
            "<DvScreen style=\"padding:0; display:flex; flex-direction:column\">" +
            "<DvBox style=\"width:100; height:50; background:#111\" hover-style=\"background:#222\"/>" +
            "<DvBox style=\"width:100; height:50; background:#111\"/>" +
            "<DvBox style=\"width:100; height:50; background:#111\"/></DvScreen>", null);
        Render(s);

        s.PointerMove(new SKPoint(10, 125)); // 세 번째 — 평범한 박스
        Assert.False(s.NeedsRender);
        s.PointerMove(new SKPoint(10, 75)); // 두 번째 — 평범한 박스
        Assert.False(s.NeedsRender);

        s.PointerMove(new SKPoint(10, 25)); // hover-style 박스로 진입
        Assert.True(s.NeedsRender);
        Render(s);

        s.PointerMove(new SKPoint(20, 30)); // 같은 박스 안 이동
        Assert.False(s.NeedsRender);

        s.PointerMove(new SKPoint(10, 75)); // hover-style 박스에서 이탈
        Assert.True(s.NeedsRender);
    }

    [Fact]
    public void Press_needs_render_when_box_has_press_style()
    {
        var s = new DvSurface(
            "<DvScreen style=\"padding:0\"><DvBox style=\"width:100; height:50\" press-style=\"background:#222\" onclick=\"Noop\"/></DvScreen>",
            new Vm());
        Render(s);
        s.PointerDown(new SKPoint(10, 10));
        Assert.True(s.NeedsRender);
        Render(s);
        s.PointerUp(new SKPoint(10, 10));
        Assert.True(s.NeedsRender);
    }

    [Fact]
    public void Knob_drag_move_needs_render()
    {
        var s = new DvSurface(
            "<DvScreen style=\"padding:0\"><DvKnob value=\"{Bind Data.Level}\" style=\"width:100; height:100\"/></DvScreen>",
            new Vm());
        Render(s);
        s.PointerDown(new SKPoint(50, 10));
        s.PointerMove(new SKPoint(90, 50));
        Assert.True(s.NeedsRender);
    }

    [Fact]
    public void No_animation_reports_null_due()
    {
        var s = new DvSurface("<DvScreen><DvLamp on=\"true\"/><DvLamp on=\"true\" blink=\"false\"/></DvScreen>", null);
        Render(s);
        Assert.Null(s.NextAnimationDue(1234));
    }

    [Fact]
    public void Blinking_lamp_reports_next_phase_flip()
    {
        var s = new DvSurface("<DvScreen><DvLamp on=\"true\" blink=\"true\" blink-interval=\"400\"/></DvScreen>", null);
        Render(s);
        Assert.Equal(1200, s.NextAnimationDue(1000));
        Assert.Equal(1200, s.NextAnimationDue(1199));
        Assert.Equal(1600, s.NextAnimationDue(1200));
    }

    [Fact]
    public void Off_lamp_does_not_animate_even_with_blink()
    {
        var s = new DvSurface("<DvScreen><DvLamp on=\"false\" blink=\"true\"/></DvScreen>", null);
        Render(s);
        Assert.Null(s.NextAnimationDue(0));
    }

    [Fact]
    public void Unacked_alarm_animates_acked_does_not()
    {
        var vm = new Vm();
        var row = vm.Log.Raise(AlarmPriority.Major, "x");
        var s = new DvSurface(
            "<DvScreen><DvAlarmBanner items=\"{Alarms}\"/><DvAlarmList items=\"{Alarms}\" style=\"height:100\"/></DvScreen>", vm);
        Render(s);
        Assert.Equal(1000, s.NextAnimationDue(700)); // 500 ms 공통 위상

        vm.Log.Ack(row.Id);
        s.Invalidate();
        Render(s);
        Assert.Null(s.NextAnimationDue(700));
    }

    [Fact]
    public void App_root_reflects_page_surface()
    {
        var page = new MarkupPage("<DvScreen><DvLamp on=\"true\" blink=\"true\" blink-interval=\"300\"/></DvScreen>");
        var (_, shell) = TestApp.WithLeft(page, 50, 300, 300);
        Render(shell);
        Assert.False(shell.NeedsRender);

        page.Surface!.Invalidate();
        Assert.True(shell.NeedsRender);
        Render(shell);
        Assert.False(shell.NeedsRender);

        Assert.Equal(1200, shell.NextAnimationDue(1000));
    }
}
