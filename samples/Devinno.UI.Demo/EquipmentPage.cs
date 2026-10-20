using Devinno.UI.App;
// 스크롤 + 반복 데모 — VM 컬렉션을 DvRepeat 로 그린 세로 목록(휠·드래그·관성·손잡이)과 가로 계기 줄.
// 행 버튼은 그 행의 설비를 VM 메서드로 받고(Inspect(Equipment)), 토글은 그 설비의 Auto 에 커밋된다.
// 끌어서 스크롤하면 버튼은 눌리지 않는다.
public sealed partial class EquipmentPage : DvPage
{
    public sealed class Equipment
    {
        public required string Name { get; init; }
        public required string State { get; init; }
        public required string StatusText { get; init; }
        public bool Auto { get; set; }
    }

    private static readonly string[] Kinds = { "취수 펌프", "송수 펌프", "약품 주입기", "여과기", "역세 밸브", "배수 밸브", "교반기", "송풍기" };
    private static readonly (string State, string Text)[] States =
        { ("run", "운전"), ("run", "운전"), ("run", "운전"), ("stop", "정지"), ("warn", "주의"), ("run", "운전"), ("fault", "경보"), ("off", "대기") };

    public List<Equipment> Items { get; } = Enumerable.Range(0, 40).Select(i => new Equipment
    {
        Name = $"{Kinds[i % Kinds.Length]} #{i / Kinds.Length + 1}",
        State = States[i % States.Length].State,
        StatusText = States[i % States.Length].Text,
        Auto = i % 3 == 0
    }).ToList();

    public List<double> Pressures { get; } = Enumerable.Range(0, 10).Select(i => 20.0 + i * 7 % 70).ToList();

    public Equipment? Selected { get; private set; }
    public string SelectedText => Selected is null ? "선택: 없음" : $"선택: {Selected.Name}";
    public string AutoText => $"자동 {Items.Count(e => e.Auto)}대";
    public string CountText => $"{Items.Count}대";

    public void Inspect(Equipment e) => Selected = e;
}
