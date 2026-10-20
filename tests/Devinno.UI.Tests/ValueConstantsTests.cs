using System.Reflection;
using Devinno.UI;
using Devinno.UI.Grid;
using Devinno.UI.Styling;
using Xunit;

// Styles.* / Values.* 키워드 상수 — 각 상수가 실제 파서·테마가 받는 값인지(오타·드리프트 방지)
public class ValueConstantsTests
{
    private static string[] Consts(Type t)
        => t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

    [Fact]
    public void Every_constant_is_lowercase_keyword()
    {
        foreach (var t in typeof(Styles).GetNestedTypes().Concat(typeof(Values).GetNestedTypes()))
        foreach (var v in Consts(t))
            Assert.Matches("^[a-z]+(-[a-z]+)?$", v);
    }

    [Fact]
    public void Content_align_has_nine_parsable_values()
    {
        var all = Consts(typeof(Styles.ContentAlign));
        Assert.Equal(9, all.Length);
        foreach (var v in all)
            Assert.NotNull(StyleParser.Parse("content-align:" + v).ContentVertical);
        Assert.Equal(ContentVAlign.Middle, StyleParser.Parse("content-align:" + Styles.ContentAlign.MiddleCenter).ContentVertical);
        Assert.Equal(TextAlign.Right, StyleParser.Parse("content-align:" + Styles.ContentAlign.BottomRight).TextAlign);
    }

    [Fact]
    public void Text_align_overflow_and_border_style_parse()
    {
        Assert.Equal(TextAlign.Left, StyleParser.Parse("text-align:" + Styles.TextAlign.Left).TextAlign);
        Assert.Equal(TextAlign.Center, StyleParser.Parse("text-align:" + Styles.TextAlign.Center).TextAlign);
        Assert.Equal(TextAlign.Right, StyleParser.Parse("text-align:" + Styles.TextAlign.Right).TextAlign);
        Assert.Equal(Overflow.Visible, StyleParser.Parse("overflow:" + Styles.Overflow.Visible).Overflow);
        Assert.Equal(Overflow.Hidden, StyleParser.Parse("overflow:" + Styles.Overflow.Hidden).Overflow);
        Assert.Equal(Overflow.Scroll, StyleParser.Parse("overflow:" + Styles.Overflow.Scroll).Overflow);
        Assert.True(StyleParser.Parse("border-style:" + Styles.BorderStyle.Dashed).BorderDashed);
        Assert.False(StyleParser.Parse("border-style:" + Styles.BorderStyle.Solid).BorderDashed);
    }

    [Fact]
    public void Tones_and_states_are_theme_tokens()
    {
        foreach (var theme in new[] { DvTheme.Light, DvTheme.Dark })
        {
            Assert.Equal(5, Consts(typeof(Styles.Tone)).Length);
            foreach (var v in Consts(typeof(Styles.Tone)))
                Assert.True(theme.TryGet("tone-" + v, out _), "tone-" + v);
            Assert.Equal(6, Consts(typeof(Styles.State)).Length);
            foreach (var v in Consts(typeof(Styles.State)))
                Assert.True(theme.TryGet("state-" + v, out _), "state-" + v);
        }
    }

    [Fact]
    public void Variants_are_builtin_button_classes()
    {
        var sheet = StyleSheet.Parse(BuiltInStyles.Sheet);
        Assert.Equal(4, Consts(typeof(Styles.Variant)).Length);
        foreach (var v in Consts(typeof(Styles.Variant)))
            Assert.True(sheet.TryGet("btn-" + v, out _), "btn-" + v);
        Assert.Equal(2, Consts(typeof(Styles.ButtonSize)).Length);
        foreach (var v in Consts(typeof(Styles.ButtonSize)))
            Assert.True(sheet.TryGet("btn-" + v, out _), "btn-" + v);
    }

    [Fact]
    public void Grid_cell_type_and_align_are_column_keywords()
    {
        Assert.Equal(new[] { "text", "check", "lamp", "button", "input", "select", "datetime", "color" }, Consts(typeof(Values.GridCellType)));
        Assert.Equal(new[] { "left", "center", "right" }, Consts(typeof(Values.GridAlign)));
    }

    [Fact]
    public void Grid_filter_constants_match_parser()
    {
        Assert.Equal(new[] { "none", "auto", "text", "values", "range", "date" }, Consts(typeof(Values.GridFilter)));
    }

    [Fact]
    public void Grid_values_match_parsers()
    {
        Assert.Equal(new[] { "none", "single", "multi" }, Consts(typeof(Values.GridSelectionMode)));
        Assert.Equal(new[] { "asc", "desc" }, Consts(typeof(Values.GridSort)));
        Assert.Equal(5, Consts(typeof(Values.GridFn)).Length);
        foreach (var v in Consts(typeof(Values.GridFn)))
            Assert.NotEqual(GridAggregate.None, GridSummary.Parse(v));
    }

    [Fact]
    public void Date_time_modes_resolve()
    {
        Assert.Equal(PickerMode.Date, PickerValue.ResolveMode(Values.DateTimeMode.Date, typeof(DateTime)));
        Assert.Equal(PickerMode.Time, PickerValue.ResolveMode(Values.DateTimeMode.Time, typeof(DateTime)));
        Assert.Equal(PickerMode.DateTime, PickerValue.ResolveMode(Values.DateTimeMode.DateTime, typeof(DateTime)));
    }
}
