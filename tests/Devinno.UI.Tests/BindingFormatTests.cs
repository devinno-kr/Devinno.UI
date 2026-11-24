using System.Diagnostics;
using System.Globalization;
using Devinno.UI;
using Devinno.UI.Binding;
using Devinno.UI.Layout;
using Xunit;

// 바인딩 표시 형식 {경로:형식} — 위젯 format 과 같은 규칙(.NET 형식, InvariantCulture, 틀린 형식 = 기본 글자 + 경고).
// 형식은 글자로 바꿀 때만 쓴다: 숫자·참거짓 등 값 속성과 두 방향 연결은 형식을 무시하고 경고. 형식 없는 값도 InvariantCulture.
// 경고 값마다 고유한 글자를 써서 다른 테스트의 경고와 섞이지 않게 한다.
public class BindingFormatTests
{
    public sealed class Vm
    {
        public double Temp { get; set; } = 24.56789;
        public double Level { get; set; } = 42.75;
        public DateTime At { get; set; } = new(2026, 11, 24, 13, 5, 9);
        public bool Run { get; set; } = true;
        public string Name { get; set; } = "P-101";
        public Inner Pump { get; } = new();
        public List<Inner> Items { get; } = [new() { Rpm = 1234.5 }];
    }

    public sealed class Inner
    {
        public double Rpm { get; set; } = 1500.25;
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

    private static (T Result, List<string> Warnings) Watch<T>(string marker, Func<T> act)
    {
        var cap = new Capture(marker);
        Trace.Listeners.Add(cap);
        try
        {
            var result = act();
            lock (cap.Lines)
                return (result, cap.Lines.ToList());
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static LayoutBox Build(string body, object? vm = null) =>
        new LayoutEngine().Layout(Devinno.UI.Markup.MarkupParser.Parse($"<DvScreen>{body}</DvScreen>"), vm ?? new Vm(), 800, 600);

    private static string LabelText(string body, object? vm = null) => Flatten(Build(body, vm)).First(b => b.Node.Tag == "DvLabel").ResolvedText ?? "";

    // ── 끼워 넣기 ──

    [Theory]
    [InlineData("온도 {Temp:0.0} ℃", "온도 24.6 ℃")]
    [InlineData("{Temp:F2}", "24.57")]
    [InlineData("{Temp:0}%", "25%")]
    [InlineData("{ Temp : 0.0 }", "24.6")]          // 경로 앞뒤 공백 무시
    [InlineData("{Data.Temp:0.0}", "24.6")]
    [InlineData("{Pump.Rpm:#,0}", "1,500")]          // 그룹 구분도 Invariant
    [InlineData("{At:HH:mm}", "13:05")]              // 첫 ':' 에서 자름 — 형식 안 ':' 는 형식 쪽
    [InlineData("{At:yyyy-MM-dd HH:mm:ss}", "2026-11-24 13:05:09")]
    [InlineData("{Temp:0.0} / {Level:0}", "24.6 / 43")]
    public void Inline_format_is_applied(string text, string expected)
        => Assert.Equal(expected, BindingResolver.ResolveInline(new Vm(), text));

    [Fact]
    public void Format_ignores_current_culture()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("T 24.6", BindingResolver.ResolveInline(new Vm(), "T {Temp:0.0}"));
            Assert.Equal("T 24.56789", BindingResolver.ResolveInline(new Vm(), "T {Temp}")); // 형식 없음도 Invariant(전: 24,56789)
            Assert.Equal("11/24/2026 13:05:09", BindingResolver.ResolveInline(new Vm(), "{At}"));
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }

    [Fact]
    public void Single_text_binding_with_format_gives_formatted_text()
    {
        Assert.Equal("24.6", LabelText("""<DvLabel text="{Temp:0.0}"/>"""));
        Assert.Equal("24.6 ℃", LabelText("""<DvLabel>{Temp:0.0} ℃</DvLabel>"""));
    }

    [Fact]
    public void Repeat_item_path_takes_a_format()
        => Assert.Equal("1234.5 → 1,235", LabelText("""<DvRepeat items="{Items}"><DvLabel text="{Item.Rpm} → {Item.Rpm:#,0}"/></DvRepeat>"""));

    [Fact]
    public void Empty_format_is_no_format()
        => Assert.Equal("24.56789", BindingResolver.ResolveInline(new Vm(), "{Temp:}"));

    [Fact]
    public void Value_without_format_is_read_as_before()
    {
        Assert.Equal(24.56789, BindingResolver.Resolve(new Vm(), "{Temp:0.0}"));   // 값 읽기는 형식을 쓰지 않는다
        Assert.Equal("Temp", BindingResolver.ExtractPath("{Temp:0.0}"));
        Assert.Equal("Pump.Rpm", BindingResolver.ExtractPath("{Bind Data.Pump.Rpm:0.0}"));
        Assert.Equal("At", BindingResolver.ExtractPath("{At:HH:mm}"));
    }

    // ── 틀린 형식·형식이 맞지 않는 값 = 기본 글자 + 경고 ──

    [Fact]
    public void Invalid_format_gives_default_text_and_a_warning()
    {
        var (text, warns) = Watch("fmt-bad-q1", () => BindingResolver.ResolveInline(new Vm(), "{Temp:fmt-bad-q1{}"));
        // 중괄호가 든 형식은 바인딩 식이 아니다(정규식이 { } 를 받지 않음) — 원문 그대로
        Assert.Equal("{Temp:fmt-bad-q1{}", text);
        Assert.Empty(warns);

        var (t2, w2) = Watch("{Level:D2}", () => BindingResolver.ResolveInline(new Vm(), "{Level:D2}"));
        Assert.Equal("42.75", t2); // double 에 D = FormatException
        Assert.Single(w2);
        Assert.Contains("not a .NET format", w2[0]);
    }

    [Fact]
    public void Format_on_a_value_that_takes_none_gives_default_text_and_a_warning()
    {
        var (text, warns) = Watch("{Run:q3fmt}", () => BindingResolver.ResolveInline(new Vm(), "{Run:q3fmt}"));
        Assert.Equal("True", text);
        Assert.Single(warns);
        Assert.Contains("takes no format", warns[0]);

        var (t2, w2) = Watch("{Name:q4fmt}", () => BindingResolver.ResolveInline(new Vm(), "{Name:q4fmt}"));
        Assert.Equal("P-101", t2);
        Assert.Single(w2);
    }

    [Fact]
    public void Missing_value_with_format_is_empty_without_warning()
    {
        var (text, warns) = Watch("q5fmt", () => BindingResolver.ResolveInline(new Vm(), "[{NoSuch:q5fmt}]"));
        Assert.Equal("[]", text);
        Assert.Empty(warns);
    }

    // ── 값 속성·두 방향은 형식을 무시 ──

    [Fact]
    public void Value_attribute_ignores_the_format_with_a_warning()
    {
        var (box, warns) = Watch("0.q6", () => Flatten(Build("""<DvGauge value="{Level:0.q6}"/>""")).First(b => b.Node.Tag == "DvGauge"));
        Assert.Equal(42.75f, box.GaugeValue); // 반올림되지 않은 값
        Assert.Single(warns);
        Assert.Contains("not a text attribute", warns[0]);
    }

    public sealed class InputVm
    {
        public double Set { get; set; } = 10;
    }

    [Fact]
    public void Two_way_binding_ignores_the_format_with_a_warning_and_still_writes()
    {
        var vm = new InputVm();
        var (_, warns) = Watch("0.0q7", () =>
        {
            BindingResolver.ResolveInline(vm, "{Bind Set:0.0q7}");
            return 0;
        });
        Assert.Contains(warns, w => w.Contains("two-way"));
        BindingResolver.SetValue(vm, BindingResolver.ExtractPath("{Bind Set:0.0q7}"), 12.5);
        Assert.Equal(12.5, vm.Set);
    }

    // ── 다시 그리기 — 값이 바뀌면 형식 붙은 글자도 새 값 ──

    [Fact]
    public void Formatted_text_follows_the_value_after_invalidate()
    {
        var vm = new Vm();
        var s = new DvSurface("""<DvScreen><DvLabel text="온도 {Temp:0.0} ℃"/></DvScreen>""", vm);
        s.Layout(400, 200);
        string Text() => Flatten(s.Root!).First(b => b.Node.Tag == "DvLabel").ResolvedText ?? "";
        Assert.Equal("온도 24.6 ℃", Text());

        vm.Temp = 31.04;
        s.Invalidate();
        s.Layout(400, 200);
        Assert.Equal("온도 31.0 ℃", Text());
    }
}
