using Devinno.UI;
using SkiaSharp;
using Xunit;

public class DvSurfaceTests
{
    private sealed class Vm
    {
        public int Clicks;
        public void OnConfirm() => Clicks++;
    }

    [Fact]
    public void PointerDown_then_up_on_same_box_dispatches_click()
    {
        var vm = new Vm();
        var surface = new DvSurface(
            "<DvScreen style=\"padding:0\"><DvButton text=\"OK\" style=\"width:80; height:30\" onclick=\"OnConfirm\"/></DvScreen>",
            vm);
        surface.Layout(200, 100);

        surface.PointerDown(new SKPoint(10, 10));
        surface.PointerUp(new SKPoint(10, 10));
        Assert.Equal(1, vm.Clicks);

        surface.PointerDown(new SKPoint(10, 10));
        surface.PointerUp(new SKPoint(150, 90));
        Assert.Equal(1, vm.Clicks);
    }

    [Fact]
    public void Click_survives_relayout_between_down_and_up()
    {
        // 폴링 타이머가 누르는 동안 Invalidate 해도 클릭이 사라지면 안 된다
        var vm = new Vm();
        var surface = new DvSurface(
            "<DvScreen style=\"padding:0\"><DvButton text=\"OK\" style=\"width:80; height:30\" onclick=\"OnConfirm\"/></DvScreen>",
            vm);
        surface.Layout(200, 100);

        surface.PointerDown(new SKPoint(10, 10));
        surface.Invalidate();
        surface.Layout(200, 100);
        surface.PointerUp(new SKPoint(10, 10));
        Assert.Equal(1, vm.Clicks);
    }
}
