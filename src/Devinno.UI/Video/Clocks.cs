using System.Diagnostics;

namespace Devinno.UI.Video;

// 재생 시계(초). 비디오 표시 스레드가 프레임 PTS 와 비교한다
internal interface IClock
{
    double Now { get; }

    // true = 멈춤(그 자리에서 멈춘다), false = 이어서 흐른다
    void Pause(bool pause);

    // 지금 시각을 pts 로 다시 잡는다(seek·처음). 멈춤 상태는 그대로
    void Reset(double pts);
}

// Stopwatch 시계 — 오디오 스트림이 없는(또는 소리를 못 내는) 파일
internal sealed class SystemClock : IClock
{
    private readonly object _gate = new();
    private readonly Stopwatch _sw = new();
    private double _base;
    private bool _paused = true;

    public double Now
    {
        get
        {
            lock (_gate)
                return _base + _sw.Elapsed.TotalSeconds;
        }
    }

    public void Pause(bool pause)
    {
        lock (_gate)
        {
            if (pause == _paused)
                return;
            _paused = pause;
            if (pause)
            {
                _base += _sw.Elapsed.TotalSeconds;
                _sw.Reset();
            }
            else
                _sw.Start();
        }
    }

    public void Reset(double pts)
    {
        lock (_gate)
        {
            _base = pts;
            if (_paused)
                _sw.Reset();
            else
                _sw.Restart();
        }
    }
}

// 오디오 장치 시계 — AudioOutput.CurrentTime(장치로 나간 위치)을 따른다.
//  - 장치는 큐에서 ChunkSeconds 덩어리씩 가져가므로 CurrentTime 은 계단처럼 오른다. 그 덩어리가 지금 들리는 중이라 보고
//    "가져간 덩어리의 시작 + 그 뒤 흐른 시간(덩어리 길이까지)" 으로 매끄럽게 잇는다.
//  - 큐가 비면(소리가 늦게 오거나 오디오 트랙이 비디오보다 짧음) 시스템 시간으로 계속 흐른다 — 비디오가 소리를 기다리며 멈추지 않게.
//  - 값은 Reset 사이에서 거꾸로 가지 않는다.
// 장치 일시정지는 플레이어가 AudioOutput.Pause 로 따로 한다(이 시계는 값만 멈춘다)
internal sealed class AudioClock : IClock
{
    private readonly object _gate = new();
    private readonly AudioOutput _output;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private bool _paused = true;
    private double _frozen;            // 멈춘 동안의 값
    private double _base, _baseAt;     // 장치 시각을 모를 때(Clear 뒤 첫 소리 전): base + 흐른 시간
    private double _lastRaw = double.NaN, _rawAt;
    private double _last = double.NegativeInfinity;

    public AudioClock(AudioOutput output) => _output = output;

    public double Now
    {
        get
        {
            lock (_gate)
                return _paused ? _frozen : Compute();
        }
    }

    public void Pause(bool pause)
    {
        lock (_gate)
        {
            if (pause == _paused)
                return;
            if (pause)
            {
                _frozen = Compute();
                _paused = true;
                return;
            }

            // 멈췄던 값에서 그대로 이어지게 기준 시각을 옮긴다(재개 때 화면이 튀거나 서지 않게)
            _paused = false;
            var t = T;
            _baseAt = t - (_frozen - _base);
            if (!double.IsNaN(_lastRaw))
                _rawAt = t - Math.Clamp(_frozen - (_lastRaw - _output.ChunkSeconds), 0, _output.ChunkSeconds);
        }
    }

    public void Reset(double pts)
    {
        lock (_gate)
        {
            _base = pts;
            _baseAt = T;
            _frozen = pts;
            _lastRaw = double.NaN;
            _last = pts;
        }
    }

    private double T => _sw.Elapsed.TotalSeconds;

    private double Compute()
    {
        var t = T;
        var raw = _output.CurrentTime;
        double v;
        if (double.IsNaN(raw))
            v = _base + (t - _baseAt);
        else
        {
            if (raw != _lastRaw)
            {
                _lastRaw = raw;
                _rawAt = t;
            }
            var chunk = _output.ChunkSeconds;
            var since = t - _rawAt;
            v = raw - chunk + (_output.QueuedSeconds > 0 ? Math.Min(since, chunk) : since);
        }
        if (v > _last)
            _last = v;
        return _last;
    }
}
