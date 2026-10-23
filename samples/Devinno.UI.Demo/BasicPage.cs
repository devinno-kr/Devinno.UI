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
    public List<RecipeItem> Recipes { get; } = Enumerable.Range(1, 9).Select(i => new RecipeItem(i, $"Wash recipe {i:00}")).ToList();
    public int? RecipeId { get; set; } = 3;
    public List<string> Zones { get; set; } = new() { "B" };
    public string ZonesText => Zones.Count == 0 ? "(none)" : string.Join(", ", Zones);
    public bool ShowRun { get; set; } = true;
    public string[] BatchSteps { get; } = { "Charge", "Mix", "Heat", "Age", "Discharge" };
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
        Lines = new[] { "Line 1", "Line 2", "Line 3", "Line 4" }.Select(n => new LineCount(n, _rng.Next(20, 140))).ToList();
    }

    public List<Equip> Plant { get; } = new()
    {
        new("Plant 1", "F1",
            new Equip("Line A", "F1-A", new Equip("Feed pump", "P-101"), new Equip("Pump 2", "P-102"), new Equip("Mixer", "MX-1")),
            new Equip("Line B", "F1-B", new Equip("Filler", "FL-1"))),
        new("Plant 2", "F2", new Equip("Line C", "F2-C", new Equip("Packer", "PK-1")), new Equip("Utility", "F2-U")),
    };
    public Equip? PickedEquip { get; set; }
    public string PickedEquipText => PickedEquip is { } e ? $"{e.Name} ({e.Tag})" : "(none)";

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
        tgPump.CheckedChanged += (_, e) => Log("Pump", tgPump.Checked ? "on" : "off", e);
        swHeater.CheckedChanged += (_, e) => Log("Heater", swHeater.Checked ? "on" : "off", e);
        knSpeed.ValueChanged += (_, e) => Log("Speed", $"{knSpeed.Value:0}%", e);
        segMode.SelectionChanged += (_, e) => Log("Mode", $"{segMode.SelectedValue} (#{segMode.SelectedIndex})", e);
        selRecipe.SelectionChanged += (_, e) => Log("Recipe", $"{selRecipe.SelectedValue}", e);
        inOperator.TextChanged += (_, e) => Log("Operator", inOperator.Text ?? "(none)", e);
        inTemp.ValueChanged += (_, e) => Log("Temp", $"{inTemp.Value}℃", e);
        tgAuto.CheckedChanged += (_, e) => Log("Auto", tgAuto.Checked ? "on" : "off", e);
        chkLog.CheckedChanged += (_, e) => Log("Log", chkLog.Checked ? "on" : "off", e);
        rdStop.CheckedChanged += (_, e) => Log("Stop radio", rdStop.Checked ? "on" : "off", e);
        slLight.ValueChanged += (_, e) => Log("Light", $"{slLight.Value:0.0}", e);
        lstShift.SelectionChanged += (_, e) => Log("Shift", $"{lstShift.SelectedValue ?? "-"}", e);
        tvPlant.SelectionChanged += (_, e) => Log("Tree", $"{(tvPlant.SelectedNode as Equip)?.Name ?? "-"}", e);
        expMore.ExpandedChanged += (_, e) => Log("Advanced", expMore.IsExpanded ? "expanded" : "collapsed", e);

        segMode.SelectedIndex = 0; // 배치 전 — 첫 레이아웃 때 적용
        knSpeed.Value = 40;
        lstShift.SelectedIndex = 2; // 처음엔 안 보이는 탭 — 그 탭이 처음 배치될 때 적용
        foreach (var (t, b) in new[] { ("Pump 1", "Running · 45 Hz · 12.4 A"), ("Pump 2", "Stopped · service due"), ("Pump 3", "Fault · overcurrent trip") })
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
        inOperator.Text = "Alex Kim";
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
        _log.Insert(0, $"{what} = {value}  ({(e.ByUser ? "screen" : "code")})");
        if (_log.Count > 8)
            _log.RemoveAt(_log.Count - 1);
        lblLog.Text = string.Join("\n", _log);
        ShowNow();
    }

    private void ShowNow()
        => lblNow.Text = $"Pump {(tgPump.Checked ? "ON" : "OFF")} · Speed {knSpeed.Value:0}% · Mode {segMode.SelectedValue ?? "-"} · " +
                         $"Recipe {selRecipe.SelectedValue ?? "-"} · Operator {inOperator.Text ?? "-"} · Temp {inTemp.Value?.ToString() ?? "-"}";
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
