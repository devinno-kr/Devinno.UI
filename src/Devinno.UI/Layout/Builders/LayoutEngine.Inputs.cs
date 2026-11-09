using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Text;
using Devinno.UI.Interaction;
using Devinno.UI.Markup;
using Devinno.UI.Styling;

namespace Devinno.UI.Layout;

// 기본·입력 태그 빌더 (DvLabel DvButton DvIcon DvImage DvVideo DvToggle DvCheckBox DvRadio DvKnob DvSlider DvInput
// DvSelect/DvOption DvSegment DvListBox DvTreeView DvDateTimeInput DvColorInput). 속성은 모두 AttrReader 로 읽는다 — 정의는 TagSchema.
// DvProgress 는 계기 공용 블록(BuildDial, LayoutEngine.Instruments.cs)에서 만든다.
public sealed partial class LayoutEngine
{
    // BuildSelf 에서 공통 속성 다음에 부른다. 태그마다 읽는 순서는 옮기기 전과 같다(DvButton = 래칭 → 아이콘 → 글자).
    private void BuildInputTag(LayoutBox box, AttrReader r)
    {
        switch (r.Node.Tag)
        {
            case "DvLabel":
                BuildText(box, r);
                break;
            case "DvButton":
                BuildLatch(box, r);
                BuildIcon(box, r);
                BuildText(box, r);
                break;
            case "DvIcon":
                BuildIcon(box, r);
                break;
            case "DvImage":
                box.ImageSrc = r.Text("src");
                box.ImageFit = Widgets.ImageFitParser.Parse(AttrRules.Enum(r, "fit"));
                break;
            case "DvVideo":
                BuildVideo(box, r);
                break;
            case "DvToggle":
                BuildToggle(box, r);
                break;
            case "DvCheckBox":
                BuildToggle(box, r);
                BuildText(box, r);
                break;
            case "DvRadio":
                BuildRadio(box, r);
                BuildText(box, r);
                break;
            case "DvKnob" or "DvSlider":
                BuildKnob(box, r);
                break;
            case "DvInput":
                BuildInput(box, r);
                break;
            case "DvSelect" or "DvSegment" or "DvTabs":
                BuildSelect(box, r);
                break;
            case "DvListBox":
                BuildListBox(box, r);
                break;
            case "DvTreeView":
                BuildTreeView(box, r);
                break;
            case "DvDateTimeInput" or "DvColorInput":
                BuildPicker(box, r);
                break;
        }
    }

    // text 속성(없으면 안쪽 글자). 핸들이 코드로 쓴 Text 는 바인딩 없이 그대로.
    private static void BuildText(LayoutBox box, AttrReader r)
    {
        box.ResolvedText = r.Overridden("text") ? r.Text("text") ?? ""
            : BindingResolver.ResolveText(r.Context, r.Raw("text") ?? r.Node.Text ?? "") ?? "";
    }

    private static void BuildIcon(LayoutBox box, AttrReader r)
    {
        if (r.Raw("icon") is not { } iconRaw)
            return;
        box.Icon = BindingResolver.ResolveText(r.Context, iconRaw);
        if (!string.IsNullOrWhiteSpace(box.Icon) && !IconFont.TryResolve(box.Icon, out _))
            WarnIcon(box.Icon);
    }

    // 래칭 버튼 — 켜짐 = value 가 참. 양방향이면 누를 때 뒤집어 쓴다(DvToggle 과 같은 커밋 경로)
    private void BuildLatch(LayoutBox box, AttrReader r)
    {
        if (r.Raw("value") is not { } latchExpr)
            return;
        var (lx, _, lov) = ValueOf(r, latchExpr);
        box.ValueBindExpr = _valueOverride ?? lx;
        box.ToggleOn = lov is not null ? AttrRules.Bool(lov.CellValue, false, r.Node.Tag, "value") : LatchOn(r);
    }

    // 래칭 버튼 켜짐 — value 가 참(공용 규칙)
    private static bool LatchOn(AttrReader r)
    {
        var vm = r.Context;
        var ve = r.Raw("value");
        var cur = ve is not null && BindingResolver.IsTwoWay(ve)
            ? BindingResolver.Resolve(vm, $"{{{BindingResolver.ExtractPath(ve)}}}")
            : BindingResolver.ResolveValueOrLiteral(vm, ve);
        return AttrRules.Bool(cur, false, r.Node.Tag, "value");
    }

    private void BuildToggle(LayoutBox box, AttrReader r)
    {
        var vm = r.Context;
        var (ve, target, vov) = ValueOf(r, r.Raw("value"));
        box.ValueBindExpr = ve;
        box.ValueTarget = target;
        object? cur = null;
        if (vov is not null)
            cur = vov.CellValue;
        else if (ve is not null && BindingResolver.IsTwoWay(ve))
            cur = BindingResolver.Resolve(target ?? vm, $"{{{BindingResolver.ExtractPath(ve)}}}");
        else
            cur = BindingResolver.ResolveValueOrLiteral(vm, ve);
        box.ToggleOn = AttrRules.Bool(cur, false, r.Node.Tag, "value");
        box.ReadOnly = InputReadOnly(r, _valueOverride);
    }

    /// <summary>
    /// <c>DvRadio</c>: checked = the group value equals this button's <c>option</c>. The group value is <c>value</c> (the VM property if two-way; for one-way/literal, that value or the value overridden by a handle);
    /// without <c>value</c>, it is the value of the group remembered by the screen (by <c>group</c> name, or this element alone if there is no name and only a handle).
    /// </summary>
    private void BuildRadio(LayoutBox box, AttrReader r)
    {
        var node = r.Node;
        var vm = r.Context;
        var option = r.Text("option") ?? "";
        box.RadioOption = option;
        var ve = r.Raw("value");
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
                ? AttrRules.ParseBool(ov.CellValue) == true
                : SelectValue.Matches(option, cur, cur?.GetType());
        }
        else
        {
            var group = r.Text("group");
            box.RadioGroup = group is { Length: > 0 } ? group : ControlOf?.Invoke(node) is not null ? node : null;
            box.ToggleOn = box.RadioGroup is { } key && RadioGroupOf?.Invoke(key) is { } picked && picked == option;
        }

        // 쓸 수 있는 값 = 양방향 바인딩, 또는 value 없이 화면이 기억하는 묶음
        box.ReadOnly = ReadOnlyOf(r, ve is not null ? BindingResolver.IsTwoWay(ve) : box.RadioGroup is not null);
    }

    // DvKnob·DvSlider — 같은 값 필드(Knob*)를 쓴다
    private void BuildKnob(LayoutBox box, AttrReader r)
    {
        var vm = r.Context;
        var (ve, target, vov) = ValueOf(r, r.Raw("value"));
        box.ValueBindExpr = ve;
        box.ValueTarget = target;
        var cur = vov is not null ? vov.CellValue
            : ve is not null && BindingResolver.IsTwoWay(ve)
            ? BindingResolver.Resolve(target ?? vm, $"{{{BindingResolver.ExtractPath(ve)}}}")
            : BindingResolver.ResolveValueOrLiteral(vm, ve);
        box.KnobValue = (float)AttrRules.Number(cur, 0, r.Node.Tag, "value");
        box.KnobMin = ToFloat(r, "min", 0f);
        box.KnobMax = ToFloat(r, "max", 100f);
        var step = ToFloat(r, "step", 1f);
        box.KnobStep = step > 0 ? step : 1f;
        box.KnobUnit = r.Text("unit");
        box.ReadOnly = InputReadOnly(r, _valueOverride);
        if (r.Node.Tag == "DvKnob")
        {
            // input-mode — 공용 목록 규칙(바인딩 가능, 모르는 값 = drag + 경고)
            box.KnobInput = AttrRules.Enum(r, "input-mode") switch
            {
                "tap" => KnobInputMode.Tap,
                "both" => KnobInputMode.Both,
                _ => KnobInputMode.Drag,
            };
        }
        else
        {
            // 슬라이더 — direction = 값이 커지는 쪽(DvProgress 와 같은 값), show-value = 손잡이 옆 값 글자
            box.Direction = AttrRules.Enum(r, "direction") ?? "right";
            box.ShowValue = AttrRules.Bool(r, "show-value");
        }
    }

    private void BuildInput(LayoutBox box, AttrReader r)
    {
        var vm = r.Context;
        string? Attr(string name) => r.Text(name);
        double? Num(string name) => AttrRules.NumberOrNull(r, name);

        var (ve, target, vov) = ValueOf(r, r.Raw("value"));
        box.ValueBindExpr = ve;
        box.ValueTarget = target;
        box.InputValue = vov is not null ? vov.CellValue
            : ve is not null && BindingResolver.IsTwoWay(ve)
            ? BindingResolver.Resolve(target ?? vm, $"{{{BindingResolver.ExtractPath(ve)}}}")
            : BindingResolver.ResolveValueOrLiteral(vm, ve);
        box.InputKind = AttrRules.Enum(r, "keyboard-type") switch
        {
            "numeric" => DvEditKind.Numeric,
            "password" => DvEditKind.Password,
            _ => DvEditKind.Text
        };
        box.InputMultiline = AttrRules.Bool(r, "multiline");
        box.InputPlaceholder = Attr("placeholder");
        box.InputFormat = Attr("format");
        box.InputMin = Num("min");
        box.InputMax = Num("max");
        box.Unit = Attr("unit");
        box.ReadOnly = InputReadOnly(r, _valueOverride);
        box.InputStep = Num("step") is > 0 and var step ? step : 1d;
        if (box.InputKind == DvEditKind.Numeric && AttrRules.Bool(r, "show-stepper"))
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

    // DvSelect·DvSegment — DvTabs 도 이 블록을 쓴다(값·옵션 형식; 옵션은 BuildTabs 가 DvTab 으로 바꿈)
    private void BuildSelect(LayoutBox box, AttrReader r)
    {
        var vm = r.Context;
        var ve = r.Raw("value");
        var (sx, target, vov) = ValueOf(r, ve);
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
        box.SelectOptions = SelectValue.Options(r, box.SelectValueType);
        box.SelectedIndex = box.SelectOptions.FindIndex(o => SelectValue.Matches(o.Value, current, box.SelectValueType));
        box.InputPlaceholder = r.Text("placeholder");
        box.ReadOnly = InputReadOnly(r, _valueOverride);
        if (r.Node.Tag == "DvSegment")
            box.SegmentFill = AttrRules.Bool(r, "fill");
    }

    // DvDateTimeInput·DvColorInput — 팝업 선택기
    private void BuildPicker(LayoutBox box, AttrReader r)
    {
        var vm = r.Context;
        var ve = r.Raw("value");
        var (px, target, vov) = ValueOf(r, ve);
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

        string? Attr(string name) => r.Text(name);
        box.InputValue = current;
        box.InputPlaceholder = Attr("placeholder");
        box.ReadOnly = InputReadOnly(r, _valueOverride);
        if (r.Node.Tag == "DvDateTimeInput")
        {
            box.PickerMode = PickerValue.ResolveMode(AttrRules.Enum(r, "mode", "using the value type's mode"), box.PickerValueType);
            box.PickerSeconds = AttrRules.Bool(r, "show-seconds");
            box.PickerFormat = Attr("format");
        }
    }

    /// <summary>
    /// <c>DvListBox</c> value: single selection uses <c>value</c> (same as DvSelect); with <c>selection-mode="multi"</c>, <c>selected-items</c> (a list of values).
    /// Value ownership rules are the same as other value widgets (binding = VM, no attribute + handle = handle slot, one-way/literal = read-only).
    /// </summary>
    private void BuildListBox(LayoutBox box, AttrReader r)
    {
        var node = r.Node;
        var vm = r.Context;
        box.ListMulti = AttrRules.Enum(r, "selection-mode") == "multi";
        var attr = box.ListMulti ? "selected-items" : "value";
        var raw = r.Raw(attr);
        var (expr, target, ov) = ValueOf(r, raw);
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
            var elem = valueType is { } vt && vt != typeof(string) ? GridInteraction.ElementType(Nullable.GetUnderlyingType(vt) ?? vt) : null;
            box.SelectValueType = elem == typeof(object) ? null : elem;
            var picked = current is System.Collections.IEnumerable e and not string
                ? HandlerErrors.Snapshot(e.Cast<object?>(), PathOf(raw), node.Tag)
                : new List<object?>();
            box.SelectOptions = SelectValue.Options(r, box.SelectValueType);
            box.ListSelected = box.SelectOptions.Select(o => picked.Any(c => SelectValue.Matches(o.Value, c, box.SelectValueType))).ToArray();
            box.InputValue = picked;
        }
        else
        {
            box.SelectValueType = valueType == typeof(object) ? null : valueType;
            box.SelectOptions = SelectValue.Options(r, box.SelectValueType);
            box.SelectedIndex = box.SelectOptions.FindIndex(o => SelectValue.Matches(o.Value, current, box.SelectValueType));
            box.ListSelected = box.SelectOptions.Select((_, i) => i == box.SelectedIndex).ToArray();
            box.InputValue = current;
        }

        box.ReadOnly = InputReadOnly(r, null, attr);
    }

    /// <summary>
    /// <c>DvTreeView</c>: <c>items</c> (root list) + <c>item-children</c> (path to the child list) + <c>item-text</c> + <c>item-value</c> (omitted = the item itself).
    /// Only expanded branches become rows (row height 40, indented per depth). Single selection: <c>value</c> holds the selected item's value (value ownership rules are the same as other value widgets).
    /// Expansion is remembered by the screen (per item object). Cyclic references (the same object as an ancestor appearing again) skip that branch, and depth is limited to 64.
    /// </summary>
    private void BuildTreeView(LayoutBox box, AttrReader r)
    {
        var node = r.Node;
        var vm = r.Context;
        var raw = r.Raw("value");
        var (expr, target, ov) = ValueOf(r, raw);
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
        box.ReadOnly = InputReadOnly(r, null);

        var itemsRaw = r.Raw("items");
        var textPath = r.Raw("item-text");
        var valuePath = r.Raw("item-value");
        var childPath = r.Raw("item-children");
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
                var open = hasKids && TreeExpandedOf?.Invoke(box.Instance, item) == true;
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
        box.SelectedIndex = rows.FindIndex(row => SelectValue.Matches(row.Value, current, box.SelectValueType));
    }

    // DvSegment — 선택지(DvOption)마다 항목 상자(DvSegmentItem, .segment-item / 선택된 것 .segment-item-on)를 만들어 한 줄로 붙인다.
    // 누르면 그 선택지 값을 세그먼트의 value 에 쓴다(DvSurface — DvSelect 와 같은 커밋 경로).
    private void AddSegmentItems(LayoutBox box, object? vm)
    {
        if (box.Node.Tag != "DvSegment")
            return;
        for (var i = 0; i < box.SelectOptions.Count; i++)
        {
            var item = Synthetic("DvSegmentItem", i == box.SelectedIndex ? "segment-item segment-item-on" : "segment-item");
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
            var item = Synthetic("DvListItem", on ? "listbox-item listbox-item-on" : "listbox-item");
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

    // DvTreeView 행(DvTreeItem, .treeview-item / 고른 것 .treeview-item-on) — 깊이만큼 들여쓰고, 가지는 ▶/▼ 아이콘, 잎은 아이콘 자리만큼 비운다.
    private void AddTreeItems(LayoutBox box, object? vm)
    {
        if (box.Node.Tag != "DvTreeView")
            return;
        for (var i = 0; i < box.TreeRows.Count; i++)
        {
            var row = box.TreeRows[i];
            var indent = TreeIndentBase + row.Depth * TreeIndentStep + (row.HasChildren ? 0 : TreeChevron + IconTextGap);
            var item = new ElementNode
            {
                Tag = "DvTreeItem",
                Attributes =
                {
                    ["class"] = i == box.SelectedIndex ? "treeview-item treeview-item-on" : "treeview-item",
                    ["style"] = "padding:0 12 0 " + indent.ToString(CultureInfo.InvariantCulture),
                },
            };
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

    // DvVideo — source(앱이 만든 IFrameSource) 가 우선, 없으면 src 파일을 요소가 가진 재생기로
    private void BuildVideo(LayoutBox box, AttrReader r)
    {
        var vm = r.Context;
        box.ImageFit = Widgets.ImageFitParser.Parse(AttrRules.Enum(r, "fit"));
        var srcExpr = r.Raw("src");
        if (r.Raw("source") is { } sourceExpr)
        {
            var value = BindingResolver.ResolveValueOrLiteral(vm, sourceExpr);
            box.VideoSource = value as Video.IFrameSource;
            if (value is not null && box.VideoSource is null)
                Warnings.Warn($"Devinno.UI: DvVideo source='{sourceExpr}' is a {value.GetType().Name}, not an IFrameSource; nothing is shown");
            if (srcExpr is not null)
                Warnings.Warn("Devinno.UI: DvVideo has both source and src; src is ignored");
            if (box.VideoSource is Video.VideoPlayer own)
                BindVideoTime(box, r, own);
            return;
        }

        var path = BindingResolver.ResolveText(vm, srcExpr);
        if (string.IsNullOrWhiteSpace(path))
            return;
        var playing = AttrRules.Bool(r, "playing");
        var loop = AttrRules.Bool(r, "loop");
        var live = VideoStore is not null;
        var player = _videos.Get(box.Instance, () => new Video.VideoPlayer(path, poster: !live), vp => vp.Path == path);
        player.Playing = playing;
        player.Loop = loop;
        box.VideoSource = player;
        box.VideoError = player.Error;
        BindVideoTime(box, r, player);
    }

    // position = 뷰모델 값이 바뀌었으면 그 위치로 이동(처음 본 재생기는 처음부터 — 뷰모델에 0 이 써진다). {Bind} 면 재생 위치를 되써 넣는다.
    // duration = {Bind} 일 때만 의미(길이를 써 넣음)
    private void BindVideoTime(LayoutBox box, AttrReader r, Video.VideoPlayer player)
    {
        if (r.Raw("position") is { } posExpr)
        {
            var v = AttrRules.Number(BindingResolver.ResolveValueOrLiteral(r.Context, posExpr), 0, r.Node.Tag, "position");
            if (double.IsNaN(player.BoundPosition))
                player.BoundPosition = v;
            else if (Math.Abs(v - player.BoundPosition) > 0.0005)
            {
                player.Seek(TimeSpan.FromSeconds(Math.Max(0, v)));
                player.BoundPosition = v;
            }
            if (BindingResolver.IsTwoWay(posExpr))
                box.VideoPositionPath = BindingResolver.ExtractPath(posExpr);
        }
        if (r.Raw("duration") is { } durExpr && BindingResolver.IsTwoWay(durExpr))
            box.VideoDurationPath = BindingResolver.ExtractPath(durExpr);
        if (box.VideoPositionPath is not null || box.VideoDurationPath is not null)
            BoundVideos.Add(box);
    }
}
