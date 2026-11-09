using Devinno.UI.Alarms;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Trends;
using Xunit;

// 2.4 계기·차트·알람 — 정의된 속성을 모두 채운 마크업을 빌드하고, 리더로 읽은 이름과 정의를 맞춘다.
// 한 태그의 속성이 갈래(예: DvLamp blink-when/blink)에 따라 나뉘어 읽히면 같은 태그 요소를 여럿 둔다(읽은 이름은 합친다).
public class TagSchemaInstrumentsTests
{
    public sealed class Bar
    {
        public double V { get; set; }
        public string N { get; set; } = "";
    }

    public sealed class Vm
    {
        public List<double> Values { get; } = [1, 2, 3];
        public List<Bar> Bars { get; } = [new() { V = 1, N = "a" }, new() { V = 2, N = "b" }];
        public List<AlarmRow> Alarms { get; } =
            [new AlarmRow { Id = 1, Time = new DateTime(2026, 11, 9), Priority = AlarmPriority.Major, Message = "m" }];
        public TrendBuffer Buf { get; } = new();
        public List<TrendPoint> Pts { get; } = [new(new DateTime(2026, 11, 9), 1)];
        public DateTime End { get; } = new(2026, 11, 9, 1, 0, 0);
        public bool On { get; set; } = true;
        public void Ack(AlarmRow row) { }
    }

    private const string Dial =
        """value="40" min="0" max="100" tone="warning" direction="left" show-value="true" unit="%" format="0" alarm-zones="0:50:#16a34a 50:100:#dc2626" ticks="5" minor-ticks="2" show-tick-labels="false" label="L" sweep="180" thickness="8" """;

    private const string ChartCommon = """tone="warning" show-value="true" y-min="0" y-max="10" area="true" inner-radius="0.5" """;

    public static TheoryData<string, string> Cases => new()
    {
        { "DvGauge", $"<DvGauge {Dial}/>" },
        { "DvRing", $"<DvRing {Dial}/>" },
        {
            "DvLamp",
            """<DvLamp on="{On}" blink-when="{On}" blink-interval="300" color="#ff0000" state="ok"/><DvLamp blink="true"/>"""
        },
        { "DvTank", """<DvTank value="40" min="0" max="100" unit="L" format="0" alarm-zones="0:50:#16a34a" show-zone-marks="false"/>""" },
        { "DvSparkline", $$"""<DvSparkline values="{Values}" {{ChartCommon}}/>""" },
        { "DvBarChart", $$"""<DvBarChart items="{Bars}" item-value="V" item-text="N" {{ChartCommon}}/>""" },
        { "DvPieChart", $$"""<DvPieChart items="{Bars}" item-value="V" item-text="N" {{ChartCommon}}/>""" },
        {
            "DvTrend",
            """<DvTrend source="{Buf}" window="30" end="{End}" y-min="0" y-max="10" area="true" smooth="false"><DvTrendChannel name="a"/></DvTrend>"""
        },
        {
            "DvTrendChannel",
            """<DvTrend source="{Buf}"><DvTrendChannel name="a" label="A" unit="u" color="#ff0000" points="{Pts}"/><DvTrendChannel/></DvTrend>"""
        },
        { "DvAlarmBanner", """<DvAlarmBanner items="{Alarms}" comm-alarm="lost" onack="Ack" show-ack="true" row-height="40"/>""" },
        { "DvAlarmList", """<DvAlarmList items="{Alarms}" comm-alarm="lost" onack="Ack" show-ack="true" row-height="40"/>""" },
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

        return seen;
    }

    private static IReadOnlyList<AttrDef> Declared(string tag)
    {
        Assert.True(TagSchema.Tags.TryGetValue(tag, out var defs), $"{tag} has no TagSchema entry");
        return defs!;
    }

    private static HashSet<string> ReadsOf(string tag, string markup)
        => Reads(markup).Where(kv => kv.Key.Tag == tag).SelectMany(kv => kv.Value).ToHashSet(StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Schema_Instruments_ReadsAreDeclared(string tag, string markup)
    {
        var allowed = Declared(tag).Select(a => a.Name).Concat(TagSchema.CommonOf(tag).Select(a => a.Name)).ToHashSet();
        var extra = ReadsOf(tag, markup).Where(n => !allowed.Contains(n)).OrderBy(n => n).ToList();
        Assert.True(extra.Count == 0, $"{tag} reads undeclared attributes: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Schema_Instruments_DeclaredAreRead(string tag, string markup)
    {
        var read = ReadsOf(tag, markup);
        var missing = Declared(tag).Where(a => !read.Contains(a.Name)).Select(a => a.Name).ToList();
        Assert.True(missing.Count == 0, $"{tag} declares attributes the build never read: {string.Join(", ", missing)}");
    }
}
