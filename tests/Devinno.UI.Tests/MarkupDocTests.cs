using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Devinno.UI;
using Devinno.UI.Markup;
using Xunit;

/// <summary>dvml-ui-markup.md 의 xml 예시가 실제로 파싱·렌더되는지 — 문서가 코드와 어긋나면 실패.</summary>
public class MarkupDocTests
{
    public static TheoryData<int, string> XmlBlocks()
    {
        var data = new TheoryData<int, string>();
        var text = File.ReadAllText(DocPath("dvml-ui-markup.md"));
        var i = 0;
        foreach (Match m in Regex.Matches(text, "```xml\\r?\\n(.*?)```", RegexOptions.Singleline))
            data.Add(++i, m.Groups[1].Value);
        return data;
    }

    [Theory]
    [MemberData(nameof(XmlBlocks))]
    public void Markup_example_parses_and_renders(int index, string block)
    {
        // 조각 예시(형제 여러 개)도 받도록 감싼다
        var markup = $"<DvBox>{block}</DvBox>";
        MarkupParser.Parse(markup);
        var png = DvRenderer.RenderToPng(markup, null, 800, 600);
        Assert.True(png.Length > 0, $"block #{index}");
    }

    // 리뷰 I1 — visible·disabled 는 참/거짓 규칙(R1): 글자 속성에 묶으면 "0"·"false" 글자가 숨기고 그 밖의 글자는 경고.
    // 문서(markup·llm-rules)와 데모 dvml 의 visible="{X}"·disabled="{X}" 가 같은 곳(문서 C# 블록·데모 .cs)에서 string 으로 선언된 X 에 묶이지 않는지
    [Fact]
    public void Visible_and_disabled_are_not_bound_to_string_properties()
    {
        var bound = new Regex(@"\b(visible|disabled)=""\{(\w+)\}""");
        var problems = new List<string>();

        void Check(string where, string markup, string code)
        {
            foreach (Match m in bound.Matches(markup))
                if (Regex.IsMatch(code, $@"\bstring\??\s+{m.Groups[2].Value}\b"))
                    problems.Add($"{where}: {m.Value}");
        }

        foreach (var doc in new[] { "dvml-ui-markup.md", "llm-rules.md" })
        {
            var text = File.ReadAllText(DocPath(doc));
            var code = string.Join("\n", Regex.Matches(text, "```csharp\\r?\\n(.*?)```", RegexOptions.Singleline).Select(m => m.Groups[1].Value));
            Check(doc, text, code);
        }

        var samples = Path.Combine(Path.GetDirectoryName(DocPath("dvml-ui-markup.md"))!, "samples");
        var sampleCode = string.Join("\n", Directory.EnumerateFiles(samples, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).Select(File.ReadAllText));
        foreach (var f in Directory.EnumerateFiles(samples, "*.dvml", SearchOption.AllDirectories))
            Check(Path.GetFileName(f), File.ReadAllText(f), sampleCode);

        Assert.Empty(problems);
    }

    // 빌드 출력 위치와 무관하게(--artifacts-path 등) 이 소스 파일 위치에서 위로 찾는다
    private static string DocPath(string name, [CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null)
        {
            var p = Path.Combine(dir.FullName, name);
            if (File.Exists(p)) return p;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(name);
    }
}
