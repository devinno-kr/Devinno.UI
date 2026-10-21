using Devinno.UI.Editing;
using Devinno.UI.Interaction;
using SkiaSharp;
using Devinno.UI.App;

namespace Devinno.UI;

// 코어 가상 키보드 — 네이티브 편집기를 쓰지 않는 창(최상위) 서피스에서 편집 중일 때 하단에 띄운다.
// 떠 있는 동안은 모달: 키보드 밖을 누르면 취소하고 그 누름은 아래로 전달하지 않는다(편집 중인 상자 위는 무시).
public sealed partial class DvSurface
{
    private SoftKeyboard? _kb;
    private string? _kbPressed;   // 누르고 있는 키 id
    private bool _kbSwallow;      // 키보드 밖 누름 — 떼기까지 삼킨다

    /// <summary>True if this is a child screen of a slot (a DvApp region/page); only the window (root) surface shows the keyboard. Set by the window surface when it attaches this to a slot.</summary>
    internal bool IsChildSurface { get; set; }

    /// <summary>For tests.</summary>
    internal SoftKeyboard? Keyboard => _kb;

    private bool KeyboardWanted => !UseNativeEditor && !IsChildSurface && Edit.IsEditing;

    // 세션 시작·전환·종료에 맞춰 키보드를 열고 닫는다(렌더·입력 직전마다 호출)
    private void SyncKeyboard()
    {
        var now = Clock();
        if (KeyboardWanted)
        {
            _kb ??= new SoftKeyboard();
            if (!_kb.IsOpen || _kb.SessionId != Edit.SessionId)
                _kb.Open(Edit, now, string.Equals(_strings?.Language, "ko", StringComparison.OrdinalIgnoreCase));
            _kbLastTarget = Edit.Rect;
        }
        else if (_kb is { IsOpen: true })
        {
            _kb.Close(now);
            _kbPressed = null;
        }
    }

    private bool KeyboardShown => _kb is not null && _kb.IsShown(Clock());

    // 열려 있거나(올라오는 중 포함) 아직 보이는 동안은 입력을 키보드가 가져간다
    private bool IsKbModal => _kb is { IsOpen: true } || KeyboardShown;

    /// <summary>How far (px) the screen was raised for the keyboard; while the keyboard slides down, the screen comes down with it, based on the last target.</summary>
    internal float KeyboardPan => _kb is null ? 0f : _kb.Pan(Edit.IsEditing ? Edit.Rect : _kbLastTarget, _w, _h, Clock());

    private SKRect _kbLastTarget;

    private bool KeyboardPointerDown(SKPoint p)
    {
        SyncKeyboard();
        if (!IsKbModal)
            return false; // 열려 있으면 올라오는 중에도 모달

        var now = Clock();
        if (_kb!.Panel(_w, _h, now).Contains(p.X, p.Y))
        {
            _kbPressed = _kb.Hit(p, _w, _h, now)?.Id;
            _needsRender = true;
            return true;
        }

        var target = Edit.Rect;
        target.Offset(0, -KeyboardPan);
        if (!(Edit.IsEditing && target.Contains(p.X, p.Y)))
            CancelEdit(Edit.SessionId); // 키보드 밖 = 취소
        _kbSwallow = true;
        return true;
    }

    private bool KeyboardPointerMove(SKPoint p)
    {
        if (_kbSwallow)
            return true;
        if (_kbPressed is null)
            return IsKbModal;

        var hit = _kb!.Hit(p, _w, _h, Clock())?.Id;
        if (hit != _kbPressed)
        {
            _kbPressed = null; // 키 밖으로 끌면 누름 취소
            _needsRender = true;
        }

        return true;
    }

    private bool KeyboardPointerUp(SKPoint p)
    {
        if (_kbSwallow)
        {
            _kbSwallow = false;
            return true;
        }

        if (_kbPressed is not { } id)
            return IsKbModal;

        _kbPressed = null;
        _needsRender = true;
        if (_kb!.Hit(p, _w, _h, Clock())?.Id == id)
            Apply(_kb.Press(id));
        return true;
    }

    private void Apply(KbResult r)
    {
        if (_kb is null || !Edit.IsEditing)
            return;

        var id = Edit.SessionId;
        if (r is KbResult.TextChanged or KbResult.Commit)
            SetEditText(id, _kb.Text);
        if (r == KbResult.Commit)
            CommitEdit(id, DvEditExit.Enter); // 거절이면 세션·키보드 유지(미리보기 줄에 사유)

        SyncKeyboard();
        _needsRender = true;
    }

    /// <summary>
    /// Passes an editing key from the physical keyboard in virtual keyboard mode. Pass character keys to <see cref="TextInput"/>.
    /// </summary>
    /// <remarks>
    /// <para>Handling: <see cref="DvKey.Enter"/> = Done key (commits; if rejected, the session and keyboard stay open), <see cref="DvKey.Backspace"/> = deletes one character (one jamo while composing),
    /// <see cref="DvKey.Escape"/> = cancels the edit, <see cref="DvKey.Tab"/>/<see cref="DvKey.ShiftTab"/> = commits with the matching reason.</para>
    /// <para>Works only while the virtual keyboard is open and an edit is in progress. Ignored if <see cref="UseNativeEditor"/> is <see langword="true"/> or no edit is in progress.
    /// In native editor mode, call <see cref="CommitEdit"/>/<see cref="CancelEdit"/> directly instead of this method. Call on the UI thread.</para>
    /// </remarks>
    /// <param name="key">The pressed key.</param>
    public void KeyInput(DvKey key)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { KeyInputCore(key); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private void KeyInputCore(DvKey key)
    {
        SyncKeyboard();
        if (_kb is not { IsOpen: true } || !Edit.IsEditing)
            return;

        var id = Edit.SessionId;
        switch (key)
        {
            case DvKey.Enter:
                Apply(_kb.Press(SoftKeyboard.Done));
                break;
            case DvKey.Backspace:
                Apply(_kb.Press(SoftKeyboard.Back));
                break;
            case DvKey.Escape:
                CancelEdit(id);
                SyncKeyboard();
                break;
            case DvKey.Tab or DvKey.ShiftTab:
                SetEditText(id, _kb.Text);
                CommitEdit(id, key == DvKey.Tab ? DvEditExit.Tab : DvEditExit.ShiftTab);
                SyncKeyboard();
                break;
        }

        _needsRender = true;
    }

    /// <summary>
    /// Passes characters typed on the physical keyboard in virtual keyboard mode (including Korean text completed by the OS IME).
    /// </summary>
    /// <remarks>
    /// Appends to the session buffer as if the virtual keyboard had been pressed. Control characters are discarded (send Enter, Backspace, etc. through <see cref="KeyInput"/>).
    /// In numeric editing (numeric keypad), only digits, the decimal point (once, for floating-point types), and <c>-</c> (toggles the sign, for signed types) are accepted.
    /// Works only while the virtual keyboard is open and an edit is in progress; otherwise ignored. Call on the UI thread.
    /// </remarks>
    /// <param name="text">The typed string. It may contain several characters at once. <see langword="null"/> and empty strings are ignored.</param>
    public void TextInput(string text)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { TextInputCore(text); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private void TextInputCore(string text)
    {
        SyncKeyboard();
        if (_kb is not { IsOpen: true } || !Edit.IsEditing || string.IsNullOrEmpty(text))
            return;

        var r = KbResult.None;
        foreach (var c in text)
        {
            if (!char.IsControl(c) && _kb.Type(c) == KbResult.TextChanged)
                r = KbResult.TextChanged;
        }

        Apply(r);
    }

    private void PaintKeyboard(SKCanvas canvas, float w, float h)
    {
        if (_kb is null)
            return;
        _kb.Paint(canvas, _theme, _strings, Edit, w, h, Clock(), _kbPressed);
    }
}
