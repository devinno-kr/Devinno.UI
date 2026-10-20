using System.Diagnostics;
using System.Reflection;

namespace Devinno.UI.Interaction;

// 위젯(DvToggle·DvKnob·DvInput·DvSelect·DvDateTimeInput·DvColorInput)의 onbeforecommit 확인 — DvDataGrid 셀과 같은 규칙.
// 속성 없음 = 그냥 씀. VM 없음·메서드 없음·bool 아닌 반환 형식 = 거절 + 경고(같은 VM 형식·이름에 1회).
// 처리기 예외·호출 실패 = 거절 + 오류 기록(Trace + owner 의 HandlerError). true 를 돌려줄 때만 씀.
internal static class CommitGate
{
    private static readonly HashSet<(Type, string)> Warned = new();

    // argTypes = 찾을 매개변수 형식 후보(앞의 것 우선). bool M(형식) 만 인정한다. owner·tag = 예외를 알릴 서피스·요소 태그(null 이면 Trace 만).
    public static bool Allows(DvSurface? owner, string? tag, object? vm, string? gate, object? value, params Type[] argTypes)
    {
        if (gate is not { Length: > 0 })
            return true;
        if (vm is null)
            return false;

        MethodInfo? mi = null;
        foreach (var t in argTypes)
        {
            if (vm.GetType().GetMethod(gate, BindingFlags.Public | BindingFlags.Instance, new[] { t }) is { } m &&
                m.ReturnType == typeof(bool))
            {
                mi = m;
                break;
            }
        }

        if (mi is null)
        {
            lock (Warned)
            {
                if (Warned.Add((vm.GetType(), gate)))
                    Trace.TraceWarning($"Devinno.UI: onbeforecommit '{gate}' — bool {gate}({argTypes.FirstOrDefault()?.Name}) 없음, 쓰지 않음");
            }

            return false;
        }

        try
        {
            return mi.Invoke(vm, new[] { value }) is true;
        }
        catch (Exception ex) // 처리기 예외(TargetInvocationException)·호출 자체 실패 모두 거절
        {
            HandlerErrors.Report(owner, ex, "onbeforecommit", gate, tag);
            return false;
        }
    }
}
