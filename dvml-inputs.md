# Input widgets

Covers: the input widgets (`DvInput` `DvSelect` `DvOption` `DvSegment` `DvDateTimeInput` `DvColorInput` `DvListBox` `DvTreeView`) — the rules they share, then one section per tag.
Read first: [markup basics](dvml-language.md#markup-basics) · [common attributes every tag takes](dvml-language.md#common-attributes) · [how attribute values are read](dvml-language.md#value-rules) · [value widgets: two-way, read-only, handles](dvml-language.md#value-widgets)
Not here: [buttons, toggles, check boxes, radios, knobs, sliders, tabs](dvml-widgets.md#dvbutton) · [semantic components (page frame, cards, badges, stats)](dvml-widgets.md#semantic-components) · [data grid and its edit cells](dvml-grid.md#dvdatagrid) · [rejecting a value with onbeforecommit](dvml-language.md#onbeforecommit) · [which widget to pick](llm-rules.md#choose-widget)

How to read the tables: `bound` = the attribute takes a binding (`{X}` / `{Bind X}`). Every tag also takes the [common attributes](dvml-language.md#common-attributes) (`id` `class` `style` `grow` `width` `height` `visible` `disabled` `onclick` `hold` `onbeforecommit` …); they are not repeated in the tables. Bool, number, word and color values follow the [value rules](dvml-language.md#value-rules): an unreadable value gives the default + a Trace warning.

<a id="input-common"></a>
## Rules shared by input widgets

Rules shared by `DvInput` `DvSelect` `DvSegment` `DvDateTimeInput` `DvColorInput` `DvListBox` `DvTreeView`:
- Editable with `value="{Bind X}"`; read-only with one-way, literal or `readonly="true"` (dashed border `.input-readonly`) — the full rule and handle ownership: [value widgets](dvml-language.md#value-widgets).
- Look: the `.input` class (surface background, border, radius 6, padding 8 12); override with `style`.
- The value is written to the VM **once, when editing ends**. An unchanged value is not written.
- Rejecting a value so it is not written: [onbeforecommit](dvml-language.md#onbeforecommit).

<a id="dvinput"></a>
### DvInput

A text, number or password field.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | text or number | — | yes | `{Bind X}` = VM. Absent + a handle = the handle owns it. One-way/literal = read-only |
| `keyboard-type` | `text` `numeric` `password` | `text` | yes | Unknown = `text` + Trace warning |
| `multiline` | bool | `false` | yes | Multi-line text |
| `placeholder` | text | empty | yes | Shown when empty |
| `format` | .NET format string | none | yes | Numeric display only. Unusable = raw value + Trace warning |
| `min` | number | none | yes | Typed values outside are rejected (not clamped). Numeric, both given and `min >= max` = 0..100 + warning |
| `max` | number | none | yes | As `min` |
| `unit` | text | empty | yes | Drawn at the right edge inside the box |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal |
| `step` | number | `1` | yes | Stepper increment. `<= 0` = 1 |
| `show-stepper` | bool | `false` | yes | −/+ buttons. Read only when `keyboard-type` is `numeric` |

**Behavior**
- Default size: 200×40 (multiline 200×96).
- Pressing starts editing. Enter / Tab / Done / pressing elsewhere = commit; Esc / Cancel = discard. Typing changes only the edit buffer.
- **numeric**: integer vs. real and sign come from the **bound property's type** (`int` signed integer, `uint`/`byte` unsigned, `double`/`decimal` real, `int?` allowed). Decimal point `.` (InvariantCulture).
- Out of `min`/`max` = **rejected**: error border + reason, editing continues. Clearing a nullable number is rejected.
- **password**: starts empty; committing it empty does not change the value.
- **Stepper** (`show-stepper="true"`, numeric only): −/+ at both ends add or subtract `step`, stop at `min`–`max`, and write immediately (empty value → from `min` or 0). Same write path as typing (type, range, `onbeforecommit`). The middle opens the keypad as usual.
- −/+ while typing applies the typed value first, then steps. Read-only, one-way or disabled = buttons dimmed and inactive. The stepped value is rounded to the decimal places of `step` (a fractional `step` is not used on an integer property); the text shown follows `format` (`step="0.5"`: 2.5 → 3 shows `3`, or `3.0` with `format="0.0"`).
- A − or + press rejected by `onbeforecommit` (or by a throwing setter) leaves the value unchanged and **shows nothing on the widget**: no error text, no edit mode. The screen is rebuilt after every press, so a reason label bound to a VM property that the method sets shows at once ([commit confirmation](dvml-language.md#onbeforecommit)).
- While editing, the text being edited wins over VM changes (`Invalidate()` polling).
- Editor by host: OpenTK and `ViewControl.TouchMode` = built-in virtual keyboard (QWERTY Korean/English, Shift, symbols, 2-set Korean (Dubeolsik), numeric keypad). Text input opens in the English layout, or Korean when `DvStrings.Language` is `ko`; the Korean/English key is always there. It slides up and pushes the screen so the box stays visible; pressing outside cancels; single line only.
- WinForms `ViewControl` = a hidden TextBox over the box (OS IME, multiple lines, clipboard; pressing outside commits). Hosts: [hosts](dvml-app.md#hosts).

**Example**
```xml
<DvInput value="{Bind Recipe.Name}" placeholder="Name"/>
<DvInput value="{Bind SetPoint}" keyboard-type="numeric" min="0" max="120" format="0.0" unit="℃" onbeforecommit="Check"/>
<DvInput value="{Bind Password}" keyboard-type="password"/>
<DvInput value="{Bind Memo}" multiline="true"/>
<DvInput value="{Bind Speed}" keyboard-type="numeric" min="0" max="100" step="0.5" unit="%" show-stepper="true"/>
```

See also: [value text and range](dvml-instruments.md#instrument-common) · [grid edit cells](dvml-grid.md#grid-edit-cells)

<a id="dvselect"></a>
### DvSelect

A dropdown list; pick one.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | any | — | yes | Converted to the bound property type. `{Bind X}` = VM. Absent + a handle = the handle owns it |
| `placeholder` | text | empty | yes | Shown when no item matches |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal |
| `items` | list | none | yes | Wins over `DvOption` children, which win over the enum of the value type |
| `item-value` | property path | the item | no | Read raw |
| `item-text` | property path | value as text | no | Read raw |
| `item-icon` | property path | none | no | Read raw |

<a id="dvoption"></a>
**`DvOption` attributes** (child of `DvSelect`, `DvSegment`, `DvListBox`; takes only these + `id`)

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | text | empty | yes | Resolved as text, converted to the property type |
| `text` | text | `value` | yes | Inner text is not used. A code `Text` override wins |
| `icon` | `fa-name` | none | yes | Shown by `DvSelect`, `DvSegment` and `DvListBox` |

**Behavior**
- Default size: 200×40.
- `value` binds to the item's **value**, not its index. Item values are converted to the property type for comparison and storage (`"1"` → int, `"Left"` → enum, `int?` allowed). No match → `placeholder`.
- Item source priority: `items` > `DvOption` children > automatic enum. With `items` given there is no fallback to `DvOption`, even when empty.
- `items` = `IEnumerable`. `item-value`/`item-text`/`item-icon` are property paths inside the item; omitted = the item itself is the value and `ToString()` the text.
- Pressing opens a list below (above if there is no room, shrunk if neither fits). Up to 8 visible; more scroll. Pressing an item writes once. Pressing outside closes it, and that press is not passed through. No keyboard navigation.

**Example**
```xml
<DvSelect value="{Bind Mode}" placeholder="Operation mode">
  <DvOption value="auto" text="Auto" icon="fa-robot"/>
  <DvOption value="manual" text="Manual"/>
</DvSelect>
<DvSelect value="{Bind RecipeId}" items="{Recipes}" item-value="Id" item-text="Name" item-icon="Icon"/>
<DvSelect value="{Bind Direction}"/>   <!-- enum property: items built automatically (text = enum name) -->
```

See also: [DvListBox](#dvlistbox) · [DvSegment](#dvsegment) · [DvRadio](dvml-widgets.md#dvradio)

<a id="dvsegment"></a>
### DvSegment

Pick one from a single row of joined buttons (a radio group).

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | any | — | yes | Option value converted to the bound property type, as in `DvSelect` (not convertible = not written). Absent + a handle = the handle owns it |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal |
| `fill` | bool | `false` | yes | Items share the extra width (see Behavior) |
| `items` | list | none | yes | Wins over `DvOption` children, which win over the enum of the value type |
| `item-value` | property path | the item | no | Read raw |
| `item-text` | property path | value as text | no | Read raw |
| `item-icon` | property path | none | no | Read raw |

**Behavior**
- Default size: sum of item text sizes + padding.
- Items are built like [DvSelect](#dvselect) (`items`/`item-*` > [`DvOption`](#dvoption) children > automatic enum). Pressing writes that item's value (conversion and `onbeforecommit` as in `DvSelect`). The selected item has a `primary` background (`.segment-item-on`).
- One-way `value` or `readonly` only displays (dashed border, `.input-readonly`); `disabled` dims it. Items are created by the library (`.segment-item`) and do not wrap.
- Its width fits its items, also inside a vertical layout (`DvStack`, `DvCard` — not stretched). When `grow`, `width` or `align-self:stretch` makes it wider, the box takes that space but the background and border are drawn only around the items.
- `fill="true"`: the items share the extra width equally (each keeps its text width, so longer labels stay wider). In a vertical layout it stretches to the full width.

**Example**
```xml
<DvSegment value="{Bind Mode}">
  <DvOption value="auto" text="Auto"/>
  <DvOption value="manual" text="Manual"/>
  <DvOption value="stop" text="Stop"/>
</DvSegment>
```
```xml
<DvRow gap="sm">
  <DvSegment value="{Bind Mode}" grow="1" fill="true">
    <DvOption value="auto" text="AUTO"/>
    <DvOption value="manual" text="MANUAL"/>
  </DvSegment>
  <DvButton text="OFF" style="width:84"/>
</DvRow>
```

See also: [DvRadio](dvml-widgets.md#dvradio) · [latching DvButton](dvml-widgets.md#dvbutton)

<a id="dvdatetimeinput"></a>
### DvDateTimeInput

A date and/or time field with a picker popup.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | `DateTime` `DateOnly` `TimeOnly` (nullable) | — | yes | `{Bind X}` = VM. Absent + a handle = the handle owns it |
| `placeholder` | text | empty | yes | Shown when the value is null |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal |
| `mode` | `date` `time` `datetime` | from the value type | yes | Unknown or not matching the type = from the value type + warning |
| `show-seconds` | bool | `false` | yes | Seconds field |
| `format` | .NET date format | type default | yes | Current culture. Invalid = default format + one warning |

**Behavior**
- Default size: 200×40.
- `DateTimeOffset` is not supported.
- `mode="date"` keeps the time part of a `DateTime`; `mode="time"` keeps the date. Hiding seconds keeps the existing seconds. A mismatched mode (`time` on `DateOnly`) falls back to the type default (one warning).
- Pressing opens a popup: calendar + hour/minute(/second) fields. Pressing a field opens the numeric keypad.
- Written once, only on **OK**. Cancel / pressing outside = not written (the outside press is not passed through).
- Null value → `placeholder`; the popup starts at the current date and time. `DateTime.MinValue`/`DateOnly.MinValue` also start at the current date. No Clear button.

**Example**
```xml
<DvDateTimeInput value="{Bind ReserveAt}"/>                       <!-- DateTime: date + time -->
<DvDateTimeInput value="{Bind Day}"/>                             <!-- DateOnly: calendar only -->
<DvDateTimeInput value="{Bind Start}" show-seconds="true"/>       <!-- TimeOnly: time fields -->
<DvDateTimeInput value="{Bind ReserveAt}" mode="date" format="yyyy.MM.dd"/>
```

See also: [DvColorInput](#dvcolorinput) · [grid datetime cells](dvml-grid.md#grid-edit-cells)

<a id="dvcolorinput"></a>
### DvColorInput

A color field with a picker popup.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | `#RRGGBB` text or `SKColor` | — | yes | `{Bind X}` = VM. Absent + a handle = the handle owns it |
| `placeholder` | text | empty | yes | Shown when the value is null |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal |

**Behavior**
- Default size: 200×40.
- Pressing opens a popup: HSV square + hue bar + `#RRGGBB` field; pressing the field opens the keyboard.
- Written once, only on **OK**. Cancel / pressing outside = not written (the outside press is not passed through).
- Null value → `placeholder`; the popup starts at white.
- No alpha editing: `SKColor` keeps its alpha; strings are written as `#RRGGBB`. No palette (swatch list).

**Example**
```xml
<DvColorInput value="{Bind PenColor}"/>
```

See also: [DvDateTimeInput](#dvdatetimeinput) · [grid color cells](dvml-grid.md#grid-edit-cells)

<a id="dvlistbox"></a>
### DvListBox

An always-open selection list; one or many.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `selection-mode` | `single` `multi` | `single` | yes | Unknown = `single` + warning |
| `value` | any | — | yes | Single selection only. `{Bind X}` = VM. Absent + a handle = the handle owns it |
| `selected-items` | list | — | yes | Multi selection only. Two-way `{Bind X}` list |
| `readonly` | bool | `false` | yes | Read-only also when `value` (or `selected-items` when multi) is one-way or a literal |
| `items` | list | none | yes | Wins over `DvOption` children, which win over the enum of the value type |
| `item-value` | property path | the item | no | Read raw |
| `item-text` | property path | value as text | no | Read raw |
| `item-icon` | property path | none | no | Read raw |

**Behavior**
- Default size: 200 × (up to 5 rows, row 40).
- Choices are built like [DvSelect](#dvselect) (`items`/`item-*` > [`DvOption`](#dvoption) children > automatic enum).
- **Single** (default): `value` = the chosen choice's value (not its index; conversion as in `DvSelect`). Pressing a row writes once; pressing the chosen row again leaves it unchanged.
- **Multi**: `selection-mode="multi"` + `selected-items="{Bind X}"`. Each row press adds or removes it, and every change writes a **new list** (`T[]` `List<T>` `IList<T>` `IReadOnlyList<T>` `IEnumerable<T>`; elements converted to `T`). `onbeforecommit` receives the new list (`bool M(object)` or that list type). Same mode name as [DvDataGrid](dvml-grid.md#grid-selection).
- Row height 40. Without a height, tall enough for up to 5 rows; more scroll inside (dragging scrolls, does not select; inertia and scrollbar). Hundreds of rows or several columns → `DvDataGrid`.
- One-way, fixed or `readonly` only displays (dashed `.input-readonly`); `disabled` dims. Only `id` and no `value` (`selected-items` for multi) = the handle holds the choice ([handles](dvml-code.md#handles)); neither = display only.
- Look `.listbox` (surface, border, rounded, inner padding 4), rows `.listbox-item`, chosen `.listbox-item-on` (`primary`). Row text does not wrap.

**Example**
```xml
<DvListBox value="{Bind RecipeId}" items="{Recipes}" item-value="Id" item-text="Name"/>
<DvListBox selection-mode="multi" selected-items="{Bind Zones}">
  <DvOption value="A" text="Zone A"/>
  <DvOption value="B" text="Zone B"/>
</DvListBox>
```

See also: [DvSelect](#dvselect) · [scrolling](dvml-layout.md#scroll)

<a id="dvtreeview"></a>
### DvTreeView

Expand and collapse hierarchical data (plant → line → equipment) and pick one. The data is the app's own objects (no tree node type).

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `items` | list | none | yes | Root list. A string or a non-collection = no rows |
| `item-children` | property path | none | no | Read raw. Absent, missing or empty = leaf. Depth limited to 64, cycles skipped |
| `item-text` | property path | the item as text | no | Read raw |
| `item-value` | property path | the item | no | Read raw. Absent = the item object itself is written |
| `value` | any | — | yes | The selected item's value. `{Bind X}` = VM. Absent + a handle = the handle owns it (`SelectedItem`) |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal. Expand/collapse works |

**Behavior**
- Default size: 200 × (up to 8 rows, row 40).
- **Pressing the arrow area (indent + icon) expands/collapses; pressing elsewhere selects.** Selecting does not expand.
- Initially all collapsed. The screen remembers expansion per tree instance (each `DvRepeat` item or component instance has its own), keyed by item object (`Equals`), across redraws. Collapsing a branch keeps the expansion below it.
- Row height 40, indent 20 per depth. Without a height, up to 8 rows; more scroll (dragging scrolls). No lazy loading — child lists are read fully at layout time.
- Single selection only. Ownership as other value widgets ([handles](dvml-code.md#handles)). A cycle (an ancestor appearing again as a child) skips that branch.
- Look `.treeview`, rows `.treeview-item`, chosen `.treeview-item-on` (`primary`).

**Example**
```xml
<DvTreeView items="{Plant}" item-children="Kids" item-text="Name" value="{Bind PickedEquip}"/>
<DvTreeView items="{Plant}" item-children="Kids" item-text="Name" item-value="Tag" value="{Bind PickedTag}"/>
```

See also: [DvListBox](#dvlistbox) · [DvDataGrid](dvml-grid.md#dvdatagrid)
