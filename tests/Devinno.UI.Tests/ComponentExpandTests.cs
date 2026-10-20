using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class ComponentExpandTests
{
    private sealed class Vm
    {
        public string Plant { get; set; } = "B라인";
    }

    [Fact]
    public void Instance_expands_with_param_scope()
    {
        var reg = new ComponentRegistry();
        reg.Register(
            "<DvComponent name=\"Card\">" +
            "<DvParam name=\"Title\" default=\"-\"/>" +
            "<DvBox><DvLabel text=\"{Title}\"/></DvBox>" +
            "</DvComponent>");

        var root = MarkupParser.Parse(
            "<DvScreen><Card title=\"{Data.Plant}\"/><Card title=\"펌프\"/></DvScreen>");
        var box = new LayoutEngine().Layout(root, new Vm(), 200, 200, reg);

        var label1 = box.Children[0].Children[0];
        var label2 = box.Children[1].Children[0];
        Assert.Equal("B라인", label1.ResolvedText);
        Assert.Equal("펌프", label2.ResolvedText);
    }

    [Fact]
    public void Template_style_resolves_param_scope_values()
    {
        var reg = new ComponentRegistry();
        reg.Register(
            "<DvComponent name=\"Metric\">" +
            "<DvParam name=\"Tone\" default=\"#222222\"/>" +
            "<DvLabel text=\"값\" style=\"color:{Tone}\"/>" +
            "</DvComponent>");

        var root = MarkupParser.Parse("<DvScreen><Metric tone=\"#15803d\"/></DvScreen>");
        var box = new LayoutEngine().Layout(root, null, 200, 80, reg);

        var label = box.Children[0];
        Assert.Equal(0x15, label.Style.Color.R);
        Assert.Equal(0x80, label.Style.Color.G);
        Assert.Equal(0x3d, label.Style.Color.B);
    }
}
