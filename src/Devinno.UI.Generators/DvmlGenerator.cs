using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Devinno.UI.Generators;

/// <summary>
/// 루트에 <c>vm="네임스페이스.클래스"</c> 가 있는 <c>*.dvml</c>(AdditionalFiles) 마다 그 VM 의 partial 을 만듭니다:
/// <c>Markup</c> 상수(dvml 원문), id 마다 핸들 필드, <c>IDvControlHost.Attach</c>, <c>partial void OnLoaded()</c>.
/// VM 이 <c>Devinno.UI.App.DvPage</c>·<c>DvBar</c> 를 (직접·간접) 상속하면 <c>MarkupSource</c> 재정의도 만듭니다.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DvmlGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(t => t.Path.EndsWith(".dvml", StringComparison.OrdinalIgnoreCase))
            .Select((t, ct) => DvmlModel.Parse(t.Path, t.GetText(ct)))
            .Collect();

        context.RegisterSourceOutput(files.Combine(context.CompilationProvider), (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static void Execute(SourceProductionContext spc, ImmutableArray<DvmlModel> all, Compilation compilation)
    {
        // 같은 파일이 AdditionalFiles 에 두 번 들어와도(글롭 중복 등) 한 번으로 — 아니면 자기 자신과 겹친다는 DVML007 이 난다.
        var files = all.GroupBy(f => NormalizePath(f.Path), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

        foreach (var f in files)
            foreach (var d in f.Reports)
                spc.ReportDiagnostic(d);

        // 클래스 — 내장 + 프로젝트 dvml 모든 <DvStyle> 이 정의한 것에 없으면
        var defined = new HashSet<string>(StyleCheck.ClassSet, StringComparer.Ordinal);
        foreach (var f in files)
            defined.UnionWith(f.ClassDefs);
        foreach (var f in files)
            foreach (var (name, at) in f.ClassUses)
                if (!defined.Contains(name))
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.StyleUnknownClass, at, name));

        var clickParamComponents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in files)
            clickParamComponents.UnionWith(f.ClickParamComponents);

        foreach (var group in files.Where(f => f.Vm is not null).GroupBy(f => f.Vm!, StringComparer.Ordinal))
        {
            var list = group.ToList();
            if (list.Count > 1)
            {
                foreach (var f in list)
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.VmUsedTwice, f.VmLocation, group.Key));
                continue;
            }

            var file = list[0];
            if (file.HasErrors)
                continue;

            var type = compilation.GetTypeByMetadataName(group.Key);
            if (type is null || type.ContainingType is not null || type.TypeKind != TypeKind.Class || type.IsGenericType)
            {
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.VmNotFound, file.VmLocation, group.Key));
                continue;
            }
            if (!IsPartial(type))
            {
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.VmNotPartial, file.VmLocation, group.Key));
                continue;
            }

            CheckHandlers(spc, type, file, clickParamComponents);
            spc.AddSource(group.Key + ".Dvml.g.cs", Emit(type, file));
        }
    }

    // DVML018 — 실행 때는 핸들러 속성 값 그대로 VM(상속 포함)의 public 인스턴스 메서드를 이름으로 찾고, 없으면 조용히 아무 일도 없다.
    // 이름만 본다(매개변수 규칙은 핸들러마다 달라 여기서 판단하지 않는다).
    private static void CheckHandlers(SourceProductionContext spc, INamedTypeSymbol type, DvmlModel file, HashSet<string> clickParamComponents)
    {
        var methods = new HashSet<string>(StringComparer.Ordinal);
        for (var t = type; t is not null; t = t.BaseType)
            foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
                if (m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic)
                    methods.Add(m.Name);

        foreach (var (attr, value, tag, at) in file.Handlers)
        {
            if (attr == "onclick" && clickParamComponents.Contains(tag))
                continue; // onclick 을 매개변수로 받는 컴포넌트 — 핸들러로 전달되지 않는다
            if (value.IndexOf('{') >= 0)
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.HandlerBinding, at, attr, value));
            else if (SyntaxFacts.IsValidIdentifier(value))
            {
                if (!methods.Contains(value))
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.HandlerNotFound, at, attr, value, type.Name));
            }
            else if (value.IndexOf('(') is > 0 and var paren && SyntaxFacts.IsValidIdentifier(value.Substring(0, paren).Trim()))
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.HandlerCallForm, at, attr, value, value.Substring(0, paren).Trim()));
            else
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.HandlerNotName, at, attr, value));
        }
    }

    private static string NormalizePath(string path)
    {
        try { return System.IO.Path.GetFullPath(path); }
        catch (Exception) { return path; } // 잘못된 경로 문자 등 — 원문 그대로 비교
    }

    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Length > 0 &&
        type.DeclaringSyntaxReferences.All(r =>
            r.GetSyntax() is TypeDeclarationSyntax t && t.Modifiers.Any(SyntaxKind.PartialKeyword));

    // vm 이 Devinno.UI.App.DvPage·DvBar·DvDialog 를 (직접·간접) 상속하는가
    private static bool IsAppScreen(INamedTypeSymbol type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (t.OriginalDefinition.ToDisplayString() is "Devinno.UI.App.DvPage" or "Devinno.UI.App.DvBar" or "Devinno.UI.App.DvDialog")
                return true;
        return false;
    }

    private static string Emit(INamedTypeSymbol type, DvmlModel file)
    {
        const string ns = "global::Devinno.UI.Controls.";
        var sb = new StringBuilder();
        sb.Append("// <auto-generated/>\n");
        sb.Append("#nullable enable\n");
        if (!type.ContainingNamespace.IsGlobalNamespace)
            sb.Append("namespace ").Append(type.ContainingNamespace.ToDisplayString()).Append(";\n");
        sb.Append("partial class ").Append(type.Name).Append(" : ").Append(ns).Append("IDvControlHost\n{\n");
        sb.Append("    public const string Markup = @\"").Append(file.Text.Replace("\"", "\"\"")).Append("\";\n");
        if (IsAppScreen(type))
        {
            sb.Append("    protected override string? MarkupFile => @\"").Append(file.Path.Replace("\"", "\"\"")).Append("\";\n"); // 핫리로드가 감시할 파일
            sb.Append("    protected override string MarkupSource => Markup;\n");
        }
        foreach (var (name, t) in file.Fields)
            sb.Append("    private ").Append(ns).Append(t).Append(' ').Append(name).Append(" = null!;\n");
        sb.Append("    private bool __dvLoaded;\n");
        sb.Append("    partial void OnLoaded();\n");
        sb.Append("    void ").Append(ns).Append("IDvControlHost.Attach(").Append(ns).Append("DvControlLookup c)\n    {\n");
        // this. — id 가 매개변수 이름 c 와 같아도 필드에 들어가도록.
        foreach (var (name, _) in file.Fields)
            sb.Append("        this.").Append(name).Append(" = c.Bind(this.").Append(name).Append(", \"").Append(name).Append("\");\n");
        sb.Append("        if (!__dvLoaded) { __dvLoaded = true; OnLoaded(); }\n");
        sb.Append("    }\n}\n");
        return sb.ToString();
    }
}
