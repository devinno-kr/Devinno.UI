using Devinno.UI.App;

// 홈 — 시나리오 데모의 시작 페이지. 버튼은 App.Show 로 다른 페이지를 요청만 한다(페이지는 페이지를 담지 않는다).
public sealed partial class HomePage : DvPage
{
    public const int Width = 1280;
    public const int Height = 800;

    public void ShowMonitor() => App.Show<ComponentDashboardPage>();

    public void ShowSetting() => App.Show<KnobPage>();

    public void ShowAlarm() => App.Show<AlarmPage>();
}
