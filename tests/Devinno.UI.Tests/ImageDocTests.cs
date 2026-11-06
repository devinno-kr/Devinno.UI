using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Generators;
using Devinno.UI.Markup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using SkiaSharp;
using Xunit;

// dvml-image.md 의 예제가 코드와 맞는지 — xml 은 파싱·렌더·빌드 진단 없음, C# 은 라이브러리와 함께 컴파일.
public class ImageDocTests
{
    private static string Doc() => File.ReadAllText(DocPath("dvml-image.md")).Replace("\r\n", "\n");

    private static string[] Blocks(string lang) =>
        Regex.Matches(Doc(), $"```{lang}\\n(.*?)```", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToArray();

    public static TheoryData<int, string> XmlBlocks()
    {
        var data = new TheoryData<int, string>();
        var i = 0;
        foreach (var b in Blocks("xml"))
            data.Add(++i, b);
        return data;
    }

    [Theory]
    [MemberData(nameof(XmlBlocks))]
    public void Xml_example_parses_renders_and_has_no_diagnostics(int index, string block)
    {
        MarkupParser.Parse(block);
        var png = DvRenderer.RenderToPng(block, null, 800, 600);
        Assert.True(png.Length > 0, $"block #{index}");

        // 없는 스타일 키·토큰·클래스(DVML012~014)가 없는지 — 생성기가 vm 없는 dvml 도 검사한다
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DvmlGenerator().AsSourceGenerator()], [new DvmlText($"doc{index}.dvml", block)], parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(CSharpCompilation.Create("ImageDocXml", [], [], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            out _, out var diags);
        Assert.Empty(diags.Select(d => d.ToString()));
    }

    [Fact]
    public void CSharp_examples_compile()
    {
        var cs = Blocks("csharp");
        Assert.NotEmpty(cs);
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = cs.Select((c, i) => CSharpSyntaxTree.ParseText(c, parse, path: $"doc{i}.cs"))
            .Append(CSharpSyntaxTree.ParseText("global using System;", parse, path: "usings.cs"));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is var n && (n.StartsWith("System.") || n is "mscorlib.dll" or "netstandard.dll"))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(DvControl).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(SKCanvas).Assembly.Location));
        var compilation = CSharpCompilation.Create("ImageDoc", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var problems = compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private sealed class DvmlText(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, System.Text.Encoding.UTF8);
    }

    private static string DocPath(string name, [CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(System.IO.Path.GetDirectoryName(source)!);
        while (dir is not null)
        {
            var p = System.IO.Path.Combine(dir.FullName, name);
            if (File.Exists(p))
                return p;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(name);
    }
}
