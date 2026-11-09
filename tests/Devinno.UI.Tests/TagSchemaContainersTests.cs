using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// 2.2 공통 + 컨테이너 — 정의된 속성을 모두 채운 마크업을 빌드하고, 리더로 읽은 이름과 정의를 맞춘다.
public class TagSchemaContainersTests
{
    public sealed class Vm
    {
        public List<string> Items { get; } = ["a", "b"];
        public string Sel { get; set; } = "a";
    }

    // 태그 → 그 태그의 정의된 속성을 모두 채운 마크업(필요하면 자식 포함)
    public static TheoryData<string, string> Cases => new()
    {
        { "DvStack", """<DvStack gap="sm" padding="4 8" align="center" justify="between"><DvLabel text="x"/></DvStack>""" },
        { "DvRow", """<DvRow gap="lg" padding="4" align="end" justify="center"><DvLabel text="x"/></DvRow>""" },
        { "DvGrid", """<DvGrid columns="3" rows="40 auto" gap="md" padding="2" align="start" justify="end"><DvLabel text="x"/></DvGrid>""" },
        { "DvBox", """<DvBox><DvLabel text="x"/></DvBox>""" },
        { "DvSpacer", """<DvSpacer gap="sm" padding="2" align="center" justify="start"/>""" },
        { "DvRepeat", """<DvRepeat items="{Items}" gap="xs" padding="2" align="stretch" justify="between"><DvLabel text="{Item}"/></DvRepeat>""" },
        { "DvSlot", """<DvSlot/>""" },
        { "DvStyle", """<DvStyle>.x { color:#fff }</DvStyle>""" },
        {
            "DvTabs",
            """<DvTabs value="{Bind Sel}" readonly="false" placeholder="p" items="{Items}" item-value="Length" item-text="Length" item-icon="Length"><DvTab value="a" text="A" disabled="false"><DvLabel text="x"/></DvTab><DvTab value="b" text="B"/></DvTabs>"""
        },
        { "DvTab", """<DvTabs value="{Bind Sel}"><DvTab value="a" text="A" disabled="false"><DvLabel text="x"/></DvTab><DvTab value="b" text="B" disabled="true"/></DvTabs>""" },
        { "DvExpander", """<DvExpander expanded="true" header="H"><DvLabel text="x"/></DvExpander>""" },
        { "DvStepBar", """<DvStepBar steps="a,b,c" current="2"/>""" },
    };

    private static Dictionary<ElementNode, HashSet<string>> Reads(string markup)
    {
        var root = MarkupParser.Parse($"<DvScreen>{markup}</DvScreen>");
        var seen = new Dictionary<ElementNode, HashSet<string>>();
        AttrReader.Watch = (n, name) =>
        {
            if (!seen.TryGetValue(n, out var set))
                seen[n] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(name);
        };
        try
        {
            new LayoutEngine().Layout(root, new Vm(), 800, 600);
        }
        finally
        {
            AttrReader.Watch = null;
        }

        // 마크업 트리 안의 노드만(합성 노드 DvTabItem 등은 제외)
        var inTree = new HashSet<ElementNode>(Flatten(root));
        return seen.Where(kv => inTree.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static IEnumerable<ElementNode> Flatten(ElementNode n) => new[] { n }.Concat(n.Children.SelectMany(Flatten));

    private static IReadOnlyList<AttrDef> Declared(string tag)
    {
        Assert.True(TagSchema.Tags.TryGetValue(tag, out var defs), $"{tag} has no TagSchema entry");
        return defs!;
    }

    private static HashSet<string> ReadsOf(string tag, string markup)
        => Reads(markup).Where(kv => kv.Key.Tag == tag).SelectMany(kv => kv.Value).ToHashSet(StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Schema_Containers_ReadsAreDeclared(string tag, string markup)
    {
        var allowed = Declared(tag).Select(a => a.Name).Concat(TagSchema.CommonOf(tag).Select(a => a.Name)).ToHashSet();
        var extra = ReadsOf(tag, markup).Where(n => !allowed.Contains(n)).OrderBy(n => n).ToList();
        Assert.True(extra.Count == 0, $"{tag} reads undeclared attributes: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Schema_Containers_DeclaredAreRead(string tag, string markup)
    {
        var read = ReadsOf(tag, markup);
        var missing = Declared(tag).Where(a => !read.Contains(a.Name)).Select(a => a.Name).ToList();
        Assert.True(missing.Count == 0, $"{tag} declares attributes the build never read: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Schema_Containers_NamesAreUnique()
    {
        foreach (var (tag, defs) in TagSchema.Tags)
            Assert.True(defs.Select(a => a.Name).Distinct().Count() == defs.Count, $"{tag} declares a name twice");
    }
}
