using System.Text.RegularExpressions;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// 태그 정의 한 벌(TagSchema) — 빌드 코드는 정의된 속성만, 리더를 거쳐 읽는다.
public class TagSchemaTests
{
    // 정의의 Note 는 지금 동작만 적는다 — 개발 단계·옛 이름 이력("stage 4 N1: was header")을 쓰지 않는다
    [Fact]
    public void Notes_DescribeCurrentBehaviorOnly()
    {
        var defs = TagSchema.Common.Select(a => ("(common)", a)).Concat(TagSchema.Tags.SelectMany(t => t.Value.Select(a => (t.Key, a))));
        var bad = defs.Where(x => x.a.Note is { } note && Regex.IsMatch(note, @"\bstage \d|\bwas (removed|renamed)|\balias\b|without conversion|every frame|shared by all instances"))
            .Select(x => $"{x.Item1} {x.a.Name}: {x.a.Note}").ToList();
        Assert.True(bad.Count == 0, string.Join(Environment.NewLine, bad));
    }

    [Fact]
    public void Common_ContainsCommonAttributes()
    {
        string[] expected =
        [
            "id", "class", "style", "grow", "width", "height", "visible", "disabled", "onclick", "hold",
            "hover-style", "press-style", "disabled-style", "content-align", "onbeforecommit",
        ];
        Assert.All(expected, n => Assert.Contains(TagSchema.Common, a => a.Name == n));
        Assert.Equal(TagSchema.Common.Count, TagSchema.Common.Select(a => a.Name).Distinct().Count());
    }

    [Fact]
    public void Reader_Watch_RecordsReads()
    {
        var seen = new List<string>();
        AttrReader.Watch = (_, name) => seen.Add(name);
        try
        {
            var r = new AttrReader(MarkupParser.Parse("<DvLabel text=\"a\"/>"), null, null);
            _ = r.Text("text");
            _ = r.Has("max");
            _ = r.Raw("onclick");
            _ = r.Value("min");
        }
        finally
        {
            AttrReader.Watch = null;
        }
        Assert.Equal(["text", "max", "onclick", "min"], seen);
    }

    // 빌드 코드의 node.Attributes 직접 읽기 수 — 그룹 과제마다 줄인다(2단계 끝 = 0). 늘면 실패.
    // DvSymbol 정의 파싱(SymbolDef.cs)은 등록 시점 코드라 대상이 아니다.
    private static readonly (string File, int Max)[] DirectReadBudget =
    [
        ("src/Devinno.UI/Layout/LayoutEngine.cs", 17),
        ("src/Devinno.UI/Layout/LayoutSugar.cs", 0),
        ("src/Devinno.UI/Layout/Builders/LayoutEngine.Containers.cs", 0),
        ("src/Devinno.UI/Layout/Builders/LayoutEngine.Inputs.cs", 0),
        ("src/Devinno.UI/Layout/Builders/LayoutEngine.Instruments.cs", 0),
        ("src/Devinno.UI/Process/ProcessModel.cs", 0),
        ("src/Devinno.UI/Grid/GridBuilder.cs", 0),
        ("src/Devinno.UI/Grid/GridColors.cs", 0),
        ("src/Devinno.UI/Grid/GridColumn.cs", 0),
        ("src/Devinno.UI/Editing/SelectOption.cs", 0),
    ];

    [Fact]
    public void BuildCode_DirectAttributeReads_DoNotGrow()
    {
        var root = RepoRoot();
        foreach (var (file, max) in DirectReadBudget)
        {
            var count = Regex.Matches(File.ReadAllText(Path.Combine(root, file)), @"\.Attributes\b").Count;
            Assert.True(count <= max, $"{file}: {count} direct .Attributes reads (budget {max}) — read through AttrReader");
        }
    }

    // 3단계 R9 — 태그가 쓰지 않는 속성(dead read)은 정의에도 읽기에도 없다(쓰면 DVML019). 정의는 그 태그가 실제로 쓰는 것만
    [Fact]
    public void Schema_HasNoDeadReads()
    {
        var dead = TagSchema.Tags
            .SelectMany(t => t.Value.Where(a => a.Note is { } n && (n.Contains("dead read") || n.Contains("unused"))).Select(a => $"{t.Key} {a.Name}"))
            .ToList();
        Assert.True(dead.Count == 0, "dead reads left: " + string.Join(", ", dead));
    }

    [Theory]
    [InlineData("DvTabs", "placeholder")]
    [InlineData("DvTabs", "items")]
    [InlineData("DvTabs", "item-text")]
    [InlineData("DvSegment", "placeholder")]
    [InlineData("DvAlarmBanner", "row-height")]
    [InlineData("DvAlarmList", "comm-alarm")]
    [InlineData("DvGauge", "tone")]
    [InlineData("DvGauge", "direction")]
    [InlineData("DvGauge", "label")]
    [InlineData("DvGauge", "sweep")]
    [InlineData("DvGauge", "thickness")]
    [InlineData("DvRing", "ticks")]
    [InlineData("DvRing", "minor-ticks")]
    [InlineData("DvRing", "show-tick-labels")]
    [InlineData("DvRing", "direction")]
    [InlineData("DvProgress", "ticks")]
    [InlineData("DvProgress", "label")]
    [InlineData("DvProgress", "sweep")]
    [InlineData("DvProgress", "thickness")]
    [InlineData("DvSparkline", "show-value")]
    [InlineData("DvSparkline", "inner-radius")]
    [InlineData("DvBarChart", "area")]
    [InlineData("DvBarChart", "inner-radius")]
    [InlineData("DvPieChart", "tone")]
    [InlineData("DvPieChart", "y-min")]
    [InlineData("DvPieChart", "area")]
    [InlineData("DvPump", "text")]
    [InlineData("DvVessel", "text")]
    [InlineData("DvTerminal", "rotate")]
    public void Schema_DeadReadsRemoved(string tag, string attr)
        => Assert.DoesNotContain(TagSchema.Tags[tag], a => a.Name == attr);

    // R8 로 쓰이게 된 것은 남는다
    [Theory]
    [InlineData("DvGauge", "unit")]
    [InlineData("DvGauge", "show-value")]
    [InlineData("DvTerminal", "text")]
    [InlineData("DvRing", "tone")]
    [InlineData("DvProgress", "direction")]
    public void Schema_UsedAttributesKept(string tag, string attr)
        => Assert.Contains(TagSchema.Tags[tag], a => a.Name == attr);

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "dvml-ui-markup.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}
