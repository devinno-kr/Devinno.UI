using Devinno.UI.Styling;
using Xunit;

public class DvColorTests
{
    [Theory]
    [InlineData("#fff", 255, 255, 255, 255)]
    [InlineData("#3A7BD5", 0x3A, 0x7B, 0xD5, 255)]
    [InlineData("#00000080", 0, 0, 0, 0x80)]
    [InlineData("transparent", 0, 0, 0, 0)]
    public void Parses_hex_and_keyword(string s, int r, int g, int b, int a)
    {
        Assert.True(DvColor.TryParse(s, out var c));
        Assert.Equal((byte)r, c.R);
        Assert.Equal((byte)g, c.G);
        Assert.Equal((byte)b, c.B);
        Assert.Equal((byte)a, c.A);
    }

    [Fact]
    public void Rejects_garbage() => Assert.False(DvColor.TryParse("nope", out _));
}
