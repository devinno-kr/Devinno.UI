# Devinno.UI Authoring Rules

Covers: the shape of a screen, core principles, choosing the built-in widget for the job, layout differences from CSS, symptom → cause → fix, code-side MUST/NEVER rules (handles, dialogs), which redraw call to make, how to verify a screen (build, PNG, driving the app without a window), what is not supported, where the C# API is and which namespace each type is in. This is the page to read first when building screens; it is for both humans and AI.
Read first: [the document map in llms.txt](llms.txt) — then this page, then [markup basics, values and binding](dvml-language.md#markup-basics).
Not here: [style keys](dvml-style.md#style-keys) · [theme tokens](dvml-style.md#theme-tokens) · [widget reference](dvml-widgets.md#semantic-components) · [input widget reference](dvml-inputs.md#input-common) · [build diagnostics DVML001–021](dvml-diagnostics.md#build-diagnostics) · [thread contract](dvml-app.md#threads) · [dialog reference](dvml-dialogs.md#dialog-flow) · [process diagrams](dvml-process.md#process-overview) · [screens from a picture](dvml-image.md#image-method)

Devinno.UI is a .NET 9 UI library that draws directly with SkiaSharp. Screens are written as XML markup with a CSS-like inline `style` and bound to a C# view model (VM). It is not a browser: **even where it looks like HTML/CSS, it is not the same engine.** Knowing the differences below avoids most mistakes.

<a id="screen-shape"></a>
## Shape of a screen

One screen is a `partial` class deriving from `DvPage` (it doubles as the VM) plus a dvml file whose root `vm="…"` names that class. `DvApp` holds the pages, and a host shows the app (here the OpenTK `ViewWindow`).

```csharp
using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Pages.Add(new MainPage());                  // the first page added is the start page
using var window = new ViewWindow(app, 1280, 800, "My HMI");
window.Run();

public sealed partial class MainPage : DvPage   // the VM of MainPage.dvml; the source generator connects them
{
    public double Temperature { get; set; } = 21.5;
    public bool AutoMode { get; set; }
    public string ModeText => AutoMode ? "Auto" : "Manual";
    public void Reset() => Temperature = 0;
}
```

`MainPage.dvml`:

```xml
<DvPageFrame vm="MainPage" title="Line 2" subtitle="Live">
  <DvCard title="Reactor R-101">
    <DvRow>
      <DvLabel class="label muted" text="Temperature"/>
      <DvLabel class="title" text="{Temperature}"/>
      <DvSpacer/>
      <DvLabel class="body" text="{ModeText}"/>
      <DvToggle value="{Bind AutoMode}"/>
      <DvButton variant="danger" text="Reset" onclick="Reset" hold="1500"/>
    </DvRow>
  </DvCard>
</DvPageFrame>
```

- `vm` is the full class name including the namespace (`vm="MyApp.Pages.MainPage"`); the samples declare classes without a namespace, so they use the bare name.
- `{X}` reads a VM property; `{Bind X}` on a value widget also writes user input back; `onclick` names a public VM method. Details: [binding](dvml-language.md#binding), [click](dvml-language.md#click).
- The csproj and the source generator: [project setup](dvml-code.md#project-setup). A full app with bars and a dialog: [app example](dvml-app.md#app-example). Choosing the host package: [llms.txt](llms.txt) and [hosts](dvml-app.md#hosts).

<a id="principles"></a>
## Core principles

1. **Not in the docs = does not exist.** Tags, attributes, style keys and values not listed in these pages are not supported. Features that "should obviously work" in CSS or other UI frameworks (`margin`, `%` widths, ellipsis, `display:none`, animations, etc.) do not work unless documented. If the docs and the code disagree, the code is right.
2. **Project tags exist only when registered.** Components (`DvComponent`) and custom widgets (classes registered with `RegisterWidget`, see [DvWidget](dvml-components.md#dvwidget)) exist when the app registers them — use them with the parameters/attributes their definition or class reads. **NEVER** invent such a tag. Write a new `DvWidget` class only when the user asks for one or no built-in widget/component can show it.
3. **Use semantic components first.** `DvPageFrame`, `DvCard`, `DvStat`, `DvBadge`, `DvStatusItem`, `DvSwitch` and `DvButton variant="…"` get their look from the theme. Build layout with `DvStack`, `DvRow`, `DvGrid` and `DvSpacer`. Use `DvBox` + `style` only for special shapes these cannot produce.
4. **Use theme tokens for colors and spacing.** `token(surface)`, `token(text-muted)`, `token(spacing.lg)`, `token(radius.md)` make the screen adapt across the built-in themes Dark, Light, Graphite and Carbon ([token list](dvml-style.md#theme-tokens)). Hardcoded colors like `#1e293b` break the theme; use them only when a color truly must be fixed, such as a brand color.
5. **Always give text a color.** The default text color of `DvLabel` is `#222222` regardless of theme, and it is **not inherited from the parent**. In the dark theme (the default), a bare `<DvLabel text="…"/>` without a class is almost invisible. Add a typography class (`class="body"`, `class="label muted"`, `class="title"`) or `style="color: token(text)"`.
6. **Data and logic are the app's (VM's) responsibility.** Widgets draw VM values and write user input to the VM once, nothing more. Alarm storage/ACK and the conditions for conditional colors are written in VM code. Filters that reduce the app data itself go in VM code; column filters the user picks on screen use `DvColumn filter` ([grid filters](dvml-grid.md#grid-filters)).
7. **When values change outside input, request a redraw.** Clicks, toggles, knobs and input commits redraw automatically; changes from timers or communication need `App.Invalidate()` (or `App.Surface.Repaint()` when only grid cell values changed). Which call on which object: [updates and threads](#updates-threads).
8. **Verify the result visually.** Most errors are silently ignored, so after writing markup build, render a PNG with `DvRenderer` and look at it yourself: [verify](#verify).

<a id="choose-widget"></a>
## Choosing the built-in for the job

| Need | Use | NEVER |
|---|---|---|
| Page, card, number tile, status chip, on/off switch | `DvPageFrame`, `DvCard`, `DvStat`, `DvBadge`, `DvStatusItem`, `DvSwitch`, `DvButton variant="…"` | style plain boxes to imitate them |
| Layout | `DvStack`, `DvRow`, `DvGrid`, `DvSpacer`; `DvBox` + `style` only for special shapes | |
| On/off | switch look `DvToggle`/`DvSwitch`; box next to text `DvCheckBox`; button look = latching button `<DvButton value="{Bind X}"/>` | |
| One of 3–5 options | `DvSegment` (+`DvOption`) | imitate selection with several buttons' `onclick` and class bindings |
| One of several with long descriptions | vertical `DvRadio` — the same `value="{Bind X}"` + each its own `option`; the group is defined by the value, not by placement | |
| One of many | `DvSelect` (collapsed) or `DvListBox` (expanded list; multi-select with `selection-mode="multi"` + `selected-items`) | |
| Pick from a hierarchy (plant → line → equipment) | `DvTreeView items="{Roots}" item-children="Kids" item-text="Name" value="{Bind Picked}"` — the app objects as-is | create tree-only node types |
| Step a numeric setpoint up/down | `<DvInput keyboard-type="numeric" step="…" show-stepper="true"/>` | build it from two −/+ buttons and handlers (misses range, validation and formatting) |
| Drag a value within a range (opening, brightness) | `<DvSlider value="{Bind X}" step="…"/>` (orientation via `direction`, as on `DvProgress`); round handle `DvKnob` | |
| Show a value without letting the user change it | one-way `{X}`, a literal, or `readonly="true"` on the value widget ([value widgets](dvml-language.md#value-widgets)) | |
| Equipment state numbers as lamps | bind VM computed properties (`StateKey` → `state-*` token key, `StateText`, `IsFault`) to `state`/`status`/`blink` of `DvStatusItem`/[`DvLamp`](dvml-instruments.md#dvlamp); there is no state table widget | |
| Current step of a process/batch sequence | `<DvStepBar steps="{Steps}" current="{Step}"/>` (`current` starts at 1, 0 = not started) | |
| Small trend · per-item comparison · proportions | `DvSparkline values="{List}"` · `DvBarChart` · `DvPieChart` (`items` + `item-value` + `item-text`, a single series) | |
| Time-axis data | `DvTrend` ([trend](dvml-instruments.md#dvtrend)) | |
| Switch content groups within one page | `DvTabs`/`DvTab` (same VM) | put a page inside a tab (no pages inside pages) |
| Expand/collapse a section | `DvExpander` (`expanded="{Bind X}"`; omitted → the screen remembers it); only one open at a time = `DvRepeat` + `DvExpander` + an item setter ([DvExpander](dvml-widgets.md#dvexpander)) | |
| Change the whole screen | page switch `App.Show<T>()` | |
| Guard a dangerous command (start, stop, reset) | `hold="1500"` (`onclick` fires after it is held that many ms, [hold](dvml-language.md#hold)) or a confirmation box `App.MessageBox` | build long-press with a timer |
| Confirm an alarm ACK | inside the `onack` handler ([code dialogs](#code-dialogs)) | an `ack-confirm` attribute (it does not exist) |
| Ask for one text, one number, one list item | `App.InputBox`, `App.InputNumber`, `App.SelectBox` ([code dialogs](#code-dialogs)) | a new dialog class + dvml for a single question |
| Lock the screen while the operator is away | `App.Lock<LockDialog>(…)` ([lock and idle](dvml-dialogs.md#lock-idle)) | a lock screen shown with `ShowDialog` |

<a id="css-differences"></a>
## Layout differences from CSS

| Item | Devinno.UI | Difference from CSS |
|---|---|---|
| `DvBox` layout | Without `display:flex` it **only stacks children vertically**. `gap`, `justify-content`, `align-items` apply; `grow` (stretching) does not work. [DvBox](dvml-layout.md#dvbox) | Like block flow, without stretching |
| `align-items` default | `stretch` — children without a size fill the parent; children with `width`/`height` get that size. Fixed-shape widgets (knobs, toggles) are drawn centered at their own size; only the area inside the shape is pressable. | Same as CSS |
| `flex-shrink` default | `0` — elements do not shrink even when overflowing; use `flex-shrink:1` or `flex:1`. An `overflow:scroll` box defaults to `1` if unspecified. [size](dvml-layout.md#size) | CSS default is `1` |
| `DvStack` | Vertical flex, `align-items:stretch`, `gap` 12 — children fill the width. [layout sugar](dvml-layout.md#layout-sugar) | |
| `DvRow` | Horizontal flex, `align-items:center`, `gap` 12 — children vertically centered. | |
| `DvGrid` | `align`/`justify` place each child in its cell; default `stretch` fills the cell, and `justify` takes `stretch` instead of `between`. [grid cells](dvml-layout.md#grid-cells) | CSS grid `justify-content` moves the tracks; here it places items like `justify-items` |
| Root element | Always fills the window; the root's `width`/`height` are ignored. | |
| Units | All numbers are px; `16`, `16px`, `16dp` are the same. No `%`, `em`, `rem`, `vw`, `auto` — the only exception is `%` in grid tracks and grid column widths. [lengths](dvml-language.md#value-length) | |
| Unsupported length | `width:50%`, `width:auto`, `height:2em` become **0, not ignored** — the element disappears. Only `font-size` falls back to 14 when it cannot be parsed. | |
| Text | Longer than the available width → wraps at spaces (at characters if there are none) and grows in height. `white-space:nowrap` (inherited) forces one line; `\n` always breaks. **`DvRow` children get no wrap width and stay on one line** — give that child `width`/`max-width` to wrap. Buttons and chips with icons are single-line. [text wrap](dvml-layout.md#text-wrap) | In CSS row children shrink and wrap, and `\n` is whitespace |
| Hiding | `visible="false"` removes the element; `display:none` does not hide. | |
| Spacing | `margin` (and `margin-top` etc.) is **not applied**; use the parent's `gap` and the element's own `padding`. One side only: a single-side key such as `padding-left` (as in CSS). | |
| Opacity and motion | `opacity` (0–1), `transform` (translate, scale; drawing only), `transition` (CSS syntax). Animate state changes with `transition` — do not step values with a timer. [transition](dvml-style.md#transition) | |
| State styles | `hover-style`, `:hover` etc. change **drawing only**; changing size, position or padding leaves the layout unchanged. [state styles](dvml-style.md#state-styles) | |
| Scroll box | Scrolls only when its size is constrained: `max-height`, `height`, or `grow="1"` in a fixed-height flex chain. [scroll](dvml-layout.md#scroll) | |
| Widget size | Gauge and input widgets have a fixed default size (`DvGauge` 120×120); for other sizes give numbers or stretch with `grow`/stretch. | |

Only `font-family` and `white-space` are inherited.

<a id="symptoms"></a>
## Symptom → cause → fix

Knowing how mistakes show up finds the cause quickly when you look at the result PNG. "Build" names the build diagnostic that catches it ([all codes](dvml-diagnostics.md#build-diagnostics)); "—" means nothing reports it at build time.

**Nothing on the screen, or wrong look**

| Symptom | Cause | Build | Fix |
|---|---|---|---|
| Text is invisible | `DvLabel` text color defaults to `#222222`, not inherited ([principles](#principles)) | — | Typography class or `style="color: token(text)"` |
| An element disappeared | Unsupported length (`width:100%`, `auto`, `2em`) becomes 0 | — | `DvStack` (stretch) or `grow="1"`; numbers in px |
| Magenta (`#FF00FF`) area | Unknown theme token (typo such as `token(surface-2)`), unknown `tone` on `DvBadge`/`DvStat`, or a theme made from scratch that lacks a token a widget uses | DVML013 (literal tokens) | Fix the name; derive themes with `DvTheme.Dark.With(...)` — [magenta rules](dvml-diagnostics.md#magenta) |
| A style key has no effect | Unknown style key (`margin-top`, `line-height`, `font-style`, `row-gap`, …) is ignored | DVML012 | Use a [supported key](dvml-style.md#style-keys) |
| An attribute has no effect | Unknown or misspelled attribute (`onClick`, `Text`) — tag and attribute names are **case-sensitive**; or an attribute this tag does not have even if a similar tag has it (`sweep` is `DvRing` only; `placeholder` is not on `DvTabs`); or `style`, `class`, `onclick`… on a child element that is not a box (`DvOption`, `DvColumn`, `DvTrendChannel`, `DvSummary`, process `DvPort`, `DvMotor`, `DvLine`, `DvSignal` …) | DVML019 | A tag has only the attributes it uses; of the common ones, non-box children take only `id` — [which tag takes what](dvml-language.md#common-attribute-matrix) |
| A tag draws as a plain box | Unknown tag (`DvPanel`, `div`) is treated as `DvBox`, no error | — | Use a listed tag |
| Markup root `<DvPage>` shows no page frame | The markup page root is `<DvPageFrame>`; the C# `DvPage` is a different thing — the screen class the app holds | — | Root `<DvPageFrame vm="…">` |
| A style color does nothing | Unparseable color in a style (`red`, `rgb(…)`, `hsl(…)`): only that declaration is ignored (previous value kept) | — | `#hex` or `token(name)` — [colors](dvml-language.md#value-color) |
| Alignment ignored | Unsupported value (`justify-content:space-around`, `align-items:baseline`, `text-align:justify`) is treated as the default (start / left) | — | Use a listed value |
| `padding` ignored | 5 or more values; only 1–4 work (3 = top, left/right, bottom — as in CSS) | — | [value formats](dvml-style.md#style-values) |
| Bound text is blank | Property path not on the VM (`{Temprature}`) gives an empty value (null) | — | Fix the path — [binding](dvml-language.md#binding) |
| Icon missing | Unknown icon name: not drawn (one Trace warning) | — | [icons](dvml-app.md#icons) |
| Picture missing | `DvImage src` or `background-image: url(…)` file missing: empty box / no picture | — | Check the file path |
| Video box shows a one-line reason | `DvVideo src` file missing or unreadable, or FFmpeg 8 shared libraries not found (one Trace warning); FFmpeg is not in the package | — | [DvVideo](dvml-widgets.md#dvvideo) |
| Video box empty | `DvVideo source="{X}"` where X is not an `IFrameSource` (a path string, an `SKImage`); Trace warning | — | A file path goes in `src`; frames from code go through a `FrameSource` |
| Screen is dark though a light one is wanted | No theme given → `DvTheme.Default` (= `DvTheme.Dark`): style tokens and widget-internal colors (toggles, gauge tracks, tanks, grids, virtual keyboard) are all dark | — | Set `DvTheme.Light` (`DvTheme.Graphite` = ISA-101 style gray for bright control rooms, `DvTheme.Carbon` = dim warm dark for night control rooms) — [built-in themes](dvml-style.md#built-in-themes) |
| Built-in widget text is English on a Korean screen (alarm columns, input errors, dialog and picker buttons) | Built-in text is English by default | — | Korean: `app.Strings = new DvStrings { Language = "ko" }`; other languages: add the `dv.` keys to your table — [localization](dvml-app.md#localization) |
| Gauge value text runs into the arc ends | Value + unit of 8 characters or more; the text scales with the gauge, so a bigger gauge does not help | — | Unit in a separate label, or `show-value="false"` — [DvGauge](dvml-instruments.md#dvgauge) |

**Layout**

| Symptom | Cause | Build | Fix |
|---|---|---|---|
| A label sits at the top of its grid cell; a toggle sits in the middle of a wide grid cell | `DvGrid` `align`/`justify` default to `stretch`: every child fills its cell | — | `align="center"` and `justify="start"` on the `DvGrid` (or `align-self` on one child) — [grid cells](dvml-layout.md#grid-cells) |
| `grow="1"` has no effect | The parent is a non-flex `DvBox` | — | Parent `DvStack`/`DvRow` or `style="display:flex"`; the parent also needs spare space in that direction (height, for a vertical stack) |
| A widget stretches to the parent's width | `align-items` default `stretch`, as in CSS | — | Parent `align-items:start`, child `align-self:start`, or a `width`. `DvRow` uses `align-items:center`, so no vertical stretch |
| Spacing with `margin` does nothing | `margin` is not applied | — | Parent `gap` or `padding`, or an empty box `<DvBox style="height:12"/>` |
| Long text inside `DvRow` pushes its neighbors | `DvRow` children neither wrap nor shrink | — | Give that text `width`/`max-width` (it wraps at that width), or move it into a `DvStack` |
| Long text split into one label per line | Not needed: **long text is one label**; the engine wraps it to the available width (window size, theme font size) and re-wraps it | — | One label; `white-space:nowrap` for one line |
| A scroll list does not scroll and fully expands | The scroll box size is not constrained, or `grow="1"` is given but a non-flex box (`DvBox`, `DvScreen`) sits in the parent chain | — | `style="max-height:320; overflow:scroll"`, or connect from `DvPageFrame` down with `DvStack`/`DvCard` and `grow="1"` — [scroll checklist](dvml-layout.md#scroll-checklist) |
| Attribute `width="100"` has no effect | A class on the same element with `width` wins: the attributes `width`, `height`, `grow` and layout sugar attributes have the lowest priority | — | Write it in the inline `style` — [priority](dvml-style.md#priority) |
| A class's `:hover` does not show | The same property in the inline `style` beats even class state styles | — | Give per-state values with the `hover-style` attribute |
| A disabled button shows no hover/press look | Normal: when disabled (including inherited) hover/press styles are not used; only `disabled-style` (or the default look) | — | [disabled](dvml-language.md#disabled) |
| XML parse error on `&` or `<` | XML special characters | DVML001 | In text write `&amp;` and `&lt;`; in attributes `"` as `&quot;` — [markup basics](dvml-language.md#markup-basics) |

**Behavior**

| Symptom | Cause | Build | Fix |
|---|---|---|---|
| A click does nothing | Handler method not on the VM (`onclick="Strat"`); where it is not checked (inside a `DvComponent` definition) nothing happens at runtime; a typo in a code handle's `Click` is a compile error | DVML018 (dvml with `vm`) | Fix the name |
| A click does nothing, no build error | The handler threw: VM exceptions do not stop the screen; they are logged to Trace and reported via `HandlerError` | — | Subscribe `DvApp.HandlerError` or watch Trace — [HandlerError](dvml-language.md#handler-error) |
| A typed or toggled value does not change | Misspelled `onbeforecommit` name, wrong signature, or a throwing setter: the value is rejected | DVML018 (misspelled name) | Check Trace; signature: [onbeforecommit](dvml-language.md#onbeforecommit) |
| A button click inside a box with `hover-style` does not reach the outer `onclick` | A click stops at the **first** ancestor with `onclick` or a hover/press style, and at disabled elements (including inherited), so pressing a disabled area does not call the outer `onclick` | — | [click](dvml-language.md#click) |
| A whole card is not pressable | — | — | Give the instance `onclick`: `<DvCard title="Pump" onclick="OpenPump"/>`. It is passed to the template root (the card box); the method is looked up on the screen VM (inside `DvRepeat`, `OpenPump(item)` takes priority); the instance's `onclick` wins over the template root's. `disabled` is passed to the root box the same way and, being inherited, disables every widget inside the template — [component instances](dvml-components.md#component-instances) |
| Changed a value from a timer, the screen does not update | No redraw is requested; pages have no `Invalidate()` of their own | — | `App.Invalidate()` (`App.Surface.Repaint()` for grid cell values) — [updates and threads](#updates-threads) |
| `vm="MainPage"` works in samples but not in my project | `vm` is the **full class name including the namespace**; the doc samples declare classes without a namespace, the only case where the bare name is correct | DVML005 | `vm="MyApp.Pages.MainPage"` |
| Page switching seems not to work | `Show` applies on the next frame: right after `App.Show<T>()`, `App.CurrentPage` is the previous page. `Show<T>()` throws when no page of that type is in `Pages` (added with `Pages.Add`) or more than one of that type is | — | Use the instance `Show(page)` for several of one type — [DvPage](dvml-app.md#dvpage) |
| The current-page highlight on a bar button does not change | The highlight comes from a class binding | — | Bind `class="{X}"` to a computed property that reads `App.CurrentPage` — [current page](dvml-app.md#current-page) |
| Reading `App` in a page constructor throws `InvalidOperationException` | There is no app until `Pages.Add` (for bars, until the area assignment) | — | Use `App` in handlers, `OnLoaded`, `OnShown` — [page lifecycle](dvml-app.md#page-lifecycle) |
| Changed values in existing grid rows, the order did not update | Sorting is re-applied only on a header tap, a different `items` list, a change in row count, or (while a filter is active) every `Repaint()`/`Invalidate()` | — | To re-sort after values change, assign a new list and call `Invalidate()` — [sorting](dvml-grid.md#grid-sorting) |
| A `DvSummary` shows nothing | It is on the first column, whose summary cell always holds the row's `label` | — | Put it on another column — [summary rows](dvml-grid.md#grid-groups-summary-colors) |
| `DvAlarmBanner` has no ACK button | By design: the whole bar is the ACK target | — | Press the bar (`onack` set); for a visible button use `DvAlarmList` — [DvAlarmBanner](dvml-instruments.md#dvalarmbanner) |
| A stepper −/+ press does nothing and shows no error | `onbeforecommit` rejected it (the widget shows nothing), or the value is at `min`/`max` | — | Show the reason through a VM property and a label — [DvInput](dvml-inputs.md#dvinput) |
| A grid filter on a string column opens as an input box instead of a list | `filter="auto"` on a string column is `text` | — | `filter="values"` to pick from a fixed set — [filters](dvml-grid.md#grid-filters) |

**The screen does not appear (exception)**

| Cause | Exception | Build |
|---|---|---|
| XML syntax error (unclosed tag, `&` not written as `&amp;`, missing attribute quotes) | Parse exception | [DVML001](dvml-diagnostics.md#dvml001) |
| Component expanding itself recursively (depth over 64) | `InvalidOperationException` | — |
| Missing `name` or template root when registering a `DvComponent` | `ArgumentException` | — |

Other problems are reported only as `System.Diagnostics.Trace` warnings (value rules and their warnings: [value rules](dvml-language.md#value-rules); message index: [Trace warnings](dvml-diagnostics.md#trace-warnings)).

<a id="code-rules"></a>
## Code-side rules

- **NEVER** attach two `DvApp`s to windows/controls in one process (the second throws `InvalidOperationException`). There is one app ([DvApp](dvml-app.md#dvapp)).
- The thread rules for VM code are in [threads](dvml-app.md#threads); the redraw calls are in [updates and threads](#updates-threads).

<a id="code-handles"></a>
### Controls from code (`id`, `vm`)

Full usage: [id and vm](dvml-code.md#code-id-vm). Only the principles to follow are listed here.

- **`id` and root `vm` are read by the source generator**, which creates VM fields; duplicates and invalid names are compile errors. In hosts that use markup only without the generator (`DvRenderer` etc.) they have no effect on the screen.
- **Bind state first.** Bind current state — value, enabled, visibility — with `text="{X}"`, `disabled="{Locked}"`, `visible="{Show}"`. `visible`/`disabled` take true/false: bind a `bool` property such as `HasError => Error != ""`, not the text itself.
- A handle (`btn.Enabled = false`) is a tool for one-off actions or when you need a position (placing a popup with `Bounds`, subscribing to `Click`).
- **When code writes, code is the owner.** Once you write `Enabled`, `Visible` or `Text` through a handle, that property's markup value (including bindings) is ignored from then on. Do not drive one property with both a binding and a handle. Revert with `ResetOverrides()` (text only: `Text = null`). [code writes](dvml-code.md#code-writes)
- **Handle fields are `null` in the VM constructor.** Do setup such as subscribing to `Click` in `partial void OnLoaded()`, called after the first connection to the screen; it runs once per VM instance.
- **Writes work from any thread, reads reflect the last layout.** `Enabled = false` is readable immediately; a write of `true` becomes readable after the next layout. Read `Bounds` on the UI thread. [code reads](dvml-code.md#code-reads)
- **`id` inside `DvRepeat` or `DvComponent` templates has no handle** (warning DVML004). Use bindings and an `onclick` handler that takes the item: `onclick="Select"` → `public void Select(Item item)` — write only the method name; the item is passed automatically. [limits](dvml-code.md#code-limits)
- `Click` fires only on the normal click path (not on toggle, input, select, grid and alarm widgets). Subscribing to `Click` alone makes that element take the click, so it does not leak to an outer `onclick`. [Click](dvml-code.md#code-click)
- `Bounds` is in window coordinates; `left`/`top` of `position:absolute` are relative to the parent's content box, so to use `Bounds` as-is, place the element under a root without padding. [handles](dvml-code.md#handles)
- If a class deriving from `DvPage` or `DvBar` is the VM, the generator also creates the markup wiring ([DvPage](dvml-app.md#dvpage)). Each page has its own class and its own dvml.
- A VM is connected to only one dvml (`vm="…"`); two dvml files naming one VM is DVML007.
- Value widgets, two-way binding, `readonly` and the handle members of each widget: [value widgets](dvml-language.md#value-widgets).

<a id="code-dialogs"></a>
### Dialogs (`DvDialog`, `ShowDialog`, `MessageBox`, `InputBox`, `InputNumber`, `SelectBox`)

Full usage and examples: [dialog flow](dvml-dialogs.md#dialog-flow); the smallest example is `SetpointDialog` in the [app example](dvml-app.md#app-example). Like pages, dialogs are **created, held and closed by the app (`DvApp`)**. Always follow the rules below.

**Showing**
- **MUST** show dialogs only with `await App.ShowDialog<TDialog, TResult>(constructor args…)`. The app creates the instance with the public constructor matching the arguments.
- **NEVER** try to show an instance created with `new MyDialog(…)` — no such API exists. `ShowDialog` does not return the instance; to reach it from outside, look it up in `App.Dialogs`.
- Constructor arguments must be **directly assignable** by type: no numeric conversion (`int` → `double`), no filling of optional parameters. No match, or more than one → `ShowDialog` throws `ArgumentException` at the call site. [show](dvml-dialogs.md#dialog-show)
- A single array argument (`string[] names`) is one argument. **Wrap with `(object)items` only when passing a single `object[]`** (otherwise its elements expand into the argument list).
- **NEVER** touch `App` or the UI in a dialog constructor. It runs on the thread that called `ShowDialog` (possibly a communication thread), and there is neither a screen nor an `App` at that time (reading `App` throws `InvalidOperationException`). Store the received values only; do setup in `partial void OnLoaded()`. [lifecycle](dvml-dialogs.md#dialog-lifecycle)

**Definition**
- Dialog = a `partial class` deriving from `DvDialog<TResult>` + a dvml whose root is `<DvDialogFrame vm="Namespace.Class" title="…">`. **Write `vm` including the namespace** (otherwise DVML005). [define](dvml-dialogs.md#dialog-define)
- Use `DvDialogFrame` as the root. Another root tag without a background is transparent, and the page below shows through.
- Size comes from `width`/`height` in the root `style` (otherwise the content size, with text wrapping within window width − 32). It never exceeds the window − 16 per side and is clipped when overflowing, so put long content in an `overflow:scroll` box (e.g. `max-height:320`) — when the window is short that box shrinks and the buttons stay inside the frame.

**Result**
- **MUST** use a result type distinguishable from cancel (`default`): reference types (`Recipe`, `string`), `bool?`, `int?`, `double?`, `DvDialogResult`. **NEVER** `bool`, `int`, `double` — closed from outside they give `false`/`0`, indistinguishable from "No" or "0".
- Inside the dialog `Close(result)` sets the result; `Close()` is cancel (`default`). Whichever way it closes (button, `CloseDialogs`, the dialog below closing, window closing) the result arrives exactly once.
- An edit dialog edits a **copy** and returns the new value as the result; applying it to the original is the caller's job (data is the app's responsibility). [patterns](dvml-dialogs.md#dialog-patterns)
- Message box: `await App.MessageBox(text, title, DvMessageButtons.YesNo)`. Closed from outside it returns `DvDialogResult.Cancel` regardless of the button set; check "Yes" with `== DvDialogResult.Yes` (not `!= No`). [message box](dvml-dialogs.md#messagebox)
- Confirmation of an alarm ACK goes in the `onack` handler, not in a widget attribute: `public async void Ack(AlarmRow r) { if (await App.MessageBox(…, DvMessageButtons.YesNo) == DvDialogResult.Yes) Log.Ack(r.Id); }`. **NEVER** write `ack-confirm` (it does not exist and is silently ignored). Example: [DvAlarmList](dvml-instruments.md#dvalarmlist).

**Built-in boxes — one value, one choice** ([built-in boxes](dvml-dialogs.md#builtin-boxes))
- **MUST** use the built-in boxes for one text, one number or one list item: `await App.InputBox(title, initialValue)` → `string?`, `await App.InputNumber(title, initialValue, min, max)` → `double?`, `await App.SelectBox(title, items, initialSelection)` → `int?` (the chosen index). **NEVER** create a new dialog class and dvml for a single question. Build your own only to edit several values together or when validation or preview is needed.
- **MUST** treat a `null` result as cancel (the cancel button, `CloseDialogs`, the dialog below closing, window closing). Receive it as `if (await App.InputNumber(…) is { } v)`; **NEVER** erase null with `?? 0`, `.Value` or `!`. For `InputBox`, `""` is empty input, not cancel.
- `InputNumber` rejects out-of-range and non-numeric input, so the result is always within `min`–`max` (`null` = no limit on that side).
- **MUST** pass `InputNumber`'s initial `value` as a finite number within `min`–`max`. NaN/infinity, `min >= max`, or an out-of-range `value` throws `ArgumentOutOfRangeException` at the call site (the box does not appear); clamp with `Math.Clamp(v, min, max)`.
- `SelectBox` picks only one. An empty `items` throws `ArgumentException` at the call site. `items` is copied at call time; `selected` -1 or out of range starts with no selection (confirm is disabled until one is picked).
- Input boxes start editing as soon as they appear (in touch mode the virtual keyboard appears at once); the keyboard's Done (Enter) confirms directly. Pressing the confirm button while the virtual keyboard is open cancels the edit and does not confirm — this is normal; do not build your own dialog because of it.

**After `await`**
- Where `await` continues, which thread, and when `App.Invalidate()` is needed: [dialog lifecycle](dvml-dialogs.md#dialog-lifecycle) and [threads](dvml-app.md#threads).

**Outside pages, duplicates, closing**
- Reaching the app from outside pages (`DvApp.Current`): [threads](dvml-app.md#threads).
- **Preventing duplicates**: give the dialog class its own key property (`public int AlarmId { get; }`) and check `App.Dialogs.OfType<AlarmDialog>().Any(d => d.AlarmId == id)` first. **NEVER** compare by title or text; the base class has no `Id` or `Title` property.
- **Closing**: inside the dialog `Close(…)`; all dialogs `App.CloseDialogs()` (all results cancel). `Close` is `protected`, so to close a specific dialog from outside give it a public wrapper (`public void Dismiss() => Close(false);`), find it in `App.Dialogs` by key and call it.

<a id="process-pointer"></a>
## Process diagrams

For a small diagram (one line of a few pieces of equipment, flow in one direction), [the small diagram fast path](dvml-process.md#process-fast-path) is enough. For any other process screen (`DvProcess`, P&ID), **MUST** read [the process diagram guide](dvml-process.md#process-overview) to the end first: procedure, equipment sizes and ports, layout and routing, hints, user symbols (`DvSymbol`), diagnostics and fixes, full examples, checklist. A process diagram states **only equipment + connections**; the library does the layout. Choose the placement mode with [placement mode](dvml-process.md#placement-mode) and size the diagram with [size and fit](dvml-process-layout.md#size-fit). Check the result against the [process checklist](dvml-process.md#process-checklist).

<a id="updates-threads"></a>
## Updates and threads

Bindings are read at layout time. Clicks and other input rebuild the screen by themselves; after a timer or communication changes the VM, request a redraw.

| Situation | Call |
|---|---|
| Click, toggle, knob, input/selection commit, ACK | Automatic (no call needed), also after a handler's `await` |
| VM property changed from a timer or communication, a list newly assigned, a `visible` condition changed | `Invalidate()` — rebuilds the markup |
| Only `DvDataGrid` cell values changed in place, rows added/removed in the same list | `Repaint()` — redraws without rebuilding |
| `TrendBuffer.Add(...)` | Then `Invalidate()` |
| `AlarmLog.Raise(...)`, `Ack(...)`, `AckAll()` | Then `Invalidate()` |

**Which object to call it on.** `DvPage`, `DvBar` and `DvDialog` have no `Invalidate()`/`Repaint()` of their own, and `DvApp` has no `Repaint()`:

| Where the code runs | Rebuild | Redraw only |
|---|---|---|
| Inside a page, bar or dialog class (VM = the class) | `App.Invalidate()` | `App.Surface.Repaint()` |
| Outside (Program.cs, a service holding the app) | `app.Invalidate()` (the `DvApp` instance) | `app.Surface.Repaint()` |
| A host that uses `DvSurface` directly (no `DvApp`) | `surface.Invalidate()` | `surface.Repaint()` |
| Host code holding the OpenTK window | `ViewWindow.Invalidate()` | `ViewWindow.Repaint()` |
| Host code holding the WinForms control | `ViewControl.MarkUpdated()` | `ViewControl.MarkRepaint()` |

- `App.Invalidate()` rebuilds the current page, the bars (fixed areas) and open dialogs. `App.Surface.Repaint()` redraws without rebuilding and is forwarded to all of them.
- `ViewWindow.Invalidate()`/`Repaint()` and `ViewControl.MarkUpdated()`/`MarkRepaint()` do the same for the host's app, but code inside pages should not depend on the host.
- Computed properties (`public string StatusText => …`) are recomputed on the next `Invalidate()`.
- Which calls are safe from which thread, consistent multi-property updates, and the UI thread: [threads](dvml-app.md#threads).

<a id="verify"></a>
## Verify

Most mistakes are silently ignored, so verify every change in this order: [build](#verify-build) → [render a PNG and look at it](#verify-png) → [drive the app and open the real window](#verify-app). Keep a Trace listener on during development: many problems show only as `System.Diagnostics.Trace` warnings. To see markup changes in a running app, use [hot reload](dvml-app.md#hot-reload).

<a id="verify-build"></a>
### Build

- **MUST** build after editing markup and check there are no DVML012 (unknown style property), DVML013 (unknown token) or DVML014 (unknown class) warnings — at runtime these are only silently ignored or turn magenta.
- **MUST** after editing a process diagram build and check there are no DVML008–011 (unknown name, bad port, duplicate name, invalid value), then render a PNG and check Trace warnings ([process diagnostics](dvml-process-layout.md#process-diagnostics)).
- Every code with its meaning and fix: [build diagnostics](dvml-diagnostics.md#build-diagnostics).

<a id="verify-png"></a>
### Render a PNG and look at it

`DvRenderer` renders a screen to a PNG file without opening a window. Rendering and opening a PNG after every markup change is the most reliable verification; an AI must look at the image itself.

```csharp
using Devinno.UI;
using Devinno.UI.Styling;

DvRenderer.RenderToFile(markup, vm, 1280, 800, "screen.png", DvTheme.Dark, registry: null);
```

- It uses the same layout and drawing code as the real window; bindings are read from the VM as-is.
- Overloads take a `ComponentRegistry` (user components) and `DvStrings` (localization).
- For blinking (lamps, alarms) the `timestampMs` argument selects the on/off moment.
- Check: text is visible (color), no magenta (token typo), no element disappeared (size 0), what should stretch stretched (`grow`/stretch), text does not overflow its box.
- One image is rarely enough: set the VM to each state (alarm state, empty list, long text, pressed looks) and render one image per state to compare.

<a id="verify-app"></a>
### Drive the app without a window, then open the real window

To test interaction (clicks, input, page switches, dialogs), drive the whole app in code; no window is needed.

- Create the app as usual (`new DvApp`, `Pages.Add`, bars).
- A **frame** is one call of `app.Surface.RenderTo(canvas, w, h, timestampMs)` (or `app.Surface.Layout(w, h)`). App requests — `Show`, bar assignments, `ShowDialog` (the dialog surface is built), dialog closing and result delivery, and the code after an `await` of a dialog — are applied at the start of a frame. Run one frame first so the screen has a layout to press.
- Send `PointerDown`, `PointerMove` and `PointerUp` to `app.Surface`, in the same units as `w`/`h`, then run a frame.
- `DvRenderer.RenderToFile(app.Surface, w, h, path)` runs one frame itself and draws the state after the change: motion in progress (`transition`, page transitions, `DvExpander` height) is drawn finished and blinking is drawn at time 0, so one call after a press is enough to see its result.
- To see motion part-way, call `RenderTo` repeatedly with an advancing `timestampMs`; transitions, page transitions, expander height and blinking follow that timestamp.
- The virtual keyboard (keypad) slide, `hold` presses and inertial scrolling follow the real clock (`Environment.TickCount64`), not the timestamp: wait real time after the keypad opens or closes, or while holding, then call `RenderTo` (a `hold` press fires inside `RenderTo`, not inside `Layout`).
- After a test writes the VM directly, call `Invalidate()` ahead of the next press. A value widget compares a press with the value from the last rebuild, so otherwise the press may do nothing.

<a id="verify-targets"></a>
**Finding where to press.** Never guess pixel positions. Take them from an element's `Bounds`:

- Give the element an `id`. The generator creates a handle field for it, and its `Bounds` is in window coordinates, the same coordinates `app.Surface.PointerDown` takes, on pages, bars and your own dialogs alike ([handles](dvml-code.md#handles)). Every tag takes `id` (tags without their own handle type get a `DvControl`), so this also finds a grid, an alarm widget, an input or a piece of process equipment.
- The handle field is private: expose what the test needs from the page class. Read `Bounds` after a frame, on the thread that runs the frames.

```csharp
using Devinno.UI.App;
using SkiaSharp;

public sealed partial class MaintPage : DvPage           // MaintPage.dvml: <DvButton id="btnReset" text="Reset" onclick="Reset"/>
{
    internal SKRect ResetBounds => btnReset.Bounds;
    public void Reset() { }
}
```

```csharp
// test code (DvApp app, with the page added: var maint = new MaintPage(); app.Pages.Add(maint);)
app.Surface.Layout(1280, 800);                           // a frame: the page has a layout
var r = maint.ResetBounds;
app.Surface.PointerDown(new SKPoint(r.MidX, r.MidY));
app.Surface.PointerUp(new SKPoint(r.MidX, r.MidY));
app.Surface.Layout(1280, 800);                           // a frame applies the result
```

Parts drawn inside a built-in widget have no handle of their own. Where they are, measured from the widget's `Bounds`:

| Target | Where to press |
|---|---|
| `DvAlarmBanner` ACK | Anywhere in the banner's `Bounds` — the whole bar is the ACK target; there is no button ([DvAlarmBanner](dvml-instruments.md#dvalarmbanner)) |
| `DvAlarmList` ACK button of a row | `AlarmListLayout.Compute(bounds, rows, rowHeight).Rows[i].AckButton` (`Devinno.UI.Alarms`) — the geometry the list itself uses; rows in display order, `null` for an acknowledged row |
| `DvInput show-stepper` − / + | The left 40 px of `Bounds` is −, the right 40 px is + |
| `DvDataGrid` row `i` (0 = first visible row, not scrolled) | Vertical center = `Bounds.Top` + `header-height` × (header tiers, + 1 when a column has a filter row) + `i` × `row-height` + `row-height` / 2. Horizontally, add your column widths (columns with a fixed `width` are the easy case) |
| Buttons of the built-in dialogs (`MessageBox`, `InputBox`, `InputNumber`, `SelectBox`) | **No supported way.** These dialogs take no `id` and no API reports their button positions. `app.CloseDialogs()` closes them with the cancel result (`DvDialogResult.Cancel`, or `null` for the input boxes), which covers the cancel path. For OK/Yes/No, render a PNG while the box is open and take the position from it: the buttons sit in the bottom row, right-aligned, OK/Yes on the far right and No to its left |

Then **open the real window** (and, with `fit`, resize it and press something). The OpenTK window reads the real cursor position, so posted OS mouse messages are not a reliable test there.

<a id="not-supported"></a>
## Not supported

Assume any tag, attribute or value not in these pages is not supported. The following are known gaps; work around them another way.

- Text: ellipsis (except grid cells), line count limits, `line-height`, `word-break`, hyphenation; automatic wrapping of text children of `DvRow` (giving a width works).
- Fonts: bundling font files (`font-family` picks from installed fonts); `font-weight` other than normal and bold.
- Layout and style: `margin`, `%`/`em`/`auto` sizes (except grid tracks and grid column widths), keyframe animation (`@keyframes`, `animation`), `rotate`/`skew`, hit areas following `transform`, compound selectors (`.a.b`, tag, descendant selectors).
- Input: keyboard focus navigation and tab order (except Enter/Tab in grid edit cells), drag and drop.
- Dialogs: closing by pressing outside, drag-moving, open/close animations, a title bar X button, physical keyboard Esc/Enter (the only exception is the edit-completion Enter of built-in input boxes), a multi-select box (`SelectBox` picks only one).
- Pickers: `DvSelect` keyboard navigation, date range selection, year-step navigation, a "clear" button in pickers, color palette/alpha editing, `DateTimeOffset`.
- `DvDataGrid`: reading or setting filter state from app code (filters are operated only by the user in the filter row), cell templates, variable row height, arrow key navigation, multi-cell paste/undo, per-row selection lists, header/summary editing, password cells.
- `DvRepeat`: virtualization, keys, selection (use `DvDataGrid` if needed).
- Process diagrams: [process limits](dvml-process.md#process-not-supported).
- Publishing: NativeAOT and trimmed publishing are not supported (the library finds bindings and handlers via reflection). Publish apps with JIT.

<a id="api-reference"></a>
## Where the C# API is

These pages cover the **markup**. On the C# side they list only the minimum tied to the markup (what the VM must provide, handler signatures).

- Detailed usage is in each type's XML doc comments: hosts (`ViewWindow`, `ViewControl`, `DvSurface`, `DvRenderer`), themes (`DvTheme`), app, pages and dialogs (`DvApp`, `DvPage`, `DvBar`, `DvDialog`, `DialogCollection`, `DvDialogResult`, `DvMessageButtons`), alarms (`AlarmRow`, `AlarmLog`), trends (`TrendBuffer`, `TrendPoint`), localization (`DvStrings`), component and process-symbol registration (`ComponentRegistry`), custom widgets (`DvWidget`), video (`FrameSource`, `VideoPlayer`), constants (`Styles`, `Values`).
- View them via IntelliSense in the IDE or, with only the package, in `Devinno.UI.xml` (`lib/net9.0/` in the package; the same folder as these pages in `.devinno/Devinno.UI/`).

<a id="namespaces"></a>
### C# namespaces

The `using` lines for the C# types these pages use. Handle fields created by the generator need no `using` in the VM.

| Types | Namespace | Package |
|---|---|---|
| `DvApp`, `DvPage`, `DvBar`, `DvDialog`, `DvDialog<TResult>`, `DialogCollection`, `DvDialogResult`, `DvMessageButtons`, `DvPageTransition` | `Devinno.UI.App` | Devinno.UI |
| `DvRenderer`, `DvSurface`, `DvHandlerErrorEventArgs`, `Styles`, `Values` | `Devinno.UI` | Devinno.UI |
| `DvTheme` | `Devinno.UI.Styling` | Devinno.UI |
| `DvControl` and the value handles (`DvLabel`, `DvButton`, `DvInput`, `DvToggle`, `DvSelect` …), `DvClickEventArgs`, `DvValueChangedEventArgs` | `Devinno.UI.Controls` | Devinno.UI |
| `AlarmLog`, `AlarmRow`, `AlarmPriority` | `Devinno.UI.Alarms` | Devinno.UI |
| `TrendBuffer`, `TrendPoint` | `Devinno.UI.Trends` | Devinno.UI |
| `DvStrings` | `Devinno.UI.Localization` | Devinno.UI |
| `ComponentRegistry` | `Devinno.UI.Components` | Devinno.UI |
| `DvWidget`, `DvWidgetContext`, `ImageCache` | `Devinno.UI.Widgets` | Devinno.UI |
| `FrameSource`, `VideoPlayer`, `IFrameSource` | `Devinno.UI.Video` | Devinno.UI |
| `ViewWindow` | `Devinno.UI.OpenTK` | Devinno.UI.OpenTK |
| `ViewControl` | `Devinno.UI.WinForms` | Devinno.UI.WinForms |
| `SKCanvas`, `SKRect`, `SKPoint`, `SKPaint`, `SKColor` (`Bounds`, pointer input, custom widget drawing) | `SkiaSharp` | SkiaSharp (comes with Devinno.UI) |
