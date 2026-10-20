using System.Reflection;
using Devinno.UI.Binding;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using Devinno.UI.Interaction;
using SkiaSharp;
using Devinno.UI.App;

namespace Devinno.UI;

// 편집 세션 — 서피스당 하나. 대상 박스·세션 번호·입력 중 텍스트를 들고, DvEditState 를 레이아웃·렌더마다 맞춘다.
// 입력 중엔 세션 버퍼만 바뀌고 VM 은 커밋 때 1회만 쓴다(키마다 VM 에 쓰면 Esc 로 되돌릴 수 없다).
public sealed partial class DvSurface
{
    private IEditTarget? _editTarget;
    private int _editSessionCounter;
    private string _editText = "";
    private string? _editError;

    // 이 서피스 자신의 세션 상태
    private readonly DvEditState _local = new();

    internal object? Vm => _vm;
    internal IEditTarget? EditTarget => _editTarget; // 테스트용

    /// <summary>
    /// Gets the state of the current edit session (in this surface's coordinates). When not editing, <see cref="DvEditState.IsEditing"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>If this surface has its own session, that session is shown; otherwise the session of a cell surface (a bar or page of <see cref="App.DvApp"/>) is shown, translated to this surface's coordinates (and clipped to that cell).
    /// So the host only needs to look at this one property of the top-level surface.</para>
    /// <para>It is always the same instance; its values are updated on layout, render and edit method calls. A host using a native editor
    /// should read it right after <see cref="RenderTo(SKCanvas, float, float, long)"/> to fit the editor, and pass this state's <see cref="DvEditState.SessionId"/>
    /// to <see cref="SetEditText"/>, <see cref="CommitEdit"/> and <see cref="CancelEdit"/>.</para>
    /// </remarks>
    public DvEditState Edit { get; } = new();

    private DvSurface? _pubOwner; // Edit 가 비추는 세션의 주인(this 또는 칸 서피스)
    private int _pubInnerId;
    private int _pubCounter;

    /// <summary>
    /// Gets or sets whether the host handles text input with a native editor (OS text box, etc.). The default <see langword="false"/> uses the built-in virtual keyboard.
    /// </summary>
    /// <remarks>
    /// <para>When <see langword="true"/>, the virtual keyboard is not shown and the text being typed is not drawn inside the edit box (because the native editor is shown on top of it).
    /// The host must read <see cref="Edit"/> to place the editor and return results with <see cref="SetEditText"/>, <see cref="CommitEdit"/> and <see cref="CancelEdit"/>.</para>
    /// <para>Continuous entry in grid input cells (Enter = next row down, Tab/Shift+Tab = continue editing in the adjacent input column) works only when this is <see langword="true"/>.
    /// In virtual keyboard mode, committing ends editing.</para>
    /// <para>The same value is passed to cell surfaces (DvApp bars and pages) on layout. Change it on the UI thread while not editing.</para>
    /// </remarks>
    public bool UseNativeEditor { get; set; }

    /// <summary>
    /// Applies the whole text being typed to the session buffer. Call it every time the native editor's text changes. Does not write to the VM.
    /// </summary>
    /// <remarks>
    /// The previous commit rejection reason (<see cref="DvEditState.Error"/>) is cleared. Does nothing if <paramref name="sessionId"/> differs from the current session or if not editing.
    /// Passing line breaks as <c>\n</c> is recommended (they are written to the VM as is).
    /// </remarks>
    /// <param name="sessionId">The <see cref="DvEditState.SessionId"/> read when editing started.</param>
    /// <param name="text">The whole current text in the editor (not the delta). <see langword="null"/> is treated as an empty string.</param>
    public void SetEditText(int sessionId, string text)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { SetEditTextCore(sessionId, text); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private void SetEditTextCore(int sessionId, string text)
    {
        if (!Route(sessionId, out var owner, out var inner))
            return;

        if (owner != this)
            owner.SetEditText(inner, text);
        else if (_editTarget is not null)
        {
            _editText = text ?? "";
            _editError = null;
            SyncEditState();
            _needsRender = true;
        }

        PublishEdit();
    }

    /// <summary>
    /// Ends editing and writes the session buffer's value to the VM. Writes only if the value changed, and only once.
    /// </summary>
    /// <remarks>
    /// <para>Writes the text last passed with <see cref="SetEditText"/>. Pass the editor's final text with <see cref="SetEditText"/> right before committing.</para>
    /// <para>Returns <see langword="false"/> if validation (number format, <c>min</c>/<c>max</c>) or an <c>onbeforecommit</c> handler rejects it.
    /// A write failure where the VM property's setter throws is the same as a rejection (reason = the <c>dv.input.write-failed</c> text; the exception is reported through <see cref="HandlerError"/>).
    /// In that case, if <paramref name="exit"/> is <see cref="DvEditExit.FocusLost"/>, the session closes without writing to the VM; for any other reason the session stays open and
    /// <see cref="DvEditState.Error"/> is filled with the rejection reason (an error border is drawn on the edit box).</para>
    /// <para>On success the session closes and <see cref="DvEditState.IsEditing"/> becomes <see langword="false"/>. However, for a grid input cell in native editor mode,
    /// if the exit is <see cref="DvEditExit.Enter"/>, <see cref="DvEditExit.Tab"/> or <see cref="DvEditExit.ShiftTab"/> and there is a next input cell to edit, a new session (with a new <see cref="DvEditState.SessionId"/>) opens in that cell right away.
    /// <see cref="DvEditExit.Up"/> and <see cref="DvEditExit.Down"/> only commit without moving the cell.</para>
    /// </remarks>
    /// <param name="sessionId">The <see cref="DvEditState.SessionId"/> read when editing started.</param>
    /// <param name="exit">The reason editing ended (the key pressed, or focus lost).</param>
    /// <returns><see langword="true"/> if it wrote to the VM, or ended normally because nothing changed. <see langword="false"/> if rejected or if <paramref name="sessionId"/> is not the current session.</returns>
    public bool CommitEdit(int sessionId, DvEditExit exit)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { return CommitEditCore(sessionId, exit); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private bool CommitEditCore(int sessionId, DvEditExit exit)
    {
        if (!Route(sessionId, out var owner, out var inner))
            return false;

        var ok = owner != this ? owner.CommitEdit(inner, exit) : CommitCurrent(exit);
        PublishEdit();
        return ok;
    }

    /// <summary>
    /// Cancels editing and closes the session. Does not write to the VM (Esc key, etc.).
    /// </summary>
    /// <param name="sessionId">The <see cref="DvEditState.SessionId"/> read when editing started. Does nothing if it differs from the current session.</param>
    public void CancelEdit(int sessionId)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { CancelEditCore(sessionId); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private void CancelEditCore(int sessionId)
    {
        if (!Route(sessionId, out var owner, out var inner))
            return;

        if (owner != this)
            owner.CancelEdit(inner);
        else
            EndEdit();
        PublishEdit();
    }

    // 바깥 세션 번호 → 실제 주인 서피스와 그 안의 세션 번호
    private bool Route(int sessionId, out DvSurface owner, out int inner)
    {
        owner = this;
        inner = 0;
        if (!Edit.IsEditing || sessionId != Edit.SessionId || _pubOwner is null)
            return false;
        owner = _pubOwner;
        inner = _pubInnerId;
        return true;
    }

    /// <summary>
    /// Syncs <see cref="Edit"/> to the current session (own session first, otherwise a cell surface's). If the owner or the owner's session changes, a new outer session number is used.
    /// A cell session is offset by the cell position and clipped to the cell area.
    /// </summary>
    private void PublishEdit()
    {
        DvSurface? owner = null;
        DvEditState? src = null;
        var slotRect = SKRect.Empty;
        if (_local.IsEditing)
            (owner, src) = (this, _local);
        else
        {
            foreach (var s in _slots)
            {
                if (!s.Child.Edit.IsEditing)
                    continue;
                (owner, src, slotRect) = (s.Child, s.Child.Edit, s.Rect); // 칸의 자식 세션(한 번에 하나 — 다른 칸을 누르면 먼저 커밋)
                break;
            }
        }

        if (owner is null || src is null)
        {
            _pubOwner = null;
            Edit.End();
            return;
        }

        if (!Edit.IsEditing || !ReferenceEquals(owner, _pubOwner) || src.SessionId != _pubInnerId)
        {
            _pubOwner = owner;
            _pubInnerId = src.SessionId;
            Edit.Begin(++_pubCounter, src.InitialText);
        }

        var snap = src.ToSnapshot();
        if (!ReferenceEquals(owner, this))
        {
            var (dx, dy) = (slotRect.Left, slotRect.Top);
            var rect = new SKRect(snap.Rect.Left + dx, snap.Rect.Top + dy, snap.Rect.Right + dx, snap.Rect.Bottom + dy);
            var clip = new SKRect(snap.Clip.Left + dx, snap.Clip.Top + dy, snap.Clip.Right + dx, snap.Clip.Bottom + dy);
            clip = SKRect.Intersect(clip, slotRect);
            snap = snap with { Rect = rect, Clip = clip.IsEmpty ? SKRect.Empty : clip };
        }

        Edit.Apply(snap);
    }

    /// <summary>Commits any remaining edit (FocusLost) — right before a screen switch, or when another area is pressed.</summary>
    internal void CommitPendingEdit()
    {
        Window.ClosePopupsOwnedBy(this); // 화면 전환 — 이 화면이 띄운 드롭다운도 닫는다
        if (_editTarget is not null)
            CommitCurrent(DvEditExit.FocusLost);
        foreach (var s in _slots)
            s.Child.CommitPendingEdit();
        PublishEdit();
    }

    /// <summary>
    /// Ends any remaining edit without writing and closes popups opened by this surface — for dialog close (the result is already decided). Called before detaching from the cell (the commit in <see cref="DetachFromParent"/>) to clear the session.
    /// </summary>
    internal void CancelPendingEdit()
    {
        Window.ClosePopupsOwnedBy(this);
        EndEdit();
        foreach (var s in _slots)
            s.Child.CancelPendingEdit();
        PublishEdit();
    }

    /// <summary>
    /// Starts editing the first editable <c>DvInput</c> (in tree order) of this surface — so the built-in input box starts editing as soon as it appears. On the input thread, after layout.
    /// </summary>
    internal void BeginEditFirstInput()
    {
        if (_box is not null && FirstEditableInput(_box) is { } box)
            BeginEdit(box);
    }

    private static LayoutBox? FirstEditableInput(LayoutBox b)
    {
        if (b.Node.Tag == "DvInput" && InputDisplay.IsEditable(b))
            return b;
        foreach (var c in b.Children)
        {
            if (FirstEditableInput(c) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>Called with the exit reason right after an edit on this surface is accepted and ends (written to the VM, or nothing changed) — the built-in input box's "Done (Enter) = OK".</summary>
    internal Action<DvEditExit>? EditCommitted { get; set; }

    private void BeginEdit(LayoutBox box)
    {
        if (_editTarget is BoxEditTarget { Box: var cur } && ReferenceEquals(box, cur))
            return;
        BeginEdit(new BoxEditTarget(this, box));
    }

    private void BeginEdit(IEditTarget target)
    {
        if (_editTarget is { } cur && cur.SameAs(target))
            return; // 같은 대상 재누름 — 세션 유지

        if (_editTarget is not null)
            CommitCurrent(DvEditExit.FocusLost);

        _editTarget = target;
        _editError = null;
        _local.Begin(++_editSessionCounter, target.InitialText);
        _editText = _local.InitialText;
        target.Reveal();
        SyncEditState();
        _needsRender = true;
    }

    private bool CommitCurrent(DvEditExit exit)
    {
        if (_editTarget is not { } t)
            return false;

        if (t.TryWrite(_editText, out var error))
        {
            var rebuild = t.RebuildAfterWrite;
            // 연속 입력 — 네이티브 편집기만(가상 키보드·터치 = 커밋 후 종료, spec 3d-1 §4)
            var next = UseNativeEditor ? t.Next(exit) : null;
            EndEdit();
            if (rebuild)
                Invalidate(); // 쓴 값으로 다시 빌드
            if (next is not null)
                BeginEdit(next); // 화면 밖이면 Reveal 이 스크롤
            EditCommitted?.Invoke(exit);
            return true;
        }

        if (exit == DvEditExit.FocusLost)
        {
            if (t is PopupFieldTarget)
                _blockCommitUntilUp = true; // 어댑터가 포커스 잃음을 누름보다 먼저 보내도 이어지는 확인은 커밋 안 함
            EndEdit(); // 원래 값 그대로 닫음
            return false;
        }

        _editError = error;
        SyncEditState();
        _needsRender = true;
        return false;
    }

    private void EndEdit()
    {
        if (_editTarget is not { } t)
            return;

        t.Ended();
        _editTarget = null;
        _editText = "";
        _editError = null;
        _local.End();
        _needsRender = true;
        PublishEdit();
    }

    // 편집을 시작할 때 대상이 다 보이도록 감싼 스크롤 상자를 안쪽부터 움직인다(넘치면 위·왼쪽 기준)
    internal void ScrollIntoView(LayoutBox target)
    {
        if (_box is not null && EditGeometry.TryLocate(_box, target, out var r, out _))
            ScrollIntoView(target, r);
    }

    /// <summary>Scrolls the enclosing scroll boxes so that a rectangle inside <paramref name="target"/> (in <see cref="EditGeometry.TryLocate"/> coordinates, e.g. a grid cell) is fully visible.
    /// Scroll boxes in which it is already fully visible are not moved.</summary>
    internal void ScrollIntoView(LayoutBox target, SKRect targetRect)
    {
        if (_box is null || !EditGeometry.TryLocate(_box, target, out var start, out _))
            return;

        var chain = new List<LayoutBox>();
        if (!Ancestors(_box, target, chain))
            return;

        // 대상 상자 기준 오프셋 — 바깥 스크롤이 움직이면 상자와 함께 움직인다
        var ox = targetRect.Left - start.Left;
        var oy = targetRect.Top - start.Top;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var sc = chain[i];
            if (!sc.IsScroller ||
                !EditGeometry.TryLocate(_box, target, out var tb, out _) ||
                !EditGeometry.TryLocate(_box, sc, out var vp, out _))
                continue;

            var r = SKRect.Create(tb.Left + ox, tb.Top + oy, targetRect.Width, targetRect.Height);

            vp = new SKRect(vp.Left, vp.Top, vp.Right - sc.GutterX, vp.Bottom - sc.GutterY);
            if (r.Bottom > vp.Bottom)
                sc.ScrollY += Math.Min(r.Bottom - vp.Bottom, r.Top - vp.Top);
            else if (r.Top < vp.Top)
                sc.ScrollY -= vp.Top - r.Top;
            if (r.Right > vp.Right)
                sc.ScrollX += Math.Min(r.Right - vp.Right, r.Left - vp.Left);
            else if (r.Left < vp.Left)
                sc.ScrollX -= vp.Left - r.Left;
            sc.ClampScroll();
        }
    }

    private static bool Ancestors(LayoutBox box, LayoutBox target, List<LayoutBox> chain)
    {
        if (ReferenceEquals(box, target))
            return true;

        chain.Add(box);
        foreach (var c in box.Children)
        {
            if (Ancestors(c, target, chain))
                return true;
        }

        chain.RemoveAt(chain.Count - 1);
        return false;
    }

    // 재빌드 후 같은 대상으로 — 사라졌으면 그때까지 입력한 값을 커밋하고 세션을 버린다
    private void RemapEdit(LayoutBox old, LayoutBox now)
    {
        if (_editTarget is not { } t || t.Remap(old, now))
            return;

        if (t.TryWrite(_editText, out _))
            _dirty = true;
        _editTarget = null;
        _editText = "";
        _editError = null;
        _local.End();
    }

    /// <summary>Applies the target's current position and style to <see cref="Edit"/> (increments Version if anything changed).</summary>
    private void SyncEditState()
    {
        if (_editTarget is not { } t)
            return;

        if (!t.TryLocate(out var rect, out var clip))
        {
            EndEdit();
            return;
        }

        if (clip.IsEmpty)
        {
            CommitCurrent(DvEditExit.FocusLost); // 스크롤로 완전히 가려짐 — 입력한 값은 살리고 닫는다
            return;
        }

        t.Show(UseNativeEditor ? null : _editText, UseNativeEditor, _editError);

        var look = t.Look(_local.InitialText + _editText);
        _local.Apply(new DvEditState.Snapshot(
            rect, clip, t.Kind, t.Multiline, t.Placeholder,
            look.FontFamily, look.FontSize, look.Bold, look.TextColor, look.Background, look.Padding, look.Align,
            t.Numeric, _editError));
        PublishEdit();
    }

    internal SKColor Token(string name, SKColor fallback)
        => (_theme ?? DvTheme.Default).TryGet(name, out var raw) && DvColor.TryParse(raw, out var c) ? c.ToSK() : fallback;
}
