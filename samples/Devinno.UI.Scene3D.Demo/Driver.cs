using System.Diagnostics;
using Devinno.UI.Scene3D;

namespace Devinno.UI.Scene3D.Demo;

/// <summary>
/// 통신 루프 흉내. 20 ms 마다 목표값을 읽어 실제값(장비 값)을 계산하고 VM 에 쓴 뒤 화면을 다시 만든다(Invalidate).
/// 크랭크-피스톤 공식처럼 장비 값끼리의 계산은 여기서 하고, 그 값이 어느 부품을 움직이는지는 마크업이 정한다.
/// </summary>
internal sealed class Driver : IDisposable
{
    // 크랭크 치수 (manifest): 크랭크 반지름, 커넥팅로드 길이
    public const double R = 0.08, L = 0.30;

    private readonly MainPage _page;
    private readonly Action _invalidate;
    private Thread? _thread;
    private volatile bool _stop;

    public Driver(MainPage page, Action invalidate)
    {
        _page = page;
        _invalidate = invalidate;
    }

    /// <summary>초. 깜빡임과 자동 패턴의 시간.</summary>
    public double Time;

    public void Start()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "Simulated PLC" };
        _thread.Start();
    }

    public void Dispose()
    {
        _stop = true;
        _thread?.Join(500);
    }

    private void Loop()
    {
        var sw = Stopwatch.StartNew();
        var last = sw.Elapsed.TotalSeconds;
        var lastText = 0.0;
        while (!_stop)
        {
            Thread.Sleep(20);
            var now = sw.Elapsed.TotalSeconds;
            Step(now - last);
            last = now;
            if (now - lastText >= 0.2)
            {
                lastText = now;
                UpdateTexts();
            }
            _invalidate(); // 바인딩 값이 바뀌었으니 화면을 다시 만든다
        }
    }

    /// <summary>시간을 dt 초 진행한다. 검증 코드는 이것을 직접 부른다.</summary>
    public void Step(double dt)
    {
        Time += dt;
        var p = _page;
        if (p.Auto)
            Pattern(Time);

        // 장비가 목표값을 일정 속도로 따라간다고 본다
        p.Turret = Approach(p.Turret, p.TurretSet, 90 * dt);
        p.Arm1 = Approach(p.Arm1, p.Arm1Set, 60 * dt);
        p.Arm2 = Approach(p.Arm2, p.Arm2Set, 60 * dt);
        p.Grip = Approach(p.Grip, p.GripSet, 150 * dt);
        p.Lift = Approach(p.Lift, p.LiftSet, 150 * dt);
        p.Door = Approach(p.Door, p.DoorSet, 60 * dt);

        // rpm → 도/초 = rpm * 6
        p.GearAngle = Wrap(p.GearAngle + p.GearRpm * 6 * dt);
        p.FanAngle = Wrap(p.FanAngle + p.FanRpm * 6 * dt);
        p.CrankAngle = Wrap(p.CrankAngle + p.CrankRpm * 6 * dt);

        Derive();
    }

    /// <summary>실제값에서 따라 나오는 값(피스톤, 커넥팅로드, 표시등 색)을 계산한다.</summary>
    public void Derive()
    {
        var p = _page;
        var (piston, rod) = CrankPose(p.CrankAngle);
        p.PistonOffset = piston * 1000;
        p.RodAngle = rod;
        p.LampColor = p.Lamp switch
        {
            "stop" => "#6B7280",
            "alarm" => Time % 0.5 < 0.25 ? "#FF2D2D" : "#400808",
            _ => "#22C55E",
        };
    }

    /// <summary>크랭크 각도(도) → 피스톤 이동(m), 커넥팅로드 각도(도).</summary>
    public static (double Piston, double Rod) CrankPose(double degrees)
    {
        var t = degrees * Math.PI / 180;
        var s = R * Math.Sin(t);
        var x = R * Math.Cos(t) + Math.Sqrt(L * L - s * s) - (R + L);
        var rod = -Math.Asin(s / L) * 180 / Math.PI;
        return (x, rod);
    }

    // 통신으로 들어오는 값 흉내
    private void Pattern(double t)
    {
        var p = _page;
        p.TurretSet = Math.Round(120 * Math.Sin(t * 2 * Math.PI / 12));
        p.Arm1Set = Math.Round(25 * Math.Sin(t * 2 * Math.PI / 8));
        p.Arm2Set = Math.Round(-35 + 35 * Math.Sin(t * 2 * Math.PI / 6 + 1));
        p.GripSet = t % 6 < 3 ? 100 : 10;
        p.LiftSet = Math.Round((-25 + 125 * Math.Sin(t * 2 * Math.PI / 10)) / 5) * 5;
        p.DoorSet = t % 16 < 8 ? 90 : 0;
    }

    public void UpdateTexts()
    {
        var p = _page;
        p.ActualText = $"Actual: turret {p.Turret:0}°, arm1 {p.Arm1:0}°, arm2 {p.Arm2:0}°, grip {p.Grip:0}%, lift {p.Lift:0} mm, door {p.Door:0}°, " +
                       $"crank {p.CrankAngle:0}°, piston {p.PistonOffset:0} mm";
        p.StatusText = $"3D render {SceneView.LastRenderMs:0.0} ms, pick {SceneView.LastPickMs:0.0} ms, renders {SceneView.RenderCount}";
    }

    private static double Approach(double value, double target, double maxStep) =>
        Math.Abs(target - value) <= maxStep ? target : value + Math.Sign(target - value) * maxStep;

    private static double Wrap(double degrees) => ((degrees % 360) + 360) % 360;
}
