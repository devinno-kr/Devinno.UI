using Devinno.UI.App;
// DashboardPage 과 같은 화면을 디자인 시스템 레이어(의미 컴포넌트·슈가·토큰)로 작성한 버전. 마크업은 Pages/ComponentDashboardPage.dvml.
// 모양은 테마가 정하므로 style 은 위젯 크기·계기판 같은 화면 고유 부분에만 쓴다.
public sealed partial class ComponentDashboardPage : DvPage
{
    public const int Width = 1280;
    public const int Height = 800;

    /// <summary>시나리오 데모(<c>-- top</c>)에서 "이전" 버튼을 보일지(기본 숨김 — 숨기면 화면에 요소가 없다).</summary>
    public bool ShowBack { get; set; }

    public void Back() => App.Show<HomePage>();

    public bool Running { get; set; } = true;
    public bool AutoMode { get; set; } = true;
    public bool Mute { get; set; }
    public int PumpSpeed { get; set; } = 2;   // 세그먼트(저속·중속·고속)
    public bool Light { get; set; } = true;   // 래칭 버튼
    public bool Fan { get; set; }

    public string StatusText => Running ? "Running" : "Stopped";
    public string StatusTone => Running ? "success" : "danger";
    public string PumpState => Running ? "run" : "stop";
    public string PumpText => Running ? "Running" : "Stopped";

    public void Start() => Running = true;

    public void Stop() => Running = false;
}
