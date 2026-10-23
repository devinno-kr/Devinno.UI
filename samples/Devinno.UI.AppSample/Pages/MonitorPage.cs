using Devinno.UI.App;

public sealed partial class MonitorPage : DvPage
{
    public double Temperature { get; set; } = 24.5;
    public double Setpoint { get; private set; } = 25.0;
    public int ShownCount { get; private set; }

    public void Back() => App.Show<HomePage>();

    public async void EditSetpoint()
    {
        double? value = await App.ShowDialog<SetpointDialog, double?>(Setpoint);
        if (value is null)
            return;                 // Cancel button, or closed from outside
        Setpoint = value.Value;     // continues on the input thread: set the VM directly and the screen re-reads it in the same frame
    }

    protected override void OnShown() => ShownCount++;   // every time it is shown
}
