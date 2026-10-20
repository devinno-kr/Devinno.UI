using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using SkiaSharp;
using Xunit;

// onbeforecommit 메서드가 없거나 시그니처가 맞지 않거나 예외를 던지면 위젯도 그리드 셀처럼 거절 + Trace 경고
public class CommitGateTests
{
    public sealed class Vm
    {
        public bool Flag { get; set; }
        public double Level { get; set; } = 40;
        public string Name { get; set; } = "펌프 A";
        public int Count { get; set; } = 2;

        public int VoidCalls;
        public void VoidGate(bool proposed) => VoidCalls++;
        public bool Throws(bool proposed) => throw new InvalidOperationException("boom");
        public bool WrongType(string proposed) => true; // 토글엔 bool 이 필요
        public bool Ok(bool proposed) => true;
    }

    private static LayoutBox Build(string inner, Vm vm)
        => new LayoutEngine().Layout(MarkupParser.Parse($"<DvScreen>{inner}</DvScreen>"), vm, 300, 300).Children[0];

    [Theory]
    [InlineData("MissingGate")]
    [InlineData("VoidGate")]
    [InlineData("WrongType")]
    [InlineData("Throws")]
    public void Toggle_rejects_when_gate_missing_mismatched_or_throwing(string gate)
    {
        var vm = new Vm();
        TwoWayBinder.Commit(Build($"<DvToggle value=\"{{Bind Data.Flag}}\" onbeforecommit=\"{gate}\"/>", vm), vm);
        Assert.False(vm.Flag);
    }

    [Fact]
    public void Toggle_with_matching_gate_or_no_gate_writes()
    {
        var vm = new Vm();
        TwoWayBinder.Commit(Build("<DvToggle value=\"{Bind Data.Flag}\" onbeforecommit=\"Ok\"/>", vm), vm);
        Assert.True(vm.Flag);
        TwoWayBinder.Commit(Build("<DvToggle value=\"{Bind Data.Flag}\"/>", vm), vm);
        Assert.False(vm.Flag);
    }

    [Fact]
    public void Knob_rejects_when_gate_missing()
    {
        var vm = new Vm();
        KnobBinder.Commit(Build("<DvKnob value=\"{Bind Data.Level}\" onbeforecommit=\"MissingGate\" style=\"width:100; height:100\"/>", vm), vm, 73);
        Assert.Equal(40, vm.Level);
    }

    [Fact]
    public void Input_rejects_missing_gate_and_keeps_session()
    {
        var vm = new Vm();
        var s = new DvSurface("<DvScreen style=\"padding:0; gap:0\"><DvInput value=\"{Bind Data.Name}\" onbeforecommit=\"MissingGate\"/></DvScreen>", vm)
            { UseNativeEditor = true };
        s.Layout(400, 300);
        s.PointerDown(new SKPoint(50, 20));
        s.PointerUp(new SKPoint(50, 20));
        var id = s.Edit.SessionId;
        s.SetEditText(id, "펌프 B");

        Assert.False(s.CommitEdit(id, DvEditExit.Enter));
        Assert.True(s.Edit.IsEditing);
        Assert.NotNull(s.Edit.Error);
        Assert.Equal("펌프 A", vm.Name);
    }

    [Fact]
    public void Select_slot_rejects_missing_gate()
    {
        var vm = new Vm();
        var s = new DvSurface("""
            <DvScreen><DvSelect value="{Bind Data.Count}" onbeforecommit="MissingGate">
              <DvOption value="1" text="하나"/><DvOption value="2" text="둘"/><DvOption value="3" text="셋"/>
            </DvSelect></DvScreen>
            """, vm);
        s.Layout(400, 300);
        var slot = new BoxSlot(s, s.Root!.Children[0]);
        Assert.Equal(CommitResult.Rejected, slot.Write(3));
        Assert.Equal(2, vm.Count);
    }

    private sealed class Capture(string marker) : TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && message.Contains(marker))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message)
            => WriteLine(message);
    }

    [Fact]
    public void Missing_gate_warns_once_per_vm_type_and_name()
    {
        var cap = new Capture("g3-missing-gate");
        Trace.Listeners.Add(cap);
        try
        {
            var vm = new Vm();
            for (var i = 0; i < 3; i++)
                TwoWayBinder.Commit(Build("<DvToggle value=\"{Bind Data.Flag}\" onbeforecommit=\"g3-missing-gate\"/>", vm), vm);
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        Assert.Single(cap.Lines);
    }
}
