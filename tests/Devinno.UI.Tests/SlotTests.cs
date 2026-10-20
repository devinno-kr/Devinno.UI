using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

public class SlotTests
{
    private sealed class Vm
    {
        public string Site { get; set; } = "현장1";
    }

    [Fact]
    public void Slot_injects_instance_children_with_outer_scope()
    {
        var reg = new ComponentRegistry();
        reg.Register(
            "<DvComponent name=\"Panel\">" +
            "<DvParam name=\"Head\" default=\"\"/>" +
            "<DvBox><DvLabel text=\"{Head}\"/><DvSlot/></DvBox>" +
            "</DvComponent>");
        var root = MarkupParser.Parse(
            "<DvScreen><Panel head=\"제목\"><DvLabel text=\"{Data.Site}\"/></Panel></DvScreen>");
        var box = new LayoutEngine().Layout(root, new Vm(), 200, 200, reg);

        var inner = box.Children[0];
        Assert.Equal("제목", inner.Children[0].ResolvedText);
        Assert.Equal("현장1", inner.Children[1].ResolvedText);
    }
}
