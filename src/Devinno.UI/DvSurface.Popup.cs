using Devinno.UI.Grid;
using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI;

// 오버레이(팝업) 스택 — 창(최상위) 서피스만 가진다. 맨 위 팝업만 입력을 받고, 그리기 = 본문 → 팝업 → 가상 키보드.
// 칸 서피스(DvApp 영역·페이지)의 DvSelect 도 창 서피스에 띄운다(칸 좌표 → 창 좌표). 이번에는 드롭다운만(8c 다이얼로그·8d 팝업이 이어서 올라감).
public sealed partial class DvSurface
{
    private readonly List<IPopup> _popups = new();
    private bool _popupSwallow; // 팝업 밖 누름 — 떼기까지 삼킨다
    private bool _blockCommitUntilUp; // 칸 커밋 거절 — 이 누름의 확인은 커밋하지 않는다

    /// <summary>If this is a slot's child surface (DvApp area/page), the owning window surface (linked by the window during layout, cleared when removed from the slot).</summary>
    internal DvSurface? ParentSurface { get; private set; }

    private DvSurface Window => ParentSurface ?? this;

    /// <summary>Topmost popup (for tests). Always null on a slot surface, because the window surface shows popups.</summary>
    internal IPopup? TopPopup => _popups.Count > 0 ? _popups[^1] : null;

    /// <summary>Topmost dropdown (for tests).</summary>
    internal DropdownPopup? Dropdown => TopPopup as DropdownPopup;

    /// <summary>Whether the popup is still on the stack (used to locate field edit targets).</summary>
    internal bool IsPopupOpen(IPopup p) => _popups.Contains(p);

    private void OpenPopup(IPopup p)
    {
        while (_popups.Count > 0)
            ClosePopup(_popups[^1]);

        p.Slot.Editing = true; // 열림 표시(포커스 링·위 화살표)
        _blockCommitUntilUp = false;
        _popups.Add(p);
        PlacePopups();
        _needsRender = true;
    }

    /// <summary>Stacks a popup on top without closing the open ones (e.g. a date picker over the filter popup).</summary>
    internal void OpenPopupOver(IPopup p)
    {
        p.Slot.Editing = true;
        _blockCommitUntilUp = false;
        _popups.Add(p);
        PlacePopups();
        _needsRender = true;
    }

    // 빈 칸의 선택기 시작값: 시작 칸 = 오늘 00:00:00, 끝 칸 = 오늘 23:59:59
    private void OpenFilterDate(GridFilterPopup fp, int field)
    {
        var today = Editing.PickerClock.Today;
        OpenPopupOver(CreatePicker(new GridFilterDateSlot(fp, field), color: false, field == 1 ? today : today.AddDays(1).AddSeconds(-1)));
    }

    private void ClosePopup(IPopup d)
    {
        // 위에 쌓인 팝업(필터 팝업 위 날짜 선택기)은 아래 팝업과 함께 닫는다 — 죽은 팝업에 쓰는 선택기가 남지 않게
        var at = _popups.IndexOf(d);
        for (var i = _popups.Count - 1; i > at && at >= 0; i--)
            ClosePopup(_popups[i]);
        if (_editTarget is PopupFieldTarget f && ReferenceEquals(f.Popup, d))
            EndEdit(); // 팝업과 함께 칸 편집도 끝(쓰지 않음)
        d.Slot.Editing = false;
        _blockCommitUntilUp = false; // 누름~떼기 사이에 닫혀도 남지 않게
        _popups.Remove(d);
        _needsRender = true;
        d.Owner._needsRender = true;
    }

    /// <summary>Repositions popups to their anchors. Closes a popup whose anchor is gone or fully hidden.</summary>
    private void PlacePopups()
    {
        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            var d = _popups[i];
            if (AnchorRect(d) is { } rect)
                d.Place(rect, _w, _h);
            else
                ClosePopup(d);
        }
    }

    private SKRect? AnchorRect(IPopup d)
    {
        if (ReferenceEquals(d.Owner, this))
            return d.Slot.TryLocate(out var r, out var clip) && !clip.IsEmpty ? r : null;

        // 칸의 자식 서피스 — 지금 칸에 붙어 있어야 하고, 칸 위치만큼 옮긴다
        if (SlotOf(d.Owner) is not { } slot ||
            !d.Slot.TryLocate(out var br, out var bclip) || bclip.IsEmpty)
            return null;
        br.Offset(slot.Rect.Left, slot.Rect.Top);
        return br;
    }

    // 재빌드 후 앵커를 새 트리의 같은 박스로(사라졌으면 닫음) — owner 서피스가 자기 Remap 에서 부른다
    private void RemapPopups(LayoutBox old, LayoutBox now)
    {
        var w = Window;
        for (var i = w._popups.Count - 1; i >= 0; i--)
        {
            var d = w._popups[i];
            if (!ReferenceEquals(d.Owner, this))
                continue;
            if (d.Slot.Remap(old, now))
                d.Slot.Editing = true;
            else
                w.ClosePopup(d);
        }
    }

    private void ClosePopupsOwnedBy(DvSurface owner)
    {
        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_popups[i].Owner, owner))
                ClosePopup(_popups[i]);
        }
    }

    // ── 입력(창 서피스): 팝업이 있으면 먼저 가져간다
    private bool PopupPointerDown(SKPoint p)
    {
        if (_popups.Count == 0)
            return false;

        var top = _popups[^1];
        // 칸 편집 중(네이티브 모드) 칸 밖 누름 — 팝업 안 = 칸 커밋, 바깥 = 칸 취소
        if (_editTarget is PopupFieldTarget f && ReferenceEquals(f.Popup, top) &&
            f.TryLocate(out var fr, out _) && !fr.Contains(p.X, p.Y))
        {
            if (top.Bounds.Contains(p.X, p.Y))
            {
                if (!CommitCurrent(DvEditExit.FocusLost))
                    _blockCommitUntilUp = true;
            }
            else
                EndEdit(); // 아래에서 팝업도 닫힘
        }

        if (top.Bounds.Contains(p.X, p.Y))
            top.Down(p);
        else
        {
            ClosePopup(top); // 바깥 누름 = 닫기, 아래로 전달하지 않음(같은 DvSelect 재누름도 닫기)
            _popupSwallow = true;
        }

        _needsRender = true;
        return true;
    }

    private bool PopupPointerMove(SKPoint p)
    {
        if (_popupSwallow)
            return true;
        if (_popups.Count == 0)
            return false;
        _popups[^1].Move(p);
        _needsRender = true;
        return true;
    }

    private bool PopupPointerUp(SKPoint p)
    {
        if (_popupSwallow)
        {
            _popupSwallow = false;
            return true;
        }

        if (_popups.Count == 0)
            return false;

        var top = _popups[^1];
        switch (top.Up(p))
        {
            case PopupResult.Cancel:
                ClosePopup(top);
                break;
            case PopupResult.Commit:
                if (_blockCommitUntilUp)
                    break;
                if (top.Owner.CommitPopup(top))
                    ClosePopup(top);
                break;
            case PopupResult.EditField:
                var field = top switch { DateTimePopup dp => dp.PendingField, GridFilterPopup fp => fp.PendingField, _ => 0 };
                if (top is GridFilterPopup { Kind: GridFilterKind.Date } fd)
                {
                    OpenFilterDate(fd, field); // date 칸은 편집 세션이 아니라 위에 쌓는 날짜 선택기
                    break;
                }

                BeginEdit(new PopupFieldTarget(this, top, field)); // 창 서피스의 세션
                SyncKeyboard(); // 가상 키보드 모드 — 누른 시각부터 올라온다
                break;
        }

        _blockCommitUntilUp = false;

        _needsRender = true;
        return true;
    }

    private bool PopupPointerWheel(SKPoint p, float delta)
    {
        if (_popups.Count == 0)
            return false;
        var top = _popups[^1];
        if (top.Bounds.Contains(p.X, p.Y))
            top.Wheel(delta);
        _needsRender = true;
        return true;
    }

    private void PaintPopups(SKCanvas canvas)
    {
        PlacePopups();
        foreach (var d in _popups)
            d.Paint(canvas, _theme, _strings);
    }

    /// <summary>Creates a picker popup. If the value is missing or MinValue, it opens at <paramref name="emptyStart"/>, or if that is not given, at the current time (seconds 0).</summary>
    private IPopup CreatePicker(IValueSlot slot, bool color, DateTime? emptyStart = null)
    {
        if (color)
        {
            var hadColor = PickerValue.TryToColor(slot.Value, out var c);
            return new ColorPopup(this, slot, new ColorPickerState(hadColor ? c : SKColors.White));
        }

        var had = PickerValue.TryToDateTime(slot.Value, out var dt) && dt != DateTime.MinValue; // MinValue(기본값) = 값 없음
        if (!had)
        {
            var now = Editing.PickerClock.Now();
            dt = emptyStart ?? new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        }

        return new DateTimePopup(this, slot, new DateTimePickerState(dt, slot.PickerMode, slot.PickerSeconds), dt, had);
    }

    /// <summary>Commits a popup (on the owner surface). True means close it; false for popup kinds that stay open when the commit is rejected.</summary>
    private bool CommitPopup(IPopup p)
    {
        switch (p)
        {
            case DropdownPopup d:
                // DvSelect 는 거절(쓰기 실패 포함)해도 닫는다(기존 동작). 그리드 셀은 거절 = 열린 채(spec 3d-1 §4).
                return CommitSelect(d.Slot, d.Picked) is not (CommitResult.Rejected or CommitResult.Failed) || d.Slot is BoxSlot;
            case DateTimePopup t:
                if (t.Slot.ValueType is not { } ttype)
                    return true;
                return CommitPicker(t.Slot, PickerValue.FromDateTime(t.State.Result(t.Original), ttype), t.HadValue);
            case ColorPopup c:
                if (c.Slot.ValueType is not { } ctype)
                    return true;
                var rgb = c.State.Rgb;
                if (c.HadValue && PickerValue.TryToColor(c.Slot.Value, out var cur) &&
                    cur.Red == rgb.Red && cur.Green == rgb.Green && cur.Blue == rgb.Blue)
                    return true; // RGB 가 같으면 표기(대소문자·알파)가 달라도 쓰지 않음
                return CommitPicker(c.Slot, PickerValue.FromColor(rgb, c.Slot.Value, ctype), hadValue: false);
            case GridFilterPopup f:
                // 열 번호가 아니라 field 경로로 찾는다(재빌드로 열 순서가 바뀌어도 연 열에 건다)
                var fc = FilterTarget(f.Grid, f.CellSlot.Column, f.Column.Reader.Path, f.Kind);
                if (fc < 0)
                    return true; // 재빌드로 열이 사라졌거나 필터 종류가 바뀜 — 적용하지 않고 닫음
                if (!f.TryBuild(_strings, out var cond, out var errField, out var err))
                {
                    f.FieldEdit = (errField, null, false, err); // 칸 오류 표시, 팝업 유지
                    return false;
                }

                f.Grid.SetFilter(fc, cond);
                Repaint();
                return true;
            default:
                return true;
        }
    }

    /// <summary>Item selection. Converts to the property type and writes to the slot (the slot handles same-value checks and the gate).</summary>
    private static CommitResult CommitSelect(IValueSlot slot, int index)
    {
        var options = slot.Options;
        if (index < 0 || index >= options.Count || !SelectValue.TryConvert(options[index].Value, slot.ValueType, out var value))
            return CommitResult.Same;
        return slot.Write(value);
    }

    /// <summary>Picker confirm. If the value is unchanged, closes without writing; returns false (popup stays open) if rejected or the write fails.</summary>
    private static bool CommitPicker(IValueSlot slot, object value, bool hadValue)
    {
        if (hadValue && Equals(value, slot.Value))
            return true;
        return slot.Write(value) is not (CommitResult.Rejected or CommitResult.Failed);
    }
}
