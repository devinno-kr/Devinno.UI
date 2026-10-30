# Devinno.UI Authoring Rules

This is the **document to read first** when building screens with Devinno.UI. It is for both humans and AI.

- This document: principles, pitfalls, how to verify.
- Full list of tags, attributes and style keys: [`dvml-ui-markup.md`](dvml-ui-markup.md)
- Process diagram (P&ID, `DvProcess`) authoring guide: [`dvml-process.md`](dvml-process.md) — always read it first when building a process screen
- C# API (ViewWindow, DvApp, DvPage, DvBar, DvDialog, DvSurface, AlarmLog, TrendBuffer, DvStrings, etc.): the XML doc comments on each type. View them via IntelliSense in the IDE, or in `Devinno.UI.xml`, which ships in the NuGet package.

Devinno.UI is a .NET 9 UI library that draws directly with SkiaSharp. Screens are written as XML markup with a CSS-like inline `style` and bound to a C# view model (VM). It is not a browser, so **even where it looks like HTML/CSS, it is not the same engine.** Knowing the differences below avoids most mistakes.

---

## 1. Core principles

1. **Assume any feature not in the docs does not exist.** Tags, attributes, style keys and values not listed in `dvml-ui-markup.md` are not supported. Features that "should obviously work" in CSS or other UI frameworks (`margin`, `%` widths, ellipsis, `display:none`, animations, etc.) do not work either unless documented. If the docs and the code disagree, the code is right. The one exception is the project's own tags: components (`DvComponent`) and custom widgets (classes registered with `RegisterWidget`, `dvml-ui-markup.md` 15.1) exist when the app registers them — use them with the parameters/attributes their definition or class reads. **NEVER** invent such a tag, and write a new `DvWidget` class only when the user asks for one or no built-in widget/component can show it.
2. **Use semantic components first.** `DvPageFrame`, `DvCard`, `DvStat`, `DvBadge`, `DvStatusItem`, `DvSwitch` and `DvButton variant="…"` get their look from the theme. Build layout with `DvStack`, `DvRow`, `DvGrid` and `DvSpacer`. Use `DvBox` + `style` only for special shapes these cannot produce.
3. **Use theme tokens for colors and spacing.** Writing `token(surface)`, `token(text-muted)`, `token(spacing.lg)`, `token(radius.md)` makes the screen adapt correctly when switching between the Dark and Light themes. Hardcoded colors like `#1e293b` break the theme, so use them only when a color truly must be fixed, such as a brand color.
4. **Always give text a color.** The default text color of `DvLabel` is `#222222` regardless of theme, and it is **not inherited from the parent.** In the dark theme (the default), a bare `<DvLabel text="…"/>` without a class is almost invisible. Add a typography class such as `class="body"`, `class="label muted"`, `class="title"`, or `style="color: token(text)"`. (The only inherited styles are `font-family` and `white-space`.)
5. **Data and logic are the app's (VM's) responsibility.** Widgets draw VM values and write user input to the VM once, nothing more. Things like alarm storage/ACK and the conditions for conditional colors are written in VM code. Filters that reduce the app data itself go in VM code; column filters the user picks on screen use `DvColumn filter`.
6. **When values change, you must request a redraw.** Clicks, toggles, knobs and input commits redraw automatically. When the VM changes anywhere else (timers, incoming communication, etc.), call `App.Invalidate()`, or `App.Surface.Repaint()` if you only changed grid cell values, for the screen to update. (section 6 — which object to call it on)
7. **Verify the result visually.** After writing markup, render a PNG with `DvRenderer` and look at it yourself, because most errors are silently ignored. (section 5)

---

## 2. Layout defaults that differ from CSS

| Item | Devinno.UI | Difference from CSS |
|---|---|---|
| `DvBox` layout | Without `display:flex`, it **only stacks children vertically**. `gap`, `justify-content` and `align-items` apply, but `grow` (stretching) does not work. | Similar to CSS block flow, but without stretching |
| `align-items` default | `stretch` — children without a size fill the parent. Children with `width`/`height` get that size. Widgets with a fixed shape, such as knobs and toggles, are drawn centered at their own size, and only the area inside that shape is pressable | Same as CSS |
| `flex-shrink` default | `0` — elements do not shrink even when overflowing. To let them shrink, use `flex-shrink:1` or `flex:1`. Exception: an `overflow:scroll` box defaults to `1` if unspecified. | CSS is `1` |
| `DvStack` | Vertical flex, `align-items:stretch`, `gap` 12 | Children fill the width |
| `DvRow` | Horizontal flex, `align-items:center`, `gap` 12 | Children are vertically centered |
| Root element | Always fills the window. The root's `width`/`height` are ignored. | |
| Units | All numbers are px. `16`, `16px` and `16dp` are the same. | No `%`, `em`, `rem`, `vw`, `auto` (only exception: `%` in grid tracks and grid column widths) |
| Unsupported length values | `width:50%`, `width:auto`, `height:2em` become **0, not ignored** (the element disappears). Only `font-size` falls back to 14 when it cannot be parsed. | |
| Text | Text longer than its available width wraps at spaces (or at characters if there are none) and grows in height. Force a single line with `white-space:nowrap` (inherited). `\n` always breaks the line. **`DvRow` children do not receive a wrap width, so they stay on one line** — to wrap, give that child `width`/`max-width`. Buttons and chips with icons are single-line. | In CSS, row children also shrink and wrap, and `\n` is treated as whitespace |
| Hiding | Remove the element with `visible="false"`. `display:none` does not hide. | |
| Spacing | `margin` (and `margin-top` etc.) is **not applied.** Use the parent's `gap` and the element's own `padding`. To space one side only, use a single-side key such as `padding-left` (same as CSS). | |
| Opacity and motion | Use `opacity` (0–1), `transform` (translate, scale; drawing only) and `transition` (CSS syntax). Animate state changes with `transition` — do not step values with a timer. | |
| State styles | State styles such as `hover-style` and `:hover` change **drawing only**. Changing size, position or padding leaves the layout unchanged. | |
| Scroll box | The height of an `overflow:scroll` box is its content height, as in CSS, capped by `max-height`. Inside a flex parent (`DvStack`, `DvRow`) with a fixed height, it shrinks when space runs short (down to `min-height`; with `flex-shrink:0` it does not shrink). **To get scrolling you must constrain its size**: `max-height`, `height`, or `grow="1"` inside a parent with a fixed height. If the parent's height is its content size, the scroll box also fully expands. Inside a non-flex `DvBox` or `DvScreen` parent it does not shrink and overflows. A flex box that contains a scroll box (`DvCard` etc.) also shrinks when space runs short — `<DvCard grow="1"><DvStack grow="1" style="overflow:scroll">` scrolls as-is (usage and troubleshooting order: markup 3.7). | Space left after `grow` by `max-*`, and the share not shrunk due to `min-*` when shrinking, are not redistributed to siblings. The containing box shrinking along with it differs from CSS (CSS needs `min-height:0`) |
| Widget size | Gauge and input widgets have a fixed default size (e.g. `DvGauge` 120×120). For other sizes, give numbers or stretch with `grow`/stretch. | |

---

## 3. What is silently ignored, what fails visibly

Knowing how mistakes show up helps you find the cause quickly when looking at the result PNG.

### 3.1 Ignored without any indication (most common)

| Mistake | Result |
|---|---|
| Unknown style key (`margin-top`, `line-height`, `font-style`, `row-gap`, etc.) | Ignored. (`background-color` is supported as a solid-color alias of `background`. Use `background` for gradients.) |
| Unknown or misspelled attribute (`onClick`, `Text`) | Ignored. Tag names and attribute names are **case-sensitive**. |
| `id`, root `vm` | No longer ignored. The source generator reads them and creates VM fields (duplicates and invalid names are compile errors, see 4.1 below). In hosts that use markup only without the generator (`DvRenderer` etc.), they have no effect on the screen. |
| Unknown tag (`DvPanel`, `div`) | Treated as a plain box (`DvBox`). No error. |
| Unparseable color (`red`, `rgb(…)`, `hsl(…)`) | Only that declaration is ignored (previous value kept). |
| Unsupported value (`justify-content:space-around`, `align-items:baseline`, `text-align:justify`) | Treated as the default (start / left). |
| 5 or more `padding` values | Ignored. Only 1–4 values work (3 = top, left/right, bottom — same as CSS). |
| Property path not on the VM (`{Temprature}`) | Empty value (null). Text is blank. |
| Handler method not on the VM (`onclick="Strat"`) | Build error DVML018 in a dvml with `vm`. Where it is not checked (inside a `DvComponent` definition), nothing happens at runtime. (With a code handle's `Click`, a typo is a compile error.) |
| `hover-style`, `content-align`, etc. on a component instance | Ignored. Only parameters and `style`, `class`, `grow`, `width`, `height`, `visible`, `onclick`, `disabled` are passed through. (See section 4) |
| Unknown icon name | The icon is not drawn (one Trace warning). |
| `DvImage src` file missing | Empty box. |

### 3.2 Shows as magenta (`#FF00FF`)

**Unknown theme tokens** inside style strings such as `style`, `class` and `hover-style` turn magenta. The same applies to unknown tokens in color-taking attributes (`DvLamp color`, `DvTrendChannel color`, zone colors in `alarm-zones`). If you see magenta, suspect a misspelled token name (`token(surface-2)`) or an invalid `tone`/`state` value (`DvBadge tone="ok"` → `token(tone-ok-fg)`).

Exception: for `DvDataGrid`'s `*-color` attributes and `DvColumn on-color`, an unknown token uses the theme default color instead of magenta and emits a Trace warning.

### 3.3 Throws an exception (the screen does not appear)

| Cause | Exception |
|---|---|
| XML syntax error (unclosed tag, `&` not written as `&amp;`, missing attribute quotes) | Parse exception |
| Invalid `alarm-zones` (format, `from >= to`, `from` descending, not a number/color) | `InvalidOperationException` at layout (same for bound values; `token()` colors work) |
| Component expanding itself recursively (depth over 64) | `InvalidOperationException` |
| Missing `name` or template root when registering a `DvComponent` | `ArgumentException` |

Conversely, **exceptions thrown by VM code do not stop the screen.** Exceptions thrown by handlers (`onclick`, `onrowclick`, `onack`, `onbeforecommit`) and by setters of properties that widgets write to are caught by the library, which logs a Trace error and reports them via the `HandlerError` event (`ViewWindow`, `ViewControl`, `DvSurface`). If a click seems to "do nothing", check this event or Trace. The same applies to exceptions raised **while reading VM values** during screen build and drawing (property getters, `ToString()`/formatting of values, enumerating `items` of `DvRepeat`, grids etc., grid sort comparisons). That value is treated as empty (null) and drawing continues (grid cell blank, sort ranks it as null, summaries exclude it, a list whose enumeration failed becomes an empty list), and the same target is reported only once until rebuilt with `Invalidate()`.

### 3.4 Other diagnostics

Some problems are reported only as `System.Diagnostics.Trace` warnings (unknown icons, unparsed `content-align`, unsupported selectors, DvDataGrid column path/type mismatches, etc.). Keep a Trace listener on during development.

If the `onbeforecommit` method is missing from the VM (a misspelled name is build error DVML018 in a dvml with `vm`) or has a different signature (including a return type other than `bool`), **the value is not written** (rejected). A Trace warning is logged once per VM type and method name. If the method throws, the value is also rejected and a Trace error is logged (also reported via `HandlerError`). If, after passing the check, **the VM property's setter throws**, it is handled the same as a rejection (`DvInput` keeps editing + shows the message "Could not save the value", picker popups stay open, toggles, knobs and check cells keep their value). This is the same for all widgets (DvToggle, DvKnob, DvInput, DvSelect, pickers) and DvDataGrid cells. A misspelled name makes the value look like it "does not change", so check Trace. Signature: `dvml-ui-markup.md` section 6.

---

## 4. Common mistakes

- **Text is invisible** → section 1, item 4. Give the label a typography class or `color`.
- **`grow="1"` has no effect** → the parent is a non-flex `DvBox`. Change the parent to `DvStack`/`DvRow` or give it `style="display:flex"`. The parent must also have spare space in that direction (height, for a vertical stack) for the child to grow.
- **A widget stretches to the parent's width** → the `align-items` default is `stretch`, as in CSS. To keep the default size, give the parent `align-items:start` (or the child `align-self:start`) or a `width`. `DvRow` uses `align-items:center`, so it does not stretch vertically.
- **`width:100%`** → becomes 0 and disappears. Use `DvStack` (stretch) or `grow="1"`.
- **Spacing with `margin`** → not applied. Use the parent's `gap` or `padding`, or an empty box such as `<DvBox style="height:12"/>`.
- **To make a whole card pressable**, give the instance `onclick`, as in `<DvCard title="펌프" onclick="OpenPump"/>`. It is passed to the template root (the card box), and the method is looked up on the screen VM (inside `DvRepeat`, `OpenPump(항목)` takes priority). Even if the template root already has `onclick`, the instance's wins. `disabled` is passed to the root box the same way, and since disabled state is inherited downward, every widget inside the template is disabled.
- **Changed a value from a timer but the screen did not update** → `App.Invalidate()` (`App.Surface.Repaint()` for grid cell values). Pages have no `Invalidate()` of their own (section 6).
- **`vm="MainPage"` works in the samples but not in my project** → `vm` is the **full class name including the namespace** (`vm="MyApp.Pages.MainPage"`). The doc samples declare their classes without a namespace, which is the only case where the bare class name is correct. The build reports a wrong name (DVML005).
- **Page switching seems not to work** → `Show` applies on the next frame. Right after `App.Show<T>()`, `App.CurrentPage` is still the previous page. If `Show<T>()` throws, either no page of that type was added with `Pages.Add` (none), or there is more than one of that type (use the instance: `Show(page)`).
- **The current-page highlight on a bar button does not change** → check that you bound `class="{X}"` to a computed property that reads `App.CurrentPage`. `:selected` and `selected-style` have been removed.
- **Reading `App` in a page constructor throws `InvalidOperationException`** → there is no app before `Pages.Add` (for bars, before the area assignment). Use `App` in handlers, `OnLoaded` and `OnShown`.
- **Markup root tag `<DvPage>` does not work** → it was renamed to `<DvPageFrame>`. The C# `DvPage` is a different thing: the screen class the app holds.
- **Added rows to the grid list but sorting did not update** → sorting is re-applied only on a header tap, a different `items` list, or a change in row count (when no filter condition is active). Assign a new list and call `Invalidate()`.
- **A button click inside a box with `hover-style` does not reach the outer `onclick`** → a click stops at the **first** ancestor that has `onclick` or a hover/press style. It also stops at disabled elements (including inherited from above), so pressing a disabled area does not call the outer `onclick`.
- **A disabled button shows no hover/press look** → this is normal. When disabled (including inherited from above), hover/press styles are not used; only `disabled-style` (or the default look if absent) applies.
- **Attribute `width="100"` has no effect** → if a class on the same element has `width`, the class wins. The attributes `width`, `height`, `grow` and layout sugar attributes have the lowest priority. To be sure, write it in the inline `style`.
- **A class's `:hover` does not show** → if the same property is written in the inline `style`, the inline value beats even the class state styles. Give per-state values with the `hover-style` attribute.
- **A grid filter on a string column opens as an input box instead of a list** → `filter="auto"` on a string column is `text`. To pick from a fixed set of states, use `filter="values"`.
- **Long text inside `DvRow` pushes its neighbors** → `DvRow` children neither wrap nor shrink. Give that text `width`/`max-width` and it wraps at that width. Or move the text into a `DvStack`.
- **Splitting long text into one label per line** → don't. **Long text is one label.** The engine wraps it to the available width (window size, theme font size) and re-wraps it. To keep it on one line, use `white-space:nowrap`.
- **A scroll list does not scroll and fully expands** → you did not constrain the scroll box's size, or you gave `grow="1"` but a non-flex box (`DvBox`, `DvScreen`) sits somewhere in the parent chain. Give a `max-height`, as in `style="max-height:320; overflow:scroll"`, or connect from the page (`DvPageFrame`) down to the scroll box with `DvStack`/`DvCard` and give `grow="1"`. Troubleshooting order: markup 3.7.5.
- **Built-in widget text appears in English on a Korean screen** (alarm columns, input errors, dialog and picker buttons) → the library's built-in text is English by default. For Korean set `app.Strings = new DvStrings { Language = "ko" }`; for other languages add the `dv.` keys to your table (`dvml-ui-markup.md` chapter 17).
- **XML special characters** → in text, write `&` and `<` as `&amp;` and `&lt;`. In attributes, write `"` as `&quot;`.
- **No theme given** → `DvTheme.Default` (= `DvTheme.Dark`) is used. Style tokens and widget-internal colors (toggles, gauge tracks, tanks, grids, virtual keyboard, etc.) are all drawn in the dark theme. If you need a light screen, specify `DvTheme.Light` explicitly.

### 4.1 Controlling controls from code in the VM (`id`, `vm`)

Full usage is in `dvml-ui-markup.md` section 20. Only the principles to follow are listed here.

- **Bind state first.** Bind "current state" such as value, enabled and visibility to VM properties with `text="{X}"`, `disabled="{Locked}"`, `visible="{Show}"`. A handle (`btn.Enabled = false`) is a tool for one-off actions or when you need a position (placing a popup with `Bounds`, subscribing to `Click`).
- **A value widget's value has a single owner** (markup 20.9). With `value="{Bind X}"`, the VM property is the owner and the handle (`tg.Checked`) is a window that reads and writes that property. With only `id` and no `value`, the handle holds the value and it is still pressable on screen. Change notifications are `CheckedChanged`, `ValueChanged`, `TextChanged`, `SelectionChanged` (`e.ByUser` distinguishes screen vs. code). For MVVM use binding; for code-centric use `id` only — mixing both within one screen is fine. The built-in `DvSwitch` has the same handle (`Checked`). A read-only toggle shows a dashed border; a disabled toggle shows semi-transparent.
- **When code writes, code is the owner.** Once you write `Enabled`, `Visible` or `Text` through a handle, that property's markup value (including bindings) is no longer used. Do not drive one property with both a binding and a handle. To revert, call `ResetOverrides()` (for text only, `Text = null`).
- **Handle fields are `null` in the VM constructor.** Do setup such as subscribing to `Click` in `partial void OnLoaded()`, which is called after the first connection to the screen. `OnLoaded()` runs once per VM instance.
- **Writes work from any thread, but reads reflect the last layout.** `Enabled = false` is readable immediately, but a write of `true` becomes readable after the next layout. Read `Bounds` on the UI thread.
- **`id` inside `DvRepeat` or `DvComponent` templates has no handle** (warning DVML004). Handle such places with bindings and an `onclick` handler that takes the item as its parameter (`onclick="Select"` → `public void Select(Item item)`; write only the method name — the item is passed automatically).
- `Click` fires only on the normal click path (excluding toggle, input, select, grid and alarm widgets). Subscribing to `Click` alone makes that element take the click so it does not leak to an outer `onclick`.
- `Bounds` is in window coordinates. `left`/`top` of `position:absolute` are relative to the parent's content box, so to use `Bounds` values as-is, place the element under a root without padding.
- If a class deriving from `DvPage` or `DvBar` is the VM, the generator also creates the markup wiring (`dvml-ui-markup.md` section 16). Each page has its own class and its own dvml.
- A VM is connected to only one dvml (`vm="…"`). Diagnostics DVML001–018 appear in the build output (DVML018 = handler attribute that is not a public method of the VM). **MUST** build after editing markup and check there are no DVML012 (unknown style property), DVML013 (unknown token) or DVML014 (unknown class) warnings — at runtime these are only silently ignored or turn magenta, without any error.

### 4.2 Dialogs (`DvDialog`, `ShowDialog`, `MessageBox`, `InputBox`, `InputNumber`, `SelectBox`)

Full usage and examples are in `dvml-ui-markup.md` section 21 (the smallest example is `SetpointDialog` in 16.1). Like pages, dialogs are **created, held and closed by the app (`DvApp`).** Always follow the rules below.

**Showing**
- **MUST** show dialogs only with `await App.ShowDialog<TDialog, TResult>(생성자 인자…)` (constructor arguments). The app creates the instance with the public constructor matching the arguments.
- **NEVER** try to show an instance created with `new MyDialog(…)`. No such API exists. `ShowDialog` does not return the instance either; if you need the instance from outside, look it up in `App.Dialogs`.
- Constructor arguments must be **directly assignable** by type. There is no numeric conversion (`int` → `double`) and no filling of optional parameters. If no constructor matches, or more than one does, `ShowDialog` throws `ArgumentException` at the call site.
- Passing a single array (`string[] names`) makes that array one argument. **Wrap with `(object)items` only when passing a single `object[]`** (otherwise its elements expand into the argument list).
- **NEVER** touch `App` or the UI in a dialog constructor. The constructor runs on the thread that called `ShowDialog` (possibly a communication thread), and at that time there is neither a screen nor an `App` (reading `App` throws `InvalidOperationException`). Only store the received values; do setup in `partial void OnLoaded()`.

**Definition**
- Dialog = a `partial class` deriving from `DvDialog<TResult>` + a dvml whose root is `<DvDialogFrame vm="네임스페이스.클래스" title="…">` (namespace.class). **Write `vm` including the namespace** (otherwise DVML005).
- Use `DvDialogFrame` as the root. If you use another tag as the root and give it no background, it is transparent and the page below shows through.
- Size comes from `width`/`height` in the root `style` (otherwise the content size, with text wrapping within window width − 32). It never exceeds the window − 16 (per side) and is clipped when overflowing, so put long content in an `overflow:scroll` box (e.g. `max-height:320`) — when the window is short, that box shrinks and the buttons stay inside the frame.

**Result**
- **MUST** use a result type distinguishable from cancel (`default`): reference types (`Recipe`, `string`), `bool?`, `int?`, `double?`, `DvDialogResult`. **NEVER** value types such as `bool`, `int`, `double` — when closed from outside you get `false`/`0`, indistinguishable from "No" or "0".
- Inside the dialog, `Close(result)` sets the result; `Close()` is cancel (`default`). Whichever way it closes (button, `CloseDialogs`, the dialog below closing, window closing), the result arrives exactly once. However, since the result is delivered on a frame, for an **app never attached to a window** `await` waits until the first frame, and for a **detached app** (WinForms `ViewControl.App` replaced with another app) it waits until reattached. `ShowDialog` called on an already **closed app** completes immediately with a cancel result on the calling thread.
- An edit dialog edits a **copy** and returns the new value as the result; applying it to the original is done by the caller (data is the app's responsibility).
- A message box is `await App.MessageBox(글, 제목, DvMessageButtons.YesNo)` (text, title); when closed from outside it returns `DvDialogResult.Cancel` regardless of the button set. Check "Yes" with `== DvDialogResult.Yes` (do not use `!= No`).
- For on/off: a switch look uses `DvToggle`/`DvSwitch`, a box next to text uses `DvCheckBox`, a button look uses the latching button `<DvButton value="{Bind X}"/>`. For choosing one of several: 3–5 joined buttons use `DvSegment` (+`DvOption`); items with long descriptions laid out vertically use `DvRadio` (the same `value="{Bind X}"` + each its own `option` — the group is defined by the value, not by placement); many items use `DvSelect` (collapsed) or `DvListBox` (expanded list; multi-select with `selection-mode="multi"` + `selected-items`). Choosing from a hierarchy (plant → line → equipment) uses `DvTreeView items="{Roots}" item-children="Kids" item-text="Name" value="{Bind Picked}"` (the app objects as-is; do not create tree-only node types). **NEVER** imitate selection state yourself with several buttons' `onclick` and class bindings.
- Numeric input for stepping setpoints up and down is `<DvInput keyboard-type="numeric" step="…" show-stepper="true"/>`. Values dragged by hand within a range (opening, brightness) use `<DvSlider value="{Bind X}" step="…"/>` (orientation via `direction`, same as `DvProgress`); for a round handle, `DvKnob`. **NEVER** build it yourself from two −/+ buttons and handlers (you will miss range, validation and formatting rules).
- To show equipment state numbers as lamps, bind VM computed properties (`StateKey` → `state-*` token key, `StateText`, `IsFault`) to the `state`/`status`/`blink` of `DvStatusItem`/`DvLamp` (`dvml-ui-markup.md` section 8, DvLamp). There is no state table widget. The current step of a process/batch sequence is `<DvStepBar steps="{Steps}" current="{Step}"/>` (`current` starts at 1, 0 = not started). Small trends use `DvSparkline values="{List}"`, per-item comparison uses `DvBarChart`, proportions use `DvPieChart` (`items` + `item-value` + `item-text`, a single series — time-axis data uses `DvTrend`).
- Switching content groups within one page is `DvTabs`/`DvTab` (same VM). Expand/collapse sections use `DvExpander` (`expanded="{Bind X}"`; if omitted, the screen remembers it); a group where only one is open uses `DvRepeat` + `DvExpander` + an item setter (`dvml-ui-markup.md` section 8). Changing the whole screen is a page switch (`App.Show<T>()`). **NEVER** put a page inside a tab (no pages inside pages).
- Prevent mis-operation of dangerous command buttons (start, stop, reset) with `hold="1500"` (must be held for that many ms before `onclick`) or a confirmation box (`App.MessageBox`). **NEVER** build long-press yourself with a timer.
- Confirmation before alarm ACK (mis-operation prevention) is done in the `onack` handler, not via a widget attribute: `public async void Ack(AlarmRow r) { if (await App.MessageBox(…, DvMessageButtons.YesNo) == DvDialogResult.Yes) Log.Ack(r.Id); }`. **NEVER** use an attribute like `ack-confirm` (it does not exist — it is silently ignored). Example in `dvml-ui-markup.md` section 13.
- Screen lock (operator away) is done with `App.Lock<LockDialog>(…)`. The app creates the lock dialog (`DvDialog<bool>`), and it calls **`Close(true)` only when authentication succeeds** — that is the unlock (`Close(false)` and `CloseDialogs` do not unlock). If authentication finished outside the dialog, call `App.Unlock()`. **NEVER** show a lock screen with `ShowDialog` — dialogs shown after it stack on top and bypass the lock. For automatic lock on no input, call `Lock` from `App.Idle` with `App.IdleTimeout` (`dvml-ui-markup.md` 21.8).

**Built-in boxes — one value, one choice** (`dvml-ui-markup.md` 21.5)
- **MUST** use the built-in boxes when asking for one piece of text, one number, or one item from a list: `await App.InputBox(제목, 처음값)` (title, initial value) → `string?`, `await App.InputNumber(제목, 처음값, min, max)` → `double?`, `await App.SelectBox(제목, 항목들, 처음선택)` (title, items, initial selection) → `int?` (the chosen index). **NEVER** create a new dialog class and dvml for a single question like this. Build your own only when editing several values together or when validation or preview is needed.
- **MUST** treat a `null` result as cancel (the cancel button, or `CloseDialogs`, the dialog below closing, window closing). Receive it as `if (await App.InputNumber(…) is { } v)`, and **NEVER** erase null with `?? 0`, `.Value` or `!` (cancel becomes 0 or an exception). For `InputBox`, `""` is empty input, not cancel.
- `InputNumber` rejects out-of-range and non-numeric input, so the result is always within `min`–`max` (`null` means no limit on that side).
- **MUST** pass `InputNumber`'s initial `value` as a finite number within `min`–`max`. NaN/infinity, `min > max`, or an out-of-range `value` throws `ArgumentOutOfRangeException` at the call site (the box does not appear), so clamp values that may be out of range with `Math.Clamp(v, min, max)` before passing.
- `SelectBox` picks only one. An empty `items` throws `ArgumentException` at the call site, so check first if the list may be empty. `items` is copied at call time, and if `selected` is -1 or out of range, it starts with no selection (confirm is disabled until then).
- Input boxes start editing as soon as they appear (in touch mode the virtual keyboard appears immediately), and when the value is accepted with the keyboard's Done (Enter), it is confirmed directly. Pressing the confirm button while the virtual keyboard is open cancels the edit, as on other screens, and does not confirm — this is normal behavior, so do not build your own dialog because of it.
- Button text uses the same `dv.dialog.ok` and `dv.dialog.cancel` keys as the message box; the width is fixed at 360.

**After `await`**
- Code in a handler (`onclick` etc.) that `await`s continues on the **input thread** in the frame the result arrives, so it may use the VM directly. Changed values are redrawn automatically in that frame, so `App.Invalidate()` is not needed (only values changed from timer or communication threads need `App.Invalidate()`).
- Code that `await`s on a communication thread also continues on the input thread. Hand off heavy work after that (communication, files) with **`Task.Run`**. Otherwise the screen freezes meanwhile.
- Exceptions from `async void` handlers arrive at `App.HandlerError` (`Kind` = `"async"`). Write async methods started from a communication thread to return `Task` and catch exceptions yourself.

**Outside pages, duplicates, closing**
- Outside pages (communication classes etc.), reach the app via **`DvApp.Current`**. It throws if never attached to a window, so start communication after opening the window. After the window closes, `Current` still returns the last app, and that app's `ShowDialog` returns a cancel result immediately without even calling the constructor (messages arriving during shutdown end quietly).
- **NEVER** attach two `DvApp`s to windows/controls in one process (the second throws `InvalidOperationException`). There is one app.
- **Preventing duplicates**: to avoid showing the same dialog twice, give the dialog class **its own key property** (e.g. `public int AlarmId { get; }`) and check `App.Dialogs.OfType<AlarmDialog>().Any(d => d.AlarmId == id)` before showing. **NEVER** compare by title or text. The base class has no `Id` or `Title` property.
- Showing several of the same dialog with different arguments is allowed. A double tap on a button is blocked by the library (input below is blocked from the moment of the request).
- **Closing**: inside the dialog, `Close(…)`; to close all, `App.CloseDialogs()` (all results are cancel). `Close` is `protected`, so to close a specific dialog from outside, give the dialog a public method wrapping `Close` (`public void Dismiss() => Close(false);`), find it in `App.Dialogs` by key, and call it.
- Pressing outside a dialog does not close it (the press is swallowed). Always put a close button inside the dialog. Dialogs remain when the page changes — if they must close, app code closes them.
- When a dialog appears, a value being edited on the page is committed; when a dialog closes, a value being edited inside the dialog is discarded.

### 4.3 Process diagrams (`DvProcess`, `DvSymbol`)

**MUST** read [`dvml-process.md`](dvml-process.md) to the end before writing a process screen (procedure, ports, values, diagnostics, checklist). Below are only the most frequent mistakes. A process diagram states **only equipment + connections**; the library does the layout.

- **NEVER** draw a process screen yourself with absolute coordinates (`position:absolute`), `DvImage` or line boxes. Place equipment (`DvVessel`, `DvPump`, `DvValve`, `DvValve3`, `DvExchanger`, `DvInline`, `DvTerminal`) inside `<DvProcess>` and connect them with `<DvLine from="장비.포트" to="장비.포트"/>` (equipment.port).
- **MUST** give each piece of equipment a `name` and connect lines by that name (not `id`). Port names are fixed: vessels `top`, `bottom`, `side`; everything else `in`, `out`; terminals have no port, only `name`.
- The process vessel is `DvVessel`. **NEVER** use the level widget `DvTank` inside a process diagram (it is not equipment and is not drawn).
- Branches and merges connect several lines to the same port. There is no junction equipment. There are no bend-point attributes — change the shape only with the `row` (order within the same column) and `rank` (column number) hints. Equipment coordinates (`x`·`y`) exist only with `<DvProcess placement="fixed">`, which is **only** for matching a drawing the user gave you; then every piece of equipment needs `x`·`y` (`dvml-process.md` chapter 20). **NEVER** choose `fixed` on your own for a new screen.
- Bind state: `running="{P1Run}"`, `open="{V1Open}"`, `level="{T1Level}"` (0–100 %). When only values change, the layout is not recomputed.
- When several supply lines feed one tank, connect them all to `to="T.top"` (or `T.side`) — the library merges them into one nozzle. **NEVER** invent merge equipment. The drawing height is set to fit the area by `DvProcess align` (default `auto`), so you usually do not specify it.
- Value displays and buttons beside equipment go inside `<DvAttach side="right">` as plain dvml (text uses `class="label"`, `caption`, `title` for theme colors). **NEVER** overlay them on the process diagram with absolute positioning (piping cannot route around them).
- Equipment not in the list is defined with `<DvSymbol>` and registered with `registry.Register(…)`. Ports are written not as coordinates but as `side` (face) + `at` (0–1), and colors are not put in the path but set via `DvShape class` (`vessel`, `state`, `accent`, `detail`…).
- **MUST** after editing a process diagram, build and check there are no DVML008–011 (name, port, duplicate, missing value), render a PNG (section 5) and check Trace warnings. If "crosses other piping", "no free path" or "Not enough space" appears, fix `row`, `rank` or the area size. Red dashed lines and red borders mean invalid connections.
- Multiple units of the same equipment (3 blenders) are defined once as a component whose template root is `<DvUnit>` and repeated with `<Blender name="BL1" U="{BL1}"/>`. Connections outside a unit go only through a `<DvPort name side>` placed on the unit, as `BL1.out` — **NEVER** connect directly into a unit like `BL1.T.top` (error). For a button inside the template to receive its own unit, use a VM method with one parameter, such as `Reset(BlenderVm unit)`.
- Place instrument bubbles `<DvInstrument name tag number on="장비"/>` (equipment) directly under `DvProcess` and **always** write `on` (a controller goes on the valve it controls). Only when matching a given drawing, use `tap="장비.면@위치"` or `tap="장비.포트"` instead to stand the bubble at that point with a connection line, and `to="T1.left@0.15"` for drawn nozzle positions (`dvml-process.md` 6.1·7.3). Signal lines are `<DvSignal from="계기" to="계기·장비"/>` (instrument → instrument/equipment) — endpoints are names only. **NEVER** draw signals with `DvLine` (piping) or invent instrument position attributes (`side`, `x`, `y`).
- When matching a given drawing, use what the library already has before giving up on a detail: `<DvSignal arrow="end" label="SH"/>` for signal arrows and switch actions, `<DvLine label="01-100-PE-N"/>` for line numbers, `DvPump kind="isa"` + `DvMotor kind="isa"` for ISA pump and motor symbols, `DvVessel height` for a tall tank, and the extra kinds in `dvml-process.md` 5.2–5.5 (globe·angle·relief valves, reducer·orifice·vent·blind, drum·column·open vessels). **NEVER** invent kinds that are not listed there — register a `DvSymbol` instead.
- Turn on flow indication with `<DvProcess flow-animation="true">` (off by default). The library decides which pipes are flowing, so **NEVER** compute flow per line in the VM and feed it in. Override with `<DvLine flow="{X}"/>` only for lines with real data, such as flowmeters.
- Pipe color is given only by medium name (`water`, `steam`, `air`, `gas`, `oil`, `acid`) as in `<DvLine medium="water">`, written once on the first line of a segment (it propagates in the flow direction). **NEVER** give pipe color with color values or `style`.
- Not yet available: units inside units. **NEVER** invent such attributes.

---

## 5. Verifying screens: render to PNG and look

To see changes directly in the running app, enable `app.HotReload = true` inside `#if DEBUG`; that screen is rebuilt every time you save the dvml (markup only — rebuild when C# changes, `dvml-ui-markup.md` 16.7).

You can render a screen to a PNG file with `DvRenderer` without opening a window. Rendering and opening a PNG after every markup change is the most reliable verification. AI must also look at this image directly to verify.

```csharp
using Devinno.UI;
using Devinno.UI.Styling;

DvRenderer.RenderToFile(markup, vm, 1280, 800, "screen.png", DvTheme.Dark, registry: null);
```

- It uses the same layout and drawing code as the real window. Bindings are read from the VM as-is.
- There are overloads that take a `ComponentRegistry` if you use user components, and `DvStrings` if you use localization.
- For blinking (lamps, alarms), the `timestampMs` argument selects the on/off moment.
- Check: is text visible (color), is there no magenta (token typo), has no element disappeared (size 0), did what should stretch stretch (`grow`/stretch), does text not overflow its box.
- If one image is not enough, change VM values (alarm state, empty list, long text, etc.) and render several images to compare.

To verify interaction too (clicks, input), send `PointerDown`/`PointerUp` to the `DvSurface` and then draw with `DvRenderer.RenderToFile(surface, w, h, path)`. It draws the state after the change: transitions (`transition`, page transitions, `DvExpander` height) are drawn finished, so one call is enough.

---

## 6. Updates and threads

| Situation | Call |
|---|---|
| Click, toggle, knob, input/selection commit, ACK | Automatic (no call needed) |
| VM property changed from a timer or communication, a list newly assigned, a `visible` condition changed | `Invalidate()` — rebuilds the markup |
| Only `DvDataGrid` cell values changed, rows added/removed in the same list | `Repaint()` — redraws only, without rebuilding |

**Which object to call it on.** `DvPage`, `DvBar` and `DvDialog` have no `Invalidate()`/`Repaint()` of their own:

| Where the code runs | Rebuild | Redraw only |
|---|---|---|
| Inside a page, bar or dialog class (VM = the class) | `App.Invalidate()` | `App.Surface.Repaint()` |
| Outside (Program.cs, a service holding the app) | `app.Invalidate()` (the `DvApp` instance) | `app.Surface.Repaint()` |
| A host that uses `DvSurface` directly (no `DvApp`) | `surface.Invalidate()` | `surface.Repaint()` |

`App.Invalidate()` rebuilds the current page, the bars and open dialogs. `App.Surface.Repaint()` is forwarded to all of them too. `ViewWindow.Invalidate()`/`Repaint()` do the same for the window's app, but code inside pages should not depend on the window.
| `TrendBuffer.Add(...)` | Then `Invalidate()` |
| `AlarmLog.Raise(...)`, `Ack(...)`, `AckAll()` | Then `Invalidate()` |

- **Keeping values consistent across threads.** Bindings are read on the UI thread during layout, possibly while a timer thread is writing. When several properties belong together (value + state text + color), update them as one unit: either write them under a `lock` that the getters also take, or put them in an immutable snapshot object and assign the whole object (`Data = new PlantData(...)`), then call `Invalidate()`. A single `double`/`bool`/`string` property needs nothing extra.
- `Invalidate()`, `Repaint()`, `TrendBuffer.Add`, `AlarmLog.Raise`/`Ack`/`AckAll` are safe to call from any thread (Raise/Ack then `Invalidate()` from a communication thread). All other UI work is done on the single UI thread only.
- `DvApp`'s `Show`, `Top`/`Bottom`/`Left`/`Right` assignments, and `Theme`/`Strings`/`Registry` settings can also be called from any thread (queued and applied on the UI thread on the next frame). Calling `App.Show<AlarmPage>()` from a communication thread is fine.
- Dialog `ShowDialog`, `MessageBox`, `CloseDialogs`, reading `Dialogs`, and `Close` inside a dialog also work from any thread. Screen creation/cleanup and result delivery happen on the UI thread on the next frame (4.2).
- In pages and areas inside `DvApp`, writing handle values (`btn.Enabled = false` etc.) also wakes the window. Only in hosts that use `DvSurface` directly must you call `Invalidate()` separately after writing values.
- `DvApp.Invalidate()` rebuilds the current page, fixed areas and open dialogs. When the page changes (even via `App.Show` from code), areas re-read `App.CurrentPage` on the next frame, so do the current-page highlight with a class binding that reads it.
- Do not change the list from another thread while the grid is drawing.
- VM handler and setter exceptions are caught within input processing (UI thread), and binding read exceptions (getter, `ToString`, enumeration) within layout/drawing, and arrive via `HandlerError` (3.3). All screen exceptions from `DvApp`'s areas and pages arrive at `DvApp.HandlerError`, so subscribing to that one is enough. Exceptions thrown inside subscribers are ignored (Trace).
- Bindings are read once at layout. Computed properties (`public string StatusText => …`) are also recomputed on the next `Invalidate()`.

---

## 7. Known limitations

These are not in the current version. If you need them, work around them another way or wait.

- Ellipsis (except grid cells), line count limits, `line-height`, `word-break`, hyphenation. Automatic wrapping of text children of `DvRow` (giving a width works).
- `margin`, `%`/`em`/`auto` sizes (except grid tracks and grid column widths), keyframe animation (`@keyframes`, `animation`), `rotate`/`skew`, hit areas following `transform`.
- Keyboard focus navigation and tab order (except Enter/Tab in grid edit cells), drag and drop.
- Dialogs: closing by pressing outside, drag-moving, open/close animations, title bar X button, physical keyboard Esc/Enter handling (the only exception is the edit-completion Enter of built-in input boxes), multi-select box (`SelectBox` picks only one).
- `DvSelect` keyboard navigation, date range selection, year-step navigation, a "clear" button in pickers, color palette/alpha editing, `DateTimeOffset`.
- `DvDataGrid`: reading or setting filter state from app code (filters are operated only by the user in the filter row), cell templates, variable row height, arrow key navigation, multi-cell paste/undo, per-row selection lists, header/summary editing, password cells.
- `DvRepeat`: virtualization, keys, selection (use `DvDataGrid` if needed).
- Process diagrams: units inside units, `exit` hint, specifying bend points directly.
- NativeAOT and trimmed publishing — **no plans to support** (the library finds bindings and handlers via reflection). Publish apps with JIT.
- Compound selectors (`.a.b`, tag, descendant selectors).

---

## 8. Where the C# API is

These two md documents cover **markup**. On the C# side they list only the minimum tied to the markup (what the VM must provide, handler signatures).

- Detailed usage of hosts (`ViewWindow`, `ViewControl`, `DvSurface`, `DvRenderer`), themes (`DvTheme`), app, pages and dialogs (`DvApp`, `DvPage`, `DvBar`, `DvDialog`, `DialogCollection`, `DvDialogResult`, `DvMessageButtons`), alarms (`AlarmRow`, `AlarmLog`), trends (`TrendBuffer`, `TrendPoint`), localization (`DvStrings`), component and process-diagram symbol registration (`ComponentRegistry`), and constants (`Styles`, `Values`) is in each type's XML doc comments.
- View them via IntelliSense in the IDE, or, if you only have the package, in the `Devinno.UI.xml` file.
