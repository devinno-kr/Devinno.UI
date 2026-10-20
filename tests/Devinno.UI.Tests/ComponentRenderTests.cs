using Devinno.UI;
using Devinno.UI.Components;
using Xunit;

public class ComponentRenderTests
{
    [Fact]
    public void Renderer_uses_registry_to_render_component_to_png()
    {
        var reg = new ComponentRegistry();
        reg.Register(
            "<DvComponent name=\"Tag\"><DvParam name=\"T\" default=\"-\"/>" +
            "<DvBox style=\"background:#fff; border-radius:8; padding:10\"><DvLabel text=\"{T}\"/></DvBox></DvComponent>");

        var png = DvRenderer.RenderToPng(
            "<DvScreen style=\"padding:12\"><Tag t=\"가동\"/></DvScreen>", null, 160, 80, reg);

        Assert.True(png.Length > 500);
    }
}
