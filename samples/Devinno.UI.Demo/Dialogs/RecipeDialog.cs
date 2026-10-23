using Devinno.UI.App;

public sealed record DemoRecipe(string Name, double Temp, int Minutes);

// 레시피 편집 — 받은 레시피의 값을 복사해 고치고, 저장이면 새 레시피를 결과로 돌려준다(취소 = null).
public sealed partial class RecipeDialog : DvDialog<DemoRecipe>
{
    public RecipeDialog(DemoRecipe source)
    {
        Name = source.Name;
        Temp = source.Temp;
        Minutes = source.Minutes;
    }

    public string Name { get; set; }
    public double Temp { get; set; }
    public int Minutes { get; set; }

    // 다이얼로그 위 다이얼로그 — 메시지 박스가 이 다이얼로그 위에 뜬다
    public async void Save()
    {
        if (await App.MessageBox($"Save recipe '{Name}'?", "Save", DvMessageButtons.YesNo) == DvDialogResult.Yes)
            Close(new DemoRecipe(Name, Temp, Minutes));
    }

    public void Cancel() => Close();
}
