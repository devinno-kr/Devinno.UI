using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// P8a-2 편집 세션 코어 — 누르면 시작, DvEditState 필드·SessionId·Version
public class EditSessionTests
{
    public sealed class Vm
    {
        public string Name { get; set; } = "펌프 A";
        public string Other { get; set; } = "B";
        public string Password { get; set; } = "secret";
        public double SetPoint { get; set; } = 12.5;
    }

    // 세로로 쌓인 입력 3개: a(0~40), b(40~80), c(80~120) — padding 0, gap 0
    private const string Three = """
        <DvScreen style="padding:0; gap:0">
          <DvInput id="a" value="{Bind Data.Name}" placeholder="이름"/>
          <DvInput id="b" value="{Bind Data.Other}"/>
          <DvInput id="c" value="{Data.Name}"/>
        </DvScreen>
        """;

    private static DvSurface Surface(string markup, object vm, DvTheme? theme = null)
    {
        var s = new DvSurface(markup, vm, theme) { UseNativeEditor = true }; // 가상 키보드(모달) 없이 세션만
        s.Layout(400, 300);
        return s;
    }

    private static void Click(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        s.Layout(400, 300);
    }

    private static LayoutBox Box(DvSurface s, string id)
        => Flatten(s.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Not_editing_initially()
    {
        var s = Surface(Three, new Vm());
        Assert.False(s.Edit.IsEditing);
        Assert.Equal(0, s.Edit.SessionId);
    }

    [Fact]
    public void Click_two_way_input_starts_session()
    {
        var s = Surface(Three, new Vm(), DvTheme.Light);
        Click(s, 50, 20);

        var e = s.Edit;
        Assert.True(e.IsEditing);
        Assert.Equal(1, e.SessionId);
        Assert.Equal(DvEditKind.Text, e.Kind);
        Assert.Equal("펌프 A", e.InitialText);
        Assert.Equal("이름", e.Placeholder);
        Assert.Equal(Box(s, "a").Rect, e.Rect);
        Assert.Equal(Box(s, "a").Rect, e.Clip);
        Assert.Equal(14f, e.FontSize);
        Assert.Equal(SKColors.White, e.Background); // Light surface
        Assert.Equal(new SKRect(12, 8, 12, 8), e.Padding);
        Assert.False(string.IsNullOrEmpty(e.FontFamily));
        Assert.True(Box(s, "a").Editing);
    }

    [Fact]
    public void One_way_and_disabled_inputs_do_not_start()
    {
        var s = Surface(Three, new Vm());
        Click(s, 50, 100); // c = 단방향
        Assert.False(s.Edit.IsEditing);

        var d = Surface("<DvScreen style=\"padding:0\"><DvInput value=\"{Bind Data.Name}\" disabled=\"true\"/></DvScreen>", new Vm());
        Click(d, 50, 20);
        Assert.False(d.Edit.IsEditing);
    }

    [Fact]
    public void Clicking_another_input_switches_session()
    {
        var s = Surface(Three, new Vm());
        Click(s, 50, 20);
        Click(s, 50, 60);

        Assert.True(s.Edit.IsEditing);
        Assert.Equal(2, s.Edit.SessionId);
        Assert.Equal("B", s.Edit.InitialText);
        Assert.Equal(Box(s, "b").Rect, s.Edit.Rect);
        Assert.False(Box(s, "a").Editing);
        Assert.True(Box(s, "b").Editing);
    }

    [Fact]
    public void Clicking_the_same_input_keeps_session()
    {
        var s = Surface(Three, new Vm());
        Click(s, 50, 20);
        var v = s.Edit.Version;
        Click(s, 60, 20);
        Assert.Equal(1, s.Edit.SessionId);
        Assert.Equal(v, s.Edit.Version);
    }

    [Fact]
    public void Clicking_empty_space_ends_session()
    {
        var s = Surface(Three, new Vm());
        Click(s, 50, 20);
        Click(s, 300, 250);
        Assert.False(s.Edit.IsEditing);
        Assert.False(Box(s, "a").Editing);
    }

    [Fact]
    public void Password_starts_empty_and_numeric_starts_raw()
    {
        var s = Surface("""
            <DvScreen style="padding:0; gap:0">
              <DvInput value="{Bind Data.Password}" keyboard-type="password"/>
              <DvInput value="{Bind Data.SetPoint}" keyboard-type="numeric" format="0.000"/>
            </DvScreen>
            """, new Vm());
        Click(s, 50, 20);
        Assert.Equal(DvEditKind.Password, s.Edit.Kind);
        Assert.Equal("", s.Edit.InitialText);

        Click(s, 50, 60);
        Assert.Equal(DvEditKind.Numeric, s.Edit.Kind);
        Assert.Equal("12.5", s.Edit.InitialText);
    }

    [Fact]
    public void Scrolling_moves_rect_clips_and_bumps_version()
    {
        var s = new DvSurface("""
            <DvScreen style="padding:0">
              <DvStack style="height:100; overflow:scroll; gap:0; padding:0">
                <DvInput value="{Bind Data.Name}"/>
                <DvInput value="{Bind Data.Other}"/>
                <DvInput value="{Bind Data.Name}"/>
                <DvInput value="{Bind Data.Other}"/>
              </DvStack>
            </DvScreen>
            """, new Vm());
        s.Layout(400, 300);
        Click(s, 50, 60); // 두 번째 입력(40~80)
        var before = s.Edit.Rect;
        var v = s.Edit.Version;

        Assert.Equal(s.Edit.Rect, s.Edit.Clip); // 전부 보임

        s.PointerWheel(new SKPoint(50, 50), -1f); // 48 아래로 → 상자 40~80 이 -8~32
        s.RenderTo(new SKCanvas(new SKBitmap(400, 300)), 400, 300);

        Assert.Equal(before.Top - 48, s.Edit.Rect.Top);
        Assert.True(s.Edit.Version > v);
        var r = s.Edit.Rect;
        Assert.Equal(new SKRect(r.Left, 0, r.Right, 32), s.Edit.Clip); // 스크롤 상자(0~100) 밖으로 나간 위쪽 8 은 잘림
    }

    [Fact]
    public void Edit_target_is_box_adapter_for_dvinput()
    {
        var vm = new Vm();
        var s = new DvSurface("""<DvScreen><DvInput id="a" value="{Bind Data.Name}"/></DvScreen>""", vm) { UseNativeEditor = true };
        s.Layout(400, 300);
        var box = s.Root!.Children[0];
        s.PointerDown(new SKPoint(box.Rect.MidX, box.Rect.MidY));
        s.PointerUp(new SKPoint(box.Rect.MidX, box.Rect.MidY));
        var t = Assert.IsType<BoxEditTarget>(s.EditTarget);
        Assert.Same(box, t.Box);
    }

    [Fact]
    public void Box_target_answers_same_hits_and_remap()
    {
        var s = Surface(Three, new Vm());
        var a = Box(s, "a");
        var b = Box(s, "b");
        var t = new BoxEditTarget(s, a);
        Assert.True(t.SameAs(new BoxEditTarget(s, a)));
        Assert.False(t.SameAs(new BoxEditTarget(s, b)));
        Assert.True(t.Hits(a, new SKPoint(a.Rect.MidX, a.Rect.MidY)));
        Assert.False(t.Hits(b, new SKPoint(b.Rect.MidX, b.Rect.MidY)));

        var old = s.Root!;
        s.Invalidate();
        s.Layout(400, 300);
        Assert.True(t.Remap(old, s.Root!));
        Assert.Same(Box(s, "a"), t.Box);
        Assert.True(t.Box.Editing);
    }
}
