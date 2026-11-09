using System.Diagnostics;

namespace Devinno.UI.Interaction;

// 마크업·바인딩 경고 한 곳 — 화면(DvSurface)마다 같은 문구 한 번. 문구에 태그·속성·값이 들어 있으므로 문구가 곧 (태그, 속성, 값) 열쇠다.
// 화면은 앰비언트 읽기 범위(HandlerErrors.ReadScope)로 안다: 서피스가 레이아웃·그리기·입력 동안, LayoutEngine·DvRenderer 는 서피스 없이 그 호출 동안 설정한다.
// 범위가 없으면(엔진 밖에서 직접 부른 경우) 중복을 거르지 않고 그대로 쓴다 — 프로세스 전체 static 집합을 두지 않아 테스트끼리 영향이 없다.
internal static class Warnings
{
    /// <summary>
    /// Writes <paramref name="message"/> with <see cref="Trace.TraceWarning(string)"/> (or <see cref="Trace.TraceError(string)"/> when <paramref name="error"/>),
    /// once per surface: the same text is written again only on another surface, or after the markup is reloaded.
    /// </summary>
    /// <param name="message">The full message, including the <c>Devinno.UI…:</c> prefix. It names the tag, the attribute and the value, so it is the de-duplication key.</param>
    /// <param name="error">Writes an error instead of a warning.</param>
    public static void Warn(string message, bool error = false)
    {
        if (HandlerErrors.CurrentRead is { } scope)
        {
            lock (scope.Warned)
            {
                if (!scope.Warned.Add(message))
                    return;
            }
        }

        if (error)
            Trace.TraceError(message);
        else
            Trace.TraceWarning(message);
    }
}
