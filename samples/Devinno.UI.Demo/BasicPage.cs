using Devinno.UI.App;
using Devinno.UI.Controls;

// 기본 컨트롤 데모 — 값 핸들: id 만 준 입력 위젯은 핸들이 값을 들고 화면에서도 눌린다.
// 바뀜 이벤트(화면·코드·바인딩)를 아래 기록에 쌓고, 핸들에서 읽은 지금 값을 보여 준다.
public sealed partial class BasicPage : DvPage
{
    public const int Width = 1360;
    public const int Height = 760;

    public bool Auto { get; set; }
    public bool AlarmSound { get; set; } = true;
    public RunMode Run { get; set; } = RunMode.Auto;
    public int Valve { get; set; } = 35;
    public double Level { get; set; } = 6.5;
    public List<RecipeItem> Recipes { get; } = Enumerable.Range(1, 9).Select(i => new RecipeItem(i, $"세척 레시피 {i:00}")).ToList();
    public int? RecipeId { get; set; } = 3;
    public List<string> Zones { get; set; } = new() { "B" };
    public string ZonesText => Zones.Count == 0 ? "(없음)" : string.Join(", ", Zones);
    public bool ShowRun { get; set; } = true;
    public string[] BatchSteps { get; } = { "원료 투입", "혼합", "가열", "숙성", "배출" };
    public int BatchStep { get; set; } = 2;

    private readonly Random _rng = new(7);
    public List<double> Flow { get; private set; } = new();
    public List<double> Pressure { get; private set; } = new();
    public double FlowNow => Flow.Count > 0 ? Flow[^1] : 0;
    public List<LineCount> Lines { get; private set; } = new();

    public void Shuffle()
    {
        Flow = Enumerable.Range(0, 20).Select(i => Math.Round(40 + 12 * Math.Sin(i / 3.0 + _rng.NextDouble()) + _rng.NextDouble() * 4, 1)).ToList();
        Pressure = Enumerable.Range(0, 30).Select(_ => Math.Round(2 + _rng.NextDouble(), 2)).ToList();
        Lines = new[] { "1라인", "2라인", "3라인", "4라인" }.Select(n => new LineCount(n, _rng.Next(20, 140))).ToList();
    }

    public List<Equip> Plant { get; } = new()
    {
        new("1공장", "F1",
            new Equip("A라인", "F1-A", new Equip("원료 펌프", "P-101"), new Equip("펌프 2", "P-102"), new Equip("혼합기", "MX-1")),
            new Equip("B라인", "F1-B", new Equip("충전기", "FL-1"))),
        new("2공장", "F2", new Equip("C라인", "F2-C", new Equip("포장기", "PK-1")), new Equip("유틸리티", "F2-U")),
    };
    public Equip? PickedEquip { get; set; }
    public string PickedEquipText => PickedEquip is { } e ? $"{e.Name} ({e.Tag})" : "(없음)";

    // 핸들 — 항목 객체를 쓰면 조상 가지가 펼쳐지고 값(item-value = Tag)이 고른 값이 된다
    public void PickPump2() => tvPlant.SelectedNode = Plant[0].Kids[0].Kids[1];

    public double PanelOpacity { get; private set; } = 1;
    public double BallX { get; private set; }
    public void TogglePanel() => PanelOpacity = PanelOpacity > 0.5 ? 0.15 : 1;
    public void BallLeft() => BallX = 0;
    public void BallRight() => BallX = 260;
    public void GoFade() => App.Show<MotionNextPage>(DvPageTransition.Fade);
    public void GoSlide() => App.Show<MotionNextPage>(DvPageTransition.Slide);

    public void PrevStep() => BatchStep = Math.Max(0, BatchStep - 1);
    public void NextStep() => BatchStep = Math.Min(BatchSteps.Length + 1, BatchStep + 1);
    public List<Section> Sections { get; } = new();

    private readonly List<string> _log = new();

    partial void OnLoaded()
    {
        tgPump.CheckedChanged += (_, e) => Log("펌프", tgPump.Checked ? "켜짐" : "꺼짐", e);
        swHeater.CheckedChanged += (_, e) => Log("히터", swHeater.Checked ? "켜짐" : "꺼짐", e);
        knSpeed.ValueChanged += (_, e) => Log("속도", $"{knSpeed.Value:0}%", e);
        segMode.SelectionChanged += (_, e) => Log("모드", $"{segMode.SelectedValue} (#{segMode.SelectedIndex})", e);
        selRecipe.SelectionChanged += (_, e) => Log("레시피", $"{selRecipe.SelectedValue}", e);
        inOperator.TextChanged += (_, e) => Log("작업자", inOperator.Text ?? "(없음)", e);
        inTemp.ValueChanged += (_, e) => Log("온도", $"{inTemp.Value}℃", e);
        tgAuto.CheckedChanged += (_, e) => Log("자동", tgAuto.Checked ? "켜짐" : "꺼짐", e);
        chkLog.CheckedChanged += (_, e) => Log("기록", chkLog.Checked ? "켬" : "끔", e);
        rdStop.CheckedChanged += (_, e) => Log("정지 라디오", rdStop.Checked ? "켜짐" : "꺼짐", e);
        slLight.ValueChanged += (_, e) => Log("조명", $"{slLight.Value:0.0}", e);
        lstShift.SelectionChanged += (_, e) => Log("교대", $"{lstShift.SelectedValue ?? "-"}", e);
        tvPlant.SelectionChanged += (_, e) => Log("트리", $"{(tvPlant.SelectedNode as Equip)?.Name ?? "-"}", e);
        expMore.ExpandedChanged += (_, e) => Log("고급 설정", expMore.IsExpanded ? "펼침" : "접힘", e);

        segMode.SelectedIndex = 0; // 배치 전 — 첫 레이아웃 때 적용
        knSpeed.Value = 40;
        lstShift.SelectedIndex = 2; // 처음엔 안 보이는 탭 — 그 탭이 처음 배치될 때 적용
        foreach (var (t, b) in new[] { ("1호 펌프", "운전 중 · 45 Hz · 12.4 A"), ("2호 펌프", "정지 · 점검 예정"), ("3호 펌프", "고장 · 과전류 트립") })
            Sections.Add(new Section(t, b, Sections));
        ShowNow();
        Shuffle();
    }

    public void CodeOn()
    {
        tgPump.Checked = true;
        swHeater.Checked = true;
        knSpeed.Value = 75;
        segMode.SelectedIndex = 1;
        selRecipe.SelectedValue = "B";
        inOperator.Text = "홍길동";
        inTemp.Value = 65;
        tgAuto.Checked = true; // 바인딩 — VM Auto 에 씀
        expMore.IsExpanded = true;
        chkLog.Checked = true;
        rdStop.Checked = true; // 바인딩 라디오 — VM Run = Stop
        slLight.Value = 0.8;
    }

    public void Reset()
    {
        tgPump.Checked = false;
        swHeater.Checked = false;
        knSpeed.Value = 0;
        segMode.SelectedIndex = 2;
        selRecipe.SelectedIndex = -1;
        inOperator.Text = null;
        inTemp.Value = null;
        tgAuto.Checked = false;
        chkLog.Checked = false;
        Run = RunMode.Auto;
    }

    private void Log(string what, string value, DvValueChangedEventArgs e)
    {
        _log.Insert(0, $"{what} = {value}  ({(e.ByUser ? "화면" : "코드")})");
        if (_log.Count > 8)
            _log.RemoveAt(_log.Count - 1);
        lblLog.Text = string.Join("\n", _log);
        ShowNow();
    }

    private void ShowNow()
        => lblNow.Text = $"펌프 {(tgPump.Checked ? "ON" : "OFF")} · 속도 {knSpeed.Value:0}% · 모드 {segMode.SelectedValue ?? "-"} · " +
                         $"레시피 {selRecipe.SelectedValue ?? "-"} · 작업자 {inOperator.Text ?? "-"} · 온도 {inTemp.Value?.ToString() ?? "-"}";
}

public enum RunMode { Auto, Manual, Stop }

// 아코디언 항목 — Open 을 켜면 같은 목록의 나머지를 닫는다(마크업은 expanded="{Bind Item.Open}" 만)
public sealed class Section(string title, string body, List<Section> all)
{
    private bool _open;
    public string Title { get; } = title;
    public string Body { get; } = body;
    public bool Open
    {
        get => _open;
        set
        {
            _open = value;
            if (value)
                foreach (var x in all.Where(x => x != this))
                    x._open = false;
        }
    }
}

public sealed record LineCount(string Name, int Count);

public sealed class Equip(string name, string tag, params Equip[] kids)
{
    public string Name { get; } = name;
    public string Tag { get; } = tag;
    public List<Equip> Kids { get; } = kids.ToList();
}
