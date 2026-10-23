using Devinno.UI.App;

// 쇼케이스 2 — 같은 라인의 P&ID. 장비와 연결만 적고 배치·배관·흐름 판단은 라이브러리가 한다.
// 장비를 누르거나 아래 래칭 버튼으로 펌프·밸브를 바꾸면 흐름 표시(점선)가 따라 바뀐다. 매체 색: 물·증기·약품.
public sealed partial class PlantProcessPage(PlantSim sim) : DvPage
{
    public bool FlowAnimation { get; set; } = true;

    public bool P101Run { get => sim.P101Run; set => sim.P101Run = value; }
    public bool P102Run { get => sim.P102Run; set => sim.P102Run = value; }
    public bool P301Run { get => sim.P301Run; set => sim.P301Run = value; }
    public bool P401Run { get => sim.P401Run; set => sim.P401Run = value; }
    public bool FcvOpen { get => sim.FcvOpen; set => sim.FcvOpen = value; }
    public bool TcvOpen { get => sim.TcvOpen; set => sim.TcvOpen = value; }
    public bool EStop => sim.EStop;
    public bool AgitatorRun => !sim.EStop;

    public void ToggleP101() { if (!sim.EStop) P101Run = !P101Run; }
    public void ToggleP102() { if (!sim.EStop) P102Run = !P102Run; }
    public void ToggleP301() { if (!sim.EStop) P301Run = !P301Run; }
    public void ToggleP401() { if (!sim.EStop) P401Run = !P401Run; }
    public void ToggleFcv() => FcvOpen = !FcvOpen;
    public void ToggleTcv() => TcvOpen = !TcvOpen;

    public double RawLevel => sim.RawLevel;
    public double ReactorLevel => sim.ReactorLevel;
    public double DosingLevel => sim.DosingLevel;

    public string FlowText => $"{(sim.FcvOpen ? sim.IntakeFlow : 0):#,0} m³/h";
    public string TempText => $"{sim.Temperature:0.0} °C";
    public string PhText => $"pH {sim.Ph:0.00}";
    public string ReactorLevelText => $"{sim.ReactorLevel:0} %";
}
