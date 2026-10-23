using Devinno.UI.Alarms;
using Devinno.UI.Trends;

// 쇼케이스 공장 시뮬레이션 — 정수장 한 라인. 타이머 스레드가 Tick 으로 값을 움직이고, 화면(Overview·Process)은 읽기만 한다.
// 실제 앱에서는 이 자리가 PLC·서버 통신이다(데이터는 앱 책임, 위젯은 표시와 입력 전달만).
public sealed class PlantSim
{
    private readonly Random _rnd = new(7);
    private readonly object _gate = new();
    private int _ticks;

    private TimeSpan _ago; // 시작 알람을 지난 시각으로 남기기 위한 시계 오프셋

    public PlantSim()
    {
        Alarms = new AlarmLog(maxRows: 200, clock: () => DateTime.Now - _ago);
        var now = DateTime.Now;
        // 처음 화면부터 트렌드·스파크라인이 차 있게 지난 2분을 미리 채운다
        for (var i = 240; i > 0; i--)
            Step(now.AddSeconds(-i * 0.5), seed: true);
        Seed(95, AlarmPriority.Minor, "UPS-2 battery test completed with warnings", "UPS-2", acked: true);
        Seed(41, AlarmPriority.Minor, "Coagulant day tank below 20 %", "T-205", acked: true);
        Seed(12, AlarmPriority.Minor, "Backwash cycle due on filter F-3", "F-303");
        Seed(6, AlarmPriority.Major, "Flocculation basin level high", "LT-201");
        Seed(2, AlarmPriority.Critical, "Filter F-2 differential pressure high-high", "PDT-302");
        _ago = TimeSpan.Zero;
    }

    private void Seed(int minutesAgo, AlarmPriority p, string message, string source, bool acked = false)
    {
        _ago = TimeSpan.FromMinutes(minutesAgo);
        var row = Alarms.Raise(p, message, source);
        if (acked)
            Alarms.Ack(row.Id);
    }

    // ── 공정값 ──
    public double IntakeFlow { get; private set; } = 1300;
    public double TreatedFlow { get; private set; } = 1240;
    public double Demand { get; private set; } = 1230;
    public double Turbidity { get; private set; } = 0.20;
    public double Chlorine { get; private set; } = 0.62;
    public double Power { get; private set; } = 410;
    public double Availability { get; private set; } = 98.6;

    public double DischargePressure { get; private set; } = 6.1;   // bar
    public double FilterDp { get; private set; } = 106;            // kPa
    public double PumpSpeed { get; private set; } = 78;            // %
    public double RawLevel { get; private set; } = 64;
    public double FlocLevel { get; private set; } = 69;
    public double ClearLevel { get; private set; } = 85;
    public double ReactorLevel { get; private set; } = 58;
    public double DosingLevel { get; private set; } = 72;
    public double Temperature { get; private set; } = 18.4;
    public double Ph { get; private set; } = 7.21;

    // 역세 시퀀스 — 1부터(0 = 대기)
    public static readonly string[] BackwashSteps = ["Filter", "Drain", "Air scour", "Backwash", "Rinse"];
    public int BackwashStep { get; private set; } = 3;

    // ── 운전 상태(화면 입력이 바꾸는 것) ──
    public bool P101Run { get; set; } = true;
    public bool P102Run { get; set; }
    public bool P301Run { get; set; } = true;
    public bool P401Run { get; set; } = true;
    public bool FcvOpen { get; set; } = true;
    public bool TcvOpen { get; set; } = true;
    public bool EStop { get; private set; }

    public string DoseMode { get; set; } = "auto";
    public double DoseSetpoint { get; set; } = 0.6;
    public double DoseRate { get; set; } = 39;
    public bool DosingPump { get; set; } = true;
    public bool PreChlorination { get; set; }

    public AlarmLog Alarms { get; }
    public TrendBuffer Trend { get; } = new(capacity: 600);

    // 스파크라인 — 바뀔 때마다 새 배열(그리는 쪽이 읽는 중에 바뀌지 않게)
    public double[] IntakeHistory { get; private set; } = [];
    public double[] TurbidityHistory { get; private set; } = [];
    public double[] ChlorineHistory { get; private set; } = [];
    public double[] PowerHistory { get; private set; } = [];
    public double[] AvailabilityHistory { get; private set; } = [];

    public List<EquipmentRow> Equipment { get; } =
    [
        new("P-101", "Raw water pump", 78, true),
        new("P-102", "Standby pump", 0, true),
        new("B-201", "Scour blower", 65, true),
        new("F-302", "Filter bed 2", 0, false) { Fault = true },
        new("M-401", "Flocculator", 47, true),
        new("P-501", "High-lift pump", 74, true),
    ];

    public void Tick(DateTime now)
    {
        lock (_gate)
            Step(now, seed: false);
    }

    public void StopAll()
    {
        lock (_gate)
        {
            EStop = true;
            P101Run = P102Run = P301Run = P401Run = false;
            DosingPump = false;
        }

        Alarms.Raise(AlarmPriority.Critical, "Emergency stop pressed at HMI", "HMI");
    }

    public void Reset()
    {
        lock (_gate)
        {
            EStop = false;
            P101Run = P301Run = P401Run = true;
            DosingPump = true;
        }
    }

    private void Step(DateTime now, bool seed)
    {
        _ticks++;
        var t = now.TimeOfDay.TotalSeconds;
        var pumping = (P101Run ? 1 : 0) + (P102Run ? 1 : 0);

        // 유량 — 느린 물결 + 잡음, 펌프가 멈추면 0 으로 내려간다
        var target = pumping == 0 ? 0 : 1180 + 110 * Math.Sin(t / 23) + 45 * Math.Sin(t / 7.3) + (pumping == 2 ? 160 : 0);
        IntakeFlow = Approach(IntakeFlow, target + Noise(12), 0.25);
        TreatedFlow = Approach(TreatedFlow, IntakeFlow * 0.955 + Noise(8), 0.2);
        Demand = Approach(Demand, 1210 + 60 * Math.Sin(t / 31 + 1) + Noise(10), 0.15);

        Turbidity = Math.Clamp(Approach(Turbidity, 0.2 + 0.04 * Math.Sin(t / 17) + Noise(0.01), 0.3), 0.05, 1);
        Chlorine = Math.Clamp(Approach(Chlorine, (DosingPump ? DoseSetpoint + 0.02 : 0.15) + Noise(0.01), 0.08), 0, 2);
        Power = Approach(Power, 200 + pumping * 150 + (P301Run ? 60 : 0) + Noise(6), 0.2);
        Availability = Math.Clamp(Approach(Availability, 98.6 + Noise(0.05), 0.1), 90, 100);

        DischargePressure = Math.Clamp(Approach(DischargePressure, pumping == 0 ? 0.4 : 6.0 + 0.3 * Math.Sin(t / 11) + Noise(0.05), 0.25), 0, 10);
        FilterDp = Math.Clamp(Approach(FilterDp, 100 + 12 * Math.Sin(t / 37) + Noise(1.5), 0.2), 0, 150);
        PumpSpeed = Math.Clamp(Approach(PumpSpeed, P101Run ? 78 + 6 * Math.Sin(t / 13) : 0, 0.3), 0, 100);

        RawLevel = Bounce(RawLevel, (pumping == 0 ? 0.05 : -0.03) + 0.04 * Math.Sin(t / 29), 30, 92);
        FlocLevel = Bounce(FlocLevel, 0.03 * Math.Sin(t / 19 + 2), 40, 85);
        ClearLevel = Bounce(ClearLevel, (P301Run ? -0.02 : 0.03) + 0.03 * Math.Sin(t / 41), 50, 95);
        ReactorLevel = Bounce(ReactorLevel, 0.05 * Math.Sin(t / 15), 35, 80);
        DosingLevel = Bounce(DosingLevel, P401Run ? -0.01 : 0, 15, 100);
        Temperature = Approach(Temperature, (TcvOpen ? 18.4 : 14.0) + Noise(0.05), 0.05);
        Ph = Approach(Ph, 7.2 + 0.05 * Math.Sin(t / 27) + Noise(0.01), 0.2);

        Equipment[0].Load = P101Run ? Math.Round(PumpSpeed) : 0;
        Equipment[0].Running = P101Run;
        Equipment[1].Load = P102Run ? 74 : 0;
        Equipment[1].Running = P102Run;

        Trend.Add("intake", now, IntakeFlow);
        Trend.Add("treated", now, TreatedFlow);
        Trend.Add("demand", now, Demand);

        if (_ticks % 4 == 0) // 스파크라인은 2초 간격, 최근 40점
        {
            IntakeHistory = Push(IntakeHistory, IntakeFlow);
            TurbidityHistory = Push(TurbidityHistory, Turbidity);
            ChlorineHistory = Push(ChlorineHistory, Chlorine);
            PowerHistory = Push(PowerHistory, Power);
            AvailabilityHistory = Push(AvailabilityHistory, Availability);
        }

        if (seed)
            return;
        if (_ticks % 16 == 0) // 8초마다 역세 다음 단계
            BackwashStep = BackwashStep >= BackwashSteps.Length ? 1 : BackwashStep + 1;
        if (_ticks % 90 == 0) // 45초마다 알람 하나
            RaiseRandomAlarm();
    }

    private static readonly (AlarmPriority P, string Msg, string Src)[] Events =
    [
        (AlarmPriority.Major, "Raw water turbidity rising", "AIT-101"),
        (AlarmPriority.Minor, "Filter F-1 run time above 48 h", "F-301"),
        (AlarmPriority.Critical, "Raw water pump P-102 failed to start", "P-102"),
        (AlarmPriority.Major, "Clear well level low", "LT-501"),
        (AlarmPriority.Minor, "Chlorine analyzer calibration due", "AIT-301"),
    ];

    private void RaiseRandomAlarm()
    {
        var e = Events[_rnd.Next(Events.Length)];
        Alarms.Raise(e.P, e.Msg, e.Src);
    }

    private double Noise(double amp) => (_rnd.NextDouble() * 2 - 1) * amp;

    private static double Approach(double v, double target, double k) => v + (target - v) * k;

    private double Bounce(double v, double drift, double min, double max)
        => Math.Clamp(v + drift + Noise(0.03), min, max);

    private static double[] Push(double[] a, double v)
        => a.Length < 40 ? [.. a, v] : [.. a.Skip(1), v];
}

// 설비 표 한 행 — 그리드가 그릴 때 값을 읽는다(Load·Running 은 시뮬레이션이 바꾼다)
public sealed class EquipmentRow(string tag, string description, double load, bool auto)
{
    public string Tag { get; } = tag;
    public string Description { get; } = description;
    public double Load { get; set; } = load;
    public bool Auto { get; set; } = auto;
    public bool Running { get; set; } = load > 0;
    public bool Fault { get; set; }

    // 램프 칸 = state-* 토큰 키, 버튼 칸 = 고장일 때만 누를 수 있다
    public string State => Fault ? "fault" : Running ? "run" : "stop";
    public bool CanReset => Fault;
}
