using Devinno.UI.App;

// 왼쪽 사이드바 — 항목마다 App.Show 를 요청하고, 현재 페이지(App.CurrentPage)에 해당하는 항목을 강조한다.
public sealed partial class MainSideBar : DvBar
{
    public string OverviewClass => NavClass<ComponentDashboardPage>();
    public string AlarmClass => NavClass<AlarmPage>();
    public string TrendClass => NavClass<TrendPage>();
    public string KnobClass => NavClass<KnobPage>();
    public string EquipmentClass => NavClass<EquipmentPage>();
    public string InputClass => NavClass<InputPage>();
    public string ProcessClass => NavClass<ProcessPage>();

    public string CurrentText => $"Current: {App.CurrentPage?.GetType().Name}";

    public void ShowOverview() => App.Show<ComponentDashboardPage>();

    public void ShowAlarm() => App.Show<AlarmPage>();

    public void ShowTrend() => App.Show<TrendPage>();

    public void ShowKnob() => App.Show<KnobPage>();

    public void ShowEquipment() => App.Show<EquipmentPage>();

    public void ShowInput() => App.Show<InputPage>();

    public void ShowProcess() => App.Show<ProcessPage>();

    private string NavClass<T>() where T : DvPage => App.CurrentPage is T ? "nav nav-active" : "nav";
}
