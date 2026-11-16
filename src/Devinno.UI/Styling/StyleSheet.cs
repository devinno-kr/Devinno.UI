using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Devinno.UI.Styling;

/// <summary>
/// Per-state declaration set for one class (<c>.name</c>). Obtained with <see cref="StyleSheet.TryGet"/>.
/// </summary>
/// <remarks>
/// Each property is the declaration list string inside the braces as written (e.g. <c>background: token(primary); color:#fff</c>); tokens are not yet substituted.
/// If a rule for the same class and state appears several times, the declarations are joined with <c>;</c>, so the later declaration wins when parsed. <see langword="null"/> if there is no rule for that state.
/// </remarks>
public sealed class ClassRule
{
    /// <summary>Declarations of the stateless base rule <c>.name { … }</c>. <see langword="null"/> if none.</summary>
    public string? Base { get; internal set; }
    /// <summary>Declarations of <c>.name:hover { … }</c>, applied while the pointer is over the element. <see langword="null"/> if none.</summary>
    public string? Hover { get; internal set; }
    /// <summary>Declarations of <c>.name:press { … }</c> or <c>.name:active { … }</c> (an alias with the same meaning), applied while pressed. <see langword="null"/> if none.</summary>
    public string? Press { get; internal set; }
    /// <summary>Declarations of <c>.name:disabled { … }</c>, applied while disabled. <see langword="null"/> if none.</summary>
    public string? Disabled { get; internal set; }
}

/// <summary>
/// A collection of class style rules. The theme stylesheet (<see cref="DvTheme.Stylesheet"/>) and the contents of markup <c>&lt;DvStyle&gt;</c> are parsed into this form.
/// </summary>
/// <remarks>
/// <para><b>Syntax</b>: a sequence of <c>셀렉터 { 선언; 선언 }</c> (selector { declaration; declaration }). The only supported selectors are a single class <c>.name</c> and
/// <c>.name:hover</c>, <c>:press</c>, <c>:active</c> (= press) and <c>:disabled</c> (case-sensitive); several selectors can be listed separated by commas.
/// Widget parts are styled with the CSS <c>::part()</c> selector on a tag or a class: <c>DvGauge::part(value) { font-size: 40 }</c>, <c>.big::part(value) { color: token(primary) }</c>
/// (keys <c>font-size</c>, <c>font-weight</c>, <c>color</c>; a class part rule wins over the tag part rule). Parts: <c>DvGauge</c> value, unit, tick · <c>DvRing</c> value, unit, name ·
/// <c>DvKnob</c> value, unit · <c>DvTank</c> value, unit, tick · <c>DvProgress</c> value · <c>DvSlider</c> value.
/// Unsupported selectors such as <c>.a.b</c>, tag, descendant or <c>#id</c> selectors are ignored, with a warning (<see cref="Trace"/>) for that selector only.
/// <c>/* … */</c> comments are removed. Braces cannot be used inside declaration values.</para>
/// <para><b>Declarations</b>: use the same keys as the inline <c>style</c> (see <see cref="ComputedStyle"/>), and <c>token(...)</c> can be used.</para>
/// <para><b>Precedence</b> (low → high): layout sugar &lt; class &lt; class:state &lt; <c>content-align</c> attribute &lt; inline <c>style</c> &lt; inline <c>hover-style</c>, etc.
/// If an element has several classes (<c>class="a b"</c>), the later class wins. The built-in part of the theme stylesheet (<see cref="BuiltInStyles.Sheet"/>) is added first, then the theme's style switch rules (quiet selection for <c>look.selection: quiet</c>, no shadows for <c>look.texture: flat</c>), then the rest of the theme stylesheet (what a theme appends after <c>BuiltInStyles.Sheet</c>, or a whole replacement sheet), then <c>&lt;DvStyle&gt;</c> inside component definitions (<see cref="Components.ComponentRegistry"/>), and the screen markup's <c>&lt;DvStyle&gt;</c> last, so for the same class and property the one added later wins.</para>
/// <para><b>Defining classes in code</b>: the stylesheet used for rendering comes from the theme, so to add classes in code,
/// create a theme by passing <c>BuiltInStyles.Sheet + 내 CSS</c> (your CSS) to <see cref="DvTheme(IReadOnlyDictionary{string, string}, string)"/>.
/// To write them directly in markup, use the <c>&lt;DvStyle&gt;</c> element. <see cref="Parse"/>/<see cref="TryGet"/> are for tools and custom widgets that read rules directly.</para>
/// <para>Threading: <see cref="Add"/> is not thread-safe. Once built and only read, it can be used from multiple threads.</para>
/// </remarks>
/// <example>
/// <code>
/// // Theme that keeps the built-in classes and adds a .dial class
/// var theme = new DvTheme(DvTheme.Dark.Tokens, BuiltInStyles.Sheet + @"
///     .dial       { width:160; height:160; border-radius:80; background: token(surface-raised) }
///     .dial:hover { border: 2 token(primary) }");
/// var surface = new DvSurface("&lt;DvBox class=\"dial\"/&gt;", null, theme);
///
/// // Read a rule directly
/// var sheet = StyleSheet.Parse(".dial { width:160 } .dial:hover { border: 2 #2563eb }");
/// if (sheet.TryGet("dial", out var rule))
///     Console.WriteLine(rule.Hover);   // border: 2 #2563eb
/// </code>
/// </example>
public sealed class StyleSheet
{
    private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Selector = new(
        @"^\.([A-Za-z0-9_-]+)(?::(hover|press|active|disabled))?$", RegexOptions.Compiled);

    // 위젯 부분 선택자: DvGauge::part(value) / .big::part(value) — 상태(:hover 등)는 받지 않는다
    private static readonly Regex PartSelector = new(
        @"^(?:\.([A-Za-z0-9_-]+)|(Dv[A-Za-z0-9_]+))::part\(\s*([A-Za-z0-9_-]+)\s*\)$", RegexOptions.Compiled);

    private readonly Dictionary<string, ClassRule> _rules = new(StringComparer.Ordinal);

    // (주인, 부분) → 선언. 주인 = 태그 이름(DvGauge) 또는 ".클래스"
    private readonly Dictionary<(string Owner, string Part), string> _parts = new();

    /// <summary>Finds the declarations of a widget part rule: <paramref name="owner"/> is a tag (<c>DvGauge</c>) or <c>.class</c>, <paramref name="part"/> the part name.</summary>
    internal bool TryGetPart(string owner, string part, out string declarations)
        => _parts.TryGetValue((owner, part), out declarations!);

    /// <summary>The part rules of one owner (a tag or <c>.class</c>), as (part name, declarations).</summary>
    internal IEnumerable<(string Part, string Declarations)> PartsOf(string owner)
        => _parts.Where(p => p.Key.Owner == owner).Select(p => (p.Key.Part, p.Value));

    /// <summary>Whether the stylesheet has any widget part rule.</summary>
    internal bool HasParts => _parts.Count > 0;

    /// <summary>Parses CSS text into a new stylesheet. Equivalent to calling <see cref="Add"/> once on an empty stylesheet.</summary>
    /// <param name="css">Stylesheet text. <see langword="null"/> or whitespace gives an empty stylesheet.</param>
    /// <returns>A new <see cref="StyleSheet"/>. Does not throw on format errors; an unclosed last block is discarded.</returns>
    public static StyleSheet Parse(string? css)
    {
        var sheet = new StyleSheet();
        sheet.Add(css);
        return sheet;
    }

    /// <summary>Finds a rule by class name.</summary>
    /// <param name="className">Class name without the dot (<c>.</c>) (e.g. <c>btn-primary</c>). Case-sensitive.</param>
    /// <param name="rule">The class's <see cref="ClassRule"/> if found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the rule exists.</returns>
    public bool TryGet(string className, out ClassRule rule) => _rules.TryGetValue(className, out rule!);

    /// <summary>
    /// Appends the rules of the CSS text to this stylesheet. If the same class and state already exist, the declarations are joined and the later declaration wins.
    /// </summary>
    /// <param name="css">Stylesheet text to append. Does nothing if <see langword="null"/> or whitespace.</param>
    public void Add(string? css)
    {
        if (string.IsNullOrWhiteSpace(css))
            return;

        var text = Comment.Replace(css, "");
        var pos = 0;
        while (pos < text.Length)
        {
            var open = text.IndexOf('{', pos);
            if (open < 0)
                break;
            var close = text.IndexOf('}', open + 1);
            if (close < 0)
                break;

            var selectors = text[pos..open];
            var body = string.Join(' ', text[(open + 1)..close].Split('\n', '\r')).Trim();
            pos = close + 1;

            foreach (var raw in selectors.Split(','))
            {
                var sel = raw.Trim();
                if (sel.Length == 0)
                    continue;

                var pm = PartSelector.Match(sel);
                if (pm.Success)
                {
                    var key = (pm.Groups[1].Success ? "." + pm.Groups[1].Value : pm.Groups[2].Value, pm.Groups[3].Value);
                    _parts[key] = Join(_parts.GetValueOrDefault(key), body)!;
                    continue;
                }

                var m = Selector.Match(sel);
                if (!m.Success)
                {
                    Trace.TraceWarning($"Devinno.UI: unsupported selector '{sel}' ignored");
                    continue;
                }

                if (!_rules.TryGetValue(m.Groups[1].Value, out var rule))
                    _rules[m.Groups[1].Value] = rule = new ClassRule();

                switch (m.Groups[2].Value)
                {
                    case "":
                        rule.Base = Join(rule.Base, body);
                        break;
                    case "hover":
                        rule.Hover = Join(rule.Hover, body);
                        break;
                    case "press":
                    case "active":
                        rule.Press = Join(rule.Press, body);
                        break;
                    case "disabled":
                        rule.Disabled = Join(rule.Disabled, body);
                        break;
                }
            }
        }
    }

    internal static string? Join(params string?[] parts)
    {
        var list = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return list.Count == 0 ? null : string.Join(';', list);
    }
}
