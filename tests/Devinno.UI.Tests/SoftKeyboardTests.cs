using Devinno.UI;
using Devinno.UI.Editing;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// P8a-8 코어 가상 키보드 — 슬라이드·모달·한/영·Shift·기호·키패드·pan·물리 키
public class SoftKeyboardTests
{
    private const float W = 800, H = 480;

    public sealed class Vm
    {
        public string Name { get; set; } = "";
        public string Password { get; set; } = "";
        public int Count { get; set; } = 5;
        public uint Pulses { get; set; } = 1;
        public double Temp { get; set; } = 20;
        public int Saves;
        public void Save() => Saves++;
    }

    private const string Form = """
        <DvScreen style="padding:0; gap:0">
          <DvInput id="name" value="{Bind Data.Name}"/>
          <DvInput id="pw" value="{Bind Data.Password}" keyboard-type="password"/>
          <DvInput id="count" value="{Bind Data.Count}" keyboard-type="numeric" min="0" max="100"/>
          <DvInput id="pulses" value="{Bind Data.Pulses}" keyboard-type="numeric"/>
          <DvInput id="temp" value="{Bind Data.Temp}" keyboard-type="numeric" unit="℃"/>
          <DvButton text="저장" onclick="Save" style="height:40"/>
        </DvScreen>
        """;

    private sealed class Rig
    {
        public long Now = 1000;
        public readonly DvSurface S;
        public readonly Vm Vm = new();

        public Rig(string markup = Form, bool native = false)
        {
            S = new DvSurface(markup, Vm) { Clock = () => Now, UseNativeEditor = native };
            Frame();
        }

        public void Frame()
        {
            using var bmp = new SKBitmap((int)W, (int)H);
            using var c = new SKCanvas(bmp);
            S.RenderTo(c, W, H);
        }

        public void Click(float x, float y)
        {
            S.PointerDown(new SKPoint(x, y));
            S.PointerUp(new SKPoint(x, y));
            Frame();
        }

        // 편집 시작 후 키보드가 다 올라올 때까지
        public void Edit(float y)
        {
            Now += SoftKeyboard.AnimMs + 10; // 앞 키보드가 다 내려간 뒤
            Frame();
            Click(50, y);
            Now += SoftKeyboard.AnimMs + 10;
            Frame();
        }

        public SoftKeyboard Kb => S.Keyboard!;

        public SoftKeyboard.Key Key(string id) => Kb.Keys(W, H, Now).First(k => k.Id == id);

        public void Tap(string id)
        {
            var r = Key(id).Rect;
            Click(r.MidX, r.MidY);
        }

        public void TapAll(string ids)
        {
            foreach (var c in ids)
                Tap(c.ToString());
        }

        public LayoutBox Box(string id) => Flatten(S.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Slides_up_over_200ms_from_the_bottom()
    {
        var r = new Rig();
        r.Click(50, 20);
        Assert.True(r.Kb.IsOpen);
        Assert.Equal(H, r.Kb.Panel(W, H, r.Now).Top);          // 시작 = 화면 아래
        Assert.Equal(r.Now + 16, r.S.NextAnimationDue(r.Now));   // 움직이는 동안 다음 프레임

        r.Now += 100;
        Assert.Equal(H - SoftKeyboard.PanelHeight(H) / 2, r.Kb.Panel(W, H, r.Now).Top, 1);
        r.Now += 150;
        Assert.Equal(H - SoftKeyboard.PanelHeight(H), r.Kb.Panel(W, H, r.Now).Top);
        Assert.Null(r.S.NextAnimationDue(r.Now));
    }

    [Fact]
    public void Text_defaults_to_hangul_and_types_with_jamo_backspace()
    {
        var r = new Rig();
        r.Edit(20);
        Assert.Equal(KbMode.Hangul, r.Kb.Mode);
        r.TapAll("ㅎㅏㄴㄱ");
        Assert.Equal("한ㄱ", r.Box("name").EditText); // ㄴ+ㄱ 은 겹받침이 아니라 새 음절
    }

    [Fact]
    public void Typing_hangul_then_done_commits_once()
    {
        var r = new Rig();
        r.Edit(20);
        r.TapAll("ㅎㅏㄴㄱㅡㄹ");
        Assert.Equal("한글", r.Box("name").EditText);
        r.Tap(SoftKeyboard.Back);
        Assert.Equal("한그", r.Box("name").EditText);
        Assert.Equal("", r.Vm.Name);

        r.Tap(SoftKeyboard.Done);
        Assert.Equal("한그", r.Vm.Name);
        Assert.False(r.S.Edit.IsEditing);
        Assert.False(r.Kb.IsOpen);
    }

    [Fact]
    public void Lang_shift_and_symbols()
    {
        var r = new Rig();
        r.Edit(20);
        r.Tap(SoftKeyboard.Lang);
        Assert.Equal(KbMode.English, r.Kb.Mode);
        r.Tap(SoftKeyboard.Shift);
        r.Tap("A");                    // Shift 중엔 대문자 키
        r.Tap("b");                    // 한 글자 뒤 해제
        r.Tap(SoftKeyboard.Sym);
        Assert.Equal(KbMode.Symbols, r.Kb.Mode);
        r.Tap("1");
        r.Tap(SoftKeyboard.Space);
        r.Tap(SoftKeyboard.Sym);       // 돌아가면 영문
        Assert.Equal(KbMode.English, r.Kb.Mode);
        Assert.Equal("Ab1 ", r.Box("name").EditText);
    }

    [Fact]
    public void Lang_switch_finishes_the_composing_syllable()
    {
        var r = new Rig();
        r.Edit(20);
        r.TapAll("ㄱㅏ");
        r.Tap(SoftKeyboard.Lang);
        r.Tap(SoftKeyboard.Lang);
        r.Tap("ㄱ");
        Assert.Equal("가ㄱ", r.Box("name").EditText); // 가 + 새 음절(받침으로 붙지 않음)
    }

    [Fact]
    public void Password_starts_in_english()
    {
        var r = new Rig();
        r.Edit(60);
        Assert.Equal(KbMode.English, r.Kb.Mode);
    }

    [Fact]
    public void Tapping_outside_cancels_and_is_not_forwarded()
    {
        var r = new Rig();
        r.Edit(20);
        r.TapAll("ㄱㅏ");
        r.Click(10, 210); // 저장 버튼 자리(200~240, 키보드 패널은 220 부터)
        Assert.False(r.S.Edit.IsEditing);
        Assert.Equal("", r.Vm.Name);
        Assert.Equal(0, r.Vm.Saves);
    }

    [Fact]
    public void Tapping_the_edited_input_keeps_editing()
    {
        var r = new Rig();
        r.Edit(20);
        var id = r.S.Edit.SessionId;
        r.Click(60, 20);
        Assert.True(r.S.Edit.IsEditing);
        Assert.Equal(id, r.S.Edit.SessionId);
    }

    [Fact]
    public void Numeric_keypad_follows_type()
    {
        var r = new Rig();
        r.Edit(100); // count: int, 0~100
        Assert.Equal(KbMode.Numeric, r.Kb.Mode);
        Assert.False(r.Key(SoftKeyboard.Dot).Enabled);
        Assert.True(r.Key(SoftKeyboard.Sign).Enabled);
        r.Tap(SoftKeyboard.Clear);
        r.TapAll("42");
        r.Tap(SoftKeyboard.Sign);
        Assert.Equal("-42", r.Box("count").EditText);
        r.Tap(SoftKeyboard.Sign);

        r.Tap(SoftKeyboard.Done);
        Assert.Equal(42, r.Vm.Count);
    }

    [Fact]
    public void Unsigned_and_real_keypads()
    {
        var r = new Rig();
        r.Edit(140); // pulses: uint
        Assert.False(r.Key(SoftKeyboard.Sign).Enabled);
        r.Tap(SoftKeyboard.Done);

        r.Edit(180); // temp: double
        Assert.True(r.Key(SoftKeyboard.Dot).Enabled);
        r.Tap(SoftKeyboard.Clear);
        r.TapAll("1");
        r.Tap(SoftKeyboard.Dot);
        r.Tap(SoftKeyboard.Dot); // 두 번째 점은 무시
        r.TapAll("5");
        Assert.Equal("1.5", r.Box("temp").EditText);
    }

    [Fact]
    public void Rejected_done_keeps_keyboard_with_error()
    {
        var r = new Rig();
        r.Edit(100);
        r.Tap(SoftKeyboard.Clear);
        r.TapAll("150");
        r.Tap(SoftKeyboard.Done);
        Assert.True(r.S.Edit.IsEditing);
        Assert.True(r.Kb.IsOpen);
        Assert.NotNull(r.S.Edit.Error);
        Assert.Equal(5, r.Vm.Count);

        r.Tap(SoftKeyboard.Back);
        r.Tap(SoftKeyboard.Back);
        r.Tap(SoftKeyboard.Done);
        Assert.Equal(1, r.Vm.Count);
    }

    [Fact]
    public void Pans_only_when_the_target_would_be_covered()
    {
        var top = new Rig();
        top.Edit(20);
        Assert.Equal(0, top.S.KeyboardPan);

        var low = new Rig("""
            <DvScreen style="padding:0; justify-content:flex-end">
              <DvInput value="{Bind Data.Name}"/>
            </DvScreen>
            """);
        low.Edit(H - 20);
        var kbTop = H - SoftKeyboard.PanelHeight(H);
        Assert.True(low.S.KeyboardPan > 0);
        Assert.Equal(low.S.Edit.Rect.Bottom + 12 - kbTop, low.S.KeyboardPan, 1);
    }

    [Fact]
    public void Physical_keys_feed_the_keyboard()
    {
        var r = new Rig();
        r.Edit(20);
        r.S.TextInput("한글");
        r.S.TextInput("ab");
        r.S.KeyInput(DvKey.Backspace);
        Assert.Equal("한글a", r.Box("name").EditText);
        r.S.KeyInput(DvKey.Enter);
        Assert.Equal("한글a", r.Vm.Name);

        r.Edit(20);
        r.S.TextInput("x");
        r.S.KeyInput(DvKey.Escape);
        Assert.Equal("한글a", r.Vm.Name);
        Assert.False(r.S.Edit.IsEditing);
    }

    [Fact]
    public void Native_mode_has_no_keyboard()
    {
        var r = new Rig(native: true);
        r.Edit(20);
        Assert.True(r.S.Edit.IsEditing);
        Assert.True(r.S.Keyboard is null || !r.S.Keyboard.IsOpen);
        r.S.TextInput("무시");
        Assert.Null(r.Box("name").EditText);
    }

    // 테마 null = DvTheme.Default — 키보드 색도 Default 테마 색
    [Fact]
    public void Keyboard_without_theme_uses_default_theme_colors()
    {
        var r = new Rig();
        r.Edit(20);
        Assert.True(r.Kb.IsOpen);
        var panel = r.Kb.Panel(W, H, r.Now);
        using var bmp = new SKBitmap((int)W, (int)H);
        using (var c = new SKCanvas(bmp))
            r.S.RenderTo(c, W, H);

        Assert.True(Devinno.UI.Styling.DvTheme.Default.TryGet("kbd-bg", out var raw));
        Assert.True(Devinno.UI.Styling.DvColor.TryParse(raw, out var bg));
        Assert.Equal(bg.ToSK(), bmp.GetPixel(1, (int)panel.Bottom - 1));
    }
}
