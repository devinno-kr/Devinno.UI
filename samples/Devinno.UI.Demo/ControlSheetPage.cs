using Devinno.UI.Alarms;
using Devinno.UI.App;
using Devinno.UI.Trends;

// 컨트롤 시트 — 모든 컨트롤·위젯을 상태별로 한 화면에 모아 테마마다 비교하는 확인용 페이지.
// "각각 예쁜데 함께 놓으면 어긋나는" 문제를 잡는 기준 화면이라, 다른 데모 타입에 기대지 않고 데이터를 여기서 다 만든다.
// 실행: -- sheet [light|graphite|carbon],  -- shot <폴더> 는 sheet-<테마>.png 4장을 함께 만든다.
public sealed partial class ControlSheetPage : DvPage
{
    public const int Width = 1600;
    public const int Height = 2400;

    // 입력
    public bool On { get; set; } = true;
    public bool Off { get; set; }
    public string Mode { get; set; } = "auto";
    public string Radio { get; set; } = "a";
    public string Name { get; set; } = "Line 2";
    public string Empty { get; set; } = "";
    public double Setpoint { get; set; } = 72.5;
    public int? Recipe { get; set; } = 2;
    public DateTime When { get; set; } = new(2026, 11, 16, 9, 30, 0);
    public string Pen { get; set; } = "#3b82f6";
    public double Rate { get; set; } = 40;
    public bool Expanded { get; set; } = true;
    public List<SheetItem> Recipes { get; } = Enumerable.Range(1, 5).Select(i => new SheetItem(i, $"Recipe {i:00}")).ToList();
    public List<SheetNode> Tree { get; } =
    [
        new("Plant 1", new SheetNode("Line A", new SheetNode("Pump P-101"), new SheetNode("Pump P-102")), new SheetNode("Line B")),
        new("Plant 2"),
    ];
    public SheetNode? PickedNode { get; set; }

    // 데이터
    public List<SheetRow> Rows { get; } =
    [
        new("P-101", "Raw water pump", 72, true, "run"),
        new("P-102", "Standby pump", 0, true, "stop"),
        new("F-302", "Filter bed 2", 0, false, "fault"),
        new("M-401", "Flocculator", 47, true, "run"),
    ];
    public TrendBuffer Trend { get; } = new(capacity: 240);
    public List<SheetBar> Bars { get; } = [new("Mon", 42), new("Tue", 55), new("Wed", 38), new("Thu", 61), new("Fri", 49)];
    public double[] Spark { get; } = Enumerable.Range(0, 40).Select(i => 50 + 12 * Math.Sin(i / 4.0)).ToArray();
    public AlarmLog Log { get; } = new(maxRows: 20);
    public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
    public string[] Steps { get; } = ["Filter", "Drain", "Air scour", "Backwash", "Rinse"];

    // 계기
    public double Pressure { get; set; } = 5.8;
    public double PressureHigh { get; set; } = 8.9;
    public double Level { get; set; } = 62;
    public double LevelHigh { get; set; } = 93;
    public double Speed { get; set; } = 75;
    public double Knob { get; set; } = 0.6;

    public ControlSheetPage()
    {
        var now = DateTime.Now;
        for (var i = 120; i >= 0; i--)
        {
            var t = now.AddSeconds(-i);
            Trend.Add("a", t, 1100 + 80 * Math.Sin(i / 15.0));
            Trend.Add("b", t, 1050 + 60 * Math.Cos(i / 20.0));
        }

        Log.Raise(AlarmPriority.Critical, "Filter F-2 differential pressure high-high", "F-2");
        Log.Raise(AlarmPriority.Major, "Flocculation basin level high", "T-2");
        Log.Raise(AlarmPriority.Minor, "Backwash cycle due on filter F-3", "F-3");
    }

    public void Ack(AlarmRow row) => Log.Ack(row.Id);
}

public sealed record SheetItem(int Id, string Name);

public sealed class SheetNode(string name, params SheetNode[] kids)
{
    public string Name { get; } = name;
    public List<SheetNode> Kids { get; } = kids.ToList();
}

public sealed class SheetRow(string tag, string description, double load, bool auto, string state)
{
    public string Tag { get; } = tag;
    public string Description { get; } = description;
    public double Load { get; } = load;
    public bool Auto { get; set; } = auto;
    public string State { get; } = state;
    public bool ResetDisabled => State != "fault";
}

public sealed record SheetBar(string Name, double Value);
