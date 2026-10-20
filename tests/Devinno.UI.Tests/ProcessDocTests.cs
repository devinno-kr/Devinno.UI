using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Devinno.UI;
using Devinno.UI.Components;
using Devinno.UI.Controls;
using Devinno.UI.Generators;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

// dvml-process.md 의 예제가 코드와 맞는지 — xml 은 파싱·렌더, 공정도 예제는 겹침·오류·교차 없이 배치, 페이지 예제(C#+dvml)는 생성기와 함께 컴파일.
public class ProcessDocTests
{
    private static string Doc() => File.ReadAllText(DocPath("dvml-process.md")).Replace("\r\n", "\n");

    private static string[] Blocks(string lang) =>
        Regex.Matches(Doc(), $"```{lang}\\n(.*?)```", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToArray();

    private static bool IsDefinition(string block) =>
        block.TrimStart().StartsWith("<DvSymbol", StringComparison.Ordinal) || block.TrimStart().StartsWith("<DvComponent", StringComparison.Ordinal);

    // 문서에 정의된 사용자 심볼·유닛 컴포넌트(DvSymbol·DvComponent 블록)를 등록한 레지스트리
    private static ComponentRegistry Registry()
    {
        var r = new ComponentRegistry();
        foreach (var b in Blocks("xml").Where(IsDefinition))
            r.Register(b);
        return r;
    }

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
    public void Xml_example_parses_and_renders(int index, string block)
    {
        var markup = IsDefinition(block) ? "<DvBox/>" : $"<DvBox>{block}</DvBox>";
        MarkupParser.Parse(markup);
        var png = DvRenderer.RenderToPng(markup, null, 1000, 800, Registry());
        Assert.True(png.Length > 0, $"block #{index}");
    }

    public static TheoryData<int, string> ProcessBlocks()
    {
        var data = new TheoryData<int, string>();
        var i = 0;
        foreach (var b in Blocks("xml"))
        {
            i++;
            if (b.Contains("<DvProcess", StringComparison.Ordinal))
                data.Add(i, b);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ProcessBlocks))]
    public void Process_example_lays_out_clean(int index, string block)
    {
        // 페이지 예제의 vm 속성(생성기용)은 렌더에는 필요 없다
        var markup = Regex.Replace(block, "\\svm=\"[^\"]*\"", "");
        var s = new DvSurface(markup, new DocVm(), DvTheme.Light, Registry());
        s.Layout(1600, 1200);
        var outer = Flatten(s.Root!).Where(b => b.Process is not null).Select(b => b.Process!).ToList();
        Assert.NotEmpty(outer);
        var diagrams = outer.Concat(outer.SelectMany(o => o.Nodes).Where(n => n.Unit is not null).Select(n => n.Unit!)).ToList();
        foreach (var d in diagrams)
        {
            Assert.All(d.Lines, l => Assert.False(l.Error, $"block #{index}: {l.FromRef}->{l.ToRef} error"));
            Assert.All(d.Nodes, n => Assert.False(n.Error, $"block #{index}: {n.Name} error"));
            var bad = ProcessTests.Violations(d);
            Assert.True(bad.Count == 0, $"block #{index}: " + string.Join(" / ", bad) + " :: " + string.Join(" | ", d.Lines.Select(l => $"{l.FromRef}->{l.ToRef} " + string.Join(" ", l.G?.Points.Select(p => $"({p.X:0},{p.Y:0})") ?? []))) + " :: " + string.Join(" | ", d.Nodes.Select(n => $"{n.Name}@({n.G.X:0},{n.G.Y:0}) sym={n.Sym}")));
            Assert.All(d.Lines, l => Assert.Equal(0, l.G!.Crossings));
        }
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

    [Fact]
    public void Page_example_compiles_with_generator()
    {
        var cs = Blocks("csharp").Where(b => b.Contains("partial class", StringComparison.Ordinal)).ToArray();
        var xml = Blocks("xml").Where(b => b.Contains(" vm=\"", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(cs);
        Assert.NotEmpty(xml);

        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = cs.Select((c, i) => CSharpSyntaxTree.ParseText(c, parse, path: $"doc{i}.cs"))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, parse, path: "usings.cs"));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is var n && (n.StartsWith("System.") || n is "mscorlib.dll" or "netstandard.dll"))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(DvControl).Assembly.Location));
        var compilation = CSharpCompilation.Create("ProcessDoc", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DvmlGenerator().AsSourceGenerator()],
            xml.Select((x, i) => (AdditionalText)new DvmlText($"doc{i}.dvml", x)),
            parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var genDiags);

        Assert.Empty(genDiags.Select(d => d.ToString()));
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()));
        var generated = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(g => g.HintName).ToArray();
        Assert.Contains(generated, h => h.Contains("WaterPage", StringComparison.Ordinal));
        Assert.Contains(generated, h => h.Contains("BlendPage", StringComparison.Ordinal));
    }

    // 문서의 수치 표가 내장 심볼과 같은지(크기·포트)
    [Theory]
    [InlineData("DvVessel", "tank", 100, 150)]
    [InlineData("DvVessel", "reactor", 100, 160)]
    [InlineData("DvVessel", "silo", 100, 160)]
    [InlineData("DvVessel", "hopper", 100, 110)]
    [InlineData("DvPump", "centrifugal", 64, 64)]
    [InlineData("DvPump", "gear", 72, 52)]
    [InlineData("DvPump", "compressor", 72, 64)]
    [InlineData("DvPump", "blower", 72, 64)]
    [InlineData("DvValve", "gate", 60, 48)]
    [InlineData("DvValve", "check", 60, 48)]
    [InlineData("DvInline", "flowmeter", 64, 48)]
    [InlineData("DvInline", "filter", 60, 60)]
    [InlineData("DvInline", "strainer", 64, 56)]
    [InlineData("DvInline", "sight", 64, 48)]
    [InlineData("DvAgitator", null, 40, 100)]
    [InlineData("DvMotor", null, 76, 46)]
    public void Documented_sizes_match(string tag, string? kind, float w, float h)
    {
        var def = Devinno.UI.Process.BuiltInSymbols.Get(tag, kind)!;
        Assert.Equal(w, def.Width);
        Assert.Equal(h, def.Height);
    }

    [Theory]
    [InlineData("DvPump", "centrifugal", 36, 20)]
    [InlineData("DvPump", "blower", 36, 19)]
    [InlineData("DvValve", "gate", 32, 32)]
    [InlineData("DvInline", "flowmeter", 32, 32)]
    [InlineData("DvInline", "strainer", 20, 20)]
    public void Documented_port_heights_match(string tag, string kind, float inY, float outY)
    {
        var def = Devinno.UI.Process.BuiltInSymbols.Get(tag, kind)!;
        Assert.Equal(inY, def.Port("in")!.At * def.Height, 0.6);
        Assert.Equal(outY, def.Port("out")!.At * def.Height, 0.6);
    }

    // 유닛 예제의 파라미터(U="{BL1}")가 객체를 받도록 — 이름만 맞는 간단한 VM
    public sealed class DocVm
    {
        public sealed class Unit
        {
            public string Name { get; set; } = "BL";
            public string FeedTag { get; set; } = "FEED";
            public string TempTag { get; set; } = "TIA";
            public string TempText { get; set; } = "80.0";
            public string Pump1Tag { get; set; } = "P1";
            public string Pump2Tag { get; set; } = "P2";
            public double Level { get; set; } = 50;
        }

        public Unit BL1 { get; } = new() { Name = "BL-1" };
        public Unit BL2 { get; } = new() { Name = "BL-2" };
        public Unit BL3 { get; } = new() { Name = "BL-3" };
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

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
