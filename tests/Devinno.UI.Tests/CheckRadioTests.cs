using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvCheckBox(상자 + 글자, 글자를 눌러도 바뀜) · DvRadio(value + option — 같은 값에 묶인 버튼이 한 묶음, value 없으면 group 이름).
public class CheckRadioTests
{
    private const float W = 600, H = 500;

    public enum Mode { Auto, Manual, Stop }

    public sealed class Vm : IDvControlHost
    {
        public DvCheckBox? chk; public DvRadio? r1; public DvRadio? r2; public DvRadio? r3;
        public bool Alarm { get; set; }
        public Mode Run { get; set; } = Mode.Auto;
        public string? Speed { get; set; }
        public bool Allow { get; set; } = true;
        public bool GateBool(bool proposed) => Allow;
        public bool GateMode(Mode proposed) => Allow;
        public void Attach(DvControlLookup c) { chk = c.Bind(chk, "chk"); r1 = c.Bind(r1, "r1"); r2 = c.Bind(r2, "r2"); r3 = c.Bind(r3, "r3"); }
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20; gap:4\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static List<LayoutBox> All(DvSurface s, string tag) => Flatten(s.Root!).Where(b => b.Node.Tag == tag).ToList();

    private static void TapAt(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static void Tap(DvSurface s, LayoutBox b) => TapAt(s, new SKPoint(b.Rect.MidX, b.Rect.MidY));

    // ── 체크박스 ──

    [Fact]
    public void CheckBox_Bound_TapAnywhereOnText_Flips()
    {
        var (s, vm) = Start("<DvCheckBox text=\"알람 울림\" value=\"{Bind Alarm}\"/>");
        var b = All(s, "DvCheckBox")[0];
        Assert.True(b.Rect.Height >= 40); // 터치 높이
        Assert.Equal("알람 울림", b.ResolvedText);
        TapAt(s, new SKPoint(b.Rect.Left + 60, b.Rect.MidY)); // 상자가 아니라 글자 위
        Assert.True(vm.Alarm);
        Assert.True(All(s, "DvCheckBox")[0].ToggleOn);
    }

    [Fact]
    public void CheckBox_WidthFollowsText_BoxSpaceIncluded()
    {
        var (s, _) = Start("<DvRow><DvCheckBox text=\"가\"/><DvCheckBox text=\"가나다라마바사\"/></DvRow>");
        var bs = All(s, "DvCheckBox");
        Assert.True(bs[0].Rect.Width >= 34 + 10);
        Assert.True(bs[1].Rect.Width > bs[0].Rect.Width);
    }

    [Fact]
    public void CheckBox_Gate_And_Disabled()
    {
        var vm = new Vm { Allow = false };
        var (s, _) = Start("<DvCheckBox text=\"A\" value=\"{Bind Alarm}\" onbeforecommit=\"GateBool\"/><DvCheckBox text=\"B\" value=\"{Bind Alarm}\" disabled=\"true\"/>", vm);
        Tap(s, All(s, "DvCheckBox")[0]);
        Tap(s, All(s, "DvCheckBox")[1]);
        Assert.False(vm.Alarm);
    }

    [Fact]
    public void CheckBox_HandleOwned_And_Static()
    {
        var (s, vm) = Start("<DvCheckBox id=\"chk\" text=\"코드\"/><DvCheckBox text=\"표시만\" value=\"true\"/>");
        var ev = new List<bool>();
        vm.chk!.CheckedChanged += (_, e) => ev.Add(e.ByUser);
        Tap(s, All(s, "DvCheckBox")[0]);
        Assert.True(vm.chk.Checked);
        vm.chk.Checked = false;
        Assert.Equal(new[] { true, false }, ev);

        Tap(s, All(s, "DvCheckBox")[1]); // 고정 값 = 읽기 전용
        Assert.True(All(s, "DvCheckBox")[1].ToggleOn);
    }

    // ── 라디오 ──

    private const string Modes =
        "<DvRadio id=\"r1\" text=\"자동\" value=\"{Bind Run}\" option=\"Auto\"/>" +
        "<DvRadio id=\"r2\" text=\"수동\" value=\"{Bind Run}\" option=\"Manual\"/>" +
        "<DvRadio id=\"r3\" text=\"정지\" value=\"{Bind Run}\" option=\"Stop\"/>";

    [Fact]
    public void Radio_Bound_Enum_OnlyMatchingIsOn_TapSelects()
    {
        var (s, vm) = Start(Modes);
        Assert.Equal(new[] { true, false, false }, All(s, "DvRadio").Select(b => b.ToggleOn));
        Tap(s, All(s, "DvRadio")[2]);
        Assert.Equal(Mode.Stop, vm.Run);
        Assert.Equal(new[] { false, false, true }, All(s, "DvRadio").Select(b => b.ToggleOn));
        Tap(s, All(s, "DvRadio")[2]); // 켜진 것을 다시 눌러도 그대로
        Assert.Equal(Mode.Stop, vm.Run);
    }

    [Fact]
    public void Radio_Handles_RaiseForBothOnAndOff()
    {
        var (s, vm) = Start(Modes);
        var log = new List<string>();
        vm.r1!.CheckedChanged += (_, e) => log.Add($"r1 {vm.r1.Checked} {e.ByUser}");
        vm.r2!.CheckedChanged += (_, e) => log.Add($"r2 {vm.r2.Checked} {e.ByUser}");
        Tap(s, All(s, "DvRadio")[1]);
        Assert.Equal(new[] { "r1 False True", "r2 True True" }, log.OrderBy(x => x));

        log.Clear();
        vm.r1.Checked = true; // 코드 — r1 켜짐, r2 꺼짐
        Assert.Equal(Mode.Auto, vm.Run);
        Assert.Equal(new[] { "r1 True False", "r2 False False" }, log.OrderBy(x => x));

        Assert.Throws<InvalidOperationException>(() => vm.r1.Checked = false); // enum 은 null 을 못 받음 — 기본값(Auto)이 들어가지 않게 막음
        Assert.Equal(Mode.Auto, vm.Run);
    }

    [Fact]
    public void Radio_Gate_GetsConvertedValue()
    {
        var vm = new Vm { Allow = false };
        var (s, _) = Start("<DvRadio text=\"수동\" value=\"{Bind Run}\" option=\"Manual\" onbeforecommit=\"GateMode\"/>", vm);
        Tap(s, All(s, "DvRadio")[0]);
        Assert.Equal(Mode.Auto, vm.Run);
    }

    [Fact]
    public void Radio_Group_NoBinding_ScreenRemembers()
    {
        var (s, _) = Start(
            "<DvRadio text=\"저\" group=\"speed\" option=\"low\"/><DvRadio text=\"중\" group=\"speed\" option=\"mid\"/>" +
            "<DvRadio text=\"A\" group=\"other\" option=\"a\"/>");
        Tap(s, All(s, "DvRadio")[1]);
        Tap(s, All(s, "DvRadio")[2]);
        Assert.Equal(new[] { false, true, true }, All(s, "DvRadio").Select(b => b.ToggleOn));
        Tap(s, All(s, "DvRadio")[0]);
        Assert.Equal(new[] { true, false, true }, All(s, "DvRadio").Select(b => b.ToggleOn));
    }

    [Fact]
    public void Radio_NoValueNoGroupNoHandle_IsStatic()
    {
        var (s, _) = Start("<DvRadio text=\"혼자\" option=\"x\"/>");
        Tap(s, All(s, "DvRadio")[0]);
        Assert.False(All(s, "DvRadio")[0].ToggleOn);
    }

    [Fact]
    public void Radio_GroupHandles_CodeWrite()
    {
        var (s, vm) = Start(
            "<DvRadio id=\"r1\" text=\"저\" group=\"g\" option=\"low\"/><DvRadio id=\"r2\" text=\"고\" group=\"g\" option=\"high\"/>");
        vm.r2!.Checked = true;
        s.Layout(W, H);
        Assert.Equal(new[] { false, true }, All(s, "DvRadio").Select(b => b.ToggleOn));
        Assert.False(vm.r1!.Checked);
        vm.r2.Checked = false;
        s.Layout(W, H);
        Assert.Equal(new[] { false, false }, All(s, "DvRadio").Select(b => b.ToggleOn));
    }

    [Fact]
    public void Radio_StringProperty_NullMeansNone()
    {
        var (s, vm) = Start("<DvRadio id=\"r1\" text=\"저\" value=\"{Bind Speed}\" option=\"low\"/>");
        Assert.False(All(s, "DvRadio")[0].ToggleOn);
        Tap(s, All(s, "DvRadio")[0]);
        Assert.Equal("low", vm.Speed);
        vm.r1!.Checked = false;
        Assert.Null(vm.Speed);
    }

    [Fact]
    public void Paints_Without_Throwing()
    {
        var (s, _) = Start("<DvCheckBox text=\"A\" value=\"true\"/><DvCheckBox text=\"B\"/>" + Modes + "<DvRadio text=\"C\" option=\"x\" disabled=\"true\"/>");
        using var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        // 켜진 체크 상자 가운데는 primary 색
        var b = All(s, "DvCheckBox")[0];
        var px = bmp.GetPixel((int)(b.Rect.Left + 3), (int)b.Rect.MidY);
        Assert.True(DvColor.TryParse(DvTheme.Light.Tokens["primary"], out var primary));
        Assert.Equal(primary.ToSK(), px);
    }
}
