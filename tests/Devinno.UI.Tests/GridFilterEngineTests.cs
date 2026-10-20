using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 3d-2 거르기 — text(보이는 글자·format·select·대소문자·공백)·values(OR·빈 값)·range/date(경계·한쪽·빈 값), AND, 정렬·요약·행 수·Repaint
public class GridFilterEngineTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public double? Level { get; set; }
        public DateTime At { get; set; }
        public bool On { get; set; }
        public string Mode { get; set; } = "a";
    }

    public sealed class Vm
    {
        // Id 0..9, Name = "Pump0".."Pump4", "Valve5".."Valve9" (7 = null), Level = Id*10 (3 = null), At = 2026-10-01 + Id 일, On = 짝수
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 10).Select(i => new Row
        {
            Id = i, Name = i == 7 ? null : (i < 5 ? "Pump" : "Valve") + i, Level = i == 3 ? null : i * 10,
            At = new DateTime(2026, 10, 1).AddDays(i), On = i % 2 == 0, Mode = i % 3 == 0 ? "b" : "a"
        }).ToList();
    }

    private const string Cols =
        "<DvColumn header=\"Id\" field=\"Id\" width=\"40\" sortable=\"true\"/>" +
        "<DvColumn header=\"N\" field=\"Name\" width=\"80\" filter=\"text\" sortable=\"true\"/>" +
        "<DvColumn header=\"L\" field=\"Level\" width=\"60\" format=\"0.0\" filter=\"auto\"/>" +
        "<DvColumn header=\"T\" field=\"At\" width=\"80\" filter=\"auto\"/>" +
        "<DvColumn header=\"O\" field=\"On\" width=\"40\" filter=\"auto\"/>" +
        "<DvColumn header=\"M\" field=\"Mode\" width=\"60\" cell-type=\"select\" filter=\"auto\"><DvOption value=\"a\" text=\"자동\"/><DvOption value=\"b\" text=\"수동\"/></DvColumn>" +
        // 6~8 = 종류가 다른 조건이 버려지지 않도록 text(Level)·text(Mode)·values(Name) 로 고정한 열(전체 폭이 500 을 넘지 않게 좁게)
        "<DvColumn header=\"LT\" field=\"Level\" width=\"40\" format=\"0.0\" filter=\"text\"/>" +
        "<DvColumn header=\"MT\" field=\"Mode\" width=\"40\" cell-type=\"select\" filter=\"text\"><DvOption value=\"a\" text=\"자동\"/><DvOption value=\"b\" text=\"수동\"/></DvColumn>" +
        "<DvColumn header=\"NV\" field=\"Name\" width=\"40\" filter=\"values\"/>";

    private static (DvSurface s, GridSpec g, Vm vm) Make(string? summary = null)
    {
        var vm = new Vm();
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", Cols + (summary ?? "")), vm);
        s.Layout(500, 400);
        return (s, s.Root!.Children[0].Grid!, vm);
    }

    // Repaint 는 다음 그리기에서 처리된다(레이아웃만 다시 하면 반영되지 않음) — 한 프레임 그린다
    private static void Render(DvSurface s)
    {
        using var bmp = new SKBitmap(500, 400);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 500, 400);
    }

    private static int[] Ids(GridSpec g)
    {
        GridSorter.Ensure(g);
        return Enumerable.Range(0, g.ViewCount).Select(v => ((Row)g.ItemAtCurrent(v)!).Id).ToArray();
    }

    private static GridFilterCondition Text(string t) => new() { Text = t, Summary = t };
    private static GridFilterCondition Set(params object?[] v) => new() { Values = new HashSet<object?>(v), Summary = "" };
    private static GridFilterCondition Range(object? min, object? max) => new() { Min = min, Max = max, Summary = "" };

    [Fact]
    public void No_filter_keeps_order_null()
    {
        var (_, g, _) = Make();
        GridSorter.Ensure(g);
        Assert.Null(g.Order);
        Assert.Equal(10, g.ViewCount);
    }

    [Fact]
    public void Text_matches_visible_text_case_insensitive_and_trimmed()
    {
        var (_, g, _) = Make();
        g.SetFilter(1, Text("  pUMP "));
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, Ids(g));
    }

    [Fact]
    public void Text_uses_format_and_select_display_text()
    {
        var (_, g, _) = Make();
        g.SetFilter(6, Text("40.0")); // format 0.0 → "40.0" 에만 맞음(원시값 "40" 이 아님)
        Assert.Equal(new[] { 4 }, Ids(g));
        g.SetFilter(6, null);
        g.SetFilter(7, Text("수동"));
        Assert.Equal(new[] { 0, 3, 6, 9 }, Ids(g));
    }

    [Fact]
    public void Text_empty_input_is_no_condition_and_null_cell_does_not_match()
    {
        var (_, g, _) = Make();
        g.SetFilter(1, Text("   "));
        Assert.Equal(10, Ids(g).Length);
        g.SetFilter(1, Text("e"));
        Assert.DoesNotContain(7, Ids(g)); // Name null
    }

    [Fact]
    public void Values_or_within_and_empty_value_item()
    {
        var (_, g, _) = Make();
        g.SetFilter(4, Set(true));
        Assert.Equal(new[] { 0, 2, 4, 6, 8 }, Ids(g));
        g.SetFilter(8, Set("Pump0", null));
        Assert.Equal(new[] { 0 }, Ids(g)); // AND: On=true 이면서 (Pump0 또는 빈 값 — 7 은 On=false)
    }

    [Fact]
    public void Range_inclusive_one_sided_and_null_excluded()
    {
        var (_, g, _) = Make();
        g.SetFilter(2, Range(20.0, 50.0));
        Assert.Equal(new[] { 2, 4, 5 }, Ids(g)); // 3 = null 탈락
        g.SetFilter(2, Range(80.0, null));
        Assert.Equal(new[] { 8, 9 }, Ids(g));
        g.SetFilter(2, Range(null, 0.0));
        Assert.Equal(new[] { 0 }, Ids(g));
    }

    [Fact]
    public void Date_range_inclusive()
    {
        var (_, g, _) = Make();
        g.SetFilter(3, Range(new DateTime(2026, 10, 3), new DateTime(2026, 10, 5)));
        Assert.Equal(new[] { 2, 3, 4 }, Ids(g));
    }

    [Fact]
    public void Sort_applies_only_to_filtered_rows_with_typed_keys()
    {
        var (s, g, _) = Make();
        g.SetFilter(4, Set(true)); // 0,2,4,6,8
        GridSorter.Toggle(g, 0);   // Id 오름(기본형 키 경로)
        GridSorter.Toggle(g, 0);   // Id 내림
        Assert.Equal(new[] { 8, 6, 4, 2, 0 }, Ids(g));
        GridSorter.Toggle(g, 1);   // + Name(문자열 키, 다중 정렬 = 비교자 경로)
        Assert.Equal(5, Ids(g).Length);
    }

    [Fact]
    public void View_count_drives_layout_rows_and_hit()
    {
        var (s, g, _) = Make();
        g.SetFilter(4, Set(true));
        s.Repaint();
        Render(s);
        var box = s.Root!.Children[0];
        Assert.Equal(5, g.Count);
        Assert.Equal(g.HeaderTotal + 5 * g.RowHeight, box.ContentHeight - g.SummaryHeight, 1);
        Assert.Equal(-1, Devinno.UI.Interaction.GridInteraction.RowAt(box, new SKPoint(20, g.HeaderTotal + 5 * g.RowHeight + 10)));
    }

    [Fact]
    public void Summary_counts_visible_rows_only()
    {
        var (s, g, _) = Make("<DvSummaryRow title=\"합\"><DvSummary field=\"Level\" fn=\"sum\"/></DvSummaryRow>");
        g.SetFilter(4, Set(true));
        GridSorter.Ensure(g);
        GridSummary.Compute(g);
        Assert.Equal("200.0", g.Summaries[0].Texts[2]); // Level 0+20+40+60+80 (Texts = 열 번호별 요약 글자, 0번 열 = 제목이라 Level 열로 확인)
    }

    [Fact]
    public void Repaint_refilters_changed_values()
    {
        var (s, g, vm) = Make();
        g.SetFilter(4, Set(true));
        Assert.Equal(5, Ids(g).Length);
        vm.Rows[0].On = false;
        s.Repaint();
        Render(s);
        Assert.Equal(new[] { 2, 4, 6, 8 }, Ids(g));
    }

    [Fact]
    public void Filter_to_zero_rows_is_safe()
    {
        var (s, g, _) = Make();
        g.SetFilter(1, Text("zzz"));
        Assert.Empty(Ids(g));
        Assert.Null(g.NextVisibleItem(-1, _ => true));
        using var bmp = new SKBitmap(500, 400);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 500, 400); // 예외 없음
        s.PointerMove(new SKPoint(20, 200));
    }

    [Fact]
    public void Next_visible_item_skips_hidden_rows()
    {
        var (_, g, vm) = Make();
        g.SetFilter(4, Set(true));
        GridSorter.Ensure(g);
        Assert.Same(vm.Rows[2], g.NextVisibleItem(0, _ => true));
    }

    [Fact]
    public void Sort_tap_refilters_current_values_and_prunes_selection_and_summary()
    {
        var (_, g, vm) = Make("<DvSummaryRow title=\"합\"><DvSummary field=\"Level\" fn=\"sum\"/></DvSummaryRow>");
        g.SetFilter(4, Set(true)); // 0,2,4,6,8
        Assert.Equal(5, Ids(g).Length);
        g.SetSelection(new object[] { vm.Rows[0] });
        GridSummary.Compute(g);
        g.SelectionPruned = false;
        Assert.False(g.SummaryDirty);

        vm.Rows[0].On = false; // Repaint 없이 VM 값만 바뀜
        GridSorter.Toggle(g, 0); // 정렬 헤더 탭 → 순서 재구성 때 현재 값으로 다시 거른다

        Assert.DoesNotContain(0, Ids(g));
        Assert.Empty(g.SelectedItems);
        Assert.True(g.SelectionPruned);
        Assert.True(g.SummaryDirty);
    }
}
