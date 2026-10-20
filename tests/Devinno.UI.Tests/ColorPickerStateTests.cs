using Devinno.UI;
using SkiaSharp;
using Xunit;

// P8d-4 색 선택 상태
public class ColorPickerStateTests
{
    [Theory]
    [InlineData(255, 0, 0)]
    [InlineData(0, 255, 0)]
    [InlineData(0, 0, 255)]
    [InlineData(59, 130, 246)]
    [InlineData(255, 255, 255)]
    [InlineData(0, 0, 0)]
    [InlineData(128, 128, 128)]
    public void Hsv_round_trips_rgb(byte r, byte g, byte b)
    {
        var (h, s, v) = ColorPickerState.ToHsv(new SKColor(r, g, b));
        Assert.Equal(new SKColor(r, g, b), ColorPickerState.FromHsv(h, s, v));
    }

    [Fact]
    public void Hue_survives_desaturation_and_black()
    {
        var st = new ColorPickerState(new SKColor(0, 0, 255)); // H 240
        st.SetSV(0, 1);
        Assert.Equal(240, st.H, 3);
        Assert.Equal(new SKColor(255, 255, 255), st.Rgb);
        st.SetSV(1, 0);
        Assert.Equal(240, st.H, 3);
        st.SetSV(1, 1);
        Assert.Equal(new SKColor(0, 0, 255), st.Rgb);
    }

    [Fact]
    public void Hex_input_accepts_hash_and_case()
    {
        var st = new ColorPickerState(SKColors.White);
        Assert.True(st.TrySetHex("#3b82F6"));
        Assert.Equal(new SKColor(0x3b, 0x82, 0xf6), st.Rgb);
        Assert.True(st.TrySetHex("ff0000"));
        Assert.Equal(new SKColor(255, 0, 0), st.Rgb);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#fff")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#gg0000")]
    [InlineData("#ff000080")]
    [InlineData("# ff000")]
    [InlineData(" ff0000")]
    public void Hex_input_rejects_bad_text(string text)
    {
        var st = new ColorPickerState(new SKColor(1, 2, 3));
        Assert.False(st.TrySetHex(text));
        Assert.Equal(new SKColor(1, 2, 3), st.Rgb); // 그대로
    }

    [Fact]
    public void Gray_hex_keeps_previous_hue()
    {
        var st = new ColorPickerState(new SKColor(0, 255, 0)); // H 120
        st.TrySetHex("#808080");
        Assert.Equal(120, st.H, 3);
        st.TrySetHex("#000000");
        Assert.Equal(120, st.H, 3);
    }

    [Fact]
    public void Setters_clamp()
    {
        var st = new ColorPickerState(SKColors.Red);
        st.SetSV(-1, 2);
        Assert.Equal((0d, 1d), (st.S, st.V));
        st.SetHue(400);
        Assert.Equal(360, st.H);
    }

    [Fact]
    public void Initial_alpha_is_dropped_for_state()
    {
        var st = new ColorPickerState(new SKColor(255, 0, 0, 0x40));
        Assert.Equal(new SKColor(255, 0, 0), st.Rgb);
        Assert.Equal(new SKColor(255, 0, 0), st.Initial);
    }
}
