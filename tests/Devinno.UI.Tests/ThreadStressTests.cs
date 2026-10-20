using System.Collections.Concurrent;
using Devinno.UI.App;
using Devinno.UI.Controls;
using Devinno.UI.Styling;
using Devinno.UI.Trends;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

// P7-6 스레드 계약 — "어느 스레드에서나" 라고 문서에 적은 API 를 여러 스레드에서 동시에 두드리는 동안
// 테스트 스레드(= UI·입력 스레드)는 레이아웃·그리기·포인터를 계속 돌린다. 예외·HandlerError·멈춤이 없어야 한다.
[Collection("AttachedApp")]
public class ThreadStressTests(ITestOutputHelper output)
{
    private const int W = 800, H = 600;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(1500);

    private sealed class StressPage(string title) : DvPage, IDvControlHost
    {
        public DvToggle? tg; public DvLabel? lbl; public DvSlider? sl; public DvListBox? lst; public DvExpander? exp;
        public int Counter;
        public bool On { get; set; }
        public string Title => title;
        public TrendBuffer Buffer { get; } = new(2000);
        public void Bump() => Counter++;

        protected override string MarkupSource => """
            <DvScreen style="padding:12; gap:8">
              <DvLabel id="lbl" class="title" text="{Title}"/>
              <DvToggle id="tg"/>
              <DvToggle value="{Bind On}"/>
              <DvSlider id="sl" style="width:300"/>
              <DvButton text="눌러" variant="primary" onclick="Bump" style="transition: background-color 120ms" press-style="transform: scale(0.95)"/>
              <DvListBox id="lst"><DvOption value="a"/><DvOption value="b"/><DvOption value="c"/></DvListBox>
              <DvExpander id="exp" header="더보기"><DvLabel text="안"/></DvExpander>
              <DvTrend source="{Buffer}" window="5" style="height:120"><DvTrendChannel name="v"/></DvTrend>
            </DvScreen>
            """;

        public void Attach(DvControlLookup c)
        {
            tg = c.Bind(tg, "tg"); lbl = c.Bind(lbl, "lbl"); sl = c.Bind(sl, "sl"); lst = c.Bind(lst, "lst"); exp = c.Bind(exp, "exp");
        }
    }

    private sealed class Ask : DvDialog<string>
    {
        protected override string MarkupSource => "<DvDialogFrame title=\"확인\"><DvButton text=\"닫기\" variant=\"primary\"/></DvDialogFrame>";
    }

    [Fact]
    public void Requests_from_many_threads_while_the_ui_thread_renders()
    {
        var pages = Enumerable.Range(0, 3).Select(i => new StressPage("페이지 " + i)).ToArray();
        var app = new DvApp { Theme = DvTheme.Dark, PageTransition = DvPageTransition.Slide };
        foreach (var p in pages)
            app.Pages.Add(p);
        app.Show(pages[0]);
        var errors = new ConcurrentQueue<string>();
        app.HandlerError += (_, e) => errors.Enqueue($"HandlerError {e.Kind} {e.Member}: {e.Exception.Message}");

        using var stop = new CancellationTokenSource(Duration);
        var workers = Enumerable.Range(0, 4).Select(w => Task.Run(() =>
        {
            var rng = new Random(100 + w);
            var n = 0;
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    var page = pages[rng.Next(pages.Length)];
                    switch (rng.Next(14))
                    {
                        case 0: app.Show(page, (DvPageTransition)rng.Next(3)); break;
                        case 1: app.Invalidate(); break;
                        case 2: if (page.tg is { } tg) tg.Checked = rng.Next(2) == 0; break;
                        case 3: if (page.lbl is { } l) l.Text = "스레드 " + n; break;
                        case 4: if (page.tg is { } t2) t2.Enabled = rng.Next(4) != 0; break;
                        case 5: if (page.sl is { } sl) sl.Value = rng.Next(100); break;
                        case 6: if (page.lst is { } lst) lst.SelectedIndex = rng.Next(-1, 3); break;
                        case 7: if (page.exp is { } exp) exp.IsExpanded = !exp.IsExpanded; break;
                        case 8: app.Theme = rng.Next(2) == 0 ? DvTheme.Light : DvTheme.Dark; break;
                        case 9: _ = app.ShowDialog<Ask, string>(); break;
                        case 10: app.CloseDialogs(); break;
                        case 11: _ = app.MessageBox("메시지 " + n); break;
                        case 12: page.Buffer.Add("v", DateTime.Now, rng.NextDouble()); break;
                        case 13: page.On = !page.On; app.Invalidate(); break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue($"worker {w}: {ex.GetType().Name} {ex.Message}");
                }
                n++;
                if (n % 200 == 0) // 실제 앱처럼 숨 돌림 — UI 스레드가 프레임을 많이 돌아 겹침이 늘어난다
                    Thread.Sleep(1);
            }
            return n;
        })).ToArray();

        // UI 스레드 — 프레임·포인터
        var rngUi = new Random(7);
        var frames = 0;
        using var bmp = new SKBitmap(W, H);
        var t = 0L;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                app.Surface.Layout(W, H);
                using (var c = new SKCanvas(bmp))
                    app.Surface.RenderTo(c, W, H, t += 16);
                var p = new SKPoint(rngUi.Next(W), rngUi.Next(H));
                app.Surface.PointerMove(p);
                if (frames % 3 == 0)
                {
                    app.Surface.PointerDown(p);
                    app.Surface.PointerUp(p);
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue($"ui: {ex.GetType().Name} {ex.Message}\n{ex.StackTrace}");
            }
            frames++;
        }

        Assert.True(Task.WaitAll(workers, TimeSpan.FromSeconds(10)), "작업 스레드가 끝나지 않음(교착?)");
        // 남은 요청 정리 — 다이얼로그를 닫고 몇 프레임
        app.CloseDialogs();
        for (var i = 0; i < 30; i++)
        {
            app.Surface.Layout(W, H);
            using var c = new SKCanvas(bmp);
            app.Surface.RenderTo(c, W, H, t += 16);
        }
        output.WriteLine($"frames {frames}, worker ops {workers.Sum(w => w.Result)}, dialogs left {app.Dialogs.Count}");
        Assert.Empty(errors);
        Assert.Empty(app.Dialogs);
    }

    [Fact]
    public void Handle_writes_race_screen_taps_without_losing_consistency()
    {
        var page = new StressPage("핸들");
        var app = new DvApp();
        app.Pages.Add(page);
        app.Show(page);
        app.Surface.Layout(W, H);
        var events = 0;
        var errors = new ConcurrentQueue<string>();
        app.HandlerError += (_, e) => errors.Enqueue(e.Exception.Message);
        page.tg!.CheckedChanged += (_, _) => Interlocked.Increment(ref events);

        using var stop = new CancellationTokenSource(Duration);
        var writer = Task.Run(() =>
        {
            var rng = new Random(3);
            while (!stop.IsCancellationRequested)
            {
                try { page.tg.Checked = rng.Next(2) == 0; }
                catch (Exception ex) { errors.Enqueue(ex.Message); }
            }
        });

        using var bmp = new SKBitmap(W, H);
        var t = 0L;
        while (!stop.IsCancellationRequested)
        {
            app.Surface.Layout(W, H);
            var b = page.tg.Bounds;
            if (!b.IsEmpty)
            {
                app.Surface.PointerDown(new SKPoint(b.MidX, b.MidY));
                app.Surface.PointerUp(new SKPoint(b.MidX, b.MidY));
            }
            using var c = new SKCanvas(bmp);
            app.Surface.RenderTo(c, W, H, t += 16);
        }
        writer.Wait(TimeSpan.FromSeconds(5));

        // 쓰기가 멈춘 뒤 한 프레임 — 화면은 핸들 값과 같다
        app.Surface.Invalidate();
        app.Surface.Layout(W, H);
        var box = Flatten(page.Surface!.Root!).First(x => x.Node.Tag == "DvToggle");
        Assert.Equal(page.tg.Checked, box.ToggleOn);
        Assert.True(events > 0);
        Assert.Empty(errors);
    }

    private static IEnumerable<Devinno.UI.Layout.LayoutBox> Flatten(Devinno.UI.Layout.LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
}
