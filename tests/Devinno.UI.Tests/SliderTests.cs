using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvSlider — 누른 자리로 값이 옮겨 가고 끄는 대로 미리보기, 놓을 때 한 번 커밋(노브와 같은 커밋 경로). direction = 값이 커지는 쪽.
public class SliderTests
{
    private const float W = 600, H = 500;

    public sealed class Vm : IDvControlHost
    {
        public DvSlider? sl;
        public int Level { get; set; } = 50;
        public double Ratio { get; set; }
        public int Writes;
        public bool Allow { get; set; } = true;
        public bool Gate(double proposed) => Allow;
        public void Attach(DvControlLookup c) => sl = c.Bind(sl, "sl");
    }

    private sealed class CountingVm
    {
        private int _level;
        public int Writes;
        public int Level { get => _level; set { _level = value; Writes++; } }
    }

    private static (DvSurface S, T Vm) Start<T>(string body, T vm)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:20; align-items:start\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static LayoutBox Slider(DvSurface s) => Flatten(s.Root!).First(b => b.Node.Tag == "DvSlider");
    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static SKPoint At(LayoutBox b, float t) // 막대 위 t(0 = 최솟값 끝, 1 = 최댓값 끝)
    {
        var (a, z) = SliderMetrics.Track(b);
        return new SKPoint(a.X + (z.X - a.X) * t, a.Y + (z.Y - a.Y) * t);
    }

    [Fact]
    public void Natural_Size_And_Track()
    {
        var (s, _) = Start("<DvSlider value=\"{Bind Level}\" style=\"width:300\"/>", new Vm());
        var b = Slider(s);
        Assert.Equal(40f, b.Rect.Height);
        var (a, z) = SliderMetrics.Track(b);
        Assert.Equal(b.Rect.Left + 12, a.X);
        Assert.Equal(b.Rect.Right - 12, z.X);
    }

    [Fact]
    public void Tap_Jumps_And_Commits_Once()
    {
        var vm = new CountingVm { Level = 0 };
        var (s, _) = Start("<DvSlider value=\"{Bind Level}\" style=\"width:300\"/>", vm);
        vm.Writes = 0;
        var p = At(Slider(s), 0.75f);
        s.PointerDown(p);
        Assert.Equal(75f, Slider(s).KnobValue); // 누르자마자 그 자리 미리보기
        Assert.Equal(0, vm.Level);              // VM 은 아직
        s.PointerUp(p);
        Assert.Equal(75, vm.Level);
        Assert.Equal(1, vm.Writes);
    }

    [Fact]
    public void Drag_Previews_Then_Commits_On_Release()
    {
        var vm = new CountingVm { Level = 10 };
        var (s, _) = Start("<DvSlider value=\"{Bind Level}\" step=\"10\" style=\"width:300\"/>", vm);
        vm.Writes = 0;
        var b = Slider(s);
        s.PointerDown(At(b, 0.1f));
        for (var t = 0.1f; t <= 0.62f; t += 0.04f)
            s.PointerMove(At(b, t));
        Assert.Equal(10, vm.Level);
        s.PointerUp(At(b, 0.62f));
        Assert.Equal(60, vm.Level); // step 10 반올림
        Assert.Equal(1, vm.Writes);
    }

    [Fact]
    public void Drag_Beyond_End_Clamps()
    {
        var vm = new Vm();
        var (s, _) = Start("<DvSlider value=\"{Bind Level}\" min=\"20\" max=\"80\" style=\"width:300\"/>", vm);
        var b = Slider(s);
        s.PointerDown(At(b, 0.5f));
        s.PointerMove(new SKPoint(b.Rect.Right + 200, b.Rect.MidY));
        s.PointerUp(new SKPoint(b.Rect.Right + 200, b.Rect.MidY));
        Assert.Equal(80, vm.Level);
    }

    [Theory]
    [InlineData("left")]
    [InlineData("up")]
    [InlineData("down")]
    public void Directions_Max_End_Is_Where_Value_Grows(string dir)
    {
        var vm = new Vm();
        var size = dir is "up" or "down" ? "height:300" : "width:300";
        var (s, _) = Start($"<DvSlider value=\"{{Bind Level}}\" direction=\"{dir}\" style=\"{size}\"/>", vm);
        var b = Slider(s);
        var (_, max) = SliderMetrics.Track(b);
        Assert.Equal(dir switch { "left" => b.Rect.Left + 12, "up" => b.Rect.Top + 12, _ => b.Rect.Bottom - 12 },
            dir == "left" ? max.X : max.Y);
        s.PointerDown(max);
        s.PointerUp(max);
        Assert.Equal(100, vm.Level);
    }

    [Fact]
    public void Vertical_Natural_Size()
    {
        var (s, _) = Start("<DvSlider value=\"{Bind Level}\" direction=\"up\" show-value=\"true\"/>", new Vm());
        Assert.Equal(200f, Slider(s).Rect.Height);
        Assert.Equal(40f + SliderMetrics.LabelSide, Slider(s).Rect.Width);
    }

    [Fact]
    public void OneWay_And_Disabled_Do_Not_Move()
    {
        var vm = new Vm();
        var (s, _) = Start("<DvSlider value=\"{Level}\" style=\"width:300\"/><DvSlider value=\"{Bind Level}\" disabled=\"true\" style=\"width:300\"/>", vm);
        foreach (var b in Flatten(s.Root!).Where(x => x.Node.Tag == "DvSlider").ToList())
        {
            s.PointerDown(At(b, 0.9f));
            s.PointerUp(At(b, 0.9f));
        }
        Assert.Equal(50, vm.Level);
    }

    [Fact]
    public void Gate_Rejects()
    {
        var vm = new Vm { Allow = false };
        var (s, _) = Start("<DvSlider value=\"{Bind Level}\" onbeforecommit=\"Gate\" style=\"width:300\"/>", vm);
        s.PointerDown(At(Slider(s), 0.9f));
        s.PointerUp(At(Slider(s), 0.9f));
        s.Layout(W, H);
        Assert.Equal(50, vm.Level);
        Assert.Equal(50f, Slider(s).KnobValue); // 미리보기 버리고 VM 값으로
    }

    [Fact]
    public void Handle_Owned_Drag_And_Code()
    {
        var vm = new Vm();
        var (s, _) = Start("<DvSlider id=\"sl\" max=\"10\" step=\"0.5\" style=\"width:300\"/>", vm);
        var ev = new List<bool>();
        vm.sl!.ValueChanged += (_, e) => ev.Add(e.ByUser);
        s.PointerDown(At(Slider(s), 0.3f));
        s.PointerUp(At(Slider(s), 0.3f));
        Assert.Equal(3.0, vm.sl.Value);
        vm.sl.Value = 7.5;
        s.Layout(W, H);
        Assert.Equal(7.5f, Slider(s).KnobValue);
        Assert.Equal(new[] { true, false }, ev);
    }

    [Fact]
    public void Paints_ShowValue_Without_Throwing()
    {
        var (s, _) = Start("<DvSlider value=\"{Bind Level}\" show-value=\"true\" unit=\"%\" style=\"width:300\"/>" +
                           "<DvSlider value=\"{Bind Level}\" direction=\"up\" show-value=\"true\"/>", new Vm());
        using var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        Assert.Equal(64f, Slider(s).Rect.Height);
    }
}
