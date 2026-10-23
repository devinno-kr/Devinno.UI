using Devinno.UI.App;
using Devinno.UI.Alarms;

// 알람 데모 — 버튼으로 알람 발생/통신 이상 토글, 배너 클릭·ACK 버튼으로 확인(긴급·전체 확인은 확인 창).
public sealed partial class AlarmPage : DvPage
{
    public const int Width = 1100;
    public const int Height = 680;

    /// <summary>시나리오 데모(<c>-- top</c>)에서 "이전" 버튼을 보일지(기본 숨김 — 숨기면 화면에 요소가 없다).</summary>
    public bool ShowBack { get; set; }

    public void Back() => App.Show<HomePage>();

    private static readonly string[] Critical = { "Filter B diff. pressure over limit", "Intake pump #2 overcurrent trip", "Clear well level high-high" };
    private static readonly string[] Major = { "Flocculation level in caution zone", "Chemical tank below 20%", "Transfer pressure fluctuating" };
    private static readonly string[] Minor = { "Filter wash cycle due", "UPS battery check needed", "Switched to night mode" };
    private int _n;

    public AlarmPage()
    {
        Log.Raise(AlarmPriority.Minor, Minor[0]);
        var a = Log.Raise(AlarmPriority.Major, Major[1]);
        Log.Ack(a.Id);
        Log.Raise(AlarmPriority.Major, Major[0]);
        Log.Raise(AlarmPriority.Critical, Critical[0]);
    }

    public AlarmLog Log { get; } = new(200);
    public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
    public string CommText { get; set; } = "";
    public string UnAckedText => $"Unacked {Log.UnAckedCount}";
    public string CountText => $"{Log.Rows.Count} items";

    // 긴급(Critical) 알람은 ACK 전에 확인 창으로 한 번 더 묻는다(오조작 방지) — 위젯 속성이 아니라 처리기에서.
    public async void Ack(AlarmRow row)
    {
        if (row.Priority == AlarmPriority.Critical &&
            await App.MessageBox($"Acknowledge alarm '{row.Message}'?", "Acknowledge", DvMessageButtons.YesNo) != DvDialogResult.Yes)
            return;
        Log.Ack(row.Id);
    }

    public async void AckAll()
    {
        if (Log.UnAckedCount > 0 &&
            await App.MessageBox($"Acknowledge all {Log.UnAckedCount} unacknowledged alarms?", "Ack all", DvMessageButtons.YesNo) == DvDialogResult.Yes)
            Log.AckAll();
    }
    public void RaiseCritical() => Log.Raise(AlarmPriority.Critical, Critical[_n++ % Critical.Length]);
    public void RaiseMajor() => Log.Raise(AlarmPriority.Major, Major[_n++ % Major.Length]);
    public void RaiseMinor() => Log.Raise(AlarmPriority.Minor, Minor[_n++ % Minor.Length]);
    public void ToggleComm() => CommText = CommText.Length == 0 ? "PLC1 no response (3 s)" : "";
}
