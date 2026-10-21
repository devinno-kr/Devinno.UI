using System.Diagnostics;
using System.Reflection;
using Devinno.UI.Binding;

namespace Devinno.UI.Interaction;

// 앱(VM) 코드 예외 처리 한 곳 — Trace 오류 1회 + 서피스 HandlerError(서피스가 있으면). 예외는 다시 던지지 않는다.
internal static class HandlerErrors
{
    public static Exception Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException { InnerException: { } inner })
            ex = inner;
        return ex;
    }

    public static void Report(DvSurface? owner, Exception ex, string kind, string? member, string? tag)
    {
        var e = Unwrap(ex);
        Trace.TraceError($"Devinno.UI: {kind} '{member}' ({tag}) threw: {e.GetType().Name}: {e.Message}");
        owner?.RaiseHandlerError(new DvHandlerErrorEventArgs(e, kind, member, tag));
    }

    // ── 레이아웃·그리기 중 VM 읽기(getter·ToString·열거·비교) 예외 — 그 값은 null(빈 값)로 보고 계속한다.
    // 알릴 서피스·중복 억제 집합은 앰비언트(UI 스레드)로: DvSurface 가 Layout/RenderTo 동안, DvRenderer·LayoutEngine 은 서피스 없이 설정한다.

    /// <summary>The target to report VM read exceptions to, and the (Kind, Member, Tag) already reported — the surface clears it on every rebuild.</summary>
    internal sealed class ReadScope(DvSurface? owner)
    {
        public DvSurface? Owner { get; } = owner;
        public HashSet<(string Kind, string? Member, string? Tag)> Seen { get; } = new();

        /// <summary>The tag of the element currently being built (set per element by LayoutEngine) — used when the binding resolver does not know the tag.</summary>
        public string? Tag { get; set; }
    }

    [ThreadStatic] private static ReadScope? _read;

    internal static ReadScope? CurrentRead => _read;

    /// <summary>Replaces the ambient read scope and returns the previous one (restore with <see cref="ExitRead"/> — for nested cell surfaces).</summary>
    internal static ReadScope? EnterRead(ReadScope scope)
    {
        var prev = _read;
        _read = scope;
        return prev;
    }

    internal static void ExitRead(ReadScope? prev) => _read = prev;

    /// <summary>Reports a VM read exception — if there is a scope, the same (Kind, Member, Tag) is reported only once. If tag is null, the scope's current element tag is used.</summary>
    public static void ReportRead(Exception ex, string kind, string? member, string? tag = null)
    {
        var scope = _read;
        tag ??= scope?.Tag;
        if (scope is not null && !scope.Seen.Add((kind, member, tag)))
            return;
        Report(scope?.Owner, ex, kind, member, tag);
    }

    /// <summary>The value's string — if the VM type's ToString throws, reports it and returns null.</summary>
    public static string? Text(object? value, string? member, string? tag = null)
    {
        if (value is null or string)
            return (string?)value;
        try
        {
            return value.ToString();
        }
        catch (Exception ex)
        {
            ReportRead(ex, "format", member, tag);
            return null;
        }
    }

    /// <summary>Reads a VM collection to the end into a list — on an exception during enumeration, reports it and returns an empty list (so a partially shown screen is never built).</summary>
    public static List<T> Snapshot<T>(IEnumerable<T> items, string? member, string? tag = null)
    {
        try
        {
            return items.ToList();
        }
        catch (Exception ex)
        {
            ReportRead(ex, "enumerate", member, tag);
            return new List<T>();
        }
    }

    /// <summary>Writes a value to the VM. On a setter exception or type conversion failure, reports it and returns false (a write failure is handled the same as a rejection).</summary>
    public static bool TrySet(DvSurface? owner, string? tag, object? ctx, string path, object? value)
    {
        try
        {
            BindingResolver.SetValue(ctx, path, value);
            return true;
        }
        catch (Exception ex)
        {
            Report(owner, ex, "setter", path, tag);
            return false;
        }
    }
}
