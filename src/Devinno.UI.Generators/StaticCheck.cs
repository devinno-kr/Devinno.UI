using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// DVML023 — static 바인딩({Static A.ABC})을 컴파일 정보로 확인한다. 실행 때 찾기(StaticBinding)와 같은 규칙:
/// 짧은 형식 이름은 프로젝트(화면 VM 의 어셈블리) 형식에서, 그 밖은 네임스페이스를 붙인 이름(가장 긴 것부터, 참조 어셈블리 포함).
/// 알리는 것: 형식·속성 없음, 그 속성을 가진 프로젝트 형식이 둘 이상.
/// 실행 때는 앱(entry) 어셈블리에서도 짧은 이름을 찾는다 — 화면이 라이브러리 프로젝트에 있고 형식이 실행 파일 쪽에 있으면 이 경고는 무시해도 된다.
/// </summary>
internal static class StaticCheck
{
    public static void Check(SourceProductionContext spc, Compilation compilation, IReadOnlyList<DvmlModel> files)
    {
        foreach (var f in files)
        {
            foreach (var (expr, path, at) in f.StaticUses)
            {
                var segs = path.Split('.').Select(s => s.Trim()).ToArray();
                if (segs.Length < 2 || segs.Any(s => s.Length == 0))
                    continue; // 실행 경고가 모양을 알린다

                var named = compilation.GetSymbolsWithName(segs[0], SymbolFilter.Type).OfType<INamedTypeSymbol>()
                    .Where(t => !t.IsGenericType).ToList();
                var hits = named.Where(t => HasStatic(t, segs[1])).ToList();
                if (hits.Count > 1)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.StaticAmbiguous, at, expr, segs[0],
                        string.Join(", ", hits.Take(3).Select(t => t.ToDisplayString())), hits[0].ToDisplayString() + "." + string.Join(".", segs.Skip(1))));
                    continue;
                }
                if (hits.Count == 1)
                    continue;

                if (Qualified(compilation, segs) is { } q)
                {
                    if (!q.Found)
                        spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.StaticNoProperty, at, expr, q.Type, q.Property));
                    continue;
                }

                if (named.Count > 0)
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.StaticNoProperty, at, expr, named[0].ToDisplayString(), segs[1]));
                else
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.StaticNotFound, at, expr, segs[0]));
            }
        }
    }

    // 네임스페이스를 붙인 이름(MyApp.Data.A.ABC) — 가장 긴 형식 이름부터. 형식이 없으면 null, 있으면 그 속성이 있는지
    private static (bool Found, string Type, string Property)? Qualified(Compilation compilation, string[] segs)
    {
        for (var last = segs.Length - 2; last >= 1; last--)
        {
            var full = string.Join(".", segs.Take(last + 1));
            if (compilation.GetTypeByMetadataName(full) is { } t)
                return (HasStatic(t, segs[last + 1]), full, segs[last + 1]);
        }
        return null;
    }

    // 읽을 수 있는 public static 속성(상속 포함) — 실행 때 BindingFlags.Public | Static | FlattenHierarchy 와 같게
    private static bool HasStatic(INamedTypeSymbol type, string name)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.GetMembers(name).OfType<IPropertySymbol>().Any(p =>
                    p.IsStatic && p.GetMethod is { DeclaredAccessibility: Accessibility.Public }))
                return true;
        }
        return false;
    }
}
