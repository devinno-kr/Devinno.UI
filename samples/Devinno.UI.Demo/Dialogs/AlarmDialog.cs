using Devinno.UI.App;

// 통신 알람 — AlarmId 가 중복 확인용 키다(다이얼로그가 스스로 정의. 제목·글자로 비교하지 않는다).
public sealed partial class AlarmDialog : DvDialog<bool?>
{
    public AlarmDialog(int alarmId, string text)
    {
        AlarmId = alarmId;
        Text = text;
    }

    public int AlarmId { get; }
    public string Text { get; }
    public string Code => $"알람 #{AlarmId}";

    public void Ack() => Close(true);
}
