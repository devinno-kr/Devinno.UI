using System.Diagnostics;
using System.Reflection;
using Devinno.UI;
using Devinno.UI.Interaction;
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

    // 리뷰 M3 — 스타일 해석 경고도 공용 경고(화면마다 한 번). 전: StyleParser 의 프로세스 전체 static 집합 — 두 번째 화면은 알리지 않았다
    [Fact]
    public void Style_TwoSurfaces_EachWarn_SameSurfaceOnce()
    {
        const string markup = "<DvScreen><DvBox style=\"transform:m3style-x\"/></DvScreen>";
        var n = Count("m3style-x", () =>
        {
            var s = Show(markup);
            Rebuild(s);
            Show(markup);
        });
        Assert.Equal(2, n);
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

    public sealed class WordVm
    {
        public string Word { get; set; } = "";
    }

    private static HashSet<string> WarnedOf(DvSurface s) =>
        ((HandlerErrors.ReadScope)typeof(DvSurface).GetField("_reads", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Warned;

    // 리뷰 I2 — 바인딩 값이 바뀔 때마다 새 문구가 되어 집합이 끝없이 커지던 것: 열쇠 = (태그, 속성, 마크업 원문). 값이 100번 바뀌어도 경고 1번·집합 1개
    [Fact]
    public void BoundBadValue_Changing_WarnsOnce_AndKeepsOneKey()
    {
        var vm = new WordVm { Word = "i2bound-0" };
        DvSurface? s = null;
        var n = Count("i2bound-", () =>
        {
            s = new DvSurface("<DvScreen><DvLamp on=\"{Word}\"/></DvScreen>", vm, DvTheme.Light);
            s.Layout(800, 500);
            for (var i = 1; i < 100; i++)
            {
                vm.Word = "i2bound-" + i;
                Rebuild(s);
            }
        });
        Assert.Equal(1, n);
        Assert.Single(WarnedOf(s!));
    }

    // 리터럴은 원문 = 값 — 값마다 한 번(두 리터럴 = 두 번)
    [Fact]
    public void LiteralBadValues_WarnOncePerValue()
    {
        var n = Count("i2lit-", () => Show("<DvScreen><DvLamp on=\"i2lit-a\"/><DvLamp on=\"i2lit-b\"/><DvLamp on=\"i2lit-a\"/></DvScreen>"));
        Assert.Equal(2, n);
    }

    // 상한 — 집합이 MaxWarned 에 닿으면 더 기록·출력하지 않고 "too many" 한 줄만
    [Fact]
    public void WarnedSet_IsCapped_WithOneFinalLine()
    {
        var scope = new HandlerErrors.ReadScope(null);
        var prev = HandlerErrors.EnterRead(scope);
        int n, tooMany;
        try
        {
            var all = new Capture("i2cap-");
            var cap = new Capture("too many");
            Trace.Listeners.Add(all);
            Trace.Listeners.Add(cap);
            try
            {
                for (var i = 0; i < Warnings.MaxWarned + 50; i++)
                    Warnings.Warn("Devinno.UI: i2cap-" + i);
            }
            finally
            {
                Trace.Listeners.Remove(all);
                Trace.Listeners.Remove(cap);
            }

            n = all.Lines.Count;
            tooMany = cap.Lines.Count;
        }
        finally
        {
            HandlerErrors.ExitRead(prev);
        }

        Assert.Equal(Warnings.MaxWarned, n);
        Assert.Equal(1, tooMany);
        Assert.Equal(Warnings.MaxWarned, scope.Warned.Count);
    }
}
