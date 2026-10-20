using Devinno.UI.Binding;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Box slot for DvSelect, DvDateTimeInput and DvColorInput — writes the value to the VM (or item scope) and rebuilds.</summary>
internal sealed class BoxSlot : IValueSlot
{
    public BoxSlot(DvSurface owner, LayoutBox box)
    {
        Owner = owner;
        Box = box;
    }

    public DvSurface Owner { get; }
    public LayoutBox Box { get; private set; }

    public object? Value => Box.InputValue;
    public Type? ValueType => Box.Node.Tag == "DvSelect" ? Box.SelectValueType : Box.PickerValueType;
    public ComputedStyle Style => Box.Style;
    public IReadOnlyList<SelectOption> Options => Box.SelectOptions;
    public int SelectedIndex => Box.SelectedIndex;
    public PickerMode PickerMode => Box.PickerMode;
    public bool PickerSeconds => Box.PickerSeconds;

    public bool Editing
    {
        get => Box.Editing;
        set => Box.Editing = value;
    }

    public bool TryLocate(out SKRect rect, out SKRect clip)
    {
        rect = clip = SKRect.Empty;
        return Owner.Root is { } root && EditGeometry.TryLocate(root, Box, out rect, out clip);
    }

    public bool Remap(LayoutBox old, LayoutBox now)
    {
        if (DvSurface.Counterpart(old, now, Box) is not { } nb)
            return false;
        Box = nb;
        return true;
    }

    // 게이트: bool M(값 타입) → 없으면 bool M(object). 메서드 없음·시그니처 불일치·예외 = 거절(CommitGate, 그리드 셀과 같은 규칙). setter 예외 = Failed.
    public CommitResult Write(object? value)
    {
        if (Box.ValueBindExpr is null || Equals(value, Box.InputValue))
            return CommitResult.Same;

        var vm = Owner.Vm;
        if (!Interaction.CommitGate.Allows(Owner, Box.Node.Tag, vm, Box.OnBeforeCommit, value, value?.GetType() ?? typeof(object), typeof(object)))
            return CommitResult.Rejected;

        if (!Interaction.HandlerErrors.TrySet(Owner, Box.Node.Tag, Box.ValueContext(vm), BindingResolver.ExtractPath(Box.ValueBindExpr), value))
            return CommitResult.Failed; // setter 예외 = 거절과 같게(알림은 TrySet 이)
        Owner.NotifyValueWritten(Box);
        Owner.Invalidate();
        return CommitResult.Written;
    }
}
