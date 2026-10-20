using Devinno.UI.Markup;
using Xunit;

public class MarkupParserTests
{
    [Fact]
    public void Parses_tag_attributes_children_and_text()
    {
        var node = MarkupParser.Parse(
            "<DvBox style=\"padding:8\"><DvLabel text=\"hi\"/></DvBox>");

        Assert.Equal("DvBox", node.Tag);
        Assert.Equal("padding:8", node.Attributes["style"]);
        Assert.Single(node.Children);
        Assert.Equal("DvLabel", node.Children[0].Tag);
        Assert.Equal("hi", node.Children[0].Attributes["text"]);
    }
}
