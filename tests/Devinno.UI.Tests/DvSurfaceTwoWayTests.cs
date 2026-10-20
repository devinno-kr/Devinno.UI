using Devinno.UI;
using SkiaSharp;
using Xunit;

public class DvSurfaceTwoWayTests
{
    private sealed class Vm
    {
        public bool Flag { get; set; }
    }

    [Fact]
    public void Click_toggle_writes_back_to_vm()
    {
        var vm = new Vm { Flag = false };
        var surface = new DvSurface(
            "<DvScreen style=\"padding:0\"><DvToggle value=\"{Bind Data.Flag}\" style=\"width:44; height:24\"/></DvScreen>",
            vm);
        surface.Layout(100, 100);

        surface.PointerDown(new SKPoint(10, 10));
        surface.PointerUp(new SKPoint(10, 10));

        Assert.True(vm.Flag);
    }
}
