using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 읽기 전용 DvInput — readonly="{Flag}" + 단방향 추론, 투명 바탕 + 점선 테두리(border-style: dashed)
public class ReadOnlyInputTests
{
    [Theory]
    [InlineData("border: 1 dashed #ff0000", true)]
    [InlineData("border: 1 solid #ff0000", false)]
    [InlineData("border: 1 #ff0000; border-style: dashed", true)]
    [InlineData("border: 1 dashed #ff0000; border-style: solid", false)]
    public void Parses_border_style(string css, bool dashed)
        => Assert.Equal(dashed, StyleParser.Parse(css).BorderDashed);

    [Fact]
    public void Dashed_border_has_gaps()
    {
        var root = new LayoutEngine().Layout(MarkupParser.Parse(
            "<DvScreen style=\"padding:20\"><DvBox style=\"width:200; height:40; border:2 dashed #ff0000\"/></DvScreen>"), null, 300, 100);
        using var bmp = new SKBitmap(300, 100);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.White);
            new Painter().Paint(c, root, null);
        }

        var row = Enumerable.Range(40, 160).Select(x => bmp.GetPixel(x, 20)).ToList();
        Assert.Contains(row, p => p.Red > 200 && p.Green < 80);           // 선
        Assert.Contains(row, p => p == SKColors.White);                    // 틈
    }

    public sealed class Vm
    {
        public string Name { get; set; } = "WASH-02";
        public bool Locked { get; set; } = true;
    }

    private static LayoutBox Input(string attrs, object vm)
        => Flatten(new LayoutEngine(DvTheme.Light).Layout(MarkupParser.Parse(
            $"<DvScreen style=\"padding:0\"><DvInput {attrs}/></DvScreen>"), vm, 400, 300)).First(b => b.Node.Tag == "DvInput");

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Theory]
    [InlineData("value=\"{Bind Data.Name}\" readonly=\"true\"", true)]
    [InlineData("value=\"{Bind Data.Name}\" readonly=\"{Data.Locked}\"", true)]
    [InlineData("value=\"{Data.Name}\"", true)]       // 단방향 = 읽기 전용
    [InlineData("value=\"WASH-02\"", true)]           // 리터럴 = 읽기 전용
    [InlineData("value=\"{Bind Data.Name}\"", false)]
    [InlineData("value=\"{Bind Data.Name}\" readonly=\"false\"", false)]
    public void Read_only_flag_and_look(string attrs, bool readOnly)
    {
        var b = Input(attrs, new Vm());
        Assert.Equal(readOnly, b.ReadOnly);
        Assert.Equal(!readOnly, InputDisplay.IsEditable(b));
        Assert.Equal(readOnly, b.Style.BorderDashed);
        if (readOnly)
            Assert.True(b.Style.Background is null || b.Style.Background.Value.A == 0);
    }

    [Fact]
    public void Disabled_wins_over_read_only_look()
    {
        var b = Input("value=\"{Bind Data.Name}\" readonly=\"true\" disabled=\"true\"", new Vm());
        Assert.False(b.StyleDisabled!.BorderDashed);
    }

    [Fact]
    public void Read_only_input_does_not_start_editing()
    {
        var s = new DvSurface("<DvScreen style=\"padding:0\"><DvInput value=\"{Bind Data.Name}\" readonly=\"{Data.Locked}\"/></DvScreen>",
            new Vm()) { UseNativeEditor = true };
        s.Layout(400, 300);
        s.PointerDown(new SKPoint(50, 20));
        s.PointerUp(new SKPoint(50, 20));
        s.Layout(400, 300);
        Assert.False(s.Edit.IsEditing);
    }
}
