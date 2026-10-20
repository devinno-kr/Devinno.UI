using Devinno.UI.Binding;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI;

/// <summary>Edit target for a DvInput box: the value is written to the VM (or the item scope). After a rebuild, the surface replaces <see cref="Box"/> with the new box.</summary>
internal sealed class BoxEditTarget : IEditTarget
{
    private readonly DvSurface _s;

    public BoxEditTarget(DvSurface surface, LayoutBox box)
    {
        _s = surface;
        Box = box;
        InitialText = InputDisplay.EditText(box);
    }

    public LayoutBox Box { get; set; }
    public string InitialText { get; }
    public DvEditKind Kind => Box.InputKind;
    public bool Multiline => Box.InputMultiline;
    public string? Placeholder => Box.InputPlaceholder;
    public bool RebuildAfterWrite => true;

    public DvNumericSpec? Numeric
        => Box.InputKind != DvEditKind.Numeric || Box.ValueBindExpr is null ? null
           : NumericEdit.Spec(BindingResolver.ResolvePropertyType(Box.ValueContext(_s.Vm), BindingResolver.ExtractPath(Box.ValueBindExpr)), Box);

    public bool TryLocate(out SKRect rect, out SKRect clip)
    {
        rect = clip = SKRect.Empty;
        return _s.Root is { } root && EditGeometry.TryLocate(root, Box, out rect, out clip);
    }

    public EditLook Look(string sample)
    {
        var st = Box.Disabled && Box.StyleDisabled is { } d ? d : Box.Style;
        var family = TextFont.ResolveTypeface(sample + Box.InputPlaceholder + InputDisplay.Text(Box), st.FontFamily).FamilyName;
        var bg = st.Background?.ToSK()
                 ?? (st.Gradient is { Stops.Count: > 0 } g ? g.Stops[0].ToSK() : (SKColor?)null)
                 ?? _s.Token("surface", SKColors.White);
        return new EditLook(family, st.FontSize, st.Bold, st.Color.ToSK(), bg,
            new SKRect(st.Padding.Left, st.Padding.Top, st.Padding.Right, st.Padding.Bottom), st.TextAlign);
    }

    public bool TryWrite(string text, out string? error)
    {
        error = null;
        if (Box.InputKind == DvEditKind.Password ? text.Length == 0 : text == InitialText)
            return true; // 바뀐 것 없음(password 는 빈 값 = 변경 안 함)

        var vm = _s.Vm;
        var ctx = Box.ValueContext(vm);
        var path = BindingResolver.ExtractPath(Box.ValueBindExpr!);
        object? value = text;
        object gateArg = text;
        if (Box.InputKind == DvEditKind.Numeric)
        {
            var type = BindingResolver.ResolvePropertyType(ctx, path);
            if (!NumericEdit.TryParse(text, type, NumericEdit.Spec(type, Box), _s.Strings, out value, out var number, out error))
                return false;
            if (NumericEdit.SameNumber(Box.InputValue, number))
                return true; // "07" → 7 처럼 표기만 다름
            gateArg = number;
        }

        if (!Interaction.CommitGate.Allows(_s, Box.Node.Tag, vm, Box.OnBeforeCommit, gateArg, gateArg.GetType()))
        {
            error = Localization.DvStrings.Text(_s.Strings, "dv.input.rejected", "허용되지 않는 값입니다");
            return false;
        }

        if (!Interaction.HandlerErrors.TrySet(_s, Box.Node.Tag, ctx, path, value))
        {
            // setter 예외 = 쓰기 실패 — 거절과 같게(세션 유지). 예외 메시지는 화면에 내지 않는다(Trace·HandlerError).
            error = Localization.DvStrings.Text(_s.Strings, "dv.input.write-failed", "값을 저장하지 못했습니다");
            return false;
        }

        _s.NotifyValueWritten(Box);
        return true;
    }

    public void Show(string? text, bool hideText, string? error)
    {
        Box.Editing = true; // 포커스 링(전에는 BeginEdit 이 직접 켰다)
        Box.EditText = text;
        Box.EditHideText = hideText;
        Box.EditError = error;
    }

    public bool SameAs(IEditTarget other) => other is BoxEditTarget o && ReferenceEquals(o.Box, Box);

    public bool Remap(LayoutBox old, LayoutBox now)
    {
        if (DvSurface.Counterpart(old, now, Box) is not { Node.Tag: "DvInput" } nb)
            return false;
        Box = nb;
        nb.Editing = true;
        return true;
    }

    public void Reveal() => _s.ScrollIntoView(Box);

    public bool Hits(LayoutBox? hitBox, SKPoint p) => ReferenceEquals(hitBox, Box);

    public IEditTarget? Next(DvEditExit exit) => null; // 연속 입력 없음

    public void Ended()
    {
        Box.Editing = false;
        Box.EditText = null;
        Box.EditHideText = false;
        Box.EditError = null;
    }
}
