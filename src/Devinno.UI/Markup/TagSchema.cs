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
    };

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
