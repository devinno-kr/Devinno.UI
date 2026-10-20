using Devinno.UI.App;
using Devinno.UI.Trends;

// 고속 트렌드 데모 — 1초 = 그래프 전체 폭, 1 kHz 샘플. 생성 스레드가 밀린 샘플을 몰아서 넣고 Invalidate 한다.
public sealed partial class FastTrendPage : DvPage
{
    public const int Width = 1100;
    public const int Height = 640;

    private readonly Random _rnd = new(3);
    private long _nextMs;

    public TrendBuffer Trend { get; } = new(1000);

    /// <summary>origin 기준 경과 ms 까지 1 ms 간격 샘플을 채운다(밀린 만큼 한 번에).</summary>
    public void CatchUp(DateTime origin, long elapsedMs)
    {
        for (; _nextMs <= elapsedMs; _nextMs++)
        {
            var t = _nextMs / 1000.0;
            var time = origin.AddTicks(_nextMs * TimeSpan.TicksPerMillisecond);
            Trend.Add("a", time, 80 * Math.Sin(2 * Math.PI * 25 * t));
            Trend.Add("b", time, 45 * Math.Sin(2 * Math.PI * 7 * t + 1) + 12 * (_rnd.NextDouble() - 0.5));
        }
    }
}
