using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Devinno.UI.Generators;

/// <summary>dvml 파일 하나를 읽은 결과: 원문, 루트의 vm, 필드로 만들 id 목록, 파일 안 진단.</summary>
internal sealed class DvmlModel
{
    public string Path { get; }
    public string Text { get; }
    /// <summary>루트 <c>vm</c> 값. 없거나 XML 오류면 <see langword="null"/>.</summary>
    public string? Vm { get; private set; }
    public Location VmLocation { get; private set; } = Location.None;
    public List<(string Name, string Type)> Fields { get; } = new();
    public List<Diagnostic> Reports { get; } = new();

    /// <summary>이 파일에서 쓴 클래스(<c>class</c>)와 이 파일의 <c>DvStyle</c> 이 정의한 클래스 — 클래스 진단은 프로젝트 전체를 모은 뒤.</summary>
    public List<(string Name, Location At)> ClassUses { get; } = new();
    public HashSet<string> ClassDefs { get; } = new(System.StringComparer.Ordinal);
    public bool HasErrors { get; private set; }

    /// <summary>핸들러 속성(onclick·onbeforecommit·onack·onrowclick) — VM 메서드 검사는 형식을 안 뒤(DvmlGenerator). DvComponent 정의 안은 빼고(어느 화면 VM 인지 모른다).</summary>
    public List<(string Attr, string Value, string Tag, Location At)> Handlers { get; } = new();

    /// <summary>이 파일의 DvComponent 중 onclick 을 매개변수로 받는 것 — 그 인스턴스의 onclick 은 핸들러로 전달되지 않는다.</summary>
    public HashSet<string> ClickParamComponents { get; } = new(System.StringComparer.Ordinal);

    private static readonly string[] HandlerAttrs = { "onclick", "onbeforecommit", "onack", "onrowclick" };

    // 핸들 형식 = 태그 이름(실행 시 DvControlLookup.Kinds 와 같은 목록). 그 밖의 태그는 DvControl.
    private static readonly HashSet<string> HandleTags = new(System.StringComparer.Ordinal)
    {
        "DvLabel", "DvButton", "DvToggle", "DvKnob", "DvInput", "DvSelect", "DvSegment", "DvDateTimeInput", "DvColorInput",
        "DvCheckBox", "DvRadio", "DvSlider", "DvListBox", "DvExpander", "DvTreeView", "DvSwitch",
    };

    private readonly SourceText _source;

    private DvmlModel(string path, SourceText source)
    {
        Path = path;
        _source = source;
        Text = source.ToString();
    }

    public static DvmlModel Parse(string path, SourceText? source)
    {
        var m = new DvmlModel(path, source ?? SourceText.From(string.Empty));
        XDocument doc;
        try
        {
            doc = XDocument.Parse(m.Text, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            m.Report(Diagnostics.BadXml, m.At(ex.LineNumber, ex.LinePosition, 1), ex.Message);
            return m;
        }

        var root = doc.Root!;
        // 공정도 연결 검사 — vm 이 없는 dvml 도. 생성(필드·Markup)은 막지 않는다(막으면 엉뚱한 컴파일 오류가 겹친다)
        ProcessCheck.Check(root, (d, at, args) => m.Reports.Add(Diagnostic.Create(d, m.At(at), args)));
        WidgetCheck.Check(root, (d, at, args) => m.Reports.Add(Diagnostic.Create(d, m.At(at), args)));
        var uses = new List<(string Name, XAttribute At)>();
        StyleCheck.Check(root, (d, at, off, len, args) => m.Reports.Add(Diagnostic.Create(d, m.At(at, off, len), args)), uses, m.ClassDefs);
        foreach (var (name, at) in uses)
            m.ClassUses.Add((name, m.At(at)));
        foreach (var c in root.DescendantsAndSelf("DvComponent"))
            if (c.Attribute("name")?.Value is { Length: > 0 } cname &&
                c.Elements("DvParam").Any(p => string.Equals(p.Attribute("name")?.Value, "onclick", System.StringComparison.OrdinalIgnoreCase)))
                m.ClickParamComponents.Add(cname);

        var vmAttr = root.Attribute("vm");
        var vm = vmAttr?.Value.Trim();
        if (string.IsNullOrEmpty(vm))
            return m;
        m.Vm = vm;
        m.VmLocation = m.At(vmAttr!);

        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        m.Walk(root, null, false, seen);
        return m;
    }

    // 실행 시 조회(DvControlLookup)와 같게: DvRepeat·DvComponent 는 그 요소부터 하위 전체를 건너뜀.
    private void Walk(XElement e, string? templateTag, bool inComponent, HashSet<string> seen)
    {
        var tag = e.Name.LocalName;
        if (templateTag is null && tag is "DvRepeat" or "DvComponent")
            templateTag = tag;
        inComponent |= tag == "DvComponent";

        if (!inComponent)
            foreach (var name in HandlerAttrs)
                if (e.Attribute(name) is { } h && !string.IsNullOrWhiteSpace(h.Value))
                    Handlers.Add((name, h.Value, tag, At(h, 0, h.Value.Length)));

        var idAttr = e.Attribute("id");
        if (idAttr is not null)
        {
            var id = idAttr.Value;
            if (templateTag is not null)
                Report(Diagnostics.IdInTemplate, At(idAttr), id, templateTag);
            else if (!SyntaxFacts.IsValidIdentifier(id) || SyntaxFacts.GetKeywordKind(id) != SyntaxKind.None)
                Report(Diagnostics.InvalidId, At(idAttr), id);
            else if (!seen.Add(id))
                Report(Diagnostics.DuplicateId, At(idAttr), id);
            else
                Fields.Add((id, HandleTags.Contains(tag) ? tag : "DvControl"));
        }

        foreach (var c in e.Elements())
            Walk(c, templateTag, inComponent, seen);
    }

    private void Report(DiagnosticDescriptor d, Location loc, params object[] args)
    {
        if (d.DefaultSeverity == DiagnosticSeverity.Error)
            HasErrors = true;
        Reports.Add(Diagnostic.Create(d, loc, args));
    }

    private Location At(XObject o)
    {
        var li = (IXmlLineInfo)o;
        var length = o switch
        {
            XAttribute a => a.Name.LocalName.Length,
            XElement e => e.Name.LocalName.Length + 1, // '<' 포함
            _ => 1,
        };
        return li.HasLineInfo() ? At(li.LineNumber, li.LinePosition, length) : At(0, 0, 0);
    }

    // 대상 안 위치 — 속성이면 값 안(속성 이름 + =" 뒤부터), 글자 노드면 그 글자 안. 줄이 바뀌는 글자(DvStyle 본문)는 줄·칸을 따라간다
    private Location At(XObject o, int offset, int length)
    {
        var li = (IXmlLineInfo)o;
        if (!li.HasLineInfo())
            return At(o);
        string text;
        int line = li.LineNumber, col = li.LinePosition;
        if (o is XAttribute a)
        {
            text = a.Value;
            col += a.Name.LocalName.Length + 2; // name="
        }
        else if (o is XText t)
        {
            text = t.Value;
        }
        else
        {
            return At(o);
        }

        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }

        return At(line, col, length);
    }

    // line·col 은 XML 쪽 1 기준. 정보가 없거나 범위 밖이면 파일 처음.
    private Location At(int line, int col, int length)
    {
        var lines = _source.Lines;
        if (line < 1 || line > lines.Count)
            return Location.Create(Path, new TextSpan(0, 0), new LinePositionSpan(default, default));
        var l = lines[line - 1];
        var start = System.Math.Min(l.Start + System.Math.Max(col - 1, 0), l.End);
        var end = System.Math.Min(start + length, l.End);
        var span = TextSpan.FromBounds(start, end);
        return Location.Create(Path, span, lines.GetLinePositionSpan(span));
    }
}
