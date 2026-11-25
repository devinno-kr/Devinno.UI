using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Devinno.UI.Interaction;

namespace Devinno.UI.Binding;

/// <summary>
/// Resolver that reads and writes the binding expressions of markup attribute values (<c>{Path}</c>, <c>{Bind Path}</c>, <c>{Data.Path}</c>, <c>{L10n.key}</c>, <c>@key</c>) against the view model.
/// </summary>
/// <remarks>
/// <para>This is an internal engine stage. App code normally does not use it directly; it uses markup bindings and <see cref="DvSurface"/>/<see cref="DvRenderer"/>. Use it from custom widgets or tools that must read and write values by the same rules.</para>
/// <para><b>Path rules</b>: a dot-separated (<c>.</c>) path of public instance properties. A leading <c>Bind </c> (two-way marker) and the <c>Data.</c> prefix are stripped before resolving.
/// If an intermediate value is <see langword="null"/> or a property does not exist, the result is <see langword="null"/> without an exception.
/// If a property getter (view model code) throws, the exception is not propagated; the value is treated as <see langword="null"/>, a Trace error is written, and during layout or drawing it is reported through that screen's <see cref="DvSurface.HandlerError"/> (<c>Kind</c> = <c>"getter"</c>).
/// In interpolation (<see cref="ResolveInline"/>), if a value's <c>ToString</c> throws, that spot becomes an empty string (<c>"format"</c>).
/// <b>Display format</b>: <c>{Path:format}</c> (cut at the first <c>:</c>) formats the value when it becomes text — interpolation and text attributes — with the .NET format string and the invariant culture,
/// by the same rule as the widget <c>format</c> attribute. Reading the value (<see cref="Resolve"/>, value attributes, two-way bindings) ignores the format. In a repeated-item context, <c>Item</c>, <c>Item.X</c> and <c>Index</c> are available;
/// inside a component template, parameter names are the first segment of the path, and a binding that is exactly a parameter (<c>{value}</c>) shows its text with the format the instance gave it (<c>value="{Level:0.0}"</c>).</para>
/// <para><b>Static properties</b>: <c>{Static A.ABC}</c> (two-way <c>{Bind Static A.ABC}</c>, formats and interpolation as usual) reads the public static property <c>ABC</c> of the type <c>A</c>; further segments
/// (<c>{Static A.Mgr.Path}</c>) follow the instance rules. A short type name (<c>A</c>) is looked up in the screen VM's assembly, then in the app's (entry) assembly, and must be the only such type with that static property;
/// a type of another assembly, or an ambiguous one, is written with its namespace (<c>{Static System.DateTime.Now}</c>, <c>{Static MyApp.Data.A.ABC}</c>). A type or property that is not found, an ambiguous name and a write to a property without a public setter give a warning (once) and an empty value.
/// A value written through a static binding is reported to the screen VM (<see cref="IDvValueCommitted"/>) with the path <c>Static A.ABC</c>.</para>
/// <para><b>Localization</b>: <c>{L10n.key}</c> and <c>@key</c> are replaced with translated strings only during layout (while the screen's localization table is set). Called outside of that, <c>{L10n.key}</c> is <see langword="null"/> and <c>@key</c> is returned literally.</para>
/// <para>Property read functions are cached per type and path; the cache is thread-safe.</para>
/// </remarks>
public static class BindingResolver
{
    private static readonly Regex InlineBindingRegex = new(@"\{[^{}]+\}", RegexOptions.Compiled);
    private static readonly Regex AtKeyRegex = new(@"^@[A-Za-z_][A-Za-z0-9_.]*$", RegexOptions.Compiled);
    private static readonly Dictionary<(Type, string), Func<object, object?>> Cache = new();
    private static readonly object Gate = new();
    private const string L10nPrefix = "L10n.";

    /// <summary>Localization table of the screen being laid out (ambient). Set by LayoutEngine only during Layout (single UI thread).</summary>
    [ThreadStatic] internal static Localization.DvStrings? CurrentStrings;

    /// <summary>
    /// Checks whether the whole value is a single binding (<c>{X}</c>, <c>{Bind X}</c>). Returns <see langword="false"/> if it is mixed with text, as in <c>"{A} 그리고 {B}"</c> ("{A} and {B}"), or contains several bindings.
    /// </summary>
    /// <param name="v">Attribute value. <see langword="false"/> if <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value consists of a single binding only.</returns>
    public static bool IsBinding(string? v)
        => v is not null && v.Length >= 2 && v[0] == '{' && v[^1] == '}' &&
           v.IndexOfAny(new[] { '{', '}' }, 1, v.Length - 2) < 0;

    /// <summary>
    /// Checks whether the value is a two-way binding (<c>{Bind 경로}</c>, i.e. {Bind path}). Widget interaction results are written to the view model only for two-way bindings.
    /// </summary>
    /// <param name="v">Attribute value. <see langword="false"/> if <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if it starts with <c>{</c>, ends with <c>}</c>, and the inside starts with <c>Bind </c>.</returns>
    public static bool IsTwoWay(string? v)
    {
        if (v is null || v.Length < 2 || v[0] != '{' || v[^1] != '}')
            return false;

        return v.AsSpan(1, v.Length - 2).TrimStart().StartsWith("Bind ");
    }

    /// <summary>
    /// Strips the braces, the <c>Bind </c>/<c>Data.</c> prefixes and a display format (<c>:0.0</c>) from a binding expression and returns only the property path. Example: <c>{Bind Data.Pump.Run}</c> → <c>Pump.Run</c>, <c>{Temp:0.0}</c> → <c>Temp</c>.
    /// </summary>
    /// <param name="expr">Binding expression including braces (at least 2 characters). The format is not checked, so pass a value verified with <see cref="IsBinding"/> or similar.</param>
    /// <returns>The property path.</returns>
    public static string ExtractPath(string expr) => Split(expr).Path;

    // {[Bind ][Data.]경로[:형식]} — 형식은 첫 ':' 에서 자른다(형식 안의 ':' — HH:mm — 는 형식 쪽). 경로·형식 앞뒤 공백 무시. 다국어 키는 형식 없음, 빈 형식 = 없음
    private static (string Path, string? Format, bool TwoWay) Split(string expr)
    {
        var inner = expr[1..^1].Trim();
        var twoWay = inner.StartsWith("Bind ", StringComparison.Ordinal);
        if (twoWay)
            inner = inner[5..].Trim();
        string? format = null;
        if (!inner.StartsWith(L10nPrefix, StringComparison.Ordinal) && inner.IndexOf(':') is var colon and >= 0)
        {
            format = inner[(colon + 1)..].Trim() is { Length: > 0 } f ? f : null;
            inner = inner[..colon].TrimEnd();
        }
        if (inner.StartsWith("Data.", StringComparison.Ordinal))
            inner = inner[5..];
        return (inner, format, twoWay);
    }

    /// <summary>
    /// The display format of a single binding (<c>{Temp:0.0}</c> → <c>0.0</c>), or <see langword="null"/> when it has none or <paramref name="expr"/> is not a single binding.
    /// A two-way binding takes no format: <c>{Bind X:0.0}</c> gives <see langword="null"/> with a warning (the value is written to <c>X</c> as usual).
    /// </summary>
    internal static string? FormatOf(string? expr)
    {
        if (expr is null || !IsBinding(expr))
            return null;
        var (_, format, twoWay) = Split(expr);
        if (format is null || !twoWay)
            return format;
        Warnings.Warn($"Devinno.UI: binding '{expr}' is two-way; a two-way binding takes no format, so ':{format}' is ignored (use the widget's format attribute)", key: "\u001fbind-format\u001f" + expr);
        return null;
    }

    /// <summary>
    /// The expression a binding becomes text with. A component template binding that is exactly a parameter (<c>{value}</c>, no format of its own)
    /// takes the format the instance gave that parameter (<c>value="{Level:0.0}"</c>), so it is returned as <c>{value:0.0}</c>; anything else is returned as-is.
    /// The parameter keeps the value itself, so value attributes in the template (<c>&lt;DvGauge value="{value}"/&gt;</c>) still read the number.
    /// </summary>
    internal static string? TextExpr(object? ctx, string? expr)
    {
        if (expr is null || !IsBinding(expr) || IsTwoWay(expr) || IsL10n(expr))
            return expr;
        var (path, format, _) = Split(expr);
        return format is null && ParamFormat(ctx, path) is { } inherited ? "{" + path + ":" + inherited + "}" : expr;
    }

    // 경로가 컴포넌트 파라미터 그 자체일 때 인스턴스가 준 형식. 반복 항목 안이면 Item·Index 가 아닌 경로를 바깥 문맥에서 찾는다(ResolvePath 와 같은 순서)
    private static string? ParamFormat(object? ctx, string path) => ctx switch
    {
        ItemScope item when path is "Item" or "Index" || path.StartsWith("Item.", StringComparison.Ordinal) => null,
        ItemScope item => ParamFormat(item.Outer, path),
        ComponentScope scope => scope.FormatOf(path),
        _ => null,
    };

    /// <summary>
    /// Resolves a markup attribute value. A single binding returns its value (type preserved), text mixed with bindings returns string interpolation (<see cref="ResolveInline"/>),
    /// <c>@key</c> returns the localized string (<c>@@x</c> is the literal <c>@x</c>), and anything else is returned literally.
    /// </summary>
    /// <param name="ctx">Binding context (view model, repeated-item scope, component parameter scope). If <see langword="null"/>, bindings resolve to <see langword="null"/>.</param>
    /// <param name="raw">Attribute value. Returns <see langword="null"/> if <see langword="null"/>.</param>
    /// <returns>The resolved value.</returns>
    /// <remarks><c>@key</c> resolution works only during layout; outside of it, the value is returned literally.</remarks>
    public static object? ResolveValueOrLiteral(object? ctx, string? raw)
    {
        if (raw is null)
            return null;
        if (IsBinding(raw))
            return Resolve(ctx, raw);

        if (CurrentStrings is { } strings && raw.Length > 1 && raw[0] == '@')
        {
            if (raw[1] == '@')
                return raw[1..];
            if (AtKeyRegex.IsMatch(raw))
                return strings.Get(raw[1..]);
        }

        return raw.Contains('{') && InlineBindingRegex.IsMatch(raw) ? ResolveInline(ctx, raw) : raw;
    }

    /// <summary>
    /// Replaces every <c>{…}</c> binding in the text with the string of its value (an empty string if the value is <see langword="null"/>). Example: <c>"온도 {Temp} ℃"</c> ("Temperature {Temp} ℃").
    /// Values are written with the invariant culture; a binding may carry a display format after the first <c>:</c>, as in C# string interpolation: <c>"{Temp:0.0} ℃"</c> → <c>24.6 ℃</c>, <c>{At:HH:mm}</c>.
    /// </summary>
    /// <param name="ctx">Binding context. If <see langword="null"/>, only <c>{L10n.…}</c> is replaced and other bindings are left as written.</param>
    /// <param name="raw">String to resolve. Returned as-is if <see langword="null"/> or whitespace.</param>
    /// <returns>The interpolated string.</returns>
    public static string? ResolveInline(object? ctx, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || (ctx is null && CurrentStrings is null))
            return raw;

        // VM 이 없을 때 일반 바인딩은 원문 유지(기존 동작), 다국어 키만 치환
        return InlineBindingRegex.Replace(raw, m =>
            ctx is null && !IsL10n(m.Value) ? m.Value : ToText(Resolve(ctx, m.Value), TextExpr(ctx, m.Value)) ?? "");
    }

    /// <summary>String form of the <see cref="ResolveValueOrLiteral"/> result. If the value's ToString throws, reports it and returns <see langword="null"/> (empty).</summary>
    internal static string? ResolveText(object? ctx, string? raw) => ToText(ResolveValueOrLiteral(ctx, raw), TextExpr(ctx, raw));

    /// <summary>
    /// String form of a value — numbers, dates and other <see cref="IFormattable"/> values with the invariant culture (<c>24.5</c>, never <c>24,5</c>).
    /// When <paramref name="expr"/> is a single binding with a format (<c>{Temp:0.0}</c>), the value is formatted with it by the widget <c>format</c> rule
    /// (<see cref="Layout.AttrRules.FormatOrNull"/>): a format that is not valid for the value, or a value that is not <see cref="IFormattable"/>, gives the default text with a warning.
    /// A ToString exception is reported and gives <see langword="null"/>. Otherwise <paramref name="expr"/> is used only as the report's Member (the path, if it is a binding).
    /// </summary>
    internal static string? ToText(object? value, string? expr)
    {
        if (value is null)
            return null;
        var format = FormatOf(expr);
        if (format is null && value is string s)
            return s;
        try
        {
            if (format is not null)
            {
                if (value is IFormattable f)
                    return Layout.AttrRules.FormatOrNull(f, format, "binding '" + expr + "'") ?? f.ToString(null, CultureInfo.InvariantCulture);
                Warnings.Warn($"Devinno.UI: binding '{expr}' has a format, but its value is a {value.GetType().Name}, which takes no format; using the default text", key: "\u001fbind-format\u001f" + expr);
            }

            return value is IFormattable plain ? plain.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
        }
        catch (Exception ex)
        {
            HandlerErrors.ReportRead(ex, "format", MemberOf(expr));
            return null;
        }
    }

    private static string? MemberOf(string? expr) => expr is not null && IsBinding(expr) ? ExtractPath(expr) : expr;

    private static bool IsL10n(string expr) => expr[1..^1].Trim().StartsWith(L10nPrefix, StringComparison.Ordinal);

    /// <summary>Reads a single binding expression from the context and returns its value (type preserved).</summary>
    /// <param name="ctx">Binding context. If <see langword="null"/>, the result is <see langword="null"/> (except for <c>{L10n.…}</c>).</param>
    /// <param name="expr">Binding expression including braces (at least 2 characters; the format is not checked).</param>
    /// <returns>The value at the path. <see langword="null"/> if an intermediate value is <see langword="null"/>, a property does not exist, or a property getter throws (the exception is only reported, not thrown).</returns>
    public static object? Resolve(object? ctx, string expr)
    {
        var path = expr[1..^1].Trim();
        if (path.StartsWith(L10nPrefix, StringComparison.Ordinal))
            return CurrentStrings?.Get(path[L10nPrefix.Length..]);

        path = Split(expr).Path; // 형식(:0.0)은 값을 읽을 때 쓰지 않는다 — 글자로 바꿀 때(ToText)만
        var isStatic = StaticBinding.IsStatic(path);
        if (ctx is null && !isStatic)
            return null;

        object? value;
        try
        {
            value = isStatic ? StaticBinding.Read(ctx, path) : ResolvePath(ctx, path);
        }
        catch (Exception ex)
        {
            // VM getter 예외 — 빈 값으로 보고 계속(레이아웃·그리기를 멈추지 않는다)
            HandlerErrors.ReportRead(ex, "getter", path);
            value = null;
        }

        BindingReads.Current?.Add(ctx, expr, value); // 자동 갱신(DvApp.AutoRefresh) — 빌드가 읽은 값을 기억
        return value;
    }

    /// <summary>Reads a dot path from an object by the instance rules (used after a static property).</summary>
    internal static object? ReadPath(object obj, string path) => ResolvePath(obj, path);

    private static object? ResolvePath(object? ctx, string path)
    {
        if (ctx is null)
            return null;

        if (ctx is ItemScope item)
        {
            if (path == "Item")
                return item.Item;
            if (path.StartsWith("Item.", StringComparison.Ordinal))
                return ResolvePath(item.Item, path[5..]);
            if (path == "Index")
                return item.Index;
            return ResolvePath(item.Outer, path); // 그 밖 = 바깥 문맥
        }

        if (ctx is IReadOnlyDictionary<string, object?> scope)
            return ResolveDictionaryScope(scope, path);

        var getter = GetGetter(ctx.GetType(), path);
        return getter(ctx);
    }

    private static object? ResolveDictionaryScope(IReadOnlyDictionary<string, object?> scope, string path)
    {
        var segs = path.Split('.');
        if (!scope.TryGetValue(segs[0], out var cur))
            return null;

        for (var i = 1; i < segs.Length; i++)
        {
            if (cur is null)
                return null;

            var pi = cur.GetType().GetProperty(segs[i], BindingFlags.Public | BindingFlags.Instance);
            cur = pi?.GetValue(cur);
        }

        return cur;
    }

    /// <summary>
    /// Writes a value to the property the path points to in the context. If the type differs from the property type, it is converted with <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> (InvariantCulture);
    /// <see cref="Nullable{T}"/> properties are converted to the underlying type.
    /// </summary>
    /// <param name="ctx">Binding context. In a repeated-item scope, <c>Item.</c> paths are written to the item and others to the outer context. Does nothing if <see langword="null"/>.</param>
    /// <param name="path">Property path with braces and <c>Bind</c> removed (the result of <see cref="ExtractPath"/>).</param>
    /// <param name="value">Value to write.</param>
    /// <remarks>
    /// Ignored without an exception if an intermediate value is <see langword="null"/>, the property does not exist, or the property is not writable (no setter).
    /// If the value cannot be converted, the exception from <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> (<see cref="InvalidCastException"/>, <see cref="FormatException"/>, <see cref="OverflowException"/>) propagates as-is.
    /// An exception thrown by the property setter propagates wrapped in <see cref="TargetInvocationException"/>.
    /// (For widget input, <see cref="DvSurface"/> catches these exceptions, treats them as a write failure, and reports them through <see cref="DvSurface.HandlerError"/>.)
    /// </remarks>
    public static void SetValue(object? ctx, string path, object? value) => Write(ctx, path, value);

    /// <summary>
    /// <see cref="SetValue"/> that tells what was written: the object the path starts from (the screen VM, or the item for <c>Item.</c> paths),
    /// the path from that object, and the value stored (after conversion). <see langword="null"/> when nothing was written (missing path, read-only property).
    /// </summary>
    internal static (object Owner, string Path, object? Value)? Write(object? ctx, string path, object? value)
    {
        if (StaticBinding.IsStatic(path))
            return StaticBinding.Write(ctx, path, value);
        if (ctx is null)
            return null;

        if (ctx is ItemScope item)
            return path.StartsWith("Item.", StringComparison.Ordinal) ? Write(item.Item, path[5..], value) : Write(item.Outer, path, value);

        var segs = path.Split('.');
        object cur = ctx;
        for (var i = 0; i < segs.Length - 1; i++)
        {
            var pi = cur.GetType().GetProperty(segs[i], BindingFlags.Public | BindingFlags.Instance);
            var next = pi?.GetValue(cur);
            if (next is null)
                return null;

            cur = next;
        }

        var last = cur.GetType().GetProperty(segs[^1], BindingFlags.Public | BindingFlags.Instance);
        if (last is not { CanWrite: true })
            return null;
        var stored = ConvertFor(last.PropertyType, value);
        last.SetValue(cur, stored);
        return (ctx, path, stored);
    }

    /// <summary>The value to store in a property of <paramref name="propertyType"/>: as is if it already is one, otherwise <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> (invariant culture) to the type, or to the underlying type of a <see cref="Nullable{T}"/>.</summary>
    internal static object? ConvertFor(Type propertyType, object? value)
    {
        // int? 등 Nullable 은 바탕 타입으로 변환(ChangeType 은 Nullable 을 직접 못 다룸)
        var target = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        // 이미 대상 타입이면 그대로(목록 등 IConvertible 아닌 값 — 그리드 선택 스냅샷)
        return value is null ? null
            : target.IsInstanceOfType(value) ? value
            : Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    /// <summary>Returns the declared type of the property the path points to. Used to build a value matching the target type before writing.</summary>
    /// <param name="ctx">Binding context (a repeated-item scope is followed by the same rules as <see cref="SetValue"/>).</param>
    /// <param name="path">Property path with braces and <c>Bind</c> removed.</param>
    /// <returns>The declared property type. <see langword="null"/> if the context is <see langword="null"/>, an intermediate value is <see langword="null"/>, or the property does not exist.</returns>
    public static Type? ResolvePropertyType(object? ctx, string path)
    {
        if (StaticBinding.IsStatic(path))
            return StaticBinding.PropertyType(ctx, path);
        if (ctx is ItemScope item)
            return path.StartsWith("Item.", StringComparison.Ordinal)
                ? ResolvePropertyType(item.Item, path[5..])
                : ResolvePropertyType(item.Outer, path);

        var segs = path.Split('.');
        object? cur = ctx;
        Type? type = null;
        for (var i = 0; i < segs.Length; i++)
        {
            if (cur is null)
                return null;
            var pi = cur.GetType().GetProperty(segs[i], BindingFlags.Public | BindingFlags.Instance);
            if (pi is null)
                return null;
            type = pi.PropertyType;
            if (i < segs.Length - 1)
            {
                try
                {
                    cur = pi.GetValue(cur);
                }
                catch (Exception ex)
                {
                    HandlerErrors.ReportRead(ex, "getter", path);
                    return null;
                }
            }
        }

        return type;
    }

    private static Func<object, object?> GetGetter(Type type, string path)
    {
        var key = (type, path);

        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var g))
                return g;

            var segs = path.Split('.');
            Func<object, object?> fn = obj =>
            {
                object? cur = obj;
                foreach (var seg in segs)
                {
                    if (cur is null)
                        return null;

                    var pi = cur.GetType().GetProperty(seg, BindingFlags.Public | BindingFlags.Instance);
                    cur = pi?.GetValue(cur);
                }

                return cur;
            };

            Cache[key] = fn;
            return fn;
        }
    }
}
