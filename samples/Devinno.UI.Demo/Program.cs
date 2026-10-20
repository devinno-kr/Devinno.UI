using Devinno.UI.App;
using Devinno.UI.Localization;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

// 데모 — DvApp 하나에 페이지를 전부 넣고, 실행 인자로 시작 페이지를 고른다.
//   dotnet run --project samples/Devinno.UI.Demo [-- lang|control|equipment|grid|trend [fast]|alarm|input|knob|neon|process|basic|v2] [-- light]
//   시나리오: -- top = 상단 바(모니터링·설정·알람) + 홈 + 각 페이지의 "이전",  -- nav = 왼쪽 사이드바,
//            -- dialog = 레시피 편집 다이얼로그 + 그 위 메시지 박스 + 통신 시뮬레이션 알람(DvApp.Current, 키 중복 방지)
internal static class Program
{
    private static void Main(string[] args)
    {
        var theme = args.Contains("light") ? DvTheme.Light : DvTheme.Dark;
        var strings = args.Contains("lang") ? LangPage.CreateStrings() : null; // 다국어는 lang 일 때만(다른 페이지의 알람 문구가 바뀌지 않게)

        var dashboard = new DashboardPage();
        var components = new ComponentDashboardPage();
        var control = new ControlPage();
        var lang = new LangPage(strings ?? LangPage.CreateStrings());
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

        var registry = new Devinno.UI.Components.ComponentRegistry();
        ProcessPage.Register(registry); // 공정도 유닛 컴포넌트(Blender)
        var app = new DvApp { Strings = strings, Registry = registry };
#if DEBUG
        app.HotReload = true; // 개발 중 — dvml 을 저장하면 실행 중인 화면에 바로 반영(배포 빌드에서는 꺼짐)
#endif
        foreach (var page in new DvPage[] { home, dashboard, components, control, lang, equipment, grid, trend, fastTrend, alarm, input, knob, neon, dialog, process, basic, motionNext })
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
            : (dashboard, DashboardPage.Width, DashboardPage.Height, "Devinno.UI Demo (OpenTK)", null);

        if (args.Contains("top"))
        {
            app.Top = new MainTopBar();
            components.ShowBack = knob.ShowBack = alarm.ShowBack = true; // 각 페이지의 "이전" → 홈
        }
        else if (args.Contains("nav"))
            app.Left = new MainSideBar();

        app.Theme = appTheme;
        app.Show(start);
        using var window = new ViewWindow(app, width, height, title);

        // 타이머가 필요한 페이지: 그리드는 행 값을 바꾸고 Repaint(그릴 때 값을 읽으므로 재빌드 불필요),
        // 트렌드는 폴링 타이머가 샘플을 넣고 Invalidate, 고속 트렌드는 별도 스레드가 1 ms 샘플을 밀린 만큼 채운다.
        System.Threading.Timer? timer = null;
        Thread? producer = null;
        SimulatedPlc? plc = null;
        var running = true;
        if (start == grid)
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
}
