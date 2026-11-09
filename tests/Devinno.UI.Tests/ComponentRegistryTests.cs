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

    // 3단계 D4 — 내장 태그 이름(DvGauge·DvScreen…)으로는 등록할 수 없다. 내장 컴포넌트(DvCard·DvPageFrame…)는 바꿀 수 있다(문서 그대로)
    [Theory]
    [InlineData("DvGauge")]
    [InlineData("DvScreen")]
    [InlineData("DvStack")]
    [InlineData("DvPump")]
    public void Register_BuiltInTagName_Throws(string name)
    {
        var reg = new ComponentRegistry();
        var ex = Assert.Throws<ArgumentException>(() => reg.Register($"<DvComponent name=\"{name}\"><DvLabel text=\"x\"/></DvComponent>"));
        Assert.Contains(name, ex.Message);
        Assert.False(reg.IsComponent(name));
    }

    [Theory]
    [InlineData("DvCard")]
    [InlineData("DvPageFrame")]
    [InlineData("DvDialogFrame")]
    [InlineData("DvSwitch")]
    public void Register_BuiltInComponentName_Replaces(string name)
    {
        var reg = new ComponentRegistry();
        reg.Register($"<DvComponent name=\"{name}\"><DvLabel text=\"x\"/></DvComponent>");
        Assert.True(reg.IsComponent(name));
    }

    // TagSchema 의 내장 컴포넌트 표시(ComponentTags)는 BuiltInComponents 에 실제로 있는 것
    [Fact]
    public void ComponentTags_AreBuiltInComponents()
        => Assert.All(Devinno.UI.Markup.TagSchema.ComponentTags, t => Assert.True(BuiltInComponents.Registry.IsComponent(t), t));
}
