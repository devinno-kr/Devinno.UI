using Devinno.UI;
using Devinno.UI.Grid;
using SkiaSharp;
using Xunit;

// 3b 리뷰 수정 — float·decimal 요약 표시, 그리기 중 목록 축소, ulong·decimal 정렬 정밀도, 타입 섞인 열
public class GridReviewFixTests
{
    public sealed class Row
    {
        public List<Row>? Owner;
        public int ShrinkOnRead;
        public string Name { get; set; } = "";
        public decimal Dec { get; set; }
        public float Flt { get; set; }
        public ulong Big { get; set; }
        public object? Mixed { get; set; }

        // 읽힐 때 목록을 줄인다 — 그리는 도중 다른 스레드가 행을 지운 것과 같다
        public int Shrinking
        {
            get
            {
                if (ShrinkOnRead > 0 && Owner is { Count: > 0 } o)
                {
                    o.RemoveRange(o.Count - Math.Min(ShrinkOnRead, o.Count), Math.Min(ShrinkOnRead, o.Count));
                    ShrinkOnRead = 0;
                }

                return 1;
            }
        }
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; set; } = new();
    }

    private static DvSurface Surface(Vm vm, string cols)
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", cols), vm);
        s.Layout(300, 300);
        return s;
    }

    private static GridSpec G(DvSurface s) => s.Root!.Children[0].Grid!;

    private static void Render(DvSurface s)
    {
        using var bmp = new SKBitmap(300, 300);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 300, 300);
    }

    private static void TapHeader(DvSurface s, float x)
    {
        s.PointerDown(new SKPoint(x, 20));
        s.PointerUp(new SKPoint(x, 20));
        s.Layout(300, 300);
    }

    [Fact]
    public void Decimal_summary_is_exact_and_float_has_no_binary_noise()
    {
        var vm = new Vm { Rows = Enumerable.Range(0, 3).Select(_ => new Row { Dec = 0.1m, Flt = 0.1f }).ToList() };
        var s = Surface(vm, "<DvColumn field=\"Name\" width=\"60\"/><DvColumn field=\"Dec\" width=\"80\"/><DvColumn field=\"Flt\"/>" +
                            "<DvSummaryRow title=\"합\" fn=\"sum\"/><DvSummaryRow title=\"최대\" fn=\"max\"/>");
        Render(s);
        Assert.Equal("0.3", G(s).Summaries[0].Texts[1]);
        Assert.Equal("0.1", G(s).Summaries[1].Texts[1]);
        Assert.Equal("0.1", G(s).Summaries[1].Texts[2]);
    }

    [Fact]
    public void List_shrinking_while_sorting_or_summing_does_not_throw()
    {
        var vm = new Vm();
        vm.Rows = Enumerable.Range(0, 50).Select(i => new Row { Owner = vm.Rows, Name = "R" + i }).ToList();
        foreach (var r in vm.Rows)
            r.Owner = vm.Rows;
        vm.Rows[3].ShrinkOnRead = 20;
        var s = Surface(vm, "<DvColumn field=\"Name\" width=\"60\"/><DvColumn field=\"Shrinking\" sortable=\"true\" sort=\"asc\"/>" +
                            "<DvSummaryRow title=\"합\" fn=\"sum\"/>");
        Render(s); // 정렬 키를 읽는 중에 목록이 줄어든다
        vm.Rows[1].ShrinkOnRead = 10;
        s.Repaint();
        Render(s); // 요약을 계산하는 중에 줄어든다
        Render(s);
        Assert.Equal(vm.Rows.Count, G(s).Count);
    }

    [Fact]
    public void Ulong_and_decimal_sort_exactly()
    {
        var vm = new Vm
        {
            Rows = new List<Row>
            {
                new() { Name = "a", Big = ulong.MaxValue, Dec = 1.0000000000000000000000000002m },
                new() { Name = "b", Big = ulong.MaxValue - 1, Dec = 1.0000000000000000000000000001m }
            }
        };
        var s = Surface(vm, "<DvColumn field=\"Big\" width=\"120\" sortable=\"true\"/><DvColumn field=\"Dec\" sortable=\"true\"/>");
        TapHeader(s, 60);
        Assert.Equal("b", ((Row)G(s).ItemAt(0)!).Name);
        TapHeader(s, 60);
        TapHeader(s, 60); // Big 해제
        TapHeader(s, 200);
        Assert.Equal("b", ((Row)G(s).ItemAt(0)!).Name);
    }

    [Fact]
    public void Mixed_datetime_and_int_sort_by_type_rank_not_raw_ticks()
    {
        var vm = new Vm
        {
            Rows = new List<Row>
            {
                new() { Name = "date", Mixed = new DateTime(1, 1, 1, 0, 0, 0).AddTicks(5) }, // 틱 5
                new() { Name = "int", Mixed = 100 }
            }
        };
        var s = Surface(vm, "<DvColumn field=\"Name\" width=\"60\"/><DvColumn field=\"Mixed\" sortable=\"true\"/>");
        TapHeader(s, 150);
        // 숫자(순위 0)가 DateTime(순위 2)보다 앞 — 틱 5 < 100 으로 비교하지 않는다
        Assert.Equal("int", ((Row)G(s).ItemAt(0)!).Name);
    }
}
