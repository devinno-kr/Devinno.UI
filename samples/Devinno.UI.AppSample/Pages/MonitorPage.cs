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
            return;                 // 취소 버튼, 또는 바깥에서 닫힘
        Setpoint = value.Value;     // 이어짐은 입력 스레드 — VM 을 바로 쓰면 화면도 같은 프레임에 다시 읽는다
    }

    protected override void OnShown() => ShownCount++;   // 보일 때마다
}
