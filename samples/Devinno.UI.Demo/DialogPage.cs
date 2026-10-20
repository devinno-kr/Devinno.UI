using Devinno.UI.App;

// 다이얼로그 데모(-- dialog) — "레시피 수정" 은 레시피 편집 다이얼로그를 띄우고(그 안의 "저장" 은 위에 메시지 박스를 한 번 더 띄운다),
// "내장 박스" 카드는 App.InputBox·InputNumber·SelectBox 를 띄우고 결과(취소 = null)를 보여 준다.
// 통신 시뮬레이션(SimulatedPlc)은 몇 초마다 DvApp.Current 로 알람 다이얼로그를 띄운다(같은 알람 번호는 하나만).
public sealed partial class DialogPage : DvPage
{
    public const int Width = 960;
    public const int Height = 600;

    public DemoRecipe Recipe { get; private set; } = new("기본 세척", 65.0, 12);
    public string LastEvent { get; private set; } = "(아직 없음)";

    public async void EditRecipe()
    {
        DemoRecipe? edited = await App.ShowDialog<RecipeDialog, DemoRecipe>(Recipe);
        if (edited is null)
        {
            Report("레시피 수정 취소");
            return;
        }
        Recipe = edited;           // 원본 반영은 띄운 쪽이(다이얼로그는 복사본을 고쳤다)
        Report($"레시피 저장: {edited.Name}");
    }

    // 잠금 — 비밀번호 1234 로 풀린다. 이미 잠겨 있으면 같은 잠금을 기다린다.
    public async void Lock()
    {
        Report("잠금");
        await App.Lock<LockDialog>("1234");
        Report("잠금 해제");
    }

    public async void AskReset()
    {
        if (await App.MessageBox("레시피를 기본값으로 되돌릴까요?", "초기화", DvMessageButtons.YesNo) != DvDialogResult.Yes)
            return;
        Recipe = new DemoRecipe("기본 세척", 65.0, 12);
        Report("레시피 초기화");
    }

    // ── 내장 박스 ──

    public static readonly string[] Modes = ["자동", "수동", "정지", "세척", "건조", "예열", "냉각", "대기", "점검", "보정", "시운전", "비상 정지"];
    public string Operator { get; private set; } = "(없음)";
    public double Setpoint { get; private set; } = 25.0;
    public int Mode { get; private set; }
    public string ModeText => Modes[Mode];

    public async void AskOperator()
    {
        if (await App.InputBox("작업자 이름", Operator == "(없음)" ? null : Operator) is { } name)
        {
            Operator = name;
            Report($"입력 박스: \"{name}\"");
        }
        else
            Report("입력 박스 취소(null)");
    }

    public async void AskSetpoint()
    {
        if (await App.InputNumber("설정 온도(℃)", Setpoint, 0, 100) is { } v)
        {
            Setpoint = v;
            Report($"숫자 박스: {v}");
        }
        else
            Report("숫자 박스 취소(null)");
    }

    public async void AskMode()
    {
        if (await App.SelectBox("운전 모드", Modes, Mode) is { } i)
        {
            Mode = i;
            Report($"선택 박스: {i} ({Modes[i]})");
        }
        else
            Report("선택 박스 취소(null)");
    }

    public void CloseAll() => App.CloseDialogs();

    // 어느 스레드에서나 — 통신 시뮬레이션도 부른다
    public void Report(string text)
    {
        LastEvent = $"{DateTime.Now:HH:mm:ss}  {text}";
        App.Invalidate();
    }
}
