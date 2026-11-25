# Changelog

## Unreleased

### Added

- `IDvValueCommitted` (`Devinno.UI`): a view model that implements `OnValueCommitted(string path, object? value)` hears every value the screen wrote, right after the write succeeded — every two-way widget, grid cells and row selection, custom widgets. Code writes, handle writes, rejected or failed writes and `DvVideo position`/`duration` are not reported, so a value polled from a device is not echoed back. `path` starts at the told object: the screen VM, or the item for `{Bind Item.X}` and grid cells. A handler exception is reported as `HandlerError` kind `"committed"` (`dvml-language.md` → After the screen wrote a value).

### Fixed

- A display format given to a component parameter is no longer lost: `<DvStat value="{Level:0.0}"/>` shows `72.0` (it showed `72`). The parameter keeps the value and its format; inside the template a binding that is exactly the parameter (`text="{value}"`, `"{value} %"`, a nested component's parameter, a custom widget's `ctx.Text`) uses that format, value attributes (`DvGauge value="{value}"`) read the number, and a format of the template's own wins. Applies to built-in (`DvStat value`, `DvCard badge` …) and user components (`DvParam`).

## 0.8.1 — 2026-11-24

Bindings take a display format, as in C# string interpolation: `text="{Temp:0.0} ℃"`. Numbers in text no longer depend on the PC's language setting.

### Added

- Binding display format `{Path:format}` (`dvml-language.md` → Display format): a .NET format string with the invariant culture, by the same rule as the widget `format` attribute. The path ends at the first `:`, so `{At:HH:mm}` works. It applies wherever the value becomes text — interpolation, element text, `style` and text attributes, including a single binding (`text="{Temp:0.0}"`). A format the value cannot use shows the default text with a Trace warning. Value attributes (`DvGauge value="{Level:0.0}"`) and two-way bindings (`{Bind X:0.0}`) keep the value as is and warn that the format is ignored.
- Build warning DVML022: an empty binding format (`{Temp:}`).

### Changed

- A bound value turned into text is written with the invariant culture: `24.5` on a German or French PC too (it was `24,5`), and dates as `11/24/2026 13:05:09`. Give dates a format (`{At:yyyy-MM-dd HH:mm}`) to choose how they look.

## 0.8.0 — 2026-11-20

A new package, Devinno.UI.Scene3D, shows a glTF 3D model in a screen and moves its parts from bound values. For it, custom widgets can read their child elements as settings. Release builds of apps get much smaller.

### Added

- `DvDefinitionWidget` and `DvDefinition` (`Devinno.UI.Widgets`): a custom widget that names the child tags it reads (`DefinitionTags`) gets them through `DvWidgetContext.Definitions`, in markup order, with attributes read like the widget's own (bindings, `DvRepeat` items, component parameters). Definitions are settings: they get no box and are not drawn. Unknown tags and children of a definition are skipped with a Trace warning.
- Package **Devinno.UI.Scene3D**: `<SceneView>` shows a `.glb` model (for example from Blender). Child elements `Rotate`, `Move`, `Color` and `Clip` turn, move and color parts or pose an animation from bound values (`follow` / `ratio` for meshed parts); `camera` switches to a camera in the model, `value` / `pick` select parts by click, `SceneState` takes values from code on any thread. Register with `registry.UseScene3D()`. Needs desktop OpenGL 3.3; on a GPU host the scene is drawn straight into a texture, on a CPU host in a hidden context. Docs: `docs/llms.txt` and `docs/scene3d.md` in the package.

### Changed

- Release builds and `dotnet publish` of an app that references Devinno.UI no longer copy the native debug symbols of SkiaSharp (`libSkiaSharp.pdb`, 84 MB for each Windows CPU). Debug builds keep them; `<DevinnoNativePdb>true</DevinnoNativePdb>` keeps them in Release. `dvml-code.md` also recommends `RuntimeIdentifier` for apps that ship to one kind of machine.

## 0.7.2 — 2026-11-18

APIs for screen editors built with Devinno.UI: edit markup and inline styles without losing how the text was written, find the element under a click, and give custom widgets the full mouse (other buttons, modifier keys, click count, hover, leave, wheel). See Breaking before you upgrade: assemblies built against 0.7.1 need a rebuild.

### Breaking

- `DvSurface.PointerDown` / `PointerUp` take optional `DvPointerButton button` and `DvModifiers modifiers`; `PointerMove` and `PointerWheel` take optional `DvModifiers modifiers`. Source compiles unchanged, but assemblies built against 0.7.1 that call these methods must be rebuilt (the method signatures changed).
- Inline `style`: a `;` inside quotes or parentheses no longer ends a declaration, so `background-image: url('a;b.png')` reads as one value.
- When the mouse leaves the host control (`ViewControl`, `ViewWindow`), hover styles turn off. Before, the last hovered element kept its hover style.

### Added

- `MarkupDocument` and `MarkupSpan` (`Devinno.UI.Markup`): parse markup and edit it by element — `SetAttribute`, `RemoveAttribute`, `InsertChild`, `RemoveElement` — while the rest of the text (comments, spacing, quotes, attribute order) stays as written. `SpanOf`, `AttributeSpan` and `NodeAt` map between elements and text positions. Each edit returns a new document.
- `StyleParser.GetDeclaration`, `SetDeclaration`, `RemoveDeclaration`: read or change one declaration of an inline style string and keep the others as written.
- `TagSchema`, `AttrDef`, `AttrKind` are public: the built-in tags, their attributes, kinds, defaults, allowed values and bindability, for property panels and completion.
- `HitTester.HitPath(root, point, wholeBox)` returns the boxes under a point from the root down; `LayoutBox.Origin` is public, so a tool maps a click to the markup element (a component instance's root box points to the instance element). `wholeBox: true` hits a widget's whole box, not only its drawn shape.
- `DvPointerButton`, `DvModifiers`, `DvPointerArgs`. Custom widgets: `OnPointerDown` / `OnPointerMove` / `OnPointerUp(ctx, DvPointerArgs)` overloads (the defaults call the existing `SKPoint` methods for the left button), `OnPointerHover`, `OnPointerLeave`, `OnPointerWheel` (return true to take the wheel instead of scrolling). Right and middle buttons go only to a custom widget under the pointer; they never click, scroll, hold or show press styles.
- `DvSurface.PointerLeave()`. `ViewControl` and `ViewWindow` pass the button, modifier keys and mouse leave.
- WinForms demo `-- editor`: a small editor canvas that selects, moves and resizes elements through these APIs.

## 0.7.1 — 2026-11-17

No code changes. The AI docs in the package (`llms.txt` and the `dvml-*.md` pages) now match the 0.7.0 look.

### Fixed

- Theme color table: `Carbon` `state-run` (and `-fg`, `-soft`), `Graphite` `alarm-minor`, `alarm-minor-fg` and `chart-3`, `Light` `lamp-off` show the 0.7.0 values.
- Process diagram colors come from the `process-*` tokens. `Light` and `Carbon` set their own `process-body-on`, so changing `state-run` alone does not recolor running process equipment in those themes (the 0.7.0 notes above say it follows `state-run`; that holds for `Dark` and `Graphite`).
- A theme built from scratch: a missing number token reads 0 and missing style switches read as `rich`, `on`, `strong`. `radius.xs` and `ratio.unit` are listed; `stroke.track`, `stroke.focus`, `focus.gap` and `row.lg` are marked as not read by built-in widgets.
- Selection look (segment, list, tree), dialog, expander, input and button sizes, title and process label weights, instrument texture, needle tip, lamp off color, `show-zone-marks`, trend legend default format and `::part()` pointers match the code.

## 0.7.0 — 2026-11-17

This release changes how every control looks: the theme now decides weights, strokes, heights, texture and selection, not only colors. It has breaking changes for custom widgets and for themes built from a full token dictionary (see Breaking).

### Breaking

- `DvWidgetContext.MeasureText` / `DrawText`: the `bool bold` parameter is now `int weight` (100–900, default 400). Replace `bold: true` with `weight: 700`.
- `ComputedStyle.Bold` is read-only (`FontWeight >= 600`); set `ComputedStyle.FontWeight` instead.
- Built-in widgets now read many more theme tokens (role tokens, style switches, `texture.depth`, `process-*`, see Added). A theme made from a complete dictionary with `new DvTheme(tokens)` lacks them: a missing color token draws magenta and a missing number token reads 0 (text of that size disappears), each with a warning. Start from a built-in theme instead — `DvTheme.Light.With(myColors)` keeps every token you do not override.
- The look changes throughout (see Changed). There is no switch back to the previous look; a theme can bring parts of it back through the tokens (for example `look.selection: strong` for filled selection, `texture.depth` for deeper shading).

### Added

- `font-variant-numeric: tabular-nums` draws every digit 0–9 at the same width, so live values do not shift.
- Theme token `texture.depth` (0–1) sets how deep the rich texture shades (process metal and pipes, tank, knob, progress, ring, lamp gloss, needle shadow, trend glow). Built-in themes use 1, `Light` 0.85.
- `DvTrendChannel format` — number format of the legend's latest value (same rule as `format` on the other widgets).
- `DvTank medium` (`water` `steam` `air` `gas` `oil` `acid`, the same colors as process pipes) and `DvTank color` set the liquid color. Order: zone color (only with `look.normal-color: on`), `color`, `medium`, `tank-fill`.
- Process diagram theme tokens: `process-line` `process-detail` `process-detail-on` `process-body` `process-body-on` `process-body-off` `process-accent` `process-liquid` `process-glass` `process-screen` `process-tag-bg` `process-tag-border` `process-tag-fg` `process-arrow` `process-pipe` `process-pipe-{water,steam,air,gas,oil,acid}`.
- `ThemeLook` (`Texture`, `NormalColor`, `Selection`), enums `LookTexture` (`Flat`|`Rich`) and `LookSelection` (`Quiet`|`Strong`), and `ThemeLook.Of(theme)` read a theme's style switches from code.
- Custom widgets: `DvWidgetContext.Look`, `TokenNumber(name)` and `StateOverlay(fill)` follow the theme the same way built-in widgets do.
- Widget parts with the CSS `::part()` selector: `DvGauge::part(value) { font-size: 28 }`, `.hot::part(value) { color: … }` (gauge, ring, knob, tank, progress, slider).
- Theme role tokens (`weight.*`, `stroke.*`, `font.meta`, `alpha.*`, `overlay.*`, `opacity.disabled`, `control.*`, `row.*`, color roles `selection` `value-input` `value-read` `normal`) and style switches `look.texture` (`flat`|`rich`), `look.normal-color`, `look.selection`. Built-in widgets read these instead of fixed numbers; `look.texture: flat` drops gloss, gradients, shadows and glow on instruments.

### Changed

- Built-in theme values that changed meaning or color (check screens that rely on them):
  - `Carbon`: `state-run` is neutral gray `#9aa0a6` (was green `#5fa77f`; also `state-run-fg`, `state-run-soft`); running lamps and status no longer use green. `normal` points to `state-run` in `Graphite` and `Carbon`.
  - `Graphite`: `alarm-minor` is blue-gray `#52708d` with white text (was yellow `#9c8000` with dark text), so minor and major alarms differ in hue; `chart-3` is `#7d8a97` (was `#2f3a44`).
  - `Light`: `lamp-off` is `#94a3b8` (was `#3a3f47`), `texture.depth` 0.85, and lighter process values (`process-line` `process-pipe` `process-accent` `process-arrow` `process-body*` `process-liquid` `process-detail-on`).
  - `Dark`/`Carbon`: dark process name tags (`process-tag-*`); `Carbon` running process body `#4a5057`.
  - All four themes: `look.selection` is `quiet`; `Graphite`/`Carbon` are `look.texture: flat` and `look.normal-color: off`.

- Input and selection controls share one height (`control.md` 40; small and large buttons `control.sm`/`control.lg`) and one corner radius (`radius.md`); list, tree, tab and expander rows are `row.md`. Button labels stay on one line.
- Weights: buttons and titles 600, tabs, segments, chips and expander headers 500 (were bold). The primary button is a solid color (no gradient).
- Borders are drawn inside the box, as in CSS (crisper 1px edges).
- Editing or open input, select, date and color boxes show an accent border instead of an outer ring. Check boxes, radios and slider thumbs keep their line width on hover (color only); the slider shows a soft halo. The off toggle knob is muted.
- Selection switch `look.selection` (`quiet` in all four built-in themes): the selected segment is a raised chip with bold `primary` text on a track in the screen `background` color, list and tree selection is a soft tint; a theme that sets `strong` fills it with `primary` (the previous look). Segment items reserve the width of the bold text, so moving the selection does not shift them.
- `content-align` belongs only to the tags that draw text (`DvLabel`, `DvButton`, `DvCheckBox`, `DvRadio`, and the horizontal part on `DvInput`). On any other tag it never did anything; the build now reports DVML019 there. Run-time behavior is unchanged.
- Expand glyphs are chevrons everywhere; the tab divider is a hairline.
- Alarm priority has one look everywhere: a chip filled with the priority color and contrast text, with the theme's `alarm-*-icon` before the text when set. Unacknowledged chips blink, acknowledged ones keep their color. The alarm list drops the left priority bar, its ACK button is neutral, and its default row height is `row.md` (40, was 36). `DvStatusItem` blinks at 500 ms like the other alarm displays.
- `DvStatusItem` shows its status as muted text; the lamp is the only color in the line. With `look.normal-color: off` the calm alarm banner is neutral.
- `DvDataGrid`: header and summary edges are hairlines, header text weight 600, numbers use tabular digits, check cells use `radius.xs`, button cells `radius.sm`; a row whose `cell-disabled` is true has no button (was a dimmed button).
- `DvTrend`: horizontal grid lines only; the number of value ticks follows the plot height; the area fill starts at 0 and is drawn only for a single channel when 0 is inside the range; the legend is name (muted), value (tabular, weight 600) and unit (smaller, muted).
- Bar chart bars round only the corners away from the baseline; chart values, the donut total and legend values use tabular digits; the pie legend name is muted. Step bar connectors and the current ring use `stroke.emphasis`.
- `DvTheme.Graphite` and `DvTheme.Carbon` use `look.texture: flat` and `look.normal-color: off`: instruments, cards and lamps are drawn flat, and tank liquid no longer changes color with the zone (limit lines and the value chip carry the state).
- Instrument values (gauge, ring, knob, tank) use tabular digits with a smaller, muted unit (`ratio.unit`); the unit is styled with the new `::part(unit)`. Value weight is 600.
- `DvGauge`: the track is a thin neutral arc (was a 10 px band); `alarm-zones` are a thin band just inside it; tick lines are under the needle and the numbers over it; the needle reaches the start of the ticks.
- `DvRing` default thickness is 1/12 of the size (was 11%); the label is regular weight at `font.label` size.
- `DvTank` zone starts are also a thin dashed line across the body. `DvKnob` indicator is a mark on the outer third of the face (clear of the value text), its value arc has round ends, and its face keeps the metal grain and shadow in every theme (also with `look.texture: flat`).
- `DvProcess` colors come from the `process-*` theme tokens instead of a fixed palette, and a diagram without `look` follows the theme's `look.texture` (rich in `Light`/`Dark`, plain in `Graphite`/`Carbon`; was always plain). Running equipment takes the theme's `state-run` color (green in `Light`/`Dark`). The default steam color is brown-gray instead of red, and flow dashes use the theme `primary`. Rich pipes take their casing from the pipe color; `Light` uses lighter process values for a white background (outlines, pipes, parts, arrows, bodies, an emerald running body, a lighter liquid, dark running details), `Dark` and `Carbon` use dark name tags. Process labels are weight 600.
- `DvStat` accent band is one solid color. With `look.texture: flat` cards, KPI tiles, dialogs and the selected segment have no shadow.
- Disabled check boxes, radios, toggles and sliders are dimmed as one group (CSS `opacity`), so a thumb or knob no longer shows the track through it.
- A unit starting with `%`, `‰` or `°` joins its value without a space (`40%`, `20°C`); other units keep one space (`3 bar`). Applies to gauge, knob, slider, tank, progress, trend legend and soft keyboard range. The slider value sits 8px above the thumb.
- `font-weight` takes 100–900 and draws the font's real face for that weight (the nearest face the installed font has). Synthetic bold is used only when 600 or more is asked for and the font has no bold face. Bold text looks crisper and slightly wider than before.
- Text is placed at subpixel positions with unhinted advance widths, so letter spacing is even at every size (before, some sizes showed uneven gaps). Text widths change by fractions of a pixel.
- Screen text no longer splits a Latin word: a word wider than its line stays whole and overflows (CSS `overflow-wrap: normal`). Korean, Chinese and Japanese still break between characters, and multi-line `DvInput` still breaks long words.
- A theme token's value can be another token (`["gauge-track"] = "token(surface-raised)"`); lookups follow it. A missing target or a cycle draws magenta with a warning.

### Fixed

- Process diagrams: a pipe no longer runs over another line's port stub (both pipes overlapped on one row, e.g. with the rich look and a valve rotated 180°). Port stubs are reserved before the pipes are routed.

## 0.6.0 — 2026-11-10

This release has breaking changes. Build an existing app against it first: most of what you need to change is reported by the build as DVML019 (an attribute the tag does not have, including renamed ones — the message names the new attribute) or DVML020 (a literal value the attribute cannot use).

### Breaking: renamed

Old markup names are not read any more (no aliases); old C# names are compile errors.

| Before | Now | Where |
|---|---|---|
| `header` | `title` | `DvExpander` |
| `title` | `label` | `DvSummaryRow` |
| `IsExpanded` | `Expanded` | `DvExpander` handle (`ExpandedChanged` unchanged) |
| `SelectedNode` | `SelectedItem` | `DvTreeView` handle (`SelectionChanged` unchanged) |
| `fit="shrink"` | `fit="scale-down"` | `DvProcess` (same behavior) |
| `width` | `thickness` | `DvSignal` (`DvVessel width`/`height` unchanged) |
| `enabled` | `cell-disabled` — **the sense flips** | `DvColumn` button cells: the item property now means *disabled*; invert it (`AckDisabled => !CanAck`) |
| `blink-when` | `blink` | `DvLamp` (`blink-when` was an alias) |
| `selection` | `selected-items` | `DvDataGrid` (same meaning as `DvListBox selected-items`) |

Markup loaded at run time (strings, hot reload) also gets a Trace warning, once per screen, for each old name it still uses.

### Breaking: one rule per kind of value

Every attribute of a kind now reads values the same way. Screens that relied on the old, per-widget readings can change.

- **True/false** (every boolean attribute and handle value): true = `true` (any case), a non-zero number or bool `true`; false = `false`, `0`, empty text or a null binding. Any other text (`yes`, `on`) uses the attribute's default with a Trace warning.
  - `disabled="2"` and process `running="-1"` / `open="-1"` are now true.
  - `show-tick-labels`, `show-zone-marks`, `show-ack`, `DvTrend area` and `smooth` are off for `0` and empty text; `smooth=""` is now off.
  - `visible` bound to a string shows only for `true` or a non-zero number; other text shows with a warning. Bind a bool instead (`HasError => !string.IsNullOrEmpty(Error)`).
- **Numbers** are read with the invariant culture everywhere; integer attributes (`hold`, `blink-interval`, process `row`/`rank`) accept decimals and round them. A value that is not a number uses the default with a Trace warning.
- **Colors** (`DvLamp color`, `DvTrendChannel color`, `DvSignal color`, grid `*-color`, `DvColumn on-color`, `alarm-zones`): `#RGB`, `#RRGGBB`, `#RRGGBBAA`, `transparent` or `token(name)`. Color names (`red`) were never supported and are now reported. A token the theme lacks draws magenta (`#FF00FF`).
- **Words** (enum attributes) are compared ignoring case and surrounding spaces; an unknown word uses the default with a Trace warning. `DvButton variant`/`size` accept only `primary` `secondary` `danger` `ghost` / `sm` `lg` (`variant="foo"` no longer adds the class `btn-foo` — use `class`).
- **`tone` and `DvLamp state`** are theme words: valid when the current theme has `tone-{word}` / `state-{word}` (so a custom theme's own words work); words ending in `-fg` or `-soft` are rejected.
- **`alarm-zones`**: a zone that cannot be read is dropped with a warning and the screen builds (before: the screen failed to build).
- **Ranges**: `min >= max` is not a range — knobs, sliders, gauges, rings, progress bars, tanks, numeric `DvInput` and grid input cells use 0..100 with a warning. The `DvKnob` keypad rejects a value outside `min`–`max` instead of clamping it. `DvApp.InputNumber` throws for `min == max` too.
- **`format`**: a format that cannot be used for the value shows the default text with a warning; `DvInput` then shows the raw value.

### Breaking: other

- **Unused attributes** that a tag read but never drew are no longer attributes of that tag (DVML019), for example `DvGauge tone`/`label`/`sweep`, `DvTabs placeholder`/`items`, `DvPieChart area`. Child elements that are not boxes (`DvOption`, `DvColumn`, `DvTrendChannel`, process `DvPort`, `DvLine`, `DvSignal` …) take no common attribute except `id`.
- **Process names and references** (`name`, `DvLine`/`DvSignal` `from`/`to`, `DvInstrument on`/`tap`) are literal, not bindable.
- **`onbeforecommit`**: one lookup for every value widget and grid cell — `bool M(item, T)`, `bool M(item, object)` (inside `DvRepeat` and for grid cells), then `bool M(T)`, `bool M(object)`. A method whose parameter is a base type or interface other than `object` is no longer found. `DvSwitch` now calls it.
- **Components**: `ComponentRegistry.Register` throws for a `<DvComponent>` named like a built-in tag (`DvGauge`, `DvStack` …); build error DVML021. Built-in components (`DvCard`, `DvPageFrame` …) can still be replaced.
- **Theme colors**: every color a built-in widget draws is a theme token. A custom theme made with `new DvTheme(...)` that lacks a token a widget uses shows magenta there (themes made with `DvTheme.Dark.With(...)` have every token). New tokens: `lamp-off`, `tank-fill`, `chart-1`…`chart-6`, `comm-alarm-fg`.
- **Canvas**: what the root does not paint is cleared with the theme `background` token (before: white in every theme).
- **Warnings**: markup and binding Trace warnings are written once per screen for each (tag, attribute, markup expression).
- **Docs**: the single `dvml-ui-markup.md` is replaced by topic pages (`dvml-language.md`, `dvml-widgets.md`, `dvml-app.md` …). Start from `llms.txt`.

### Added

- **`DvVideo`** — plays video files with FFmpeg 8 (`src`), or shows frames from code (`source` = `FrameSource`). `VideoPlayer` for code control (`Seek`, `Position`, `Duration`, `Ended`, `Loop`). `position="{Bind P}"` / `duration="{Bind D}"` for a seek bar.
- **Sound** for `DvVideo` with SDL2, in sync with the picture (the picture follows the sound clock; a late picture is skipped). Files with only sound play too. `volume` and `muted` (`VideoPlayer.Volume`, `VideoPlayer.Muted`). FFmpeg 8 and SDL2 are not in the package — see the README "Video and sound".
- **Built-in themes** `DvTheme.Graphite` (ISA-101 style gray for bright control rooms) and `DvTheme.Carbon` (dim, warm dark for night control rooms).
- **`readonly`** on `DvToggle`, `DvCheckBox`, `DvRadio`, `DvKnob` and `DvSlider`, with one read-only look (dashed outline) on every value widget.
- `format` on `DvSlider`, `DvBarChart` and `DvPieChart`; `show-value` on `DvGauge` and `DvRing`; `DvGauge unit` is drawn.
- `DvLine` and `DvSignal` `visible`.
- `DvDialog.BoundsOf(id)` and fixed ids on the built-in boxes (`yes`, `ok`, `no`, `cancel`, `input`, `list`).
- `ViewControl.Gpu` (WinForms) — paint with OpenGL, falling back to the CPU.
- Build diagnostics DVML019 (attribute the tag does not have), DVML020 (literal value the attribute cannot use), DVML021 (component named like a built-in tag).

### Fixed

- `DvGrid align`/`justify` place each child in its cell; grid boxes keep `position:absolute` children out of the cells.
- The `DvGauge` value text fits between the arc ends.
- A `DvSummary` on the first column warns (the first cell holds the row label).
- Tabs, expanders and trees remember their state per instance; a component root keeps its state when the instance passes attributes.
- `DvSegment` converts the tapped option to the bound property type.
- `DvAlarmList` stops blinking for a communication alarm it does not draw.
- Process equipment honors the handle's `Visible` override; unit component instances pass `style`, `onclick` and handles.
- `ViewWindow` `fit` margins use the theme `background` token.
- The `DvAlarmList` ACK button text is localized (`dv.alarm.ack`).
