using Devinno.UI;
using Devinno.UI.Editing;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
[Collection("FixedPickerClock")]
public class Grid3d1EditSamples : FixedPickerClockSample
{
    public sealed class Row
    {
        public int Id { get; init; }
        public string Name { get; set; } = "";
        public double SetPoint { get; set; }
        public string Mode { get; set; } = "auto";
        public int RecipeId { get; set; } = 1;
        public DateTime ReserveAt { get; set; }
        public string Pen { get; set; } = "#3B82F6";
    }

    public sealed record Recipe(int Id, string Name);

    public sealed class Vm
    {
        private static readonly string[] Pens = { "#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#A855F7", "#14B8A6" };
        public List<Row> Rows { get; } = Enumerable.Range(1, 12).Select(i => new Row
        {
            Id = i, Name = $"펌프 {i:00}", SetPoint = 20 + i * 3.5, Mode = i % 3 == 0 ? "manual" : "auto", RecipeId = i % 3 + 1,
            ReserveAt = new DateTime(2026, 10, 14, 8, 0, 0).AddHours(i * 5), Pen = Pens[i % Pens.Length]
        }).ToList();
        public List<Recipe> Catalog { get; } = new() { new(1, "세척"), new(2, "살균"), new(3, "건조") };
        public bool CheckSetPoint(Row r, double v) => v <= 100;
    }

    private const string Markup = """
        <DvPageFrame title="편집 셀" subtitle="DvDataGrid cell-type = input · select · datetime · color">
          <DvCard title="레시피" grow="1">
            <DvDataGrid items="{Rows}" row-key="Id" grow="1">
              <DvColumn header="No" field="Id" width="60"/>
              <DvColumn header="이름" field="Name" width="140" cell-type="input"/>
              <DvColumn header="설정값" field="SetPoint" width="110" cell-type="input" min="0" max="120" unit="℃" format="0.0" onbeforecommit="CheckSetPoint"/>
              <DvColumn header="모드" field="Mode" width="110" cell-type="select">
                <DvOption value="auto" text="자동" icon="fa-robot"/>
                <DvOption value="manual" text="수동" icon="fa-hand"/>
              </DvColumn>
              <DvColumn header="레시피" field="RecipeId" width="110" cell-type="select" items="{Catalog}" item-value="Id" item-text="Name"/>
              <DvColumn header="예약" field="ReserveAt" width="140" cell-type="datetime" mode="datetime" show-seconds="false" format="MM-dd HH:mm"/>
              <DvColumn header="색" field="Pen" cell-type="color"/>
            </DvDataGrid>
          </DvCard>
        </DvPageFrame>
        """;

    private const float W = 900, H = 760; // 달력 팝업(약 415px)이 편집 셀 아래에 다 들어가는 높이

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
    }

    // 그리드의 화면 행 view·열 column 가운데(창 좌표)
    private static SKPoint CellMid(DvSurface s, int view, int column)
    {
        var box = Flatten(s.Root!).First(b => b.Grid is not null);
        Assert.True(EditGeometry.TryLocate(s.Root!, box, out var r, out _));
        var cell = GridGeometry.CellRect(box.Grid!, r, box.ScrollX, box.ScrollY, view, column, out _);
        return new SKPoint(cell.MidX, cell.MidY);
    }

    private static void Shot(DvTheme theme, string themeName, string name)
    {
        long now = 0;
        var s = new DvSurface(Markup, new Vm(), theme) { Clock = () => now, UseNativeEditor = true };
        Render(s).Dispose();
        switch (name)
        {
            case "input": // 가상 키보드로 설정값 입력 중
                s.UseNativeEditor = false;
                Tap(s, CellMid(s, 1, 2));
                Render(s).Dispose(); // 키보드가 열리기 시작(now=0)
                now = 10_000; // 키보드가 다 올라온 시각
                Render(s).Dispose();
                // 사용자처럼 키패드 키를 눌러 입력(키보드 미리보기 줄과 셀이 같은 글자)
                foreach (var id in new[] { SoftKeyboard.Clear, "3", "7", SoftKeyboard.Dot, "5" })
                {
                    var k = s.Keyboard!.Keys(W, H, now).First(k => k.Id == id);
                    Tap(s, new SKPoint(k.Rect.MidX, k.Rect.MidY));
                    Render(s).Dispose();
                }

                break;
            case "select":
                Tap(s, CellMid(s, 1, 3));
                break;
            case "datetime":
                Tap(s, CellMid(s, 1, 5));
                break;
            case "color":
                Tap(s, CellMid(s, 1, 6));
                break;
        }

        using var bmp = Render(s);
        using var fs = File.Create(Path.Combine(ReportsDir(), $"phase3d1-grid-{name}-{themeName}.png"));
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
    }

    private static SKBitmap Render(DvSurface s)
    {
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        return bmp;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_edit_cells(string themeName)
    {
        var theme = themeName == "light" ? DvTheme.Light : DvTheme.Dark;
        foreach (var n in new[] { "closed", "input", "select", "datetime", "color" })
            Shot(theme, themeName, n);
    }

    private static string ReportsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "docs", "superpowers");
            if (Directory.Exists(marker))
            {
                var reports = Path.Combine(marker, "reports");
                Directory.CreateDirectory(reports);
                return reports;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate docs/superpowers from test output.");
    }
}
