using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// 그리드 트랙(열·행) = fr / px(숫자) / % / auto, 비율 고정(aspect-ratio, contain), box-shadow:none
public class GridTrackTests
{
    private static LayoutBox Lay(string markup, float w = 400, float h = 300)
        => new LayoutEngine().Layout(MarkupParser.Parse(markup), null, w, h);

    private static string Boxes(int n, string style = "height:20")
        => string.Concat(Enumerable.Repeat($"<DvBox style=\"{style}\"/>", n));

    // ── 열
    [Fact]
    public void Percent_columns_share_space_left_after_fixed_tracks()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:grid; grid-template-columns:100 25% 75%; gap:0\">{Boxes(3)}</DvScreen>");
        Assert.Equal(100, b.Children[0].Rect.Width, 3);
        Assert.Equal(75, b.Children[1].Rect.Width, 3);
        Assert.Equal(225, b.Children[2].Rect.Width, 3);
    }

    [Fact]
    public void Percent_columns_exclude_gaps_so_four_quarters_fit_exactly()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:grid; grid-template-columns:25% 25% 25% 25%; gap:10\">{Boxes(4)}</DvScreen>");
        Assert.Equal(92.5f, b.Children[0].Rect.Width, 3); // (400 - 30) / 4
        Assert.Equal(400, b.Children[3].Rect.Right, 3);
    }

    [Fact]
    public void Percent_over_100_is_normalized()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:grid; grid-template-columns:50% 50% 50% 50%; gap:0\">{Boxes(4)}</DvScreen>");
        Assert.All(b.Children, c => Assert.Equal(100, c.Rect.Width, 3));
    }

    [Fact]
    public void Auto_column_takes_content_width_and_fr_takes_the_rest()
    {
        var b = Lay("<DvScreen style=\"padding:0; display:grid; grid-template-columns:auto 1fr; gap:0\">" +
                    "<DvBox style=\"width:80; height:20\"/><DvBox style=\"height:20\"/></DvScreen>");
        Assert.Equal(80, b.Children[0].Rect.Width, 3);
        Assert.Equal(320, b.Children[1].Rect.Width, 3);
    }

    [Fact]
    public void Percent_and_fr_mix_percent_first_then_fr_gets_remainder()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:grid; grid-template-columns:50% 1fr 1fr; gap:0\">{Boxes(3)}</DvScreen>");
        Assert.Equal(200, b.Children[0].Rect.Width, 3);
        Assert.Equal(100, b.Children[1].Rect.Width, 3);
        Assert.Equal(100, b.Children[2].Rect.Width, 3);
    }

    [Fact]
    public void Quoted_track_list_is_accepted()
    {
        var b = Lay($"<DvScreen style='padding:0; display:grid; grid-template-columns:\"1fr 3fr\"; gap:0'>{Boxes(2)}</DvScreen>");
        Assert.Equal(100, b.Children[0].Rect.Width, 3);
        Assert.Equal(300, b.Children[1].Rect.Width, 3);
    }

    // ── 행
    [Fact]
    public void Row_template_fixed_and_fr_fill_container_height()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:grid; grid-template-columns:1fr; grid-template-rows:100 1fr 2fr; gap:0\">{Boxes(3)}</DvScreen>",
            400, 400);
        Assert.Equal(100, b.Children[0].Rect.Height, 3);
        Assert.Equal(100, b.Children[1].Rect.Height, 3);
        Assert.Equal(200, b.Children[2].Rect.Height, 3);
        Assert.Equal(400, b.Children[2].Rect.Bottom, 3);
    }

    [Fact]
    public void Percent_rows_exclude_gaps()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:grid; grid-template-columns:1fr; grid-template-rows:25% 75%; gap:20\">{Boxes(2)}</DvScreen>",
            400, 420);
        Assert.Equal(100, b.Children[0].Rect.Height, 3);
        Assert.Equal(300, b.Children[1].Rect.Height, 3);
    }

    [Fact]
    public void Rows_beyond_the_template_are_auto()
    {
        var b = Lay("<DvScreen style=\"padding:0; display:grid; grid-template-columns:1fr; grid-template-rows:50; gap:0\">" +
                    "<DvBox style=\"height:20\"/><DvBox style=\"height:30\"/></DvScreen>");
        Assert.Equal(50, b.Children[0].Rect.Height, 3);
        Assert.Equal(30, b.Children[1].Rect.Height, 3);
    }

    [Fact]
    public void Rows_only_grid_is_a_single_full_width_column()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:grid; grid-template-rows:1fr 1fr; gap:0\">{Boxes(2)}</DvScreen>");
        Assert.Equal(400, b.Children[0].Rect.Width, 3);
        Assert.Equal(150, b.Children[0].Rect.Height, 3);
        Assert.Equal(150, b.Children[1].Rect.Top, 3);
    }

    [Fact]
    public void Content_sized_grid_treats_fr_rows_as_auto_when_measuring()
    {
        // 높이가 정해지지 않은(내용 크기) 그리드 안에서 fr 행은 내용 높이로 측정된다
        var b = Lay("<DvScreen style=\"padding:0\"><DvBox style=\"display:grid; grid-template-columns:1fr; grid-template-rows:1fr 1fr; gap:0\">" +
                    "<DvBox style=\"height:20\"/><DvBox style=\"height:40\"/></DvBox></DvScreen>");
        Assert.Equal(60, b.Children[0].Rect.Height, 3);
    }

    // ── DvGrid 슈가
    [Fact]
    public void DvGrid_accepts_track_lists_for_columns_and_rows()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\"><DvGrid columns=\"1fr 3fr\" rows=\"60 1fr\" gap=\"none\" grow=\"1\">{Boxes(4)}</DvGrid></DvScreen>");
        var g = b.Children[0];
        Assert.Equal(100, g.Children[0].Rect.Width, 3);
        Assert.Equal(300, g.Children[1].Rect.Width, 3);
        Assert.Equal(60, g.Children[0].Rect.Height, 3);
        Assert.Equal(240, g.Children[2].Rect.Height, 3);
    }

    [Fact]
    public void DvGrid_column_count_still_works()
    {
        var b = Lay($"<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\"><DvGrid columns=\"4\" gap=\"none\">{Boxes(4)}</DvGrid></DvScreen>");
        Assert.All(b.Children[0].Children, c => Assert.Equal(100, c.Rect.Width, 3));
    }

    // ── aspect-ratio (contain)
    [Fact]
    public void Aspect_ratio_fits_largest_box_inside_allotted_area_centered()
    {
        var b = Lay("<DvScreen style=\"padding:0; display:flex; flex-direction:row; align-items:stretch\">" +
                    "<DvBox style=\"flex-grow:1; aspect-ratio:1\"/></DvScreen>", 300, 100);
        var r = b.Children[0].Rect;
        Assert.Equal(100, r.Width, 3);
        Assert.Equal(100, r.Height, 3);
        Assert.Equal(100, r.Left, 3);
        Assert.Equal(0, r.Top, 3);
    }

    [Fact]
    public void Aspect_ratio_accepts_fraction()
    {
        var b = Lay("<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:stretch\">" +
                    "<DvBox style=\"flex-grow:1; aspect-ratio:16/9\"/></DvScreen>", 320, 400);
        var r = b.Children[0].Rect;
        Assert.Equal(320, r.Width, 3);
        Assert.Equal(180, r.Height, 3);
        Assert.Equal(110, r.Top, 3);
    }

    [Fact]
    public void Aspect_ratio_derives_missing_size_when_measuring()
    {
        var b = Lay("<DvScreen style=\"padding:0\"><DvBox style=\"width:120; aspect-ratio:2\"/></DvScreen>");
        Assert.Equal(60, b.Children[0].Rect.Height, 3);
    }

    // ── box-shadow:none
    [Fact]
    public void Box_shadow_none_removes_class_shadow()
    {
        var b = Lay("<DvScreen><DvStyle>.s { box-shadow: 0 8 24 #00000040 }</DvStyle>" +
                    "<DvBox class=\"s\" style=\"box-shadow:none; height:10\"/><DvBox class=\"s\" style=\"height:10\"/></DvScreen>");
        Assert.Null(b.Children[0].Style.Shadow);
        Assert.NotNull(b.Children[1].Style.Shadow);
    }
}
