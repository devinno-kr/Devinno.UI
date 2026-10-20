using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

// 3d-1 편집 셀 테스트 공용 — 행·VM·마크업·좌표 헬퍼
public enum MachineKind { Pump, Valve, Fan }

public sealed record EditRecipe(int Id, string Name);

public struct EditPt
{
    public int X { get; set; }
}

public sealed class EditRow
{
    private string _name;
    private double _setPoint;
    private string _mode;
    private DateTime _reserveAt;
    private string _pen;

    public EditRow(int i)
    {
        Id = i;
        _name = "R" + i;
        _setPoint = 20 + i;
        _mode = i % 2 == 0 ? "auto" : "manual";
        _reserveAt = new DateTime(2026, 10, 14, 9, 30, 0).AddDays(i);
        _pen = "#0000FF";
    }

    public int Id { get; }
    /// <summary>편집 열(이름·설정값·모드·예약·색) 속성 쓰기 수.</summary>
    public int Writes;
    public string Name { get => _name; set { _name = value; Writes++; } }
    public double SetPoint { get => _setPoint; set { _setPoint = value; Writes++; } }
    public string Mode { get => _mode; set { _mode = value; Writes++; } }
    public DateTime ReserveAt { get => _reserveAt; set { _reserveAt = value; Writes++; } }
    public string Pen { get => _pen; set { _pen = value; Writes++; } }

    public int? Count { get; set; } = 3;
    public uint Cycles { get; set; } = 7;
    public MachineKind Kind { get; set; } = MachineKind.Pump;
    public int RecipeId { get; set; } = 1;
    public SKColor Fill { get; set; } = new(0, 0, 255, 0x80);
    public DateOnly Day { get; set; } = new(2026, 10, 14);
    public EditPt Pos { get; set; }                 // 구조체 경로 Pos.X = 쓰기 불가
    public string Computed => "C" + Id;             // setter 없음
}

public sealed class EditVm
{
    public List<EditRow> Rows { get; set; } = Enumerable.Range(0, 10).Select(i => new EditRow(i)).ToList();
    public List<EditRow> Picked { get; set; } = new();
    public List<EditRow> Clicked { get; } = new();
    public void Inspect(EditRow r) => Clicked.Add(r);
    public List<EditRecipe> Catalog { get; } = new() { new(1, "A"), new(2, "B"), new(3, "C") };

    public bool Allow = true;
    public List<(EditRow Row, double Value)> SetPointGate { get; } = new();
    public bool CheckSetPoint(EditRow r, double v) { SetPointGate.Add((r, v)); return Allow; }
    public List<string> NameGate { get; } = new();
    public bool CheckName(string v) { NameGate.Add(v); return Allow; }
    public List<string> ModeGate { get; } = new();
    public bool CheckMode(string v) { ModeGate.Add(v); return Allow; }
    public List<(EditRow Row, DateTime Value)> AtGate { get; } = new();
    public bool CheckAt(EditRow r, DateTime v) { AtGate.Add((r, v)); return Allow; }
    public bool Throws(EditRow r, string v) => throw new InvalidOperationException("거절");
}

internal static class GridEditFixture
{
    public const float W = 600, H = 700;

    public const string IdCol = "<DvColumn header=\"Id\" field=\"Id\" width=\"60\" sortable=\"true\"/>";

    // Id 60 | 이름 100 | 설정값 100 | 모드 100 | 예약 100 | 색 100 → 편집 열 k 의 가운데 x = 110 + 100k, 행 r 가운데 y = 64 + 48r
    // 그리드 높이 240 = 헤더 40 + 몸통 200(행 0~3 다 보임, 행 4 는 232~240 만), 10행 = 세로 넘침(오른쪽 12 스크롤바 자리)
    public const string Cols = IdCol +
        "<DvColumn header=\"이름\" field=\"Name\" width=\"100\" cell-type=\"input\"/>" +
        "<DvColumn header=\"설정값\" field=\"SetPoint\" width=\"100\" cell-type=\"input\" min=\"0\" max=\"120\" unit=\"℃\" format=\"0.0\"" +
        " onbeforecommit=\"CheckSetPoint\" sortable=\"true\"/>" +
        "<DvColumn header=\"모드\" field=\"Mode\" width=\"100\" cell-type=\"select\">" +
        "<DvOption value=\"auto\" text=\"자동\" icon=\"fa-robot\"/><DvOption value=\"manual\" text=\"수동\" icon=\"fa-hand\"/></DvColumn>" +
        "<DvColumn header=\"예약\" field=\"ReserveAt\" width=\"100\" cell-type=\"datetime\" format=\"MM-dd HH:mm\"/>" +
        "<DvColumn header=\"색\" field=\"Pen\" width=\"100\" cell-type=\"color\"/>";

    public const string Attrs = "items=\"{Rows}\" row-key=\"Id\" selection-mode=\"single\" selection=\"{Bind Picked}\" onrowclick=\"Inspect\"";

    public static string Grid(string cols = Cols, string attrs = Attrs)
        => $"<DvDataGrid {attrs} style=\"height:240\">{cols}</DvDataGrid>";

    public static string Screen(string cols = Cols, string attrs = Attrs)
        => "<DvScreen style=\"padding:0; gap:0; display:flex; flex-direction:column; align-items:stretch\">" + Grid(cols, attrs) + "</DvScreen>";

    public static DvSurface Surface(EditVm vm, string cols = Cols, bool native = true, string attrs = Attrs)
    {
        var s = new DvSurface(Screen(cols, attrs), vm, DvTheme.Light) { UseNativeEditor = native };
        s.Layout(W, H);
        return s;
    }

    public static LayoutBox GridBox(DvSurface s, int index = 0) => s.Root!.Children[index];
    public static float X(int k) => 110 + 100 * k;
    public static float Y(int row) => 64 + 48 * row;
    public static SKPoint Cell(int k, int row) => new(X(k), Y(row));
    public static SKRect CellRect(int k, int row) => SKRect.Create(60 + 100 * k, 40 + 48 * row, 100, 48);
    public static SKPoint Mid(SKRect r) => new(r.MidX, r.MidY);

    public static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    public static SKBitmap Render(DvSurface s, long ts = 0)
    {
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H, ts);
        return bmp;
    }
}
