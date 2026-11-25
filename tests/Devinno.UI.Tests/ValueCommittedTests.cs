using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// IDvValueCommitted — 화면이 값을 쓴 뒤에만 알린다(코드·핸들 쓰기, 거절·실패, 비디오 위치는 알리지 않음).
// 알림 받는 쪽 = 경로가 시작하는 객체(화면 VM, Item. 경로면 항목).
public class ValueCommittedTests
{
    private const float W = 400, H = 300;

    public sealed class Motor
    {
        public bool Run { get; set; }
    }

    public sealed class Row : IDvValueCommitted
    {
        public bool On { get; set; }
        public List<(string Path, object? Value)> Seen { get; } = new();
        public void OnValueCommitted(string path, object? value) => Seen.Add((path, value));
    }

    public sealed class Vm : IDvValueCommitted, IDvControlHost
    {
        public DvToggle? tg;
        public bool Flag { get; set; }
        public Motor Motor { get; } = new();
        public string Name { get; set; } = "a";
        public double Speed { get; set; }
        public bool Allow { get; set; } = true;
        public bool Gate(bool proposed) => Allow;
        public bool Throw { get; set; }
        public bool Bad { get => false; set => throw new InvalidOperationException("setter"); }
        public List<Row> Rows { get; } = [new(), new()];
        public List<(string Path, object? Value)> Seen { get; } = new();

        public void OnValueCommitted(string path, object? value)
        {
            Seen.Add((path, value));
            if (Throw)
                throw new InvalidOperationException("handler");
        }

        public void Attach(DvControlLookup c) => tg = c.Bind(tg, "tg");
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:0; gap:0\">{body}</DvScreen>", vm) { UseNativeEditor = true };
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static void Tap(DvSurface s, LayoutBox b)
    {
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static void TapTag(DvSurface s, string tag, int nth = 0) => Tap(s, Flatten(s.Root!).Where(b => b.Node.Tag == tag).ElementAt(nth));

    private const string Toggle = "style=\"width:44; height:24\"";

    [Fact]
    public void Screen_write_is_told_with_path_and_stored_value()
    {
        var (s, vm) = Start($"<DvToggle value=\"{{Bind Data.Flag}}\" {Toggle}/>");
        TapTag(s, "DvToggle");
        Assert.True(vm.Flag);
        Assert.Equal([("Flag", (object?)true)], vm.Seen);
    }

    [Fact]
    public void Nested_path_is_told_to_the_screen_vm()
    {
        var (s, vm) = Start($"<DvToggle value=\"{{Bind Motor.Run}}\" {Toggle}/>");
        TapTag(s, "DvToggle");
        Assert.Equal([("Motor.Run", (object?)true)], vm.Seen);
    }

    [Fact]
    public void Code_writes_are_not_told()
    {
        var (s, vm) = Start($"<DvToggle id=\"tg\" value=\"{{Bind Flag}}\" {Toggle}/>");
        vm.Flag = true;
        s.Invalidate();
        s.Layout(W, H);
        vm.tg!.Checked = false; // 핸들로 쓴 값(코드)
        s.Layout(W, H);
        Assert.False(vm.Flag);
        Assert.Empty(vm.Seen);
    }

    [Fact]
    public void Rejected_or_failed_writes_are_not_told()
    {
        var vm = new Vm { Allow = false };
        var (s, _) = Start($"<DvToggle value=\"{{Bind Flag}}\" onbeforecommit=\"Gate\" {Toggle}/><DvToggle value=\"{{Bind Bad}}\" {Toggle}/>", vm);
        var errors = new List<DvHandlerErrorEventArgs>();
        s.HandlerError += (_, e) => errors.Add(e);
        TapTag(s, "DvToggle", 0);
        TapTag(s, "DvToggle", 1);
        Assert.False(vm.Flag);
        Assert.Empty(vm.Seen);
        Assert.Contains(errors, e => e.Kind == "setter");
    }

    [Fact]
    public void Input_commit_is_told_with_the_converted_value()
    {
        var (s, vm) = Start("<DvInput value=\"{Bind Speed}\" style=\"height:40\"/>");
        TapTag(s, "DvInput");
        var id = s.Edit.SessionId;
        s.SetEditText(id, "12.5");
        Assert.Empty(vm.Seen); // 치는 동안은 쓰지 않는다
        Assert.True(s.CommitEdit(id, DvEditExit.Enter));
        Assert.Equal([("Speed", (object?)12.5)], vm.Seen);
    }

    [Fact]
    public void Repeat_item_write_is_told_to_the_item()
    {
        var (s, vm) = Start($"<DvRepeat items=\"{{Rows}}\"><DvToggle value=\"{{Bind Item.On}}\" {Toggle}/></DvRepeat>");
        TapTag(s, "DvToggle", 1);
        Assert.Empty(vm.Seen);
        Assert.Empty(vm.Rows[0].Seen);
        Assert.Equal([("On", (object?)true)], vm.Rows[1].Seen);
    }

    [Fact]
    public void Handler_exception_is_reported_and_the_value_stays()
    {
        var vm = new Vm { Throw = true };
        var (s, _) = Start($"<DvToggle value=\"{{Bind Flag}}\" {Toggle}/>", vm);
        var errors = new List<DvHandlerErrorEventArgs>();
        s.HandlerError += (_, e) => errors.Add(e);
        TapTag(s, "DvToggle");
        Assert.True(vm.Flag);
        var e = Assert.Single(errors);
        Assert.Equal("committed", e.Kind);
        Assert.Equal("Flag", e.Member);
        Assert.Equal("DvToggle", e.Tag);
    }
}
