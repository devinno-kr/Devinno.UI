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

    private static readonly string[] Critical = { "여과기 B 차압 상한 초과", "취수 펌프 #2 과전류 트립", "정수지 수위 상상한" };
    private static readonly string[] Major = { "응집조 수위 주의 구간 진입", "약품 탱크 잔량 20% 미만", "송수 압력 변동 과다" };
    private static readonly string[] Minor = { "필터 세척 주기 도래", "UPS 배터리 점검 필요", "야간 운전 모드 전환" };
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
    public string UnAckedText => $"미확인 {Log.UnAckedCount}건";
    public string CountText => $"{Log.Rows.Count}건";

    // 긴급(Critical) 알람은 ACK 전에 확인 창으로 한 번 더 묻는다(오조작 방지) — 위젯 속성이 아니라 처리기에서.
    public async void Ack(AlarmRow row)
    {
        if (row.Priority == AlarmPriority.Critical &&
            await App.MessageBox($"'{row.Message}' 알람을 확인할까요?", "알람 확인", DvMessageButtons.YesNo) != DvDialogResult.Yes)
            return;
        Log.Ack(row.Id);
    }

    public async void AckAll()
    {
        if (Log.UnAckedCount > 0 &&
            await App.MessageBox($"미확인 알람 {Log.UnAckedCount}건을 모두 확인할까요?", "전체 확인", DvMessageButtons.YesNo) == DvDialogResult.Yes)
            Log.AckAll();
    }
    public void RaiseCritical() => Log.Raise(AlarmPriority.Critical, Critical[_n++ % Critical.Length]);
    public void RaiseMajor() => Log.Raise(AlarmPriority.Major, Major[_n++ % Major.Length]);
    public void RaiseMinor() => Log.Raise(AlarmPriority.Minor, Minor[_n++ % Minor.Length]);
    public void ToggleComm() => CommText = CommText.Length == 0 ? "PLC1 응답 없음 (3초)" : "";
}
