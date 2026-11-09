using Devinno.UI.App;
using Devinno.UI.Styling;

// 쇼케이스 상단 바 — 페이지 전환(Overview·Process·Widgets) + 공장 상태 칩·시계·테마 선택·전체 확인·비상 정지.
// 비상 정지·복귀는 1초 누름(hold) — 잘못 누름 방지.
public sealed partial class ShowcaseBar(PlantSim sim) : DvBar
{
    public string OverviewClass => Nav<OverviewPage>();
    public string ProcessClass => Nav<PlantProcessPage>();
    public string WidgetsClass => App.CurrentPage is OverviewPage or PlantProcessPage ? "nav" : "nav nav-on";

    public void ShowOverview() => App.Show<OverviewPage>();
    public void ShowProcess() => App.Show<PlantProcessPage>();
    public void ShowWidgets() => App.Show<WidgetsPage>();

    public string ModeText => sim.EStop ? "E-STOP" : sim.DoseMode == "auto" ? "AUTO MODE" : "MANUAL MODE";
    public string ModeTone => sim.EStop ? "danger" : "info";
    public string UnackedText => $"{sim.Alarms.UnAckedCount} UNACKED";
    public string UnackedTone => sim.Alarms.UnAckedCount > 0 ? "danger" : "neutral";
    public string Clock => DateTime.Now.ToString("MM-dd  HH:mm:ss");

    public bool Running => !sim.EStop;
    public bool Stopped => sim.EStop;

    // 테마 선택 — 고르는 즉시 앱 전체(페이지·바·열린 대화상자)가 그 테마로 다시 그려진다
    public string ThemeName
    {
        get
        {
            var t = App.Theme ?? DvTheme.Default;
            return t == DvTheme.Light ? "Light" : t == DvTheme.Graphite ? "Graphite" : t == DvTheme.Carbon ? "Carbon" : "Dark";
        }
        set => App.Theme = value switch
        {
            "Light" => DvTheme.Light,
            "Graphite" => DvTheme.Graphite,
            "Carbon" => DvTheme.Carbon,
            _ => DvTheme.Dark,
        };
    }

    public void AckAll() => sim.Alarms.AckAll();
    public void EmergencyStop() => sim.StopAll();
    public void ResetPlant() => sim.Reset();

    private string Nav<T>() where T : DvPage => App.CurrentPage is T ? "nav nav-on" : "nav";
}
