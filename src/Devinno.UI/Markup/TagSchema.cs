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
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<AttrDef>> Tags = new Dictionary<string, IReadOnlyList<AttrDef>>(StringComparer.Ordinal);

    /// <summary>The definition of <paramref name="name"/> on <paramref name="tag"/> (tag-specific first, then common), or <see langword="null"/>.</summary>
    public static AttrDef? Find(string tag, string name) =>
        (Tags.TryGetValue(tag, out var own) ? own.FirstOrDefault(a => a.Name == name) : null) ?? Common.FirstOrDefault(a => a.Name == name);
}
