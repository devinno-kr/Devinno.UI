using Devinno.UI;
using Devinno.UI.Alarms;
using Devinno.UI.Styling;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P6bAlarmSamples
{
    private const string Markup = """
        <DvPageFrame title="알람" subtitle="ISA-18.2 MVP · 통신 이상 스트립 + 미확인 영역 분리, 목록은 미확인 → 등급 → 최신 순">
          <DvAlarmBanner items="{Alarms}" comm-alarm="{Comm}" onack="Ack" style="border-radius:12"/>
          <DvCard title="알람 목록" badge="{Count}" grow="1">
            <DvAlarmList items="{Alarms}" onack="Ack" grow="1"/>
          </DvCard>
        </DvPageFrame>
        """;

    public sealed class Vm
    {
        private DateTime _t = new(2026, 10, 12, 14, 20, 0);

        public Vm()
        {
            Log = new AlarmLog(200, () => _t = _t.AddSeconds(97));
            Log.Raise(AlarmPriority.Minor, "필터 세척 주기 도래");
            var a = Log.Raise(AlarmPriority.Major, "약품 탱크 잔량 20% 미만");
            Log.Raise(AlarmPriority.Major, "응집조 수위 주의 구간 진입");
            var b = Log.Raise(AlarmPriority.Minor, "야간 운전 모드 전환");
            Log.Raise(AlarmPriority.Minor, "UPS 배터리 점검 필요");
            Log.Raise(AlarmPriority.Critical, "여과기 B 차압 상한 초과");
            Log.Ack(a.Id);
            Log.Ack(b.Id);
        }

        public AlarmLog Log { get; }
        public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
        public string Comm => "PLC1 응답 없음 (3초)";
        public string Count => $"{Log.Rows.Count}건 · 미확인 {Log.UnAckedCount}";

        public void Ack(AlarmRow row) => Log.Ack(row.Id);
    }

    [Fact]
    public void Render_alarms()
    {
        var dir = ReportsDir();
        DvRenderer.RenderToFile(Markup, new Vm(), 1100, 600, Path.Combine(dir, "phase6b-alarms-dark.png"), DvTheme.Dark, null, 0L);
        DvRenderer.RenderToFile(Markup, new Vm(), 1100, 600, Path.Combine(dir, "phase6b-alarms-light.png"), DvTheme.Light, null, 600L);
        Assert.True(new FileInfo(Path.Combine(dir, "phase6b-alarms-dark.png")).Length > 1000);
    }

    private static string ReportsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "docs", "superpowers");
            if (Directory.Exists(marker))
            {
                var reports = Path.Combine(marker, "reports");
                Directory.CreateDirectory(reports);
                return reports;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate docs/superpowers from test output.");
    }
}
