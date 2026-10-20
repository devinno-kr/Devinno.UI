using Devinno.UI;
using Devinno.UI.Editing;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
[Collection("FixedPickerClock")]
public class Grid3d2FilterSamples : FixedPickerClockSample
{
    public sealed class Row
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string State { get; init; } = "";
        public double SetPoint { get; init; }
        public DateTime ReserveAt { get; init; }
        public string Memo { get; init; } = "";
    }

    public sealed class Vm
    {
        private static readonly string[] Names = { "1호 급수 펌프", "2호 급수 펌프", "송풍기 A", "송풍기 B", "냉각수 순환 펌프", "응집제 투입기" };
        private static readonly string[] States = { "운전", "정지", "경보" };
        public List<Row> Rows { get; } = Enumerable.Range(0, 14).Select(i => new Row
        {
            Id = i + 1,
            Name = Names[i % Names.Length],
            State = States[(i * 5 + i / 3) % States.Length],
            SetPoint = 20 + (i * 7) % 60 + (i % 2) * 0.5,
            ReserveAt = new DateTime(2026, 10, 14, 6, 0, 0).AddHours(i * 4),
            Memo = i % 4 == 0 ? "정기 점검 예정" : i % 4 == 1 ? "" : "야간 가동"
        }).ToList();
    }

    private const string Markup = """
        <DvPageFrame title="설비 필터" subtitle="DvDataGrid filter = text · values · range · date">
          <DvCard title="설비 목록" grow="1">
            <DvDataGrid items="{Rows}" row-key="Id" grow="1">
              <DvColumn header="No" field="Id" width="60"/>
              <DvColumn header="설비명" field="Name" width="170" filter="values"/>
              <DvColumn header="상태" field="State" width="110" align="center" filter="values"/>
              <DvColumn header="설정값" field="SetPoint" width="120" format="0.0" unit="℃" filter="range"/>
              <DvColumn header="예약" field="ReserveAt" width="170" format="MM-dd HH:mm" filter="date"/>
              <DvColumn header="메모" field="Memo" width="180" filter="text"/>
            </DvDataGrid>
          </DvCard>
        </DvPageFrame>
        """;

    private const float W = 900, H = 640; // 날짜 선택기(약 415px)가 필터 팝업 위에 겹쳐도 화면 안에 들어가는 높이

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static SKPoint Mid(SKRect r) => new(r.MidX, r.MidY);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    // 그리드의 필터 행 column 칸(창 좌표) — 탭마다 트리가 다시 만들어지므로 매번 새로 찾는다
    private static SKPoint FilterCell(DvSurface s, int column)
    {
        var box = Flatten(s.Root!).First(b => b.Grid is not null);
        Assert.True(EditGeometry.TryLocate(s.Root!, box, out var r, out _));
        return Mid(GridGeometry.FilterCellRect(box.Grid!, r, box.ScrollX, column));
    }

    private static GridFilterPopup OpenFilter(DvSurface s, int column)
    {
        Tap(s, FilterCell(s, column));
        return Assert.IsType<GridFilterPopup>(s.TopPopup);
    }

    private static void SetField(DvSurface s, GridFilterPopup p, int field, string text)
    {
        Tap(s, Mid(p.FieldRect(field)));
        s.SetEditText(s.Edit!.SessionId, text);
        s.CommitEdit(s.Edit.SessionId, DvEditExit.FocusLost);
    }

    // values 팝업에서 항목 몇 개를 해제하고 확인
    private static void ValuesFilter(DvSurface s, int column, params string[] uncheck)
    {
        var p = OpenFilter(s, column);
        UncheckItems(s, p, uncheck);
        Tap(s, Mid(p.OkRect));
    }

    private static void UncheckItems(DvSurface s, GridFilterPopup p, string[] uncheck)
    {
        foreach (var t in uncheck)
        {
            var i = p.Items.Select(x => x.Text).ToList().IndexOf(t);
            Assert.True(i >= 0, t);
            Tap(s, Mid(p.ItemRect(i)));
        }
    }

    private static void Shot(DvTheme theme, string themeName, string name)
    {
        var s = new DvSurface(Markup, new Vm(), theme) { UseNativeEditor = true };
        s.Layout(W, H);
        Render(s).Dispose();
        switch (name)
        {
            case "rows": // 조건 없음(설정값·예약·메모) · 조건 있음(상태) · 말줄임(설비명 — 여러 값)
                ValuesFilter(s, 2, "정지");
                ValuesFilter(s, 1, "송풍기 A", "응집제 투입기");
                break;
            case "text":
            {
                var p = OpenFilter(s, 5);
                SetField(s, p, 0, "점검");
                break;
            }
            case "values":
            {
                var p = OpenFilter(s, 1);
                UncheckItems(s, p, new[] { "송풍기 B", "응집제 투입기" });
                break;
            }
            case "range":
            {
                var p = OpenFilter(s, 3);
                SetField(s, p, 1, "30");
                SetField(s, p, 2, "65.5");
                break;
            }
            case "date":
            {
                var p = OpenFilter(s, 4);
                Tap(s, Mid(p.FieldRect(1)));
                var dp = Assert.IsType<DateTimePopup>(s.TopPopup);
                dp.State.TapDay(new DateOnly(2026, 10, 15));
                break;
            }
        }

        using var bmp = Render(s);
        using var fs = File.Create(Path.Combine(ReportsDir(), $"phase3d2-grid-{name}-{themeName}.png"));
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
    }

    private static SKBitmap Render(DvSurface s)
    {
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        return bmp;
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_filter_states(string themeName)
    {
        var theme = themeName == "light" ? DvTheme.Light : DvTheme.Dark;
        foreach (var n in new[] { "rows", "text", "values", "range", "date" })
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
