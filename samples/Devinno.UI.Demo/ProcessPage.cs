using Devinno.UI.App;
using Devinno.UI.Components;

// 공정도 데모 — 장비와 연결만 적은 두 공정(좌→우 정수 공정, 위→아래 블렌더). 배치·직교 배관은 라이브러리가 한다.
// 정수 공정에는 계기 버블(FT·FIC)과 신호선(FT → FIC → 조절 밸브)을 붙였다.
// 래칭 버튼으로 펌프·밸브 상태를 바꾸고(값만 바뀌므로 배치는 그대로), 타이머가 수위를 움직인다. 모양(plain·rich)은 세그먼트로.
// 두 번째 탭: 같은 블렌더 유닛 3기 — 템플릿 루트가 DvUnit 인 컴포넌트(Blender)를 반복, 유닛 안 Reset 은 Reset(BlenderVm) 로 자기 유닛을 받는다.
public sealed partial class ProcessPage : DvPage
{
    public const int Width = 1600, Height = 1080;

    public string Look { get; set; } = "rich";

    // 흐름 표시(flow-animation) — 라이브러리가 연결·장비 상태로 흐르는 배관을 판단해 점선을 움직인다
    public bool FlowAnim { get; set; } = true;
    public bool P1Run { get; set; } = true;
    public bool P2Run { get; set; }
    public bool V1Open { get; set; } = true;
    public bool BlPump1 { get; set; } = true;
    public bool BlPump2 { get; set; }
    public double T1Level { get; private set; } = 62;
    public double T2Level { get; private set; } = 35;
    public double BlLevel { get; private set; } = 55;
    public string BlTemp => $"{82 + BlLevel / 5:0.0} ℃";

    // 계기 값(DvInstrument value) — 유량 제어기 FIC-101
    public string FlowText => $"{(V1Open ? (P1Run ? 12.5 : 0) + (P2Run ? 11.0 : 0) : 0):0.0} m³/h";

    private int _resets;
    public string ResetText => $"Reset {_resets}회{(_lastReset is null ? "" : $" (마지막: {_lastReset})")}";
    private string? _lastReset;

    public void ResetBl()
    {
        _resets++;
        _lastReset = "BL-1";
    }

    // ── 유닛 탭 ──
    public sealed class BlenderVm
    {
        public required string Name { get; init; }
        public required string FeedTag { get; init; }
        public required string TempTag { get; init; }
        public required string Pump1Tag { get; init; }
        public required string Pump2Tag { get; init; }
        public double Level { get; set; }
        public bool Pump1Run { get; set; } = true;
        public bool Pump2Run { get; set; }
        public double Temp { get; set; } = 80;
        public string TempText => $"{Temp:0.0} ℃";
    }

    public BlenderVm BL1 { get; } = MakeBlender(1);
    public BlenderVm BL2 { get; } = MakeBlender(2);
    public BlenderVm BL3 { get; } = MakeBlender(3);
    public double RmtLevel { get; private set; } = 40;

    // 유닛 템플릿 안 Reset 버튼 — 눌린 유닛이 들어온다
    public void Reset(BlenderVm unit)
    {
        unit.Temp = 25;
        _resets++;
        _lastReset = unit.Name;
    }

    private static BlenderVm MakeBlender(int i) => new()
    {
        Name = $"BL-{i}", FeedTag = $"MTO RMMT-{i}", TempTag = $"TIA 100{i}",
        Pump1Tag = $"RTP 10{i}1", Pump2Tag = $"RTP 10{i}2", Pump2Run = i == 2, Temp = 78 + i * 3,
    };

    /// <summary>이 페이지가 쓰는 유닛 컴포넌트 — 앱 레지스트리에 등록한다.</summary>
    public static void Register(ComponentRegistry registry) => registry.Register("""
        <DvComponent name="Blender">
          <DvParam name="U"/>
          <DvUnit direction="tb" label="{U.Name}">
            <DvTerminal name="IN" text="{U.FeedTag}"/>
            <DvVessel name="T" level="{U.Level}">
              <DvAgitator running="{U.Pump1Run}"/>
              <DvAttach side="right">
                <DvLabel class="caption muted" text="{U.TempTag}"/>
                <DvLabel class="title" text="{U.TempText}"/>
              </DvAttach>
              <DvAttach side="left">
                <DvButton variant="secondary" size="sm" text="Reset" onclick="Reset"/>
              </DvAttach>
            </DvVessel>
            <DvPump name="P1" running="{U.Pump1Run}"/>
            <DvPump name="P2" running="{U.Pump2Run}"/>
            <DvPort name="out" side="bottom" at="0.7"/>
            <DvLine from="IN" to="T.top" arrow="end"/>
            <DvLine from="T.bottom" to="P1.in"/>
            <DvLine from="T.bottom" to="P2.in"/>
            <DvLine from="P1.out" to="out"/>
            <DvLine from="P2.out" to="out"/>
          </DvUnit>
        </DvComponent>
        """);

    private double _t;

    // 타이머(0.5초) — 수위만 바꾼다
    public void Tick()
    {
        _t += 0.5;
        T1Level = 55 + 25 * Math.Sin(_t / 6);
        T2Level = 40 + 20 * Math.Sin(_t / 9 + 1);
        BlLevel = 50 + 30 * Math.Sin(_t / 7 + 2);
        BL1.Level = 45 + 30 * Math.Sin(_t / 5);
        BL2.Level = 50 + 25 * Math.Sin(_t / 6 + 1);
        BL3.Level = 55 + 30 * Math.Sin(_t / 7 + 2);
        RmtLevel = 40 + 20 * Math.Sin(_t / 8);
        foreach (var b in new[] { BL1, BL2, BL3 })
            b.Temp = Math.Min(95, b.Temp + 0.2);
    }
}
