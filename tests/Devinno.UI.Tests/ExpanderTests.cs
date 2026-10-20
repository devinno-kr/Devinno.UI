using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvExpander — 제목 줄(▶/▼ + header)을 누르면 내용이 펼쳐지고 접힌다. 접으면 제목 줄 높이만, 내용은 만들지 않는다.
public class ExpanderTests
{
    private const float W = 600, H = 800;

    // 아코디언 패턴(문서 예제) — 항목의 Open setter 가 같은 목록의 나머지를 닫는다
    public sealed class Section(string title, List<Section> all)
    {
        private bool _open;
        public string Title { get; } = title;
        public bool Open
        {
            get => _open;
            set
            {
                _open = value;
                if (value)
                    foreach (var x in all.Where(x => x != this))
                        x._open = false;
            }
        }
    }

    public sealed class Vm : IDvControlHost
    {
        public DvExpander? exp;
        public bool Open { get; set; }
        public List<Section> Sections { get; } = new();
        public Vm() => Sections.AddRange(new[] { "펌프", "밸브", "탱크" }.Select(t => new Section(t, Sections)));
        public void Attach(DvControlLookup c) => exp = c.Bind(exp, "exp");
    }

    private static (DvSurface S, Vm Vm) Start(string body, Vm? vm = null)
    {
        vm ??= new Vm();
        var s = new DvSurface($"<DvScreen style=\"padding:20; gap:8\">{body}</DvScreen>", vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static List<LayoutBox> All(DvSurface s, string tag) => Flatten(s.Root!).Where(b => b.Node.Tag == tag).ToList();
    private static LayoutBox Header(DvSurface s, int i = 0) => All(s, "DvExpanderHeader")[i];

    private static void Tap(DvSurface s, LayoutBox b)
    {
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
        Settle(s);
    }

    // 펼침 움직임(0.2초)을 끝까지 — 시작 프레임, 끝난 뒤 프레임, 다시 만든 화면
    private static long _clock = 10_000;

    private static void Settle(DvSurface s)
    {
        using var bmp = new SKBitmap((int)W, (int)H);
        using (var c = new SKCanvas(bmp))
        {
            s.RenderTo(c, W, H, _clock);
            s.RenderTo(c, W, H, _clock += 300);
        }
        _clock += 1000;
        s.Layout(W, H);
    }

    private const string Body = "<DvLabel id=\"inner\" text=\"내용\"/><DvButton text=\"버튼\" variant=\"primary\"/>";

    [Fact]
    public void Collapsed_ByDefault_HeaderOnly_TapExpands_ScreenRemembers()
    {
        var (s, _) = Start($"<DvExpander header=\"펌프 설정\">{Body}</DvExpander>");
        var e = All(s, "DvExpander")[0];
        Assert.Equal("펌프 설정", Header(s).ResolvedText);
        Assert.Equal("fa-chevron-right", Header(s).Icon);
        Assert.Empty(All(s, "DvLabel"));                // 접으면 내용을 만들지 않음
        Assert.InRange(e.Rect.Height, 44, 47);           // 제목 줄 + 테두리

        Tap(s, Header(s));
        Assert.Single(All(s, "DvLabel"));
        Assert.Equal("fa-chevron-down", Header(s).Icon);
        Assert.True(All(s, "DvExpander")[0].Rect.Height > 90);

        s.Invalidate();
        s.Layout(W, H);
        Assert.Single(All(s, "DvLabel"));                // 다시 만들어도 유지
        Tap(s, Header(s));
        Assert.Empty(All(s, "DvLabel"));
    }

    [Fact]
    public void Bound_WritesVm()
    {
        var (s, vm) = Start($"<DvExpander header=\"A\" expanded=\"{{Bind Open}}\">{Body}</DvExpander>");
        Tap(s, Header(s));
        Assert.True(vm.Open);
        vm.Open = false;
        s.Invalidate();
        s.Layout(W, H);
        Settle(s);
        Assert.Empty(All(s, "DvLabel"));
    }

    [Fact]
    public void Literal_Expanded_IsFixed_AndDisabled_DoesNotToggle()
    {
        var (s, _) = Start($"<DvExpander header=\"A\" expanded=\"true\">{Body}</DvExpander><DvExpander header=\"B\" disabled=\"true\">{Body}</DvExpander>");
        Tap(s, Header(s, 0));
        Tap(s, Header(s, 1));
        Assert.Single(All(s, "DvLabel")); // A 는 펼친 채 고정, B 는 비활성이라 접힌 채
    }

    [Fact]
    public void Handle_IsExpanded_ScreenState_And_Event()
    {
        var (s, vm) = Start($"<DvExpander id=\"exp\" header=\"A\">{Body}</DvExpander>");
        var ev = new List<bool>();
        vm.exp!.ExpandedChanged += (_, e) => ev.Add(e.ByUser);
        Assert.False(vm.exp.IsExpanded);
        vm.exp.IsExpanded = true;
        s.Layout(W, H);
        Assert.Single(All(s, "DvLabel"));
        Tap(s, Header(s));
        Assert.False(vm.exp.IsExpanded);
        Assert.Equal(new[] { false, true }, ev);
    }

    [Fact]
    public void Repeat_Accordion_Pattern()
    {
        var (s, vm) = Start(
            "<DvRepeat items=\"{Sections}\" gap=\"sm\">" +
            "  <DvExpander header=\"{Item.Title}\" expanded=\"{Bind Item.Open}\"><DvLabel text=\"{Item.Title} 내용\"/></DvExpander>" +
            "</DvRepeat>");
        Tap(s, Header(s, 0));
        Assert.Equal(new[] { true, false, false }, vm.Sections.Select(x => x.Open));
        Tap(s, Header(s, 2));
        Assert.Equal(new[] { false, false, true }, vm.Sections.Select(x => x.Open));
        Assert.Equal(new[] { "fa-chevron-right", "fa-chevron-right", "fa-chevron-down" }, All(s, "DvExpanderHeader").Select(h => h.Icon));
    }

    [Fact]
    public void Paints_Without_Throwing()
    {
        var (s, _) = Start($"<DvExpander header=\"A\" expanded=\"true\">{Body}</DvExpander>");
        using var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
    }

    [Fact]
    public void Opening_and_closing_move_the_height_over_200ms_and_push_what_is_below()
    {
        var (s, _) = Start($"<DvExpander header=\"펌프\">{Body}</DvExpander><DvLabel id=\"below\" text=\"아래\"/>");
        LayoutBox Below() => All(s, "DvLabel").Last();
        var closedTop = Below().Rect.Top;
        using var bmp = new SKBitmap((int)W, (int)H);
        float Frame(long t)
        {
            using (var c = new SKCanvas(bmp))
                s.RenderTo(c, W, H, t);
            return All(s, "DvExpander")[0].Rect.Height;
        }

        Frame(1000);
        var h = Header(s);
        s.PointerDown(new SKPoint(h.Rect.MidX, h.Rect.MidY));
        s.PointerUp(new SKPoint(h.Rect.MidX, h.Rect.MidY));
        var start = Frame(2000);
        var mid = Frame(2100);
        var end = Frame(2250);
        Frame(2300);
        var full = All(s, "DvExpander")[0].Rect.Height;
        Assert.InRange(start, 44, 48);                    // 시작은 제목 줄 높이
        Assert.InRange(mid, start + 10, full - 5);        // 중간은 사이
        Assert.Equal(full, end, 1);
        Assert.True(Below().Rect.Top > closedTop + 40);   // 아래 내용이 밀려 내려감

        // 접기 — 내용은 움직이는 동안 남아 있다가 끝나면 빠짐
        h = Header(s);
        s.PointerDown(new SKPoint(h.Rect.MidX, h.Rect.MidY));
        s.PointerUp(new SKPoint(h.Rect.MidX, h.Rect.MidY));
        Frame(3000);
        var closing = Frame(3100);
        Assert.InRange(closing, 50, full - 5);
        Assert.NotEmpty(All(s, "DvLabel").Where(b => b.ResolvedText == "내용"));
        Frame(3300);
        Frame(3320);
        Assert.Empty(All(s, "DvLabel").Where(b => b.ResolvedText == "내용"));
        Assert.Equal(closedTop, Below().Rect.Top, 1);
        Assert.Null(s.NextAnimationDue(3320));
    }
}
