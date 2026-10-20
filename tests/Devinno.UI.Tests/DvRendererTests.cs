using Devinno.UI;
using Xunit;

public class DvRendererTests
{
    private sealed class Vm
    {
        public string Title { get; set; } = "Device Monitor";
    }

    [Fact]
    public void Renders_markup_to_nonempty_png()
    {
        var markup =
            "<DvScreen style=\"background:#f4f6f8; padding:24; display:flex; flex-direction:column; gap:16\">" +
            "<DvLabel text=\"{Data.Title}\" style=\"font-size:24; font-weight:bold; color:#1a1a1a\"/>" +
            "<DvBox style=\"background:#ffffff; border-radius:12; padding:20; box-shadow:0 4 12 #00000022\">" +
            "<DvLabel text=\"Running\" style=\"font-size:16; color:#3A7BD5\"/>" +
            "</DvBox>" +
            "</DvScreen>";

        var png = DvRenderer.RenderToPng(markup, new Vm(), 480, 320);

        Assert.NotNull(png);
        Assert.True(png.Length > 1000);
        Assert.Equal(0x89, png[0]);
        Assert.Equal((byte)'P', png[1]);
    }
}
