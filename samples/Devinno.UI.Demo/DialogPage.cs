using Devinno.UI.App;

// 다이얼로그 데모(-- dialog) — "레시피 수정" 은 레시피 편집 다이얼로그를 띄우고(그 안의 "저장" 은 위에 메시지 박스를 한 번 더 띄운다),
// "내장 박스" 카드는 App.InputBox·InputNumber·SelectBox 를 띄우고 결과(취소 = null)를 보여 준다.
// 통신 시뮬레이션(SimulatedPlc)은 몇 초마다 DvApp.Current 로 알람 다이얼로그를 띄운다(같은 알람 번호는 하나만).
public sealed partial class DialogPage : DemoPage
{
    public const int Width = 960;
    public const int Height = 600;

    public DemoRecipe Recipe { get; private set; } = new("Basic wash", 65.0, 12);
    public string LastEvent { get; private set; } = "(none yet)";

    public async void EditRecipe()
    {
        DemoRecipe? edited = await App.ShowDialog<RecipeDialog, DemoRecipe>(Recipe);
        if (edited is null)
        {
            Report("Recipe edit canceled");
            return;
        }
        Recipe = edited;           // 원본 반영은 띄운 쪽이(다이얼로그는 복사본을 고쳤다)
        Report($"Recipe saved: {edited.Name}");
    }

    // 잠금 — 비밀번호 1234 로 풀린다. 이미 잠겨 있으면 같은 잠금을 기다린다.
    public async void Lock()
    {
        Report("Locked");
        await App.Lock<LockDialog>("1234");
        Report("Unlocked");
    }

    public async void AskReset()
    {
        if (await App.MessageBox("Reset the recipe to defaults?", "Reset", DvMessageButtons.YesNo) != DvDialogResult.Yes)
            return;
        Recipe = new DemoRecipe("Basic wash", 65.0, 12);
        Report("Recipe reset");
    }

    // ── 내장 박스 ──

    public static readonly string[] Modes = ["Auto", "Manual", "Stop", "Wash", "Dry", "Preheat", "Cool", "Standby", "Inspect", "Calibrate", "Test run", "E-stop"];
    public string Operator { get; private set; } = "(none)";
    public double Setpoint { get; private set; } = 25.0;
    public int Mode { get; private set; }
    public string ModeText => Modes[Mode];

    public async void AskOperator()
    {
        if (await App.InputBox("Operator name", Operator == "(none)" ? null : Operator) is { } name)
        {
            Operator = name;
            Report($"Input box: \"{name}\"");
        }
        else
            Report("Input box canceled (null)");
    }

    public async void AskSetpoint()
    {
        if (await App.InputNumber("Setpoint (℃)", Setpoint, 0, 100) is { } v)
        {
            Setpoint = v;
            Report($"Number box: {v}");
        }
        else
            Report("Number box canceled (null)");
    }

    public async void AskMode()
    {
        if (await App.SelectBox("Run mode", Modes, Mode) is { } i)
        {
            Mode = i;
            Report($"Select box: {i} ({Modes[i]})");
        }
        else
            Report("Select box canceled (null)");
    }

    public void CloseAll() => App.CloseDialogs();

    // 어느 스레드에서나 — 통신 시뮬레이션도 부른다
    public void Report(string text)
    {
        LastEvent = $"{DateTime.Now:HH:mm:ss}  {text}";
        App.Invalidate();
    }
}
