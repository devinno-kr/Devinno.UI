using Devinno.UI.App;
// 입력(DvInput) 데모 — OpenTK 는 가상 키보드, WinForms 는 숨긴 TextBox 로 같은 화면을 편집한다.
// WinForms 데모(Devinno.UI.Demo.WinForms)가 이 파일을 링크해 같은 마크업·VM 을 쓴다.
public sealed partial class InputPage : DvPage
{
    public const int Width = 1000;
    public const int Height = 760;

    public string Name { get; set; } = "Wash process B";
    public string Password { get; set; } = "";
    public string Memo { get; set; } = "Step 1: preheat, then add cleaning fluid.\nAbove 60℃, lower the pump speed.";
    public double Temp { get; set; } = 62.5;
    public int Repeat { get; set; } = 3;
    public int Offset { get; set; } = -5;
    public string Machine { get; set; } = "WASH-02";
    public string Saved { get; set; } = "Not saved yet";
    public string Mode { get; set; } = "auto";
    public Rotation Direction { get; set; } = Rotation.Forward;
    public int? BaseRecipe { get; set; }
    public DateTime ReserveAt { get; set; } = new(2026, 10, 14, 9, 30, 0);
    public DateOnly Day { get; set; } = new(2026, 10, 14);
    public TimeOnly Start { get; set; } = new(6, 15, 30);
    public string Pen { get; set; } = "#3B82F6";
    public List<RecipeItem> Recipes { get; } = Enumerable.Range(1, 12)
        .Select(i => new RecipeItem(i, $"Standard wash {i:00}")).ToList();

    public void Save()
        => Saved = $"Saved {DateTime.Now:HH:mm:ss} — {Name} / {Temp:0.0}℃ / {Repeat} times / offset {Offset} / {Mode} / {Direction}" +
                   (Password.Length > 0 ? " / password changed" : "");
}

public enum Rotation { Forward, Reverse }

public sealed record RecipeItem(int Id, string Name);
