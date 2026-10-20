using Devinno.UI.App;

public sealed partial class HomePage : DvPage
{
    public void OpenMonitor() => App.Show<MonitorPage>();
}
