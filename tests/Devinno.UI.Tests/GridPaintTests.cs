using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 3a-3 그리드 그리기 — 헤더·선택 행·고정 열·빈 목록·큰 목록
public class GridPaintTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class Vm
    {
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 100).Select(i => new Row { Id = i, Name = "R" + i }).ToList();
        public List<Row> Picked { get; set; } = new();
    }

    private static SKColor Token(string key)
    {
        Assert.True(DvTheme.Light.TryGet(key, out var raw));
        Assert.True(DvColor.TryParse(raw, out var c));
        return c.ToSK();
    }

    private static SKBitmap Render(DvSurface s)
    {
        var bmp = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, 300, 300);
        return bmp;
    }

    private static DvSurface Surface(string markup, object vm)
    {
        var s = new DvSurface(markup, vm, DvTheme.Light);
        s.Layout(300, 300);
        return s;
    }

    [Fact]
    public void Header_uses_grid_header_token()
    {
        using var bmp = Render(Surface(GridLayoutBuildTests.Screen(), new Vm()));
        Assert.Equal(Token("grid-header"), bmp.GetPixel(3, 8));
    }

    [Fact]
    public void Selected_row_uses_selection_token()
    {
        var vm = new Vm();
        vm.Picked.Add(vm.Rows[2]);
        using var bmp = Render(Surface(
            GridLayoutBuildTests.Screen("items=\"{Rows}\" selection-mode=\"single\" selection=\"{Bind Picked}\""), vm));
        Assert.Equal(Token("grid-row-selected"), bmp.GetPixel(3, 40 + 48 * 2 + 20));
        Assert.Equal(Token("grid-row"), bmp.GetPixel(3, 40 + 48 * 0 + 20));
        Assert.Equal(Token("grid-row-alt"), bmp.GetPixel(3, 40 + 48 * 1 + 20));
    }

    [Fact]
    public void Frozen_columns_do_not_move_with_horizontal_scroll()
    {
        var s = Surface(GridLayoutBuildTests.Screen(cols:
            "<DvColumn header=\"ID\" field=\"Id\" width=\"120\" frozen=\"true\"/><DvColumn header=\"이름\" field=\"Name\" width=\"400\"/>"),
            new Vm());
        using var before = Render(s);
        var grid = s.Root!.Children[0];
        grid.ScrollX = 100;
        s.Repaint();
        using var after = Render(s);

        var frozenSame = true;
        var scrolledDiff = false;
        for (var y = 0; y < 230; y++)
        {
            for (var x = 0; x < 118; x++)
                frozenSame &= before.GetPixel(x, y) == after.GetPixel(x, y);
            for (var x = 125; x < 280; x++)
                scrolledDiff |= before.GetPixel(x, y) != after.GetPixel(x, y);
        }

        Assert.True(frozenSame);
        Assert.True(scrolledDiff);
    }

    [Fact]
    public void Cell_text_uses_theme_text_color_in_dark()
    {
        var s = new DvSurface(GridLayoutBuildTests.Screen(), new Vm(), DvTheme.Dark);
        s.Layout(300, 300);
        using var bmp = Render(s);
        // 첫 행 이름 셀(x 92~150) 글자 중 가장 밝은 픽셀 — 다크 테마 글자(#f8fafc)면 밝아야 한다
        var max = 0;
        for (var x = 92; x < 150; x++)
            for (var y = 50; y < 80; y++)
                max = Math.Max(max, bmp.GetPixel(x, y).Red);
        Assert.True(max > 0xc0, $"max red {max:x2}");
    }

    [Fact]
    public void Rounded_corners_and_border_are_drawn_over_the_cells()
    {
        var s = Surface("<DvScreen style=\"padding:0; background:#ffffff; display:flex; flex-direction:column; align-items:stretch\">" +
                        "<DvDataGrid items=\"{Rows}\" style=\"height:240; border-radius:16; border:3 solid #ff0000\">" +
                        "<DvColumn field=\"Id\"/></DvDataGrid></DvScreen>", new Vm());
        using var bmp = Render(s);
        Assert.Equal(new SKColor(0xff, 0, 0), bmp.GetPixel(0, 120));           // 왼쪽 테두리(행 배경에 덮이지 않음, 3px 선 = 경계 중심)
        Assert.Equal(new SKColor(0xff, 0xff, 0xff), bmp.GetPixel(1, 1));        // 모서리 밖 = 화면 배경(헤더가 삐져나오지 않음)
        Assert.Equal(new SKColor(0xff, 0xff, 0xff), bmp.GetPixel(298, 238));
    }

    [Fact]
    public void Empty_items_paint_header_only()
    {
        var vm = new Vm { Rows = new List<Row>() };
        using var bmp = Render(Surface(GridLayoutBuildTests.Screen(), vm));
        Assert.Equal(Token("grid-header"), bmp.GetPixel(3, 8));
    }

    [Fact]
    public void Large_list_paints_in_the_middle()
    {
        var vm = new Vm { Rows = Enumerable.Range(0, 300_000).Select(i => new Row { Id = i, Name = "R" + i }).ToList() };
        var s = Surface(GridLayoutBuildTests.Screen(), vm);
        s.Root!.Children[0].ScrollY = 150_000 * 48;
        using var bmp = Render(s);
        Assert.Equal(Token("grid-row"), bmp.GetPixel(3, 40 + 20)); // 150000 = 짝수 행
    }

    [Fact]
    public void Shrinking_list_while_scrolled_paints_without_error()
    {
        var vm = new Vm();
        var s = Surface(GridLayoutBuildTests.Screen(), vm);
        s.Root!.Children[0].ScrollY = 4000;
        vm.Rows.RemoveRange(10, 90);
        s.Repaint();
        using var bmp = Render(s);
        Assert.Equal(10 * 48 + 40 - 240, s.Root!.Children[0].ScrollY, 3);
    }
}
