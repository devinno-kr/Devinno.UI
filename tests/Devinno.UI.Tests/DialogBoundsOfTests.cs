using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// DvDialog.BoundsOf(id) — 내장 다이얼로그의 고정 id 와 사용자 다이얼로그의 id 를 창 좌표로 찾는다(헤드리스 테스트가 PNG 에서 좌표를 읽지 않게)
[Collection("AttachedApp")]
public class DialogBoundsOfTests
{
    private const float W = 800, H = 600;

    private sealed class Page : DvPage
    {
        protected override string MarkupSource => "<DvScreen style=\"padding:0\"/>";
    }

    public sealed class UserDialog : DvDialog<bool?>
    {
        protected override string MarkupSource => """
            <DvDialogFrame title="Mine" style="width:300">
              <DvStack id="scroller" style="height:60; overflow:scroll">
                <DvLabel text="a" style="height:40"/>
                <DvLabel text="b" style="height:40"/>
                <DvButton id="deep" text="Deep" style="height:40"/>
              </DvStack>
              <DvRepeat id="rows" items="{Rows}">
                <DvLabel id="row" text="{Item}"/>
              </DvRepeat>
              <DvButton id="hidden" text="Hidden" visible="false"/>
              <DvButton id="go" text="Go" onclick="Go"/>
            </DvDialogFrame>
            """;

        public IReadOnlyList<string> Rows { get; } = ["r1", "r2"];

        public void Go() => Close(true);
    }

    private static DvApp Start(bool native = true)
    {
        var app = new DvApp();
        app.Pages.Add(new Page());
        app.Surface.UseNativeEditor = native;
        app.Surface.Layout(W, H);
        return app;
    }

    private static Task<T> Done<T>(Task<T> t) => t.WaitAsync(TimeSpan.FromSeconds(5));

    private static SKRect DialogSlot(DvApp app) => app.Surface.Slots.Single(s => ReferenceEquals(s.Child, app.Dialogs.Top!.Surface)).Rect;

    // 찾은 사각형 = 비지 않고 다이얼로그 칸 안
    private static SKRect Find(DvApp app, string id)
    {
        var r = app.Dialogs.Top!.BoundsOf(id);
        Assert.NotNull(r);
        Assert.False(r!.Value.IsEmpty);
        Assert.True(DialogSlot(app).Contains(r.Value), $"{id} {r} outside {DialogSlot(app)}");
        return r.Value;
    }

    private static void Press(DvApp app, SKRect r)
    {
        var p = new SKPoint(r.MidX, r.MidY);
        app.Surface.PointerDown(p);
        app.Surface.PointerUp(p);
        app.Surface.Layout(W, H);
    }

    [Theory]
    [InlineData(DvMessageButtons.Ok, "ok", DvDialogResult.Ok)]
    [InlineData(DvMessageButtons.OkCancel, "ok", DvDialogResult.Ok)]
    [InlineData(DvMessageButtons.OkCancel, "cancel", DvDialogResult.Cancel)]
    [InlineData(DvMessageButtons.YesNo, "yes", DvDialogResult.Yes)]
    [InlineData(DvMessageButtons.YesNo, "no", DvDialogResult.No)]
    [InlineData(DvMessageButtons.YesNoCancel, "yes", DvDialogResult.Yes)]
    [InlineData(DvMessageButtons.YesNoCancel, "no", DvDialogResult.No)]
    [InlineData(DvMessageButtons.YesNoCancel, "cancel", DvDialogResult.Cancel)]
    public async Task MessageBox_PressingTheIdBounds_ReturnsThatButton(DvMessageButtons buttons, string id, DvDialogResult expected)
    {
        var app = Start();
        var t = app.MessageBox("x", "t", buttons);
        app.Surface.Layout(W, H);

        Press(app, Find(app, id));

        Assert.Equal(expected, await Done(t));
    }

    [Theory]
    [InlineData(DvMessageButtons.Ok, new[] { "yes", "no", "cancel" })]
    [InlineData(DvMessageButtons.OkCancel, new[] { "yes", "no" })]
    [InlineData(DvMessageButtons.YesNo, new[] { "ok", "cancel" })]
    [InlineData(DvMessageButtons.YesNoCancel, new[] { "ok" })]
    public void MessageBox_ButtonsNotShown_AreNull(DvMessageButtons buttons, string[] absent)
    {
        var app = Start();
        _ = app.MessageBox("x", "t", buttons);
        app.Surface.Layout(W, H);

        foreach (var id in absent)
            Assert.Null(app.Dialogs.Top!.BoundsOf(id));
    }

    [Fact]
    public async Task InputBox_TypeThenPressOk_ReturnsTyped()
    {
        var app = Start();
        var t = app.InputBox("Name", "pump");
        app.Surface.Layout(W, H);

        Find(app, "input");
        app.Surface.SetEditText(app.Surface.Edit.SessionId, "fan");
        Press(app, Find(app, "ok"));

        Assert.Equal("fan", await Done(t));
    }

    [Fact]
    public async Task InputBox_Cancel_ReturnsNull()
    {
        var app = Start();
        var t = app.InputBox("Name", "pump");
        app.Surface.Layout(W, H);

        Press(app, Find(app, "cancel"));

        Assert.Null(await Done(t));
    }

    [Fact]
    public async Task InputNumber_InputOkCancel()
    {
        var app = Start();
        var t = app.InputNumber("Speed", 5);
        app.Surface.Layout(W, H);

        Find(app, "input");
        Find(app, "cancel");
        Press(app, Find(app, "ok"));

        Assert.Equal(5, await Done(t));
    }

    [Fact]
    public async Task SelectBox_ListOkCancel()
    {
        var app = Start();
        var t = app.SelectBox("Pick", ["a", "b"], 1);
        app.Surface.Layout(W, H);

        var list = Find(app, "list");
        Assert.True(list.Height >= 80); // 두 행(각 최소 40)
        Find(app, "cancel");
        Press(app, Find(app, "ok"));

        Assert.Equal(1, await Done(t));
    }

    [Fact]
    public async Task UserDialog_OwnId_PressReturnsResult()
    {
        var app = Start();
        var t = app.ShowDialog<UserDialog, bool?>();
        app.Surface.Layout(W, H);

        Press(app, Find(app, "go"));

        Assert.True(await Done(t));
    }

    [Fact]
    public void UserDialog_NullCases()
    {
        var app = Start();
        _ = app.ShowDialog<UserDialog, bool?>();
        var d = app.Dialogs.Top!;
        Assert.Null(d.BoundsOf("go")); // 아직 프레임 전 — 화면 없음

        app.Surface.Layout(W, H);
        Assert.Null(d.BoundsOf("nope"));   // 그런 id 없음
        Assert.Null(d.BoundsOf("hidden")); // visible=false
        Assert.Null(d.BoundsOf("row"));    // DvRepeat 안은 찾지 않는다
        Assert.NotNull(d.BoundsOf("rows")); // DvRepeat 자신은 찾는다

        app.CloseDialogs();
        Assert.Null(d.BoundsOf("go")); // 닫힘
        app.Surface.Layout(W, H);
        Assert.Null(d.BoundsOf("go"));
    }

    [Fact]
    public void UserDialog_InsideScrollBox_FollowsScroll()
    {
        var app = Start();
        _ = app.ShowDialog<UserDialog, bool?>();
        app.Surface.Layout(W, H);
        var d = app.Dialogs.Top!;
        var scroller = d.BoundsOf("scroller")!.Value;
        var deep = d.BoundsOf("deep")!.Value;
        Assert.True(deep.Top >= scroller.Bottom - 1); // 처음엔 스크롤 상자 아래로 잘려 있다(높이 60 안에 40+40)

        var box = d.Surface!.Root!;
        static LayoutBox? FindScroller(LayoutBox b) => b.IsScroller ? b : b.Children.Select(FindScroller).FirstOrDefault(x => x is not null);
        FindScroller(box)!.ScrollY = 20;
        Assert.Equal(deep.Top - 20, d.BoundsOf("deep")!.Value.Top, 1);
    }
}
