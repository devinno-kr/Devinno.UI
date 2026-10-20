using Devinno.UI.Grid;
using Devinno.UI.Styling;
using Xunit;

// 3a-1 그리드 순수 계산 — 접근자, 열 폭, 보이는 행, 점 → 행
public class GridCoreTests
{
    public sealed class Motor { public double Speed { get; set; } }
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Motor? Motor { get; set; }
        public int? Opt { get; set; }
        public DateTime Time { get; set; }
    }
    public struct Point2 { public int X { get; set; } }

    [Fact]
    public void Accessor_reads_dotted_path()
    {
        var e = FieldAccessor.For(typeof(Row), "Motor.Speed");
        Assert.Equal(12.5, e.Get(new Row { Motor = new Motor { Speed = 12.5 } }));
        Assert.Equal(typeof(double), e.ValueType);
    }

    [Fact]
    public void Accessor_null_in_middle_gives_null()
    {
        var e = FieldAccessor.For(typeof(Row), "Motor.Speed");
        Assert.Null(e.Get(new Row { Motor = null }));
    }

    [Fact]
    public void Accessor_missing_property_has_no_value_type()
    {
        var e = FieldAccessor.For(typeof(Row), "Nope");
        Assert.Null(e.ValueType);
        Assert.Null(e.Get(new Row()));
    }

    [Fact]
    public void Accessor_struct_item_and_nullable_value_type()
    {
        Assert.Equal(7, FieldAccessor.For(typeof(Point2), "X").Get(new Point2 { X = 7 }));
        var opt = FieldAccessor.For(typeof(Row), "Opt");
        Assert.Equal(typeof(int), opt.ValueType);
        Assert.Null(opt.Get(new Row()));
    }

    [Fact]
    public void Double_accessor_reads_numbers_without_boxing_and_nan_for_null()
    {
        Assert.Equal(7.0, FieldAccessor.For(typeof(Row), "Id").GetDouble!(new Row { Id = 7 }));
        Assert.True(double.IsNaN(FieldAccessor.For(typeof(Row), "Opt").GetDouble!(new Row())));
        Assert.Equal(3.0, FieldAccessor.For(typeof(Row), "Opt").GetDouble!(new Row { Opt = 3 }));
        Assert.True(double.IsNaN(FieldAccessor.For(typeof(Row), "Motor.Speed").GetDouble!(new Row())));
        Assert.Null(FieldAccessor.For(typeof(Row), "Name").GetDouble);
        Assert.Null(FieldAccessor.For(typeof(Row), "Nope").GetDouble);
    }

    [Fact]
    public void Accessor_is_cached_per_type_and_path()
        => Assert.Same(FieldAccessor.For(typeof(Row), "Id"), FieldAccessor.For(typeof(Row), "Id"));

    [Fact]
    public void Reader_follows_item_type()
    {
        var r = new FieldReader("X");
        Assert.Equal(3, r.Get(new Point2 { X = 3 }));
        Assert.Null(r.Get(new Row())); // 다른 타입 — 그 타입에 X 없음
    }

    [Theory]
    [InlineData("120", "Px", 120f)]
    [InlineData("120px", "Px", 120f)]
    [InlineData("20%", "Percent", 20f)]
    [InlineData("*", "Star", 1f)]
    [InlineData("2*", "Star", 2f)]
    [InlineData(null, "Star", 1f)]
    [InlineData("abc", "Star", 1f)]
    public void Width_parse(string? raw, string kind, float value)
    {
        var w = GridWidth.Parse(raw);
        Assert.Equal(kind, w.Kind.ToString());
        Assert.Equal(value, w.Value);
    }

    private static GridColumn Col(string width) => new() { Header = "", Reader = new FieldReader("Id"), Width = GridWidth.Parse(width) };

    [Fact]
    public void Columns_px_percent_star()
    {
        var cols = new[] { Col("100"), Col("25%"), Col("*"), Col("2*") };
        var total = GridGeometry.ResolveColumns(cols, 1000);
        Assert.Equal(100f, cols[0].W);
        Assert.Equal(250f, cols[1].W);
        Assert.Equal(650f / 3f, cols[2].W, 3);
        Assert.Equal(1300f / 3f, cols[3].W, 3);
        Assert.Equal(new[] { 0f, 100f, 350f, 350f + 650f / 3f }, cols.Select(c => c.X).ToArray());
        Assert.Equal(1000f, total, 3);
    }

    [Fact]
    public void Star_gets_minimum_when_no_room()
    {
        var cols = new[] { Col("600"), Col("600"), Col("*") };
        var total = GridGeometry.ResolveColumns(cols, 1000);
        Assert.Equal(GridGeometry.MinStarWidth, cols[2].W);
        Assert.Equal(1200f + GridGeometry.MinStarWidth, total);
    }

    [Theory]
    [InlineData(0f, 200f, 0, 4)]      // 200/48 → 0..4 (4.17)
    [InlineData(100f, 200f, 2, 6)]    // 100/48=2.08 .. 300/48=6.25
    [InlineData(4600f, 200f, 95, 99)] // 끝 — count 로 제한
    public void Visible_rows(float scrollY, float bodyH, int first, int last)
        => Assert.Equal((first, last), GridGeometry.VisibleRows(scrollY, bodyH, 48f, 100));

    [Fact]
    public void Visible_rows_empty()
        => Assert.Equal((0, -1), GridGeometry.VisibleRows(0, 200, 48, 0));

    [Theory]
    [InlineData(10f, 0f, -1)]   // 헤더
    [InlineData(40f, 0f, 0)]    // 몸통 첫 줄
    [InlineData(87.9f, 0f, 0)]
    [InlineData(88f, 0f, 1)]
    [InlineData(40f, 96f, 2)]   // 스크롤 2행
    [InlineData(239f, 0f, 4)]
    [InlineData(241f, 0f, -1)]  // 몸통 아래(그리드 밖)
    public void Hit_row(float y, float scrollY, int expected)
        => Assert.Equal(expected, GridGeometry.HitRow(y, scrollY, 40f, 200f, 48f, 100));

    [Fact]
    public void Hit_row_below_last_row_is_none()
        => Assert.Equal(-1, GridGeometry.HitRow(40f + 48f * 3 + 5, 0, 40f, 400f, 48f, 3));

    [Fact]
    public void Column_text_formats_and_aligns_numbers_right()
    {
        var c = new GridColumn { Header = "", Reader = new FieldReader("Id"), Width = GridWidth.Parse("*"), Format = "000" };
        Assert.Equal("007", c.Text(new Row { Id = 7 }));
        Assert.Equal(TextAlign.Right, c.EffectiveAlign);
        var n = new GridColumn { Header = "", Reader = new FieldReader("Name"), Width = GridWidth.Parse("*") };
        Assert.Equal("a", n.Text(new Row { Name = "a" }));
        Assert.Equal(TextAlign.Left, n.EffectiveAlign);
    }

    [Fact]
    public void Column_text_bad_format_falls_back()
    {
        var c = new GridColumn { Header = "", Reader = new FieldReader("Time"), Width = GridWidth.Parse("*"), Format = "{" };
        var t = new DateTime(2026, 1, 2, 3, 4, 5);
        Assert.Equal(t.ToString(System.Globalization.CultureInfo.InvariantCulture), c.Text(new Row { Time = t }));
    }
}
