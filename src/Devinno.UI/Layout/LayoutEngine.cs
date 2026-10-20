using System.Globalization;
using Devinno.UI.Components;
using Devinno.UI.Binding;
using Devinno.UI.Interaction;
using Devinno.UI.Markup;
using Devinno.UI.Styling;
using Devinno.UI.Text;
using SkiaSharp;

namespace Devinno.UI.Layout;

/// <summary>
/// The layout engine: builds a <see cref="LayoutBox"/> tree from an <see cref="ElementNode"/> tree and a view model, and computes each box's position and size (px).
/// </summary>
/// <remarks>
/// <para>An internal engine stage. App code normally does not use it directly and uses <see cref="DvSurface"/>/<see cref="DvRenderer"/> instead.
/// Tests and tools can use it to inspect layout results (box positions, bound values) or to draw the result directly with <see cref="Painting.Painter"/>.</para>
/// <para>A single <see cref="Layout"/> call performs the following stages in order.
/// (1) Build: collects the theme stylesheet, the <c>DvStyle</c> in registry component definitions, and the <c>DvStyle</c> in the markup, in that order; then creates a box for each element, resolves bindings, classes and inline styles, and fills in widget values
/// (elements whose <c>visible</c> is false are not created, components are expanded, and <c>DvRepeat</c> repeats for each item).
/// (2) Text property inheritance: passes <c>font-family</c> and <c>white-space</c> down from parent to child.
/// (3) Measure and arrange: sets each box's <see cref="LayoutBox.Rect"/> using flex/grid rules and computes the content size of scroll boxes.</para>
/// <para>Bound values are a snapshot taken at call time. When the view model changes, call it again to build a new tree (<see cref="DvSurface"/> does this on Invalidate).
/// An instance changes internal state during a call, so do not use one instance from multiple threads at the same time. Different threads can each create their own instance.</para>
/// </remarks>
/// <example>
/// <code>
/// using Devinno.UI.Layout;
/// using Devinno.UI.Markup;
///
/// var root = MarkupParser.Parse("&lt;DvRow&gt;&lt;DvButton text=\"시작\"/&gt;&lt;DvButton text=\"정지\"/&gt;&lt;/DvRow&gt;");
/// LayoutBox tree = new LayoutEngine().Layout(root, vm: null, width: 800, height: 480);
/// foreach (var b in tree.Children)
///     Console.WriteLine($"{b.ResolvedText}: {b.Rect}");
/// </code>
/// </example>
public sealed class LayoutEngine
{
    private const int MaxComponentDepth = 64;

    private readonly DvTheme? _theme;
    private readonly Localization.DvStrings? _strings;
    private StyleSheet _sheet = new();

    /// <summary>Creates an engine with the default theme (<see cref="DvTheme.Default"/>) and no multilingual table.</summary>
    public LayoutEngine() : this(null)
    {
    }

    /// <summary>Creates an engine with the specified theme (no multilingual table).</summary>
    /// <param name="theme">The theme to take the stylesheet and tokens from. <see cref="DvTheme.Default"/> if <see langword="null"/>.</param>
    public LayoutEngine(DvTheme? theme) : this(theme, null)
    {
    }

    /// <summary>Creates an engine with the specified theme and multilingual table.</summary>
    /// <param name="theme">The theme to take the stylesheet and tokens from. <see cref="DvTheme.Default"/> if <see langword="null"/>.</param>
    /// <param name="strings">
    /// The multilingual string table. Used to resolve multilingual keys in bindings during layout, and to pick the default font for the language from the root element's <c>font-lang-{언어}</c> attribute.
    /// If <see langword="null"/>, neither is used.
    /// </param>
    public LayoutEngine(DvTheme? theme, Localization.DvStrings? strings)
    {
        _theme = theme;
        _strings = strings;
    }

    /// <summary>
    /// Binds the element tree to the view model to build a box tree, and arranges the root in the area (0, 0, <paramref name="width"/>, <paramref name="height"/>).
    /// </summary>
    /// <param name="root">The root element created with <see cref="MarkupParser.Parse"/>.</param>
    /// <param name="vm">The view model used to resolve binding expressions (<c>{Path}</c>). If <see langword="null"/>, bound values are treated as empty.</param>
    /// <param name="width">Width of the root area (px).</param>
    /// <param name="height">Height of the root area (px).</param>
    /// <param name="registry">User component definitions. A tag registered as a component is expanded with its template. If <see langword="null"/>, no components are used.</param>
    /// <returns>The fully arranged root box. If the root element is omitted because <c>visible</c> is false, returns a single root box with an empty style.</returns>
    /// <exception cref="InvalidOperationException">Component expansion exceeds 64 levels (a component references itself cyclically).</exception>
    public LayoutBox Layout(ElementNode root, object? vm, float width, float height,
        ComponentRegistry? registry = null)
    {
        _sheet = StyleSheet.Parse((_theme ?? DvTheme.Default).Stylesheet);
        if (registry is not null) // 컴포넌트 정의 안 DvStyle — 화면 DvStyle 보다 먼저(같은 클래스는 화면 쪽이 이김)
            foreach (var def in registry.Definitions)
                foreach (var css in def.Styles)
                    _sheet.Add(css);
        CollectStyles(root);

        var prevStrings = BindingResolver.CurrentStrings;
        BindingResolver.CurrentStrings = _strings;
        // VM 읽기 예외 알림 범위 — 서피스가 정해 두지 않았으면(직접 호출·DvRenderer) 이 레이아웃 동안만 쓰는 범위(서피스 없음 = Trace 만)
        var prevRead = HandlerErrors.CurrentRead;
        var readScope = prevRead ?? new HandlerErrors.ReadScope(null);
        HandlerErrors.EnterRead(readScope);
        var prevTag = readScope.Tag;
        try
        {
            var box = Build(root, vm, registry, 0) ?? new LayoutBox { Node = root, Style = new ComputedStyle() };

            // 루트 font-lang-{언어} = font-family 를 지정하지 않은 요소의 기본 폰트, 이후 자식에게 상속
            var langFont = _strings is { } s ? root.Attributes.GetValueOrDefault("font-lang-" + s.Language) : null;
            InheritFontFamily(box, langFont);
            InheritDisabled(box, false);
            MarkWidthOverridden(box);

            Rearrange(box, width, height);
            return box;
        }
        finally
        {
            BindingResolver.CurrentStrings = prevStrings;
            readScope.Tag = prevTag;
            HandlerErrors.ExitRead(prevRead);
        }
    }

    // 상위가 disabled 면 하위는 자기 값과 상관없이 비활성(HTML fieldset·WPF IsEnabled 와 같음)
    private static void InheritDisabled(LayoutBox box, bool inherited)
    {
        box.Disabled |= inherited;
        foreach (var c in box.Children)
            InheritDisabled(c, box.Disabled);
    }

    // 부모가 배치 때 자식의 width 를 따르지 않는 경우 표시(ArrangeCore·ArrangeWrap·ArrangeGrid·ArrangeAbsolute 규칙과 같게)
    private static void MarkWidthOverridden(LayoutBox box)
    {
        var st = box.Style;
        var grid = IsGridBox(st);
        var row = st.IsFlex && st.FlexDirection == FlexDirection.Row;
        foreach (var c in box.Children)
        {
            var cs = c.Style;
            c.WidthOverridden = cs.Position == Position.Absolute
                ? cs.Left is not null && cs.Right is not null
                : grid || (row ? cs.FlexGrow > 0 : Stretches(cs, st, col: true));
            MarkWidthOverridden(c);
        }

        // 가용 폭이 오면 자연 크기로 건너뛰지 않고 그 폭으로 잰다: width 를 덮어쓰이는 글자·컨테이너(자연 폭 = 선언 width 라
        // 실제 내용 폭을 대변하지 못함), 그런 자식에게 폭을 넘기는(폭이 확정되지 않은) 세로 배치·grid,
        // 그리고 부모가 폭을 정하는(늘어나는) grid — %·fr 칸이 배치 폭으로 커지므로 자연 폭으로 재면 행이 더 높게 나온다
        var passes = grid || !st.IsFlex || (st.FlexDirection == FlexDirection.Column && !st.Wrap);
        box.MeasureAtAvail = box.Grid is null &&
            ((box.WidthOverridden && st.Width is not null &&
              (box.ResolvedText is not null ? st.NoWrap != true && !IconFont.TryResolve(box.Icon, out _) : box.Children.Count > 0)) ||
             (passes && FixedWidth(box) is null && box.Children.Any(c => c.MeasureAtAvail && c.Style.Position != Position.Absolute)) ||
             (grid && box.WidthOverridden));
    }

    // 글자 관련 상속 — font-family 와 white-space(CSS 처럼 둘 다 상속)
    private static void InheritFontFamily(LayoutBox box, string? inherited, bool? inheritedNoWrap = null)
    {
        var family = box.Style.FontFamily ??= inherited;
        var noWrap = box.Style.NoWrap ??= inheritedNoWrap;
        foreach (var st in new[] { box.StyleHover, box.StylePress, box.StyleDisabled })
        {
            if (st is not null)
            {
                st.FontFamily ??= family;
                st.NoWrap ??= noWrap;
            }
        }

        foreach (var c in box.Children)
            InheritFontFamily(c, family, noWrap);
    }

    private void CollectStyles(ElementNode node)
    {
        foreach (var ch in node.Children)
        {
            if (ch.Tag == "DvStyle")
                _sheet.Add(ch.Text);
            else
                CollectStyles(ch);
        }
    }

    private LayoutBox? Build(ElementNode node, object? vm, ComponentRegistry? registry, int depth)
    {
        SetReadTag(node);
        if (node.Tag is "DvStyle" or "DvTrendChannel" or "DvOption" or "DvColumn" or "DvColumnGroup" or "DvSummaryRow" or "DvSummary" ||
            !IsVisibleOf(node, vm))
            return null;

        if (TryGetComponent(registry, node.Tag, out var def))
            return ExpandComponent(node, def, vm, registry, depth,
                child => Build(child, vm, registry, depth) is { } b ? new[] { b } : Array.Empty<LayoutBox>());

        if (node.Tag == "DvRepeat")
            return BuildRepeat(node, vm,
                (ch, scope) => Build(ch, scope, registry, depth) is { } b ? new[] { b } : Array.Empty<LayoutBox>());

        if (node.Tag == "DvTabs")
            return BuildTabs(BuildSelf(node, vm), vm,
                ch => Build(ch, vm, registry, depth) is { } b ? new[] { b } : Array.Empty<LayoutBox>());

        if (node.Tag == "DvExpander")
            return BuildExpander(BuildSelf(node, vm), vm,
                ch => Build(ch, vm, registry, depth) is { } b ? new[] { b } : Array.Empty<LayoutBox>());

        if (node.Tag == "DvProcess")
        {
            var proc = BuildSelf(node, vm);
            Process.ProcessBuilder.Build(proc, vm, registry, ProcessBuilders(registry, depth));
            return proc;
        }

        var box = BuildSelf(node, vm);

        foreach (var ch in node.Children)
        {
            if (Build(ch, vm, registry, depth) is { } child)
                box.Children.Add(child);
        }
        AddSegmentItems(box, vm);
        AddListItems(box, vm);
        AddTreeItems(box, vm);

        return box;
    }

    // 공정도가 장비·유닛·붙은 칸 상자를 만들 때 쓰는 방법 — 문맥(화면 VM 또는 유닛 컴포넌트 파라미터 범위)을 받는다
    private Process.ProcessBuilder.Builders ProcessBuilders(ComponentRegistry? registry, int depth) => new(
        (n, ctx) => BuildSelf(n, ctx),
        (n, ctx) => Build(n, ctx, registry, depth + 1),
        (n, ctx) => ExpandUnit(n, ctx, registry));

    // 템플릿 루트가 DvUnit 인 컴포넌트 인스턴스 → (유닛 노드, 파라미터 범위). 아니면 null
    private static (ElementNode Unit, object? Scope)? ExpandUnit(ElementNode instance, object? outerVm, ComponentRegistry? registry)
    {
        if (!TryGetComponent(registry, instance.Tag, out var def) || def.Template.Tag != "DvUnit")
            return null;
        var scope = new ComponentScope();
        foreach (var p in def.Params)
            scope[p.Name] = TryGetAttribute(instance, p.Name, out var raw) ? BindingResolver.ResolveValueOrLiteral(outerVm, raw) : p.Default;
        return (def.Template, scope);
    }

    /// <summary>The selected index of a DvTabs without <c>value</c> (remembered by the screen, so it survives rebuilds). 0 if none.</summary>
    internal Func<ElementNode, int?>? TabIndexOf { get; set; }

    // DvTabs — [머리글 줄(.tab-header: 탭마다 .tab / 선택 .tab-on) · 구분선(.tab-rule) · 선택된 DvTab 내용(.tab-body)].
    // 선택: value 가 있으면 VM 값과 맞는 탭(없으면 첫 탭), 없으면 화면이 기억한 번호. 나머지 탭 내용은 만들지 않는다.
    // 머리글을 누르면 DvSurface 가 그 탭 value 를 쓰거나(양방향) 기억한 번호를 바꾼다(value 없음).
    private LayoutBox BuildTabs(LayoutBox box, object? vm, Func<ElementNode, IEnumerable<LayoutBox>> buildChild)
    {
        var tabs = box.Node.Children.Where(c => c.Tag == "DvTab" && IsVisibleOf(c, vm)).ToList();
        var bound = box.Node.Attributes.ContainsKey("value");
        box.SelectOptions = tabs.Select(t =>
        {
            var raw = BindingResolver.ResolveText(vm, t.Attributes.GetValueOrDefault("value"));
            var value = SelectValue.TryConvert(raw, box.SelectValueType, out var converted) ? converted : raw;
            return new SelectOption(value, BindingResolver.ResolveText(vm, t.Attributes.GetValueOrDefault("text")) ?? raw ?? "", null);
        }).ToList();
        var selected = bound
            ? box.SelectOptions.FindIndex(o => SelectValue.Matches(o.Value, box.InputValue, box.SelectValueType))
            : TabIndexOf?.Invoke(box.Node) ?? 0;
        if (selected < 0 || selected >= tabs.Count)
            selected = 0;
        box.SelectedIndex = tabs.Count == 0 ? -1 : selected;

        var header = BuildSelf(new ElementNode { Tag = "DvTabHeader", Attributes = { ["class"] = "tab-header" } }, vm);
        for (var i = 0; i < tabs.Count; i++)
        {
            var item = new ElementNode { Tag = "DvTabItem" };
            item.Attributes["class"] = i == box.SelectedIndex ? "tab tab-on" : "tab";
            if (tabs[i].Attributes.TryGetValue("disabled", out var dis))
                item.Attributes["disabled"] = dis;
            var itemBox = BuildSelf(item, vm);
            itemBox.ResolvedText = box.SelectOptions[i].Text;
            itemBox.SegmentOwner = box;
            itemBox.SegmentIndex = i;
            header.Children.Add(itemBox);
        }
        box.Children.Add(header);
        box.Children.Add(BuildSelf(new ElementNode { Tag = "DvTabRule", Attributes = { ["class"] = "tab-rule" } }, vm));

        if (box.SelectedIndex >= 0)
        {
            var tab = tabs[box.SelectedIndex];
            var body = BuildSelf(tab, vm);
            foreach (var ch in tab.Children)
                body.Children.AddRange(buildChild(ch));
            box.Children.Add(body);
        }
        return box;
    }

    /// <summary>
    /// Charts (single series): DvSparkline uses <c>values</c> (a list of numbers or "1,2,3"); DvBarChart and DvPieChart use <c>items</c> + <c>item-value</c> (number) + <c>item-text</c> (name).
    /// Common: <c>tone</c> (info), <c>show-value</c>; sparkline and bar: <c>y-min</c>/<c>y-max</c> (omitted = automatic); sparkline: <c>area</c>; pie: <c>inner-radius</c> (ratio 0 to 0.9, donut).
    /// Values that cannot be read as numbers are 0 (pie charts do not draw negative or zero slices).
    /// </summary>
    private void BuildChart(LayoutBox box, ElementNode node, object? vm)
    {
        string? Attr(string name) => BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault(name));
        double? Num(string name) => double.TryParse(Attr(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        static double ToNum(object? v) => v switch
        {
            null => 0,
            IConvertible c when v is not string => SafeDouble(c),
            _ => double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0,
        };

        box.Tone = Attr("tone") is { Length: > 0 } tone ? tone : "info";
        box.ShowValue = ToBool(Attr("show-value"));
        box.ChartYMin = Num("y-min");
        box.ChartYMax = Num("y-max");
        box.ChartArea = ToBool(Attr("area"));
        box.ChartInner = Math.Clamp((float)(Num("inner-radius") ?? 0), 0f, 0.9f);

        if (node.Tag == "DvSparkline")
        {
            var raw = node.Attributes.GetValueOrDefault("values");
            box.ChartValues = BindingResolver.ResolveValueOrLiteral(vm, raw) switch
            {
                string text => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(t => ToNum(t)).ToArray(),
                System.Collections.IEnumerable list => HandlerErrors.Snapshot(list.Cast<object?>(), PathOf(raw), node.Tag).Select(ToNum).ToArray(),
                _ => Array.Empty<double>(),
            };
            box.ChartLabels = Array.Empty<string>();
            return;
        }

        var itemsRaw = node.Attributes.GetValueOrDefault("items");
        var items = BindingResolver.ResolveValueOrLiteral(vm, itemsRaw) is System.Collections.IEnumerable e and not string
            ? HandlerErrors.Snapshot(e.Cast<object?>(), PathOf(itemsRaw), node.Tag)
            : new List<object?>();
        var valuePath = node.Attributes.GetValueOrDefault("item-value");
        var textPath = node.Attributes.GetValueOrDefault("item-text");
        box.ChartValues = items.Select(it => ToNum(valuePath is null ? it : BindingResolver.Resolve(it, $"{{{valuePath}}}"))).ToArray();
        box.ChartLabels = items.Select(it => HandlerErrors.Text(textPath is null ? it : BindingResolver.Resolve(it, $"{{{textPath}}}"), textPath, node.Tag) ?? "").ToArray();
    }

    private static double SafeDouble(IConvertible c)
    {
        try { return c.ToDouble(CultureInfo.InvariantCulture); }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException) { return 0; }
    }

    /// <summary>The expanded state of a DvExpander without <c>expanded</c> (remembered by the screen, so it survives rebuilds). Collapsed if none.</summary>
    internal Func<ElementNode, bool?>? ExpanderOf { get; set; }

    /// <summary>
    /// Expand animation: given (element, repeat item, currently expanded), returns the currently visible ratio (0 to 1) while animating, otherwise null.
    /// The screen starts the animation if the state changed compared with the previous one. Content is still built while collapsing (visible according to the ratio).
    /// </summary>
    internal Func<ElementNode, object?, bool, float?>? ExpanderRevealOf { get; set; }

    // DvExpander — [제목 줄(.expander-header: ▶/▼ 아이콘 + header 글자) · 펼쳤으면 내용(.expander-body)]. 접으면 내용은 만들지 않는다(탭과 같음).
    // 펼침: expanded 가 양방향이면 VM 값, 단방향·고정이면 그 값(핸들이 덮어쓰면 그 값 — 제목을 눌러도 안 바뀜), 없으면 화면이 기억.
    // 제목을 누르면 DvSurface 가 뒤집어 쓴다.
    private LayoutBox BuildExpander(LayoutBox box, object? vm, Func<ElementNode, IEnumerable<LayoutBox>> buildChild)
    {
        var node = box.Node;
        var raw = node.Attributes.GetValueOrDefault("expanded");
        var (expr, _, ov) = ValueOf(node, raw);
        box.ValueBindExpr = expr;
        box.ToggleOn = ov is not null ? ToBool(ov.CellValue)
            : expr is not null && BindingResolver.IsTwoWay(expr) ? ToBool(BindingResolver.Resolve(vm, $"{{{BindingResolver.ExtractPath(expr)}}}"))
            : raw is not null ? ToBool(BindingResolver.ResolveValueOrLiteral(vm, raw))
            : ExpanderOf?.Invoke(node) ?? false;

        var header = BuildSelf(new ElementNode { Tag = "DvExpanderHeader", Attributes = { ["class"] = "expander-header" } }, vm);
        header.ResolvedText = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("header")) ?? "";
        header.Icon = box.ToggleOn ? "fa-chevron-down" : "fa-chevron-right";
        header.ExpanderOwner = box;
        if (box.Disabled)
            header.Disabled = true;
        box.Children.Add(header);

        var item = (vm as ItemScope)?.Item;
        var reveal = ExpanderRevealOf?.Invoke(node, item, box.ToggleOn);
        if (box.ToggleOn || reveal is not null)
        {
            var body = BuildSelf(new ElementNode { Tag = "DvExpanderBody", Attributes = { ["class"] = "expander-body" } }, vm);
            foreach (var ch in node.Children)
                body.Children.AddRange(buildChild(ch));
            if (reveal is not null)
            {
                body.Reveal = reveal;
                body.Style.Overflow = Overflow.Hidden; // 보이는 높이 밖은 잘림(움직임이 끝나면 다시 만들어 원래대로)
            }
            body.RevealKey = (node, item);
            box.Children.Add(body);
        }
        return box;
    }

    // DvSegment — 선택지(DvOption)마다 항목 상자(DvSegmentItem, .segment-item / 선택된 것 .segment-item-on)를 만들어 한 줄로 붙인다.
    // 누르면 그 선택지 값을 세그먼트의 value 에 쓴다(DvSurface — DvSelect 와 같은 커밋 경로).
    private void AddSegmentItems(LayoutBox box, object? vm)
    {
        if (box.Node.Tag != "DvSegment")
            return;
        for (var i = 0; i < box.SelectOptions.Count; i++)
        {
            var item = new ElementNode { Tag = "DvSegmentItem" };
            item.Attributes["class"] = i == box.SelectedIndex ? "segment-item segment-item-on" : "segment-item";
            var itemBox = BuildSelf(item, vm);
            itemBox.ResolvedText = box.SelectOptions[i].Text; // 글자는 그대로(바인딩 해석 없이)
            itemBox.Icon = box.SelectOptions[i].Icon;
            itemBox.SegmentOwner = box;
            itemBox.SegmentIndex = i;
            box.Children.Add(itemBox);
        }
    }

    // DvListBox — 선택지마다 행 상자(DvListItem, .listbox-item / 고른 것 .listbox-item-on)를 세로로 쌓는다. 목록 상자는 스크롤 패널(.listbox).
    // 누르면 DvSurface 가 고른다(한 개 = value, 여럿 = selected-items 토글).
    private void AddListItems(LayoutBox box, object? vm)
    {
        if (box.Node.Tag != "DvListBox")
            return;
        for (var i = 0; i < box.SelectOptions.Count; i++)
        {
            var on = box.ListSelected is { } sel && i < sel.Length && sel[i];
            var item = new ElementNode { Tag = "DvListItem" };
            item.Attributes["class"] = on ? "listbox-item listbox-item-on" : "listbox-item";
            var itemBox = BuildSelf(item, vm);
            itemBox.ResolvedText = box.SelectOptions[i].Text;
            itemBox.Icon = box.SelectOptions[i].Icon;
            itemBox.ListOwner = box;
            itemBox.ListIndex = i;
            if (box.Disabled)
                itemBox.Disabled = true;
            box.Children.Add(itemBox);
        }
    }

    /// <summary>The expanded items of a <c>DvTreeView</c> (remembered by the screen, keyed by element node + item object). Collapsed if absent.</summary>
    internal Func<ElementNode, object, bool>? TreeExpandedOf { get; set; }

    /// <summary>
    /// <c>DvTreeView</c>: <c>items</c> (root list) + <c>item-children</c> (path to the child list) + <c>item-text</c> + <c>item-value</c> (omitted = the item itself).
    /// Only expanded branches become rows (row height 40, indented per depth). Single selection: <c>value</c> holds the selected item's value (value ownership rules are the same as other value widgets).
    /// Expansion is remembered by the screen (per item object). Cyclic references (the same object as an ancestor appearing again) skip that branch, and depth is limited to 64.
    /// </summary>
    private void BuildTreeView(LayoutBox box, ElementNode node, object? vm)
    {
        var raw = node.Attributes.GetValueOrDefault("value");
        var (expr, target, ov) = ValueOf(node, raw);
        box.ValueBindExpr = expr;
        box.ValueTarget = target;
        var ctx = target ?? vm;
        var twoWay = BindingResolver.IsTwoWay(expr);
        object? current;
        if (ov is not null)
            current = ov.CellValue;
        else if (twoWay)
        {
            var path = BindingResolver.ExtractPath(expr!);
            current = BindingResolver.Resolve(ctx, $"{{{path}}}");
            box.SelectValueType = BindingResolver.ResolvePropertyType(ctx, path) is { } t && t != typeof(object) ? t : null;
        }
        else
            current = BindingResolver.ResolveValueOrLiteral(vm, raw);
        box.InputValue = current;
        box.ReadOnly = !(twoWay || target is not null) ||
                       ToBool(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("readonly")));

        var itemsRaw = node.Attributes.GetValueOrDefault("items");
        var textPath = node.Attributes.GetValueOrDefault("item-text");
        var valuePath = node.Attributes.GetValueOrDefault("item-value");
        var childPath = node.Attributes.GetValueOrDefault("item-children");
        var rows = new List<TreeRow>();
        var all = new List<TreeRow>();

        var ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance); // 지금 가지의 조상들 — 순환 끊기
        void Walk(System.Collections.IEnumerable items, int depth, object? parent, bool visible)
        {
            foreach (var item in HandlerErrors.Snapshot(items.Cast<object?>(), PathOf(itemsRaw), node.Tag))
            {
                if (item is null || ancestors.Contains(item))
                    continue;
                var kids = childPath is null || depth >= 64
                    ? null
                    : BindingResolver.Resolve(item, $"{{{childPath}}}") as System.Collections.IEnumerable;
                var kidList = kids is null or string ? null : HandlerErrors.Snapshot(kids.Cast<object?>(), childPath, node.Tag);
                var hasKids = kidList is { Count: > 0 };
                var open = hasKids && TreeExpandedOf?.Invoke(node, item) == true;
                var row = new TreeRow(item,
                    valuePath is null ? item : BindingResolver.Resolve(item, $"{{{valuePath}}}"),
                    HandlerErrors.Text(textPath is null ? item : BindingResolver.Resolve(item, $"{{{textPath}}}"), textPath, node.Tag) ?? "",
                    parent, depth, hasKids, open);
                all.Add(row);
                if (visible)
                    rows.Add(row);
                if (hasKids)
                {
                    ancestors.Add(item);
                    Walk(kidList!, depth + 1, item, visible && open);
                    ancestors.Remove(item);
                }
            }
        }

        if (BindingResolver.ResolveValueOrLiteral(vm, itemsRaw) is System.Collections.IEnumerable roots and not string)
            Walk(roots, 0, null, true);
        box.TreeRows = rows;
        box.TreeAll = all;
        box.SelectedIndex = rows.FindIndex(r => SelectValue.Matches(r.Value, current, box.SelectValueType));
    }

    // DvTreeView 행(DvTreeItem, .treeview-item / 고른 것 .treeview-item-on) — 깊이만큼 들여쓰고, 가지는 ▶/▼ 아이콘, 잎은 아이콘 자리만큼 비운다.
    private void AddTreeItems(LayoutBox box, object? vm)
    {
        if (box.Node.Tag != "DvTreeView")
            return;
        for (var i = 0; i < box.TreeRows.Count; i++)
        {
            var row = box.TreeRows[i];
            var item = new ElementNode { Tag = "DvTreeItem" };
            item.Attributes["class"] = i == box.SelectedIndex ? "treeview-item treeview-item-on" : "treeview-item";
            var indent = TreeIndentBase + row.Depth * TreeIndentStep + (row.HasChildren ? 0 : TreeChevron + IconTextGap);
            item.Attributes["style"] = "padding:0 12 0 " + indent.ToString(CultureInfo.InvariantCulture);
            var itemBox = BuildSelf(item, vm);
            itemBox.ResolvedText = row.Text;
            itemBox.Icon = row.HasChildren ? (row.Open ? "fa-chevron-down" : "fa-chevron-right") : null;
            itemBox.TreeOwner = box;
            itemBox.TreeIndex = i;
            if (box.Disabled)
                itemBox.Disabled = true;
            box.Children.Add(itemBox);
        }
    }

    /// <summary>Tree row left padding, indentation per depth, and expand icon width (px).</summary>
    internal const float TreeIndentBase = 10f, TreeIndentStep = 20f, TreeChevron = 14f;

    /// <summary>
    /// <c>DvListBox</c> value: single selection uses <c>value</c> (same as DvSelect); with <c>selection-mode="multi"</c>, <c>selected-items</c> (a list of values).
    /// Value ownership rules are the same as other value widgets (binding = VM, no attribute + handle = handle slot, one-way/literal = read-only).
    /// </summary>
    private void BuildListBox(LayoutBox box, ElementNode node, object? vm)
    {
        box.ListMulti = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("selection-mode")) == "multi";
        var attr = box.ListMulti ? "selected-items" : "value";
        var raw = node.Attributes.GetValueOrDefault(attr);
        var (expr, target, ov) = ValueOf(node, raw);
        box.ValueBindExpr = expr;
        box.ValueTarget = target;
        var ctx = target ?? vm;
        var twoWay = BindingResolver.IsTwoWay(expr);
        object? current;
        Type? valueType;
        if (ov is not null)
        {
            current = ov.CellValue;
            valueType = null;
        }
        else if (twoWay)
        {
            var path = BindingResolver.ExtractPath(expr!);
            current = BindingResolver.Resolve(ctx, $"{{{path}}}");
            valueType = BindingResolver.ResolvePropertyType(ctx, path);
        }
        else
        {
            current = BindingResolver.ResolveValueOrLiteral(vm, raw);
            valueType = current?.GetType();
        }

        if (box.ListMulti)
        {
            // 목록 속성 — 선택지 값은 요소 형식으로 바꿔 비교
            var elem = valueType is { } vt && vt != typeof(string) ? Interaction.GridInteraction.ElementType(Nullable.GetUnderlyingType(vt) ?? vt) : null;
            box.SelectValueType = elem == typeof(object) ? null : elem;
            var picked = current is System.Collections.IEnumerable e and not string
                ? HandlerErrors.Snapshot(e.Cast<object?>(), PathOf(raw), node.Tag)
                : new List<object?>();
            box.SelectOptions = SelectValue.Options(node, vm, box.SelectValueType);
            box.ListSelected = box.SelectOptions.Select(o => picked.Any(c => SelectValue.Matches(o.Value, c, box.SelectValueType))).ToArray();
            box.InputValue = picked;
        }
        else
        {
            box.SelectValueType = valueType == typeof(object) ? null : valueType;
            box.SelectOptions = SelectValue.Options(node, vm, box.SelectValueType);
            box.SelectedIndex = box.SelectOptions.FindIndex(o => SelectValue.Matches(o.Value, current, box.SelectValueType));
            box.ListSelected = box.SelectOptions.Select((_, i) => i == box.SelectedIndex).ToArray();
            box.InputValue = current;
        }

        box.ReadOnly = !(twoWay || target is not null) ||
                       ToBool(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("readonly")));
    }

    /// <summary>
    /// DvRepeat: for each item in <c>items</c>, builds the children (template) in that item's scope (<see cref="ItemScope"/>).
    /// Empty, null, or not a collection = no children (height 0). Strings are not treated as collections.
    /// </summary>
    private LayoutBox BuildRepeat(ElementNode node, object? ctx, Func<ElementNode, object?, IEnumerable<LayoutBox>> buildChild)
    {
        var box = BuildSelf(node, ctx);
        var itemsRaw = node.Attributes.GetValueOrDefault("items");
        if (BindingResolver.ResolveValueOrLiteral(ctx, itemsRaw) is not System.Collections.IEnumerable items || items is string)
            return box;

        // 먼저 끝까지 읽는다 — 열거 중 VM 예외면 항목 없음(일부만 만든 화면을 남기지 않음)
        var index = 0;
        foreach (var item in HandlerErrors.Snapshot(items.Cast<object?>(), PathOf(itemsRaw), node.Tag))
        {
            var scope = new ItemScope(ctx, item, index++);
            foreach (var ch in node.Children)
                box.Children.AddRange(buildChild(ch, scope));
        }

        return box;
    }

    private LayoutBox BuildSelf(ElementNode node, object? vm)
    {
        // cascade: 레이아웃 슈가 < 클래스 < 클래스:상태 < content-align 속성 < inline style < inline *-style
        // (content-align 속성은 슈가와 달리 클래스를 이긴다 — 테마 .btn 기본값을 요소에서 바꿀 수 있게)
        var sugar = ResolveStyle(LayoutSugar.Decls(node, vm), vm);
        var rules = Classes(node, vm, _valueOverride).Select(c => _sheet.TryGet(c, out var r) ? r : null).OfType<ClassRule>().ToList();
        var classBase = ResolveStyle(StyleSheet.Join(rules.Select(r => r.Base).ToArray()), vm);
        var attrDecls = node.Attributes.TryGetValue("content-align", out var ca) &&
                        BindingResolver.ResolveText(vm, ca) is { Length: > 0 } caValue
            ? "content-align:" + caValue
            : null;
        var inline = ResolveStyle(node.Attributes.GetValueOrDefault("style"), vm);
        var baseDecls = StyleSheet.Join(sugar, classBase, attrDecls, inline);
        var style = StyleParser.Parse(baseDecls);
        var box = new LayoutBox
        {
            Node = node, Style = style, ItemScope = vm as ItemScope ?? _rootItemScope,
            ComponentScope = vm as ComponentScope ?? (vm as ItemScope)?.Outer as ComponentScope,
        };
        _rootItemScope = null;

        ComputedStyle? State(Func<ClassRule, string?> pick, string attr)
        {
            var cls = ResolveStyle(StyleSheet.Join(rules.Select(pick).ToArray()), vm);
            var own = node.Attributes.TryGetValue(attr, out var raw) ? ResolveStyle(raw, vm) : null;
            if (cls is null && own is null)
                return null;
            return StyleParser.Parse(StyleSheet.Join(sugar, classBase, cls, attrDecls, inline, own));
        }

        box.StyleHover = State(r => r.Hover, "hover-style");
        box.StylePress = State(r => r.Press, "press-style");
        box.StyleDisabled = State(r => r.Disabled, "disabled-style");
        if (node.Attributes.TryGetValue("disabled", out var dv))
        {
            var rv = BindingResolver.ResolveValueOrLiteral(vm, dv);
            box.Disabled = string.Equals(BindingResolver.ToText(rv, dv), "true", StringComparison.OrdinalIgnoreCase);
        }

        var ov = ControlOf?.Invoke(node)?.Overrides;
        if (ov?.Enabled is { } en)
            box.Disabled = !en;
        box.HasCodeClick = ov?.HasClick == true;

        box.OnClick = node.Attributes.GetValueOrDefault("onclick");
        if (BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("hold")) is { } holdText &&
            int.TryParse(holdText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var holdMs) && holdMs > 0)
            box.HoldMs = holdMs;
        box.OnBeforeCommit = node.Attributes.GetValueOrDefault("onbeforecommit");

        if (node.Tag == "DvButton" && node.Attributes.TryGetValue("value", out var latchExpr))
        {
            // 래칭 버튼 — 켜짐 = value 가 참. 양방향이면 누를 때 뒤집어 쓴다(DvToggle 과 같은 커밋 경로)
            var (lx, _, lov) = ValueOf(node, latchExpr);
            box.ValueBindExpr = _valueOverride ?? lx;
            box.ToggleOn = lov is not null ? ToBool(lov.CellValue) : LatchOn(node, vm);
        }

        if (node.Tag == "DvRadio")
            BuildRadio(box, node, vm);

        if (node.Tag == "DvListBox")
            BuildListBox(box, node, vm);

        if (node.Tag == "DvTreeView")
            BuildTreeView(box, node, vm);

        if (node.Tag is "DvSparkline" or "DvBarChart" or "DvPieChart")
            BuildChart(box, node, vm);

        if (node.Tag == "DvStepBar")
        {
            // steps = 목록(항목 글자) 또는 "가,나,다"(쉼표로 나눔), current = 1부터(0 = 시작 전, 개수보다 크면 모두 끝)
            var stepsRaw = node.Attributes.GetValueOrDefault("steps");
            box.StepNames = BindingResolver.ResolveValueOrLiteral(vm, stepsRaw) switch
            {
                string text => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                System.Collections.IEnumerable list => HandlerErrors.Snapshot(list.Cast<object?>(), PathOf(stepsRaw), node.Tag)
                    .Select(o => HandlerErrors.Text(o, PathOf(stepsRaw), node.Tag) ?? "").ToArray(),
                _ => Array.Empty<string>(),
            };
            box.StepCurrent = (int)MathF.Round(ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("current")), 0f));
        }

        if (node.Tag is "DvToggle" or "DvCheckBox")
        {
            var (ve, target, vov) = ValueOf(node, node.Attributes.GetValueOrDefault("value"));
            box.ValueBindExpr = ve;
            box.ValueTarget = target;
            object? cur = null;
            if (vov is not null)
                cur = vov.CellValue;
            else if (ve is not null && BindingResolver.IsTwoWay(ve))
                cur = BindingResolver.Resolve(target ?? vm, $"{{{BindingResolver.ExtractPath(ve)}}}");
            else
                cur = BindingResolver.ResolveValueOrLiteral(vm, ve);
            box.ToggleOn = cur is bool b && b ||
                           string.Equals(BindingResolver.ToText(cur, ve), "true", StringComparison.OrdinalIgnoreCase);
        }

        if (node.Tag == "DvLamp")
        {
            box.LampOn = ToBool(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("on")));
            var blinkExpr = node.Attributes.GetValueOrDefault("blink-when")
                            ?? node.Attributes.GetValueOrDefault("blink");
            box.LampBlink = ToBool(BindingResolver.ResolveValueOrLiteral(vm, blinkExpr));
            if (BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("blink-interval")) is { } bi &&
                int.TryParse(bi, out var bint) && bint > 0)
                box.BlinkIntervalMs = bint;
            if (ParseColor(BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("color"))) is { } lampColor)
                box.LampColor = lampColor;
            box.LampState = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("state"));
        }

        if (node.Tag is "DvGauge" or "DvRing" or "DvProgress")
        {
            box.GaugeValue = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("value")), 0f);
            box.GaugeMin = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("min")), 0f);
            box.GaugeMax = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("max")), 100f);
            box.GaugeZones = ParseZones(node, vm);
            box.GaugeTicks = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("ticks")), 0f);
            box.GaugeMinorTicks = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("minor-ticks")), 0f);
            if (node.Attributes.TryGetValue("show-tick-labels", out var tl))
                box.GaugeTickLabels = !string.Equals(
                    BindingResolver.ResolveText(vm, tl), "false", StringComparison.OrdinalIgnoreCase);

            string? Attr(string name) => BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault(name));
            box.Tone = Attr("tone") is { Length: > 0 } tone ? tone : "info";
            box.Direction = Attr("direction") is { Length: > 0 } dir ? dir : "right";
            box.ShowValue = ToBool(Attr("show-value"));
            box.Unit = Attr("unit");
            box.Label = Attr("label");
            var sweep = ToFloat(Attr("sweep"), 270f);
            box.RingSweep = Math.Clamp(sweep, 1f, 360f);
            if (Attr("thickness") is { Length: > 0 } th)
                box.RingThickness = ToFloat(th, 0f) is > 0 and var t ? t : null;
        }

        if (node.Tag == "DvTrend")
            box.Trend = BuildTrend(node, vm);

        if (node.Tag == "DvDataGrid")
            box.Grid = Grid.GridBuilder.Build(node, vm, _theme, GridClassStyle);

        if (node.Tag is "DvAlarmBanner" or "DvAlarmList")
        {
            // 레이아웃 시점 스냅샷 — VM 변경은 host 의 Invalidate 후 반영
            var alarmsRaw = node.Attributes.GetValueOrDefault("items");
            box.AlarmItems = BindingResolver.ResolveValueOrLiteral(vm, alarmsRaw)
                is IEnumerable<Alarms.AlarmRow> items ? HandlerErrors.Snapshot(items, PathOf(alarmsRaw), node.Tag) : Array.Empty<Alarms.AlarmRow>();
            box.CommAlarm = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("comm-alarm"));
            box.OnAck = node.Attributes.GetValueOrDefault("onack");
            // show-ack: 지정 시 그 값(바인딩 가능 — 권한별 표시), 미지정 시 onack 이 있을 때만 표시
            box.AlarmShowAck = node.Attributes.TryGetValue("show-ack", out var sa)
                ? !string.Equals(BindingResolver.ResolveText(vm, sa), "false",
                    StringComparison.OrdinalIgnoreCase)
                : !string.IsNullOrEmpty(box.OnAck);
            var rh = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("row-height")), 36f);
            box.AlarmRowHeight = rh > 0 ? rh : 36f;
        }

        if (node.Tag == "DvImage")
        {
            box.ImageSrc = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("src"));
            box.ImageFit = Widgets.ImageFitParser.Parse(
                BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("fit")));
        }

        if (node.Tag is "DvKnob" or "DvSlider")
        {
            var (ve, target, vov) = ValueOf(node, node.Attributes.GetValueOrDefault("value"));
            box.ValueBindExpr = ve;
            box.ValueTarget = target;
            var cur = vov is not null ? vov.CellValue
                : ve is not null && BindingResolver.IsTwoWay(ve)
                ? BindingResolver.Resolve(target ?? vm, $"{{{BindingResolver.ExtractPath(ve)}}}")
                : BindingResolver.ResolveValueOrLiteral(vm, ve);
            box.KnobValue = ToFloat(cur, 0f);
            box.KnobMin = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("min")), 0f);
            box.KnobMax = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("max")), 100f);
            var step = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("step")), 1f);
            box.KnobStep = step > 0 ? step : 1f;
            box.KnobUnit = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("unit"));
            if (node.Tag == "DvKnob")
            {
                box.KnobInput = node.Attributes.GetValueOrDefault("input-mode") switch
                {
                    null or "drag" => KnobInputMode.Drag,
                    "tap" => KnobInputMode.Tap,
                    "both" => KnobInputMode.Both,
                    var mode => WarnKnobMode(mode),
                };
            }
            else
            {
                // 슬라이더 — direction = 값이 커지는 쪽(DvProgress 와 같은 값), show-value = 손잡이 옆 값 글자
                var dir = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("direction"));
                box.Direction = dir is "right" or "left" or "up" or "down" ? dir : WarnSliderDirection(dir);
                box.ShowValue = ToBool(BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("show-value")));
            }
        }

        if (node.Tag == "DvInput")
        {
            string? Attr(string name) => BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault(name));
            double? Num(string name) => double.TryParse(Attr(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d : null;

            var (ve, target, vov) = ValueOf(node, node.Attributes.GetValueOrDefault("value"));
            box.ValueBindExpr = ve;
            box.ValueTarget = target;
            box.InputValue = vov is not null ? vov.CellValue
                : ve is not null && BindingResolver.IsTwoWay(ve)
                ? BindingResolver.Resolve(target ?? vm, $"{{{BindingResolver.ExtractPath(ve)}}}")
                : BindingResolver.ResolveValueOrLiteral(vm, ve);
            box.InputKind = Attr("keyboard-type") switch
            {
                "numeric" => DvEditKind.Numeric,
                "password" => DvEditKind.Password,
                _ => DvEditKind.Text // email 등은 text
            };
            box.InputMultiline = ToBool(Attr("multiline"));
            box.InputPlaceholder = Attr("placeholder");
            box.InputFormat = Attr("format");
            box.InputMin = Num("min");
            box.InputMax = Num("max");
            box.Unit = Attr("unit");
            box.ReadOnly = InputReadOnly(node, vm, _valueOverride);
            box.InputStep = Num("step") is > 0 and var step ? step : 1d;
            if (box.InputKind == DvEditKind.Numeric && ToBool(Attr("show-stepper")))
            {
                // −/+ 자리 — 좌우 padding 에 버튼 폭을 더해 글자·커서·네이티브 편집기 위치가 그대로 맞게 한다
                box.InputStepper = true;
                foreach (var s in new[] { box.Style, box.StyleHover, box.StylePress, box.StyleDisabled })
                {
                    if (s is null)
                        continue;
                    var p = s.Padding;
                    s.Padding = new Thickness(p.Top, p.Right + InputDisplay.StepperWidth, p.Bottom, p.Left + InputDisplay.StepperWidth);
                }
            }
        }

        if (node.Tag is "DvSelect" or "DvSegment" or "DvTabs")
        {
            var ve = node.Attributes.GetValueOrDefault("value");
            var (sx, target, vov) = ValueOf(node, ve);
            box.ValueBindExpr = _valueOverride ?? sx;
            box.ValueTarget = target;
            var twoWay = BindingResolver.IsTwoWay(box.ValueBindExpr);
            var ctx = target ?? vm;
            object? current;
            if (vov is not null)
            {
                current = vov.CellValue;
                box.SelectValueType = current?.GetType();
            }
            else if (twoWay)
            {
                var path = BindingResolver.ExtractPath(box.ValueBindExpr!);
                // 컴포넌트 양방향 파라미터는 바깥 VM 경로 — 값은 템플릿 범위({p})로 읽는다
                current = _valueOverride is not null ? BindingResolver.ResolveValueOrLiteral(vm, ve) : BindingResolver.Resolve(ctx, $"{{{path}}}");
                box.SelectValueType = _valueOverride is null ? BindingResolver.ResolvePropertyType(ctx, path) : current?.GetType();
            }
            else
            {
                current = BindingResolver.ResolveValueOrLiteral(vm, ve);
                box.SelectValueType = current?.GetType();
            }

            box.InputValue = current;
            box.SelectOptions = SelectValue.Options(node, vm, box.SelectValueType);
            box.SelectedIndex = box.SelectOptions.FindIndex(o => SelectValue.Matches(o.Value, current, box.SelectValueType));
            box.InputPlaceholder = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("placeholder"));
            box.ReadOnly = InputReadOnly(node, vm, _valueOverride);
        }

        if (node.Tag is "DvDateTimeInput" or "DvColorInput")
        {
            var ve = node.Attributes.GetValueOrDefault("value");
            var (px, target, vov) = ValueOf(node, ve);
            box.ValueBindExpr = _valueOverride ?? px;
            box.ValueTarget = target;
            var ctx = target ?? vm;
            object? current;
            if (vov is not null)
            {
                current = vov.CellValue;
                box.PickerValueType = vov.CellType;
            }
            else if (BindingResolver.IsTwoWay(box.ValueBindExpr))
            {
                var path = BindingResolver.ExtractPath(box.ValueBindExpr!);
                // 컴포넌트 양방향 파라미터는 바깥 VM 경로 — 값은 템플릿 범위({p})로 읽는다
                current = _valueOverride is not null ? BindingResolver.ResolveValueOrLiteral(vm, ve) : BindingResolver.Resolve(ctx, $"{{{path}}}");
                box.PickerValueType = _valueOverride is null ? BindingResolver.ResolvePropertyType(ctx, path) : current?.GetType();
            }
            else
            {
                current = BindingResolver.ResolveValueOrLiteral(vm, ve);
                box.PickerValueType = current?.GetType();
            }

            string? Attr(string name) => BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault(name));
            box.InputValue = current;
            box.InputPlaceholder = Attr("placeholder");
            box.ReadOnly = InputReadOnly(node, vm, _valueOverride);
            if (node.Tag == "DvDateTimeInput")
            {
                box.PickerMode = PickerValue.ResolveMode(Attr("mode"), box.PickerValueType);
                box.PickerSeconds = ToBool(Attr("show-seconds"));
                box.PickerFormat = Attr("format");
            }
        }

        if (node.Tag == "DvTank")
        {
            box.TankValue = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("value")), 0f);
            box.TankMin = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("min")), 0f);
            box.TankMax = ToFloat(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("max")), 100f);
            box.TankUnit = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("unit"));
            box.TankZones = ParseZones(node, vm);
            // show-zone-marks="false" → 구간 색은 유지하고 경계 눈금·숫자만 숨김 (기본 true)
            if (node.Attributes.TryGetValue("show-zone-marks", out var zm))
                box.TankZoneMarks = !string.Equals(
                    BindingResolver.ResolveText(vm, zm), "false", StringComparison.OrdinalIgnoreCase);
        }

        if (node.Tag is "DvIcon" or "DvButton" && node.Attributes.TryGetValue("icon", out var iconRaw))
        {
            box.Icon = BindingResolver.ResolveText(vm, iconRaw);
            if (!string.IsNullOrWhiteSpace(box.Icon) && !IconFont.TryResolve(box.Icon, out _))
                WarnIcon(box.Icon);
        }

        if (node.Tag is "DvLabel" or "DvButton" or "DvCheckBox" or "DvRadio")
        {
            var raw = node.Attributes.GetValueOrDefault("text") ?? node.Text ?? "";
            box.ResolvedText = ov is { HasText: true } ? ov.Value.Text : BindingResolver.ResolveText(vm, raw) ?? "";
        }

        return box;
    }

    // DvTrend 설정 + 채널 데이터 스냅샷 (points 바인딩 우선, 없으면 source 버퍼에서 채널 이름으로)
    private Trends.TrendSpec BuildTrend(ElementNode node, object? vm)
    {
        object? Attr(ElementNode n, string name) => BindingResolver.ResolveValueOrLiteral(vm, n.Attributes.GetValueOrDefault(name));
        double? Num(ElementNode n, string name) => Attr(n, name) is { } v &&
            double.TryParse(BindingResolver.ToText(v, null), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
        string? Text(ElementNode n, string name) => BindingResolver.ResolveText(vm, n.Attributes.GetValueOrDefault(name));
        bool Flag(string name, bool dflt) => Text(node, name) is { Length: > 0 } s
            ? !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) : dflt;

        var spec = new Trends.TrendSpec
        {
            WindowSeconds = Num(node, "window") is > 0 and var w ? w : 60,
            End = Attr(node, "end") switch { DateTime dt => dt, _ => null },
            YMin = Num(node, "y-min"),
            YMax = Num(node, "y-max"),
            Area = Flag("area", false),
            Smooth = Flag("smooth", true)
        };

        var source = Attr(node, "source") as Trends.TrendBuffer;
        var index = 0;
        foreach (var ch in node.Children.Where(c => c.Tag == "DvTrendChannel"))
        {
            var name = Text(ch, "name") ?? $"ch{index}";
            var points = Attr(ch, "points") is IEnumerable<Trends.TrendPoint> bound
                ? HandlerErrors.Snapshot(bound, PathOf(ch.Attributes.GetValueOrDefault("points")), ch.Tag).ToArray()
                : source?.Snapshot(name) ?? Array.Empty<Trends.TrendPoint>();
            var color = ParseColor(Text(ch, "color"))?.ToSK()
                        ?? Trends.TrendSpec.Palette[index % Trends.TrendSpec.Palette.Length];
            spec.Channels.Add(new Trends.TrendChannelData
            {
                Name = name,
                Label = Text(ch, "label"),
                Unit = Text(ch, "unit"),
                Color = color,
                Points = points
            });
            index++;
        }

        return spec;
    }

    // 색 속성 값(바인딩을 푼 뒤) — #색 또는 token(이름)(이 엔진의 테마로 해석, 모르는 토큰은 자홍). 해석 못 하면 null.
    private DvColor? ParseColor(string? raw)
        => !string.IsNullOrWhiteSpace(raw) && DvColor.TryParse(ThemeResolver.ResolveVars(raw.Trim(), _theme), out var c) ? c : null;

    // alarm-zones — 바인딩(문자열 전체)을 풀고 구간 색의 token(이름) 을 테마로 바꾼 뒤 해석. 형식 오류는 GaugeZone.ParseZones 의 예외 그대로.
    private List<Widgets.GaugeZone> ParseZones(ElementNode node, object? vm)
        => Widgets.GaugeZone.ParseZones(ThemeResolver.ResolveVars(
            BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("alarm-zones")), _theme));

    // row-class·cell-class — 클래스의 기본 규칙에서 background·color·font-weight 만(지정한 것만 값)
    private static readonly System.Text.RegularExpressions.Regex DeclName = new(@"(?:^|[;\s])([a-z-]+)\s*:",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private Grid.GridCellStyle? GridClassStyle(string className)
    {
        if (!_sheet.TryGet(className, out var rule) || string.IsNullOrWhiteSpace(rule.Base))
            return null;
        var decls = ThemeResolver.ResolveVars(rule.Base, _theme) ?? "";
        var names = DeclName.Matches(decls).Select(m => m.Groups[1].Value.ToLowerInvariant()).ToHashSet();
        var st = StyleParser.Parse(decls);
        return new Grid.GridCellStyle(
            names.Contains("background") || names.Contains("background-color") ? st.Background : null,
            names.Contains("color") ? st.Color : null,
            names.Contains("font-weight") ? st.Bold : null);
    }

    /// <summary>
    /// <c>DvRadio</c>: checked = the group value equals this button's <c>option</c>. The group value is <c>value</c> (the VM property if two-way; for one-way/literal, that value or the value overridden by a handle);
    /// without <c>value</c>, it is the value of the group remembered by the screen (by <c>group</c> name, or this element alone if there is no name and only a handle).
    /// </summary>
    private void BuildRadio(LayoutBox box, ElementNode node, object? vm)
    {
        var option = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("option")) ?? "";
        box.RadioOption = option;
        var ve = node.Attributes.GetValueOrDefault("value");
        if (ve is not null && BindingResolver.IsTwoWay(ve))
        {
            var path = BindingResolver.ExtractPath(ve);
            box.ValueBindExpr = ve;
            box.SelectValueType = BindingResolver.ResolvePropertyType(vm, path);
            box.ToggleOn = SelectValue.Matches(option, BindingResolver.Resolve(vm, $"{{{path}}}"), box.SelectValueType);
        }
        else if (ve is not null)
        {
            var cur = BindingResolver.ResolveValueOrLiteral(vm, ve);
            box.ToggleOn = ControlOf?.Invoke(node)?.Slot is { HasOverride: true } ov
                ? ToBool(ov.CellValue)
                : SelectValue.Matches(option, cur, cur?.GetType());
        }
        else
        {
            var group = BindingResolver.ResolveText(vm, node.Attributes.GetValueOrDefault("group"));
            box.RadioGroup = group is { Length: > 0 } ? group : ControlOf?.Invoke(node) is not null ? node : null;
            box.ToggleOn = box.RadioGroup is { } key && RadioGroupOf?.Invoke(key) is { } picked && picked == option;
        }
    }

    /// <summary>The selected <c>option</c> of a <c>DvRadio</c> group without <c>value</c> (by name or element), remembered by the screen (<see cref="DvSurface"/>).</summary>
    internal Func<object, string?>? RadioGroupOf { get; set; }

    // 래칭 버튼 켜짐 — value 가 bool true 또는 "true"
    private static bool LatchOn(ElementNode node, object? vm)
    {
        var ve = node.Attributes.GetValueOrDefault("value");
        var cur = ve is not null && BindingResolver.IsTwoWay(ve)
            ? BindingResolver.Resolve(vm, $"{{{BindingResolver.ExtractPath(ve)}}}")
            : BindingResolver.ResolveValueOrLiteral(vm, ve);
        return cur is bool b && b || string.Equals(BindingResolver.ToText(cur, ve), "true", StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveStyle(string? style, object? vm)
        => ThemeResolver.ResolveVars(BindingResolver.ResolveInline(vm, style), _theme);

    private IEnumerable<string> Classes(ElementNode node, object? vm, string? valueOverride)
    {
        // 의미 속성 → 암묵 클래스 (DvButton variant/size). 바인딩을 풀어 리터럴과 같게 — 값이 없거나 비면 기존 DvButton 그대로.
        if (node.Tag == "DvButton" && node.Attributes.TryGetValue("variant", out var variantRaw) &&
            BindingResolver.ResolveText(vm, variantRaw)?.Trim() is { Length: > 0 } variant)
        {
            yield return "btn";
            yield return "btn-" + variant;
            if (node.Attributes.TryGetValue("size", out var sizeRaw) &&
                BindingResolver.ResolveText(vm, sizeRaw)?.Trim() is { Length: > 0 } size)
                yield return "btn-" + size;
        }

        if (node.Tag == "DvButton" && node.Attributes.ContainsKey("value"))
        {
            // 래칭 버튼: variant 가 없으면 secondary 모양, 켜지면 .btn-on 을 덧붙인다(뒤에 정의돼 variant 를 덮음)
            if (!node.Attributes.ContainsKey("variant"))
            {
                yield return "btn";
                yield return "btn-secondary";
            }
            if (LatchOn(node, vm))
                yield return "btn-on";
        }

        if (node.Tag == "DvSegment")
            yield return "segment";

        if (node.Tag == "DvCheckBox")
            yield return "checkbox";

        if (node.Tag == "DvListBox")
            yield return "listbox";

        if (node.Tag == "DvTreeView")
            yield return "treeview";

        if (node.Tag == "DvExpander")
            yield return "expander";

        if (node.Tag == "DvRadio")
            yield return "radio";

        if (node.Tag == "DvTabs")
            yield return "tabs";

        if (node.Tag == "DvTab")
            yield return "tab-body";

        if (node.Tag is "DvInput" or "DvSelect" or "DvDateTimeInput" or "DvColorInput")
        {
            yield return "input";
            if (InputReadOnly(node, vm, valueOverride))
                yield return "input-readonly";
        }

        if (node.Tag == "DvIcon")
            yield return "icon";

        if (node.Attributes.TryGetValue("class", out var raw) &&
            BindingResolver.ResolveText(vm, raw) is { } list)
        {
            foreach (var c in list.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                yield return c;
        }
    }

    /// <summary>Lookup function for values overridden from code through control handles. If <see langword="null"/>, no lookup is done (a screen without handles).</summary>
    internal Func<ElementNode, Controls.DvControl?>? ControlOf { get; set; }

    // 핸들의 Visible 덮어쓰기가 마크업 visible 보다 우선
    private bool IsVisibleOf(ElementNode node, object? ctx) =>
        ControlOf?.Invoke(node)?.Overrides.Visible is { } v ? v : IsVisible(node, ctx);

    /// <summary>If the <c>visible</c> attribute is false or empty, the element is not created.</summary>
    private static bool IsVisible(ElementNode node, object? ctx)
    {
        if (!node.Attributes.TryGetValue("visible", out var raw))
            return true;

        var v = BindingResolver.ResolveValueOrLiteral(ctx, raw);
        return v switch
        {
            null => false,
            bool b => b,
            _ => BindingResolver.ToText(v, raw) is { Length: > 0 } s && s != "false" && s != "0"
        };
    }

    private static bool TryGetComponent(ComponentRegistry? registry, string tag, out ComponentDef def)
    {
        if ((registry is not null && registry.TryGet(tag, out var d) && d is not null) ||
            (BuiltInComponents.Registry.TryGet(tag, out d) && d is not null))
        {
            def = d;
            return true;
        }

        def = null!;
        return false;
    }

    // slotBuilder = 인스턴스 자식(슬롯 내용)을 만드는 방법. 최상위 인스턴스면 바깥 VM 으로 일반 빌드,
    // 다른 템플릿 안의 인스턴스면 그 템플릿 문맥(파라미터 범위·바깥 슬롯)으로 빌드한다.
    private LayoutBox? ExpandComponent(ElementNode instance, ComponentDef component, object? outerVm,
        ComponentRegistry? registry, int depth, Func<ElementNode, IEnumerable<LayoutBox>> slotBuilder)
    {
        if (depth >= MaxComponentDepth)
            throw new InvalidOperationException(
                $"Component expansion depth exceeded {MaxComponentDepth}; possible cycle at '{instance.Tag}'.");

        var scope = new ComponentScope();
        TwoWayParams? twoWay = null;
        // 인스턴스의 값 핸들(DvSwitch 등) — 값의 주인 규칙(설계 4.1)을 컴포넌트 값 파라미터에 적용한다
        var slot = ControlOf?.Invoke(instance)?.Slot;
        foreach (var p in component.Params)
        {
            var isValue = slot is not null && string.Equals(p.Name, slot.ValueAttr, StringComparison.Ordinal);
            if (TryGetAttribute(instance, p.Name, out var raw))
            {
                scope[p.Name] = isValue && slot!.HasOverride && !BindingResolver.IsTwoWay(raw)
                    ? slot.CellValue // 단방향·리터럴 위에 코드가 덮어씀
                    : BindingResolver.ResolveValueOrLiteral(outerVm, raw);
                if (BindingResolver.IsTwoWay(raw))
                    (twoWay ??= new TwoWayParams(instance))[p.Name] = raw;
            }
            else if (isValue && slot!.CanOwn)
            {
                // value 없음 + 핸들 = 핸들 칸이 주인 — 안쪽 위젯이 칸을 읽고 쓴다
                scope[p.Name] = slot.CellValue;
                twoWay ??= new TwoWayParams(instance);
                twoWay[p.Name] = Controls.ValueSlot.CellExpr;
                twoWay.Targets[p.Name] = slot.Cell;
            }
            else
            {
                scope[p.Name] = p.Default;
            }
        }

        var root = WithPassthrough(component, instance, outerVm);
        // 인스턴스 onclick 이 DvRepeat 항목 안이면 루트 박스가 그 항목을 받게 한다(M(항목) 우선 — 일반 onclick 과 같은 규칙)
        var passedClick = instance.Attributes.ContainsKey("onclick") &&
                          !component.Params.Any(p => string.Equals(p.Name, "onclick", StringComparison.OrdinalIgnoreCase));
        _rootItemScope = passedClick ? outerVm as ItemScope ?? _rootItemScope : null; // 루트가 또 컴포넌트면 바깥 항목을 이어 준다
        try
        {
            var box = BuildTemplate(root, scope, registry, depth + 1, instance.Children, slotBuilder, twoWay);
            if (box is not null)
            {
                // 템플릿 노드는 인스턴스끼리 공유된다 — 핸들 조회·덮어쓰기는 인스턴스 노드 기준으로 루트 박스에만
                box.Origin = instance;
                if (ControlOf?.Invoke(instance)?.Overrides.Enabled is { } en)
                    box.Disabled = !en;
                box.HasCodeClick = ControlOf?.Invoke(instance)?.Overrides.HasClick == true;
            }
            return box;
        }
        finally
        {
            _rootItemScope = null;
        }
    }

    // 인스턴스의 style/class/레이아웃·onclick·disabled 속성을 템플릿 루트로 전달 — inline style 은 병합(덮어쓰기), 나머지는 인스턴스 쪽이 이김
    private static readonly string[] PassthroughAttributes = { "class", "grow", "width", "height", "onclick", "disabled" };

    // 전달된 인스턴스 onclick 의 DvRepeat 항목 범위 — 템플릿 루트 박스를 만들 때 한 번 쓰고 비운다
    private ItemScope? _rootItemScope;

    private static ElementNode WithPassthrough(ComponentDef component, ElementNode instance, object? outerVm)
    {
        var template = component.Template;
        bool IsParam(string name) => component.Params.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        var passed = PassthroughAttributes.Where(a => instance.Attributes.ContainsKey(a) && !IsParam(a)).ToList();
        var hasStyle = instance.Attributes.ContainsKey("style") && !IsParam("style");
        if (passed.Count == 0 && !hasStyle)
            return template;

        var copy = new ElementNode { Tag = template.Tag, Text = template.Text };
        foreach (var (k, v) in template.Attributes)
            copy.Attributes[k] = v;
        copy.Children.AddRange(template.Children);

        foreach (var a in passed)
        {
            if (a == "onclick")
            {
                copy.Attributes[a] = instance.Attributes[a]; // 메서드 이름 그대로 — 화면 뷰모델에서 찾는다
                continue;
            }

            var v = BindingResolver.ResolveText(outerVm, instance.Attributes[a]) ?? "";
            copy.Attributes[a] = a == "class" && copy.Attributes.TryGetValue("class", out var own) ? $"{own} {v}" : v;
        }

        if (hasStyle)
        {
            var instStyle = BindingResolver.ResolveInline(outerVm, instance.Attributes["style"]);
            copy.Attributes["style"] = StyleSheet.Join(copy.Attributes.GetValueOrDefault("style"), instStyle) ?? "";
        }

        return copy;
    }

    private LayoutBox? BuildTemplate(ElementNode node, object? scope, ComponentRegistry? registry, int depth,
        IReadOnlyList<ElementNode> slotChildren, Func<ElementNode, IEnumerable<LayoutBox>> slotBuilder,
        IReadOnlyDictionary<string, string>? twoWay)
    {
        SetReadTag(node);
        if (node.Tag is "DvStyle" or "DvTrendChannel" or "DvOption" or "DvColumn" or "DvColumnGroup" or "DvSummaryRow" or "DvSummary" ||
            !IsVisibleOf(node, scope))
            return null;

        if (TryGetComponent(registry, node.Tag, out var def))
            return ExpandComponent(node, def, scope, registry, depth,
                child => TemplateChild(child, scope, registry, depth, slotChildren, slotBuilder, twoWay));

        if (node.Tag == "DvRepeat")
            return BuildRepeat(node, scope,
                (ch, itemScope) => TemplateChild(ch, itemScope, registry, depth, slotChildren, slotBuilder, twoWay));

        // 양방향 파라미터: 템플릿 DvToggle/DvKnob/DvInput value="{p}" 의 커밋 대상을 바깥 VM 경로로 연결
        string? outerExpr = null;
        object? outerTarget = null;
        if (node.Tag is "DvToggle" or "DvCheckBox" or "DvKnob" or "DvSlider" or "DvInput" or "DvSelect" or "DvSegment" or "DvTabs" or "DvDateTimeInput" or "DvColorInput" or "DvButton" && twoWay is not null &&
            node.Attributes.TryGetValue("value", out var ve) && BindingResolver.IsBinding(ve) &&
            twoWay.TryGetValue(ve[1..^1].Trim(), out var oe))
        {
            outerExpr = oe;
            (twoWay as TwoWayParams)?.Targets.TryGetValue(ve[1..^1].Trim(), out outerTarget);
        }

        _valueOverride = outerExpr;
        LayoutBox box;
        try
        {
            box = BuildSelf(node, scope);
        }
        finally
        {
            _valueOverride = null;
        }

        if (outerExpr is not null)
        {
            box.ValueBindExpr = outerExpr;
            box.ValueTarget = outerTarget; // 인스턴스 핸들 칸(없으면 바깥 VM)
            box.ValueOrigin = (twoWay as TwoWayParams)?.Instance;
        }

        if (node.Tag == "DvTabs")
            return BuildTabs(box, scope, ch => TemplateChild(ch, scope, registry, depth, slotChildren, slotBuilder, twoWay));

        if (node.Tag == "DvExpander")
            return BuildExpander(box, scope, ch => TemplateChild(ch, scope, registry, depth, slotChildren, slotBuilder, twoWay));

        if (node.Tag == "DvProcess")
        {
            Process.ProcessBuilder.Build(box, scope, registry, ProcessBuilders(registry, depth));
            return box;
        }

        foreach (var ch in node.Children)
            box.Children.AddRange(TemplateChild(ch, scope, registry, depth, slotChildren, slotBuilder, twoWay));
        AddSegmentItems(box, scope);
        AddListItems(box, scope);
        AddTreeItems(box, scope);

        return box;
    }

    // 템플릿 자식 1개 → 박스 0..N 개. DvSlot 은 이 템플릿 인스턴스의 자식(슬롯 내용)으로 펼친다
    // — 중첩 컴포넌트 안에 놓인 DvSlot 도 바깥 인스턴스 자식을 받는다.
    private IEnumerable<LayoutBox> TemplateChild(ElementNode ch, object? scope, ComponentRegistry? registry, int depth,
        IReadOnlyList<ElementNode> slotChildren, Func<ElementNode, IEnumerable<LayoutBox>> slotBuilder,
        IReadOnlyDictionary<string, string>? twoWay)
    {
        if (ch.Tag == "DvSlot")
            return slotChildren.SelectMany(slotBuilder).ToList();

        return BuildTemplate(ch, scope, registry, depth, slotChildren, slotBuilder, twoWay) is { } b
            ? new[] { b }
            : Array.Empty<LayoutBox>();
    }

    private static bool TryGetAttribute(ElementNode node, string name, out string value)
    {
        foreach (var (key, current) in node.Attributes)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = current;
                return true;
            }
        }

        value = "";
        return false;
    }

    // 컴포넌트 템플릿에서 value="{p}" 가 양방향 파라미터로 바깥 {Bind …} 에 연결된 경우 그 식(스타일 계산 전에 알아야 함)
    private string? _valueOverride;

    /// <summary>Two-way component parameters (name → outer expression). For parameters owned by an instance handle, <see cref="Targets"/> holds that slot.</summary>
    private sealed class TwoWayParams(ElementNode instance) : Dictionary<string, string>(StringComparer.Ordinal)
    {
        public ElementNode Instance { get; } = instance;
        public Dictionary<string, object> Targets { get; } = new(StringComparer.Ordinal);
    }

    // 읽기 전용 = readonly 참, 또는 값이 양방향 바인딩이 아님(단방향·리터럴)
    // 쓸 수 있는 값 = 양방향 바인딩이거나 핸들이 주인(value 없음 + 값 핸들)
    private bool InputReadOnly(ElementNode node, object? vm, string? valueOverride)
        => !(BindingResolver.IsTwoWay(valueOverride ?? node.Attributes.GetValueOrDefault("value")) ||
             valueOverride is null && OwnerSlot(node) is not null) ||
           ToBool(BindingResolver.ResolveValueOrLiteral(vm, node.Attributes.GetValueOrDefault("readonly")));

    // value 없음 + 값 핸들 = 핸들이 주인인 칸(설계 4.1). 아니면 null.
    private Controls.ValueSlot? OwnerSlot(ElementNode node)
        => ControlOf?.Invoke(node)?.Slot is { CanOwn: true } slot && !node.Attributes.ContainsKey(slot.ValueAttr) ? slot : null;

    /// <summary>
    /// Value ownership (design 4.1): <c>{Bind X}</c> = VM; no <c>value</c> + a value handle = the handle slot (a synthetic <c>{Bind Value}</c> whose write target is the slot);
    /// one-way/literal = markup (or the value a handle overrode it with). Expr = the box's ValueBindExpr; Target = the write/read target (the slot, otherwise null = VM/item scope);
    /// Override = the slot holding a value written over a one-way/literal value, if any (otherwise null).
    /// </summary>
    private (string? Expr, object? Target, Controls.ValueSlot? Override) ValueOf(ElementNode node, string? ve)
    {
        if (_valueOverride is not null)
            return (ve, null, null); // 컴포넌트 양방향 파라미터 — 호출자가 _valueOverride 를 쓴다(핸들은 컴포넌트 안에 없음)
        if (OwnerSlot(node) is { } own)
            return (Controls.ValueSlot.CellExpr, own.Cell, null);
        if (ve is not null && !BindingResolver.IsTwoWay(ve) && ControlOf?.Invoke(node)?.Slot is { HasOverride: true } ov)
            return (ve, null, ov);
        return (ve, null, null);
    }

    /// <summary>Gap between a button's icon and its text.</summary>
    internal const float IconTextGap = 8f;

    private static readonly HashSet<string> WarnedIcons = new();

    private static void WarnIcon(string spec)
    {
        lock (WarnedIcons)
        {
            if (WarnedIcons.Add(spec))
                System.Diagnostics.Trace.TraceWarning($"Devinno.UI: 알 수 없는 아이콘 '{spec}' — 그리지 않음(fa-이름 / far fa-이름 / fab fa-이름)");
        }
    }

    // VM 읽기 예외 알림의 요소 태그 — 이 요소의 바인딩을 푸는 동안
    private static void SetReadTag(ElementNode node)
    {
        if (HandlerErrors.CurrentRead is { } scope)
            scope.Tag = node.Tag;
    }

    // 알림의 Member — 바인딩이면 경로, 아니면 값 그대로
    private static string? PathOf(string? raw) => raw is not null && BindingResolver.IsBinding(raw) ? BindingResolver.ExtractPath(raw) : raw;

    private static bool ToBool(object? v)
        => v is bool b ? b : string.Equals(BindingResolver.ToText(v, null), "true", StringComparison.OrdinalIgnoreCase);

    // 모르는 input-mode = 경고 후 drag
    private static KnobInputMode WarnKnobMode(string mode)
    {
        System.Diagnostics.Trace.TraceWarning($"Devinno.UI: DvKnob input-mode='{mode}' 은(는) 없는 값입니다(drag·tap·both) — drag 로 동작");
        return KnobInputMode.Drag;
    }

    private static string WarnSliderDirection(string? dir)
    {
        if (!string.IsNullOrEmpty(dir))
            System.Diagnostics.Trace.TraceWarning($"Devinno.UI: DvSlider direction='{dir}' 은(는) 없는 값입니다(right·left·up·down) — right 로 동작");
        return "right";
    }

    private static float ToFloat(object? v, float dflt)
        => v is null
            ? dflt
            : float.TryParse(BindingResolver.ToText(v, null), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f)
                ? f
                : dflt;

    /// <summary>Only re-arranges an already built tree at a new size (no build, binding or style resolution); for when only the window size changed.</summary>
    /// <summary>Clears all measured size caches; call before <see cref="Rearrange"/> when there are boxes whose size changes over time (expand animation).</summary>
    internal static void ClearMeasure(LayoutBox box)
    {
        box.MeasureCache = null;
        box.MeasureCacheAt = null;
        foreach (var c in box.Children)
            ClearMeasure(c);
    }

    internal static void Rearrange(LayoutBox root, float width, float height)
    {
        root.Rect = SKRect.Create(0, 0, width, height);
        Arrange(root, 0, 0, width, height);
    }

    /// <summary>
    /// The box's own size (style width/height, or the content size if absent). Used to compute the size of DvApp regions and dialog roots.
    /// If <paramref name="availableWidth"/> is given, text is wrapped to that width (outer width) when measuring; otherwise a single line (independent of the layout area).
    /// </summary>
    internal static (float w, float h) MeasureBox(LayoutBox box, float? availableWidth = null) => Measure(box, availableWidth);

    private static (float w, float h) Measure(LayoutBox box) => Measure(box, null);

    // 캐시: 자연 크기(가용 폭 없음) 하나 + 마지막 가용 폭 결과 하나. 자연 폭이 가용 폭 안에 들어가면 줄바꿈이 없으므로
    // 자연 크기 그대로다 — 줄바꿈이 필요 없는 글자는 측정 비용이 늘지 않고 크기·위치도 그대로.
    private static (float w, float h) Measure(LayoutBox box, float? avail)
    {
        if (box.MeasureCache is not { } natural)
        {
            natural = MeasureUncached(box, null);
            box.MeasureCache = natural;
        }

        if (avail is not { } a || (!box.MeasureAtAvail && (natural.w <= a + TextWrap.Tolerance || !WidthSensitive(box))))
            return natural;

        if (box.MeasureCacheAt is { } hit && box.MeasureCacheAvail == a)
            return hit;
        var result = MeasureUncached(box, a);
        box.MeasureCacheAt = result;
        box.MeasureCacheAvail = a;
        return result;
    }

    // 최종 폭으로 확정된 스타일 width — 부모가 덮어쓰는 width(stretch·grid 칸·grow)는 줄바꿈 기준이 아니다
    private static float? FixedWidth(LayoutBox box) => box.WidthOverridden ? null : box.Style.Width;

    // 가용 폭에 따라 크기가 달라질 수 있는 상자 — 줄바꿈하는 글자, 또는 자식에게 폭을 넘기는 세로 배치·grid.
    // width 가 고정이면 자연 크기가 이미 그 폭 기준이다. row·flex-wrap 은 자식에게 폭을 주지 않는다(spec §4.1).
    private static bool WidthSensitive(LayoutBox box)
    {
        var st = box.Style;
        if (FixedWidth(box) is not null || box.Grid is not null)
            return false;
        if (box.ResolvedText is not null)
            return st.NoWrap != true && !IconFont.TryResolve(box.Icon, out _);
        if (box.Children.Count == 0)
            return false;
        if (IsGridBox(st))
            return true;
        return !st.IsFlex || (st.FlexDirection == FlexDirection.Column && !st.Wrap);
    }

    // 자식(또는 글자)이 쓸 안쪽 가용 폭 — width 고정(부모가 따르는 경우)이면 그 폭, 아니면 가용 폭을 min/max-width 로 제한한 폭
    // (가용 폭이 없으면 max-width), 패딩 제외
    private static float? InnerLimit(LayoutBox box, float? avail)
    {
        var st = box.Style;
        var outer = FixedWidth(box) ?? (avail is { } a ? Clamp(a, st.MinWidth, st.MaxWidth) : st.MaxWidth);
        return outer is { } o ? MathF.Max(0, o - st.Padding.Horizontal) : null;
    }

    // 글자 줄 정보 — 같은 글자·글꼴이면 상자에 둔 것을 쓴다
    private static TextLines LinesOf(LayoutBox box, ComputedStyle st, SKFont font)
    {
        var text = box.ResolvedText ?? "";
        if (box.TextLines is { } t && t.Matches(text, st.FontSize, st.Bold, st.FontFamily))
            return t;
        return box.TextLines = new TextLines(text, st.FontSize, st.Bold, st.FontFamily, font);
    }

    /// <summary>
    /// The lines to draw a text box with at inner width <paramref name="contentWidth"/>; with <c>white-space:nowrap</c>, splits only at <c>\n</c>.
    /// If lines were already split at the same width during measurement, they are reused (for the Painter).
    /// </summary>
    internal static IReadOnlyList<string> TextLinesFor(LayoutBox box, ComputedStyle st, SKFont font, float contentWidth)
        => LinesOf(box, st, font).At(st.NoWrap == true ? null : contentWidth, font);

    private static (float w, float h) MeasureUncached(LayoutBox box, float? avail)
    {
        box.MeasureCount++;
        var (w, h) = MeasureRaw(box, avail);
        if (box.Reveal is { } reveal)
            h *= reveal; // 접는 패널이 펼치거나 접히는 중 — 보이는 만큼만 자리를 차지
        var st = box.Style;
        // 스크롤 상자의 자연 높이도 내용 높이(CSS 와 같음) — max-height 로 제한하고, flex 주 축에서 모자라면 줄어든다(ShrinkOf)
        // 비율 고정: 한쪽만 지정됐으면 다른 쪽을 비율로 정한다
        if (st.AspectRatio is { } ar)
        {
            if (st.Width is not null && st.Height is null)
                h = w / ar;
            else if (st.Height is not null && st.Width is null)
                w = h * ar;
        }

        return (Clamp(w, st.MinWidth, st.MaxWidth), Clamp(h, st.MinHeight, st.MaxHeight));
    }

    private static float Clamp(float value, float? min, float? max)
    {
        if (max is { } mx && value > mx)
            value = mx;
        if (min is { } mn && value < mn)
            value = mn;
        return value;
    }

    private static (float w, float h) MeasureRaw(LayoutBox box, float? avail)
    {
        var st = box.Style;
        if (box.Node.Tag == "DvToggle")
            return (st.Width ?? 44f, st.Height ?? 24f);

        if (box.Node.Tag == "DvLamp")
            return (st.Width ?? 24f, st.Height ?? 24f);

        if (box.Node.Tag == "DvGauge")
            return (st.Width ?? 120f, st.Height ?? 120f);

        if (box.Node.Tag == "DvImage")
            return (st.Width ?? 100f, st.Height ?? 100f);

        if (box.Node.Tag == "DvTank")
            return (st.Width ?? 80f, st.Height ?? 160f);

        if (box.Node.Tag == "DvKnob")
            return (st.Width ?? 96f, st.Height ?? 96f);

        if (box.Node.Tag == "DvSparkline")
            return (st.Width ?? 120f, st.Height ?? 32f);

        if (box.Node.Tag == "DvBarChart")
            return (st.Width ?? 320f, st.Height ?? 200f);

        if (box.Node.Tag == "DvPieChart") // 원 + 오른쪽 범례
            return (st.Width ?? 320f, st.Height ?? 180f);

        if (box.Node.Tag == "DvStepBar") // 단계마다 96 폭(최소 200), 점 28 + 이름 줄
            return (st.Width ?? MathF.Max(200f, box.StepNames.Length * 96f), st.Height ?? 64f);

        if (box.Node.Tag is "DvListBox" or "DvTreeView" && st.Height is null)
        {
            // 행 5개(트리 8개)까지 보이는 높이(더 많으면 스크롤), 폭 = 가장 긴 행(최소 200)
            var shown = Math.Min(box.Children.Count, box.Node.Tag == "DvTreeView" ? 8 : 5);
            var lh = shown * 40f + Math.Max(0, shown - 1) * st.Gap + st.Padding.Vertical;
            var lw = st.Width ?? MathF.Max(200f, box.Children.Select(c => Measure(c, null).w).DefaultIfEmpty(0f).Max() + st.Padding.Horizontal);
            return (lw, MathF.Max(lh, 40f + st.Padding.Vertical));
        }

        if (box.Node.Tag == "DvSlider") // 손잡이 24(터치 자리 40) — show-value 면 값 글자 자리를 더한다
            return Interaction.SliderMetrics.Vertical(box)
                ? (st.Width ?? 40f + (box.ShowValue ? Interaction.SliderMetrics.LabelSide : 0f), st.Height ?? 200f)
                : (st.Width ?? 200f, st.Height ?? 40f + (box.ShowValue ? Interaction.SliderMetrics.LabelBand : 0f));

        if (box.Node.Tag == "DvInput")
            return (st.Width ?? 200f, st.Height ?? (box.InputMultiline ? 96f : 40f));

        if (box.Node.Tag is "DvSelect" or "DvDateTimeInput" or "DvColorInput")
            return (st.Width ?? 200f, st.Height ?? 40f);

        if (box.Node.Tag == "DvRing")
            return (st.Width ?? 120f, st.Height ?? 120f);

        if (box.Node.Tag == "DvTrend")
            return (st.Width ?? 480f, st.Height ?? 240f);

        if (box.Node.Tag == "DvAlarmBanner")
            return (st.Width ?? 480f, st.Height ?? (string.IsNullOrEmpty(box.CommAlarm) ? 0f : Alarms.AlarmBannerLayout.StripHeight)
                + Alarms.AlarmBannerLayout.DefaultBottom);

        if (box.Node.Tag == "DvAlarmList")
            return (st.Width ?? 560f, st.Height ?? 30f + box.AlarmRowHeight * 5f);

        if (box.Process is not null)
        {
            var natural = Process.ProcessBuilder.Natural(box);
            return (st.Width ?? natural.Width + st.Padding.Horizontal, st.Height ?? natural.Height + st.Padding.Vertical);
        }

        if (box.Grid is { } grid)
            return (st.Width ?? 480f, st.Height ?? grid.HeaderTotal + grid.RowHeight * 5f + grid.SummaryHeight);

        if (box.Node.Tag == "DvProgress")
            return box.Direction is "up" or "down"
                ? (st.Width ?? 12f, st.Height ?? 120f)
                : (st.Width ?? 200f, st.Height ?? 12f);

        if (box.Node.Tag == "DvIcon")
            return (st.Width ?? st.FontSize + st.Padding.Horizontal, st.Height ?? st.FontSize + st.Padding.Vertical);

        if (box.ResolvedText is not null)
        {
            using var font = TextFont.Create(box.ResolvedText, st.FontSize, st.Bold, st.FontFamily);
            float tw;
            float th;
            if (IconFont.TryResolve(box.Icon, out _))
            {
                // 아이콘(글자 크기 정사각) + 글자가 있으면 간격 — 한 줄 유지(spec §8-8)
                tw = font.MeasureText(box.ResolvedText) + st.FontSize + (box.ResolvedText.Length > 0 ? IconTextGap : 0);
                th = Math.Max(font.Spacing, st.FontSize);
            }
            else
            {
                // 안쪽 가용 폭에 맞춰 줄바꿈(nowrap 이면 \n 에서만) — 높이 = 줄 수 × 줄 간격, 폭 = min(한 줄 폭, 가용 폭)
                var limit = st.NoWrap == true ? null : InnerLimit(box, avail);
                var info = LinesOf(box, st, font);
                var lines = info.At(limit, font);
                tw = ReferenceEquals(lines, info.Paras) ? info.Width : MathF.Min(info.Width, limit!.Value);
                th = font.Spacing * lines.Count;
            }

            return (st.Width ?? tw + st.Padding.Horizontal, st.Height ?? th + st.Padding.Vertical);
        }

        if (IsGridBox(st))
            return MeasureGrid(box, avail);

        float main = 0;
        float cross = 0;
        var n = 0;
        var col = !st.IsFlex || st.FlexDirection == FlexDirection.Column;
        // 세로 배치는 자식에게 안쪽 폭을 넘긴다(가로 배치·flex-wrap 은 넘기지 않음 — 한 줄 유지)
        var childAvail = col && !(st.IsFlex && st.Wrap) ? InnerLimit(box, avail) : null;

        // 늘이기로 놓일 자식과 1단계 높이(두 번째 단계에서 다시 잴 후보)
        List<(LayoutBox Box, float H)>? stretched = null;
        foreach (var c in box.Children)
        {
            if (c.Style.Position == Position.Absolute)
                continue;

            var (cw, ch) = Measure(c, childAvail);
            if (col && c.WidthOverridden)
                (stretched ??= new()).Add((c, ch));
            if (col)
            {
                main += ch;
                cross = MathF.Max(cross, cw);
            }
            else
            {
                main += cw;
                cross = MathF.Max(cross, ch);
            }

            n++;
        }

        if (n > 1)
            main += st.Gap * (n - 1);

        // 두 번째 단계(CSS 와 같은 세로 배치 측정): 폭이 위 1단계의 가용 폭과 다르게 정해지면(내용 폭으로 줄어드는 상자)
        // 늘이기로 그 폭에 놓일 자식의 높이를 그 폭으로 다시 잰다 — % grid 칸처럼 폭에 따라 높이가 바뀌는 자식이
        // 측정보다 좁게 배치되어 넘치지 않게. 폭이 고정이거나 부모가 폭을 정하면(배치 폭 = 가용 폭) 다시 재지 않는다.
        // 자연 크기가 그 폭 안에 들어가고 폭에 따라 달라지지 않는 자식은 캐시된 자연 크기를 그대로 돌려준다(측정 추가 없음).
        if (stretched is not null && !(st.IsFlex && st.Wrap) && FixedWidth(box) is null && !(box.WidthOverridden && childAvail is not null))
        {
            // 놓일 안쪽 폭 = 내용 폭(가용 폭까지), 단 min-width 아래로는 아님(측정 결과도 min-width 로 올려 잡힌다).
            // max-width 는 childAvail(InnerLimit)에 이미 들어 있다
            var placed = MathF.Max(childAvail is { } l ? MathF.Min(cross, l) : cross, (st.MinWidth ?? 0) - st.Padding.Horizontal);
            if (childAvail is not { } l2 || MathF.Abs(placed - l2) > TextWrap.Tolerance)
            {
                for (var i = 0; i < stretched.Count; i++)
                    main += Measure(stretched[i].Box, placed).h - stretched[i].H;
            }
        }

        var contentW = col ? cross : main;
        var contentH = col ? main : cross;

        return (st.Width ?? contentW + st.Padding.Horizontal,
            st.Height ?? contentH + st.Padding.Vertical);
    }

    /// <summary>Arranges a single box in the given area (used by the process diagram to place attached slot boxes).</summary>
    internal static void ArrangeBox(LayoutBox box, float x, float y, float w, float h) => Arrange(box, x, y, w, h);

    private static void Arrange(LayoutBox box, float x, float y, float w, float h)
    {
        if (box.Process is not null)
        {
            // 공정도 — 장비·붙은 칸 위치는 공정도 배치가 정한다(flex 배치 안 함)
            box.Rect = SKRect.Create(x, y, w, h);
            var pad = box.Style.Padding;
            Process.ProcessBuilder.Arrange(box, new SKRect(x + pad.Left, y + pad.Top, x + w - pad.Right, y + h - pad.Bottom));
            return;
        }

        if (box.Grid is not null)
        {
            // 그리드는 자식 박스가 없다 — 내용 크기는 행 수·열 폭으로
            ArrangeCore(box, x, y, w, h);
            Grid.GridLayout.Update(box);
            return;
        }

        if (!box.IsScroller)
        {
            ArrangeCore(box, x, y, w, h);
            return;
        }

        // 넘치는 축에만 스크롤바 자리(gutter)를 비우고 그 안쪽에 다시 배치 — 내용이 스크롤바에 붙지 않게
        box.GutterX = 0;
        box.GutterY = 0;
        ArrangeCore(box, x, y, w, h);
        FinishScroll(box);
        if (box.MaxScrollY > 0)
        {
            box.GutterX = Interaction.ScrollBars.Gutter;
            ArrangeCore(box, x, y, w, h);
            FinishScroll(box);
        }

        if (box.MaxScrollX > 0 && box.GutterY == 0)
        {
            box.GutterY = Interaction.ScrollBars.Gutter;
            ArrangeCore(box, x, y, w, h);
            FinishScroll(box);
        }
    }

    // 스크롤 상자의 내용 크기 = 자식이 차지한 범위 + 패딩(뷰포트보다 작으면 뷰포트). 오프셋을 새 범위로 제한.
    private static void FinishScroll(LayoutBox box)
    {
        var r = box.Rect;
        var pad = box.Style.Padding;
        float right = 0, bottom = 0;
        foreach (var c in box.Children)
        {
            right = MathF.Max(right, c.Rect.Right - r.Left);
            bottom = MathF.Max(bottom, c.Rect.Bottom - r.Top);
        }

        box.ContentWidth = MathF.Max(r.Width, right + pad.Right + box.GutterX);
        box.ContentHeight = MathF.Max(r.Height, bottom + pad.Bottom + box.GutterY);
        box.ClampScroll();
    }

    private static void ArrangeCore(LayoutBox box, float x, float y, float w, float h)
    {
        var st = box.Style;
        // 비율 고정(contain): 배정된 영역 안의 가장 큰 해당 비율 상자, 가운데 정렬
        if (st.AspectRatio is { } ar && w > 0 && h > 0)
        {
            var fw = MathF.Min(w, h * ar);
            var fh = fw / ar;
            x += (w - fw) / 2f;
            y += (h - fh) / 2f;
            w = fw;
            h = fh;
        }

        box.Rect = SKRect.Create(x, y, w, h);
        if (box.Children.Count == 0)
            return;

        if (IsGridBox(st))
        {
            ArrangeGrid(box, x, y, w, h);
            return;
        }

        var col = !st.IsFlex || st.FlexDirection == FlexDirection.Column;
        var innerX = x + st.Padding.Left;
        var innerY = y + st.Padding.Top;
        var innerW = w - st.Padding.Horizontal - box.GutterX;
        var innerH = h - st.Padding.Vertical - box.GutterY;

        if (st.IsFlex && st.Wrap)
        {
            ArrangeWrap(box, innerX, innerY, innerW, innerH, col);
            return;
        }

        var flow = new List<int>();
        for (var i = 0; i < box.Children.Count; i++)
            if (box.Children[i].Style.Position != Position.Absolute)
                flow.Add(i);

        // 세로 배치는 자식을 실제 안쪽 폭으로 잰다(글자 줄바꿈) — 스크롤 gutter 가 생기면 기존 두 번째 ArrangeCore 가 좁아진 폭으로 다시 잰다
        var sizes = new (float w, float h)[box.Children.Count];
        foreach (var i in flow)
            sizes[i] = Measure(box.Children[i], col ? innerW : null);

        if (st.IsFlex)
        {
            DistributeMain(sizes, box.Children, flow, col, col ? innerH : innerW, st.Gap);
        }

        float totalMain = 0;
        foreach (var i in flow)
            totalMain += col ? sizes[i].h : sizes[i].w;

        totalMain += st.Gap * (flow.Count - 1);

        var avail = col ? innerH : innerW;
        var cursor = col ? innerY : innerX;
        float extraGap = 0;

        if (st.Justify == JustifyContent.Center)
            cursor += (avail - totalMain) / 2;
        else if (st.Justify == JustifyContent.End)
            cursor += avail - totalMain;
        else if (st.Justify == JustifyContent.SpaceBetween && flow.Count > 1)
            extraGap = (avail - totalMain) / (flow.Count - 1);

        foreach (var i in flow)
        {
            var (cw, ch) = sizes[i];
            float cx;
            float cy;
            var cwUse = cw;
            var chUse = ch;

            var childAlign = box.Children[i].Style.AlignSelf ?? st.Align;
            if (col)
            {
                cwUse = Stretches(box.Children[i].Style, st, col: true) ? StretchTo(box.Children[i].Style, innerW, col: true) : cw;
                cx = innerX + CrossOffset(childAlign, innerW, cwUse);
                cy = cursor;
                cursor += ch + st.Gap + extraGap;
            }
            else
            {
                chUse = Stretches(box.Children[i].Style, st, col: false) ? StretchTo(box.Children[i].Style, innerH, col: false) : ch;
                cy = innerY + CrossOffset(childAlign, innerH, chUse);
                cx = cursor;
                cursor += cw + st.Gap + extraGap;
            }

            Arrange(box.Children[i], cx, cy, cwUse, chUse);
        }

        ArrangeAbsoluteChildren(box);
    }

    // CSS 와 같이 — stretch 는 교차 축 크기(width/height)가 정해지지 않은 자식만 늘리고, min/max 안으로 자른다.
    // aspect-ratio 상자는 늘린 칸을 받아 그 안에 맞춘다(기존 동작).
    private static bool Stretches(ComputedStyle child, ComputedStyle parent, bool col) =>
        (child.AlignSelf ?? parent.Align) == AlignItems.Stretch && (col ? child.Width : child.Height) is null;

    private static float StretchTo(ComputedStyle child, float cross, bool col)
    {
        var (min, max) = col ? (child.MinWidth, child.MaxWidth) : (child.MinHeight, child.MaxHeight);
        if (max is { } hi && cross > hi) cross = hi;
        if (min is { } lo && cross < lo) cross = lo;
        return cross;
    }

    private static float CrossOffset(AlignItems a, float container, float item) => a switch
    {
        AlignItems.Center => (container - item) / 2,
        AlignItems.End => container - item,
        _ => 0
    };

    private static void DistributeMain(
        (float w, float h)[] sizes,
        List<LayoutBox> children,
        List<int> idx,
        bool col,
        float avail,
        float gap)
    {
        if (idx.Count == 0)
            return;

        float used = 0;
        foreach (var i in idx)
            used += col ? sizes[i].h : sizes[i].w;
        used += gap * (idx.Count - 1);

        var diff = avail - used;

        if (diff > 0)
        {
            float growSum = 0;
            foreach (var i in idx)
                growSum += children[i].Style.FlexGrow;
            if (growSum <= 0)
                return;

            foreach (var i in idx)
            {
                var add = diff * children[i].Style.FlexGrow / growSum;
                // 늘어난 크기에도 max 를 다시 적용 — 남는 공간을 다른 형제에게 다시 나누지는 않는다(CSS 의 freeze 반복과 다름)
                var cs = children[i].Style;
                sizes[i] = col
                    ? (sizes[i].w, Clamp(sizes[i].h + add, null, cs.MaxHeight))
                    : (Clamp(sizes[i].w + add, null, cs.MaxWidth), sizes[i].h);
            }
        }
        else if (diff < 0)
        {
            // 줄어들 수 있는 양 — 스크롤 상자를 품은 상자(지정 안 함)는 그 스크롤 상자가 줄어드는 만큼까지(CSS 와 다른 점: min-height:0 없이도)
            var reducible = new float[sizes.Length];
            float shrinkSum = 0;
            foreach (var i in idx)
            {
                var cs = children[i].Style;
                if (!cs.FlexShrinkSet && cs.Overflow != Overflow.Scroll)
                    reducible[i] = Reducible(children[i], col);
                shrinkSum += ShrinkOf(cs, reducible[i]);
            }

            if (shrinkSum <= 0)
                return;

            foreach (var i in idx)
            {
                var cs = children[i].Style;
                var sub = diff * ShrinkOf(cs, reducible[i]) / shrinkSum;
                var cur = col ? sizes[i].h : sizes[i].w;
                // min-height(세로)·min-width(가로) 아래로는 줄지 않는다(CSS 와 같음) — 못 준 만큼 다른 형제에게 다시 나누지는 않는다.
                // 스크롤 상자를 품은 상자는 스크롤 상자가 다 줄어든 크기 아래로도 줄지 않는다
                var min = MathF.Max((col ? cs.MinHeight : cs.MinWidth) ?? 0, reducible[i] > 0 ? cur - reducible[i] : 0);
                var next = MathF.Max(0, MathF.Max(min, cur + sub));
                sizes[i] = col ? (sizes[i].w, next) : (next, sizes[i].h);
            }
        }
    }

    // 줄어드는 비율 — 지정한 값, 지정 안 한 스크롤 상자는 CSS 처럼 1(최소 0 까지), 스크롤 상자를 품어 줄어들 수 있는 상자도 1, 그 밖은 0
    private static float ShrinkOf(ComputedStyle st, float reducible)
        => st.FlexShrinkSet ? st.FlexShrink : st.Overflow == Overflow.Scroll || reducible > 0 ? 1f : 0f;

    // 주 축으로 줄어들 수 있는 양 — 같은 방향 스크롤 상자는 최소(min-*)까지, 그것을 품은 flex 상자는 안쪽이 줄어드는 만큼
    // (같은 방향으로 쌓인 자식은 합, 옆으로 나란한 자식은 가장 큰 쪽 기준). 크기를 정한 상자·기본 흐름·그리드는 줄지 않는다(0)
    private static float Reducible(LayoutBox b, bool col)
    {
        var st = b.Style;
        if ((col ? st.Height : st.Width) is not null)
            return 0;
        var natural = col ? Measure(b).h : Measure(b).w;
        float r;
        if (st.Overflow == Overflow.Scroll)
        {
            r = natural - ((col ? st.MinHeight : st.MinWidth) ?? 0);
        }
        else if (!st.IsFlex || st.Wrap || IsGridBox(st))
        {
            r = 0;
        }
        else
        {
            var along = (st.FlexDirection == FlexDirection.Column) == col;
            r = 0;
            float natMax = 0, afterMax = 0;
            foreach (var c in b.Children)
            {
                if (c.Style.Position == Position.Absolute)
                    continue;
                var cr = Reducible(c, col);
                if (along)
                {
                    r += cr;
                }
                else
                {
                    var cm = col ? Measure(c).h : Measure(c).w;
                    natMax = MathF.Max(natMax, cm);
                    afterMax = MathF.Max(afterMax, cm - cr);
                }
            }

            if (!along)
                r = natMax - afterMax;
            r = MathF.Min(r, natural - ((col ? st.MinHeight : st.MinWidth) ?? 0));
        }

        return MathF.Max(0, r);
    }

    private static void ArrangeWrap(LayoutBox box, float x, float y, float w, float h, bool col)
    {
        var st = box.Style;
        var sizes = box.Children.Select(c => Measure(c)).ToArray();
        var mainAvail = col ? h : w;
        var crossAvail = col ? w : h;

        var lines = new List<List<int>>();
        var cur = new List<int>();
        float lineMain = 0;
        for (var i = 0; i < box.Children.Count; i++)
        {
            if (box.Children[i].Style.Position == Position.Absolute)
                continue;

            var m = col ? sizes[i].h : sizes[i].w;
            var add = cur.Count > 0 ? st.Gap + m : m;
            if (cur.Count > 0 && lineMain + add > mainAvail)
            {
                lines.Add(cur);
                cur = new List<int>();
                lineMain = 0;
            }

            lineMain += cur.Count > 0 ? st.Gap + m : m;
            cur.Add(i);
        }

        if (cur.Count > 0)
            lines.Add(cur);

        var lineCross = lines
            .Select(line => line.Max(i => col ? sizes[i].w : sizes[i].h))
            .ToArray();
        var totalCross = lineCross.Sum() + st.Gap * (lines.Count - 1);

        var crossCursor = col ? x : y;
        float crossExtra = 0;
        switch (st.AlignContent)
        {
            case AlignContent.Center:
                crossCursor += (crossAvail - totalCross) / 2;
                break;
            case AlignContent.End:
                crossCursor += crossAvail - totalCross;
                break;
            case AlignContent.SpaceBetween when lines.Count > 1:
                crossExtra = (crossAvail - totalCross) / (lines.Count - 1);
                break;
        }

        for (var li = 0; li < lines.Count; li++)
        {
            var line = lines[li];
            DistributeMain(sizes, box.Children, line, col, mainAvail, st.Gap);

            float lineTotalMain = 0;
            foreach (var i in line)
                lineTotalMain += col ? sizes[i].h : sizes[i].w;
            lineTotalMain += st.Gap * (line.Count - 1);

            var mainCursor = col ? y : x;
            float mainExtra = 0;
            if (st.Justify == JustifyContent.Center)
                mainCursor += (mainAvail - lineTotalMain) / 2;
            else if (st.Justify == JustifyContent.End)
                mainCursor += mainAvail - lineTotalMain;
            else if (st.Justify == JustifyContent.SpaceBetween && line.Count > 1)
                mainExtra = (mainAvail - lineTotalMain) / (line.Count - 1);

            foreach (var i in line)
            {
                var (cw, ch) = sizes[i];
                var childAlign = box.Children[i].Style.AlignSelf ?? st.Align;
                var bandCross = lineCross[li];

                if (col)
                {
                    var cwUse = Stretches(box.Children[i].Style, st, col: true) ? StretchTo(box.Children[i].Style, bandCross, col: true) : cw;
                    var cx = crossCursor + CrossOffset(childAlign, bandCross, cwUse);
                    Arrange(box.Children[i], cx, mainCursor, cwUse, ch);
                    mainCursor += ch + st.Gap + mainExtra;
                }
                else
                {
                    var chUse = Stretches(box.Children[i].Style, st, col: false) ? StretchTo(box.Children[i].Style, bandCross, col: false) : ch;
                    var cy = crossCursor + CrossOffset(childAlign, bandCross, chUse);
                    Arrange(box.Children[i], mainCursor, cy, cw, chUse);
                    mainCursor += cw + st.Gap + mainExtra;
                }
            }

            crossCursor += lineCross[li] + st.Gap + crossExtra;
        }

        ArrangeAbsoluteChildren(box);
    }

    private static void ArrangeAbsoluteChildren(LayoutBox parent)
    {
        foreach (var child in parent.Children)
            if (child.Style.Position == Position.Absolute)
                ArrangeAbsolute(parent, child);
    }

    private static void ArrangeAbsolute(LayoutBox parent, LayoutBox child)
    {
        var pst = parent.Style;
        var pcbX = parent.Rect.Left + pst.Padding.Left;
        var pcbY = parent.Rect.Top + pst.Padding.Top;
        var pcbW = parent.Rect.Width - pst.Padding.Horizontal;
        var pcbH = parent.Rect.Height - pst.Padding.Vertical;

        var cst = child.Style;
        // left+right 면 폭이 정해진다(CSS 와 같음) — 그 폭으로 재야 줄바꿈한 높이가 나온다
        var (mw, mh) = cst.Left is { } la && cst.Right is { } ra ? Measure(child, MathF.Max(0, pcbW - la - ra)) : Measure(child);

        var cw = cst.Width ?? mw;
        if (cst.Left is { } l0 && cst.Right is { } r0)
            cw = pcbW - l0 - r0;

        var ch = cst.Height ?? mh;
        if (cst.Top is { } t0 && cst.Bottom is { } b0)
            ch = pcbH - t0 - b0;

        float cx;
        if (cst.Left is { } l)
            cx = pcbX + l;
        else if (cst.Right is { } r)
            cx = pcbX + pcbW - cw - r;
        else
            cx = pcbX;

        float cy;
        if (cst.Top is { } t)
            cy = pcbY + t;
        else if (cst.Bottom is { } b)
            cy = pcbY + pcbH - ch - b;
        else
            cy = pcbY;

        Arrange(child, cx, cy, cw, ch);
    }

    private static bool IsGridBox(ComputedStyle st)
        => st.IsGrid && (st.GridColumns.Length > 0 || st.GridRows.Length > 0);

    // 열 수 = 열 템플릿 길이(행 템플릿만 있으면 폭을 채우는 1열)
    private static int GridColumnCount(ComputedStyle st) => Math.Max(1, st.GridColumns.Length);

    private static readonly string[] SingleFrColumn = { "1fr" };

    private static string[] ColumnTracks(ComputedStyle st) => st.GridColumns.Length > 0 ? st.GridColumns : SingleFrColumn;

    // 열별 최대 내용 너비(자연 크기)
    private static float[] GridColContent((float w, float h)[] sizes, int cols)
    {
        var colContent = new float[cols];
        for (var i = 0; i < sizes.Length; i++)
            colContent[i % cols] = MathF.Max(colContent[i % cols], sizes[i].w);
        return colContent;
    }

    /// <summary>
    /// Resolves track sizes. px (numbers) and auto (content size) are subtracted first, then % tracks divide the remaining space (excluding gaps)
    /// (normalized proportionally if the total exceeds 100%), and fr tracks divide what is left proportionally.
    /// If there are more tracks than the template, the extra ones are auto. When measuring content size (<paramref name="measuring"/>), % and fr are also treated as auto.
    /// </summary>
    private static float[] ResolveTracks(string[] tokens, float[] content, float avail, float gap, bool measuring)
    {
        var n = content.Length;
        var size = new float[n];
        var pct = new float[n];
        var fr = new float[n];
        float fixedSum = 0, pctSum = 0, frSum = 0;

        for (var i = 0; i < n; i++)
        {
            var t = i < tokens.Length ? tokens[i].Trim() : "auto";
            if (!measuring && t.EndsWith("fr", StringComparison.OrdinalIgnoreCase) &&
                float.TryParse(t[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0)
            {
                fr[i] = f;
                frSum += f;
            }
            else if (!measuring && t.EndsWith('%') &&
                     float.TryParse(t[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && p > 0)
            {
                pct[i] = p;
                pctSum += p;
            }
            else if (!t.EndsWith("fr", StringComparison.OrdinalIgnoreCase) && !t.EndsWith('%') &&
                     float.TryParse(t.Replace("px", "").Replace("dp", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
            {
                size[i] = px;
                fixedSum += px;
            }
            else
            {
                size[i] = content[i]; // auto (측정 중의 fr·% 포함)
                fixedSum += content[i];
            }
        }

        var free = MathF.Max(0, avail - fixedSum - gap * MathF.Max(0, n - 1));
        var norm = pctSum > 100f ? 100f / pctSum : 1f;
        float pctUsed = 0;
        for (var i = 0; i < n; i++)
        {
            if (pct[i] <= 0)
                continue;
            size[i] = free * pct[i] * norm / 100f;
            pctUsed += size[i];
        }

        var rest = MathF.Max(0, free - pctUsed);
        for (var i = 0; i < n; i++)
            if (fr[i] > 0)
                size[i] = frSum > 0 ? rest * fr[i] / frSum : 0;

        return size;
    }

    // 행별 최대 내용 높이 — 칸은 열 폭으로 잰다(글자가 열 폭에 맞춰 줄바꿈)
    private static float[] GridRowContent(LayoutBox box, float[] colW, int cols)
    {
        var n = box.Children.Count;
        var rowContent = new float[(n + cols - 1) / cols];
        for (var i = 0; i < n; i++)
            rowContent[i / cols] = MathF.Max(rowContent[i / cols], Measure(box.Children[i], colW[i % cols]).h);
        return rowContent;
    }

    // grid 내용 크기 = 트랙 합 + gap. 열: fr 트랙은 CSS 처럼 가장 큰 (내용 폭 / fr) 비율 × fr(같은 fr 이면 같은 폭), % 는 내용 폭.
    // 그 폭(width 고정이면 그 폭, 부모가 폭을 정하거나 가용 폭을 넘으면 가용 폭)으로 배치 때와 같게 트랙을 정하고 그 칸 폭으로 행 높이를 잰다
    // — 측정 높이와 배치 높이가 어긋나 글자가 grid 밖으로 넘치지 않게.
    private static (float w, float h) MeasureGrid(LayoutBox box, float? avail)
    {
        var st = box.Style;
        var cols = GridColumnCount(st);
        var tracks = ColumnTracks(st);
        var gaps = st.Gap * Math.Max(0, cols - 1);
        var sizes = box.Children.Select(c => Measure(c)).ToArray();
        var colContent = GridColContent(sizes, cols);
        var natural = ResolveTracks(tracks, colContent, 0, st.Gap, measuring: true);
        FrMaxContent(tracks, colContent, natural);

        var naturalInner = natural.Sum() + gaps;
        var inner = naturalInner;
        if (InnerLimit(box, avail) is { } limit && (FixedWidth(box) is not null || box.WidthOverridden || inner > limit + TextWrap.Tolerance))
            inner = limit;
        var colW = ResolveTracks(tracks, colContent, inner, st.Gap, measuring: false);
        var rowContent = GridRowContent(box, colW, cols);
        var rowH = ResolveTracks(st.GridRows, rowContent, 0, st.Gap, measuring: true);

        var contentH = rowH.Sum() + st.Gap * Math.Max(0, rowH.Length - 1);
        // 보고하는 폭 = min(내용 폭, 트랙을 정한 폭)(CSS fit-content 기여) — 늘어나는 grid 가 가용 폭 전체를 보고하면
        // 내용 크기로 줄어드는 조상(가운데 정렬 상자·다이얼로그)이 창 폭으로 커진다. 행 높이는 위의 폭(배치 폭)으로 잰 값.
        return (st.Width ?? MathF.Min(naturalInner, inner) + st.Padding.Horizontal, st.Height ?? contentH + st.Padding.Vertical);
    }

    // 측정(내용 크기) 때 fr 열 트랙 = fr × max(내용 폭 / fr) — CSS 의 폭 미정 grid 와 같음
    private static void FrMaxContent(string[] tokens, float[] content, float[] size)
    {
        float ratio = 0;
        for (var i = 0; i < size.Length; i++)
            if (FrOf(tokens, i) is { } f)
                ratio = MathF.Max(ratio, content[i] / f);
        for (var i = 0; i < size.Length; i++)
            if (FrOf(tokens, i) is { } f)
                size[i] = f * ratio;
    }

    private static float? FrOf(string[] tokens, int i)
    {
        var t = i < tokens.Length ? tokens[i].Trim() : "auto";
        return t.EndsWith("fr", StringComparison.OrdinalIgnoreCase) &&
               float.TryParse(t[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0
            ? f
            : null;
    }

    // 자식은 칸(열 폭 × 행 높이)을 채운다
    private static void ArrangeGrid(LayoutBox box, float x, float y, float w, float h)
    {
        var st = box.Style;
        var innerX = x + st.Padding.Left;
        var innerY = y + st.Padding.Top;
        var innerW = w - st.Padding.Horizontal - box.GutterX;
        var innerH = h - st.Padding.Vertical - box.GutterY;
        var cols = GridColumnCount(st);
        var gap = st.Gap;

        // 열 폭을 먼저 정하고 그 칸 폭으로 행 높이를 잰다
        var sizes = box.Children.Select(c => Measure(c)).ToArray();
        var colContent = GridColContent(sizes, cols);
        var colW = ResolveTracks(ColumnTracks(st), colContent, innerW, gap, measuring: false);
        var rowContent = GridRowContent(box, colW, cols);
        var rowH = ResolveTracks(st.GridRows, rowContent, innerH, gap, measuring: false);

        var rowY = innerY;
        for (var r = 0; r < rowH.Length; r++)
        {
            var cx = innerX;
            for (var c = 0; c < cols && r * cols + c < box.Children.Count; c++)
            {
                Arrange(box.Children[r * cols + c], cx, rowY, colW[c], rowH[r]);
                cx += colW[c] + gap;
            }

            rowY += rowH[r] + gap;
        }
    }
}
