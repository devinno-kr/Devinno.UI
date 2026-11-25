using System.Reflection;
using Devinno.UI.Interaction;

namespace Devinno.UI.Binding;

// {Static A.ABC} · {Bind Static A.ABC} — 형식의 public static 속성을 읽고 쓴다(뒤 단계는 인스턴스 규칙).
// 형식 찾기: 첫 단계가 짧은 이름(A)이면 화면 VM 의 어셈블리 → 앱(entry) 어셈블리 순서, 거기서 그 static 속성을 가진 형식이 하나여야 한다
// (불러온 어셈블리 전체를 훑지 않는다 — 수백 ms 걸린다). 그 밖의 형식은 네임스페이스를 붙인 이름(System.DateTime.Now) — 가장 긴 형식 이름부터,
// 어셈블리마다 이름으로 바로 찾는다. 경로(어셈블리, 경로)마다 한 번 찾아 캐시한다.
internal static class StaticBinding
{
    internal const string Prefix = "Static ";

    private sealed record Route(Type Type, PropertyInfo Property, string? Rest);

    private static readonly Dictionary<(Assembly?, string), Route?> Routes = new();
    private static readonly object Gate = new();

    /// <summary>Whether a binding path (braces, <c>Bind</c> and format removed) is a static path (<c>Static A.ABC</c>).</summary>
    public static bool IsStatic(string path) => path.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Reads a static path. A getter exception propagates (the caller reports it like an instance getter).</summary>
    public static object? Read(object? ctx, string path)
    {
        if (RouteOf(ctx, path) is not { } r)
            return null;
        var value = r.Property.GetValue(null);
        return r.Rest is null || value is null ? value : BindingResolver.ReadPath(value, r.Rest);
    }

    /// <summary>
    /// Writes a static path. The told object is the screen VM (unwrapped from a repeated item); without one, the type. <see langword="null"/> when nothing was written.
    /// Conversion and exceptions as <see cref="BindingResolver.SetValue"/>.
    /// </summary>
    public static (object Owner, string Path, object? Value)? Write(object? ctx, string path, object? value)
    {
        if (RouteOf(ctx, path) is not { } r)
            return null;
        object? stored;
        if (r.Rest is null)
        {
            if (r.Property.SetMethod is not { IsPublic: true })
            {
                Warnings.Warn($"Devinno.UI: static binding '{{{path}}}': {r.Type.Name}.{r.Property.Name} has no public setter; the value is not written",
                    key: "\u001fstatic-readonly\u001f" + path);
                return null;
            }
            stored = BindingResolver.ConvertFor(r.Property.PropertyType, value);
            r.Property.SetValue(null, stored);
        }
        else
        {
            if (BindingResolver.Write(r.Property.GetValue(null), r.Rest, value) is not { } w)
                return null;
            stored = w.Value;
        }
        return (RootVm(ctx) ?? r.Type, Normalize(path), stored);
    }

    /// <summary>The declared type at a static path, or <see langword="null"/>.</summary>
    public static Type? PropertyType(object? ctx, string path)
    {
        if (RouteOf(ctx, path) is not { } r)
            return null;
        return r.Rest is null ? r.Property.PropertyType : BindingResolver.ResolvePropertyType(r.Property.GetValue(null), r.Rest);
    }

    private static string Normalize(string path) => Prefix + path[Prefix.Length..].Trim();

    // 화면 VM — 반복 항목은 바깥으로, 컴포넌트 파라미터 범위는 VM 이 아니다(null)
    private static object? RootVm(object? ctx) => ctx switch
    {
        ItemScope item => RootVm(item.Outer),
        ComponentScope => null,
        IReadOnlyDictionary<string, object?> => null,
        _ => ctx,
    };

    private static Route? RouteOf(object? ctx, string path)
    {
        var asm = RootVm(ctx)?.GetType().Assembly;
        var body = path[Prefix.Length..].Trim();
        lock (Gate)
        {
            if (Routes.TryGetValue((asm, body), out var known))
                return known;
        }
        var route = Find(asm, body); // 찾기(어셈블리 훑기)는 잠금 밖 — 같은 경로를 두 스레드가 찾아도 결과는 같다
        lock (Gate)
            Routes[(asm, body)] = route;
        return route;
    }

    private static Route? Find(Assembly? asm, string body)
    {
        var segs = body.Split('.');
        if (segs.Length < 2 || segs.Any(s => s.Trim().Length == 0))
        {
            Warn(body, "write a type and a static property, e.g. {Static Settings.Mode}");
            return null;
        }
        for (var i = 0; i < segs.Length; i++)
            segs[i] = segs[i].Trim();

        // 짧은 이름 — 화면 어셈블리에서 하나면 그것, 없으면 앱 어셈블리에서 하나
        var hits = Matches(asm is null ? [] : [asm], segs[0], segs[1]);
        if (hits.Count == 0 && Assembly.GetEntryAssembly() is { } entry && entry != asm)
            hits = Matches([entry], segs[0], segs[1]);
        if (hits.Count > 1)
        {
            Warn(body, $"'{segs[0]}' is ambiguous ({string.Join(", ", hits.Take(3).Select(t => t.FullName))}); write the full name, e.g. {{Static {hits[0].FullName}.{string.Join('.', segs[1..])}}}");
            return null;
        }
        if (hits.Count == 1)
            return RouteFor(hits[0], segs, 1);

        // 네임스페이스를 붙인 이름 — 가장 긴 형식 이름부터
        for (var last = segs.Length - 2; last >= 1; last--)
        {
            var full = string.Join('.', segs[..(last + 1)]);
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetType(full, throwOnError: false) is { } t && StaticProperty(t, segs[last + 1]) is not null)
                    return RouteFor(t, segs, last + 1);
            }
        }

        Warn(body, $"no type '{segs[0]}' with a public static property '{segs[1]}' in the screen's or the app's assembly; a type of another assembly needs its namespace, e.g. {{Static System.DateTime.Now}}");
        return null;
    }

    private static Route RouteFor(Type t, string[] segs, int propIndex)
        => new(t, StaticProperty(t, segs[propIndex])!, propIndex + 1 < segs.Length ? string.Join('.', segs[(propIndex + 1)..]) : null);

    private static List<Type> Matches(IEnumerable<Assembly> assemblies, string typeName, string property)
    {
        var list = new List<Type>();
        foreach (var a in assemblies)
        {
            foreach (var t in TypesOf(a))
            {
                if (t.Name == typeName && !t.IsGenericTypeDefinition && StaticProperty(t, property) is not null && !list.Contains(t))
                    list.Add(t);
            }
        }
        return list;
    }

    private static IEnumerable<Type> TypesOf(Assembly a)
    {
        try
        {
            return a.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
        catch (Exception)
        {
            return [];
        }
    }

    // 읽을 수 있는 public static 속성(상속 포함)
    private static PropertyInfo? StaticProperty(Type t, string name)
    {
        try
        {
            return t.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) is { GetMethod.IsPublic: true } p ? p : null;
        }
        catch (AmbiguousMatchException)
        {
            return null;
        }
    }

    private static void Warn(string body, string why)
        => Warnings.Warn($"Devinno.UI: static binding '{{{Prefix}{body}}}': {why}; the value is empty", key: "\u001fstatic\u001f" + body);
}
