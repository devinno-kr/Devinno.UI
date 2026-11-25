using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

// 컴포넌트 파라미터를 거친 표시 형식 — 인스턴스가 value="{Level:0.0}" 로 주면 파라미터는 값(72.0) 그대로 받고,
// 템플릿에서 그 파라미터 자체({value})가 글자가 될 때 인스턴스의 형식을 쓴다. 값 속성({value} → DvGauge)은 숫자 그대로.
public class BindingFormatParamTests
{
    public sealed class Vm
    {
        public double Level { get; set; } = 72.0;
        public double Delta { get; set; } = 1.5;
        public List<double> Items { get; } = [1, 2];
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static LayoutBox Build(string body, ComponentRegistry? reg = null) =>
        new LayoutEngine().Layout(MarkupParser.Parse($"<DvScreen>{body}</DvScreen>"), new Vm(), 800, 600, reg);

    private static List<string> Texts(LayoutBox root) =>
        Flatten(root).Where(b => b.Node.Tag == "DvLabel").Select(b => b.ResolvedText ?? "").ToList();

    [Fact]
    public void DvStat_value_keeps_the_instance_format()
    {
        var texts = Texts(Build("<DvStat label=\"수위\" value=\"{Level:0.0}\" unit=\"%\"/>"));
        Assert.Contains("72.0", texts);
        Assert.DoesNotContain("72", texts);
    }

    [Fact]
    public void Built_in_text_params_keep_the_format()
    {
        Assert.Contains("+1.50", Texts(Build("<DvStat label=\"a\" value=\"1\" delta=\"{Delta:+0.00}\"/>")));
        Assert.Contains("72.0", Texts(Build("<DvCard title=\"t\" badge=\"{Level:0.0}\"><DvLabel text=\"x\"/></DvCard>")));
    }

    private static ComponentRegistry Tile()
    {
        var reg = new ComponentRegistry();
        reg.Register("""
            <DvComponent name="Tile">
              <DvParam name="v"/>
              <DvStack>
                <DvLabel text="{v}"/>
                <DvLabel text="now {v} %"/>
                <DvLabel text="{v:0.00}"/>
                <DvGauge value="{v}" min="0" max="100"/>
              </DvStack>
            </DvComponent>
            """);
        reg.Register("""
            <DvComponent name="Outer">
              <DvParam name="x"/>
              <DvStat label="o" value="{x}"/>
            </DvComponent>
            """);
        return reg;
    }

    [Fact]
    public void User_component_param_keeps_the_format_and_the_value()
    {
        var root = Build("<Tile v=\"{Level:0.0}\"/>", Tile());
        var texts = Texts(root);
        Assert.Equal("72.0", texts[0]);
        Assert.Equal("now 72.0 %", texts[1]);
        Assert.Equal("72.00", texts[2]); // 템플릿 자신의 형식이 이긴다
        Assert.Equal(72f, Flatten(root).First(b => b.Node.Tag == "DvGauge").GaugeValue);
    }

    [Fact]
    public void Without_a_format_the_param_text_is_the_default()
        => Assert.Equal("72", Texts(Build("<Tile v=\"{Level}\"/>", Tile()))[0]);

    [Fact]
    public void Format_passes_through_nested_components()
        => Assert.Contains("72.0", Texts(Build("<Outer x=\"{Level:0.0}\"/>", Tile())));

    [Fact]
    public void Format_reaches_a_repeat_inside_the_template()
    {
        var reg = new ComponentRegistry();
        reg.Register("""
            <DvComponent name="Rows">
              <DvParam name="v"/>
              <DvParam name="list"/>
              <DvRepeat items="{list}"><DvLabel text="{v}|{Item}"/></DvRepeat>
            </DvComponent>
            """);
        Assert.Equal(["72.0|1", "72.0|2"], Texts(Build("<Rows v=\"{Level:0.0}\" list=\"{Items}\"/>", reg)));
    }

    public sealed class TextProbe : DvWidget
    {
        public static string? Seen;
        public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(10, 10);
        public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx) => Seen = ctx.Text("label");
    }

    [Fact]
    public void Custom_widget_text_inside_a_template_keeps_the_format()
    {
        var reg = new ComponentRegistry();
        reg.RegisterWidget<TextProbe>("TextProbe");
        reg.Register("""
            <DvComponent name="Holder">
              <DvParam name="v"/>
              <TextProbe label="{v}"/>
            </DvComponent>
            """);
        var s = new DvSurface("<DvScreen><Holder v=\"{Level:0.0}\"/></DvScreen>", new Vm(), null, reg);
        s.Layout(100, 50);
        using var bmp = new SKBitmap(100, 50);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 100, 50);
        Assert.Equal("72.0", TextProbe.Seen);
    }
}
