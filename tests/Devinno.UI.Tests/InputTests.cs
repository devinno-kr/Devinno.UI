using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

public class InputTests
{
    public sealed class Vm
    {
        public string Name { get; set; } = "레시피 A";
        public string Empty { get; set; } = "";
        public double SetPoint { get; set; } = 12.345;
        public int Count { get; set; } = 7;
        public string Password { get; set; } = "secret";
    }

    private static LayoutBox Lay(string markup, object? vm = null, DvTheme? theme = null)
        => new LayoutEngine(theme).Layout(MarkupParser.Parse(markup), vm, 400, 300);

    private static LayoutBox Input(LayoutBox root)
        => Flatten(root).First(b => b.Node.Tag == "DvInput");

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    // ── 파싱
    [Fact]
    public void Parses_attributes_and_binding()
    {
        var b = Input(Lay("""
            <DvScreen style="align-items:start"><DvInput value="{Bind Data.SetPoint}" keyboard-type="numeric" min="0" max="120"
                format="0.0" unit="℃" placeholder="설정값" onbeforecommit="Check"/></DvScreen>
            """, new Vm()));
        Assert.Equal("{Bind Data.SetPoint}", b.ValueBindExpr);
        Assert.Equal(DvEditKind.Numeric, b.InputKind);
        Assert.Equal(0d, b.InputMin);
        Assert.Equal(120d, b.InputMax);
        Assert.Equal("0.0", b.InputFormat);
        Assert.Equal("℃", b.Unit);
        Assert.Equal("설정값", b.InputPlaceholder);
        Assert.Equal("Check", b.OnBeforeCommit);
        Assert.Equal(12.345, b.InputValue);
    }

    [Fact]
    public void Keyboard_type_defaults_to_text_and_email_is_text()
    {
        Assert.Equal(DvEditKind.Text, Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"a\"/></DvScreen>")).InputKind);
        Assert.Equal(DvEditKind.Text, Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"a\" keyboard-type=\"email\"/></DvScreen>")).InputKind);
        Assert.Equal(DvEditKind.Password,
            Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"a\" keyboard-type=\"password\"/></DvScreen>")).InputKind);
    }

    [Fact]
    public void Default_size_is_200x40_and_multiline_96()
    {
        var one = Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"a\"/></DvScreen>"));
        Assert.Equal((200f, 40f), (one.Rect.Width, one.Rect.Height));
        var multi = Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"a\" multiline=\"true\"/></DvScreen>"));
        Assert.True(multi.InputMultiline);
        Assert.Equal(96f, multi.Rect.Height);
    }

    // ── 표시 문자열
    [Theory]
    [InlineData("<DvInput value=\"{Bind Data.Name}\"/>", "레시피 A")]
    [InlineData("<DvInput value=\"{Bind Data.SetPoint}\" keyboard-type=\"numeric\" format=\"0.0\"/>", "12.3")]
    [InlineData("<DvInput value=\"{Bind Data.SetPoint}\" keyboard-type=\"numeric\"/>", "12.345")]
    [InlineData("<DvInput value=\"{Bind Data.Count}\" keyboard-type=\"numeric\" format=\"000\"/>", "007")]
    [InlineData("<DvInput value=\"{Bind Data.Password}\" keyboard-type=\"password\"/>", "••••••")]
    [InlineData("<DvInput value=\"{Data.Name}\"/>", "레시피 A")]
    public void Display_text(string input, string expected)
    {
        var b = Input(Lay($"<DvScreen style=\"align-items:start\">{input}</DvScreen>", new Vm()));
        Assert.Equal(expected, InputDisplay.Text(b));
    }

    [Fact]
    public void Format_uses_invariant_culture()
    {
        var prev = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var b = Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"{Bind Data.SetPoint}\" keyboard-type=\"numeric\" format=\"0.0\"/></DvScreen>",
                new Vm()));
            Assert.Equal("12.3", InputDisplay.Text(b));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prev;
        }
    }

    [Fact]
    public void Empty_value_shows_placeholder()
    {
        var b = Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"{Bind Data.Empty}\" placeholder=\"이름\"/></DvScreen>", new Vm()));
        Assert.Equal("", InputDisplay.Text(b));
        Assert.True(InputDisplay.ShowsPlaceholder(b));
    }

    [Fact]
    public void One_way_binding_is_read_only_and_two_way_is_editable()
    {
        Assert.False(InputDisplay.IsEditable(Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"{Data.Name}\"/></DvScreen>", new Vm()))));
        Assert.True(InputDisplay.IsEditable(Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"{Bind Data.Name}\"/></DvScreen>", new Vm()))));
        Assert.False(InputDisplay.IsEditable(
            Input(Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"{Bind Data.Name}\" disabled=\"true\"/></DvScreen>", new Vm()))));
    }

    // ── 렌더
    private static SKBitmap Render(LayoutBox root, int w = 400, int h = 300)
    {
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        new Painter(DvTheme.Light).Paint(canvas, root, null);
        return bmp;
    }

    [Fact]
    public void Paints_box_border()
    {
        var root = Lay("<DvScreen style=\"align-items:start\"><DvInput value=\"\"/></DvScreen>", null, DvTheme.Light);
        var b = Input(root);
        using var bmp = Render(root);
        // 테두리(왼쪽 변 가운데)는 흰 배경과 다르고, 상자 안쪽은 표면색(흰색)
        var edge = bmp.GetPixel((int)b.Rect.Left, (int)b.Rect.MidY);
        Assert.NotEqual(SKColors.White, edge);
        Assert.Equal(SKColors.White, bmp.GetPixel((int)b.Rect.MidX, (int)b.Rect.MidY));
    }

    [Fact]
    public void Editing_paints_focus_ring_outside_the_box()
    {
        var root = Lay("<DvScreen style=\"padding:20\"><DvInput value=\"\"/></DvScreen>", null, DvTheme.Light);
        var b = Input(root);
        using (var plain = Render(root))
            Assert.Equal(SKColors.White, plain.GetPixel((int)b.Rect.MidX, (int)b.Rect.Top - 2));

        b.Editing = true;
        using var focused = Render(root);
        Assert.NotEqual(SKColors.White, focused.GetPixel((int)b.Rect.MidX, (int)b.Rect.Top - 2));
    }

    [Fact]
    public void Edit_error_paints_danger_border()
    {
        var root = Lay("<DvScreen style=\"padding:20\"><DvInput value=\"\"/></DvScreen>", null, DvTheme.Light);
        var b = Input(root);
        b.EditError = "범위";
        using var bmp = Render(root);
        var edge = bmp.GetPixel((int)b.Rect.Left, (int)b.Rect.MidY);
        Assert.True(edge.Red > edge.Blue + 40, $"테두리가 위험 색이어야 함: {edge}");
    }
}
