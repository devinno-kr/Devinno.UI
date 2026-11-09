# DVML data grid

Covers: `DvDataGrid` and its child tags `DvColumn`, `DvColumnGroup`, `DvSummaryRow`, `DvSummary` — attributes, scrolling and updates, selection, sorting, header groups, summary rows, conditional colors, immediate cells (check, lamp, button), edit cells (input, select, datetime, color) and column filters.
Read first: [markup basics](dvml-language.md#markup-basics) · [binding syntax](dvml-language.md#binding) · [value rules for true/false, numbers, words and colors](dvml-language.md#value-rules)
Not here: [which redraw call to use from code](llm-rules.md#updates-threads) · [the thread contract](dvml-app.md#threads) · [the `onbeforecommit` gate and its signatures](dvml-language.md#onbeforecommit) · [repeating rows without a grid (`DvRepeat`)](dvml-components.md#dvrepeat) · [`DvListBox` for a short pick list](dvml-inputs.md#dvlistbox) · [magenta colors](dvml-diagnostics.md#magenta) · [localization keys](dvml-app.md#localization)

<a id="dvdatagrid"></a>
## DvDataGrid

A virtualized table over an app-owned row list, with frozen columns, selection, sorting, summaries, in-cell editing and filters.

```xml
<DvDataGrid items="{Rows}" grow="1" selection-mode="multi" selected-items="{Bind Picked}" row-key="Id"
            onrowclick="Inspect" row-class="RowClass"
            style="border-radius:token(radius.md); border:1 solid token(border)">
  <DvColumn header="No"   field="Id"   width="80" frozen="true" sortable="true"/>
  <DvColumn header="Name" field="Name" cell-type="input"/>
  <DvColumnGroup header="Process values">
    <DvColumn header="Value" field="Value" width="120" format="0.00" sortable="true" sort="desc" cell-class="ValueClass"/>
    <DvColumn header="Equipment" field="Equip.Name" width="20%" align="center"/>
  </DvColumnGroup>
  <DvSummaryRow label="Total" fn="sum"/>
  <DvSummaryRow label="Average">
    <DvSummary field="Value" fn="avg" format="0.00"/>
    <DvSummary field="Name" fn="count"/>
  </DvSummaryRow>
</DvDataGrid>
```

### Attributes

`DvDataGrid` also takes the [common attributes](dvml-language.md#common-attribute-matrix) (`id`, `class`, `style`, `grow`, `width`, `height`, `visible`, `disabled` …).

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `items` | row list | none | yes | `IList` recommended: referenced without copying (even 300,000 rows are instant) |
| | | | | Any other `IEnumerable` is copied on every layout (warning) |
| | | | | null, an empty list, a string or a non-collection = header only |
| `row-height` | number (px) | `48` | yes | All rows have the same height; `<= 0` = 48, silently |
| `header-height` | number (px) | `40` | yes | Height of one header tier; `<= 0` = 40, silently |
| `selection-mode` | `none` `single` `multi` | `none` | yes | `multi` = each tap toggles the row |
| `selected-items` | `{Bind X}` or `{X}` | none | yes | Selection binding, same role as `DvListBox selected-items`; elements are row objects; single mode also takes one `T` ([selection](#grid-selection)) |
| | | | | Applied only when `selection-mode` is not `none` |
| `row-key` | property path | the item reference | no | Identifies a row; keeps selection and editing when row objects are replaced |
| `onrowclick` | method name | none | no | `M(Row row)` if it exists and the row fits, otherwise `M()`; called after the selection is updated |
| `row-class` | property path | none | no | The row item property's value = class names applied to the row ([conditional colors](#grid-groups-summary-colors)) |
| `header-color` | color | `grid-*` token | yes | Header background |
| `header-text-color` | color | `grid-*` token | yes | Header text |
| `text-color` | color | `grid-*` token | yes | Cell text |
| `row-color` | color | `grid-*` token | yes | First stripe color |
| `row-alt-color` | color | `grid-*` token | yes | Second stripe color |
| `hover-color` | color | `grid-*` token | yes | Row under the pointer |
| `selected-color` | color | `grid-*` token | yes | Selected rows |
| `line-color` | color | `grid-*` token | yes | Grid lines |
| `summary-color` | color | `grid-*` token | yes | Summary band background |
| `summary-text-color` | color | `grid-*` token | yes | Summary band text |

Color attributes take `#rrggbb[aa]` or `token(name)`. Omitted = the matching `grid-*` theme token; unreadable = that token + a Trace warning; an unknown token = [magenta](dvml-diagnostics.md#magenta) + a Trace warning.

### Behavior

- Default size 480 × (header + 5 rows + summary rows).
- Rounded corners and borders are set with `style`. Cells, header and scrollbars are clipped inside the corners, and the border is drawn on top.
- Cell text uses the grid's `font-size`.
- Child elements: `DvColumn`, `DvColumnGroup`, `DvSummaryRow`. They take only their own attributes and `id`.

### See also

[DvColumn](#dvcolumn) · [scrolling and updates](#grid-scroll-updates) · [filters](#grid-filters) · [choosing a widget](llm-rules.md#choose-widget)

<a id="dvcolumn"></a>
## DvColumn

One column of a `DvDataGrid`: header, value path, width, display and cell kind.

### Attributes

General:

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `id` | identifier | none | no | The only common attribute a column takes |
| `header` | text | `""` | interpolation only | Header text (`"{X} units"` formats the value into the text) |
| `field` | property path (`A.B`) | `""` | no | null in the middle of the path = empty cell |
| | | | | Missing property = empty cell + warning |
| | | | | Empty = no value (no sort, no edit) |
| `width` | `120` (px) · `20%` (of grid width) · `*` · `2*` | `*` | yes | Star columns split the remaining width by weight, minimum 48 |
| | | | | Not enough width = horizontal scrolling |
| `align` | `left` `center` `right` | by value type | yes | Numbers right, everything else left (select, datetime and color cells are left) |
| `format` | .NET format string | none | yes | InvariantCulture; a format not usable for the value = default text + Trace warning |
| | | | | Long text is truncated with "…" |
| `frozen` | `true` `false` | `false` | yes | Fixed during horizontal scrolling |
| | | | | Only columns contiguous from the start; `frozen` on a middle column is ignored + warning |
| `sortable` | `true` `false` | `false` | yes | Sort by tapping the header ([sorting](#grid-sorting)) |
| `sort` | `asc` `desc` | none | yes | Initial sort; with several columns, column order = priority |
| `cell-class` | property path | none | no | The row item property's value = class names of this column's cells ([conditional colors](#grid-groups-summary-colors)) |
| `cell-type` | `text` `check` `lamp` `button` `input` `select` `datetime` `color` | `text` | yes | [Immediate cells](#grid-immediate-cells) · [edit cells](#grid-edit-cells) |
| `filter` | `none` `auto` `text` `values` `range` `date` | `none` | yes | [Filters](#grid-filters) |

Cell-kind attributes (read only for the named cell kinds):

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `readonly` | `true` `false` | `false` | yes | check and edit cells: shown, not written |
| `onbeforecommit` | method name | none | no | check and edit cells; [gate rules](dvml-language.md#onbeforecommit) — the row is the item |
| `on-color` | color | `state-run` token | yes | lamp cells; unreadable = `state-run` + warning; unknown token = magenta + warning |
| `blink` | `true` `false` | `false` | yes | lamp cells; a lit lamp blinks at a fixed 500 ms (no `blink-interval`) |
| `text` | text | `""` | interpolation only | button cells; a code `Text` override on the `DvColumn` handle wins and is used as is |
| `icon` | icon name | none | yes | button cells |
| `variant` | `primary` `secondary` `danger` `ghost` | `primary` | yes | button cells |
| `onclick` | method name | none | no | button cells: `VM.M(Row row)` or `VM.M()` |
| `cell-disabled` | property path | none | no | button cells: an item bool property; true = that row's button is dimmed and inactive (same direction as the common `disabled`) |
| `min` | number | none | yes | input cells: typed values below are rejected |
| `max` | number | none | yes | input cells: typed values above are rejected; both given and `min >= max` = 0..100 + warning |
| `unit` | text | none | interpolation only | input cells: drawn after the value |
| `mode` | `date` `time` `datetime` | by value type | yes | datetime cells; a mismatch with the value type = by value type + warning |
| `show-seconds` | `true` `false` | `false` | yes | datetime cells |
| `items` | list | none | yes | select cells: option list, relative to the grid VM; wins over `DvOption` children, which win over the enum of the value type |
| `item-value` | property path | the item itself | no | select cells |
| `item-text` | property path | the value as text | no | select cells |
| `item-icon` | property path | none | no | select cells |

### Behavior

- A column with an unreadable word value (`cell-type`, `filter`, `variant`, `align`, `sort`, `mode`) uses the default + Trace warning ([word rule](dvml-language.md#value-word)).
- Columns may be grouped under a [`DvColumnGroup`](#dvcolumngroup).

### Example

```xml
<DvDataGrid items="{Rows}" grow="1">
  <DvColumn header="Tag" field="Tag" width="140" frozen="true"/>
  <DvColumn header="Value" field="Value" format="0.0" sortable="true"/>
  <DvColumn header="Run" field="Running" cell-type="lamp" blink="true"/>
</DvDataGrid>
```

### See also

[DvDataGrid](#dvdatagrid) · [header groups and summaries](#grid-groups-summary-colors)

<a id="dvcolumngroup"></a>
## DvColumnGroup

A header tier spanning the columns it contains.

### Attributes

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `id` | identifier | none | no | The only common attribute a group takes |
| `header` | text | `""` | interpolation only | Group header text |

### Behavior

- Nesting adds tiers; each tier's height is `header-height`.
- A group with no columns is dropped.
- A group spanning the frozen-column boundary is drawn split in two (warning).

### Example

See the [DvDataGrid example](#dvdatagrid) (`Process values` group).

<a id="dvsummaryrow"></a>
## DvSummaryRow

A summary band at the bottom of the grid.

### Attributes

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `id` | identifier | none | no | The only common attribute a summary row takes |
| `label` | text | `""` | interpolation only | Shown in the first column. That cell always holds the label (empty without one), never a summary |
| `fn` | `sum` `avg` `min` `max` `count` | none | yes | Applies to every numeric column without its own `DvSummary`; unknown = none + warning |

### Behavior

- Place `DvSummaryRow` directly under `DvDataGrid`.
- `DvSummary` children set single cells of this row.

### Example

See the [DvDataGrid example](#dvdatagrid) (`Total` and `Average` rows).

<a id="dvsummary"></a>
## DvSummary

One cell of a `DvSummaryRow`, for one column.

### Attributes

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `id` | identifier | none | no | The only common attribute a summary cell takes |
| `field` | property path | `""` | no | Exactly the same path as the column's `DvColumn field`; no matching column = warning, the cell is ignored. The first column's field is ignored without a message (that cell is the row's `label`) |
| `fn` | `sum` `avg` `min` `max` `count` | none | yes | Unknown = none + warning |
| `format` | .NET format string | the column's `format` | yes | Unusable format = default text + warning |
| `value` | any object | none | yes | Present = shown instead of the computed value (server totals); also when it resolves to null |

### Behavior

- See [summary rules](#grid-groups-summary-colors).

### Example

See the [DvDataGrid example](#dvdatagrid) (`Average` row).

<a id="grid-scroll-updates"></a>
## Scrolling and updates

- Only visible rows are drawn. Wheel, drag, inertia and scrollbars behave like `overflow:scroll`. The header is fixed, and `frozen` columns are fixed during horizontal scrolling.
- Cell values are read **at draw time**.
- Changed cell values, or rows added/removed in the **same list**: call `Repaint()` (`DvSurface.Repaint()`, `ViewWindow.Repaint()`, `ViewControl.MarkRepaint()`). No rebuild; callable from another thread. Full table: [redraw calls](llm-rules.md#updates-threads).
- `items` pointing to a **different list**: call `Invalidate()`.
- Do not modify the list from another thread while it is being drawn ([thread contract](dvml-app.md#threads)).

<a id="grid-selection"></a>
## Selection

- `selected-items="{Bind X}"` writes a **new list** every time the selection changes. `X` can be `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`; in single mode, `T` is also allowed. Another property type = Trace warning.
- Without `{Bind}`, the selection lives only on screen. One-way `{X}` is re-applied when the VM assigns a new value: do not modify the list, assign a new list.
- With `row-key`, the selection is kept when row objects are replaced.
- Rows removed by a filter are dropped from the selection ([filters](#grid-filters)).

<a id="grid-sorting"></a>
## Sorting

- Tapping the header of a `sortable="true"` column cycles none → ▲ (asc) → ▼ (desc) → none.
- Sorts stack in tap order; with 2 or more, a priority number is shown.
- `sort="asc|desc"` = initial sort.
- The app's list is not changed: the order exists only inside the grid.
- Stable sort, nulls first, no exception with mixed types.
- Re-sorts on: header tap, `items` pointing to a different list, a change in row count.
- **A change in values alone does not re-sort** when no filter condition is set, so rows do not move under the finger. Refilling the same list with the same row count does not re-sort either: assign a new list.
- With a filter condition set, `Repaint()`, `Invalidate()` and edit end re-filter and re-sort ([filters](#grid-filters)).
- Selection and `onrowclick` follow the visible order.
- Edited columns are not re-sorted when no filter condition is set.

<a id="grid-groups-summary-colors"></a>
## Header groups, summary rows, conditional colors

**Header groups**
- `DvColumnGroup header` creates a header tier grouping columns. Nesting adds tiers; each tier's height is `header-height`.
- A group spanning the frozen-column boundary is drawn split in two (warning).

**Summary rows**
- `DvSummaryRow label fn` = a summary band at the bottom. `fn` = `sum` `avg` `min` `max` `count`.
- The first column of every summary row shows the row's `label` (empty without one). Summaries go only in the other columns: the row's `fn` covers the numeric columns from the second on, and a `DvSummary` on the first column shows nothing, with no build or Trace message. To count rows, put the count on another column: `<DvSummary field="Message" fn="count"/>`.
- Place `DvSummaryRow` directly under `DvDataGrid`.
- `DvSummary field fn format` sets one column's cell (`field` is exactly the same path as `DvColumn field`).
- `DvSummary value="{X}"` shows that value instead of a computed one.
- Non-numeric values and nulls are excluded from computation; `count` = the number of non-null values.
- Summaries are recomputed after `Invalidate()`/`Repaint()`, and compute **visible rows only** when a filter is set.

**Conditional colors**
- `row-class="RowClass"` (grid) and `cell-class="ValueClass"` (column) name properties on the row item.
- That property's value is class names: space-separated, later wins, null = none. The condition is app code.

```csharp
public string? ValueClass => Value > Hi ? "over" : null;
```
```xml
<DvStyle>.over { background: token(tone-danger); color: #ffffff; font-weight: bold }</DvStyle>
```

- Grid classes use **only** `background` (solid color), `background-color`, `color`, `font-weight`.
- Background priority: `cell-class` > selection > `row-class` > hover > stripe.
- Unknown classes are ignored.
- Classes are read at draw time: call `Repaint()` when the value changes.

<a id="grid-immediate-cells"></a>
## Immediate cells (`check` `lamp` `button`)

| `cell-type` | Behavior | Extra attributes |
|---|---|---|
| `check` | `bool`/`bool?` property. A tap toggles it and **writes directly to the row item**. A property with no public setter is only displayed, dimmed | `onbeforecommit` — without it the value is written directly; a missing method or a signature mismatch = nothing written ([gate rules](dvml-language.md#onbeforecommit)) · `readonly` |
| `lamp` | Display only. `bool` → on = `on-color` (default `state-run` token). string/enum → the `state-{value}` token; a value the theme has no token for = off, no warning | `on-color` (`#rrggbb` or `token(name)`, bindable; unreadable = `state-run` + warning; unknown token = magenta) · `blink="true"` (a lit lamp blinks) |
| `button` | `onclick` → `VM.M(Row row)` or `VM.M()` | `text` · `icon` · `variant` (`primary`; otherwise `secondary` `danger` `ghost`) · `cell-disabled` (bool property path on the row item; true = dimmed and inactive, same direction as the common `disabled`) |

- Tapping a check or button cell performs only that action: no selection, no `onrowclick`.
- Tapping a lamp cell is a normal row tap.
- The whole cell is the touch area.

<a id="grid-edit-cells"></a>
## Edit cells (`input` `select` `datetime` `color`)

| `cell-type` | Property type | Extra attributes |
|---|---|---|
| `input` | `string` (text keyboard); `int` `long` `short` `sbyte` `uint` `ulong` `ushort` `byte` `double` `float` `decimal` and nullable (numeric keypad; integer/sign from the type) | `min` `max` (out of range is rejected; unparseable = one warning) · `unit` · `format` (display) |
| `select` | Any type | `DvOption value text icon` children, or `items="{Catalog}" item-value item-text item-icon` (relative to the grid VM, re-read on `Invalidate()`), or an enum automatically. The cell shows the chosen item's icon + text |
| `datetime` | `DateTime` `DateOnly` `TimeOnly` (+ nullable) | `mode` `show-seconds` `format` ([DvDateTimeInput rules](dvml-inputs.md#dvdatetimeinput)) |
| `color` | `string` (written as `#RRGGBB`), `SKColor` (alpha kept) | The cell shows a color swatch + hex value |

- Tapping an edit cell **edits immediately** (no selection, no `onrowclick`). `input` edits in place (virtual keyboard or native editor); the others open a popup below the cell.
- The value is written once to the row item's property (not written if it is the same). Then only the grid is redrawn, summaries included, no rebuild. Other widgets bound to the same data change on the next `Invalidate()`.
- `onbeforecommit` follows the [commit gate rules](dvml-language.md#onbeforecommit). Method missing, throwing, or returning anything other than `true` = rejected: `input` stays open showing the reason, a popup stays open.
- A throwing row property setter is handled the same way (input message `Could not save the value`, defined in the [commit gate rules](dvml-language.md#onbeforecommit)).
- **Display-only cases** (a tap is a normal row tap): `readonly="true"`, no public setter, a struct in the path, a property type that does not match the cell kind (one warning).
- Editing follows the row (`row-key`, otherwise the item reference) and survives sorting, `Invalidate()` and `Repaint()`.
- Row scrolled fully off screen: `input` commits, popups close. Row disappears: editing closes without writing.
- Header tap during `input` editing commits first; with the virtual keyboard it cancels (the keyboard is modal). A header tap while a popup is open only closes the popup.
- Only with the native editor (WinForms `ViewControl` with `TouchMode` off): Enter = next row in the same column; Tab / Shift+Tab = next/previous input column in the row (non-editable cells skipped, scrolls into view, ends at the last row/column, stops on a rejected value).
- With the virtual keyboard, committing ends editing.

<a id="grid-filters"></a>
## Filters

Giving a column `filter` adds a **filter row** below the header (no column with `filter` = no filter row). Filters work only inside the grid; the app's list is not changed.

```xml
<DvDataGrid items="{Rows}" grow="1" row-key="Id">
  <DvColumn header="Tag"      field="Tag"   width="140" filter="text"/>
  <DvColumn header="State"    field="State" width="100" filter="values"/>
  <DvColumn header="Setpoint" field="SetPoint" width="110" format="0.0" filter="range"/>
  <DvColumn header="Reserved" field="ReserveAt" width="150" format="MM-dd HH:mm" filter="date"/>
  <DvColumn header="Mode"     field="Mode" width="100" cell-type="select" filter="auto">
    <DvOption value="auto" text="Auto"/>
    <DvOption value="manual" text="Manual"/>
  </DvColumn>
</DvDataGrid>
```

| `filter` | Behavior |
|---|---|
| `none` | No filter (default, same as omitting the attribute) |
| `auto` | By column value type (the `field` property type; nullable uses the underlying type): `string` → `text`; numbers → `range`; `DateTime` `DateOnly` `TimeOnly` → `date`; `bool`, enum → `values`; a `cell-type="select"` column → `values`; anything else or unknown (e.g. no items) → `text` |
| `text` | Rows whose visible cell text contains the entered text |
| `values` | Rows with values checked in the list |
| `range` | Numeric range min–max |
| `date` | Date/time range start–end |

- Bindable; a bound value is read the same as a literal. Any other value = warning, then `none`.
- **`auto` on a string column is `text`.** To pick from a fixed set of states, use `values`.
- Constants: `Values.GridFilter` ([VM constants](dvml-language.md#vm-constants)).

**Filter row and popup**
- Tapping a filter row cell opens that column's popup.
- A cell with a condition shows an accent-colored funnel + condition summary ("…" if long); a cell without one shows only a dimmed funnel. Cells of columns without `filter` are empty.
- Popup buttons: **OK** = apply and close; **Clear** = remove that column's condition and close; **Cancel** = close unchanged. Tapping outside the popup = Cancel.
- `text` popup: one input field.
- `values` popup: "Select all" + value list (checked = shown). `(Empty)` comes first when there are empty values.
- `values` with more than 1000 distinct values lists only the first 1000; unchecking even one item then makes the condition "checked values", so values outside the list are hidden too (same as Excel).
- The `values` list is a snapshot taken when the popup opens, built from rows with the other columns' filters applied. All checked = no condition.
- `range` popup: two fields, min and max, numeric keypad rules (non-numbers are rejected with a reason line). Either side may be empty.
- `date` popup: tapping the start or end field opens a **date picker over the filter popup**. Picker OK changes only the field (no VM write). Tapping outside the picker closes only the picker.
- An empty `date` start time is 00:00:00; an empty end time is 23:59:59.

**Filtering rules**
- AND across columns; `values` within one column are OR.
- `text`: case-insensitive, culture-invariant substring match on the **visible text** (including `format` and select item text).
- `values`: any checked value matches; `(Empty)` = null or empty text.
- `range`, `date`: bounds inclusive; either side alone is allowed; cells with empty values fail range conditions.

**Interaction with other features**
- Sorting is applied to the filtered rows; summary rows compute **visible rows only**.
- Rows removed by a filter are dropped from the selection (the `selected-items` binding is updated too).
- A row being edited stays until editing ends, then is re-filtered immediately.
- Filtering is re-run on `Repaint()`, rebuild and `items` replacement. With a condition set, `Repaint()`, `Invalidate()` and edit end re-filter and re-sort, so rows whose values changed may move.
- Filter state survives rebuilds but **cannot be read or set from app code**.
- Theme tokens: `grid-filter` (filter row background), `grid-filter-fg` (funnel without condition), `grid-filter-active` (funnel with condition, summary). Full list: [theme tokens](dvml-style.md#theme-tokens).
- Localization keys (`DvStrings`): `dv.filter.ok` `clear` `cancel` `all` `empty` `min` `max` `from` `to` `true` `false` `more` ([localization](dvml-app.md#localization)).
