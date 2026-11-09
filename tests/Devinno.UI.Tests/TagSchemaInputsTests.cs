using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// 2.3 기본·입력 — 정의된 속성을 모두 채운 마크업을 빌드하고, 리더로 읽은 이름과 정의를 맞춘다.
// 한 태그의 속성이 갈래(예: DvVideo source/src, DvListBox 한 개/여럿)에 따라 나뉘어 읽히면 같은 태그 요소를 여럿 둔다(읽은 이름은 합친다).
public class TagSchemaInputsTests
{
    public sealed class TreeNode
    {
        public string Name { get; set; } = "";
        public List<TreeNode> Kids { get; } = [];
    }

    public sealed class Vm
    {
        public List<string> Items { get; } = ["a", "b"];
        public string Sel { get; set; } = "a";
        public bool Flag { get; set; } = true;
        public double Num { get; set; } = 3;
        public List<string> Multi { get; set; } = ["a"];
        public DateTime? When { get; set; } = new DateTime(2026, 11, 9);
        public string Color { get; set; } = "#112233";
        public object? Nothing => null;
        public double Pos { get; set; }
        public double Dur { get; set; }
        public List<TreeNode> Roots { get; } = [new TreeNode { Name = "r", Kids = { new TreeNode { Name = "k" } } }];
    }

    public static TheoryData<string, string> Cases => new()
    {
        { "DvLabel", """<DvLabel text="x"/>""" },
        { "DvButton", """<DvButton text="B" icon="fa-check" variant="primary" size="sm" value="{Bind Flag}"/>""" },
        { "DvIcon", """<DvIcon icon="fa-check"/>""" },
        { "DvImage", """<DvImage src="nope.png" fit="cover"/>""" },
        {
            "DvVideo",
            """<DvVideo source="{Nothing}" src="nope.mp4" fit="cover"/><DvVideo src="nope.mp4" fit="fill" playing="false" loop="true" position="{Bind Pos}" duration="{Bind Dur}"/>"""
        },
        { "DvToggle", """<DvToggle value="{Bind Flag}"/>""" },
        { "DvCheckBox", """<DvCheckBox value="{Bind Flag}" text="c"/>""" },
        { "DvRadio", """<DvRadio option="a" value="{Bind Sel}" text="r"/><DvRadio option="b" group="g" text="r2"/>""" },
        { "DvKnob", """<DvKnob value="{Bind Num}" min="0" max="10" step="1" unit="u" input-mode="tap"/>""" },
        { "DvSlider", """<DvSlider value="{Bind Num}" min="0" max="10" step="1" unit="u" direction="up" show-value="true"/>""" },
        {
            "DvInput",
            """<DvInput value="{Bind Num}" keyboard-type="numeric" multiline="false" placeholder="p" format="0.0" min="0" max="9" unit="u" readonly="false" step="1" show-stepper="true"/>"""
        },
        { "DvSelect", """<DvSelect value="{Bind Sel}" placeholder="p" readonly="false" items="{Items}" item-value="Length" item-text="Length" item-icon="Length"/>""" },
        { "DvOption", """<DvSelect value="{Bind Sel}"><DvOption value="a" text="A" icon="fa-check"/><DvOption value="b"/></DvSelect>""" },
        {
            "DvSegment",
            """<DvSegment value="{Bind Sel}" readonly="false" fill="true" placeholder="p" items="{Items}" item-value="Length" item-text="Length" item-icon="Length"/>"""
        },
        {
            "DvListBox",
            """<DvListBox value="{Bind Sel}" readonly="false" items="{Items}" item-value="Length" item-text="Length" item-icon="Length"/><DvListBox selection-mode="multi" selected-items="{Bind Multi}" items="{Items}"/>"""
        },
        { "DvTreeView", """<DvTreeView items="{Roots}" item-children="Kids" item-text="Name" item-value="Name" value="{Bind Sel}" readonly="false"/>""" },
        { "DvDateTimeInput", """<DvDateTimeInput value="{Bind When}" mode="date" show-seconds="true" format="yyyy-MM-dd" placeholder="p" readonly="false"/>""" },
        { "DvColorInput", """<DvColorInput value="{Bind Color}" placeholder="p" readonly="false"/>""" },
        {
            "DvProgress",
            """<DvProgress value="40" min="0" max="100" tone="warning" direction="left" show-value="true" unit="%" format="0" alarm-zones="0:50:#16a34a 50:100:#dc2626" ticks="5" minor-ticks="2" show-tick-labels="false" label="L" sweep="180" thickness="8"/>"""
        },
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

        // 마크업 트리 안의 노드만(합성 노드 DvSegmentItem 등은 제외)
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
    public void Schema_Inputs_ReadsAreDeclared(string tag, string markup)
    {
        var allowed = Declared(tag).Select(a => a.Name).Concat(TagSchema.CommonOf(tag).Select(a => a.Name)).ToHashSet();
        var extra = ReadsOf(tag, markup).Where(n => !allowed.Contains(n)).OrderBy(n => n).ToList();
        Assert.True(extra.Count == 0, $"{tag} reads undeclared attributes: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Schema_Inputs_DeclaredAreRead(string tag, string markup)
    {
        var read = ReadsOf(tag, markup);
        var missing = Declared(tag).Where(a => !read.Contains(a.Name)).Select(a => a.Name).ToList();
        Assert.True(missing.Count == 0, $"{tag} declares attributes the build never read: {string.Join(", ", missing)}");
    }
}
