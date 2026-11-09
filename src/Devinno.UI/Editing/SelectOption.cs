using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;

namespace Devinno.UI;

/// <summary>A DvSelect option — value, display text and icon (<c>fa-…</c>).</summary>
internal sealed record SelectOption(object? Value, string Text, string? Icon);

/// <summary>DvSelect value ↔ option — converts to the bound property type for comparison and writing (string "1" → int, "Left" → enum, Nullable).</summary>
internal static class SelectValue
{
    public static bool TryConvert(object? value, Type? target, out object? result)
    {
        result = value;
        if (target is null || value is null)
            return true;

        var t = Nullable.GetUnderlyingType(target) ?? target;
        if (t.IsInstanceOfType(value))
            return true;

        try
        {
            if (t.IsEnum)
            {
                if (value is string s)
                    return Enum.TryParse(t, s, ignoreCase: true, out result);
                result = Enum.ToObject(t, value);
                return true;
            }

            result = Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            result = null;
            return false;
        }
    }

    public static bool Matches(object? optionValue, object? current, Type? target)
        => TryConvert(optionValue, target ?? current?.GetType(), out var v) && SafeEquals(v, current);

    // VM 형식의 Equals 가 던지면 다른 값으로 본다(알리고 계속)
    private static bool SafeEquals(object? a, object? b)
    {
        try
        {
            return Equals(a, b);
        }
        catch (Exception ex)
        {
            HandlerErrors.ReportRead(ex, "compare", "value");
            return false;
        }
    }

    /// <summary>Option list: items (path) &gt; DvOption children &gt; automatic enum.</summary>
    public static List<SelectOption> Options(Markup.ElementNode node, object? ctx, Type? valueType)
        => Options(new AttrReader(node, ctx, null), valueType);

    /// <summary>Option list read through <paramref name="r"/> (the element's own attributes): items (path) &gt; DvOption children &gt; automatic enum.</summary>
    public static List<SelectOption> Options(AttrReader r, Type? valueType)
    {
        var node = r.Node;
        var ctx = r.Context;
        var list = new List<SelectOption>();
        // DvOption 은 그 노드의 리더로 — 핸들 덮어쓰기는 보지 않는다(지금 동작 그대로: 마크업 값만)
        string? Attr(Markup.ElementNode n, string name) => new AttrReader(n, ctx, null).Text(name);

        if (r.Raw("items") is { } itemsRaw)
        {
            if (BindingResolver.ResolveValueOrLiteral(ctx, itemsRaw) is System.Collections.IEnumerable items and not string)
            {
                var valuePath = r.Raw("item-value");
                var textPath = r.Raw("item-text");
                var iconPath = r.Raw("item-icon");
                var member = BindingResolver.IsBinding(itemsRaw) ? BindingResolver.ExtractPath(itemsRaw) : itemsRaw;
                foreach (var item in Interaction.HandlerErrors.Snapshot(items.Cast<object?>(), member)) // 열거 중 VM 예외 = 항목 없음
                {
                    var value = string.IsNullOrEmpty(valuePath) ? item : BindingResolver.Resolve(item, $"{{{valuePath}}}");
                    var text = string.IsNullOrEmpty(textPath)
                        ? BindingResolver.ToText(value, valuePath)
                        : BindingResolver.ToText(BindingResolver.Resolve(item, $"{{{textPath}}}"), textPath);
                    var icon = string.IsNullOrEmpty(iconPath) ? null : BindingResolver.ToText(BindingResolver.Resolve(item, $"{{{iconPath}}}"), iconPath);
                    list.Add(new SelectOption(value, text ?? "", icon));
                }
            }

            return list;
        }

        var options = node.Children.Where(c => c.Tag == "DvOption").ToList();
        if (options.Count > 0)
        {
            foreach (var o in options)
            {
                var value = Attr(o, "value");
                list.Add(new SelectOption(value, Attr(o, "text") ?? value ?? "", Attr(o, "icon")));
            }

            return list;
        }

        var t = valueType is null ? null : Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (t is { IsEnum: true })
            list.AddRange(Enum.GetValues(t).Cast<object>().Select(v => new SelectOption(v, v.ToString() ?? "", null)));
        return list;
    }
}

internal static class SelectDisplay
{
    public static SelectOption? Selected(LayoutBox box)
        => box.SelectedIndex >= 0 && box.SelectedIndex < box.SelectOptions.Count ? box.SelectOptions[box.SelectedIndex] : null;

    public static string Text(LayoutBox box) => Selected(box)?.Text ?? "";

    /// <summary>Can be opened if it is a two-way binding and is not disabled or read-only.</summary>
    public static bool IsOpenable(LayoutBox box) => InputDisplay.IsEditable(box);
}
