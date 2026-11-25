using System.Runtime.CompilerServices;

namespace Devinno.UI.Binding;

// 자동 갱신(DvApp.AutoRefresh) — 화면 한 번 빌드가 읽은 바인딩(문맥, 식, 값)을 기억했다가, 같은 문맥에서 다시 읽어 값이 달라졌는지 본다.
// 같은 (문맥, 식)은 한 번만. 비교는 Equals — 목록은 같은 객체면 같다(안의 항목이 바뀐 것은 모른다)
internal sealed class BindingReads
{
    /// <summary>The recorder of the build running on this thread (set by <see cref="Layout.LayoutEngine"/> for surfaces that record); <see langword="null"/> = not recording.</summary>
    [ThreadStatic] internal static BindingReads? Current;

    private readonly List<(object? Ctx, string Expr, object? Value)> _reads = new();
    private readonly HashSet<(object? Ctx, string Expr)> _seen = new(KeyComparer.Instance);

    /// <summary>The number of distinct bindings recorded.</summary>
    public int Count => _reads.Count;

    public void Add(object? ctx, string expr, object? value)
    {
        if (_seen.Add((ctx, expr)))
            _reads.Add((ctx, expr, value));
    }

    /// <summary>Whether any recorded binding now reads a different value. Reads on the calling thread, without recording.</summary>
    public bool Changed()
    {
        var prev = Current;
        Current = null;
        try
        {
            foreach (var (ctx, expr, value) in _reads)
            {
                if (!Equals(BindingResolver.Resolve(ctx, expr), value))
                    return true;
            }
            return false;
        }
        finally
        {
            Current = prev;
        }
    }

    // 문맥은 참조로(같은 반복 항목 범위·같은 VM), 식은 글자 그대로
    private sealed class KeyComparer : IEqualityComparer<(object? Ctx, string Expr)>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals((object? Ctx, string Expr) a, (object? Ctx, string Expr) b)
            => ReferenceEquals(a.Ctx, b.Ctx) && string.Equals(a.Expr, b.Expr, StringComparison.Ordinal);

        public int GetHashCode((object? Ctx, string Expr) k)
            => HashCode.Combine(k.Ctx is null ? 0 : RuntimeHelpers.GetHashCode(k.Ctx), StringComparer.Ordinal.GetHashCode(k.Expr));
    }
}
