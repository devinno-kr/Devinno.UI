using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Devinno.UI.Markup;

/// <summary>A range of characters in a markup text: <see cref="Start"/> is the index of the first character, <see cref="Length"/> the number of characters.</summary>
/// <param name="Start">Index of the first character (0-based).</param>
/// <param name="Length">Number of characters.</param>
public readonly record struct MarkupSpan(int Start, int Length)
{
    /// <summary>Index just past the last character (<see cref="Start"/> + <see cref="Length"/>).</summary>
    public int End => Start + Length;
}

/// <summary>
/// A markup text together with its element tree and where each element and attribute sits in the text. Edits change only the characters they must,
/// so comments, indentation, attribute order, quotes and everything else in the text stay as written.
/// </summary>
/// <remarks>
/// <para><b>When to use</b>: tools that change markup by element rather than by text — a screen editor that moves a widget and writes back its new
/// <c>style</c>, or an AI tool that sets one attribute. Rewriting the whole text from the tree would reformat it and make every diff huge; these edits do not.
/// To show a screen, keep passing the text (<see cref="Text"/>) to <see cref="DvSurface"/>/<see cref="DvRenderer"/> as usual.</para>
/// <para><b>Immutable</b>: every edit returns a new document parsed from the new text, with a new <see cref="Root"/> tree. Nodes of the old document
/// are not part of the new one (passing them throws); find the element again in the new tree, for example by its child-index path from the root.</para>
/// <para><b>Positions</b>: <see cref="SpanOf"/>, <see cref="AttributeSpan"/> and <see cref="NodeAt"/> connect the tree with the text, for example to
/// highlight the selected element in a code editor, or to select the element under the code editor's caret. Offsets count UTF-16 characters of <see cref="Text"/>.</para>
/// <para><see cref="Root"/> is the same tree <see cref="MarkupParser.Parse"/> returns for the same text, and parsing fails the same way. The class has no shared state.</para>
/// </remarks>
/// <example>
/// <code>
/// var doc = MarkupDocument.Parse(File.ReadAllText("Main.dvml"));
/// var button = doc.Root.Children[1];
/// doc = doc.SetAttribute(button, "style", StyleParser.SetDeclaration(button.Attributes.GetValueOrDefault("style") ?? "", "left", "120"));
/// File.WriteAllText("Main.dvml", doc.Text); // only the style value changed in the file
/// </code>
/// </example>
public sealed class MarkupDocument
{
    private readonly Dictionary<ElementNode, Info> _info;
    private readonly List<ElementNode> _order; // 문서 순서(앞 순회)

    private MarkupDocument(string text, ElementNode root, List<ElementNode> order, Dictionary<ElementNode, Info> info)
    {
        Text = text;
        Root = root;
        _order = order;
        _info = info;
    }

    /// <summary>The markup text, exactly as given or as produced by the last edit.</summary>
    public string Text { get; }

    /// <summary>The element tree of <see cref="Text"/> — the same tree <see cref="MarkupParser.Parse"/> returns. Do not change it: edits go through this class's methods.</summary>
    public ElementNode Root { get; }

    /// <summary>Parses a markup text and records where each element and attribute is.</summary>
    /// <param name="text">An XML markup string with a single root element.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is <see langword="null"/>, empty or whitespace only.</exception>
    /// <exception cref="XmlException">The XML syntax is invalid (the same errors as <see cref="MarkupParser.Parse"/>, with line and column).</exception>
    public static MarkupDocument Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("markup is empty", nameof(text));

        var xdoc = XDocument.Parse(text); // 문법 판정은 Parse 와 같은 XML 파서
        var order = new List<ElementNode>();
        var root = MarkupParser.Convert(xdoc.Root!, order);
        var scanned = Scan(text);
        if (scanned.Count != order.Count)
            throw new InvalidOperationException($"markup scan found {scanned.Count} elements, the XML parser {order.Count}");

        var info = new Dictionary<ElementNode, Info>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < order.Count; i++)
            info[order[i]] = scanned[i];
        return new MarkupDocument(text, root, order, info);
    }

    /// <summary>The whole element in the text: from its <c>&lt;</c> to the end of its end tag (or of <c>/&gt;</c>).</summary>
    /// <param name="node">An element of this document's <see cref="Root"/> tree.</param>
    /// <returns>The element's span.</returns>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not in this document.</exception>
    public MarkupSpan SpanOf(ElementNode node)
    {
        var i = InfoOf(node);
        return new MarkupSpan(i.Start, i.End - i.Start);
    }

    /// <summary>The value of an attribute in the text — the characters between the quotes, as written (entities such as <c>&amp;amp;</c> not decoded).</summary>
    /// <param name="node">An element of this document's <see cref="Root"/> tree.</param>
    /// <param name="name">The attribute name (as in <see cref="ElementNode.Attributes"/>).</param>
    /// <returns>The value span, or <see langword="null"/> if the element has no such attribute.</returns>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not in this document.</exception>
    public MarkupSpan? AttributeSpan(ElementNode node, string name) =>
        FindAttr(InfoOf(node), name) is { } a ? new MarkupSpan(a.ValueStart, a.ValueEnd - a.ValueStart) : null;

    /// <summary>The innermost element whose text contains <paramref name="offset"/> (its <see cref="SpanOf"/> range).</summary>
    /// <param name="offset">A character index into <see cref="Text"/>.</param>
    /// <returns>The element, or <see langword="null"/> if the offset is outside the root element (a comment before it, the end of the text, or out of range).</returns>
    public ElementNode? NodeAt(int offset)
    {
        ElementNode? hit = null;
        foreach (var n in _order) // 앞 순회 — 뒤에 맞는 것이 더 안쪽
        {
            var i = _info[n];
            if (offset >= i.Start && offset < i.End)
                hit = n;
        }

        return hit;
    }

    /// <summary>
    /// Sets an attribute. If the element has it, only the characters of its value change and the original quote character is kept;
    /// otherwise <c>name="value"</c> is added after the last attribute (on its own line, with the same indent, when the attributes are written one per line).
    /// </summary>
    /// <param name="node">An element of this document's <see cref="Root"/> tree.</param>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The new value, unescaped (<c>&amp;</c>, <c>&lt;</c>, the quote and line breaks are escaped in the text).</param>
    /// <returns>A new document.</returns>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not in this document, or <paramref name="name"/> is not a valid XML name.</exception>
    public MarkupDocument SetAttribute(ElementNode node, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var info = InfoOf(node);
        VerifyName(name);
        if (FindAttr(info, name) is { } a)
            return Replace(a.ValueStart, a.ValueEnd, Escape(value, a.Quote));

        var attr = $"{name}=\"{Escape(value, '"')}\"";
        if (info.Attrs.Count > 0)
        {
            var last = info.Attrs[^1];
            var lastLine = LineStart(last.NameStart);
            var ownLine = lastLine > info.Start && Text[lastLine..last.NameStart].All(c => c is ' ' or '\t');
            var sep = ownLine ? NewLine() + Text[lastLine..last.NameStart] : " ";
            return Replace(last.End, last.End, sep + attr);
        }

        return Replace(info.NameEnd, info.NameEnd, " " + attr);
    }

    /// <summary>Removes an attribute together with the space or line break before it. Does nothing if the element has no such attribute.</summary>
    /// <param name="node">An element of this document's <see cref="Root"/> tree.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>A new document, or this document if there was nothing to remove.</returns>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not in this document.</exception>
    public MarkupDocument RemoveAttribute(ElementNode node, string name)
    {
        var info = InfoOf(node);
        if (FindAttr(info, name) is not { } a)
            return this;
        var from = a.NameStart;
        while (from > info.NameEnd && char.IsWhiteSpace(Text[from - 1]))
            from--;
        return Replace(from, a.End, "");
    }

    /// <summary>
    /// Inserts a child element. The new child follows the indentation of its siblings; a self-closing or empty parent is opened
    /// (<c>&lt;DvBox/&gt;</c> becomes <c>&lt;DvBox&gt;</c> … <c>&lt;/DvBox&gt;</c>). Every line of a multi-line child is indented.
    /// </summary>
    /// <param name="parent">An element of this document's <see cref="Root"/> tree.</param>
    /// <param name="index">The child index the new element gets: 0 = first, <c>parent.Children.Count</c> = last.</param>
    /// <param name="childMarkup">The markup of one element, e.g. <c>&lt;DvLabel text="New"/&gt;</c>.</param>
    /// <returns>A new document.</returns>
    /// <exception cref="ArgumentException"><paramref name="parent"/> is not in this document.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is below 0 or above the child count.</exception>
    /// <exception cref="XmlException"><paramref name="childMarkup"/> is not one well-formed element.</exception>
    public MarkupDocument InsertChild(ElementNode parent, int index, string childMarkup)
    {
        var p = InfoOf(parent);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, parent.Children.Count);
        ArgumentNullException.ThrowIfNull(childMarkup);
        var child = childMarkup.Trim();
        XElement.Parse(child); // 요소 하나인지 — 아니면 XmlException
        var nl = NewLine();

        if (parent.Children.Count > 0)
        {
            if (index < parent.Children.Count)
            {
                var next = _info[parent.Children[index]];
                var indent = IndentBefore(next.Start);
                return indent is null
                    ? Replace(next.Start, next.Start, child) // 같은 줄에 붙은 형제 — 그대로 앞에
                    : Replace(next.Start, next.Start, Reindent(child, indent, nl) + nl + indent);
            }

            var prev = _info[parent.Children[^1]];
            var prevIndent = IndentBefore(prev.Start);
            return prevIndent is null
                ? Replace(prev.End, prev.End, child)
                : Replace(prev.End, prev.End, nl + prevIndent + Reindent(child, prevIndent, nl));
        }

        var outer = IndentBefore(p.Start) ?? "";
        var inner = outer + IndentUnit();
        var body = nl + inner + Reindent(child, inner, nl) + nl + outer;
        if (p.SelfClosing)
        {
            // <X a="1" /> → <X a="1"> … </X>: '/>' 앞 공백까지 바꾼다
            var close = p.StartTagEnd - 2;
            while (close > p.NameEnd && char.IsWhiteSpace(Text[close - 1]))
                close--;
            return Replace(close, p.End, ">" + body + "</" + Text[(p.Start + 1)..p.NameEnd] + ">");
        }

        // 자식 요소가 없는 열린 요소 — 안쪽 공백만 있으면 바꾸고, 글자가 있으면 끝 태그 앞에 붙인다
        var content = Text[p.StartTagEnd..p.EndTagStart];
        return string.IsNullOrWhiteSpace(content)
            ? Replace(p.StartTagEnd, p.EndTagStart, body)
            : Replace(p.EndTagStart, p.EndTagStart, body);
    }

    /// <summary>Removes an element. When it is alone on its line, the whole line goes with it.</summary>
    /// <param name="node">An element of this document's <see cref="Root"/> tree, other than the root.</param>
    /// <returns>A new document.</returns>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not in this document, or is the root.</exception>
    public MarkupDocument RemoveElement(ElementNode node)
    {
        var info = InfoOf(node);
        if (ReferenceEquals(node, Root))
            throw new ArgumentException("the root element cannot be removed", nameof(node));

        var from = info.Start;
        var to = info.End;
        var lineStart = LineStart(from);
        var after = to;
        while (after < Text.Length && Text[after] is ' ' or '\t')
            after++;
        var lineEnd = after < Text.Length && Text[after] == '\r' && after + 1 < Text.Length && Text[after + 1] == '\n' ? after + 2
            : after < Text.Length && Text[after] is '\n' or '\r' ? after + 1
            : -1;
        if (lineEnd >= 0 && Text[lineStart..from].All(c => c is ' ' or '\t'))
        {
            from = lineStart;
            to = lineEnd;
        }

        return Replace(from, to, "");
    }

    // ─── 내부 ───

    private Info InfoOf(ElementNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _info.TryGetValue(node, out var i) ? i : throw new ArgumentException("the node is not in this document", nameof(node));
    }

    private static Attr? FindAttr(Info info, string name)
    {
        Attr? found = null;
        foreach (var a in info.Attrs)
            if (a.LocalName == name)
                found = a; // 같은 이름이 둘이면(접두사만 다름) 트리처럼 뒤가 이긴다
        return found;
    }

    private static void VerifyName(string name)
    {
        try { XmlConvert.VerifyName(name); }
        catch (Exception ex) when (ex is XmlException or ArgumentNullException) { throw new ArgumentException($"'{name}' is not a valid attribute name", nameof(name), ex); }
    }

    private MarkupDocument Replace(int from, int to, string with) => Parse(string.Concat(Text.AsSpan(0, from), with, Text.AsSpan(to)));

    private int LineStart(int at)
    {
        var i = at;
        while (i > 0 && Text[i - 1] is not '\n' and not '\r')
            i--;
        return i;
    }

    // 요소 앞이 줄 시작부터 공백뿐이면 그 공백(들여쓰기), 아니면 null(같은 줄에 다른 것이 있음)
    private string? IndentBefore(int at)
    {
        var ls = LineStart(at);
        var lead = Text[ls..at];
        return lead.All(c => c is ' ' or '\t') ? lead : null;
    }

    private string NewLine() => Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    // 들여쓰기 한 단 — 자기 줄에 있는 부모·자식 들여쓰기 차이에서 처음 찾은 것, 없으면 공백 4칸
    private string IndentUnit()
    {
        foreach (var parent in _order)
        {
            var pi = IndentBefore(_info[parent].Start);
            if (pi is null)
                continue;
            foreach (var c in parent.Children)
            {
                var ci = IndentBefore(_info[c].Start);
                if (ci is not null && ci.Length > pi.Length && ci.StartsWith(pi, StringComparison.Ordinal))
                    return ci[pi.Length..];
            }
        }

        return "    ";
    }

    private static string Reindent(string child, string indent, string nl)
    {
        var lines = child.Replace("\r\n", "\n").Split('\n');
        return string.Join(nl, lines.Select((l, i) => i == 0 || l.Length == 0 ? l : indent + l));
    }

    private static string Escape(string v, char quote)
    {
        var sb = new StringBuilder(v.Length);
        foreach (var c in v)
        {
            sb.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '"' when quote == '"' => "&quot;",
                '\'' when quote == '\'' => "&apos;",
                '\n' => "&#10;",
                '\r' => "&#13;",
                '\t' => "&#9;",
                _ => c.ToString(),
            });
        }

        return sb.ToString();
    }

    private sealed record Attr(string LocalName, int NameStart, int ValueStart, int ValueEnd, char Quote)
    {
        public int End => ValueEnd + 1; // 닫는 따옴표 뒤
    }

    private sealed class Info
    {
        public int Start;        // '<'
        public int NameEnd;      // 태그 이름 끝
        public int StartTagEnd;  // 시작 태그의 '>' 다음
        public bool SelfClosing;
        public int EndTagStart;  // 끝 태그 '</' (SelfClosing 이면 End)
        public int End;          // 요소 끝(끝 태그 '>' 다음)
        public readonly List<Attr> Attrs = new();
    }

    // 원문을 처음부터 훑어 요소를 문서 순서로 모은다. 문법은 XDocument 가 이미 확인했으므로 잘 짜인 XML 만 온다.
    private static List<Info> Scan(string t)
    {
        var list = new List<Info>();
        var open = new Stack<Info>();
        var i = 0;
        while ((i = t.IndexOf('<', i)) >= 0)
        {
            if (string.CompareOrdinal(t, i, "<!--", 0, 4) == 0)
                i = t.IndexOf("-->", i + 4, StringComparison.Ordinal) + 3;
            else if (string.CompareOrdinal(t, i, "<![CDATA[", 0, 9) == 0)
                i = t.IndexOf("]]>", i + 9, StringComparison.Ordinal) + 3;
            else if (string.CompareOrdinal(t, i, "<?", 0, 2) == 0)
                i = t.IndexOf("?>", i + 2, StringComparison.Ordinal) + 2;
            else if (string.CompareOrdinal(t, i, "<!", 0, 2) == 0)
                i = SkipDeclaration(t, i);
            else if (string.CompareOrdinal(t, i, "</", 0, 2) == 0)
            {
                var close = t.IndexOf('>', i);
                var e = open.Pop();
                e.EndTagStart = i;
                e.End = close + 1;
                i = close + 1;
            }
            else
            {
                var e = new Info { Start = i };
                var j = i + 1;
                while (!IsNameEnd(t[j]))
                    j++;
                e.NameEnd = j;
                while (true)
                {
                    while (char.IsWhiteSpace(t[j]))
                        j++;
                    if (t[j] == '/')
                    {
                        e.SelfClosing = true;
                        e.StartTagEnd = j + 2;
                        e.EndTagStart = e.End = j + 2;
                        break;
                    }

                    if (t[j] == '>')
                    {
                        e.StartTagEnd = j + 1;
                        break;
                    }

                    var nameStart = j;
                    while (t[j] != '=' && !char.IsWhiteSpace(t[j]))
                        j++;
                    var raw = t[nameStart..j];
                    while (t[j] != '=')
                        j++;
                    j++;
                    while (t[j] != '"' && t[j] != '\'')
                        j++;
                    var quote = t[j];
                    var valueStart = j + 1;
                    var valueEnd = t.IndexOf(quote, valueStart);
                    var colon = raw.IndexOf(':');
                    e.Attrs.Add(new Attr(colon >= 0 ? raw[(colon + 1)..] : raw, nameStart, valueStart, valueEnd, quote));
                    j = valueEnd + 1;
                }

                list.Add(e);
                if (!e.SelfClosing)
                    open.Push(e);
                i = e.StartTagEnd;
            }
        }

        return list;
    }

    private static bool IsNameEnd(char c) => char.IsWhiteSpace(c) || c is '/' or '>';

    // <!DOCTYPE …> — 따옴표와 [내부 부분집합] 안의 '>' 는 건너뛴다
    private static int SkipDeclaration(string t, int i)
    {
        var depth = 0;
        for (var j = i + 2; j < t.Length; j++)
        {
            var c = t[j];
            if (c is '"' or '\'')
                j = t.IndexOf(c, j + 1);
            else if (c == '[')
                depth++;
            else if (c == ']')
                depth--;
            else if (c == '>' && depth == 0)
                return j + 1;
        }

        return t.Length;
    }
}
