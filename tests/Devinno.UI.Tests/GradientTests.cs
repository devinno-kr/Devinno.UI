using Devinno.UI.Styling;
using Xunit;

public class GradientTests
{
    [Fact]
    public void Parses_linear_gradient_angle_and_stops()
    {
        var s = StyleParser.Parse("background:linear-gradient(90deg, #ff0000, #0000ff)");

        Assert.NotNull(s.Gradient);
        Assert.Equal(90, s.Gradient!.AngleDeg);
        Assert.Equal(2, s.Gradient.Stops.Count);
        Assert.Equal((byte)255, s.Gradient.Stops[0].R);
        Assert.Equal((byte)255, s.Gradient.Stops[1].B);
        Assert.Null(s.Background);
    }

    [Theory]
    [InlineData("to Right", 90)]
    [InlineData("TO LEFT", 270)]
    [InlineData("to  top", 0)]
    public void Direction_keywords_ignore_case_and_extra_spaces(string dir, float angle)
    {
        var g = LinearGradient.TryParse($"linear-gradient({dir}, #ff0000, #0000ff)");

        Assert.NotNull(g);
        Assert.Equal(angle, g!.AngleDeg);
    }
}
