using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// P8d-8 팝업 칸 편집 — 숫자 키패드·hex 키보드, 네이티브/가상 키보드 순서, pan
public class PopupFieldEditTests
{
    public sealed class Vm
    {
        public DateTime At { get; set; } = new(2026, 10, 14, 9, 30, 0);
        public string Pen { get; set; } = "#0000FF";
    }

    private const float W = 800, H = 600;

    private static DvSurface Surface(bool native, string markup = """<DvScreen style="padding:0"><DvDateTimeInput id="at" value="{Bind Data.At}"/><DvColorInput id="pen" value="{Bind Data.Pen}"/></DvScreen>""")
    {
        var s = new DvSurface(markup, new Vm()) { UseNativeEditor = native };
        s.Layout(W, H);
        return s;
    }

    private static void Tap(DvSurface s, SKPoint p) { s.PointerDown(p); s.PointerUp(p); s.Layout(W, H); }
    private static SKPoint Mid(SKRect r) => new(r.MidX, r.MidY);
    private static LayoutBox Box(DvSurface s, string id)
        => Flatten(s.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);
    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Tapping_hour_field_starts_numeric_session_on_field_rect()
    {
        var s = Surface(native: true);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(0)));
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(DvEditKind.Numeric, s.Edit.Kind);
        Assert.Equal("09", s.Edit.InitialText);
        Assert.Equal(p.FieldRect(0), s.Edit.Rect);
        Assert.Equal(23d, s.Edit.Numeric!.Max);
        Assert.False(s.Edit.Numeric.IsSigned);
    }

    [Fact]
    public void Enter_commits_field_to_state_not_vm()
    {
        var s = Surface(native: true);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(1)));
        s.SetEditText(s.Edit.SessionId, "45");
        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.Equal(45, p.State.Minute);
        Assert.False(s.Edit.IsEditing);
        Assert.Same(p, s.TopPopup);
    }

    [Fact]
    public void Out_of_range_is_rejected_and_session_stays()
    {
        var s = Surface(native: true);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(0)));
        s.SetEditText(s.Edit.SessionId, "24");
        Assert.False(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.True(s.Edit.IsEditing);
        Assert.NotNull(s.Edit.Error);
        Assert.Equal(9, p.State.Hour);
    }

    [Fact]
    public void Native_ok_commits_field_first_then_popup()
    {
        var vm = new Vm();
        var s = new DvSurface("""<DvScreen style="padding:0"><DvDateTimeInput id="at" value="{Bind Data.At}"/></DvScreen>""", vm) { UseNativeEditor = true };
        s.Layout(W, H);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(0)));
        s.SetEditText(s.Edit.SessionId, "13");
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new DateTime(2026, 10, 14, 13, 30, 0), vm.At);
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Native_ok_with_rejected_field_keeps_popup_and_vm()
    {
        var vm = new Vm();
        var s = new DvSurface("""<DvScreen style="padding:0"><DvDateTimeInput id="at" value="{Bind Data.At}"/></DvScreen>""", vm) { UseNativeEditor = true };
        s.Layout(W, H);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(0)));
        s.SetEditText(s.Edit.SessionId, "99");
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 0), vm.At);
        Assert.Same(p, s.TopPopup);
        Assert.False(s.Edit.IsEditing); // FocusLost 거절 = 원래 값으로 닫힘(DvInput 규칙)
    }

    [Fact]
    public void Native_focus_lost_before_ok_press_blocks_commit()
    {
        var vm = new Vm();
        var s = new DvSurface("""<DvScreen style="padding:0"><DvDateTimeInput id="at" value="{Bind Data.At}"/></DvScreen>""", vm) { UseNativeEditor = true };
        s.Layout(W, H);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        var day = Enumerable.Range(0, 42).First(i => p.State.CellDate(i) == new DateOnly(2026, 10, 20));
        Tap(s, Mid(p.DayRect(day)));
        Tap(s, Mid(p.FieldRect(0)));
        s.SetEditText(s.Edit.SessionId, "99");
        Assert.False(s.CommitEdit(s.Edit.SessionId, DvEditExit.FocusLost)); // 어댑터가 포커스 잃음을 먼저 보냄
        Tap(s, Mid(p.OkRect));
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 0), vm.At);
        Assert.Same(p, s.TopPopup);
    }

    [Fact]
    public void Commit_block_does_not_leak_into_next_popup()
    {
        var vm = new Vm();
        var s = new DvSurface("""<DvScreen style="padding:0"><DvDateTimeInput id="at" value="{Bind Data.At}"/></DvScreen>""", vm) { UseNativeEditor = true };
        s.Layout(W, H);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(0)));
        s.SetEditText(s.Edit.SessionId, "99");
        s.CommitEdit(s.Edit.SessionId, DvEditExit.FocusLost); // 거절 — 막음 표시
        Tap(s, new SKPoint(W - 5, H - 5));                    // 바깥 누름 = 닫힘(떼기는 삼킴)
        Assert.Null(s.TopPopup);

        Tap(s, Mid(Box(s, "at").Rect));
        p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.OkRect)); // 다시 연 팝업의 첫 누름 — 막히면 팝업이 남는다
        Assert.Null(s.TopPopup);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 0), vm.At);
    }

    [Fact]
    public void Native_outside_tap_cancels_field_and_closes_popup()
    {
        var s = Surface(native: true);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(0)));
        s.SetEditText(s.Edit.SessionId, "13");
        Tap(s, new SKPoint(W - 5, H - 5));
        Assert.False(s.Edit.IsEditing);
        Assert.Null(s.TopPopup);
    }

    [Fact]
    public void Soft_keyboard_outside_tap_cancels_field_and_keeps_popup()
    {
        var s = Surface(native: false);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(0)));
        Assert.NotNull(s.Keyboard);
        s.SetEditText(s.Edit.SessionId, "13");
        Tap(s, Mid(p.DayRect(0))); // 키보드 모달 — 칸 편집 취소, 누름은 삼킴
        Assert.False(s.Edit.IsEditing);
        Assert.Same(p, s.TopPopup);
        Assert.Equal(9, p.State.Hour);
        Assert.Equal(new DateOnly(2026, 10, 14), p.State.Selected); // 날짜 탭도 전달 안 됨
    }

    [Fact]
    public void Hex_field_rejects_bad_text_and_accepts_good()
    {
        var s = Surface(native: true);
        Tap(s, Mid(Box(s, "pen").Rect));
        var p = (ColorPopup)s.TopPopup!;
        Tap(s, Mid(p.HexRect));
        Assert.Equal(DvEditKind.Text, s.Edit.Kind);
        Assert.Equal("#0000FF", s.Edit.InitialText);
        s.SetEditText(s.Edit.SessionId, "#12");
        Assert.False(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.Equal("#RRGGBB 형식이 아닙니다", s.Edit.Error);
        s.SetEditText(s.Edit.SessionId, "22c55e");
        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.Equal(new SKColor(0x22, 0xc5, 0x5e), p.State.Rgb);
    }

    [Fact]
    public void Popups_are_painted_with_keyboard_pan()
    {
        // 화면 아래쪽 상자 → 팝업이 위로 열림 → 시각 칸이 키패드에 가려지는 높이
        long now = 0;
        var s = new DvSurface("""
            <DvScreen style="padding:0"><DvDateTimeInput id="at" value="{Bind Data.At}" style="position:absolute; left:20; top:540"/></DvScreen>
            """, new Vm()) { UseNativeEditor = false, Clock = () => now };
        s.Layout(W, H);
        Tap(s, Mid(Box(s, "at").Rect));
        var p = (DateTimePopup)s.TopPopup!;
        Tap(s, Mid(p.FieldRect(0)));
        now = 10_000; // 키보드가 다 올라온 시각
        using var bmp = new SKBitmap((int)W, (int)H);
        using (var c = new SKCanvas(bmp))
            s.RenderTo(c, W, H);
        var pan = s.KeyboardPan;
        Assert.True(pan > 0);
        var panel = s.Keyboard!.Panel(W, H, now);
        var field = p.FieldRect(0);
        Assert.True(field.Bottom - pan <= panel.Top); // 올라간 칸이 키보드 위
        Assert.True(field.Top >= panel.Top);           // pan 이 없다면 칸은 키보드 아래에 가려진다

        // 편집 칸 테두리(primary 2px)는 올라간 위치에 그려지고, 원래 위치는 키보드 바탕
        static SKColor Tok(Devinno.UI.Styling.DvTheme t, string name)
            => t.TryGet(name, out var raw) && Devinno.UI.Styling.DvColor.TryParse(raw, out var c) ? c.ToSK() : SKColors.Transparent;
        var primary = Tok(Devinno.UI.Styling.DvTheme.Default, "primary");
        var x = (int)field.MidX;
        var top = (int)(field.Top - pan);
        Assert.Contains(Enumerable.Range(top - 2, 5), y => bmp.GetPixel(x, y) == primary);
        var kbd = Tok(Devinno.UI.Styling.DvTheme.Default, "kbd-bg"); // 서피스 테마 없음 → 키보드도 Default
        Assert.All(Enumerable.Range((int)field.Top - 2, 5), y => Assert.Equal(kbd, bmp.GetPixel(x, y)));
    }
}
