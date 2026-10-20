using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Xunit;

// P8b-1 DvSelect 표시 — DvOption / items 경로 / enum 자동, 값 → 선택 항목(타입 변환), placeholder, 읽기 전용
public class SelectTests
{
    public enum Direction { Left, Right, Up }

    public sealed class Recipe
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string? Icon { get; init; }
    }

    public sealed class Vm
    {
        public string Mode { get; set; } = "manual";
        public int RecipeId { get; set; } = 20;
        public Direction Dir { get; set; } = Direction.Right;
        public int Count { get; set; } = 2;
        public int? Maybe { get; set; }
        public List<Recipe> Recipes { get; } = new()
        {
            new() { Id = 10, Name = "세척 A", Icon = "fa-droplet" },
            new() { Id = 20, Name = "세척 B" },
            new() { Id = 30, Name = "건조" }
        };
        public List<string> Plain { get; } = new() { "하나", "둘" };
    }

    private static LayoutBox Select(string inner, object? vm = null)
        => Flatten(new LayoutEngine(DvTheme.Light).Layout(MarkupParser.Parse(
            $"<DvScreen style=\"padding:0; align-items:start\">{inner}</DvScreen>"), vm ?? new Vm(), 400, 300)).First(b => b.Node.Tag == "DvSelect");

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Options_from_children()
    {
        var b = Select("""
            <DvSelect value="{Bind Data.Mode}">
              <DvOption value="auto" text="자동" icon="fa-robot"/>
              <DvOption value="manual" text="수동"/>
              <DvOption value="stop"/>
            </DvSelect>
            """);
        Assert.Equal(new[] { "자동", "수동", "stop" }, b.SelectOptions.Select(o => o.Text));
        Assert.Equal("fa-robot", b.SelectOptions[0].Icon);
        Assert.Equal(1, b.SelectedIndex);
        Assert.Empty(b.Children); // DvOption 은 레이아웃 자식이 아니다
    }

    [Fact]
    public void Options_from_items_with_paths()
    {
        var b = Select("<DvSelect value=\"{Bind Data.RecipeId}\" items=\"{Data.Recipes}\" item-value=\"Id\" item-text=\"Name\" item-icon=\"Icon\"/>");
        Assert.Equal(new[] { "세척 A", "세척 B", "건조" }, b.SelectOptions.Select(o => o.Text));
        Assert.Equal(10, b.SelectOptions[0].Value);
        Assert.Equal("fa-droplet", b.SelectOptions[0].Icon);
        Assert.Equal(1, b.SelectedIndex);
    }

    [Fact]
    public void Items_without_paths_use_the_item_itself()
    {
        var vm = new Vm { Mode = "둘" };
        var b = Select("<DvSelect value=\"{Bind Data.Mode}\" items=\"{Data.Plain}\"/>", vm);
        Assert.Equal(new[] { "하나", "둘" }, b.SelectOptions.Select(o => o.Text));
        Assert.Equal(1, b.SelectedIndex);
    }

    [Fact]
    public void Enum_property_generates_options()
    {
        var b = Select("<DvSelect value=\"{Bind Data.Dir}\"/>");
        Assert.Equal(new[] { "Left", "Right", "Up" }, b.SelectOptions.Select(o => o.Text));
        Assert.Equal(1, b.SelectedIndex);
    }

    [Fact]
    public void String_option_values_match_typed_properties()
    {
        var b = Select("""
            <DvSelect value="{Bind Data.Count}">
              <DvOption value="1" text="하나"/>
              <DvOption value="2" text="둘"/>
            </DvSelect>
            """);
        Assert.Equal(1, b.SelectedIndex);
        var e = Select("<DvSelect value=\"{Bind Data.Dir}\"><DvOption value=\"Up\" text=\"위\"/></DvSelect>", new Vm { Dir = Direction.Up });
        Assert.Equal(0, e.SelectedIndex);
    }

    [Fact]
    public void No_match_shows_placeholder()
    {
        var b = Select("""
            <DvSelect value="{Bind Data.Maybe}" placeholder="선택하세요">
              <DvOption value="1" text="하나"/>
            </DvSelect>
            """);
        Assert.Equal(-1, b.SelectedIndex);
        Assert.Equal("선택하세요", b.InputPlaceholder);
        Assert.Equal("", SelectDisplay.Text(b));
    }

    [Fact]
    public void Default_size_and_read_only_rules()
    {
        var b = Select("<DvSelect value=\"{Bind Data.Dir}\"/>");
        Assert.Equal((200f, 40f), (b.Rect.Width, b.Rect.Height));
        Assert.True(SelectDisplay.IsOpenable(b));
        Assert.Equal("Right", SelectDisplay.Text(b));

        var ro = Select("<DvSelect value=\"{Data.Dir}\"/>");
        Assert.True(ro.ReadOnly);
        Assert.True(ro.Style.BorderDashed);
        Assert.False(SelectDisplay.IsOpenable(ro));
        Assert.Equal(3, ro.SelectOptions.Count); // 단방향도 enum 항목은 만든다(표시용)

        Assert.False(SelectDisplay.IsOpenable(Select("<DvSelect value=\"{Bind Data.Dir}\" disabled=\"true\"/>")));
        Assert.False(SelectDisplay.IsOpenable(Select("<DvSelect value=\"{Bind Data.Dir}\" readonly=\"true\"/>")));
    }
}
