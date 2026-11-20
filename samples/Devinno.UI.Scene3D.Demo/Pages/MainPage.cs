using Devinno.UI.App;

namespace Devinno.UI.Scene3D.Demo;

/// <summary>
/// 화면 값. 3D 모델에 대해서는 모른다(노드 이름·축·단위 없음). 장비 값만 둔다.
/// 어느 부품이 어느 값으로 움직이는지는 MainPage.dvml 의 SceneView 자식이 정한다.
/// </summary>
public sealed partial class MainPage : DvPage
{
    public string Camera { get; set; } = "CamFront";
    public string? Selected { get; set; }
    public string SelectedText => string.IsNullOrEmpty(Selected) ? "Click a part to select it" : $"Selected: {Selected}";

    // 목표값 (슬라이더, 통신 쓰기)
    public bool Auto { get; set; }
    public double TurretSet { get; set; }
    public double Arm1Set { get; set; } = 20;
    public double Arm2Set { get; set; } = -30;
    public double GripSet { get; set; } = 50;
    public double LiftSet { get; set; }
    public double DoorSet { get; set; }
    public double GearRpm { get; set; } = 10;
    public double FanRpm { get; set; } = 30;
    public double CrankRpm { get; set; } = 15;
    public string CrankMode { get; set; } = "formula";
    public string Lamp { get; set; } = "run";

    // 실제값 (통신 읽기 흉내) — 3D 가 이 값에 바인딩된다
    public double Turret { get; set; }
    public double Arm1 { get; set; } = 20;
    public double Arm2 { get; set; } = -30;
    public double Grip { get; set; } = 50;    // %
    public double Lift { get; set; }          // mm
    public double Door { get; set; }          // 도
    public double GearAngle { get; set; }     // 도
    public double FanAngle { get; set; }      // 도
    public double CrankAngle { get; set; }    // 0~360 도
    public double PistonOffset { get; set; }  // mm, C# 공식으로 계산
    public double RodAngle { get; set; }      // 도, C# 공식으로 계산
    public string LampColor { get; set; } = "#22C55E";

    public bool UseClip => CrankMode == "clip";
    public bool UseFormula => !UseClip;

    public string ActualText { get; set; } = "";
    public string StatusText { get; set; } = "";
}
