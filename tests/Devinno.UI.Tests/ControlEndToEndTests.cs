using Devinno.UI;
using Devinno.UI.Controls;
using SkiaSharp;
using Xunit;

namespace ControlEndToEnd
{
    // 실제 Pages/ControlDemo.dvml 에서 생성기가 만든 부분(Markup·필드·Attach)과 사용자 부분이 합쳐진 VM.
    public partial class ControlDemoVm
    {
        public int Loaded;
        public int Starts;
        public DvButton StartHandle => btnStart;
        public DvLabel StateHandle => lblState;

        partial void OnLoaded() => Loaded++;

        public void Start()
        {
            Starts++;
            btnStart.Enabled = false;
            lblState.Text = "가동 중";
        }

        public void Stop() { }
    }
}

// 생성기 → 컴파일 → DvSurface 끝-끝: 클릭하면 VM 코드가 핸들로 컨트롤을 바꾼다.
public class ControlEndToEndTests
{
    private static SKPoint Tap(DvSurface s, SKRect r)
    {
        var p = new SKPoint(r.MidX, r.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(400, 300);
        return p;
    }

    [Fact]
    public void GeneratedVm_AttachesOnce_AndHandlesTapToDisableButton()
    {
        var vm = new ControlEndToEnd.ControlDemoVm();
        var s = new DvSurface(ControlEndToEnd.ControlDemoVm.Markup, vm);
        s.Layout(400, 300);

        Assert.Equal(1, vm.Loaded);
        Assert.False(vm.StartHandle.Bounds.IsEmpty);

        Tap(s, vm.StartHandle.Bounds);

        Assert.Equal(1, vm.Starts);
        Assert.False(vm.StartHandle.Enabled);
        Assert.Equal("가동 중", vm.StateHandle.Text);
        Assert.Equal(1, vm.Loaded);
    }

    [Fact]
    public void GeneratedVm_ReattachedToNewSurface_RunsOnLoadedOnlyOnce()
    {
        var vm = new ControlEndToEnd.ControlDemoVm();
        _ = new DvSurface(ControlEndToEnd.ControlDemoVm.Markup, vm);
        var s2 = new DvSurface(ControlEndToEnd.ControlDemoVm.Markup, vm);
        s2.Layout(400, 300);
        Assert.Equal(1, vm.Loaded);
    }
}
