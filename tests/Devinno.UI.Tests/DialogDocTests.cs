using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Devinno.UI.Controls;
using Devinno.UI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

// dvml-ui-markup.md 21장(다이얼로그)의 C#·dvml 예제가 생성기와 함께 실제로 컴파일되는지 — 문서가 API 와 어긋나면 실패.
// (xml 블록의 파싱·렌더는 MarkupDocTests 가, 16.1 샘플의 동작은 AppSampleDocTests 가 본다.)
public class DialogDocTests
{
    private sealed class DvmlText(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, Encoding.UTF8);
    }

    // SDK 의 ImplicitUsings(enable) 와 같은 전역 using — 문서 예제는 보통 프로젝트에 그대로 붙여 쓰므로
    private const string ImplicitUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    [Fact]
    public void Dialog_chapter_examples_compile_with_generator()
    {
        var chapter = Chapter21();
        var cs = Blocks(chapter, "csharp");
        var xml = Blocks(chapter, "xml");
        Assert.True(cs.Length >= 6, $"C# 예제 {cs.Length}개");
        Assert.True(xml.Length >= 4, $"dvml 예제 {xml.Length}개");

        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = cs.Select((c, i) => CSharpSyntaxTree.ParseText(c, parse, path: $"doc{i}.cs"))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, parse, path: "usings.cs"));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is var n && (n.StartsWith("System.") || n is "mscorlib.dll" or "netstandard.dll"))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(DvControl).Assembly.Location));
        var compilation = CSharpCompilation.Create("DialogDoc", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DvmlGenerator().AsSourceGenerator()],
            xml.Select((x, i) => (AdditionalText)new DvmlText($"doc{i}.dvml", x)),
            parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var genDiags);

        Assert.Empty(genDiags.Select(d => d.ToString()));
        var problems = output.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => d.ToString())
            .ToArray();
        Assert.Empty(problems);

        // 다이얼로그·페이지마다 생성기가 마크업을 붙였는지(vm 이 네임스페이스까지 맞는지)
        var generated = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName).ToArray();
        foreach (var vm in new[] { "RecipeEditDialog", "RecipePage", "AlarmDialog", "ValveDialog" })
            Assert.Contains(generated, h => h.Contains(vm, StringComparison.Ordinal));
    }

    private static string Chapter21()
    {
        var text = File.ReadAllText(DocPath("dvml-ui-markup.md")).Replace("\r\n", "\n");
        var start = text.IndexOf("\n## 21.", StringComparison.Ordinal);
        Assert.True(start >= 0, "21장이 없습니다");
        var end = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string[] Blocks(string text, string lang)
        => Regex.Matches(text, $"```{lang}\\n(.*?)```", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToArray();

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
