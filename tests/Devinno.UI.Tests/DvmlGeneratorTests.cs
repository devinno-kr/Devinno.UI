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

    // 메시지는 기계 언어와 상관없이 기본(영어) 리소스로 비교한다 — CI 는 영어, 개발 PC 는 한국어
    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    private const string MainVm = "namespace MyApp; public partial class MainVm { public void Start() { } }";

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
        Assert.Contains(id, d.GetMessage(Invariant));
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
        var r = Run("namespace MyApp; public partial class MainVm { public int btnStart; public void Start() { } }", ("Main.dvml", MainDvml));
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
            <DvSignal from="FIC" to="P1" kind="pneumatic"/>
            <DvSignal from="FIC" to="T1" kind="{K}"/>
            <DvSignal from="FIC" to="E1" color="#D32F2F" thickness="2.5"/>
            <DvSignal from="FIC" to="V3" color="token(text)" thickness="{W}"/>
            <DvSignal from="P1" to="T1" color="{C}"/>
            <DvInstrument name="PI1" tag="PI" on="P1" x="120" y="40.5"/>
            <DvInstrument name="PI2" tag="PI" on="P1" x="{X}" y="{Y}"/>
            <DvLine from="V3.b" to="OUT" via="10,20 30,20"/>
            <DvLine from="P1.out" to="V3.a" via="{Route}"/>
            """, "direction=\"lr\" look=\"rich\" align=\"auto\""));
    }

    // 면 위 자리(장비.면@위치)와 계기 연결선(tap) — tap 이 있으면 on 은 없어도 된다
    [Fact]
    public void Process_FaceAtAndTap_NoDiagnostics()
    {
        Assert.Empty(Process(Plant + """
            <DvLine from="IN" to="T1.left@0.15"/>
            <DvLine from="T1.right@.9" to="P1.in"/>
            <DvInstrument name="LS" tag="L" tap="T1.left@0.8"/>
            <DvInstrument name="PI" tag="PI" tap="P1.out"/>
            """));
    }

    [Theory]
    [InlineData("<DvLine from=\"NOPE\" to=\"P1.in\"/>", "DVML008", "NOPE")]
    [InlineData("<DvLine from=\"P1.out\" to=\"V1.inlet\"/>", "DVML009", "inlet")]
    [InlineData("<DvLine from=\"T1\" to=\"P1.in\"/>", "DVML009", "needs a port")]
    [InlineData("<DvLine from=\"E1.out3\" to=\"OUT\"/>", "DVML009", "in·out·in2·out2")]
    [InlineData("<DvLine from=\"FIC\" to=\"P1.in\"/>", "DVML008", "DvSignal")]
    [InlineData("<DvSignal from=\"FIC\" to=\"V1.in\"/>", "DVML009", "without ports")]
    [InlineData("<DvSignal from=\"FIC\" to=\"OUT\"/>", "DVML009", "terminals")]
    [InlineData("<DvSignal from=\"FT\" to=\"V1\"/>", "DVML008", "FT")]
    [InlineData("<DvInstrument name=\"X\" tag=\"PI\" on=\"NOPE\"/>", "DVML008", "NOPE")]
    [InlineData("<DvInstrument name=\"X\" tag=\"PI\"/>", "DVML008", "on")]
    [InlineData("<DvPump name=\"P1\"/>", "DVML010", "P1")]
    [InlineData("<DvInstrument name=\"V1\" tag=\"PI\" on=\"P1\"/>", "DVML010", "V1")]
    [InlineData("<DvLine from=\"IN\" to=\"T1.middle@0.5\"/>", "DVML009", "top·bottom·left·right")]
    [InlineData("<DvLine from=\"IN\" to=\"T1.left@x\"/>", "DVML009", "0")]
    [InlineData("<DvInstrument name=\"X\" tag=\"PI\" tap=\"NOPE.in\"/>", "DVML008", "NOPE")]
    [InlineData("<DvInstrument name=\"X\" tag=\"PI\" tap=\"P1.inlet\"/>", "DVML009", "inlet")]
    [InlineData("<DvPump name=\"P9\" kind=\"turbo\"/>", "DVML011", "turbo")]
    [InlineData("<DvPump name=\"P9\" rotate=\"45\"/>", "DVML011", "45")]
    [InlineData("<DvLine from=\"P1.out\" to=\"V1.in\" medium=\"lava\"/>", "DVML011", "lava")]
    [InlineData("<DvSignal from=\"FIC\" to=\"V1\" kind=\"wireless\"/>", "DVML011", "wireless")]
    [InlineData("<DvSignal from=\"FIC\" to=\"V1\" color=\"crimson\"/>", "DVML011", "crimson")]
    [InlineData("<DvSignal from=\"FIC\" to=\"V1\" thickness=\"0\"/>", "DVML011", "thickness")]
    [InlineData("<DvSignal from=\"FIC\" to=\"V1\" thickness=\"thick\"/>", "DVML011", "thick")]
    [InlineData("<DvLine from=\"P1.out\" to=\"V1.in\" via=\"10,20 30\"/>", "DVML011", "10,20 30")]
    [InlineData("<DvInstrument name=\"X\" tag=\"PI\" on=\"P1\" x=\"abc\"/>", "DVML011", "abc")]
    public void Process_Errors_Reported(string extra, string id, string text)
    {
        var d = Assert.Single(Process(Plant + extra));
        Assert.Equal(id, d.Id);
        Assert.Contains(text, d.GetMessage(Invariant));
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
        Assert.Contains(d, x => x.Id == "DVML008" && x.GetMessage(Invariant).Contains("NOPE"));         // 유닛 안 이름
        Assert.Contains(d, x => x.Id == "DVML009" && x.GetMessage(Invariant).Contains("'out'"));        // 없는 유닛 포트
        Assert.Contains(d, x => x.Id == "DVML009" && x.GetMessage(Invariant).Contains("DvPort"));       // 안쪽 직접 연결
        Assert.Equal(3, d.Length);
    }

    [Fact]
    public void Process_SkipsWhatIsUnknownAtBuildTime()
    {
        // 등록 심볼·컴포넌트 유닛의 포트와 바인딩된 값(kind·rotate)은 실행 때만 안다 — 오류를 내지 않는다
        Assert.Empty(Process("""
            <Cyclone name="CY"/>
            <Blender name="BL1" U="{BL1}"/>
            <DvTerminal name="A" text="A"/>
            <DvLine from="A" to="CY.whatever"/>
            <DvLine from="BL1.out" to="A"/>
            <DvPump name="P" kind="{K}" rotate="{R}"/>
            """));
    }

    // 3단계 D3 — 공정도 이름·참조는 원문(바인딩 아님): { 가 든 이름도 그 글자 그대로 검사하고, 실행 때처럼 경고한다(DVML011)
    [Fact]
    public void Process_BraceNames_AreLiteral()
    {
        var d = Process("""
            <DvPump name="{Name}"/>
            <DvTerminal name="A" text="A"/>
            <DvLine from="A" to="SOMEONE.in"/>
            <DvLine from="A" to="{X}"/>
            <DvLine from="A" to="{Name}.in"/>
            """);
        Assert.Equal(["DVML011 <DvPump> name", "DVML008 SOMEONE", "DVML011 <DvLine> to", "DVML008 {X}", "DVML011 <DvLine> to"],
            d.Select(x => x.Id + " " + (x.Id == "DVML011" ? x.GetMessage(Invariant).Split('=')[0] : System.Text.RegularExpressions.Regex.Match(x.GetMessage(Invariant), "'([^']*)' is not").Groups[1].Value)));
        Assert.Contains("not bindable", d[0].GetMessage(Invariant));
        Assert.Contains("바인딩", d[0].GetMessage(new System.Globalization.CultureInfo("ko-KR")));
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
                Assert.Equal(Devinno.UI.Process.BuiltInSymbols.Get(tag, kind)!.Ports.Select(p => p.Name), Devinno.UI.Generators.ProcessCheck.PortsOf(tag, kind));
        }

        Assert.Equal(Devinno.UI.Process.BuiltInSymbols.Kinds.Keys.OrderBy(k => k), Devinno.UI.Generators.ProcessCheck.Kinds.Keys.OrderBy(k => k));
        Assert.Equal(Devinno.UI.Process.ProcessMedium.Names, Devinno.UI.Generators.ProcessCheck.Media);
    }

    // 핸들 형식 목록은 라이브러리 한 곳(TagCatalog)에만 — 생성기 복사본이 같은지
    [Fact]
    public void HandleTags_MatchLibrary()
    {
        Assert.Equal(Devinno.UI.Markup.TagCatalog.HandleTags.OrderBy(t => t), Devinno.UI.Generators.DvmlModel.HandleTags.OrderBy(t => t));
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
        Assert.Contains(mentions, d.GetMessage(Invariant));
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
        Assert.Contains("'colr'", d.GetMessage(Invariant));
        Assert.Contains("'color'", d.GetMessage(Invariant));
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
        Assert.Contains(text, d.GetMessage(Invariant));
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
    // ── 핸들러 검사(DVML018) — 핸들러 속성이 VM 의 public 인스턴스 메서드를 가리켜야 한다(실행 때는 조용히 아무 일도 없다) ──

    private const string HandlerVm = """
        namespace MyApp;
        public class BaseVm { public void FromBase() { } }
        public partial class HVm : BaseVm
        {
            public void Go() { }
            public void Pick(object item) { }
            public bool Gate(bool v) => v;
            public void Ack(long id) { }
            private void Hidden() { }
            public static void Shared() { }
        }
        """;

    private static Diagnostic[] Handlers(string body) =>
        Run(HandlerVm, ("H.dvml", $"<DvStack vm=\"MyApp.HVm\">\n{body}\n</DvStack>")).Ids("DVML018");

    [Fact]
    public void Handler_Valid_NoDiagnostics()
    {
        Assert.Empty(Handlers("""
            <DvButton text="A" onclick="Go"/>
            <DvButton text="B" onclick="FromBase"/>
            <DvToggle value="{Bind On}" onbeforecommit="Gate"/>
            <DvAlarmList items="{Rows}" onack="Ack"/>
            <DvRepeat items="{Items}"><DvButton text="{Name}" onclick="Pick"/></DvRepeat>
            <DvDataGrid items="{Rows}" onrowclick="Pick">
              <DvColumn field="Name" cell-type="button" onclick="Pick" onbeforecommit="Gate"/>
            </DvDataGrid>
            <DvCard title="C" onclick="Go"/>
            <DvButton text="empty" onclick=""/>
            """));
    }

    [Theory]
    [InlineData("<DvButton onclick=\"Goo\"/>", "'Goo' is not a public instance method of 'HVm'")]
    [InlineData("<DvButton onclick=\"Hidden\"/>", "'Hidden' is not a public instance method")]
    [InlineData("<DvButton onclick=\"Shared\"/>", "'Shared' is not a public instance method")]
    [InlineData("<DvToggle onbeforecommit=\"Gat\"/>", "onbeforecommit=\"Gat\"")]
    [InlineData("<DvAlarmBanner onack=\"Akc\"/>", "onack=\"Akc\"")]
    [InlineData("<DvDataGrid onrowclick=\"Pik\"/>", "onrowclick=\"Pik\"")]
    [InlineData("<DvRepeat items=\"{X}\"><DvButton onclick=\"Pik\"/></DvRepeat>", "'Pik'")]
    [InlineData("<DvDataGrid><DvColumn field=\"A\" cell-type=\"button\" onclick=\"Pik\"/></DvDataGrid>", "'Pik'")]
    [InlineData("<DvButton onclick=\"{Handler}\"/>", "bindings")]
    [InlineData("<DvButton onclick=\"Pick(item)\"/>", "onclick=\"Pick\"")]
    [InlineData("<DvButton onclick=\" Go\"/>", "is not a method name")]
    public void Handler_Errors(string body, string mentions)
    {
        var d = Assert.Single(Handlers(body));
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Contains(mentions, d.GetMessage(Invariant));
    }

    [Fact]
    public void Handler_Location_PointsAtValue()
    {
        var d = Assert.Single(Handlers("<DvButton text=\"A\" onclick=\"Goo\"/>"));
        var span = d.Location.GetLineSpan();
        Assert.Equal(1, span.StartLinePosition.Line);
        Assert.Equal("<DvButton text=\"A\" onclick=\"Goo\"/>".IndexOf("Goo"), span.StartLinePosition.Character);
        Assert.Equal(3, span.EndLinePosition.Character - span.StartLinePosition.Character);
    }

    [Fact]
    public void Handler_Skipped_WhereTheTargetIsUnknown()
    {
        // DvComponent 정의 안(어느 화면 VM 인지 모른다) · onclick 을 매개변수로 받는 컴포넌트의 인스턴스
        Assert.Empty(Handlers("""
            <DvComponent name="MyTile">
              <DvParam name="title"/>
              <DvButton text="{title}" onclick="Whatever"/>
            </DvComponent>
            <DvComponent name="ClickParam">
              <DvParam name="onclick"/>
              <DvLabel text="{onclick}"/>
            </DvComponent>
            <ClickParam onclick="NotAMethod"/>
            """));

        // vm 이 없는 dvml — 대상 VM 이 없다
        var r = Run(HandlerVm, ("N.dvml", "<DvStack><DvButton onclick=\"Nope\"/></DvStack>"));
        Assert.Empty(r.Ids("DVML018"));
    }

    // ── 없는 속성 검사(DVML019) — 내장 태그에 그 태그·공통 정의에 없는 속성 ──

    // 생성기의 속성 이름 목록은 라이브러리 정의(TagSchema)의 복사본 — 이름만, 태그마다 같아야 한다
    [Fact]
    public void AttributeTables_MatchLibrary()
    {
        Assert.Equal(Devinno.UI.Markup.TagSchema.Common.Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal),
            AttributeCheck.Common.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(Devinno.UI.Markup.TagSchema.Tags.Keys.OrderBy(k => k, StringComparer.Ordinal), AttributeCheck.Tags.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (var (tag, defs) in Devinno.UI.Markup.TagSchema.Tags)
            Assert.True(defs.Select(a => a.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal)
                .SequenceEqual(AttributeCheck.Tags[tag].OrderBy(n => n, StringComparer.Ordinal)), tag);
    }

    private static Diagnostic[] Attrs(string dvml) =>
        Run("public partial class P { }", ("P.dvml", dvml)).Ids("DVML019");

    [Fact]
    public void Attribute_Unknown_Warns()
    {
        var d = Assert.Single(Attrs("<DvStack>\n<DvLabel txet=\"a\"/>\n</DvStack>"));
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        var msg = d.GetMessage(Invariant);
        Assert.Contains("DvLabel", msg);
        Assert.Contains("txet", msg);
        Assert.Contains("'text'", msg); // 가까운 이름
        Assert.Equal(1, d.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Contains("txet", d.GetMessage(new System.Globalization.CultureInfo("ko-KR")));
    }

    [Fact]
    public void Attribute_Valid_NoWarnings()
    {
        Assert.Empty(Attrs("""
            <DvStack vm="MyApp.P" font-lang-ko="Malgun Gothic" xmlns:x="urn:x" gap="sm" padding="md" class="card" style="color:red">
              <DvLabel id="l" text="{T}" grow="1" visible="{V}" content-align="middle-center"/>
              <DvButton text="Go" icon="fa-play" variant="primary" size="lg" onclick="Go" hold="500" hover-style="opacity:0.8"/>
              <DvSlider value="{Bind X}" min="0" max="10" step="1" direction="up" show-value="true" onbeforecommit="Check"/>
              <DvGrid columns="3" rows="auto" gap="none"/>
              <DvDataGrid items="{Rows}" row-height="40"><DvColumn header="A" field="A" cell-type="lamp" on-color="#0f0"/></DvDataGrid>
              <DvProcess placement="fixed" base-size="800 600"><DvVessel name="T1" level="50" width="80" x="10" y="20"/></DvProcess>
              <DvLine visible="false"/>
            </DvStack>
            """));
    }

    [Fact]
    public void Attribute_Skipped_OnUnknownTags()
    {
        // 사용자 컴포넌트 인스턴스(파라미터) · 등록 위젯(빌드 때 모름) · DvSymbol 정의 안 · 컴포넌트 정의 요소 · 내장이 아닌 루트
        Assert.Empty(Attrs("""
            <DvStack>
              <DvComponent name="Tile" anything="x">
                <DvParam name="title" default="t"/>
                <DvLabel text="{title}"/>
              </DvComponent>
              <Tile title="A" whatever="1"/>
              <BatteryGauge level="80" cells="4"/>
              <DvSymbol name="Tank2" width="40" height="60">
                <DvPort name="in" side="left" at="0.5" role="inlet"/>
                <DvLabel foo="bar"/>
              </DvSymbol>
            </DvStack>
            """));
        Assert.Empty(Attrs("<DvPageFrame vm=\"MyApp.P\" title=\"T\" subtitle=\"S\"><DvLabel text=\"a\"/></DvPageFrame>"));
    }

    // 3단계 R9 — 태그가 쓰지 않던 속성(공용 블록의 dead read)은 이제 없는 속성
    [Fact]
    public void Attribute_DeadReads_Warn()
    {
        var d = Attrs("""
            <DvStack>
              <DvSparkline values="1,2" show-value="true"/>
              <DvGauge value="1" sweep="180"/>
              <DvTabs placeholder="p"><DvTab value="a"/></DvTabs>
              <DvAlarmBanner row-height="40"/>
              <DvProcess><DvPump name="P" text="x"/><DvTerminal name="T" rotate="90" label="L" text="IN"/></DvProcess>
            </DvStack>
            """);
        Assert.Equal(["DvSparkline show-value", "DvGauge sweep", "DvTabs placeholder", "DvAlarmBanner row-height", "DvPump text", "DvTerminal rotate", "DvTerminal label"],
            d.Select(x => System.Text.RegularExpressions.Regex.Match(x.GetMessage(Invariant), @"^(\S+) has no attribute '([^']+)'")).Select(m => $"{m.Groups[1]} {m.Groups[2]}"));
    }

    // 4단계 이름 통일 — 옛 속성 이름은 DVML019 이고, 메시지가 새 이름을 알린다(비슷한 이름 제안으로는 못 찾는 것들)
    [Theory]
    [InlineData("<DvExpander header=\"A\"/>", "DvExpander", "header", "title")]
    [InlineData("<DvDataGrid><DvColumn field=\"A\"/><DvSummaryRow title=\"T\" fn=\"sum\"/></DvDataGrid>", "DvSummaryRow", "title", "label")]
    [InlineData("<DvProcess><DvInstrument name=\"FIC\" tag=\"FIC\" on=\"P\"/><DvPump name=\"P\"/><DvSignal from=\"FIC\" to=\"P\" width=\"2\"/></DvProcess>", "DvSignal", "width", "thickness")]
    [InlineData("<DvDataGrid><DvColumn field=\"A\" cell-type=\"button\" enabled=\"CanAck\"/></DvDataGrid>", "DvColumn", "enabled", "cell-disabled")]
    [InlineData("<DvLamp on=\"true\" blink-when=\"true\"/>", "DvLamp", "blink-when", "blink")]
    [InlineData("<DvDataGrid selection-mode=\"multi\" selection=\"{Bind Picked}\"/>", "DvDataGrid", "selection", "selected-items")]
    public void Attribute_Renamed_SaysTheNewName(string body, string tag, string old, string now)
    {
        var d = Assert.Single(Attrs($"<DvStack>{body}</DvStack>"));
        var msg = d.GetMessage(Invariant);
        Assert.StartsWith($"{tag} has no attribute '{old}'", msg);
        Assert.Contains($"'{old}' was renamed to '{now}'", msg);
        Assert.Contains($"'{old}' 는 '{now}' 로 이름이 바뀌", d.GetMessage(new System.Globalization.CultureInfo("ko-KR"))); // Resources.ko.resx 문구 — 한국어 항목이 없으면 실패
    }

    // 4단계 검토 M4 — 생성기 Renamed 는 라이브러리 TagSchema.Renamed 의 복사본(뜻 뒤집힘 포함). 새 이름은 그 태그의 속성이고 옛 이름은 아니다
    [Fact]
    public void Renamed_MatchesLibrary_NewNamesExist_OldNamesDoNot()
    {
        static IEnumerable<string> Rows(IEnumerable<KeyValuePair<(string Tag, string Old), (string New, bool Reversed)>> map) =>
            map.Select(kv => $"{kv.Key.Tag} {kv.Key.Old} -> {kv.Value.New} {kv.Value.Reversed}").OrderBy(x => x, StringComparer.Ordinal);

        Assert.Equal(Rows(Devinno.UI.Markup.TagSchema.Renamed), Rows(AttributeCheck.Renamed));
        Assert.Equal(6, Devinno.UI.Markup.TagSchema.Renamed.Count);
        foreach (var ((tag, old), (now, _)) in Devinno.UI.Markup.TagSchema.Renamed)
        {
            var own = Devinno.UI.Markup.TagSchema.Tags[tag].Select(a => a.Name).Concat(Devinno.UI.Markup.TagSchema.CommonOf(tag).Select(a => a.Name)).ToList();
            Assert.True(own.Contains(now), $"{tag} has no '{now}'");
            Assert.False(own.Contains(old), $"{tag} still has '{old}'");
        }
    }

    // enabled → cell-disabled 는 뜻도 뒤집혔다(true = 버튼 꺼짐) — 메시지가 그것을 말한다
    [Fact]
    public void Attribute_Renamed_Enabled_SaysTheSenseFlipped()
    {
        var d = Assert.Single(Attrs("<DvStack><DvDataGrid><DvColumn field=\"A\" cell-type=\"button\" enabled=\"CanAck\"/></DvDataGrid></DvStack>"));
        Assert.Contains("true = the button is disabled", d.GetMessage(Invariant));
    }

    [Fact]
    public void Attribute_NewNames_NoWarnings()
    {
        Assert.Empty(Attrs("""
            <DvStack>
              <DvExpander title="A"/>
              <DvLamp on="true" blink="true"/>
              <DvDataGrid selection-mode="multi" selected-items="{Bind Picked}">
                <DvColumn field="A" cell-type="button" cell-disabled="AckDisabled"/>
                <DvSummaryRow label="T" fn="sum"/>
              </DvDataGrid>
              <DvProcess><DvInstrument name="FIC" tag="FIC" on="P"/><DvPump name="P"/><DvSignal from="FIC" to="P" thickness="2"/></DvProcess>
            </DvStack>
            """));
    }

    // fit="shrink" → scale-down: 옛 값은 DVML020 이고 값 목록이 새 이름을 보여 준다
    [Fact]
    public void Fit_Shrink_IsInvalid_AndListsScaleDown()
    {
        var d = Assert.Single(Run("public partial class P { }", ("P.dvml", "<DvProcess fit=\"shrink\"/>")).Ids("DVML020"));
        Assert.Contains("fit='shrink'", d.GetMessage(Invariant));
        Assert.Contains("scale-down", d.GetMessage(Invariant));
        Assert.Empty(Run("public partial class P { }", ("P.dvml", "<DvProcess fit=\"scale-down\"/>")).Ids("DVML020"));
    }

    // 3단계 R10 — 상자가 아닌 하위 요소는 공통 속성을 받지 않는다(id 와 자기 정의에 적은 것만)
    [Fact]
    public void Attribute_CommonOnChildTags_Warns()
    {
        Assert.Equal(Devinno.UI.Markup.TagSchema.ChildTags.OrderBy(n => n, StringComparer.Ordinal), AttributeCheck.ChildTags.OrderBy(n => n, StringComparer.Ordinal));
        var d = Attrs("""
            <DvStack>
              <DvSelect><DvOption id="o1" value="a" style="color:red"/></DvSelect>
              <DvDataGrid><DvColumn field="A" width="80" onclick="Go" class="x"/><DvSummaryRow fn="sum" visible="false"/></DvDataGrid>
              <DvTrend><DvTrendChannel name="a" grow="1"/></DvTrend>
              <DvProcess><DvPump name="P" style="opacity:0.5"><DvMotor visible="false" hold="100"/></DvPump><DvLine from="P.in" to="P.out" visible="false" disabled="true"/></DvProcess>
              <DvTabs><DvTab value="a" style="padding:8" class="c"/></DvTabs>
            </DvStack>
            """);
        Assert.Equal(["DvOption style", "DvColumn class", "DvSummaryRow visible", "DvTrendChannel grow", "DvMotor hold", "DvLine disabled"],
            d.Select(x => System.Text.RegularExpressions.Regex.Match(x.GetMessage(Invariant), @"^(\S+) has no attribute '([^']+)'")).Select(m => $"{m.Groups[1]} {m.Groups[2]}"));
    }

    // 남은 2단계 구멍 — DvScreen 은 검사한다. 틀(DvPageFrame·DvDialogFrame = 내장 컴포넌트)은 앱이 C# 으로 바꿀 수 있어(생성기는 모른다)
    // 없는 속성은 알리지 않고 공통 속성 값만 본다(리뷰 I3 — 옛: titel·gap 을 DVML019 로 알림)
    [Fact]
    public void Attribute_ScreenChecked_FramesOnlyCommonValues()
    {
        var d = Attrs("""<DvScreen vm="MyApp.P" padding="8"><DvPageFrame title="T" subtitle="S" titel="x" class="c"/><DvDialogFrame title="D" gap="md" style="width:300"/></DvScreen>""");
        Assert.Equal(["DvScreen padding"],
            d.Select(x => System.Text.RegularExpressions.Regex.Match(x.GetMessage(Invariant), @"^(\S+) has no attribute '([^']+)'")).Select(m => $"{m.Groups[1]} {m.Groups[2]}"));
        var v = Values("""<DvScreen><DvPageFrame title="T" icon="fa-x" visible="abc"/><DvDialogFrame title="D" grow="many"/></DvScreen>""");
        Assert.Equal(2, v.Length);
        Assert.Contains(v, x => x.GetMessage(Invariant).Contains("visible"));
        Assert.Contains(v, x => x.GetMessage(Invariant).Contains("grow"));
        // 같은 파일이 틀을 자기 컴포넌트로 바꾸면 그 인스턴스는 건너뛴다
        Assert.Empty(Attrs("""<DvScreen><DvComponent name="DvPageFrame"><DvParam name="title"/><DvParam name="icon"/><DvStack><DvSlot/></DvStack></DvComponent><DvPageFrame title="T" icon="fa-x"/></DvScreen>"""));
    }

    // 리뷰 I3 — 틀을 다른 dvml 의 DvComponent 로 바꿔도 건너뛴다(전: 파일마다 모아 다른 파일의 바꾸기를 몰랐다)
    [Fact]
    public void Attribute_FrameReplacedInAnotherFile_IsSkipped()
    {
        var r = Run("public partial class P { }",
            ("Frames.dvml", """<DvStack><DvComponent name="DvPageFrame"><DvParam name="title"/><DvParam name="visible"/><DvStack><DvSlot/></DvStack></DvComponent></DvStack>"""),
            ("P.dvml", """<DvPageFrame title="T" visible="abc" icon="fa-x"><DvLabel text="a"/></DvPageFrame>"""));
        Assert.Empty(r.Ids("DVML019"));
        Assert.Empty(r.Ids("DVML020"));
    }

    // 3단계 D4 — 내장 태그 이름의 컴포넌트는 빌드 오류(DVML021). 내장 컴포넌트(DvCard·DvPageFrame…)를 바꾸는 것은 된다
    [Fact]
    public void Component_BuiltInTagName_IsAnError()
    {
        var r = Run("public partial class P { }", ("P.dvml", """
            <DvScreen>
              <DvComponent name="DvGauge"><DvParam name="v"/><DvLabel text="{v}"/></DvComponent>
              <DvComponent name="DvCard"><DvParam name="title"/><DvLabel text="{title}"/></DvComponent>
              <DvComponent name="DvPageFrame"><DvParam name="title"/><DvLabel text="{title}"/></DvComponent>
              <DvComponent name="Tile"><DvLabel text="t"/></DvComponent>
            </DvScreen>
            """));
        var d = Assert.Single(r.Ids("DVML021"));
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Contains("DvGauge", d.GetMessage(Invariant));
        Assert.Contains("built-in", d.GetMessage(Invariant));
        Assert.Contains("내장", d.GetMessage(new System.Globalization.CultureInfo("ko-KR")));
        Assert.Equal(Devinno.UI.Markup.TagSchema.ComponentTags.OrderBy(n => n, StringComparer.Ordinal), AttributeCheck.ComponentTags.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Attribute_RootOnlyNames_WarnElsewhere()
    {
        var d = Assert.Single(Attrs("<DvStack><DvRow vm=\"X\"/></DvStack>"));
        Assert.Contains("vm", d.GetMessage(Invariant));
    }

    // ── 값 검사(DVML020) — 참/거짓·숫자 속성의 리터럴 값이 그 종류가 아님 ──

    // 생성기의 종류 목록도 라이브러리 정의(TagSchema)의 복사본 — Bool·Number 이름이 태그마다 같아야 한다
    [Fact]
    public void AttributeKinds_MatchLibrary()
    {
        static string[] Names(IEnumerable<Devinno.UI.Markup.AttrDef> defs, Devinno.UI.Markup.AttrKind kind) =>
            defs.Where(a => a.Kind == kind).Select(a => a.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        static string[] Sorted(string[] names) => names.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(Names(Devinno.UI.Markup.TagSchema.Common, Devinno.UI.Markup.AttrKind.Bool), Sorted(AttributeCheck.CommonBool));
        Assert.Equal(Names(Devinno.UI.Markup.TagSchema.Common, Devinno.UI.Markup.AttrKind.Number), Sorted(AttributeCheck.CommonNumber));
        foreach (var (kind, table) in new[] { (Devinno.UI.Markup.AttrKind.Bool, AttributeCheck.Bool), (Devinno.UI.Markup.AttrKind.Number, AttributeCheck.Number) })
        {
            foreach (var (tag, defs) in Devinno.UI.Markup.TagSchema.Tags)
                Assert.True(Names(defs, kind).SequenceEqual(table.TryGetValue(tag, out var n) ? Sorted(n) : []), $"{kind} {tag}");
            Assert.All(table.Keys, tag => Assert.True(Devinno.UI.Markup.TagSchema.Tags.ContainsKey(tag), tag));
        }

        // 목록 값(이름과 값 목록)·색
        static string Enums(IEnumerable<Devinno.UI.Markup.AttrDef> defs) =>
            string.Join("; ", defs.Where(a => a.Kind == Devinno.UI.Markup.AttrKind.Enum).OrderBy(a => a.Name, StringComparer.Ordinal)
                .Select(a => a.Name + "=" + string.Join(",", a.Values ?? [])));
        static string Copy(IReadOnlyDictionary<string, string[]>? table) =>
            table is null ? "" : string.Join("; ", table.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + string.Join(",", p.Value)));
        Assert.Equal(Enums(Devinno.UI.Markup.TagSchema.Common), Copy(AttributeCheck.CommonEnum));
        foreach (var (tag, defs) in Devinno.UI.Markup.TagSchema.Tags)
        {
            Assert.True(Enums(defs) == Copy(AttributeCheck.Enum.TryGetValue(tag, out var e) ? e : null), $"Enum {tag}");
            Assert.True(Names(defs, Devinno.UI.Markup.AttrKind.Color).SequenceEqual(AttributeCheck.Color.TryGetValue(tag, out var c) ? Sorted(c) : []), $"Color {tag}");
        }
        Assert.Empty(Names(Devinno.UI.Markup.TagSchema.Common, Devinno.UI.Markup.AttrKind.Color));
        Assert.All(AttributeCheck.Enum.Keys.Concat(AttributeCheck.Color.Keys), tag => Assert.True(Devinno.UI.Markup.TagSchema.Tags.ContainsKey(tag), tag));
    }

    private static Diagnostic[] Values(string dvml) =>
        Run("public partial class P { }", ("P.dvml", dvml)).Ids("DVML020");

    [Fact]
    public void Value_NotBool_Warns()
    {
        var d = Assert.Single(Values("<DvStack>\n<DvLamp on=\"yes\"/>\n</DvStack>"));
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        var msg = d.GetMessage(Invariant);
        Assert.Contains("DvLamp", msg);
        Assert.Contains("on='yes'", msg);
        Assert.Contains("true/false", msg);
        Assert.Equal(1, d.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Contains("참/거짓", d.GetMessage(new System.Globalization.CultureInfo("ko-KR")));
    }

    [Fact]
    public void Value_NotNumber_Warns()
    {
        var d = Assert.Single(Values("<DvStack><DvGauge max=\"abc\"/></DvStack>"));
        Assert.Contains("DvGauge max='abc'", d.GetMessage(Invariant));
        Assert.Contains("숫자가 아닙니다", d.GetMessage(new System.Globalization.CultureInfo("ko-KR")));
        Assert.Single(Values("<DvStack><DvLabel text=\"a\" visible=\"on\"/></DvStack>")); // 공통 속성
        Assert.Single(Values("<DvStack><DvLamp blink-interval=\"1,5\"/></DvStack>")); // 문화권 소수점 아님
    }

    [Fact]
    public void Value_Valid_NoWarnings()
    {
        Assert.Empty(Values("""
            <DvStack vm="MyApp.P" hold="500" grow="1">
              <DvLamp on="true" blink=" FALSE " blink-interval="1.5e3"/>
              <DvLamp on="1" blink="-1"/>
              <DvLamp on="" blink="{Blink}"/>
              <DvLabel text="a" visible="False" disabled="0"/>
              <DvGauge value="{V}" max="1.5" min="-10"/>
              <DvTrend smooth="" window=" 60 "/>
              <DvProcess><DvVessel name="T1" width="80" level="50"/></DvProcess>
              <DvLine flow="{F}"/>
            </DvStack>
            """));
    }

    // 3단계 R3 — 목록 값: 정의된 값(대소문자·앞뒤 공백 무시)이 아니면 DVML020. 이미 DVML011·DVML017 이 보는 속성은 그쪽만
    [Fact]
    public void Value_NotEnum_Warns()
    {
        var d = Assert.Single(Values("<DvStack><DvProgress direction=\"sideways\"/></DvStack>"));
        var msg = d.GetMessage(Invariant);
        Assert.Contains("DvProgress direction='sideways'", msg);
        Assert.Contains("right, left, up, down", msg);
        Assert.Contains("중 하나가 아닙니다", d.GetMessage(new System.Globalization.CultureInfo("ko-KR")));
        Assert.Single(Values("<DvStack><DvButton variant=\"foo\" text=\"a\"/></DvStack>"));
        Assert.Single(Values("<DvStack><DvLabel text=\"a\" content-align=\"center\"/></DvStack>")); // 공통 속성
        Assert.Single(Values("<DvStack><DvProcess><DvPump name=\"P\" flip=\"x\"/></DvProcess></DvStack>"));
    }

    // 3.4 — tone·DvLamp state 값은 앱 테마 토큰(tone-{v}·state-{v})에 달려 생성기가 알 수 없다 → DVML020 이 보지 않는다
    [Fact]
    public void Value_ToneAndState_NotChecked()
    {
        Assert.Empty(Values("<DvStack><DvRing tone=\"ok\"/><DvBarChart tone=\"brand\"/><DvLamp state=\"idle\"/></DvStack>"));
    }

    [Fact]
    public void Value_EnumCheckedElsewhere_NotTwice()
    {
        var all = Run("public partial class P { }", ("P.dvml", "<DvStack><DvSlider direction=\"sideways\"/><DvProcess look=\"fancy\"/></DvStack>"));
        Assert.Empty(all.Ids("DVML020"));
        Assert.Single(all.Ids("DVML017"));
        Assert.Single(all.Ids("DVML011"));
        // 같은 규칙: 대소문자·앞뒤 공백 무시
        var ok = Run("public partial class P { }", ("P.dvml", "<DvStack><DvSlider direction=\" UP \"/><DvProcess look=\"Rich\"/></DvStack>"));
        Assert.Empty(ok.Ids("DVML017"));
        Assert.Empty(ok.Ids("DVML011"));
    }

    // 공정도 숫자 속성(DvSignal thickness·DvInstrument x/y)은 DVML011 이 본다 — DVML020 은 같은 값을 다시 알리지 않는다
    [Fact]
    public void Value_ProcessNumberCheckedElsewhere_NotTwice()
    {
        var all = Run("public partial class P { }", ("P.dvml",
            "<DvProcess><DvPump name=\"P\"/><DvInstrument name=\"I\" on=\"P\" x=\"a\" y=\"b\"/><DvSignal from=\"I\" to=\"P\" thickness=\"thick\"/></DvProcess>"));
        Assert.Empty(all.Ids("DVML020"));
        Assert.Equal(3, all.Ids("DVML011").Length);
        // 같은 숫자 규칙: NaN·무한은 숫자가 아니다, 빈 값 = 없음
        var odd = Run("public partial class P { }", ("P.dvml",
            "<DvProcess><DvPump name=\"P\"/><DvInstrument name=\"I\" on=\"P\" x=\"NaN\" y=\"\"/><DvSignal from=\"I\" to=\"P\" thickness=\"Infinity\"/></DvProcess>"));
        Assert.Empty(odd.Ids("DVML020"));
        Assert.Equal(2, odd.Ids("DVML011").Length);
    }

    // 3단계 R2 — 색: #RGB·#RRGGBB·#RRGGBBAA·transparent·token(이름) 이 아니면 DVML020(색 이름 red 는 실행 때도 못 읽는다)
    [Fact]
    public void Value_NotColor_Warns()
    {
        var d = Assert.Single(Values("<DvStack><DvLamp color=\"red\"/></DvStack>"));
        Assert.Contains("DvLamp color='red'", d.GetMessage(Invariant));
        Assert.Contains("색이 아닙니다", d.GetMessage(new System.Globalization.CultureInfo("ko-KR")));
        Assert.Single(Values("<DvStack><DvDataGrid header-color=\"#12345\"/></DvStack>"));
        Assert.Single(Values("<DvStack><DvDataGrid><DvColumn field=\"A\" on-color=\"token(a\"/></DvDataGrid></DvStack>"));
        // DvSignal color 는 DVML011 이 같은 규칙으로 본다(#RGBA 4자리는 실행 때 못 읽음)
        var sig = Run("public partial class P { }", ("P.dvml", "<DvProcess><DvPump name=\"P\"/><DvInstrument name=\"I\" on=\"P\"/><DvSignal from=\"I\" to=\"P\" color=\"#f00f\"/></DvProcess>"));
        Assert.Single(sig.Ids("DVML011"));
        Assert.Empty(sig.Ids("DVML020"));
    }

    [Fact]
    public void Value_EnumAndColor_Valid_NoWarnings()
    {
        Assert.Empty(Values("""
            <DvStack align="Center" justify="between">
              <DvButton variant=" PRIMARY " size="sm" text="a"/>
              <DvButton variant="{Kind}" text="b"/>
              <DvLabel text="a" content-align="middle-left"/>
              <DvLamp color="token(state-run)" state="Run"/>
              <DvLamp color="#00ff0080" state=""/>
              <DvTrend><DvTrendChannel color="var(--tone-info)"/></DvTrend>
              <DvDataGrid header-color="transparent" row-color="#ffffff"><DvColumn field="A" align="right" cell-type="Check" sort="desc"/><DvSummaryRow fn="sum"/></DvDataGrid>
              <DvProcess fit="scale-down"><DvPump name="P" flip="vh" rotate="90"><DvAttach side="left"><DvLabel text="a"/></DvAttach></DvPump><DvLine from="P.in" to="P.out" arrow="end"/></DvProcess>
            </DvStack>
            """));
    }

    [Fact]
    public void Value_Skipped_OnUnknownTags()
    {
        // 사용자 컴포넌트 인스턴스(파라미터) · 컴포넌트 템플릿의 파라미터 값 · DvSymbol 정의 안
        Assert.Empty(Values("""
            <DvStack>
              <DvComponent name="Tile"><DvParam name="on"/><DvLamp on="{on}"/></DvComponent>
              <Tile on="yes" visible="maybe"/>
              <DvSymbol name="S2" width="40" height="60"><DvLamp on="yes"/></DvSymbol>
            </DvStack>
            """));
    }

    // ── 진단 문구 언어 — 기본 영어, 한국어 리소스(ko) ──

    [Fact]
    public void Messages_English_ByDefault_Korean_ForKo()
    {
        var d = Assert.Single(Handlers("<DvButton onclick=\"Goo\"/>"));
        Assert.Contains("is not a public instance method", d.GetMessage(Invariant));
        Assert.Contains("public 인스턴스 메서드가 아닙니다", d.GetMessage(new System.Globalization.CultureInfo("ko-KR")));
        Assert.Equal("Handler method not found", d.Descriptor.Title.ToString(Invariant));

        // 인자로 넘긴 문구 조각(공정도 포트 설명)도 같은 언어로
        var p = Process(Plant + "<DvSignal from=\"FIC\" to=\"V1.in\"/>").Single();
        Assert.Contains("without ports", p.GetMessage(Invariant));
        Assert.Contains("포트 없이", p.GetMessage(new System.Globalization.CultureInfo("ko-KR")));
    }
}