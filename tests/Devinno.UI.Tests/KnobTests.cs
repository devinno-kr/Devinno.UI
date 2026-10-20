using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

public class KnobTests
{
    public sealed class Vm
    {
        public double Level { get; set; } = 40;
        public int Count { get; set; } = 10;
        public bool Allow { get; set; } = true;
        public double? LastProposed;

        public bool Gate(double proposed)
        {
            LastProposed = proposed;
            return Allow;
        }
    }

    private static LayoutBox Lay(string markup, object? vm = null)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), vm, 300, 300);

    private static LayoutBox Knob(LayoutBox root)
        => Flatten(root).First(b => b.Node.Tag == "DvKnob");

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    // ── Task 1: 파싱
    [Fact]
    public void Parses_value_range_step_unit_and_binding()
    {
        var k = Knob(Lay("<DvScreen style=\"align-items:start\"><DvKnob value=\"{Bind Data.Level}\" min=\"10\" max=\"90\" step=\"5\" unit=\"%\"/></DvScreen>",
            new Vm()));
        Assert.Equal(40, k.KnobValue);
        Assert.Equal(10, k.KnobMin);
        Assert.Equal(90, k.KnobMax);
        Assert.Equal(5, k.KnobStep);
        Assert.Equal("%", k.KnobUnit);
        Assert.Equal("{Bind Data.Level}", k.ValueBindExpr);
    }

    [Fact]
    public void Defaults_to_0_100_step_1_and_96_square()
    {
        var k = Knob(Lay("<DvScreen style=\"align-items:start\"><DvKnob value=\"30\"/></DvScreen>"));
        Assert.Equal((0f, 100f, 1f), (k.KnobMin, k.KnobMax, k.KnobStep));
        Assert.Equal(96, k.Rect.Width);
        Assert.Equal(96, k.Rect.Height);
    }

    // ── Task 2: 렌더
    private static SKBitmap Render(string markup, DvTheme? theme = null)
    {
        var box = new LayoutEngine(theme).Layout(MarkupParser.Parse(markup), null, 120, 120);
        var bmp = new SKBitmap(120, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        new Painter(theme).Paint(canvas, box, null);
        return bmp;
    }

    [Fact]
    public void Value_arc_fills_top_of_dial_only_when_value_passes_midpoint()
    {
        const string fmt = "<DvKnob value=\"{0}\" style=\"width:120; height:120\"/>";
        using var low = Render(string.Format(fmt, 0));
        using var high = Render(string.Format(fmt, 100));

        // 270° 호의 정상단(12시) = 중간값 지점. 호 반지름 = 60-8 = 52 → (60, 8)
        var lowTop = low.GetPixel(60, 8);
        var highTop = high.GetPixel(60, 8);
        Assert.True(highTop.Blue > 180 && highTop.Red < 120, $"high={highTop}");
        Assert.False(lowTop.Blue > 180 && lowTop.Red < 120, $"low={lowTop}");
    }

    [Fact]
    public void Face_follows_dark_theme()
    {
        using var bmp = Render("<DvKnob value=\"50\" style=\"width:120; height:120\"/>", DvTheme.Dark);
        var face = bmp.GetPixel(40, 45); // 면 좌상(텍스트·12시 지시선 피한 지점)
        Assert.True(face.Red < 90 && face.Green < 90, $"face={face}");
    }

    // ── Task 3: 회전 드래그 입력 — 100×100 노브, 중심 (50,50). 270° = 전체 범위(0~100)
    private const string BindKnob =
        "<DvScreen style=\"align-items:start\"><DvKnob value=\"{Bind Data.Level}\" style=\"width:100; height:100\" /></DvScreen>";

    private static readonly SKPoint Top = new(50, 10), Right = new(90, 50), Bottom = new(50, 90), Left = new(10, 50);

    private static DvSurface Surface(string markup, object vm)
    {
        var s = new DvSurface(markup, vm);
        s.Layout(300, 300);
        return s;
    }

    /// <summary>첫 점에서 잡고 나머지 점을 차례로 지나며 돌린 뒤 마지막 점에서 놓는다.</summary>
    private static void Rotate(DvSurface s, params SKPoint[] path)
    {
        s.PointerDown(path[0]);
        foreach (var p in path.Skip(1))
            s.PointerMove(p);
        s.PointerUp(path[^1]);
    }

    [Fact]
    public void Rotating_clockwise_increases_by_angle_and_commits_on_pointer_up()
    {
        var vm = new Vm();
        var s = Surface(BindKnob, vm);

        Rotate(s, Top, Right); // +90° → +33.3 → 73
        Assert.Equal(73, vm.Level);
    }

    [Fact]
    public void Rotating_counter_clockwise_decreases()
    {
        var vm = new Vm();
        var s = Surface(BindKnob, vm);

        Rotate(s, Top, Left); // -90° → 40-33.3 → 7
        Assert.Equal(7, vm.Level);
    }

    [Fact]
    public void Grab_point_does_not_make_value_jump()
    {
        var vm = new Vm();
        var s = Surface(BindKnob, vm);

        // 지시선(40 → 약 243°)과 무관한 오른쪽을 잡아도 움직인 각도만큼만 변한다
        Rotate(s, Right, Bottom); // +90°
        Assert.Equal(73, vm.Level);
    }

    [Fact]
    public void Vm_is_not_touched_until_pointer_up()
    {
        var vm = new Vm();
        var s = Surface(BindKnob, vm);

        s.PointerDown(Top);
        s.PointerMove(Right);
        Assert.Equal(40, vm.Level);

        s.PointerUp(Right);
        Assert.Equal(73, vm.Level);
    }

    [Fact]
    public void Drag_survives_relayout_from_invalidate()
    {
        // 폴링 타이머가 드래그 중 Invalidate 해도 노브가 멈추지 않아야 한다(레이아웃 재빌드 후 새 박스로 이어짐)
        var vm = new Vm();
        var s = Surface(BindKnob, vm);

        s.PointerDown(Top);
        s.PointerMove(Right); // 미리보기 73
        s.Invalidate();
        s.Layout(300, 300);
        Assert.Equal(73, Knob(s.Root!).KnobValue);

        s.PointerMove(Bottom); // +90° 더 → 106.7 → clamp 100
        Assert.Equal(100, Knob(s.Root!).KnobValue);
        Assert.Equal(40, vm.Level);

        s.PointerUp(Bottom);
        Assert.Equal(100, vm.Level);
    }

    [Fact]
    public void Movement_below_8px_is_ignored()
    {
        var vm = new Vm();
        var s = Surface(BindKnob, vm);

        Rotate(s, Top, new SKPoint(55, 11));
        Assert.Equal(40, vm.Level);
    }

    [Fact]
    public void Overshoot_past_max_is_remembered_so_grip_does_not_slip()
    {
        var vm = new Vm();
        var s = Surface(BindKnob, vm);

        // 한 바퀴(+360° = +133) → 100 에서 멈춤(초과 +60 은 기억), 반대로 90°(-33.3) → 여전히 초과 구간 → 100
        Rotate(s, Top, Right, Bottom, Left, Top, Left);
        Assert.Equal(100, vm.Level);
    }

    [Fact]
    public void Overshoot_past_min_must_be_unwound_before_value_rises()
    {
        var knob = Knob(Lay(BindKnob, new Vm { Level = 0 }));
        var drag = new Devinno.UI.Interaction.KnobDrag(knob, Top);

        drag.Move(Left);                  // 0 에서 반시계 90° 더 → 0 유지
        Assert.Equal(0, drag.Value);
        drag.Move(new SKPoint(22, 22));   // 좌상(-135°): 시계 +45° 되감기 → 아직 초과 45° 남아 0
        Assert.Equal(0, drag.Value);
        drag.Move(new SKPoint(78, 22));   // 우상(-45°): 시계 +90° → 초과 45° 소진 후 45° 만큼 → 16.7 → 17
        Assert.Equal(17, drag.Value);
    }

    [Fact]
    public void Overshoot_is_capped_at_one_turn_so_unwinding_never_exceeds_a_turn()
    {
        var knob = Knob(Lay(BindKnob, new Vm()));
        var drag = new Devinno.UI.Interaction.KnobDrag(knob, Top);

        // 40(=108°)에서 시계 540° → 끝(270°)을 378° 초과 → 한 바퀴 넘은 360° 는 버리고 18° 만 기억
        foreach (var p in new[] { Right, Bottom, Left, Top, Right, Bottom })
            drag.Move(p);
        Assert.Equal(100, drag.Value);

        // 반시계 45° → 초과 18° 소진 후 27° 만큼 감소 → 90
        drag.Move(new SKPoint(78, 78));
        Assert.Equal(90, drag.Value);
    }

    [Fact]
    public void Value_is_rounded_to_step()
    {
        var vm = new Vm();
        var s = Surface(
            "<DvScreen style=\"align-items:start\"><DvKnob value=\"{Bind Data.Level}\" step=\"5\" style=\"width:100; height:100\"/></DvScreen>", vm);

        Rotate(s, Top, Right); // 73.3 → 75
        Assert.Equal(75, vm.Level);
    }

    [Fact]
    public void Decimal_step_commits_exact_decimal_value()
    {
        var vm = new Vm { Level = 0 };
        var s = Surface(
            "<DvScreen style=\"align-items:start\"><DvKnob value=\"{Bind Data.Level}\" min=\"0\" max=\"10\" step=\"0.1\" style=\"width:100; height:100\"/></DvScreen>",
            vm);

        Rotate(s, Top, Right); // +3.333 → 3.3
        Assert.Equal(3.3, vm.Level);
    }

    [Fact]
    public void Center_dead_zone_is_ignored()
    {
        var vm = new Vm();
        var s = Surface(BindKnob, vm);

        Rotate(s, new SKPoint(50, 38), new SKPoint(52, 62)); // 중심을 가로질러 반대편 — 각도 무의미
        Assert.Equal(40, vm.Level);
    }

    [Fact]
    public void Int_property_receives_converted_value()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"align-items:start\"><DvKnob value=\"{Bind Data.Count}\" style=\"width:100; height:100\"/></DvScreen>", vm);
        Rotate(s, Top, Right);
        Assert.Equal(43, vm.Count);
    }

    [Fact]
    public void Before_commit_gate_can_reject()
    {
        var vm = new Vm { Allow = false };
        var s = Surface(
            "<DvScreen style=\"align-items:start\"><DvKnob value=\"{Bind Data.Level}\" onbeforecommit=\"Gate\" style=\"width:100; height:100\"/></DvScreen>", vm);

        Rotate(s, Top, Right);
        Assert.Equal(73, vm.LastProposed);
        Assert.Equal(40, vm.Level);
    }

    [Fact]
    public void Literal_or_disabled_knob_does_not_commit()
    {
        var vm = new Vm();
        Rotate(Surface("<DvScreen style=\"align-items:start\"><DvKnob value=\"40\" style=\"width:100; height:100\"/></DvScreen>", vm), Top, Right);
        Rotate(Surface(
            "<DvScreen style=\"align-items:start\"><DvKnob value=\"{Bind Data.Level}\" disabled=\"true\" style=\"width:100; height:100\"/></DvScreen>", vm),
            Top, Right);

        Assert.Equal(40, vm.Level);
    }

    [Fact]
    public void Knob_inside_component_commits_through_two_way_param()
    {
        var reg = new Devinno.UI.Components.ComponentRegistry();
        reg.Register("<DvComponent name=\"Setpoint\"><DvParam name=\"v\"/>" +
                     "<DvBox><DvKnob value=\"{v}\" style=\"width:100; height:100\"/></DvBox></DvComponent>");
        var vm = new Vm();
        var s = new DvSurface("<DvScreen style=\"align-items:start\"><Setpoint v=\"{Bind Data.Level}\"/></DvScreen>", vm, reg);
        s.Layout(300, 300);

        Rotate(s, Top, Right);
        Assert.Equal(73, vm.Level);
    }
}
