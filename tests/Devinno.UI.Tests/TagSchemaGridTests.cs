using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// 2.5 그리드 — 정의된 속성을 모두 채운 DvDataGrid(열·그룹·요약 행 포함)를 빌드하고, 리더로 읽은 이름과 정의를 맞춘다.
// 자식 노드(DvColumn 등)도 그 노드의 리더로 읽혀야 기록된다.
public class TagSchemaGridTests
{
    public sealed class Row
    {
        public string Kind { get; set; } = "a";
        public double Qty { get; set; } = 1;
        public bool Ok { get; set; } = true;
        public string Cls { get; set; } = "";
    }

    public sealed class Opt
    {
        public string V { get; set; } = "a";
        public string T { get; set; } = "A";
        public string I { get; set; } = "fa-play";
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; } = [new(), new() { Kind = "b", Qty = 2 }];
        public List<Opt> Opts { get; } = [new(), new() { V = "b", T = "B" }];
        public List<Row> Selected { get; set; } = [];
        public double Total => 3;
        public void Click(Row row) { }
        public void RowClick(Row row) { }
        public bool Before(Row row, bool v) => true;
    }

    private const string Markup =
        """
        <DvDataGrid items="{Rows}" row-height="40" header-height="36" selection-mode="multi" selection="{Bind Selected}" row-key="Kind" row-class="Cls" onrowclick="RowClick"
                    header-color="#111111" header-text-color="#eeeeee" text-color="#222222" row-color="#ffffff" row-alt-color="#f0f0f0"
                    hover-color="#dddddd" selected-color="#ccccee" line-color="#999999" summary-color="#eeeeee" summary-text-color="#000000">
          <DvColumnGroup header="G">
            <DvColumn header="H" field="Kind" width="100" align="center" format="0.0" frozen="false" sortable="true" sort="asc" cell-class="Cls"
                      cell-type="select" filter="values" readonly="false" onbeforecommit="Before" on-color="#00ff00" blink="true" text="Go" icon="fa-play"
                      variant="danger" onclick="Click" enabled="Ok" min="0" max="10" unit="kg" mode="date" show-seconds="true"
                      items="{Opts}" item-value="V" item-text="T" item-icon="I"/>
          </DvColumnGroup>
          <DvColumn field="Qty"/>
          <DvSummaryRow title="Total" fn="sum"><DvSummary field="Qty" fn="avg" format="0.0" value="{Total}"/></DvSummaryRow>
        </DvDataGrid>
        """;

    public static TheoryData<string> Tags => new() { "DvDataGrid", "DvColumn", "DvColumnGroup", "DvSummaryRow", "DvSummary" };

    private static HashSet<string> ReadsOf(string tag)
    {
        var root = MarkupParser.Parse($"<DvScreen>{Markup}</DvScreen>");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AttrReader.Watch = (n, name) =>
        {
            if (n.Tag == tag)
                seen.Add(name);
        };
        try
        {
            new LayoutEngine().Layout(root, new Vm(), 800, 600);
        }
        finally
        {
            AttrReader.Watch = null;
        }

        return seen;
    }

    private static IReadOnlyList<AttrDef> Declared(string tag)
    {
        Assert.True(TagSchema.Tags.TryGetValue(tag, out var defs), $"{tag} has no TagSchema entry");
        return defs!;
    }

    [Theory]
    [MemberData(nameof(Tags))]
    public void Schema_Grid_ReadsAreDeclared(string tag)
    {
        var allowed = Declared(tag).Select(a => a.Name).Concat(TagSchema.Common.Select(a => a.Name)).ToHashSet();
        var extra = ReadsOf(tag).Where(n => !allowed.Contains(n)).OrderBy(n => n).ToList();
        Assert.True(extra.Count == 0, $"{tag} reads undeclared attributes: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(Tags))]
    public void Schema_Grid_DeclaredAreRead(string tag)
    {
        var read = ReadsOf(tag);
        var missing = Declared(tag).Where(a => a.Note?.Contains("unused") != true && !read.Contains(a.Name)).Select(a => a.Name).ToList();
        Assert.True(missing.Count == 0, $"{tag} declares attributes the build never read: {string.Join(", ", missing)}");
    }
}
