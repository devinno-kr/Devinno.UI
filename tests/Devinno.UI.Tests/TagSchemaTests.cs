using System.Text.RegularExpressions;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// 태그 정의 한 벌(TagSchema) — 빌드 코드는 정의된 속성만, 리더를 거쳐 읽는다.
public class TagSchemaTests
{
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
        ("src/Devinno.UI/Layout/LayoutEngine.cs", 31),
        ("src/Devinno.UI/Layout/LayoutSugar.cs", 0),
        ("src/Devinno.UI/Layout/Builders/LayoutEngine.Containers.cs", 0),
        ("src/Devinno.UI/Layout/Builders/LayoutEngine.Inputs.cs", 0),
        ("src/Devinno.UI/Process/ProcessModel.cs", 65),
        ("src/Devinno.UI/Grid/GridBuilder.cs", 9),
        ("src/Devinno.UI/Grid/GridColors.cs", 2),
        ("src/Devinno.UI/Grid/GridColumn.cs", 1),
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

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "dvml-ui-markup.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}
