using Devinno.UI.App;
using Devinno.UI.Trends;

// 실시간 트렌드 데모 — 타이머(폴링 스레드)가 TrendBuffer 에 샘플을 넣고 화면 갱신을 요청한다.
public sealed partial class TrendPage : DvPage
{
    public const int Width = 1100;
    public const int Height = 640;

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
