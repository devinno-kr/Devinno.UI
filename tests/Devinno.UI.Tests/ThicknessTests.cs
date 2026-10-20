using Devinno.UI.Styling;
using Xunit;

public class ThicknessTests
{
    [Fact]
    public void One_value_applies_all()
    {
        Assert.True(Thickness.TryParse("8", out var t));
        Assert.Equal(8, t.Left);
        Assert.Equal(8, t.Top);
        Assert.Equal(8, t.Right);
        Assert.Equal(8, t.Bottom);
    }

    [Fact]
    public void Two_values_vertical_horizontal()
    {
        Assert.True(Thickness.TryParse("12 24", out var t));
        Assert.Equal(12, t.Top);
        Assert.Equal(24, t.Right);
        Assert.Equal(12, t.Bottom);
        Assert.Equal(24, t.Left);
    }

    [Fact]
    public void Four_values_trbl()
    {
        Assert.True(Thickness.TryParse("1 2 3 4", out var t));
        Assert.Equal(1, t.Top);
        Assert.Equal(2, t.Right);
        Assert.Equal(3, t.Bottom);
        Assert.Equal(4, t.Left);
    }

    [Fact]
    public void Three_values_top_horizontal_bottom()
    {
        Assert.True(Thickness.TryParse("1 2 3", out var t));
        Assert.Equal(1, t.Top);
        Assert.Equal(2, t.Right);
        Assert.Equal(3, t.Bottom);
        Assert.Equal(2, t.Left);
    }
}
