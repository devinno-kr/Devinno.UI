using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Devinno.UI.Grid;

/// <summary>
/// A compiled accessor that reads an item property path (<c>Motor.Speed</c>). Built only once per (type, path),
/// so drawing a cell value does not go through reflection (<c>PropertyInfo.GetValue</c>) every time.
/// </summary>
internal static class FieldAccessor
{
    /// <param name="Get">Reads the path value from an item (null midway along the path = null).</param>
    /// <param name="ValueType">The type of the last property (the underlying type for Nullable). Null if the path contains a missing property.</param>
    /// <param name="GetDouble">For numeric properties, reads as double without boxing (null, or null midway along the path = NaN) — for summary aggregation. Null if not numeric.</param>
    /// <param name="Writable">The property at the end of the path can be written (public setter, no struct along the path — with a struct the write would go to a copy and have no effect).</param>
    internal sealed record Entry(Func<object, object?> Get, Type? ValueType, Func<object, double>? GetDouble = null, bool Writable = false);

    private static readonly Dictionary<(Type, string), Entry> Cache = new();
    private static readonly HashSet<(Type, string)> Warned = new();

    /// <summary>Missing-property warning — once per (type, path) per process (so it does not repeat on every rebuild).</summary>
    public static void WarnMissing(Type type, string path)
    {
        lock (Warned)
        {
            if (!Warned.Add((type, path)))
                return;
        }

        Trace.TraceWarning($"Devinno.UI: DvDataGrid field '{path}' — {type.Name} 에 없는 속성, 빈 셀로 표시");
    }

    private static readonly Entry Missing = new(_ => null, null);

    public static Entry For(Type type, string path)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue((type, path), out var e))
                Cache[(type, path)] = e = Compile(type, path);
            return e;
        }
    }

    private static Entry Compile(Type type, string path)
    {
        var props = new List<PropertyInfo>();
        var cur = type;
        foreach (var seg in path.Split('.'))
        {
            var pi = cur.GetProperty(seg, BindingFlags.Public | BindingFlags.Instance);
            if (pi is null || pi.GetIndexParameters().Length > 0)
                return Missing;
            props.Add(pi);
            cur = pi.PropertyType;
        }

        var p = Expression.Parameter(typeof(object), "item");
        var nullObj = Expression.Constant(null, typeof(object));
        var body = Chain(Expression.Convert(p, type), props, 0, e => Expression.Convert(e, typeof(object)), nullObj);
        var get = Expression.Lambda<Func<object, object?>>(body, p).Compile();

        var valueType = Nullable.GetUnderlyingType(cur) ?? cur;
        Func<object, double>? getDouble = null;
        if (IsNumeric(valueType))
        {
            var nan = Expression.Constant(double.NaN);
            var dbody = Chain(Expression.Convert(p, type), props, 0, ToDouble, nan);
            getDouble = Expression.Lambda<Func<object, double>>(dbody, p).Compile();
        }

        var writable = props[^1].SetMethod is { IsPublic: true } &&
                       props.Take(props.Count - 1).All(pi => !pi.PropertyType.IsValueType);
        return new Entry(get, valueType, getDouble, writable);

        static Expression ToDouble(Expression e)
        {
            if (Nullable.GetUnderlyingType(e.Type) is null)
                return Expression.Convert(e, typeof(double));
            var tmp = Expression.Variable(e.Type);
            return Expression.Block(new[] { tmp }, Expression.Assign(tmp, e),
                Expression.Condition(Expression.Property(tmp, "HasValue"),
                    Expression.Convert(Expression.Property(tmp, "Value"), typeof(double)),
                    Expression.Constant(double.NaN)));
        }
    }

    private static bool IsNumeric(Type t) => !t.IsEnum && Type.GetTypeCode(t) is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or
        TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or
        TypeCode.Decimal;

    // 경로 중간 값이 null 이면 nullValue — 예외 없이 빈 셀. 마지막 값은 terminal 로(object 박싱 또는 double 변환)
    private static Expression Chain(Expression obj, List<PropertyInfo> props, int i, Func<Expression, Expression> terminal, Expression nullValue)
    {
        if (i == props.Count)
            return terminal(obj);

        var access = Expression.Property(obj, props[i]);
        var t = props[i].PropertyType;
        if (i == props.Count - 1 || (t.IsValueType && Nullable.GetUnderlyingType(t) is null))
            return Chain(access, props, i + 1, terminal, nullValue);

        var tmp = Expression.Variable(t);
        var next = Nullable.GetUnderlyingType(t) is not null ? Expression.Property(tmp, "Value") : (Expression)tmp;
        return Expression.Block(new[] { tmp },
            Expression.Assign(tmp, access),
            Expression.Condition(Expression.Equal(tmp, Expression.Constant(null, t)),
                nullValue,
                Chain(next, props, i + 1, terminal, nullValue)));
    }
}

/// <summary>
/// Value reading for one column — remembers the accessor of the last seen item type, so the same type needs no dictionary lookup (works even with mixed item types).
/// </summary>
internal sealed class FieldReader
{
    private Type? _type;
    private FieldAccessor.Entry? _entry;

    public FieldReader(string path) => Path = path;

    public string Path { get; }

    /// <summary>The value type based on the last read item type (null if nothing has been read yet or the property is missing).</summary>
    public Type? ValueType => _entry?.ValueType;

    /// <summary>Whether the property at the end of the path can be written, based on the last read item type.</summary>
    public bool Writable => _entry?.Writable ?? false;

    /// <summary>The path value of the item. If the item property getter (VM code) throws, it is reported and null is returned (empty cell, null in sorting, excluded from summaries).</summary>
    public object? Get(object item)
    {
        var e = EntryFor(item);
        try
        {
            return e.Get(item);
        }
        catch (Exception ex)
        {
            Interaction.HandlerErrors.ReportRead(ex, "getter", Path, "DvDataGrid");
            return null;
        }
    }

    /// <summary>Reads without boxing for numeric columns — false = not a numeric column (→ <see cref="Get"/>). NaN if the value is null (or the getter throws).</summary>
    public bool TryGetDouble(object item, out double value)
    {
        if (EntryFor(item).GetDouble is { } f)
        {
            try
            {
                value = f(item);
            }
            catch (Exception ex)
            {
                Interaction.HandlerErrors.ReportRead(ex, "getter", Path, "DvDataGrid");
                value = double.NaN;
            }

            return true;
        }

        value = 0;
        return false;
    }

    private FieldAccessor.Entry EntryFor(object item)
    {
        var t = item.GetType();
        if (!ReferenceEquals(t, _type))
        {
            _type = t;
            _entry = FieldAccessor.For(t, Path);
            if (_entry.ValueType is null && Path.Length > 0) // 버튼 열처럼 field 없는 열은 경고 없이 빈 값
                FieldAccessor.WarnMissing(t, Path);
        }

        return _entry!;
    }
}
