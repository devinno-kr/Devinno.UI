using Devinno.UI.App;

// 상단 바 — 버튼 3개로 페이지를 요청하고, 현재 페이지(App.CurrentPage)에 해당하는 버튼을 강조한다.
public sealed partial class MainTopBar : DvBar
{
    public string MonitorClass => TabClass<ComponentDashboardPage>();
    public string SettingClass => TabClass<KnobPage>();
    public string AlarmClass => TabClass<AlarmPage>();

    public void ShowMonitor() => App.Show<ComponentDashboardPage>();

    public void ShowSetting() => App.Show<KnobPage>();

    public void ShowAlarm() => App.Show<AlarmPage>();

    private string TabClass<T>() where T : DvPage => App.CurrentPage is T ? "tab tab-active" : "tab";
}
