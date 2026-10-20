using Devinno.UI;
using Devinno.UI.Styling;
using Devinno.UI.Trends;
using Xunit;

// 데모(TrendPage)와 같은 화면 — 고정 시각·고정 시드로 결정적 렌더
[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P6bTrendSamples
{
    public const string Markup = """
        <DvPageFrame title="실시간 트렌드" subtitle="폴링 스레드 → TrendBuffer → Invalidate · 최근 2분">
          <DvCard title="공정 값" badge="2 min" grow="1">
            <DvTrend source="{Trend}" window="120" area="true" grow="1">
              <DvTrendChannel name="temp" label="온도" unit="C" color="#22d3ee"/>
              <DvTrendChannel name="press" label="압력" unit="bar" color="#a78bfa"/>
            </DvTrend>
          </DvCard>
          <DvGrid columns="2">
            <DvCard title="유량" badge="m3/h">
              <DvTrend source="{Trend}" window="120" y-min="0" y-max="200" style="height:150">
                <DvTrendChannel name="flow" label="유량" unit="m3/h" color="#10b981"/>
              </DvTrend>
            </DvCard>
            <DvCard title="전력" badge="kW">
              <DvTrend source="{Trend}" window="120" area="true" smooth="false" style="height:150">
                <DvTrendChannel name="power" label="전력" unit="kW" color="#f59e0b"/>
              </DvTrend>
            </DvCard>
          </DvGrid>
        </DvPageFrame>
        """;

    public sealed class Vm
    {
        private readonly Random _rnd = new(7);
        private double _t;

        public TrendBuffer Trend { get; } = new(2000);

        /// <summary>start 시각부터 seconds 만큼 0.5초 간격 샘플을 채운다.</summary>
        public void Seed(DateTime start, int seconds)
        {
            for (var i = 0; i <= seconds * 2; i++)
                Sample(start.AddMilliseconds(i * 500));
        }

        public void Sample(DateTime now)
        {
            _t += 0.5;
            Trend.Add("temp", now, 62 + 9 * Math.Sin(_t / 9) + 2.5 * Math.Sin(_t / 2.3) + _rnd.NextDouble());
            Trend.Add("press", now, 41 + 6 * Math.Sin(_t / 14 + 1) + 1.5 * Math.Cos(_t / 3.1));
            Trend.Add("flow", now, 120 + 35 * Math.Sin(_t / 11) + 6 * _rnd.NextDouble());
            Trend.Add("power", now, 380 + 40 * Math.Sin(_t / 7) + (_t % 20 < 4 ? 60 : 0));
        }
    }

    [Fact]
    public void Render_trend()
    {
        var vm = new Vm();
        vm.Seed(new DateTime(2026, 10, 12, 14, 0, 0), 150);
        var dir = ReportsDir();
        DvRenderer.RenderToFile(Markup, vm, 1100, 640, Path.Combine(dir, "phase6b-trend-dark.png"), DvTheme.Dark);
        DvRenderer.RenderToFile(Markup, vm, 1100, 640, Path.Combine(dir, "phase6b-trend-light.png"), DvTheme.Light);
        Assert.True(new FileInfo(Path.Combine(dir, "phase6b-trend-dark.png")).Length > 1000);
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
