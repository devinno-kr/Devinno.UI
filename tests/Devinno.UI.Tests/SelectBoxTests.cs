using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 8c-2 Task 2 — App.SelectBox (spec §9 확정)
[Collection("AttachedApp")]
public class SelectBoxTests
{
    private const float W = 800, H = 600;

    private sealed class Page : DvPage
    {
        protected override string MarkupSource => "<DvScreen style=\"padding:0\"/>";
    }

    private static (DvApp App, DvSurface Root) Start()
    {
        var app = new DvApp { Strings = new Devinno.UI.Localization.DvStrings { Language = "ko" } }; // 버튼을 한국어 글자로 찾는다
        app.Pages.Add(new Page());
        app.Surface.Layout(W, H);
        return (app, app.Surface);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static DvSurface TopSurface(DvApp app) => app.Dialogs.Top!.Surface!;

    private static List<LayoutBox> All(DvApp app) => Flatten(TopSurface(app).Root!).ToList();

    private static LayoutBox Button(DvApp app, string text) => All(app).Single(b => b.Node.Tag == "DvButton" && b.ResolvedText == text);

    private static LayoutBox Row(DvApp app, string text) => All(app).Single(b => b.Node.Tag == "DvLabel" && b.ResolvedText == text);

    private static Task<T> Done<T>(Task<T> t) => t.WaitAsync(TimeSpan.FromSeconds(5));

    private static SKRect Slot(DvApp app, DvSurface root) => root.Slots.Single(s => ReferenceEquals(s.Child, TopSurface(app))).Rect;

    private static void Click(DvApp app, DvSurface root, LayoutBox box)
    {
        var slot = Slot(app, root);
        var p = new SKPoint(slot.Left + box.Rect.MidX, slot.Top + box.Rect.MidY);
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(W, H);
    }

    [Fact]
    public async Task Select_TapItemThenOk_ReturnsIndex()
    {
        var (app, root) = Start();
        var t = app.SelectBox("종류", ["A", "B", "C"]);
        root.Layout(W, H);

        Click(app, root, Row(app, "B"));
        Click(app, root, Button(app, "확인"));

        Assert.Equal(1, await Done(t));
        Assert.Empty(app.Dialogs);
    }

    [Fact]
    public async Task Select_InitialSelected_OkReturnsIt()
    {
        var (app, root) = Start();
        var t = app.SelectBox("종류", ["A", "B", "C"], selected: 2);
        root.Layout(W, H);

        Assert.Equal(["취소", "확인"], All(app).Where(b => b.Node.Tag == "DvButton").Select(b => b.ResolvedText!).ToArray());
        Click(app, root, Button(app, "확인"));

        Assert.Equal(2, await Done(t));
    }

    [Fact]
    public void Select_NoSelection_OkDisabled()
    {
        var (app, root) = Start();
        var t = app.SelectBox("종류", ["A", "B"]);
        root.Layout(W, H);

        Assert.True(Button(app, "확인").Disabled);
        Click(app, root, Button(app, "확인"));
        Assert.False(t.IsCompleted);
        Assert.Single(app.Dialogs);

        Click(app, root, Row(app, "A")); // 선택하면 활성화
        Assert.False(Button(app, "확인").Disabled);
    }

    [Fact]
    public void Select_OutOfRangeSelected_TreatedAsNone()
    {
        var (app, root) = Start();
        _ = app.SelectBox("종류", ["A", "B"], selected: 99);
        root.Layout(W, H);

        Assert.True(Button(app, "확인").Disabled);
    }

    [Fact]
    public void Select_SelectedRowStyled()
    {
        var (app, root) = Start();
        _ = app.SelectBox("종류", ["A", "B", "C"], selected: 1);
        root.Layout(W, H);

        var a = Row(app, "A").Style.Background;
        var b = Row(app, "B").Style.Background;
        Assert.NotEqual(a, b);
        Assert.Equal(a, Row(app, "C").Style.Background);

        Click(app, root, Row(app, "C")); // 선택 이동
        Assert.NotEqual(Row(app, "A").Style.Background, Row(app, "C").Style.Background);
        Assert.Equal(Row(app, "A").Style.Background, Row(app, "B").Style.Background);
    }

    [Fact]
    public void Select_ManyItems_ListScrollsWithin320()
    {
        var (app, root) = Start();
        _ = app.SelectBox("종류", Enumerable.Range(0, 200).Select(i => "항목" + i).ToList());
        root.Layout(W, H);

        var list = All(app).Single(b => b.Node.Tag == "DvRepeat");
        Assert.True(list.Rect.Height <= 320f + 0.01f);
        Assert.True(list.MaxScrollY > 0);
        Assert.True(Slot(app, root).Height <= H - 32f + 0.01f);

        var slot = Slot(app, root);
        root.PointerWheel(new SKPoint(slot.Left + list.Rect.MidX, slot.Top + list.Rect.MidY), -1);
        root.Layout(W, H);
        Assert.True(All(app).Single(b => b.Node.Tag == "DvRepeat").ScrollY > 0);
    }

    [Fact]
    public void Select_EmptyItems_ThrowsAtCall()
    {
        var (app, _) = Start();

        Assert.Throws<ArgumentException>(() => { _ = app.SelectBox("종류", []); });
        Assert.Empty(app.Dialogs);
    }

    [Fact]
    public async Task Select_Cancel_ReturnsNull()
    {
        var (app, root) = Start();
        var t = app.SelectBox("종류", ["A", "B"], selected: 0);
        root.Layout(W, H);

        Click(app, root, Button(app, "취소"));

        Assert.Null(await Done(t));
    }

    [Fact]
    public async Task Select_CloseDialogs_ReturnsNull()
    {
        var (app, root) = Start();
        var t = app.SelectBox("종류", ["A", "B"], selected: 0);
        root.Layout(W, H);

        app.CloseDialogs();
        root.Layout(W, H);

        Assert.Null(await Done(t));
    }

    [Fact]
    public async Task Select_ShortWindow_ButtonsStayInsideSlot()
    {
        var app = new DvApp { Strings = new Devinno.UI.Localization.DvStrings { Language = "ko" } }; // 버튼을 한국어 글자로 찾는다
        app.Pages.Add(new Page());
        app.Surface.Layout(800, 360);
        var root = app.Surface;
        var t = app.SelectBox("종류", Enumerable.Range(0, 200).Select(i => "항목" + i).ToList());
        root.Layout(800, 360);

        var slot = Slot(app, root);
        foreach (var text in new[] { "취소", "확인" })
        {
            var r = Button(app, text).Rect;
            Assert.True(r.Top >= -0.01f && r.Bottom <= slot.Height + 0.01f, text + " " + r + " slot " + slot);
        }
        var p = new SKPoint(slot.Left + Button(app, "취소").Rect.MidX, slot.Top + Button(app, "취소").Rect.MidY);
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(800, 360);
        Assert.Null(await Done(t));
    }

    [Fact]
    public async Task Select_DuplicateItems_SecondReturnsIndex1()
    {
        var (app, root) = Start();
        var t = app.SelectBox("종류", ["A", "A"]);
        root.Layout(W, H);

        Click(app, root, All(app).Where(b => b.Node.Tag == "DvLabel" && b.ResolvedText == "A").ElementAt(1));
        Click(app, root, Button(app, "확인"));

        Assert.Equal(1, await Done(t));
    }

    [Fact]
    public async Task Select_MutateListAfterCall_KeepsOriginal()
    {
        var (app, root) = Start();
        var items = new List<string> { "A", "B" };
        var t = app.SelectBox("종류", items);
        items.Clear();
        root.Layout(W, H);

        Click(app, root, Row(app, "B"));
        Click(app, root, Button(app, "확인"));

        Assert.Equal(1, await Done(t));
    }

    [Fact]
    public void Select_SelectedRowUnderHover_StillLooksSelected()
    {
        var (app, root) = Start();
        _ = app.SelectBox("종류", ["A", "B"], selected: 1);
        root.Layout(W, H);
        var before = Row(app, "B").Style.Background;

        var slot = Slot(app, root);
        var r = Row(app, "B").Rect;
        root.PointerMove(new SKPoint(slot.Left + r.MidX, slot.Top + r.MidY));
        root.Layout(W, H);

        var row = Row(app, "B");
        Assert.Equal(before, (row.StyleHover ?? row.Style).Background);
    }

    private static void AssertButtonsInside(DvApp app, DvSurface root)
    {
        var slot = Slot(app, root);
        foreach (var text in new[] { "취소", "확인" })
        {
            var r = Button(app, text).Rect;
            Assert.True(r.Top >= -0.01f && r.Bottom <= slot.Height + 0.01f, text + " " + r + " slot " + slot);
        }
    }

    [Fact]
    public async Task Select_WindowShrinksWhileOpen_ButtonsStayInside()
    {
        var (app, root) = Start();
        var t = app.SelectBox("종류", Enumerable.Range(0, 200).Select(i => "항목" + i).ToList());
        root.Layout(W, H);

        root.Layout(800, 360); // 한 번에 — 다시 만들기(refit) 없음

        AssertButtonsInside(app, root);
        Assert.True(All(app).Single(b => b.Node.Tag == "DvRepeat").Rect.Height < 320f);
        var slot = Slot(app, root);
        var b = Button(app, "취소").Rect;
        var p = new SKPoint(slot.Left + b.MidX, slot.Top + b.MidY);
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(800, 360);
        Assert.Null(await Done(t));
    }

    [Fact]
    public void Select_LargerThemeFontsAndSpacing_ButtonsStayInside()
    {
        var app = new DvApp { Strings = new Devinno.UI.Localization.DvStrings { Language = "ko" } }; // 버튼을 한국어 글자로 찾는다
        app.Pages.Add(new Page());
        app.Theme = DvTheme.Default.With(new Dictionary<string, string>
        {
            ["font.title"] = "32", ["font.body"] = "24", ["spacing.lg"] = "32", ["spacing.md"] = "24", ["spacing.sm"] = "16",
        });
        var root = app.Surface;
        root.Layout(800, 360);
        _ = app.SelectBox("종류", Enumerable.Range(0, 200).Select(i => "항목" + i).ToList());
        root.Layout(800, 360);

        AssertButtonsInside(app, root);
    }

    // 창을 한 번도 배치하기 전에 띄워도 첫 배치에서 바로 창에 맞는다
    [Fact]
    public void Select_BeforeAnyLayout_FirstLayoutFits()
    {
        var app = new DvApp { Strings = new Devinno.UI.Localization.DvStrings { Language = "ko" } }; // 버튼을 한국어 글자로 찾는다
        app.Pages.Add(new Page());
        _ = app.SelectBox("종류", Enumerable.Range(0, 200).Select(i => "항목" + i).ToList());

        var root = app.Surface;
        root.Layout(800, 360);

        AssertButtonsInside(app, root);
        Assert.True(All(app).Single(b => b.Node.Tag == "DvRepeat").Rect.Height < 320f);
    }

    // 긴 항목은 줄바꿈되어 행이 높아지고 다음 행은 그 아래에서 시작
    [Fact]
    public void Select_LongItem_WrapsAndRowGrows()
    {
        var (app, root) = Start();
        var longText = string.Join(" ", Enumerable.Repeat("가나다라마바사", 20));
        _ = app.SelectBox("종류", [longText, "B"]);
        root.Layout(W, H);

        var row = Row(app, longText);
        var next = Row(app, "B");
        using var font = Devinno.UI.Text.TextFont.Create(longText, row.Style.FontSize, row.Style.Bold, row.Style.FontFamily);
        var lines = Devinno.UI.Text.TextWrap.Wrap(longText, font, row.Rect.Width - row.Style.Padding.Horizontal);
        Assert.True(lines.Count >= 2);
        Assert.True(row.Rect.Height >= lines.Count * font.Spacing - 0.5f, $"row {row.Rect} lines {lines.Count}");
        Assert.True(row.Rect.Height > 40f);
        Assert.True(next.Rect.Top >= row.Rect.Bottom - 0.01f, $"next {next.Rect} row {row.Rect}");
        Assert.Equal(40f, next.Rect.Height, 0.01f);
        Assert.True(Slot(app, root).Width <= 360.01f);
    }

    // 항목이 적으면 목록은 내용 높이(행 40 × 개수) — 스크롤 없음
    [Fact]
    public void Select_FewItems_ListIsContentHeight()
    {
        var (app, root) = Start();
        _ = app.SelectBox("종류", ["A", "B", "C"]);
        root.Layout(W, H);

        var list = All(app).Single(b => b.Node.Tag == "DvRepeat");
        Assert.Equal(120f, list.Rect.Height, 0.01f);
        Assert.Equal(0f, list.MaxScrollY, 0.01f);
        AssertButtonsInside(app, root);
    }
    // 8c-2 최종 리뷰 M-1: 처음 선택한 항목이 목록 밖이면 보이게 스크롤
    [Fact]
    public void Select_InitialSelected_ScrolledIntoView()
    {
        var (app, root) = Start();
        var items = Enumerable.Range(0, 12).Select(i => $"항목{i}").ToArray();
        _ = app.SelectBox("종류", items, selected: 11);
        root.Layout(W, H);

        var list = All(app).Single(b => b.Node.Tag == "DvRepeat");
        var row = Row(app, "항목11");
        Assert.True(list.ScrollY > 0);
        Assert.True(row.Rect.Top - list.ScrollY >= list.Rect.Top - 0.5f, $"row {row.Rect} scroll {list.ScrollY} list {list.Rect}");
        Assert.True(row.Rect.Bottom - list.ScrollY <= list.Rect.Bottom + 0.5f, $"row {row.Rect} scroll {list.ScrollY} list {list.Rect}");
    }
}
