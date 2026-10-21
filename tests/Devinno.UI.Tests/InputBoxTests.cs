using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Editing;
using Devinno.UI.Layout;
using Devinno.UI.Localization;
using SkiaSharp;
using Xunit;

// 8c-2 Task 1 — App.InputBox · App.InputNumber (spec §9 확정)
[Collection("AttachedApp")]
public class InputBoxTests
{
    private const float W = 800, H = 600;

    private sealed class Page : DvPage
    {
        protected override string MarkupSource => "<DvScreen style=\"padding:0\"/>";
    }

    private static (DvApp App, DvSurface Root) Start(bool native = false)
    {
        var app = new DvApp { Strings = new Devinno.UI.Localization.DvStrings { Language = "ko" } }; // 버튼을 한국어 글자로 찾는다
        app.Pages.Add(new Page());
        app.Surface.UseNativeEditor = native;
        app.Surface.Layout(W, H);
        return (app, app.Surface);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static DvSurface TopSurface(DvApp app) => app.Dialogs.Top!.Surface!;

    private static List<LayoutBox> Buttons(DvApp app) => Flatten(TopSurface(app).Root!).Where(b => b.Node.Tag == "DvButton").ToList();

    private static string[] ButtonTexts(DvApp app) => Buttons(app).Select(b => b.ResolvedText!).ToArray();

    private static Task<T> Done<T>(Task<T> t) => t.WaitAsync(TimeSpan.FromSeconds(5));

    private static void ClickButton(DvApp app, DvSurface root, string text)
    {
        var slot = root.Slots.Single(s => ReferenceEquals(s.Child, TopSurface(app))).Rect;
        var r = Buttons(app).Single(b => b.ResolvedText == text).Rect;
        var p = new SKPoint(slot.Left + r.MidX, slot.Top + r.MidY);
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(W, H);
    }

    private static void Render(DvSurface root)
    {
        using var bmp = new SKBitmap((int)W, (int)H);
        using var canvas = new SKCanvas(bmp);
        root.RenderTo(canvas, W, H);
    }

    [Fact]
    public async Task InputBox_Ok_ReturnsInitialValue()
    {
        var (app, root) = Start(native: true); // 가상 키보드 모드의 확인 누름은 TouchMode 테스트(관찰 동작) 참조
        var t = app.InputBox("이름", "펌프");
        root.Layout(W, H);

        Assert.Equal(["취소", "확인"], ButtonTexts(app));
        Assert.Equal("primary", Buttons(app)[^1].Node.Attributes["variant"]);
        ClickButton(app, root, "확인");

        Assert.Equal("펌프", await Done(t));
        Assert.Empty(app.Dialogs);
    }

    [Fact]
    public void InputBox_StartsEditingOnShow()
    {
        var (app, root) = Start();
        _ = app.InputBox("이름", "펌프");
        root.Layout(W, H);

        Assert.True(root.Edit.IsEditing); // 같은 프레임에 루트 Edit 에 실린다
        Assert.Equal("펌프", root.Edit.InitialText);
        Assert.Equal(DvEditKind.Text, root.Edit.Kind);
        Assert.Equal(360f, root.Slots.Single(s => ReferenceEquals(s.Child, TopSurface(app))).Rect.Width);
    }

    [Fact]
    public async Task InputBox_Typed_ThenOk_ReturnsTyped()
    {
        var (app, root) = Start(native: true);
        var t = app.InputBox("이름", "펌프");
        root.Layout(W, H);

        root.SetEditText(root.Edit.SessionId, "새값");
        ClickButton(app, root, "확인"); // 편집 중 확인 — 편집이 먼저 커밋된 뒤 확인

        Assert.Equal("새값", await Done(t));
    }

    // 판정된 동작: 가상 키보드가 떠 있는 동안 키보드 밖(확인 버튼)을 누르면 다른 화면과 같이 편집이 취소되고
    // 그 누름은 삼켜진다 — 확인이 처리되지 않고 다이얼로그가 남는다. 확인은 키보드 완료(Enter)로(아래 테스트).
    [Fact]
    public void InputBox_TouchMode_OkWhileKeyboardOpen_CancelsEditAndSwallows()
    {
        var (app, root) = Start(native: false);
        var t = app.InputBox("이름", "펌프");
        root.Layout(W, H);
        Render(root);
        Assert.True(root.Keyboard is { IsOpen: true });

        root.SetEditText(root.Edit.SessionId, "새값");
        ClickButton(app, root, "확인");

        Assert.False(t.IsCompleted); // 관찰: 확인이 처리되지 않음
        Assert.Single(app.Dialogs);
        Assert.False(root.Edit.IsEditing); // 관찰: 편집 취소
        Assert.Equal("펌프", ((DvInputBoxDialog)app.Dialogs.Top!).Value);
    }

    [Fact]
    public async Task InputBox_TouchMode_Enter_Confirms()
    {
        var (app, root) = Start(native: false);
        var t = app.InputBox("이름", "펌프");
        root.Layout(W, H);
        Render(root);

        root.TextInput("2");
        root.KeyInput(DvKey.Enter); // 키보드 완료 = 커밋 + 확인
        root.Layout(W, H);

        Assert.Equal("펌프2", await Done(t));
        Assert.Empty(app.Dialogs);
        Assert.False(root.Edit.IsEditing);
    }

    [Fact]
    public async Task InputBox_NativeEnter_Confirms()
    {
        var (app, root) = Start(native: true);
        var t = app.InputBox("이름", "펌프");
        root.Layout(W, H);

        root.SetEditText(root.Edit.SessionId, "새값");
        Assert.True(root.CommitEdit(root.Edit.SessionId, DvEditExit.Enter));
        root.Layout(W, H);

        Assert.Equal("새값", await Done(t));
    }

    [Fact]
    public void InputBox_NativeFocusLost_DoesNotConfirm()
    {
        var (app, root) = Start(native: true);
        var t = app.InputBox("이름", "펌프");
        root.Layout(W, H);

        root.SetEditText(root.Edit.SessionId, "새값");
        root.CommitEdit(root.Edit.SessionId, DvEditExit.FocusLost);
        root.Layout(W, H);

        Assert.False(t.IsCompleted);
        Assert.Single(app.Dialogs);
    }

    [Fact]
    public void InputNumber_TouchMode_EnterOutOfRange_StaysOpen()
    {
        var (app, root) = Start(native: false);
        var t = app.InputNumber("온도", 25, 0, 100);
        root.Layout(W, H);
        Render(root);

        root.KeyInput(DvKey.Backspace);
        root.KeyInput(DvKey.Backspace);
        root.TextInput("150"); // 범위 밖
        root.KeyInput(DvKey.Enter);
        root.Layout(W, H);

        Assert.False(t.IsCompleted);
        Assert.Single(app.Dialogs);
        Assert.True(root.Edit.IsEditing); // 거절 — 세션·키보드 유지
        Assert.NotNull(root.Edit.Error);
        Assert.Equal(25.0, ((DvNumberBoxDialog)app.Dialogs.Top!).Value);
    }

    [Fact]
    public async Task InputNumber_TouchMode_EnterInRange_Confirms()
    {
        var (app, root) = Start(native: false);
        var t = app.InputNumber("온도", 25, 0, 100);
        root.Layout(W, H);
        Render(root);

        root.KeyInput(DvKey.Backspace);
        root.KeyInput(DvKey.Backspace);
        root.TextInput("42.5");
        root.KeyInput(DvKey.Enter);
        root.Layout(W, H);

        Assert.Equal(42.5, await Done(t));
    }

    [Fact]
    public async Task InputBox_Cancel_ReturnsNull()
    {
        var (app, root) = Start(native: true);
        var t = app.InputBox("이름", "펌프");
        root.Layout(W, H);

        ClickButton(app, root, "취소");

        Assert.Null(await Done(t));
    }

    [Fact]
    public async Task InputBox_CloseDialogs_ReturnsNull()
    {
        var (app, root) = Start();
        var t = app.InputBox("이름", "펌프");
        root.Layout(W, H);

        app.CloseDialogs();
        root.Layout(W, H);

        Assert.Null(await Done(t));
        Assert.False(root.Edit.IsEditing);
    }

    [Fact]
    public async Task InputBox_NullValue_EmptyString()
    {
        var (app, root) = Start(native: true);
        var t = app.InputBox("x");
        root.Layout(W, H);
        Assert.Equal("", root.Edit.InitialText);

        ClickButton(app, root, "확인");

        Assert.Equal("", await Done(t));
    }

    [Fact]
    public async Task InputNumber_Ok_ReturnsValue()
    {
        var (app, root) = Start(native: true);
        var t = app.InputNumber("온도", 25);
        root.Layout(W, H);

        ClickButton(app, root, "확인");

        Assert.Equal(25.0, await Done(t));
    }

    [Fact]
    public async Task InputNumber_OutOfRange_Rejected_ReturnsLastValid()
    {
        var (app, root) = Start(native: true);
        var t = app.InputNumber("온도", 25, 0, 100);
        root.Layout(W, H);

        root.SetEditText(root.Edit.SessionId, "150");
        ClickButton(app, root, "확인");

        Assert.Equal(25.0, await Done(t));
    }

    [Fact]
    public async Task InputNumber_InRange_Typed()
    {
        var (app, root) = Start(native: true);
        var t = app.InputNumber("온도", 25, 0, 100);
        root.Layout(W, H);

        root.SetEditText(root.Edit.SessionId, "42.5");
        ClickButton(app, root, "확인");

        Assert.Equal(42.5, await Done(t));
    }

    [Fact]
    public void InputNumber_NumericKeyboard()
    {
        var (app, root) = Start();
        _ = app.InputNumber("온도", 25, 0, 100);
        root.Layout(W, H);

        Assert.True(root.Edit.IsEditing);
        Assert.Equal(DvEditKind.Numeric, root.Edit.Kind);
        Assert.Equal("25", root.Edit.InitialText);
        Assert.Equal(0, root.Edit.Numeric!.Min);
        Assert.Equal(100, root.Edit.Numeric!.Max);
    }

    [Fact]
    public void InputNumber_NoRange_NoLimits()
    {
        var (app, root) = Start();
        _ = app.InputNumber("온도", 25);
        root.Layout(W, H);

        Assert.Null(root.Edit.Numeric!.Min);
        Assert.Null(root.Edit.Numeric!.Max);
    }

    [Fact]
    public async Task TwoInputBoxes_Independent()
    {
        var (app, root) = Start(native: true);
        var a = app.InputBox("A", "가");
        root.Layout(W, H);
        var b = app.InputBox("B", "나");
        root.Layout(W, H);

        Assert.Equal(2, app.Dialogs.Count);
        Assert.Equal("나", root.Edit.InitialText); // 위 박스가 편집 중
        root.SetEditText(root.Edit.SessionId, "나2");
        ClickButton(app, root, "확인");
        Assert.Equal("나2", await Done(b));
        Assert.False(a.IsCompleted);

        ClickButton(app, root, "확인");
        Assert.Equal("가", await Done(a));
    }

    [Fact]
    public void ButtonTexts_FromStrings()
    {
        var (app, root) = Start();
        var strings = new DvStrings { Language = "ko" };
        strings.Add("ko", new Dictionary<string, string> { ["dv.dialog.ok"] = "OK" });
        app.Strings = strings;
        _ = app.InputBox("x");
        root.Layout(W, H);

        Assert.Equal(["취소", "OK"], ButtonTexts(app));
    }
    // ── 8c-2 최종 리뷰 I-1: 편집은 박스가 맨 위 다이얼로그일 때만 ─────────────

    [Fact]
    public void InputBox_SameFrameAsMessageBox_DoesNotEditUnderneath()
    {
        var (app, root) = Start(native: false);
        var input = app.InputBox("이름", "펌프");
        var msg = app.MessageBox("알림", buttons: DvMessageButtons.OkCancel);
        root.Layout(W, H);
        Render(root);

        Assert.False(root.Edit.IsEditing); // 위에 메시지 박스 — 아래 입력 박스는 편집을 시작하지 않는다
        root.KeyInput(DvKey.Enter);
        root.Layout(W, H);

        Assert.False(input.IsCompleted);
        Assert.False(msg.IsCompleted);
        Assert.Equal(2, app.Dialogs.Count);
    }

    [Fact]
    public async Task InputBox_UnderMessageBox_StartsEditingWhenTop()
    {
        var (app, root) = Start(native: false);
        var input = app.InputBox("이름", "펌프");
        var msg = app.MessageBox("알림");
        root.Layout(W, H);
        Assert.False(root.Edit.IsEditing);

        ClickButton(app, root, "확인"); // 메시지 박스 닫힘 → 다음 Layout 에 입력 박스가 맨 위

        Assert.Equal(DvDialogResult.Ok, await Done(msg));
        Assert.True(root.Edit.IsEditing);
        Assert.Equal("펌프", root.Edit.InitialText);
        Assert.False(input.IsCompleted);
    }

    [Fact]
    public async Task InputBox_ClosedBeforeFirstCompose_NoEdit()
    {
        var (app, root) = Start(native: false);
        var input = app.InputBox("이름", "펌프");
        app.SyncContext.Post(_ => app.CloseDialogs(), null); // 적용 뒤(같은 프레임 Post 대기열), 첫 칸 배치 전에 닫힘
        root.Layout(W, H);

        Assert.False(root.Edit.IsEditing);
        root.Layout(W, H);
        Assert.Null(await Done(input));
        Assert.False(root.Edit.IsEditing);
    }

    // ── I-2: InputNumber 인자 검사 ─────────────

    [Theory]
    [InlineData(double.NaN, null, null)]
    [InlineData(double.PositiveInfinity, null, null)]
    [InlineData(double.NegativeInfinity, null, null)]
    [InlineData(5.0, 10.0, 0.0)]   // min > max
    [InlineData(-1.0, 0.0, 100.0)] // < min
    [InlineData(101.0, 0.0, 100.0)] // > max
    [InlineData(-1.0, 0.0, null)]
    [InlineData(101.0, null, 100.0)]
    public void InputNumber_InvalidArgs_Throws_NothingRegistered(double value, double? min, double? max)
    {
        var (app, _) = Start();

        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = app.InputNumber("온도", value, min, max); }); // 호출 자리에서 동기로 던진다
        Assert.Empty(app.Dialogs); // Dialogs.Count == 0
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(100.0)]
    public void InputNumber_BoundaryValue_Accepted(double value)
    {
        var (app, root) = Start();
        _ = app.InputNumber("온도", value, 0, 100);
        root.Layout(W, H);

        Assert.Single(app.Dialogs);
    }
}
