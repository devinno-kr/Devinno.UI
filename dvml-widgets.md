# Widgets (semantic components, basic widgets)

Covers: the built-in semantic components (`DvPageFrame` `DvDialogFrame` `DvCard` `DvBadge` `DvStat` `DvStatusItem` `DvSwitch`), the basic widgets (`DvLabel` `DvButton` `DvIcon` `DvImage` `DvVideo` `DvToggle` `DvCheckBox` `DvRadio` `DvKnob` `DvSlider` `DvTabs` `DvExpander` `DvStepBar`) — one section per tag.
Read first: [markup basics](dvml-language.md#markup-basics) · [common attributes every tag takes](dvml-language.md#common-attributes) · [how attribute values are read](dvml-language.md#value-rules) · [value widgets: two-way, read-only, handles](dvml-language.md#value-widgets)
Not here: [input widgets: text, select, segment, date/time, color, list, tree](dvml-inputs.md#input-common) · [gauges, lamps, tanks, charts, trends, alarms](dvml-instruments.md#instrument-common) · [data grid](dvml-grid.md#dvdatagrid) · [layout boxes DvBox/DvStack/DvRow/DvGrid](dvml-layout.md#layout-sugar) · [user components and DvRepeat](dvml-components.md#dvcomponent) · [dialogs](dvml-dialogs.md#dialog-define) · [which widget to pick](llm-rules.md#choose-widget)

How to read the tables: `bound` = the attribute takes a binding (`{X}` / `{Bind X}`). Every tag also takes the [common attributes](dvml-language.md#common-attributes) (`id` `class` `style` `grow` `width` `height` `visible` `disabled` `onclick` `hold` `onbeforecommit` …); they are not repeated in the tables. Bool, number, word and color values follow the [value rules](dvml-language.md#value-rules): an unreadable value gives the default + a Trace warning.

<a id="semantic-components"></a>
## Semantic components

Built-in components whose appearance is defined by the theme. You rarely need `style`. They need no registration; an app component with the same name replaces the built-in one ([component names](dvml-components.md#component-names)).

| Tag | Parameters (default) | Appearance |
|---|---|---|
| [`DvPageFrame`](#dvpageframe) | `title` `subtitle` | Page background gradient + padding 24 + header; children stack vertically (gap 16) |
| [`DvDialogFrame`](#dvdialogframe) | `title` `subtitle` | Dialog frame (`.dialog`); children stack vertically (gap 12) |
| [`DvCard`](#dvcard) | `title` `badge` | Card surface; children stack vertically (gap 12, horizontal stretch) |
| [`DvBadge`](#dvbadge) | `text` `tone`(neutral) | Small chip |
| [`DvStat`](#dvstat) | `label` `value` `unit` `delta` `tone`(info) | KPI card with an accent band on top |
| [`DvStatusItem`](#dvstatusitem) | `label` `state`(off) `status` `blink`(false) | One line of lamp + name + status chip |
| [`DvSwitch`](#dvswitch) | `label` `value` | One line of name + toggle |

- `tone` = `neutral` `info` `success` `warning` `danger`; `state` = `run` `stop` `fault` `warn` `manual` `off` (built-in themes; a theme may add more `tone-*` / `state-*` tokens — [theme tokens](dvml-style.md#theme-tokens)).
- An unknown `tone` on `DvBadge`/`DvStat` draws magenta (the component builds a token name that does not exist — [magenta](dvml-diagnostics.md#magenta)). An unknown `state` on `DvStatusItem` uses `off` for the lamp and the chip, with one Trace warning.
- Presence rule: `title` `subtitle` `badge` `delta` `unit` `status` are shown when their text is non-empty — any text, including `"0"`. A bound number is shown as its text.
- All parameters are bound (`tone="{StatusTone}"`, `value="{Bind Auto}"`).
- What else an instance accepts (`style`, `class`, `grow`, `width`, `height`, `visible`, `onclick`, `disabled`) and how it reaches the template: [component instances](dvml-components.md#component-instances).

<a id="dvpageframe"></a>
### DvPageFrame

The page root: page background, padding and a heading. The usual root of a page dvml is `<DvPageFrame vm="…">`. The markup tag `<DvPageFrame>` is not the C# class `DvPage` (one screen the app retains — [DvPage](dvml-app.md#dvpage)).

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `title` | text | empty | yes | Component parameter: the heading. Empty = no heading block |
| `subtitle` | text | empty | yes | Component parameter: muted line under the title. Shown only with a `title` |

**Behavior**
- Page background gradient (`.page`) + padding 24 + header; children stack vertically (gap 16).
- No header without `title`.

**Example**
```xml
<DvPageFrame title="Line 1" subtitle="Overview">
  <DvLabel class="body" text="Running"/>
</DvPageFrame>
```

See also: [DvPage and the app](dvml-app.md#dvpage) · [DvDialogFrame](#dvdialogframe)

<a id="dvdialogframe"></a>
### DvDialogFrame

The root of a dialog dvml: background, border, shadow and title drawn by the theme.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `title` | text | empty | yes | Title (bold). Absent or empty = no title row at all |
| `subtitle` | text | empty | yes | Dimmed text below the title. Shown only when `title` is present |
| `width` / `height` in `style` | length | content size | — | Dialog size. Absent = the content size |

**Behavior**
- Children stack vertically (gap `md` 12); horizontally they fill the dialog width.
- Appearance = built-in class `.dialog` (background `surface-overlay`, border `border`, radius 12, shadow `shadow.lg`, padding 20). Override it with `style`/`class` ([what an instance accepts](dvml-components.md#component-instances)).
- All attributes are bindable (`title="{Caption}"`).
- Size limit, centering, scrolling long content and showing the dialog: [defining a dialog](dvml-dialogs.md#dialog-define).

**Example**
```xml
<DvDialogFrame title="{Caption}" subtitle="Confirm the change" style="width:360">
  <DvLabel class="body" text="Apply the new set point?"/>
</DvDialogFrame>
```

See also: [dialog flow](dvml-dialogs.md#dialog-flow) · [DvPageFrame](#dvpageframe)

<a id="dvcard"></a>
### DvCard

A card surface with an optional header.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `title` | text | empty | yes | Header text. Header shown only when `title` is non-empty |
| `badge` | text | empty | yes | Chip on the right of the header. Shown only when non-empty and the header is shown |

**Behavior**
- Card surface (`.card`); children stack vertically (gap 12, horizontal stretch).

**Example**
```xml
<DvCard title="Intake pump #1" badge="AUTO">
  <DvLabel class="body" text="Flow 12.5 m3/h"/>
</DvCard>
```

See also: [DvBadge](#dvbadge) · [component instances (onclick on a card)](dvml-components.md#component-instances)

<a id="dvbadge"></a>
### DvBadge

A small tone-colored chip.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `text` | text | empty | yes | Chip text |
| `tone` | theme word | `neutral` | yes | Colors `tone-{tone}-fg` on `tone-{tone}-soft`. Unknown word = magenta |

**Behavior**
- Drawn with the `.chip` class.

**Example**
```xml
<DvBadge text="Warning" tone="warning"/>
```

See also: [theme tokens](dvml-style.md#theme-tokens) · [magenta](dvml-diagnostics.md#magenta)

<a id="dvstat"></a>
### DvStat

A KPI card: label, large value, unit and an optional change chip.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `label` | text | empty | yes | Muted caption |
| `value` | text | empty | yes | Large value text |
| `unit` | text | empty | yes | Shown after the value when non-empty |
| `delta` | text | empty | yes | Tone-colored chip (a `DvBadge`) when non-empty |
| `tone` | theme word | `info` | yes | Accent band and `delta` chip color. Unknown word = magenta |

**Behavior**
- Accent band on top in `tone-{tone}`; label and `delta` on the first row, value + unit below.

**Example**
```xml
<DvStat label="Output" value="{Output}" unit="kW" delta="+3%" tone="success"/>
```

See also: [DvBadge](#dvbadge) · [instruments](dvml-instruments.md#instrument-common)

<a id="dvstatusitem"></a>
### DvStatusItem

One line of lamp + name + status chip — the usual display for an equipment state.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `label` | text | empty | yes | Name |
| `state` | theme word | `off` | yes | Lamp color and chip colors (`state-{state}`, `state-{state}-fg`, `state-{state}-soft`). Unknown word = `off` + one Trace warning |
| `status` | text | empty | yes | Status chip text. Chip shown only when non-empty |
| `blink` | bool | `false` | yes | Lamp blinks |

**Behavior**
- The lamp is always on, colored by `state`.
- A multi-state display (equipment state number → key, text, blinking) is VM code: [multi-state lamp](dvml-instruments.md#dvlamp).

**Example**
```xml
<DvStatusItem label="Pump 2" state="{Pump2.StateKey}" status="{Pump2.StateText}" blink="{Pump2.IsFault}"/>
```

See also: [DvLamp](dvml-instruments.md#dvlamp) · [theme tokens](dvml-style.md#theme-tokens)

<a id="dvswitch"></a>
### DvSwitch

One line of name + toggle (a settings row).

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `label` | text | empty | yes | Name on the left |
| `value` | bool | — | yes | Two-way with `{Bind X}`. Omitted + `id` = the handle owns the value |

**Behavior**
- The press target is the toggle. Value, writing and look are those of [DvToggle](#dvtoggle); `onbeforecommit` on the instance is passed to the toggle.
- The handle is the same value handle as `DvToggle`; its `Bounds` covers the whole row ([handles](dvml-code.md#handles)).

**Example**
```xml
<DvSwitch label="Auto restart" value="{Bind AutoRestart}"/>
```

See also: [DvToggle](#dvtoggle) · [value widgets](dvml-language.md#value-widgets)

## Basic widgets

<a id="dvlabel"></a>
### DvLabel

Text.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `text` | text | empty | yes | Absent = inner text. A code `Text` override wins and is used as is |

**Behavior**
- Default size: text size + padding.
- Wraps when longer than the width it is given; force a single line with `white-space:nowrap` ([text wrapping](dvml-layout.md#text-wrap)).
- The default text color is `#222222` — give a typography class (`class="body"` etc.) or `color`.
- Align the text with `content-align`/`text-align` ([content-align](dvml-style.md#content-align)).

**Example**
```xml
<DvLabel class="body" text="Tank level"/>
```

See also: [style keys](dvml-style.md#style-keys)

<a id="dvbutton"></a>
### DvButton

A button; with `value` it is a latching (on/off) button.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `text` | text | empty | yes | Absent = inner text. A code `Text` override wins |
| `icon` | `fa-name` / `far fa-name` / `fab fa-name` | none | yes | Icon left of the text (gap 8). Unknown name = not drawn (Trace) |
| `variant` | `primary` `secondary` `danger` `ghost` | none | yes | Adds `.btn .btn-{variant}`. Empty = unstyled button |
| `size` | `sm` `lg` | none | yes | Adds `.btn-{size}`. Only when `variant` is set |
| `value` | bool | — | yes | Present = latching button (see Behavior). A handle cannot own it |

**Behavior**
- Default size: text (+ icon) size + padding.
- `variant` gives the themed look including hover, press and disabled looks. Without `variant` it is an unstyled text box (for custom styling).
- `icon="fa-floppy-disk"`: icon size = `font-size`, color = `color`. An icon alone without text is fine. Icon names: [icons](dvml-app.md#icons).
- A bound `variant` gives the same look as the literal. A null or empty bound value = no `variant`. Any other word = no `variant` (a latching button keeps `btn btn-secondary`) + Trace warning — `variant="foo"` does not add `.btn-foo`; style your own class with `class`. Changes apply on `Invalidate()`.
- Press handling, `hold` and `disabled`: [click](dvml-language.md#click) · [hold](dvml-language.md#hold) · [disabled](dvml-language.md#disabled).
- **Latching (`value`)**: each press flips the bool and writes it to the VM. On = pressed look (`.btn-on`, `primary` background). Without `variant` the look is `secondary`.
- One-way `value="{AutoMode}"` only shows the on state and does not write.
- Reject a flip with `onbeforecommit` = `bool M(bool)` ([onbeforecommit](dvml-language.md#onbeforecommit)). `onclick`, if present, runs after the value is written. With `hold`, it flips only on a long press.
- A set of independently toggled buttons = latching buttons side by side in a `DvRow`.

**Example**
```xml
<DvButton text="Save" icon="fa-floppy-disk" variant="primary" onclick="Save"/>
<DvButton text="Auto" value="{Bind AutoMode}"/>
```

See also: [DvToggle](#dvtoggle) · [DvSegment](dvml-inputs.md#dvsegment) · [classes](dvml-style.md#classes)

<a id="dvicon"></a>
### DvIcon

One icon.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `icon` | `fa-name` / `far fa-name` / `fab fa-name` | none | yes | Unknown name = not drawn (Trace) |

**Behavior**
- Default size: `font-size` (24) square + padding.
- Size = `font-size` (default 24), color = `color` (default `token(text)`).
- Icon names: [icons](dvml-app.md#icons).

**Example**
```xml
<DvIcon icon="fa-triangle-exclamation" style="font-size:32; color:token(tone-warning)"/>
```

See also: [DvButton icon](#dvbutton)

<a id="dvimage"></a>
### DvImage

A picture from a file.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `src` | file path | none | yes | Relative to the current working directory. A changed file is never re-read (cache) |
| `fit` | `contain` `cover` `fill` | `contain` | yes | `contain` keep ratio, fit inside; `cover` keep ratio, fill and crop; `fill` stretch. Case ignored |

**Behavior**
- Default size: 100×100.
- A missing or unreadable file (permissions, locked, format error) = an empty box; read errors are Trace warnings.
- A path once read is decoded to pixels and cached; the same path is not re-read even if the file changes. A failed path is retried after about 2 seconds (files created later are shown).
- The cache is shared by the whole app. Above **about 128MB of decoded pixels or 256 images** it evicts the least recently used images first, so changing paths on a 24-hour screen (snapshots, equipment photos) do not accumulate memory.
- Images drawn within about the last 2 seconds are not evicted. Reusing an evicted path re-reads the file.

**Example**
```xml
<DvImage src="images/pump.png" fit="cover" style="width:200; height:120"/>
```

See also: [DvVideo](#dvvideo) · [screens from a picture](dvml-image.md#image-files)

<a id="dvvideo"></a>
### DvVideo

A picture that changes: a video file with its sound, or frames your C# code produces (camera, decoder, generated images). Plays sound-only files too.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `src` | file path | none | yes | Played by a player the element owns. Ignored (Trace) when `source` is present |
| `source` | `IFrameSource` object | none | yes | From the view model. Wins over `src`. Any other type shows nothing (Trace) |
| `fit` | `contain` `cover` `fill` | `contain` | yes | As in `DvImage` |
| `playing` | bool | `true` | yes | `src` only. `false` pauses on the current frame |
| `loop` | bool | `false` | yes | `src` only. `false` stops on the last frame |
| `volume` | number 0–1 | `1` | yes | `src` only. Clamped to 0–1 |
| `muted` | bool | `false` | yes | `src` only. `true` = no sound; `volume` keeps its value |
| `position` | number (seconds) | — | yes | A changed value seeks. `{Bind X}` is written back while playing. `src` or `source` = `VideoPlayer` only |
| `duration` | number (seconds) | — | yes | Only `{Bind X}` is used (written out). One-way or literal is ignored silently |

**Behavior**
- Default size: 320×180.
- `src` = video file path, relative to the current directory like `DvImage src`. The element plays it with FFmpeg. Changing `src` closes the old file and opens the new one; the player is closed when the element disappears.
- `source` = a binding to an `IFrameSource` (namespace `Devinno.UI.Video`): a `FrameSource` your code `Publish`es pictures to from any thread, or a `VideoPlayer` you created for code control (`Seek`, `Position`, `Duration`, `Ended`, `Volume`, `Muted`). `playing`/`loop`/`volume`/`muted` are then not used — control the object. `source` and `src` both given → `source` wins (Trace warning).
- `position="{Bind P}"`: the video writes its position about 4 times a second while playing (and once when it stops or ends); when `P` changes elsewhere (a slider, code + `Invalidate`), the video jumps there. One-way `{P}` only jumps.
- `duration="{Bind D}"` writes the length in seconds once the file is open. `position` and `duration` work with `src`, and with `source` when it is a `VideoPlayer`.
- A newly opened file (first build, or `src` changed) starts from 0 and writes 0 to `P` — it does not jump to the old value.
- Each position write rebuilds the screen so bound widgets follow: a bound video repaints the whole screen about 4 times a second instead of only its box.
- The picture is clipped to the box. Rounded corners: `overflow:hidden` + `border-radius`. Background and border come from `style` (`background:#000000` for letterbox bars).
- **FFmpeg 8 is needed for `src` and `VideoPlayer`** and is not part of the package. Windows: the DLLs of an FFmpeg 8 *shared* build (`avcodec-62.dll`, `avformat-62.dll`, `avutil-60.dll`, `swscale-9.dll`, `swresample-6.dll`) next to the app or in an `ffmpeg` subfolder, or set `VideoPlayer.FFmpegDirectory` at startup. Linux: FFmpeg 8 from the OS (`libavcodec.so.62`).
- FFmpeg missing, a different major version, or an unreadable file → the box shows a one-line reason instead of the picture (one Trace warning). `source` with your own frames needs no FFmpeg.
- **Sound** plays in sync with the picture: the picture follows the sound clock, and a picture that is late is skipped. A change of `volume`/`muted` is heard after about 20 ms. A file with only sound (`.m4a`, `.mp3`, …) plays with nothing drawn; `position`/`duration` work.
- **SDL2 is needed for sound** and is not part of the package. Windows: `SDL2.dll` (SDL 2.x, x64) next to the app or in the `VideoPlayer.FFmpegDirectory` folder. Linux: the OS package `libsdl2-2.0-0` (`libSDL2-2.0.so.0`; the `-dev` package is not needed). The same app code on both. SDL2 missing or no sound device → the video plays silently (one Trace warning); a sound-only file shows the reason in the box.
- Repainting: while playing, only the video box is repainted, and only when a new frame has arrived. A paused or ended video does not repaint.
- When no screen shows the video for about half a second (its page is hidden), playback time and sound stop and continue when shown again.
- A one-off render without a surface (`DvRenderer.RenderToPng(markup, …)`) shows the first frame of `src`.

**Example**
```xml
<DvVideo src="videos/guide.mp4" loop="true" fit="cover" style="height:300"/>
<DvVideo src="{GuidePath}" playing="{IsPlaying}" volume="{Volume}" muted="{IsMuted}"/>
<DvVideo source="{Preview}" fit="contain"/>

<!-- seek bar: the video writes Pos/Len, the slider writes Pos back -->
<DvVideo src="videos/guide.mp4" position="{Bind Pos}" duration="{Bind Len}"/>
<DvSlider value="{Bind Pos}" max="{Len}" step="0.1"/>
```

See also: [DvImage](#dvimage) · [DvSlider](#dvslider) · [threads](dvml-app.md#threads)

<a id="dvtoggle"></a>
### DvToggle

An on/off switch.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | bool | — | yes | `{Bind X}` = VM. Absent + a handle = the handle owns it. One-way/literal = read-only |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal ([read-only rule](dvml-language.md#value-widgets)) |

**Behavior**
- Default size: 44×24.
- `value="{Bind X}"` (bool): pressing inverts and writes it. One-way only displays.
- Reject with `onbeforecommit` = `bool M(bool)`.
- Colors: the `toggle-on` `toggle-off` `toggle-knob` tokens. Disabled = translucent.
- Read-only (one-way or fixed value, `readonly`, or neither `value` nor a handle) = a dashed border (`border-strong`) around the track.

**Example**
```xml
<DvToggle value="{Bind Heater}"/>
```

See also: [DvSwitch](#dvswitch) · [DvCheckBox](#dvcheckbox)

<a id="dvcheckbox"></a>
### DvCheckBox

Box + text.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | bool | — | yes | `{Bind X}` = VM. Absent + a handle = the handle owns it. One-way/literal = read-only |
| `text` | text | empty | yes | Absent = inner text. A code `Text` override is honored by the build; the handle exposes no `Text` |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal |

**Behavior**
- Default size: box 24 + gap 10 + text, height 40 or more.
- Toggles when **the whole element, including the text**, is pressed. Stacked vertically, the full row width is the press target.
- Value, writing and `onbeforecommit` (`bool M(bool)`) are those of `DvToggle`. A one-way or fixed value only displays. With only `id` and no `value`, the handle holds the value ([handles](dvml-code.md#handles)).
- Look `.checkbox` (the box is drawn in the 34 left padding, `min-height:40`). On = `primary` fill + check mark; off = `surface` + `border-strong` border; disabled = translucent + dimmed text.
- No three-state (middle bar).

**Example**
```xml
<DvCheckBox text="Alarm sound" value="{Bind AlarmSound}"/>
```

See also: [DvToggle](#dvtoggle) · [DvRadio](#dvradio)

<a id="dvradio"></a>
### DvRadio

Circle + text. Among buttons tied to the same value, only one is on (the HTML name/value idea).

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `option` | text | empty | yes | This button's value. Converted to the type of the bound value for comparison |
| `value` | any | — | yes | The group value. `{Bind X}` = VM; one-way/literal = that value (read-only). Absent = the screen remembers the group. A handle cannot own it |
| `group` | name | none | yes | Read only when `value` is absent. No name + a handle = this element alone |
| `text` | text | empty | yes | Absent = inner text. A code `Text` override is honored by the build; the handle exposes no `Text` |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal. An absent `value` with group memory is writable |

**Behavior**
- Default size: circle 24 + gap 10 + text, height 40 or more.
- **On = the group value equals this button's `option`.** Pressing converts `option` to the property type (enum, number, text) and writes it (conversion and `onbeforecommit` as in [DvSelect](dvml-inputs.md#dvselect)). Pressing an on button again does not turn it off.
- **The group is set by the value, not by placement**: buttons with the same `value="{Bind Mode}"` form one group and may be spread across rows or cards.
- Without binding, use `group="name"`: omit `value`, give the same `group`, and the screen remembers the chosen `option` (kept across redraws; initially none). Read it with an `id` handle ([handles](dvml-code.md#handles)) or bind `value`. `value`, `group` and `id` all absent = display only.
- A one-way or fixed `value` only displays. Look `.radio` (circle on the left); on = `primary` border + center dot.
- A joined row of buttons = [DvSegment](dvml-inputs.md#dvsegment); a dropdown = [DvSelect](dvml-inputs.md#dvselect).

**Example**
```xml
<DvRadio text="Auto" value="{Bind Mode}" option="Auto"/>
<DvRadio text="Manual" value="{Bind Mode}" option="Manual"/>
<DvRadio text="Stop" value="{Bind Mode}" option="Stop"/>
```

See also: [DvSegment](dvml-inputs.md#dvsegment) · [choose a widget](llm-rules.md#choose-widget)

<a id="dvknob"></a>
### DvKnob

A rotary knob for a number.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | number | `0` | yes | `{Bind X}` = VM. Absent + a handle = the handle owns it. One-way/literal = read-only. Code writes are not snapped or clamped |
| `min` | number | `0` | yes | `min >= max` = 0..100 + warning. Drag and keypad never write a value outside |
| `max` | number | `100` | yes | As `min` |
| `step` | number | `1` | yes | `<= 0` = 1 |
| `unit` | text | empty | yes | Drawn after the value text with one space |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal |
| `input-mode` | `drag` `tap` `both` | `drag` | yes | Unknown = `drag` + warning |

**Behavior**
- Default size: 96×96. Internals scale with size ([size scaling](dvml-instruments.md#instrument-common)).
- Drag to rotate; rotation is relative to the grab point. Written once on release (`step` rounding → `onbeforecommit` = `bool M(double)`).
- `input-mode`: `drag` = drag rotates; `tap` = a tap opens a numeric keypad (no rotation); `both` = drag rotates, a tap without movement opens the keypad.
- The keypad is the built-in number box ([InputNumber](dvml-dialogs.md#builtin-boxes)) with the current value, `min`–`max` and `unit`. A value outside `min`–`max` is rejected in the keypad; a confirmed value goes through `step` rounding → `onbeforecommit` → write. Cancel = unchanged.
- The keypad appears only on `DvApp` screens (pages, bars, dialogs). Hosts using `DvSurface` directly ignore the tap (Trace warning).
- A tap on a `tap`/`both` knob opens the keypad instead of `onclick`.
- Read-only and `disabled` knobs do not rotate or open the keypad.
- A drag started on the knob does not scroll.

**Example**
```xml
<DvKnob value="{Bind SetPoint}" min="0" max="120" unit="C" input-mode="both"/>
```

See also: [DvSlider](#dvslider) · [range and value text](dvml-instruments.md#instrument-common)

<a id="dvslider"></a>
### DvSlider

A track + handle for a number.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | number | `0` | yes | `{Bind X}` = VM. Absent + a handle = the handle owns it. One-way/literal = read-only. Code writes are not snapped or clamped |
| `min` | number | `0` | yes | `min >= max` = 0..100 + warning. Drag never writes a value outside |
| `max` | number | `100` | yes | As `min` |
| `step` | number | `1` | yes | `<= 0` = 1 |
| `unit` | text | empty | yes | After the value text with one space |
| `readonly` | bool | `false` | yes | Read-only also when `value` is one-way or a literal |
| `direction` | `right` `left` `up` `down` | `right` | yes | Side where the value increases; `up`/`down` = vertical. Unknown = `right` + warning |
| `show-value` | bool | `false` | yes | Value text (+ unit) above the handle (right of it when vertical) |
| `format` | .NET format string | none | yes | Value text digits ([format](dvml-instruments.md#instrument-common)). Absent = decimals of `step` |

**Behavior**
- Default size: 200×40 (vertical 40×200); +24 with `show-value` (vertical +56).
- Handle diameter 24. **Pressing anywhere on the track moves the value there**; it follows the drag and is written **once on release** (`step` rounding → `min`–`max` → `onbeforecommit` = `bool M(double)`). A press and release without drag writes that spot.
- Colors: track `gauge-track`, fill and handle border `knob-fill`. No ticks.
- A drag started on the slider does not scroll. One-way, `readonly` or `disabled` sliders do not move.

**Example**
```xml
<DvSlider value="{Bind Valve}" unit="%" show-value="true"/>
```

See also: [DvKnob](#dvknob) · [DvProgress](dvml-instruments.md#dvprogress)

<a id="dvtabs"></a>
### DvTabs

Switch between content groups with tabs inside one page (same VM — not page switching with `App.Show`). Children are `DvTab` elements.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | any | — | yes | Two-way `{Bind X}` selects by `DvTab` `value`. One-way/literal = read-only. Absent = the screen remembers the selected tab |
| `readonly` | bool | `false` | yes | Header presses do not switch. No read-only look on tabs |

<a id="dvtab"></a>
**`DvTab` attributes** (a box: content children + common attributes)

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | text | empty | yes | Resolved as text, then converted to the type of the `DvTabs` value |
| `text` | text | empty | yes | Header text. Falls back to `value`. A code `Text` override on the `DvTab` wins |
| `disabled` | bool | `false` | yes | Disables the header; also the body when that tab is selected. A code `Enabled` override wins |

**Behavior**
- Default size: header + content of the selected tab.
- Builds a header row and a divider on top, and below it **only the selected tab's content** (other tabs are not built). Content stacks vertically and fills the remaining height (`.tab-body`).
- Pressing a header writes that tab's `value` (conversion and `onbeforecommit` as in [DvSelect](dvml-inputs.md#dvselect)). No tab matching the VM value → the first tab is shown.
- `value` omitted: the screen remembers the selection (starts at the first tab, kept across redraws; each `DvRepeat` item and component instance keeps its own). Bind `value` if code changes or reads the tab.
- A `DvTab` with an `id` takes a `DvControl` handle: `Enabled = false` disables its header and content; `Visible = false` removes the tab.
- Switching tabs does not keep the scroll position of the tab left. A value being edited is committed first when a header is pressed.
- One panel at a time without headers (a switching panel) has no widget — use a VM bool property + `visible`: `<DvBox visible="{IsTrend}">…</DvBox><DvBox visible="{IsAlarm}">…</DvBox>` with `public bool IsTrend => Mode == "trend";`.

**Example**
```xml
<DvTabs value="{Bind Tab}" grow="1">
  <DvTab value="trend" text="Trend"> … </DvTab>
  <DvTab value="alarm" text="Alarm"> … </DvTab>
  <DvTab value="setting" text="Settings" disabled="{NotAdmin}"> … </DvTab>
</DvTabs>
```

See also: [DvExpander](#dvexpander) · [DvSegment](dvml-inputs.md#dvsegment) · [pages](dvml-app.md#dvpage)

<a id="dvexpander"></a>
### DvExpander

A panel whose content expands and collapses when its header row is pressed (the WPF / WinUI Expander). Stack several to split a settings screen into sections.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `title` | text | empty | yes | The heading on the header row |
| `expanded` | bool | — | yes | Two-way `{Bind X}` follows the VM. One-way/literal locks it. Absent = the screen remembers (collapsed) |

**Behavior**
- Default size: header row 44 (+ content when expanded).
- Header row = right-arrow (collapsed) / down-arrow (expanded) icon + `title`, height 44. `title` is the group heading (like `DvCard`/`DvPageFrame` `title`), not a tooltip.
- The whole header row is the press target. Collapsed = only the header height; the content is not built (`id` handles in collapsed content have empty `Bounds`). Content stacks vertically (`.expander-body`).
- `expanded="{Bind X}"` (bool): the VM owns it; pressing flips and writes it. Omitted: the screen remembers it (initially collapsed, kept across redraws; each `DvRepeat` item and component instance separately). One-way or fixed (`expanded="true"`) locks it.
- From code: the `id` handle's `Expanded`/`ExpandedChanged` ([handles](dvml-code.md#handles)).
- Expanding and collapsing animate the height over 0.2 s (ease-out) and push the content below (also when changed by VM or handle; the first screen does not animate). Collapsing content stays until the end and is then removed.
- Looks: `.expander` (surface, border, rounded corners), `.expander-header`, `.expander-body`.
- Only one open at a time (accordion) has no widget: put `DvExpander` in `DvRepeat` and let the item's `Open` setter close the others.

**Example**
```xml
<DvExpander title="Advanced" expanded="{Bind ShowAdvanced}">
  <DvCheckBox text="Auto restart" value="{Bind AutoRestart}"/>
  <DvSlider value="{Bind Valve}" unit="%" show-value="true"/>
</DvExpander>
```

Accordion:
```xml
<DvRepeat items="{Sections}" gap="sm">
  <DvExpander title="{Item.Title}" expanded="{Bind Item.Open}"><DvLabel class="body" text="{Item.Body}"/></DvExpander>
</DvRepeat>
```
```csharp
public sealed class Section(string title, List<Section> all)
{
    private bool _open;
    public string Title { get; } = title;
    public string Body { get; init; } = "";
    public bool Open
    {
        get => _open;
        set { _open = value; if (value) foreach (var x in all.Where(x => x != this)) x._open = false; }
    }
}
```

See also: [DvRepeat](dvml-components.md#dvrepeat) · [DvTabs](#dvtabs)

<a id="dvstepbar"></a>
### DvStepBar

Shows the current step of a process or batch sequence. Display only; pressing does nothing.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `steps` | list or comma-separated text | none | yes | List binding: each item's `ToString()`. Text: leading/trailing spaces ignored |
| `current` | number | `0` | yes | Rounded. Counted from 1; `0` = not started; above the count = all done |

**Behavior**
- Default size: 96 width per step (min 200) × 64.
- `current` counts **from 1** (matches PLC step numbers). `0` = all waiting; greater than the count = all done. Earlier steps are done (check mark), later steps waiting.
- Look: an equal-width cell per step, a circle (diameter 28) + name below (ellipsis if longer than the cell). Done = `primary` fill + check; in progress = `primary` border + bold number and name; waiting = `border-strong` border + dimmed number and name. Lines between circles are `primary` only for passed segments.

**Example**
```xml
<DvStepBar steps="{BatchSteps}" current="{BatchStep}"/>
<DvStepBar steps="Wash, Rinse, Dry" current="2"/>
```

See also: [DvProgress](dvml-instruments.md#dvprogress)
