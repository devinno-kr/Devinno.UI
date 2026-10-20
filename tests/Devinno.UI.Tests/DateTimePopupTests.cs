using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// P8d-6 시간 팝업 — 열기·배치·날짜·월 이동·확인/취소·게이트·수명
public class DateTimePopupTests
{
    public sealed class Vm
    {
        private DateTime _at = new(2026, 10, 14, 9, 30, 0);
        public int Writes;
        public DateTime At { get => _at; set { _at = value; Writes++; } }
        public DateOnly Day { get; set; } = new(2026, 10, 14);
        public DateTime? None { get; set; }
        public bool Allow = true;
        public DateTime? Gated;
        public bool CheckAt(DateTime v) { Gated = v; return Allow; }
        public int Saves;
        public void Save() => Saves++;
    }

    private const float W = 800, H = 600;

    private const string Form = """
        <DvScreen style="padding:0; gap:0">
          <DvDateTimeInput id="at" value="{Bind Data.At}" onbeforecommit="CheckAt"/>
          <DvDateTimeInput id="day" value="{Bind Data.Day}"/>
          <DvDateTimeInput id="none" value="{Bind Data.None}"/>
          <DvButton text="저장" onclick="Save" style="position:absolute; left:700; top:550; width:80; height:40"/>
        </DvScreen>
        """;

    private static DvSurface Surface(Vm vm, float w = W, float h = H)
    {
        var s = new DvSurface(Form, vm) { UseNativeEditor = true };
        s.Layout(w, h);
        return s;
    }

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static SKPoint Mid(SKRect r) => new(r.MidX, r.MidY);

    private static LayoutBox Box(DvSurface s, string id)
        => Flatten(s.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static DateTimePopup Open(DvSurface s, string id)
    {
        Tap(s, Mid(Box(s, id).Rect));
        return Assert.IsType<DateTimePopup>(s.TopPopup);
    }

    private static int IndexOf(DateTimePopup p, DateOnly d)
        => Enumerable.Range(0, 42).First(i => p.State.CellDate(i) == d);

    [Fact]
    public void Opens_below_anchor_with_mode_size()
    {
        var s = Surface(new Vm());
        var p = Open(s, "at");
        var a = Box(s, "at").Rect;
        Assert.True(Box(s, "at").Editing);
        Assert.Equal(a.Bottom + PopupPlacement.Gap, p.Bounds.Top);
        Assert.Equal(DateTimePopup.SizeFor(PickerMode.DateTime), p.Bounds.Size);
        Assert.Equal(2, p.FieldCount);
        Assert.Equal(0, Open(Surface(new Vm()), "day").FieldCount); // 새 서피스 — 열린 팝업 바깥 누름은 닫기만
    }

    [Fact]
    public void Flips_above_or_fits_vertically_when_no_room()
    {
        var vm = new Vm();
        var s = new DvSurface("""
            <DvScreen style="padding:0">
              <DvDateTimeInput id="low" value="{Bind Data.At}" style="position:absolute; left:760; top:520"/>
            </DvScreen>
            """, vm) { UseNativeEditor = true };
        s.Layout(W, H);
        var lr = Box(s, "low").Rect;                                       // 기본 폭 200 — 가운데는 화면 밖이라 보이는 부분을 누름
        Tap(s, new SKPoint(lr.Left + 20, lr.MidY));
        var p = Assert.IsType<DateTimePopup>(s.TopPopup);
        Assert.True(p.Bounds.Bottom <= Box(s, "low").Rect.Top);           // 위로
        Assert.True(p.Bounds.Right <= W - PopupPlacement.Margin + 0.01f);  // 화면 안으로

        // 위·아래 다 모자람 — 화면 안 세로 맞춤(앵커가 화면 안에 남도록 따로 만든 서피스)
        var t = new DvSurface("""
            <DvScreen style="padding:0">
              <DvDateTimeInput id="mid" value="{Bind Data.At}" style="position:absolute; left:20; top:130"/>
            </DvScreen>
            """, new Vm()) { UseNativeEditor = true };
        t.Layout(W, 300);
        var a = Mid(Box(t, "mid").Rect);
        t.PointerDown(a);
        t.PointerUp(a);
        t.Layout(W, 300);
        var q = Assert.IsType<DateTimePopup>(t.TopPopup);
        var ar = Box(t, "mid").Rect;
        Assert.NotEqual(ar.Bottom + PopupPlacement.Gap, q.Bounds.Top);     // 아래 아님
        Assert.True(q.Bounds.Bottom > ar.Top);                             // 위 아님
        Assert.True(q.Bounds.Top >= PopupPlacement.Margin);
    }

    [Fact]
    public void Ok_writes_once_and_closes()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "at");
        Tap(s, Mid(p.DayRect(IndexOf(p, new DateOnly(2026, 10, 20)))));
        Assert.Equal(0, vm.Writes);                       // 고르기만으로는 안 씀
        Tap(s, Mid(p.OkRect));
        Assert.Equal(1, vm.Writes);
        Assert.Equal(new DateTime(2026, 10, 20, 9, 30, 0), vm.At);
        Assert.Null(s.TopPopup);
        Assert.False(Box(s, "at").Editing);
    }

    [Fact]
    public void Same_value_ok_does_not_write()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "at");
        Tap(s, Mid(p.OkRect));
        Assert.Equal(0, vm.Writes);
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Same_value_with_milliseconds_ok_does_not_write()
    {
        var vm = new Vm();
        vm.At = new DateTime(2026, 10, 14, 9, 30, 15, 123);
        vm.Writes = 0;
        var s = Surface(vm);
        var p = Open(s, "at");
        Tap(s, Mid(p.OkRect));
        Assert.Equal(0, vm.Writes);
        Assert.Null(vm.Gated);
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Gate_rejection_keeps_popup_open()
    {
        var vm = new Vm { Allow = false };
        var s = Surface(vm);
        var p = Open(s, "at");
        Tap(s, Mid(p.DayRect(IndexOf(p, new DateOnly(2026, 10, 21)))));
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new DateTime(2026, 10, 21, 9, 30, 0), vm.Gated);
        Assert.Equal(0, vm.Writes);
        Assert.Same(p, s.TopPopup);
    }

    [Fact]
    public void Cancel_and_outside_tap_write_nothing_and_do_not_forward()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "at");
        Tap(s, Mid(p.DayRect(IndexOf(p, new DateOnly(2026, 10, 22)))));
        Tap(s, Mid(p.CancelRect));
        Assert.Null(s.TopPopup);
        Open(s, "at");
        Tap(s, new SKPoint(740, 570)); // 저장 버튼 — 바깥 누름은 닫기만
        Assert.Null(s.TopPopup);
        Assert.Equal(0, vm.Saves);
        Assert.Equal(0, vm.Writes);
    }

    [Fact]
    public void Tapping_same_box_closes()
    {
        var s = Surface(new Vm());
        Open(s, "at");
        Tap(s, Mid(Box(s, "at").Rect));
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Month_arrows_move_view_only()
    {
        var s = Surface(new Vm());
        var p = Open(s, "at");
        Tap(s, Mid(p.NextRect));
        Assert.Equal((2026, 11), (p.State.ViewYear, p.State.ViewMonth));
        Assert.Equal(new DateOnly(2026, 10, 14), p.State.Selected);
        Tap(s, Mid(p.PrevRect)); Tap(s, Mid(p.PrevRect));
        Assert.Equal((2026, 9), (p.State.ViewYear, p.State.ViewMonth));
    }

    [Fact]
    public void Null_value_opens_today_and_cancel_writes_nothing()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "none");
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), p.State.Selected);
        Assert.False(p.HadValue);
        Tap(s, Mid(p.CancelRect));
        Assert.Null(vm.None);
    }

    [Fact]
    public void Null_value_ok_writes_now()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "none");
        Tap(s, Mid(p.OkRect));
        Assert.NotNull(vm.None);   // null → 값: "같은 값"이 아니므로 쓴다
        Assert.Equal(0, vm.None!.Value.Second);
    }

    [Fact]
    public void DateOnly_property_gets_dateonly()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "day");
        Tap(s, Mid(p.DayRect(IndexOf(p, new DateOnly(2026, 10, 1)))));
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new DateOnly(2026, 10, 1), vm.Day);
    }

    [Fact]
    public void Rebuild_keeps_popup_and_temp_value()
    {
        var vm = new Vm();
        var s = Surface(vm);
        var p = Open(s, "at");
        Tap(s, Mid(p.DayRect(IndexOf(p, new DateOnly(2026, 10, 25)))));
        s.Invalidate();
        s.Layout(W, H);
        Assert.Same(p, s.TopPopup);
        Assert.Same(Box(s, "at"), Assert.IsType<BoxSlot>(p.Slot).Box);
        Assert.True(Box(s, "at").Editing);
        Assert.Equal(new DateOnly(2026, 10, 25), p.State.Selected);
    }

    [Fact]
    public void Readonly_and_disabled_do_not_open()
    {
        var vm = new Vm();
        var s = new DvSurface("""
            <DvScreen><DvDateTimeInput id="a" value="{Data.At}"/><DvDateTimeInput id="b" value="{Bind Data.At}" disabled="true"/></DvScreen>
            """, vm);
        s.Layout(W, H);
        Tap(s, Mid(Box(s, "a").Rect));
        Tap(s, Mid(Box(s, "b").Rect));
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Opening_commits_pending_dvinput_edit()
    {
        var vm = new InputVm();
        var s = new DvSurface("""
            <DvScreen><DvInput id="name" value="{Bind Data.Name}"/><DvDateTimeInput id="at" value="{Bind Data.At}"/></DvScreen>
            """, vm) { UseNativeEditor = true };
        s.Layout(W, H);
        Tap(s, Mid(Box(s, "name").Rect));
        s.SetEditText(s.Edit.SessionId, "새 이름");
        Tap(s, Mid(Box(s, "at").Rect));
        Assert.Equal("새 이름", vm.Name);
        Assert.IsType<DateTimePopup>(s.TopPopup);
    }

    [Fact]
    public void Cancel_uses_secondary_button_fill()
    {
        var theme = Devinno.UI.Styling.DvTheme.Dark;
        var s = new DvSurface(Form, new Vm(), theme) { UseNativeEditor = true };
        s.Layout(W, H);
        var p = Open(s, "at");
        using var bmp = new SKBitmap((int)W, (int)H);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, W, H);
        var r = p.CancelRect;
        Assert.True(theme.TryGet("surface-raised", out var raw));
        Assert.True(Devinno.UI.Styling.DvColor.TryParse(raw, out var raised));
        Assert.Equal(raised.ToSK(), bmp.GetPixel((int)r.Left + 10, (int)r.Top + 6)); // DvButton secondary 채움
    }

    public sealed class MinVm
    {
        private DateTime _at;
        private DateOnly _day = DateOnly.MinValue;
        public int Writes;
        public DateTime At { get => _at; set { _at = value; Writes++; } }
        public DateOnly Day { get => _day; set { _day = value; Writes++; } }
    }

    [Theory]
    [InlineData("at")]
    [InlineData("day")]
    public void MinValue_opens_like_null_and_paints(string id)
    {
        var vm = new MinVm();
        var s = new DvSurface("""
            <DvScreen style="padding:0"><DvDateTimeInput id="at" value="{Bind Data.At}"/><DvDateTimeInput id="day" value="{Bind Data.Day}"/></DvScreen>
            """, vm) { UseNativeEditor = true };
        s.Layout(W, H);
        var p = Open(s, id);
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, W, H);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), p.State.Selected);
        Assert.False(p.HadValue);
        Tap(s, Mid(p.CancelRect));
        Assert.Null(s.TopPopup);
        Assert.Equal(0, vm.Writes);
    }

    public sealed class InputVm
    {
        public string Name { get; set; } = "";
        public DateTime At { get; set; } = new(2026, 10, 14);
    }
}
