# DVML language

Covers: the smallest program, `vm` and project setup, markup basics, the common attributes and which tags take them, how every value is read (true/false, number, word, color, length, theme word), binding syntax and two-way binding, the value widgets (value attribute, `readonly`, handle, change event), attributes that are not bound, click / `hold` / `disabled`, `onbeforecommit`, VM constants, `HandlerError`, attribute naming and a glossary.
Read first: [the rules every screen follows](llm-rules.md#principles) · [the shape of a complete screen](llm-rules.md#screen-shape)
Not here: [layout and sizes](dvml-layout.md#size) · [style keys](dvml-style.md#style-keys) · [theme tokens](dvml-style.md#theme-tokens) · [each widget's attributes](dvml-widgets.md#semantic-components) · [handles and what the generator creates](dvml-code.md#handles) · [build diagnostics DVML001–021](dvml-diagnostics.md#build-diagnostics) · [threads and `Invalidate()`](llm-rules.md#updates-threads)

<a id="getting-started"></a>
## Getting started

This is the smallest program. One screen is a `partial` class deriving from `DvPage` (which doubles as the VM) plus a dvml file whose root `vm="…"` points to that class; `DvApp` holds the page and puts it on an OpenTK window (`ViewWindow`).

```csharp
using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Pages.Add(new MainPage());                  // the first page added is the start page
using var window = new ViewWindow(app, 1280, 800, "My HMI");
window.Run();

public sealed partial class MainPage : DvPage   // the markup's vm; the generator pairs it with the dvml file
{
    public bool Running { get; set; } = true;
    public bool AutoMode { get; set; }
    public string StatusText => Running ? "Running" : "Stopped";
    public string StatusTone => Running ? "success" : "danger";
    public void Start() => Running = true;
    public void Stop() => Running = false;
}
```

Below is `MainPage.dvml`.

```xml
<DvPageFrame vm="MainPage" title="Water plant line 2" subtitle="Live operation">
  <DvRow>
    <DvBadge tone="{StatusTone}" text="{StatusText}"/>
    <DvSpacer/>
    <DvButton variant="primary" text="Start" onclick="Start"/>
    <DvButton variant="danger" text="Emergency stop" onclick="Stop"/>
  </DvRow>
  <DvGrid columns="4">
    <DvStat label="Flow" value="1,284" unit="m3/h" delta="+3.2%" tone="success"/>
    <DvStat label="Turbidity" value="0.18" unit="NTU" delta="-0.04" tone="success"/>
    <DvStat label="Power" value="412" unit="kW" delta="+8.1%" tone="warning"/>
    <DvStat label="Availability" value="98.6" unit="%" delta="On target" tone="info"/>
  </DvGrid>
  <DvRow align="stretch" grow="1">
    <DvCard title="Level" badge="3 units" grow="1">
      <DvRow justify="between">
        <DvTank value="64" unit="%" alarm-zones="0:70:#3b82f6 70:88:#f59e0b 88:100:#ef4444" style="width:84; height:200"/>
        <DvGauge value="58" ticks="20" minor-ticks="5" style="width:160; height:160"/>
      </DvRow>
    </DvCard>
    <DvCard title="Equipment status" width="310">
      <DvStatusItem label="Intake pump #1" state="run" status="Running"/>
      <DvStatusItem label="Filter B" state="fault" status="Alarm" blink="true"/>
      <DvSpacer/>
      <DvSwitch label="Auto mode" value="{Bind AutoMode}"/>
    </DvCard>
  </DvRow>
</DvPageFrame>
```

- Multiple pages, a top bar or a sidebar: [the complete DvApp example](dvml-app.md#app-example). Other hosts (WinForms `ViewControl`, self-drawn `DvSurface`, PNG rendering `DvRenderer`): [host summary](dvml-app.md#hosts).
- Where to put the `.dvml` file and how the generator picks it up: [generator setup](dvml-code.md#generator-setup).
- Choosing `app.Theme` (`Dark`, `Light`, `Graphite`, `Carbon`): [built-in themes](dvml-style.md#built-in-themes). The window size is decided by the host, not by the markup: [hosts](dvml-app.md#hosts).

<a id="project-setup"></a>
## The `vm` attribute

- The root's `vm` points to the VM class. The file name plays no part in the connection (giving the dvml file the class's name is only a convention).
- `vm` is the **full class name including the namespace** (`vm="MyApp.Pages.MainPage"`). The samples in these docs declare their classes without a namespace, so they use the bare name.
- A wrong name is build error [DVML005](dvml-diagnostics.md#dvml005).

<a id="markup-basics"></a>
## Markup basics

- Markup MUST be **well-formed XML** (one root element, every tag closed, attributes quoted). Write `&` and `<` in text as `&amp;` and `&lt;`.
- **Tag names and attribute names are case-sensitive.** `onclick` (correct), `onClick` (wrong).
- The root element always fills the whole window (or drawing area). The root's `width`/`height` are ignored.
- Unknown tags are treated as a plain box (`DvBox`). Names like `DvScreen` and `DvShell` are likewise just boxes.
- Text can be given via the `text` attribute or as text inside the element (`DvLabel`, `DvButton`, `DvCheckBox` and `DvRadio` only). The inner text of an element that has child elements is ignored.

<a id="common-attributes"></a>
## Common attributes

| Attribute | Description | Bound |
|---|---|---|
| `style` | Inline style ([style syntax](dvml-style.md#style-syntax)) | Yes (`{X}` interpolation inside the value, `token()`) |
| `class` | Space-separated class list ([classes](dvml-style.md#classes)) | Yes |
| `hover-style` `press-style` `disabled-style` | Per-state inline style ([state styles](dvml-style.md#state-styles)) | Yes |
| `grow` | Same as `flex-grow` | Yes |
| `width` `height` | Same as style `width`/`height`, lower priority ([priority](dvml-style.md#priority)) | Yes |
| `content-align` | Text/icon position ([content-align](dvml-style.md#content-align)) | Yes |
| `visible` | False ([true/false values](#value-bool): `false`, `0`, empty or a null binding) = the element is not created at all. Any other text (`abc`) shows it, with a Trace warning | Yes |
| `disabled` | True (`true`, a non-zero number) blocks clicks and commits (the press does not leak to an outer `onclick` either), no hover/press look, `disabled-style` applied. **Inherited downward**: if an ancestor is disabled, the child is disabled even if its own value is `false` ([disabled](#disabled)) | Yes |
| `onclick` | VM method name called on click ([click](#click)) | No (name as is) |
| `hold` | Long press in ms: it must be held this long for `onclick` to fire (prevents accidental operation, [hold](#hold)) | Yes |
| `onbeforecommit` | VM method that approves a value ahead of the write; used by value widgets only (built-in `DvSwitch` passes it to its toggle) ([onbeforecommit](#onbeforecommit)) | No (name as is) |
| `id` | Name of the control to grab from code ([`id` and `vm`](dvml-code.md#code-id-vm)). Does not affect how the screen looks | No (literal text, C# identifier) |

<a id="common-attribute-matrix"></a>
### Which tags take the common attributes

These attributes mean the same on every element that is a box. Child elements that are not boxes take none of them except `id`.

| Element | Common attributes it takes |
|---|---|
| Layout tags (`DvBox`, `DvStack`, `DvRow`, `DvGrid` …), widgets, instruments, process equipment and units, the content of a `DvTab`, a `DvAttach` cell | All of them |
| `DvOption`, `DvColumnGroup`, `DvSummaryRow`, `DvSummary`, `DvTrendChannel`, `DvStyle` | `id` only |
| `DvColumn` | `id`, plus its own `width`, `onclick`, `onbeforecommit`, `readonly` ([DvColumn](dvml-grid.md#dvcolumn)) |
| Process `DvPort`, `DvAgitator`, `DvMotor`, `DvLine`, `DvSignal` | `id`, plus their own `visible` ([connections](dvml-process.md#connections)) |

- Any other common attribute on a non-box child (`<DvOption style="…"/>`) is build warning [DVML019](dvml-diagnostics.md#dvml019).

<a id="value-rules"></a>
## Value rules

Every attribute value is read by one of these rules, on every tag. "Literal" = written as fixed text in the markup (not a binding).

| Kind | Accepted | Empty text | Anything else | Build check |
|---|---|---|---|---|
| [True/false](#value-bool) | `true`/`false` (any case), a number (non-zero = true), a bool binding | false | the attribute's default + Trace warning | literal on a built-in tag: DVML020 |
| [Number](#value-number) | invariant culture (`1.5`, `1e3`) | same as left out | the attribute's default + Trace warning | literal: DVML020 |
| [Word](#value-word) | one word of the tag's list, case and surrounding spaces ignored | same as left out | the attribute's default + Trace warning | literal: DVML020 |
| [Color](#value-color) | `#RGB` `#RRGGBB` `#RRGGBBAA` `transparent` `token(name)` `var(--name)` | same as left out | unknown token: magenta + Trace warning; other value: default color + Trace warning | literal: DVML020 |
| [Length](#value-length) | numbers in px (`16`, `16px`, `16dp`) | — | see the length rule | — |
| [Theme word](#value-theme-word) | a word the current theme has a `tone-…`/`state-…` token for | same as left out | the tag's fallback + Trace warning (magenta only on `DvBadge`/`DvStat` `tone`) | none (the theme belongs to the app) |
| [Literal name](#not-bound) | method names, paths, process names and references | — | used as written | handler names: DVML018 |

- An attribute left out uses its default (listed in each tag's `Attributes` table).
- The Trace warning names the tag, the attribute and the value. A bound value warns once per screen; a literal warns once per value. Details of DVML020: [DVML020](dvml-diagnostics.md#dvml020).

<a id="value-bool"></a>
### True/false values

- Every true/false attribute (`visible`, `disabled`, `readonly`, `DvLamp on`·`blink`, `area`, `smooth`, `show-ack`, `show-value`, grid column flags, process `running`·`open`·`panel` …) and the `value` of on/off widgets read a value the same way.
- True = `true` (any case), a non-zero number (`1`, `-1`, also as text) or a bool `true` binding.
- False = `false`, `0`, empty text or a binding that resolves to null. Surrounding spaces are ignored.
- Any other text (`yes`, `on`, `abc`) uses the attribute's default and writes a Trace warning; written as a literal on a built-in tag it is also build warning [DVML020](dvml-diagnostics.md#dvml020).
- An attribute left out uses its default.

<a id="value-number"></a>
### Number values

- Number attributes (`min`, `max`, `step`, `blink-interval`, `row-height`, `window` …) are read with the invariant culture (`1.5`, never `1,5`, whatever the PC's language).
- A value that is not a number uses the attribute's default and writes a Trace warning (a literal one is also DVML020).
- Empty text is the same as leaving it out.

<a id="value-word"></a>
### Word values

- Attributes that take one word from a fixed list (`direction`, `fit`, `variant`, `size`, `selection-mode`, `cell-type`, `sort`, `fn`, `input-mode`, `keyboard-type`, `content-align`, the `align`/`justify` of layout tags, process `kind`, `flip`, `arrow`, `side` …) compare the value ignoring case and surrounding spaces (`" Up "` = `up`).
- Any other word uses the attribute's default and writes a Trace warning (a literal one is also DVML020). Empty text is the same as leaving it out.
- The lists are in each tag's section.
- `content-align` written as a style key (not as the attribute) is case-sensitive: [content-align](dvml-style.md#content-align).

<a id="value-color"></a>
### Color values

- Color attributes (`DvLamp color`, `DvTrendChannel color`, `DvSignal color`, `DvColumn on-color`, the `DvDataGrid` `*-color` attributes, zone colors in `alarm-zones`) take `#RGB`, `#RRGGBB`, `#RRGGBBAA`, `transparent` or a theme token `token(name)` (alias `var(--name)`).
- A token the theme does not have draws magenta `#FF00FF` and writes a Trace warning, so a misspelled name stands out ([magenta](dvml-diagnostics.md#magenta)).
- Any other value (`red`, `nope` — color names are not supported) is treated as not given (the attribute's default color) and writes a Trace warning; a literal one is also DVML020.
- Empty text is the same as leaving it out.

<a id="value-length"></a>
### Length values

- `width`, `height` and the style size keys are numbers in logical px (`16`, `16px` and `16dp` are accepted). The `width`/`height` attributes are passed to the style as is, so they follow the style rule: [style value formats](dvml-style.md#style-values) (`%`, `em` and `auto` are not accepted there).
- What a size means in layout (content size, min/max, stretching): [size](dvml-layout.md#size). Logical px and display scaling: [logical px](dvml-layout.md#logical-px).
- Lengths with their own formats are described with their tag: `gap`/`padding` step names on layout sugar ([layout sugar](dvml-layout.md#layout-sugar)), grid tracks `25%` `1fr` ([grid tracks](dvml-layout.md#grid-tracks)), `DvColumn width` `120` · `20%` · `*` · `2*` ([DvColumn](dvml-grid.md#dvcolumn)).

<a id="value-theme-word"></a>
### Theme words (`tone`, `state`)

- `tone` and `state` are theme words: a value is valid when the current theme has the token `tone-{value}` / `state-{value}`.
- Built-in themes: `tone` = `neutral` `info` `success` `warning` `danger`; `state` = `run` `stop` `fault` `warn` `manual` `off`. A custom theme can add its own (a `state-idle` token makes `state="idle"` valid).
- A word ending in `-fg` or `-soft` is never a word (`tone-info-fg`, `state-run-soft` are the derived colors of a word).
- Same case/space rule as [word values](#value-word). Theme words are not checked at build time (the theme belongs to the app).
- A word the theme has no token for:

| Where | Result |
|---|---|
| `tone` on `DvRing`, `DvProgress`, `DvSparkline`, `DvBarChart` | `info` + Trace warning |
| `DvLamp state` | no state color (the lamp uses `color` or the style color) + Trace warning |
| `DvStatusItem state` (lamp and status chip) | `off` + one Trace warning |
| `tone` on `DvBadge` and `DvStat` (including the `DvStat` `delta` chip) | magenta (the tag builds a token name from the word) ([magenta](dvml-diagnostics.md#magenta)) |

<a id="binding"></a>
## Binding syntax

| Syntax | Meaning |
|---|---|
| `text="{Temperature}"` | Value of a public VM property (one-way) |
| `text="{Motor.Speed}"` | Dot path. Empty if an intermediate value is null |
| `{Data.X}` | Same as `{X}`: the `Data.` prefix is stripped (do not write it). So a VM property named `Data` cannot be reached this way: `{Data.X}` reads `X` on the VM itself and shows nothing, with no warning. Give such a property another name (for a snapshot object: `Live`, bound as `{Live.Temp}`) |
| `text="Pump #{No} - {State}"` | Interpolation inside text. Braces are always interpreted as bindings |
| `style="color:{StatusColor}"` | Interpolation inside a style |
| `style="color: token(tone-{Tone}-fg)"` | The binding is resolved first, then the token |
| `value="{Bind AutoMode}"` | Two-way: writes user input to the VM ([two-way](#two-way)) |
| `text="@pump"` | Localized string ([localization](dvml-app.md#localization)) |

- Bindings read **public instance properties** only (not fields). A missing path becomes null and shows as blank.
- If the whole attribute value is a single binding (`"{X}"`), the value's type is preserved (number, bool, list). Mixed text becomes a string.
- Bindings are read once at layout. Computed properties (`public string StatusText => …`) are recomputed on the next `Invalidate()`. After changing the VM from a timer or another thread, call `Invalidate()`: [updates and threads](llm-rules.md#updates-threads).

<a id="two-way"></a>
### Two-way binding

- `value="{Bind X}"` writes user input to the VM property `X`. It is meaningful only on the value attributes listed in [value widgets](#value-widgets).
- On any other attribute, `{Bind X}` is read-only, like `{X}`.
- Example: `<DvSwitch label="Auto mode" value="{Bind AutoMode}"/>`.

<a id="value-widgets"></a>
## Value widgets

A value widget shows a value and lets the user change it. One table for all of them:

| Tag | Value attribute | Written by the screen with `{Bind X}` | `T` for [onbeforecommit](#onbeforecommit) | `readonly` | Handle | Value member | Change event |
|---|---|---|---|---|---|---|---|
| [`DvToggle`](dvml-widgets.md#dvtoggle) | `value` | Yes | `bool` | Yes | `DvToggle` | `bool Checked` | `CheckedChanged` |
| [`DvCheckBox`](dvml-widgets.md#dvcheckbox) | `value` | Yes | `bool` | Yes | `DvCheckBox` | `bool Checked` | `CheckedChanged` |
| [`DvSwitch`](dvml-widgets.md#dvswitch) (built-in component) | `value` | Yes | `bool` (its `onbeforecommit` goes to its toggle) | No | `DvSwitch` | `bool Checked` | `CheckedChanged` |
| [`DvRadio`](dvml-widgets.md#dvradio) | `value` (group value) with `option`, or `group` | Yes | `option` converted to the bound property's type; for a `group` set, `string` | Yes | `DvRadio` | `bool Checked` (whether the group value is `option`) | `CheckedChanged` (also when turned off) |
| Latching [`DvButton`](dvml-widgets.md#dvbutton) (has `value`) | `value` | Yes | `bool` | No | `DvButton` | `bool Checked` | `CheckedChanged` |
| [`DvKnob`](dvml-widgets.md#dvknob), [`DvSlider`](dvml-widgets.md#dvslider) | `value` | Yes | `double` | Yes | same name | `double Value` | `ValueChanged` |
| [`DvInput`](dvml-inputs.md#dvinput) | `value` | Yes, once when editing ends | numeric (`keyboard-type="numeric"`): `double` (even if the property is int); text/password: `string` | Yes | `DvInput` | `string? Text`, plus `double? Value` for numeric input | `TextChanged`, followed by `ValueChanged` for numeric input |
| [`DvSelect`](dvml-inputs.md#dvselect), [`DvSegment`](dvml-inputs.md#dvsegment) | `value` | Yes | the actual type of the value to write | Yes | same name | `int SelectedIndex` (none = -1), `object? SelectedValue` | `SelectionChanged` |
| [`DvListBox`](dvml-inputs.md#dvlistbox) | `value` (one), or `selected-items` with `selection-mode="multi"` | Yes | the actual type of the value to write | Yes | `DvListBox` | `SelectedIndex`, `SelectedValue` (the first one if multiple), `IReadOnlyList<object?> SelectedItems` (writing selects those values) | `SelectionChanged` |
| [`DvTreeView`](dvml-inputs.md#dvtreeview) | `value` | Yes | the actual type of the value to write | Yes | `DvTreeView` | `object? SelectedItem` (writing it expands the ancestor branches) | `SelectionChanged` |
| [`DvTabs`](dvml-widgets.md#dvtabs) | `value` (optional) | Yes | the actual type of the value to write | Yes (no read-only look) | plain `DvControl` | — | — |
| [`DvDateTimeInput`](dvml-inputs.md#dvdatetimeinput) | `value` | Yes | the actual type of the value to write | Yes | same name | `DateTime? Value` | `ValueChanged` |
| [`DvColorInput`](dvml-inputs.md#dvcolorinput) | `value` | Yes | the actual type of the value to write | Yes | same name | `SKColor? Value` | `ValueChanged` |
| [`DvExpander`](dvml-widgets.md#dvexpander) | `expanded` (optional) | Yes | not called | No | `DvExpander` | `bool Expanded` | `ExpandedChanged` |
| [`DvVideo`](dvml-widgets.md#dvvideo) | `position`, `duration` (seconds) | `position` about every 250 ms while playing; `duration` once the file is open | not called | No | plain `DvControl` | — | — |
| [`DvDataGrid`](dvml-grid.md#grid-selection) | `selected-items`, check and edit cells | Yes | check cell: `bool`; edit cell: the property's underlying type | `DvColumn readonly` (check and edit cells) | plain `DvControl` | — | — |
| [Custom widgets](dvml-components.md#dvwidget) | `value` | Yes (`ctx.Commit`) | the actual type of the value to write | No (they use `ctx.CanCommit`) | — | — | — |

### Behavior

- **Editable vs read-only.** A value widget is editable when its value attribute is `{Bind X}`. It is **read-only** when `readonly` is true (bindable, the [true/false rule](#value-bool)) **or** its value is one-way (`{X}`) or a literal. This applies to every widget with `Yes` in the `readonly` column.
- Read-only shows the value as is and blocks taps, drags, the keypad and the stepper (nothing is written and `onbeforecommit` is not called). It is not dimmed — dimming is [`disabled`](#disabled).
- Read-only look: box-shaped widgets (`DvInput`, `DvSelect`, `DvDateTimeInput`, `DvColorInput`, `DvSegment`, `DvListBox`, `DvTreeView`) get `.input-readonly` (transparent background, dashed border); drawn widgets (`DvToggle`, `DvCheckBox`, `DvRadio`, `DvKnob`, `DvSlider`) get a dashed line around the shape. `DvTabs` has no read-only look (header presses just do not switch).
- A `DvRadio` without `value` (group remembered by the screen) is writable unless `readonly` is true. Code (handles) and the VM can change the value of a read-only widget.
- **One owner per value.** With `value="{Bind X}"` the VM property is the owner and the handle (`tg.Checked`) reads and writes that property. With only `id` and no `value`, the handle holds the value and the widget is pressable on screen. The full ownership table: [code writes](dvml-code.md#code-writes).
- For MVVM use binding; for code-centric screens use `id` only. Mixing both styles within one screen is fine.
- Change events are raised only when the value actually changes; `e.ByUser` tells a screen change from a code write ([handles](dvml-code.md#handles)).
- `DvRadio.Checked = false` throws `InvalidOperationException` when the bound property cannot take null (enum, int) — check another button instead.
- `DvDataGrid selected-items` and `DvListBox selected-items` are read as written: `{Bind X}` = two-way list, `{X}` = one-way.
- Inside a component template, `value="{p}"` writes to the outer path of a two-way parameter `p` on `DvToggle`, `DvCheckBox`, `DvKnob`, `DvSlider`, `DvInput`, `DvSelect`, `DvSegment`, `DvTabs`, `DvDateTimeInput`, `DvColorInput`, latching `DvButton` and every custom widget ([component parameters](dvml-components.md#component-params)).

<a id="not-bound"></a>
## Attributes that are not bound

Most attributes are bound. The following are **not bound and are used literally**.

| Element | Non-bound attributes |
|---|---|
| All elements | `onclick` `onbeforecommit` (method names), `id` (control name), `vm` on the root |
| `DvAlarmBanner` `DvAlarmList` | `onack` |
| `DvDataGrid` | `row-key` `row-class` `onrowclick` (paths / names) |
| `DvColumn` | `field` `onclick` `onbeforecommit` `cell-disabled` `cell-class` (paths / names) |
| `DvSelect` `DvSegment` `DvListBox`, select cell, `DvBarChart` `DvPieChart` | `item-value` `item-text` `item-icon` (paths inside the item) |
| `DvTreeView` | `item-children` `item-text` `item-value` (paths inside the item) |
| `DvSummary` | `field` |
| Process diagram | equipment, unit, `DvPort` and `DvInstrument` `name`, `DvLine`/`DvSignal` `from` `to`, `DvInstrument` `on` `tap` (names and references; a `{` in them writes a Trace warning and is used as written) |
| `DvComponent`/`DvParam` | `name` `default` |

- `selected-items` of `DvDataGrid` is not in this table: `{Bind X}` = two-way, `{X}` = one-way ([value widgets](#value-widgets)).
- `header`·`text`·`unit` of `DvColumn`, `DvColumnGroup header`, and `DvSummaryRow label` support only text interpolation (`"{X} items"`).
- Process names and references are never bound — write them literally ([connections](dvml-process.md#connections)).

<a id="click"></a>
## Click (`onclick`)

- `onclick="Method"` → the VM's `public void Method()`.
- Write only the method name — there is no call form (`onclick="Method(item)"` never runs); arguments such as the `DvRepeat` item are passed automatically.
- A name that is not a public instance method of the VM is build error [DVML018](dvml-diagnostics.md#dvml018) (same for `onbeforecommit`, `onack`, `onrowclick`).
- Inside a `DvRepeat` item, `Method(ItemType item)` is called if it exists, otherwise `Method()` ([DvRepeat](dvml-components.md#dvrepeat)).
- **Bubbling:** starting from the deepest element at the pressed point and moving up, the first interactive element encountered receives the click. Interactive element = has `onclick`, or has a hover/press style (including via class), or is an input, toggle, knob, grid, or ACK-capable alarm widget. If that element has no `onclick`, nothing happens (it does not bubble further).
- If you press inside a scroll box (including a grid) and drag 8 or more, it is treated as a scroll, not a click ([scroll](dvml-layout.md#scroll)).
- A click happens when the pressed element and the released element are the same.
- Invisible regions are not clicked or hovered: parts clipped outside a parent with `overflow:hidden`, children pushed outside the viewport of a scroll box (including grids), elements with false `visible`, and elements covered by an open popup or the virtual keyboard.
- For overlapping elements (`position:absolute`), the one drawn on top receives the input. A visible child that sticks out of an `overflow:visible` (default) parent (a badge or close button floated on a card corner) is clicked and hovered.
- If a handler throws, only that handler stops and the screen keeps working ([HandlerError](#handler-error)).
- Component instances and `onclick`: [component instances](dvml-components.md#component-instances). The `Click` event of a handle: [Click from code](dvml-code.md#code-click).

<a id="hold"></a>
### Long press (`hold`)

- With `hold="1500"`, the element must be held for 1.5 seconds for `onclick` to fire — protection against accidental operation of dangerous commands such as start, stop or reset.
- It fires once, ahead of the release, the moment the time is reached. A short press and release does nothing.
- While pressing, a band (translucent text color) fills the element from the left to show progress. Moving outside the element while pressed, or being judged a scroll, cancels it.
- Bindable (`hold="{HoldMs}"`).
- Applies only to ordinary elements receiving `onclick` (buttons, boxes). It has no effect on `DvToggle`, `DvCheckBox`, `DvRadio`, `DvInput`, `DvSelect`, `DvKnob`, `DvSlider`, pickers, grids or alarm widgets.

<a id="disabled"></a>
### Disabled

- An element with `disabled="true"` and everything inside it is not clicked, edited or committed (lock a whole area with `<DvStack disabled="{Locked}">`).
- Knob rotation, input editing, select/date/color popups, grid rows/edit cells/header sorting, alarm ACK and `DvRepeat` item `onclick` are all blocked. Scrolling (wheel / drag) keeps working.
- A press on a disabled area is swallowed there. Bubbling stops at the disabled element, so the `onclick` of an enabled ancestor is not called either (same as a disabled button in HTML).
- Bind it to a `bool` property (`disabled="{Locked}"`), not to text.

<a id="onbeforecommit"></a>
## Commit confirmation (`onbeforecommit`)

When a widget is about to write a value to the VM, a VM method confirms it. The value is written only when it returns `true`.

Every widget and grid cell looks the method up the same way, in this order (the first that exists is called):

1. Inside a `DvRepeat` item or for a grid cell: `bool M(Item item, T value)`, then `bool M(Item item, object value)` (`Item` = a type the repeat item / grid row is an instance of).
2. `bool M(T value)`, then `bool M(object value)`.

- `T` is the type of the value to write (for a nullable property its underlying type — `int` for `int?`, and a `T?` parameter also matches). `T` per widget is in the [value widgets table](#value-widgets).
- The value parameter must be exactly `T` or `object` — another base type or interface (`IComparable`, `IList<string>`) is not matched.

Behavior:

- If the method is not on the VM (a misspelled name is build error DVML018 in a dvml with `vm`) or the signature differs (including a non-`bool` return such as `void M(bool)`), **no widget or cell writes** (rejected). A Trace warning is logged once per screen for each method name and signature. A misspelled name makes the value look like it "does not change", so check Trace.
- If the method throws, the write is rejected, a Trace error is logged, and it is reported via [HandlerError](#handler-error) (the exception is not propagated to the app).
- Without `onbeforecommit`, the value is written without confirmation.
- If the VM property's setter throws after the confirmation (including type conversion failure), it is a write failure and handled exactly like a rejection. The error text for `DvInput` (including grid input cells) is "Could not save the value" (`dv.input.write-failed`); the exception message is not shown on screen.
- **The rejection text is the same for every rejection** ("This value is not allowed", `dv.input.rejected`; Korean or your own text with `DvStrings`, [localization](dvml-app.md#localization)). A confirmation method cannot return its own reason.
- To show a specific reason, set a VM property inside the method, bind a label to it, and call `App.Invalidate()` in the method — a rejected commit does not rebuild the screen by itself.
- When rejected (all cases above): `DvInput` stays in edit mode and shows the error text; picker popups stay open; a `DvSelect` dropdown closes and the value is unchanged (a grid `select` cell stays open); toggles, knobs and `check` cells keep their value; grid row selection (`selected-items="{Bind …}"`) keeps the previous selection. The same holds for every widget and `DvDataGrid` cell.

<a id="vm-constants"></a>
## VM constants (`Styles`, `Values`)

Instead of writing keyword values as strings in the VM, use constants. All are the same lowercase strings as in markup.

```csharp
public string LampState { get; set; } = Styles.State.Run;      // "run"
public string Align { get; set; } = Styles.ContentAlign.MiddleCenter;
```

| Class | Values |
|---|---|
| `Styles.ContentAlign` | 9 positions |
| `Styles.TextAlign` | `left` `center` `right` |
| `Styles.Overflow` | `visible` `hidden` `scroll` |
| `Styles.BorderStyle` | `solid` `dashed` |
| `Styles.Tone` | `neutral` `info` `success` `warning` `danger` |
| `Styles.State` | `run` `stop` `fault` `warn` `manual` `off` |
| `Styles.Variant` | `primary` `secondary` `danger` `ghost` |
| `Styles.ButtonSize` | `sm` `lg` |
| `Values.GridSelectionMode` | `none` `single` `multi` |
| `Values.GridCellType` | `text` `check` `lamp` `button` `input` `select` `datetime` `color` |
| `Values.GridFilter` | `none` `auto` `text` `values` `range` `date` |
| `Values.GridAlign` | `left` `center` `right` |
| `Values.GridSort` | `asc` `desc` |
| `Values.GridFn` | `sum` `avg` `min` `max` `count` |
| `Values.DateTimeMode` | `date` `time` `datetime` |

<a id="handler-error"></a>
## App code exceptions (`HandlerError`)

Exceptions thrown by VM code do not stop the screen. If VM code called by the screen throws — handler methods, property setters that write values, and property getters / `ToString()` / `items` enumeration that read values while building and drawing — **the exception does not escape input processing, layout, drawing or the host loop.** The library catches it, logs a Trace error once (even with no subscribers), and reports it via the `HandlerError` event. If a click seems to "do nothing", check this event or Trace.

```csharp
app.HandlerError += (_, e) =>                  // same event name on DvApp, ViewWindow, ViewControl and DvSurface
    Log.Error(e.Exception, $"{e.Kind} {e.Member} ({e.Tag})");
```

| Property | Content |
|---|---|
| `Exception` | The exception thrown by VM code (for reflection's `TargetInvocationException`, the unwrapped inner exception) |
| `Kind` | Where it is thrown — one value of the table below |
| `Member` | Method name or property path (for grid value reads, the column's `field`) |
| `Tag` | Element tag (`DvButton`, `DvInput`, `DvDataGrid` …) |

| `Kind` | Thrown by | `Member` |
|---|---|---|
| `onclick` | an `onclick` handler (including a grid `button` cell) | method name |
| `Click` | a subscriber of a handle's `Click` event | `id` |
| `onrowclick` | a `DvDataGrid onrowclick` handler | method name |
| `onack` | an alarm widget `onack` handler | method name |
| `onbeforecommit` | a confirmation method (= rejection) | method name |
| `setter` | a VM property setter that a widget writes (= rejection) | property path |
| `getter` | a property getter read during layout / drawing (including a `DvControl` value read) | property path |
| `format` | a value's `ToString()` / formatting | property path |
| `enumerate` | list enumeration of `DvRepeat` / `DvDataGrid` / `DvSelect` / alarms / trends | property path |
| `compare` | `CompareTo` in grid sorting | the column's `field` |
| `attach` | `Attach` of a handle VM (`IDvControlHost`), including the generated `OnLoaded()` | VM type name (`Tag` = null) |
| `keypad` | writing a value typed on the `DvKnob` keypad (the value is not written) | the `value` binding |
| `CheckedChanged` `ValueChanged` `TextChanged` `SelectionChanged` `ExpandedChanged` | a subscriber of a handle's change event, on a screen-side path (event per widget: [value widgets](#value-widgets)) | `id` |
| `Pointer` `FrameInterval` `Measure` `Paint` | a custom widget's code ([custom widgets](dvml-components.md#dvwidget)) | |
| `shown` `hidden` | a page's `OnShown`/`OnHidden` ([page lifecycle](dvml-app.md#page-lifecycle)); `DvApp.HandlerError` only | page class name (`Tag` = none) |
| `closed` | a dialog's `OnClosed` ([dialog lifecycle](dvml-dialogs.md#dialog-lifecycle)); `DvApp.HandlerError` only | dialog class name |
| `async` | an `async void` handler or `await` continuation ([threads](dvml-app.md#threads)); `DvApp.HandlerError` only | none |
| `idle` | a `DvApp.Idle` handler ([lock and idle](dvml-dialogs.md#lock-idle)); `DvApp.HandlerError` only | none |
| `hotreload` | invalid dvml during hot reload ([hot reload](dvml-app.md#hot-reload)); `DvApp.HandlerError` only | file path |

Behavior:

- The handler is treated as ended at that point. A value write failure (`setter`) or confirmation exception (`onbeforecommit`) is the same as a rejection ([onbeforecommit](#onbeforecommit)).
- A value read failure (`getter`, `format`, `enumerate`, `compare`) treats that value as **empty (null)** and drawing continues. Text becomes an empty string, `visible` becomes hidden, grid cells are blank, sort keys get the null rank (first in ascending order), and summaries aggregate without that value.
- A list whose enumeration threw is an **empty list** (the leading part is not shown either). A comparison exception is treated as equal values (the order of that column is undefined).
- No flooding: paths read every frame (grids) report the same (`Kind`, `Member`, `Tag`) only once until the screen is rebuilt with `Invalidate()`.
- With `DvApp`, exceptions from area, page and dialog screens, page transitions, dialog `OnClosed` and `async void` handlers are all collected into `DvApp.HandlerError`. Subscribing to that one is enough per app.
- The `HandlerError` of `ViewWindow`·`ViewControl` only forwards it, so subscribing to both delivers the same exception twice.
- Exceptions thrown inside a subscriber are swallowed and only logged to Trace.
- `attach`: the screen constructor runs ahead of any subscription, so Trace is logged immediately and `HandlerError` is reported once on that screen's **first layout** (first frame). Subscribing between creating the window and the first frame receives it. Handles attached ahead of the throw keep working.
- Exceptions from methods the app code calls directly propagate to that call as is (a handle write raises its change event on the writing thread: [handles](dvml-code.md#handles)).
- When drawing with `DvRenderer` (no screen to report to), value read exceptions are only logged as Trace errors.
- Write async methods started from a communication thread to return `Task` and catch exceptions yourself.

<a id="naming"></a>
## Naming — what an attribute name means

The same name means the same thing on every tag.

- `text` = the text the control itself shows (`DvLabel`, `DvButton`, `DvCheckBox`, `DvTab`, a `DvColumn` button).
- `label` = a name tag beside or under a value (`DvRing`, `DvStat`, `DvStatusItem`, process equipment, `DvSignal`, `DvSummaryRow`).
- `title` = the heading of a group (`DvCard`, `DvPageFrame`, `DvDialogFrame`, `DvExpander`). `title` is a heading, **not an HTML tooltip**.
- Grid column headings keep the grid word `header` (`DvColumn`, `DvColumnGroup`).
- `min`·`max` = the value range of a widget (gauge, knob, slider, input, grid input cell). `y-min`·`y-max` = the range of a chart's vertical axis (the horizontal axis is time or items). `DvVessel level` is always 0–100 %.
- `direction`: on `DvSlider`·`DvProgress` it is the side the value grows toward (`right` `left` `up` `down`); on `DvProcess`·`DvUnit` it is the flow direction (`lr` `tb`). Box layout uses the style key `flex-direction` instead.
- `kind` = *what* a thing is (process equipment and signals: `centrifugal`, `gear`, `pneumatic` …); `variant` = *how it looks* (`DvButton`: `primary`, `danger` …).
- `DvInput`'s handle `Text` **is its value** (the typed string, as WPF `TextBox.Text`); `Value` is the same value as a number. On `DvLabel`, `Text` overrides the shown text.
- Lists of picked items are `selected-items` (`DvListBox`, `DvDataGrid`). A path read from each row item is `<scope>-<what>` (`item-text`, `row-key`, `cell-class`, `cell-disabled`).
- C# handle members are the markup name in PascalCase (`expanded` → `Expanded`).
- State words (`state="run"`, theme colors) and process booleans (`running`, `open`) are different systems. How to link them, and `DvTank` vs `DvVessel`: [process glossary](dvml-process.md#process-glossary).

<a id="glossary"></a>
## Glossary

| Term | Meaning |
|---|---|
| VM | The view model: the C# class named by the root's `vm` (a `DvPage` subclass in an app). Bindings read its public properties; handlers are its public methods |
| Box | An element that is laid out and drawn (layout tags, widgets, process equipment). Takes the [common attributes](#common-attributes) |
| Binding | `{X}`: a value read from the VM at layout ([binding syntax](#binding)) |
| Two-way binding | `{Bind X}` on a value attribute: user input is written to `X` ([two-way](#two-way)) |
| Literal | A fixed value written in the markup (not a binding) |
| Value widget | A widget whose value the user can change ([value widgets](#value-widgets)) |
| Read-only | A value widget that shows its value but does not write it (`readonly`, one-way or literal value) |
| Commit | The write of a user-changed value to the VM, confirmed by [`onbeforecommit`](#onbeforecommit) |
| Handle | The C# object for an element with `id` ([handles](dvml-code.md#handles)) |
| Theme word | A `tone`/`state` value, valid when the theme has its token ([theme words](#value-theme-word)) |
| Token | A named theme value, `token(name)` ([theme tokens](dvml-style.md#theme-tokens)) |
| Trace warning | A message written to `System.Diagnostics.Trace` at run time ([Trace warnings](dvml-diagnostics.md#trace-warnings)) |
| DVML0nn | A build diagnostic from the source generator ([build diagnostics](dvml-diagnostics.md#build-diagnostics)) |
