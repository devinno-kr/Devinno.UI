using Devinno.UI.Layout;

namespace Devinno.UI;

/// <summary>Display of DvDateTimeInput / DvColorInput boxes: text, whether it can open, and icon.</summary>
internal static class PickerDisplay
{
    public static string Text(LayoutBox box)
        => box.Node.Tag == "DvColorInput"
            ? PickerValue.TryToColor(box.InputValue, out var c) && box.InputValue is not null ? PickerValue.Hex(c) : ""
            : PickerValue.Display(box.InputValue, box.PickerMode, box.PickerSeconds, box.PickerFormat);

    /// <summary>Can open if the value is a two-way binding and the box is neither disabled nor read-only.</summary>
    public static bool IsOpenable(LayoutBox box) => InputDisplay.IsEditable(box);

    public static string Icon(LayoutBox box)
        => box.Node.Tag == "DvColorInput" ? "fa-palette" : box.PickerMode == PickerMode.Time ? "fa-clock" : "fa-calendar";
}
