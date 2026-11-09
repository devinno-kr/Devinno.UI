using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Generators;
using Devinno.UI.Markup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

// AI 문서 묶음(llms.txt·어댑터 llms.txt·llm-rules.md·루트 dvml-*.md)을 지키는 테스트 —
// 링크·앵커, 번호 참조·이력 낱말·한글 금지(본문만, 코드 블록·백틱 밖), TagSchema 태그·속성이 그 태그 절에 있는지, 진단 ID, 최소 예제.
public class AiDocsTests
{
    private static string Root([CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "llm-rules.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }

    // 문서 묶음: 저장소 루트 기준 상대 경로('/' 구분)
    private static IReadOnlyList<string> DocSet()
    {
        var root = Root();
        var list = new List<string> { "llms.txt", "llm-rules.md" };
        list.AddRange(Directory.EnumerateFiles(root, "dvml-*.md").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal));
        list.AddRange(Directory.EnumerateFiles(Path.Combine(root, "src"), "llms.txt", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).Order(StringComparer.Ordinal));
        return list;
    }

    private static string[] Lines(string doc) => File.ReadAllText(Path.Combine(Root(), doc)).Replace("\r\n", "\n").Split('\n');

    private static readonly Regex AnchorRx = new("<a id=\"([^\"]+)\"></a>");
    private static readonly Regex HeadingRx = new(@"^(#{1,6})\s");

    private static List<string> Anchors(string doc) =>
        Lines(doc).SelectMany(l => AnchorRx.Matches(l).Select(m => m.Groups[1].Value)).ToList();

    // 본문만: 코드 블록 줄은 null, 백틱 구간·HTML 주석·링크 대상은 지운 글
    private static IEnumerable<(int Line, string Text)> Prose(string doc)
    {
        var fence = false;
        var n = 0;
        foreach (var raw in Lines(doc))
        {
            n++;
            if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fence = !fence;
                continue;
            }

            if (fence)
                continue;
            var t = Regex.Replace(raw, "``[^`]*``", " ");
            t = Regex.Replace(t, "`[^`]*`", " ");
            t = Regex.Replace(t, "<!--.*?-->", " ");
            t = Regex.Replace(t, @"\]\([^)]*\)", "]");
            yield return (n, t);
        }
    }

    [Fact]
    public void Links_resolve_to_existing_pages_and_anchors()
    {
        var root = Root();
        var docs = DocSet();
        var problems = new List<string>();
        var anchors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        HashSet<string> AnchorsOf(string rel)
        {
            if (!anchors.TryGetValue(rel, out var set))
                anchors[rel] = set = File.Exists(Path.Combine(root, rel)) ? [.. Anchors(rel)] : [];
            return set;
        }

        foreach (var doc in docs)
        {
            var dir = Path.GetDirectoryName(doc)!.Replace('\\', '/');
            var fence = false;
            var n = 0;
            foreach (var line in Lines(doc))
            {
                n++;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    fence = !fence;
                    continue;
                }

                if (fence)
                    continue;
                foreach (Match m in Regex.Matches(Regex.Replace(line, "`[^`]*`", " "), @"\]\(([^)\s]*)\)"))
                {
                    var target = m.Groups[1].Value;
                    if (target.Contains("://", StringComparison.Ordinal) || target.StartsWith("mailto:", StringComparison.Ordinal))
                        continue;
                    var hash = target.IndexOf('#');
                    var file = hash < 0 ? target : target[..hash];
                    var anchor = hash < 0 ? null : target[(hash + 1)..];
                    var rel = file.Length == 0 ? doc : Path.GetFullPath(Path.Combine(root, dir, file)).Substring(Path.GetFullPath(root).Length).TrimStart('\\', '/').Replace('\\', '/');
                    if (!File.Exists(Path.Combine(root, rel)))
                    {
                        problems.Add($"{doc}:{n}: missing page {target}");
                        continue;
                    }

                    if (anchor is not null && !AnchorsOf(rel).Contains(anchor))
                        problems.Add($"{doc}:{n}: no <a id=\"{anchor}\"> on {rel} ({target})");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Anchor_ids_are_unique_within_a_page()
    {
        var problems = DocSet().SelectMany(doc => Anchors(doc).GroupBy(a => a).Where(g => g.Count() > 1).Select(g => $"{doc}: {g.Key} ×{g.Count()}")).ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static readonly Regex NumberedRef = new(@"chapter \d|§ ?\d|section \d|\b\d+\.\d+ (?:chapter|section)", RegexOptions.IgnoreCase);

    [Fact]
    public void Prose_has_no_numbered_references()
    {
        var problems = DocSet().SelectMany(doc => Prose(doc).Where(p => NumberedRef.IsMatch(p.Text))
            .Select(p => $"{doc}:{p.Line}: {NumberedRef.Match(p.Text).Value}")).ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static readonly Regex HistoryWord = new(
        @"\b(previously|no longer|renamed|was renamed|formerly|deprecated|not yet|later stage|stage \d|planned)\b|\bbefore 0\.", RegexOptions.IgnoreCase);

    [Fact]
    public void Prose_has_no_history_words()
    {
        var problems = DocSet().SelectMany(doc => Prose(doc).Where(p => HistoryWord.IsMatch(p.Text))
            .Select(p => $"{doc}:{p.Line}: {HistoryWord.Match(p.Text).Value}")).ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static readonly Regex Hangul = new(@"[ᄀ-ᇿ㄰-㆏가-힣]");

    // 한글 금지 — 예외는 dvml-app.md 의 <!-- data:ko --> … <!-- /data:ko --> 안(내장 문자열 한국어 열)뿐
    [Fact]
    public void Prose_has_no_hangul()
    {
        var problems = new List<string>();
        foreach (var doc in DocSet())
        {
            var data = false;
            var lines = Lines(doc);
            foreach (var (line, text) in Prose(doc))
            {
                var raw = lines[line - 1];
                if (doc == "dvml-app.md" && raw.Contains("<!-- data:ko -->", StringComparison.Ordinal)) data = true;
                if (doc == "dvml-app.md" && raw.Contains("<!-- /data:ko -->", StringComparison.Ordinal)) data = false;
                if (!data && Hangul.IsMatch(text))
                    problems.Add($"{doc}:{line}: {text.Trim()}");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // 태그 절: 그 태그의 <a id> 줄부터.
    // 앵커 바로 뒤(앵커·빈 줄만 사이에 둠)에 제목이 오면 → 같은 수준 이하(#가 같거나 적은) 다음 제목 앞까지(하위 앵커 dvtab·dvoption 은 부모 절 안에 포함).
    // 바로 뒤에 제목이 없는 하위 앵커 → 다음 제목 앞까지.
    internal static string? TagSection(string tag, out string? page)
    {
        var id = tag.ToLowerInvariant();
        page = null;
        foreach (var doc in DocSet())
        {
            var lines = Lines(doc);
            var start = Array.FindIndex(lines, l => l.Contains($"<a id=\"{id}\"></a>", StringComparison.Ordinal));
            if (start < 0)
                continue;
            page = doc;
            var i = start + 1;
            while (i < lines.Length && (lines[i].Trim().Length == 0 || AnchorRx.IsMatch(lines[i]) && lines[i].Trim().StartsWith("<a id=", StringComparison.Ordinal)))
                i++;
            var level = i < lines.Length && HeadingRx.Match(lines[i]) is { Success: true } h ? h.Groups[1].Value.Length : 7;
            var end = level < 7 ? i + 1 : i;
            var fence = false;
            for (; end < lines.Length; end++)
            {
                if (lines[end].TrimStart().StartsWith("```", StringComparison.Ordinal)) fence = !fence;
                if (!fence && HeadingRx.Match(lines[end]) is { Success: true } m && m.Groups[1].Value.Length <= level)
                    break;
            }

            // 다음 절의 앵커 줄(제목 바로 위)은 빼지 않아도 무해 — 속성 이름 검사만 한다
            return string.Join("\n", lines[start..end]);
        }

        return null;
    }

    [Fact]
    public void Every_builtin_tag_has_a_section_with_all_its_attributes()
    {
        var problems = new List<string>();
        foreach (var (tag, attrs) in TagSchema.Tags)
        {
            var section = TagSection(tag, out var page);
            if (section is null)
            {
                problems.Add($"{tag}: no <a id=\"{tag.ToLowerInvariant()}\"> in the doc set");
                continue;
            }

            // 코드 블록(```)을 먼저 지운다 — 남겨 두면 백틱 짝이 어긋난다
            var prose = Regex.Replace(section, "```.*?```", " ", RegexOptions.Singleline);
            var spans = Regex.Matches(prose, "`([^`]+)`").Select(m => m.Groups[1].Value.Trim()).ToList();
            foreach (var a in attrs)
                if (!spans.Any(s => s == a.Name || s.StartsWith(a.Name + "=", StringComparison.Ordinal)))
                    problems.Add($"{tag} ({page}): attribute `{a.Name}` not in backticks in its section");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Diagnostics_page_has_every_build_diagnostic()
    {
        var anchors = Anchors("dvml-diagnostics.md").ToHashSet();
        var expected = Enumerable.Range(1, 21).Select(i => $"DVML{i:000}").ToHashSet();
        var resx = File.ReadAllText(Path.Combine(Root(), "src", "Devinno.UI.Generators", "Resources.resx"));
        expected.UnionWith(Regex.Matches(resx, @"DVML\d{3}").Select(m => m.Value));
        var missing = expected.Where(id => !anchors.Contains(id.ToLowerInvariant())).Order().ToList();
        Assert.True(missing.Count == 0, "dvml-diagnostics.md has no anchor for: " + string.Join(", ", missing));
    }

    // llm-rules.md#screen-shape 의 최소 예제 — xml 은 파싱·렌더, C# 은 생성기와 함께 컴파일(ViewWindow 는 같은 생성자 모양의 대역)
    private static (string Cs, string Xml) ScreenShape()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "llm-rules.md")).Replace("\r\n", "\n");
        var start = text.IndexOf("<a id=\"screen-shape\"></a>", StringComparison.Ordinal);
        Assert.True(start >= 0, "llm-rules.md has no #screen-shape");
        var end = text.IndexOf("\n<a id=", start + 1, StringComparison.Ordinal);
        var section = end < 0 ? text[start..] : text[start..end];
        string Block(string lang) => Regex.Match(section, $"```{lang}\\n(.*?)```", RegexOptions.Singleline).Groups[1].Value;
        return (Block("csharp"), Block("xml"));
    }

    public sealed class ScreenVm
    {
        public double Temperature { get; set; } = 21.5;
        public bool AutoMode { get; set; }
        public string ModeText => AutoMode ? "Auto" : "Manual";
        public void Reset() => Temperature = 0;
    }

    [Fact]
    public void Screen_shape_markup_parses_and_renders()
    {
        var (_, xml) = ScreenShape();
        Assert.False(string.IsNullOrWhiteSpace(xml));
        MarkupParser.Parse(xml);
        var png = DvRenderer.RenderToPng(xml, new ScreenVm(), 1280, 800);
        Assert.True(png.Length > 0);
    }

    private sealed class DvmlText(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, Encoding.UTF8);
    }

    private const string ImplicitUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    // OpenTK 호스트는 이 테스트 프로젝트가 참조하지 않는다 — 실제 ViewWindow 와 같은 생성자 모양의 대역
    private const string ViewWindowStub = """
        namespace Devinno.UI.OpenTK
        {
            public sealed class ViewWindow : System.IDisposable
            {
                public ViewWindow(Devinno.UI.App.DvApp app, int width, int height, string title, bool gles = false, bool fullscreen = false, bool fit = false) { }
                public void Run() { }
                public void Dispose() { }
            }
        }
        """;

    [Fact]
    public void Screen_shape_code_compiles_with_generator()
    {
        var (cs, xml) = ScreenShape();
        Assert.False(string.IsNullOrWhiteSpace(cs));
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(cs, parse, path: "Program.cs"),
            CSharpSyntaxTree.ParseText(ImplicitUsings, parse, path: "usings.cs"),
            CSharpSyntaxTree.ParseText(ViewWindowStub, parse, path: "stub.cs"),
        };
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is var n && (n.StartsWith("System.") || n is "mscorlib.dll" or "netstandard.dll"))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(DvControl).Assembly.Location));
        var compilation = CSharpCompilation.Create("ScreenShape", trees, refs,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DvmlGenerator().AsSourceGenerator()],
            [new DvmlText("MainPage.dvml", xml)],
            parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var genDiags);

        Assert.Empty(genDiags.Select(d => d.ToString()));
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()));
        var generated = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName).ToArray();
        Assert.Contains(generated, h => h.Contains("MainPage", StringComparison.Ordinal));
    }
}
