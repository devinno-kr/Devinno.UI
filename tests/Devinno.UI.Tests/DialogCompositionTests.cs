using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 8c-1 Task 4 — 다이얼로그 칸 합성(가운데·크기 제한), 막(scrim), 모달 입력, <DvDialogFrame> (spec §6.2·§6.3·§6.6)
public class DialogCompositionTests
{
    private const float W = 800, H = 600;

    // 창 전체를 덮는 페이지 버튼 — 다이얼로그 밖으로 새는 클릭은 모두 여기서 잡힌다
    private const string PageMarkup = """
        <DvScreen style="padding:0">
          <DvButton id="btn" text="P" onclick="Hit" style="position:absolute; left:0; top:0; width:800; height:600"/>
        </DvScreen>
        """;

    private static string ButtonDialog(float w, float h) => $"""
        <DvStack style="width:{w}; height:{h}; padding:0; gap:0; background:#ffffff">
          <DvButton id="btn" text="D" onclick="Hit" style="position:absolute; left:0; top:0; width:{w}; height:{h}"/>
        </DvStack>
        """;

    private const string InputDialog = """
        <DvStack style="width:300; height:200; padding:0; gap:0">
          <DvInput id="in" value="{Bind Data.Name}" style="width:200"/>
        </DvStack>
        """;

    private const string SelectDialog = """
        <DvStack style="width:300; height:200; padding:0; gap:0">
          <DvSelect id="sel" value="{Bind Data.Mode}" style="width:200">
            <DvOption value="a" text="에이"/>
            <DvOption value="b" text="비"/>
          </DvSelect>
        </DvStack>
        """;

    public sealed class Page(string markup) : DvPage
    {
        public int Clicks;
        public Action? OnHit;

        protected override string MarkupSource => markup;

        public void Hit()
        {
            Clicks++;
            OnHit?.Invoke();
        }
    }

    public sealed class Dlg : MarkupDialog<string>
    {
        public int Clicks;
        public string Name { get; set; } = "처음";
        public string Mode { get; set; } = "a";

        public Dlg() : base(ButtonDialog(300, 200)) { }

        public Dlg(string markup) : base(markup) { }

        public void Hit() => Clicks++;
    }

    private static (DvApp App, DvSurface Root, Page Page) Start(bool native = false)
    {
        var app = new DvApp();
        var page = new Page(PageMarkup);
        app.Pages.Add(page);
        var root = app.Surface;
        root.UseNativeEditor = native;
        root.Layout(W, H);
        return (app, root, page);
    }

    private static Dlg Show(DvApp app, DvSurface root, string? markup = null, float w = W, float h = H)
    {
        if (markup is null)
            app.ShowDialog<Dlg, string>();
        else
            app.ShowDialog<Dlg, string>(markup);
        root.Layout(w, h);
        return Assert.IsType<Dlg>(app.Dialogs.Top);
    }

    private static SKRect DialogRect(DvSurface root, Dlg d) => root.Slots.Single(s => ReferenceEquals(s.Child, d.Surface)).Rect;

    private static void Click(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        s.Layout(W, H);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static SKBitmap Render(DvSurface root)
    {
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        root.RenderTo(c, W, H);
        return bmp;
    }

    private static readonly SKColor Magenta = new(0xff, 0x00, 0xff);

    private static DvTheme MagentaScrim => DvTheme.Light.With(new Dictionary<string, string> { ["scrim"] = "#ff00ff" });

    // ── 배치 ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dialog_CenteredWithRootSize()
    {
        var (app, root, _) = Start();
        var d = Show(app, root);

        Assert.Equal(new SKRect(250, 200, 550, 400), DialogRect(root, d));
        Assert.True(root.Slots[^1].Modal);
        Assert.Same(d.Surface, root.Slots[^1].Child);
    }

    [Fact]
    public void Dialog_ClampedToWindowMinusMargin()
    {
        var (app, root, _) = Start();
        var d = Show(app, root, ButtonDialog(2000, 200));

        var r = DialogRect(root, d);
        Assert.Equal(768, r.Width);
        Assert.Equal(16, r.Left);
        Assert.Equal(200, r.Top);
    }

    [Fact]
    public void Dialog_RecentersOnResize()
    {
        var (app, root, _) = Start();
        var d = Show(app, root);

        root.Layout(1000, 600);

        Assert.Equal(350, DialogRect(root, d).Left);
    }

    // ── 입력 ─────────────────────────────────────────────────────────────

    [Fact]
    public void ClickInsideDialog_GoesToDialog_NotPage()
    {
        var (app, root, page) = Start();
        var d = Show(app, root);

        Click(root, 400, 300);

        Assert.Equal(1, d.Clicks);
        Assert.Equal(0, page.Clicks);
    }

    [Fact]
    public void ClickOutsideDialog_Swallowed()
    {
        var (app, root, page) = Start();
        var d = Show(app, root);

        Click(root, 100, 100);

        Assert.Equal(0, page.Clicks);
        Assert.Equal(0, d.Clicks);
        Assert.True(d.IsOpen);
        Assert.Single(app.Dialogs);
    }

    [Fact]
    public void DoubleTap_SecondTapBlocked()
    {
        var (app, root, page) = Start();
        page.OnHit = () => app.ShowDialog<Dlg, string>();

        root.PointerDown(new SKPoint(100, 100));
        root.PointerUp(new SKPoint(100, 100));
        root.PointerDown(new SKPoint(100, 100)); // 같은 프레임(Layout 없음) — 다이얼로그는 요청만 된 상태
        root.PointerUp(new SKPoint(100, 100));

        Assert.Equal(1, page.Clicks);
        Assert.Single(app.Dialogs);
    }

    [Fact]
    public void PressThenDialog_UpDoesNotClick()
    {
        var (app, root, page) = Start();

        root.PointerDown(new SKPoint(100, 100));
        app.ShowDialog<Dlg, string>();
        root.Layout(W, H);
        root.PointerUp(new SKPoint(100, 100));

        Assert.Equal(0, page.Clicks);
    }

    [Fact]
    public void LowerDialog_NoInput()
    {
        var (app, root, _) = Start();
        var d1 = Show(app, root);
        var d2 = Show(app, root, ButtonDialog(100, 50));
        Assert.NotSame(d1, d2);

        Click(root, 260, 210); // d1 안, d2(350..450 × 275..325) 밖

        Assert.Equal(0, d1.Clicks);
        Assert.Equal(0, d2.Clicks);

        Click(root, 400, 300); // d2 안
        Assert.Equal(1, d2.Clicks);
    }

    [Fact]
    public void LowerDialog_TopRequestedOnly_NoInput()
    {
        var (app, root, _) = Start();
        var d1 = Show(app, root);

        app.ShowDialog<Dlg, string>(ButtonDialog(100, 50)); // 요청만(적용 전) — 아래 d1 도 받지 않는다(두 번 탭)
        root.PointerDown(new SKPoint(260, 210));
        root.PointerUp(new SKPoint(260, 210));

        Assert.Equal(0, d1.Clicks);
    }

    [Fact]
    public void CloseRequested_BeforeApply_StillSwallows()
    {
        var (app, root, page) = Start();
        var d1 = Show(app, root);
        var d2 = Show(app, root, ButtonDialog(100, 50));

        d2.Cancel(); // 닫기 요청만(다음 프레임 적용) — d2 는 아직 칸에 그려져 있다
        root.PointerDown(new SKPoint(400, 300));
        root.PointerUp(new SKPoint(400, 300));
        Assert.Equal(0, d1.Clicks);
        Assert.Equal(0, d2.Clicks);

        d1.Cancel(); // 전부 닫기 요청 — 적용 전에는 페이지도 받지 않는다
        root.PointerDown(new SKPoint(100, 100));
        root.PointerUp(new SKPoint(100, 100));
        Assert.Equal(0, page.Clicks);

        root.Layout(W, H);
        Click(root, 100, 100);
        Assert.Equal(1, page.Clicks);
    }

    private sealed class Bar : DvBar
    {
        protected override string MarkupSource => """
            <DvStack style="height:40; padding:0; gap:0">
              <DvButton id="bb" text="B" style="position:absolute; left:0; top:0; width:800; height:40"/>
            </DvStack>
            """;
    }

    [Fact]
    public void DialogShown_ClearsHoverBelow()
    {
        var (app, root, page) = Start();
        var bar = new Bar();
        app.Top = bar;
        root.Layout(W, H);

        root.PointerMove(new SKPoint(100, 100)); // 페이지 버튼 위
        Assert.NotNull(page.Surface!.HoverBox);
        root.PointerMove(new SKPoint(100, 20)); // 상단 바 버튼 위 — 페이지 hover 는 꺼진다
        Assert.NotNull(bar.Surface!.HoverBox);

        var d1 = Show(app, root); // 포인터는 그대로 — 막 아래 바에 hover 가 남으면 안 된다
        Assert.Null(bar.Surface.HoverBox);
        Assert.Null(page.Surface.HoverBox);

        root.PointerMove(new SKPoint(260, 210)); // d1 안
        Assert.NotNull(d1.Surface!.HoverBox);
        Show(app, root, ButtonDialog(100, 50)); // d2 는 그 점을 덮지 않는다 — 아래 다이얼로그 hover 도 지운다
        Assert.Null(d1.Surface.HoverBox);
    }

    [Fact]
    public void DialogShown_PageHoverCleared_FromPageSlot()
    {
        var (app, root, page) = Start();
        root.PointerMove(new SKPoint(100, 100));
        Assert.NotNull(page.Surface!.HoverBox);

        Show(app, root);

        Assert.Null(page.Surface.HoverBox);
    }

    [Fact]
    public void WheelOutsideDialog_DoesNotScrollPage()
    {
        var app = new DvApp();
        var page = new Page("""
            <DvScreen style="padding:0">
              <DvBox id="sc" style="position:absolute; left:0; top:0; width:200; height:100; overflow:scroll; display:flex; flex-direction:column">
                <DvBox style="height:500"/>
              </DvBox>
            </DvScreen>
            """);
        app.Pages.Add(page);
        var root = app.Surface;
        root.Layout(W, H);
        LayoutBox Scroller() => Flatten(page.Surface!.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == "sc");

        root.PointerWheel(new SKPoint(100, 50), -1); // 대조 — 다이얼로그 없으면 스크롤된다
        root.Layout(W, H);
        Assert.Equal(48, Scroller().ScrollY, 3);

        Show(app, root);
        root.PointerWheel(new SKPoint(100, 50), -1); // 다이얼로그(250..550) 밖, 페이지 스크롤 상자 위
        root.Layout(W, H);
        Assert.Equal(48, Scroller().ScrollY, 3);
    }

    [Fact]
    public void SelectPopupInsideDialog_OnTop()
    {
        var (app, root, page) = Start();
        var d = Show(app, root, SelectDialog);
        var sel = Flatten(d.Surface!.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == "sel").Rect;
        var r = DialogRect(root, d);

        Click(root, r.Left + sel.MidX, r.Top + sel.MidY);

        var dd = root.Dropdown;
        Assert.NotNull(dd);
        Assert.Null(d.Surface.TopPopup); // 창(루트)이 띄운다

        var item = dd!.ItemRect(1);
        Click(root, item.MidX, item.MidY);

        Assert.Equal("b", d.Mode);
        Assert.Equal(0, page.Clicks);
    }

    [Fact]
    public void CloseDialogs_ClosesDialogPopup()
    {
        var (app, root, _) = Start();
        var d = Show(app, root, SelectDialog);
        var sel = Flatten(d.Surface!.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == "sel").Rect;
        var r = DialogRect(root, d);
        Click(root, r.Left + sel.MidX, r.Top + sel.MidY);
        Assert.NotNull(root.Dropdown);

        app.CloseDialogs();
        root.Layout(W, H);

        Assert.Null(root.TopPopup);
        Assert.DoesNotContain(root.Slots, s => s.Modal);
    }

    [Fact]
    public void CloseDialog_CancelsDialogEdit()
    {
        var (app, root, _) = Start(native: true);
        var d = Show(app, root, InputDialog);
        var r = DialogRect(root, d);

        Click(root, r.Left + 50, r.Top + 20);
        Assert.True(root.Edit.IsEditing);
        root.SetEditText(root.Edit.SessionId, "새 값");

        app.CloseDialogs();
        root.Layout(W, H);

        Assert.Equal("처음", d.Name);
        Assert.False(root.Edit.IsEditing);
    }

    [Fact]
    public async Task PageMarkupError_DialogCloseStillCompletes()
    {
        var (app, root, _) = Start();
        var task = app.ShowDialog<Dlg, string>();
        root.Layout(W, H);
        var d = Assert.IsType<Dlg>(app.Dialogs.Top);

        var bad = new Page("<DvScreen><broken</DvScreen>");
        app.Pages.Add(bad);
        app.Show(bad);
        d.Done("x");

        Assert.ThrowsAny<System.Xml.XmlException>(() => root.Layout(W, H));
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal("x", await task); // 이미 완료됨(위 단언)
        Assert.Empty(app.AppliedDialogs);
    }

    // ── 그리기 ───────────────────────────────────────────────────────────

    [Fact]
    public void Scrim_DrawnBelowTopDialogOnly()
    {
        var (app, root, _) = Start();
        app.Theme = MagentaScrim;
        root.Layout(W, H);
        Show(app, root);
        Show(app, root, ButtonDialog(100, 50));

        using var bmp = Render(root);

        Assert.Equal(Magenta, bmp.GetPixel(260, 210)); // d1 안, d2 밖 — 막이 d1 위
        Assert.Equal(Magenta, bmp.GetPixel(50, 50));   // 창 빈 곳
        Assert.NotEqual(Magenta, bmp.GetPixel(400, 300)); // d2 안
    }

    [Fact]
    public void NoDialog_RenderUnchanged()
    {
        var (app, root, _) = Start();
        app.Theme = MagentaScrim;
        root.Layout(W, H);

        using var bmp = Render(root);

        var magenta = 0;
        for (var y = 0; y < bmp.Height; y += 2)
            for (var x = 0; x < bmp.Width; x += 2)
                if (bmp.GetPixel(x, y) == Magenta)
                    magenta++;
        Assert.Equal(0, magenta);
    }

    [Fact]
    public void Dialog_MinWidth_PercentGrid_NoSlack()
    {
        // min-width:600 다이얼로그 안의 % grid — 높이 = 배치된 내용 높이(빈 공간 없음)
        var (app, root, _) = Start();
        var d = Show(app, root, """
            <DvStack style="min-width:600; padding:0; gap:0">
              <DvBox style="display:grid; grid-template-columns:50% 50%"><DvLabel text="Long text label that is about three hundred px wide ok"/><DvLabel text="B"/></DvBox>
              <DvLabel id="after" text="after"/>
            </DvStack>
            """);
        var rect = DialogRect(root, d);
        var dlgRoot = d.Surface!.Root!;
        Assert.Equal(600, rect.Width, 2);
        var grid = dlgRoot.Children[0];
        Assert.Equal(grid.Children[0].Rect.Bottom, grid.Rect.Bottom, 2);
        Assert.Equal(dlgRoot.Children[1].Rect.Bottom, rect.Height, 2);
    }

    [Fact]
    public void DialogFrame_WithFrGrid_OpensAtContentWidth()
    {
        // 짧은 글자의 1fr 1fr grid — 창 폭 − 32 가 아니라 내용 폭으로 열린다
        var (app, root, _) = Start();
        var d = Show(app, root, """
            <DvDialogFrame title="T">
              <DvBox style="display:grid; grid-template-columns:1fr 1fr"><DvLabel text="A"/><DvLabel text="B"/></DvBox>
            </DvDialogFrame>
            """);
        var rect = DialogRect(root, d);
        Assert.True(rect.Width < 200, $"dialog width {rect.Width}");
        Assert.Equal(d.Surface!.Root!.Rect.Width, rect.Width, 2);
        Assert.Equal((W - rect.Width) / 2, rect.Left, 2);
    }

    [Fact]
    public void DialogFrame_TitleRendered()
    {
        var (app, root, _) = Start();
        var d = Show(app, root, """
            <DvDialogFrame title="제목" style="width:300">
              <DvLabel text="본문"/>
            </DvDialogFrame>
            """);

        var boxes = Flatten(d.Surface!.Root!).ToList();
        Assert.Contains(boxes, b => b.Node.Tag == "DvLabel" && b.ResolvedText == "제목");
        Assert.Contains(boxes, b => b.Node.Tag == "DvLabel" && b.ResolvedText == "본문");
        Assert.Equal(300, DialogRect(root, d).Width);
    }
}
