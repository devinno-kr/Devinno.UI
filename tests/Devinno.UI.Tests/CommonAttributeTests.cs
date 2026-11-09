using Devinno.UI.Markup;
using Xunit;

// 3단계 R10 — 공통 속성은 모든 상자 태그에서 같은 뜻. 상자가 아닌 하위 요소는 공통 속성을 받지 않는다(자기 정의에 적은 것과 id 만).
public class CommonAttributeTests
{
    public sealed class Vm
    {
        public bool Show { get; set; }
        public bool On { get; set; } = true;
    }

    private const string Plant =
        """
        <DvPump name="P1"/>
        <DvValve name="V1"/>
        <DvInstrument name="I1" on="P1"/>
        """;

    [Fact]
    public void Line_VisibleFalse_IsNotDrawn()
    {
        var (_, d) = ProcessTests.Start(Plant + """<DvLine from="P1.out" to="V1.in" visible="false"/><DvLine from="V1.out" to="P1.in"/>""");
        Assert.Single(d.Lines);
        Assert.Equal("V1.out", d.Lines[0].FromRef);
    }

    [Fact]
    public void Signal_VisibleFalse_IsNotDrawn()
    {
        var (_, d) = ProcessTests.Start(Plant + """<DvSignal from="I1" to="V1" visible="false"/>""");
        Assert.Empty(d.Signals);
        var (_, shown) = ProcessTests.Start(Plant + """<DvSignal from="I1" to="V1" visible="true"/>""");
        Assert.Single(shown.Signals);
    }

    [Fact]
    public void LineAndSignal_Visible_FollowsTheCommonRule()
    {
        // 바인딩(D1: null·false = 숨김) · 0 = 숨김 · 그 밖의 글자 = 기본값(보임)
        var (_, d) = ProcessTests.Start(Plant + """<DvLine from="P1.out" to="V1.in" visible="{Show}"/><DvSignal from="I1" to="V1" visible="0"/>""", vm: new Vm());
        Assert.Empty(d.Lines);
        Assert.Empty(d.Signals);
        var (_, d2) = ProcessTests.Start(Plant + """<DvLine from="P1.out" to="V1.in" visible="{On}"/><DvSignal from="I1" to="V1" visible="abc"/>""", vm: new Vm());
        Assert.Single(d2.Lines);
        Assert.Single(d2.Signals);
    }

    [Fact]
    public void Equipment_Visible_FollowsTheCommonRule()
    {
        var (_, d) = ProcessTests.Start("""<DvPump name="P1" visible="0"/><DvPump name="P2" visible="abc"/><DvPump name="P3" visible="-1"/>""");
        Assert.Equal(["P2", "P3"], d.Nodes.Select(n => n.Name));
    }

    public static TheoryData<string> ChildTags => new()
    {
        "DvOption", "DvColumn", "DvColumnGroup", "DvSummaryRow", "DvSummary", "DvTrendChannel",
        "DvPort", "DvAgitator", "DvMotor", "DvLine", "DvSignal", "DvStyle",
    };

    [Theory]
    [MemberData(nameof(ChildTags))]
    public void ChildTags_TakeNoCommonAttributes_ExceptId(string tag)
    {
        Assert.Contains(tag, TagSchema.ChildTags);
        Assert.NotNull(TagSchema.Find(tag, "id"));
        foreach (var name in new[] { "class", "style", "hover-style", "grow", "height", "hold", "content-align" })
            Assert.Null(TagSchema.Find(tag, name));
        Assert.Equal(["id"], TagSchema.CommonOf(tag).Select(a => a.Name));
    }

    [Fact]
    public void ChildTags_KeepWhatTheyDeclare()
    {
        Assert.NotNull(TagSchema.Find("DvColumn", "width"));
        Assert.NotNull(TagSchema.Find("DvColumn", "onclick"));
        Assert.NotNull(TagSchema.Find("DvColumn", "onbeforecommit"));
        Assert.NotNull(TagSchema.Find("DvPort", "visible"));
        Assert.NotNull(TagSchema.Find("DvLine", "visible"));
        Assert.Null(TagSchema.Find("DvOption", "visible"));
        Assert.Null(TagSchema.Find("DvColumn", "disabled"));
    }

    // 상자인 하위 요소(DvTab 내용, DvAttach 칸)는 공통 속성을 그대로 받는다
    [Theory]
    [InlineData("DvTab")]
    [InlineData("DvAttach")]
    [InlineData("DvUnit")]
    [InlineData("DvPump")]
    public void BoxTags_TakeCommonAttributes(string tag)
    {
        Assert.DoesNotContain(tag, TagSchema.ChildTags);
        Assert.NotNull(TagSchema.Find(tag, "style"));
        Assert.Equal(TagSchema.Common.Count, TagSchema.CommonOf(tag).Count);
    }
}
