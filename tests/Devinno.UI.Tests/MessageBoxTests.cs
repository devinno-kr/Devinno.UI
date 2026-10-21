using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Layout;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 8c-1 Task 6 — App.MessageBox (spec §7)
[Collection("AttachedApp")]
public class MessageBoxTests
{
    private const float W = 800, H = 600;

    private sealed class Page : DvPage
    {
        protected override string MarkupSource => "<DvScreen style=\"padding:0\"/>";
    }

    private static (DvApp App, DvSurface Root) Start(float w = W, float h = H, string? lang = "ko")
    {
        var app = new DvApp { Strings = lang is null ? null : new Devinno.UI.Localization.DvStrings { Language = lang } }; // 기본은 버튼을 한국어 글자로 찾는다
        app.Pages.Add(new Page());
        app.Surface.Layout(w, h);
        return (app, app.Surface);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static DvSurface TopSurface(DvApp app) => app.Dialogs.Top!.Surface!;

    private static List<LayoutBox> Buttons(DvApp app) => Flatten(TopSurface(app).Root!).Where(b => b.Node.Tag == "DvButton").ToList();

    private static string[] ButtonTexts(DvApp app) => Buttons(app).Select(b => b.ResolvedText!).ToArray();

    // MessageBox 는 async — 이어짐이 비동기로 올 수 있어(테스트 동기화 컨텍스트) 기다려서 받는다
    private static Task<DvDialogResult> Done(Task<DvDialogResult> t) => t.WaitAsync(TimeSpan.FromSeconds(5));

    private static void ClickButton(DvApp app, DvSurface root, string text, float w = W, float h = H)
    {
        var slot = root.Slots.Single(s => ReferenceEquals(s.Child, TopSurface(app))).Rect;
        var r = Buttons(app).Single(b => b.ResolvedText == text).Rect;
        var p = new SKPoint(slot.Left + r.MidX, slot.Top + r.MidY);
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(w, h);
    }

    [Fact]
    public void Buttons_are_English_by_default()
    {
        var (app, root) = Start(lang: null);
        _ = app.MessageBox("Text", "Title", DvMessageButtons.YesNoCancel);
        root.Layout(W, H);
        Assert.Equal(new[] { "Cancel", "No", "Yes" }, ButtonTexts(app));
    }

    [Theory]
    [InlineData(DvMessageButtons.Ok, new[] { "확인" })]
    [InlineData(DvMessageButtons.OkCancel, new[] { "취소", "확인" })]
    [InlineData(DvMessageButtons.YesNo, new[] { "아니요", "예" })]
    [InlineData(DvMessageButtons.YesNoCancel, new[] { "취소", "아니요", "예" })]
    public void ButtonLayout_LeftToRight_PrimaryRightmost(DvMessageButtons buttons, string[] expected)
    {
        var (app, root) = Start();
        _ = app.MessageBox("내용", "제목", buttons);
        root.Layout(W, H);

        Assert.Equal(expected, ButtonTexts(app));
        var last = Buttons(app)[^1];
        Assert.Equal("primary", last.Node.Attributes["variant"]);
        Assert.All(Buttons(app).Take(expected.Length - 1), b => Assert.Equal("secondary", b.Node.Attributes["variant"]));
    }

    [Theory]
    [InlineData(DvMessageButtons.Ok, "확인", DvDialogResult.Ok)]
    [InlineData(DvMessageButtons.OkCancel, "취소", DvDialogResult.Cancel)]
    [InlineData(DvMessageButtons.OkCancel, "확인", DvDialogResult.Ok)]
    [InlineData(DvMessageButtons.YesNo, "예", DvDialogResult.Yes)]
    [InlineData(DvMessageButtons.YesNo, "아니요", DvDialogResult.No)]
    [InlineData(DvMessageButtons.YesNoCancel, "취소", DvDialogResult.Cancel)]
    [InlineData(DvMessageButtons.YesNoCancel, "아니요", DvDialogResult.No)]
    [InlineData(DvMessageButtons.YesNoCancel, "예", DvDialogResult.Yes)]
    public async Task ClickingButton_ReturnsResult(DvMessageButtons buttons, string click, DvDialogResult expected)
    {
        var (app, root) = Start();
        var t = app.MessageBox("내용", "제목", buttons);
        root.Layout(W, H);

        ClickButton(app, root, click);

        Assert.Equal(expected, await Done(t));
        Assert.Empty(app.Dialogs);
    }

    [Theory]
    [InlineData(DvMessageButtons.Ok)]
    [InlineData(DvMessageButtons.YesNo)]
    public async Task CloseDialogs_ReturnsCancel(DvMessageButtons buttons)
    {
        var (app, root) = Start();
        var t = app.MessageBox("내용", "제목", buttons);
        root.Layout(W, H);

        app.CloseDialogs();
        root.Layout(W, H);

        Assert.Equal(DvDialogResult.Cancel, await Done(t));
    }

    [Fact]
    public void Caption_Null_NoTitleLabel_WithCaption_Title()
    {
        var (app, root) = Start();
        _ = app.MessageBox("본문", null);
        root.Layout(W, H);
        var noTitle = Flatten(TopSurface(app).Root!).Count(b => b.Node.Tag == "DvLabel");

        app.CloseDialogs();
        root.Layout(W, H);
        _ = app.MessageBox("본문", "삭제 확인");
        root.Layout(W, H);
        var labels = Flatten(TopSurface(app).Root!).Where(b => b.Node.Tag == "DvLabel").Select(b => b.ResolvedText).ToList();

        Assert.Contains("삭제 확인", labels);
        Assert.Equal(noTitle + 1, labels.Count);
    }

    [Fact]
    public void Strings_Override()
    {
        var (app, root) = Start();
        var strings = new DvStrings { Language = "ko" };
        strings.Add("ko", new Dictionary<string, string> { ["dv.dialog.ok"] = "OK" });
        app.Strings = strings;
        _ = app.MessageBox("x");
        root.Layout(W, H);

        Assert.Equal(["OK"], ButtonTexts(app));
    }

    // 본문 라벨(하나) — 줄 수·줄 폭은 화면에 놓인 라벨 폭으로 나눈 결과(엔진 줄바꿈과 같은 규칙)
    private static LayoutBox Body(DvApp app) => Flatten(TopSurface(app).Root!).Single(b => b.Node.Tag == "DvLabel" && b.Node.Attributes["class"] == "body");

    private static (IReadOnlyList<string> Lines, float Spacing, SKFont Font) BodyLines(DvApp app)
    {
        var l = Body(app);
        var font = Devinno.UI.Text.TextFont.Create(l.ResolvedText!, l.Style.FontSize, l.Style.Bold, l.Style.FontFamily);
        return (Devinno.UI.Text.TextWrap.Wrap(l.ResolvedText!, font, l.Rect.Width - l.Style.Padding.Horizontal), font.Spacing, font);
    }

    private static SKRect DialogSlot(DvApp app, DvSurface root) => root.Slots.Single(s => ReferenceEquals(s.Child, TopSurface(app))).Rect;

    private static float DialogWidth(DvApp app, DvSurface root) => DialogSlot(app, root).Width;

    // 본문이 줄마다 라벨 폭 안, 라벨 높이 = 줄 수 × 줄 간격, 버튼 줄이 본문 아래·칸 안
    private static void AssertBodyFits(DvApp app, DvSurface root)
    {
        var slot = DialogSlot(app, root);
        var body = Body(app);
        var (lines, spacing, font) = BodyLines(app);
        Assert.All(lines, t => Assert.True(font.MeasureText(t) <= body.Rect.Width + 0.05f, $"line {font.MeasureText(t)} > label {body.Rect.Width}"));
        Assert.Equal(lines.Count * spacing, body.Rect.Height - body.Style.Padding.Vertical, 0.5f);
        Assert.True(body.Rect.Right <= slot.Width + 0.01f, $"label {body.Rect} slot {slot}");
        foreach (var b in Buttons(app))
        {
            Assert.True(b.Rect.Top >= body.Rect.Bottom - 0.01f, $"button {b.Rect} body {body.Rect}");
            Assert.True(b.Rect.Bottom <= slot.Height + 0.01f && b.Rect.Right <= slot.Width + 0.01f, $"button {b.Rect} slot {slot}");
        }
    }

    [Fact]
    public void LongText_WrapsWithinWindow()
    {
        var (app, root) = Start(400, 600);
        _ = app.MessageBox(new string('가', 300), "긴 글");
        root.Layout(400, 600);

        Assert.True(DialogWidth(app, root) <= 400 - 32);
        Assert.True(BodyLines(app).Lines.Count > 1);
        AssertBodyFits(app, root);
    }

    [Fact]
    public void LongText_WidensWithinWindow_LinesFitInnerWidth()
    {
        var (app, root) = Start(1000, 600);
        _ = app.MessageBox(string.Join(" ", Enumerable.Repeat("가나다라마바사", 60)), "긴 글");
        root.Layout(1000, 600);

        var w = DialogWidth(app, root);
        Assert.True(w > 360 && w <= 968, $"width {w}");
        Assert.True(BodyLines(app).Lines.Count > 1);
        AssertBodyFits(app, root);
    }

    [Fact]
    public void ShortText_Width360()
    {
        var (app, root) = Start(1000, 600);
        _ = app.MessageBox("짧은 글", "제목");
        root.Layout(1000, 600);

        Assert.Equal(360, DialogWidth(app, root));
        Assert.Single(BodyLines(app).Lines);
    }

    // 다이얼로그 칸 크기(측정)와 그 칸에 배치한 결과가 같은 줄바꿈 — 측정 높이 = 칸 높이, 내용이 칸에 꼭 맞음
    [Fact]
    public void DialogRoot_SameWrapMeasureAndLayout()
    {
        var (app, root) = Start(W, H);
        _ = app.MessageBox(string.Join(" ", Enumerable.Repeat("가나다라마바사", 40)), "긴 글");
        root.Layout(W, H);

        var ds = TopSurface(app);
        var measured = ds.MeasureRoot(W, H, W - 32);
        var slot = DialogSlot(app, root);
        Assert.Equal(measured.Width, slot.Width, 0.01f);
        Assert.Equal(measured.Height, slot.Height, 0.01f);

        // 루트 안 마지막 자식(버튼 줄) 아래 = 칸 높이 − (패딩 20 + 테두리 1) — 측정한 줄 수로 배치됐다
        var frame = ds.Root!;
        var last = frame.Children[^1];
        Assert.Equal(slot.Height - frame.Style.Padding.Bottom, last.Rect.Bottom, 0.5f);
        Assert.True(BodyLines(app).Lines.Count > 1);
        AssertBodyFits(app, root);
    }

    // 열린 채 창이 줄면 다시 줄바꿈되어 글자가 칸 안에 남는다(이전 "다시 나누지 않음" 한계 해소)
    [Fact]
    public void WindowShrinksWhileOpen_Rewraps()
    {
        var (app, root) = Start(1000, 600);
        _ = app.MessageBox(string.Join(" ", Enumerable.Repeat("가나다라마바사", 30)), "긴 글");
        root.Layout(1000, 600);
        var wide = BodyLines(app).Lines.Count;

        root.Layout(500, 600);

        Assert.True(DialogWidth(app, root) <= 500 - 32 + 0.01f);
        Assert.True(BodyLines(app).Lines.Count > wide);
        AssertBodyFits(app, root);
    }

    // 창보다 긴 글 — 본문만 스크롤되고 버튼은 칸 안에서 눌린다
    [Fact]
    public async Task TallText_BodyScrolls_ButtonsInsideSlot()
    {
        var (app, root) = Start(900, 360);
        var t = app.MessageBox(string.Join((char)10, Enumerable.Range(0, 300).Select(i => "줄 " + i)), "긴 글");
        root.Layout(900, 360);

        var slot = DialogSlot(app, root);
        Assert.True(slot.Height <= 360 - 32 + 0.01f);
        foreach (var b in Buttons(app))
            Assert.True(b.Rect.Top >= -0.01f && b.Rect.Bottom <= slot.Height + 0.01f && b.Rect.Right <= slot.Width + 0.01f, $"button {b.Rect} slot {slot}");
        var scroll = Flatten(TopSurface(app).Root!).Single(b => b.Children.Contains(Body(app)));
        Assert.True(scroll.MaxScrollY > 0);
        Assert.True(scroll.Rect.Bottom <= Buttons(app)[0].Rect.Top + 0.01f);

        // 스크롤바 자리를 뺀 폭으로 나뉘어 마지막 줄까지 스크롤로 닿는다
        var body = Body(app);
        Assert.True(body.Rect.Right <= scroll.Rect.Right - 12 + 0.01f, $"body {body.Rect} scroll {scroll.Rect}");
        var (lines, spacing, _) = BodyLines(app);
        Assert.Equal(300, lines.Count);
        Assert.Equal(lines.Count * spacing, body.Rect.Height, 0.5f);
        Assert.Equal(body.Rect.Height - scroll.Rect.Height + scroll.Style.Padding.Vertical, scroll.MaxScrollY, 0.5f);

        ClickButton(app, root, "확인", 900, 360);
        Assert.Equal(DvDialogResult.Ok, await Done(t));
    }

    [Fact]
    public void ShortText_NoScroll()
    {
        var (app, root) = Start(1000, 600);
        _ = app.MessageBox("짧은 글", "제목");
        root.Layout(1000, 600);

        var scroll = Flatten(TopSurface(app).Root!).Single(b => b.Children.Contains(Body(app)));
        Assert.Equal(0f, scroll.MaxScrollY, 0.01f);
        Assert.Equal(360, DialogWidth(app, root));
        Assert.Equal(scroll.Rect.Width, Body(app).Rect.Width, 0.01f); // 스크롤바 자리 없음
    }

    [Fact]
    public void LargerThemeFont_WrapsIntoMoreLines()
    {
        var text = string.Join(" ", Enumerable.Repeat("가나다라마바사", 12));
        var (a, ra) = Start();
        _ = a.MessageBox(text);
        ra.Layout(W, H);
        var normal = BodyLines(a).Lines.Count;

        var (b, rb) = Start();
        b.Theme = DvTheme.Dark.With(new Dictionary<string, string> { ["font.body"] = "28" });
        _ = b.MessageBox(text);
        rb.Layout(W, H);

        Assert.True(BodyLines(b).Lines.Count > normal);
        AssertBodyFits(b, rb);
    }

    [Fact]
    public void Newlines_SeparateLines_BlankLineKept()
    {
        var (app, root) = Start();
        _ = app.MessageBox("첫째" + (char)10 + (char)10 + "셋째");
        root.Layout(W, H);

        Assert.Equal(["첫째", "", "셋째"], BodyLines(app).Lines.ToArray());
        AssertBodyFits(app, root);
    }

    private sealed class AwaitPage : DvPage
    {
        public DvDialogResult? After;
        protected override string MarkupSource => "<DvScreen style=\"padding:0\"><DvButton id=\"b\" text=\"P\" onclick=\"Go\" style=\"position:absolute; left:0; top:0; width:100; height:100\"/></DvScreen>";

        public async void Go() => After = await App.MessageBox("내용", "제목", DvMessageButtons.YesNo);
    }

    [Fact]
    public void AwaitInPageHandler_ResumesInSameFrameAsClose()
    {
        var app = new DvApp { Strings = new Devinno.UI.Localization.DvStrings { Language = "ko" } }; // 버튼을 한국어 글자로 찾는다
        var page = new AwaitPage();
        app.Pages.Add(page);
        var root = app.Surface;
        root.Layout(W, H);
        root.PointerDown(new SKPoint(50, 50));
        root.PointerUp(new SKPoint(50, 50));
        root.Layout(W, H); // 박스 화면 생성

        ClickButton(app, root, "예"); // 클릭 + Layout 한 번

        Assert.Equal(DvDialogResult.Yes, page.After);
    }

    [Fact]
    public async Task TwoMessageBoxes_Independent()
    {
        var (app, root) = Start();
        var a = app.MessageBox("첫째", "A", DvMessageButtons.YesNo);
        var b = app.MessageBox("둘째", "B", DvMessageButtons.OkCancel);
        root.Layout(W, H);

        Assert.Equal(2, app.Dialogs.Count);
        Assert.NotSame(app.Dialogs[0], app.Dialogs[1]);

        ClickButton(app, root, "취소");
        Assert.Equal(DvDialogResult.Cancel, await Done(b));
        Assert.False(a.IsCompleted);

        ClickButton(app, root, "예");
        Assert.Equal(DvDialogResult.Yes, await Done(a));
    }
}
