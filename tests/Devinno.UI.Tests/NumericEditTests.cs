using Devinno.UI;
using Devinno.UI.Components;
using SkiaSharp;
using Xunit;

// P8a-4 숫자 — 타입 추론(정수·부호·실수), InvariantCulture, min/max 거절, onbeforecommit(double)
public class NumericEditTests
{
    public sealed class Vm
    {
        public int Count { get; set; } = 10;
        public uint Pulses { get; set; } = 5;
        public double Temp { get; set; } = 12.5;
        public decimal Price { get; set; } = 1.1m;
        public int? Optional { get; set; }
        public int Writes;
        private int _gated = 7;
        public int Gated { get => _gated; set { _gated = value; Writes++; } }
        public double? Proposed;
        public bool CheckGated(double v) { Proposed = v; return v < 100; }
    }

    private static DvSurface Edit(Vm vm, string input)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:0\">{input}</DvScreen>", vm);
        s.Layout(400, 300);
        s.PointerDown(new SKPoint(50, 20));
        s.PointerUp(new SKPoint(50, 20));
        s.Layout(400, 300);
        Assert.True(s.Edit.IsEditing);
        return s;
    }

    private static bool Commit(DvSurface s, string text)
    {
        var id = s.Edit.SessionId;
        s.SetEditText(id, text);
        var ok = s.CommitEdit(id, DvEditExit.Enter);
        s.Layout(400, 300);
        return ok;
    }

    [Theory]
    [InlineData("Count", true, true)]
    [InlineData("Pulses", true, false)]
    [InlineData("Temp", false, true)]
    [InlineData("Price", false, true)]
    [InlineData("Optional", true, true)]
    public void Spec_follows_bound_property_type(string prop, bool isInteger, bool isSigned)
    {
        var s = Edit(new Vm(), $"<DvInput value=\"{{Bind Data.{prop}}}\" keyboard-type=\"numeric\" min=\"0\" max=\"120\" unit=\"℃\"/>");
        var spec = s.Edit.Numeric!;
        Assert.Equal((isInteger, isSigned), (spec.IsInteger, spec.IsSigned));
        Assert.Equal((0d, 120d, "℃"), (spec.Min, spec.Max, spec.Unit));
    }

    [Fact]
    public void Text_input_has_no_numeric_spec()
    {
        var s = Edit(new Vm(), "<DvInput value=\"{Bind Data.Count}\"/>");
        Assert.Null(s.Edit.Numeric);
    }

    [Fact]
    public void Commits_typed_values()
    {
        var vm = new Vm();
        Assert.True(Commit(Edit(vm, "<DvInput value=\"{Bind Data.Count}\" keyboard-type=\"numeric\"/>"), "42"));
        Assert.Equal(42, vm.Count);
        Assert.True(Commit(Edit(vm, "<DvInput value=\"{Bind Data.Price}\" keyboard-type=\"numeric\"/>"), "0.1"));
        Assert.Equal(0.1m, vm.Price);
        Assert.True(Commit(Edit(vm, "<DvInput value=\"{Bind Data.Optional}\" keyboard-type=\"numeric\"/>"), "-3"));
        Assert.Equal(-3, vm.Optional);
    }

    [Fact]
    public void Parses_with_invariant_culture()
    {
        var prev = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var vm = new Vm();
            Assert.True(Commit(Edit(vm, "<DvInput value=\"{Bind Data.Temp}\" keyboard-type=\"numeric\"/>"), "1.5"));
            Assert.Equal(1.5, vm.Temp);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prev;
        }
    }

    [Theory]
    [InlineData("Count", "3.5")]
    [InlineData("Count", "abc")]
    [InlineData("Count", "")]
    [InlineData("Count", "99999999999")]
    [InlineData("Pulses", "-1")]
    [InlineData("Temp", "NaN")]
    [InlineData("Temp", "1,5")]
    public void Rejects_invalid_text_and_keeps_session(string prop, string text)
    {
        var vm = new Vm();
        var s = Edit(vm, $"<DvInput value=\"{{Bind Data.{prop}}}\" keyboard-type=\"numeric\"/>");
        var id = s.Edit.SessionId;
        Assert.False(Commit(s, text));
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(id, s.Edit.SessionId);
        Assert.False(string.IsNullOrEmpty(s.Edit.Error));
        Assert.Equal((10, 5u, 12.5), (vm.Count, vm.Pulses, vm.Temp));
    }

    [Fact]
    public void Rejects_out_of_range_without_clamping()
    {
        var vm = new Vm();
        var s = Edit(vm, "<DvInput value=\"{Bind Data.Temp}\" keyboard-type=\"numeric\" min=\"0\" max=\"120\"/>");
        Assert.False(Commit(s, "130"));
        Assert.Contains("120", s.Edit.Error);
        Assert.Equal(12.5, vm.Temp);
        Assert.True(Commit(s, "120"));
        Assert.Equal(120, vm.Temp);
    }

    [Fact]
    public void Gate_receives_double_and_can_reject()
    {
        var vm = new Vm();
        var s = Edit(vm, "<DvInput value=\"{Bind Data.Gated}\" keyboard-type=\"numeric\" onbeforecommit=\"CheckGated\"/>");
        Assert.False(Commit(s, "150"));
        Assert.Equal(150d, vm.Proposed);
        Assert.Equal(0, vm.Writes);
        Assert.True(Commit(s, "50"));
        Assert.Equal(50, vm.Gated);
        Assert.Equal(1, vm.Writes);
    }

    [Fact]
    public void Same_number_in_different_text_is_not_written()
    {
        var vm = new Vm();
        var s = Edit(vm, "<DvInput value=\"{Bind Data.Gated}\" keyboard-type=\"numeric\" onbeforecommit=\"CheckGated\"/>");
        Assert.True(Commit(s, "07"));
        Assert.Equal(0, vm.Writes);
        Assert.Null(vm.Proposed);
    }

    [Fact]
    public void Two_way_component_parameter_commits_to_outer_vm()
    {
        var reg = new ComponentRegistry();
        reg.Register("""
            <DvComponent name="Field">
              <DvParam name="Value"/>
              <DvInput value="{Value}" keyboard-type="numeric"/>
            </DvComponent>
            """);
        var vm = new Vm();
        var s = new DvSurface("<DvScreen style=\"padding:0\"><Field Value=\"{Bind Data.Count}\"/></DvScreen>", vm, reg);
        s.Layout(400, 300);
        s.PointerDown(new SKPoint(50, 20));
        s.PointerUp(new SKPoint(50, 20));
        s.Layout(400, 300);
        Assert.True(s.Edit.IsEditing);
        Assert.Equal("10", s.Edit.InitialText);
        Assert.True(Commit(s, "77"));
        Assert.Equal(77, vm.Count);
    }
}
