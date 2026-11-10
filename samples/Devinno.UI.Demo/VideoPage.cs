using Devinno.UI.Video;
using SkiaSharp;

// 동영상 데모 — 왼쪽 = 파일 재생(src, FFmpeg 8, 소리는 SDL2), 오른쪽 = 코드가 만든 프레임(FrameSource, 카메라 흉내)
public sealed partial class VideoPage : DemoPage
{
    public const int Width = 1100;
    public const int Height = 620;

    private Thread? _producer;
    private volatile bool _cameraOn = true;
    private volatile bool _shown;

    // 페이지가 보일 때만 프레임을 만든다
    protected override void OnShown()
    {
        _shown = true;
        if (_producer is null)
        {
            _producer = new Thread(Produce) { IsBackground = true, Name = "Demo camera" };
            _producer.Start();
        }
    }

    protected override void OnHidden() => _shown = false;

    public string Clip { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "demo-clip.mp4");

    public bool Playing { get; set; } = true;
    public string PlayText => Playing ? "Pause" : "Play";
    public string PlayIcon => Playing ? "fa-pause" : "fa-play";

    public void TogglePlay() => Playing = !Playing;

    // 소리 — 클립은 매초 흰 사각형과 삐 소리가 함께 나온다(립싱크 확인용, 0초만 높은 음)
    public double Volume { get; set; } = 0.8;
    public bool Muted { get; set; }
    public string MuteIcon => Muted ? "fa-volume-xmark" : "fa-volume-high";
    public string VolumeText => Muted ? "muted" : $"{Volume * 100:0} %";

    public void ToggleMute() => Muted = !Muted;

    // DvVideo 가 재생 위치·길이(초)를 써 넣고, 슬라이더가 Position 을 바꾸면 그 위치로 이동
    public double Position { get; set; }
    public double Duration { get; set; }
    public string TimeText => $"{Position:0.0} / {Duration:0.0} s";

    public FrameSource Camera { get; } = new();

    public bool CameraOn
    {
        get => _cameraOn;
        set
        {
            _cameraOn = value;
            Camera.IsLive = value; // 멈추면 화면도 확인을 멈춘다(마지막 프레임은 남음)
        }
    }

    // 30fps 로 움직이는 그림을 만들어 Publish — 실제 앱이라면 카메라·디코더 스레드
    private void Produce()
    {
        var start = Environment.TickCount64;
        var info = new SKImageInfo(480, 270);
        using var surface = SKSurface.Create(info);
        using var font = new SKFont(SKTypeface.Default, 28);
        using var paint = new SKPaint { IsAntialias = true };
        var frame = 0;
        while (true)
        {
            Thread.Sleep(33);
            if (!_cameraOn || !_shown)
                continue;
            var t = (Environment.TickCount64 - start) / 1000f;
            var c = surface.Canvas;
            c.Clear(new SKColor(0x10, 0x18, 0x28));
            for (var i = 0; i < 6; i++)
            {
                paint.Color = SKColor.FromHsv((i * 60 + t * 40) % 360, 70, 90);
                var x = 240 + MathF.Cos(t * 1.3f + i) * 160;
                var y = 135 + MathF.Sin(t * 1.7f + i * 2) * 90;
                c.DrawCircle(x, y, 22, paint);
            }
            paint.Color = SKColors.White;
            c.DrawText($"frame {frame++}   {t:0.0} s", 24, 135, SKTextAlign.Left, font, paint);
            Camera.Publish(surface.Snapshot());
        }
    }
}
