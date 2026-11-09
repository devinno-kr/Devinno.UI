using Devinno.UI.Binding;
using Devinno.UI.Interaction;
using Devinno.UI.Markup;
using Devinno.UI.Styling;

namespace Devinno.UI.Layout;

// 공통 + 컨테이너 태그 빌더 (DvRepeat DvTabs/DvTab DvExpander DvStepBar). 속성은 모두 AttrReader 로 읽는다 — 정의는 TagSchema.
public sealed partial class LayoutEngine
{
    // 다른 노드(자식 DvTab 등)의 리더 — 그 노드 핸들의 덮어쓰기를 함께 본다(그 리더의 Child 도 같은 조회)
    private AttrReader Reader(ElementNode node, object? ctx) => new(node, ctx, OverridesOf?.Invoke(node), OverridesOf);

    // 합성 노드(머리글 줄·탭 항목 등) — 클래스(와 탭 disabled 원문)만 가진다
    private static ElementNode Synthetic(string tag, string cls, string? disabled = null) => disabled is null
        ? new ElementNode { Tag = tag, Attributes = { ["class"] = cls } }
        : new ElementNode { Tag = tag, Attributes = { ["class"] = cls, ["disabled"] = disabled } };

    /// <summary>
    /// DvRepeat: for each item in <c>items</c>, builds the children (template) in that item's scope (<see cref="ItemScope"/>).
    /// Empty, null, or not a collection = no children (height 0). Strings are not treated as collections.
    /// </summary>
    private LayoutBox BuildRepeat(ElementNode node, object? ctx, Func<ElementNode, object?, IEnumerable<LayoutBox>> buildChild)
    {
        var box = BuildSelf(node, ctx);
        var itemsRaw = Reader(node, ctx).Raw("items");
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

    // DvTabs — [머리글 줄(.tab-header: 탭마다 .tab / 선택 .tab-on) · 구분선(.tab-rule) · 선택된 DvTab 내용(.tab-body)].
    // 선택: value 가 있으면 VM 값과 맞는 탭(없으면 첫 탭), 없으면 화면이 기억한 번호. 나머지 탭 내용은 만들지 않는다.
    // 머리글을 누르면 DvSurface 가 그 탭 value 를 쓰거나(양방향) 기억한 번호를 바꾼다(value 없음).
    // value·readonly 는 BuildSelf 의 DvSelect 공용 블록이 먼저 읽는다(선택지는 여기서 DvTab 으로 만든다 — placeholder·items 는 탭에 없다).
    private LayoutBox BuildTabs(LayoutBox box, object? vm, Func<ElementNode, IEnumerable<LayoutBox>> buildChild)
    {
        var tabs = box.Node.Children.Where(c => c.Tag == "DvTab" && IsVisibleOf(c, vm)).ToList();
        var r = Reader(box.Node, vm);
        var bound = r.Has("value");
        box.SelectOptions = tabs.Select(t =>
        {
            var tr = r.Child(t);
            var raw = tr.Text("value");
            var value = SelectValue.TryConvert(raw, box.SelectValueType, out var converted) ? converted : raw;
            // text — 탭 핸들의 Text 덮어쓰기가 이긴다(자식 노드도 자기 덮어쓰기로 읽는다)
            return new SelectOption(value, tr.Text("text") ?? raw ?? "", null);
        }).ToList();
        var selected = bound
            ? box.SelectOptions.FindIndex(o => SelectValue.Matches(o.Value, box.InputValue, box.SelectValueType))
            : TabIndexOf?.Invoke(box.Instance) ?? 0;
        if (selected < 0 || selected >= tabs.Count)
            selected = 0;
        box.SelectedIndex = tabs.Count == 0 ? -1 : selected;

        var header = BuildSelf(Synthetic("DvTabHeader", "tab-header"), vm);
        for (var i = 0; i < tabs.Count; i++)
        {
            // disabled 는 머리글 항목에 복사 — 탭 핸들의 Enabled 덮어쓰기가 있으면 그 값, 없으면 마크업 원문
            var tr = r.Child(tabs[i]);
            var disabled = tr.Overridden("disabled") ? (tr.Value("disabled") is true ? "true" : "false") : tr.Raw("disabled");
            var item = Synthetic("DvTabItem", i == box.SelectedIndex ? "tab tab-on" : "tab", disabled);
            var itemBox = BuildSelf(item, vm);
            itemBox.ResolvedText = box.SelectOptions[i].Text;
            itemBox.SegmentOwner = box;
            itemBox.SegmentIndex = i;
            header.Children.Add(itemBox);
        }
        box.Children.Add(header);
        box.Children.Add(BuildSelf(Synthetic("DvTabRule", "tab-rule"), vm));

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

    // DvExpander — [제목 줄(.expander-header: ▶/▼ 아이콘 + header 글자) · 펼쳤으면 내용(.expander-body)]. 접으면 내용은 만들지 않는다(탭과 같음).
    // 펼침: expanded 가 양방향이면 VM 값, 단방향·고정이면 그 값(핸들이 덮어쓰면 그 값 — 제목을 눌러도 안 바뀜), 없으면 화면이 기억.
    // 제목을 누르면 DvSurface 가 뒤집어 쓴다.
    private LayoutBox BuildExpander(LayoutBox box, object? vm, Func<ElementNode, IEnumerable<LayoutBox>> buildChild)
    {
        var node = box.Node;
        var r = Reader(node, vm);
        var raw = r.Raw("expanded");
        var (expr, _, ov) = ValueOf(r, raw);
        box.ValueBindExpr = expr;
        bool Expanded(object? v) => AttrRules.Bool(v, false, node.Tag, "expanded");
        box.ToggleOn = ov is not null ? Expanded(ov.CellValue)
            : expr is not null && BindingResolver.IsTwoWay(expr) ? Expanded(BindingResolver.Resolve(vm, $"{{{BindingResolver.ExtractPath(expr)}}}"))
            : raw is not null ? Expanded(BindingResolver.ResolveValueOrLiteral(vm, raw))
            : ExpanderOf?.Invoke(box.Instance) ?? false;

        var header = BuildSelf(Synthetic("DvExpanderHeader", "expander-header"), vm);
        header.ResolvedText = r.Text("header") ?? "";
        header.Icon = box.ToggleOn ? "fa-chevron-down" : "fa-chevron-right";
        header.ExpanderOwner = box;
        if (box.Disabled)
            header.Disabled = true;
        box.Children.Add(header);

        var reveal = ExpanderRevealOf?.Invoke(box.Instance, box.ToggleOn);
        if (box.ToggleOn || reveal is not null)
        {
            var body = BuildSelf(Synthetic("DvExpanderBody", "expander-body"), vm);
            foreach (var ch in node.Children)
                body.Children.AddRange(buildChild(ch));
            if (reveal is not null)
            {
                body.Reveal = reveal;
                body.Style.Overflow = Overflow.Hidden; // 보이는 높이 밖은 잘림(움직임이 끝나면 다시 만들어 원래대로)
            }
            body.RevealKey = box.Instance;
            box.Children.Add(body);
        }
        return box;
    }

    // DvStepBar — steps = 목록(항목 글자) 또는 "가,나,다"(쉼표로 나눔), current = 1부터(0 = 시작 전, 개수보다 크면 모두 끝)
    private static void BuildStepBar(LayoutBox box, AttrReader r)
    {
        var node = r.Node;
        var stepsRaw = r.Raw("steps");
        box.StepNames = BindingResolver.ResolveValueOrLiteral(r.Context, stepsRaw) switch
        {
            string text => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            System.Collections.IEnumerable list => HandlerErrors.Snapshot(list.Cast<object?>(), PathOf(stepsRaw), node.Tag)
                .Select(o => HandlerErrors.Text(o, PathOf(stepsRaw), node.Tag) ?? "").ToArray(),
            _ => Array.Empty<string>(),
        };
        box.StepCurrent = (int)MathF.Round(ToFloat(r, "current", 0f));
    }
}
