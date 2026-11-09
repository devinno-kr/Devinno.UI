using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// R14 — 경고는 화면(DvSurface)마다 (태그, 속성, 값) 한 번. 다른 화면은 다시 알리고, 같은 화면의 재빌드는 다시 알리지 않는다.
// 값마다 고유한 표시 글자를 써서 다른 테스트의 경고와 섞이지 않게 한다.
public class WarningScopeTests
{
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

    private static int Count(string marker, Action act)
    {
        var cap = new Capture(marker);
        Trace.Listeners.Add(cap);
        try
        {
            act();
        }
        finally
        {
            Trace.Listeners.Remove(cap);
        }

        lock (cap.Lines)
            return cap.Lines.Count;
    }

    private static DvSurface Show(string markup)
    {
        var s = new DvSurface(markup, null, DvTheme.Light);
        s.Layout(800, 500);
        using var bmp = new SKBitmap(800, 500);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, 800, 500, 0);
        return s;
    }

    private static void Rebuild(DvSurface s)
    {
        s.Invalidate();
        s.Layout(800, 500);
    }

    [Fact]
    public void Widget_SameSurfaceRebuild_WarnsOnce()
    {
        var n = Count("knobmode-w14a", () =>
        {
            var s = Show("<DvScreen><DvKnob input-mode=\"knobmode-w14a\"/></DvScreen>");
            Rebuild(s);
            Rebuild(s);
        });
        Assert.Equal(1, n);
    }

    [Fact]
    public void Widget_TwoSurfaces_EachWarn()
    {
        var n = Count("slidedir-w14b", () =>
        {
            Show("<DvScreen><DvSlider direction=\"slidedir-w14b\"/></DvScreen>");
            Show("<DvScreen><DvSlider direction=\"slidedir-w14b\"/></DvScreen>");
        });
        Assert.Equal(2, n);
    }

    [Fact]
    public void Process_TwoSurfaces_EachWarn()
    {
        var n = Count("look-w14c", () =>
        {
            Show("<DvScreen><DvProcess look=\"look-w14c\"><DvPump name=\"P1\"/></DvProcess></DvScreen>");
            Show("<DvScreen><DvProcess look=\"look-w14c\"><DvPump name=\"P1\"/></DvProcess></DvScreen>");
        });
        Assert.Equal(2, n);
    }

    [Fact]
    public void Process_SameSurfaceRebuild_WarnsOnce()
    {
        var n = Count("look-w14d", () =>
        {
            var s = Show("<DvScreen><DvProcess look=\"look-w14d\"><DvPump name=\"P1\"/></DvProcess></DvScreen>");
            Rebuild(s);
        });
        Assert.Equal(1, n);
    }

    [Fact]
    public void Grid_TwoSurfaces_EachWarn()
    {
        const string markup = "<DvScreen><DvDataGrid><DvColumn field=\"A\" filter=\"filter-w14e\"/></DvDataGrid></DvScreen>";
        var n = Count("filter-w14e", () =>
        {
            Show(markup);
            Show(markup);
        });
        Assert.Equal(2, n);
    }

    [Fact]
    public void Grid_SameSurfaceRebuild_WarnsOnce()
    {
        const string markup = "<DvScreen><DvDataGrid header-color=\"color-w14f\"><DvColumn field=\"A\"/></DvDataGrid></DvScreen>";
        var n = Count("color-w14f", () =>
        {
            var s = Show(markup);
            Rebuild(s);
        });
        Assert.Equal(1, n);
    }

    [Fact]
    public void ReloadedMarkup_WarnsAgain()
    {
        const string markup = "<DvScreen><DvKnob input-mode=\"knobmode-w14g\"/></DvScreen>";
        var n = Count("knobmode-w14g", () =>
        {
            var s = Show(markup);
            Assert.Null(s.ReloadMarkup(markup));
            s.Layout(800, 500);
        });
        Assert.Equal(2, n);
    }
}
