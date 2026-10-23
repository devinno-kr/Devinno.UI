using Devinno.UI.App;
// 컨트롤 핸들 데모 — dvml 의 id 가 같은 이름의 필드(btnStart·btnStop·lblState)로 생긴다.
// "시작" 을 누르면 버튼을 잠그고 라벨을 바꾸며, 버튼 Bounds 바로 아래에 안내 상자를 띄운다.
public partial class ControlPage : DvPage
{
    public const int Width = 640;
    public const int Height = 300;

    public bool Locked { get; set; }
    public bool ShowHint { get; private set; }
    public float PopX { get; private set; }
    public float PopY { get; private set; }

    // 화면에 연결된 뒤 한 번 불린다
    partial void OnLoaded()
    {
        btnStop.Enabled = false;
        // Click 은 onclick(Stop) 다음에 불린다 — 상태 라벨은 Stop 만 쓰고, 클릭 좌표는 따로 보여 준다
        btnStop.Click += (_, e) => lblClick.Text = $"Stop clicked at ({e.Position.X:0}, {e.Position.Y:0})";
    }

    public void Start()
    {
        btnStart.Enabled = false;
        btnStop.Enabled = true;
        lblState.Text = "Running";

        var r = btnStart.Bounds; // 직전 레이아웃의 창 좌표
        PopX = r.Left;
        PopY = r.Bottom + 8;
        ShowHint = true;
    }

    public void Stop()
    {
        btnStart.Enabled = true;
        btnStop.Enabled = false;
        lblState.Text = "Stopped";
        ShowHint = false;
    }
}
