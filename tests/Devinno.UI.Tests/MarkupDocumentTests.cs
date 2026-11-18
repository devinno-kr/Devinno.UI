using System.Xml;
using Devinno.UI.Markup;
using Xunit;

// 원문 위치를 아는 마크업 — 편집기가 값 하나를 바꾸면 원문에서 그 자리만 바뀐다(주석·들여쓰기·속성 순서 보존)
public class MarkupDocumentTests
{
    private const string Sample =
        "<!-- 첫 화면 -->\r\n" +
        "<DvStack gap=\"md\">\r\n" +
        "    <!-- 제목 -->\r\n" +
        "    <DvLabel text=\"A &amp; B\" style='color:#fff; left:10'/>\r\n" +
        "    <DvRow\r\n" +
        "        class=\"bar\"\r\n" +
        "        grow=\"1\">\r\n" +
        "        <DvButton text=\"OK\" onclick=\"Ok\" />\r\n" +
        "    </DvRow>\r\n" +
        "</DvStack>\r\n";

    private static ElementNode Label(MarkupDocument d) => d.Root.Children[0];
    private static ElementNode Row(MarkupDocument d) => d.Root.Children[1];

    // 바뀐 부분만 — 앞뒤 공통 부분을 떼고 남은 원문·결과 조각
    private static (string Old, string New) Diff(string a, string b)
    {
        var p = 0;
        while (p < a.Length && p < b.Length && a[p] == b[p]) p++;
        var s = 0;
        while (s < a.Length - p && s < b.Length - p && a[^(s + 1)] == b[^(s + 1)]) s++;
        return (a[p..^s], b[p..^s]);
    }

    [Fact]
    public void Root_IsTheSameTreeAsParse()
    {
        var d = MarkupDocument.Parse(Sample);
        Assert.Equal(Sample, d.Text);
        AssertSameTree(MarkupParser.Parse(Sample), d.Root);
    }

    [Fact]
    public void Root_IsTheSameTreeAsParse_ForEverySampleFile()
    {
        var root = FindRepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "samples"), "*.dvml", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            var d = MarkupDocument.Parse(text);
            AssertSameTree(MarkupParser.Parse(text), d.Root);
            // 모든 요소의 원문 조각이 그 태그로 시작한다
            foreach (var n in All(d.Root))
            {
                var s = d.SpanOf(n);
                Assert.Matches("^<([\\w.-]+:)?" + n.Tag + "[\\s/>]", text.Substring(s.Start, s.Length));
                Assert.EndsWith(">", text.Substring(s.Start, s.Length));
            }
        }
    }

    [Fact]
    public void Scan_SkipsQuotedAngles_Cdata_AndDeclarations()
    {
        const string text = "<?xml version=\"1.0\"?>\n<!DOCTYPE x [ <!ENTITY a \"<>\"> ]>\n<DvStack>\n  <DvLabel text=\"a > b\" />\n  <DvStyle><![CDATA[.x { } <DvFake/>]]></DvStyle>\n  <DvButton   text = 'go' />\n</DvStack>";
        var d = MarkupDocument.Parse(text);
        AssertSameTree(MarkupParser.Parse(text), d.Root);
        Assert.Equal(3, d.Root.Children.Count);
        var e = d.SetAttribute(d.Root.Children[2], "text", "stop");
        Assert.Equal(text.Replace("'go'", "'stop'"), e.Text);
    }

    [Fact]
    public void BadXml_ThrowsLikeParse()
    {
        Assert.Throws<XmlException>(() => MarkupDocument.Parse("<DvStack><DvLabel></DvStack>"));
        Assert.Throws<ArgumentException>(() => MarkupDocument.Parse("  "));
    }

    [Fact]
    public void Spans_PointIntoTheText()
    {
        var d = MarkupDocument.Parse(Sample);
        var label = d.SpanOf(Label(d));
        Assert.Equal("<DvLabel text=\"A &amp; B\" style='color:#fff; left:10'/>", Sample.Substring(label.Start, label.Length));
        var row = d.SpanOf(Row(d));
        Assert.StartsWith("<DvRow\r\n", Sample.Substring(row.Start, row.Length));
        Assert.EndsWith("</DvRow>", Sample.Substring(row.Start, row.Length));
        Assert.Equal(row.Start + row.Length, row.End);

        var text = d.AttributeSpan(Label(d), "text")!.Value;
        Assert.Equal("A &amp; B", Sample.Substring(text.Start, text.Length)); // 따옴표 안, 원문 그대로
        Assert.Null(d.AttributeSpan(Label(d), "nope"));
    }

    [Fact]
    public void NodeAt_FindsTheDeepestElement()
    {
        var d = MarkupDocument.Parse(Sample);
        Assert.Same(Row(d).Children[0], d.NodeAt(Sample.IndexOf("onclick", StringComparison.Ordinal)));
        Assert.Same(Row(d), d.NodeAt(Sample.IndexOf("class=", StringComparison.Ordinal)));
        Assert.Same(d.Root, d.NodeAt(Sample.IndexOf("<!-- 제목", StringComparison.Ordinal)));
        Assert.Null(d.NodeAt(0)); // 맨 앞 주석은 요소 밖
        Assert.Null(d.NodeAt(Sample.Length));
    }

    [Fact]
    public void SetAttribute_ChangesOnlyTheValue()
    {
        var d = MarkupDocument.Parse(Sample);
        var e = d.SetAttribute(Row(d), "grow", "2");
        Assert.Equal(("1", "2"), Diff(Sample, e.Text));
        Assert.Equal("2", Row(e).Attributes["grow"]);
    }

    [Fact]
    public void SetAttribute_KeepsTheQuoteAndEscapes()
    {
        var d = MarkupDocument.Parse(Sample);
        var e = d.SetAttribute(Label(d), "style", "content:'x' & <y>");
        Assert.Equal(Sample.Replace("style='color:#fff; left:10'", "style='content:&apos;x&apos; &amp; &lt;y>'"), e.Text);
        Assert.Equal("content:'x' & <y>", Label(e).Attributes["style"]);

        var f = d.SetAttribute(Label(d), "text", "say \"hi\"\nnext");
        Assert.Equal("say \"hi\"\nnext", Label(f).Attributes["text"]);
    }

    [Fact]
    public void SetAttribute_AddsAMissingAttributeAtTheEndOfTheStartTag()
    {
        var d = MarkupDocument.Parse(Sample);
        var e = d.SetAttribute(Row(d).Children[0], "disabled", "true");
        Assert.Contains("<DvButton text=\"OK\" onclick=\"Ok\" disabled=\"true\" />", e.Text);

        // 속성마다 줄을 나눈 태그 — 새 속성도 같은 들여쓰기로 새 줄에
        var f = d.SetAttribute(Row(d), "gap", "sm");
        Assert.Contains("<DvRow\r\n        class=\"bar\"\r\n        grow=\"1\"\r\n        gap=\"sm\">", f.Text);

        var g = MarkupDocument.Parse("<DvBox/>");
        Assert.Equal("<DvBox style=\"a:1\"/>", g.SetAttribute(g.Root, "style", "a:1").Text);
    }

    [Fact]
    public void RemoveAttribute_TakesItsLeadingSpaceOrLine()
    {
        var d = MarkupDocument.Parse(Sample);
        Assert.Contains("<DvButton onclick=\"Ok\" />", d.RemoveAttribute(Row(d).Children[0], "text").Text);
        Assert.Contains("<DvRow\r\n        grow=\"1\">", d.RemoveAttribute(Row(d), "class").Text);
        Assert.Same(d, d.RemoveAttribute(Row(d), "nope")); // 없으면 그대로
    }

    [Fact]
    public void RemoveElement_TakesItsLine()
    {
        var d = MarkupDocument.Parse(Sample);
        var e = d.RemoveElement(Label(d));
        Assert.Equal(Sample.Replace("    <DvLabel text=\"A &amp; B\" style='color:#fff; left:10'/>\r\n", ""), e.Text);
        Assert.Single(e.Root.Children);

        var inline = MarkupDocument.Parse("<DvRow><DvLabel/><DvButton/></DvRow>");
        Assert.Equal("<DvRow><DvButton/></DvRow>", inline.RemoveElement(inline.Root.Children[0]).Text);

        Assert.Throws<ArgumentException>(() => d.RemoveElement(d.Root));
    }

    [Fact]
    public void InsertChild_FollowsTheSiblingIndent()
    {
        var d = MarkupDocument.Parse(Sample);
        var first = d.InsertChild(Row(d), 0, "<DvLabel text=\"x\"/>");
        Assert.Contains("grow=\"1\">\r\n        <DvLabel text=\"x\"/>\r\n        <DvButton", first.Text);

        var last = d.InsertChild(d.Root, 2, "<DvSpacer/>");
        Assert.Contains("    </DvRow>\r\n    <DvSpacer/>\r\n</DvStack>", last.Text);
        Assert.Equal("DvSpacer", last.Root.Children[2].Tag);

        Assert.Throws<ArgumentOutOfRangeException>(() => d.InsertChild(d.Root, 3, "<DvSpacer/>"));
        Assert.Throws<XmlException>(() => d.InsertChild(d.Root, 0, "<DvSpacer>"));
    }

    [Fact]
    public void InsertChild_OpensASelfClosingOrEmptyParent()
    {
        var d = MarkupDocument.Parse("<DvStack>\n    <DvBox style=\"a:1\" />\n</DvStack>");
        var e = d.InsertChild(d.Root.Children[0], 0, "<DvLabel/>");
        Assert.Equal("<DvStack>\n    <DvBox style=\"a:1\">\n        <DvLabel/>\n    </DvBox>\n</DvStack>", e.Text);

        var empty = MarkupDocument.Parse("<DvStack></DvStack>");
        Assert.Equal("<DvStack>\n    <DvLabel/>\n</DvStack>", empty.InsertChild(empty.Root, 0, "<DvLabel/>").Text);
    }

    [Fact]
    public void InsertChild_IndentsEveryLineOfAMultiLineChild()
    {
        var d = MarkupDocument.Parse("<DvStack>\n  <DvLabel/>\n</DvStack>");
        var e = d.InsertChild(d.Root, 1, "<DvRow>\n  <DvButton/>\n</DvRow>");
        Assert.Equal("<DvStack>\n  <DvLabel/>\n  <DvRow>\n    <DvButton/>\n  </DvRow>\n</DvStack>", e.Text);
    }

    [Fact]
    public void Edits_RejectANodeFromAnotherDocument()
    {
        var d = MarkupDocument.Parse(Sample);
        var other = MarkupDocument.Parse(Sample);
        Assert.Throws<ArgumentException>(() => d.SetAttribute(other.Root, "gap", "sm"));
        Assert.Throws<ArgumentException>(() => d.SpanOf(other.Root));
        Assert.Throws<ArgumentException>(() => d.SetAttribute(d.Root, "bad name", "x"));
    }

    private static IEnumerable<ElementNode> All(ElementNode n) => new[] { n }.Concat(n.Children.SelectMany(All));

    private static void AssertSameTree(ElementNode a, ElementNode b)
    {
        Assert.Equal(a.Tag, b.Tag);
        Assert.Equal(a.Text, b.Text);
        Assert.Equal(a.Attributes.OrderBy(x => x.Key), b.Attributes.OrderBy(x => x.Key));
        Assert.Equal(a.Children.Count, b.Children.Count);
        for (var i = 0; i < a.Children.Count; i++)
            AssertSameTree(a.Children[i], b.Children[i]);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Devinno.UI.slnx")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
