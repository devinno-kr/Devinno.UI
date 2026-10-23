using Devinno.UI.Alarms;
using Devinno.UI.App;
using Devinno.UI.Trends;

// 쇼케이스 1 — 정수장 운영 대시보드. 값은 PlantSim 이 움직이고(타이머 → App.Invalidate), 화면 입력은 PlantSim 에 쓴다.
public sealed partial class OverviewPage(PlantSim sim) : DvPage
{
    public sealed record Kpi(string Label, string Value, string Unit, string Delta, string Tone, double[] History);

    public IReadOnlyList<Kpi> Kpis =>
    [
        new("Intake flow", $"{sim.IntakeFlow:#,0}", "m³/h", $"{(sim.IntakeFlow - 1180) / 1180:+0.0%;-0.0%}", "info", sim.IntakeHistory),
        new("Treated turbidity", $"{sim.Turbidity:0.00}", "NTU", sim.Turbidity < 0.3 ? "In spec" : "High", sim.Turbidity < 0.3 ? "success" : "warning", sim.TurbidityHistory),
        new("Free chlorine", $"{sim.Chlorine:0.00}", "mg/L", $"SP {sim.DoseSetpoint:0.00}", "info", sim.ChlorineHistory),
        new("Power demand", $"{sim.Power:0}", "kW", $"{(sim.Power - 382) / 382:+0.0%;-0.0%}", "warning", sim.PowerHistory),
        new("Availability", $"{sim.Availability:0.0}", "%", "30 d", "success", sim.AvailabilityHistory),
    ];

    public TrendBuffer Trend => sim.Trend;

    public double DischargePressure => sim.DischargePressure;
    public double FilterDp => sim.FilterDp;
    public double PumpSpeed => sim.PumpSpeed;
    public IReadOnlyList<string> BackwashSteps => PlantSim.BackwashSteps;
    public int BackwashStep => sim.BackwashStep;
    public string DutyText => sim.P102Run ? "P-102 duty" : "P-101 duty";

    public double RawLevel => sim.RawLevel;
    public double FlocLevel => sim.FlocLevel;
    public double ClearLevel => sim.ClearLevel;

    public IReadOnlyList<AlarmRow> Alarms => sim.Alarms.Rows;
    public string AlarmCount => $"{sim.Alarms.Rows.Count} today";
    public void Ack(AlarmRow row) => sim.Alarms.Ack(row.Id);

    public List<EquipmentRow> Equipment => sim.Equipment;
    public string EquipmentCount => $"{sim.Equipment.Count} units";

    public void ResetEquipment(EquipmentRow row)
    {
        row.Fault = false;
        sim.Alarms.Raise(AlarmPriority.Minor, $"{row.Tag} reset from HMI", row.Tag);
    }

    public string DoseMode { get => sim.DoseMode; set => sim.DoseMode = value; }
    public bool RateLocked => sim.DoseMode != "manual";   // 투입률은 수동일 때만 손으로
    public double DoseSetpoint { get => sim.DoseSetpoint; set => sim.DoseSetpoint = value; }
    public double DoseRate { get => sim.DoseRate; set => sim.DoseRate = value; }
    public bool DosingPump { get => sim.DosingPump; set => sim.DosingPump = value; }
    public bool PreChlorination { get => sim.PreChlorination; set => sim.PreChlorination = value; }
}
