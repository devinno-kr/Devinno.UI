using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Tests.StaticData;
using SkiaSharp;
using Xunit;

// {Static A.ABC} — 형식의 public static 속성 읽기·쓰기(형식·끼워 넣기·중첩·반복 항목·컴포넌트 안), 짧은 이름·네임스페이스 이름,
// 못 찾음·모호함·setter 없음 = 빈 값 + 경고, 쓴 값은 화면 VM 에 "Static A.ABC" 로 알린다
public class StaticBindingTests
{
    private const float W = 400, H = 300;

    public sealed class Vm : IDvValueCommitted
    {
        public List<string> Rows { get; } = ["a", "b"];
        public List<(string Path, object? Value)> Seen { get; } = new();
        public void OnValueCommitted(string path, object? value) => Seen.Add((path, value));
    }

    private sealed class Capture : TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && message.Contains("static binding"))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message) => WriteLine(message);
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:0; gap:0\">{body}</DvScreen>", vm) { UseNativeEditor = true };
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static List<string?> Texts(DvSurface s) => Flatten(s.Root!).Where(b => b.Node.Tag == "DvLabel").Select(b => b.ResolvedText).ToList();

    private static void Tap(DvSurface s, LayoutBox b)
    {
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    [Fact]
    public void Reads_a_static_property_with_format_and_interpolation()
    {
        PlantState.Mode = "AUTO";
        PlantState.Temp = 24.56;
        var (s, _) = Start("""
            <DvLabel text="{Static PlantState.Mode}"/>
            <DvLabel text="{Static PlantState.Temp:0.0}"/>
            <DvLabel text="T {Static PlantState.Temp:0.0} ℃ / {Static PlantState.Mode}"/>
            """);
        Assert.Equal(["AUTO", "24.6", "T 24.6 ℃ / AUTO"], Texts(s));
    }

    [Fact]
    public void A_rebuild_reads_the_new_static_value()
    {
        PlantState.Mode = "AUTO";
        var (s, _) = Start("<DvLabel text=\"{Static PlantState.Mode}\"/>");
        PlantState.Mode = "MANUAL";
        s.Invalidate();
        s.Layout(W, H);
        Assert.Equal(["MANUAL"], Texts(s));
    }

    [Fact]
    public void Nested_path_reads_and_writes_through_the_static_object()
    {
        PlantState.Pump = new PumpState { Speed = 1200 };
        var (s, vm) = Start("""
            <DvLabel text="{Static PlantState.Pump.Speed}"/>
            <DvInput value="{Bind Static PlantState.Pump.Speed}" style="height:40"/>
            """);
        Assert.Equal(["1200"], Texts(s));
        Tap(s, Flatten(s.Root!).First(b => b.Node.Tag == "DvInput"));
        s.SetEditText(s.Edit.SessionId, "1500");
        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.Equal(1500, PlantState.Pump.Speed);
        Assert.Equal([("Static PlantState.Pump.Speed", (object?)1500.0)], vm.Seen);
    }

    [Fact]
    public void Two_way_writes_the_static_property_and_tells_the_screen_vm()
    {
        PlantState.Auto = false;
        var (s, vm) = Start("<DvToggle value=\"{Bind Static PlantState.Auto}\" style=\"width:44; height:24\"/>");
        Tap(s, Flatten(s.Root!).First(b => b.Node.Tag == "DvToggle"));
        Assert.True(PlantState.Auto);
        Assert.Equal([("Static PlantState.Auto", (object?)true)], vm.Seen);
        Assert.True(Flatten(s.Root!).First(b => b.Node.Tag == "DvToggle").ToggleOn); // 다시 읽어 켜짐으로
    }

    [Fact]
    public void Works_inside_repeated_items_and_component_templates()
    {
        PlantState.Mode = "AUTO";
        var (s, _) = Start("""
            <DvComponent name="ModeTag"><DvLabel text="[{Static PlantState.Mode}]"/></DvComponent>
            <DvRepeat items="{Rows}"><DvLabel text="{Item}:{Static PlantState.Mode}"/></DvRepeat>
            <ModeTag/>
            """);
        Assert.Equal(["[AUTO]", "a:AUTO", "b:AUTO"], Texts(s).Order());
    }

    [Fact]
    public void A_namespace_qualified_name_resolves_and_an_ambiguous_short_name_does_not()
    {
        Devinno.UI.Tests.StaticData.Twin.Value = 1;
        Devinno.UI.Tests.StaticData2.Twin.Value = 2;
        var cap = new Capture();
        Trace.Listeners.Add(cap);
        try
        {
            var (s, _) = Start("""
                <DvLabel text="{Static Devinno.UI.Tests.StaticData2.Twin.Value}"/>
                <DvLabel text="{Static Twin.Value}"/>
                """);
            Assert.Equal(["2", ""], Texts(s));
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
        Assert.Contains(cap.Lines, l => l.Contains("ambiguous") && l.Contains("Twin"));
    }

    [Fact]
    public void Missing_type_or_property_and_a_read_only_write_give_a_warning()
    {
        var cap = new Capture();
        Trace.Listeners.Add(cap);
        try
        {
            var (s, vm) = Start("""
                <DvLabel text="{Static NoSuchTypeXyz.Value}"/>
                <DvLabel text="{Static PlantState.NoSuchProperty}"/>
                <DvToggle value="{Bind Static PlantState.ReadOnlyFlag}" style="width:44; height:24"/>
                """);
            Assert.Equal(["", ""], Texts(s));
            Tap(s, Flatten(s.Root!).First(b => b.Node.Tag == "DvToggle"));
            Assert.False(PlantState.ReadOnlyFlag);
            Assert.Empty(vm.Seen);
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
        Assert.Contains(cap.Lines, l => l.Contains("NoSuchTypeXyz"));
        Assert.Contains(cap.Lines, l => l.Contains("NoSuchProperty"));
        Assert.Contains(cap.Lines, l => l.Contains("ReadOnlyFlag") && l.Contains("setter"));
    }

    [Fact]
    public void A_type_of_another_assembly_is_written_with_its_namespace()
    {
        var (s, _) = Start("""
            <DvLabel text="{Static System.Environment.NewLine}"/>
            <DvLabel text="{Static System.Environment.ProcessorCount:0}"/>
            """);
        Assert.Equal([Environment.NewLine, Environment.ProcessorCount.ToString()], Texts(s));
    }

    [Fact]
    public void Static_works_without_a_view_model()
    {
        PlantState.Mode = "AUTO";
        // VM 이 없으면 짧은 이름은 앱(entry) 어셈블리에서 — 테스트 실행기에는 없으니 네임스페이스로
        var s = new DvSurface("<DvScreen><DvLabel text=\"{Static Devinno.UI.Tests.StaticData.PlantState.Mode}\"/></DvScreen>", null);
        s.Layout(W, H);
        Assert.Equal(["AUTO"], Texts(s));
    }
}

namespace Devinno.UI.Tests.StaticData
{
    public static class PlantState
    {
        public static string Mode { get; set; } = "AUTO";
        public static double Temp { get; set; }
        public static bool Auto { get; set; }
        public static bool ReadOnlyFlag => false;
        public static PumpState Pump { get; set; } = new();
    }

    public sealed class PumpState
    {
        public double Speed { get; set; }
    }

    public static class Twin
    {
        public static int Value { get; set; }
    }
}

namespace Devinno.UI.Tests.StaticData2
{
    public static class Twin
    {
        public static int Value { get; set; }
    }
}
