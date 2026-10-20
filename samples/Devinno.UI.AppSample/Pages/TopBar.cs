using Devinno.UI.App;

public sealed partial class TopBar : DvBar
{
    public string HomeClass => Tab<HomePage>();
    public string MonitorClass => Tab<MonitorPage>();

    public void ShowHome() => App.Show<HomePage>();
    public void ShowMonitor() => App.Show<MonitorPage>();

    private string Tab<T>() where T : DvPage => App.CurrentPage is T ? "tab on" : "tab";
}
