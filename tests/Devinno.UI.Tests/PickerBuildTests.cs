using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// P8d-5 선택기 상자 — 모드·표시·읽기 전용·크기·그리기
public class PickerBuildTests
{
    public sealed class Vm
    {
        public DateTime At { get; set; } = new(2026, 10, 14, 9, 5, 7);
        public DateOnly Day { get; set; } = new(2026, 10, 14);
        public TimeOnly Clock { get; set; } = new(9, 5, 7);
        public DateTime? None { get; set; }
        public string Pen { get; set; } = "#3b82f6";
        public SKColor Fill { get; set; } = new(0x22, 0xc5, 0x5e, 0x80);
    }

    private static LayoutBox Build(string inner, Vm? vm = null)
    {
        var s = new DvSurface($"<DvScreen style=\"align-items:start\">{inner}</DvScreen>", vm ?? new Vm());
        s.Layout(600, 400);
        return s.Root!.Children[0];
    }

    [Fact]
    public void Mode_follows_bound_type()
    {
        Assert.Equal(PickerMode.DateTime, Build("""<DvDateTimeInput value="{Bind Data.At}"/>""").PickerMode);
        Assert.Equal(PickerMode.Date, Build("""<DvDateTimeInput value="{Bind Data.Day}"/>""").PickerMode);
        Assert.Equal(PickerMode.Time, Build("""<DvDateTimeInput value="{Bind Data.Clock}"/>""").PickerMode);
        Assert.Equal(PickerMode.Date, Build("""<DvDateTimeInput value="{Bind Data.At}" mode="date"/>""").PickerMode);
    }

    [Fact]
    public void Display_text_uses_format_and_seconds()
    {
        Assert.Equal("2026-10-14 09:05", PickerDisplay.Text(Build("""<DvDateTimeInput value="{Bind Data.At}"/>""")));
        Assert.Equal("09:05:07", PickerDisplay.Text(Build("""<DvDateTimeInput value="{Bind Data.Clock}" show-seconds="true"/>""")));
        Assert.Equal("2026.10.14", PickerDisplay.Text(Build("""<DvDateTimeInput value="{Bind Data.At}" mode="date" format="yyyy.MM.dd"/>""")));
        Assert.Equal("#3B82F6", PickerDisplay.Text(Build("""<DvColorInput value="{Bind Data.Pen}"/>""")));
        Assert.Equal("#22C55E", PickerDisplay.Text(Build("""<DvColorInput value="{Bind Data.Fill}"/>""")));
        Assert.Equal("", PickerDisplay.Text(Build("""<DvDateTimeInput value="{Bind Data.None}" placeholder="예약 없음"/>""")));
    }

    [Fact]
    public void Nullable_type_is_resolved_even_when_null()
    {
        var b = Build("""<DvDateTimeInput value="{Bind Data.None}"/>""");
        Assert.Equal(typeof(DateTime?), b.PickerValueType);
        Assert.Equal(PickerMode.DateTime, b.PickerMode);
    }

    [Fact]
    public void Openable_only_for_two_way_enabled()
    {
        Assert.True(PickerDisplay.IsOpenable(Build("""<DvDateTimeInput value="{Bind Data.At}"/>""")));
        Assert.False(PickerDisplay.IsOpenable(Build("""<DvDateTimeInput value="{Data.At}"/>""")));
        Assert.False(PickerDisplay.IsOpenable(Build("""<DvDateTimeInput value="{Bind Data.At}" readonly="true"/>""")));
        Assert.False(PickerDisplay.IsOpenable(Build("""<DvColorInput value="{Bind Data.Pen}" disabled="true"/>""")));
    }

    [Fact]
    public void Default_size_and_input_class()
    {
        var b = Build("""<DvColorInput value="{Bind Data.Pen}"/>""");
        Assert.Equal((200f, 40f), (b.Rect.Width, b.Rect.Height));
        var ro = Build("""<DvDateTimeInput value="{Data.At}"/>""");
        Assert.True(ro.Style.BorderDashed); // input-readonly = 점선
    }

    [Fact]
    public void Icon_by_mode()
    {
        Assert.Equal("fa-calendar", PickerDisplay.Icon(Build("""<DvDateTimeInput value="{Bind Data.Day}"/>""")));
        Assert.Equal("fa-clock", PickerDisplay.Icon(Build("""<DvDateTimeInput value="{Bind Data.Clock}"/>""")));
        Assert.Equal("fa-palette", PickerDisplay.Icon(Build("""<DvColorInput value="{Bind Data.Pen}"/>""")));
    }

    [Fact]
    public void Color_box_paints_swatch_pixel()
    {
        var s = new DvSurface("""<DvScreen style="padding:0"><DvColorInput value="{Bind Data.Pen}"/></DvScreen>""", new Vm());
        using var bmp = new SKBitmap(600, 400);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, 600, 400);
        var b = s.Root!.Children[0];
        var p = b.Rect.Left + b.Style.Padding.Left + PickerSwatchMid;
        Assert.Equal(new SKColor(0x3b, 0x82, 0xf6), bmp.GetPixel((int)p, (int)b.Rect.MidY));
    }

    private const float PickerSwatchMid = 10f;
}
