using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

// dvml 소스 생성기 — vm 이 지정된 dvml 에서 id 핸들 필드·Markup 상수·Attach 를 만든다.
public class DvmlGeneratorTests
{
    private sealed class DvmlText(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, Encoding.UTF8);
    }

    private sealed record Result(
        string[] Sources,
        string[] HintNames,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableArray<Diagnostic> CompileErrors,
        CSharpCompilation Compilation)
    {
        public string All => string.Join("\n", Sources);
        public Diagnostic[] Ids(string id) => GeneratorDiagnostics.Where(d => d.Id == id).ToArray();
    }

    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Where(p => System.IO.Path.GetFileName(p) is var n && (n.StartsWith("System.") || n is "mscorlib.dll" or "netstandard.dll"))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(DvControl).Assembly.Location))
            .ToArray();

    private static Result Run(string csSource, params (string path, string dvml)[] files)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create("GenTest",
            new[] { CSharpSyntaxTree.ParseText(csSource, parse) },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new DvmlGenerator().AsSourceGenerator() },
            files.Select(f => (AdditionalText)new DvmlText(f.path, f.dvml)),
            parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var genDiags);

        var generated = driver.GetRunResult().Results.SelectMany(g => g.GeneratedSources).ToArray();
        var sources = generated.Select(g => g.SourceText.ToString()).ToArray();
        var hints = generated.Select(g => g.HintName).ToArray();
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        return new Result(sources, hints, genDiags, errors, (CSharpCompilation)output);
    }

    private const string MainVm = "namespace MyApp; public partial class MainVm { }";

    private const string MainDvml =
        "<DvStack vm=\"MyApp.MainVm\">\n" +
        "  <DvLabel id=\"lblTemp\" text=\"{Temperature}\"/>\n" +
        "  <DvButton id=\"btnStart\" text=\"시작\" onclick=\"Start\"/>\n" +
        "</DvStack>";

    [Fact]
    public void Generates_Fields_Markup_Attach()
    {
        var r = Run(MainVm, ("Pages/Main.dvml", MainDvml));

        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Single(r.Sources);
        Assert.Contains("global::Devinno.UI.Controls.DvButton btnStart", r.All);
        Assert.Contains("global::Devinno.UI.Controls.DvLabel lblTemp", r.All);
        Assert.Contains("public const string Markup", r.All);
        Assert.Contains("IDvControlHost.Attach", r.All);
        Assert.Contains("this.btnStart = c.Bind(this.btnStart, \"btnStart\");", r.All);
        Assert.Contains("partial void OnLoaded();", r.All);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void Generated_File_Name_Is_Vm_Dvml_g_cs()
    {
        var r = Run(MainVm, ("Pages/Main.dvml", MainDvml));
        Assert.Equal(new[] { "MyApp.MainVm.Dvml.g.cs" }, r.HintNames);
    }

    [Fact]
    public void Markup_Escapes_Quotes()
    {
        var r = Run(MainVm, ("Main.dvml", MainDvml));
        Assert.Empty(r.CompileErrors);

        using var ms = new MemoryStream();
        var emit = r.Compilation.Emit(ms);
        Assert.True(emit.Success);
        var asm = Assembly.Load(ms.ToArray());
        var field = asm.GetType("MyApp.MainVm")!.GetField("Markup", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(MainDvml, (string)field.GetRawConstantValue()!);
    }

    [Fact]
    public void Attach_Binds_And_Calls_OnLoaded_Once()
    {
        const string cs = "namespace MyApp; public partial class MainVm { public int Loaded; partial void OnLoaded() => Loaded++; public Devinno.UI.Controls.DvButton Btn => btnStart; }";
        var r = Run(cs, ("Main.dvml", "<DvStack vm=\"MyApp.MainVm\"><DvButton id=\"btnStart\" text=\"A\"/></DvStack>"));
        Assert.Empty(r.CompileErrors);

        using var ms = new MemoryStream();
        Assert.True(r.Compilation.Emit(ms).Success);
        var t = Assembly.Load(ms.ToArray()).GetType("MyApp.MainVm")!;
        var vm = Activator.CreateInstance(t)!;
        var markup = (string)t.GetField("Markup")!.GetRawConstantValue()!;

        var s1 = new DvSurface(markup, vm);
        var btn1 = t.GetProperty("Btn")!.GetValue(vm);
        var s2 = new DvSurface(markup, vm);
        var btn2 = t.GetProperty("Btn")!.GetValue(vm);

        Assert.NotNull(btn1);
        Assert.Same(btn1, btn2);
        Assert.Equal(1, (int)t.GetField("Loaded")!.GetValue(vm)!);
        GC.KeepAlive(s1); GC.KeepAlive(s2);
    }

    [Fact]
    public void Global_Namespace_Vm()
    {
        var r = Run("public partial class RootVm { }", ("Root.dvml", "<DvStack vm=\"RootVm\"><DvButton id=\"b\"/></DvStack>"));
        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Contains("partial class RootVm", Assert.Single(r.Sources));
        Assert.DoesNotContain("namespace", r.All);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void Id_Named_Like_Attach_Parameter_Compiles()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack vm=\"MyApp.MainVm\"><DvButton id=\"c\"/></DvStack>"));
        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void Value_Widgets_Get_Their_Handle_Types()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack vm=\"MyApp.MainVm\"><DvToggle id=\"t\"/><DvKnob id=\"k\"/><DvInput id=\"i\"/>" +
            "<DvSelect id=\"s\"/><DvSegment id=\"g\"/><DvDateTimeInput id=\"d\"/><DvColorInput id=\"o\"/><DvCheckBox id=\"c\"/><DvRadio id=\"r\"/><DvSlider id=\"l\"/><DvListBox id=\"b\"/><DvExpander id=\"x\"/><DvTreeView id=\"v\"/></DvStack>"));
        foreach (var (t, id) in new[] { ("DvToggle", "t"), ("DvKnob", "k"), ("DvInput", "i"), ("DvSelect", "s"), ("DvSegment", "g"), ("DvDateTimeInput", "d"), ("DvColorInput", "o"), ("DvCheckBox", "c"), ("DvRadio", "r"), ("DvSlider", "l"), ("DvListBox", "b"), ("DvExpander", "x"), ("DvTreeView", "v") })
            Assert.Contains($"global::Devinno.UI.Controls.{t} {id}", r.All);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void Unknown_Tag_Is_DvControl()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack vm=\"MyApp.MainVm\"><DvBox id=\"box1\"/><PumpCard id=\"p1\"/></DvStack>"));
        Assert.Contains("global::Devinno.UI.Controls.DvControl box1", r.All);
        Assert.Contains("global::Devinno.UI.Controls.DvControl p1", r.All);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void NoVm_GeneratesNothing()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack><DvButton id=\"btnStart\"/></DvStack>"));
        Assert.Empty(r.Sources);
        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void Non_Dvml_Additional_Files_Ignored()
    {
        var r = Run(MainVm, ("Main.xml", MainDvml));
        Assert.Empty(r.Sources);
    }

    [Fact]
    public void Duplicate_Id_DVML002()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack vm=\"MyApp.MainVm\">\n<DvButton id=\"b\"/>\n<DvLabel id=\"b\"/>\n</DvStack>"));
        var d = Assert.Single(r.Ids("DVML002"));
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("Main.dvml", d.Location.GetLineSpan().Path);
        Assert.Equal(2, d.Location.GetLineSpan().StartLinePosition.Line); // 0-based → 3번째 줄
        Assert.Empty(r.Sources);
    }

    [Theory]
    [InlineData("1btn")]
    [InlineData("class")]
    [InlineData("my-btn")]
    public void Invalid_Id_DVML003(string id)
    {
        var r = Run(MainVm, ("Main.dvml", $"<DvStack vm=\"MyApp.MainVm\"><DvButton id=\"{id}\"/></DvStack>"));
        var d = Assert.Single(r.Ids("DVML003"));
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Contains(id, d.GetMessage());
        Assert.Empty(r.Sources);
    }

    [Fact]
    public void Repeat_Id_DVML004_NoField()
    {
        const string dvml =
            "<DvStack vm=\"MyApp.MainVm\">\n" +
            "  <DvButton id=\"ok\"/>\n" +
            "  <DvRepeat items=\"{Xs}\"><DvButton id=\"inRepeat\"/></DvRepeat>\n" +
            "  <DvComponent name=\"Card\"><DvLabel id=\"inComp\"/></DvComponent>\n" +
            "</DvStack>";
        var r = Run(MainVm, ("Main.dvml", dvml));
        var ds = r.Ids("DVML004");
        Assert.Equal(2, ds.Length);
        Assert.All(ds, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        Assert.Contains("DvButton ok", r.All);
        Assert.DoesNotContain("DvButton inRepeat", r.All);
        Assert.DoesNotContain("DvLabel inComp", r.All);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void Id_On_Repeat_Itself_DVML004()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack vm=\"MyApp.MainVm\"><DvRepeat id=\"rep\" items=\"{Xs}\"/></DvStack>"));
        Assert.Single(r.Ids("DVML004"));
        Assert.DoesNotContain("DvControl rep", r.All);
    }

    [Fact]
    public void Missing_Vm_DVML005()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack vm=\"MyApp.NoSuchVm\"><DvButton id=\"b\"/></DvStack>"));
        var d = Assert.Single(r.Ids("DVML005"));
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("Main.dvml", d.Location.GetLineSpan().Path);
        Assert.Empty(r.Sources);
    }

    [Fact]
    public void Nested_Vm_DVML005()
    {
        var r = Run("namespace MyApp; public partial class Outer { public partial class Inner { } }",
            ("Main.dvml", "<DvStack vm=\"MyApp.Outer.Inner\"/>"));
        Assert.Single(r.Ids("DVML005"));
        Assert.Empty(r.Sources);
    }

    [Fact]
    public void NotPartial_DVML006()
    {
        var r = Run("namespace MyApp; public class MainVm { }", ("Main.dvml", MainDvml));
        var d = Assert.Single(r.Ids("DVML006"));
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Empty(r.Sources);
    }

    [Fact]
    public void SameVm_Twice_DVML007()
    {
        var r = Run(MainVm, ("A.dvml", MainDvml), ("B.dvml", MainDvml));
        var ds = r.Ids("DVML007");
        Assert.Equal(2, ds.Length);
        Assert.All(ds, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.Equal(new[] { "A.dvml", "B.dvml" }, ds.Select(d => d.Location.GetLineSpan().Path).OrderBy(p => p).ToArray());
        Assert.Empty(r.Sources);
    }

    [Fact]
    public void SameFile_AddedTwice_NoDVML007_OneOutput()
    {
        var full = System.IO.Path.GetFullPath("Pages/Main.dvml");
        var r = Run(MainVm, (full, MainDvml), (full.ToUpperInvariant(), MainDvml), ("Pages/Main.dvml", MainDvml));
        Assert.Empty(r.Ids("DVML007"));
        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Single(r.Sources);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void BadXml_DVML001()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack vm=\"MyApp.MainVm\">\n<DvButton id=\"b\">\n</DvStack>"));
        var d = Assert.Single(r.Ids("DVML001"));
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("Main.dvml", d.Location.GetLineSpan().Path);
        Assert.Equal(2, d.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Empty(r.Sources);
    }

    [Fact]
    public void Id_Clashes_With_Member_IsCompilerError()
    {
        var r = Run("namespace MyApp; public partial class MainVm { public int btnStart; }", ("Main.dvml", MainDvml));
        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Contains(r.CompileErrors, d => d.Id == "CS0102");
    }
    // DvPage·DvBar 를 (직접·간접) 상속한 vm 은 마크업 연결 멤버도 재정의한다
    [Theory]
    [InlineData("namespace MyApp; public partial class HomePage : Devinno.UI.App.DvPage { }")]
    [InlineData("namespace MyApp; public abstract class BasePage : Devinno.UI.App.DvPage { } public partial class HomePage : BasePage { }")]
    [InlineData("namespace MyApp; public partial class HomePage : Devinno.UI.App.DvBar { }")]
    public void DvPage_Derived_Emits_MarkupSource_Override(string cs)
    {
        const string dvml = "<DvStack vm=\"MyApp.HomePage\"><DvButton id=\"b\"/></DvStack>";
        var r = Run(cs, ("Home.dvml", dvml));
        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Contains("protected override string MarkupSource => Markup;", r.All);
        Assert.Empty(r.CompileErrors);

        using var ms = new MemoryStream();
        Assert.True(r.Compilation.Emit(ms).Success);
        var vm = Activator.CreateInstance(Assembly.Load(ms.ToArray()).GetType("MyApp.HomePage")!)!;
        var markup = vm switch
        {
            Devinno.UI.App.DvPage p => p.GetMarkup(),
            Devinno.UI.App.DvBar b => b.GetMarkup(),
            _ => null,
        };
        Assert.Equal(dvml, markup);
        var file = vm switch // 핫리로드가 감시할 dvml 경로
        {
            Devinno.UI.App.DvPage p => p.GetMarkupFile(),
            Devinno.UI.App.DvBar b => b.GetMarkupFile(),
            _ => null,
        };
        Assert.Equal("Home.dvml", file);
    }

    [Fact]
    public void DialogVm_EmitsMarkupSourceOverride()
    {
        const string dvml = "<DvStack vm=\"MyApp.D\"><DvButton id=\"b\"/></DvStack>";
        var r = Run("namespace MyApp; public sealed partial class D : Devinno.UI.App.DvDialog<string> { }", ("D.dvml", dvml));
        Assert.Empty(r.GeneratorDiagnostics);
        Assert.Contains("protected override string MarkupSource => Markup;", r.All);
        Assert.Empty(r.CompileErrors);
    }

    [Fact]
    public void NonPage_Vm_Unchanged()
    {
        var r = Run(MainVm, ("Main.dvml", MainDvml));
        Assert.DoesNotContain("MarkupSource", r.All);
        Assert.Empty(r.CompileErrors);
    }

    // ── 공정도 연결 검사(DVML008~011) — 실행 때 Trace 로 알리던 연결 오류를 빌드 때 ──

    private static Diagnostic[] Process(string body, string attrs = "")
    {
        var r = Run("public partial class P { }", ("P.dvml", $"<DvScreen>\n<DvProcess {attrs}>\n{body}\n</DvProcess>\n</DvScreen>"));
        return r.GeneratorDiagnostics.Where(d => d.Id is "DVML008" or "DVML009" or "DVML010" or "DVML011").ToArray();
    }

    private const string Plant = """
        <DvTerminal name="IN" text="IN"/>
        <DvVessel name="T1"/>
        <DvPump name="P1"/>
        <DvValve name="V1" kind="control"/>
        <DvValve3 name="V3"/>
        <DvExchanger name="E1"/>
        <DvTerminal name="OUT" text="OUT"/>
        <DvInstrument name="FIC" tag="FIC" on="V1"/>
        """;

    [Fact]
    public void Process_ValidMarkup_NoDiagnostics()
    {
        Assert.Empty(Process(Plant + """
            <DvLine from="IN" to="T1.top" medium="water"/>
            <DvLine from="T1.bottom" to="P1.in"/>
            <DvLine from="P1.out" to="V1.in"/>
            <DvLine from="V1.out" to="V3.a"/>
            <DvLine from="V3.b" to="E1.in"/>
            <DvLine from="V3.c" to="OUT"/>
            <DvSignal from="FIC" to="V1"/>
            """, "direction=\"lr\" look=\"rich\" align=\"auto\""));
    }

    [Theory]
    [InlineData("<DvLine from=\"NOPE\" to=\"P1.in\"/>", "DVML008", "NOPE")]
    [InlineData("<DvLine from=\"P1.out\" to=\"V1.inlet\"/>", "DVML009", "inlet")]
    [InlineData("<DvLine from=\"T1\" to=\"P1.in\"/>", "DVML009", "포트를 골라야")]
    [InlineData("<DvLine from=\"E1.out3\" to=\"OUT\"/>", "DVML009", "in·out·in2·out2")]
    [InlineData("<DvLine from=\"FIC\" to=\"P1.in\"/>", "DVML008", "DvSignal")]
    [InlineData("<DvSignal from=\"FIC\" to=\"V1.in\"/>", "DVML009", "포트 없이")]
    [InlineData("<DvSignal from=\"FIC\" to=\"OUT\"/>", "DVML009", "끝점")]
    [InlineData("<DvSignal from=\"FT\" to=\"V1\"/>", "DVML008", "FT")]
    [InlineData("<DvInstrument name=\"X\" tag=\"PI\" on=\"NOPE\"/>", "DVML008", "NOPE")]
    [InlineData("<DvInstrument name=\"X\" tag=\"PI\"/>", "DVML008", "on")]
    [InlineData("<DvPump name=\"P1\"/>", "DVML010", "P1")]
    [InlineData("<DvInstrument name=\"V1\" tag=\"PI\" on=\"P1\"/>", "DVML010", "V1")]
    [InlineData("<DvPump name=\"P9\" kind=\"turbo\"/>", "DVML011", "turbo")]
    [InlineData("<DvPump name=\"P9\" rotate=\"45\"/>", "DVML011", "45")]
    [InlineData("<DvLine from=\"P1.out\" to=\"V1.in\" medium=\"lava\"/>", "DVML011", "lava")]
    public void Process_Errors_Reported(string extra, string id, string text)
    {
        var d = Assert.Single(Process(Plant + extra));
        Assert.Equal(id, d.Id);
        Assert.Contains(text, d.GetMessage());
        Assert.Equal("P.dvml", d.Location.GetLineSpan().Path);
        Assert.True(d.Location.GetLineSpan().StartLinePosition.Line > 0); // 그 줄을 가리킨다
    }

    [Fact]
    public void Process_ProcessAttributes_Checked()
    {
        var d = Process(Plant, "direction=\"rl\" look=\"dark\" align=\"middle\"");
        Assert.Equal(3, d.Count(x => x.Id == "DVML011"));
    }

    [Fact]
    public void Process_Units_ScopeAndPorts()
    {
        const string unit = """
            <DvUnit name="U1">
              <DvPort name="in" side="left"/>
              <DvVessel name="T"/>
              <DvLine from="in" to="T.top"/>
              <DvLine from="T.bottom" to="NOPE"/>
            </DvUnit>
            <DvTerminal name="A" text="A"/>
            <DvLine from="A" to="U1.in"/>
            <DvLine from="A" to="U1.out"/>
            <DvLine from="A" to="U1.T.top"/>
            """;
        var d = Process(unit);
        Assert.Contains(d, x => x.Id == "DVML008" && x.GetMessage().Contains("NOPE"));         // 유닛 안 이름
        Assert.Contains(d, x => x.Id == "DVML009" && x.GetMessage().Contains("'out'"));        // 없는 유닛 포트
        Assert.Contains(d, x => x.Id == "DVML009" && x.GetMessage().Contains("DvPort"));       // 안쪽 직접 연결
        Assert.Equal(3, d.Length);
    }

    [Fact]
    public void Process_SkipsWhatIsUnknownAtBuildTime()
    {
        // 바인딩 이름·등록 심볼·컴포넌트 유닛은 실행 때만 안다 — 오류를 내지 않는다
        Assert.Empty(Process("""
            <DvPump name="{Name}"/>
            <Cyclone name="CY"/>
            <Blender name="BL1" U="{BL1}"/>
            <DvTerminal name="A" text="A"/>
            <DvLine from="A" to="CY.whatever"/>
            <DvLine from="BL1.out" to="A"/>
            <DvLine from="A" to="SOMEONE.in"/>
            <DvLine from="A" to="{X}"/>
            <DvPump name="P" kind="{K}" rotate="{R}"/>
            """));
    }

    [Fact]
    public void Process_Errors_DoNotBlockGeneration()
    {
        var r = Run(MainVm, ("Main.dvml", "<DvStack vm=\"MyApp.MainVm\"><DvButton id=\"b\"/><DvProcess><DvLine from=\"X\" to=\"Y\"/></DvProcess></DvStack>"));
        Assert.NotEmpty(r.Ids("DVML008"));
        Assert.Contains("DvButton b", r.All);
    }

    [Fact]
    public void Process_Tables_MatchLibrary()
    {
        foreach (var (tag, kinds) in Devinno.UI.Process.BuiltInSymbols.Kinds)
        {
            Assert.Equal(kinds, Devinno.UI.Generators.ProcessCheck.Kinds[tag]);
            foreach (var kind in kinds)
                Assert.Equal(Devinno.UI.Process.BuiltInSymbols.Get(tag, kind)!.Ports.Select(p => p.Name), Devinno.UI.Generators.ProcessCheck.Ports[tag]);
        }

        Assert.Equal(Devinno.UI.Process.BuiltInSymbols.Kinds.Keys.OrderBy(k => k), Devinno.UI.Generators.ProcessCheck.Kinds.Keys.OrderBy(k => k));
        Assert.Equal(Devinno.UI.Process.ProcessMedium.Names, Devinno.UI.Generators.ProcessCheck.Media);
    }

    // ── 기본 컨트롤 검사(DVML015~017) ──

    private static Diagnostic[] Widgets(string body)
        => Run("public partial class P { }", ("P.dvml", $"<DvStack>\n{body}\n</DvStack>")).GeneratorDiagnostics
            .Where(d => d.Id is "DVML015" or "DVML016" or "DVML017").ToArray();

    [Fact]
    public void Widget_Valid_NoDiagnostics()
    {
        Assert.Empty(Widgets("""
            <DvRadio text="A" value="{Bind Mode}" option="a"/>
            <DvListBox value="{Bind One}"/>
            <DvListBox selection-mode="multi" selected-items="{Bind Many}"/>
            <DvListBox selection-mode="{Mode}" value="{Bind One}" selected-items="{Bind Many}"/>
            <DvSlider direction="up"/>
            <DvSlider direction="{Dir}"/>
            """));
    }

    [Theory]
    [InlineData("<DvRadio text=\"A\" value=\"{Bind Mode}\"/>", "DVML015", "option")]
    [InlineData("<DvListBox selection-mode=\"multi\" value=\"{Bind One}\"/>", "DVML016", "selected-items")]
    [InlineData("<DvListBox selected-items=\"{Bind Many}\"/>", "DVML016", "selection-mode")]
    [InlineData("<DvListBox selection-mode=\"many\"/>", "DVML017", "many")]
    [InlineData("<DvSlider direction=\"h\"/>", "DVML017", "right·left·up·down")]
    public void Widget_Mistakes(string body, string id, string mentions)
    {
        var d = Assert.Single(Widgets(body));
        Assert.Equal(id, d.Id);
        Assert.Contains(mentions, d.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
    }

    // ── 스타일 검사(DVML012~014) — 없는 스타일 속성·토큰·클래스 ──

    private static Diagnostic[] Styles(params (string path, string dvml)[] files)
    {
        var r = Run("public partial class P { }", files);
        return r.GeneratorDiagnostics.Where(d => d.Id is "DVML012" or "DVML013" or "DVML014").ToArray();
    }

    private static Diagnostic[] Styles(string body) => Styles(("P.dvml", $"<DvStack>\n{body}\n</DvStack>"));

    [Fact]
    public void Style_Valid_NoDiagnostics()
    {
        Assert.Empty(Styles("""
            <DvLabel class="title muted" text="A" style="color:token(text); padding:8 12; background:var(--surface)"/>
            <DvButton class="btn btn-primary" hover-style="background:token(primary)" text="B"/>
            <DvCard title="C" style="max-height:320; overflow:scroll"/>
            <DvLabel style="color:{Color}; padding:{Pad}"/>
            <DvLabel style="{WholeStyle}"/>
            <DvLabel class="{Cls}"/>
            <DvLamp color="token(tone-{tone}-fg)"/>
            <DvStyle>
              .my-chip { background: token(surface); border-radius: token(radius.lg) }
              .my-chip:hover { opacity: 0.8 }
            </DvStyle>
            <DvLabel class="my-chip" text="D"/>
            """));
    }

    [Fact]
    public void Style_UnknownKey_WithSuggestion_AndColumn()
    {
        var d = Assert.Single(Styles("<DvLabel text=\"A\" style=\"padding:8; colr:#fff\"/>"));
        Assert.Equal("DVML012", d.Id);
        Assert.Contains("'colr'", d.GetMessage());
        Assert.Contains("'color'", d.GetMessage());
        var span = d.Location.GetLineSpan();
        var line = "<DvLabel text=\"A\" style=\"padding:8; colr:#fff\"/>";
        Assert.Equal(line.IndexOf("colr"), span.StartLinePosition.Character); // 'colr' 를 가리킨다
        Assert.Equal(4, span.EndLinePosition.Character - span.StartLinePosition.Character);
    }

    [Theory]
    [InlineData("<DvButton text=\"A\" press-style=\"font-wieght:bold\"/>", "DVML012", "font-weight")]
    [InlineData("<DvLabel style=\"animation:spin 2s\"/>", "DVML012", "animation")]
    [InlineData("<DvLabel style=\"color:{Color}; colr:{X}\"/>", "DVML012", "colr")]
    [InlineData("<DvLabel style=\"color:token(txet)\"/>", "DVML013", "txet")]
    [InlineData("<DvLabel style=\"background:var(--nope)\"/>", "DVML013", "nope")]
    [InlineData("<DvLamp color=\"token(nope)\"/>", "DVML013", "nope")]
    [InlineData("<DvLabel class=\"titel\"/>", "DVML014", "titel")]
    public void Style_Errors(string body, string id, string text)
    {
        var d = Assert.Single(Styles(body));
        Assert.Equal(id, d.Id);
        Assert.Contains(text, d.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
    }

    [Fact]
    public void Style_DvStyleBody_KeysAndTokens_PointIntoText()
    {
        var dvml = "<DvStack>\n<DvStyle>\n  .a { colr: red }\n  .b { color: token(nope) }\n</DvStyle>\n<DvLabel class=\"a b\"/>\n</DvStack>";
        var d = Styles(("P.dvml", dvml)).OrderBy(x => x.Id).ToArray();
        Assert.Equal(["DVML012", "DVML013"], d.Select(x => x.Id));
        Assert.Equal(2, d[0].Location.GetLineSpan().StartLinePosition.Line); // 0 기준 셋째 줄(.a)
        Assert.Equal("  .a { ".Length, d[0].Location.GetLineSpan().StartLinePosition.Character);
        Assert.Equal(3, d[1].Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public void Style_ClassDefinedInOtherDvml_Counts()
    {
        // 클래스는 프로젝트 dvml 전체의 <DvStyle> 을 본 뒤 판단
        Assert.Empty(Styles(("Styles.dvml", "<DvBox><DvStyle>.shared { padding: 4 }</DvStyle></DvBox>"),
            ("P.dvml", "<DvStack><DvLabel class=\"shared\"/></DvStack>")));
        var d = Assert.Single(Styles(("P.dvml", "<DvStack><DvLabel class=\"shared\"/></DvStack>")));
        Assert.Equal("DVML014", d.Id);
    }

    [Fact]
    public void Style_Tables_MatchLibrary()
    {
        // 스타일 속성: StyleParser 의 case 목록
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Devinno.UI", "Styling", "StyleParser.cs"));
        var keys = System.Text.RegularExpressions.Regex.Matches(src, "case \"([a-z-]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Equal(keys.OrderBy(k => k), Devinno.UI.Generators.StyleCheck.Keys.OrderBy(k => k));

        // 토큰: 내장 테마(다크·라이트)
        var tokens = Devinno.UI.Styling.DvTheme.Dark.Tokens.Keys.Concat(Devinno.UI.Styling.DvTheme.Light.Tokens.Keys)
            .Select(k => k.ToLowerInvariant()).ToHashSet();
        Assert.Equal(tokens.OrderBy(k => k, StringComparer.Ordinal), Devinno.UI.Generators.StyleCheck.Tokens.OrderBy(k => k, StringComparer.Ordinal));

        // 클래스: 내장 스타일시트의 선택자
        var css = System.Text.RegularExpressions.Regex.Replace(Devinno.UI.Styling.BuiltInStyles.Sheet, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        var classes = System.Text.RegularExpressions.Regex.Matches(css, @"(?:^|[},])\s*\.([A-Za-z0-9_-]+)").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Equal(classes.OrderBy(k => k, StringComparer.Ordinal), Devinno.UI.Generators.StyleCheck.Classes.OrderBy(k => k, StringComparer.Ordinal));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Devinno.UI")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
}
