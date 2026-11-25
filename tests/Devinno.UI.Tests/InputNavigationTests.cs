using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 입력 칸: Tab·Shift+Tab 다음·이전 칸(전체 선택), select-on-focus, escape="commit", 여러 줄 칸 휠 스크롤
public class InputNavigationTests
{
    private const float W = 400, H = 400;

    public sealed class Vm
    {
        public string A { get; set; } = "aa";
        public string B { get; set; } = "bb";
        public string C { get; set; } = "cc";
        public double N { get; set; } = 320;
        public string Long { get; set; } = string.Join("\n", Enumerable.Range(1, 30).Select(i => "line " + i));
        public string Short { get; set; } = "one line";
    }

    private const string Three = """
        <DvInput id="a" value="{Bind A}" style="height:40"/>
        <DvInput id="r" value="{Bind B}" readonly="true" style="height:40"/>
        <DvInput id="b" value="{Bind B}" style="height:40"/>
        <DvInput id="c" value="{Bind C}" style="height:40"/>
        """;

    private static (DvSurface S, Vm Vm) Start(string body, bool native = true)
    {
        var vm = new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:0; gap:0\">{body}</DvScreen>", vm) { UseNativeEditor = native };
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static LayoutBox Box(DvSurface s, string id) => Flatten(s.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);

    private static void Tap(DvSurface s, string id)
    {
        var b = Box(s, id);
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static string? Editing(DvSurface s) => Flatten(s.Root!).FirstOrDefault(b => b.Editing)?.Node.Attributes.GetValueOrDefault("id");

    [Fact]
    public void Tab_commits_and_edits_the_next_input_with_the_text_selected()
    {
        var (s, vm) = Start(Three);
        Tap(s, "a");
        Assert.False(s.Edit.SelectAll); // 눌러서 시작 = 커서 끝
        s.SetEditText(s.Edit.SessionId, "A2");
        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.Tab));
        s.Layout(W, H);
        Assert.Equal("A2", vm.A);
        Assert.True(s.Edit.IsEditing);
        Assert.Equal("b", Editing(s)); // 읽기 전용 칸은 건너뛴다
        Assert.Equal("bb", s.Edit.InitialText);
        Assert.True(s.Edit.SelectAll);

        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.ShiftTab));
        s.Layout(W, H);
        Assert.Equal("a", Editing(s));
        Assert.Equal("A2", s.Edit.InitialText);
    }

    [Fact]
    public void Tab_on_the_last_input_only_commits()
    {
        var (s, vm) = Start(Three);
        Tap(s, "c");
        s.SetEditText(s.Edit.SessionId, "C2");
        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.Tab));
        s.Layout(W, H);
        Assert.Equal("C2", vm.C);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Tab_moves_with_the_virtual_keyboard_too()
    {
        var (s, vm) = Start(Three, native: false);
        Tap(s, "a");
        s.KeyInput(DvKey.Tab);
        s.Layout(W, H);
        Assert.Equal("b", Editing(s));
        Assert.True(s.Edit.SelectAll);
        s.TextInput("x"); // 선택된 글 전체를 바꾼다
        s.KeyInput(DvKey.Enter);
        s.Layout(W, H);
        Assert.Equal("x", vm.B);
    }

    [Fact]
    public void Select_on_focus_selects_when_pressed()
    {
        var (s, vm) = Start("""<DvInput id="n" value="{Bind N}" keyboard-type="numeric" select-on-focus="true" style="height:40"/>""", native: false);
        Tap(s, "n");
        Assert.True(s.Edit.SelectAll);
        Assert.Equal("320", s.Edit.InitialText);
        s.TextInput("976");
        s.KeyInput(DvKey.Enter);
        Assert.Equal(976, vm.N); // "320976" 이 아니다
    }

    [Fact]
    public void Backspace_on_a_selected_text_clears_it()
    {
        var (s, vm) = Start("""<DvInput id="a" value="{Bind A}" select-on-focus="true" style="height:40"/>""", native: false);
        Tap(s, "a");
        s.KeyInput(DvKey.Backspace);
        s.TextInput("z");
        s.KeyInput(DvKey.Enter);
        Assert.Equal("z", vm.A);
    }

    [Fact]
    public void Escape_cancels_by_default()
    {
        var (s, vm) = Start("""<DvInput id="a" value="{Bind A}" multiline="true"/>""");
        Tap(s, "a");
        s.SetEditText(s.Edit.SessionId, "typed");
        s.EscapeEdit(s.Edit.SessionId);
        Assert.False(s.Edit.IsEditing);
        Assert.Equal("aa", vm.A);
    }

    [Fact]
    public void Escape_commit_keeps_the_typed_text()
    {
        var (s, vm) = Start("""<DvInput id="a" value="{Bind A}" multiline="true" escape="commit"/>""");
        Tap(s, "a");
        s.SetEditText(s.Edit.SessionId, "typed");
        s.EscapeEdit(s.Edit.SessionId);
        Assert.False(s.Edit.IsEditing);
        Assert.Equal("typed", vm.A);
    }

    [Fact]
    public void Escape_key_on_the_virtual_keyboard_follows_the_attribute()
    {
        var (s, vm) = Start("""<DvInput id="a" value="{Bind A}" escape="commit" style="height:40"/>""", native: false);
        Tap(s, "a");
        s.TextInput("x");
        s.KeyInput(DvKey.Escape);
        Assert.Equal("aax", vm.A);
    }

    [Fact]
    public void Wheel_scrolls_a_long_multiline_input_and_survives_a_rebuild()
    {
        var (s, _) = Start("""<DvInput id="m" value="{Bind Long}" multiline="true" style="height:96"/><DvInput id="k" value="{Bind Short}" multiline="true" style="height:96"/>""");
        var m = Box(s, "m");
        s.PointerWheel(new SKPoint(m.Rect.MidX, m.Rect.MidY), -1f);
        Assert.True(Box(s, "m").InputScrollY > 0);
        var y = Box(s, "m").InputScrollY;
        s.Invalidate();
        s.Layout(W, H);
        Assert.Equal(y, Box(s, "m").InputScrollY);
        s.PointerWheel(new SKPoint(m.Rect.MidX, m.Rect.MidY), 100f); // 맨 위에서 멈춘다
        Assert.Equal(0, Box(s, "m").InputScrollY);

        var k = Box(s, "k");
        s.PointerWheel(new SKPoint(k.Rect.MidX, k.Rect.MidY), -1f); // 넘치지 않는 칸은 그대로
        Assert.Equal(0, Box(s, "k").InputScrollY);
    }

    [Fact]
    public void Scrolled_multiline_text_is_drawn_from_the_offset()
    {
        var (s, _) = Start("""<DvInput id="m" value="{Bind Long}" multiline="true" style="height:96"/>""");
        string Top()
        {
            using var bmp = new SKBitmap((int)W, (int)H);
            using (var c = new SKCanvas(bmp))
                s.RenderTo(c, W, H);
            // 첫 줄 자리(칸 위쪽 안)의 글자 잉크 픽셀 수 — 내리면 다른 줄이 그 자리에 온다
            var m = Box(s, "m");
            var ink = 0;
            for (var x = (int)m.Rect.Left + 4; x < (int)m.Rect.Right - 4; x++)
                for (var yy = (int)m.Rect.Top + 4; yy < (int)m.Rect.Top + 30; yy++)
                    if (bmp.GetPixel(x, yy).Red < 128)
                        ink++;
            return ink.ToString();
        }
        var before = Top();
        var m = Box(s, "m");
        s.PointerWheel(new SKPoint(m.Rect.MidX, m.Rect.MidY), -3f);
        Assert.NotEqual(before, Top());
    }
}
