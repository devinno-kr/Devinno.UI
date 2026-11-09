using System.Diagnostics;

namespace Devinno.UI.Interaction;

// 마크업·바인딩 경고 한 곳 — 화면(DvSurface)마다 같은 열쇠 한 번. 열쇠를 주지 않으면 문구가 곧 열쇠(문구에 태그·속성·값이 들어 있다).
// 속성 값 경고(AttrRules)는 열쇠 = (태그, 속성, 마크업 원문) — 바인딩은 값이 바뀌어도 한 번, 리터럴은 값마다 한 번(원문 = 값).
// 화면은 앰비언트 읽기 범위(HandlerErrors.ReadScope)로 안다: 서피스가 레이아웃·그리기·입력 동안, LayoutEngine·DvRenderer 는 서피스 없이 그 호출 동안 설정한다.
// 범위가 없으면(엔진 밖에서 직접 부른 경우) 중복을 거르지 않고 그대로 쓴다 — 프로세스 전체 static 집합을 두지 않아 테스트끼리 영향이 없다.
// 화면마다 열쇠는 MaxWarned 개까지 — 넘으면 "too many" 한 줄을 쓰고 그 화면의 새 경고는 쓰지 않는다(마크업을 다시 읽으면 처음부터).
internal static class Warnings
{
    /// <summary>The most de-duplication keys one surface records; past it, one final line is written and further new warnings on that surface are dropped.</summary>
    internal const int MaxWarned = 4096;

    /// <summary>
    /// Writes <paramref name="message"/> with <see cref="Trace.TraceWarning(string)"/> (or <see cref="Trace.TraceError(string)"/> when <paramref name="error"/>),
    /// once per surface for each <paramref name="key"/>: the same key is written again only on another surface, or after the markup is reloaded.
    /// A surface records at most <see cref="MaxWarned"/> keys; past that, one "too many warnings" line is written and further new warnings on that surface are not.
    /// </summary>
    /// <param name="message">The full message, including the <c>Devinno.UI…:</c> prefix.</param>
    /// <param name="error">Writes an error instead of a warning.</param>
    /// <param name="key">The de-duplication key; <see langword="null"/> = the message itself.</param>
    public static void Warn(string message, bool error = false, string? key = null)
    {
        if (HandlerErrors.CurrentRead is { } scope)
        {
            lock (scope.Warned)
            {
                if (scope.Warned.Contains(key ?? message))
                    return;
                if (scope.Warned.Count >= MaxWarned)
                {
                    if (scope.WarnedFull)
                        return;
                    scope.WarnedFull = true;
                    message = $"Devinno.UI: too many markup warnings on this screen ({MaxWarned}); further warnings are not written until the markup is reloaded";
                    error = false;
                }
                else
                {
                    scope.Warned.Add(key ?? message);
                }
            }
        }

        if (error)
            Trace.TraceError(message);
        else
            Trace.TraceWarning(message);
    }
}
