using System.Reflection;
using Devinno.UI.Layout;

namespace Devinno.UI.Interaction;

/// <summary>Component that finds and calls the view model method named in a markup <c>onclick</c> attribute.</summary>
/// <remarks>An internal engine step. App code normally does not use it directly; it uses markup bindings and <see cref="DvSurface"/>/<see cref="DvRenderer"/>.</remarks>
public static class EventDispatcher
{
    /// <summary>
    /// Calls the public view model method for the box's <c>onclick="M"</c>. Inside a <c>DvRepeat</c> item, <c>M(항목형)</c> (taking the item type) takes priority;
    /// inside a component template, <c>M(파라미터형)</c> (taking a parameter type) that accepts a matching-typed value from that instance's parameters (the first in parameter declaration order);
    /// if neither exists, the parameterless <c>M()</c> is called. Does nothing if no matching method exists.
    /// </summary>
    /// <param name="box">The pressed box. Ignored if it is disabled or has no <c>onclick</c>.</param>
    /// <param name="vm">The screen view model. Ignored if <see langword="null"/>.</param>
    /// <remarks>Exceptions thrown by the called method are propagated wrapped in <see cref="TargetInvocationException"/>.
    /// (Click handling in <see cref="DvSurface"/> catches this exception and reports it through <see cref="DvSurface.HandlerError"/>.)</remarks>
    public static void Click(LayoutBox box, object? vm)
    {
        if (box.Disabled || vm is null || string.IsNullOrEmpty(box.OnClick))
            return;

        if (box.ItemScope?.Item is { } item &&
            vm.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m =>
                m.Name == box.OnClick && m.GetParameters() is { Length: 1 } ps && ps[0].ParameterType.IsInstanceOfType(item))
            is { } withItem)
        {
            withItem.Invoke(vm, new[] { item });
            return;
        }

        // 컴포넌트 템플릿 안 — 형식이 맞는 파라미터 값(예: 유닛 템플릿 안 Reset 버튼 → Reset(BlenderVm u))
        if (box.ComponentScope is { } scope)
        {
            var oneArg = vm.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == box.OnClick && m.GetParameters().Length == 1).ToList();
            foreach (var value in scope.Values)
            {
                if (value is null)
                    continue;
                if (oneArg.FirstOrDefault(m => m.GetParameters()[0].ParameterType.IsInstanceOfType(value)) is { } withParam)
                {
                    withParam.Invoke(vm, new[] { value });
                    return;
                }
            }
        }

        var mi = vm.GetType().GetMethod(
            box.OnClick,
            BindingFlags.Public | BindingFlags.Instance,
            Type.EmptyTypes);

        mi?.Invoke(vm, null);
    }
}
