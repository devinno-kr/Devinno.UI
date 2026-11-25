using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Painting;
using SkiaSharp;
using Xunit;

// 그리기 컬링 — 클립에 닿지 않는 자식을 건너뛰어도 그림은 한 점도 달라지지 않는다(그림자·넘친 글·바깥 절대 위치·transform 포함),
// 긴 스크롤 목록에서는 화면 밖 상자를 그리지 않는다, 도구가 상자를 옮기면(Rect) 다시 잰다
public class PaintCullingTests
{
    public sealed class Row(int i)
    {
        public string Name => $"Pump {i:00}";
        public bool On => i % 2 == 0;
        public string Long => "a long label that wraps to several lines and overflows its fixed height " + i;
    }

    public sealed class Vm
    {
        public List<Row> Items { get; } = Enumerable.Range(0, 60).Select(i => new Row(i)).ToList();
        public void Hit() { }
    }

    // 넘쳐 그려지는 것들: 아래로 큰 그림자, 고정 높이를 넘는 줄바꿈 글, 행 밖 절대 위치 표시, hover 때만 커지는 transform
    private const string Markup = """
        <DvScreen style="padding:0">
          <DvStack style="height:300; overflow:scroll; gap:0">
            <DvRepeat items="{Items}" style="gap:6">
              <DvRow style="height:36; position:relative">
                <DvLabel text="{Item.Name}"/>
                <DvLabel text="{Item.Long}" style="width:120; height:16; font-size:11"/>
                <DvToggle value="{Item.On}"/>
                <DvBox style="width:30; height:20; background:#2563eb; box-shadow: 0 34 10 #ff000080"/>
                <DvBox style="position:absolute; left:380; top:-26; width:24; height:24; background:#16a34a"/>
                <DvBox style="width:20; height:20; background:#f59e0b" hover-style="transform: scale(3)" onclick="Hit"/>
              </DvRow>
            </DvRepeat>
          </DvStack>
        </DvScreen>
        """;

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static DvSurface Start(float scrollY)
    {
        var s = new DvSurface(Markup, new Vm());
        s.Layout(480, 320);
        Flatten(s.Root!).First(b => b.IsScroller).ScrollY = scrollY;
        return s;
    }

    private static (byte[] Pixels, int Painted) Render(DvSurface s, bool cull)
    {
        Painter.NoCulling = !cull;
        Painter.PaintedBoxes = 0;
        try
        {
            using var bmp = new SKBitmap(480, 320);
            using (var c = new SKCanvas(bmp))
                s.RenderTo(c, 480, 320);
            return (bmp.Bytes, Painter.PaintedBoxes);
        }
        finally
        {
            Painter.NoCulling = false;
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(437f)] // 행 경계에 걸친 자리 — 위 행의 그림자·넘친 글, 아래 행의 바깥 절대 위치가 보이는 자리
    [InlineData(1500f)]
    public void Culled_painting_is_pixel_identical(float scrollY)
    {
        var s = Start(scrollY);
        var all = Render(s, cull: false);
        var culled = Render(s, cull: true);
        Assert.Equal(all.Pixels, culled.Pixels);
        Assert.True(culled.Painted < all.Painted / 3, $"painted {culled.Painted} of {all.Painted}");
    }

    [Fact]
    public void A_box_moved_by_a_tool_is_painted_at_its_new_place()
    {
        var s = Start(0);
        Render(s, cull: true); // 범위를 한 번 잰다
        var rows = Flatten(s.Root!).Where(b => b.Node.Tag == "DvRow").ToList();
        var far = rows[^1];
        var dy = rows[1].Rect.Top - far.Rect.Top; // 맨 아래(화면 밖) 행을 둘째 행 자리로
        foreach (var b in Flatten(far))
            b.Rect = SKRect.Create(b.Rect.Left, b.Rect.Top + dy, b.Rect.Width, b.Rect.Height);
        Assert.Equal(Render(s, cull: false).Pixels, Render(s, cull: true).Pixels);
    }

    [Fact]
    public void A_hovered_transform_widens_the_paint_area()
    {
        var box = Flatten(Start(0).Root!).First(b => b.StyleHover is { Transform.IsIdentity: false });
        var bounds = Painter.PaintBoundsOf(box);
        // scale(3) 가운데 기준 — 20px 상자(+ 양쪽 여유 24)가 세 배로
        Assert.Equal((20 + 2 * 24) * 3, bounds.Width, 1);
        Assert.Equal(box.Rect.MidY, bounds.MidY, 1);
    }

    [Fact]
    public void A_hovered_scaled_box_paints_the_same_culled()
    {
        var s = Start(437);
        // 화면 안 둘째 행의 상자 — 커지면 위아래 이웃 행 위로 그려진다
        var target = Flatten(s.Root!).Where(b => b.StyleHover is { Transform.IsIdentity: false }).First(b => b.Rect.MidY - 437 > 60);
        var before = Render(s, cull: true).Pixels;
        s.PointerMove(new SKPoint(target.Rect.MidX, target.Rect.MidY - 437));
        s.Layout(480, 320);
        var culled = Render(s, cull: true).Pixels;
        Assert.NotEqual(before, culled); // hover 가 걸렸다
        Assert.Equal(Render(s, cull: false).Pixels, culled);
    }
}
