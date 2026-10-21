# Devinno.UI Markup Reference

This document collects every tag, attribute, style key, value, default, binding syntax, handler signature and theme token of Devinno.UI screen markup.

- **Read [`llm-rules.md`](llm-rules.md) first.** The principles and pitfalls (how it differs from CSS, what is silently ignored) are there.
- Assume any tag, attribute or value not in this document is not supported.
- The process diagram (`DvProcess`) is covered in detail in a separate document, [`dvml-process.md`](dvml-process.md).
- On the C# side, only the minimum that connects to the markup is described here. For detailed C# usage, see the XML doc comments of each type (IntelliSense, `Devinno.UI.xml` in the package).

## Contents

1. Getting started
2. Markup basics
3. Layout
4. Styles
5. Theme tokens
6. Data binding and interaction
7. Semantic components
8. Basic widgets (button, toggle, checkbox, radio, segment, tabs, expander, stepper)
9. Instrument widgets (gauge, ring, progress bar, tank, knob, slider, graph)
10. Input widgets (DvInput, DvSelect, DvDateTimeInput, DvColorInput, DvListBox, DvTreeView)
11. Data grid (DvDataGrid)
12. Trend (DvTrend)
13. Alarms (DvAlarmBanner, DvAlarmList)
14. Repeat (DvRepeat)
15. User components (DvComponent)
16. Pages and app (DvApp, DvPage, DvBar)
17. Localization
18. Icons
19. Host summary
20. Working with controls from code (`id`, `vm`)
21. Dialogs (DvDialog, ShowDialog, MessageBox)
22. Process diagram (DvProcess, DvSymbol) — details in [`dvml-process.md`](dvml-process.md)

---

## 1. Getting started

This is the smallest program. One screen is a `partial` class deriving from `DvPage` (which doubles as the VM) plus a dvml file whose root `vm="…"` points to that class; `DvApp` holds the page and puts it on an OpenTK window (`ViewWindow`).

```csharp
using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Pages.Add(new MainPage());                  // 처음 넣은 페이지가 시작 페이지
using var window = new ViewWindow(app, 1280, 800, "My HMI");
window.Run();

public sealed partial class MainPage : DvPage   // 마크업의 vm. dvml 과 짝(생성기가 연결, 20장)
{
    public bool Running { get; set; } = true;
    public bool AutoMode { get; set; }
    public string StatusText => Running ? "정상 운전" : "정지";
    public string StatusTone => Running ? "success" : "danger";
    public void Start() => Running = true;
    public void Stop() => Running = false;
}
```

Below is `MainPage.dvml`. The root `vm` points to the class above (the file name plays no part in the connection — using the same name is a convention).

> **`vm` is the full class name including the namespace** (`vm="MyApp.Pages.MainPage"`). The samples in this document declare their classes without a namespace, so they use the bare name. A wrong name is build error DVML005 (20.7).

```xml
<DvPageFrame vm="MainPage" title="정수장 2공정" subtitle="실시간 운영">
  <DvRow>
    <DvBadge tone="{StatusTone}" text="{StatusText}"/>
    <DvSpacer/>
    <DvButton variant="primary" text="운전 시작" onclick="Start"/>
    <DvButton variant="danger" text="비상 정지" onclick="Stop"/>
  </DvRow>
  <DvGrid columns="4">
    <DvStat label="처리 유량" value="1,284" unit="m3/h" delta="+3.2%" tone="success"/>
    <DvStat label="출수 탁도" value="0.18" unit="NTU" delta="-0.04" tone="success"/>
    <DvStat label="전력 사용" value="412" unit="kW" delta="+8.1%" tone="warning"/>
    <DvStat label="가동률" value="98.6" unit="%" delta="목표 달성" tone="info"/>
  </DvGrid>
  <DvRow align="stretch" grow="1">
    <DvCard title="수위" badge="3기" grow="1">
      <DvRow justify="between">
        <DvTank value="64" unit="%" alarm-zones="0:70:#3b82f6 70:88:#f59e0b 88:100:#ef4444" style="width:84; height:200"/>
        <DvGauge value="58" ticks="20" minor-ticks="5" style="width:160; height:160"/>
      </DvRow>
    </DvCard>
    <DvCard title="설비 상태" width="310">
      <DvStatusItem label="취수 펌프 #1" state="run" status="운전"/>
      <DvStatusItem label="여과기 B" state="fault" status="경보" blink="true"/>
      <DvSpacer/>
      <DvSwitch label="자동 운전" value="{Bind Data.AutoMode}"/>
    </DvCard>
  </DvRow>
</DvPageFrame>
```

- For multiple pages or a top bar / sidebar, see chapter 16 (16.1 full example); for other hosts (WinForms `ViewControl`, self-drawn `DvSurface`, PNG rendering `DvRenderer`), see chapter 19.
- Put the dvml above anywhere in the project as a `.dvml` file (`*.dvml` is automatically a generator input, chapter 20).
- Set the theme with `app.Theme`. If omitted, `DvTheme.Default` (= `DvTheme.Dark`) is used. Style tokens and widget internal colors are all drawn with this theme. For a light screen, specify `DvTheme.Light` explicitly.
- The window size is decided by the adapter (window/control). The markup does not decide the window.

---

## 2. Markup basics

- Markup MUST be **well-formed XML** (one root element, every tag closed, attributes quoted). Write `&` and `<` in text as `&amp;` and `&lt;`.
- **Tag names and attribute names are case-sensitive.** `onclick` (correct), `onClick` (wrong).
- The root element always fills the whole window (or drawing area). The root's `width`/`height` are ignored.
- Unknown tags are treated as a plain box (`DvBox`). Names like `DvScreen` and `DvShell` are likewise just boxes.
- Text can be given via the `text` attribute or as text inside the element (`DvLabel` and `DvButton` only). The text of an element that has child elements is ignored.

### Attributes common to all elements

| Attribute | Description | Binding |
|---|---|---|
| `style` | Inline style (chapter 4) | Yes (`{X}` interpolation inside the value, `token()`) |
| `class` | Space-separated class list | Yes |
| `hover-style` `press-style` `disabled-style` | Per-state inline style (4.7) | Yes |
| `grow` | Same as `flex-grow` | Yes |
| `width` `height` | Same as style `width`/`height` (lower priority, 4.7) | Yes |
| `content-align` | Text/icon position (4.4) | Yes |
| `visible` | If false, empty, `0` or `false`, the element is not created at all | Yes |
| `disabled` | If `true`, blocks clicks and commits (the press does not leak to an outer `onclick` either), no hover/press look, `disabled-style` applied. **Inherited downward** — if an ancestor is disabled, the child is disabled even if its own value is `false` | Yes |
| `onclick` | VM method name to call on click (6.4) | No (name as is) |
| `hold` | Long press (ms) — must be held this long for `onclick` to fire (prevents accidental operation, 6.4) | Yes |
| `id` | Name of the control to grab from code (chapter 20). Does not affect how the screen looks | No (literal text, C# identifier) |

---

## 3. Layout

### 3.1 Layout sugar (recommended)

| Tag | Expands to | Defaults |
|---|---|---|
| `DvStack` | Vertical flex | `align-items:stretch`, `gap` = `token(spacing.md)` = 12 |
| `DvRow` | Horizontal flex | `align-items:center`, `gap` = 12 |
| `DvGrid columns="N"` | Grid of N equal-width columns (next row every N children) | `gap` = `token(spacing.lg)` = 16, N omitted or 0 or less = 2 |
| `DvGrid columns="1fr 2fr 25% 120" rows="auto 1fr"` | Track-list grid (3.4) | |
| `DvSpacer` | Empty box with `flex-grow:1` (pushes neighbors to both ends) | |
| `DvRepeat` | Same as `DvStack` (chapter 14) | |

Sugar-only attributes (`DvStack` `DvRow` `DvGrid` `DvSpacer` `DvRepeat`):

| Attribute | Values |
|---|---|
| `gap` | `none`(0) `xs`(4) `sm`(8) `md`(12) `lg`(16) `xl`(24) `2xl`(32) or a number |
| `padding` | 1–4 values, each a step name above or a number (`padding="none md"`) |
| `align` | Cross-axis alignment: `start` `center` `end` `stretch` |
| `justify` | Main-axis alignment: `start` `center` `end` `between` |

Sugar attributes and the `grow`/`width`/`height` attributes have the lowest priority, so if a class or inline `style` sets the same property, that wins. These attributes are bindable, and bound values are interpreted exactly like literals (a number in `columns="{N}"` = column count, `md` in `gap="{G}"` = 12, `between` in `justify="{J}"`). A null value is the same as not setting the attribute.

### 3.2 DvBox and direct placement

`DvBox` is a plain box like a `div`. Lay it out with `style`.

- Without `display:flex` it **only stacks children vertically** (`gap`, `justify-content` and `align-items` apply; no growing or shrinking).
- With `display:flex` + `flex-direction:row|column`, it uses flex layout. Use `flex-grow`, `flex-shrink` (default 0), `flex-wrap:wrap` and `align-content`.
- With `display:grid` + `grid-template-columns`/`grid-template-rows`, it uses grid layout (3.4). With no track list at all, it is not a grid.
- The default for `align-items` is `stretch` (same as CSS). Children without a width (when stacking vertically) or without a height (in a horizontal row) fill the parent. Children with `width`/`height` keep that size, and children with `min-*`/`max-*` stretch only within those limits. To keep widgets at their default size, give the parent `align-items:start`, or for a single child, give that child `align-self:start`.
- **Fixed-shape widgets** such as knobs, instruments, rings, lamps, toggles and icons are drawn centered at their own size (round ones based on the short side) even when the box stretches, and the touch area is only inside that shape. Touching the empty space beside it does not change the value. The handle's `Bounds` is the stretched box.

### 3.3 Size

- `width` `height` `min-width` `max-width` `min-height` `max-height` — numbers (px).
- Without a size, it is the content size (text size + `padding`, or sum of child sizes + `gap` + `padding`).
- Widgets have their own default sizes (tables in chapters 8–13).
- `flex` shorthand: `flex: 1` = grow 1, shrink 1; `flex: 1 0` = grow 1, shrink 0. There is no basis.

### 3.4 Grid tracks

Each track of `grid-template-columns`/`grid-template-rows` (or `columns`/`rows` of `DvGrid`):

| Track | Meaning |
|---|---|
| `120`, `120px` | Fixed |
| `auto` | Largest content size among children in that column (row) |
| `25%` | Ratio of the **remaining space** after fixed/auto tracks and gaps (if the total exceeds 100%, scaled down proportionally) |
| `1fr` | Space left after `%` tracks, divided by fr ratio |

- If there are more rows than the row template, the extra rows are `auto`. Giving only rows without a column template makes a 1-column grid that fills the width.
- Children fill their cell (column width × row height) completely. Children fill cells in document order, left→right, top→bottom (no cell merging).
- When the grid itself is content-sized (no width given and not stretched), `%` tracks are measured as `auto`, and `fr` columns as in CSS: `fr × (largest content width per fr)` — with `1fr 1fr`, both columns get the same width fitted to the widest content. `fr` in rows (`grid-template-rows`) is the content height during measurement.
- Track lists may be quoted: `grid-template-columns:"1fr 2fr 120"`.

### 3.5 Absolute position

`position:absolute` + `left` `top` `right` `bottom`. The reference is **the content area (inside padding) of the immediate parent** (the parent does not need `position:relative`). Giving both `left`+`right` sets the width; both `top`+`bottom` sets the height. Absolutely positioned children are excluded from flow layout and from the parent's size calculation. `position:relative` is parsed but has no effect.

### 3.6 aspect-ratio

`aspect-ratio: 1` (or `16/9`, `1.5`) = width/height. It becomes the largest box of that ratio that fits in the allotted area, centered (can be combined with `grow`/stretch). If only `width` or `height` is given, the other is computed from the ratio during measurement.

### 3.7 overflow and scrolling (scroll panel)

| Value | Behavior |
|---|---|
| `visible` (default) | No clipping |
| `hidden` | Clips children to the rounded border shape |
| `scroll` | Clips, and scrolls along the axis where content overflows |

There is no dedicated scroll panel tag. **Give any box `style="overflow:scroll"`** and that box becomes a scroll panel (`DvStack`, `DvBox`, `DvRepeat`, etc.).

#### 3.7.1 When scrolling happens — remember just this

Scrolling happens **only when the content is larger than the box**. So the scroll box's size (its height, for a vertical list) **must be determined** independently of the content. There are three ways.

| Method | How | Result |
|---|---|---|
| ① Fill the remaining space | `grow="1"` on the scroll box (or the card containing it) | The height left over after everything else on the screen. Follows window resizing — **most common** |
| ② Fixed | `style="height:300; overflow:scroll"` | Always 300 |
| ③ Maximum | `style="max-height:320; overflow:scroll"` | Content height when content is small; scrolls at 320 when large |

For ①, **every parent from the scroll box up to the window must use flex layout** for the remaining space to be passed down.

- Flex layout: `DvPageFrame`, `DvStack`, `DvRow`, `DvCard`, the tab body of `DvTabs`, boxes with `style="display:flex"`.
- **Not** flex: `DvBox`, `DvScreen` (a plain box under another name), boxes without `display`. If such a box sits in between, `grow` has no effect below it and no scrolling happens. Replace it with `DvStack`.
- Start the top of the window with `DvPageFrame` (DvApp page) or `DvStack`. If the top of the page is `DvScreen`, `grow` directly below it does not work.

#### 3.7.2 Common patterns

**A list that fills the rest of the page** (most common) — `grow="1"` on both the card and the list. The card shrinks down to just its title row, and the inner list takes the rest by scrolling (3.7.3).

```xml
<DvPageFrame title="설비 목록">
  <DvRow><DvBadge text="{SelectedText}"/></DvRow>
  <DvCard title="펌프·밸브" grow="1">
    <DvRepeat items="{Items}" gap="xs" grow="1" style="overflow:scroll">
      <DvStatusItem label="{Item.Name}" state="{Item.State}"/>
    </DvRepeat>
  </DvCard>
  <DvCard title="요약"><DvLabel text="{Summary}"/></DvCard>
</DvPageFrame>
```

**A list with a set height** — only one of several blocks inside a card at a fixed height.

```xml
<DvCard title="최근 이벤트">
  <DvStack gap="xs" style="height:240; overflow:scroll">
    <DvRepeat items="{Events}"><DvLabel text="{Item}"/></DvRepeat>
  </DvStack>
</DvCard>
```

**Content-sized, max height when overflowing** — small with a few lines, stops growing when there are many.

```xml
<DvStack gap="xs" style="max-height:320; overflow:scroll">
  <DvRepeat items="{Notes}"><DvLabel text="{Item}"/></DvRepeat>
</DvStack>
```

**Horizontal scroll row** — cards/instruments overflowing horizontally. Give it a height (a horizontal scroll box can also shrink vertically, so without a height it gets flattened when space is short).

```xml
<DvRepeat items="{Pressures}" style="display:flex; flex-direction:row; gap:16; height:164; overflow:scroll">
  <DvGauge value="{Item}" style="width:140; height:140"/>
</DvRepeat>
```

**A list with a fixed side panel** — the row (`DvRow`) fills the remaining space, and only the list inside it scrolls.

```xml
<DvRow grow="1" align="stretch" gap="md">
  <DvStack grow="1" style="overflow:scroll">
    <DvRepeat items="{Items}"><DvLabel text="{Item.Name}"/></DvRepeat>
  </DvStack>
  <DvCard title="상세" style="width:280"><DvLabel text="{Detail}"/></DvCard>
</DvRow>
```

#### 3.7.3 Sizing rules (in detail)

- **Height works as in CSS.** Without `height`, it is the content height (+`padding`), limited by `min-height`/`max-height`.
- **When space is short, the scroll box shrinks.** A scroll box without `flex-shrink` is treated as shrink 1, minimum 0 (the default for other elements is 0, 3.3). Inside a flex parent with a determined height, when siblings take up space, the scroll box shrinks so the rest stays inside the parent.
- **Boxes containing a scroll box shrink too.** If a scroll box is inside a flex box (`DvCard`, `DvStack`, etc.) without `flex-shrink`, then when space is short that box shrinks **as far as the inner scroll box can shrink** (for a card, down to the title row + the scroll box's `min-height`). The same holds through multiple levels of nesting. That is why "a list that fills the rest of the page" above works as is.
  - **Difference from CSS:** In CSS you must give the card `min-height:0` explicitly for it to shrink (a common pitfall: with only `flex: 1`, the card grows to content height and no scrolling happens). Here you do not need to write it. Existing markup that uses `height:0` also still works.
  - When there is space to spare, nothing changes (content size + the `grow` share of the remainder). Boxes that do not contain a scroll box do not shrink, as before.
  - Side-by-side children (a `DvRow` containing a vertical list) are based on the tallest cell — even when the scroll cell shrinks, it does not get shorter than the content of the neighboring cell.
- **Shrinking happens only inside a flex parent.** Inside a default-flow parent (`DvBox`, `DvScreen`, etc., no `display`), even if the parent's height is set, the scroll box takes its content height and overflows outside the parent. Give it `height`/`max-height`, or make the parent a `DvStack`.
- Shrinking never goes below `min-height`/`min-width`. With `flex-shrink:0`, that box (scroll box or containing box) does not shrink.
- If nothing in the parent chain determines a height (all content-sized), the scroll box also takes its content height and **no scrolling happens.**
- `max-height`/`max-width` is applied again to the size grown by `grow`. The space left over this way is not redistributed to other siblings (CSS redistributes — a difference).
- The same goes for shrinking. When one sibling shrinks less because it hits `min-height`/`min-width`, other siblings do not shrink more to make up for it (CSS redistributes — a difference). That share overflows outside the parent, so if several boxes must shrink, lower their `min-*` generously or let only one box shrink.

#### 3.7.4 Interaction and appearance

- Mouse wheel (48 per notch), drag (moving 8 or more cancels the press so the button is not pressed; on release it continues with inertia), draggable scrollbar.
- On an overflowing axis, 12 is reserved for the scrollbar so content does not touch the bar.
- Knobs inside keep the drag for themselves (no scrolling).
- The scroll position is kept across `Invalidate()` and page transitions (`DvApp` keeps page screens).
- For many rows (thousands or more), use `DvDataGrid` instead of `DvRepeat`. `DvRepeat` creates a box for every row (no virtualization).

#### 3.7.5 When scrolling does not work — checklist

1. Does the box to scroll have `style="overflow:scroll"`?
2. Is that box's height determined? One of `grow="1"`, `height`, `max-height` (3.7.1).
3. If `grow="1"`, are **all** parents from that box up to the top of the page flex? If there is a `DvBox`, `DvScreen` or box without `display` in between, replace it with `DvStack`.
4. Does the top of the parent chain receive the window size? On a DvApp page, `DvPageFrame` fills the window.
5. Is a `flex-shrink:0` or a large `min-height` somewhere preventing shrinking?
6. Verify with an off-screen PNG: send `PointerWheel` to the `DvSurface`, redraw, and check whether the image changes (llm-rules chapter 5).

### 3.8 Text wrapping (`white-space`)

Elements with text (`DvLabel`, button text, etc.) split into multiple lines when the text is longer than the width they are placed in, and their height grows by the number of lines (height = line count × font line spacing + `padding`). Text that fits on one line keeps its position and size.

- **Break points**: at spaces first; if there are no spaces (long Korean words, etc.), per character. There is no hyphenation, line-breaking rules (kinsoku), ellipsis (`text-overflow`) or line-count limit.
- **Alignment**: each line is aligned horizontally by `text-align`, and the block of lines as a whole is aligned vertically by `content-align`.
- **Where the wrap width (available width) comes from**
  - Screen root: the screen (cell) width.
  - Vertical layout (`DvStack`, `DvRepeat`, `flex-direction:column`, `DvBox` without `display`): the parent's inner width (excluding `padding` and the scrollbar space). Excludes `flex-wrap:wrap`.
  - Grid: the cell (column) width.
  - Dialog root: window width − 32 (21.2). The dialog width is `min(single-line width, window width − 32)` and respects `width`, `min-width` and `max-width`.
  - An element with `width`: that width (with only `max-width`, up to that width). However, where the parent does not honor that `width` (stretch in vertical layout, grid cells, `grow` in horizontal layout, `position:absolute` with both `left`+`right`), it wraps at the width it is actually placed in.
- **To keep it on one line**, use `white-space:nowrap`. It is inherited, so setting it on a parent makes all text inside single-line (reset with `white-space:normal` inside).
- **Write long text as a single label.** Do not create a separate label per line — when the width changes (window size, theme font size), the engine re-wraps it.

Differences from CSS:

- `\n` breaks the line in both `normal` and `nowrap` (CSS `normal` treats it as a space). Whitespace is not collapsed either — text is shown exactly as written.
- **Children of a horizontal layout (`DvRow`, `flex-direction:row`) receive no wrap width and stay on one line** (CSS shrinks and wraps). To wrap inside a horizontal layout, give that child a `width` or `max-width`. Text inside a child whose width was changed by `grow`/`flex-shrink` re-wraps at that width, but the row height is based on a single line, so it can overflow downward.
- Text boxes with an icon (buttons/chips with `icon`) are always single-line.
- Space left over by `max-*` after `grow`, and the share not shrunk due to `min-*` when shrinking, are not redistributed to other siblings (3.7).

```xml
<DvCard title="안내" style="width:320">
  <DvLabel class="body" text="필터 교체 주기가 지났습니다. 운전을 멈춘 뒤 필터를 교체하고, 교체 후에는 누적 운전 시간을 초기화하세요."/>
  <DvLabel class="caption muted" text="TAG-1001 · 마지막 교체 2026-09-01" style="white-space:nowrap"/>
</DvCard>
```

---

## 4. Styles

### 4.1 Syntax

`style="key: value; key: value"`. Keys are case-insensitive. If the same key appears more than once, the later one wins. Bindings (`{X}`) and tokens (`token(name)`) can be used inside values.

### 4.2 Supported style keys (all other keys are ignored)

| Key | Value | Default / notes |
|---|---|---|
| `background` | Color or `linear-gradient(...)` | None (transparent) |
| `background-color` | Color only (solid-color alias of `background`) | Does not accept gradients. When used together with `background`, the later declaration wins |
| `color` | Color | `#222222` (theme-independent, not inherited) |
| `font-size` | Number | 14 |
| `font-family` | Comma list | First installed font that has all the characters; otherwise per-character fallback. **Inherited by children** |
| `font-weight` | `bold` or a number (600 or more = bold) | Normal |
| `text-align` | `left` `center` `right` | `left` (per line when multi-line) |
| `white-space` | `normal` `nowrap` (lowercase only, anything else = `normal`) | `normal` = wrap to fit width, `nowrap` = break only at `\n` (3.8). **Inherited by children** |
| `content-align` | 9 positions (4.4) | Varies by widget |
| `padding` | 1–4 values (`8`, `8 12`, `4 8 12` = top, left/right, bottom, `4 8 4 8` = top right bottom left) | 0 |
| `padding-top` `padding-right` `padding-bottom` `padding-left` | 1 value — changes only that side. As in CSS, the later declaration wins (`padding:12; padding-left:0` = only left is 0) | — |
| `border` | `width color`, `width solid color`, `width dashed color` | None |
| `border-style` | `solid` `dashed` | `solid` |
| `border-radius` | One number | 0 |
| `box-shadow` | `dx dy blur color` or `none` | None |
| `display` | `flex` `grid` (anything else = default flow) | Default flow |
| `flex-direction` | `row` `column` | `row` (when display:flex) |
| `justify-content` | `start` `center` `end`(`flex-end`) `space-between` | `start` |
| `align-items` | `start`(`flex-start`) `center` `end`(`flex-end`) `stretch` | `stretch` |
| `align-self` | `start`(`flex-start`) `center` `end`(`flex-end`) `stretch` | Parent's `align-items` |
| `flex-grow` | Number | 0 |
| `flex-shrink` | Number | **0** (1 for an `overflow:scroll` box if not specified, 3.7) |
| `flex` | `grow` or `grow shrink` | |
| `flex-wrap` | `wrap` | No wrapping |
| `align-content` | `start` `center` `end` `space-between` `stretch` | `start` (when wrapping) |
| `gap` | One number | 0 |
| `width` `height` `min-width` `max-width` `min-height` `max-height` | Number | Content size |
| `grid-template-columns` `grid-template-rows` | Track list (3.4) | |
| `aspect-ratio` | `1`, `1.5`, `16/9` | |
| `overflow` | `visible` `hidden` `scroll` | `visible` |
| `position` | `absolute` (`relative` has no effect) | Flow layout |
| `left` `top` `right` `bottom` | Number | Only with `position:absolute` |
| `opacity` | 0–1 | 1. The box and all its children are composited as one layer and made translucent (same as CSS). At 0 it is not drawn (its space remains) |
| `transform` | `translate(x[, y])` `translateX(x)` `translateY(y)` `scale(s[, sy])` `scaleX(s)` `scaleY(s)` separated by spaces, or `none` | None. Relative to the box center; regardless of the written order, it is "translate, then scale". Children move along. **Drawing only** — layout and touch area stay unchanged. Other functions such as `rotate` are ignored (warning) |
| `transition` | `property duration [curve] [delay], …` (4.8) | None (changes instantly when the state changes) |

- **Parsed but not applied:** `margin`. Use `gap`/`padding` for spacing.
- Numbers are px. `16`, `16px` and `16dp` are accepted. `%`, `em` and `auto` fail to parse and become **0** (only `font-size` becomes 14). If `padding` contains an unparseable value, the entire `padding` declaration is ignored.
- Borders are the same on all four sides (no per-side keys like `border-top`). Rounded corners are also the same on all four corners.

### 4.3 Value formats

**Color**

| Format | Example |
|---|---|
| `#RGB` | `#fff` |
| `#RRGGBB` | `#2563eb` |
| `#RRGGBBAA` (alpha **last**) | `#00000040` |
| `transparent` | |
| Theme token | `token(primary)` (chapter 5) |

`rgb()`, `rgba()`, `hsl()` and color names (`red`) are not supported.

**Border:** `border: 1 #cbd5e1`, `border: 1 solid token(border)`, `border: 2 dashed #f59e0b`. First value = width, last value = color. You can also set `border-style: dashed` separately.

**Shadow:** `box-shadow: 0 8 24 #00000040` — exactly in `dx dy blur color` order. There is no spread and no multiple shadows. Tokens also work, as in `box-shadow: token(shadow.md)`. `box-shadow: none` removes a shadow set by a class.

**Gradient:** `background: linear-gradient(135deg, #2563eb, #7c3aed)`.
- The angle is `<number>deg` or `to right` `to left` `to top` `to bottom` (case-insensitive). If omitted, top→bottom.
- 2 or more colors. Position values after a color (`#fff 30%`) are ignored and the colors are spread evenly.
- A later solid `background` (or `background-color`) replaces an earlier gradient, and vice versa.

### 4.4 content-align

Sets where the text (and button icon) of `DvLabel`/`DvButton` is placed inside the padding.

9 values (lowercase kebab-case only): `top-left` `top-center` `top-right` `middle-left` `middle-center` `middle-right` `bottom-left` `bottom-center` `bottom-right`. Any other value is ignored after one Trace warning.

| Element | Default |
|---|---|
| `DvButton` with `variant` | `middle-center` (`.btn` class) |
| `DvLabel`, `DvButton` without `variant` | `top-left` |
| `DvButton` with only an icon and no text | Vertically centered |

- It can be given as a style key (`style="content-align:middle-left"`) or as an attribute (`<DvButton content-align="middle-left"/>`), and is bindable (`content-align="{Align}"`, string).
- Priority: class < `content-align` attribute < inline `style`. Within the same declaration list, whichever of `text-align` and `content-align` comes later decides the horizontal position.

### 4.5 Classes and DvStyle

`class="a b"` — with multiple classes, the later class wins.

**Built-in classes**

| Category | Classes | Content |
|---|---|---|
| Typography | `.display` (32 bold) `.heading` (21 bold) `.title` (15 bold) `.body` (14) `.label` (13) `.caption` (11, dimmed color) `.mono` (14) | Font size + text color `token(text)` |
| Text color | `.muted` (`text-muted`) `.subtle` (`text-subtle`) | |
| Surface | `.page` (background gradient + padding 24) `.card` (surface, border, radius 16, shadow, padding) `.card-flat` (card without shadow) `.dialog` (`DvDialogFrame` frame: `surface-overlay`, border, radius 12, shadow, padding 20) | |
| Chip | `.chip` | Small bold text + rounded background |
| Button | `.btn` `.btn-primary` `.btn-secondary` `.btn-danger` `.btn-ghost` `.btn-sm` `.btn-lg` | Added automatically by `DvButton variant`/`size` |
| Latching / segment | `.btn-on` `.segment` `.segment-item` `.segment-item-on` | `.btn-on` when a latching button is on; added automatically to `DvSegment` and its items |
| Checkbox / radio | `.checkbox` `.radio` | Automatic on `DvCheckBox`/`DvRadio` (box/circle in a 34 left margin, height 40 or more) |
| List | `.listbox` `.listbox-item` `.listbox-item-on` | Automatic on `DvListBox` and its rows |
| Expander | `.expander` `.expander-header` `.expander-body` | Automatic on `DvExpander` and its header row / content |
| Tree | `.treeview` `.treeview-item` `.treeview-item-on` | Automatic on `DvTreeView` and its rows |
| Tabs | `.tabs` `.tab-header` `.tab` `.tab-on` `.tab-rule` `.tab-body` | Added automatically to `DvTabs`, the header, the divider and `DvTab` content |
| Icon | `.icon` | Automatic on `DvIcon` (24, `token(text)`) |
| Input | `.input` `.input-readonly` | Automatic on input widgets |

There are also classes for internal use by built-in semantic components (`.stat` `.stat-accent` `.stat-body` `.stat-value` `.status-item` `.switch-row`), but they are not meant to be used directly.

**Defining your own** — place `<DvStyle>` anywhere in the screen markup.

```xml
<DvStyle>
  .dial { width:160; height:160; border-radius:80; background: token(surface-raised) }
  .dial:hover { border: 2 token(primary) }
</DvStyle>
```

- Only the selectors `.name` and `.name:hover` `:press` `:active` (= press) `:disabled` work. Multiple selectors can be listed with commas (`.a, .b { … }`). `.a.b`, tag and descendant selectors are ignored (Trace warning).
- `/* comments */` are allowed.
- Defining a name identical to a built-in class appends to it and overrides it.
- A `DvStyle` inside a `DvComponent` definition is also applied globally (chapter 15). For the same class, the `DvStyle` in the screen markup wins.

### 4.6 State styles

| State | When | How to set |
|---|---|---|
| hover | Pointer is over it | `hover-style` attribute, `.x:hover` |
| press | Being pressed | `press-style`, `.x:press` / `.x:active` |
| disabled | `disabled="true"` | `disabled-style`, `.x:disabled` |

- When several states overlap, only one is used, in the order disabled > press > hover.
- Disabled elements (including those inherited from an ancestor) do not use hover/press looks. If there is a `disabled-style`, that is used; otherwise the default look. There is also no knob face brightening and no grid hover row.
- State styles apply to drawing only. Changing size, position or padding does not change the layout.
- hover/press is visible only when that element is the interaction target (bubbling rules in 6.4).

### 4.7 Priority (low → high)

1. Layout sugar and the attributes `grow`, `width`, `height`, `gap`, `padding`, `align`, `justify`
2. Classes (later class wins)
3. Class states (`.x:hover` etc., when in that state)
4. `content-align` attribute
5. Inline `style`
6. Inline state attributes (`hover-style` etc., when in that state)

Inline `style` is merged (only the given properties change). Inline `style` is stronger than class state styles, so to change a property set inline on hover, use the `hover-style` attribute.

### 4.8 Transitions (`transition`)

Changes values gradually when the state changes (same as CSS). This works both when switching to hover/press/disabled styles and when the element is rebuilt because a class or bound value changed.
```xml
<DvButton text="저장" variant="primary" style="transition: background-color 150ms, transform 120ms ease-out" press-style="transform: scale(0.95)"/>
<DvBox style="opacity:{PanelOpacity}; transition: opacity 300ms">…</DvBox>
<DvBox style="transform: translateX({BallX}); transition: transform 400ms ease-in-out"/>
```
- Properties: `background-color` (also a solid `background`), `color`, `border-color`, `opacity`, `transform`, `all` (all of the above). Gradient backgrounds change instantly.
- Duration: `200ms` or `0.2s`. A second duration is the delay. Curves: `linear` `ease` (default) `ease-in` `ease-out` `ease-in-out`. Multiple entries are separated by commas. `none` = no transition.
- Which one is used: the `transition` in the style of the state **after** the change. If written in the base `style`, hover/press styles inherit it too (state styles are layered over the base).
- If it changes again mid-transition, it restarts from the currently visible value (no jump). Nothing is animated on the very first draw.
- Frames are drawn only while animating (about 60fps) and stop when done. Malformed entries are skipped (Trace warning).

---
## 5. Theme tokens

Use `token(이름)` to read a theme value. `var(--이름)` means the same thing, but uses dashes instead of dots (`var(--spacing-md)` = `token(spacing.md)`). Names are case-insensitive. **An unknown token becomes magenta `#FF00FF`**.

The themes are `DvTheme.Dark` and `DvTheme.Light`. To change only some values: `DvTheme.Dark.With(new Dictionary<string,string>{ ["primary"] = "#0ea5e9" })`.

| Category | Tokens |
|---|---|
| Background / surface | `background` `background-alt` `surface` `surface-raised` `surface-overlay` |
| Border | `border` `border-strong` |
| Text | `text` `text-muted` `text-subtle` |
| Primary color | `primary` `primary-hover` `primary-press` `primary-fg` `focus` |
| Tone (`neutral` `info` `success` `warning` `danger`) | `tone-{톤}` `tone-{톤}-fg` `tone-{톤}-soft` |
| Equipment state (`run` `stop` `fault` `warn` `manual` `off`) | `state-{상태}` `state-{상태}-fg` `state-{상태}-soft` |
| Alarm | `alarm-critical` `alarm-critical-fg` `alarm-major` `alarm-major-fg` `alarm-minor` `alarm-minor-fg` `comm-alarm` |
| Spacing | `spacing.xs` 4 · `spacing.sm` 8 · `spacing.md` 12 · `spacing.lg` 16 · `spacing.xl` 24 · `spacing.2xl` 32 |
| Corner radius | `radius.sm` 6 · `radius.md` 10 · `radius.lg` 16 · `radius.full` 999 |
| Shadow | `shadow.sm` `shadow.md` `shadow.lg` (a `dx dy blur 색` string) |
| Font size | `font.display` 32 · `font.heading` 21 · `font.title` 15 · `font.body` 14 · `font.label` 13 · `font.caption` 11 · `font.mono` 14 |
| Instrument | `gauge-track` `gauge-needle` `gauge-needle-tip` `gauge-tick` |
| Tank | `tank-empty-top` `tank-empty-bottom` `tank-frame` `tank-chip-bg` `tank-chip-fg` |
| Knob | `knob-face` `knob-fill` `knob-indicator` |
| Toggle | `toggle-on` `toggle-off` `toggle-knob` |
| Trend | `trend-grid` |
| Scrollbar | `scrollbar-thumb` `scrollbar-track` |
| Dialog | `scrim` (the translucent layer covering the whole window just below the topmost dialog, chapter 21) |
| Data grid | `grid-header` `grid-header-fg` `grid-row` `grid-row-alt` `grid-row-hover` `grid-row-selected` `grid-line` `grid-summary` `grid-summary-fg` |
| Virtual keyboard | `kbd-bg` `kbd-key` `kbd-key-fg` `kbd-key-special` `kbd-key-special-fg` |

Tokens kept for backward compatibility (`bg` `bg2` `surface-alt` `muted` `border-warn` `success` `success-bg` `success-border` `warn-text` `warn-bg` `shadow`) still exist, but use the list above for new screens.

Example: `style="background: token(surface); border: 1 token(border); padding: token(spacing.lg); border-radius: token(radius.md)"`

---

## 6. Data binding and interaction

### 6.1 Binding syntax

| Syntax | Meaning |
|---|---|
| `text="{Temperature}"` | Value of a public VM property (one-way) |
| `text="{Motor.Speed}"` | Dot path. Empty if an intermediate value is null |
| `{Data.X}` | Same as `{X}` (the `Data.` prefix is ignored) |
| `text="펌프 #{No} — {State}"` | Interpolation inside text. Braces are always interpreted as bindings |
| `style="color:{StatusColor}"` | Interpolation inside a style |
| `style="color: token(tone-{Tone}-fg)"` | The binding is resolved first, then the token |
| `value="{Bind Data.AutoMode}"` | Two-way (writes user input to the VM) |
| `text="@pump"` | Localized string (chapter 17) |

- Bindings read **public instance properties** only (not fields). A missing path becomes null and shows as blank.
- If the whole attribute value is a single binding (`"{X}"`), the value's type is preserved (number, bool, list). Mixed text becomes a string.
- Bindings are read at layout time. After changing the VM from a timer etc., `Invalidate()` is required (`llm-rules.md` chapter 6).

### 6.2 Where two-way binding is accepted

`value="{Bind …}"` is meaningful only on: `DvToggle`, `DvCheckBox`, `DvRadio`, `DvSwitch`, `DvKnob`, `DvSlider`, `DvInput`, `DvSelect`, `DvListBox` (including `selected-items`), `DvExpander` (`expanded`), `DvTreeView`, `DvSegment`, `DvTabs`, `DvButton` (latching), `DvDateTimeInput`, `DvColorInput`, and the `selection` of `DvDataGrid`. On any other attribute, `{Bind X}` is read-only, like `{X}`. Giving an input widget a one-way `{X}` or a literal makes it **read-only**.

### 6.3 Attributes that are not bound

Most attributes are bound. The following are **not bound and are used literally**.

| Element | Non-bound attributes |
|---|---|
| All elements | `onclick` `onbeforecommit` (method names), `id` (control name, chapter 20), `vm` on the root |
| `DvAlarmBanner` `DvAlarmList` | `onack` |
| `DvDataGrid` | `row-key` `row-class` `onrowclick` (paths / names), `selection` (the binding expression itself) |
| `DvColumn` | `field` `cell-type` `align` `format` `icon` `variant` `onclick` `onbeforecommit` `enabled` `cell-class` `sort` |
| `DvSelect`, select cell | `item-value` `item-text` `item-icon` (paths inside the item) |
| `DvSummary` `DvSummaryRow` | `field` `fn` `format` |
| `DvComponent`/`DvParam` | `name` `default` |

`header`·`text`·`unit` of `DvColumn`, `DvColumnGroup header`, and `DvSummaryRow title` support only text interpolation (`"{X}개"`).

### 6.4 Click (`onclick`)

- `onclick="Method"` → the VM's `public void Method()`.
- Write only the method name — there is no call form (`onclick="Method(item)"` never runs); arguments such as the `DvRepeat` item are passed automatically. A name that is not a public instance method of the VM (including base classes) is build error DVML018 (20.7). The same applies to `onbeforecommit`, `onack`, and `onrowclick`.
- Inside a `DvRepeat` item, `Method(항목형 item)` is called if it exists, otherwise `Method()`.
- **Bubbling:** starting from the deepest element at the pressed point and moving up, the first interactive element encountered receives the click. Interactive element = has `onclick`, or has a hover/press style (including via class), or is an input, toggle, knob, grid, or ACK-capable alarm widget. If that element has no `onclick`, nothing happens (it does not bubble further).
- If you press inside a scroll box (including a grid) and drag 8 or more, it is treated as a scroll, not a click.
- A click happens when the pressed element and the released element are the same.
- **Long press (`hold`):** with `hold="1500"`, you must hold for 1.5 seconds before `onclick` fires — protection against accidental operation of dangerous commands such as start, stop, or reset. It fires once **before release**, the moment the time is reached; a short press and release does nothing. While pressing, a band (translucent text color) filling from the left over the element shows progress. Moving outside the element while pressed, or being judged a scroll, cancels it. Bindable (`hold="{HoldMs}"`). Applies only to ordinary elements receiving `onclick` (buttons, boxes); it has no effect on toggles, inputs, selects, pickers, grids, alarms, or knobs.
- An element with `disabled="true"` and everything inside it is not clicked, edited, or committed (e.g. lock a whole area with `<DvStack disabled="{Locked}">`). Knob rotation, input editing, select/date/color popups, grid rows/edit cells/header sorting, alarm ACK, and `DvRepeat` item `onclick` are all blocked. Scrolling (wheel / drag) still works.
- A press on a disabled area is swallowed there. Because bubbling stops at the disabled element, the `onclick` of an enabled ancestor outside is not called either (same as a disabled button in HTML).
- Invisible regions are not clicked or hovered: parts clipped outside a parent with `overflow:hidden`, children pushed outside the viewport of a scroll box (including grids), elements with false `visible`, and elements covered by an open popup or the virtual keyboard. For overlapping elements (`position:absolute`), the one drawn on top receives the input. Conversely, visible regions do receive it: a child that sticks out of an `overflow:visible` (default) parent and is visible (e.g. a badge or close button floated on a card corner with `position:absolute`) is also clicked and hovered.
- A component instance's `onclick` is passed to the template root (chapter 7). An `onclick` inside a component template also calls a method on the screen VM. If that method has **one parameter** (`Reset(BlenderVm unit)`), it receives the instance's parameter value whose type matches (the first in parameter declaration order; if none, `Reset()`) — so when using the same component several times you can tell which instance's button was pressed.
- How to subscribe to the `Click` event from code (`id` handle) is in chapter 20. If `onclick` is present, `Click` subscribers are called after `onclick`.
- If a handler throws, only that handler stops partway and the screen keeps working. The exception does not escape to the host; it is reported as a Trace error and via the `HandlerError` event (6.7). The same applies to `onrowclick` (chapter 11) and `onack` (chapter 13).

### 6.5 Commit confirmation (`onbeforecommit`)

Right before writing a value to the VM, a VM method confirms it. The value is written only when it returns `true`.

| Widget | Signature |
|---|---|
| `DvToggle` (including inside `DvSwitch`), `DvCheckBox`, latching `DvButton` | `bool M(bool proposed)` |
| `DvRadio` | `bool M(형식 proposed)` — `option` converted to the bound property's type (if none, `bool M(object)`); for a `group` set, `bool M(string)` |
| `DvKnob`, `DvSlider` | `bool M(double proposed)` |
| `DvInput` numeric (`keyboard-type="numeric"`) | `bool M(double proposed)` (double even if the property is int) |
| `DvInput` text / password | `bool M(string proposed)` |
| `DvSelect` `DvSegment` `DvTabs` `DvDateTimeInput` `DvColorInput` | `bool M(T value)` (T = actual type of the value to write) or `bool M(object value)` |
| `DvColumn cell-type="check"` | `bool M(Row row, bool value)` or `bool M(bool value)` |
| `DvColumn` edit cell (`input` `select` `datetime` `color`) | `bool M(Row row, T value)` or `bool M(T value)` (T = the property's underlying type; `int` for `int?`) |

- If the method is not on the VM (a misspelled name is build error DVML018 in a dvml with `vm`) or the signature differs (including a non-`bool` return such as `void M(bool)`), **no widget or cell writes** (rejected). A Trace warning is logged once per VM type and method name.
- If the method throws, the write is also rejected, a Trace error is logged, and it is reported via `HandlerError` (6.7; the exception is not propagated to the app).
- Without `onbeforecommit`, the value is written without confirmation.
- Even after passing confirmation, **if the VM property's setter throws** (including type conversion failure), it is treated as a write failure and handled exactly like a rejection. The error text for `DvInput` (including grid input cells) is "Could not save the value" (`dv.input.write-failed`, chapter 17); the exception message is not shown on screen.

- **The rejection text is the same for every rejection** ("This value is not allowed", `dv.input.rejected`; Korean or your own text with `DvStrings`, chapter 17). A confirmation method cannot return its own reason. To show a specific reason, set a VM property inside the method, bind a label to it, and call `App.Invalidate()` in the method — a rejected commit does not rebuild the screen by itself.

When rejected (including the cases above): `DvInput` stays in edit mode and shows the error text, and picker popups stay open. A `DvSelect` dropdown closes and the value is unchanged (a grid `select` cell stays open). Toggles, knobs, and `check` cells keep their value, and grid row selection (`selection="{Bind …}"`) keeps the previous selection.

### 6.6 VM constants (`Styles`, `Values`)

Instead of writing keyword values as strings directly in the VM, you can use constants. All are the same lowercase strings as in markup.

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
| `Values.GridAlign` | `left` `center` `right` |
| `Values.GridSort` | `asc` `desc` |
| `Values.GridFn` | `sum` `avg` `min` `max` `count` |
| `Values.DateTimeMode` | `date` `time` `datetime` |

### 6.7 App code exceptions (`HandlerError`)

Even if VM code called by the screen (handler methods, property setters that write values, and property getters / `ToString()` / `items` enumeration that read values while building and drawing the screen) throws, **the exception does not escape input processing, layout, drawing, or the host loop.** The library catches it, logs a Trace error once (even with no subscribers), and reports it via the `HandlerError` event.

```csharp
app.HandlerError += (_, e) =>                  // DvApp · ViewWindow · ViewControl · DvSurface 모두 같은 이름
    Log.Error(e.Exception, $"{e.Kind} {e.Member} ({e.Tag})");
```

| Property | Content |
|---|---|
| `Exception` | The exception thrown by VM code (for reflection's `TargetInvocationException`, the unwrapped inner exception) |
| `Kind` | One of: `hotreload` (invalid dvml during hot reload, 16.7), `onclick` `Click` `onrowclick` `onack` `onbeforecommit` `setter` (during input processing), `getter` `format` `enumerate` `compare` (reading values during layout / drawing), `attach` (handle VM attach, chapter 20), `shown` `hidden` (page `OnShown`/`OnHidden` exceptions, chapter 16), `closed` (dialog `OnClosed` exception), `async` (exceptions from `async void` handlers / `await` continuations, chapter 21) |
| `Member` | Method name or property path (for grid value reads, the column's `field`) |
| `Tag` | Element tag (`DvButton`, `DvInput`, `DvDataGrid` …) |

- Result: the handler is treated as ended at that point; a value write failure (`setter`) or confirmation exception (`onbeforecommit`) is the same as a rejection (6.5).
- A value read failure (`getter` property getter, `format` the value's `ToString()` / formatting, `enumerate` list enumeration of `DvRepeat` / `DvDataGrid` / `DvSelect` / alarms / trends, `compare` `CompareTo` in grid sorting) treats that value as **empty (null)** and keeps drawing. Text becomes an empty string, `visible` becomes hidden, grid cells are blank, sort keys get the null rank (first in ascending order), and summaries aggregate without that value. A list whose enumeration threw is an **empty list** (the leading part is not shown either). A comparison exception is treated as equal values (the order of that column is undefined).
- Paths read every frame, such as grids, do not flood. The same (`Kind`, `Member`, `Tag`) is reported only once until the screen is rebuilt with `Invalidate()`.
- With `DvApp`, exceptions from area, page, and dialog screens, page transition notifications (`shown`/`hidden`, `Member` = page class name, `Tag` = none), dialog `OnClosed` (`closed`, `Member` = dialog class name), and `async void` handlers (`async`, `Member` = none) are all collected into `DvApp.HandlerError`. Subscribing to that one is enough per app. The `HandlerError` of `ViewWindow`·`ViewControl` only forwards it, so subscribing to both delivers the same exception twice.
- Exceptions thrown inside a subscriber are swallowed and only logged to Trace.
- `attach`: an exception thrown by `Attach` of a handle VM (`IDvControlHost`) (including the generator-produced `OnLoaded()`) while building the screen. `Member` = VM type name, `Tag` = none (null). Since subscription is not yet possible inside the screen constructor, Trace is logged immediately and `HandlerError` is reported once on that screen's **first layout** (first frame). So subscribing after creating the window but before the first frame receives it. Handles attached before the throw keep working.
- Exceptions from methods the app code calls directly propagate to that call as is. When drawing with `DvRenderer` (no screen to report to), value read exceptions are only logged as Trace errors.

---

## 7. Semantic components

Built-in components whose appearance is defined by the theme. You rarely need `style`.

| Tag | Attributes (default) | Appearance |
|---|---|---|
| `DvPageFrame` | `title` `subtitle` | Page background gradient + padding 24 + header. Children stack vertically (gap 16). No header without `title` |
| `DvDialogFrame` | `title` `subtitle` | Dialog frame (`.dialog`). Children stack vertically (gap 12). Used as the root of a dialog dvml (chapter 21) |
| `DvCard` | `title` `badge` | Card surface. Children stack vertically (gap 12, horizontal stretch). Header only when `title` is set; `badge` is a chip on the right of the header |
| `DvBadge` | `text` `tone`(neutral) | Small chip |
| `DvStat` | `label` `value` `unit` `delta` `tone`(info) | KPI card with an accent band on top. `delta` is a tone-colored chip |
| `DvStatusItem` | `label` `state`(off) `status` `blink`(false) | One line of lamp + name + status chip |
| `DvSwitch` | `label` `value` (two-way; omitted + `id` = the handle owns it) | One line of name + toggle. The press target is the toggle |

- `tone` = `neutral` `info` `success` `warning` `danger`, `state` = `run` `stop` `fault` `warn` `manual` `off`. Other values appear magenta.
- All attributes are bound (`tone="{StatusTone}"`, `value="{Bind Data.Auto}"`).

**What you can give an instance** (common to built-in and user components): component parameters, plus `style` (merged into the template root's style), `class` (appended after the template root's classes), `grow`, `width`, `height`, `visible`, `onclick`, `disabled`. Other attributes (`hover-style`, `content-align`, etc.) are ignored.

- `onclick` becomes the click handler of the template root box. The method is looked up on the screen VM, and if the instance is inside `DvRepeat`, `M(항목)` takes precedence (same as ordinary `onclick`).
- `disabled` is evaluated against the outer VM and applied to the template root box. Disabled state is inherited downward, so widgets inside the template (e.g. the toggle in `DvSwitch`) are all disabled too.
- Even if the template root already has `onclick`·`disabled`, **the instance wins** (an explicit value given from outside). If a parameter with the same name exists, it is not forwarded and is used as the parameter.

```xml
<DvCard title="취수 펌프 #1" onclick="OpenPump"> … </DvCard>
```

---

## 8. Basic widgets

| Tag | Attributes (default) | Default size |
|---|---|---|
| `DvBox` | (common attributes only) | Content size |
| `DvLabel` | `text` or inner text | Text size + padding |
| `DvButton` | `text` or inner text, `icon`, `variant` `size`, `onclick`, `disabled`, `hold`, `value` (latching) `onbeforecommit` | Text (+icon) size + padding |
| `DvIcon` | `icon` | `font-size` (24) square + padding |
| `DvLamp` | `on` `blink`(false) `blink-interval`(500 ms) `color` `state` | 24×24 |
| `DvToggle` | `value` (two-way) `onbeforecommit` `disabled` | 44×24 |
| `DvCheckBox` | `text` `value` (two-way) `onbeforecommit` `disabled` | Box 24 + gap 10 + text, height 40 or more |
| `DvRadio` | `text` `value` (two-way, group value) `option` `group` `onbeforecommit` `disabled` | Circle 24 + gap 10 + text, height 40 or more |
| `DvSegment` | `value` (two-way) `readonly` `disabled` `onbeforecommit` + `DvOption` children | Sum of item text sizes + padding |
| `DvTabs` | `value` (two-way, optional) `onbeforecommit` + `DvTab` children (`value` `text` `disabled`) | Header + content of the selected tab |
| `DvExpander` | `header` `expanded` (two-way, optional) `disabled` + content children | Header row 44 (+ content when expanded) |
| `DvImage` | `src` `fit`(contain) | 100×100 |
| `DvStepBar` | `steps` `current`(0) | 96 width per step (min 200)×64 |

**DvLabel** — text. Wraps if longer than the width it is given (3.8; force a single line with `white-space:nowrap`). The default text color is `#222222`, so give a typography class (`class="body"` etc.) or `color`. Align the text with `content-align`/`text-align`.

**DvButton**
- `variant` = `primary` `secondary` `danger` `ghost` → themed button look (`.btn .btn-{variant}`), including hover/press/disabled looks. `size` = `sm` `lg` (only when `variant` is set).
- Without `variant`, it is an unstyled text box (for custom styling).
- `icon="fa-floppy-disk"` = icon to the left of the text (gap 8). Icon size = `font-size`, color = `color`. An icon alone without text is fine.
- `variant`·`size` are bound (`variant="{Kind}"` — same look as the literal). If the bound value is null or empty, it is the same as having no `variant`. Changes are applied on `Invalidate()`.

**DvIcon** — `<DvIcon icon="fa-triangle-exclamation" style="font-size:32; color:token(tone-warning)"/>`. Size = `font-size` (default 24), color = `color` (default `token(text)`). Icon names are in chapter 18.

**DvLamp**
- On when `on="{Running}"` is true. With `blink="true"`, it blinks while on (every `blink-interval` ms; all blinking on screen shares the same phase). `blink-when` is another name for `blink`.
- On-color priority: `color` (`#RRGGBB` or `token(이름)`, bindable) > `state` (`state-{값}` token) > `color` in `style`. The off color is a fixed dark gray. `blink-interval` is also bound.
- **Multi-state lamp (equipment state number → color / text / blinking):** there is no separate widget; the VM converts the state into a `state` key, text, and blinking and binds them (state rules are app data that differ per equipment). Lamp + name + status chip is drawn by `DvStatusItem`. For a color not in the theme, use `DvLamp color="{PumpColor}"`.
```xml
<DvStatusItem label="2호 펌프" state="{Pump2.StateKey}" status="{Pump2.StateText}" blink="{Pump2.IsFault}"/>
```
```csharp
public int State { get; set; }   // 설비가 보내는 상태 번호
public string StateKey => State switch { 1 => "run", 2 => "fault", 3 => "manual", 4 => "warn", _ => "stop" };   // state-* 토큰
public string StateText => State switch { 1 => "운전", 2 => "고장", 3 => "수동", 4 => "주의", _ => "정지" };
public bool IsFault => State == 2;
```

**Latching button (DvButton `value`)** — `<DvButton text="자동" value="{Bind AutoMode}"/>`. Each press flips the bool value and writes it to the VM; when on, it is drawn in the pressed look (`.btn-on` — `primary` background). Without `variant`, the default look is `secondary`. One-way (`value="{AutoMode}"`) only shows the on state and does not write. It can be rejected with `onbeforecommit` = `bool M(bool)`, and `onclick`, if present, is called after the value is written. Combined with `hold`, it changes only on a long press. For a set of independently toggled buttons, place latching buttons side by side in a `DvRow`.

**DvSegment** — pick one from a single row of joined buttons (a radio button group).
```xml
<DvSegment value="{Bind Mode}">
  <DvOption value="auto" text="자동"/>
  <DvOption value="manual" text="수동"/>
  <DvOption value="stop" text="정지"/>
</DvSegment>
```
- Writes the pressed item's `value` to the VM (value type conversion and `onbeforecommit` are the same as `DvSelect` — 10.2). The selected item has a `primary` background (`.segment-item-on`).
- With one-way `value`, `readonly`, or `disabled`, it only displays. Items are created by the library (`.segment-item`) and do not wrap.

**DvTabs · DvTab** — switch between content groups with tabs within one page (different from page transition `App.Show` — same VM).
```xml
<DvTabs value="{Bind Tab}" grow="1">
  <DvTab value="trend" text="트렌드"> … </DvTab>
  <DvTab value="alarm" text="알람"> … </DvTab>
  <DvTab value="setting" text="설정" disabled="{NotAdmin}"> … </DvTab>
</DvTabs>
```
- Builds a tab header row and divider on top, and below it **only the selected tab's content** (other tabs are not built). Tab content stacks vertically and fills the remaining height (`.tab-body`).
- Pressing a header writes that tab's `value` to the VM (value type conversion and `onbeforecommit` are the same as `DvSelect`). If no tab matches the VM value, the first tab is shown.
- If `value` is **omitted**, the screen remembers the selection (starting at the first tab, kept across redraws). Bind `value` if code needs to change tabs or read the selection.
- When switching tabs, the scroll position of the tab being left is not kept. A value being edited is committed first when a header is pressed.
- **Showing only one without headers (switching panel)** has no dedicated widget — do it with a bool computed property on the VM and `visible`: `<DvBox visible="{IsTrend}">…</DvBox><DvBox visible="{IsAlarm}">…</DvBox>` (`public bool IsTrend => Mode == "trend";`).

**DvExpander** — a panel whose content expands and collapses when the header row is pressed (the Expander of WPF / WinUI). Used to split a settings screen into sections by stacking several vertically.
```xml
<DvExpander header="고급 설정" expanded="{Bind ShowAdvanced}">
  <DvCheckBox text="자동 재시작" value="{Bind AutoRestart}"/>
  <DvSlider value="{Bind Valve}" unit="%" show-value="true"/>
</DvExpander>
```
- Header row = ▶ (collapsed) / ▼ (expanded) icon + `header` text (bindable), height 44. The whole row is the press target. When collapsed it takes **only the header row height** and the content is not built (same as tabs — `id` handles in collapsed content have empty `Bounds`). Content stacks vertically (`.expander-body`).
- Expanded state: with `expanded="{Bind X}"` (bool), the VM owns it — pressing flips and writes it. **If omitted, the screen remembers it** (initially collapsed, kept across redraws). A one-way or fixed value (`expanded="true"`) locks it in that state and pressing does not change it. To control it from code, use the `id` handle's `IsExpanded`·`ExpandedChanged` (20.9).
- When expanding and collapsing, the height changes gradually over 0.2 seconds (ease-out) and the content below is pushed along (same when changed via VM or handle; the first screen does not animate). While collapsing, the content remains until the end and is then removed. Appearance: `.expander` (surface, border, rounded corners), `.expander-header`, `.expander-body`.
- **A group where only one is open (accordion)** has no dedicated widget — put `DvExpander` inside `DvRepeat`, and have the item's `Open` setter close the rest of the same list.
```xml
<DvRepeat items="{Sections}" gap="sm">
  <DvExpander header="{Item.Title}" expanded="{Bind Item.Open}"><DvLabel class="body" text="{Item.Body}"/></DvExpander>
</DvRepeat>
```
```csharp
public sealed class Section(string title, List<Section> all)
{
    private bool _open;
    public string Title { get; } = title;
    public bool Open
    {
        get => _open;
        set { _open = value; if (value) foreach (var x in all.Where(x => x != this)) x._open = false; }
    }
}
```

**DvToggle** — `value="{Bind X}"` (bool). Pressing inverts and writes it. One-way only displays. Colors are the `toggle-on` `toggle-off` `toggle-knob` tokens. When disabled it is translucent; when read-only (one-way or fixed value, or neither `value` nor a handle) a dashed border (`border-strong`) is drawn around the track to show it cannot be pressed.

**DvCheckBox** — box + text. `<DvCheckBox text="알람 소리" value="{Bind AlarmSound}"/>`
- It toggles when **the whole element (including the text)** is pressed, not just the box (touch). When stacked vertically, the full row width is the press target.
- Value, writing, and `onbeforecommit` (`bool M(bool)`) are the same as `DvToggle`. A one-way or fixed value only displays. With only `id` and no `value`, the handle holds the value (20.9).
- Appearance is `.checkbox` (the box is drawn in the 34 left padding area, `min-height:40`). On = `primary` fill + ✓, off = `surface` + `border-strong` border, disabled = translucent + dimmed text. There is no three-state (middle bar).

**DvRadio** — circle + text. Among buttons tied to the same value, only one is on (same idea as HTML name/value).
```xml
<DvRadio text="자동" value="{Bind Mode}" option="Auto"/>
<DvRadio text="수동" value="{Bind Mode}" option="Manual"/>
<DvRadio text="정지" value="{Bind Mode}" option="Stop"/>
```
- **On = the group value equals this button's `option`.** Pressing converts `option` to the property type (enum, number, text) and writes it (same conversion and `onbeforecommit` as `DvSelect` — 10.2). Pressing an on button again does not turn it off.
- **The group is determined by value, not by placement.** Buttons using the same `value="{Bind Mode}"` form one group, so they may be scattered across multiple rows or cards.
- **To use without binding, use `group="이름"`** — omit `value` and give the same `group` name, and the screen remembers the chosen `option` (kept across redraws; initially nothing is chosen). To read it from code, use an `id` handle (20.9) or bind `value`. If `value`·`group`·`id` are all absent, it only displays.
- A one-way or fixed `value` only displays. Appearance is `.radio` (circle on the left); on = `primary` border + center dot.
- A list-style choice where each radio is placed individually. For a single row of joined buttons use `DvSegment`; for a dropdown list use `DvSelect`.

**DvStepBar** — shows which step is current in a process / batch sequence (display only; pressing does nothing).
```xml
<DvStepBar steps="{BatchSteps}" current="{BatchStep}"/>
<DvStepBar steps="세척, 헹굼, 건조" current="2"/>
```
- `steps` = step names. A list binding (each item's `ToString()`) or comma-separated text (leading/trailing spaces ignored).
- `current` = number of the step in progress, counted **from 1** (to match PLC step numbers). `0` = not started (all waiting); greater than the count = all done. Earlier steps are done (✓), later steps are waiting.
- Appearance: equal-width cell per step, a circle in the middle (diameter 28) + name below (ellipsis if longer than the cell). Done = `primary` fill + ✓, in progress = `primary` border + bold number and name, waiting = `border-strong` border + dimmed number and name. The lines between circles are `primary` only for passed segments.

**DvImage** — `src` = file path (bindable). `fit` = `contain` (keep ratio, fit inside) `cover` (keep ratio, fill and crop) `fill` (stretch). Case is ignored. If the file is missing or unreadable (permissions, locked, format error), it is an empty box (read errors are Trace warnings). A path once read is decoded to pixels and cached (the same path is not re-read even if the file changes), and a failed path is retried after about 2 seconds (files created later are also shown). The cache is shared by the whole app and, when it exceeds **about 128MB of decoded pixels or 256 images**, evicts the least recently used images first — even if paths keep changing on a 24-hour screen (snapshots, equipment photos), memory does not accumulate. Images currently visible (drawn within about the last 2 seconds) are not evicted, and reusing an evicted path re-reads the file.

---

## 9. Instrument widgets

| Tag | Attributes (default) | Default size |
|---|---|---|
| `DvGauge` | `value` `min`(0) `max`(100) `alarm-zones` `ticks` `minor-ticks` `show-tick-labels`(true) | 120×120 |
| `DvRing` | `value` `min`(0) `max`(100) `tone`(info) `alarm-zones` `unit` `label` `sweep`(270) `thickness` | 120×120 |
| `DvProgress` | `value` `min`(0) `max`(100) `direction`(right) `tone`(info) `alarm-zones` `show-value`(false) `unit` | 200×12 (vertical 12×120) |
| `DvTank` | `value` `min`(0) `max`(100) `unit` `alarm-zones` `show-zone-marks`(true) | 80×160 |
| `DvKnob` | `value`(two-way) `min`(0) `max`(100) `step`(1) `unit` `disabled` `onbeforecommit` `input-mode`(drag) | 96×96 |
| `DvSlider` | `value`(two-way) `min`(0) `max`(100) `step`(1) `direction`(right) `show-value`(false) `unit` `disabled` `onbeforecommit` | 200×40 (vertical 40×200), +24 with `show-value` (vertical +56) |
| `DvSparkline` | `values` `y-min` `y-max` `area`(false) `tone`(info) | 120×32 |
| `DvBarChart` | `items` `item-value` `item-text` `y-min` `y-max` `show-value`(false) `tone`(info) | 320×200 |
| `DvPieChart` | `items` `item-value` `item-text` `inner-radius`(0) `show-value`(false) | 320×180 |

- **Value text:** integers as integers, otherwise one decimal place (gauge, ring, progress bar, tank).
- **Size scaling:** the internals of gauges, rings, knobs, and tanks (arc thickness, needle, value text, chip, zone marks) scale with the widget size (default size = 1×; gauges, rings, and knobs use the shorter side, tanks use the smaller of width/80 and height/160). Proportions stay correct even when filling a cell with `grow="1"` or `align="stretch"`.
- **DvGauge:** 270° needle instrument. `ticks` = major tick **interval** (in value units, e.g. `20` gives 0·20·40…), `minor-ticks` = minor tick interval (drawn only when `ticks` is set). `show-tick-labels="false"` hides tick numbers. The value text and needle tip use the current zone color. `unit`·`tone` are not used.
- **DvRing:** thick arc fill. `sweep` = arc angle (1–360; 360 = a full circle starting at 12 o'clock), `thickness` = arc thickness (omitted = 11% of size), value + `unit` in the center, `label` below. Fill color = current zone color, otherwise `tone-{tone}`.
- **DvProgress:** `direction` = `right` `left` `up` `down`. `show-value="true"` shows the value (+`unit`) in the center (only horizontal and height 14 or more). Fill color = current zone color or `tone-{tone}`. Corners can be changed with `border-radius` (default pill shape).
- **DvTank:** cylindrical vessel. Value chip in the center (+`unit`), liquid color = current zone color. `show-zone-marks="false"` keeps the zone colors and hides only the boundary ticks and numbers on the right.
- **DvKnob:** drag to rotate; written once on release (`onbeforecommit` = `bool M(double)`). Rotation is relative to the grab point. `unit` is bound. Choose the input method with `input-mode`: `drag` (default, drag to rotate), `tap` (tap opens a numeric keypad — no rotation), `both` (drag rotates; a tap without movement opens the keypad). The keypad is a built-in number box (same look as 21.5 `InputNumber`) showing the current value, `min`–`max`, and `unit`; a confirmed value goes through the same `step` rounding → range → `onbeforecommit` as dragging before being written. Cancel leaves it unchanged. The keypad appears only on `DvApp` screens (pages, bars, dialogs) — on hosts using `DvSurface` directly, taps are ignored (Trace warning). A tap on a `tap`·`both` knob opens the keypad instead of `onclick`. Read-only (one-way `value`) and `disabled` knobs do not open the keypad. An unknown `input-mode` value logs a warning and falls back to `drag`.
- **DvSlider:** track + handle (diameter 24). **Pressing anywhere on the track moves the value there**, it follows while dragging, and it is written **once on release** (same commit as the knob — `step` rounding → `min`–`max` → `onbeforecommit` = `bool M(double)`). A press and release without dragging also writes the value at that spot. `direction` = the side where the value increases (same values as `DvProgress`: `right` default, `left` `up` `down` — vertical for `up`·`down`). `show-value="true"` shows the value above the handle (to the right when vertical) (+`unit`, decimal places = those of `step`). Colors: track `gauge-track`, fill and handle border `knob-fill`. There are no ticks. As with the knob, a drag started on the slider does not scroll. One-way or `disabled` sliders do not move. An unknown `direction` logs a warning and falls back to `right`.
- **Charts (DvSparkline · DvBarChart · DvPieChart):** display-only widgets that draw one series. For values flowing over time, use `DvTrend` (chapter 12).
  - **DvSparkline** — a small line with no axes or ticks (trend in a table cell or card header). `values` = a number list binding or `"3,5,4,8"` (unreadable values become 0). A dot on the last point; `area="true"` adds a translucent fill under the line. Range is `y-min`·`y-max`; if omitted, data min to max. Color = `tone-{tone}`.
  - **DvBarChart** — vertical bars. For each item in `items`, `item-value` (number path) and `item-text` (name path, below the bar — ellipsis if longer than the cell). Range is `y-min`·`y-max`; if omitted, includes 0 and the data (negative values go below the baseline). `show-value="true"` shows the value above the bar (integers as integers, otherwise one decimal place). Color = `tone-{tone}`.
  - **DvPieChart** — circle + legend on the right (color dot, name, percentage; value with `show-value="true"`). Slices go clockwise from 12 o'clock, colored with the trend default colors in order. Values of 0 or less are not drawn. `inner-radius` = center hole ratio (0–0.9, e.g. `0.55` → donut); with `show-value` on a donut, the total is shown in the center. If there is no room for the legend (width − height ≥ 100), only the circle is drawn.
  - Values are read at layout time — after changing a VM list, call `Invalidate()` (automatic inside a click handler). Pressing does nothing.

### alarm-zones

```xml
<DvTank value="{Level}" alarm-zones="0:70:#3b82f6 70:88:#f59e0b 88:100:#ef4444"/>
```

- Format: a space-separated list of `from:to:#색`. `from < to`, with `from` in ascending order.
- Colors are `#RGB`/`#RRGGBB`/`#RRGGBBAA` or `token(이름)` (a theme color, e.g. `0:85:token(tone-success) 85:100:token(tone-danger)`). Unknown tokens are magenta.
- The whole attribute can be bound (`alarm-zones="{Zones}"`, a string in the same format). A null or empty bound value means no zones.
- An invalid format (not three parts, not a number, `from >= to`, descending, not a color), whether literal or bound, throws `InvalidOperationException` at layout time and the screen does not appear.

---

## 10. Input widgets

| Tag | Main attributes | Default size |
|---|---|---|
| `DvInput` | `value` `keyboard-type`(text) `min` `max` `step`(1) `show-stepper`(false) `format` `unit` `placeholder` `multiline`(false) `readonly`(false) `disabled` `onbeforecommit` | 200×40 (multiline 200×96) |
| `DvSelect` | `value` `placeholder` `readonly` `disabled` `onbeforecommit` + `DvOption` children or `items` `item-value` `item-text` `item-icon` | 200×40 |
| `DvDateTimeInput` | `value` `mode` `show-seconds`(false) `format` `placeholder` `readonly` `disabled` `onbeforecommit` | 200×40 |
| `DvColorInput` | `value` `placeholder` `readonly` `disabled` `onbeforecommit` | 200×40 |
| `DvListBox` | `value` (one) or `selection-mode="multi"` + `selected-items` (many) `readonly` `disabled` `onbeforecommit` + `DvOption` children or `items` `item-value` `item-text` `item-icon` | 200×(up to 5 rows, row 40) |
| `DvTreeView` | `items` `item-children` `item-text` `item-value` `value` `readonly` `disabled` `onbeforecommit` | 200×(up to 8 rows, row 40) |

Common rules:
- With `value="{Bind X}"` it is editable; with one-way (`{X}`), a literal, or `readonly="true"` (bindable), it is read-only (dashed border `.input-readonly`).
- Appearance is the `.input` class (surface background, border, radius 6, padding 8 12) and can be overridden with `style`.
- The value is written to the VM **once, when editing ends**. An unchanged value is not written.

### 10.1 DvInput

```xml
<DvInput value="{Bind Recipe.Name}" placeholder="이름"/>
<DvInput value="{Bind SetPoint}" keyboard-type="numeric" min="0" max="120" format="0.0" unit="℃" onbeforecommit="Check"/>
<DvInput value="{Bind Password}" keyboard-type="password"/>
<DvInput value="{Bind Memo}" multiline="true"/>
<DvInput value="{Bind Speed}" keyboard-type="numeric" min="0" max="100" step="0.5" unit="%" show-stepper="true"/>
```

- `keyboard-type` = `text` (default) `numeric` `password`. Any other value is `text`.
- Pressing starts editing. Enter / Tab / Done key / pressing elsewhere = commit, Esc / Cancel = discard. While typing, only the edit buffer changes.
- **numeric:** integer vs. real and sign are determined by the **bound property's type** (`int` = signed integer, `uint`·`byte` = unsigned integer, `double`·`decimal` = real, `int?` allowed). The decimal point is `.` (InvariantCulture). Values outside `min`/`max` are **rejected** (not clamped) — an error border and the reason are shown and editing continues. Clearing a nullable number is also rejected. `format` is for display.
- **password:** starts empty; committing it empty does not change the value.
- **± buttons (`show-stepper="true"`, numeric only):** draws −/+ at both ends of the input box. Pressing subtracts or adds `step` (default 1) from the current value, clamps to `min`–`max`, and writes immediately (if the value is empty, from `min` or 0). Writing uses the same path as numeric input (type, range, `onbeforecommit`); pressing the middle enters a value directly with the keypad as usual. Pressing −/+ while typing applies the typed value first, then steps. When read-only, one-way, or disabled, the buttons are dimmed and do nothing. Decimal places match those of `step` (a fractional `step` is not used on an integer property).
- While editing, even if the VM value changes (`Invalidate()` polling), the text being edited takes priority.
- The editor depends on the host: OpenTK and `ViewControl.TouchMode` = built-in virtual keyboard (QWERTY Korean/English, Shift, symbols, 2-set Korean (Dubeolsik), numeric keypad; text input opens in the English layout, or the Korean layout when `DvStrings.Language` is `ko`, and the Korean/English key is always there; slides up from the bottom and pushes the screen up so the input box stays visible; pressing outside cancels; single line only). WinForms `ViewControl` = a hidden TextBox placed over the box (OS IME, multiple lines, clipboard; pressing outside commits).

### 10.2 DvSelect

```xml
<DvSelect value="{Bind Mode}" placeholder="운전 모드">
  <DvOption value="auto" text="자동" icon="fa-robot"/>
  <DvOption value="manual" text="수동"/>
</DvSelect>
<DvSelect value="{Bind RecipeId}" items="{Recipes}" item-value="Id" item-text="Name" item-icon="Icon"/>
<DvSelect value="{Bind Direction}"/>   <!-- enum 속성: 항목 자동 생성(글자 = enum 이름) -->
```

- `value` binds to the item's **value** (not its index). Item values are converted to the property type for comparison and storage (`"1"` → int, `"Left"` → enum, `int?` allowed). If no item matches, `placeholder` is shown.
- Item source priority: `items` > `DvOption` children > automatic enum. If `items` is given, it does not fall back to `DvOption` even if the result is empty.
- `items` = `IEnumerable`. `item-value`/`item-text`/`item-icon` are property paths inside the item. If omitted, the item itself is the value and `ToString()` is the text.
- `value` `text` (omitted = value) `icon` of `DvOption` are bound.
- Pressing opens a list below (above if there is no room, shrunk if neither fits). Up to 8 are visible; more scroll. Pressing an item writes once. Pressing outside closes it, and that press is not passed through below. There is no keyboard navigation.

### 10.3 DvDateTimeInput, DvColorInput

```xml
<DvDateTimeInput value="{Bind ReserveAt}"/>                       <!-- DateTime → 날짜 + 시각 -->
<DvDateTimeInput value="{Bind Day}"/>                             <!-- DateOnly → 달력만 -->
<DvDateTimeInput value="{Bind Start}" show-seconds="true"/>       <!-- TimeOnly → 시각 칸 -->
<DvDateTimeInput value="{Bind ReserveAt}" mode="date" format="yyyy.MM.dd"/>
<DvColorInput value="{Bind PenColor}"/>
```

- Value types: `DateTime` `DateOnly` `TimeOnly` (nullable allowed). `DateTimeOffset` is not supported. Colors are a `#RRGGBB` string or `SKColor`.
- `mode` = `date` `time` `datetime`; if omitted, it is determined by the type. A `mode` that does not match the type (e.g. `time` on DateOnly) falls back to the type default (one warning). `mode="date"` keeps the time part of a DateTime, and `mode="time"` keeps the date. Hiding seconds keeps the existing seconds.
- `format` = .NET date format (current culture). An invalid format falls back to the default format (one warning).
- Pressing opens a popup (calendar + hour·minute(·second) fields, or HSV square + hue bar + `#RRGGBB` field). Pressing a field opens the numeric keypad / keyboard.
- The value is written once only when **OK** is pressed. Cancel / pressing outside = not written (the outside press is not passed through below).
- If the value is null, `placeholder` is shown and the popup starts at today/now (white for colors). For `DateTime.MinValue`/`DateOnly.MinValue` the popup also starts at today. There is no "Clear" button.
- Color: no alpha editing. `SKColor` keeps its original alpha; strings are written as `#RRGGBB`. There is no color palette (swatch list).

### 10.4 DvListBox

An always-open selection list. Choices are built exactly like `DvSelect` (`items`·`item-value`·`item-text`·`item-icon` > `DvOption` children > automatic enum, 10.2).

```xml
<DvListBox value="{Bind RecipeId}" items="{Recipes}" item-value="Id" item-text="Name"/>
<DvListBox selection-mode="multi" selected-items="{Bind Zones}">
  <DvOption value="A" text="A 구역"/>
  <DvOption value="B" text="B 구역"/>
</DvListBox>
```

- **Single selection** (default): `value` is the **value** of the chosen choice (not its index — same conversion and comparison as `DvSelect`). Pressing a row writes that value once. Pressing the chosen row again leaves it unchanged.
- **Multiple selection**: `selection-mode="multi"` + `selected-items="{Bind X}"`. Each row press adds or removes it, and every change writes a **new list** (same type rules as `DvDataGrid`'s `selection`: `T[]` `List<T>` `IList<T>` `IReadOnlyList<T>` `IEnumerable<T>`, elements are choice values converted to `T`). `onbeforecommit` receives the new list (`bool M(object)` or that list type). The mode name is `selection-mode`, same as `DvDataGrid`.
- Row height is 40; without a given height, it is tall enough to show up to 5 rows. More rows scroll inside the list (scroll panel — dragging scrolls and does not select; includes inertia and scrollbar). For hundreds of rows or multiple columns, use `DvDataGrid`.
- One-way, fixed value, or `readonly` only displays; `disabled` dims it. With only `id` and no `value` (`selected-items` for multiple), the handle holds the chosen value (20.9). With neither, it only displays.
- Appearance: `.listbox` (surface background, border, rounded corners, inner padding 4), rows `.listbox-item`, chosen row `.listbox-item-on` (`primary` background). Row text does not wrap.

### 10.5 DvTreeView

Expand and collapse hierarchical data (plant → line → equipment) and pick one. The data is the app's own objects as is (no tree-specific node type).

```xml
<DvTreeView items="{Plant}" item-children="Kids" item-text="Name" value="{Bind PickedEquip}"/>
<DvTreeView items="{Plant}" item-children="Kids" item-text="Name" item-value="Tag" value="{Bind PickedTag}"/>
```

- `items` = list of root items, `item-children` = path of the child list inside an item (missing or empty = leaf), `item-text` = text path. If `item-value` is given, that value is written to `value`; if omitted, **the item object itself** is written.
- **Pressing the ▶/▼ area (indent + icon) expands/collapses; pressing anywhere else selects.** Selecting does not expand. Expanding/collapsing works even when read-only.
- Initially everything is collapsed, and the screen remembers expansion (keyed by item object — the same object (`Equals`) keeps it across redraws). Collapsing a branch keeps the expansion memory below it, so it is restored when re-expanded.
- Row height 40, indent 20 per depth. Without a given height, up to 8 rows are visible and more scroll (scroll panel — dragging scrolls). There is no lazy loading (reading children on expand) — child lists are read fully at layout time.
- Single selection only. Value ownership rules are the same as other value widgets (with only `id` and no `value`, the handle holds it — `SelectedNode`, 20.9). Cycles (an ancestor object appearing again as a child) skip that branch.
- Appearance: `.treeview`, rows `.treeview-item`, chosen row `.treeview-item-on` (`primary` background).

---
## 11. Data grid (DvDataGrid)

```xml
<DvDataGrid items="{Rows}" grow="1" selection-mode="multi" selection="{Bind Picked}" row-key="Id"
            onrowclick="Inspect" row-class="RowClass"
            style="border-radius:token(radius.md); border:1 solid token(border)">
  <DvColumn header="No"   field="Id"   width="80" frozen="true" sortable="true"/>
  <DvColumn header="이름" field="Name" cell-type="input"/>
  <DvColumnGroup header="공정값">
    <DvColumn header="값" field="Value" width="120" format="0.00" sortable="true" sort="desc" cell-class="ValueClass"/>
    <DvColumn header="설비" field="Equip.Name" width="20%" align="center"/>
  </DvColumnGroup>
  <DvSummaryRow title="합계" fn="sum"/>
  <DvSummaryRow title="평균">
    <DvSummary field="Value" fn="avg" format="0.00"/>
    <DvSummary field="Name" fn="count"/>
  </DvSummaryRow>
</DvDataGrid>
```

### 11.1 DvDataGrid attributes

| Attribute | Value (default) |
|---|---|
| `items` | Row list. `IList` recommended (referenced without copying; even 300,000 rows are instant). Any other `IEnumerable` is copied on every layout (warning). null or empty list = header only |
| `row-height` | Row height (48) — all rows are the same |
| `header-height` | Height of one header tier (40) |
| `selection-mode` | `none` (default) `single` `multi` (multi = each tap toggles) |
| `selection` | Selection binding (11.4) |
| `row-key` | Property path that identifies a row. Keeps selection and editing even when row objects are replaced |
| `onrowclick` | Row click method — `M(Row row)` if it exists, otherwise `M()` (called after the selection is updated) |
| `row-class` | Property path on the row item. Its value = class name applied to the row (11.6) |
| `header-color` `header-text-color` `text-color` `row-color` `row-alt-color` `hover-color` `selected-color` `line-color` `summary-color` `summary-text-color` | Colors (`#rrggbb[aa]` or `token(이름)`, bindable). Omitted = `grid-*` theme tokens. `row-alt-color` = second stripe color |

Default size 480 × (header + 5 rows + summary rows). Rounded corners and borders are set with `style`; cells, header and scrollbars are clipped inside the corners, and the border is drawn on top. Cell text uses the grid's `font-size`.

### 11.2 DvColumn attributes

| Attribute | Value (default) |
|---|---|
| `header` | Header text |
| `field` | Property path on the row item (`A.B`). null in the middle = empty cell; missing property = empty cell + warning |
| `width` | `120` (px) · `20%` (fraction of grid width) · `*` · `2*` (default `*`). Star columns split the remaining width by weight, minimum 48; if there is not enough width, horizontal scrolling |
| `align` | `left` `center` `right`. Default = numbers right, everything else left (select, datetime and color cells are left) |
| `format` | .NET format string (InvariantCulture). Long text is truncated with "…" |
| `frozen` | `true` = fixed during horizontal scrolling. **Only columns contiguous from the start** (columns in the middle are ignored + warning) |
| `sortable` | `true` = sort by tapping the header |
| `sort` | `asc` `desc` = initial sort |
| `cell-class` | Property path on the row item. Its value = class of this column's cells (11.6) |
| `cell-type` | `text` (default) `check` `lamp` `button` `input` `select` `datetime` `color` (11.7, 11.8) |
| `filter` | `none` (default) `auto` `text` `values` `range` `date` (11.9) |

### 11.3 Scrolling and updates

- Only visible rows are drawn. Wheel, drag, inertia and scrollbars behave like `overflow:scroll`. The header is fixed, and `frozen` columns are fixed during horizontal scrolling.
- Cell values are read **at draw time**. If you changed cell values or added/removed rows in the same list, call `Repaint()` (`DvSurface.Repaint()`, `ViewWindow.Repaint()`, `ViewControl.MarkRepaint()` — no rebuild, callable from another thread). If you changed `items` to point to a **different list**, call `Invalidate()`.
- Do not modify the list from another thread while it is being drawn.

### 11.4 Selection

- `selection="{Bind X}"` writes a **new list** every time the selection changes. The type of `X` can be `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`; in single mode, `T` is also allowed.
- Without `{Bind}`, the selection lives only on screen. One-way `{X}` is re-applied when the VM assigns a new value (do not modify the list; assign a new list).
- With `row-key`, selection is kept even when row objects are replaced.

### 11.5 Sorting

- Tapping the header of a `sortable="true"` column cycles none → ▲ → ▼ → none. Sorts stack in tap order, and with 2 or more a priority number is shown. `sort="asc|desc"` = initial sort.
- The app's list is not changed (the order exists only inside the grid). Stable sort, nulls first, no exception even with mixed types.
- When it re-sorts: header tap, `items` pointing to a different list, a change in row count. **A change in values alone does not re-sort** (when no filter condition is set; so rows do not move under the finger). The same applies when the same list is refilled with the same row count, so assign a new list. When a condition is set, see 11.9.
- Selection and `onrowclick` follow the visible order. Edited columns are not re-sorted either (when no filter condition is set).

### 11.6 Header groups, summary rows, conditional colors

- **`DvColumnGroup header`** creates a header tier grouping columns. Nesting adds tiers, and each tier's height is `header-height`. A group spanning the frozen-column boundary is drawn split in two (warning).
- **`DvSummaryRow title fn`** = summary band at the bottom. `fn` = `sum` `avg` `min` `max` `count`. The row's `fn` applies to all numeric columns, and `title` is shown in the first column. Place `DvSummaryRow` directly under `DvDataGrid`.
- **`DvSummary field fn format`** sets one column's cell individually (`field` is exactly the same path as `DvColumn field`). With `value="{X}"`, that value is shown instead of a computed one (server totals, etc.). Non-numeric values and nulls are excluded from computation, and `count` is the number of non-null values. Summaries are recomputed after `Invalidate()`/`Repaint()`.
- **Conditional colors:** `row-class="RowClass"` (grid) and `cell-class="ValueClass"` (column) are property names on the row item. That property's value is a class name (space-separated, later wins, null = none). The condition is app code.

```csharp
public string? ValueClass => Value > Hi ? "over" : null;
```
```xml
<DvStyle>.over { background: token(tone-danger); color: #ffffff; font-weight: bold }</DvStyle>
```

- Classes use **only** `background` (solid color), `background-color`, `color`, `font-weight`. Background priority: `cell-class` > selection > `row-class` > hover > stripe. Unknown classes are ignored. Read at draw time, so call `Repaint()` when the value changes.

### 11.7 Immediate cells (`check` `lamp` `button`)

| `cell-type` | Behavior | Extra attributes |
|---|---|---|
| `check` | bool/bool? property. Tapping toggles it and **writes directly to the row item**. A property with no public setter is only displayed, dimmed | `onbeforecommit` (6.5 — without it the value is written directly; if the named method is missing or its signature does not match, nothing is written), `readonly` |
| `lamp` | Display only. bool → on = `on-color` (default `state-run` token); string/enum → `state-{값}` token (unknown value = off) | `on-color` (`#rrggbb` or `token()`, bindable), `blink="true"` (lit lamp blinks) |
| `button` | `onclick` → `VM.M(Row row)` or `VM.M()` | `text` `icon` `variant` (primary, otherwise secondary danger ghost) `enabled` (bool property path on the row item; false = dimmed and inactive) |

Tapping a check or button cell performs only that action (no selection, no `onrowclick`). Tapping a lamp cell is a normal row tap. The whole cell is the touch area.

### 11.8 Edit cells (`input` `select` `datetime` `color`)

| `cell-type` | Property type | Extra attributes |
|---|---|---|
| `input` | `string` (text keyboard), `int` `long` `short` `sbyte` `uint` `ulong` `ushort` `byte` `double` `float` `decimal` and nullable (numeric keypad; integer/sign determined by the type) | `min` `max` (out of range is rejected; unparseable gives one warning), `unit`, `format` (for display) |
| `select` | Any type | `DvOption value text icon` children, or `items="{Catalog}" item-value item-text item-icon` (relative to the grid VM, re-read on `Invalidate()`), or enum automatically. The cell shows the chosen item's icon + text |
| `datetime` | `DateTime` `DateOnly` `TimeOnly` (+ nullable) | `mode` `show-seconds` `format` (10.3 rules) |
| `color` | `string` (written as `#RRGGBB`), `SKColor` (alpha kept) | The cell shows a color swatch + hex value |

- Tapping a cell **edits immediately** (no selection, no `onrowclick`). input edits in place (virtual keyboard or native editor); the others open a popup below the cell.
- Writes once to the row item's property (not written if the value is the same). Then only the grid is redrawn (including summaries, no rebuild). Other widgets bound to the same data change on the next `Invalidate()`.
- `onbeforecommit` is in 6.5. If the method is missing, throws, or returns anything other than `true`, the value is rejected — input stays open showing the reason, and a popup stays open. The same applies if the row item property's setter throws (input message = "Could not save the value", 6.7).
- **Display-only cases** (tapping is a normal row tap): `readonly="true"`, no public setter, a struct in the path, property type not matching the cell kind (one warning).
- Editing follows the row (`row-key`, otherwise the item reference). It survives sorting, `Invalidate()` and `Repaint()`; when scrolled fully off screen, input commits and popups close; when the row disappears, it closes without writing. Tapping a header during input editing commits first (the virtual keyboard cancels — because the keyboard is modal). A header tap while a popup is open only closes the popup.
- Only with the native editor (`UseNativeEditor = true`): Enter = next row in the same column, Tab / Shift+Tab = next/previous input column in the row (non-editable cells are skipped, scrolls into view, ends at the last row/column, stops on a rejected value). With the virtual keyboard, committing ends editing.

### 11.9 Filters

Giving a column `filter` adds a **filter row** below the header (if no column has `filter`, there is no row). Filters only filter inside the grid; the app's list is not changed.

```xml
<DvDataGrid items="{Rows}" grow="1" row-key="Id">
  <DvColumn header="태그"   field="Tag"   width="140" filter="text"/>
  <DvColumn header="상태"   field="State" width="100" filter="values"/>
  <DvColumn header="설정값" field="SetPoint" width="110" format="0.0" filter="range"/>
  <DvColumn header="예약"   field="ReserveAt" width="150" format="MM-dd HH:mm" filter="date"/>
  <DvColumn header="모드"   field="Mode" width="100" cell-type="select" filter="auto">
    <DvOption value="auto" text="자동"/>
    <DvOption value="manual" text="수동"/>
  </DvColumn>
</DvDataGrid>
```

| `filter` | Behavior |
|---|---|
| `none` | No filter (default, same as omitting the attribute) |
| `auto` | Decided by the column value type (type of the `field` property; nullable uses the underlying type): `string` → `text`, numbers → `range`, `DateTime`·`DateOnly`·`TimeOnly` → `date`, `bool`·enum → `values`, `cell-type="select"` column → `values`, anything else or unknown type (e.g. no items) → `text` |
| `text` | Rows whose visible cell text contains the entered text |
| `values` | Rows with values checked in the list |
| `range` | Numeric range min–max |
| `date` | Date/time range start–end |

Bindable, interpreted the same as literals. Any other value = warning, then `none`. **`auto` on a string column is `text`.** To pick from a fixed set of states, use `values`. Constants are `Values.GridFilter`.

**Filter row and popup**
- Tapping a filter row cell opens that column's popup. A cell with a condition shows an accent-colored funnel + condition summary ("…" if long); a cell without a condition shows only a dimmed funnel. Cells of columns without `filter` are empty.
- Buttons at the bottom of the popup: **OK** = apply the condition and close, **Clear** = remove that column's condition + close, **Cancel** = close unchanged. Tapping outside the popup is Cancel.
- `text`: one input field. `values`: "Select all" + value list (checked = shown; if there are empty values, `(Empty)` comes first; with more than 1000 values only the first 1000 — in that case unchecking even one item makes the condition "checked values", so values outside the list are hidden too (same as Excel)). The list is a snapshot taken when opened, built from rows with the other columns' filters applied. All checked = no condition.
- `range`: two fields, min and max, numeric keypad rules (non-numbers are rejected with a reason line). Either side may be empty.
- `date`: tapping the start or end field opens a **date picker overlaid on the filter popup**. Picker OK = only the field value changes (no VM write). Outside the picker = only the picker closes. An empty start field starts at 00:00:00, an end field at 23:59:59.

**Filtering rules**
- AND across columns; `values` within one column are OR.
- `text`: case-insensitive, culture-invariant substring match on the **visible text** (including `format` and select item text).
- `values`: any of the checked values (OR); `(Empty)` is null or empty text. `range`·`date`: bounds inclusive, either side alone is allowed, cells with empty values fail range conditions.

**Interaction with other features**
- Sorting is applied to the filtered rows, and summary rows compute **visible rows only**.
- Rows removed by a filter are dropped from the selection (the `selection` binding is updated too).
- A row being edited stays until editing ends, then is immediately re-filtered.
- Even when values change, filtering is re-run on `Repaint()`, rebuild and `items` replacement. While a condition is set, `Repaint()`, `Invalidate()` and edit end re-filter and also re-sort, so rows whose values changed may move. Filter state survives rebuilds but **cannot be read or set from app code**.
- Theme tokens: `grid-filter` (filter row background), `grid-filter-fg` (funnel without condition), `grid-filter-active` (funnel with condition, summary).
- Localization keys (`DvStrings`): `dv.filter.ok` `clear` `cancel` `all` `empty` `min` `max` `from` `to` `true` `false` `more`.

---

## 12. Trend (DvTrend)

```xml
<DvTrend source="{Trend}" window="120" area="true">
  <DvTrendChannel name="temp" label="온도" unit="℃" color="#22d3ee"/>
  <DvTrendChannel name="press" label="압력" points="{PressHistory}"/>
</DvTrend>
```

| Attribute | Value (default) |
|---|---|
| `source` | `TrendBuffer` (real-time data) |
| `window` | Visible time span, seconds (60). Less than 1 second is allowed (`0.1`) |
| `end` | Time at the right edge (`DateTime` binding). Omitted = based on the latest data |
| `y-min` `y-max` | Vertical range. Omitted = automatic. **One vertical axis is shared by all channels** — for values on very different scales (e.g. 40 ℃ and 4 bar) use two `DvTrend`s or scale the values before adding them |
| `area` | Fill below the line (false) |
| `smooth` | Curve interpolation (true) |

`DvTrendChannel` (child of `DvTrend`):

| Attribute | Value |
|---|---|
| `name` | Channel name to look up in the `source` buffer (omitted = `ch0`, `ch1`…) |
| `label` `unit` | Legend text |
| `color` | `#RRGGBB` or `token(이름)` (theme color; unknown token = magenta), bindable (omitted or unparseable = default palette order) |
| `points` | `IEnumerable<TrendPoint>` binding — if present, this data is used instead of `source` |

- Real-time: the VM provides a `TrendBuffer`, calls `TrendBuffer.Add(채널, 시각, 값)` (thread-safe), then `Invalidate()`.
- History: bind `IEnumerable<TrendPoint>` to `points` and pin the right edge with `end="{EndTime}"`.
- `double.NaN` breaks the line. With no data, "No data" (`dv.trend.empty`) is shown.
- Default size 480×240. Grid line color is the `trend-grid` token.

---

## 13. Alarms (DvAlarmBanner, DvAlarmList)

Raising, storing and ACKing alarms is the app's (server's) responsibility. The widgets only display them and pass ACK requests to the VM.

| Tag | Attributes (default) | Default size |
|---|---|---|
| `DvAlarmBanner` | `items` `comm-alarm` `onack` `show-ack` | 480 × 40 (+24 with `comm-alarm`) |
| `DvAlarmList` | `items` `onack` `show-ack` `row-height` (36) | 560 × 210 |

- `items` = `IEnumerable<AlarmRow>` (`AlarmRow` = `Id` `Time` `Priority` (Critical·Major·Minor) `Message` `Source` `Acked` `AckTime`). Read at layout time, so call `Invalidate()` when it changes. There is also an in-memory store `AlarmLog` for standalone HMIs and demos (`items="{Alarms}"` + `Alarms => Log.Rows`). `AlarmLog` is thread-safe, so `Raise`/`Ack` from any thread, then `Invalidate()`.
- Order: unacknowledged → priority → newest. The banner shows the first such alarm and the unacknowledged count.
- `onack="Ack"` → `public void Ack(AlarmRow row)` or `public void Ack(long id)`. The banner ACKs the representative unacknowledged alarm; the list uses the ACK button of the tapped row.
- `show-ack` = show the ACK button. Omitted = shown when `onack` is present. Bindable (show per permission).
- `comm-alarm` = if it has text, a communication-fault band is drawn above the banner.
- Color tokens: `alarm-critical` `alarm-major` `alarm-minor` (+`-fg`), `comm-alarm`.

```csharp
public IReadOnlyList<AlarmRow> Alarms { get; set; } = [];
public async void Ack(AlarmRow row) { await api.AckAsync(row.Id); Alarms = await api.GetAsync(); } // 처리기 안 await 뒤 → 자동으로 다시 그림
```

**Confirm before ACK (prevent mis-operation).** The widget has no confirm attribute — ask with `App.MessageBox` (21.5) in the `onack` handler and ACK only on Yes. Which alarms to ask about, the wording and permission checks are also decided in the handler.

```csharp
public async void Ack(AlarmRow row)
{
    if (row.Priority == AlarmPriority.Critical &&
        await App.MessageBox($"'{row.Message}' 알람을 확인할까요?", "알람 확인", DvMessageButtons.YesNo) != DvDialogResult.Yes)
        return;                                   // 아니요·외부에서 닫힘(Cancel) = ACK 안 함
    Log.Ack(row.Id);
}
```

- Compare the result with `== DvDialogResult.Yes`. Writing `!= No` mistakes a dialog closed from outside (`Cancel`) for Yes.
- While the confirm dialog is open, the screen below accepts no input, so tapping the ACK button repeatedly does not open two confirm dialogs.
- Dialogs can only be shown from screens inside `DvApp` (pages, bars, dialogs). In hosts that use `DvSurface` directly, the app must build its own confirm UI.

---

## 14. Repeat (DvRepeat)

```xml
<DvRepeat items="{Pumps}" gap="xs" grow="1" style="overflow:scroll">
  <DvRow>
    <DvLabel class="body" text="{Index}"/>                <!-- 0부터 -->
    <DvLabel class="body" text="{Item.Name} {Unit}"/>     <!-- Item = 현재 항목, 그 밖의 경로 = 화면 VM -->
    <DvToggle value="{Bind Item.Auto}"/>                  <!-- 양방향은 항목에 씀 -->
    <DvButton variant="ghost" text="열기" onclick="Inspect"/>  <!-- VM.Inspect(Pump p) 가 있으면 그것, 없으면 VM.Inspect() -->
  </DvRow>
</DvRepeat>
```

- `items` = a non-string `IEnumerable`. Empty or null = no children (height 0, no error).
- Children are a template, created once per item. Layout is the same as `DvStack` and uses the sugar attributes (`gap` `padding` `align` `justify`). For horizontal rows or grids, use `style` (`display:flex; flex-direction:row`).
- Inside the template: `{Item}`/`{Item.X}` = current item, `{Index}` = 0-based index, any other path = outer context (screen VM, component parameters, or the item scope of an outer repeat).
- The data belongs to the app. Call `Invalidate()` after changing the collection.
- Nesting: `<DvRepeat items="{Item.Children}">` — the inner `Item` shadows the outer `Item`. Also works inside component templates (outer scope = component parameters). However, clicks raised by an `onclick` **inside a component template** within a repeat do not receive the item (an `onclick` given on the instance does receive the item, chapter 7).
- No virtualization, keys or selection (use `DvDataGrid` for those). Use long lists together with `overflow:scroll`.

---

## 15. User components (DvComponent)

```xml
<DvComponent name="PumpCard">
  <DvParam name="Title" default="-"/>
  <DvParam name="Value"/>
  <DvCard title="{Title}">
    <DvLabel class="display" text="{Value}"/>
    <DvSlot/>
  </DvCard>
</DvComponent>
```

```xml
<PumpCard Title="취수 펌프" Value="{Flow}" grow="1">
  <DvLabel class="caption" text="정상 운전"/>
</PumpCard>
```

- Register in C# with `ComponentRegistry.Register(markup)`, and pass that `registry` to the host (`ViewWindow`, `DvSurface`, `DvRenderer`).
- `DvComponent` requires `name`, and **the first single child** that is not `DvParam`/`DvStyle` is the template root.
- `DvParam name default` — instance attribute names are matched to parameters case-insensitively, while `{Name}` inside the template is case-sensitive. Parameters not given get `default` (null if none).
- `{Name}` inside the template refers **only to parameters** (not the screen VM). Instance attribute values are bound from the outer VM.
- The instance's children go in place of `<DvSlot/>` (a DvSlot inside a nested component also receives the outer instance's children).
- Giving a parameter `{Bind Data.X}` makes `value="{Param}"` of `DvToggle` `DvKnob` `DvInput` `DvSelect` `DvDateTimeInput` `DvColorInput` inside the template write two-way to the outer VM.
- `onclick` inside the template calls a method on the screen VM (the method name is fixed and cannot be changed via a parameter). If the method has one parameter, it receives the parameter value of matching type (6.4) — a button inside `<PumpCard Pump="{P1}"/>` → `Stop(PumpVm pump)`.
- A component whose template root is `<DvUnit>` becomes a unit inside a process diagram (`DvProcess`) (chapter 22, `dvml-process.md` chapter 8).
- What passes from the instance to the template root is the same as in chapter 7 (`style` `class` `grow` `width` `height` `visible` `onclick` `disabled`). If the template root also has `onclick`, the instance's wins.
- The same `Register` also registers process diagram equipment symbols (`<DvSymbol>`) (22.7).
- With the same name, a user component takes precedence over a built-in component (`DvCard`, etc.). A cycle that expands itself throws at depth 64.
- A `<DvStyle>` inside a definition (whether before or inside the template) applies **globally** to every screen using that registry (same as a screen `DvStyle`). The order is theme < component `DvStyle` (registration order) < screen `DvStyle`, so for the same class and same property, **the screen `DvStyle` wins**. Prefix class names with the component name so they do not collide with other components (`.pumpcard-head`).

---

## 16. Pages and the app (DvApp·DvPage·DvBar)

In a program with multiple screens, a single `DvApp` holds and switches all screens. One app is one window. There are three building blocks.

| Type | Role |
|---|---|
| `DvApp` | Manager that holds pages and changes the current page. Applies theme, localization and component registry to all screens |
| `DvPage` | One screen. The class itself is the markup's VM (target of bindings and `onclick`) |
| `DvBar` | Always-visible fixed area (top bar, sidebar). Placed in `DvApp.Top` `Bottom` `Left` `Right` |

Dialogs shown over pages (`DvDialog`) are also held by the app (chapter 21). Pages and bars are both made as "`partial class` + dvml". They are connected only by `vm="네임스페이스.클래스"` on the dvml root (file name does not matter; the same name is convention), and the generator fills in the markup connection and `id` fields (chapter 20).

### 16.1 Complete example

The smallest app: a top bar + 2 pages (Home → Monitoring → "Back") + one dialog (Monitoring's "Change setpoint"). It is **the exact same code** as `samples/Devinno.UI.AppSample` (a test compares them), and runs with `dotnet run --project samples/Devinno.UI.AppSample`.

`Program.cs`

```csharp
using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Top = new TopBar();                 // 상단 고정 영역(선택)
app.Pages.Add(new HomePage());          // 처음 넣은 페이지 = 시작 페이지
app.Pages.Add(new MonitorPage());

using var window = new ViewWindow(app, 1024, 640, "App Sample");
window.Run();
```

`Pages/TopBar.cs`

```csharp
using Devinno.UI.App;

public sealed partial class TopBar : DvBar
{
    public string HomeClass => Tab<HomePage>();
    public string MonitorClass => Tab<MonitorPage>();

    public void ShowHome() => App.Show<HomePage>();
    public void ShowMonitor() => App.Show<MonitorPage>();

    private string Tab<T>() where T : DvPage => App.CurrentPage is T ? "tab on" : "tab";
}
```

`Pages/TopBar.dvml`

```xml
<DvRow vm="TopBar" style="height:56; padding:0 16; gap:8; background: token(surface)">
  <DvStyle>
    .tab { padding:8 18; border-radius: token(radius.md); color: token(text-muted) }
    .on { background: token(primary); color: token(primary-fg) }
  </DvStyle>
  <DvLabel class="title" text="App Sample"/>
  <DvSpacer/>
  <DvButton class="{HomeClass}" text="홈" onclick="ShowHome"/>
  <DvButton class="{MonitorClass}" text="모니터링" onclick="ShowMonitor"/>
</DvRow>
```

`Pages/HomePage.cs`

```csharp
using Devinno.UI.App;

public sealed partial class HomePage : DvPage
{
    public void OpenMonitor() => App.Show<MonitorPage>();
}
```

`Pages/HomePage.dvml`

```xml
<DvPageFrame vm="HomePage" title="홈" subtitle="버튼이나 상단 바로 페이지를 바꿉니다">
  <DvRow>
    <DvButton variant="primary" text="모니터링 열기" onclick="OpenMonitor"/>
  </DvRow>
</DvPageFrame>
```

`Pages/MonitorPage.cs`

```csharp
using Devinno.UI.App;

public sealed partial class MonitorPage : DvPage
{
    public double Temperature { get; set; } = 24.5;
    public double Setpoint { get; private set; } = 25.0;
    public int ShownCount { get; private set; }

    public void Back() => App.Show<HomePage>();

    public async void EditSetpoint()
    {
        double? value = await App.ShowDialog<SetpointDialog, double?>(Setpoint);
        if (value is null)
            return;                 // 취소 버튼, 또는 바깥에서 닫힘
        Setpoint = value.Value;     // 이어짐은 입력 스레드 — VM 을 바로 쓰면 화면도 같은 프레임에 다시 읽는다
    }

    protected override void OnShown() => ShownCount++;   // 보일 때마다
}
```

`Pages/MonitorPage.dvml`

```xml
<DvPageFrame vm="MonitorPage" title="모니터링" subtitle="{ShownCount}번째 표시">
  <DvRow>
    <DvButton variant="ghost" text="← 이전" onclick="Back"/>
  </DvRow>
  <DvCard title="수온">
    <DvLabel class="title" text="{Temperature} ℃"/>
    <DvRow>
      <DvLabel class="body muted" text="설정 {Setpoint} ℃"/>
      <DvButton variant="secondary" text="설정 변경" onclick="EditSetpoint"/>
    </DvRow>
  </DvCard>
</DvPageFrame>
```

`Pages/SetpointDialog.cs` — the dialog opened by "설정 변경" (chapter 21). It is here to show the pattern for building your own dialog; in a real app a single number like this should use the built-in `App.InputNumber` (21.5).

```csharp
using Devinno.UI.App;

public sealed partial class SetpointDialog : DvDialog<double?>
{
    public SetpointDialog(double current) => Value = current;   // 원본이 아니라 값을 복사해 편집

    public double Value { get; set; }

    public void Ok() => Close(Value);
    public void Cancel() => Close();                            // 결과 = null(취소)
}
```

`Pages/SetpointDialog.dvml`

```xml
<DvDialogFrame vm="SetpointDialog" title="설정 온도" subtitle="0 ~ 60 ℃" style="width:360">
  <DvInput value="{Bind Value}" keyboard-type="numeric" min="0" max="60" format="0.0" unit="℃"/>
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="secondary" text="취소" onclick="Cancel"/>
    <DvButton variant="primary" text="확인" onclick="Ok"/>
  </DvRow>
</DvDialogFrame>
```

- The bar button highlight binds a computed property (`HomeClass`) that reads `App.CurrentPage` to `class`. When the page changes, the app re-reads the bar (16.6).
- `MonitorPage` is retained, so when shown again `ShownCount` increases and the screen re-reads it (16.3).
- "설정 변경" opens `SetpointDialog` and waits for the result with `await`. OK returns the new value (`double`), Cancel returns `null`. `Setpoint` changed after `await` is reflected immediately in the screen's "설정 … ℃" without any notification (21.3).

### 16.2 Creating the app

```csharp
var app = new DvApp { Theme = DvTheme.Dark, Strings = strings };
app.Pages.Add(new HomePage());          // 처음 넣은 페이지 = 시작 페이지
app.Pages.Add(new MonitorPage());
app.Pages.Add(new AlarmPage());
app.Top = new MainTopBar();             // 선택. 영역이 없으면 페이지가 창 전체
app.HandlerError += (_, e) => Log.Error(e.Exception, $"{e.Kind} {e.Member}");
using var window = new ViewWindow(app, 1280, 800, "My HMI");
window.Run();
```

- **`Pages.Add(page)`** — adds the page and connects it to this app (`page.App` is filled). Adding the same instance again is ignored; if it is already in another `DvApp`, `InvalidOperationException`. Once added there is no way to remove it (it stays retained).
- **`Pages.Get<T>()`** — returns the one page of type `T` (including derived). If there is none or more than one, `InvalidOperationException` (if you have several of the same class, use `Show(page)` with the instance).
- **Start page** — while there is no current page, the first added page is shown. To show a different page first, call `app.Show(page)` before opening the window.
- **`CurrentPage`** — the page currently shown. Read-only, and the value **after** `Show` is applied (from the next frame, not right after `Show`). `null` if nothing has been shown yet.
- **`Show<T>()`** / **`Show(page)`** — changes the current page. `Show(page)` first adds a page not in the list (`Pages.Add`). If it is already the current page nothing happens; a page of another app throws.
- **`Invalidate()`** — signals that the current page, fixed areas and open dialogs should be rebuilt. Use it when you changed VM values from a timer or communication (`llm-rules.md` chapter 6). Values changed inside a handler or after a handler's `await` are re-read automatically, so you do not need to call it. Pages, bars and dialogs have no `Invalidate()` of their own — inside them call `App.Invalidate()`.
- **`App.Surface.Repaint()`** — redraws without rebuilding (grid cell values changed in place, rows added to the same list). Forwarded to the current page, the bars and open dialogs. `DvApp` has no `Repaint()` of its own; use this.
- **Adapter** — an app attaches to only one window/control at a time. Giving the same app to a second adapter throws `InvalidOperationException`. When the window closes it detaches and can attach to another adapter. The `Surface` (root screen to draw) and `RenderRequested` (repaint notification) used by adapters are public, but ordinary apps only need `ViewWindow`·`ViewControl` and never use them directly.

### 16.3 Page lifecycle

- **Retention.** A page's screen is built once when first shown and retained. When you go to another page and come back, scroll position, input values, `Click` subscriptions and control values written from code are as they were when you left. Bindings re-read the VM every time the page is shown again, so values changed while hidden and values changed in `OnShown` appear immediately. There is no operation to close or discard a page.
- **Notification order.** When `Show(b)` is applied: previous page `OnHidden` → (if b's screen is new: build → generator-made `OnLoaded()` once) → b's `OnShown`. `OnLoaded()` runs once in a page's lifetime.
- **Thread.** `OnShown`·`OnHidden`·`OnLoaded` are always called on the input (UI) thread.
- **In-progress input on the leaving page.** On a transition, values being edited on the leaving page are **committed** (`OnHidden` sees the committed values), and open popups and the virtual keyboard are closed. When you return, input values are as they were.
- **`App`.** Usable after `Pages.Add` (or `Show(page)`). Reading it before that (including inside the page constructor) throws `InvalidOperationException`, so put code that uses `App` in handlers, `OnLoaded` or `OnShown`. It is not null, so use it directly, e.g. `App.Show<T>()`.
- **Notification exceptions.** Exceptions thrown by `OnShown`·`OnHidden` are reported via `HandlerError` (`Kind` = `"shown"`/`"hidden"`, `Member` = page class name), and the transition continues.
- **Markup errors.** If a page's dvml cannot be read as XML, an exception is thrown as-is at the moment it is first shown (developer error). The current page does not change, but the previous page's `OnHidden` has already been called and values being edited have already been committed.

### 16.4 Threads and requests

`Show`, area assignment (`Top`, etc.), setting `Theme`/`Strings`/`Registry`, and `Invalidate` can be called **from any thread**. Requests are queued and applied on the input thread at the adapter's next frame; if called several times in between, only the last one is applied.

```csharp
// 통신 스레드에서
void OnAlarmReceived(AlarmRow row)
{
    alarmLog.Raise(row);
    app.Show<AlarmPage>();     // 렌더 중이어도 안전 — 다음 프레임에 전환
}
```

**Thread contract table.** The "input thread" is the thread on which the adapter handles layout, drawing, pointer and keys (the UI thread of the OpenTK window or WinForms control).

| What | Which thread | Notes |
|---|---|---|
| `DvApp.Show`·`Show<T>` (including transition argument)·`PageTransition`·`Top`/`Bottom`/`Left`/`Right`·`Theme`·`Strings`·`Registry`·`HotReload` | Any thread | Queued, applied on the input thread at the next frame |
| `DvApp.Invalidate`·`DvSurface.Invalidate`·`Repaint` | Any thread | Only sets a flag. Bindings are re-read at the next frame |
| `ShowDialog`·`MessageBox`·`InputBox`·`InputNumber`·`SelectBox`·`CloseDialogs`·`DvDialog.Close`·`IsTop`·`Dialogs` | Any thread | The result `Task` completes on the input thread. If you `await` on the input thread, the continuation also runs on the input thread |
| `Lock`·`Unlock`·`IsLocked`·`DvApp.Current`·`Pages` | Any thread | |
| Writing handle values (`Checked`·`Value`·`SelectedIndex`·`IsExpanded`·`Text`·`Enabled`, etc.) | Any thread | Schedules a repaint. If the value changes, the changed event (`ByUser = false`) fires **on the writing thread** |
| Events raised by user interaction, `onclick` handlers | Called on the input thread | Screen values may be changed directly inside the handler |
| `TrendBuffer.Add`·`AlarmLog` (all members)·`DvSyncContext.Post` | Any thread | Internal locking. Enumerating `AlarmLog` gives a snapshot at that moment |
| Writing view model properties | App's responsibility | The library only reads. Calling `Invalidate` after writing reflects it at the next frame |
| `DvSurface.Layout`·`RenderTo`·`Pointer*`·`Key*`·`Text*`, `LayoutBox`·`DvEditState` | Input thread only | Called by the adapter. Apps normally never call them directly |
| `DvSyncContext.Send` | Input thread only | Calling from another thread throws `NotSupportedException` |

The "Any thread" items in this table are verified by a stress test (`ThreadStressTests`) in which multiple threads call them concurrently while the input thread keeps running layout, drawing and pointer handling.

### 16.5 Fixed areas (`DvBar`)

Place a `DvBar` in `app.Top`·`Bottom`·`Left`·`Right`. All are optional.

- **The bar's dvml root sets its size.** `Top`/`Bottom` are full window width × the root's **height**; `Left`/`Right` are the height between the top and bottom areas × the root's **width**. Give the root a size like `style="height:56"`·`style="width:220"` (if not given, content size). The page fills the remaining space. With no areas at all, the page is the whole window and `Bounds` stays relative to the window.
- **If larger than the window**, they receive only what remains, in the order `Top` → `Bottom`, `Left` → `Right`.
- **Bars do not contain pages.** Buttons only **request** `App.Show<T>()`. Bars also have `App` (from when assigned to an area; reading it before that or inside the constructor throws `InvalidOperationException`) and `OnLoaded()`, but no `OnShown`/`OnHidden` (always visible).
- **One instance = one slot.** Placing the same bar in two areas at once throws `InvalidOperationException` (to move it, first set the original area to `null`). A bar that was placed in another `DvApp` also throws.
- **A bar removed from an area still belongs to that app.** It retains its screen and state (input values, scroll), so placing it again in an area of the same app shows it exactly as it was when it left.
- Dropdown/picker popups and the virtual keyboard are drawn relative to the whole window, so a top bar's `DvSelect` list unfolds over the page area without being clipped.
- The pointer is received by the screen of the cell that was pressed; when dragging while pressed, that cell keeps receiving it until release.

### 16.6 Highlighting the current page

For a bar button to indicate the current page, bind a computed property that reads `App.CurrentPage` to `class`. When the page changes, the app re-reads the areas.

```csharp
// MainTopBar.cs
public sealed partial class MainTopBar : DvBar
{
    public string MonitorClass => TabClass<MonitorPage>();
    public string AlarmClass   => TabClass<AlarmPage>();

    public void ShowMonitor() => App.Show<MonitorPage>();
    public void ShowAlarm()   => App.Show<AlarmPage>();

    private string TabClass<T>() where T : DvPage => App.CurrentPage is T ? "tab tab-active" : "tab";
}
```

Top bar `MainTopBar.dvml` — a single row of height 56, with the current page's tab highlighted.

```xml
<DvRow vm="MainTopBar" style="height:56; padding:0 16; gap:8; background: token(surface); border: 1 token(border)">
  <DvStyle>
    .tab { padding:8 18; border-radius: token(radius.md); color: token(text-muted); font-size: token(font.body) }
    .tab:hover { background: token(surface-raised); color: token(text) }
    .tab-active, .tab-active:hover { background: token(primary); color: token(primary-fg) }
  </DvStyle>
  <DvLabel class="title" text="Devinno.UI"/>
  <DvSpacer/>
  <DvButton class="{MonitorClass}" text="모니터링" onclick="ShowMonitor"/>
  <DvButton class="{AlarmClass}" text="알람" onclick="ShowAlarm"/>
</DvRow>
```

A "Back" button on the page side that returns home is a plain `onclick` (`Back()` is in the 16.1 complete example). To show it only when not on home, bind `visible` (e.g. `public bool ShowBack { get; set; }`).

```xml
<DvPageFrame vm="MonitorPage" title="모니터링">
  <DvRow visible="{ShowBack}">
    <DvButton variant="ghost" text="← 이전" onclick="Back"/>
  </DvRow>
  <DvCard title="수온">
    <DvLabel class="title" text="{Temperature} ℃"/>
  </DvCard>
</DvPageFrame>
```

The left sidebar `MainSideBar.dvml` is a vertical list of width 220. The C# side follows the same rule (`NavClass<T>()` reads `App.CurrentPage is T`).

```xml
<DvStack vm="MainSideBar" gap="xs" padding="lg" style="width:220; background: token(surface); border: 1 token(border)">
  <DvStyle>
    .nav { padding:10 14; border-radius: token(radius.md); color: token(text-muted); font-size: token(font.body); content-align: middle-left }
    .nav:hover { background: token(surface-raised); color: token(text) }
    .nav-active, .nav-active:hover { background: token(primary); color: token(primary-fg) }
  </DvStyle>
  <DvLabel class="title" text="Devinno.UI"/>
  <DvBox style="height:12"/>
  <DvButton class="{OverviewClass}" text="개요" onclick="ShowOverview"/>
  <DvButton class="{AlarmClass}" text="알람" onclick="ShowAlarm"/>
  <DvButton class="{TrendClass}" text="트렌드" onclick="ShowTrend"/>
  <DvSpacer/>
  <DvLabel class="caption muted" text="{CurrentText}"/>
</DvStack>
```

(`CurrentText` is `$"현재: {App.CurrentPage?.GetType().Name}"`.) The complete example is `samples/Devinno.UI.Demo/Bars/`, and the demo runs with `-- top` (top bar) or `-- nav` (sidebar).

### 16.7 Global settings

Changing `app.Theme`·`app.Strings`·`app.Registry` applies to **all** retained page and area screens. Rather than creating new screens, the same screens are rebuilt, so scroll position, values being edited, input values, control values written from code (`btn.Enabled = false`, etc.) and `Click` subscriptions are kept. They can be changed from any thread and are applied at the next frame.

**Page transition animation:** with `app.PageTransition = DvPageTransition.Fade` (or `Slide`), changing pages animates for about 0.2 seconds. Default `None` (instant). To do it differently just once: `App.Show<SettingsPage>(DvPageTransition.Slide)` / `App.Show(page, DvPageTransition.Fade)`.
- `Fade` = the new page gradually becomes opaque over the old page; `Slide` = the new page pushes in from the right and the old page exits to the left. During the animation both pages are drawn, and input goes to the new page from the start. The first page does not animate. Areas (`DvBar`) and dialogs do not animate.

**Hot reload (during development):** with `app.HotReload = true`, the dvml files of pages, areas and dialogs are watched, and on save that screen is swapped for the new markup immediately (without restarting the app).
```csharp
#if DEBUG
app.HotReload = true;   // 배포 빌드에서는 켜지 마세요
#endif
```
- Applied at the next frame within about 0.15 seconds after saving. VM objects and values, `id` handles and values written by handles are kept, and `OnLoaded()` is not called again. The screen is rebuilt as with `Invalidate()`.
- Only the markup changes. Anything that needs C# changes — new `id` handle fields, new VM properties or handlers — requires a rebuild (bindings to missing properties are empty values as usual). Element-based state the screen remembered (tab index without `value`, expander/tree expansion) resets to initial.
- If you save with invalid XML, the previous screen is kept and it is reported via `HandlerError` (`Kind` = `"hotreload"`, `Member` = file path). Fix and save again to reapply.
- File paths are absolute paths embedded by the source generator at build time, so it only works on the PC that built it. Components and styles registered from code are not watched.

### 16.8 `<DvPageFrame>` and C# `DvPage`

The markup tag for a page background with a header is **`<DvPageFrame>`** (the old name `<DvPage>` no longer exists). The C# class `DvPage` is something else (one screen retained by the app), so the usual root of a page dvml is `<DvPageFrame vm="…">`. See chapter 7.

### 16.9 Removed items

`DvNavigator`, `NavView`, `DvNavHost`, `DvNavItem`, the `:selected` pseudo-class and the `selected-style` attribute, the `ViewWindow(markup, vm …)` constructor, `ViewControl.Surface`, and `ViewWindow.Strings` (→ `app.Strings`) have been removed. Selection indication is done with a class binding that reads `App.CurrentPage` (16.6), and page switching with `App.Show`. The engine APIs `new DvSurface(markup, vm …)` and `DvRenderer` remain.

---
## 17. Localization

```xml
<DvPageFrame title="{L10n.title}" font-lang-ko="Malgun Gothic" font-lang-ja="Yu Gothic UI" font-lang-en="Segoe UI">
  <DvLabel class="body" text="@pump"/>                    <!-- {L10n.pump} 의 줄임. "@@x" = 글자 "@x" -->
  <DvLabel class="body" text="{L10n.pump} #1 {Status}"/>  <!-- VM 바인딩과 섞어 쓰기 -->
</DvPageFrame>
```

- When you set `DvStrings` on the app (`app.Strings = strings`, from any thread), `{L10n.키}` and `@키` are replaced with strings in the current language. Changing the language redraws the screen automatically.
- Lookup order: current language → `FallbackLanguage` → shows `[키]` (never silently blank).
- `@키` works only when the whole attribute value has the form `@name` (name = ASCII letters, digits, `_`, `.`).
- `font-lang-{언어}` on the root = the default font for that language (elements that set `font-family` keep their own).
- **Built-in widget text** (alarm columns, input errors, picker and dialog buttons, filter popup, virtual keyboard "Done") ships in **English (default) and Korean**. With no `DvStrings`, or with `Language = "en"`, it is English; set `Language = "ko"` for Korean (`app.Strings = new DvStrings { Language = "ko" }`). For any other language, or to change a text, add the key to your table — your entries always win. The virtual keyboard's Korean/English switch key is always shown.

| Key | English (default) | Korean (`ko`) |
|---|---|---|
| `dv.dialog.ok` `dv.dialog.cancel` `dv.dialog.yes` `dv.dialog.no` | OK / Cancel / Yes / No | 확인 / 취소 / 예 / 아니요 |
| `dv.alarm.critical` `dv.alarm.major` `dv.alarm.minor` | Critical / Major / Minor | 긴급 / 경고 / 주의 |
| `dv.alarm.none` `dv.alarm.empty` | No active alarms / No alarms to show | 활성 알람 없음 / 표시할 알람이 없습니다 |
| `dv.alarm.unacked` `dv.alarm.acked` | {0} unacknowledged / Acked {0} | 미확인 {0}건 / 확인 {0} |
| `dv.alarm.comm` | Communication fault | 통신 이상 |
| `dv.alarm.saturated` (the alarm `AlarmLog` adds when its buffer is full; other alarm messages are shown as the app wrote them) | Alarm buffer saturated: acknowledge (ACK) the unacknowledged alarms | 알람 버퍼 포화 — 미확인 알람을 확인(ACK)하세요 |
| `dv.alarm.col.time` `dv.alarm.col.priority` `dv.alarm.col.message` `dv.alarm.col.ack` | Time / Priority / Message / Ack | 시간 / 등급 / 메시지 / 확인 |
| `dv.trend.empty` | No data | 데이터 없음 |
| `dv.input.required` `dv.input.number` `dv.input.integer` `dv.input.unsigned` | Enter a value / Not a number / Only whole numbers are allowed / Negative numbers are not allowed | 값을 입력하세요 / 숫자 형식이 아닙니다 / 정수만 입력할 수 있습니다 / 음수는 입력할 수 없습니다 |
| `dv.input.range` `dv.input.overflow` `dv.input.rejected` | Allowed range: {0} to {1} / Out of the allowed range / This value is not allowed | 허용 범위: {0} ~ {1} / 입력할 수 있는 범위를 벗어났습니다 / 허용되지 않는 값입니다 |
| `dv.input.write-failed` (VM setter exception) | Could not save the value | 값을 저장하지 못했습니다 |
| `dv.picker.ok` `dv.picker.cancel` | OK / Cancel | 확인 / 취소 |
| `dv.picker.month` `dv.picker.dow` | {0}-{1:00} / Sun,Mon,Tue,Wed,Thu,Fri,Sat | {0}년 {1}월 / 일,월,화,수,목,금,토 |
| `dv.picker.hex` | Not in #RRGGBB format | #RRGGBB 형식이 아닙니다 |
| `dv.kbd.done` | Done | 완료 |
| `dv.filter.ok` `dv.filter.cancel` `dv.filter.clear` `dv.filter.all` | OK / Cancel / Clear / Select all | 확인 / 취소 / 지우기 / 전체 선택 |
| `dv.filter.empty` `dv.filter.true` `dv.filter.false` `dv.filter.more` | (Empty) / Yes / No / Too many values; showing the first {0} | (빈 값) / 예 / 아니오 / 값이 많아 처음 {0}개만 표시 |
| `dv.filter.min` `dv.filter.max` `dv.filter.from` `dv.filter.to` | Min / Max / From / To | 최소 / 최대 / 시작 / 끝 |

---

## 18. Icons (Font Awesome 6 Free)

Used by `DvIcon icon`, `DvButton icon`, `DvOption icon`, `DvSelect item-icon`, and `DvColumn icon`.

| Notation | Font |
|---|---|
| `fa-gear`, `fas fa-gear`, `fa-solid fa-gear` | Solid |
| `far fa-star`, `fa-regular fa-star` | Regular (some icons only) |
| `fab fa-windows`, `fa-brands fa-windows` | Brands |

- Names are Font Awesome 6.1.2 Free names (https://fontawesome.com/search?o=r&m=free). Unknown names are not drawn (one Trace warning).
- Size = the element's `font-size`, color = `color`.
- Use brand logos only to refer to that brand.

```xml
<DvIcon icon="fa-triangle-exclamation" style="font-size:32; color:token(tone-warning)"/>
<DvButton text="저장" icon="fa-floppy-disk" variant="primary" onclick="Save"/>
```

---

## 19. Host summary

For detailed usage, see each type's XML documentation.

| Host | Purpose | Refresh |
|---|---|---|
| `ViewWindow` (Devinno.UI.OpenTK) | OpenTK window. Touch-first (virtual keyboard). Sleeps when there is no input, using almost no CPU. `Gpu = true` draws with OpenGL | `Invalidate()`, `Repaint()` |
| `ViewControl` (Devinno.UI.WinForms) | WinForms control. `App = app`. Edits through a hidden TextBox by default; virtual keyboard in `TouchMode` | `MarkUpdated()`, `MarkRepaint()` |
| `DvSurface` | For hosts you build yourself (engine API). Drawing to a canvas and pointer input | `Invalidate()`, `Repaint()` |
| `DvRenderer` | Markup → PNG (check output without a window) | — |

```csharp
// OpenTK
using var window = new ViewWindow(app, 1280, 800, "My HMI") { Gpu = true };   // Gpu 는 선택
window.Run();

// WinForms
var view = new ViewControl { Dock = DockStyle.Fill, TouchMode = false, App = app };
form.Controls.Add(view);
```

- Both adapters take a single `DvApp` (chapter 16): `new ViewWindow(app, width, height, title)` / `ViewControl.App`. `ViewWindow(markup, vm …)`, which took markup and a VM directly, and `ViewControl.Surface` have been removed.
- The adapter decides the size of the window/control (the `DvApp` areas divide the space inside it).
- **Display scaling (DPI):** px in markup are **logical px** (the same as CSS px). When the Windows display scale is 125% or 150%, both adapters scale drawing up accordingly, so text and widgets appear at the same physical size as on a 100% display, only sharper. Pointer coordinates, `Bounds` (chapter 20), and the `width`/`height` of the `ViewWindow` constructor are also logical px (at 150%, a `1280×800` window is 1920×1200 physical). If the scale changes at runtime (moving to a monitor with a different scale, changing the display scale), the screen is redrawn at the new scale immediately, and `ViewWindow` also grows or shrinks the window to the new scale to keep its logical size. WinForms receives per-monitor scaling when the app is `PerMonitorV2` (`<ApplicationHighDpiMode>`), and resizing the window (Form) for the scale is the form's job — for a form built in code, do it like designer code: `SuspendLayout()` → `AutoScaleDimensions = new SizeF(96, 96)`, `AutoScaleMode = AutoScaleMode.Dpi`, `ClientSize` (at 100%) → `ResumeLayout(false)` (otherwise, at 150% the window stays the same while the content grows and gets clipped). On a 100% display (the typical HMI panel), nothing changes from before.
- Localization is `app.Strings` (the old `ViewWindow.Strings` is gone); the theme is `app.Theme`.
- An app attaches to only one adapter at a time, and only one app per process can be attached to an adapter (attaching while another app is attached throws `InvalidOperationException`). Changing `ViewControl.App` or closing the window detaches the previous app. Closing the window or calling `Dispose` on the control closes open dialogs as canceled; merely switching the app leaves dialogs open (21.6).
- All three hosts report VM code exceptions through the `HandlerError` event (6.7). With `DvApp`, `app.HandlerError` alone is enough.

---

## 20. Working with controls from code (`id`, `vm`)

Controls given a name (`id`) in markup can be used directly inside the VM as **typed fields**. The source generator creates the field declarations at build time, so the only files you write are the dvml and the VM. A misspelled name is a compile error before you ever run the app.

```xml
<!-- Pages/MainPage.dvml -->
<DvStack vm="MyApp.MainPage">
  <DvLabel  id="lblTemp" class="title" text="{Temperature} ℃"/>
  <DvButton id="btnStart" text="시작" onclick="Start"/>
</DvStack>
```

```csharp
// MainPage.cs — 사용자가 쓰는 유일한 C# 파일. DvPage(또는 DvBar)를 상속하면 페이지가 곧 VM
public partial class MainPage : DvPage
{
    public double Temperature { get; set; }

    partial void OnLoaded()                   // 화면에 처음 연결된 뒤 1회 (이때 App 도 채워져 있음)
    {
        btnStart.Click += (s, e) => { /* ... */ };
    }

    public void Start()
    {
        btnStart.Enabled = false;
        lblTemp.Text = "가동 중";
        var r = btnStart.Bounds;              // SKRect, 창 좌표
    }
}

// 앱 시작 — 마크업 연결은 생성기가 합니다
var app = new DvApp { Theme = DvTheme.Dark };
app.Pages.Add(new MainPage());
new ViewWindow(app, 1280, 800, "My HMI").Run();
```

### 20.1 Attributes and what is generated

- `id="이름"` — can go on any element. It must be a C# identifier (starts with an ASCII letter or `_`, no keywords) and be unique within one dvml. It has no effect on appearance.
- `vm="네임스페이스.클래스"` on the root element — the VM that uses this dvml. It must be a top-level (not nested), non-generic `partial class`. **One dvml per VM.** For `DvApp` pages (`DvPage`) and areas (`DvBar`), each one's own class is its VM, and when you inherit from them the generator also creates the markup hookup (the `MarkupSource` override) (chapter 16). A dvml without `vm` generates nothing and is used as before.
- What the generator adds to the VM (`MainPage.g.cs`; do not edit by hand)
  - `public const string Markup` — the dvml source text. For `DvPage`/`DvBar` the app uses this value automatically; when you use `DvSurface`/`DvRenderer` directly, pass this value so the generator and runtime use the same source and never diverge.
  - One **private** field per `id`, named the same as the `id`. Its type is the handle with the same name as the tag — `DvLabel`, `DvButton`, value widgets (`DvToggle`, `DvSwitch`, `DvCheckBox`, `DvRadio`, `DvKnob`, `DvSlider`, `DvInput`, `DvSelect`, `DvSegment`, `DvListBox`, `DvExpander`, `DvTreeView`, `DvDateTimeInput`, `DvColorInput`, 20.9). All other tags = `DvControl`.
  - `partial void OnLoaded()` — implementing it is optional.
- If an `id` matches an existing VM member, the C# compiler reports a duplicate definition error.

### 20.2 Handles (`DvControl`)

Namespace `Devinno.UI.Controls`.

| Member | Description |
|---|---|
| `Id` | The `id` from markup |
| `Tag` | Any value the app wants to attach (`object?`) |
| `Enabled` | Whether it is enabled. Writing it overrides `disabled` in markup |
| `Visible` | Whether it is visible. Writing it overrides `visible` in markup |
| `Bounds` | The area (`SKRect`, logical px — see display scaling in chapter 19). In **window (top-level screen) coordinates** |
| `Click` | `EventHandler<DvClickEventArgs>`. `Position` = click position (window coordinates) |
| `ResetOverrides()` | Clears all values written from code and reverts to the markup values |
| `DvLabel.Text`, `DvButton.Text` | Text. Writing it overrides `text` |
| Value members (`Checked`, `Value`, `Text`, `SelectedIndex` …) | Read/write a value widget's value and its change events — 20.9 |

- **When handles are connected.** Inside the VM constructor, the fields are `null`. When the screen (`DvSurface`) is created, the fields are filled in and `OnLoaded()` is called. Do not use handles in the constructor; use them from `OnLoaded()` on.
- **Reattaching.** The same VM can be attached to a new screen again (e.g. when you recreate a `DvSurface` with the same VM). `DvApp` creates the page/area screens once and keeps them, so changing the theme or language only rebuilds the same screen (16.7). The handle objects created the first time are reused, and values written from code and `Click` subscriptions are kept. `OnLoaded()` is called **only once, the first time**, for that VM instance (so subscriptions are not added twice). If the same VM is attached to two screens at once, the later screen is the current one.
- **When markup and VM don't match.** If you pass different markup to an `IDvControlHost` VM via `DvSurface` and an `id` is missing or has a different tag type, there is no exception: a Trace warning is logged for each such `id` (again on every reattach), and that handle becomes "not connected" (`Bounds` = `SKRect.Empty`, writes are only stored, `Click` is never raised). `DvApp` pages/areas always use their own dvml, so this does not happen on the normal path; when using `DvSurface` directly, pass the `Markup` constant.

### 20.3 Writing values — if code writes it, code owns it

- `btnStart.Enabled = false` and `lblTemp.Text = "…"` **replace** the markup value (whether a literal or a `{binding}`). After that, even if the VM property changes, that property keeps the value written by code.
- Written text is **literal text**. Even if it contains `{` `}`, it is not resolved as a binding.
- Reverting: `Text = null` reverts only the text; `ResetOverrides()` reverts `Enabled`, `Visible`, and `Text` all to the markup values.
- Written values persist across reattaching to a new screen (changing the theme will not unlock a button you disabled).
- Writing values is **safe from any thread** and sets a flag telling the screen to rebuild (the same flag as `DvSurface.Invalidate()`).
  - For **`DvApp` pages/areas**, the write also wakes the adapter (window/control) immediately (`DvApp.RenderRequested`). Even `btnStart.Enabled = false` from a timer or communication thread shows up on the very next frame, so there is no need to call `Invalidate()` separately. Likewise, values written inside a click handler are redrawn automatically.
  - A **host that uses `DvSurface` directly** only sets the flag and **does not wake** the host. If you wrote from a timer or communication thread, call the host's `Invalidate()` just as you would after changing a VM value (`DvSurface.Invalidate()`, then wake the render loop). If you don't, the change appears the next time something else triggers a draw.
- If a `Click` subscription or a write requires a rebuild, it takes effect on the next layout.

### 20.4 Reading — based on the last completed layout

- Writing `false` to `Enabled`/`Visible` reads back as `false` immediately. All other reads reflect the actual state from the last completed layout. So after writing `true`, it reads as `true` only once the next layout completes. If an ancestor is disabled or hidden, `Enabled`/`Visible` read as `false` regardless of the element's own value.
- If the element is not yet laid out or not connected, the read returns the last written value (`true` if none).
- `Text` is the overridden value; otherwise the markup/binding value from the last layout; otherwise `null`.
- `Bounds` is `SKRect.Empty` when `visible` is false, before layout, or when not connected. Inside a scroll box it reflects the current scroll position; for a `DvApp` area/page screen it reflects where that slot is drawn in the window.
- `Bounds` is **safest to read on the UI thread**. Reading it from another thread while the window is resizing may mix before and after values.

```csharp
// 버튼 바로 아래에 안내 상자를 띄우기
var r = btnStart.Bounds;
PopX = r.Left;  PopY = r.Bottom + 4;  ShowHint = true;
Invalidate();   // 호스트의 Invalidate (6장)
```

`left`/`top` of `position:absolute` are **relative to the parent's content box** (3.5). To use `Bounds` values as-is, place the box under a root without padding. See the example `samples/Devinno.UI.Demo/Pages/ControlPage.dvml`.

### 20.5 Click event

- Raised on press and release. Hit testing is the same as `onclick` (pressed element = released element, dragging 8 or more scrolls instead, not on disabled elements).
- If the same element also has `onclick`, `onclick` runs first, then the `Click` subscribers.
- **Subscribing makes the element a click target.** Even a `DvBox` without `onclick` receives clicks once you subscribe to `Click`, and the click does not leak to an outer element's `onclick` (6.4 bubbling).
- Subscriptions take effect from the **next layout** (subscribing/unsubscribing requests a redraw).
- Not raised when disabled (including disabled inherited from ancestors).
- Raised **only on the plain click path**. It is not raised on elements with their own behavior, such as toggle, input, select, date, color, grid, and alarm widgets.
- If a subscriber throws, it is reported via `HandlerError` (`Kind` = `"Click"`) and the screen keeps running. **Subscribers remaining after it in that event are not called** (default C# event behavior).
- It is called on the same input-processing thread as `onclick`.

### 20.6 Where it cannot be used

- An `id` inside a `DvRepeat`/`DvComponent` template does not create a handle (it is cloned once per item/instance). Warning DVML004 is reported. Handle such elements with bindings and an `onclick` method that takes the item as its parameter (`onclick="Select"` → `public void Select(Item item)`, 6.4).
- An `id` on a component **instance** (`<PumpCard id="p1"/>`) works. `Bounds` and overrides apply only to that instance.

### 20.7 Build diagnostics

Problems in each dvml are reported at compile time. The location points inside the dvml file.

| Code | Severity | Meaning |
|---|---|---|
| DVML001 | Error | dvml could not be read as XML (syntax error) |
| DVML002 | Error | Duplicate `id` within the same dvml |
| DVML003 | Error | `id` that is not a valid C# identifier |
| DVML004 | Warning | `id` inside `DvRepeat`/`DvComponent` — cloned, so no field is created |
| DVML005 | Error | `vm` type not found |
| DVML006 | Error | `vm` type is not `partial` |
| DVML007 | Error | More than one dvml specifies the same `vm` |
| DVML008 | Error | Process diagram: the equipment/instrument name referenced by `DvLine`, `DvSignal`, or `DvInstrument on` does not exist |
| DVML009 | Error | Process diagram: nonexistent port, port required but not chosen, port/endpoint on a signal line, connecting directly from outside to equipment inside a unit |
| DVML010 | Error | Process diagram: duplicate equipment/instrument name (within the process diagram or a unit) |
| DVML011 | Warning | Process diagram: nonexistent value (`kind`, `medium`, `rotate`, `direction`, `look`, `align`) — default used at runtime |
| DVML012 | Warning | Nonexistent style property name in `style`, `*-style`, or `<DvStyle>` (e.g. `colr`, or unsupported ones like `margin`) — silently ignored at runtime. Suggests a similar name if one exists |
| DVML013 | Warning | Name in `token(이름)`/`var(--이름)` not in the built-in theme — magenta at runtime |
| DVML014 | Warning | `class` name not found in built-in classes or any `<DvStyle>` in the project's dvml — silently ignored at runtime |
| DVML015 | Warning | `DvRadio` without `option` — selects no value and never turns on |
| DVML016 | Warning | `DvListBox` value attribute does not match the mode — `value` with multi-select (`selection-mode="multi"`), `selected-items` with single-select (ignored at runtime) |
| DVML017 | Warning | Value not allowed for an attribute that accepts fixed values — `DvListBox selection-mode` (single, multi), `DvSlider direction` (right, left, up, down). Default used at runtime |
| DVML018 | Error | Handler attribute (`onclick`, `onbeforecommit`, `onack`, `onrowclick`) that is not the name of a public instance method of the VM — misspelled, private or static method, binding (`{…}`), or call form (`M(item)`). At runtime the handler would silently never run |

DVML012–017 are checked for all dvml, including those without `vm` (DVML016/017 are skipped when the value is a binding `{…}`). A style that is entirely a binding (`style="{S}"`) is skipped; for declarations where only the value is bound (`color:{C}`), only the name is checked. Token names built from component parameters (`token(tone-{tone}-fg)`) and bound classes (`class="{C}"`) are skipped. **Themes created in C# (`new DvTheme(토큰, 스타일시트)`) and `<DvStyle>` of components registered in code are not visible at build time** — apps that use such tokens/classes turn the check off in `.editorconfig` with `dotnet_diagnostic.DVML013.severity = none` (`DVML014` for classes).

DVML018 checks dvml with `vm` only (the VM type is the target), including handlers inside `DvRepeat` and on `DvColumn`. Only the name is checked — a public instance method with that name on the VM type or its base classes; parameter rules are in 6.4 and 6.5. Skipped: inside a `<DvComponent>` definition (which screen VM it runs on is not known at build time) and `onclick` on instances of a project-dvml component that declares an `onclick` parameter. A component registered in C# that declares an `onclick` parameter is not visible at build time — if you use one, turn the check off with `dotnet_diagnostic.DVML018.severity = none` in `.editorconfig`.

Messages are in English. When the compiler's UI language is Korean (Korean Visual Studio, or `dotnet build` on a Korean OS), they are shown in Korean.

DVML008–011 also check dvml without `vm`, and errors do not block VM code generation. Only what is certain at build time is checked — names/values containing bindings (`{…}`) and equipment whose ports are unknown at build time (`DvSymbol` registered in code, component units) are skipped and reported via Trace at runtime (22.8).

### 20.8 Project setup

- When you reference the NuGet package (`Devinno.UI`), `*.dvml` files are automatically included as `AdditionalFiles` and the generator is attached. Nothing else to do.
- When referencing via `ProjectReference` inside the repository, the package props do not apply, so wire up the generator as an analyzer and the dvml as `AdditionalFiles` yourself.
- **NativeAOT and trimming are not supported.** Bindings (`{속성}`), `onclick`, and grid `field` look up properties/methods by name at runtime (reflection). Publish the app as a normal JIT app (enabling `PublishAot`/`PublishTrimmed` can leave bindings empty or handlers uncalled).

```csharp
// *.csproj (ProjectReference 방식)
// <ProjectReference Include="..\..\src\Devinno.UI.Generators\Devinno.UI.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
// <AdditionalFiles Include="Pages/*.dvml" />
```

See the example `samples/Devinno.UI.Demo/Devinno.UI.Demo.csproj`.

### 20.9 Value handles — reading and writing values from code

Giving an `id` to a widget that has a value creates a handle for that value. Use it like WinForms' `chk.Checked = false`.

| Element | Handle | Value member | Change event |
|---|---|---|---|
| `DvToggle`, `DvSwitch`, `DvCheckBox` | Same name | `bool Checked` | `CheckedChanged` |
| `DvRadio` | `DvRadio` | `bool Checked` (whether the group value is `option` — turning it on turns the other buttons off; it cannot be turned off for a property that cannot take null) | `CheckedChanged` (also when turned off) |
| Latching `DvButton` (has `value`) | `DvButton` | `bool Checked` | `CheckedChanged` |
| `DvKnob`, `DvSlider` | Same name | `double Value` | `ValueChanged` |
| `DvInput` | `DvInput` | `string? Text`, plus `double? Value` for numeric input | `TextChanged`, followed by `ValueChanged` for numeric input |
| `DvSelect`, `DvSegment` | Same name | `int SelectedIndex` (none = -1), `object? SelectedValue` | `SelectionChanged` |
| `DvExpander` | `DvExpander` | `bool IsExpanded` | `ExpandedChanged` |
| `DvTreeView` | `DvTreeView` | `object? SelectedNode` (the selected item object — writing it expands the ancestor branches) | `SelectionChanged` |
| `DvListBox` | `DvListBox` | `SelectedIndex`, `SelectedValue` (the first one if multiple), `IReadOnlyList<object?> SelectedItems` (writing selects those values) — for multi-select, `selected-items` decides the owner | `SelectionChanged` |
| `DvDateTimeInput` | Same name | `DateTime? Value` | `ValueChanged` |
| `DvColorInput` | Same name | `SKColor? Value` | `ValueChanged` |

**A value has exactly one owner.** The element's `value` attribute decides who holds the value.

| Markup | Owner | Read via handle | Write via handle | Change on screen |
|---|---|---|---|---|
| `value="{Bind X}"` | VM property `X` | The value of `X` | Writes to `X` (converted to the property type). Does not call `onbeforecommit` | Writes to `X`, as before |
| No `value` + `id` | Handle | The value the handle holds (initially `false`/0/empty) | Stored in the handle | **Yes** — stored in the handle (`onbeforecommit` still checks it) |
| `value="{X}"` (one-way) or a fixed value | Markup | The displayed value | **Overrides** the on-screen value (revert with `ResetOverrides()`) | No (read-only, as before) |

- The second row is the way to use a value without a binding. An input widget with neither `id` nor `value` is read-only, as before.
- Handling one value through both a binding and a handle never gets out of sync. With a binding, the handle is just a window for reading and writing the VM property.
- **Change events** are raised only when the value actually changes, in all three cases. Distinguish them with `e.ByUser`.
  - Changed on screen (press, drag release, edit finished, selection) — `ByUser = true`, on the input-processing thread.
  - Code writes to the handle — `ByUser = false`, immediately **on the thread that wrote**. Handler exceptions propagate directly to the writing code.
  - Code changes a bound VM property directly and the screen is rebuilt (the next layout after `Invalidate()`) — `ByUser = false`.
  - Exceptions thrown by handlers on the screen-side paths are reported via `HandlerError` (`Kind` = event name, `Member` = `id`).
  - The value at first attachment to the screen is not a "change".
- `DvInput` changes **once when editing finishes**, not on every keystroke. Writing non-numeric text to `Text` on a numeric input throws `FormatException`.
- `SelectedIndex` is based on the options from the last layout. If written before layout (inside `OnLoaded()`), it is applied on the first layout. Out of range throws `ArgumentOutOfRangeException` (after layout).
- Values written from code to `Value` of `DvKnob`/`DvSlider` are not adjusted by `step` or `min`/`max` (only values turned on screen are adjusted).
- `DvDateTimeInput.Value` reads as `DateTime` even when the bound property is `DateOnly`/`TimeOnly`, and converts to that type on write. `DvColorInput.Value` also reads a string property (`#RRGGBB`) as a color and writes it back as `#RRGGBB`.
- Writes also work from other threads (same as 20.3 — a redraw is scheduled).
- Change events are `EventHandler<DvValueChangedEventArgs>` (`(sender, e) => … e.ByUser`).
- Handles of elements that are **not built right now** (content of another tab, a collapsed `DvExpander`) still work: values and `Enabled`/`Visible` written to them are kept and shown when the element is built; `Bounds` is empty meanwhile.
- **Display-only widgets have no value handle** (`DvProgress`, `DvLamp`, `DvGauge`, `DvRing`, `DvTank`, `DvStepBar`, charts): their `id` gives a plain `DvControl` (`Enabled`, `Visible`, `Text`, `Bounds`). Drive their values with a one-way binding to a VM property (`value="{Speed}"`) and call `App.Invalidate()` after changing it — this is the normal pattern even on pages that use handles for everything else.
- `DvSwitch` (a built-in component) is the same value handle as `DvToggle` (omit `value` and give only `id` to make the handle the owner). Its `Bounds` covers the whole row including the label. User components have no value handle (plain `DvControl`).

```csharp
partial void OnLoaded()
{
    tgPump.CheckedChanged += (_, e) => Log($"펌프 {tgPump.Checked} ({(e.ByUser ? "화면" : "코드")})");
    segMode.SelectedIndex = 0;      // 배치 전 — 첫 레이아웃 때 적용
}
public void Stop() { tgPump.Checked = false; knSpeed.Value = 0; }
```

See the example `samples/Devinno.UI.Demo/Pages/BasicPage.dvml` (run argument `basic`).

---
## 21. Dialogs (DvDialog·ShowDialog·MessageBox)

A dialog is a window that floats above the page and collects an answer from the user (recipe editing, "Really delete?", communication alarms). Like pages, **dialogs are created, held, and closed by `DvApp`.**
App code asks "show this dialog with these arguments" and receives the result returned on close via `await`.

| Name | Role |
|---|---|
| `DvDialog<TResult>` | One dialog. Built like a page with a `partial class` + dvml; the class itself is the VM. `TResult` is the type of the result it returns |
| `<DvDialogFrame>` | Root tag of a dialog's dvml. The theme draws the background, border, shadow and title |
| `App.ShowDialog<TDialog, TResult>(인자…)` | The app creates an instance through the constructor matching the arguments, shows it on top, and returns a `Task<TResult?>` that yields the result |
| `App.MessageBox(글, 제목, 버튼)` | Built-in message box. Returns the button pressed (`DvDialogResult`) |
| `App.InputBox` · `App.InputNumber` · `App.SelectBox` | Built-in boxes. Ask for one string, one number, or one item from a list and return that value (`null` on cancel, 21.5) |
| `App.Dialogs` | List of open dialogs (bottom → top). `Count`, indexer, `Top` (topmost) |
| `App.CloseDialogs()` | Closes all open dialogs as canceled |
| `DvApp.Current` | The way to reach the app from outside a page (communication classes, etc.) |
| `App.Lock<TDialog>()` · `App.Unlock()` · `App.IsLocked` | Lock layer — shows an app-made login dialog above all dialogs and blocks input below until unlocked (21.8) |
| `App.IdleTimeout` · `App.Idle` | Idle detection — one event when there is no input for the set time (21.8) |

All are in the `Devinno.UI.App` namespace. The smallest example is `SetpointDialog` (setpoint temperature editing) in 16.1.

### 21.1 Flow at a glance

Pressing the "Change setting" button in 16.1 proceeds as follows.

1. `EditSetpoint()` calls `App.ShowDialog<SetpointDialog, double?>(Setpoint)`. The app immediately creates `new SetpointDialog(25.0)` and puts it on top of the list (`App.Dialogs`). From this moment the page below receives no presses.
2. On the next frame the dialog's view is built and drawn at the center of the window. A translucent overlay (`scrim`) covers what is below.
3. When the user edits the value and presses "OK", `Ok()` sets the result with `Close(Value)`. The dialog is removed from the list immediately.
4. On the next frame the view is detached, and `await` receives the result (`30.0`) and continues. If canceled, it is `null`.

### 21.2 Defining

Same approach as pages (chapter 16). Write two files, a class and dvml, and connect them with `vm` on the dvml root.

```xml
<DvDialogFrame vm="MyApp.RecipeEditDialog" title="레시피 수정" subtitle="값을 고친 뒤 확인을 누르세요" style="width:420">
  <DvInput value="{Bind Name}" placeholder="이름"/>
  <DvInput value="{Bind Temp}" keyboard-type="numeric" min="0" max="200" format="0.0" unit="℃"/>
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="secondary" text="취소" onclick="Cancel"/>
    <DvButton variant="primary" text="확인" onclick="Ok"/>
  </DvRow>
</DvDialogFrame>
```

```csharp
namespace MyApp;

public sealed record Recipe(string Name, double Temp);
```

```csharp
using Devinno.UI.App;

namespace MyApp;

// 원본 Recipe 를 직접 고치지 않고 값을 복사해 편집한다 — 취소하면 원본이 그대로다.
public sealed partial class RecipeEditDialog : DvDialog<Recipe>
{
    public RecipeEditDialog(Recipe source)
    {
        Name = source.Name;
        Temp = source.Temp;
    }

    public string Name { get; set; }
    public double Temp { get; set; }

    public void Ok() => Close(new Recipe(Name, Temp));
    public void Cancel() => Close();        // 결과 = null(취소)
}
```

**`<DvDialogFrame>` attributes**

| Attribute | Meaning |
|---|---|
| `title` | Title (bold). If absent, there is no title row at all |
| `subtitle` | Dimmed text below the title. Shown only when `title` is present |
| `width` / `height` in `style` | Dialog size. If absent, the content size |
| Children | Stacked vertically (gap `md` 12; horizontally they fill the dialog width) |

- The appearance is set by the built-in class `.dialog` (background `surface-overlay`, border `border`, radius 12, shadow `shadow.lg`, padding 20). It can be overridden with `style`·`class` (chapter 7, "What you can give an instance"). All attributes are bindable (`title="{Caption}"`).
- **Size limit**: a dialog never grows larger than the window minus 16 on each side. Text is measured with wrapping within window width − 32 (3.8), and re-wrapped when the window shrinks. Content that overflows the height is clipped, so put long content in an `overflow:scroll` box (3.7) — when laid out in a height-clipped slot, the scroll box shrinks so the rest (button row, etc.) stays inside the slot. Example: `style="max-height:320; overflow:scroll"`, or a box with only `overflow:scroll` and no `max-height` (content height if the content fits in the window, otherwise shrinks to fit the slot — the body of the built-in message box works this way). The position is always the center of the whole window (it also covers fixed areas such as the top bar), and it re-centers when the window size changes.
- **Use `DvDialogFrame` as the root.** Other tags (`DvStack`, etc.) can also be the root and are centered at their size, but without a background they are **transparent, so what is below shows through.**
- **`vm` is `namespace.class`** (`vm="MyApp.RecipeEditDialog"`). Only a class declared without a namespace (the sample in 16.1) uses the bare class name. If wrong, build error DVML005 (20.7).
- The class is a `partial class` deriving from `DvDialog<TResult>` and MUST have a **public constructor**. The generator creates the markup connection, `id` fields and `OnLoaded()` (chapter 20). `DvInput`·`DvSelect`·pickers·the virtual keyboard inside a dialog behave exactly as on a page, and popups appear above the dialog.
- The base class has no properties such as `Id`·`Title`. The title is the dvml `title`; if you need a key to tell dialogs apart, put it on the class yourself (21.7 pattern 3).
- What a dialog can use: `App` (the app that showed it — in handlers·`OnLoaded`. **Reading it inside the constructor throws `InvalidOperationException`**), `IsOpen`, `protected Close(result)`·`Close()`, and the overridable `protected virtual void OnClosed()` (for cleanup after closing).

### 21.3 Showing and results

Signature: `Task<TResult?> ShowDialog<TDialog, TResult>(params object?[] args) where TDialog : DvDialog<TResult>`. Callable from any thread.

**Constructor selection** — the app picks the one public constructor whose **parameter count equals `args` and to whose parameter types each argument is directly assignable**, and creates the instance on the thread that called `ShowDialog`.

- If there is no matching constructor or more than one, `ArgumentException` is thrown on the spot (the message lists the passed argument types and the available constructors). The dialog is not registered. An exception thrown by the constructor also propagates as-is to the caller.
- No numeric conversion is done. Passing `25` (int) to a `double` parameter does not match, so match the type, as in `25.0`. Optional parameters (default values) are not filled in either, so pass the full count.
- `null` matches only reference-type and nullable-type parameters. `ShowDialog<X, R>(null)` means "one argument that is null".
- Passing one array (`string[] names`) makes that array one argument. **To pass a single `object[]`, wrap it as `(object)items`** — unwrapped, the array's elements are spread into the argument list.
- **In the constructor, only store the received values in fields·properties.** The constructor may run on any thread, such as a communication thread; there is no view yet, and `App` is still empty. Put setup code in `OnLoaded()` (once after the view exists, input thread).

**Results**

- If the dialog calls `Close(result)` the result is that value; `Close()` gives `default` (cancel).
- If closed from outside (`CloseDialogs()`, the dialog below closes, the window closes), the result is always `default`. Therefore **use a result type in which `default` is distinguishable as "cancel".**

| Result type | On cancel | Use |
|---|---|---|
| Reference type (`Recipe`, `string`) | `null` | Returning edited data |
| `bool?` | `null` | Yes/No + cancel |
| `int?`, `double?` | `null` | Chosen number, entered number |
| `DvDialogResult` | `Cancel` (value 0) | The button pressed |
| ~~`bool`, `int`, `double`~~ | `false`, `0` | **Do not use** — "No"·"entered 0" cannot be distinguished from cancel |

- The result is delivered **exactly once**, whatever the closing path. Calling `Close` twice keeps the first value as the result and ignores the rest. `Close` is callable from any thread.
- The result is carried on the app's frame. So there are two cases where `await` keeps waiting: an **app never attached to a window** waits until its first frame, and a **detached app** (WinForms `ViewControl.App` switched to another app) waits until it is attached again. `ShowDialog` called on an already **closed app** does not wait; it completes immediately with a cancel result on the calling thread.
- `Close` is `protected`, so only the dialog itself can call it. The only public way to close from outside is `App.CloseDialogs()` (all); if only a specific dialog must be closed, use 21.7 pattern 5.
- `ShowDialog` does not return the instance. If you need the instance outside the dialog, find it in `App.Dialogs`.

**After `await` — on which thread, and what to do**

- If you `await` in a page·area·dialog handler (`onclick`, etc.), execution continues **on the input (UI) thread** in the frame where the result arrived. You can use the VM directly.
- VM values changed after `await` are **redrawn without separate notification.** In the frame where the continuation runs, the app re-reads the current page·areas·open dialogs (16.1 `EditSetpoint`). Values changed from timer·communication threads still need `App.Invalidate()` as before.
- If you `await` where there is no synchronization context, such as a communication thread, execution continues on the input thread that delivered the result. Long work after that (waiting for a communication response, saving a file) freezes the screen meanwhile, so **hand it off with `Task.Run`.**
- Handlers may be written as `async void`. Exceptions inside them do not kill the process; they arrive at `App.HandlerError` (`Kind` = `"async"`, `Member` = null) (6.7). `async` methods started from a communication thread do not get this protection, so write them to return `Task` and handle exceptions yourself.

### 21.4 Message box

Signature: `Task<DvDialogResult> MessageBox(string text, string? caption = null, DvMessageButtons buttons = DvMessageButtons.Ok)`.

| `buttons` | Buttons (left → right) | Result |
|---|---|---|
| `Ok` (default) | [OK] | `Ok` |
| `OkCancel` | [Cancel] [OK] | `Cancel` · `Ok` |
| `YesNo` | [No] [Yes] | `No` · `Yes` |
| `YesNoCancel` | [Cancel] [No] [Yes] | `Cancel` · `No` · `Yes` |

- The main button (OK·Yes) is the rightmost `primary` button.
- When closed from outside, the result is `DvDialogResult.Cancel`. This also applies to `Ok`·`YesNo` boxes that have no cancel button, so when checking for "Yes", compare with `== DvDialogResult.Yes` (comparing with `!= No` mistakes a close from outside for "Yes").
- If `caption` is `null`, there is no title row. The body can be split into lines with `\n`.
- The width defaults to 360; long text widens it up to window width − 32 and then wraps within it (font size is the theme's `font.body`; the body is a single label — 3.8). If the window size changes while the box is shown, it re-wraps to that width. If the text is taller than the window, only the body scrolls inside the box and the button row stays in the box (short text has no scrollbar).
- Button text can be changed through localization (chapter 17) keys `dv.dialog.ok` `dv.dialog.cancel` `dv.dialog.yes` `dv.dialog.no`; if absent, the built-in English (or Korean with `Language = "ko"`) texts OK·Cancel·Yes·No are used.
- Each call creates a new box that is stacked. Showing several in a row does not overwrite one another; each returns its own result. Callable from any thread.

### 21.5 Built-in boxes — text·number input, choosing from a list

When asking for **just one value**, such as a name, a setting, or a mode, **you do not need to build a dialog yourself.** The app includes three boxes.
Build one yourself as in 21.2 only when editing several values together or when validation·preview is needed.

| Signature | On OK | Appearance |
|---|---|---|
| `Task<string?> InputBox(string caption, string? value = null)` | The entered text | Title + one input box + [Cancel] [OK] |
| `Task<double?> InputNumber(string caption, double value, double? min = null, double? max = null)` | The entered number | Title + one numeric input box + [Cancel] [OK] |
| `Task<int?> SelectBox(string caption, IReadOnlyList<string> items, int selected = -1)` | Index of the chosen item | Title + item list + [Cancel] [OK] |

**Common**

- **A `null` result means cancel.** That is, the cancel button was pressed, or the box was closed from outside (`CloseDialogs()`, the dialog below closes, the window closes). Receiving it with `is { } value` as in the example below naturally skips the cancel case. Do not erase the null with `?? 0` or `.Value`; cancel would become 0 or an exception.
- The width is fixed at 360 and the title is `caption`. OK is the rightmost `primary` button, and button text uses the same localization keys as the message box, `dv.dialog.ok`·`dv.dialog.cancel` (if absent, OK·Cancel).
- Like the message box, callable from any thread; each call stacks a new box that returns its own result. Thread rules after `await` are the same as 21.3.

**Input boxes (`InputBox`·`InputNumber`)**

- **Editing starts as soon as the box appears.** In touch mode (virtual keyboard) the keyboard comes up immediately, so the user does not need to press the input box again. `InputNumber` shows the numeric keypad. If another dialog appears on top at the same time, editing starts when that dialog closes and the input box becomes topmost (a box buried below is not edited).
- **Done (Enter) on the keyboard is OK.** When input is accepted with the virtual keyboard's Done key, a physical keyboard Enter, or Enter in the native editor (`UseNativeEditor = true`), the box closes immediately with that value. This is so operators in the field do not have to press "Done → OK" twice.
- Pressing the on-screen OK button while the virtual keyboard is shown counts, as on any other screen, as a "press outside the keyboard": **editing is canceled and that press is not treated as OK** (the box stays, and the value is the pre-edit value). In touch mode, confirm with the Done key. In native editor mode, pressing OK while editing first commits the typed text and then confirms.
- If `InputBox`'s `value` is `null`, it starts empty. Confirming with an empty field gives `""`, which is different from cancel (`null`).
- `InputNumber`'s `min`/`max` follow the `DvInput keyboard-type="numeric"` rules exactly. Input that is not a number or is out of range is rejected (error shown, editing continues) and not written to the value, so **the OK result is always within range.** Pressing Done on an out-of-range value does not close the box. If `min`·`max` is `null`, that side has no limit.
- `InputNumber`'s initial `value` must also be within range. If `value` is NaN·infinity, `min > max`, or `value` is outside `min`~`max`, `ArgumentOutOfRangeException` is thrown at the call site and the box is not shown (the boundary values `value == min`·`value == max` are allowed). If a stored value may be out of range, clamp it with `Math.Clamp` before passing it.

**Select box (`SelectBox`)**

- Items appear one per row (height 40 — an item longer than the box width wraps and the row grows accordingly), and pressing a row changes it to the selection color. Only **one** can be chosen (there is no multi-select).
- **OK can be pressed only when a row is selected.** `selected` is the initially selected index; if it is -1 (default) or out of range, nothing is selected at start.
- If `items` is empty, `ArgumentException` (`ArgumentNullException` if `null`) is thrown at the call site and the box is not shown. If the list may be empty, check before calling.
- `items` is copied at the moment of the call. Changing the list afterwards is not reflected in the box, and the result index refers to the list as of the call (if two items have the same text, the index of the chosen row).
- The list area has a maximum height of 320 and scrolls when it overflows. With few items, the list is as tall as the items. If the window is short, the list shrinks further so the buttons stay visible in the window (scroll box shrink, 3.7), and it adjusts in place when the window size changes while the box is shown.

Example — rename, change temperature, and choose mode added to the recipe page of 21.7 pattern 1 (another file of the same `partial class`).

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class RecipePage
{
    public string[] Modes { get; } = ["자동", "수동", "정지"];
    public int Mode { get; private set; }

    public async void RenameRecipe()
    {
        if (Current is not { } recipe)
            return;
        if (await App.InputBox("레시피 이름", recipe.Name) is { } name)        // null = 취소
            Current = recipe with { Name = name };
    }

    public async void ChangeTemp()
    {
        if (Current is not { } recipe)
            return;
        if (await App.InputNumber("설정 온도(℃)", recipe.Temp, 0, 200) is { } temp) // 항상 0~200
            Current = recipe with { Temp = temp };
    }

    public async void ChooseMode()
    {
        if (await App.SelectBox("운전 모드", Modes, Mode) is { } index)
            Mode = index;
    }
}
```

### 21.6 Lifecycle and input

| Stage | When · which thread | What happens |
|---|---|---|
| `ShowDialog` call | Immediately · calling thread | Constructor runs → registered on top of `App.Dialogs` (`IsOpen` = true). From now on, the page·areas below receive no presses·wheel |
| View creation | Next frame · input thread | Values being edited on the page·areas are **committed** and open popups close. Dialog view is created → `OnLoaded()` once |
| Open | Input thread | Only the topmost dialog receives input. Changing theme·language rebuilds it like a page |
| `Close` · `CloseDialogs` | Immediately · any thread | Result fixed, `IsOpen` = false, removed from `App.Dialogs` at once. Dialogs stacked above are first closed as canceled |
| Cleanup | Next frame · input thread | View detached and **values being edited are discarded** (the result is already fixed) → `OnClosed()` → result delivered (`await` continues). With several, top → bottom order |
| Window closes | Immediately · thread closing the window | All open dialogs close as canceled (values being edited are discarded and popups close; `OnClosed()` also runs on this thread). Code `await`ing in handlers also continues immediately on this thread. After that, `ShowDialog` calls no constructor and immediately returns a cancel result on the calling thread |

- **Dialogs stay shown when the page changes.** A dialog is owned by the app, not the page. If it must close, app code closes it with `CloseDialogs()` or similar.
- `ShowDialog` called **before attaching to a window** waits and appears on the first frame (a login dialog at startup, etc.). `await` waits until then too.
- Switching WinForms `ViewControl.App` to another app is "detaching", not closing. Dialogs remain and appear as they were when that app is attached again; meanwhile code `await`ing their results keeps waiting. Calling `Dispose` on the control or closing the OpenTK window is closing.
- **Input**: a press outside the dialog is swallowed and the dialog does not close. Lower dialogs are also under the overlay (`scrim`), appear dimmed, and receive no input. If a dialog appears while a press is held, that press is canceled and releasing it does not produce a click. Blocking starts at registration, so even a fast double press on a button does not show the same dialog twice.
- **Exceptions**: exceptions from handlers·setters·value reads in the dialog view arrive at `App.HandlerError`, as with pages. An exception in `OnClosed()` is reported with `Kind` = `"closed"` (`Member` = the dialog type name), and closing·result delivery continue. If the dvml cannot be read as XML, the exception is thrown as-is in the frame that builds the view (developer error), and that dialog closes as canceled so `await` does not hang.
- Not available: closing by an outside press, drag-to-move, open/close animation, a title-bar X button, closing·confirming with physical keyboard Esc/Enter (the only exception is the edit-complete Enter in the built-in input boxes — 21.5). Build closing with buttons inside the dialog.

### 21.7 Patterns

All code below is complete files in `namespace MyApp`, and the tests actually compile them (they use `Recipe`·`RecipeEditDialog` from 21.2).

**Pattern 1 — Confirmation.** Ask before irreversible actions such as delete·write. A message box is enough.

```xml
<DvPageFrame vm="MyApp.RecipePage" title="레시피">
  <DvCard title="현재 레시피">
    <DvLabel class="title" text="{CurrentText}"/>
    <DvRow gap="sm">
      <DvButton variant="primary" text="수정" onclick="EditRecipe"/>
      <DvButton variant="danger" text="삭제" onclick="DeleteRecipe"/>
    </DvRow>
  </DvCard>
</DvPageFrame>
```

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class RecipePage : DvPage
{
    public Recipe? Current { get; private set; } = new("기본", 80.0);

    public string CurrentText => Current is { } r ? $"{r.Name} · {r.Temp:0.0} ℃" : "(없음)";

    public async void DeleteRecipe()
    {
        if (Current is null)
            return;
        var answer = await App.MessageBox($"'{Current.Name}' 레시피를 삭제할까요?", "삭제", DvMessageButtons.YesNo);
        if (answer != DvDialogResult.Yes)
            return;                 // 아니요, 또는 바깥에서 닫힘(Cancel)
        Current = null;
    }
}
```

**Pattern 2 — Edit a copy.** The dialog edits a copy of the received value and, on OK, returns the new value as its result. Applying it to the original is the caller's job (data is the app's responsibility). On cancel the original is unchanged, so no undo code is needed. The dialog is `RecipeEditDialog` from 21.2.

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class RecipePage
{
    public async void EditRecipe()
    {
        if (Current is null)
            return;
        Recipe? edited = await App.ShowDialog<RecipeEditDialog, Recipe>(Current);
        if (edited is null)
            return;                 // 취소
        Current = edited;           // 원본 반영은 띄운 쪽이(화면은 자동으로 다시 읽는다)
    }
}
```

**Pattern 3 — Communication alarms and duplicate prevention.** A communication class is not a page, so it reaches the app through `DvApp.Current`. To show only one window even if the same alarm comes in several times, give the dialog class **its own key property** (`AlarmId`) and check `App.Dialogs`. Do not compare by title or text (it breaks with localization·wording changes).

```xml
<DvDialogFrame vm="MyApp.AlarmDialog" title="알람" subtitle="{Code}" style="width:400">
  <DvLabel class="body" text="{Text}"/>
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="primary" text="확인(ACK)" onclick="Ack"/>
  </DvRow>
</DvDialogFrame>
```

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class AlarmDialog : DvDialog<bool?>
{
    public AlarmDialog(int alarmId, string text)
    {
        AlarmId = alarmId;
        Text = text;
    }

    public int AlarmId { get; }             // 중복 확인용 키 — 다이얼로그가 스스로 정의한다
    public string Text { get; }
    public string Code => $"#{AlarmId}";

    public void Ack() => Close(true);       // 사용자가 확인
    public void Dismiss() => Close(false);  // 바깥(통신)에서 닫기 — 패턴 5
}
```

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed class PlcClient
{
    // 통신 스레드에서 불린다(창을 연 뒤에 통신을 시작한다 — 그 전에는 DvApp.Current 가 예외).
    public void OnAlarmRaised(int alarmId, string text)
    {
        var app = DvApp.Current;
        if (app.Dialogs.OfType<AlarmDialog>().Any(d => d.AlarmId == alarmId))
            return;                                             // 이미 떠 있다 — 키로 비교
        _ = WaitAckAsync(app, alarmId, text);
    }

    // 알람이 PLC 에서 해제됐다 — 떠 있는 그 알람 창만 닫는다(패턴 5)
    public void OnAlarmCleared(int alarmId)
    {
        DvApp.Current.Dialogs.OfType<AlarmDialog>().FirstOrDefault(d => d.AlarmId == alarmId)?.Dismiss();
    }

    private async Task WaitAckAsync(DvApp app, int alarmId, string text)
    {
        try
        {
            bool? acked = await app.ShowDialog<AlarmDialog, bool?>(alarmId, text);
            if (acked == true)
                await Task.Run(() => SendAck(alarmId));         // 이어짐은 입력 스레드 — 통신은 넘긴다
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"알람 {alarmId} ACK 실패: {ex.Message}");
        }
    }

    private void SendAck(int alarmId) { /* PLC 에 ACK 쓰기 */ }
}
```

- Between the check and the show, another thread could show the same alarm. If alarms come in from only one communication thread there is no problem; if they come from several threads, wrap these two lines in a `lock`.
- For an alarm that arrives after the window has closed (during program shutdown), `ShowDialog` immediately returns a cancel result (`null`), so it ends quietly without an exception. `DvApp.Current` also keeps returning the last app after closing.

**Pattern 4 — Dialog on top of a dialog.** Showing another dialog from a dialog handler stacks it on top. Only the topmost receives input, and the dialog below appears dimmed under the overlay. If the lower dialog closes first, the one above closes with it as canceled.

```xml
<DvDialogFrame vm="MyApp.ValveDialog" title="{ValveName}" subtitle="밸브 조작" style="width:360">
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="secondary" text="닫기" onclick="Cancel"/>
    <DvButton variant="primary" text="열기" onclick="RequestOpen"/>
  </DvRow>
</DvDialogFrame>
```

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class ValveDialog : DvDialog<bool?>
{
    public ValveDialog(string valveName) => ValveName = valveName;

    public string ValveName { get; }

    public async void RequestOpen()
    {
        // 이 다이얼로그 위에 메시지 박스가 뜬다
        if (await App.MessageBox($"{ValveName} 을(를) 열까요?", "확인", DvMessageButtons.YesNo) == DvDialogResult.Yes)
            Close(true);
    }

    public void Cancel() => Close();
}
```

**Pattern 5 — Closing only a specific dialog from outside.** `Close` is `protected`, so a dialog that must be closable from outside provides **its own public method** wrapping `Close` (`AlarmDialog.Dismiss()` in pattern 3). Outside code finds it by key in `App.Dialogs` and calls that method (`PlcClient.OnAlarmCleared`). It may be called from any thread. Use `App.CloseDialogs()` only when closing all (all results are cancel).

### 21.8 Lock and idle (Lock · Unlock · IdleTimeout · Idle)

Locks the screen so that no one else can operate it while the operator is away. **The library handles only the "topmost lock layer" and "idle detection"; the lock screen's appearance and password·card·permission verification are built by the app.**

- `App.Lock<TDialog>(인자…)` — shows an app-made `DvDialog<bool>` (login screen) in the **lock layer above all dialogs**. Until unlocked, pages·areas·other dialogs receive no input. The returned `Task` completes when unlocked.
- **How to unlock:** `Close(true)` when authentication succeeds inside the lock dialog. `Close(false)`·`Close()`·`App.CloseDialogs()` do not unlock (the lock stays). To unlock from outside the dialog (card reader signal·remote release), use `App.Unlock()` — it unlocks without verification, so the app authenticates first.
- **While locked:** drawing continues (instruments·trends·alarm blinking, `App.Invalidate()` is reflected). Dialogs shown after locking (communication alarms, etc.) stack **below** the lock layer and receive input after unlocking. The lock dialog does not appear in `App.Dialogs`. If already locked, `Lock` shows nothing new and returns the current lock's `Task`.
- `App.IdleTimeout` — if there is no input (touch·mouse·key·editing) for this duration, `App.Idle` fires **once** (input thread). `TimeSpan.Zero` (default) = off. It fires again after the same duration passes following new input. It does not count while locked, and starts counting again once unlocked. What to do on idle (lock, go to home) is decided by the app.
- When the app (window) closes, the lock closes too and the `Task` completes.

```xml
<DvDialogFrame vm="MyApp.LockDialog" title="잠금 — 로그인 필요" style="width:360">
  <DvInput value="{Bind Password}" keyboard-type="password"/>
  <DvLabel text="{Error}" visible="{Error}"/>
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="primary" text="확인" onclick="Login"/>
  </DvRow>
</DvDialogFrame>
```

```csharp
namespace MyApp;

using Devinno.UI.App;

public sealed partial class LockDialog : DvDialog<bool>
{
    private readonly Func<string, bool> _check; // 인증은 앱 몫 — 서버·카드·로컬 비밀번호 등
    public LockDialog(Func<string, bool> check) => _check = check;

    public string Password { get; set; } = "";
    public string Error { get; set; } = "";

    public void Login()
    {
        if (_check(Password))
            Close(true);                       // 이것이 잠금 해제
        else
        {
            Error = "비밀번호가 틀렸습니다";     // 틀리면 그대로 잠금 유지
            Password = "";
        }
    }
}

public static class LockSetup
{
    public static void Use(DvApp app, Func<string, bool> check)
    {
        app.IdleTimeout = TimeSpan.FromMinutes(5);
        app.Idle += (_, _) => _ = app.Lock<LockDialog>(check);   // 5분 무입력 → 잠금
    }
}
```

---

## 22. Process diagrams (DvProcess)

> **Read [`dvml-process.md`](dvml-process.md) before writing a process diagram.** The authoring procedure, per-equipment sizes·port positions, layout·routing rules and numbers, how to use hints, how to draw user symbols, fixes for each diagnostic message, and full examples are all in that document. This chapter is a summary.

A process screen that draws tanks·pumps·valves and pipes. **You do not write coordinates.** Write only the equipment and "what connects to what", and the library places the equipment and draws right-angle (orthogonal) pipes. When equipment is added or removed, the pipes re-route themselves.

```xml
<DvProcess direction="lr" look="plain">
  <DvTerminal name="IN" text="RAW WATER"/>
  <DvVessel name="T1" level="{T1Level}" label="T-101"/>
  <DvPump   name="P1" running="{P1Run}" label="P-101"/>
  <DvPump   name="P2" running="{P2Run}" label="P-102"/>
  <DvValve  name="V1" kind="control" open="{V1Open}" label="FCV-101"/>
  <DvTerminal name="OUT" text="TO DRAIN"/>

  <DvLine from="IN" to="T1.top" arrow="end"/>
  <DvLine from="T1.bottom" to="P1.in"/>
  <DvLine from="T1.bottom" to="P2.in"/>   <!-- 분기: 같은 포트에서 선 두 개 -->
  <DvLine from="P1.out" to="V1.in"/>
  <DvLine from="P2.out" to="V1.in"/>      <!-- 합류: 같은 포트로 선 두 개 -->
  <DvLine from="V1.out" to="OUT"/>
</DvProcess>
```

- Equipment is referred to by `name`, and lines connect `장비.포트` (equipment.port). Terminals (`DvTerminal`) use only `장비` (the equipment name), without a port.
- Branch points (T-junctions) have no tag. They arise when several lines connect to one port.
- Everything outside `DvProcess` (title, buttons, tabs) is ordinary dvml.

### 22.1 DvProcess attributes

| Attribute | Values | Description |
|---|---|---|
| `direction` | `lr` (default) · `tb` | Flow direction. `lr` = left→right, `tb` = top→bottom |
| `look` | `plain` (default) · `rich` | Appearance. `plain` = gray, color only for state (ISA-101 style). `rich` = metallic gradients·thick pipes |
| `style` | Ordinary style | `padding`, `background`, `width`/`height`, etc. |

- Without a given size, the process diagram size = the content size.
- `flow-animation="true"` (off by default): dashes move in the flow direction over pipes the library judges to be flowing from the connections·equipment state (redrawn at 20 frames per second). Flow can be overridden per line with `<DvLine flow="{X}"/>` (lines with real flow-rate data). For the detailed judgment rules see `dvml-process.md` 12.1.
- `align="auto|step|row"` (default `auto`): whether equipment after a line leaving a tank bottom is placed stepped below the tank (`step`) or on the same row as the tank (`row`, low and wide). `auto` uses stepped if it fits the area, otherwise the same row.
- If the size is smaller than the content, it re-lays out by changing the row alignment (`auto`) and equipment spacing (down to 0.5×). If still short, it leaves a Trace warning ("Not enough space") and draws overflowing. If there is spare space, it is centered.
- The background is not painted. It follows the app theme or `style`'s `background` (`rich` looks best on a dark background).

### 22.2 Equipment

| Tag | `kind` (first value is the default) | Ports | State attribute |
|---|---|---|---|
| `DvVessel` | `tank` `reactor` `silo` `hopper` | `top` · `bottom` · `side` | `level` (0~100 %) |
| `DvPump` | `centrifugal` `gear` `compressor` `blower` | `in` (left) · `out` (right) | `running` |
| `DvValve` | `gate` `ball` `butterfly` `control` `check` | `in` · `out` | `open` |
| `DvInline` | `flowmeter` `filter` `strainer` `sight` | `in` · `out` | — |
| `DvValve3` | `ball` `control` | `a` (common, left) · `b` (straight, right) · `c` (branch, bottom) | `open-b` (a↔b) · `open-c` (a↔c) |
| `DvExchanger` | `shell` `plate` | `in` · `out` (process, left→right) · `in2` (top) · `out2` (bottom) (heating/cooling medium) | — |
| `DvTerminal` | — | One port (used without a name) | `text` (tag label text) |

Attributes available on all equipment:

| Attribute | Description |
|---|---|
| `name` | Equipment name. Used to connect lines. MUST NOT be duplicated within a process diagram. (`id` is separate, for grabbing the control as a handle in code — chapter 20) |
| `label` | Name tag (e.g. `P-101`). For vessels, drawn at the center of the body; for others, on a side not used by lines (bottom → top → right → left) |
| `kind` | Type from the table above. An unknown value draws the default type and leaves a Trace warning |
| `rotate` | `0` `90` `180` `270` (clockwise). Port sides rotate too (valve `rotate="90"` = enters from the top, exits at the bottom) |
| `flip` | `h` (horizontal) · `v` (vertical) · `hv` |
| `row` · `rank` | Layout hints (22.5) |
| `onclick` `hold` `disabled` `visible` `class` `style` | Same as ordinary controls. Pressing the equipment triggers `onclick` |

- State attributes are bound: `running="{P1Run}"`, `open="{V1Open}"` (true/false, or a number greater than 0 = open), `level="{T1Level}"`.
- Three-way valves serve both diverting and mixing (paths are bidirectional). When used for mixing in a left→right process, put the common `a` on the right with `flip="h"`. Details in `dvml-process.md` 5.7·5.8.
- Layout is not recomputed when values change (if the structure is the same, the previous layout is reused). So updating several times per second is cheap.

**Terminals** (`DvTerminal`): tags for pipes that continue off-screen. As a line's `from` it means "coming in from outside" (the pipe leaves from the tag's tip); as `to` it means "going out". The arrow shape points in the flow direction.

### 22.3 Connections (DvLine)

| Attribute | Description |
|---|---|
| `from` · `to` | `장비.포트` (equipment.port). Terminals use only `장비` (the equipment name). A user symbol with only one port may also use just `장비` |
| `arrow` | `end` (destination port side) · `start` · `both` · `none` (default). An arrow pointing into the port |
| `flow` | Flow override for this line (true·false binding). If absent, judged automatically |

- Lines are always orthogonal. They come straight out of the side the port is on and bend only in empty corridors between equipment. There is no way to write bend points directly (they would break when equipment moves) — change the shape with the hints in 22.5.
- **Vessel ports**
  - `bottom` — a single point at the bottom center. Connecting several lines branches them from that point.
  - `top` — the top surface. Incoming lines and outgoing lines are each merged into one nozzle and gather in a T·cross in front of the tank (if there is an agitator, they avoid the center).
  - `side` — the side surfaces. Each line picks the side (left/right) facing its counterpart equipment, and lines on the same side in the same direction merge into one nozzle. If that side is covered by an attached slot (22.4), the opposite side is used.
- On a wrong connection (nonexistent equipment·port, no port given on equipment with several ports), that line is drawn as a **red dashed line** between the two pieces of equipment, equipment with the wrong port gets a **red border**, and a Trace error is left. The rest is drawn normally.

### 22.4 Attaching to equipment (DvAttach · DvAgitator · DvMotor · DvInstrument · DvSignal)

```xml
<DvVessel name="BL" level="{Bl.Level}" label="BL-1">
  <DvAgitator running="{Bl.AgitatorRun}"/>
  <DvAttach side="right">
    <DvLabel class="caption muted" text="TIA 1001"/>
    <DvLabel class="title" text="{Bl.TempText}"/>
  </DvAttach>
  <DvAttach side="left">
    <DvButton variant="secondary" size="sm" text="Reset" onclick="ResetBl"/>
  </DvAttach>
</DvVessel>
```

- Inside `<DvAttach side="left|right|top|bottom">` put ordinary dvml (value displays, buttons, lamps). It attaches beside the equipment, is included in the equipment size during layout, and pipes avoid that slot. Buttons are pressable like ordinary buttons.
- Text color inside an attached slot is given by class (`label`·`caption`·`title` …) as on ordinary screens (follows the theme). Text without a class is a dark color regardless of the theme.
- `<DvAgitator running>` (agitator)·`<DvMotor running>` (motor) are accessories mounted on top of equipment. The agitator's shaft goes into the vessel.
- Instrument bubbles: place `<DvInstrument name tag number on panel value>` **directly under `DvProcess`** (not inside equipment). `on` (required) is the name of the equipment to attach to — instruments do not take part in pipe layout; they sit on a free side next to that equipment and pipes avoid them. Controllers not attached to equipment (FIC·LIC) attach to the valve they control. `tag` is the text above in the bubble, `number` the text below, `panel="true"` adds a horizontal line through the middle, and `value` is small text to the right of the bubble.
- Signal lines are `<DvSignal from to>` — both ends are instrument·equipment **names only** (no port; for a control valve it touches the actuator). Drawn as thin orthogonal dashed lines after all pipes are drawn; they do not affect layout·flow and do not jump where they cross pipes. Do not draw signals with pipes (`DvLine`). Detailed rules in `dvml-process.md` 7.3·7.4.

```xml
<DvInstrument name="FT1" tag="FT" number="101" on="F1"/>
<DvInstrument name="FIC1" tag="FIC" number="101" on="V1" panel="true" value="{FlowText}"/>
<DvSignal from="FT1" to="FIC1"/>
<DvSignal from="FIC1" to="V1"/>
```

### 22.5 Layout rules and hints

- Equipment is placed in columns (lr) or layers (tb) in flow order, the order is chosen to reduce line crossings, and connected ports are aligned on the same line. So a serial process becomes a straight line without bends.
- **Parallel equipment** (e.g. standby pumps with the same upstream and downstream equipment) stack in turn along the flow direction and stand on the same line. They share the inlet header and outlet header.
- Return lines (recirculation) are drawn too. Layout breaks cycles and aligns equipment positions using only flow-direction lines; recirculation lines are routed around the equipment.
- The same markup always produces the same drawing (no randomness). Changing one hint changes the result predictably.

| Hint | Description |
|---|---|
| `row="0"` | Order within the same column (layer). Smaller values go on top (lr) / left (tb). Order among hints is always respected |
| `rank="3"` | Fixes which column (layer) to place it in. From 0 |

Line crossings or blocked paths are reported as Trace warnings (22.8). First change the order with `row`, then the column with `rank`.

### 22.6 Appearance (`look`)

| | `plain` (default) | `rich` |
|---|---|---|
| Body | Flat gray | Metallic gradient·shadow |
| Running/open | Dark gray | Green |
| Stopped/closed | White | Gray |
| Liquid | Gray-blue | Blue gradient |
| Pipe | 3px gray, corner radius 6 | 11px pipe (three layers), corner radius 10 |

- Where pipes cross other pipes, a semicircle jump is drawn on one of the lines (in lr the vertical line jumps over, in tb the horizontal line).
- Medium color is `<DvLine medium="water">` (`water`·`steam`·`air`·`gas`·`oil`·`acid`, default none = gray). Written once, it spreads in the flow direction through equipment and stops at vessels·terminals·another `medium`. There is no way to give a color value directly. Details in `dvml-process.md` 12.2.

### 22.7 User symbols (DvSymbol)

For equipment not in the list, define a symbol and use it. Built-in equipment is defined in the same format.

```xml
<DvSymbol name="Cyclone" view-box="0 0 60 100">
  <DvParam name="running" type="bool"/>
  <DvShape class="state" when="running" path="M10 0 H50 V40 L35 90 H25 L10 40 Z"/>
  <DvShape class="detail" path="M14 12 H46"/>
  <DvPort name="in"   side="left"   at="0.2" role="in"/>
  <DvPort name="gas"  side="top"    at="0.5" role="out"/>
  <DvPort name="dust" side="bottom" at="0.5" role="out"/>
  <DvPass from="in" to="gas"/>
  <DvPass from="in" to="dust"/>
</DvSymbol>
```

```csharp
registry.Register(cycloneMarkup);   // 컴포넌트와 같은 ComponentRegistry.Register
```

```xml
<Cyclone name="CY1" running="{Cy1Run}" label="CY-1"/>
<DvLine from="P1.out" to="CY1.in"/>
```

| Element | Description |
|---|---|
| `DvSymbol name view-box` | `view-box="0 0 너비 높이"` (0 0 width height) = the symbol's coordinate system and its drawn size (px) |
| `DvParam name type default` | State value received from outside. Bound as an instance attribute |
| `DvShape class path when` | One drawing layer (SVG path syntax). Color is not put in the path; it is set by `class` (table below). `when` = the parameter the state color follows |
| `DvPort name side at role` | Port. `side` = `left` `top` `right` `bottom`, `at` = position along that side (0~1), `role` = `in` `out` `inout` (default) |
| `DvPass from to when` | Flow rule: the `from` port connects through to the `to` port (only when the `when` parameter is true). Used by the 3-level flow display |
| `DvFill clip value` | Level fill: clips to the shape of the `clip` class layer and fills from the bottom by the `value` parameter (0~100) |

`class` of `DvShape`:

| class | Paint |
|---|---|
| `vessel` | Body (no state) |
| `state` | Body, running/stopped color according to the `when` parameter |
| `accent` | Flanges·supports·handles (dark color) |
| `detail` · `dash` | Stroke only (dashed) |
| `screen` · `glass` | Display window · glass |

- Ports are written as **side + position**, not coordinates. This is because the side determines which direction the pipe must leave in.
- If the definition is wrong, `Register` throws an `ArgumentException` describing what is wrong: `view-box` size, port `side`·`at`·`role`, duplicate port names, `DvPass` pointing to a nonexistent port, unknown `DvShape` class, path syntax, a `DvFill` `clip` class that does not exist, unknown child elements.
- An unregistered tag used inside a process diagram is not drawn and leaves a Trace error.

### 22.8 Diagnostics (Trace)

Trace warnings·errors are left based on the layout drawn on screen (the same message only once). Connection errors (nonexistent names·ports, duplicate names, missing values) in dvml appear first at build time as DVML008~011 (20.7).

| Message | Meaning · what to do |
|---|---|
| `'A.out'→'B.in': crosses other piping N time(s)` | Crossing. Try changing the order with `row` |
| `'A.out'→'B.in': no free path, so it overlaps other piping` | Path blocked, drawn overlapping. Change `row`·`rank` or enlarge the area |
| `Not enough space: needs W×H, area is W×H` | Overflows even at minimum spacing. Enlarge the area or split the screen |
| `no equipment named 'X'` · `'X' has no port 'p' (…)` · `'X' requires a port (…)` | Wrong connection (red marking). The parentheses list the usable ports |
| `<Tag> is not process equipment` | A non-equipment tag inside a process diagram |
| `<DvInstrument …> has no on …` · `on='Y': no such equipment` · `DvSignal '…': no instrument or equipment named 'X'` | Wrong name of the equipment an instrument attaches to, or of a signal line end (not drawn) |

### 22.9 Units (DvUnit · DvPort)

Groups several pieces of equipment into one unit (repeating the same equipment set, or a flow direction that differs for only a part). Detailed rules in `dvml-process.md` chapter 8.

- Inside `<DvUnit name direction label>` put equipment·`DvLine`·`<DvPort name side at>`. The inside of the unit is laid out first, and from outside it is placed like a single piece of equipment. No border·background by default (use `style`).
- Outside lines connect only via **`유닛.포트`** (unit.port) (connecting directly to the inside, like `U1.T.top`, is an error). Lines inside the unit use the port name only (`to="out"`).
- Repeating the same unit: register a `DvComponent` whose template root is `DvUnit` and use `<Blender name="BL1" U="{BL1}"/>`. Buttons inside the template receive their own unit through a method with one parameter, like `Reset(BlenderVm unit)`.
- To line up several units side by side, use outer `direction="tb"` + exit port `side="bottom"`.

### 22.10 Not yet available

To be added in a later stage. Do not use it now (it is silently ignored or drawn as an error).

- The `exit` hint.
