using Devinno.UI.Components;
using Xunit;

public class ComponentRegistryTests
{
    [Fact]
    public void Registers_component_with_params_and_template()
    {
        var reg = new ComponentRegistry();
        reg.Register(
            "<DvComponent name=\"MotorCard\">" +
            "<DvParam name=\"Title\" type=\"string\" default=\"\"/>" +
            "<DvParam name=\"Motor\"/>" +
            "<DvBox><DvLabel text=\"{Title}\"/></DvBox>" +
            "</DvComponent>");

        Assert.True(reg.TryGet("MotorCard", out var def));
        Assert.Equal("MotorCard", def!.Name);
        Assert.Equal(2, def.Params.Count);
        Assert.Equal("Title", def.Params[0].Name);
        Assert.Equal("", def.Params[0].Default);
        Assert.Null(def.Params[1].Default);
        Assert.Equal("DvBox", def.Template.Tag);
    }
}
