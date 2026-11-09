# DVML styles and theme tokens

Covers: `style` syntax, every supported style key, value formats (color, border, shadow, gradient, background picture), `content-align`, classes and `<DvStyle>`, state styles, priority and inheritance, transitions, and the theme tokens with their values in all four built-in themes.
Read first: [markup basics](dvml-language.md#markup-basics) · [how attribute values are read](dvml-language.md#value-rules)
Not here: [layout behavior of flex, grid, absolute and scrolling](dvml-layout.md#layout-sugar) · [why something is magenta](dvml-diagnostics.md#magenta) · [build warnings for styles and tokens](dvml-diagnostics.md#build-diagnostics) · [process diagram colors (`look`)](dvml-process.md#look-flow-medium) · [building a screen from pictures](dvml-image.md#image-method)

<a id="style-syntax"></a>
## Syntax

- `style="key: value; key: value"`.
- Keys are case-insensitive.
- If the same key appears more than once, the later one wins.
- Bindings (`{X}`) and tokens (`token(name)`) can be used inside values.

<a id="style-keys"></a>
## Supported style keys

Any other key is ignored at run time, and the build reports it as warning [DVML012](dvml-diagnostics.md#dvml012).

| Key | Value | Default / notes |
|---|---|---|
| `background` | Color, `linear-gradient(...)` or `url(path)` | None (transparent). Like CSS, it sets the whole background: a color clears a gradient and a picture; `url(…)` clears the color and gradient |
| `background-color` | Color only (solid-color alias of `background`) | No gradients (use `background` for gradients). Used with `background`, the later declaration wins. Keeps `background-image` |
| `background-image` | `url(path)` (quotes optional) or `none` | None. A picture behind the content ([background picture](#style-values)) |
| `background-size` | `cover` · `contain` · `fill` (`100% 100%` = `fill`) · `natural` | `cover` |
| `background-image-offset` | `X Y` in px (one value = both) | With `natural` only: where the picture's top-left corner goes, from the box's top-left corner. Without it, the picture is centered |
| `color` | Color | `#222222` (theme-independent, not inherited) |
| `font-size` | Number | 14 |
| `font-family` | Comma list | First installed font that has all the characters; otherwise per-character fallback. **Inherited by children** |
| `font-weight` | `bold` or a number (600 or more = bold) | Normal |
| `text-align` | `left` `center` `right` | `left` (per line when multi-line) |
| `white-space` | `normal` `nowrap` (lowercase only, anything else = `normal`) | `normal` = wrap to fit width, `nowrap` = break only at `\n` ([text wrapping](dvml-layout.md#text-wrap)). **Inherited by children** |
| `content-align` | 9 positions ([content-align](#content-align)) | Varies by widget |
| `padding` | 1–4 values (`8`; `8 12`; `4 8 12` = top, left/right, bottom; `4 8 4 8` = top right bottom left) | 0 |
| `padding-top` `padding-right` `padding-bottom` `padding-left` | 1 value; changes only that side | As in CSS, the later declaration wins (`padding:12; padding-left:0` = only left is 0) |
| `border` | `width color`, `width solid color`, `width dashed color` | None |
| `border-style` | `solid` `dashed` | `solid` |
| `border-radius` | One number | 0 |
| `box-shadow` | `dx dy blur color` or `none` | None |
| `display` | `flex` `grid` (anything else = default flow) | Default flow |
| `flex-direction` | `row` `column` | `row` (when `display:flex`) |
| `justify-content` | `start` `center` `end`(`flex-end`) `space-between`; in a grid it places children in their cells ([grid cells](dvml-layout.md#grid-cells)) | `start` (`stretch` in a grid) |
| `align-items` | `start`(`flex-start`) `center` `end`(`flex-end`) `stretch`; in a grid it places children in their cells ([grid cells](dvml-layout.md#grid-cells)) | `stretch` |
| `align-self` | `start`(`flex-start`) `center` `end`(`flex-end`) `stretch` | Parent's `align-items` |
| `flex-grow` | Number | 0 |
| `flex-shrink` | Number | **0** (1 for an `overflow:scroll` box if not specified; [scrolling](dvml-layout.md#scroll)) |
| `flex` | `grow` or `grow shrink` | [`flex` shorthand](dvml-layout.md#size) |
| `flex-wrap` | `wrap` | No wrapping |
| `align-content` | `start` `center` `end` `space-between` `stretch` | `start` (when wrapping) |
| `gap` | One number | 0 |
| `width` `height` `min-width` `max-width` `min-height` `max-height` | Number | Content size |
| `grid-template-columns` `grid-template-rows` | Track list ([grid tracks](dvml-layout.md#grid-tracks)) | |
| `aspect-ratio` | `1`, `1.5`, `16/9` | [aspect-ratio](dvml-layout.md#aspect-ratio) |
| `overflow` | `visible` `hidden` `scroll` | `visible` |
| `position` | `absolute` (`relative` has no effect) | Flow layout |
| `left` `top` `right` `bottom` | Number | Only with `position:absolute` |
| `opacity` | 0–1 | 1. The box and all its children are composited as one layer and made translucent (as CSS). At 0 it is not drawn (its space remains) |
| `transform` | `translate(x[, y])` `translateX(x)` `translateY(y)` `scale(s[, sy])` `scaleX(s)` `scaleY(s)` separated by spaces, or `none` | None. Relative to the box center; whatever the written order, it is "translate, then scale". Children move along. **Drawing only**: layout and touch area stay unchanged |
| `transition` | `property duration [curve] [delay], …` ([transitions](#transition)) | None (changes instantly when the state changes) |

- `transform` with any other function (`rotate`, `skew`, …) is ignored entirely, valid parts included (`scale(0.9) rotate(5deg)` does nothing), with a Trace warning.
- `margin` is parsed but not applied. Use `gap`/`padding` for spacing.
- Borders are the same on all four sides (no per-side keys like `border-top`). Rounded corners are the same on all four corners.
- `background-position` and `background-repeat` are not supported.

<a id="style-values"></a>
## Value formats

**Numbers**

- Numbers are px. `16`, `16px` and `16dp` are accepted.
- `%`, `em` and `auto` fail to parse and become **0**. Exceptions: `font-size` → 14, `opacity` → 1, `aspect-ratio` → none.
- If `padding` contains an unparseable value, the entire `padding` declaration is ignored.

**Color** (how color values are read everywhere: [color values](dvml-language.md#value-color))

| Format | Example |
|---|---|
| `#RGB` | `#fff` |
| `#RRGGBB` | `#2563eb` |
| `#RRGGBBAA` (alpha **last**) | `#00000040` |
| `transparent` | |
| Theme token | `token(primary)` ([theme tokens](#theme-tokens)) |

`rgb()`, `rgba()`, `hsl()` and color names (`red`) are not supported.

**Border:** `border: 1 #cbd5e1`, `border: 1 solid token(border)`, `border: 2 dashed #f59e0b`. First value = width, last value = color. `border-style: dashed` can also be set separately.

**Shadow:** `box-shadow: 0 8 24 #00000040`, exactly in `dx dy blur color` order. There is no spread and no multiple shadows. Tokens work (`box-shadow: token(shadow.md)`). `box-shadow: none` removes a shadow set by a class.

**Gradient:** `background: linear-gradient(135deg, #2563eb, #7c3aed)`.

- The angle is `<number>deg` or `to right` `to left` `to top` `to bottom` (case-insensitive). If omitted, top→bottom.
- 2 or more colors. Position values after a color (`#fff 30%`) are ignored, and the colors are spread evenly.
- A later solid `background` (or `background-color`) replaces an earlier gradient, and vice versa.

**Background picture:** `background-image: url(Assets/panel.png); background-size: cover`.

- The path is read like `DvImage src` (relative to the current directory, cached, a missing file draws nothing; see [DvImage](dvml-widgets.md#dvimage)). Bindable: `background-image: url({PanelImg})`. An empty `url()` means no picture.
- It covers the whole box, **padding included**, over the solid or gradient background and under the border and the content. With `cover`, `contain` and `fill` it is cut to the box shape (`border-radius`).
- `cover` (default) fills the box keeping the proportions, cuts the overflow, centered. `contain` shows the whole picture, centered, leaving bands. `fill` stretches it to the box. There is no `background-position` or `background-repeat`: the picture is drawn once.
- **`natural`: a plate with a glow or shadow around it.** The picture is drawn at its own pixel size, centered on the box, and **not cut** at the box edge or by `border-radius`. Make the box the size of the plate (the part that should be pressed) and let the PNG's margin spread outside it: `width:250; height:160; background-image:url(card.png); background-size:natural` for a 282×192 PNG with a 16 px margin.
- With `natural` and margins that differ per side, place the picture with `background-image-offset: -left -top` (the margins, e.g. `-16 -12`).
- With `natural`, the part outside the box is drawn only: presses there do not reach the box, and elements drawn later lie on top of it. `background-image-offset` with another `background-size` is ignored with a Trace warning.
- The picture is part of the box, not a child, so it never covers a press. In a state style it changes the picture while pressed or hovered: `press-style="background-image:url(Assets/key_on.png)"`.
- A state style's background color does **not** darken the picture (the color is drawn under the picture, as in CSS). Show a press with another picture, `transform` or `opacity`.
- One picture per box. It works on any element: a `DvScreen` or `DvPage` root for the screen background, a `DvBox`/`DvStack` for a panel, a `DvButton` for an image button. Building a whole screen from pictures: [screens from images](dvml-image.md#image-method).

<a id="content-align"></a>
## content-align

Sets where the text (and button icon) of `DvLabel`/`DvButton` is placed inside the padding.

- 9 values: `top-left` `top-center` `top-right` `middle-left` `middle-center` `middle-right` `bottom-left` `bottom-center` `bottom-right`.
- As a style key (`style="content-align:middle-left"`), only lowercase kebab-case is accepted; any other value is ignored after one Trace warning.
- As the `content-align` attribute (`<DvButton content-align="middle-left"/>`), case and surrounding spaces are ignored ([word values](dvml-language.md#value-word)). The attribute is bindable (`content-align="{Align}"`, string).
- Priority: class < `content-align` attribute < inline `style`. Within one declaration list, whichever of `text-align` and `content-align` comes later decides the horizontal position.

| Element | Default |
|---|---|
| `DvButton` with `variant`, or a latching `DvButton` (`value`) | `middle-center` (`.btn` class) |
| `DvLabel`, a non-latching `DvButton` without `variant` | `top-left` |
| `DvButton` with only an icon and no text | Vertically centered |

<a id="classes"></a>
## Classes

`class="a b"`: with multiple classes, the later class wins.

**Built-in classes**

| Category | Classes | Content |
|---|---|---|
| Typography | `.display` (32 bold) `.heading` (21 bold) `.title` (15 bold) `.body` (14) `.label` (13) `.caption` (11, dimmed color) `.mono` (14) | Font size + text color `token(text)` |
| Text color | `.muted` (`text-muted`) `.subtle` (`text-subtle`) | |
| Surface | `.page` (background gradient + padding 24) `.card` (surface, border, radius 16, shadow, padding) `.card-flat` (card without shadow) `.dialog` (`DvDialogFrame` frame: `surface-overlay`, border, radius 12, shadow, padding 20) | |
| Chip | `.chip` | Small bold text + rounded background |
| Button | `.btn` `.btn-primary` `.btn-secondary` `.btn-danger` `.btn-ghost` `.btn-sm` `.btn-lg` | Added automatically by `DvButton` `variant`/`size` |
| Latching / segment | `.btn-on` `.segment` `.segment-item` `.segment-item-on` | `.btn-on` when a latching button is on; the others added automatically to `DvSegment` and its items |
| Checkbox / radio | `.checkbox` `.radio` | Automatic on `DvCheckBox`/`DvRadio` (box/circle in a 34 left margin, height 40 or more) |
| List | `.listbox` `.listbox-item` `.listbox-item-on` | Automatic on `DvListBox` and its rows |
| Expander | `.expander` `.expander-header` `.expander-body` | Automatic on `DvExpander`, its header row and its content |
| Tree | `.treeview` `.treeview-item` `.treeview-item-on` | Automatic on `DvTreeView` and its rows |
| Tabs | `.tabs` `.tab-header` `.tab` `.tab-on` `.tab-rule` `.tab-body` | Added automatically to `DvTabs`, the header, the divider and `DvTab` content |
| Icon | `.icon` | Automatic on `DvIcon` (24, `token(text)`) |
| Input | `.input` `.input-readonly` | Automatic on input widgets; `.input-readonly` also on a read-only `DvSegment`, `DvListBox`, `DvTreeView` |

Built-in semantic components use internal classes (`.stat` `.stat-accent` `.stat-body` `.stat-value` `.status-item` `.switch-row`). They exist but are not meant to be used directly.

<a id="dvstyle"></a>
## DvStyle (your own classes)

Define your own classes by placing `<DvStyle>` anywhere in the screen markup.

```xml
<DvStyle>
  .dial { width:160; height:160; border-radius:80; background: token(surface-raised) }
  .dial:hover { border: 2 token(primary) }
</DvStyle>
```

- Only the selectors `.name` and `.name:hover` `:press` `:active` (= press) `:disabled` work. Comma lists work (`.a, .b { … }`). `.a.b`, tag and descendant selectors are ignored (Trace warning).
- `/* comments */` are allowed.
- Defining a name identical to a built-in class appends to it and overrides it.
- A `DvStyle` inside a `DvComponent` definition is applied globally ([components](dvml-components.md#dvcomponent)). For the same class, the `DvStyle` in the screen markup wins.

<a id="state-styles"></a>
## State styles

| State | When | How to set |
|---|---|---|
| hover | Pointer is over it | `hover-style` attribute, `.x:hover` |
| press | Being pressed | `press-style`, `.x:press` / `.x:active` |
| disabled | `disabled="true"` | `disabled-style`, `.x:disabled` |

- When several states overlap, only one is used, in the order disabled > press > hover.
- Disabled elements (including those disabled through an ancestor) use no hover/press look: `disabled-style` if there is one, otherwise the default look. There is also no knob face brightening and no grid hover row.
- State styles apply to drawing only. Changing size, position or padding in them does not change the layout.
- hover/press is visible only when that element is the interaction target ([click and bubbling rules](dvml-language.md#click)).

<a id="priority"></a>
## Priority and inheritance

Priority, low → high:

1. Layout sugar and the attributes `grow`, `width`, `height`, `gap`, `padding`, `align`, `justify`
2. Classes (later class wins)
3. Class states (`.x:hover` etc., when in that state)
4. `content-align` attribute
5. Inline `style`
6. Inline state attributes (`hover-style` etc., when in that state)

- Inline `style` is merged: only the given properties change.
- Inline `style` beats class state styles, so to change an inline-set property on hover, use the `hover-style` attribute.
- The only inherited styles are `font-family` and `white-space`. Everything else, `color` included, applies to the element itself only.

<a id="transition"></a>
## Transitions (`transition`)

Changes values gradually when the state changes (as CSS). This works both when switching to hover/press/disabled styles and when the element is rebuilt because a class or bound value changed.

```xml
<DvButton text="Save" variant="primary" style="transition: background-color 150ms, transform 120ms ease-out" press-style="transform: scale(0.95)"/>
<DvBox style="opacity:{PanelOpacity}; transition: opacity 300ms">…</DvBox>
<DvBox style="transform: translateX({BallX}); transition: transform 400ms ease-in-out"/>
```

- Properties: `background-color` (also a solid `background`), `color`, `border-color`, `opacity`, `transform`, `all` (all of these). Gradient backgrounds change instantly.
- Duration: `200ms` or `0.2s`. A second duration is the delay. Curves: `linear` `ease` (default) `ease-in` `ease-out` `ease-in-out`. Entries are separated by commas. `none` = no transition.
- The transition used is the one in the style of the state **after** the change. Written in the base `style`, it is inherited by hover/press styles (state styles are layered over the base).
- A change mid-transition restarts from the currently visible value (no jump). Nothing is animated on the very first draw.
- Frames are drawn only while animating (about 60fps) and stop when done. Malformed entries are skipped (Trace warning).
- `DvRenderer.RenderToFile(surface, …)` (PNG of an existing screen) draws transitions finished: the image shows the end state, not the start of the animation.

<a id="theme-tokens"></a>
## Theme tokens

- `token(name)` reads a value of the current theme. `var(--name)` is the same, with dashes instead of dots (`var(--spacing-md)` = `token(spacing.md)`). Names are case-insensitive.
- An unknown token in a color position becomes magenta `#FF00FF`; in a number position (`padding: token(x)`) the declaration is ignored. Both write a Trace warning, and a literal unknown token is build warning [DVML013](dvml-diagnostics.md#dvml013). Finding the cause of magenta: [magenta](dvml-diagnostics.md#magenta).
- Built-in widgets draw every part of themselves from these tokens (lamp off color `lamp-off`, tank liquid `tank-fill`, chart colors `chart-1`…`chart-6`, keyboard, grid, …). There are no hidden fallback colors, so a theme made from scratch with `new DvTheme(...)` must define all of them: a missing one shows magenta there, with a Trace warning. Derive custom themes with `DvTheme.Dark.With(...)` / `DvTheme.Light.With(...)` instead.
- The process diagram (`DvProcess`) is the one exception: its symbol colors follow `look`, not the theme ([process look](dvml-process.md#look-flow-medium)).

```xml
<DvBox style="background: token(surface); border: 1 token(border); padding: token(spacing.lg); border-radius: token(radius.md)"/>
```

Token groups:

| Category | Tokens |
|---|---|
| Background / surface | `background` `background-alt` `surface` `surface-raised` `surface-overlay` |
| Border | `border` `border-strong` |
| Text | `text` `text-muted` `text-subtle` |
| Primary color | `primary` `primary-hover` `primary-press` `primary-fg` `focus` |
| Tone (`neutral` `info` `success` `warning` `danger`) | `tone-{tone}` `tone-{tone}-fg` `tone-{tone}-soft` |
| Equipment state (`run` `stop` `fault` `warn` `manual` `off`) | `state-{state}` `state-{state}-fg` `state-{state}-soft` |
| Alarm | `alarm-critical` `alarm-critical-fg` `alarm-major` `alarm-major-fg` `alarm-minor` `alarm-minor-fg` `comm-alarm` `comm-alarm-fg` |
| Spacing | `spacing.xs` 4 · `spacing.sm` 8 · `spacing.md` 12 · `spacing.lg` 16 · `spacing.xl` 24 · `spacing.2xl` 32 |
| Corner radius | `radius.sm` 6 · `radius.md` 10 · `radius.lg` 16 · `radius.full` 999 |
| Shadow | `shadow.sm` `shadow.md` `shadow.lg` (a `dx dy blur color` string) |
| Font size | `font.display` 32 · `font.heading` 21 · `font.title` 15 · `font.body` 14 · `font.label` 13 · `font.caption` 11 · `font.mono` 14 |
| Instrument | `gauge-track` `gauge-needle` `gauge-needle-tip` `gauge-tick` |
| Lamp | `lamp-off` (the off color of `DvLamp` and grid lamp cells) |
| Tank | `tank-empty-top` `tank-empty-bottom` `tank-frame` `tank-chip-bg` `tank-chip-fg` `tank-fill` (liquid when no `alarm-zones` zone applies) |
| Knob | `knob-face` `knob-fill` `knob-indicator` |
| Toggle | `toggle-on` `toggle-off` `toggle-knob` |
| Trend | `trend-grid` |
| Chart colors | `chart-1` … `chart-6` (trend channels without `color` and pie slices, cycled in order) |
| Scrollbar | `scrollbar-thumb` `scrollbar-track` |
| Dialog | `scrim` (the translucent layer covering the whole window just below the topmost dialog; [dialogs](dvml-dialogs.md#dialog-lifecycle)) |
| Data grid | `grid-header` `grid-header-fg` `grid-row` `grid-row-alt` `grid-row-hover` `grid-row-selected` `grid-line` `grid-summary` `grid-summary-fg` `grid-filter` `grid-filter-fg` `grid-filter-active` |
| Virtual keyboard | `kbd-bg` `kbd-key` `kbd-key-fg` `kbd-key-special` `kbd-key-special-fg` |

Also defined, not for new screens: `bg` `bg2` `surface-alt` `muted` `border-warn` `success` `success-bg` `success-border` `warn-text` `warn-bg` `shadow`. Every built-in theme has them (values in the full table below).

<a id="built-in-themes"></a>
## Built-in themes

- Set the theme with `app.Theme`. If omitted, `DvTheme.Default` (= `DvTheme.Dark`) is used. Style tokens and widget internal colors are all drawn with this theme ([DvApp](dvml-app.md#dvapp)).
- For a light screen, set `DvTheme.Light` explicitly. For control rooms there are `DvTheme.Graphite` (bright room) and `DvTheme.Carbon` (night room).
- The built-in themes all have the same tokens and differ only in colors. The spacing, corner radius and font-size scales are shared (values in the token groups table above).
- To change only some values: `DvTheme.Dark.With(new Dictionary<string,string>{ ["primary"] = "#0ea5e9" })`. The original theme is unchanged.

| Theme | For | Look |
|---|---|---|
| `DvTheme.Dark` (default) | General dark screens | Navy surfaces, light text, light-blue brand |
| `DvTheme.Light` | General light screens | White and light-gray surfaces, dark text, blue brand |
| `DvTheme.Graphite` | Bright, daytime control rooms (ISA-101 high-performance HMI) | Neutral gray, dark text, muted steel-blue brand. Normal states are gray (running = dark gray `state-run`, stopped = light gray `state-stop`); saturated color only for abnormal states and alarms |
| `DvTheme.Carbon` | Night control rooms | Low-luminance warm charcoal without a blue cast, warm off-white text, one brass accent. Dimmer than `Dark`; red and amber only for alarms and faults |

Every color and shadow token, with its value in each built-in theme (from `DvTheme`):

| Group | Token | Light | Dark | Graphite | Carbon |
|---|---|---|---|---|---|
| Background / surface | `background` | `#f1f5f9` | `#0b1020` | `#d5d8dc` | `#121314` |
| Background / surface | `background-alt` | `#e2e8f0` | `#111827` | `#cbcfd4` | `#0e0f10` |
| Background / surface | `surface` | `#ffffff` | `#1e293b` | `#e6e8eb` | `#1b1c1e` |
| Background / surface | `surface-raised` | `#ffffff` | `#273449` | `#f2f3f5` | `#242629` |
| Background / surface | `surface-overlay` | `#ffffff` | `#334155` | `#f7f8f9` | `#2c2e32` |
| Border | `border` | `#cbd5e1` | `#334155` | `#b8bdc4` | `#33363a` |
| Border | `border-strong` | `#94a3b8` | `#475569` | `#8e959e` | `#46494e` |
| Text | `text` | `#0f172a` | `#f8fafc` | `#1e2329` | `#e8e4dc` |
| Text | `text-muted` | `#64748b` | `#94a3b8` | `#4f5761` | `#9a958c` |
| Text | `text-subtle` | `#94a3b8` | `#64748b` | `#7d858f` | `#66625c` |
| Primary color | `primary` | `#2563eb` | `#60a5fa` | `#4a6278` | `#bda67e` |
| Primary color | `primary-hover` | `#1d4ed8` | `#3b82f6` | `#3f5569` | `#c9b48f` |
| Primary color | `primary-press` | `#1e40af` | `#2563eb` | `#34475a` | `#a8916a` |
| Primary color | `primary-fg` | `#ffffff` | `#ffffff` | `#ffffff` | `#17140f` |
| Primary color | `focus` | `#2563eb66` | `#60a5fa80` | `#4a627866` | `#bda67e80` |
| Shadow (`dx dy blur color`) | `shadow.sm` | `0 1 3 #0000001a` | `0 2 6 #00000033` | `0 1 2 #00000014` | `0 2 6 #00000040` |
| Shadow (`dx dy blur color`) | `shadow.md` | `0 6 18 #0000001f` | `0 8 24 #00000040` | `0 3 10 #00000017` | `0 8 24 #0000004d` |
| Shadow (`dx dy blur color`) | `shadow.lg` | `0 12 30 #00000029` | `0 14 36 #00000066` | `0 10 26 #00000026` | `0 14 36 #00000073` |
| Dialog | `scrim` | `#0f172a66` | `#00000099` | `#1e232966` | `#000000a6` |
| Tone | `tone-neutral` | `#64748b` | `#64748b` | `#6b737d` | `#77736c` |
| Tone | `tone-neutral-fg` | `#334155` | `#cbd5e1` | `#3f4750` | `#c4bfb5` |
| Tone | `tone-neutral-soft` | `#e2e8f0` | `#94a3b81f` | `#dadde1` | `#8e8a821f` |
| Tone | `tone-info` | `#2563eb` | `#3b82f6` | `#4a6278` | `#7f96ad` |
| Tone | `tone-info-fg` | `#1d4ed8` | `#93c5fd` | `#3a4e62` | `#a9bacb` |
| Tone | `tone-info-soft` | `#dbeafe` | `#3b82f61f` | `#d8e0e8` | `#7f96ad1f` |
| Tone | `tone-success` | `#16a34a` | `#10b981` | `#4f7a5f` | `#5fa77f` |
| Tone | `tone-success-fg` | `#15803d` | `#6ee7b7` | `#355742` | `#8fc4a3` |
| Tone | `tone-success-soft` | `#dcfce7` | `#10b9811f` | `#d6e3da` | `#5fa77f1f` |
| Tone | `tone-warning` | `#d97706` | `#f59e0b` | `#f08c00` | `#f5a524` |
| Tone | `tone-warning-fg` | `#92400e` | `#fcd34d` | `#7a4300` | `#f5c878` |
| Tone | `tone-warning-soft` | `#fef3c7` | `#f59e0b1f` | `#fbe6c4` | `#f5a5241f` |
| Tone | `tone-danger` | `#dc2626` | `#ef4444` | `#d92d20` | `#e5484d` |
| Tone | `tone-danger-fg` | `#b91c1c` | `#fca5a5` | `#a8231a` | `#f2898c` |
| Tone | `tone-danger-soft` | `#fee2e2` | `#ef44441f` | `#f9dad6` | `#e5484d1f` |
| Equipment state | `state-run` | `#16a34a` | `#10b981` | `#2f3a44` | `#5fa77f` |
| Equipment state | `state-run-fg` | `#15803d` | `#6ee7b7` | `#2f3a44` | `#8fc4a3` |
| Equipment state | `state-run-soft` | `#dcfce7` | `#10b9811f` | `#d0d5db` | `#5fa77f1f` |
| Equipment state | `state-stop` | `#64748b` | `#64748b` | `#9aa1aa` | `#5c5f65` |
| Equipment state | `state-stop-fg` | `#334155` | `#cbd5e1` | `#4a525c` | `#a8a39a` |
| Equipment state | `state-stop-soft` | `#e2e8f0` | `#64748b26` | `#eceef0` | `#8e8a8226` |
| Equipment state | `state-fault` | `#dc2626` | `#ef4444` | `#d92d20` | `#e5484d` |
| Equipment state | `state-fault-fg` | `#b91c1c` | `#fca5a5` | `#a8231a` | `#f2898c` |
| Equipment state | `state-fault-soft` | `#fee2e2` | `#ef44441f` | `#f9dad6` | `#e5484d1f` |
| Equipment state | `state-warn` | `#d97706` | `#f59e0b` | `#f08c00` | `#f5a524` |
| Equipment state | `state-warn-fg` | `#92400e` | `#fcd34d` | `#7a4300` | `#f5c878` |
| Equipment state | `state-warn-soft` | `#fef3c7` | `#f59e0b1f` | `#fbe6c4` | `#f5a5241f` |
| Equipment state | `state-manual` | `#7c3aed` | `#8b5cf6` | `#5a7590` | `#9c8ac4` |
| Equipment state | `state-manual-fg` | `#6d28d9` | `#c4b5fd` | `#3a4e62` | `#beb0dd` |
| Equipment state | `state-manual-soft` | `#ede9fe` | `#8b5cf61f` | `#d8e0e8` | `#9c8ac41f` |
| Equipment state | `state-off` | `#94a3b8` | `#475569` | `#c4c9cf` | `#3a3c40` |
| Equipment state | `state-off-fg` | `#475569` | `#94a3b8` | `#5b636d` | `#9a958c` |
| Equipment state | `state-off-soft` | `#f1f5f9` | `#47556926` | `#e6e8eb` | `#8e8a821a` |
| Alarm | `alarm-critical` | `#dc2626` | `#dc2626` | `#d92d20` | `#d03a40` |
| Alarm | `alarm-critical-fg` | `#ffffff` | `#ffffff` | `#ffffff` | `#ffffff` |
| Alarm | `alarm-major` | `#f59e0b` | `#f59e0b` | `#c86e00` | `#f5a524` |
| Alarm | `alarm-major-fg` | `#111827` | `#111827` | `#111111` | `#121314` |
| Alarm | `alarm-minor` | `#3b82f6` | `#3b82f6` | `#9c8000` | `#7f96ad` |
| Alarm | `alarm-minor-fg` | `#ffffff` | `#ffffff` | `#111111` | `#121314` |
| Alarm | `comm-alarm` | `#7a1fa2` | `#7a1fa2` | `#6b2c91` | `#7d4a9e` |
| Alarm | `comm-alarm-fg` | `#ffffff` | `#ffffff` | `#ffffff` | `#ffffff` |
| Instrument | `gauge-track` | `#e2e8f0` | `#334155` | `#c9ced4` | `#2c2e32` |
| Instrument | `gauge-needle` | `#0f172a` | `#e2e8f0` | `#1e2329` | `#e8e4dc` |
| Instrument | `gauge-needle-tip` | `#ef4444` | `#f87171` | `#2f3a44` | `#bda67e` |
| Instrument | `gauge-tick` | `#94a3b8` | `#64748b` | `#8e959e` | `#5a5750` |
| Lamp | `lamp-off` | `#3a3f47` | `#3a3f47` | `#b4bac2` | `#3a3c40` |
| Tank | `tank-empty-top` | `#f5f8fc` | `#1e293b` | `#eef0f2` | `#202124` |
| Tank | `tank-empty-bottom` | `#e6ecf3` | `#0f172a` | `#dde0e4` | `#141516` |
| Tank | `tank-frame` | `#cbd5e1` | `#475569` | `#8e959e` | `#46494e` |
| Tank | `tank-chip-bg` | `#ffffffdd` | `#0f172acc` | `#f2f3f5dd` | `#121314cc` |
| Tank | `tank-chip-fg` | `#0f172a` | `#f1f5f9` | `#1e2329` | `#e8e4dc` |
| Tank | `tank-fill` | `#3a7bd5` | `#3a7bd5` | `#6f8aa3` | `#6e8a9a` |
| Knob | `knob-face` | `#f8fafc` | `#273449` | `#f2f3f5` | `#242629` |
| Knob | `knob-fill` | `#2563eb` | `#3b82f6` | `#4a6278` | `#bda67e` |
| Knob | `knob-indicator` | `#0f172a` | `#f1f5f9` | `#1e2329` | `#e8e4dc` |
| Toggle | `toggle-on` | `#3a7bd5` | `#60a5fa` | `#4a6278` | `#bda67e` |
| Toggle | `toggle-off` | `#c7cdd5` | `#475569` | `#aab0b8` | `#3d4045` |
| Toggle | `toggle-knob` | `#ffffff` | `#e2e8f0` | `#ffffff` | `#d8d3ca` |
| Trend | `trend-grid` | `#e2e8f0` | `#263247` | `#c9ced4` | `#2a2c30` |
| Chart colors | `chart-1` | `#3b82f6` | `#3b82f6` | `#4a6278` | `#bda67e` |
| Chart colors | `chart-2` | `#22d3ee` | `#22d3ee` | `#4f8a8b` | `#7fa7a0` |
| Chart colors | `chart-3` | `#a78bfa` | `#a78bfa` | `#2f3a44` | `#c4876a` |
| Chart colors | `chart-4` | `#f59e0b` | `#f59e0b` | `#9a8455` | `#9daa7a` |
| Chart colors | `chart-5` | `#10b981` | `#10b981` | `#7a7396` | `#a58cb8` |
| Chart colors | `chart-6` | `#ef4444` | `#ef4444` | `#8fa6ba` | `#8fa3b8` |
| Scrollbar | `scrollbar-thumb` | `#64748b80` | `#94a3b880` | `#5b636d80` | `#8e8a8266` |
| Scrollbar | `scrollbar-track` | `#0f172a0f` | `#ffffff0f` | `#1e23290f` | `#ffffff0a` |
| Data grid | `grid-header` | `#eef2f6` | `#273449` | `#d5d9de` | `#242629` |
| Data grid | `grid-header-fg` | `#475569` | `#cbd5e1` | `#3f4750` | `#c4bfb5` |
| Data grid | `grid-row` | `#ffffff` | `#111827` | `#f2f3f5` | `#18191b` |
| Data grid | `grid-row-alt` | `#f8fafc` | `#151e2e` | `#eaecef` | `#1c1d20` |
| Data grid | `grid-row-hover` | `#eff6ff` | `#1c2a42` | `#e1e6ec` | `#26282b` |
| Data grid | `grid-row-selected` | `#dbeafe` | `#1d3557` | `#d3dce5` | `#3a3324` |
| Data grid | `grid-line` | `#e2e8f0` | `#263247` | `#cbd0d6` | `#2a2c30` |
| Data grid | `grid-summary` | `#e2e8f0` | `#253247` | `#dadde1` | `#26282b` |
| Data grid | `grid-summary-fg` | `#0f172a` | `#f1f5f9` | `#1e2329` | `#e8e4dc` |
| Data grid | `grid-filter` | `#f8fafc` | `#1a2436` | `#eaecef` | `#1f2023` |
| Data grid | `grid-filter-fg` | `#94a3b8` | `#64748b` | `#7d858f` | `#66625c` |
| Data grid | `grid-filter-active` | `#2563eb` | `#60a5fa` | `#4a6278` | `#bda67e` |
| Virtual keyboard | `kbd-bg` | `#d5dbe3` | `#0b1020` | `#c4c9cf` | `#0e0f10` |
| Virtual keyboard | `kbd-key` | `#ffffff` | `#273449` | `#f2f3f5` | `#242629` |
| Virtual keyboard | `kbd-key-fg` | `#0f172a` | `#f1f5f9` | `#1e2329` | `#e8e4dc` |
| Virtual keyboard | `kbd-key-special` | `#b8c2cf` | `#1a2436` | `#a9b0b8` | `#1b1c1e` |
| Virtual keyboard | `kbd-key-special-fg` | `#0f172a` | `#cbd5e1` | `#1e2329` | `#c4bfb5` |
| Also defined, not for new screens | `bg` | `#f8fafc` | `#0f172a` | `#d5d8dc` | `#121314` |
| Also defined, not for new screens | `bg2` | `#eef2f7` | `#111827` | `#cbcfd4` | `#0e0f10` |
| Also defined, not for new screens | `surface-alt` | `#f1f5f9` | `#273449` | `#dde0e4` | `#202124` |
| Also defined, not for new screens | `muted` | `#64748b` | `#94a3b8` | `#4f5761` | `#9a958c` |
| Also defined, not for new screens | `border-warn` | `#f59e0b` | `#fbbf24` | `#f08c00` | `#f5a524` |
| Also defined, not for new screens | `success` | `#15803d` | `#86efac` | `#355742` | `#8fc4a3` |
| Also defined, not for new screens | `success-bg` | `#dcfce7` | `#123524` | `#d6e3da` | `#1a2a21` |
| Also defined, not for new screens | `success-border` | `#86efac` | `#166534` | `#9db8a6` | `#2f5a41` |
| Also defined, not for new screens | `warn-text` | `#92400e` | `#fde68a` | `#7a4300` | `#f5c878` |
| Also defined, not for new screens | `warn-bg` | `#fef3c7` | `#3b2f13` | `#fbe6c4` | `#33281a` |
| Also defined, not for new screens | `shadow` | `#00000022` | `#00000066` | `#0000001f` | `#00000080` |
