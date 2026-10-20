using System.Diagnostics;
using System.Globalization;
using Devinno.UI.Styling;
using SkiaSharp;

namespace Devinno.UI.Grid;

internal enum GridCellType { Text, Check, Lamp, Button, Input, Select, DateTime, Color }

/// <summary>One <c>DvColumn</c> — its definition plus layout result (<see cref="X"/>, <see cref="W"/>, in grid content coordinates).</summary>
internal sealed class GridColumn
{
    public required string Header { get; init; }
    public required FieldReader Reader { get; init; }
    public GridWidth Width { get; init; } = GridWidth.Star;
    /// <summary>Explicit alignment. null = decided by value type (numbers → right).</summary>
    public TextAlign? Align { get; init; }
    public string? Format { get; init; }
    public bool Frozen { get; init; }
    /// <summary>Sortable by tapping the header (<c>sortable</c>).</summary>
    public bool Sortable { get; init; }
    /// <summary>Initial sort (<c>sort="asc|desc"</c>) — null = none, true = descending.</summary>
    public bool? InitialSortDesc { get; init; }
    /// <summary><c>cell-class</c> — the item property value = the class name of this column's cells.</summary>
    public FieldReader? CellClass { get; init; }

    // ── 즉시 셀(3c): cell-type = check · lamp · button
    public GridCellType CellType { get; init; }
    /// <summary>The check column is display-only (<c>readonly</c>).</summary>
    public bool ReadOnly { get; init; }
    /// <summary>VM check before a check commit — <c>bool M(항목, bool)</c> (item, bool) first, otherwise <c>bool M(bool)</c>.</summary>
    public string? OnBeforeCommit { get; init; }
    /// <summary>Lamp on color (for bool values). null = the state-run token.</summary>
    public DvColor? OnColor { get; init; }
    public bool Blink { get; init; }
    public string ButtonText { get; init; } = "";
    public string? Icon { get; init; }
    /// <summary>Button color — primary · secondary · danger · ghost.</summary>
    public string Variant { get; init; } = "primary";
    public string? OnClick { get; init; }

    // ── 필터(3d-2)
    public GridFilterSetting Filter { get; init; }

    /// <summary>The effective filter kind — auto uses the current value type (text if the type is unknown because there are no items).</summary>
    public GridFilterKind FilterKind => Filter switch
    {
        GridFilterSetting.None => GridFilterKind.None,
        GridFilterSetting.Text => GridFilterKind.Text,
        GridFilterSetting.Values => GridFilterKind.Values,
        GridFilterSetting.Range => GridFilterKind.Range,
        GridFilterSetting.Date => GridFilterKind.Date,
        _ => AutoFilterKind()
    };

    private GridFilterKind AutoFilterKind()
    {
        if (CellType == GridCellType.Select)
            return GridFilterKind.Values;
        var t = Reader.ValueType is { } vt ? Nullable.GetUnderlyingType(vt) ?? vt : null;
        if (t is null || t == typeof(string))
            return GridFilterKind.Text;
        if (IsNumeric(t))
            return GridFilterKind.Range;
        if (t == typeof(DateTime) || t == typeof(DateOnly) || t == typeof(TimeOnly))
            return GridFilterKind.Date;
        if (t == typeof(bool) || t.IsEnum)
            return GridFilterKind.Values;
        return GridFilterKind.Text;
    }
    /// <summary>Button <c>enabled</c> — an item bool property; when false, the button is dimmed and does nothing.</summary>
    public FieldReader? Enabled { get; init; }

    // ── 편집 셀(3d-1): cell-type = input · select · datetime · color
    public double? Min { get; init; }
    public double? Max { get; init; }
    public string? Unit { get; init; }
    /// <summary>Raw datetime <c>mode</c> text (omitted = by value type).</summary>
    public string? Mode { get; init; }
    public bool ShowSeconds { get; init; }
    /// <summary>Source of select options — the DvColumn node (DvOption children, items, item-*) and the grid VM context.</summary>
    internal Markup.ElementNode? OptionsNode { get; init; }
    internal object? OptionsContext { get; init; }

    private static readonly object NoMatch = new(); // 변환 못 한 항목 값 — 어떤 값과도 같지 않음
    private static readonly HashSet<(Type, string, GridCellType)> MismatchWarned = new();
    private List<SelectOption>? _options;
    private object?[] _optionValues = Array.Empty<object?>();
    private Type? _optionsType;

    public bool IsEditCell => CellType is GridCellType.Input or GridCellType.Select or GridCellType.DateTime or GridCellType.Color;

    /// <summary>Select options — built once based on the value type once it is known (enums included automatically).</summary>
    public IReadOnlyList<SelectOption> Options
    {
        get
        {
            EnsureOptions();
            return _options!;
        }
    }

    /// <summary>Index of the option equal to the value (-1 if none). Option values are converted to the property type only once (no conversion or allocation per draw).</summary>
    public int OptionIndex(object? value)
    {
        EnsureOptions();
        var vals = _optionValues;
        for (var i = 0; i < vals.Length; i++)
        {
            if (Equals(vals[i], value))
                return i;
        }

        return -1;
    }

    // 항목 목록(글자·아이콘·값 원문)은 레이아웃 때 한 번 — 그리기 경로엔 다국어 테이블이 없어 @키를 못 푼다.
    // 값 타입이 바뀌면 변환 값만 다시(타입별 캐시, 섞인 항목 타입도 셀마다 재변환 없음).
    // 예외: enum 자동 목록(DvOption·items 없음)은 타입에 따라 달라지고 @키도 없으므로 다시 만든다.
    private void EnsureOptions()
    {
        var t = Reader.ValueType;
        if (_options is not null && ReferenceEquals(_optionsType, t))
            return;
        if (_options is null || IsEnumAuto)
        {
            _options = OptionsNode is null ? new List<SelectOption>() : SelectValue.Options(OptionsNode, OptionsContext, t);
            _valuesByType.Clear();
        }

        var key = t ?? typeof(void);
        if (!_valuesByType.TryGetValue(key, out var vals))
        {
            vals = _options.Select(o => SelectValue.TryConvert(o.Value, t, out var v) ? v : NoMatch).ToArray();
            _valuesByType[key] = vals;
        }

        _optionValues = vals;
        _optionsType = t;
    }

    private readonly Dictionary<Type, object?[]> _valuesByType = new();

    private bool IsEnumAuto => OptionsNode is { } n && !n.Attributes.ContainsKey("items") && !n.Children.Any(c => c.Tag == "DvOption");

    /// <summary>Datetime mode — DvDateTimeInput rules (omitted = by type; DateOnly and TimeOnly allow only their own mode).</summary>
    public PickerMode DateTimeMode => PickerValue.ResolveMode(Mode, Reader.ValueType);

    /// <summary>
    /// Whether this is an editable edit cell — readonly, no field, no setter, a struct path, or a mismatch between cell kind and property type = display only (tap = ordinary row tap).
    /// A mismatch warns once per (item type, path, kind).
    /// </summary>
    public bool CanEdit(object item)
    {
        if (!IsEditCell || ReadOnly || Reader.Path.Length == 0)
            return false;
        Reader.Get(item); // 이 항목 타입의 접근자로(값 타입·쓰기 가능 확정)
        if (Reader.ValueType is not { } t || !Reader.Writable)
            return false;
        if (TypeFits(CellType, t))
            return true;

        lock (MismatchWarned)
        {
            if (MismatchWarned.Add((item.GetType(), Reader.Path, CellType)))
                Trace.TraceWarning($"Devinno.UI: DvColumn cell-type=\"{CellType.ToString().ToLowerInvariant()}\" field '{Reader.Path}' — {t.Name} 은 맞지 않는 타입, 표시만");
        }

        return false;
    }

    /// <summary>Whether the property's underlying type fits the cell kind (input = string or number, select = any type, datetime = DateTime, DateOnly or TimeOnly, color = string or SKColor).</summary>
    internal static bool TypeFits(GridCellType kind, Type t) => kind switch
    {
        GridCellType.Input => t == typeof(string) || IsNumeric(t),
        GridCellType.Select => true,
        GridCellType.DateTime => t == typeof(DateTime) || t == typeof(DateOnly) || t == typeof(TimeOnly),
        GridCellType.Color => t == typeof(string) || t == typeof(SKColor),
        _ => false
    };

    /// <summary>Cell display text — select = the chosen option's text ("" if none), datetime = DvDateTimeInput display, color = #RRGGBB, otherwise = <see cref="Text"/>.</summary>
    public string CellText(object item) => CellType switch
    {
        GridCellType.Select => OptionIndex(Reader.Get(item)) is var i and >= 0 ? Options[i].Text : "",
        GridCellType.DateTime => PickerValue.Display(Reader.Get(item), DateTimeMode, ShowSeconds, Format),
        GridCellType.Color => Reader.Get(item) is { } v && PickerValue.TryToColor(v, out var c) ? PickerValue.Hex(c) : "",
        _ => Text(item)
    };

    /// <summary>Cell display text for a single value — select = option text, datetime and color = same display as the cell, otherwise = format applied (exception = "").</summary>
    public string ValueText(object? value)
    {
        if (value is null)
            return "";
        switch (CellType)
        {
            case GridCellType.Select:
                return OptionIndex(value) is var i and >= 0 ? Options[i].Text : "";
            case GridCellType.DateTime:
                return PickerValue.Display(value, DateTimeMode, ShowSeconds, Format);
            case GridCellType.Color:
                return PickerValue.TryToColor(value, out var c) ? PickerValue.Hex(c) : "";
        }

        try
        {
            return FormatValue(value);
        }
        catch (Exception ex)
        {
            Interaction.HandlerErrors.ReportRead(ex, "format", Reader.Path, "DvDataGrid");
            return "";
        }
    }

    public float X { get; set; }
    public float W { get; set; }

    /// <summary>The value type is numeric (eligible for automatic summary-row fn).</summary>
    public bool IsNumericValue => IsNumeric(Reader.ValueType);

    public TextAlign EffectiveAlign => Align ??
        (CellType is GridCellType.Select or GridCellType.DateTime or GridCellType.Color ? TextAlign.Left
         : IsNumeric(Reader.ValueType) ? TextAlign.Right : TextAlign.Left);

    public string Text(object item)
    {
        var v = Reader.Get(item);
        if (v is null)
            return "";
        try
        {
            return FormatValue(v);
        }
        catch (Exception ex)
        {
            // VM 형식의 ToString·IFormattable 예외 = 빈 칸
            Interaction.HandlerErrors.ReportRead(ex, "format", Reader.Path, "DvDataGrid");
            return "";
        }
    }

    private string FormatValue(object v)
    {
        if (Format is { Length: > 0 } f && v is IFormattable fm)
        {
            try
            {
                return fm.ToString(f, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
            }
        }

        return v is IFormattable plain ? plain.ToString(null, CultureInfo.InvariantCulture) : v.ToString() ?? "";
    }

    private static bool IsNumeric(Type? t) => t is not null && Type.GetTypeCode(t) switch
    {
        TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => !t.IsEnum,
        _ => false
    };
}
