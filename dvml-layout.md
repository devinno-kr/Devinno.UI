# DVML layout

Covers: layout sugar tags (`DvStack` `DvRow` `DvGrid` `DvSpacer`), `DvBox` with flex/grid style, sizes, grid tracks, `position:absolute`, `aspect-ratio`, scrolling (with a checklist), text wrapping, and logical px / display scaling.
Read first: [markup basics](dvml-language.md#markup-basics) · [common attributes such as `grow`, `width`, `height`](dvml-language.md#common-attributes)
Not here: [every style key and its default](dvml-style.md#style-keys) · [which of sugar, class and inline style wins](dvml-style.md#priority) · [repeating rows with `DvRepeat`](dvml-components.md#dvrepeat) · [widget default sizes](dvml-widgets.md#semantic-components) · [instrument default sizes](dvml-instruments.md#instrument-common) · [process diagram size and fit](dvml-process-layout.md#size-fit)

<a id="layout-sugar"></a>
<a id="dvstack"></a>
<a id="dvrow"></a>
<a id="dvgrid"></a>
<a id="dvspacer"></a>
## Layout sugar (recommended)

| Tag | Expands to | Defaults |
|---|---|---|
| `DvStack` | Vertical flex | `align-items:stretch`, `gap` = `token(spacing.md)` = 12 |
| `DvRow` | Horizontal flex | `align-items:center`, `gap` = 12 |
| `DvGrid columns="N"` | Grid of N equal-width columns (next row every N children) | `gap` = `token(spacing.lg)` = 16; N omitted, 0 or less = 2 |
| `DvGrid columns="1fr 2fr 25% 120" rows="auto 1fr"` | Track-list grid ([grid tracks](#grid-tracks)) | |
| `DvSpacer` | Empty box with `flex-grow:1` (pushes neighbors to both ends) | |
| `DvRepeat` | Same as `DvStack` ([repeating rows](dvml-components.md#dvrepeat)) | |

Sugar-only attributes (`DvStack` `DvRow` `DvGrid` `DvSpacer` `DvRepeat`):

| Attribute | Values |
|---|---|
| `gap` | `none`(0) `xs`(4) `sm`(8) `md`(12) `lg`(16) `xl`(24) `2xl`(32) or a number |
| `padding` | 1–4 values, each a step name above or a number (`padding="none md"`) |
| `align` | Cross-axis alignment: `start` `center` `end` `stretch`. **No effect on `DvGrid`** ([grid cells](#grid-tracks)) |
| `justify` | Main-axis alignment: `start` `center` `end` `between`. **No effect on `DvGrid`** |

`DvGrid` attributes:

| Attribute | Values |
|---|---|
| `columns` | An integer N = N equal columns (N 0 or less = 2; default 2); anything else is a track list used as is ([grid tracks](#grid-tracks)) |
| `rows` | A track list used as is ([grid tracks](#grid-tracks)) |

- `DvGrid` accepts `align` and `justify` without a build or Trace message, but they do nothing: every child fills its cell. To place content inside a cell, see [grid tracks](#grid-tracks).

- Sugar attributes and the `grow`/`width`/`height` attributes have the lowest priority: a class or inline `style` that sets the same property wins ([priority order](dvml-style.md#priority)).
- These attributes are bindable. Bound values are interpreted exactly like literals (a number in `columns="{N}"` = column count, `md` in `gap="{G}"` = 12, `between` in `justify="{J}"`). A null value is the same as not setting the attribute.

<a id="dvbox"></a>
<a id="dvscreen"></a>
## DvBox and direct placement

`DvBox` is a plain box like a `div`. It takes the common attributes only, and its default size is its content size. Lay it out with `style`. `DvScreen` is the same plain box under another name.

- Without `display:flex` it **only stacks children vertically** (`gap`, `justify-content` and `align-items` apply; no growing or shrinking).
- With `display:flex` + `flex-direction:row|column`, it uses flex layout. Use `flex-grow`, `flex-shrink` (default 0), `flex-wrap:wrap` and `align-content`.
- With `display:grid` + `grid-template-columns`/`grid-template-rows`, it uses grid layout ([grid tracks](#grid-tracks)). With no track list at all, it is not a grid.
- The default for `align-items` is `stretch` (same as CSS). Children without a width (when stacking vertically) or without a height (in a horizontal row) fill the parent. Children with `width`/`height` keep that size, and children with `min-*`/`max-*` stretch only within those limits. To keep widgets at their default size, give the parent `align-items:start`, or give a single child `align-self:start`.
- **Fixed-shape widgets** (knobs, instruments, rings, lamps, toggles, icons) are drawn centered at their own size (round ones based on the short side) even when the box stretches. The touch area is only inside that shape: touching the empty space beside it does not change the value. The handle's `Bounds` is the stretched box.

<a id="size"></a>
## Size

- `width` `height` `min-width` `max-width` `min-height` `max-height` are numbers (px).
- Without a size, an element is its content size (text size + `padding`, or the sum of child sizes + `gap` + `padding`).
- Widgets have their own default sizes: see the tables in [basic and input widgets](dvml-widgets.md#semantic-components) and [instruments](dvml-instruments.md#instrument-common).
- `flex` shorthand: `flex: 1` = grow 1, shrink 1; `flex: 1 0` = grow 1, shrink 0. There is no basis. Because `flex: N` sets `flex-shrink:1`, it turns off the scroll-container shrinking described in [scrolling](#scroll); use `grow="1"` or `flex-grow` there.

<a id="grid-tracks"></a>
## Grid tracks

Each track of `grid-template-columns`/`grid-template-rows` (or `columns`/`rows` of `DvGrid`):

| Track | Meaning |
|---|---|
| `120`, `120px` | Fixed |
| `auto` | Largest content size among children in that column (row) |
| `25%` | Ratio of the **remaining space** after fixed/auto tracks and gaps (if the total exceeds 100%, scaled down proportionally) |
| `1fr` | Space left after `%` tracks, divided by fr ratio |
| anything else (`minmax()`, `repeat()`, `abc`) | `auto`, silently |

- If there are more rows than the row template, the extra rows are `auto`. Giving only rows without a column template makes a 1-column grid that fills the width.
- Children fill their cell (column width × row height) completely, in document order, left→right, top→bottom. There is no cell merging.
- Inside a cell, a child's own `width`/`height` and `align-self` are not used, and the grid's `align`/`justify` (and `align-items`/`justify-content`) do nothing. To place content within its cell:
  - Text of a `DvLabel` or `DvButton`: `content-align` on that element (`content-align="middle-left"` puts a label level with a 40 px input in the next column) ([content-align](dvml-style.md#content-align)).
  - Anything else: wrap it in a `DvRow` (or `DvStack`). The wrapper fills the cell and its own `align`/`justify` place the child. A fixed-shape widget (toggle, knob, gauge) placed directly in a cell is drawn centered in the whole cell, far from a label in the column to its left; inside a `DvRow` it sits at the start.

```xml
<DvGrid columns="180 1fr" gap="sm">
  <DvLabel class="body" text="Recipe name" content-align="middle-left"/>
  <DvInput value="{Bind Name}"/>
  <DvLabel class="body" text="Auto start" content-align="middle-left"/>
  <DvRow><DvToggle value="{Bind AutoStart}"/></DvRow>
</DvGrid>
```
- When the grid itself is content-sized (no width given and not stretched), `%` tracks are measured as `auto`, and `fr` columns as in CSS: `fr × (largest content width per fr)`. With `1fr 1fr`, both columns get the same width, fitted to the widest content. `fr` in rows is the content height during measurement.
- Track lists may be quoted: `grid-template-columns:"1fr 2fr 120"`.

<a id="absolute"></a>
## Absolute position

`position:absolute` + `left` `top` `right` `bottom`.

- The reference is **the content area (inside padding) of the immediate parent**: `left`/`top` are relative to the parent's content box. The parent does not need `position:relative`.
- `left`+`right` together set the width; `top`+`bottom` together set the height.
- Absolutely positioned children are excluded from flow layout and from the parent's size calculation, in every kind of box (stack, flex and grid).
- `position:relative` is parsed but has no effect.
- To place a box at a widget's `Bounds` ([handles](dvml-code.md#handles)), put it under a root without padding so the `Bounds` values can be used as-is.

<a id="aspect-ratio"></a>
## aspect-ratio

`aspect-ratio: 1` (or `16/9`, `1.5`) = width/height. The element becomes the largest box of that ratio that fits in the allotted area, centered (it can be combined with `grow`/stretch). If only `width` or `height` is given, the other is computed from the ratio during measurement.

<a id="scroll"></a>
## Overflow and scrolling (scroll panel)

| Value | Behavior |
|---|---|
| `visible` (default) | No clipping |
| `hidden` | Clips children to the rounded border shape |
| `scroll` | Clips, and scrolls along the axis where content overflows |

There is no dedicated scroll panel tag. **Give any box `style="overflow:scroll"`** (`DvStack`, `DvBox`, `DvRepeat`, …) and that box becomes a scroll panel.

### When scrolling happens

Scrolling happens **only when the content is larger than the box**. So the scroll box's size (its height, for a vertical list) **must be determined** independently of the content. There are three ways:

| Method | How | Result |
|---|---|---|
| (1) Fill the remaining space | `grow="1"` on the scroll box (or the card containing it) | The height left over after everything else on the screen; follows window resizing. **Most common** |
| (2) Fixed | `style="height:300; overflow:scroll"` | Always 300 |
| (3) Maximum | `style="max-height:320; overflow:scroll"` | Content height when content is small; scrolls at 320 when large |

For (1), **every parent from the scroll box up to the window must use flex layout** so the remaining space is passed down.

- Flex layout: `DvPageFrame`, `DvStack`, `DvRow`, `DvCard`, the tab body of `DvTabs`, boxes with `style="display:flex"`.
- **Not** flex: `DvBox`, `DvScreen` (a plain box under another name), boxes without `display`. If such a box sits in between, `grow` has no effect below it and no scrolling happens. Replace it with `DvStack`.
- Start the top of the window with `DvPageFrame` (a `DvApp` page) or `DvStack`. If the top of the page is `DvScreen`, `grow` directly below it does not work.

### Common patterns

**A list that fills the rest of the page** (most common): `grow="1"` on both the card and the list. The card shrinks down to just its title row, and the inner list takes the rest by scrolling (see the sizing rules below).

```xml
<DvPageFrame title="Equipment list">
  <DvRow><DvBadge text="{SelectedText}"/></DvRow>
  <DvCard title="Pumps and valves" grow="1">
    <DvRepeat items="{Items}" gap="xs" grow="1" style="overflow:scroll">
      <DvStatusItem label="{Item.Name}" state="{Item.State}"/>
    </DvRepeat>
  </DvCard>
  <DvCard title="Summary"><DvLabel text="{Summary}"/></DvCard>
</DvPageFrame>
```

**A list with a set height**: one of several blocks inside a card, at a fixed height.

```xml
<DvCard title="Recent events">
  <DvStack gap="xs" style="height:240; overflow:scroll">
    <DvRepeat items="{Events}"><DvLabel text="{Item}"/></DvRepeat>
  </DvStack>
</DvCard>
```

**Content-sized, max height when overflowing**: small with a few lines, stops growing when there are many.

```xml
<DvStack gap="xs" style="max-height:320; overflow:scroll">
  <DvRepeat items="{Notes}"><DvLabel text="{Item}"/></DvRepeat>
</DvStack>
```

**Horizontal scroll row**: cards or instruments overflowing horizontally. Give it a height: a horizontal scroll box can also shrink vertically, so without a height it is flattened when space is short.

```xml
<DvRepeat items="{Pressures}" style="display:flex; flex-direction:row; gap:16; height:164; overflow:scroll">
  <DvGauge value="{Item}" style="width:140; height:140"/>
</DvRepeat>
```

**A list with a fixed side panel**: the row (`DvRow`) fills the remaining space, and only the list inside it scrolls.

```xml
<DvRow grow="1" align="stretch" gap="md">
  <DvStack grow="1" style="overflow:scroll">
    <DvRepeat items="{Items}"><DvLabel text="{Item.Name}"/></DvRepeat>
  </DvStack>
  <DvCard title="Detail" style="width:280"><DvLabel text="{Detail}"/></DvCard>
</DvRow>
```

### Sizing rules (in detail)

- **Height works as in CSS.** Without `height`, it is the content height (+`padding`), limited by `min-height`/`max-height`.
- **When space is short, the scroll box shrinks.** A scroll box without `flex-shrink` is treated as shrink 1, minimum 0 (the default for other elements is 0). Inside a flex parent with a determined height, when siblings take up space, the scroll box shrinks so the rest stays inside the parent.
- **Boxes containing a scroll box shrink too.** If a scroll box is inside a flex box (`DvCard`, `DvStack`, …) that has no `flex-shrink`, then when space is short that box shrinks **as far as the inner scroll box can shrink** (for a card: the title row + the scroll box's `min-height`). This holds through multiple levels of nesting, and it is why "a list that fills the rest of the page" works as written. `flex: N` counts as setting `flex-shrink:1`, which turns this off; use `grow="1"` or `flex-grow`.
  - Difference from CSS: CSS needs `min-height:0` on the card for it to shrink (a common pitfall: with only `flex: 1`, the card grows to content height and nothing scrolls). Here it is not needed; `height:0` on the card also works.
  - When there is space to spare, nothing changes (content size + the `grow` share of the remainder). Boxes that do not contain a scroll box do not shrink.
  - Side-by-side children (a `DvRow` containing a vertical list) are sized by the tallest cell: the scroll cell never gets shorter than the content of the neighboring cell.
- **Shrinking happens only inside a flex parent.** Inside a default-flow parent (`DvBox`, `DvScreen`, any box without `display`), the scroll box takes its content height and overflows the parent, even when the parent's height is set. Give it `height`/`max-height`, or make the parent a `DvStack`.
- Shrinking never goes below `min-height`/`min-width`. A box with `flex-shrink:0` (scroll box or containing box) does not shrink.
- If nothing in the parent chain determines a height (all content-sized), the scroll box takes its content height and **no scrolling happens**.
- `max-height`/`max-width` is applied again to the size grown by `grow`. The space left over this way is not redistributed to other siblings (CSS redistributes).
- The same goes for shrinking: when one sibling shrinks less because it hits `min-height`/`min-width`, the other siblings do not shrink more to make up for it (CSS redistributes). That share overflows the parent, so if several boxes must shrink, lower their `min-*` generously or let only one box shrink.

### Interaction and appearance

- Input: mouse wheel (48 per notch); drag (moving 8 or more cancels the press so the button is not pressed; on release it continues with inertia); draggable scrollbar.
- On an overflowing axis, 12 is reserved for the scrollbar so content does not touch it.
- Knobs (unless `input-mode="tap"`), sliders and custom widgets that capture the gesture keep the drag for themselves inside a scroll box (no scrolling). See [DvKnob](dvml-widgets.md#dvknob), [DvSlider](dvml-widgets.md#dvslider), [custom widgets](dvml-components.md#dvwidget).
- The scroll position is kept across `Invalidate()` and page transitions (`DvApp` keeps page screens).
- For many rows (thousands or more), use [`DvDataGrid`](dvml-grid.md#dvdatagrid) instead of `DvRepeat`. `DvRepeat` creates a box for every row (no virtualization).

<a id="scroll-checklist"></a>
## When scrolling does not work: checklist

1. Does the box to scroll have `style="overflow:scroll"`?
2. Is that box's height determined: one of `grow="1"`, `height`, `max-height` ([the three ways](#scroll))?
3. With `grow="1"`, are **all** parents from that box up to the top of the page flex? A `DvBox`, `DvScreen` or box without `display` in between → replace it with `DvStack`.
4. Does the top of the parent chain receive the window size? On a `DvApp` page, `DvPageFrame` fills the window.
5. Is a `flex-shrink:0`, a `flex: N`, or a large `min-height` somewhere preventing shrinking?
6. Verify with an off-screen PNG: send `PointerWheel` to the `DvSurface`, redraw, and check whether the image changes ([PNG verification](llm-rules.md#verify-png)).

<a id="text-wrap"></a>
## Text wrapping (`white-space`)

Elements with text (`DvLabel`, button text, …) split into multiple lines when the text is longer than the width they are placed in, and their height grows with the line count (height = line count × font line spacing + `padding`). Text that fits on one line keeps its position and size.

- **Break points**: at spaces first; where there are no spaces (long words, such as Korean text without spaces), per character. There is no hyphenation, no line-breaking rules (kinsoku), no ellipsis (`text-overflow`) and no line-count limit.
- **Alignment**: each line is aligned horizontally by `text-align`; the block of lines is aligned vertically by [`content-align`](dvml-style.md#content-align).
- **To keep text on one line**, use `white-space:nowrap`. It is inherited, so on a parent it makes all text inside single-line (reset inside with `white-space:normal`).
- **Write long text as a single label.** Do not create one label per line: when the width changes (window size, theme font size), the engine re-wraps it.

Where the wrap width (available width) comes from:

| Place | Wrap width |
|---|---|
| Screen root | The screen (cell) width |
| Vertical layout (`DvStack`, `DvRepeat`, `flex-direction:column`, `DvBox` without `display`) | The parent's inner width (excluding `padding` and the scrollbar space). Not for `flex-wrap:wrap` |
| Grid | The cell (column) width |
| Dialog root | Window width − 32. The dialog width is `min(single-line width, window width − 32)` and respects `width`, `min-width`, `max-width` ([defining dialogs](dvml-dialogs.md#dialog-define)) |
| Element with `width` | That width (with only `max-width`: up to that width) |
| Parent that does not honor `width` (stretch in vertical layout, grid cells, `grow` in horizontal layout, `position:absolute` with `left`+`right`) | The width the element is actually placed in |

Differences from CSS:

- `\n` breaks the line in both `normal` and `nowrap` (CSS `normal` treats it as a space). Whitespace is not collapsed: text is shown exactly as written.
- **Children of a horizontal layout (`DvRow`, `flex-direction:row`) receive no wrap width and stay on one line** (CSS shrinks and wraps). To wrap inside a horizontal layout, give that child a `width` or `max-width`. Text inside a child whose width is changed by `grow`/`flex-shrink` re-wraps at that width, but the row height is based on a single line, so it can overflow downward.
- Text boxes with an icon (buttons/chips with `icon`) are always single-line.
- Space left over by `max-*` after `grow`, and the share not shrunk due to `min-*`, are not redistributed to other siblings ([sizing rules](#scroll)).

```xml
<DvCard title="Notice" style="width:320">
  <DvLabel class="body" text="The filter replacement interval has passed. Stop the unit, replace the filter, and reset the accumulated run time after replacement."/>
  <DvLabel class="caption muted" text="TAG-1001 · last replaced 2026-09-01" style="white-space:nowrap"/>
</DvCard>
```

<a id="logical-px"></a>
## Logical px and display scaling (DPI)

- px in markup are **logical px** (the same as CSS px). At 100% display scale, a logical px is one physical pixel.
- At a Windows display scale of 125% or 150%, both adapters scale drawing accordingly, so text and widgets keep the same physical size as on a 100% display, only sharper.
- Pointer coordinates, `Bounds`, and the `width`/`height` of the `ViewWindow` constructor are logical px (at 150%, a `1280×800` window is 1920×1200 physical).
- When the scale changes at runtime (moving to a monitor with a different scale, changing the display scale), the screen redraws at the new scale immediately, and `ViewWindow` resizes the window to keep its logical size.
- Host-specific setup (WinForms per-monitor scaling, `fit`): [ViewWindow](dvml-app.md#viewwindow) · [ViewControl](dvml-app.md#viewcontrol).
