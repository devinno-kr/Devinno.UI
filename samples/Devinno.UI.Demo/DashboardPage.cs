using Devinno.UI.App;
// 한강 정수장 대시보드 — 마크업은 Pages/DashboardPage.dvml. 색은 상태에 따라 VM 이 정한다.
public sealed partial class DashboardPage : DvPage
{
    public const int Width = 1280;
    public const int Height = 800;

    public bool Running { get; set; } = true;
    public bool AutoMode { get; set; } = true;
    public bool Mute { get; set; }

    public string StatusText => Running ? "정상 운전" : "운전 정지";
    public string StatusFg => Running ? "#6ee7b7" : "#fca5a5";
    public string StatusBg => Running ? "#10b9811f" : "#ef44441f";
    public string StatusBorder => Running ? "#10b98155" : "#ef444455";
    public string LampColor => Running ? "#10b981" : "#ef4444";
    public string PumpState => Running ? "운전" : "정지";
    public string PumpFg => Running ? "#6ee7b7" : "#94a3b8";
    public string PumpBg => Running ? "#10b9811f" : "#1e293b";

    public void Start() => Running = true;

    public void Stop() => Running = false;
}
