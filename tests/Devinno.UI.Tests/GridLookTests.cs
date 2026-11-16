using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 그리드 생김새(단계 4): 머리글 아래 경계 = hairline(H3), 동작 못 하는 행의 버튼은 숨김, 숫자 값은 고정폭 숫자(H6).
public class GridLookTests
{
    public sealed class Row
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public bool AckDisabled { get; set; }
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; } =
        [
            new() { Name = "a", Count = 111, AckDisabled = true },
            new() { Name = "b", Count = 888 },
        ];
        public void Ack(Row r) { }
    }

    // 머리글 40, 행 48 — 이름 100 | 수 100(오른쪽 정렬) | 버튼 100
    private static SKBitmap Render(string gridStyle = "")
    {
        var markup = "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\">" +
                     $"<DvDataGrid items=\"{{Rows}}\" style=\"height:240; {gridStyle}\">" +
                     "<DvColumn header=\"Name\" field=\"Name\" width=\"100\"/>" +
                     "<DvColumn header=\"Count\" field=\"Count\" width=\"100\" align=\"right\"/>" +
                     "<DvColumn header=\"Ack\" width=\"100\" cell-type=\"button\" text=\"ACK\" onclick=\"Ack\" cell-disabled=\"AckDisabled\"/>" +
                     "</DvDataGrid></DvScreen>";
        var s = new DvSurface(markup, new Vm(), DvTheme.Light);
        s.Layout(300, 300);
        var bmp = new SKBitmap(300, 300);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, 300, 300);
        return bmp;
    }

    private static SKColor Token(string key) => ThemeResolver.Color(DvTheme.Light, key);

    [Fact]
    public void Header_edge_is_a_hairline()
    {
        using var bmp = Render();
        // 머리글 바닥 40 — 경계선이 1px 이면 y=38 은 아직 머리글 배경
        Assert.Equal(Token("grid-header"), bmp.GetPixel(30, 38));
    }

    [Fact]
    public void Button_is_hidden_in_a_row_that_cannot_act()
    {
        using var bmp = Render();
        var name0 = bmp.GetPixel(95, 40 + 24);
        for (var x = 206; x < 294; x++) // 첫 행(비활성) 버튼 자리 전체(글자 포함) — 행 배경만
            for (var y = 48; y < 80; y++)
                Assert.True(bmp.GetPixel(x, y) == name0, $"button pixel at ({x},{y})");
        Assert.NotEqual(bmp.GetPixel(95, 40 + 48 + 24), bmp.GetPixel(250, 40 + 48 + 24)); // 둘째 행 버튼은 보임
    }

    [Fact]
    public void Number_cells_use_tabular_digits()
    {
        // Georgia 숫자는 비례폭(1 이 8 보다 좁음) — 고정폭 칸이면 오른쪽 정렬 "111"·"888" 의 왼쪽 끝이 거의 같다
        using var bmp = Render("font-family: Georgia");
        static int LeftInk(SKBitmap b, int y)
        {
            var bg = b.GetPixel(102, y);
            for (var x = 102; x < 198; x++)
                for (var dy = -6; dy <= 6; dy++)
                    if (b.GetPixel(x, y + dy) != bg && b.GetPixel(x, y + dy).Red < 200)
                        return x;
            return -1;
        }
        var a = LeftInk(bmp, 40 + 24);
        var b = LeftInk(bmp, 40 + 48 + 24);
        Assert.True(Math.Abs(a - b) <= 3, $"111 starts at {a}, 888 at {b}");
    }
}
