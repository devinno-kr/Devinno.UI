using Devinno.UI.Controls;

namespace Devinno.UI.Markup;

/// <summary>
/// Tag lists that several parts of the library need, kept in one place. The source generator keeps its own copy of
/// <see cref="HandleTags"/> (it cannot reference the library); a test checks that the two are equal.
/// </summary>
internal static class TagCatalog
{
    /// <summary>Handle type of each tag that has its own handle (handle type name = tag name). Any other tag gets <see cref="DvControl"/>.</summary>
    public static readonly IReadOnlyDictionary<Type, (string Tag, Func<string, DvControl> New)> HandleKinds = new Dictionary<Type, (string, Func<string, DvControl>)>
    {
        [typeof(DvLabel)] = ("DvLabel", id => new DvLabel(id)),
        [typeof(DvButton)] = ("DvButton", id => new DvButton(id)),
        [typeof(DvToggle)] = ("DvToggle", id => new DvToggle(id)),
        [typeof(DvKnob)] = ("DvKnob", id => new DvKnob(id)),
        [typeof(DvSlider)] = ("DvSlider", id => new DvSlider(id)),
        [typeof(DvListBox)] = ("DvListBox", id => new DvListBox(id)),
        [typeof(DvExpander)] = ("DvExpander", id => new DvExpander(id)),
        [typeof(DvTreeView)] = ("DvTreeView", id => new DvTreeView(id)),
        [typeof(DvInput)] = ("DvInput", id => new DvInput(id)),
        [typeof(DvSelect)] = ("DvSelect", id => new DvSelect(id)),
        [typeof(DvSegment)] = ("DvSegment", id => new DvSegment(id)),
        [typeof(DvDateTimeInput)] = ("DvDateTimeInput", id => new DvDateTimeInput(id)),
        [typeof(DvColorInput)] = ("DvColorInput", id => new DvColorInput(id)),
        [typeof(DvCheckBox)] = ("DvCheckBox", id => new DvCheckBox(id)),
        [typeof(DvRadio)] = ("DvRadio", id => new DvRadio(id)),
        [typeof(DvSwitch)] = ("DvSwitch", id => new DvSwitch(id)),
    };

    /// <summary>Tags that have their own handle type.</summary>
    public static IEnumerable<string> HandleTags => HandleKinds.Values.Select(k => k.Tag);

    /// <summary>Widgets whose <c>value="{p}"</c> inside a component template writes to the outer path of a two-way parameter <c>p</c> (custom widgets too).</summary>
    public static readonly HashSet<string> TemplateTwoWayTags = new(StringComparer.Ordinal)
    {
        "DvToggle", "DvCheckBox", "DvKnob", "DvSlider", "DvInput", "DvSelect", "DvSegment", "DvTabs", "DvDateTimeInput", "DvColorInput", "DvButton",
    };

    /// <summary>Tags with their own press behavior, where <c>hold</c> does not apply.</summary>
    public static readonly HashSet<string> HoldIgnored = new(StringComparer.Ordinal)
    {
        "DvToggle", "DvCheckBox", "DvRadio", "DvInput", "DvSelect", "DvKnob", "DvSlider",
    };
}
