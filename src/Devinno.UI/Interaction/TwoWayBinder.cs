using Devinno.UI.Binding;
using Devinno.UI.Layout;

namespace Devinno.UI.Interaction;

/// <summary>Component that writes the inverted value to the two-way binding (<c>value="{Bind …}"</c>) target when a <c>DvToggle</c> is pressed.</summary>
/// <remarks>An internal engine step. App code normally does not use it directly; it uses markup bindings and <see cref="DvSurface"/>/<see cref="DvRenderer"/>.</remarks>
public static class TwoWayBinder
{
    /// <summary>
    /// Writes the inverse of the toggle box's current state to the binding path. If <c>onbeforecommit="M"</c> is present, <c>bool M(bool proposed)</c> is called first and
    /// the value is written only when it returns <see langword="true"/>. If the method is missing or its signature does not match (including a return type other than <see langword="bool"/>), nothing is written and a Trace warning is logged (once per cause);
    /// if the method throws, nothing is written and a Trace error is logged. Inside a <c>DvRepeat</c> item, it writes in the item scope.
    /// </summary>
    /// <param name="box">The pressed toggle box. Ignored if it is disabled or its value is not a two-way binding.</param>
    /// <param name="vm">The screen view model. Ignored if <see langword="null"/>.</param>
    /// <remarks>
    /// Exceptions thrown by the check method and write failures (property setter exceptions, type conversion failures) are not propagated. They are treated as not written and a Trace error is logged.
    /// (For toggle operations that go through <see cref="DvSurface"/>, they are also reported through <see cref="DvSurface.HandlerError"/>.)
    /// </remarks>
    public static void Commit(LayoutBox box, object? vm) => Commit(box, vm, owner: null);

    // owner = 예외를 알릴 서피스(HandlerError)
    internal static void Commit(LayoutBox box, object? vm, DvSurface? owner)
    {
        if (box.Disabled || vm is null || box.ValueBindExpr is null)
            return;
        if (!BindingResolver.IsTwoWay(box.ValueBindExpr))
            return;

        var path = BindingResolver.ExtractPath(box.ValueBindExpr);
        var proposed = !box.ToggleOn;

        if (!CommitGate.Allows(owner, box.Node.Tag, vm, box.OnBeforeCommit, proposed, typeof(bool)))
            return;

        // DvRepeat 안이면 항목 범위로. setter 예외 = 쓰지 않음(다시 빌드되어 VM 값으로 보임)
        if (HandlerErrors.TrySet(owner, box.Node.Tag, box.ValueContext(vm), path, proposed))
            owner?.NotifyValueWritten(box);
    }
}
