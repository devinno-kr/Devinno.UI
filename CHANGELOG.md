# Changelog

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
