using Devinno.UI.Editing;
using Xunit;

/// <summary>
/// 날짜 선택기가 보이는 샘플 PNG 는 "오늘" 표시가 실행한 날에 따라 바뀐다 — 오늘을 2026-10-14 09:30 으로 고정해 같은 그림이 나오게 한다.
/// 이 컬렉션의 샘플끼리는 차례로 돌고, 끝나면 원래 시계로 되돌린다.
/// </summary>
[CollectionDefinition("FixedPickerClock", DisableParallelization = true)]
public sealed class FixedPickerClockCollection;

public abstract class FixedPickerClockSample : IDisposable
{
    private readonly Func<DateTime> _previous = PickerClock.Now;

    protected FixedPickerClockSample() => PickerClock.Now = () => new DateTime(2026, 10, 14, 9, 30, 0);

    public void Dispose() => PickerClock.Now = _previous;
}
