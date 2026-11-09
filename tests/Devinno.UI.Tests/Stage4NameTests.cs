using Devinno.UI;
using Devinno.UI.Grid;
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

    private sealed class Capture : System.Diagnostics.TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && message.Contains("Devinno.UI"))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(System.Diagnostics.TraceEventCache? c, string source, System.Diagnostics.TraceEventType type, int id, string? message) => WriteLine(message);
    }

    private static List<string> Warnings(Action act)
    {
        var cap = new Capture();
        System.Diagnostics.Trace.Listeners.Add(cap);
        try
        {
            act();
            lock (cap.Lines) return cap.Lines.ToList();
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(cap);
        }
    }

    // N6 — DvProcess fit="shrink" → "scale-down" (CSS object-fit). 옛 값은 모르는 값: 줄이지 않고, 경고가 새 값을 보여 준다
    [Fact]
    public void ProcessFit_ScaleDown_Shrinks_OldShrinkIsUnknown()
    {
        var (_, d) = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"scale-down\"", 420, 220);
        Assert.True(d.Shrink);
        Devinno.UI.Process.ProcessDiagram? old = null;
        var w = Warnings(() => old = ProcessTests.Start(ProcessInstrumentTests.Loop, "fit=\"shrink\"", 420, 220).D);
        Assert.False(old!.Shrink);
        Assert.Contains(w, x => x.Contains("fit='shrink'") && x.Contains("scale-down"));
    }

    // N7 — DvSignal width → thickness (DvRing thickness; width stays the box/body width). 옛 이름은 읽지 않는다
    [Fact]
    public void Signal_Thickness_IsTheStroke_WidthIsNotRead()
    {
        static float? Stroke(string attr) => ProcessTests.Start(ProcessInstrumentTests.Loop.Replace("""<DvSignal from="FIC1" to="V1"/>""", $"""<DvSignal from="FIC1" to="V1" {attr}="4"/>"""))
            .D.Signals.Single(s => s.ToRef == "V1").Width;

        Assert.Equal(4f, Stroke("thickness"));
        Assert.Null(Stroke("width"));
    }

    // N10 — DvColumn enabled → cell-disabled, 뜻도 뒤집힘: 행 항목의 bool 이 true 면 버튼이 꺼진다. 옛 enabled 는 읽지 않는다(모든 버튼 켜짐)
    [Fact]
    public void ButtonColumn_CellDisabled_TrueDisables_EnabledIsNotRead()
    {
        static GridColumn Col(string attr)
        {
            var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", $"<DvColumn field=\"Name\"/><DvColumn cell-type=\"button\" text=\"Go\" {attr}=\"AckDisabled\"/>"),
                new GridCellTypeTests.Vm(), DvTheme.Light);
            s.Layout(400, 300);
            return s.Root!.Children[0].Grid!.Columns[1];
        }

        var row = new GridCellTypeTests.Row { AckDisabled = true };
        Assert.True(Col("cell-disabled").IsButtonDisabled(row));
        Assert.False(Col("cell-disabled").IsButtonDisabled(new GridCellTypeTests.Row()));
        Assert.False(Col("enabled").IsButtonDisabled(row));
    }

    // N11 — DvLamp blink-when 삭제(별칭이었다). blink 하나만 읽는다
    [Fact]
    public void Lamp_Blink_Only_BlinkWhenIsNotRead()
    {
        static bool Blink(string attrs) => Flatten(Surface($"<DvLamp on=\"true\" {attrs}/>").Root!).Single(b => b.Node.Tag == "DvLamp").LampBlink;

        Assert.True(Blink("blink=\"true\""));
        Assert.False(Blink("blink-when=\"true\""));
        Assert.True(Blink("blink=\"true\" blink-when=\"false\"")); // 옛: blink-when 이 이겼다
    }

    // N14 — DvDataGrid selection → selected-items (DvListBox 와 같은 이름·뜻). 옛 이름은 읽지 않는다
    [Fact]
    public void Grid_SelectedItems_IsTheSelection_SelectionIsNotRead()
    {
        static GridSpec Grid(string attr, GridLayoutBuildTests.Vm vm)
        {
            var s = new DvSurface(GridLayoutBuildTests.Screen($"items=\"{{Rows}}\" selection-mode=\"multi\" {attr}=\"{{Bind Picked}}\""), vm, DvTheme.Light);
            s.Layout(400, 300);
            return s.Root!.Children[0].Grid!;
        }

        var vm = new GridLayoutBuildTests.Vm();
        vm.Picked.Add(vm.Rows[3]);
        Assert.True(Grid("selected-items", vm).IsSelected(vm.Rows[3]));
        Assert.False(Grid("selection", vm).IsSelected(vm.Rows[3]));
    }
}
