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
        new("content-align", AttrKind.Enum, Values: ["top-left", "top-center", "top-right", "middle-left", "middle-center", "middle-right", "bottom-left", "bottom-center", "bottom-right"],
            Note: EnumRule + " (ignored)"),
        new("grow", AttrKind.Number, "0"),
        new("width", AttrKind.Length),
        new("height", AttrKind.Length),
        new("visible", AttrKind.Bool, "true", Note: BoolRule + "; absent = shown"),
        new("disabled", AttrKind.Bool, "false", Note: BoolRule + "; inherited downward"),
        new("onclick", AttrKind.Expr, Bindable: false),
        new("hold", AttrKind.Number, "0", Note: "ms, rounded; <= 0 = none; " + NumberRule + "; ignored on tags in TagCatalog.HoldIgnored"),
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
            new("readonly", AttrKind.Bool, "false", Note: "the common read-only rule (" + ReadOnlyRule + "): header taps do not change the value; no read-only visual on tabs"),
            new("placeholder", AttrKind.Text, Note: "dead read: the block shared with DvSelect reads it, tabs never show it"),
            new("items", AttrKind.Items, Note: "dead read: the block shared with DvSelect builds options from it, then the DvTab children replace them"),
            new("item-value", AttrKind.Path, Note: "dead read (see items)"),
            new("item-text", AttrKind.Path, Note: "dead read (see items)"),
            new("item-icon", AttrKind.Path, Note: "dead read (see items)"),
        ],
        ["DvTab"] =
        [
            new("value", AttrKind.Text, Note: "resolved as text, then converted to the type of the DvTabs value"),
            new("text", AttrKind.Text, Note: "header text; falls back to value; a code Text override on the DvTab wins"),
            new("disabled", AttrKind.Bool, "false", Note: "copied to the header item (a code Enabled override on the DvTab wins over the markup text); also disables the body when that tab is selected"),
        ],
        ["DvExpander"] =
        [
            new("expanded", AttrKind.Bool, Note: "two-way {Bind X} follows the VM; one-way/literal locks it (" + BoolRule + "); absent = the screen remembers (collapsed)"),
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
            new("variant", AttrKind.Enum, Values: ["primary", "secondary", "danger", "ghost"], Note: "adds the classes btn btn-{variant}; empty after resolving = unstyled button; " + EnumRule + " (no classes)"),
            new("size", AttrKind.Enum, Values: ["sm", "lg"], Note: "adds btn-{size}; only when variant is set; " + EnumRule + " (no size class)"),
            new("value", AttrKind.Bool, Note: "present = latching button: on = " + BoolRule + "; {Bind X} flips on press; without variant adds btn btn-secondary, on adds btn-on; a handle cannot own it"),
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
        ["DvToggle"] = [BoolValue(), ReadOnly()],
        ["DvCheckBox"] = [BoolValue(), Text("a code Text override is honored by the build, but the handle exposes no Text"), ReadOnly()],
        ["DvRadio"] =
        [
            new("option", AttrKind.Text, "", Note: "converted to the type of the bound value for comparison"),
            new("value", AttrKind.Object, Note: "the group value; {Bind X} = VM; one-way/literal = that value (a handle override is read as checked); absent = the screen remembers the group; a handle cannot own it"),
            new("group", AttrKind.Text, Note: "read only when value is absent; no name + a handle = this element alone"),
            Text("a code Text override is honored by the build, but the handle exposes no Text"),
            ReadOnly("read-only also when value is one-way or a literal (absent value with group memory is writable)"),
        ],
        ["DvKnob"] =
        [
            .. KnobRange(),
            ReadOnly(),
            new("input-mode", AttrKind.Enum, "drag", Values: ["drag", "tap", "both"], Note: EnumRule),
        ],
        ["DvSlider"] =
        [
            .. KnobRange(),
            ReadOnly(),
            new("direction", AttrKind.Enum, "right", Values: Directions, Note: EnumRule),
            new("show-value", AttrKind.Bool, "false", Note: "value text (+ unit) beside the thumb; decimals come from step unless format is given; " + BoolRule),
            new("format", AttrKind.Text, Note: FormatRule),
        ],
        ["DvInput"] =
        [
            OwnedValue("text or a number (keyboard-type numeric)"),
            new("keyboard-type", AttrKind.Enum, "text", Values: ["text", "numeric", "password"], Note: EnumRule + "; the handle resolves it against the screen VM"),
            new("multiline", AttrKind.Bool, "false"),
            Placeholder(),
            new("format", AttrKind.Text, Note: "numeric display; " + FormatRule),
            new("min", AttrKind.Number, Note: "typed values out of range are rejected (not clamped), the stepper stops at the bound; numeric only: both given and min >= max = 0..100 + warning; " + NumberRule),
            new("max", AttrKind.Number, Note: "typed values out of range are rejected (not clamped), the stepper stops at the bound; numeric only: both given and min >= max = 0..100 + warning; " + NumberRule),
            new("unit", AttrKind.Text, Note: "drawn at the right edge inside the box (input layout), not after the value"),
            ReadOnly(),
            new("step", AttrKind.Number, "1", Note: "<= 0 = 1; " + NumberRule),
            new("show-stepper", AttrKind.Bool, "false", Note: "read only when keyboard-type is numeric"),
        ],
        ["DvSelect"] = [OwnedValue("converted to the bound property type"), Placeholder(), ReadOnly(), .. Items()],
        ["DvOption"] =
        [
            new("value", AttrKind.Text, Note: "resolved as text"),
            new("text", AttrKind.Text, Note: "falls back to value; inner text is not used; a code Text override on the DvOption wins"),
            new("icon", AttrKind.Text, Note: "fa-name; shown by DvSegment and DvListBox"),
        ],
        ["DvSegment"] =
        [
            OwnedValue("the option value is written without conversion to the bound type"),
            ReadOnly(),
            new("fill", AttrKind.Bool, "false"),
            new("placeholder", AttrKind.Text, Note: "dead read: the block shared with DvSelect reads it, a segment never shows it"),
            .. Items("not documented for DvSegment"),
        ],
        ["DvListBox"] =
        [
            new("selection-mode", AttrKind.Enum, "single", Values: ["single", "multi"], Note: EnumRule + "; the handle resolves it against the screen VM"),
            OwnedValue("single selection only (read when selection-mode is not multi)"),
            new("selected-items", AttrKind.Items, Note: "multi selection only (read when selection-mode is multi); two-way {Bind X} list"),
            ReadOnly("read-only also when the value attribute (value, or selected-items when multi) is one-way or a literal"),
            .. Items(),
        ],
        ["DvTreeView"] =
        [
            new("items", AttrKind.Items, Note: "root list; a string or a non-collection = no rows"),
            new("item-children", AttrKind.Path, Bindable: false, Note: "read raw; absent = every item is a leaf; depth limited to 64, cycles skipped"),
            new("item-text", AttrKind.Path, Bindable: false, Note: "read raw; absent = the item itself as text"),
            new("item-value", AttrKind.Path, Bindable: false, Note: "read raw; absent = the item itself; the handle also reads it raw"),
            OwnedValue("the selected item's value"),
            ReadOnly("read-only also when value is one-way or a literal; expand/collapse still works"),
        ],
        ["DvDateTimeInput"] =
        [
            OwnedValue("DateTime, DateOnly or TimeOnly (nullable)"),
            Placeholder(),
            ReadOnly(),
            new("mode", AttrKind.Enum, Values: ["date", "time", "datetime"], Note: "absent = from the value type; " + EnumRule + " (from the value type); a mismatch with the value type = from the value type + warning"),
            new("show-seconds", AttrKind.Bool, "false"),
            new("format", AttrKind.Text, Note: ".NET date format"),
        ],
        ["DvColorInput"] = [OwnedValue("#RRGGBB text or SKColor"), Placeholder(), ReadOnly()],
        ["DvProgress"] =
        [
            new("value", AttrKind.Number, "0", Note: "one-way; read by the block shared with DvGauge/DvRing"),
            new("min", AttrKind.Number, "0", Note: RangeRule),
            new("max", AttrKind.Number, "100", Note: RangeRule),
            Tone(),
            new("direction", AttrKind.Enum, "right", Values: Directions, Note: EnumRule),
            new("show-value", AttrKind.Bool, "false", Note: "value text (+ unit) in the center; horizontal: height 14 or more; vertical: only when the text fits in the width; " + BoolRule),
            new("unit", AttrKind.Text, Note: UnitRule),
            new("format", AttrKind.Text, Note: FormatRule),
            AlarmZones(),
            GaugeDead("ticks", AttrKind.Number), GaugeDead("minor-ticks", AttrKind.Number), GaugeDead("show-tick-labels", AttrKind.Bool),
            GaugeDead("label", AttrKind.Text), GaugeDead("sweep", AttrKind.Number), GaugeDead("thickness", AttrKind.Number),
        ],
        // 계기·차트·알람 (2.4)
        ["DvGauge"] =
        [
            .. DialRange(),
            new("ticks", AttrKind.Number, "0", Note: "major tick interval; 0 = no scale"),
            new("minor-ticks", AttrKind.Number, "0", Note: "minor tick interval; drawn only when ticks is set"),
            new("show-tick-labels", AttrKind.Bool, "true", Note: BoolRule),
            new("format", AttrKind.Text, Note: FormatRule),
            new("unit", AttrKind.Text, Note: UnitRule),
            new("show-value", AttrKind.Bool, "true", Note: "the value text (+ unit) below the needle; " + BoolRule),
            DialDead("tone", AttrKind.Text, "the gauge", "the needle tip uses the zone color or gauge-needle-tip; " + ThemeWordRule, def: "info"),
            DialDead("direction", AttrKind.Enum, "the gauge", EnumRule, Directions, "right"),
            DialDead("label", AttrKind.Text, "the gauge"), DialDead("sweep", AttrKind.Number, "the gauge", "the gauge sweep is fixed at 270"),
            DialDead("thickness", AttrKind.Number, "the gauge"),
        ],
        ["DvRing"] =
        [
            .. DialRange(),
            Tone("a zone color wins"),
            new("unit", AttrKind.Text, Note: UnitRule),
            new("label", AttrKind.Text, Note: "drawn below the value"),
            new("sweep", AttrKind.Number, "270", Note: "degrees, clamped to 1..360; " + NumberRule),
            new("thickness", AttrKind.Number, Note: "px; absent or <= 0 = 11% of the size; " + NumberRule),
            new("format", AttrKind.Text, Note: FormatRule),
            new("show-value", AttrKind.Bool, "true", Note: "the value text (+ unit) in the center; false hides it, label stays; " + BoolRule),
            DialDead("ticks", AttrKind.Number, "the ring"), DialDead("minor-ticks", AttrKind.Number, "the ring"),
            DialDead("show-tick-labels", AttrKind.Bool, "the ring"), DialDead("direction", AttrKind.Enum, "the ring", EnumRule, Directions, "right"),
        ],
        ["DvLamp"] =
        [
            new("on", AttrKind.Bool, "false", Note: BoolRule),
            new("blink", AttrKind.Bool, "false", Note: BoolRule + "; read only when blink-when is absent"),
            new("blink-when", AttrKind.Bool, Note: "another name for blink that wins over it; not in the doc table"),
            new("blink-interval", AttrKind.Number, "500", Note: "ms, rounded; <= 0 = 500; " + NumberRule),
            new("color", AttrKind.Color, Note: ColorRule + " (the state or text color); wins over state"),
            new("state", AttrKind.Text, Note: "state-{state} token color; valid when the theme has the token state-{state} (built-in: run stop fault warn manual off); " + ThemeWordRule + " (no state color)"),
        ],
        ["DvTank"] =
        [
            .. DialRange("separate fields from DvGauge/DvRing/DvProgress (TankValue/TankMin/TankMax)"),
            new("unit", AttrKind.Text, Note: UnitRule),
            new("format", AttrKind.Text, Note: FormatRule + "; the value chip is always drawn (no show-value)"),
            new("show-zone-marks", AttrKind.Bool, "true", Note: BoolRule + "; off hides the zone boundary marks; the zone colors stay either way"),
        ],
        ["DvSparkline"] =
        [
            new("values", AttrKind.Items, Note: "a list of numbers or comma-separated text; anything else = no values; an unreadable number = 0"),
            .. ChartCommon(),
            new("area", AttrKind.Bool, "false", Note: BoolRule),
            ChartDead("show-value", AttrKind.Bool, "the sparkline"),
            ChartDead("inner-radius", AttrKind.Number, "the sparkline"),
        ],
        ["DvBarChart"] =
        [
            .. ChartItems(),
            .. ChartCommon(),
            new("show-value", AttrKind.Bool, "false", Note: BoolRule),
            new("format", AttrKind.Text, Note: "the show-value numbers; " + FormatRule),
            ChartDead("area", AttrKind.Bool, "the bar chart"),
            ChartDead("inner-radius", AttrKind.Number, "the bar chart"),
        ],
        ["DvPieChart"] =
        [
            .. ChartItems(),
            new("show-value", AttrKind.Bool, "false", Note: BoolRule + "; shows the numbers (and the donut total) instead of percentages"),
            new("format", AttrKind.Text, Note: "the show-value numbers and the donut total (not the percentages); " + FormatRule),
            new("inner-radius", AttrKind.Number, "0", Note: "ratio, clamped to 0..0.9 (donut); " + NumberRule),
            ChartDead("tone", AttrKind.Text, "the pie chart", "slices use the fixed palette; " + ThemeWordRule, def: "info"),
            ChartDead("y-min", AttrKind.Number, "the pie chart"), ChartDead("y-max", AttrKind.Number, "the pie chart"),
            ChartDead("area", AttrKind.Bool, "the pie chart"),
        ],
        ["DvTrend"] =
        [
            new("source", AttrKind.Object, Note: "a TrendBuffer; any other type = no buffer data, silently"),
            new("window", AttrKind.Number, "60", Note: "seconds; <= 0 = 60; " + NumberRule),
            new("end", AttrKind.Object, Note: "a DateTime (absent = the latest sample); any other type (DateTimeOffset, text) is ignored silently"),
            new("y-min", AttrKind.Number, Note: "absent = automatic; " + NumberRule + " (automatic)"),
            new("y-max", AttrKind.Number, Note: "absent = automatic; " + NumberRule + " (automatic)"),
            new("area", AttrKind.Bool, "false", Note: BoolRule),
            new("smooth", AttrKind.Bool, "true", Note: BoolRule + "; empty = off"),
        ],
        ["DvTrendChannel"] =
        [
            new("name", AttrKind.Text, Note: "the source buffer key; absent = ch{index}"),
            new("label", AttrKind.Text, Note: "legend text; absent = name"),
            new("unit", AttrKind.Text, Note: "legend only"),
            new("color", AttrKind.Color, Note: "absent = palette[index]; " + ColorRule + " (the palette color)"),
            new("points", AttrKind.Items, Note: "an IEnumerable<TrendPoint> that wins over source; any other type = from source by name"),
        ],
        ["DvAlarmBanner"] =
        [
            .. AlarmCommon(),
            new("comm-alarm", AttrKind.Text, Note: "non-empty = the communication-loss strip with this text (blinks)"),
            new("row-height", AttrKind.Number, "36", Note: "dead read: the block shared with DvAlarmList reads it, the banner never uses it"),
        ],
        ["DvAlarmList"] =
        [
            .. AlarmCommon(),
            new("row-height", AttrKind.Number, "36", Note: "px; <= 0 = 36; " + NumberRule + " (the geometry keeps at least 24)"),
            new("comm-alarm", AttrKind.Text, Note: "dead read: the block shared with DvAlarmBanner reads it; not drawn (and since f841ebe it no longer keeps the blink timer running)"),
        ],

        // 그리드 (2.5)
        ["DvDataGrid"] =
        [
            new("items", AttrKind.Items, Note: "an IList; another IEnumerable is copied on every layout (warning); a string or a non-collection = header only"),
            new("row-height", AttrKind.Number, "48", Note: "px; <= 0 = 48, silently; " + NumberRule),
            new("header-height", AttrKind.Number, "40", Note: "px; <= 0 = 40, silently; " + NumberRule),
            new("selection-mode", AttrKind.Enum, "none", Values: ["none", "single", "multi"], Note: EnumRule),
            new("selection", AttrKind.Expr, Note: "read as written; {Bind X} = two-way list, otherwise resolved one-way; a single object = one row; applied only when selection-mode is not none"),
            new("row-key", AttrKind.Path, Bindable: false, Note: "read raw; absent = the item reference"),
            new("row-class", AttrKind.Path, Bindable: false, Note: "read raw; the item property value is a class name (background, color, font-weight only)"),
            new("onrowclick", AttrKind.Expr, Bindable: false, Note: "M(row) when the row fits, otherwise M(); a missing method is silent at run time"),
            GridColor("header-color"), GridColor("header-text-color"), GridColor("text-color"), GridColor("row-color"),
            GridColor("row-alt-color"), GridColor("hover-color"), GridColor("selected-color"), GridColor("line-color"),
            GridColor("summary-color"), GridColor("summary-text-color"),
        ],
        ["DvColumn"] =
        [
            new("header", AttrKind.Text, "", Note: "inline interpolation only (a binding is formatted into the text)"),
            new("field", AttrKind.Path, "", Bindable: false, Note: "read raw; empty = no value (no sort, no edit)"),
            new("width", AttrKind.Length, "*", Note: "px, %, * or n*; bound (resolved as text)"),
            new("align", AttrKind.Enum, Values: ["left", "center", "right"], Note: "absent = by value type (numbers right); " + EnumRule + " (by value type)"),
            new("format", AttrKind.Text, Note: FormatRule),
            GridFlag("frozen", "only a leading run of columns; frozen on a middle column warns and is ignored"),
            GridFlag("sortable"),
            new("sort", AttrKind.Enum, Values: ["asc", "desc"], Note: "initial sort, column order = priority; " + EnumRule + " (no sort)"),
            new("cell-class", AttrKind.Path, Bindable: false, Note: "read raw; the item property value is a class name"),
            new("cell-type", AttrKind.Enum, "text", Values: ["text", "check", "lamp", "button", "input", "select", "datetime", "color"], Note: EnumRule),
            new("filter", AttrKind.Enum, "none", Values: ["none", "auto", "text", "values", "range", "date"], Note: EnumRule),
            GridFlag("readonly", "check and edit cells"),
            new("onbeforecommit", AttrKind.Expr, Bindable: false, Note: "check cells: bool M(item, bool) first, otherwise bool M(bool)"),
            new("on-color", AttrKind.Color, Note: "lamp cells; absent = the state-run token; " + ColorRule + " (state-run); named on-color (DvLamp uses color)"),
            GridFlag("blink", "lamp cells; fixed 500 ms (no blink-interval)"),
            new("text", AttrKind.Text, "", Note: "button cells; inline interpolation only; a code Text override on the DvColumn wins and is used as is"),
            new("icon", AttrKind.Text, Note: "button cells"),
            new("variant", AttrKind.Enum, "primary", Values: ["primary", "secondary", "danger", "ghost"], Note: "button cells; " + EnumRule),
            new("onclick", AttrKind.Expr, Bindable: false, Note: "button cells; M(row) or M()"),
            new("enabled", AttrKind.Path, Bindable: false, Note: "button cells; read raw; an item bool property path (the common disabled is the inverse sense and is not read here)"),
            new("min", AttrKind.Number, Note: "input cells; typed values out of range are rejected; both given and min >= max = 0..100 + warning; " + NumberRule + " (ignored)"),
            new("max", AttrKind.Number, Note: "input cells; typed values out of range are rejected; both given and min >= max = 0..100 + warning; " + NumberRule + " (ignored)"),
            new("unit", AttrKind.Text, Note: "input cells; inline interpolation only"),
            new("mode", AttrKind.Enum, Values: ["date", "time", "datetime"], Note: "datetime cells; absent = by value type; " + EnumRule + " (by value type); a mismatch = by value type + warning"),
            GridFlag("show-seconds", "datetime cells"),
            .. Items("select cells (read only when cell-type is select); wins over DvOption children, which win over the enum of the value type"),
        ],
        ["DvColumnGroup"] =
        [
            new("header", AttrKind.Text, "", Note: "inline interpolation only; a group with no columns is dropped; split at the frozen boundary (warning)"),
        ],
        ["DvSummaryRow"] =
        [
            new("title", AttrKind.Text, "", Note: "inline interpolation only"),
            new("fn", AttrKind.Enum, Values: ["sum", "avg", "min", "max", "count"], Note: "numeric columns without a DvSummary; absent = none; " + EnumRule + " (none)"),
        ],
        ["DvSummary"] =
        [
            new("field", AttrKind.Path, "", Bindable: false, Note: "read raw; no matching column = warning, the cell is ignored"),
            new("fn", AttrKind.Enum, Values: ["sum", "avg", "min", "max", "count"], Note: "absent = none; " + EnumRule + " (none)"),
            new("format", AttrKind.Text, Note: "absent = the column format; " + FormatRule),
            new("value", AttrKind.Object, Note: "present = shown instead of the computed value (also when it resolves to null)"),
        ],

        // 공정도 (2.6) — 읽기는 ProcessBuilder(ProcessModel.cs). DvSymbol 정의(SymbolDef, 등록 시점)와 사용자 심볼 인스턴스의 DvParam 속성은 대상이 아니다
        ["DvProcess"] =
        [
            Direction(),
            new("look", AttrKind.Enum, "plain", Values: ["plain", "rich"], Note: EnumRule + "; units inherit it (no per-unit look)"),
            new("align", AttrKind.Enum, "auto", Values: ["auto", "step", "row"], Note: EnumRule + "; with placement=fixed ignored + warning"),
            new("placement", AttrKind.Enum, "auto", Values: ["auto", "fixed"], Note: EnumRule + "; a structural switch (the layout cache keys on it)"),
            new("base-size", AttrKind.Text, Note: "\"W H\" px (space or comma); fixed placement only, otherwise ignored + warning; a bad value = ignored + warning"),
            new("flow-animation", AttrKind.Bool, "false", Note: BoolRule),
            new("fit", AttrKind.Enum, "none", Values: ["none", "shrink", "contain"],
                Note: "contain without placement=fixed = shrink + warning; " + EnumRule + "; name clash: DvImage/DvVideo fit is contain|cover|fill"),
        ],
        ["DvUnit"] =
        [
            new("name", AttrKind.Text, Bindable: false, Note: "required, unique among the outer equipment (missing = placeholder + error); for a unit component read from the instance only, not the template; " + RawName),
            Direction(),
            new("label", AttrKind.Text, Note: "title at the top-left; for a unit component the instance wins over the template DvUnit"),
            ProcessInt("row", "for a unit component the instance wins over the template DvUnit"),
            ProcessInt("rank", "for a unit component the instance wins over the template DvUnit"),
            ProcessCoord("x", "unit frame center; for a unit component the instance wins over the template DvUnit"),
            ProcessCoord("y", "unit frame center; for a unit component the instance wins over the template DvUnit"),
            new("placement", AttrKind.Enum, "auto", Values: ["auto"], Note: "read only to warn (" + EnumRule + "): the inside is always laid out automatically"),
            ProcessVisible(),
        ],
        ["DvPort"] =
        [
            new("name", AttrKind.Text, Bindable: false, Note: "unit outer port (inside DvUnit only; elsewhere ignored + error); DvPort inside DvSymbol (name side at role) is parsed at registration and throws instead; " + RawName),
            new("side", AttrKind.Enum, Values: ["left", "top", "right", "bottom"], Note: "required; missing = right + error; case and surrounding spaces ignored; an unknown value = right + error"),
            new("at", AttrKind.Number, "0.5", Note: "0-1 along the side; not a number or outside = 0.5 + error"),
            ProcessVisible(),
        ],
        ["DvVessel"] =
        [
            .. Equipment("DvVessel"),
            VesselSize("width"),
            VesselSize("height"),
            new("level", AttrKind.Number, "0", Note: "state param (DvParam level): 0-100, clamped; " + NumberRule + "; DvTank uses value/min/max instead"),
        ],
        ["DvPump"] = [.. Equipment("DvPump"), StateBool("running")],
        ["DvValve"] = [.. Equipment("DvValve"), StateBool("open", "check has no open param")],
        ["DvValve3"] = [.. Equipment("DvValve3"), StateBool("open-b"), StateBool("open-c")],
        ["DvInline"] = Equipment("DvInline"),
        ["DvExchanger"] = Equipment("DvExchanger"),
        ["DvTerminal"] = Equipment("DvTerminal", "multi-line end-point text (&#10;)"),
        ["DvAgitator"] = Accessory(null),
        ["DvMotor"] = Accessory(["isa"]),
        ["DvAttach"] =
        [
            new("side", AttrKind.Enum, "right", Values: ["left", "right", "top", "bottom"], Note: EnumRule),
            new("visible", AttrKind.Bool, "true", Note: "the common visible rule (" + BoolRule + "), read by the process parser and the common build alike"),
        ],
        ["DvLine"] =
        [
            ProcessRef("from", "eq.port, eq, eq.face@0-1, unit.port or a port name inside a unit"),
            ProcessRef("to", "eq.port, eq, eq.face@0-1, unit.port or a port name inside a unit"),
            ProcessArrow(),
            new("flow", AttrKind.Bool, Note: "absent or a null binding = computed; otherwise " + BoolRule),
            new("medium", AttrKind.Enum, Values: Process.ProcessMedium.Names, Note: "absent = default gray; " + EnumRule + " (default gray); a fixed palette (no color attribute)"),
            new("via", AttrKind.Text, Note: "\"x,y x,y\" bend points; fixed placement only, otherwise ignored + warning; a bad format = automatic route + warning"),
            new("label", AttrKind.Text),
            new("visible", AttrKind.Bool, Note: "unused: lines are always drawn"),
        ],
        ["DvSignal"] =
        [
            ProcessRef("from", "an instrument or equipment name, no port"),
            ProcessRef("to", "an instrument or equipment name, no port"),
            ProcessArrow(),
            new("label", AttrKind.Text, Note: "small text beside the from end"),
            new("kind", AttrKind.Enum, "electric", Values: ["electric", "pneumatic"], Note: EnumRule),
            new("color", AttrKind.Color, Note: "absent = theme text color; " + ColorRule + " (theme text color); the only process color attribute (the rest use a fixed palette)"),
            new("width", AttrKind.Number, "1.6", Note: "stroke px > 0; not a number or <= 0 = default + warning; name clash: DvVessel width is the body width"),
            new("visible", AttrKind.Bool, Note: "unused: signal lines are always drawn"),
        ],
        ["DvInstrument"] =
        [
            new("name", AttrKind.Text, Bindable: false, Note: "required, unique together with equipment names (missing or duplicate = error, not connectable); " + RawName),
            new("on", AttrKind.Text, Bindable: false, Note: "equipment name; required unless tap; no such equipment = not drawn + error; " + RawName),
            new("tap", AttrKind.Text, Bindable: false, Note: "eq.port or eq.face@pos; wins over on (a different on = warning); bad = not drawn + error; " + RawName),
            new("tag", AttrKind.Text, Note: "ISA letters (TI, PIC); name clash: not an element tag"),
            new("number", AttrKind.Text),
            new("value", AttrKind.Text, Note: "display text in the bubble; name clash: elsewhere value is the control value"),
            new("panel", AttrKind.Bool, "false", Note: BoolRule),
            ProcessCoord("x", "bubble center; one of x/y missing = placed automatically + warning (still drawn)"),
            ProcessCoord("y", "bubble center; one of x/y missing = placed automatically + warning (still drawn)"),
            ProcessVisible(),
        ],
    };

    // 2.6 공정도 공용 조각
    // 3단계 R1·R7 — 모든 Bool·Number 속성의 공용 해석(Layout.AttrRules)
    private const string BoolRule = "common boolean rule: true, a non-zero number = on; false, 0, empty or null = off; any other text = the default + warning";
    private const string NumberRule = "invariant culture; not a number = the default + warning";
    // 3단계 R2·R3·R4(D3) — 색·목록 값(Layout.AttrRules.Color·Enum), 공정도 이름·참조는 원문
    private const string ColorRule = "#hex or token(name); an unknown token = magenta + warning; unparsable = the default + warning";
    private const string EnumRule = "case and surrounding spaces ignored; an unknown value = the default + warning";
    // 3.4 — tone·DvLamp state: 고정 목록이 아니라 지금 테마의 토큰(Layout.AttrRules.ThemeWord) — 생성기(DVML020)는 값을 보지 않는다
    // 3단계 R8 — 값 글자(Layout.AttrRules.FormatOrNull)
    private const string FormatRule = ".NET format string, invariant culture, bindable; a format not valid for the value type = the default text + warning";
    private const string UnitRule = "drawn after the value text with one space";
    // 3단계 R6 — 범위(Layout.AttrRules.Range)
    private const string RangeRule = "min >= max = 0..100 + warning; " + NumberRule;
    private const string ThemeWordRule = "case and surrounding spaces ignored; a word without that theme token = the default + warning; not checked at build time (the theme is the app's)";
    private const string RawName = "read raw (process names and references are not bindable; a { in it = warning, used as written)";
    private static string[] Directions => ["right", "left", "up", "down"];

    private static AttrDef Direction() =>
        new("direction", AttrKind.Enum, "lr", Values: ["lr", "tb"], Note: EnumRule + "; name clash: flex direction is row|column");

    private static AttrDef ProcessVisible() =>
        new("visible", AttrKind.Bool, "true", Note: "the common visible rule (" + BoolRule + "); a handle Visible override wins");

    private static AttrDef ProcessInt(string name, string? note = null) =>
        new(name, AttrKind.Number, Note: "layout hint (integer, rounded); not a number = ignored + warning; with placement=fixed ignored + warning" + (note is null ? "" : "; " + note));

    private static AttrDef ProcessCoord(string name, string where) =>
        new(name, AttrKind.Number, Note: $"px, {where}; placement=fixed only (otherwise ignored + warning)");

    private static AttrDef ProcessRef(string name, string what) =>
        new(name, AttrKind.Text, Bindable: false, Note: $"{what}; {RawName}");

    private static AttrDef ProcessArrow() =>
        new("arrow", AttrKind.Enum, "none", Values: ["end", "start", "both", "none"], Note: EnumRule);

    private static AttrDef StateBool(string name, string? note = null) =>
        new(name, AttrKind.Bool, Note: $"state param (DvParam {name}): {BoolRule}; a boolean, not the UI state words (state=run|stop|fault)" + (note is null ? "" : "; " + note));

    private static AttrDef VesselSize(string name) =>
        new(name, AttrKind.Number, Note: $"body {name} px, 0.6-4x the kind's natural size (outside = clamped + warning; not a number = natural + warning); "
            + $"the common layout also applies it as the box CSS {name}; name clash: DvSignal width is the stroke");

    // 장비 공통 — name·label·kind·rotate·flip·row·rank·x·y·text·visible (상태 속성은 태그마다)
    private static AttrDef[] Equipment(string tag, string? textNote = null)
    {
        List<AttrDef> list =
        [
            new("name", AttrKind.Text, Bindable: false, Note: "required, unique within the diagram or unit (missing = placeholder + error, duplicate = error); " + RawName),
            new("label", AttrKind.Text, Note: "one-line name chip"),
        ];
        if (Process.BuiltInSymbols.Kinds.TryGetValue(tag, out var kinds))
            list.Add(new("kind", AttrKind.Enum, kinds[0], Values: kinds, Note: EnumRule + "; elsewhere the look selector is variant"));
        list.AddRange(
        [
            new("rotate", AttrKind.Enum, "0", Values: ["0", "90", "180", "270"], Note: EnumRule + (tag == "DvTerminal" ? "; read, but a terminal does not rotate" : "")),
            new("flip", AttrKind.Enum, Values: ["h", "v", "hv"], Note: "vh is also accepted (= hv); " + EnumRule + " (no flip)"),
            ProcessInt("row"),
            ProcessInt("rank"),
            ProcessCoord("x", "symbol center (both needed, otherwise not drawn + error)"),
            ProcessCoord("y", "symbol center (both needed, otherwise not drawn + error)"),
            new("text", AttrKind.Text, Note: textNote ?? "dead read: read on every equipment tag, drawn only by DvTerminal"),
            ProcessVisible(),
        ]);
        return [.. list];
    }

    // 부속(DvAgitator·DvMotor) — 상자가 없어 공통 속성(class·style·onclick)은 읽지 않는다
    private static AttrDef[] Accessory(string[]? kinds) =>
    [
        new("running", AttrKind.Bool, "false", Note: BoolRule + "; a boolean, not the UI state words"),
        new("kind", AttrKind.Enum, Values: kinds, Note: kinds is null ? "no kinds: any non-empty value = the default shape + warning" : "absent = the default shape; " + EnumRule),
        ProcessVisible(),
    ];

    // 2.5 그리드 공용 조각
    private static AttrDef GridColor(string name) =>
        new(name, AttrKind.Color, Note: "absent = the grid-* theme token; " + ColorRule + " (the grid-* theme token)");

    private static AttrDef GridFlag(string name, string? note = null) =>
        new(name, AttrKind.Bool, "false", Note: (note is null ? "" : note + "; ") + BoolRule);

    // 2.3 공용 조각 — 같은 해석을 여러 태그가 쓴다
    private static AttrDef Text(string? note = null) =>
        new("text", AttrKind.Text, "", Note: note ?? "absent = inner text; a code Text override wins and is used as is");

    private static AttrDef Icon() => new("icon", AttrKind.Text, Note: "fa-name / far fa-name / fab fa-name; unknown = not drawn (Trace)");

    private static AttrDef Fit() => new("fit", AttrKind.Enum, "contain", Values: ["contain", "cover", "fill"], Note: EnumRule);

    private static AttrDef BoolValue() =>
        new("value", AttrKind.Bool, Note: "on = " + BoolRule + "; {Bind X} = VM; absent + a handle = the handle owns it; one-way/literal = read-only");

    private static AttrDef OwnedValue(string what) =>
        new("value", AttrKind.Object, Note: what + "; {Bind X} = VM; absent + a handle = the handle owns it; one-way/literal = read-only");

    private static AttrDef Placeholder() => new("placeholder", AttrKind.Text);

    // 3단계 R5 — 모든 값 위젯 같은 판단식·모양(LayoutEngine.ReadOnlyOf, Painter.PaintReadOnlyEdge / .input-readonly)
    private const string ReadOnlyRule = BoolRule + "; read-only = this is true or the value is one-way/literal; the value is shown as is, taps/drags/keypad do not write it (no dimming)";

    private static AttrDef ReadOnly(string? note = null) =>
        new("readonly", AttrKind.Bool, "false", Note: (note ?? "read-only also when value is one-way or a literal") + "; " + ReadOnlyRule + "; look: dashed outline (.input-readonly on box widgets)");

    private static AttrDef[] KnobRange() =>
    [
        new("value", AttrKind.Number, "0", Note: "{Bind X} = VM; absent + a handle = the handle owns it; one-way/literal = read-only; code writes are not snapped or clamped"),
        new("min", AttrKind.Number, "0", Note: RangeRule + "; drag and keypad never write a value outside"),
        new("max", AttrKind.Number, "100", Note: RangeRule + "; drag and keypad never write a value outside"),
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

    // 2.4 공용 조각
    private static AttrDef[] DialRange(string? note = null) =>
    [
        new("value", AttrKind.Number, "0", Note: (note is null ? "" : note + "; ") + "one-way; " + NumberRule + "; drawn clamped to min..max (the value itself is not changed)"),
        new("min", AttrKind.Number, "0", Note: RangeRule),
        new("max", AttrKind.Number, "100", Note: RangeRule),
        AlarmZones(),
    ];

    private static AttrDef AlarmZones() =>
        new("alarm-zones", AttrKind.Text, Note: "\"from:to:color ...\", color = #hex or token(name) (an unknown token = magenta + warning); "
            + "a bad zone (format, number, from >= to, color, from below the previous zone) is dropped + warning, the others stay");

    private static AttrDef Tone(string? note = null) =>
        new("tone", AttrKind.Text, "info", Note: "tone-{tone} token; valid when the theme has the token tone-{tone} (built-in: neutral info success warning danger); empty = info; " + ThemeWordRule + (note is null ? "" : "; " + note));

    // DvGauge/DvRing 가 계기 공용 블록에서 읽기만 하고 그리지 않는 속성
    private static AttrDef DialDead(string name, AttrKind kind, string who, string? why = null, string[]? values = null, string? def = null) =>
        new(name, kind, def, Values: values, Note: $"dead read: the block shared by DvGauge/DvRing/DvProgress reads it, {who} never uses it" + (why is null ? "" : "; " + why));

    // DvSparkline·DvBarChart 공용
    private static AttrDef[] ChartCommon() =>
    [
        Tone(),
        new("y-min", AttrKind.Number, Note: "absent = automatic; " + NumberRule + " (automatic)"),
        new("y-max", AttrKind.Number, Note: "absent = automatic; " + NumberRule + " (automatic)"),
    ];

    private static AttrDef[] ChartItems() =>
    [
        new("items", AttrKind.Items, Note: "a list (a string or a non-collection = no items)"),
        new("item-value", AttrKind.Path, Bindable: false, Note: "read raw; absent = the item itself; an unreadable number = 0"),
        new("item-text", AttrKind.Path, Bindable: false, Note: "read raw; absent = the item itself as text"),
    ];

    // 차트 공용 블록이 모든 차트 태그에서 읽지만 이 차트는 쓰지 않는 속성
    private static AttrDef ChartDead(string name, AttrKind kind, string who, string? why = null, string[]? values = null, string? def = null) =>
        new(name, kind, def, Values: values, Note: $"dead read: the block shared by the charts reads it, {who} never uses it" + (why is null ? "" : "; " + why));

    private static AttrDef[] AlarmCommon() =>
    [
        new("items", AttrKind.Items, Note: "an IEnumerable<AlarmRow>; any other type = no rows, silently"),
        new("onack", AttrKind.Expr, Bindable: false, Note: "method M(AlarmRow) or M(long id); a missing method is silent"),
        new("show-ack", AttrKind.Bool, Note: "absent = shown only when onack is set; present = " + BoolRule + " (default shown)"),
    ];

    // DvProgress 가 계기 공용 블록에서 읽기만 하고 그리지 않는 속성
    private static AttrDef GaugeDead(string name, AttrKind kind) =>
        new(name, kind, Note: "dead read: the block shared with DvGauge/DvRing reads it, the progress bar never uses it");

    // DvStack 계열 슈가(gap·padding·align·justify) — 기본값만 태그마다 다르다
    private static AttrDef[] Sugar(string? gap, string? align, string? justify, string? note = null) =>
    [
        new("gap", AttrKind.Length, gap, Note: note ?? "step name xs..2xl = token, none = 0, anything else used as is"),
        new("padding", AttrKind.Length, "0", Note: note ?? "1 to 4 values, each mapped like gap"),
        new("align", AttrKind.Enum, align, Values: ["start", "center", "end", "stretch"], Note: (note is null ? "" : note + "; ") + "align-items; " + EnumRule),
        new("justify", AttrKind.Enum, justify, Values: ["start", "center", "end", "between"], Note: (note is null ? "" : note + "; ") + "between = space-between; " + EnumRule),
    ];

    /// <summary>The definition of <paramref name="name"/> on <paramref name="tag"/> (tag-specific first, then common), or <see langword="null"/>.</summary>
    public static AttrDef? Find(string tag, string name) =>
        (Tags.TryGetValue(tag, out var own) ? own.FirstOrDefault(a => a.Name == name) : null) ?? Common.FirstOrDefault(a => a.Name == name);
}
