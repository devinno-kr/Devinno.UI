using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Editing;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 값 핸들(설계 4.1 "값의 주인은 하나"): {Bind}=VM, value 없음 + id = 핸들이 주인(화면에서도 눌림), 단방향·리터럴 = 덮어쓰기.
public class ControlValueTests
{
    private const float W = 600, H = 400;

    public sealed class Vm : IDvControlHost
    {
        public DvToggle? tg; public DvButton? btn; public DvKnob? kn; public DvInput? inp; public DvInput? num;
        public DvSelect? sel; public DvSegment? seg; public DvDateTimeInput? at; public DvColorInput? col;

        public bool On { get; set; }
        public int Speed { get; set; } = 10;
        public string Name { get; set; } = "a";
        public string Mode { get; set; } = "auto";
        public DateOnly Day { get; set; } = new(2026, 1, 2);
        public string Hex { get; set; } = "#112233";
        public bool Allow { get; set; } = true;
        public bool Gate(bool proposed) => Allow;

        public void Attach(DvControlLookup c)
        {
            tg = c.Bind(tg, "tg"); btn = c.Bind(btn, "btn"); kn = c.Bind(kn, "kn"); inp = c.Bind(inp, "inp"); num = c.Bind(num, "num");
            sel = c.Bind(sel, "sel"); seg = c.Bind(seg, "seg"); at = c.Bind(at, "at"); col = c.Bind(col, "col");
        }
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20; gap:10\">{body}</DvScreen>", vm, DvTheme.Light) { UseNativeEditor = true };
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static LayoutBox Box(DvSurface s, string tag) => Flatten(s.Root!).First(b => b.Node.Tag == tag);

    private static void Tap(DvSurface s, LayoutBox b)
    {
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static List<bool> Track(Action<EventHandler<DvValueChangedEventArgs>> subscribe)
    {
        var list = new List<bool>();
        subscribe((_, e) => list.Add(e.ByUser));
        return list;
    }

    // ── 토글: 세 주인 ──

    [Fact]
    public void Owned_Toggle_TapFlips_HandleHoldsValue()
    {
        var (s, vm) = Start("<DvToggle id=\"tg\"/>");
        var ev = Track(h => vm.tg!.CheckedChanged += h);
        Assert.False(vm.tg!.Checked);

        Tap(s, Box(s, "DvToggle"));
        Assert.True(vm.tg.Checked);
        Assert.True(Box(s, "DvToggle").ToggleOn);
        Assert.Equal(new[] { true }, ev); // 화면에서 바꿈 = ByUser
    }

    [Fact]
    public void Owned_Toggle_CodeWrite_RedrawsAndRaisesOnce()
    {
        var (s, vm) = Start("<DvToggle id=\"tg\"/>");
        var ev = Track(h => vm.tg!.CheckedChanged += h);
        vm.tg!.Checked = true;
        vm.tg.Checked = true; // 같은 값 = 이벤트 없음
        Assert.Equal(new[] { false }, ev);
        s.Layout(W, H);
        Assert.True(Box(s, "DvToggle").ToggleOn);
        Assert.Single(ev); // 다시 만든 화면에서 또 알리지 않음
    }

    [Fact]
    public void Owned_Toggle_OnBeforeCommit_StillGates()
    {
        var vm = new Vm { Allow = false };
        var (s, _) = Start("<DvToggle id=\"tg\" onbeforecommit=\"Gate\"/>", vm);
        Tap(s, Box(s, "DvToggle"));
        Assert.False(vm.tg!.Checked);
    }

    [Fact]
    public void Bound_Toggle_HandleReadsAndWritesVm()
    {
        var (s, vm) = Start("<DvToggle id=\"tg\" value=\"{Bind On}\"/>");
        var ev = Track(h => vm.tg!.CheckedChanged += h);

        vm.tg!.Checked = true;
        Assert.True(vm.On);
        s.Layout(W, H);
        Assert.True(Box(s, "DvToggle").ToggleOn);

        Tap(s, Box(s, "DvToggle"));
        Assert.False(vm.On);
        Assert.False(vm.tg.Checked);

        vm.On = true; // 코드가 VM 을 직접 바꿈 → 다시 만들 때 알아챔
        s.Invalidate();
        s.Layout(W, H);
        Assert.Equal(new[] { false, true, false }, ev);
    }

    [Fact]
    public void OneWay_Toggle_HandleOverrides_ReadOnlyOnScreen_ResetRestores()
    {
        var vm = new Vm { On = false };
        var (s, _) = Start("<DvToggle id=\"tg\" value=\"{On}\"/>", vm);
        vm.tg!.Checked = true;
        s.Layout(W, H);
        Assert.True(Box(s, "DvToggle").ToggleOn);
        Assert.False(vm.On);

        Tap(s, Box(s, "DvToggle")); // 단방향 = 화면에서는 안 바뀜
        Assert.True(vm.tg.Checked);

        vm.tg.ResetOverrides();
        s.Layout(W, H);
        Assert.False(Box(s, "DvToggle").ToggleOn);
        Assert.False(vm.tg.Checked);
    }

    [Fact]
    public void Literal_Toggle_ReadsMarkupValue()
    {
        var (_, vm) = Start("<DvToggle id=\"tg\" value=\"true\"/>");
        Assert.True(vm.tg!.Checked);
    }

    [Fact]
    public void Handle_IsolatesEventException_FromUiPath()
    {
        var (s, vm) = Start("<DvToggle id=\"tg\"/>");
        DvHandlerErrorEventArgs? err = null;
        s.HandlerError += (_, e) => err = e;
        vm.tg!.CheckedChanged += (_, _) => throw new InvalidOperationException("boom");
        Tap(s, Box(s, "DvToggle"));
        Assert.True(vm.tg.Checked);
        Assert.Equal("CheckedChanged", err?.Kind);
    }

    // ── 래칭 버튼 ──

    [Fact]
    public void LatchButton_Checked_BoundAndPlain()
    {
        var (s, vm) = Start("<DvButton id=\"btn\" text=\"자동\" value=\"{Bind On}\"/>");
        var ev = Track(h => vm.btn!.CheckedChanged += h);
        Tap(s, Box(s, "DvButton"));
        Assert.True(vm.btn!.Checked);
        Assert.Equal(new[] { true }, ev);

        var (s2, vm2) = Start("<DvButton id=\"btn\" text=\"보통\"/>"); // value 없는 버튼은 래칭이 아님
        Tap(s2, Box(s2, "DvButton"));
        Assert.False(vm2.btn!.Checked);
        Assert.False(Box(s2, "DvButton").ToggleOn);
    }

    // ── 노브 ──

    [Fact]
    public void Knob_Owned_And_Bound()
    {
        var (s, vm) = Start("<DvKnob id=\"kn\" min=\"0\" max=\"100\" style=\"width:120; height:120\"/>");
        vm.kn!.Value = 42;
        s.Layout(W, H);
        Assert.Equal(42f, Box(s, "DvKnob").KnobValue);
        Assert.True(Devinno.UI.Interaction.KnobBinder.CanDrag(Box(s, "DvKnob"))); // 핸들이 주인 = 돌릴 수 있음

        var (s2, vm2) = Start("<DvKnob id=\"kn\" value=\"{Bind Speed}\"/>");
        var ev = Track(h => vm2.kn!.ValueChanged += h);
        Assert.Equal(10, vm2.kn!.Value);
        vm2.kn.Value = 33.4; // int 속성 — 형식 변환
        Assert.Equal(33, vm2.Speed);
        Assert.Equal(new[] { false }, ev);
    }

    // ── 입력 ──

    [Fact]
    public void Input_Owned_EditCommit_RaisesByUser()
    {
        var (s, vm) = Start("<DvInput id=\"inp\"/>");
        var ev = Track(h => vm.inp!.TextChanged += h);
        Assert.True(Devinno.UI.Interaction.HitTester.HitTarget(s.Root!, new SKPoint(Box(s, "DvInput").Rect.MidX, Box(s, "DvInput").Rect.MidY), out _) is not null);
        Assert.False(Box(s, "DvInput").ReadOnly); // value 없음 + 핸들 = 편집 가능

        Tap(s, Box(s, "DvInput"));
        Assert.True(s.Edit.IsEditing);
        s.SetEditText(s.Edit.SessionId, "hello");
        s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter);
        s.Layout(W, H);
        Assert.Equal("hello", vm.inp!.Text);
        Assert.Equal("hello", Box(s, "DvInput").InputValue);
        Assert.Equal(new[] { true }, ev);
    }

    [Fact]
    public void Input_NoHandle_NoValue_StaysReadOnly()
    {
        var s = new DvSurface("<DvScreen><DvInput/></DvScreen>", new object(), DvTheme.Light);
        s.Layout(W, H);
        Assert.True(Flatten(s.Root!).First(b => b.Node.Tag == "DvInput").ReadOnly); // 기존 동작 그대로
    }

    [Fact]
    public void Input_Numeric_Owned_ValueAndText()
    {
        var (s, vm) = Start("<DvInput id=\"num\" keyboard-type=\"numeric\"/>");
        var tv = Track(h => vm.num!.ValueChanged += h);
        vm.num!.Value = 1.5;
        Assert.Equal("1.5", vm.num.Text);
        vm.num.Text = "2";
        Assert.Equal(2d, vm.num.Value);
        Assert.Throws<FormatException>(() => vm.num.Text = "abc");
        s.Layout(W, H);
        Assert.Equal(2d, Box(s, "DvInput").InputValue);
        Assert.Equal(new[] { false, false }, tv);
    }

    [Fact]
    public void Input_Bound_TextWritesVm()
    {
        var (_, vm) = Start("<DvInput id=\"inp\" value=\"{Bind Name}\"/>");
        Assert.Equal("a", vm.inp!.Text);
        vm.inp.Text = "b";
        Assert.Equal("b", vm.Name);
    }

    // ── 선택·세그먼트 ──

    private const string Options = "<DvOption value=\"auto\" text=\"자동\"/><DvOption value=\"manual\" text=\"수동\"/><DvOption value=\"off\" text=\"끔\"/>";

    [Fact]
    public void Segment_Owned_TapSelects()
    {
        var (s, vm) = Start($"<DvSegment id=\"seg\">{Options}</DvSegment>");
        var ev = Track(h => vm.seg!.SelectionChanged += h);
        Assert.Equal(-1, vm.seg!.SelectedIndex);
        var items = Flatten(s.Root!).Where(b => b.Node.Tag == "DvSegmentItem").ToList();
        Tap(s, items[1]);
        Assert.Equal(1, vm.seg.SelectedIndex);
        Assert.Equal("manual", vm.seg.SelectedValue);
        Assert.Equal(new[] { true }, ev);
    }

    [Fact]
    public void Select_IndexBeforeLayout_AppliesOnFirstLayout()
    {
        var vm = new Vm();
        var s = new DvSurface($"<DvScreen><DvSelect id=\"sel\">{Options}</DvSelect></DvScreen>", vm, DvTheme.Light);
        vm.sel!.SelectedIndex = 2; // OnLoaded 처럼 배치 전
        Assert.Equal(2, vm.sel.SelectedIndex);
        s.Layout(W, H);
        s.Layout(W, H); // 적용한 값으로 다시 만든 화면
        Assert.Equal("off", vm.sel.SelectedValue);
        Assert.Equal(2, Flatten(s.Root!).First(b => b.Node.Tag == "DvSelect").SelectedIndex);
    }

    [Fact]
    public void Select_Bound_IndexWritesVm_OutOfRangeThrows()
    {
        var (_, vm) = Start($"<DvSelect id=\"sel\" value=\"{{Bind Mode}}\">{Options}</DvSelect>");
        Assert.Equal(0, vm.sel!.SelectedIndex);
        vm.sel.SelectedIndex = 1;
        Assert.Equal("manual", vm.Mode);
        Assert.Throws<ArgumentOutOfRangeException>(() => vm.sel.SelectedIndex = 3);
    }

    // ── 날짜·색 ──

    [Fact]
    public void DateTime_Bound_DateOnly_Converts()
    {
        var (_, vm) = Start("<DvDateTimeInput id=\"at\" value=\"{Bind Day}\"/>");
        Assert.Equal(new DateTime(2026, 1, 2), vm.at!.Value);
        vm.at.Value = new DateTime(2026, 5, 6, 7, 8, 9);
        Assert.Equal(new DateOnly(2026, 5, 6), vm.Day);
    }

    [Fact]
    public void Color_Bound_String_And_Owned()
    {
        var (s, vm) = Start("<DvColorInput id=\"col\" value=\"{Bind Hex}\"/>");
        Assert.Equal(new SKColor(0x11, 0x22, 0x33), vm.col!.Value);
        vm.col.Value = new SKColor(0xAA, 0xBB, 0xCC);
        Assert.Equal("#AABBCC", vm.Hex);

        var (s2, vm2) = Start("<DvColorInput id=\"col\"/>");
        vm2.col!.Value = SKColors.Red;
        s2.Layout(W, H);
        Assert.Equal(SKColors.Red, Box(s2, "DvColorInput").InputValue);
        Assert.False(Box(s2, "DvColorInput").ReadOnly);
    }

    // ── 연결 전 핸들 ──

    [Fact]
    public void Detached_Handle_StoresValue()
    {
        var vm = new Vm();
        var s = new DvSurface("<DvScreen/>", vm, DvTheme.Light); // id 없음 = 연결되지 않은 핸들
        s.Layout(W, H);
        vm.tg!.Checked = true;
        Assert.True(vm.tg.Checked);
    }
}
