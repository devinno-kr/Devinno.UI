using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// Task 3 — 고정 영역(Top·Bottom·Left·Right)과 현재 페이지를 DvApp 의 루트 화면 하나로 합성(칸·입력·팝업·편집·깨우기)
public class AppCompositionTests
{
    private const float W = 800, H = 600;

    // 마크업을 직접 받는 테스트 페이지(생성기 대신 MarkupSource·Attach 를 손으로)
    private class Page(string markup) : DvPage, IDvControlHost
    {
        public DvButton? Btn;
        public string Name { get; set; } = "처음";
        public int Clicks;
        public Exception? Throw;
        public Action<DvApp>? Shown;

        protected override string MarkupSource => markup;

        protected override void OnShown() => Shown?.Invoke(App);

        void IDvControlHost.Attach(DvControlLookup controls) => Btn = controls.Bind(Btn, "btn");

        public void Hit()
        {
            Clicks++;
            if (Throw is { } ex) throw ex;
        }
    }

    private sealed class PageB(string markup) : Page(markup);

    private class Bar(string markup) : DvBar, IDvControlHost
    {
        public DvButton? Btn;
        public int Count { get; set; } = 1;
        public int Clicks;
        public Action<DvApp>? OnHit;

        protected override string MarkupSource => markup;

        void IDvControlHost.Attach(DvControlLookup controls) => Btn = controls.Bind(Btn, "btn");

        public void Hit()
        {
            Clicks++;
            OnHit?.Invoke(App);
        }
    }

    // 창 전체를 덮는 페이지 버튼(어느 칸에서 새는 클릭이든 잡히게) + 왼쪽 위 작은 입력
    private const string PageButton = """
        <DvScreen style="padding:0">
          <DvButton id="btn" text="P" onclick="Hit" style="position:absolute; left:100; top:50; width:80; height:40"/>
        </DvScreen>
        """;

    private const string PageInput = """
        <DvScreen style="padding:0; gap:0">
          <DvInput id="in" value="{Bind Data.Name}" style="width:200"/>
        </DvScreen>
        """;

    private static string BarMarkup(string size, string extra = "") => $"""
        <DvStack style="{size}; padding:0; gap:0">
          <DvButton id="btn" text="B" onclick="Hit" style="position:absolute; left:10; top:10; width:50; height:30"/>
          {extra}
        </DvStack>
        """;

    private static void Click(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        s.Layout(W, H);
    }

    private static void OffThread(Action a)
    {
        var t = new Thread(() => a());
        t.Start();
        t.Join();
    }

    private static LayoutBox Box(DvSurface s, string id)
        => Flatten(s.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    // ── 배치 ─────────────────────────────────────────────────────────────

    [Fact]
    public void PageOnly_FillsWindow_BoundsWindowRelative()
    {
        var app = new DvApp();
        var a = new Page(PageButton);
        app.Pages.Add(a);
        var root = app.Surface;
        root.Layout(W, H);

        Assert.Same(a, app.CurrentPage);
        Assert.Equal(SKRect.Create(0, 0, W, H), a.Surface!.Root!.Rect);
        Assert.Equal(new SKPoint(0, 0), a.Surface.WindowOffset);
        Assert.Equal(SKRect.Create(100, 50, 80, 40), a.Btn!.Bounds);

        Click(root, 140, 70);
        Assert.Equal(1, a.Clicks);
    }

    [Fact]
    public void Regions_Layout()
    {
        var app = new DvApp();
        var page = new Page(PageButton);
        app.Pages.Add(page);
        var top = new Bar(BarMarkup("height:60"));
        var left = new Bar(BarMarkup("width:200"));
        app.Top = top;
        app.Left = left;
        var root = app.Surface;
        root.Layout(W, H);

        // 페이지 칸 = (200,60)-(W,H)
        Assert.Equal(new SKPoint(200, 60), page.Surface!.WindowOffset);
        Assert.Equal(SKRect.Create(0, 0, W - 200, H - 60), page.Surface.Root!.Rect);
        Assert.Equal(SKRect.Create(0, 0, W, 60), top.Surface!.Root!.Rect);
        Assert.Equal(new SKPoint(0, 60), left.Surface!.WindowOffset);
        Assert.Equal(SKRect.Create(0, 0, 200, H - 60), left.Surface.Root!.Rect);

        // 영역·페이지 버튼 Bounds = 창 기준
        Assert.Equal(SKRect.Create(10, 10, 50, 30), top.Btn!.Bounds);
        Assert.Equal(SKRect.Create(10, 70, 50, 30), left.Btn!.Bounds);
        Assert.Equal(SKRect.Create(300, 110, 80, 40), page.Btn!.Bounds);

        // 아래·오른쪽까지 — Top·Bottom 은 창 폭 전체, Left·Right 는 그 사이 높이
        var bottom = new Bar(BarMarkup("height:40"));
        var right = new Bar(BarMarkup("width:100"));
        app.Bottom = bottom;
        app.Right = right;
        root.Layout(W, H);

        Assert.Equal(new SKPoint(200, 60), page.Surface.WindowOffset);
        Assert.Equal(SKRect.Create(0, 0, 500, 500), page.Surface.Root!.Rect);
        Assert.Equal(new SKPoint(0, 560), bottom.Surface!.WindowOffset);
        Assert.Equal(SKRect.Create(0, 0, W, 40), bottom.Surface.Root!.Rect);
        Assert.Equal(new SKPoint(700, 60), right.Surface!.WindowOffset);
        Assert.Equal(SKRect.Create(0, 0, 100, 500), right.Surface.Root!.Rect);
        Assert.Equal(SKRect.Create(0, 0, 200, 500), left.Surface.Root!.Rect);

        // 크기를 지정하지 않은 영역 = 내용 크기
        var auto = new Bar("""<DvStack style="padding:0; gap:0"><DvStack style="height:33; width:44"/></DvStack>""");
        app.Bottom = auto;
        app.Right = null;
        root.Layout(W, H);
        Assert.Equal(new SKPoint(0, H - 33), auto.Surface!.WindowOffset);
        Assert.Equal(SKRect.Create(0, 0, 600, H - 60 - 33), page.Surface.Root!.Rect);
    }

    [Fact]
    public void TopBottomBars_LongLabel_NarrowWindow_HeightIncludesWrappedLines()
    {
        // 창 폭(200)에서 줄바꿈된 라벨 — 영역 높이 = 배치된 내용 높이, 페이지는 그 아래부터
        const string text = "The quick brown fox jumps over the lazy dog again and again until the line is long";
        const float w = 200;
        var app = new DvApp();
        var page = new Page(PageButton);
        app.Pages.Add(page);
        var top = new Bar($"""<DvStack style="padding:0; gap:0"><DvLabel id="t" text="{text}"/></DvStack>""");
        var bottom = new Bar($"""<DvStack style="padding:0; gap:0"><DvLabel id="b" text="{text}"/></DvStack>""");
        app.Top = top;
        app.Bottom = bottom;
        var root = app.Surface;
        root.Layout(w, H);

        var label = Box(top.Surface!, "t");
        Assert.True(label.Rect.Height > 30); // 여러 줄
        Assert.Equal(label.Rect.Bottom, top.Surface!.Root!.Rect.Height, 2);
        Assert.Equal(top.Surface.Root.Rect.Height, page.Surface!.WindowOffset.Y, 2);

        var bl = Box(bottom.Surface!, "b");
        Assert.Equal(bl.Rect.Bottom, bottom.Surface!.Root!.Rect.Height, 2);
        Assert.Equal(H - bottom.Surface.Root.Rect.Height, bottom.Surface.WindowOffset.Y, 2);
        Assert.Equal(H - top.Surface.Root.Rect.Height - bottom.Surface.Root.Rect.Height, page.Surface.Root!.Rect.Height, 2);
    }

    [Fact]
    public void Click_InLeftBar_ShowsPage()
    {
        var app = new DvApp();
        var a = new Page(PageButton);
        var b = new PageB(PageButton);
        app.Pages.Add(a);
        app.Pages.Add(b);
        var left = new Bar(BarMarkup("width:200")) { OnHit = x => x.Show<PageB>() };
        app.Left = left;
        var root = app.Surface;
        root.Layout(W, H);
        Assert.Same(a, app.CurrentPage);

        Click(root, 30, 25); // 왼쪽 영역 버튼 → Show<PageB>() 요청 → 다음 Layout 에 적용
        Assert.Equal(1, left.Clicks);
        Assert.Same(b, app.CurrentPage);
        Assert.Equal(new SKPoint(200, 0), b.Surface!.WindowOffset);
        Assert.Equal(0, a.Clicks);

        Click(root, 340, 70); // 이제 페이지 칸의 입력은 B 가 받는다
        Assert.Equal(1, b.Clicks);
        Assert.Equal(0, a.Clicks);
    }

    [Fact]
    public void Pointer_Capture_StaysInPressedSlot()
    {
        var app = new DvApp();
        var page = new Page("""
            <DvScreen style="padding:0; gap:0">
              <DvStack id="sc" style="overflow:scroll; width:800; height:540; padding:0"><DvStack style="height:2000"/></DvStack>
            </DvScreen>
            """);
        app.Pages.Add(page);
        var top = new Bar(BarMarkup("height:60"));
        app.Top = top;
        var root = app.Surface;
        root.Layout(W, H);

        // 페이지에서 눌러 위쪽 영역까지 끌고 가 뗌 — 끝까지 페이지가 받는다(스크롤이 영역 안 이동·뗌까지 따라감)
        root.PointerDown(new SKPoint(400, 300));
        root.PointerMove(new SKPoint(400, 250));
        root.PointerMove(new SKPoint(400, 150));
        root.PointerMove(new SKPoint(400, 20)); // 위쪽 영역 위
        root.PointerUp(new SKPoint(30, 25));    // 위쪽 영역 버튼 위에서 뗌
        root.Layout(W, H);

        Assert.Equal(0, top.Clicks);
        Assert.True(Box(page.Surface!, "sc").ScrollY > 240, $"ScrollY={Box(page.Surface!, "sc").ScrollY}");

        // 뗀 뒤에는 잡힘이 풀린다 — 위쪽 영역 버튼이 정상 클릭
        Click(root, 30, 25);
        Assert.Equal(1, top.Clicks);
    }

    // ── 팝업·편집 ────────────────────────────────────────────────────────

    [Fact]
    public void TopBar_Select_Popup_OverPage_Clickable()
    {
        var app = new DvApp();
        var page = new Page("""
            <DvScreen style="padding:0">
              <DvButton id="btn" text="P" onclick="Hit" style="position:absolute; left:0; top:0; width:800; height:560"/>
            </DvScreen>
            """);
        app.Pages.Add(page);
        var top = new Bar("""
            <DvStack style="height:40; padding:0; gap:0">
              <DvSelect id="sel" value="{Bind Data.Count}" style="width:300">
                <DvOption value="1" text="하나"/>
                <DvOption value="2" text="둘"/>
                <DvOption value="3" text="셋"/>
              </DvSelect>
            </DvStack>
            """);
        app.Top = top;
        var root = app.Surface;
        root.Layout(W, H);

        Click(root, 100, 20);
        var dd = root.Dropdown;
        Assert.NotNull(dd);
        Assert.Same(top.Surface, dd!.Owner);
        Assert.True(dd.Bounds.Bottom > 40, "드롭다운이 페이지 영역 위로 펼쳐짐");

        // 그려도 예외 없음(루트가 창 전체 기준으로 그림)
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);

        var item = dd.ItemRect(2);
        Assert.True(item.MidY > 40, "고를 항목이 페이지 칸 위에 있음");
        Click(root, item.MidX, item.MidY);

        Assert.Equal(3, top.Count);
        Assert.Null(root.Dropdown);
        Assert.Equal(0, page.Clicks); // 아래 페이지 버튼으로 새지 않음
    }

    [Fact]
    public void Switch_WhileEditing_ClosesKeyboard_KeepsValue()
    {
        var app = new DvApp();
        var a = new Page(PageInput);
        var b = new PageB(PageButton);
        app.Pages.Add(a);
        app.Pages.Add(b);
        var root = app.Surface; // 가상 키보드 모드(기본)
        root.Layout(W, H);

        Click(root, 50, 20); // A 의 입력
        root.Layout(W, H);
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);
        Assert.True(root.Edit.IsEditing);
        Assert.True(root.Keyboard is { IsOpen: true });
        Assert.Null(a.Surface!.Keyboard); // 자식 화면은 키보드를 띄우지 않는다

        root.TextInput("가");
        app.Show<PageB>(); // 편집 중 전환(통신 콜백·영역 버튼 등)
        root.Layout(W, H);

        Assert.Same(b, app.CurrentPage);
        Assert.Equal("처음가", a.Name); // 떠나는 페이지의 편집은 커밋(nav 전환과 같은 규칙)
        Assert.False(root.Edit.IsEditing);
        Assert.False(a.Surface.Edit.IsEditing);
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);
        Assert.False(root.Keyboard!.IsOpen);

        app.Show(a);
        root.Layout(W, H);
        Assert.Same(a, app.CurrentPage);
        Assert.Equal("처음가", Box(a.Surface, "in").InputValue);
    }

    [Fact]
    public void Switch_ViaBarButton_NativeEditor_CommitsPageEdit()
    {
        var app = new DvApp();
        var a = new Page(PageInput);
        var b = new PageB(PageButton);
        app.Pages.Add(a);
        app.Pages.Add(b);
        app.Left = new Bar(BarMarkup("width:200")) { OnHit = x => x.Show<PageB>() };
        var root = app.Surface;
        root.UseNativeEditor = true;
        root.Layout(W, H);

        Click(root, 250, 20); // A 의 입력(페이지 칸 x 200~)
        var id = root.Edit.SessionId;
        Assert.True(root.Edit.IsEditing);
        root.SetEditText(id, "새 값");

        Click(root, 30, 25); // 영역 버튼 → 편집 커밋 + B 로 전환
        Assert.Equal("새 값", a.Name);
        Assert.Same(b, app.CurrentPage);
        Assert.False(root.Edit.IsEditing);
    }

    [Fact]
    public void NativeEditor_EditState_FromActiveChild()
    {
        var app = new DvApp();
        var page = new Page(PageInput);
        app.Pages.Add(page);
        var left = new Bar(BarMarkup("width:200", """<DvInput id="lin" value="{Bind Data.Count}" style="position:absolute; left:0; top:100; width:150"/>"""));
        app.Left = left;
        app.Top = new Bar(BarMarkup("height:60"));
        var root = app.Surface;
        root.UseNativeEditor = true;
        root.Layout(W, H);

        Click(root, 250, 80); // 페이지 입력(창 (200,60) 기준)
        Assert.True(page.Surface!.UseNativeEditor);
        Assert.True(root.Edit.IsEditing);
        var inner = page.Surface.Edit.Rect;
        Assert.Equal(inner.Left + 200, root.Edit.Rect.Left);
        Assert.Equal(inner.Top + 60, root.Edit.Rect.Top);
        Assert.Equal("처음", root.Edit.InitialText);

        var first = root.Edit.SessionId;
        root.SetEditText(first, "페이지 값");
        Assert.True(root.CommitEdit(first, DvEditExit.Enter));
        root.Layout(W, H);
        Assert.Equal("페이지 값", page.Name);
        Assert.False(root.Edit.IsEditing);

        // 왼쪽 영역 입력으로 — 새 세션, 창 기준 (0,60) 옮김
        Click(root, 50, 175);
        Assert.True(root.Edit.IsEditing);
        Assert.NotEqual(first, root.Edit.SessionId);
        Assert.Equal(left.Surface!.Edit.Rect.Top + 60, root.Edit.Rect.Top);
        Assert.Equal("1", root.Edit.InitialText);
    }

    // ── 깨우기 ───────────────────────────────────────────────────────────

    [Fact]
    public void RenderRequested_On_OffThread_Invalidate()
    {
        var app = new DvApp();
        app.Pages.Add(new Page(PageButton));
        app.Top = new Bar(BarMarkup("height:60"));
        var root = app.Surface;
        root.Layout(W, H);
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);
        Assert.False(root.NeedsRender);

        var count = 0;
        object? sender = null;
        app.RenderRequested += (s, _) => { Interlocked.Increment(ref count); sender = s; };
        OffThread(app.Invalidate);

        Assert.True(Volatile.Read(ref count) > 0);
        Assert.Same(app, sender);
        Assert.True(root.NeedsRender);
    }

    [Fact]
    public void RenderRequested_On_OffThread_Show()
    {
        var app = new DvApp();
        var a = new Page(PageButton);
        var b = new PageB(PageButton);
        app.Pages.Add(a);
        app.Pages.Add(b);
        var root = app.Surface;
        root.Layout(W, H);
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);
        Assert.False(root.NeedsRender);

        var count = 0;
        app.RenderRequested += (_, _) => Interlocked.Increment(ref count);
        OffThread(app.Show<PageB>);

        Assert.True(Volatile.Read(ref count) > 0);
        Assert.True(root.NeedsRender);
        root.Layout(W, H);
        Assert.Same(b, app.CurrentPage);
    }

    // 요청이 BeforeLayout 의 스냅숏 뒤(같은 Layout 안)에 들어와도 잃지 않는다 — NeedsRender 가 남은 요청을 알린다
    [Fact]
    public void Request_AfterApplySnapshot_KeepsNeedsRender_AppliedNextLayout()
    {
        var app = new DvApp();
        var a = new Page(PageButton);
        var b = new PageB(PageButton);
        a.Shown = x => { a.Shown = null; x.Show(b); }; // ApplyPending 스냅숏 뒤에 실행(다른 스레드 Show 와 같은 끼어듦)
        app.Pages.Add(a);
        app.Pages.Add(b);
        var root = app.Surface;
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);

        Assert.Same(a, app.CurrentPage);
        Assert.True(root.NeedsRender, "남은 요청이 있으면 다시 그려야 함");
        root.Layout(W, H);
        Assert.Same(b, app.CurrentPage);
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);
        Assert.False(root.NeedsRender);
    }

    [Fact]
    public void RenderRequested_On_HandleWrite_InPage()
    {
        var app = new DvApp();
        var page = new Page(PageButton);
        app.Pages.Add(page);
        var root = app.Surface;
        root.Layout(W, H);
        using (var bmp = new SKBitmap((int)W, (int)H))
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);
        Assert.False(root.NeedsRender);

        var count = 0;
        app.RenderRequested += (_, _) => Interlocked.Increment(ref count);
        OffThread(() => page.Btn!.Text = "새 글자");

        Assert.True(Volatile.Read(ref count) > 0);
        Assert.True(root.NeedsRender);
    }

    [Fact]
    public void RenderRequested_SubscriberException_DoesNotEscape()
    {
        var app = new DvApp();
        var page = new Page(PageButton);
        app.Pages.Add(page);
        app.Surface.Layout(W, H);
        var second = 0;
        app.RenderRequested += (_, _) => throw new InvalidOperationException("구독자 실패");
        app.RenderRequested += (_, _) => second++;

        page.Btn!.Text = "x"; // Invalidate → Wake → 구독자 예외는 삼킨다
        app.Invalidate();
        Assert.True(second > 0);
    }

    // ── 영역 교체·소유 ───────────────────────────────────────────────────

    [Fact]
    public void Region_Replace_Applied_NextFrame()
    {
        var app = new DvApp();
        var page = new Page(PageButton);
        app.Pages.Add(page);
        var tall = new Bar(BarMarkup("height:60"));
        var shortBar = new Bar(BarMarkup("height:30"));
        app.Top = tall;
        var root = app.Surface;
        root.Layout(W, H);
        Assert.Equal(60, page.Surface!.WindowOffset.Y);

        app.Top = shortBar;
        Assert.Same(shortBar, app.Top);
        Assert.Equal(60, page.Surface.WindowOffset.Y); // 아직 적용 전
        root.Layout(W, H);
        Assert.Equal(30, page.Surface.WindowOffset.Y);
        Assert.Equal(H - 30, page.Surface.Root!.Rect.Height);

        // 새 영역(높이 30)이 입력을 받고 떠난 영역은 받지 않는다 — (30,25) 는 새 영역 버튼 위
        Click(root, 30, 25);
        Assert.Equal(1, shortBar.Clicks);
        Assert.Equal(0, tall.Clicks);

        app.Top = null;
        root.Layout(W, H);
        Assert.Equal(0, page.Surface.WindowOffset.Y);
        Assert.Equal(SKRect.Create(0, 0, W, H), page.Surface.Root!.Rect);
    }

    [Fact]
    public void Bar_SameInstance_TwoRegions_Throws()
    {
        var app = new DvApp();
        var bar = new Bar(BarMarkup("height:60"));
        app.Top = bar;
        Assert.Throws<InvalidOperationException>(() => app.Left = bar);
        Assert.Null(app.Left);

        app.Top = bar; // 같은 자리 재대입은 괜찮음
        app.Top = null; // 비우면 다른 자리로 옮길 수 있다
        app.Left = bar;
        Assert.Same(bar, app.Left);
    }

    [Fact]
    public void Bar_RemovedFromRegion_StaysOwnedByApp_KeepsState()
    {
        var app = new DvApp();
        app.Pages.Add(new Page(PageButton));
        var bar = new Bar(BarMarkup("height:60"));
        app.Top = bar;
        var root = app.Surface;
        root.Layout(W, H);
        var surface = bar.Surface;

        app.Top = null;
        root.Layout(W, H);
        Assert.Same(app, bar.App); // 한 앱에 한 번 놓인 틀은 그 앱 소속 그대로
        Assert.Throws<InvalidOperationException>(() => new DvApp().Top = bar);

        app.Bottom = bar; // 같은 앱 다른 자리 — 같은 화면(상태 유지)
        root.Layout(W, H);
        Assert.Same(surface, bar.Surface);
        Assert.Equal(new SKPoint(0, H - 60), bar.Surface!.WindowOffset);
    }

    // ── 옛 네비게이터 셸 동작의 대체(Task 6) ─────────────────────────────

    private static void Render(DvSurface s)
    {
        using var bmp = new SKBitmap((int)W, (int)H);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, W, H);
    }

    // 영역 입력 ↔ 페이지 입력을 오가면 떠나는 칸의 편집이 커밋되고 새 세션이 열린다
    [Fact]
    public void NativeEditor_SwitchBetweenBarAndPageInputs_CommitsAndNewSession()
    {
        var app = new DvApp();
        var page = new Page(PageInput);
        app.Pages.Add(page);
        var left = new Bar(BarMarkup("width:200", """<DvInput id="lin" value="{Bind Data.Count}" style="position:absolute; left:0; top:100; width:150"/>"""));
        app.Left = left;
        var root = app.Surface;
        root.UseNativeEditor = true;
        root.Layout(W, H);

        Click(root, 250, 20); // 페이지 입력
        var first = root.Edit.SessionId;
        root.SetEditText(first, "페이지 새 값");

        Click(root, 50, 115); // 왼쪽 영역 입력 — 페이지 편집 커밋, 새 세션
        Assert.Equal("페이지 새 값", page.Name);
        Assert.True(root.Edit.IsEditing);
        Assert.NotEqual(first, root.Edit.SessionId);
        Assert.Equal("1", root.Edit.InitialText);
        var second = root.Edit.SessionId;
        root.SetEditText(second, "7");

        Click(root, 250, 20); // 다시 페이지 — 영역 편집 커밋
        Assert.Equal(7, left.Count);
        Assert.NotEqual(second, root.Edit.SessionId);
        Assert.Equal("페이지 새 값", root.Edit.InitialText);
    }

    // app.Invalidate() 하나로 현재 페이지·영역이 VM 을 다시 읽는다(폴링 등 클릭 밖의 VM 변경)
    [Fact]
    public void Invalidate_RebuildsPageAndBarsFromVm()
    {
        var app = new DvApp();
        var page = new Page("""<DvScreen><DvLabel id="lbl" text="{Name}"/></DvScreen>""");
        app.Pages.Add(page);
        var top = new Bar("""<DvStack style="height:40"><DvLabel id="lbl" text="{Count}"/></DvStack>""");
        app.Top = top;
        var root = app.Surface;
        Render(root);

        page.Name = "둘째";
        top.Count = 2;
        Render(root);
        Assert.Equal("처음", Box(page.Surface!, "lbl").ResolvedText); // 알리기 전에는 그대로

        app.Invalidate();
        Assert.True(root.NeedsRender);
        Render(root);
        Assert.Equal("둘째", Box(page.Surface!, "lbl").ResolvedText);
        Assert.Equal("2", Box(top.Surface!, "lbl").ResolvedText);
    }

    private sealed class CurrentBar() : Bar("""<DvStack style="height:40"><DvLabel id="cur" text="{Current}"/></DvStack>""")
    {
        public string Current => App.CurrentPage?.GetType().Name ?? "";
    }

    // 영역은 페이지가 바뀔 때마다(코드에서 Show 해도, 보관된 페이지로 돌아가도) App.CurrentPage 를 다시 읽는다 — 옛 DvNavItem 선택 표시의 대체
    [Fact]
    public void Bar_RereadsCurrentPage_OnEveryTransition()
    {
        var app = new DvApp();
        var a = new Page(PageButton);
        var b = new PageB(PageButton);
        app.Pages.Add(a);
        app.Pages.Add(b);
        var top = new CurrentBar();
        app.Top = top;
        var root = app.Surface;
        Render(root);
        Assert.Equal("Page", Box(top.Surface!, "cur").ResolvedText);
        Assert.False(root.NeedsRender);

        app.Show<PageB>(); // 영역 클릭이 아닌 코드 전환
        Assert.True(root.NeedsRender);
        Render(root);
        Assert.Equal("PageB", Box(top.Surface!, "cur").ResolvedText);

        app.Show(a); // 보관된 페이지로 복귀
        Render(root);
        Assert.Equal("Page", Box(top.Surface!, "cur").ResolvedText);
    }

    private static Devinno.UI.Localization.DvStrings Table(string word)
    {
        var s = new Devinno.UI.Localization.DvStrings { Language = "ko" };
        s.LoadJson("ko", $$"""{ "word": "{{word}}" }""");
        s.LoadJson("en", $$"""{ "word": "{{word}}-en" }""");
        return s;
    }

    // 숨겨져 보관 중인 페이지도 문자열 교체·언어 전환을 받아, 다시 보일 때 새 글자로 그려진다
    [Fact]
    public void Strings_Change_WhileHidden_ReachesKeptPageOnReturn()
    {
        var app = new DvApp { Strings = Table("one") };
        var a = new Page("""<DvScreen><DvLabel id="lbl" text="@word"/></DvScreen>""");
        var b = new PageB(PageButton);
        app.Pages.Add(a);
        app.Pages.Add(b);
        var root = app.Surface;
        Render(root);
        Assert.Equal("one", Box(a.Surface!, "lbl").ResolvedText);

        app.Show(b);
        Render(root);
        var two = Table("two");
        app.Strings = two; // a 는 숨겨져 보관 중
        Render(root);
        app.Show(a);
        Render(root);
        Assert.Same(a, app.CurrentPage);
        Assert.Equal("two", Box(a.Surface!, "lbl").ResolvedText);

        app.Show(b);
        Render(root);
        two.Language = "en"; // 같은 테이블의 언어 전환 — 역시 숨겨진 동안
        Render(root);
        app.Show(a);
        Render(root);
        Assert.Equal("two-en", Box(a.Surface!, "lbl").ResolvedText);
    }

    // 보관된 페이지로 돌아오면 VM 을 다시 읽는다 — OnShown 에서 바꾼 값과 숨겨진 동안 바뀐 값이 두 번째 표시부터도 보인다
    [Fact]
    public void KeptPage_Reshow_RereadsVm_OnShownAndWhileHidden()
    {
        var app = new DvApp();
        var a = new Page("""<DvScreen><DvLabel id="lbl" text="{Name}"/></DvScreen>""");
        var b = new PageB(PageButton);
        app.Pages.Add(a);
        app.Pages.Add(b);
        var shows = 0;
        a.Shown = _ => a.Name = "표시 " + ++shows;
        var root = app.Surface;
        Render(root);
        Assert.Equal("표시 1", Box(a.Surface!, "lbl").ResolvedText);

        app.Show(b);
        Render(root);
        app.Show(a); // 두 번째 표시 — 같은 크기라 Invalidate 없이는 레이아웃이 아무것도 하지 않는다
        Render(root);
        Assert.Equal("표시 2", Box(a.Surface!, "lbl").ResolvedText);

        a.Shown = null;
        app.Show(b);
        Render(root);
        a.Name = "숨김 중 변경"; // a 는 숨겨져 보관 중 — 알리지 않음
        Render(root);
        app.Show(a);
        Render(root);
        Assert.Equal("숨김 중 변경", Box(a.Surface!, "lbl").ResolvedText);
    }

    private sealed class CountPage(string markup) : Page(markup)
    {
        public int Count { get; set; } = 1;
    }

    private sealed class WideBar() : Bar("""
        <DvStack style="padding:0; gap:0">
          <DvLabel text="a" style="width:100; height:30"/>
          <DvLabel text="b" visible="{Wide}" style="width:300; height:30"/>
        </DvStack>
        """)
    {
        public bool Wide { get; set; }
    }

    // 칸이 옮겨지면(루트는 할 일 없는 레이아웃이어도) 페이지가 띄운 드롭다운이 앵커를 따라간다 — 그리기 전 입력도 새 위치 기준
    [Fact]
    public void SlotMove_ReplacesChildPopup_WithoutRootRebuild()
    {
        var app = new DvApp();
        var page = new CountPage("""
            <DvScreen style="padding:0; gap:0">
              <DvSelect id="sel" value="{Bind Data.Count}" style="width:200">
                <DvOption value="1" text="하나"/>
                <DvOption value="2" text="둘"/>
              </DvSelect>
            </DvScreen>
            """);
        app.Pages.Add(page);
        var left = new WideBar();
        app.Left = left;
        var root = app.Surface;
        Render(root);
        Assert.Equal(100, root.Slots.Single(s => ReferenceEquals(s.Child, page.Surface)).Rect.Left);

        Click(root, 150, 15); // 페이지 선택 상자
        var dd = root.Dropdown;
        Assert.NotNull(dd);
        Assert.Same(page.Surface, dd!.Owner);
        var before = dd.Bounds.Left;

        left.Wide = true;
        left.Surface!.Invalidate(); // 틀만 다시 만듦 — 루트는 dirty 아님(같은 크기 → 할 일 없는 경로)
        root.Layout(W, H); // 그리기 없이
        Assert.Equal(300, root.Slots.Single(s => ReferenceEquals(s.Child, page.Surface)).Rect.Left);
        Assert.Same(dd, root.Dropdown);
        Assert.Equal(before + 200, dd.Bounds.Left);
    }

    // ── 오류·그리기 ──────────────────────────────────────────────────────

    [Fact]
    public void HandlerError_FromChild_ReachesAppOnce()
    {
        var app = new DvApp();
        var page = new Page(PageButton) { Throw = new InvalidOperationException("페이지 처리기") };
        app.Pages.Add(page);
        var left = new Bar(BarMarkup("width:200")) { OnHit = _ => throw new InvalidOperationException("틀 처리기") };
        app.Left = left;
        var errors = new List<DvHandlerErrorEventArgs>();
        app.HandlerError += (s, e) => { Assert.Same(app, s); errors.Add(e); };
        var root = app.Surface;
        root.Layout(W, H);

        Click(root, 340, 70); // 페이지 버튼
        Assert.Single(errors);
        Assert.Equal("onclick", errors[0].Kind);
        Assert.Equal("페이지 처리기", errors[0].Exception.Message);

        Click(root, 30, 25); // 왼쪽 영역 버튼
        Assert.Equal(2, errors.Count);
        Assert.Equal("틀 처리기", errors[1].Exception.Message);
    }

    [Fact]
    public void Render_DrawsAllSlots()
    {
        var app = new DvApp();
        app.Pages.Add(new Page("""<DvScreen style="padding:0; background:#ff00ff"/>"""));
        app.Top = new Bar("""<DvStack style="height:50; background:#ff0000"/>""");
        app.Bottom = new Bar("""<DvStack style="height:50; background:#0000ff"/>""");
        app.Left = new Bar("""<DvStack style="width:100; background:#00ff00"/>""");
        app.Right = new Bar("""<DvStack style="width:100; background:#ffff00"/>""");
        var root = app.Surface;

        using var bmp = new SKBitmap((int)W, (int)H);
        using (var canvas = new SKCanvas(bmp))
            root.RenderTo(canvas, W, H);

        Assert.Equal(new SKColor(0xff, 0x00, 0x00), bmp.GetPixel(400, 25));  // Top
        Assert.Equal(new SKColor(0x00, 0x00, 0xff), bmp.GetPixel(400, 575)); // Bottom
        Assert.Equal(new SKColor(0x00, 0xff, 0x00), bmp.GetPixel(50, 300));  // Left
        Assert.Equal(new SKColor(0xff, 0xff, 0x00), bmp.GetPixel(750, 300)); // Right
        Assert.Equal(new SKColor(0xff, 0x00, 0xff), bmp.GetPixel(400, 300)); // 페이지
        Assert.Equal(new SKColor(0x00, 0xff, 0x00), bmp.GetPixel(50, 55));   // 경계: Left 는 Top 아래부터
    }

    [Fact]
    public void Empty_App_RendersBlank()
    {
        var app = new DvApp();
        using var bmp = new SKBitmap((int)W, (int)H);
        using (var canvas = new SKCanvas(bmp))
            app.Surface.RenderTo(canvas, W, H);
        Assert.Null(app.CurrentPage);
        app.Surface.PointerDown(new SKPoint(10, 10));
        app.Surface.PointerUp(new SKPoint(10, 10));
    }
}
