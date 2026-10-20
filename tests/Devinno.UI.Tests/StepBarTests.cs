using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvStepBar — 표시 전용. steps = 목록 또는 "가,나,다", current = 1부터(0 = 시작 전, 개수보다 크면 모두 끝).
public class StepBarTests
{
    private const float W = 700, H = 300;

    public sealed class Vm
    {
        public List<string> Names { get; } = new() { "투입", "혼합", "가열", "배출" };
        public int Step { get; set; } = 2;
    }

    private static (DvSurface S, LayoutBox Bar) Start(string body, Vm? vm = null)
    {
        var s = new DvSurface($"<DvScreen style=\"padding:20; align-items:start\">{body}</DvScreen>", vm ?? new Vm(), DvTheme.Light);
        s.Layout(W, H);
        return (s, Flatten(s.Root!).First(b => b.Node.Tag == "DvStepBar"));
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Steps_From_List_Or_Comma_Text()
    {
        var (_, a) = Start("<DvStepBar steps=\"{Names}\" current=\"{Step}\"/>");
        Assert.Equal(new[] { "투입", "혼합", "가열", "배출" }, a.StepNames);
        Assert.Equal(2, a.StepCurrent);
        var (_, b) = Start("<DvStepBar steps=\" 세척 , 헹굼,건조 \" current=\"0\"/>");
        Assert.Equal(new[] { "세척", "헹굼", "건조" }, b.StepNames);
        Assert.Equal(0, b.StepCurrent);
    }

    [Fact]
    public void Natural_Size()
    {
        var (_, a) = Start("<DvStepBar steps=\"{Names}\" current=\"1\"/>");
        Assert.Equal(4 * 96f, a.Rect.Width);
        Assert.Equal(64f, a.Rect.Height);
        var (_, b) = Start("<DvStepBar steps=\"하나\"/>");
        Assert.Equal(200f, b.Rect.Width);
    }

    [Fact]
    public void Paints_Done_Current_Waiting()
    {
        var (s, bar) = Start("<DvStepBar steps=\"{Names}\" current=\"{Step}\"/>");
        using var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        Assert.True(DvColor.TryParse(DvTheme.Light.Tokens["primary"], out var primary));
        var cell = bar.Rect.Width / 4;
        var cy = (int)(bar.Rect.Top + 16);
        SKColor At(int i, float dx) => bmp.GetPixel((int)(bar.Rect.Left + cell * (i + 0.5f) + dx), cy);
        Assert.Equal(primary.ToSK(), At(0, 9));   // 끝난 단계 — 채움(✓ 옆)
        Assert.Equal(primary.ToSK(), At(1, 12.5f)); // 진행 중 — 테두리
        Assert.NotEqual(primary.ToSK(), At(1, 6)); // 진행 중 안쪽은 채우지 않음
        Assert.NotEqual(primary.ToSK(), At(3, 12.5f)); // 대기
    }

    [Fact]
    public void Empty_And_Out_Of_Range_Do_Not_Throw()
    {
        var vm = new Vm { Step = 99 };
        var (s, _) = Start("<DvStepBar steps=\"{Names}\" current=\"{Step}\"/><DvStepBar steps=\"\"/><DvStepBar current=\"-3\" steps=\"a,b\" style=\"width:60\"/>", vm);
        using var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
    }
}
