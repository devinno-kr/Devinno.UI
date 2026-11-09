using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Process;
using Xunit;

// 2.6 공정도 — 정의된 속성을 모두 채운 공정도(유닛·포트·배관·신호선·계기·부속·붙은 칸 포함)를 빌드하고, 리더로 읽은 이름과 정의를 맞춘다.
// 공정도 자식은 ProcessBuilder 가 읽으므로 그 노드의 리더로 읽혀야 기록된다. 사용자 심볼·유닛 컴포넌트 인스턴스 속성(파라미터)은 대상이 아니다.
public class TagSchemaProcessTests
{
    public sealed class Vm
    {
        public bool Run { get; set; } = true;
        public void Hit() { }
    }

    // 값은 경고가 나지 않게 고른다 — ProcessBuilder.Warn 은 같은 문구를 프로세스 전체에서 한 번만 내므로, 여기서 먼저 내면 그 경고를 확인하는 다른 테스트가 못 본다.
    // (좌표 배치라 x·y 를 쓰고, row·rank 는 빈 값, align 은 auto)
    private const string Markup =
        """
        <DvProcess direction="lr" look="rich" align="auto" placement="fixed" base-size="1200 800" flow-animation="true" fit="contain">
          <DvTerminal name="SX_IN" text="FEED" label="L" rotate="0" flip="h" row="" rank="" x="40" y="100" visible="true"/>
          <DvVessel name="SX_T" kind="tank" label="T" rotate="0" flip="v" row="" rank="" x="160" y="100" visible="true" text="x" width="100" height="150" level="40">
            <DvAgitator running="{Run}" kind="" visible="true"/>
            <DvAttach side="left"><DvLabel text="a"/></DvAttach>
          </DvVessel>
          <DvPump name="SX_P" kind="centrifugal" label="P" rotate="0" flip="hv" row="" rank="" x="300" y="200" visible="true" text="x" running="{Run}">
            <DvMotor running="true" kind="isa" visible="true"/>
          </DvPump>
          <DvValve name="SX_V" kind="gate" label="V" rotate="0" flip="h" row="" rank="" x="420" y="200" visible="true" text="x" open="true"/>
          <DvValve3 name="SX_V3" kind="ball" label="V3" rotate="0" flip="h" row="" rank="" x="540" y="200" visible="true" text="x" open-b="true" open-c="false"/>
          <DvInline name="SX_F" kind="flowmeter" label="F" rotate="0" flip="h" row="" rank="" x="660" y="200" visible="true" text="x"/>
          <DvExchanger name="SX_E" kind="shell" label="E" rotate="0" flip="h" row="" rank="" x="780" y="200" visible="true" text="x"/>
          <DvUnit name="SX_U" direction="tb" label="Unit" row="" rank="" x="420" y="500" placement="auto" visible="true">
            <DvPump name="SX_UP"/>
            <DvPort name="out" side="right" at="0.5" visible="true"/>
            <DvLine from="SX_UP.out" to="out"/>
          </DvUnit>
          <DvLine from="SX_IN" to="SX_T.top" arrow="end" flow="true" medium="water" via="40,30 160,30" label="L" visible="true"/>
          <DvLine from="SX_T.bottom" to="SX_P.in"/>
          <DvLine from="SX_U.out" to="SX_V.in"/>
          <DvInstrument name="SX_TI" on="SX_P" tap="SX_P.in" tag="TI" number="101" value="40" panel="true" x="300" y="320" visible="true" onclick="Hit"/>
          <DvInstrument name="SX_PI" on="SX_V"/>
          <DvSignal from="SX_TI" to="SX_PI" kind="pneumatic" color="#ff0000" width="2" arrow="both" label="S" visible="true"/>
        </DvProcess>
        """;

    public static TheoryData<string> Tags => new()
    {
        "DvProcess", "DvUnit", "DvPort", "DvVessel", "DvPump", "DvValve", "DvValve3", "DvInline", "DvExchanger", "DvTerminal",
        "DvAgitator", "DvMotor", "DvAttach", "DvLine", "DvSignal", "DvInstrument",
    };

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
            new LayoutEngine().Layout(root, new Vm(), 1200, 800);
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
    public void Schema_Process_ReadsAreDeclared(string tag)
    {
        var allowed = Declared(tag).Select(a => a.Name).Concat(TagSchema.CommonOf(tag).Select(a => a.Name)).ToHashSet();
        var extra = ReadsOf(tag).Where(n => !allowed.Contains(n)).OrderBy(n => n).ToList();
        Assert.True(extra.Count == 0, $"{tag} reads undeclared attributes: {string.Join(", ", extra)}");
    }

    [Theory]
    [MemberData(nameof(Tags))]
    public void Schema_Process_DeclaredAreRead(string tag)
    {
        var read = ReadsOf(tag);
        var missing = Declared(tag).Where(a => !read.Contains(a.Name)).Select(a => a.Name).ToList();
        Assert.True(missing.Count == 0, $"{tag} declares attributes the build never read: {string.Join(", ", missing)}");
    }

    // 내장 장비의 상태 속성(running·open·level…)은 BuiltInSymbols 마크업의 DvParam 이 원본 — 정의와 맞는지 대조한다
    [Fact]
    public void Schema_Process_StateParamsMatchBuiltInSymbols()
    {
        foreach (var (tag, kinds) in BuiltInSymbols.Kinds)
        {
            var symbolParams = kinds.SelectMany(k => BuiltInSymbols.Get(tag, k)!.Params.Select(p => p.Name)).ToHashSet();
            var declaredState = Declared(tag).Where(a => a.Note?.StartsWith("state param") == true).Select(a => a.Name).ToHashSet();
            Assert.True(symbolParams.SetEquals(declaredState),
                $"{tag}: symbol params [{string.Join(", ", symbolParams.Order())}] vs declared state [{string.Join(", ", declaredState.Order())}]");
        }
    }
}
