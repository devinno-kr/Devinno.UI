using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Devinno.UI.App;
using Devinno.UI.Controls;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;
using SkiaSharp;

// P7-7 장시간 스모크 — 데모 페이지를 실제 OpenTK 창에 띄우고 사람이 쓰듯 계속 조작한다.
//   dotnet run -c Release --project tools/Smoke -- --tag 이름 --hours 8.5 --out 폴더 [--gpu]
// 1분마다 지표(CSV), 시작 1분 뒤 기준값 + 1시간마다 요약(텍스트)을 남긴다. 실행 중에는 절전에 들지 않게 한다.
internal static class Program
{
    private static readonly ConcurrentQueue<Action> Ui = new(); // 창 스레드에서 할 일
    private static readonly object FrameGate = new();
    private static List<double> _frames = new();
    private static long _framesTotal, _actions, _errors;
    private static string? _lastError;

    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);

    private static int Main(string[] args)
    {
        string Arg(string name, string dflt) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault() ?? dflt;
        var tag = Arg("--tag", "run");
        var hours = double.Parse(Arg("--hours", "8"), CultureInfo.InvariantCulture);
        var outDir = Path.GetFullPath(Arg("--out", "smoke-out"));
        var gpu = args.Contains("--gpu");
        Directory.CreateDirectory(outDir);
        var txt = Path.Combine(outDir, tag + ".txt");
        var csv = Path.Combine(outDir, tag + ".csv");
        var images = MakeImages(Path.Combine(outDir, "images"), 300);

        SetThreadExecutionState(0x80000000 | 0x00000001 | 0x00000002); // ES_CONTINUOUS | SYSTEM | DISPLAY — 끝나면 프로세스와 함께 풀린다
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            File.AppendAllText(txt, $"[{DateTime.Now:HH:mm}] !! 비정상 종료: {e.ExceptionObject}\n");

        var dashboard = new DashboardPage();
        var components = new ComponentDashboardPage();
        var grid = new GridPage();
        var trend = new TrendPage();
        var alarm = new AlarmPage();
        var knob = new KnobPage();
        var process = new ProcessPage();
        var basic = new BasicPage();
        var imagePage = new SmokeImagePage(images);
        var pages = new DvPage[] { basic, dashboard, grid, trend, imagePage, alarm, process, components, knob };

        var registry = new Devinno.UI.Components.ComponentRegistry();
        ProcessPage.Register(registry);
        var app = new DvApp { Registry = registry, Theme = DvTheme.Dark };
        foreach (var p in pages)
            app.Pages.Add(p);
        trend.Seed(DateTime.Now.AddSeconds(-120), 120);
        app.Show(basic);

        using var window = new ViewWindow(app, 1360, 800, $"Devinno.UI Smoke — {tag}") { Gpu = gpu };
        window.FrameRendered = ms =>
        {
            lock (FrameGate)
            {
                _frames.Add(ms);
                _framesTotal++;
            }
        };
        window.HandlerError += (_, e) =>
        {
            Interlocked.Increment(ref _errors);
            _lastError = $"{e.Kind} {e.Member} {e.Exception.GetType().Name}: {e.Exception.Message}";
        };
        window.RenderFrame += _ =>
        {
            while (Ui.TryDequeue(out var a))
            {
                try { a(); Interlocked.Increment(ref _actions); }
                catch (Exception ex) { Interlocked.Increment(ref _errors); _lastError = "driver: " + ex.Message; }
            }
        };

        var start = DateTime.Now;
        var end = start.AddHours(hours);
        var proc = Process.GetCurrentProcess();
        File.WriteAllText(txt,
            $"Devinno.UI 장시간 스모크 — {tag}\n" +
            $"시작 {start:yyyy-MM-dd HH:mm:ss}, 예정 종료 {end:yyyy-MM-dd HH:mm} ({hours}시간), 창 1360×800 {(gpu ? "GPU" : "CPU")}, " +
            $"커밋 {Git("rev-parse --short HEAD")}\n" +
            "동작: 20초마다 페이지 전환(9쪽), 0.5초마다 트렌드·그리드·공정도 값 갱신과 이미지 경로 바꿈(300장 순환), 기본 컨트롤 누르기(3초), " +
            "1분마다 메시지 박스 열고 닫기, 10분마다 테마 바꿈.\n" +
            "열: 작업 메모리 / 전용 메모리 / 관리 힙 / OS 핸들 / GDI·USER 개체 / 스레드 / 2세대 GC 수 / 그 시간 동안 그린 프레임(평균·p99·최대 ms) / 핸들러 오류 / 실행한 동작\n\n");
        File.WriteAllText(csv, "time,elapsed_min,working_mb,private_mb,managed_mb,handles,gdi,user,threads,gc2,frames,frame_avg_ms,frame_p99_ms,frame_max_ms,errors,actions\n");

        // 조작 — 0.5초마다 창 스레드에 할 일을 넣고 깨운다
        var tick = 0L;
        var pageIndex = 0;
        var rng = new Random(20261020);
        using var driver = new System.Threading.Timer(_ =>
        {
            var n = Interlocked.Increment(ref tick);
            Ui.Enqueue(() =>
            {
                trend.Sample(DateTime.Now);
                grid.Tick();
                process.Tick();
                imagePage.Next();
            });
            if (n % 40 == 0) // 20초
                Ui.Enqueue(() => app.Show(pages[pageIndex = (pageIndex + 1) % pages.Length]));
            if (n % 6 == 0 && ReferenceEquals(pages[pageIndex], basic))
                Ui.Enqueue(() => PokeBasic(app, basic, rng));
            if (n % 120 == 0) // 1분 — 다이얼로그 열고 2초 뒤 닫기
                Ui.Enqueue(() => _ = app.MessageBox("스모크 — 다이얼로그 수명 확인", "Smoke"));
            if (n % 120 == 4)
                Ui.Enqueue(app.CloseDialogs);
            if (n % 1200 == 0) // 10분 — 테마
                Ui.Enqueue(() => app.Theme = n / 1200 % 2 == 1 ? DvTheme.Light : DvTheme.Dark);
            if (DateTime.Now >= end)
                Ui.Enqueue(window.Close);
            app.Invalidate(); // 창 깨우기(값이 바뀌었으니 다시 만들기)
        }, null, 500, 500);

        // 지표 — 1분마다 CSV, 시작 1분 뒤와 1시간마다 요약
        Sample? baseline = null;
        var nextHour = 1;
        var samples = new List<Sample>();
        using var meter = new System.Threading.Timer(_ =>
        {
            var s = Measure(proc, start);
            lock (samples)
            {
                samples.Add(s);
                File.AppendAllText(csv, s.Csv() + "\n");
                if (baseline is null)
                {
                    baseline = s;
                    File.AppendAllText(txt, s.Line("기준(1분)", null) + "\n");
                }
                else if (s.Elapsed.TotalHours >= nextHour)
                {
                    File.AppendAllText(txt, s.Line($"{nextHour}시간", baseline) + "\n");
                    nextHour++;
                }
            }
        }, null, 60_000, 60_000);

        window.Run();

        var last = Measure(proc, start);
        File.AppendAllText(txt, last.Line("종료", baseline) + "\n");
        File.AppendAllText(txt, Trend(samples));
        if (_lastError is not null)
            File.AppendAllText(txt, $"마지막 오류: {_lastError}\n");
        return 0;
    }

    // 기본 컨트롤 탭 — 탭 머리글을 돌고, 그 탭에서 핸들이 있는 위젯을 누른다(Bounds 는 창 좌표 = 루트 화면 좌표)
    private static readonly string[][] BasicTargets =
    {
        new[] { "tgPump", "tgAuto", "btnCode", "btnReset" },
        new[] { "chkLog", "slLight", "rdStop" },
        new[] { "lstShift" },
        new[] { "tvPlant" },
        new[] { "expMore" },
        Array.Empty<string>(),
    };
    private static readonly float[] TabX = { 65, 205, 340, 420, 500, 580 };
    private static int _basicStep;

    private static void PokeBasic(DvApp app, BasicPage page, Random rng)
    {
        var s = app.Surface;
        var step = _basicStep++;
        if (step % 4 == 0)
        {
            Tap(s, new SKPoint(TabX[step / 4 % TabX.Length], 104));
            return;
        }

        var names = BasicTargets[step / 4 % BasicTargets.Length];
        if (names.Length == 0)
            return;
        var name = names[rng.Next(names.Length)];
        if (typeof(BasicPage).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(page) is not DvControl c)
            return;
        var b = c.Bounds;
        if (b.IsEmpty)
            return;
        // 목록·트리는 안쪽 첫 몇 행 자리, 그 밖은 가운데(슬라이더는 아무 데나)
        var p = name is "lstShift" or "tvPlant"
            ? new SKPoint(b.Left + 40 + rng.Next(80), b.Top + 24 + 42 * rng.Next(3))
            : name == "slLight" ? new SKPoint(b.Left + 14 + rng.Next((int)Math.Max(1, b.Width - 28)), b.Bottom - 20) : new SKPoint(b.MidX, b.MidY);
        Tap(s, p);
    }

    private static void Tap(Devinno.UI.DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
    }

    private static string[] MakeImages(string dir, int count)
    {
        Directory.CreateDirectory(dir);
        var paths = new string[count];
        using var font = new SKFont(SKTypeface.Default, 96);
        for (var i = 0; i < count; i++)
        {
            paths[i] = Path.Combine(dir, $"img_{i:000}.png");
            if (File.Exists(paths[i]))
                continue;
            using var bmp = new SKBitmap(512, 512);
            using (var c = new SKCanvas(bmp))
            {
                using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(512, 512),
                    new[] { SKColor.FromHsv(i * 7 % 360, 60, 90), SKColor.FromHsv((i * 7 + 120) % 360, 70, 60) }, SKShaderTileMode.Clamp);
                using var paint = new SKPaint { Shader = shader };
                c.DrawRect(0, 0, 512, 512, paint);
                using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
                c.DrawText(i.ToString(CultureInfo.InvariantCulture), 256, 290, SKTextAlign.Center, font, ink);
            }
            using var f = File.Create(paths[i]);
            bmp.Encode(f, SKEncodedImageFormat.Png, 80);
        }
        return paths;
    }

    private static Sample Measure(Process proc, DateTime start)
    {
        proc.Refresh();
        List<double> frames;
        long total;
        lock (FrameGate)
        {
            frames = _frames;
            _frames = new List<double>();
            total = _framesTotal;
        }
        frames.Sort();
        return new Sample(DateTime.Now, DateTime.Now - start,
            proc.WorkingSet64 / 1048576.0, proc.PrivateMemorySize64 / 1048576.0, GC.GetTotalMemory(false) / 1048576.0,
            proc.HandleCount, GetGuiResources(proc.Handle, 0), GetGuiResources(proc.Handle, 1), proc.Threads.Count,
            GC.CollectionCount(2), frames.Count, total,
            frames.Count == 0 ? 0 : frames.Average(), frames.Count == 0 ? 0 : frames[(int)Math.Min(frames.Count - 1, frames.Count * 0.99)],
            frames.Count == 0 ? 0 : frames[^1], Interlocked.Read(ref _errors), Interlocked.Read(ref _actions));
    }

    // 전용 메모리 기울기 — 첫 1시간을 뺀(준비 단계 제외) 나머지 표본의 최소제곱 직선(MB/시간)
    private static string Trend(List<Sample> all)
    {
        List<Sample> s;
        lock (all)
            s = all.Where(x => x.Elapsed.TotalHours >= 1).ToList();
        if (s.Count < 30)
            return "추세: 1시간 이후 표본이 부족해 계산하지 않음\n";
        double Slope(Func<Sample, double> f)
        {
            var xs = s.Select(x => x.Elapsed.TotalHours).ToArray();
            var ys = s.Select(f).ToArray();
            double mx = xs.Average(), my = ys.Average();
            var num = xs.Zip(ys, (x, y) => (x - mx) * (y - my)).Sum();
            var den = xs.Sum(x => (x - mx) * (x - mx));
            return den == 0 ? 0 : num / den;
        }
        return $"추세(1시간 이후, 시간당): 전용 메모리 {Slope(x => x.PrivateMb):+0.0;-0.0} MB, 작업 메모리 {Slope(x => x.WorkingMb):+0.0;-0.0} MB, " +
               $"관리 힙 {Slope(x => x.ManagedMb):+0.0;-0.0} MB, OS 핸들 {Slope(x => x.Handles):+0.0;-0.0}, GDI {Slope(x => x.Gdi):+0.0;-0.0}, USER {Slope(x => x.User):+0.0;-0.0}\n";
    }

    private static string Git(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
            return p.StandardOutput.ReadToEnd().Trim();
        }
        catch (Exception) { return "?"; }
    }

    private sealed record Sample(DateTime At, TimeSpan Elapsed, double WorkingMb, double PrivateMb, double ManagedMb,
        int Handles, uint Gdi, uint User, int Threads, int Gc2, int Frames, long FramesTotal,
        double FrameAvg, double FrameP99, double FrameMax, long Errors, long Actions)
    {
        public string Csv() => string.Join(",", At.ToString("HH:mm:ss"), Elapsed.TotalMinutes.ToString("0", CultureInfo.InvariantCulture),
            F(WorkingMb), F(PrivateMb), F(ManagedMb), Handles, Gdi, User, Threads, Gc2, Frames, F(FrameAvg), F(FrameP99), F(FrameMax), Errors, Actions);

        public string Line(string label, Sample? b)
        {
            string D(double now, double? was) => was is { } w ? $" ({now - w:+0.0;-0.0})" : "";
            var sb = new StringBuilder();
            sb.Append($"[{At:HH:mm}] {label,-8} 경과 {(int)Elapsed.TotalHours}h{Elapsed.Minutes:00}m | ");
            sb.Append($"작업 {WorkingMb:0.0}MB{D(WorkingMb, b?.WorkingMb)} | 전용 {PrivateMb:0.0}MB{D(PrivateMb, b?.PrivateMb)} | 관리 힙 {ManagedMb:0.0}MB{D(ManagedMb, b?.ManagedMb)} | ");
            sb.Append($"핸들 {Handles}{D(Handles, b?.Handles)} | GDI {Gdi} USER {User} | 스레드 {Threads} | GC2 {Gc2} | ");
            sb.Append($"프레임(최근 1분) {Frames} 평균 {FrameAvg:0.0}ms p99 {FrameP99:0.0}ms 최대 {FrameMax:0.0}ms, 누적 {FramesTotal:N0} | 오류 {Errors} | 동작 {Actions:N0}");
            return sb.ToString();
        }

        private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}

/// <summary>DvImage 경로를 계속 바꾸는 쪽 — 이미지 캐시가 쌓이는지 본다(300장 순환, 한 장 디코드 512×512 ≈ 1MB).</summary>
internal sealed class SmokeImagePage(string[] images) : DvPage
{
    private int _i;
    public string Src1 => images[_i % images.Length];
    public string Src2 => images[(_i + 1) % images.Length];
    public string Src3 => images[(_i + 2) % images.Length];
    public string Caption => $"이미지 {_i % images.Length:000} ~ {(_i + 2) % images.Length:000} / {images.Length}";

    public void Next() => _i++;

    protected override string MarkupSource => """
        <DvPageFrame title="이미지 (스모크)" subtitle="{Caption}">
          <DvRow gap="md">
            <DvImage src="{Src1}" style="width:256; height:256"/>
            <DvImage src="{Src2}" style="width:256; height:256"/>
            <DvImage src="{Src3}" style="width:256; height:256"/>
          </DvRow>
        </DvPageFrame>
        """;
}
