namespace Devinno.UI.Markup;

/// <summary>How an attribute's value is read.</summary>
internal enum AttrKind
{
    /// <summary>Text (interpolation allowed).</summary>
    Text,
    /// <summary>A number.</summary>
    Number,
    /// <summary>True or false.</summary>
    Bool,
    /// <summary>One of a fixed set of words (<see cref="AttrDef.Values"/>).</summary>
    Enum,
    /// <summary>A color (<c>#hex</c>, a name or <c>token(...)</c>).</summary>
    Color,
    /// <summary>A size (px, %, star, step name).</summary>
    Length,
    /// <summary>A style declaration list.</summary>
    Style,
    /// <summary>An expression read as written: a handler method name or a two-way value path.</summary>
    Expr,
    /// <summary>A property path relative to an item (<c>item-text</c>, <c>field</c>).</summary>
    Path,
    /// <summary>A list from the view model.</summary>
    Items,
    /// <summary>Any object from the view model.</summary>
    Object,
}

/// <summary>
/// One attribute of a tag: its name, how it is read, its default, whether it may be a binding.
/// <paramref name="Note"/> records current quirks that stage 3 (rule unification) will fix.
/// </summary>
internal sealed record AttrDef(string Name, AttrKind Kind, string? Default = null, bool Bindable = true, string[]? Values = null, string? Note = null);

/// <summary>
/// The attributes of every built-in tag, in one place. The build code reads only these names, and only through
/// <see cref="Layout.AttrReader"/> (tests record the reads and compare them with this table).
/// </summary>
internal static class TagSchema
{
    /// <summary>Attributes every element accepts.</summary>
    public static readonly IReadOnlyList<AttrDef> Common =
    [
        new("id", AttrKind.Expr, Bindable: false, Note: "generator only; becomes the handle field"),
        new("class", AttrKind.Text),
        new("style", AttrKind.Style),
        new("hover-style", AttrKind.Style),
        new("press-style", AttrKind.Style),
        new("disabled-style", AttrKind.Style),
        new("content-align", AttrKind.Enum, Values: ["top-left", "top", "top-right", "left", "center", "right", "bottom-left", "bottom", "bottom-right"]),
        new("grow", AttrKind.Number, "0"),
        new("width", AttrKind.Length),
        new("height", AttrKind.Length),
        new("visible", AttrKind.Bool, "true", Note: "false, empty, 0 or false hide; other text shows"),
        new("disabled", AttrKind.Bool, "false", Note: "true, 1 or a non-zero number disable; inherited downward"),
        new("onclick", AttrKind.Expr, Bindable: false),
        new("hold", AttrKind.Number, "0", Note: "ms; ignored on tags in TagCatalog.HoldIgnored"),
        new("onbeforecommit", AttrKind.Expr, Bindable: false, Note: "used by value widgets only"),
    ];

    /// <summary>Tag-specific attributes, by tag. Filled group by group in stage 2.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<AttrDef>> Tags = new Dictionary<string, IReadOnlyList<AttrDef>>(StringComparer.Ordinal)
    {
        // 공통 + 컨테이너 (2.2)
        ["DvStack"] = Sugar("md", "stretch", "start"),
        ["DvRow"] = Sugar("md", "center", "start"),
        ["DvGrid"] =
        [
            new("columns", AttrKind.Text, "2", Note: "an integer N = N equal 1fr columns (N <= 0 gives 2); otherwise a track list used as is"),
            new("rows", AttrKind.Text, Note: "a track list used as is"),
            .. Sugar("lg", null, null),
        ],
        ["DvSpacer"] = Sugar(null, null, null, "applied as on DvStack although a spacer has no children"),
        ["DvRepeat"] =
        [
            new("items", AttrKind.Items, Note: "one-way; a string or a non-collection gives no children; enumeration errors give no children"),
            .. Sugar("md", "stretch", "start"),
        ],
        ["DvBox"] = [],
        ["DvSlot"] = [],
        ["DvStyle"] = [],
        ["DvTabs"] =
        [
            new("value", AttrKind.Object, Note: "two-way {Bind X} selects by DvTab value; one-way/literal = read-only; absent = the screen remembers the index (shared by all instances of the node)"),
            new("readonly", AttrKind.Bool, "false", Note: "read through the block shared with DvSelect; not documented for DvTabs"),
            new("placeholder", AttrKind.Text, Note: "dead read: the block shared with DvSelect reads it, tabs never show it"),
            new("items", AttrKind.Items, Note: "dead read: the block shared with DvSelect builds options from it, then the DvTab children replace them"),
            new("item-value", AttrKind.Path, Note: "dead read (see items)"),
            new("item-text", AttrKind.Path, Note: "dead read (see items)"),
            new("item-icon", AttrKind.Path, Note: "dead read (see items)"),
        ],
        ["DvTab"] =
        [
            new("value", AttrKind.Text, Note: "resolved as text, then converted to the type of the DvTabs value"),
            new("text", AttrKind.Text, Note: "header text; falls back to value; a code Text override is not applied"),
            new("disabled", AttrKind.Bool, "false", Note: "the markup text is copied to the header item; also disables the body when that tab is selected"),
        ],
        ["DvExpander"] =
        [
            new("expanded", AttrKind.Bool, Note: "two-way {Bind X} follows the VM; one-way/literal locks it (true, case-insensitive); absent = the screen remembers (collapsed)"),
            new("header", AttrKind.Text, ""),
        ],
        ["DvStepBar"] =
        [
            new("steps", AttrKind.Items, Note: "a list (each item as text) or comma-separated text"),
            new("current", AttrKind.Number, "0", Note: "rounded; 1-based, 0 = not started, above the count = all done"),
        ],

        // 기본·입력 (2.3)
        ["DvLabel"] = [Text()],
        ["DvButton"] =
        [
            Text(),
            Icon(),
            new("variant", AttrKind.Enum, Values: ["primary", "secondary", "danger", "ghost"], Note: "adds the classes btn btn-{variant} unvalidated; empty after resolving = unstyled button"),
            new("size", AttrKind.Enum, Values: ["sm", "lg"], Note: "adds btn-{size} unvalidated; only when variant is set"),
            new("value", AttrKind.Bool, Note: "present = latching button: on = bool true or text true (case-insensitive); {Bind X} flips on press; without variant adds btn btn-secondary, on adds btn-on; a handle cannot own it"),
        ],
        ["DvIcon"] = [Icon()],
        ["DvImage"] =
        [
            new("src", AttrKind.Path, Note: "file path; the image cache never re-reads a changed file"),
            Fit(),
        ],
        ["DvVideo"] =
        [
            new("src", AttrKind.Path, Note: "file path played by a player the element owns; ignored (Trace) when source is present"),
            new("source", AttrKind.Object, Note: "an IFrameSource from the view model; wins over src; any other type shows nothing (Trace)"),
            Fit(),
            new("playing", AttrKind.Bool, "true", Note: "src only; read only when src resolves to a non-blank path"),
            new("loop", AttrKind.Bool, "false", Note: "src only; read only when src resolves to a non-blank path"),
            new("position", AttrKind.Number, Note: "seconds; a changed value seeks; {Bind X} is written back every frame; with src or source=VideoPlayer only"),
            new("duration", AttrKind.Number, Note: "seconds; only {Bind X} is used (written out); a one-way value or literal is ignored silently"),
        ],
        ["DvToggle"] = [BoolValue()],
        ["DvCheckBox"] = [BoolValue(), Text("a code Text override is honored by the build, but the handle exposes no Text")],
        ["DvRadio"] =
        [
            new("option", AttrKind.Text, "", Note: "converted to the type of the bound value for comparison"),
            new("value", AttrKind.Object, Note: "the group value; {Bind X} = VM; one-way/literal = that value (a handle override is read as checked); absent = the screen remembers the group; a handle cannot own it"),
            new("group", AttrKind.Text, Note: "read only when value is absent; no name + a handle = this element alone"),
            Text("a code Text override is honored by the build, but the handle exposes no Text"),
        ],
        ["DvKnob"] =
        [
            .. KnobRange(),
            new("input-mode", AttrKind.Enum, "drag", Bindable: false, Values: ["drag", "tap", "both"], Note: "read raw (a binding is not resolved); an unknown value warns at run time only (Trace) and falls back to drag"),
        ],
        ["DvSlider"] =
        [
            .. KnobRange(),
            new("direction", AttrKind.Enum, "right", Values: ["right", "left", "up", "down"], Note: "an unknown value warns (Trace) and falls back to right"),
            new("show-value", AttrKind.Bool, "false", Note: "decimals come from step"),
        ],
        ["DvInput"] =
        [
            OwnedValue("text or a number (keyboard-type numeric)"),
            new("keyboard-type", AttrKind.Enum, "text", Values: ["text", "numeric", "password"], Note: "any other value = text; the handle reads it raw (a binding is not resolved there)"),
            new("multiline", AttrKind.Bool, "false"),
            Placeholder(),
            new("format", AttrKind.Text, Note: ".NET number format"),
            new("min", AttrKind.Number, Note: "out of range is rejected (not clamped)"),
            new("max", AttrKind.Number, Note: "out of range is rejected (not clamped)"),
            new("unit", AttrKind.Text),
            ReadOnly(),
            new("step", AttrKind.Number, "1", Note: "<= 0 or not a number = 1"),
            new("show-stepper", AttrKind.Bool, "false", Note: "read only when keyboard-type is numeric"),
        ],
        ["DvSelect"] = [OwnedValue("converted to the bound property type"), Placeholder(), ReadOnly(), .. Items()],
        ["DvOption"] =
        [
            new("value", AttrKind.Text, Note: "resolved as text"),
            new("text", AttrKind.Text, Note: "falls back to value; inner text is not used; a code Text override is not applied"),
            new("icon", AttrKind.Text, Note: "fa-name; shown by DvSegment and DvListBox"),
        ],
        ["DvSegment"] =
        [
            OwnedValue("the option value is written without conversion to the bound type"),
            ReadOnly("no read-only visual"),
            new("fill", AttrKind.Bool, "false"),
            new("placeholder", AttrKind.Text, Note: "dead read: the block shared with DvSelect reads it, a segment never shows it"),
            .. Items("not documented for DvSegment"),
        ],
        ["DvListBox"] =
        [
            new("selection-mode", AttrKind.Enum, "single", Values: ["single", "multi"], Note: "anything but multi = single"),
            OwnedValue("single selection only (read when selection-mode is not multi)"),
            new("selected-items", AttrKind.Items, Note: "multi selection only (read when selection-mode is multi); two-way {Bind X} list"),
            new("readonly", AttrKind.Bool, "false", Note: "own formula (not the shared input rule); no input-readonly class"),
            .. Items(),
        ],
        ["DvTreeView"] =
        [
            new("items", AttrKind.Items, Note: "root list; a string or a non-collection = no rows"),
            new("item-children", AttrKind.Path, Bindable: false, Note: "read raw; absent = every item is a leaf; depth limited to 64, cycles skipped"),
            new("item-text", AttrKind.Path, Bindable: false, Note: "read raw; absent = the item itself as text"),
            new("item-value", AttrKind.Path, Bindable: false, Note: "read raw; absent = the item itself; the handle also reads it raw"),
            OwnedValue("the selected item's value"),
            new("readonly", AttrKind.Bool, "false", Note: "own formula (not the shared input rule); expand/collapse still works"),
        ],
        ["DvDateTimeInput"] =
        [
            OwnedValue("DateTime, DateOnly or TimeOnly (nullable)"),
            Placeholder(),
            ReadOnly(),
            new("mode", AttrKind.Enum, Values: ["date", "time", "datetime"], Note: "absent = from the value type; an unknown value warns (Trace)"),
            new("show-seconds", AttrKind.Bool, "false"),
            new("format", AttrKind.Text, Note: ".NET date format"),
        ],
        ["DvColorInput"] = [OwnedValue("#RRGGBB text or SKColor"), Placeholder(), ReadOnly()],
        ["DvProgress"] =
        [
            new("value", AttrKind.Number, "0", Note: "one-way; read by the block shared with DvGauge/DvRing"),
            new("min", AttrKind.Number, "0"),
            new("max", AttrKind.Number, "100"),
            new("tone", AttrKind.Text, "info", Note: "empty = info; not validated"),
            new("direction", AttrKind.Enum, "right", Values: ["right", "left", "up", "down"], Note: "empty = right; an unknown value is kept without a warning (DvSlider warns)"),
            new("show-value", AttrKind.Bool, "false"),
            new("unit", AttrKind.Text),
            new("format", AttrKind.Text, Note: ".NET number format"),
            new("alarm-zones", AttrKind.Text, Note: "\"from:to:#color ...\"; token(name) colors resolved; a bad format throws"),
            GaugeDead("ticks", AttrKind.Number), GaugeDead("minor-ticks", AttrKind.Number), GaugeDead("show-tick-labels", AttrKind.Bool),
            GaugeDead("label", AttrKind.Text), GaugeDead("sweep", AttrKind.Number), GaugeDead("thickness", AttrKind.Number),
        ],
    };

    // 2.3 공용 조각 — 같은 해석을 여러 태그가 쓴다
    private static AttrDef Text(string? note = null) =>
        new("text", AttrKind.Text, "", Note: note ?? "absent = inner text; a code Text override wins and is used as is");

    private static AttrDef Icon() => new("icon", AttrKind.Text, Note: "fa-name / far fa-name / fab fa-name; unknown = not drawn (Trace)");

    private static AttrDef Fit() => new("fit", AttrKind.Enum, "contain", Values: ["contain", "cover", "fill"], Note: "an unknown value = contain, silently");

    private static AttrDef BoolValue() =>
        new("value", AttrKind.Bool, Note: "on = bool true or text true (case-insensitive); {Bind X} = VM; absent + a handle = the handle owns it; one-way/literal = read-only");

    private static AttrDef OwnedValue(string what) =>
        new("value", AttrKind.Object, Note: what + "; {Bind X} = VM; absent + a handle = the handle owns it; one-way/literal = read-only");

    private static AttrDef Placeholder() => new("placeholder", AttrKind.Text);

    private static AttrDef ReadOnly(string? note = null) =>
        new("readonly", AttrKind.Bool, "false", Note: note ?? "read-only also when value is one-way or a literal");

    private static AttrDef[] KnobRange() =>
    [
        new("value", AttrKind.Number, "0", Note: "{Bind X} = VM; absent + a handle = the handle owns it; one-way/literal = read-only; code writes are not snapped or clamped"),
        new("min", AttrKind.Number, "0"),
        new("max", AttrKind.Number, "100"),
        new("step", AttrKind.Number, "1", Note: "<= 0 = 1"),
        new("unit", AttrKind.Text),
    ];

    private static AttrDef[] Items(string? note = null) =>
    [
        new("items", AttrKind.Items, Note: note ?? "wins over DvOption children, which win over the enum of the value type"),
        new("item-value", AttrKind.Path, Bindable: false, Note: "read raw; absent = the item itself"),
        new("item-text", AttrKind.Path, Bindable: false, Note: "read raw; absent = the value as text"),
        new("item-icon", AttrKind.Path, Bindable: false, Note: "read raw"),
    ];

    // DvProgress 가 계기 공용 블록에서 읽기만 하고 그리지 않는 속성
    private static AttrDef GaugeDead(string name, AttrKind kind) =>
        new(name, kind, Note: "dead read: the block shared with DvGauge/DvRing reads it, the progress bar never uses it");

    // DvStack 계열 슈가(gap·padding·align·justify) — 기본값만 태그마다 다르다
    private static AttrDef[] Sugar(string? gap, string? align, string? justify, string? note = null) =>
    [
        new("gap", AttrKind.Length, gap, Note: note ?? "step name xs..2xl = token, none = 0, anything else used as is"),
        new("padding", AttrKind.Length, "0", Note: note ?? "1 to 4 values, each mapped like gap"),
        new("align", AttrKind.Enum, align, Values: ["start", "center", "end", "stretch"], Note: note ?? "passed to align-items unvalidated"),
        new("justify", AttrKind.Enum, justify, Values: ["start", "center", "end", "between"], Note: note ?? "between = space-between; others passed to justify-content unvalidated"),
    ];

    /// <summary>The definition of <paramref name="name"/> on <paramref name="tag"/> (tag-specific first, then common), or <see langword="null"/>.</summary>
    public static AttrDef? Find(string tag, string name) =>
        (Tags.TryGetValue(tag, out var own) ? own.FirstOrDefault(a => a.Name == name) : null) ?? Common.FirstOrDefault(a => a.Name == name);
}
