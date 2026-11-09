using System.Reflection;

namespace Devinno.UI.Interaction;

// onbeforecommit 확인 — 모든 값 위젯(토글·노브·슬라이더·입력·선택·라디오·목록·날짜·색·커스텀 위젯·DvSwitch)과 그리드 셀(체크·편집)이 이것 하나를 쓴다(3단계 R13).
// 탐색 순서: 반복·그리드 행 안이면(item 있음) bool M(항목, 값형) → bool M(항목, object), 그다음 bool M(값형) → bool M(object).
// 값형은 정확히 같은 형(Nullable 은 바탕 형과 같게 본다). 반환 형이 bool 이 아니면 확인 메서드가 아니다.
// 속성 없음 = 그냥 씀. VM 없음·메서드 없음 = 거절 + 경고(화면마다 같은 문구 1회). 처리기 예외·호출 실패 = 거절 + 오류 기록(Trace + owner 의 HandlerError).
// true 를 돌려줄 때만 씀.
internal static class CommitGate
{
    // valueType = 쓰려는 값의 형(값이 null 이면 object), item = 반복 항목·그리드 행(없으면 null). owner·tag = 예외를 알릴 서피스·요소 태그(null 이면 Trace 만).
    public static bool Allows(DvSurface? owner, string? tag, object? vm, string? gate, object? value, Type valueType, object? item = null)
    {
        if (gate is not { Length: > 0 })
            return true;
        if (vm is null)
            return false;

        var (mi, withItem) = Find(vm.GetType(), gate, valueType, item);
        if (mi is null)
        {
            var t = (Nullable.GetUnderlyingType(valueType) ?? valueType).Name;
            Warnings.Warn(item is null
                ? $"Devinno.UI: {tag} onbeforecommit '{gate}': no bool {gate}({t}) or bool {gate}(object) found; not written"
                : $"Devinno.UI: {tag} onbeforecommit '{gate}': no bool {gate}(item, {t}) or bool {gate}({t}) found (object also accepted); not written");
            return false;
        }

        try
        {
            return mi.Invoke(vm, withItem ? new[] { item, value } : new[] { value }) is true;
        }
        catch (Exception ex) // 처리기 예외(TargetInvocationException)·호출 자체 실패(인수 불일치 등) 모두 거절
        {
            HandlerErrors.Report(owner, ex, "onbeforecommit", gate, tag);
            return false;
        }
    }

    // 탐색 순서대로 첫 메서드. WithItem = 항목을 첫 인수로 받는 형식인가
    private static (MethodInfo? Method, bool WithItem) Find(Type vmType, string gate, Type valueType, object? item)
    {
        var exact = Nullable.GetUnderlyingType(valueType) ?? valueType;
        bool Exact(Type p) => (Nullable.GetUnderlyingType(p) ?? p) == exact;
        var methods = vmType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == gate && m.ReturnType == typeof(bool)).ToList();

        if (item is not null)
        {
            var two = methods.Where(m => m.GetParameters() is { Length: 2 } ps && ps[0].ParameterType.IsInstanceOfType(item)).ToList();
            var w = two.FirstOrDefault(m => Exact(m.GetParameters()[1].ParameterType)) ?? two.FirstOrDefault(m => m.GetParameters()[1].ParameterType == typeof(object));
            if (w is not null)
                return (w, true);
        }

        var one = methods.Where(m => m.GetParameters().Length == 1).ToList();
        return (one.FirstOrDefault(m => Exact(m.GetParameters()[0].ParameterType)) ?? one.FirstOrDefault(m => m.GetParameters()[0].ParameterType == typeof(object)), false);
    }
}
