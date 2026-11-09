using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Xunit;

// 4단계 이름 통일 — 새 이름이 동작하고, 옛 이름은 읽지 않는다(호환 별칭 없음, 결정 2). 옛 이름의 빌드 진단은 DvmlGeneratorTests.
public class Stage4NameTests
{
    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static DvSurface Surface(string body, object? vm = null, float w = 600, float h = 600)
    {
        var s = new DvSurface($"<DvScreen>{body}</DvScreen>", vm ?? new object(), DvTheme.Light);
        s.Layout(w, h);
        return s;
    }

    private static string? ExpanderTitle(DvSurface s) => Flatten(s.Root!).Single(b => b.Node.Tag == "DvExpanderHeader").ResolvedText;

    // N1 — DvExpander header → title
    [Fact]
    public void Expander_Title_IsTheHeading_HeaderIsNotRead()
    {
        Assert.Equal("Pumps", ExpanderTitle(Surface("<DvExpander title=\"Pumps\"/>")));
        Assert.Equal("", ExpanderTitle(Surface("<DvExpander header=\"Pumps\"/>")));
    }

    // N1 — DvSummaryRow title → label
    [Fact]
    public void SummaryRow_Label_IsTheFirstCell_TitleIsNotRead()
    {
        static string Label(string attr)
        {
            var vm = new GridSummaryTests.Vm();
            var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", $"<DvColumn field=\"Name\"/><DvColumn field=\"Qty\"/><DvSummaryRow {attr}=\"Total\" fn=\"sum\"/>"),
                vm, DvTheme.Light);
            s.Layout(300, 300);
            return s.Root!.Children[0].Grid!.Summaries[0].Title;
        }

        Assert.Equal("Total", Label("label"));
        Assert.Equal("", Label("title"));
    }
}
