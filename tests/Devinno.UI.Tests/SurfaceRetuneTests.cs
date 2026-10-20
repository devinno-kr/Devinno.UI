using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 실행 중 테마·레지스트리 교체(상태 유지)와 깨우기 훅
public class SurfaceRetuneTests
{
    public sealed class Vm : IDvControlHost
    {
        public DvButton? btn;
        public string Name { get; set; } = "x";
        public void Attach(DvControlLookup c) => btn = c.Bind(btn, "btn");
    }

    private const string Page =
        "<DvScreen style=\"padding:0; gap:0; background:var(--bg)\">" +
        "<DvInput id=\"a\" value=\"{Bind Data.Name}\" style=\"width:300; height:40\"/>" +
        "<DvBox style=\"overflow:scroll; width:300; height:100; padding:0; gap:0\">" +
        "<DvBox style=\"height:150\"/><DvBox style=\"height:150\"/></DvBox>" +
        "<DvButton id=\"btn\" text=\"A\"/></DvScreen>";

    private static LayoutBox? Box(DvSurface s, string tag) => LayoutQuery.FindByTag(s.Root!, tag);

    private static DvColor? Bg(DvSurface s) => s.Root!.Style.Background;

    [Fact]
    public void SetTheme_KeepsScrollAndInput()
    {
        var vm = new Vm();
        var s = new DvSurface(Page, vm, DvTheme.Dark) { UseNativeEditor = true };
        s.Layout(400, 300);
        var dark = Bg(s);
        s.PointerWheel(new SKPoint(10, 60), -50f / 48f);
        var scroller = s.Root!.Children[1];
        Assert.Equal(50, scroller.ScrollY, 3);
        s.PointerDown(new SKPoint(50, 20)); s.PointerUp(new SKPoint(50, 20));
        s.Layout(400, 300);
        Assert.True(s.Edit.IsEditing);
        s.SetEditText(s.Edit.SessionId, "abc");
        vm.btn!.Enabled = false;

        s.SetTheme(DvTheme.Light);
        s.Layout(400, 300);

        Assert.NotEqual(dark, Bg(s));
        Assert.True(DvTheme.Light.TryGet("--bg", out var raw)); Assert.True(DvColor.TryParse(raw, out var light));
        Assert.Equal(light, Bg(s));
        Assert.Equal(50, s.Root!.Children[1].ScrollY, 3);
        Assert.True(s.Edit.IsEditing);
        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.Equal("abc", vm.Name);
        Assert.True(Box(s, "DvButton")!.Disabled);
    }

    [Fact]
    public void SetRegistry_RebuildsComponents()
    {
        static ComponentRegistry Reg(string t)
        {
            var r = new ComponentRegistry();
            r.Register($"<DvComponent name=\"Tag\"><DvBox><DvLabel text=\"{t}\"/></DvBox></DvComponent>");
            return r;
        }
        var s = new DvSurface("<DvScreen><Tag/></DvScreen>", new object(), null, Reg("v1"));
        s.Layout(200, 200);
        Assert.Equal("v1", s.Root!.Children[0].Children[0].ResolvedText);

        s.SetRegistry(Reg("v2"));
        s.Layout(200, 200);

        Assert.Equal("v2", s.Root!.Children[0].Children[0].ResolvedText);
    }

    [Fact]
    public void Invalidate_CallsWake_FromAnyThread()
    {
        var s = new DvSurface("<DvScreen/>", new object());
        s.Layout(100, 100);
        var n = 0;
        s.Wake = () => Interlocked.Increment(ref n);
        var t = new Thread(s.Invalidate); t.Start(); t.Join();
        Assert.Equal(1, n);
        s.Repaint();
        Assert.Equal(2, n);
    }

    [Fact]
    public void HandleWrite_CallsWake()
    {
        var vm = new Vm();
        var s = new DvSurface(Page, vm);
        s.Layout(400, 300);
        var n = 0;
        s.Wake = () => Interlocked.Increment(ref n);
        vm.btn!.Text = "x";
        Assert.Equal(1, n);
    }
}
