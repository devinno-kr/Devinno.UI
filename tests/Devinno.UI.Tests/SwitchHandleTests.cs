using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvSwitch(내장 컴포넌트 — 이름 + 토글) 값 핸들. 값의 주인은 DvToggle 과 같다: {Bind X} = VM, value 없음 + id = 핸들 칸, 단방향·고정 = 덮어쓰기.
// 토글 겉모양: 비활성 = 반투명, 읽기 전용(단방향·고정) = 점선 테두리.
public class SwitchHandleTests
{
    private const float W = 500, H = 300;

    public sealed class Vm : IDvControlHost
    {
        public DvSwitch? sw;
        public bool Auto { get; set; }
        public bool Allow { get; set; } = true;
        public bool Gate(bool proposed) => Allow;
        public void Attach(DvControlLookup c) => sw = c.Bind(sw, "sw");
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20; gap:8\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static LayoutBox Toggle(DvSurface s) => Flatten(s.Root!).First(b => b.Node.Tag == "DvToggle");

    private static void TapToggle(DvSurface s)
    {
        var t = Toggle(s).Rect;
        var p = new SKPoint(t.MidX, t.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    [Fact]
    public void Generated_and_lookup_handle_type_is_DvSwitch()
    {
        var (_, vm) = Start("<DvSwitch id=\"sw\" label=\"자동\"/>");
        Assert.IsType<DvSwitch>(vm.sw);
    }

    [Fact]
    public void Without_value_the_handle_owns_it_and_the_screen_flips_it()
    {
        var (s, vm) = Start("<DvSwitch id=\"sw\" label=\"자동\"/>");
        var events = new List<bool>();
        vm.sw!.CheckedChanged += (_, e) => events.Add(e.ByUser);
        Assert.False(vm.sw.Checked);

        TapToggle(s);
        Assert.True(vm.sw.Checked);
        Assert.True(Toggle(s).ToggleOn);

        vm.sw.Checked = false;      // 코드에서
        s.Layout(W, H);
        Assert.False(Toggle(s).ToggleOn);
        Assert.Equal(new[] { true, false }, events);
    }

    [Fact]
    public void Bound_value_reads_and_writes_the_vm()
    {
        var (s, vm) = Start("<DvSwitch id=\"sw\" label=\"자동\" value=\"{Bind Auto}\"/>");
        var byUser = new List<bool>();
        vm.sw!.CheckedChanged += (_, e) => byUser.Add(e.ByUser);

        TapToggle(s);
        Assert.True(vm.Auto);
        Assert.True(vm.sw.Checked);

        vm.sw.Checked = false;
        Assert.False(vm.Auto);
        Assert.Equal(new[] { true, false }, byUser);
    }

    [Fact]
    public void One_way_value_is_read_only_on_screen_and_code_overrides_it()
    {
        var (s, vm) = Start("<DvSwitch id=\"sw\" label=\"자동\" value=\"{Auto}\"/>");
        TapToggle(s);
        Assert.False(vm.sw!.Checked);   // 화면에서 안 눌림

        vm.sw.Checked = true;           // 덮어쓰기
        s.Layout(W, H);
        Assert.True(Toggle(s).ToggleOn);
        Assert.False(vm.Auto);

        vm.sw.ResetOverrides();
        s.Layout(W, H);
        Assert.False(Toggle(s).ToggleOn);
    }

    [Fact]
    public void Gate_and_disabled_block_the_handle_owned_switch()
    {
        var vm = new Vm { Allow = false };
        var (s, _) = Start("<DvSwitch id=\"sw\" label=\"자동\" disabled=\"true\"/>", vm);
        TapToggle(s);
        Assert.False(vm.sw!.Checked);
    }

    [Fact]
    public void Toggle_looks_dim_when_disabled_and_dashed_when_read_only()
    {
        var (s, _) = Start("""
            <DvToggle value="true"/>
            <DvToggle value="{Bind Auto}" disabled="true"/>
            """, new Vm { Auto = true });
        using var bmp = new SKBitmap((int)W, (int)H);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, W, H, 0);
        var ts = Flatten(s.Root!).Where(b => b.Node.Tag == "DvToggle").Select(b => Devinno.UI.Painting.WidgetMetrics.ShapeRect(b)).ToList();

        // 읽기 전용: 트랙 바깥 2.5px 에 점선 테두리 — 테두리 자리 위 픽셀 일부가 배경과 다름
        var ro = ts[0];
        var edgeY = (int)MathF.Round(ro.Top - 2.5f);
        var marked = Enumerable.Range((int)ro.MidX - 10, 20).Count(x => bmp.GetPixel(x, edgeY) != bmp.GetPixel(x, edgeY - 4));
        Assert.InRange(marked, 4, 16); // 점선 — 일부만

        // 비활성: 켜진 트랙 색이 반투명(배경과 섞여 원래 색보다 옅음)
        var on = bmp.GetPixel((int)ts[1].Left + 6, (int)ts[1].MidY);
        Assert.NotEqual(new SKColor(0x3a, 0x7b, 0xd5), on);
        Assert.True(on.Red > 0x3a && on.Green > 0x7b, on.ToString());
    }
}
