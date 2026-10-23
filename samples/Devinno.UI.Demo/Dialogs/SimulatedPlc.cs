using Devinno.UI.App;

// 통신 시뮬레이션 — System.Threading.Timer(스레드 풀)가 몇 초마다 알람을 "수신"한다.
// 페이지가 아니므로 DvApp.Current 로 앱에 닿고, 같은 번호의 알람 창이 이미 떠 있으면 다시 띄우지 않는다.
internal sealed class SimulatedPlc : IDisposable
{
    private static readonly string[] Texts = ["Pump 1 overload", "Tank level high", "PLC response delay"];

    private readonly object _gate = new();
    private readonly Action<string> _report;
    private readonly System.Threading.Timer _timer;
    private int _count;

    public SimulatedPlc(Action<string> report, int periodMs)
    {
        _report = report;
        _timer = new System.Threading.Timer(_ => OnMessage(), null, periodMs, periodMs);
    }

    public void Dispose() => _timer.Dispose();

    // 통신 스레드
    private void OnMessage()
    {
        var alarmId = _count++ % Texts.Length + 1; // 1, 2, 3, 1, 2, 3 …
        var app = DvApp.Current;                   // 창이 붙은 뒤에 시작했으므로 예외 없음(닫힌 뒤에도 마지막 앱)
        lock (_gate)                               // 확인과 띄우기 사이에 다른 콜백이 끼지 않게
        {
            if (app.Dialogs.OfType<AlarmDialog>().Any(d => d.AlarmId == alarmId))
            {
                _report($"Alarm #{alarmId} received again — already open, skipped");
                return;
            }
            _ = WaitAckAsync(app, alarmId, Texts[alarmId - 1]);
        }
    }

    private async Task WaitAckAsync(DvApp app, int alarmId, string text)
    {
        try
        {
            bool? acked = await app.ShowDialog<AlarmDialog, bool?>(alarmId, text);
            // 이어짐은 입력 스레드 — PLC 쓰기 같은 무거운 일이면 Task.Run 으로 넘긴다
            _report(acked == true ? $"Alarm #{alarmId} ACK" : $"Alarm #{alarmId} closed (no ACK)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Alarm #{alarmId}: {ex.Message}");
        }
    }
}
