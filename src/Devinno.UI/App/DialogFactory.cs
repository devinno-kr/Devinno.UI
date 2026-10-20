using System.Reflection;

namespace Devinno.UI.App;

/// <summary>
/// Picks a dialog constructor from the <c>ShowDialog</c> arguments and creates an instance (spec §6.4). Runs on the calling thread.
/// </summary>
internal static class DialogFactory
{
    /// <summary>
    /// Creates the instance with the single public instance constructor that matches <paramref name="args"/>. <see langword="null"/> is treated as one null argument, and an array that is not <c>object[]</c> (covariant params) as one argument.
    /// Throws <see cref="ArgumentException"/> if no constructor matches or more than one does. Constructor exceptions are thrown as-is, not wrapped.
    /// </summary>
    internal static TDialog Create<TDialog>(object?[]? args) where TDialog : DvDialog
    {
        args ??= [null];
        if (args.GetType() != typeof(object[]))
            args = [args]; // ShowDialog<X,R>(names) — string[] 가 params 배열 자리로 들어옴: 호출자는 배열 하나를 넘긴 것

        var type = typeof(TDialog);
        if (type.IsAbstract)
            throw new ArgumentException($"다이얼로그 '{type.Name}' 은(는) abstract 라 만들 수 없습니다. 상속한 구체 다이얼로그 형식을 ShowDialog 에 넘기세요.", nameof(args));
        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        ConstructorInfo? found = null;
        var count = 0;
        foreach (var c in ctors)
        {
            if (!Matches(c.GetParameters(), args))
                continue;
            found = c;
            count++;
        }

        if (count != 1)
        {
            var passed = string.Join(", ", args.Select(a => a?.GetType().Name ?? "null"));
            var list = ctors.Length == 0 ? "(없음)" : string.Join("; ", ctors.Select(c => Signature(type, c)));
            var why = count == 0 ? "맞는 public 생성자가 없습니다" : "맞는 public 생성자가 둘 이상이라 모호합니다(null 인자는 형식을 정할 수 없습니다)";
            throw new ArgumentException($"다이얼로그 '{type.Name}' 에 넘긴 인자 ({passed}) 와 {why}. 있는 생성자: {list}", nameof(args));
        }

        return (TDialog)found!.Invoke(BindingFlags.DoNotWrapExceptions, binder: null, args, culture: null);
    }

    // 개수가 같고, 각 인자가 대입 가능(null 은 참조·nullable 형식에만)
    private static bool Matches(ParameterInfo[] ps, object?[] args)
    {
        if (ps.Length != args.Length)
            return false;
        for (var i = 0; i < ps.Length; i++)
        {
            var t = ps[i].ParameterType;
            if (t.IsByRef || t.IsPointer)
                return false;
            if (args[i] is null)
            {
                if (t.IsValueType && Nullable.GetUnderlyingType(t) is null)
                    return false;
            }
            else if (!t.IsInstanceOfType(args[i]))
                return false;
        }
        return true;
    }

    private static string Signature(Type type, ConstructorInfo c)
        => $"{type.Name}({string.Join(", ", c.GetParameters().Select(p => $"{TypeName(p.ParameterType)} {p.Name}"))})";

    private static string TypeName(Type t) => Nullable.GetUnderlyingType(t) is { } u ? u.Name + "?" : t.Name;
}
