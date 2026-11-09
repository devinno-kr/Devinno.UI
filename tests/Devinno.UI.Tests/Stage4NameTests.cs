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

    // 4단계 검토 I1 — 옛 속성 이름은 실행 때 조용히 무시되므로(DvColumn enabled 는 모든 버튼이 켜짐 = 열린 쪽 실패) 화면마다 한 번 경고하고 새 이름을 알린다
    [Theory]
    [InlineData("<DvExpander header=\"A\"/>", "DvExpander 'header' was renamed to 'title'")]
    [InlineData("<DvDataGrid><DvColumn field=\"A\"/><DvSummaryRow title=\"T\" fn=\"sum\"/></DvDataGrid>", "DvSummaryRow 'title' was renamed to 'label'")]
    [InlineData("<DvProcess>" + ProcessInstrumentTests.Loop + "<DvSignal from=\"FT1\" to=\"V1\" width=\"2\"/></DvProcess>", "DvSignal 'width' was renamed to 'thickness'")]
    [InlineData("<DvDataGrid><DvColumnGroup header=\"G\"><DvColumn field=\"A\" cell-type=\"button\" enabled=\"CanAck\"/></DvColumnGroup></DvDataGrid>", "DvColumn 'enabled' was renamed to 'cell-disabled'")]
    [InlineData("<DvLamp on=\"true\" blink-when=\"true\"/>", "DvLamp 'blink-when' was renamed to 'blink'")]
    [InlineData("<DvDataGrid selection-mode=\"multi\" selection=\"{Bind Picked}\"/>", "DvDataGrid 'selection' was renamed to 'selected-items'")]
    public void OldAttribute_WarnsOncePerSurface_NamingTheNewOne(string body, string expected)
    {
        var w = Warnings(() =>
        {
            var s = new DvSurface($"<DvScreen>{body}</DvScreen>", new ProcessTests.Vm(), DvTheme.Light);
            s.Layout(1100, 700);
            s.Layout(1000, 650);
        });
        Assert.Single(w, x => x.Contains(expected));
    }

    // 뜻이 뒤집힌 것(DvColumn enabled → cell-disabled)은 그것까지 말한다 — 이름만 바꾸면 모든 버튼이 반대로 된다
    [Fact]
    public void OldAttribute_ColumnEnabled_SaysTheMeaningIsReversed()
    {
        var w = Warnings(() => Surface("<DvDataGrid><DvColumn field=\"A\" cell-type=\"button\" enabled=\"CanAck\"/></DvDataGrid>"));
        Assert.Contains(w, x => x.Contains("DvColumn 'enabled' was renamed to 'cell-disabled'") && x.Contains("meaning is reversed") && x.Contains("true = disabled"));
        // 뒤집힘은 (태그, 옛 이름) 으로 — 다른 이름 바꿈 문구에는 없다
        var other = Warnings(() => Surface("<DvExpander header=\"A\"/>"));
        Assert.DoesNotContain(other, x => x.Contains("reversed"));
    }

    // 등록 컴포넌트 틀 안의 옛 이름도 알린다
    [Fact]
    public void OldAttribute_InComponentTemplate_Warns()
    {
        var reg = new Devinno.UI.Components.ComponentRegistry();
        reg.Register("<DvComponent name=\"Panel\"><DvExpander header=\"A\"/></DvComponent>");
        var w = Warnings(() => new DvSurface("<DvScreen><Panel/></DvScreen>", new object(), DvTheme.Light, reg).Layout(600, 600));
        Assert.Contains(w, x => x.Contains("DvExpander 'header' was renamed to 'title'"));
    }

    // 새 이름만 쓰면 이름 바꿈 경고가 없다
    [Fact]
    public void NewAttributes_NoRenameWarning()
    {
        var w = Warnings(() => Surface("<DvExpander title=\"A\"/><DvLamp on=\"true\" blink=\"true\"/><DvDataGrid selection-mode=\"multi\" selected-items=\"{Bind Picked}\"><DvColumn field=\"A\" cell-type=\"button\" cell-disabled=\"X\"/><DvSummaryRow label=\"T\" fn=\"sum\"/></DvDataGrid>"));
        Assert.DoesNotContain(w, x => x.Contains("was renamed"));
    }

    // 4단계 검토 M6 — cell-disabled 는 행 항목 값이 true 일 때만 끈다. null·bool 아닌 값·없는 속성·예외 내는 getter = 켜짐(옛 enabled 의 "false 일 때만 끔" 과 같은 쪽)
    public sealed class EdgeRow
    {
        public string Name { get; set; } = "a";
        public bool? Maybe { get; set; }
        public string Word { get; set; } = "true";
        public int One { get; set; } = 1;
        public bool Throws => throw new InvalidOperationException("getter");
    }

    public sealed class EdgeVm
    {
        public List<EdgeRow> Rows { get; } = [new()];
    }

    [Theory]
    [InlineData("Maybe")]
    [InlineData("Word")]
    [InlineData("One")]
    [InlineData("Nope")]
    [InlineData("Throws")]
    public void ButtonColumn_CellDisabled_NotTrue_IsEnabled(string path)
    {
        var vm = new EdgeVm();
        var s = new DvSurface(GridLayoutBuildTests.Screen("items=\"{Rows}\"", $"<DvColumn field=\"Name\"/><DvColumn cell-type=\"button\" text=\"Go\" cell-disabled=\"{path}\"/>"),
            vm, DvTheme.Light);
        s.Layout(400, 300);
        Assert.False(s.Root!.Children[0].Grid!.Columns[1].IsButtonDisabled(vm.Rows[0]));
    }
}
