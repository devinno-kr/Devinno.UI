using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using SkiaSharp;
using Devinno.UI.App;

namespace Devinno.UI;

// overflow:scroll 입력 — 휠, 드래그(임계 거리 넘으면 스크롤로 판정하고 자식 press 취소), 손잡이 드래그, 관성.
// 관성은 배경 스레드가 아니라 렌더 시각으로 위치를 계산한다(배경 스레드에서 오프셋을 바꾸면 그리기 스레드와 경합한다).
public sealed partial class DvSurface
{
    /// <summary>Clock (ms) for inertia and velocity calculation. Replaced with a fixed clock in tests.</summary>
    internal Func<long> Clock { get; init; } = () => Environment.TickCount64;

    private const float WheelStep = 48f;
    private const long FlingWindowMs = 100;      // 손을 떼기 직전 이 구간의 이동으로 속도 계산
    private const double FlingMinSpeed = 50;     // px/s 미만이면 관성 없음
    private const double FlingStopSpeed = 5;     // px/s 까지 감속하면 정지
    private const double Decel = 0.996;          // ms 당 속도 유지율
    private static readonly double DecelLn = Math.Log(Decel);

    private sealed class ScrollGesture
    {
        public required LayoutBox Box;
        public SKPoint Down;
        public float StartX, StartY;
        public bool Dragging;
        public readonly List<(long T, SKPoint P)> Samples = new();
    }

    private sealed class ThumbDrag
    {
        public required LayoutBox Box;
        public bool Vertical;
        public SKPoint Down;
        public float Start;
    }

    private sealed class Fling
    {
        public required LayoutBox Box;
        public long T0;
        public float X0, Y0;
        public double VX, VY;   // px/s, 내용 이동 방향(+ = 오프셋 증가)
        public double DurationMs;
    }

    private ScrollGesture? _scroll;
    private ThumbDrag? _thumb;
    private Fling? _fling;

    /// <summary>
    /// Delivers mouse wheel rotation. Among the scroll boxes (<c>overflow:scroll</c>) under the pointer, starting from the innermost, the first box that can move further in that direction
    /// is scrolled vertically by 48 per notch (an inner box that has reached its end is skipped and the outer box moves).
    /// </summary>
    /// <remarks>
    /// Horizontal wheels are not supported. Ignored while the virtual keyboard is shown; while a popup (dropdown, etc.) is open, the wheel over the popup goes to the popup and the wheel outside it is discarded.
    /// Scrolling works inside disabled areas too (same as HTML). After scrolling, hover moves to the box that is newly under the pointer (the grid hover row is turned off until the next pointer move). Call it on the UI thread.
    /// </remarks>
    /// <param name="p">The pointer position (same coordinate system as <see cref="RenderTo(SKCanvas, float, float, long)"/>).</param>
    /// <param name="delta">The number of wheel notches. Positive when rolled up (toward the start of the content). Ignored if 0. Fractions (high-resolution wheels) are allowed.</param>
    public void PointerWheel(SKPoint p, float delta)
    {
        using var sync = SyncScope.Enter(this); // DvApp 루트면 입력 스레드 동기화 컨텍스트(D15)
        Composer?.NoteInput(); // 무입력 감지 — 루트에만 Composer 가 있다
        var prevRead = HandlerErrors.EnterRead(_reads); // 입력 처리 중 VM 읽기 예외도 이 화면의 HandlerError 로
        try { PointerWheelCore(p, delta); }
        finally { HandlerErrors.ExitRead(prevRead); }
    }

    private void PointerWheelCore(SKPoint p, float delta)
    {
        if (IsKbModal || PopupPointerWheel(p, delta))
            return; // 키보드·팝업이 떠 있는 동안은 모달

        var blocked = Blocked;
        if ((blocked ? ModalSlotAt(p) : SlotAt(p)) is { } slot) // 다이얼로그가 있으면 맨 위 다이얼로그 칸 안만, 밖은 삼킨다
        {
            slot.Child.PointerWheel(ToChild(p, slot), delta);
            return;
        }

        if (_box is null || delta == 0 || blocked)
            return;

        var path = HitTester.HitPath(_box, p);
        for (var i = path.Count - 1; i >= 0; i--)
        {
            var b = path[i].Box;
            if (!b.IsScroller || b.MaxScrollY <= 0)
                continue;
            var next = Math.Clamp(b.ScrollY - delta * WheelStep, 0, b.MaxScrollY);
            if (next == b.ScrollY)
                continue; // 이 방향 끝 — 바깥 스크롤 상자에 기회
            b.ScrollY = next;
            UpdateHover(p); // 내용이 포인터 밑에서 움직였다 — 밀려난(가려진) 상자에 hover 가 남지 않게
            ClearGridHover(b); // 그리드 hover 행은 기존대로 다음 포인터 이동까지 끈다
            _fling = null;
            _needsRender = true;
            return;
        }
    }

    // 손잡이 위에서 누르면 손잡이 드래그 시작(자식 press 없음)
    private bool BeginThumbDrag(List<(LayoutBox Box, SKPoint Local)> path, SKPoint p)
    {
        for (var i = path.Count - 1; i >= 0; i--)
        {
            var (b, local) = path[i];
            if (!b.IsScroller || ScrollBars.HitThumb(b, local) is not { } vertical)
                continue;
            _thumb = new ThumbDrag { Box = b, Vertical = vertical, Down = p, Start = vertical ? b.ScrollY : b.ScrollX };
            return true;
        }

        return false;
    }

    // 넘치는 가장 안쪽 스크롤 상자에서 드래그 후보 시작 — 아직 스크롤 아님(임계 거리 전까지는 클릭일 수 있음)
    private void BeginScrollGesture(List<(LayoutBox Box, SKPoint Local)> path, SKPoint p)
    {
        for (var i = path.Count - 1; i >= 0; i--)
        {
            var b = path[i].Box;
            if (!b.IsScroller || (b.MaxScrollX <= 0 && b.MaxScrollY <= 0))
                continue;
            _scroll = new ScrollGesture { Box = b, Down = p, StartX = b.ScrollX, StartY = b.ScrollY };
            _scroll.Samples.Add((Clock(), p));
            return;
        }
    }

    /// <returns>True if the scroll side consumed the move (hover update etc. is skipped).</returns>
    private bool ScrollPointerMove(SKPoint p)
    {
        if (_thumb is { } t)
        {
            var delta = t.Vertical ? p.Y - t.Down.Y : p.X - t.Down.X;
            var next = t.Start + delta * ScrollBars.OffsetPerThumbPixel(t.Box, t.Vertical);
            if (t.Vertical)
                t.Box.ScrollY = Math.Clamp(next, 0, t.Box.MaxScrollY);
            else
                t.Box.ScrollX = Math.Clamp(next, 0, t.Box.MaxScrollX);
            ClearGridHover(t.Box);
            _needsRender = true;
            return true;
        }

        if (_scroll is not { } g)
            return false;

        g.Samples.Add((Clock(), p));
        if (!g.Dragging)
        {
            if (SKPoint.Distance(p, g.Down) < KnobBinder.MinDragDistance)
                return false;
            // 스크롤로 판정 — 누르고 있던 자식은 클릭되지 않는다
            g.Dragging = true;
            if (PressVisible(_state.Press))
                _needsRender = true;
            _pressOrigin = null;
            _state.Press = null;
        }

        ApplyDrag(g, p);
        return true;
    }

    // 손가락을 따라 내용이 움직인다(손가락 위로 = 오프셋 증가)
    private void ApplyDrag(ScrollGesture g, SKPoint p)
    {
        var b = g.Box;
        if (b.MaxScrollX > 0)
            b.ScrollX = Math.Clamp(g.StartX - (p.X - g.Down.X), 0, b.MaxScrollX);
        if (b.MaxScrollY > 0)
            b.ScrollY = Math.Clamp(g.StartY - (p.Y - g.Down.Y), 0, b.MaxScrollY);
        ClearGridHover(b);
        _needsRender = true;
    }

    /// <returns>True if the scroll side consumed the release (no click).</returns>
    private bool ScrollPointerUp(SKPoint p)
    {
        if (_thumb is not null)
        {
            _thumb = null;
            return true;
        }

        if (_scroll is not { } g)
            return false;

        _scroll = null;
        if (!g.Dragging)
            return false;

        ApplyDrag(g, p); // 손을 뗀 위치까지 반영
        StartFling(g, p);
        _pressOrigin = null;
        _state.Press = null;
        return true;
    }

    private void StartFling(ScrollGesture g, SKPoint p)
    {
        var now = Clock();
        g.Samples.Add((now, p));
        var from = g.Samples.First(s => now - s.T <= FlingWindowMs);
        var dt = now - from.T;
        if (dt <= 0)
            return;

        var b = g.Box;
        var vx = b.MaxScrollX > 0 ? (from.P.X - p.X) / (double)dt * 1000 : 0;
        var vy = b.MaxScrollY > 0 ? (from.P.Y - p.Y) / (double)dt * 1000 : 0;
        var speed = Math.Max(Math.Abs(vx), Math.Abs(vy));
        if (speed < FlingMinSpeed)
            return;

        _fling = new Fling
        {
            Box = b, T0 = now, X0 = b.ScrollX, Y0 = b.ScrollY, VX = vx, VY = vy,
            DurationMs = Math.Log(FlingStopSpeed / speed) / DecelLn
        };
    }

    // 렌더 직전 — 관성 위치 = p0 + (r^t − 1) / (1000·ln r) · v  (속도 v·r^t 로 감속), 끝에 닿거나 충분히 느려지면 정지
    private void AdvanceFling()
    {
        if (_fling is not { } f)
            return;

        var t = Math.Min(Clock() - f.T0, f.DurationMs);
        var k = (Math.Pow(Decel, t) - 1) / (1000 * DecelLn);
        var b = f.Box;
        var x = Math.Clamp((float)(f.X0 + k * f.VX), 0, b.MaxScrollX);
        var y = Math.Clamp((float)(f.Y0 + k * f.VY), 0, b.MaxScrollY);
        var stuck = (f.VX == 0 || x == b.ScrollX) && (f.VY == 0 || y == b.ScrollY) && t > 0;
        b.ScrollX = x;
        b.ScrollY = y;
        ClearGridHover(b);
        _needsRender = true;

        if (t >= f.DurationMs || stuck)
            _fling = null;
    }

    // 재빌드 후 스크롤 위치·진행 중 제스처를 새 트리의 같은 상자로 옮긴다
    private void RemapScroll(LayoutBox old, LayoutBox now)
    {
        CopyOffsets(old);

        if (_scroll is { } g)
        {
            if (Counterpart(old, now, g.Box) is { } nb) g.Box = nb;
            else _scroll = null;
        }

        if (_thumb is { } t)
        {
            if (Counterpart(old, now, t.Box) is { } nb) t.Box = nb;
            else _thumb = null;
        }

        if (_fling is { } f)
        {
            if (Counterpart(old, now, f.Box) is { } nb) f.Box = nb;
            else _fling = null;
        }

        void CopyOffsets(LayoutBox b)
        {
            if (b.IsScroller && (b.ScrollX != 0 || b.ScrollY != 0) && Counterpart(old, now, b) is { } nb)
            {
                nb.ScrollX = b.ScrollX;
                nb.ScrollY = b.ScrollY;
                nb.ClampScroll();
            }

            foreach (var c in b.Children)
                CopyOffsets(c);
        }
    }
}
