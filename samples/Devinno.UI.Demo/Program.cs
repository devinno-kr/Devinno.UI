using Devinno.UI.App;
using Devinno.UI.Localization;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

// 데모 — DvApp 하나에 페이지를 전부 넣고, 실행 인자로 시작 페이지를 고른다.
//   인자 없음 = 쇼케이스(영어): 상단 바 + Overview(운영 대시보드)·Process(P&ID)·Widgets(아래 페이지 목록), PlantSim 이 값을 움직인다.
//   -- shot <폴더> = 쇼케이스 화면을 창 없이 PNG 로(README 이미지), anim 을 더하면 P&ID 흐름 애니메이션 프레임도
//   dotnet run --project samples/Devinno.UI.Demo [-- gpu] [-- dashboard|lang|control|equipment|grid|trend [fast]|alarm|input|knob|neon|process|basic|v2] [-- light]
//   시나리오: -- top = 상단 바(모니터링·설정·알람) + 홈 + 각 페이지의 "이전",  -- nav = 왼쪽 사이드바,
//            -- dialog = 레시피 편집 다이얼로그 + 그 위 메시지 박스 + 통신 시뮬레이션 알람(DvApp.Current, 키 중복 방지)
internal static class Program
{
    private static void Main(string[] args)
    {
        var theme = args.Contains("light") ? DvTheme.Light : DvTheme.Dark;
        string[] scenarios = ["top", "nav", "dialog", "lang", "control", "equipment", "grid", "trend", "alarm", "input", "knob", "basic", "process", "neon", "v2", "dashboard"];
        var showcase = !args.Any(scenarios.Contains);
        // 데모 화면은 모두 영어 — 언어 전환 페이지(lang)가 ko·en·ja 를 바꿀 수 있게 그 페이지의 표를 앱 전체에 쓰고, 영어로 시작한다
        var strings = WithLanguage(LangPage.CreateStrings(), "en");

        var dashboard = new DashboardPage();
        var components = new ComponentDashboardPage();
        var control = new ControlPage();
        var lang = new LangPage(strings);
        var equipment = new EquipmentPage();
        var grid = new GridPage();
        var trend = new TrendPage();
        var fastTrend = new FastTrendPage();
        var home = new HomePage();
        var alarm = new AlarmPage();
        var input = new InputPage();
        var knob = new KnobPage();
        var neon = new NeonPage();
        var dialog = new DialogPage();
        var process = new ProcessPage();
        var basic = new BasicPage();
        var motionNext = new MotionNextPage();

        var sim = new PlantSim();
        var overview = new OverviewPage(sim);
        var plantProcess = new PlantProcessPage(sim);
        var widgets = new WidgetsPage(
        [
            new("fa-toggle-on", "Basic controls", "Buttons, check boxes, radios, sliders, tabs, expanders, tree and charts", basic),
            new("fa-keyboard", "Input", "Text and number input, virtual keyboard, select, date and color pickers", input),
            new("fa-table", "Data grid", "Sorting, filters, edit cells, lamps and buttons in cells, summary rows", grid),
            new("fa-chart-line", "Trend", "Real-time trend from a TrendBuffer, two minutes of history", trend),
            new("fa-wave-square", "Fast trend", "1 ms samples drawn as they arrive", fastTrend),
            new("fa-bell", "Alarms", "ISA-18.2 style banner and list with acknowledge", alarm),
            new("fa-sliders", "Knobs and gauges", "Knob with drag or keypad input, instruments", knob),
            new("fa-window-restore", "Dialogs", "Your own dialogs, message box, built-in input boxes", dialog),
            new("fa-diagram-project", "Process diagrams", "Equipment and connections only; layout and piping by the library", process),
            new("fa-list", "Scrolling list", "DvRepeat over a VM collection with inertial scrolling", equipment),
            new("fa-language", "Localization", "Switch the string table at run time", lang),
            new("fa-code", "Controls from code", "Typed handles generated from id", control),
            new("fa-table-columns", "Semantic components", "Cards, stats, badges and status items from the theme", components),
            new("fa-palette", "Custom styling", "Gradients, glow shadows and absolute layout", neon),
        ]);

        var registry = new Devinno.UI.Components.ComponentRegistry();
        ProcessPage.Register(registry); // 공정도 유닛 컴포넌트(Blender)
        var app = new DvApp { Strings = strings, Registry = registry };
#if DEBUG
        app.HotReload = true; // 개발 중 — dvml 을 저장하면 실행 중인 화면에 바로 반영(배포 빌드에서는 꺼짐)
#endif
        foreach (var page in new DvPage[] { overview, plantProcess, widgets, home, dashboard, components, control, lang, equipment, grid, trend, fastTrend, alarm, input, knob, neon, dialog, process, basic, motionNext })
            app.Pages.Add(page);

        // 시작 페이지 · 창 크기 · 제목 · 테마(null = 기본 테마)
        var fast = args.Contains("trend") && args.Contains("fast");
        var (start, width, height, title, appTheme) =
            args.Contains("top") ? ((DvPage)home, HomePage.Width, HomePage.Height + 56, "Devinno.UI Demo — Top bar", (DvTheme?)theme)
            : args.Contains("nav") ? (components, 1400, 820, "Devinno.UI Demo — Side bar", theme)
            : args.Contains("dialog") ? (dialog, DialogPage.Width, DialogPage.Height, "Devinno.UI Demo — Dialog", theme)
            : args.Contains("lang") ? (lang, LangPage.Width, LangPage.Height, "Devinno.UI Demo — Localization", (DvTheme?)theme)
            : args.Contains("control") ? (control, ControlPage.Width, ControlPage.Height, "Devinno.UI Demo — Control access", theme)
            : args.Contains("equipment") ? (equipment, 1100, 680, "Devinno.UI Demo — Equipment", theme)
            : args.Contains("grid") ? (grid, GridPage.Width, GridPage.Height, "Devinno.UI Demo — DataGrid", theme)
            : fast ? (fastTrend, FastTrendPage.Width, FastTrendPage.Height, "Devinno.UI Demo — Fast Trend", theme)
            : args.Contains("trend") ? (trend, TrendPage.Width, TrendPage.Height, "Devinno.UI Demo — Trend", theme)
            : args.Contains("alarm") ? (alarm, AlarmPage.Width, AlarmPage.Height, "Devinno.UI Demo — Alarm", theme)
            : args.Contains("input") ? (input, InputPage.Width, InputPage.Height, "Devinno.UI Demo — Input", theme)
            : args.Contains("knob") ? (knob, KnobPage.Width, KnobPage.Height, "Devinno.UI Demo — Knob", DvTheme.Dark)
            : args.Contains("basic") ? (basic, BasicPage.Width, BasicPage.Height, "Devinno.UI Demo — Basic controls", theme)
            : args.Contains("process") ? (process, ProcessPage.Width, ProcessPage.Height, "Devinno.UI Demo — Process", theme)
            : args.Contains("neon") ? (neon, NeonPage.Width, NeonPage.Height, "Devinno.UI Demo — Neon", null)
            : args.Contains("v2") ? (components, ComponentDashboardPage.Width, ComponentDashboardPage.Height, "Devinno.UI Demo — Components", theme)
            : args.Contains("dashboard") ? (dashboard, DashboardPage.Width, DashboardPage.Height, "Devinno.UI Demo — Dashboard", null)
            : (overview, 1440, 900, "Devinno.UI Demo", theme);

        if (args.Contains("top"))
        {
            app.Top = new MainTopBar();
            components.BackTo = knob.BackTo = alarm.BackTo = home; // 각 페이지의 "Back" → 홈
        }
        else if (args.Contains("nav"))
            app.Left = new MainSideBar();
        else if (showcase)
        {
            app.Top = new ShowcaseBar(sim);
            foreach (var e in widgets.Entries) // 기능 페이지 제목 줄의 "Back" → Widgets 목록
                if (e.Page is DemoPage page)
                    page.BackTo = widgets;
            trend.Seed(DateTime.Now.AddSeconds(-120), 120);
        }

        app.Theme = appTheme;
        app.Show(start);

        if (args.Contains("shot")) // 창 없이 쇼케이스 화면과 Widgets 의 각 페이지를 PNG 로
        {
            var dir = args.SkipWhile(a => a != "shot").Skip(1).FirstOrDefault() ?? ".";
            Directory.CreateDirectory(dir);
            var shots = new (DvPage, string)[] { (overview, "showcase-overview"), (plantProcess, "showcase-process"), (widgets, "showcase-widgets") }
                .Concat(widgets.Entries.Select(e => (e.Page, "widget-" + e.Title.ToLowerInvariant().Replace(' ', '-'))));
            foreach (var (page, name) in shots)
            {
                app.Show(page);
                Shot(app, width, height, null); // 페이지 전환은 다음 프레임에 적용
                Shot(app, width, height, Path.Combine(dir, name + ".png"));
            }

            if (args.Contains("anim")) // -- shot <폴더> anim = P&ID 운전 전환 시퀀스를 50ms 프레임으로(README 애니메이션 WebP)
            {
                // 단계마다 1.2초(점선 한 주기 600ms 의 배수) — 예비 펌프 P-102 기동(유량 상승) 후 정지, 처음 상태로 돌아와 끊김 없이 반복
                Action[] steps =
                [
                    () => { },
                    () => sim.P102Run = true,
                    () => sim.P102Run = false,
                ];
                app.Show(plantProcess);
                Shot(app, width, height, null);
                var frame = 0;
                var t0 = DateTime.Now;
                foreach (var step in steps)
                {
                    step();
                    for (var i = 0; i < 24; i++, frame++)
                    {
                        if (frame % 4 == 0) // 200ms 마다 시뮬레이션(실제 500ms 보다 빠르게) — 펌프 2대면 유량이 오르는 게 단계 안에 보이도록
                            sim.Tick(t0.AddMilliseconds(frame * 50));
                        app.Invalidate();
                        Shot(app, width, height, Path.Combine(dir, $"process-frame-{frame:000}.png"), frame * 50L);
                    }
                }
            }

            return;
        }

        using var window = new ViewWindow(app, width, height, title) { Gpu = args.Contains("gpu") }; // -- gpu = OpenGL 로 그리기

        // 타이머가 필요한 페이지: 그리드는 행 값을 바꾸고 Repaint(그릴 때 값을 읽으므로 재빌드 불필요),
        // 트렌드는 폴링 타이머가 샘플을 넣고 Invalidate, 고속 트렌드는 별도 스레드가 1 ms 샘플을 밀린 만큼 채운다.
        System.Threading.Timer? timer = null;
        Thread? producer = null;
        SimulatedPlc? plc = null;
        var running = true;
        if (showcase) // 쇼케이스 — 공장 값과, Widgets 에서 열 수 있는 페이지들(그리드·트렌드·공정도)을 같이 움직인다
        {
            timer = new System.Threading.Timer(_ =>
            {
                var now = DateTime.Now;
                sim.Tick(now);
                grid.Tick();
                trend.Sample(now);
                process.Tick();
                app.Invalidate();
            }, null, 500, 500);
            var origin = DateTime.Now;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            producer = new Thread(() => // 고속 트렌드 — 샘플은 늘 채우고, 다시 그리기는 그 페이지가 보일 때만
            {
                while (Volatile.Read(ref running))
                {
                    fastTrend.CatchUp(origin, clock.ElapsedMilliseconds);
                    var shown = app.CurrentPage == fastTrend;
                    if (shown)
                        app.Invalidate();
                    Thread.Sleep(shown ? 1 : 50);
                }
            }) { IsBackground = true };
            producer.Start();
        }
        else if (start == grid)
        {
            timer = new System.Threading.Timer(_ => { grid.Tick(); window.Repaint(); }, null, 500, 500);
        }
        else if (start == process) // 공정도 수위 — 값만 바뀌므로 배치는 다시 계산하지 않는다
        {
            timer = new System.Threading.Timer(_ => { process.Tick(); app.Invalidate(); }, null, 500, 500);
        }
        else if (start == trend || args.Contains("nav")) // 사이드바 시나리오에서도 트렌드·공정도 페이지가 살아 있게
        {
            trend.Seed(DateTime.Now.AddSeconds(-120), 120);
            timer = new System.Threading.Timer(_ => { trend.Sample(DateTime.Now); process.Tick(); app.Invalidate(); }, null, 500, 500);
        }
        else if (start == dialog)
        {
            plc = new SimulatedPlc(dialog.Report, 4000); // 창(앱)이 붙은 뒤 시작 — DvApp.Current 를 쓴다
            app.IdleTimeout = TimeSpan.FromSeconds(30); // 30초 무입력 → 잠금(그 뒤 뜬 알람 다이얼로그는 잠금 아래에 쌓인다)
            app.Idle += (_, _) => dialog.Lock();
        }
        else if (start == fastTrend)
        {
            var origin = DateTime.Now;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            producer = new Thread(() =>
            {
                while (Volatile.Read(ref running))
                {
                    fastTrend.CatchUp(origin, clock.ElapsedMilliseconds);
                    app.Invalidate();
                    Thread.Sleep(1);
                }
            }) { IsBackground = true };
            producer.Start();
        }

        window.Run();
        Volatile.Write(ref running, false);
        timer?.Dispose();
        plc?.Dispose();
    }

    private static DvStrings WithLanguage(DvStrings strings, string language)
    {
        strings.Language = language;
        return strings;
    }

    // 창 없이 앱 화면(바 + 현재 페이지) 한 프레임 — path 가 null 이면 그리기만(대기 중인 페이지 전환 적용)
    private static void Shot(DvApp app, int width, int height, string? path, long timestampMs = 0)
    {
        using var bitmap = new SkiaSharp.SKBitmap(width, height);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
            app.Surface.RenderTo(canvas, width, height, timestampMs);
        if (path is null)
            return;
        using var file = File.Create(path);
        bitmap.Encode(file, SkiaSharp.SKEncodedImageFormat.Png, 100);
    }
}
