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
/// Unsupported selectors such as <c>.a.b</c>, tag, descendant or <c>#id</c> selectors are ignored, with a warning (<see cref="Trace"/>) for that selector only.
/// <c>/* … */</c> comments are removed. Braces cannot be used inside declaration values.</para>
/// <para><b>Declarations</b>: use the same keys as the inline <c>style</c> (see <see cref="ComputedStyle"/>), and <c>token(...)</c> can be used.</para>
/// <para><b>Precedence</b> (low → high): layout sugar &lt; class &lt; class:state &lt; <c>content-align</c> attribute &lt; inline <c>style</c> &lt; inline <c>hover-style</c>, etc.
/// If an element has several classes (<c>class="a b"</c>), the later class wins. The theme stylesheet is added first, then <c>&lt;DvStyle&gt;</c> inside component definitions (<see cref="Components.ComponentRegistry"/>), and the screen markup's <c>&lt;DvStyle&gt;</c> last, so for the same class and property the one added later wins.</para>
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

    private readonly Dictionary<string, ClassRule> _rules = new(StringComparer.Ordinal);

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
